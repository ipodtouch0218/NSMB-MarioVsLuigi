namespace Quantum {
    public unsafe class GameLogicSystem : SystemMainThread {
        public override void Update(Frame f) {
            switch (f.Global->GameState) {
            case GameState.WaitingForPlayers:
                if (f.PlayerConnectedCount == f.RuntimeConfig.ExpectedPlayers) {
                    // Progress to next stage.
                    f.Global->GameState = GameState.Starting;
                    f.Global->GameStartFrames = 3 * 60 + 78;
                    f.Global->Timer = f.RuntimeConfig.TimerSeconds;
                    f.Events.GameStateChanged(f, GameState.Starting);    
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
                if (f.RuntimeConfig.TimerEnabled && f.Global->Timer > 0) {
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
    }
}