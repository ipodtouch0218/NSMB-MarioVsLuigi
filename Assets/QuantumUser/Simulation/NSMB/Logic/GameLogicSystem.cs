using Photon.Deterministic;
using System.Collections.Generic;
using UnityEngine;

namespace Quantum {
    public unsafe class GameLogicSystem : SystemMainThread, ISignalOnPlayerAdded, ISignalOnPlayerRemoved, ISignalOnMarioPlayerDied,
        ISignalOnLoadingComplete, ISignalOnMarioPlayerCollectedStar, ISignalOnReturnToRoom {

        public override void OnInit(Frame f) {
            var config = f.RuntimeConfig;
            f.Global->Rules = f.SimulationConfig.DefaultRules;
            if (!config.IsRealGame) {
                f.Global->GameState = GameState.WaitingForPlayers;
            }
        }

        public override void Update(Frame f) {
            // Ping command is always accepted
            for (int i = 0; i < f.PlayerCount; i++) {
                var playerData = QuantumUtils.GetPlayerData(f, i);
                if (playerData != null) {
                    switch (f.GetPlayerCommand(i)) {
                    case CommandUpdatePing updatePing:
                        playerData->Ping = updatePing.PingMs;
                        f.Events.PlayerDataChanged(f, i);
                        break;
                    case CommandSetInSettings setInSettings:
                        playerData->IsInSettings = setInSettings.InSettings;
                        f.Events.PlayerDataChanged(f, i);
                        break;
                    }
                }
            }

            switch (f.Global->GameState) {
            case GameState.PreGameRoom:
                for (int i = 0; i < f.PlayerCount; i++) {
                    var playerData = QuantumUtils.GetPlayerData(f, i);

                    switch (f.GetPlayerCommand(i)) {
                    case CommandChangePlayerData changeData:
                        CommandChangePlayerData.Changes playerChanges = changeData.EnabledChanges;

                        if (playerChanges.HasFlag(CommandChangePlayerData.Changes.Character)) {
                            playerData->Character = changeData.Character;
                        }
                        if (playerChanges.HasFlag(CommandChangePlayerData.Changes.Skin)) {
                            playerData->Skin = changeData.Skin;
                        }
                        if (playerChanges.HasFlag(CommandChangePlayerData.Changes.Team)) {
                            playerData->Team = changeData.Team;
                        }
                        if (playerChanges.HasFlag(CommandChangePlayerData.Changes.Spectating)) {
                            playerData->IsSpectator = changeData.Spectating;
                        }

                        f.Events.PlayerDataChanged(f, playerData->PlayerRef);
                        break;
                    case CommandStartTyping:
                        f.Events.PlayerStartedTyping(f, i);
                        break;
                    case CommandSendChatMessage chatMessage:
                        if (!playerData->CanSendChatMessage(f)) {
                            break;
                        }
                        playerData->LastChatMessage = f.Number;
                        f.Events.PlayerSentChatMessage(f, i, chatMessage.Message);
                        break;
                    case CommandToggleReady:
                        playerData->IsReady = !playerData->IsReady;
                        f.Events.PlayerDataChanged(f, i);
                        break;
                    case CommandToggleCountdown:
                        if (!playerData->IsRoomHost) {
                            // Only the host can start the countdown.
                            break;
                        }
                        bool gameStarting = f.Global->GameStartFrames == 0;
                        f.Global->GameStartFrames = (ushort) (gameStarting ? 3 * 60 : 0);
                        f.Events.StartingCountdownChanged(f, gameStarting);
                        break;
                    case CommandChangeHost changeHost:
                        if (!playerData->IsRoomHost) {
                            // Only the host can give it to another player.
                            break;
                        }
                        var newHostPlayerData = QuantumUtils.GetPlayerData(f, changeHost.NewHost);
                        if (newHostPlayerData == null) {
                            return;
                        }

                        playerData->IsRoomHost = false;
                        newHostPlayerData->IsRoomHost = true;
                        f.Events.HostChanged(f, changeHost.NewHost);
                        break;
                    case CommandChangeRules changeRules:
                        if (!playerData->IsRoomHost) {
                            // Only the host can change rules.
                            break;
                        }

                        CommandChangeRules.Changes rulesChanges = changeRules.EnabledChanges;
                        var rules = f.Global->Rules;
                        bool levelChanged = false;

                        if (rulesChanges.HasFlag(CommandChangeRules.Changes.Level)) {
                            levelChanged = rules.Level != changeRules.Level;
                            rules.Level = changeRules.Level;
                        }
                        if (rulesChanges.HasFlag(CommandChangeRules.Changes.StarsToWin)) {
                            rules.StarsToWin = changeRules.StarsToWin;
                        }
                        if (rulesChanges.HasFlag(CommandChangeRules.Changes.CoinsForPowerup)) {
                            rules.CoinsForPowerup = changeRules.CoinsForPowerup;
                        }
                        if (rulesChanges.HasFlag(CommandChangeRules.Changes.Lives)) {
                            rules.Lives = changeRules.Lives;
                        }
                        if (rulesChanges.HasFlag(CommandChangeRules.Changes.TimerSeconds)) {
                            rules.TimerSeconds = changeRules.TimerSeconds;
                        }
                        if (rulesChanges.HasFlag(CommandChangeRules.Changes.TeamsEnabled)) {
                            rules.TeamsEnabled = changeRules.TeamsEnabled;
                        }
                        if (rulesChanges.HasFlag(CommandChangeRules.Changes.CustomPowerupsEnabled)) {
                            rules.CustomPowerupsEnabled = changeRules.CustomPowerupsEnabled;
                        }
                        if (rulesChanges.HasFlag(CommandChangeRules.Changes.DrawOnTimeUp)) {
                            rules.DrawOnTimeUp = changeRules.DrawOnTimeUp;
                        }

                        f.Global->Rules = rules;
                        f.Events.RulesChanged(f, levelChanged);
                        break;
                    }
                }

                if (f.Global->GameStartFrames > 0) {
                    if (QuantumUtils.Decrement(ref f.Global->GameStartFrames)) {
                        // Start the game!
                        if (f.IsVerified) {
                            f.MapAssetRef = f.Global->Rules.Level;
                        }
                        f.Global->PlayerLoadFrames = (ushort) (25 * f.UpdateRate);
                        f.Global->GameState = GameState.WaitingForPlayers;
                        f.Events.GameStateChanged(f, GameState.WaitingForPlayers);
                    } else if (f.Global->GameStartFrames % 60 == 0) {
                        f.Events.CountdownTick(f, f.Global->GameStartFrames / 60);
                    }
                }

                break;
            case GameState.WaitingForPlayers:
                for (int i = 0; i < f.PlayerCount; i++) {
                    var playerData = QuantumUtils.GetPlayerData(f, i);
                    if (f.GetPlayerCommand(i) is CommandPlayerLoaded) {
                        bool wasLoaded = playerData->IsLoaded;
                        playerData->IsLoaded = true;
                        
                        if (!wasLoaded) {
                            f.Events.PlayerLoaded(f, i);
                        }
                    }
                }
                
                bool allPlayersLoaded = true;
                var playerDataFilter = f.Filter<PlayerData>();
                byte players = 0;
                while (playerDataFilter.NextUnsafe(out _, out PlayerData* data)) {
                    allPlayersLoaded &= data->IsSpectator || data->IsLoaded;
                    if (!data->IsSpectator) {
                        players++;
                    }
                }
                f.Global->RealPlayers = players;

                if (players <= 0) {
                    break;
                }

                if (!f.RuntimeConfig.IsRealGame || allPlayersLoaded) {
                    // Progress to next stage.
                    f.Global->GameState = GameState.Starting;
                    f.Global->GameStartFrames = 3 * 60 + 78;
                    f.Global->Timer = f.Global->Rules.TimerSeconds;

                    f.Signals.OnLoadingComplete();
                    f.Events.GameStateChanged(f, GameState.Starting);
                } else {
                    // TODO Time out if players don't send a "ready" command in time
                }
                break;
            case GameState.Starting:
                if (QuantumUtils.Decrement(ref f.Global->GameStartFrames)) {
                    // Now playing
                    f.Global->GameState = GameState.Playing;
                    f.Events.GameStateChanged(f, GameState.Playing);
                    f.Global->StartFrame = f.Number;

                } else if (f.Global->GameStartFrames == 78) {
                    // Respawn all players and enable systems
                    f.SystemEnable<GameplaySystemGroup>();
                    f.Signals.OnGameStarting();
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
                    f.SystemEnable<GameplaySystemGroup>();
                    if (f.IsVerified) {
                        f.Map = null;
                    }
                    f.Signals.OnReturnToRoom();
                    f.Global->GameState = GameState.PreGameRoom;
                    f.Events.GameStateChanged(f, GameState.PreGameRoom);
                    f.SystemDisable<GameplaySystemGroup>();
                }
                break;
            }
        }

        public static void CheckForGameEnd(Frame f) {
            
            // End Condition: only one team alive
            var marioFilter = f.Filter<MarioPlayer>();
            bool livesGame = f.Global->Rules.IsLivesEnabled;

            bool oneOrNoTeamAlive = true;
            int aliveTeam = -1;
            while (marioFilter.NextUnsafe(out _, out MarioPlayer* mario)) {
                if (livesGame && mario->Lives <= 0) {
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
            f.Signals.OnGameEnding(winningTeam.GetValueOrDefault(), winningTeam.HasValue);
            f.Events.GameEnded(f, winningTeam.GetValueOrDefault(), winningTeam.HasValue);

            f.Global->GameState = GameState.Ended;
            f.Events.GameStateChanged(f, GameState.Ended);
            f.Global->GameStartFrames = (ushort) (6 * f.UpdateRate);
            f.SystemDisable<GameplaySystemGroup>();
        }

        public void OnMarioPlayerDied(Frame f, EntityRef entity) {
            CheckForGameEnd(f);
        }

        public void OnPlayerAdded(Frame f, PlayerRef player, bool firstTime) {
            EntityRef newEntity = f.Create();
            f.Add(newEntity, out PlayerData* newData);
            newData->PlayerRef = player;
            newData->JoinTick = f.Number;
            
            var datas = f.ResolveDictionary(f.Global->PlayerDatas);
            if (datas.Count == 0) {
                // First player is host
                newData->IsRoomHost = true;
                f.Events.HostChanged(f, player);
            }

            datas[player] = newEntity;
            f.Events.PlayerAdded(f, player);
        }

        public void OnPlayerRemoved(Frame f, PlayerRef player) {
            var datas = f.ResolveDictionary(f.Global->PlayerDatas);

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
                }

                f.Destroy(entity);
                datas.Remove(player);
            }

            f.Events.PlayerRemoved(f, player);
        }

        public void OnLoadingComplete(Frame f) {
            // Spawn players
            var config = f.SimulationConfig;
            var stage = f.FindAsset<VersusStageData>(f.Map.UserAsset);
            var playerDatas = f.Filter<PlayerData>();
            int teamCounter = 0;
            while (playerDatas.NextUnsafe(out _, out PlayerData* data)) {
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
        }
    }
}