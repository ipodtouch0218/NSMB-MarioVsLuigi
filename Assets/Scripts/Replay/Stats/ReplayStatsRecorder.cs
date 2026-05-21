using Photon.Deterministic;
using Quantum;
using UnityEngine;
using NSMB.Utilities;
using System.Collections.Generic;

namespace NSMB.Replay.Stats {
    public class ReplayStatsRecorder : Singleton<ReplayStatsRecorder> {
        public BinaryReplayFile ReplayFile { get; private set; }
        public int ReplayStart => ReplayFile.Header.InitialFrameNumber;
        public int ReplayLength => ReplayFile.Header.ReplayLengthInFrames;
        public int ReplayEnd => ReplayStart + ReplayLength;
        public Dictionary<PlayerRef, PlayerInfo> PlayerInfos { get; private set; }
        public GlobalInfo GlobalInfo { get; private set; }
        private SessionRunner Runner;

        public void StartAnalyzing(BinaryReplayFile replayFile) {
            ReplayFile = replayFile;

            if (ReplayFile.LoadAllIfNeeded() != ReplayParseResult.Success) {
                return;
            }

            Init();
            TimePoint.ResetIndex();

            while (Runner.Session.FramePredicted == null || Runner.Session.FramePredicted.Number < ReplayEnd) {
                Runner.Service(1);
                //Console.WriteLine($"Simulating frame {Runner.Session.FramePredicted.Number - InitialFrameNumber} of {_maxFrame - InitialFrameNumber}");
            }

            Runner.Shutdown();
        }

        private void Init() {
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
            Runner = QuantumRunner.StartGame(arguments);

            // data tracking variables
            GlobalInfo = new GlobalInfo();
            PlayerInfos = new();
        }
    }
}
