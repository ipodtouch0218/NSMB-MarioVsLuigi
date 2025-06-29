using NSMB.Networking;
using NSMB.UI.MainMenu.Submenus.Replays;
using NSMB.Utilities;
using Photon.Deterministic;
using Photon.Realtime;
using Quantum;
using Quantum.Prototypes;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace NSMB.Replay {
    public class ActiveReplayManager : Singleton<ActiveReplayManager> {

        //---Static
        public static event Action<ActiveReplayManager> OnReplayFastForwardEnded;

        //---Properties
        public BinaryReplayFile CurrentReplay { get; private set; }
        public bool IsReplay => CurrentReplay != null;
        public int ReplayStart => CurrentReplay?.Header.InitialFrameNumber ?? -1;
        public int ReplayLength => CurrentReplay?.Header.ReplayLengthInFrames ?? -1;
        public int ReplayEnd => ReplayStart + ReplayLength;
        public bool IsReplayFastForwarding {
            get => _isReplayFastForwarding;
            set {
                if (_isReplayFastForwarding && !value) {
                    OnReplayFastForwardEnded?.Invoke(this);
                }
                _isReplayFastForwarding = value;
            }
        }
        public string SavedRecordingPath { get; set; }

        //---Public Variables
        public readonly List<byte[]> ReplayFrameCache = new();

        //---Private Variables
        private bool _isReplayFastForwarding;
        private QuantumGame currentlyRecordingGame;
        private int initialFrame;
        private byte[] initialFrameData;


        public void Awake() {
            Set(this);
            QuantumCallback.Subscribe<CallbackSimulateFinished>(this, OnSimulateFinished);
            QuantumCallback.Subscribe<CallbackGameDestroyed>(this, OnGameDestroyed);
            QuantumCallback.Subscribe<CallbackGameResynced>(this, OnGameResynced);
            QuantumEvent.Subscribe<EventRecordingStarted>(this, OnRecordingStarted);
            QuantumEvent.Subscribe<EventGameEnded>(this, OnGameEnded);
            Settings.OnReplaysEnabledChanged += OnReplaysEnabledChanged;
        }

        public void OnDestroy() {
            Settings.OnReplaysEnabledChanged -= OnReplaysEnabledChanged;
        }

        public unsafe void SaveReplay(sbyte winner) {
//#if !UNITY_STANDALONE
//        return;
//#endif
            QuantumGame game = currentlyRecordingGame;

            if (currentlyRecordingGame == null || currentlyRecordingGame.RecordInputStream == null) {
                SavedRecordingPath = null;
                return;
            }

            if (IsReplay || game.RecordInputStream == null) {
                SavedRecordingPath = null;
                return;
            }

            if (!Settings.Instance.GeneralReplaysEnabled) {
                // Disabled replays mid-game
                DisposeReplay();
                SavedRecordingPath = null;
                return;
            }

            // Make room for this replay - delete old ones.
            var manager = ReplayListManager.Instance;
            if (manager) {
                var deletions = manager.GetTemporaryReplaysToDelete();
                if (deletions != null) {
                    foreach (var replay in deletions) {
                        Debug.Log($"[Replay] Automatically deleting temporary replay '{replay.ReplayFile.Header.GetDisplayName()}' ({replay.ReplayFile.FilePath}) to make room.");
                        File.Delete(replay.ReplayFile.FilePath);
                        manager.RemoveReplay(replay);
                    }
                }
            }

            // JSON-friendly replay
            QuantumReplayFile jsonReplay = game.GetRecordedReplay();
            jsonReplay.InitialTick = initialFrame;
            jsonReplay.InitialFrameData = initialFrameData;
            initialFrame = 0;
            initialFrameData = null;

            // Create directories and open file
            string replayFolder = Path.Combine(ReplayListManager.ReplayDirectory, "temp");
            Directory.CreateDirectory(replayFolder);

            // Find end-game data
            Frame f = game.Frames.Verified;
            var gamemode = f.FindAsset(f.Global->Rules.Gamemode);

            int players = f.Global->RealPlayers;
            ReplayPlayerInformation[] playerInformation = new ReplayPlayerInformation[players];

            for (int i = 0; i < players; i++) {
                ref PlayerInformation inGamePlayerInformation = ref f.Global->PlayerInfo[i];
                playerInformation[i].Nickname = inGamePlayerInformation.Nickname;
                playerInformation[i].Character = inGamePlayerInformation.Character;
                playerInformation[i].Team = inGamePlayerInformation.Team;
                playerInformation[i].PlayerRef = inGamePlayerInformation.PlayerRef;

                var filter = f.Filter<MarioPlayer>();
                filter.UseCulling = false;
                while (filter.NextUnsafe(out _, out MarioPlayer* mario)) {
                    if (mario->PlayerRef != playerInformation[i].PlayerRef) {
                        continue;
                    }

                    // Found him :)
                    if (mario->Lives > 0 || !f.Global->Rules.IsLivesEnabled) {
                        playerInformation[i].FinalObjectiveCount = gamemode.GetObjectiveCount(f, mario);
                    }
                    break;
                }
            }

            // Write binary replay
            string now = DateTimeOffset.Now.ToUnixTimeSeconds().ToString();
            string finalFilePath = Path.Combine(replayFolder, $"Replay-{now}.mvlreplay");
            int attempts = 0;
            FileStream outputStream = null;
            do {
                try {
                    outputStream = new FileStream(finalFilePath, FileMode.Create);
                } catch {
                    // Failed to create file; maybe they have two copies of the game open?
                    finalFilePath = Path.Combine(replayFolder, $"Replay-{now}-{++attempts}.mvlreplay");
                }
            } while (outputStream == null);

            ref GameRules rules = ref f.Global->Rules;
            BinaryReplayHeader header = new() {
                Version = BinaryReplayHeader.GetCurrentVersion(),
                UnixTimestamp = DateTimeOffset.Now.ToUnixTimeSeconds(),
                InitialFrameNumber = jsonReplay.InitialTick,
                ReplayLengthInFrames = jsonReplay.LastTick - jsonReplay.InitialTick,

                Rules = new GameRulesPrototype {
                    Stage = rules.Stage,
                    Gamemode = rules.Gamemode,
                    StarsToWin = rules.StarsToWin,
                    CoinsForPowerup = rules.CoinsForPowerup,
                    Lives = rules.Lives,
                    TimerMinutes = rules.TimerMinutes,
                    CustomPowerupsEnabled = rules.CustomPowerupsEnabled,
                    TeamsEnabled = rules.TeamsEnabled,
                },
                PlayerInformation = playerInformation,
                WinningTeam = winner,
            };

            BinaryReplayFile binaryReplay = BinaryReplayFile.FromReplayData(jsonReplay, header);
            long writtenBytes = binaryReplay.WriteToStream(outputStream);
            outputStream.Dispose();

            SavedRecordingPath = finalFilePath;

            // Complete
            Debug.Log($"[Replay] Saved new temporary replay '{finalFilePath}' ({Utils.BytesToString(writtenBytes)})");
            DisposeReplay();
        }

        private void DisposeReplay() {
            if (currentlyRecordingGame != null && currentlyRecordingGame.RecordInputStream != null) {
                currentlyRecordingGame.RecordInputStream.Dispose();
                currentlyRecordingGame.RecordInputStream = null;
            }
        }

        public unsafe void RecordReplay(QuantumGame game, Frame f) {
            if (!Settings.Instance.GeneralReplaysEnabled) {
                return;
            }

            game.StartRecordingInput(f.Number);
            initialFrameData = f.Serialize(DeterministicFrameSerializeMode.Serialize);
            initialFrame = f.Number;
            currentlyRecordingGame = game;

            Debug.Log("[Replay] Started recording a new replay.");
        }

        public async void StartReplay(BinaryReplayFile replay) {
            if (NetworkHandler.Client.IsConnected) {
                await NetworkHandler.Client.DisconnectAsync();
            }
            if (NetworkHandler.Runner && NetworkHandler.Runner.IsRunning) {
                await NetworkHandler.Runner.ShutdownAsync();
            }
            if (replay.LoadAllIfNeeded() != ReplayParseResult.Success) {
                return;
            }

            CurrentReplay = replay;

            var serializer = new QuantumUnityJsonSerializer();
            RuntimeConfig runtimeConfig;
            try {
                runtimeConfig = serializer.ConfigFromByteArray<RuntimeConfig>(replay.DecompressedRuntimeConfigData, compressed: false);
            } catch {
                // Bodge: support old 1.8 replays that double compressed.
                runtimeConfig = serializer.ConfigFromByteArray<RuntimeConfig>(replay.DecompressedRuntimeConfigData, compressed: true);
            }
            var deterministicConfig = DeterministicSessionConfig.FromByteArray(replay.DecompressedDeterministicConfigData);
            var inputStream = new BitStream(replay.DecompressedInputData);
            var replayInputProvider = new BitStreamReplayInputProvider(inputStream, ReplayEnd);

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
                InitialTick = ReplayStart,
                FrameData = replay.DecompressedInitialFrameData,
                DeltaTimeType = SimulationUpdateTime.EngineDeltaTime,
            };

            GlobalController.Instance.loadingCanvas.Initialize(null);
            ReplayFrameCache.Clear();
            ReplayFrameCache.Add(arguments.FrameData);
            
            NetworkHandler.Runner = await QuantumRunner.StartGameAsync(arguments);
        }

        private void OnSimulateFinished(CallbackSimulateFinished e) {
            if (!IsReplay) {
                return;
            }

            Frame f = e.Frame;
            if ((f.Number - ReplayStart) % (5 * f.UpdateRate) == 0) {
                // Save this frame to the replay cache
                int index = (f.Number - ReplayStart) / (5 * f.UpdateRate);
                if (ReplayFrameCache.Count <= index) {
                    ReplayFrameCache.Add(f.Serialize(DeterministicFrameSerializeMode.Serialize));
                }
            }
        }

        private void OnGameDestroyed(CallbackGameDestroyed e) {
            if (e.Game == currentlyRecordingGame) {
                SaveReplay(-1);
            }
            CurrentReplay = null;
        }

        private unsafe void OnGameResynced(CallbackGameResynced e) {
            if (IsReplay) {
                return;
            }

            Frame f = e.Game.Frames.Verified;
            if (f.Global->GameState == GameState.Playing) {
                RecordReplay(e.Game, f);
            }
        }

        private void OnRecordingStarted(EventRecordingStarted e) {
            RecordReplay(e.Game, e.Game.Frames.Verified);
        }

        private void OnGameEnded(EventGameEnded e) {
            if (e.Game == currentlyRecordingGame) {
                SaveReplay((sbyte) e.WinningTeam);
            }
        }

        private unsafe void OnReplaysEnabledChanged(bool enable) {
            var game = QuantumRunner.DefaultGame;
            if (game == null) {
                return;
            }

            Frame f = game.Frames.Predicted;
            if (enable) {
                if (f.Global->GameState >= GameState.Starting && f.Global->GameState < GameState.Ended) {
                    RecordReplay(game, f);
                }
            } else {
                // Disable
                DisposeReplay();
            }
        }
    }
}
