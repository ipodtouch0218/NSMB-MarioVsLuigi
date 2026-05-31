using Photon.Deterministic;
using Quantum;
using UnityEngine;
using NSMB.Utilities;
using System.Collections.Generic;
using NSMB.Networking;
using System.Threading.Tasks;
using NSMB.Addons;
using NSMB.UI.MainMenu.Submenus.ReplayStats;
using NSMB.UI.MainMenu;
using System.Threading;

namespace NSMB.Replay.Stats {
    public class ReplayStatsRecorder : Singleton<ReplayStatsRecorder> {
        public BinaryReplayFile ReplayFile { get; private set; }
        public int ReplayStart => ReplayFile.Header.InitialFrameNumber;
        public int ReplayLength => ReplayFile.Header.ReplayLengthInFrames;
        public int ReplayEnd => ReplayStart + ReplayLength;
        public Dictionary<PlayerRef, PlayerInfo> PlayerInfos { get; private set; }
        public GlobalInfo GlobalInfo { get; private set; }
        public bool IsGameValid => Runner != null;
        private SessionRunner Runner;

        public bool TerminateReplayRunner() {
            if (Runner != null) {
                Runner.ShutdownAsync();
                return true;
            }
            return false;
        }

        public async Awaitable StartAnalyzing(BinaryReplayFile replayFile, ReplayStatsManager statsManager) {
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

            while (Runner.Session.FramePredicted == null || Runner.Session.FramePredicted.Number < ReplayEnd) {
                Runner.Service(1);
                statsManager.UpdateProgressBar(Runner.Session.FramePredicted.Number, ReplayEnd);
                await Task.Delay(1);
            }

            await Runner.ShutdownAsync();
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
