using Photon.Deterministic;
using Quantum.Profiling;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Quantum {
    public unsafe class GameLogicSystem : SystemMainThread, ISignalOnPlayerAdded, ISignalOnPlayerRemoved, ISignalOnMarioPlayerDied,
        ISignalOnLoadingComplete, ISignalOnReturnToRoom, ISignalOnComponentRemoved<MarioPlayer> {

        public override void OnInit(Frame f) {
            var gamemode = f.Context.GetAllAssets<GamemodeAsset>()[0];
            gamemode.DefaultRules.Materialize(f, ref f.Global->Rules);

            // Support booting in the editor.
            if (!f.RuntimeConfig.IsRealGame) {
                f.Global->GameState = GameState.WaitingForPlayers;
                f.Global->PlayerLoadFrames = (ushort) (20 * f.UpdateRate);
            } else {
                f.Events.GameStateChanged(f.Global->GameState);
            }
        }

        public override void Update(Frame f) {
            // Tick RNG
            _ = f.RNG->Next();

            // Parse lobby commands
            var playerDataDictionary = f.ResolveDictionary(f.Global->PlayerDatas);
            for (PlayerRef player = 0; player < f.MaxPlayerCount; player++) {
#if QUANTUM_3_1
                for (int i = 0; i < f.GetPlayerCommandCount(player); i++) {
                    if (f.GetPlayerCommand(player, i) is ILobbyCommand lobbyCommand) {
                        var playerData = QuantumUtils.GetPlayerData(f, player, playerDataDictionary);
                        if (playerData == null) {
                            break;
                        }
                        lobbyCommand.Execute(f, player, playerData);
                    }
                }
#else
                if (f.GetPlayerCommand(player) is ILobbyCommand lobbyCommand) {
                    using var profiler = HostProfiler.Start("ILobbyCommand.Execute");
                    var playerData = QuantumUtils.GetPlayerData(f, player, playerDataDictionary);
                    if (playerData == null) {
                        break;
                    }
                    lobbyCommand.Execute(f, player, playerData);
                }
#endif
            }

            // Game state logic
            switch (f.Global->GameState) {
            case GameState.PreGameRoom:
                if (f.Global->GameStartFrames > 0) {
                    if (!QuantumUtils.IsGameStartable(f)) {
                        StopCountdown(f);
                        break;
                    }

                    if (QuantumUtils.Decrement(ref f.Global->GameStartFrames)) {
                        // Start the game!
                        if (f.IsVerified) {
                            AssetRef<Map> nextStage;

                            switch (f.Global->Rules.ChooseMode) {
                            case StageChooseMode.Choose:
                                nextStage = f.Global->Rules.Stage;
                                break;
                            case StageChooseMode.Random: {
                                // Pick a random map
                                var allMaps = f.Context.GetAllAssets<Map>().ToList();

                                // Exclude disabled maps.
                                if (f.TryResolveHashSet(f.Global->Rules.RandomDisabledStages, out var disabledStages)) {
                                    allMaps.RemoveAll(map => disabledStages.Contains(map));
                                }

                                // Remove the previous map (if possible)
                                if (allMaps.Count > 1 && f.TryFindAsset(f.Global->PreviousStage, out Map previousStageMap)) {
                                    allMaps.Remove(previousStageMap);
                                }

                                nextStage = allMaps[f.RNG->Next(0, allMaps.Count)];
                                break;
                            }
                            default:
                                throw new ArgumentOutOfRangeException($"Unknown StageChooseMode {f.Global->Rules.ChooseMode}");
                            }

                            f.MapAssetRef = nextStage;
                        }

                        f.Global->PlayerLoadFrames = (ushort) (20 * f.UpdateRate);
                        f.Global->GameState = GameState.WaitingForPlayers;
                        f.Global->IsStartGameCountdownActive = false;

                        f.Events.GameStateChanged(GameState.WaitingForPlayers);
                    } else if (f.Global->GameStartFrames > 0 && f.Global->GameStartFrames % f.UpdateRate == 0) {
                        f.Events.CountdownTick(f.Global->GameStartFrames / f.UpdateRate);
                    }
                }
                break;

            case GameState.WaitingForPlayers:
                int validPlayers = 0;
                int loadedPlayers = 0;

                foreach ((_, var data) in f.Unsafe.GetComponentBlockIterator<PlayerData>()) {
                    if (!f.RuntimeConfig.IsRealGame) {
                        data->IsLoaded = true;
                        data->IsSpectator = false;
                    }

                    if (!data->IsSpectator) {
                        validPlayers++;
                        if (data->IsLoaded) {
                            loadedPlayers++;
                        }
                    }
                }
                f.Global->RealPlayers = (byte) validPlayers;

                if (validPlayers <= 0) {
                    break;
                }

                if (QuantumUtils.Decrement(ref f.Global->PlayerLoadFrames) || !f.RuntimeConfig.IsRealGame || (validPlayers == loadedPlayers)) {
                    // Progress to next stage.
                    f.Global->RealPlayers = (byte) loadedPlayers;
                    f.Global->GameState = GameState.Starting;
                    f.Global->GameStartFrames = (ushort) (6 * f.UpdateRate);
                    f.Global->Timer = f.Global->Rules.TimerMinutes * 60;

                    f.Signals.OnLoadingComplete();
                    f.Events.GameStateChanged(GameState.Starting);
                }
                break;

            case GameState.Starting:
                if (QuantumUtils.Decrement(ref f.Global->GameStartFrames)) {
                    // Now playing
                    f.Global->GameState = GameState.Playing;
                    f.Events.GameStateChanged(GameState.Playing);

                    foreach ((_, var data) in f.Unsafe.GetComponentBlockIterator<PlayerData>()) {
                        data->IsLoaded = false;
                        data->IsReady = false;
                    }

                } else if (f.Global->GameStartFrames == 79) {
                    f.Events.RecordingStarted();

                } else if (f.Global->GameStartFrames == 78) {
                    // Respawn all players and enable systems
                    f.Global->StartFrame = f.Number;
                    f.SystemEnable<StartDisabledSystemGroup>();

                    foreach (var otherGamemode in f.Context.GetAllAssets<GamemodeAsset>()) {
                        otherGamemode.DisableGamemode(f);
                    }

                    var gamemode = f.FindAsset(f.Global->Rules.Gamemode);
                    gamemode.EnableGamemode(f);

                    f.Signals.OnGameStarting();
                    f.Events.GameStarted();
                }
                break;
            
            case GameState.Playing:
                if (f.Global->Rules.TimerMinutes > 0 && f.Global->Timer > 0) {
                    if ((f.Global->Timer -= f.DeltaTime) <= 0) {
                        f.Global->Timer = 0;
                        CheckForGameEnd(f);
                        f.Events.TimerExpired();
                    }
                }

                if (f.Global->AutomaticStageRefreshInterval > 0) {
                    if (QuantumUtils.Decrement(ref f.Global->AutomaticStageRefreshTimer)) {
                        VersusStageData stage = f.FindAsset<VersusStageData>(f.Map.UserAsset);
                        stage.ResetStage(f, false);
                        f.Global->AutomaticStageRefreshTimer = f.Global->AutomaticStageRefreshInterval;
                        f.Events.StageAutoRefresh();
                    }
                }

#if QUANTUM_3_1
                foreach (var _ in f.GetPlayerCommands<CommandHostEndGame>(f.Global->Host)) {
                    EndGame(f, true, null);
                    break;
                }
#else
                if (f.GetPlayerCommand(f.Global->Host) is CommandHostEndGame) { 
                    EndGame(f, true, null);
                }
#endif
                break;

            case GameState.Ended:
                QuantumUtils.Decrement(ref f.Global->GameStartFrames);
                if (f.Global->GameStartFrames == 30) {
                    f.Events.StartGameEndFade();
                }

                if (f.Global->GameStartFrames == 0) {
                    // Move back to lobby.
                    f.Global->TotalGamesPlayed++;
                    if (f.IsVerified) {
                        f.Global->PreviousStage = f.MapAssetRef;
                        f.Map = null;
                    }
                    f.SystemEnable<StartDisabledSystemGroup>();
                    f.Signals.OnReturnToRoom();
                    f.Global->GameState = GameState.PreGameRoom;
                    f.Events.GameStateChanged(GameState.PreGameRoom);
                    f.SystemDisable<StartDisabledSystemGroup>();
                }
                break;
            }
        }

        public static void StopCountdown(Frame f) {
            f.Global->GameStartFrames = 0;
            f.Global->IsStartGameCountdownActive = false;
            f.Events.StartingCountdownChanged(false);
        }

        public static void CheckForGameEnd(Frame f) {
            var gamemode = f.FindAsset(f.Global->Rules.Gamemode);
            gamemode.CheckForGameEnd(f);
        }

        public static void EndGame(Frame f, bool endedByHost, int? winningTeam) {
            if (f.Global->GameState != GameState.Playing) {
                return;
            }

            f.Global->WinningTeam = winningTeam.GetValueOrDefault();
            f.Global->HasWinner = winningTeam.HasValue;

            f.Signals.OnGameEnding(winningTeam.GetValueOrDefault(), winningTeam.HasValue);
            f.Events.GameEnded(endedByHost, winningTeam.GetValueOrDefault(), winningTeam.HasValue);

            foreach ((_, var data) in f.Unsafe.GetComponentBlockIterator<PlayerData>()) {
                if (winningTeam == data->RealTeam && !data->IsSpectator) {
                    data->Wins++;
                }
                data->IsSpectator = data->ManualSpectator;
            }
            
            f.Global->GameState = GameState.Ended;
            f.Events.GameStateChanged(GameState.Ended);
            f.Global->GameStartFrames = (ushort) ((endedByHost ? Constants._3_50 : 21) * f.UpdateRate);
            f.SystemDisable<StartDisabledSystemGroup>();

            var gamemode = f.FindAsset(f.Global->Rules.Gamemode);
            gamemode.DisableGamemode(f);
        }

        public void OnMarioPlayerDied(Frame f, EntityRef entity) {
            CheckForGameEnd(f);
        }

        public void OnPlayerAdded(Frame f, PlayerRef player, bool firstTime) {
            RuntimePlayer runtimePlayer = f.GetPlayerData(player);

            var bans = f.ResolveList(f.Global->BannedPlayerIds);
            foreach (var ban in bans) {
                if (ban.MatchesPlayer(runtimePlayer)) {
                    // banned user- ignore them.
                    return;
                }
            }

            EntityRef newEntity = f.Create();
            f.Add(newEntity, out PlayerData* newData);
            newData->PlayerRef = player;
            newData->JoinTick = f.Number;
            newData->IsSpectator = f.Global->GameState != GameState.PreGameRoom;
            newData->RealTeam = 255;

            // Get team counts
            int teams = f.Context.GetAllAssets<TeamAsset>().Count;
            Span<byte> teamCounts = stackalloc byte[teams];
            var playerDatas = f.ResolveDictionary(f.Global->PlayerDatas);
            foreach ((PlayerRef otherPlayer, EntityRef otherEntity) in playerDatas) {
                var data = f.Unsafe.GetPointer<PlayerData>(otherEntity);
                if (data->RequestedTeam < teams) {
                    teamCounts[data->RequestedTeam]++;
                }
            }

            // Assign the player to the team with the least players
            int lowestTeamCount = teamCounts[0];
            int lowestTeamIndex = 0;
            for (int i = 1; i < teams; i++) {
                if (teamCounts[i] < lowestTeamCount) {
                    lowestTeamCount = teamCounts[i];
                    lowestTeamIndex = i;
                }
            }
            newData->RequestedTeam = (byte) lowestTeamIndex;

            // Other bookkeeping
            newData->Character = runtimePlayer.Character;
            newData->Palette = runtimePlayer.Palette;

            if (playerDatas.Count == 0) {
                // First player is host
                newData->SetAsHost(f, true);
            }

            foreach ((_, EntityRef otherEntity) in playerDatas) {
                var data = f.Unsafe.GetPointer<PlayerData>(otherEntity);
                if (data->RequestedTeam < teams) {
                    teamCounts[data->RequestedTeam]++;
                }
            }

            playerDatas[player] = newEntity;
            f.Events.PlayerAdded(player);
            f.Events.PlayerDataChanged(player);
        }

        public void OnPlayerRemoved(Frame f, PlayerRef player) {
            var playerDatas = f.ResolveDictionary(f.Global->PlayerDatas);
            bool hostChanged = false;

            for (int i = 0; i < f.Global->RealPlayers; i++) {
                ref PlayerInformation info = ref f.Global->PlayerInfo[i];
                if (info.PlayerRef == player) {
                    info.PlayerRef = PlayerRef.None;
                }
            }

            if (playerDatas.TryGetValue(player, out EntityRef entity)
                && f.Unsafe.TryGetPointer(entity, out PlayerData* deletedPlayerData)) {

                if (deletedPlayerData->IsRoomHost(f)) {
                    // Give the host to the youngest player.
                    PlayerData* youngestPlayer = null;
                    foreach ((_, EntityRef otherEntity) in playerDatas) {
                        PlayerData* otherPlayerData = f.Unsafe.GetPointer<PlayerData>(otherEntity);
                        if (deletedPlayerData == otherPlayerData) {
                            continue;
                        }

                        if (youngestPlayer == null || otherPlayerData->JoinTick < youngestPlayer->JoinTick) {
                            youngestPlayer = otherPlayerData;
                        }
                    }

                    if (youngestPlayer != null) {
                        youngestPlayer->SetAsHost(f, true);
                    }

                    hostChanged = true;
                }

                f.Destroy(entity);
                playerDatas.Remove(player);

                f.Events.PlayerRemoved(player);
            }

            switch (f.Global->GameState) {
            case GameState.PreGameRoom:
                if (f.Global->GameStartFrames > 0 && (hostChanged || !QuantumUtils.IsGameStartable(f))) {
                    StopCountdown(f);
                }
                break;
            case GameState.Starting:
                // Fixes spectators being able to take over old players' objects.
                foreach ((_, var mario) in f.Unsafe.GetComponentBlockIterator<MarioPlayer>()) {
                    if (mario->PlayerRef == player) {
                        mario->Disconnected = true;
                        mario->PlayerRef = PlayerRef.None;
                    }
                }
                goto case GameState.Playing;
            case GameState.Playing:
                for (int i = 0; i < f.Global->RealPlayers; i++) {
                    ref PlayerInformation info = ref f.Global->PlayerInfo[i];
                    if (info.PlayerRef != player) {
                        continue;
                    }

                    info.Disconnected = true;
                    info.Disqualified = true;
                    break;
                }
                break;
            }
        }

        public void OnLoadingComplete(Frame f) {
            // Spawn players
            var config = f.SimulationConfig;
            var stage = f.FindAsset<VersusStageData>(f.Map.UserAsset);

            var activePlayerDatas = stackalloc PlayerData*[f.ComponentCount<PlayerData>()];
            int activePlayerCount = 0;
            foreach ((_, var data) in f.Unsafe.GetComponentBlockIterator<PlayerData>()) {
                if (!data->IsLoaded) {
                    // Force spectator, didn't load in time
                    data->IsSpectator = true;
                    continue;
                }

                if (data->IsSpectator) {
                    continue;
                }

                activePlayerDatas[activePlayerCount++] = data;
            }
            SortByJoinTick(activePlayerDatas, activePlayerCount);

            int teamCount = 0;
            for (int i = 0; i < activePlayerCount; i++) {
                var data = activePlayerDatas[i];
                if (!f.TryFindAsset(data->Character, out var character)) {
                    character = f.Context.GetAllAssets<CharacterAsset>()[0];
                }

                EntityRef newPlayer = f.Create(character.Prototype);
                var mario = f.Unsafe.GetPointer<MarioPlayer>(newPlayer);
                mario->PlayerRef = data->PlayerRef;
                mario->Lives = (byte) f.Global->Rules.Lives;
                data->RealTeam = (byte) (f.Global->Rules.TeamsEnabled ? data->RequestedTeam : teamCount++);

                var newTransform = f.Unsafe.GetPointer<Transform2D>(newPlayer);
                newTransform->Position = stage.Spawnpoint;

                // Save runtimeplayer info for late joiners, in case this player DCs
                RuntimePlayer runtimePlayer = f.GetPlayerData(data->PlayerRef);
                f.Global->PlayerInfo[i] = new PlayerInformation {
                    PlayerRef = data->PlayerRef,
                    Nickname = runtimePlayer.PlayerNickname,
                    NicknameColor = runtimePlayer.NicknameColor,
                    Character = runtimePlayer.Character,
                    Team = data->RealTeam,
                };
            }

            // Assign random spawnpoints
            f.Global->TotalMarios = (byte) f.ComponentCount<MarioPlayer>();
            List<int> spawnpoints = Enumerable.Range(0, f.Global->TotalMarios).ToList();
            foreach ((var entity, var mario) in f.Unsafe.GetComponentBlockIterator<MarioPlayer>()) {
                int randomIndex = FPMath.FloorToInt(f.RNG->Next() * spawnpoints.Count);
                mario->SpawnpointIndex = (byte) spawnpoints[randomIndex];
                spawnpoints.RemoveAt(randomIndex);

                var camera = f.Unsafe.GetPointer<CameraController>(entity);
                camera->Recenter(stage, stage.GetWorldSpawnpointForPlayer(mario->SpawnpointIndex, f.Global->TotalMarios));
            }
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
            for (int i = 0; i < f.Global->PlayerInfo.Length; i++) {
                f.Global->PlayerInfo[i] = default;
            }

            foreach ((_, var data) in f.Unsafe.GetComponentBlockIterator<PlayerData>()) {
                data->IsLoaded = false;
                data->IsReady = false;
                data->IsSpectator = data->ManualSpectator;
                data->VotedToContinue = false;
                data->RealTeam = 255;
            }

            f.FindAsset(f.Global->Rules.Gamemode).OnReturnToRoom(f);
        }

        public void OnRemoved(Frame f, EntityRef entity, MarioPlayer* component) {
            for (int i = 0; i < f.Global->RealPlayers; i++) {
                ref PlayerInformation info = ref f.Global->PlayerInfo[i];
                if (info.PlayerRef != component->PlayerRef) {
                    continue;
                }

                info.Disqualified = true;
                break;
            }
        }

        private static void SortByJoinTick(PlayerData** span, int count) {
            for (int i = 1; i < count; i++) {
                var key = span[i];
                int j = i - 1;
                while (j >= 0 && span[j]->JoinTick > key->JoinTick) {
                    span[j + 1] = span[j];
                    j--;
                }
                span[j + 1] = key;
            }
        }
    }
}