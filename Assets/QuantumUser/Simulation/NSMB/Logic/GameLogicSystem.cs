
namespace Quantum {
    public unsafe class GameLogicSystem : SystemMainThread, ISignalOnPlayerAdded, ISignalOnPlayerRemoved {

        public override void OnInit(Frame f) {
            var config = f.RuntimeConfig;
            if (config.IsRealGame) {
                f.Global->GameState = GameState.WaitingForPlayers;
            }
        }

        public override void Update(Frame f) {
            switch (f.Global->GameState) {
            case GameState.PreGameRoom:
                var playerDataDictionary = f.ResolveDictionary(f.Global->PlayerDatas);
                for (int i = 0; i < f.PlayerCount; i++) {
                    if (!playerDataDictionary.TryGetValue(i, out EntityRef entity)
                        || !f.Unsafe.TryGetPointer(entity, out PlayerData* playerData)) {
                        continue;
                    }

                    switch (f.GetPlayerCommand(i)) {
                    case CommandChangePlayerData changeData:
                        break;
                    case CommandStartTyping:
                        f.Events.PlayerStartedTyping(f, i);
                        break;
                    case CommandSendChatMessage chatMessage:
                        f.Events.PlayerSentChatMessage(f, i, chatMessage.Message);
                        break;
                    case CommandToggleReady:
                        playerData->IsReady = !playerData->IsReady;
                        break;
                    }
                }

                break;
            
            case GameState.WaitingForPlayers:
                bool allPlayersLoaded = true;
                var playerDataFilter = f.Filter<PlayerData>();
                while (playerDataFilter.NextUnsafe(out _, out PlayerData* data)) {
                    allPlayersLoaded &= data->IsSpectator || data->IsLoaded;
                }

                if (allPlayersLoaded) {
                    // Progress to next stage.
                    f.Global->GameState = GameState.Starting;
                    f.Global->GameStartFrames = 3 * 60 + 78;
                    f.Global->Timer = f.Global->Rules.TimerSeconds;
                    f.Events.GameStateChanged(f, GameState.Starting);    
                } else {
                    // Time out if players don't send a "ready" command in time
                }
                break;
            case GameState.Starting:
                if (QuantumUtils.Decrement(ref f.Global->GameStartFrames)) {
                    // Now playing
                    f.Global->GameState = GameState.Playing;
                    f.Global->StartFrame = f.Number;
                    f.Events.GameStateChanged(f, GameState.Playing);

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
                        // ...
                    }
                }
                break;

            case GameState.Ended:
                break;
            }
        }


        public void OnPlayerAdded(Frame f, PlayerRef player, bool firstTime) {
            EntityRef newEntity = f.Create();
            f.Add(newEntity, out PlayerData* newData);
            newData->PlayerRef = player;

            var datas = f.ResolveDictionary(f.Global->PlayerDatas);
            datas[player] = newEntity;

            f.Events.PlayerAdded(f, player);
        }

        public void OnPlayerRemoved(Frame f, PlayerRef player) {
            var datas = f.ResolveDictionary(f.Global->PlayerDatas);

            if (datas.TryGetValue(player, out EntityRef entity)) {
                f.Destroy(entity);
                datas.Remove(player);
            }

            f.Events.PlayerRemoved(f, player);
        }
    }
}