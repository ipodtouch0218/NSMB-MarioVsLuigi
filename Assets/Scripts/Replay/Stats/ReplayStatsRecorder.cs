using NSMB.Addons;
using NSMB.Networking;
using NSMB.UI.MainMenu;
using NSMB.UI.MainMenu.Submenus.ReplayStats;
using NSMB.Utilities;
using Photon.Deterministic;
using Quantum;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace NSMB.Replay.Stats {
    public class ReplayStatsRecorder : Singleton<ReplayStatsRecorder> {
        public BinaryReplayFile ReplayFile { get; private set; }
        public int ReplayStart => ReplayFile.Header.InitialFrameNumber;
        public int ReplayLength => ReplayFile.Header.ReplayLengthInFrames;
        public int ReplayEnd => ReplayStart + ReplayLength;
        public Dictionary<PlayerRef, PlayerInfo> PlayerInfos { get; private set; }
        public GlobalInfo GlobalInfo { get; private set; }
        public bool IsGameValid => Runner != null;
        public bool IsSimulating => Runner == null || Runner.State == SessionRunner.SessionState.Running;
        private SessionRunner Runner;
        private CancellationTokenSource currentCancellationSource;

        private async Awaitable StartNewTaskSequence(Func<CancellationToken, Awaitable> asyncTask) {
            try {
                CancelExistingTask();

                var token = (currentCancellationSource = new()).Token;

                if (token.IsCancellationRequested) {
                    return;
                }

                await asyncTask(token);
            } catch {
                // Move exceptions to the main thread so they're printed.
                await Awaitable.MainThreadAsync();
                throw;
            }
        }

        private void CancelExistingTask() {
            if (currentCancellationSource != null) {
                currentCancellationSource.Cancel();
                currentCancellationSource.Dispose();
            }
            currentCancellationSource = null;
        }

        public bool MurderRunner() {
            CancelExistingTask();
            return Runner != null;
        }

        public async Awaitable StartAnalyzing(BinaryReplayFile replayFile, ReplayStatsManager statsManager) {
            await StartNewTaskSequence(async (cancellationToken) => {
                await StartAnalyzing(cancellationToken, replayFile, statsManager);
            });
        }

        private async Awaitable StartAnalyzing(CancellationToken cancellationToken, BinaryReplayFile replayFile, ReplayStatsManager statsManager) {
            if (cancellationToken.IsCancellationRequested) {
                return;
            }

            await Awaitable.MainThreadAsync();
            ReplayFile = replayFile;
            Runner = null;
            statsManager.UpdateProgressBar(0, ReplayEnd);

            if (ReplayFile.LoadAllIfNeeded() != ReplayParseResult.Success) {
                return;
            }

            if (GlobalController.Instance.addonManager.isActiveAndEnabled) {
                var loadAddonResult = await GlobalController.Instance.addonManager.LoadAllAddons(ReplayFile.Header.AddonGuids);
                if (loadAddonResult.Result == LoadAllAddonsResult.Success) {
                    await Init();
                } else if (loadAddonResult.Result == LoadAllAddonsResult.DownloadRequired) {
                    AddonManager.RequestDownloadAddons(loadAddonResult.RequiredDownloads, (result) => {
                        if (result == AddonManager.AddonDownloadResult.Success) {
                            _ = Init();
                        } else if (result == AddonManager.AddonDownloadResult.Cancelled) {
                            GlobalController.Instance.loadingCanvas.EndAnimation();
                        } else if (result == AddonManager.AddonDownloadResult.Failure) {
                            NetworkHandler.ThrowError("ui.error.replay.addons.downloadfailed", false);
                        }
                    });
                } else if (loadAddonResult.Result == LoadAllAddonsResult.Failure) {
                    NetworkHandler.ThrowError("ui.error.replay.addons.downloadfailed", false);
                    return;
                }
            } else {
                await Init();
            }
            TimePoint.ResetIndex();

            if (!IsGameValid) {
                return;
            }

            while ((Runner.Session.FramePredicted == null || Runner.Session.FramePredicted.Number < ReplayEnd) && !cancellationToken.IsCancellationRequested) {
                Runner.Service(1);
                statsManager.UpdateProgressBar(Runner.Session.FramePredicted.Number, ReplayEnd);
                await Task.Delay(1);
            }

            await Runner.ShutdownAsync();
            statsManager.Prepare();
        }

        private async Task Init() {
            // data tracking variables
            GlobalInfo = new GlobalInfo();
            PlayerInfos = new();

            var serializer = new QuantumUnityJsonSerializer();
            RuntimeConfig runtimeConfig = serializer.ConfigFromByteArray<RuntimeConfig>(ReplayFile.DecompressedRuntimeConfigData, compressed: false);
            var deterministicConfig = DeterministicSessionConfig.FromByteArray(ReplayFile.DecompressedDeterministicConfigData);
            var inputStream = new BitStream(ReplayFile.DecompressedInputData);
            var replayInputProvider = new BitStreamReplayInputProvider(inputStream, ReplayEnd);

            var eventDispatcher = new EventDispatcher();
            var callbackDispatcher = new CallbackDispatcher();
            var eventManager = new EventManager(this, eventDispatcher, callbackDispatcher);

            // Disable checksums- they murder performance.
            deterministicConfig.ChecksumInterval = 0;

            var arguments = new SessionRunner.Arguments {
                GameParameters = QuantumRunnerUnityFactory.CreateGameParameters,
                RuntimeConfig = runtimeConfig,
                SessionConfig = deterministicConfig,
                ReplayProvider = replayInputProvider,
                GameMode = DeterministicGameMode.Replay,
                RunnerId = "LOCALREPLAY",
                PlayerCount = deterministicConfig.PlayerCount,
                InitialTick = ReplayFile.Header.InitialFrameNumber,
                FrameData = ReplayFile.DecompressedInitialFrameData,
                DeltaTimeType = SimulationUpdateTime.EngineDeltaTime,

                EventDispatcher = eventDispatcher,
                CallbackDispatcher = callbackDispatcher,
            };

            ActiveReplayManager.Instance.ReplayFrameCache.Clear();
            ActiveReplayManager.Instance.ReplayFrameCache.Add(arguments.FrameData);

            try {
                Runner = await QuantumRunner.StartGameAsync(arguments);
            } catch {
                NetworkHandler.ThrowError("ui.error.replay.corrupt", false);
            }
        }
    }
}
