using Photon.Deterministic;

namespace Quantum {
    public unsafe class CoinSystem : SystemMainThreadFilter<CoinSystem.Filter>, ISignalOnStageReset, ISignalOnTrigger2D, ISignalOnMarioPlayerCollectedCoin, ISignalOnEntityBumped {
        public struct Filter {
            public EntityRef Entity;
            public Transform2D* Transform;
            public Coin* Coin;
        }

        public override void Update(Frame f, ref Filter filter) {
            var coin = filter.Coin;

            QuantumUtils.Decrement(ref coin->UncollectableFrames);

            if (!coin->IsFloating) {
                if (QuantumUtils.Decrement(ref coin->Lifetime)) {
                    f.Destroy(filter.Entity);
                    return;
                }

                var physicsObject = f.Unsafe.GetPointer<PhysicsObject>(filter.Entity);
                foreach (var contact in f.ResolveList(physicsObject->Contacts)) {
                    if (FPVector2.Dot(contact.Normal, FPVector2.Up) < FP._0_33 * 2) {
                        continue;
                    }

                    physicsObject->Velocity = coin->PreviousVelocity;
                    physicsObject->Velocity.Y *= -FP._0_33 * 2;

                    if (physicsObject->Velocity.Y > FP._0_75) {
                        f.Events.CoinBounced(f, filter.Entity, *coin);
                    }
                }

                coin->PreviousVelocity = physicsObject->Velocity;
            }

            if (coin->DottedChangeFrames > 0 && QuantumUtils.Decrement(ref coin->DottedChangeFrames)) {
                // Become a normal coin
                coin->IsCurrentlyDotted = false;
                f.Events.CoinChangedType(f, filter.Entity, *coin);
            }
        }

        public void OnStageReset(Frame f, QBoolean full) {
            var allCoins = f.Filter<Coin>();
            while (allCoins.NextUnsafe(out EntityRef entity, out Coin* coin)) {
                if (!full && (!coin->IsCollected || !coin->IsFloating)) {
                    continue;
                }

                coin->IsCollected = false;
                f.Events.CoinChangeCollected(f, entity, *coin);

                if (coin->IsDotted && (!coin->IsCurrentlyDotted || full)) {
                    coin->IsCurrentlyDotted = true;
                    f.Events.CoinChangedType(f, entity, *coin);
                }
            }
        }

        public void OnTrigger2D(Frame f, TriggerInfo2D info) {
            // Collecting a coin
            if (!f.Unsafe.TryGetPointer(info.Other, out Coin* coin)
                || coin->IsCollected
                || coin->UncollectableFrames > 0
                || !f.TryGet(info.Other, out Transform2D coinTransform)
                || !f.TryGet(info.Other, out PhysicsCollider2D coinCollider)
                || f.DestroyPending(info.Other)) {

                return;
            }

            EntityRef marioEntity = info.Entity;

            if (!f.Unsafe.TryGetPointer(info.Entity, out MarioPlayer* mario)) {
                // Try to see if a moving koopa has a previous mario holder
                if (!f.Unsafe.TryGetPointer(info.Entity, out Koopa* koopa) 
                    || !koopa->IsKicked
                    || !f.Unsafe.TryGetPointer(info.Entity, out Holdable* holdable)
                    || !f.Unsafe.TryGetPointer(holdable->PreviousHolder, out mario)) {
                    return;
                }

                marioEntity = holdable->PreviousHolder;
            }

            if (mario->IsDead) {
                return;
            }

            if (coin->IsCurrentlyDotted) {
                if (coin->DottedChangeFrames == 0) {
                    coin->DottedChangeFrames = 30;
                }
                return;
            }

            if (coin->IsFloating) {
                coin->IsCollected = true;
                f.Events.CoinChangeCollected(f, info.Other, *coin);
            } else {
                 f.Destroy(info.Other);
            }
            f.Signals.OnMarioPlayerCollectedCoin(marioEntity, mario, coinTransform.Position + coinCollider.Shape.Centroid, false, false);
        }

        public void OnMarioPlayerCollectedCoin(Frame f, EntityRef marioEntity, MarioPlayer* mario, FPVector2 worldLocation, QBoolean fromBlock, QBoolean downwards) {
            byte newCoins = (byte) (mario->Coins + 1);
            bool item = newCoins == f.RuntimeConfig.CoinsForPowerup;
            if (item) {
                mario->Coins = 0;
                MarioPlayerSystem.SpawnItem(f, marioEntity, mario, default);
            } else {
                mario->Coins = newCoins;
            }

            f.Events.MarioPlayerCollectedCoin(f, marioEntity, *mario, newCoins, item, worldLocation, fromBlock, downwards);
        }

        public void OnEntityBumped(Frame f, EntityRef entity, EntityRef blockBump) {
            if (!f.Unsafe.TryGetPointer(entity, out Coin* coin)
                || !f.TryGet(entity, out Transform2D transform)
                || coin->IsCollected
                || !f.TryGet(blockBump, out BlockBump block)) {
                return;
            }

            if (coin->IsCurrentlyDotted) {
                if (coin->DottedChangeFrames == 0) {
                    coin->DottedChangeFrames = 30;
                }
                return;
            } else if (!coin->IsCollected && f.Unsafe.TryGetPointer(block.Owner, out MarioPlayer* mario)) {
                f.Signals.OnMarioPlayerCollectedCoin(block.Owner, mario, transform.Position, false, false);

                if (coin->IsFloating) {
                    coin->IsCollected = true;
                    f.Events.CoinChangeCollected(f, entity, *coin);
                } else {
                    f.Destroy(entity);
                }
            }
        }
    }
}