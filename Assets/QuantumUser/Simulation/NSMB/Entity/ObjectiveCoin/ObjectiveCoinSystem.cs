using Photon.Deterministic;

namespace Quantum {
    public unsafe class ObjectiveCoinSystem : SystemMainThread, ISignalOnMarioPlayerDropObjective, ISignalOnMarioPlayerCollectedCoin,
        ISignalOnMarioPlayerDied {

        public override bool StartEnabled => false;

        public override void Update(Frame f) {

        }

        public void OnMarioPlayerDropObjective(Frame f, EntityRef entity, int amount, QBoolean causedByOpposingPlayer) {
            var transform = f.Unsafe.GetPointer<Transform2D>(entity);
            byte excludeTeamNumber;
            if (causedByOpposingPlayer) {
                excludeTeamNumber = 0;
            } else {
                excludeTeamNumber = (byte) ((f.Unsafe.GetPointer<MarioPlayer>(entity)->GetTeam(f) + 1) ?? 0);
            }

            // Spawn objective coins relative to the "amount" parameter
            SpawnObjectiveCoins(f, transform->Position, 10 + 5 * (amount - 1), excludeTeamNumber);
        }

        public void OnMarioPlayerDied(Frame f, EntityRef entity) {
            // Lose half of all coins
            var mario = f.Unsafe.GetPointer<MarioPlayer>(entity);
            var transform = f.Unsafe.GetPointer<Transform2D>(entity);
            
            int coinsToSpawn = mario->GamemodeData.CoinRunners->ObjectiveCoins / 2;
            mario->GamemodeData.CoinRunners->ObjectiveCoins -= coinsToSpawn;
        }

        public void SpawnObjectiveCoins(Frame f, FPVector2 origin, int amount, byte exludeTeam) {
            VersusStageData stage = f.FindAsset<VersusStageData>(f.Map.UserAsset);
            for (int i = 0; i < amount; i++) {
                EntityRef newCoin = f.Create(f.SimulationConfig.ObjectiveCoinPrototype);
                var transform = f.Unsafe.GetPointer<Transform2D>(newCoin);
                var physicsObject = f.Unsafe.GetPointer<PhysicsObject>(newCoin);

                transform->Position = origin;

                FP range = 100;
                FP angle = f.RNG->Next(0, range / 2) - f.RNG->Next(0, range / 2) + 90;
                physicsObject->Velocity = FPVector2.Rotate(FPVector2.Right, angle * FP.Deg2Rad);
                FP dot = FPVector2.Dot(physicsObject->Velocity, FPVector2.Up);
                physicsObject->Velocity *= dot * 7;

                if (origin.Y < stage.StageWorldMin.Y) {
                    physicsObject->Velocity.Y += 7;
                }

                var coin = f.Unsafe.GetPointer<Coin>(newCoin);
                coin->UncollectableByTeam = exludeTeam;
            }
        }

        public void OnMarioPlayerCollectedCoin(Frame f, EntityRef marioEntity, EntityRef coinEntity, FPVector2 worldLocation, QBoolean fromBlock, QBoolean downwards) {
            if (!f.Unsafe.TryGetPointer(coinEntity, out Coin* coin) || !coin->CoinType.HasFlag(CoinType.Objective)) {
                // Powerup coin. Let the CoinSystem handle this.
                return;
            }

            var mario = f.Unsafe.GetPointer<MarioPlayer>(marioEntity);
            mario->GamemodeData.CoinRunners->ObjectiveCoins++;

            f.Events.MarioPlayerCollectedObjectiveCoin(marioEntity);
        }
    }
}