using Photon.Deterministic;

namespace Quantum {
    public unsafe class CoinSystem : SystemMainThreadFilterStage<CoinSystem.Filter>, ISignalOnStageReset, ISignalOnMarioPlayerCollectedCoin,
        ISignalOnEntityBumped, ISignalOnEntityCrushed {

        private static readonly FP BounceThreshold = FP._1_50;
        private static readonly FP BounceStrength = Constants._0_66;

        public struct Filter {
            public EntityRef Entity;
            public Transform2D* Transform;
            public Coin* Coin;
        }

        public override void Update(Frame f, ref Filter filter, VersusStageData stage) {
            var entity = filter.Entity;

            if (f.DestroyPending(entity)) {
                return;
            }

            var coin = filter.Coin;
            QuantumUtils.Decrement(ref coin->UncollectableFrames);

            if (!coin->IsFloating) {
                if (coin->Lifetime == 480) {
                    // Eject
                    PhysicsObjectSystem.TryEject((FrameThreadSafe) f, entity, stage);
                }
                if (QuantumUtils.Decrement(ref coin->Lifetime)
                    || filter.Transform->Position.Y < stage.StageWorldMin.Y) {

                    f.Events.CollectableDespawned(entity, filter.Transform->Position, false);
                    f.Destroy(entity);
                    return;
                }

                var physicsObject = f.Unsafe.GetPointer<PhysicsObject>(entity);
                foreach (var contact in f.ResolveList(physicsObject->Contacts)) {
                    if (FPVector2.Dot(contact.Normal, FPVector2.Up) < PhysicsObjectSystem.GroundMaxAngle) {
                        continue;
                    }

                    if (physicsObject->PreviousFrameVelocity.Y < -BounceThreshold) {
                        physicsObject->Velocity = physicsObject->PreviousFrameVelocity;
                        physicsObject->Velocity.Y *= -BounceStrength;
                        physicsObject->IsTouchingGround = false;
                        f.Events.CoinBounced(entity, *coin);
                    } else {
                        physicsObject->Velocity.Y = 0;
                    }
                }
            }

            if (coin->DottedChangeFrames > 0 && QuantumUtils.Decrement(ref coin->DottedChangeFrames)) {
                // Become a normal coin
                coin->IsCurrentlyDotted = false;
                f.Events.CoinChangedType(entity, *coin);
            }
        }

        public void OnStageReset(Frame f, QBoolean full) {
            var allCoins = f.Filter<Coin, Interactable>();
            while (allCoins.NextUnsafe(out EntityRef entity, out Coin* coin, out Interactable* interactable)) {
                if (!full && (!coin->IsCollected || !coin->IsFloating)) {
                    continue;
                }

                coin->IsCollected = false;
                interactable->ColliderDisabled = false;
                f.Events.CoinChangeCollected(entity, *coin, false);

                if (coin->IsDotted && (!coin->IsCurrentlyDotted || full)) {
                    coin->IsCurrentlyDotted = true;
                    f.Events.CoinChangedType(entity, *coin);
                }
            }
        }

        public static void TryCollectCoin(Frame f, EntityRef coinEntity, EntityRef marioEntity) {
            if (!f.Unsafe.TryGetPointer(coinEntity, out Coin* coin)
                || coin->IsCollected
                || coin->UncollectableFrames > 0
                || f.DestroyPending(coinEntity)) {
                return;
            }

            if (coin->IsCurrentlyDotted) {
                if (coin->DottedChangeFrames == 0) {
                    coin->DottedChangeFrames = 30;
                }
                return;
            }

            var coinTransform = f.Unsafe.GetPointer<Transform2D>(coinEntity);
            var coinCollider = f.Unsafe.GetPointer<PhysicsCollider2D>(coinEntity);
            var coinInteractable = f.Unsafe.GetPointer<Interactable>(coinEntity);
            f.Signals.OnMarioPlayerCollectedCoin(marioEntity, f.Unsafe.GetPointer<MarioPlayer>(marioEntity), coinTransform->Position + coinCollider->Shape.Centroid, false, false);

            if (coin->IsFloating) {
                coin->IsCollected = true;
                coinInteractable->ColliderDisabled = true;
                f.Events.CoinChangeCollected(coinEntity, *coin, true);
            } else {
                f.Events.CollectableDespawned(coinEntity, coinTransform->Position, true);
                f.Destroy(coinEntity);
            }
        }

        public void OnMarioPlayerCollectedCoin(Frame f, EntityRef marioEntity, MarioPlayer* mario, FPVector2 worldLocation, QBoolean fromBlock, QBoolean downwards) {
            byte newCoins = (byte) (mario->Coins + 1);
            bool item = newCoins == f.Global->Rules.CoinsForPowerup;
            if (item) {
                mario->Coins = 0;
                MarioPlayerSystem.SpawnItem(f, marioEntity, mario, default);
            } else {
                mario->Coins = newCoins;
            }

            f.Events.MarioPlayerCollectedCoin(marioEntity, *mario, newCoins, item, worldLocation, fromBlock, downwards);
        }

        public void OnEntityBumped(Frame f, EntityRef entity, FPVector2 position, EntityRef bumpOwner, QBoolean fromBelow) {
            if (!f.Unsafe.TryGetPointer(entity, out Coin* coin)
                || !f.Unsafe.TryGetPointer(entity, out Transform2D* transform)
                || coin->IsCollected) {
                return;
            }

            if (coin->IsCurrentlyDotted) {
                if (coin->DottedChangeFrames == 0) {
                    coin->DottedChangeFrames = 30;
                }
                return;
            } else if (!coin->IsCollected && f.Unsafe.TryGetPointer(bumpOwner, out MarioPlayer* mario)) {
                f.Signals.OnMarioPlayerCollectedCoin(bumpOwner, mario, transform->Position, false, false);

                if (coin->IsFloating) {
                    coin->IsCollected = true;
                    f.Events.CoinChangeCollected(entity, *coin, true);
                } else {
                    f.Destroy(entity);
                }
            }
        }

        public void OnEntityCrushed(Frame f, EntityRef entity) {
            if (f.Unsafe.TryGetPointer(entity, out Coin* coin) && !coin->IsFloating) {
                coin->Lifetime = 0;
            }
        }
    }
}