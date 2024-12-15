using Photon.Deterministic;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Quantum {
    public unsafe class GameLogicSystem : SystemMainThread, ISignalOnPlayerAdded, ISignalOnPlayerRemoved, ISignalOnMarioPlayerDied,
        ISignalOnLoadingComplete, ISignalOnMarioPlayerCollectedStar, ISignalOnReturnToRoom {

        public override void OnInit(Frame f) {
            var config = f.RuntimeConfig;
            f.Global->Rules = f.SimulationConfig.DefaultRules;

            // Support booting in the editor.
            if (!config.IsRealGame) {
                f.Global->GameState = GameState.WaitingForPlayers;
                f.Global->PlayerLoadFrames = (ushort) (20 * f.UpdateRate);
            }
        }

        public override void Update(Frame f) {
            // Parse lobby commands
            var playerDataDictionary = f.ResolveDictionary(f.Global->PlayerDatas);
            for (int i = 0; i < f.PlayerCount; i++) {
                if (f.GetPlayerCommand(i) is ILobbyCommand lobbyCommand) {
                    var playerData = QuantumUtils.GetPlayerData(f, i, playerDataDictionary);
                    if (playerData == null) {
                        continue;
                    }

                    lobbyCommand.Execute(f, i, playerData);
                }
            }

            // Gaem state logic
            switch (f.Global->GameState) {
            case GameState.PreGameRoom:
                if (f.Global->GameStartFrames > 0) {
                    if (QuantumUtils.Decrement(ref f.Global->GameStartFrames)) {
                        // Start the game!
                        if (f.IsVerified) {
                            f.MapAssetRef = f.Global->Rules.Level;
                        }
                        f.Global->PlayerLoadFrames = (ushort) (20 * f.UpdateRate);
                        f.Global->GameState = GameState.WaitingForPlayers;

                        f.Events.GameStateChanged(f, GameState.WaitingForPlayers);
                    } else if (f.Global->GameStartFrames % 60 == 0) {
                        f.Events.CountdownTick(f, f.Global->GameStartFrames / 60);
                    }
                }
                break;
            case GameState.WaitingForPlayers:
                bool allPlayersLoaded = true;
                var playerDataFilter = f.Filter<PlayerData>();
                byte players = 0;
                while (playerDataFilter.NextUnsafe(out _, out PlayerData* data)) {
                    if (!f.RuntimeConfig.IsRealGame) {
                        data->IsLoaded = true;
                        data->IsSpectator = false;
                    }

                    allPlayersLoaded &= data->IsSpectator || data->IsLoaded;
                    if (!data->IsSpectator) {
                        players++;
                    }
                }
                f.Global->RealPlayers = players;

                if (players <= 0) {
                    break;
                }

                if (QuantumUtils.Decrement(ref f.Global->PlayerLoadFrames) || !f.RuntimeConfig.IsRealGame || allPlayersLoaded) {
                    // Progress to next stage.
                    f.Global->GameState = GameState.Starting;
                    f.Global->GameStartFrames = 3 * 60 + 120 + 60;
                    f.Global->Timer = f.Global->Rules.TimerSeconds;

                    f.Signals.OnLoadingComplete();
                    f.Events.GameStateChanged(f, GameState.Starting);
                }
                break;
            case GameState.Starting:
                if (QuantumUtils.Decrement(ref f.Global->GameStartFrames)) {
                    // Now playing
                    f.Global->GameState = GameState.Playing;
                    f.Events.GameStateChanged(f, GameState.Playing);
                    f.Global->StartFrame = f.Number;

                } else if (f.Global->GameStartFrames == 79) {
                    f.Events.RecordingStarted(f);

                } if (f.Global->GameStartFrames == 78) {
                    // Respawn all players and enable systems
                    f.SystemEnable<StartDisabledSystemGroup>();
                    f.Signals.OnGameStarting();
                    f.Events.GameStarted(f);
                }
                break;

            case GameState.Playing:
                if (f.Global->Rules.TimerSeconds > 0 && f.Global->Timer > 0) {
                    if ((f.Global->Timer -= f.DeltaTime) <= 0) {
                        f.Global->Timer = 0;
                        CheckForGameEnd(f);
                        f.Events.TimerExpired(f);
                    }
                }

                PlayerRef host = QuantumUtils.GetHostPlayer(f, out _);
                if (f.GetPlayerCommand(host) is CommandHostEndGame) {
                    EndGame(f, null);
                }
                break;

            case GameState.Ended:
                if (QuantumUtils.Decrement(ref f.Global->GameStartFrames)) {
                    // Move back to lobby.
                    f.Global->TotalGamesPlayed++;
                    if (f.IsVerified) {
                        //f.MapAssetRef = f.SimulationConfig.LobbyMap;
                        f.Map = null;
                    }
                    f.SystemEnable<StartDisabledSystemGroup>();
                    f.Signals.OnReturnToRoom();
                    f.Global->GameState = GameState.PreGameRoom;
                    f.Events.GameStateChanged(f, GameState.PreGameRoom);
                    f.SystemDisable<StartDisabledSystemGroup>();
                }
                break;
            }
        }

        public static void StopCountdown(Frame f) {
            f.Global->GameStartFrames = 0;
            f.Events.StartingCountdownChanged(f, false);
        }

        public static void CheckForGameEnd(Frame f) {
            // End Condition: only one team alive
            var marioFilter = f.Filter<MarioPlayer>();
            marioFilter.UseCulling = false;

            bool livesGame = f.Global->Rules.IsLivesEnabled;
            bool oneOrNoTeamAlive = true;
            int aliveTeam = -1;
            while (marioFilter.NextUnsafe(out _, out MarioPlayer* mario)) {
                if ((livesGame && mario->Lives <= 0) || mario->Disconnected) {
                    continue;
                }

                if (aliveTeam == -1) {
                    aliveTeam = mario->Team;
                } else {
                    oneOrNoTeamAlive = false;
                    break;
                }
            }

            if (oneOrNoTeamAlive) {
                if (aliveTeam == -1) {
                    // It's a draw
                    EndGame(f, null);
                    return;
                } else if (f.Global->RealPlayers > 1) {
                    // <team> wins, assuming more than 1 player
                    // so the player doesn't insta-win in a solo game.
                    EndGame(f, aliveTeam);
                    return;
                }
            }

            int? winningTeam = QuantumUtils.GetWinningTeam(f, out int stars);

            // End Condition: team gets to enough stars
            if (winningTeam != null && stars >= f.Global->Rules.StarsToWin) {
                // <team> wins
                EndGame(f, winningTeam.Value);
                return;
            }

            // End Condition: timer expires
            if (f.Global->Rules.IsTimerEnabled && f.Global->Timer <= 0) {
                if (f.Global->Rules.DrawOnTimeUp) {
                    // It's a draw
                    EndGame(f, null);
                    return;
                }

                // Check if one team is winning
                if (winningTeam != null) {
                    // <team> wins
                    EndGame(f, winningTeam.Value);
                    return;
                }
            }
        }

        public static void EndGame(Frame f, int? winningTeam) {
            if (f.Global->GameState != GameState.Playing) {
                return;
            }

            f.Signals.OnGameEnding(winningTeam.GetValueOrDefault(), winningTeam.HasValue);
            f.Events.GameEnded(f, winningTeam.GetValueOrDefault(), winningTeam.HasValue);

            var playerDatas = f.Filter<PlayerData>();
            playerDatas.UseCulling = false;
            while (playerDatas.NextUnsafe(out _, out PlayerData* data)) {
                if (winningTeam == data->Team && !data->IsSpectator) {
                    data->Wins++;
                }
                data->IsSpectator = data->ManualSpectator;
            }

            f.Global->GameState = GameState.Ended;
            f.Events.GameStateChanged(f, GameState.Ended);
            f.Global->GameStartFrames = (ushort) (6 * f.UpdateRate);
            f.SystemDisable<StartDisabledSystemGroup>();
        }

        public void OnMarioPlayerDied(Frame f, EntityRef entity) {
            CheckForGameEnd(f);
        }

        public void OnPlayerAdded(Frame f, PlayerRef player, bool firstTime) {
            EntityRef newEntity = f.Create();
            f.Add(newEntity, out PlayerData* newData);
            newData->PlayerRef = player;
            newData->JoinTick = f.Number;
            newData->IsSpectator = f.Global->GameState != GameState.PreGameRoom;

            RuntimePlayer runtimePlayer = f.GetPlayerData(player);
            newData->Character = runtimePlayer.Character;
            newData->Palette = runtimePlayer.Palette;

            var datas = f.ResolveDictionary(f.Global->PlayerDatas);
            if (datas.Count == 0) {
                // First player is host
                newData->IsRoomHost = true;
                f.Events.HostChanged(f, player);
            }

            datas[player] = newEntity;
            f.Events.PlayerAdded(f, player);
            f.Events.PlayerDataChanged(f, player);
        }

        public void OnPlayerRemoved(Frame f, PlayerRef player) {
            var datas = f.ResolveDictionary(f.Global->PlayerDatas);
            bool hostChanged = false;

            if (datas.TryGetValue(player, out EntityRef entity)) {
                var deletedPlayerData = f.Unsafe.GetPointer<PlayerData>(entity);

                if (deletedPlayerData->IsRoomHost) {
                    // Give the host to the youngest player.
                    var playerDataFilter = f.Filter<PlayerData>();
                    PlayerData* youngestPlayer = null;
                    while (playerDataFilter.NextUnsafe(out _, out PlayerData* otherPlayerData)) {
                        if (deletedPlayerData == otherPlayerData) {
                            continue;
                        }
                        
                        if (youngestPlayer == null || otherPlayerData->JoinTick < youngestPlayer->JoinTick) {
                            youngestPlayer = otherPlayerData;
                        }
                    }

                    if (youngestPlayer != null) {
                        youngestPlayer->IsRoomHost = true;
                        f.Events.HostChanged(f, youngestPlayer->PlayerRef);
                    }

                    hostChanged = true;
                }

                f.Destroy(entity);
                datas.Remove(player);
            }

            f.Events.PlayerRemoved(f, player);

            if (f.Global->GameStartFrames > 0 && (hostChanged || !QuantumUtils.IsGameStartable(f))) {
                StopCountdown(f);
            }
        }

        public void OnLoadingComplete(Frame f) {
            // Spawn players
            var config = f.SimulationConfig;
            var stage = f.FindAsset<VersusStageData>(f.Map.UserAsset);
            int teamCounter = 0;

            // Debug: give existing mario players the same team
            var sceneMarios = f.Filter<MarioPlayer>();
            while (sceneMarios.NextUnsafe(out _, out MarioPlayer* mario)) {
                mario->Team = 255;
            }

            var playerDatas = f.Filter<PlayerData>();
            while (playerDatas.NextUnsafe(out _, out PlayerData* data)) {
                if (!data->IsLoaded) {
                    // Force spectator, didn't load in time
                    data->IsSpectator = true;
                    continue;
                }

                if (data->IsSpectator) {
                    continue;
                }

                int characterIndex = FPMath.Clamp(data->Character, 0, config.CharacterDatas.Length - 1);
                CharacterAsset character = config.CharacterDatas[characterIndex];

                EntityRef newPlayer = f.Create(character.Prototype);
                var mario = f.Unsafe.GetPointer<MarioPlayer>(newPlayer);
                mario->PlayerRef = data->PlayerRef;
                mario->Team = (byte) (f.Global->Rules.TeamsEnabled ? data->Team : teamCounter++);

                var newTransform = f.Unsafe.GetPointer<Transform2D>(newPlayer);
                newTransform->Position = stage.Spawnpoint;
            }

            // Assign random spawnpoints
            f.Global->TotalMarios = (byte) f.ComponentCount<MarioPlayer>();
            List<int> spawnpoints = Enumerable.Range(0, f.ComponentCount<MarioPlayer>()).ToList();
            var allMarios = f.Filter<MarioPlayer>();
            while (allMarios.NextUnsafe(out EntityRef entity, out MarioPlayer* mario)) {
                int randomIndex = FPMath.FloorToInt(f.RNG->Next() * spawnpoints.Count);
                mario->SpawnpointIndex = (byte) spawnpoints[randomIndex];
                spawnpoints.RemoveAt(randomIndex);

                var camera = f.Unsafe.GetPointer<CameraController>(entity);
                camera->Recenter(stage, stage.GetWorldSpawnpointForPlayer(mario->SpawnpointIndex, f.Global->TotalMarios));
            }
        }

        public void OnMarioPlayerCollectedStar(Frame f, EntityRef entity) {
            CheckForGameEnd(f);
        }

        public void OnReturnToRoom(Frame f) {
            // Destroy all entities except PlayerDatas
            List<EntityRef> entities = new();
            f.GetAllEntityRefs(entities);

            foreach (var entity in entities) {
                if (f.Has<PlayerData>(entity)) {
                    continue;
                }

                f.Destroy(entity);
            }

            // Reset variables
            f.Global->Timer = 0;

            var playerDatas = f.Filter<PlayerData>();
            while (playerDatas.NextUnsafe(out _, out PlayerData* playerData)) {
                playerData->IsLoaded = false;
                playerData->IsReady = false;
            }
        }
    }
}