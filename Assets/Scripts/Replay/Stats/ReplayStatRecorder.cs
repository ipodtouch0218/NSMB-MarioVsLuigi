using Photon.Deterministic;
using Quantum;
using UnityEngine;
using System.Collections.Generic;

namespace NSMB.Replay.Stats {
    public class ReplayStatRecorder {
        public readonly BinaryReplayFile ReplayFile;
        public int ReplayStart => ReplayFile.Header.InitialFrameNumber;
        public int ReplayLength => ReplayFile.Header.ReplayLengthInFrames;
        public int ReplayEnd => ReplayStart + ReplayLength;
        public readonly Dictionary<PlayerRef, PlayerInfo> PlayerInfos = new();
        public GlobalInfo GlobalInfo { get; private set; }
        private SessionRunner Runner;

        public ReplayStatRecorder(BinaryReplayFile replayFile) {
            ReplayFile = replayFile;
        }

        public void Start() {
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

            Runner = QuantumRunner.StartGame(arguments);
            GlobalInfo = new GlobalInfo();
        }
    }
}
