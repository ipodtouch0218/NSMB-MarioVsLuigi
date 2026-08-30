using NSMB.Addons;
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
using System.Linq;
using System.Threading.Tasks;
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

        public void StartRecordingReplay(QuantumGame game) {
            if (!Settings.Instance.GeneralReplaysEnabled) {
                return;
            }

            Frame f = game.Frames.Verified;
            game.StartRecordingInput(f.Number);
            initialFrameData = f.Serialize(DeterministicFrameSerializeMode.Serialize);
            initialFrame = f.Number;
            currentlyRecordingGame = game;

            Debug.Log("[Replay] Started recording a new replay.");
        }

        public unsafe void SaveReplay(sbyte winner) {
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
                var deletions = ReplayListManager.GetTemporaryReplaysToDelete();
                foreach (var replayPath in deletions) {
                    Debug.Log($"[Replay] Automatically deleting temporary replay '{replayPath}'.");
                    manager.RemoveReplayByPath(replayPath);
                    File.Delete(replayPath);
                }
            }

            // JSON-friendly replay
            QuantumReplayFile jsonReplay = game.GetRecordedReplay();
            jsonReplay.InitialTick = initialFrame;
            jsonReplay.InitialFrameData = initialFrameData;
            initialFrame = 0;
            initialFrameData = null;

            // Create directories and open file
            Directory.CreateDirectory(ReplayListManager.TempDirectory);

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
                playerInformation[i].FinalObjectiveCount = gamemode.GetObjectiveCount(f, inGamePlayerInformation.PlayerRef);
            }

            // Write binary replay
            string now = DateTimeOffset.Now.ToUnixTimeSeconds().ToString();

            string finalFilePath = Path.Combine(ReplayListManager.TempDirectory, $"Replay-{now}.mvlreplay");
            Stream outputStream = null;
            long writtenBytes;
            try {
                ref GameRules rules = ref f.Global->Rules;
                var gamemodeSpecific = f.FindAsset(rules.Gamemode);

                DictionaryEntry_AssetRefCoinItemAsset_FP[] customSpawnWeights;
                if (f.TryResolveDictionary(rules.CoinItemCustomSpawnWeights, out var customWeights)) {
                    customSpawnWeights = new DictionaryEntry_AssetRefCoinItemAsset_FP[customWeights.Count];
                    int count = 0;
                    foreach ((var key, var value) in customWeights) {
                        customSpawnWeights[count++] = new DictionaryEntry_AssetRefCoinItemAsset_FP {
                            Key = key,
                            Value = value
                        };
                    }
                } else {
                    customSpawnWeights = null;
                }

                BinaryReplayHeader header = new() {
                    Version = GameVersion.Current,
                    UnixTimestamp = DateTimeOffset.Now.ToUnixTimeSeconds(),
                    InitialFrameNumber = jsonReplay.InitialTick,
                    ReplayLengthInFrames = jsonReplay.LastTick - jsonReplay.InitialTick,

                    Rules = new GameRulesPrototype {
                        Stage = f.MapAssetRef,
                        Gamemode = rules.Gamemode,
                        StarsToWin = rules.StarsToWin,
                        CoinsForPowerup = rules.CoinsForPowerup,
                        Lives = rules.Lives,
                        TimerMinutes = rules.TimerMinutes,
                        CustomPowerupsEnabled = rules.CustomPowerupsEnabled,
                        TeamsEnabled = rules.TeamsEnabled,
                        StarFountain = rules.StarFountain,
                        CoinDeathPenalty = rules.CoinDeathPenalty,
                        TeamAttack = rules.TeamAttack,
                        CoinItemCustomSpawnWeights = customSpawnWeights,
                    },
                    PlayerInformation = playerInformation,
                    WinningTeam = winner,
                    AddonGuids = GlobalController.Instance.addonManager.LoadedAddons
                        .Select(la => la.Definition.ReleaseGuid)
                        .ToList()
                };

                BinaryReplayFile binaryReplay = BinaryReplayFile.FromReplayData(jsonReplay, header);

#if !UNITY_WEBGL
                // Write to file
                int attempts = 0;
                do {
                    try {
                        outputStream = new FileStream(finalFilePath, FileMode.Create);
                    } catch {
                        // Failed to create file; maybe they have two copies of the game open?
                        finalFilePath = Path.Combine(ReplayListManager.TempDirectory, $"Replay-{now}-{++attempts}.mvlreplay");
                    }
                } while (outputStream == null && attempts < 5);

                writtenBytes = binaryReplay.WriteToStream(outputStream);
                binaryReplay.FilePath = finalFilePath;
#else
                outputStream = new DummyStream();
                writtenBytes = binaryReplay.WriteToStream(outputStream);
#endif

                // Register replay file immediately, because WebGL can't load replays from the filesystem.
                if (ReplayListManager.Instance) {
                    ReplayListManager.Instance.AddReplay(binaryReplay);
                }
            } finally {
                outputStream?.Dispose();
            }

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

        public async void StartReplayPlayback(BinaryReplayFile replay) {
            if (replay.LoadAllIfNeeded() != ReplayParseResult.Success) {
                return;
            }

            GlobalController.Instance.loadingCanvas.dontHideOnGameDestroy = true;
            GlobalController.Instance.loadingCanvas.Initialize(null);

            if (NetworkHandler.Runner && NetworkHandler.Runner.IsRunning) {
                await NetworkHandler.Runner.ShutdownAsync();
            }
            if (NetworkHandler.Client.IsConnected) {
                await NetworkHandler.Client.DisconnectAsync();
            }

            if (GlobalController.Instance.addonManager.isActiveAndEnabled) {
                var loadAddonResult = await GlobalController.Instance.addonManager.LoadAllAddons(replay.Header.AddonGuids);
                if (loadAddonResult.Result == LoadAllAddonsResult.Success) {
                    _ = StartReplay(replay);
                } else if (loadAddonResult.Result == LoadAllAddonsResult.DownloadRequired) {
                    AddonManager.RequestDownloadAddons(loadAddonResult.RequiredDownloads, (result) => {
                        if (result == AddonManager.AddonDownloadResult.Success) {
                            _ = StartReplay(replay);
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
                _ = StartReplay(replay);
            }
        }

        private async Task StartReplay(BinaryReplayFile replay) {
            CurrentReplay = replay;

            var serializer = new QuantumUnityJsonSerializer();
            RuntimeConfig runtimeConfig = serializer.ConfigFromByteArray<RuntimeConfig>(replay.DecompressedRuntimeConfigData, compressed: false);
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

            ReplayFrameCache.Clear();
            ReplayFrameCache.Add(arguments.FrameData);

            try {
                NetworkHandler.Runner = await QuantumRunner.StartGameAsync(arguments);
            } catch {
                NetworkHandler.ThrowError("ui.error.replay.corrupt", false);
            }
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
                StartRecordingReplay(e.Game);
            }
        }

        private void OnRecordingStarted(EventRecordingStarted e) {
            StartRecordingReplay(e.Game);
        }

        private void OnGameEnded(EventGameEnded e) {
            if (e.Game == currentlyRecordingGame) {
                SaveReplay((sbyte) e.WinningTeam);
            }
        }

        private unsafe void OnReplaysEnabledChanged(bool enable) {
            QuantumGame game = QuantumRunner.DefaultGame;
            if (game == null) {
                return;
            }

            Frame f = game.Frames.Verified;
            if (enable) {
                if (f.Global->GameState >= GameState.Starting && f.Global->GameState < GameState.Ended) {
                    StartRecordingReplay(game);
                }
            } else {
                // Disable
                DisposeReplay();
            }
        }
    }
}
