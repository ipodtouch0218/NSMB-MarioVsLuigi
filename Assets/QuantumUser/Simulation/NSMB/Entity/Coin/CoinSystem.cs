namespace Quantum {

    public unsafe class CoinSystem : SystemMainThreadFilter<CoinSystem.Filter>, ISignalOnStageReset, ISignalOnTrigger2D {
        public struct Filter {
            public EntityRef Entity;
            public Transform2D* Transform;
            public Coin* Coin;
            public PhysicsObject* PhysicsObject;
        }

        public override void Update(Frame f, ref Filter filter) {



        }

        public void OnStageReset(Frame f) {
            var allCoins = f.Filter<Coin>();
            while (allCoins.NextUnsafe(out _, out Coin* coin)) {

            }
        }

        public void OnTrigger2D(Frame f, TriggerInfo2D info) {
            if (!f.Unsafe.TryGetPointer(info.Entity, out MarioPlayer* mario)
                || !f.Unsafe.TryGetPointer(info.Other, out Coin* coin)
                || !f.TryGet(info.Other, out Transform2D coinTransform)
                || !f.TryGet(info.Other, out PhysicsCollider2D coinCollider)
                || f.DestroyPending(info.Other)) {
                return;
            }

            byte newCoins = (byte) (mario->Coins + 1);
            bool item = newCoins == f.SimulationConfig.CoinsForPowerup;
            if (item) {
                mario->Coins = 0;
                MarioPlayerSystem.SpawnItem(f, info.Entity, mario, default);
            } else {
                mario->Coins = newCoins;
            }

            f.Destroy(info.Other);
            f.Events.MarioPlayerCollectedCoin(f, info.Entity, *mario, newCoins, item, coinTransform.Position + coinCollider.Shape.Centroid);
        }
    }
}