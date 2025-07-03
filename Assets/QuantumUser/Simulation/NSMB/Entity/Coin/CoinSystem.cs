using Photon.Deterministic;
using UnityEngine;

namespace Quantum {
    public unsafe class CoinSystem : SystemMainThreadEntityFilter<Coin, CoinSystem.Filter>, ISignalOnStageReset, ISignalOnMarioPlayerCollectedCoin,
        ISignalOnEntityBumped, ISignalOnEntityCrushed {

        private static readonly FP BounceThreshold = FP._1_50;
        private static readonly FP BounceStrength = Constants._0_66;
        private static readonly FP GroundFriction = FP.FromString("0.95");

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

            if (!coin->CoinType.HasFlag(CoinType.BakedInStage)) {
                if (coin->Lifetime == 480) {
                    // Eject
                    PhysicsObjectSystem.TryEject((FrameThreadSafe) f, entity, stage);
                }
                if (QuantumUtils.Decrement(ref coin->Lifetime)
                    || (coin->UncollectableFrames == 0 && filter.Transform->Position.Y < stage.StageWorldMin.Y)) {

                    f.Events.CollectableDespawned(entity, filter.Transform->Position, false);
                    f.Destroy(entity);
                    return;
                }

                var physicsObject = f.Unsafe.GetPointer<PhysicsObject>(entity);
                bool invertX = false, invertY = false, applyFriction = false;
                foreach (var contact in f.ResolveList(physicsObject->Contacts)) {
                    if (FPMath.Abs(FPVector2.Dot(contact.Normal, FPVector2.Up)) < PhysicsObjectSystem.GroundMaxAngle) {
                        // Wall touch
                        invertX = true;
                    } else {
                        // Ground touch
                        applyFriction = true;
                        if (physicsObject->PreviousFrameVelocity.Y < -BounceThreshold) {
                            physicsObject->IsTouchingGround = false;
                            invertY = true;
                        } else {
                            physicsObject->Velocity.Y = 0;
                        }
                    }
                }

                if (invertX) {
                    physicsObject->Velocity.X = physicsObject->PreviousFrameVelocity.X * -BounceStrength;
                }
                if (invertY) {
                    physicsObject->Velocity.Y = physicsObject->PreviousFrameVelocity.Y * -BounceStrength;
                }
                if (/*!coin->CoinType.HasFlag(CoinType.Objective) &&*/ (invertX || invertY)) {
                    f.Events.CoinBounced(entity, *coin);
                }
                if (applyFriction) {
                    physicsObject->Velocity.X *= GroundFriction;
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
                if (!full && (!coin->IsCollected || !coin->CoinType.HasFlag(CoinType.BakedInStage))) {
                    continue;
                }

                coin->IsCollected = false;
                interactable->ColliderDisabled = false;
                f.Events.CoinChangeCollected(entity, *coin, false);

                if (coin->CoinType.HasFlag(CoinType.Dotted) && (!coin->IsCurrentlyDotted || full)) {
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

            if (f.Unsafe.TryGetPointer(coinEntity, out ObjectiveCoin* objectiveCoin)) {
                var mario = f.Unsafe.GetPointer<MarioPlayer>(marioEntity);
                bool sameTeam = ((mario->GetTeam(f) + 1) ?? int.MinValue) == objectiveCoin->UncollectableByTeam;
                if (mario->IsDead || (sameTeam && (!mario->CanCollectOwnTeamsObjectiveCoins || objectiveCoin->SpawnedViaSelfDamage))) {
                    return;
                }
            }

            var coinTransform = f.Unsafe.GetPointer<Transform2D>(coinEntity);
            var coinCollider = f.Unsafe.GetPointer<PhysicsCollider2D>(coinEntity);
            var coinInteractable = f.Unsafe.GetPointer<Interactable>(coinEntity);
            f.Signals.OnMarioPlayerCollectedCoin(marioEntity, coinEntity, coinTransform->Position + coinCollider->Shape.Centroid, false, false);

            if (coin->CoinType.HasFlag(CoinType.BakedInStage)) {
                coin->IsCollected = true;
                coinInteractable->ColliderDisabled = true;
                f.Events.CoinChangeCollected(coinEntity, *coin, true);
            } else {
                f.Events.CollectableDespawned(coinEntity, coinTransform->Position, true);
                f.Destroy(coinEntity);
            }
        }

        public void OnMarioPlayerCollectedCoin(Frame f, EntityRef marioEntity, EntityRef coinEntity, FPVector2 worldLocation, QBoolean fromBlock, QBoolean downwards) {
            if (f.Unsafe.TryGetPointer(coinEntity, out Coin* coin) && coin->CoinType.HasFlag(CoinType.Objective)) {
                // Objective coin. Let the ObjectiveCoin system handle this.
                return;
            }

            // Normal, powerup coin.
            var mario = f.Unsafe.GetPointer<MarioPlayer>(marioEntity);

            byte newCoins = (byte) (mario->Coins + 1);
            bool item = newCoins == f.Global->Rules.CoinsForPowerup;
            if (item) {
                mario->Coins = 0;
                MarioPlayerSystem.SpawnItem(f, marioEntity, mario, default, fromBlock);
            } else {
                mario->Coins = newCoins;
            }

            f.Events.MarioPlayerCollectedCoin(marioEntity, newCoins, item, worldLocation, fromBlock, downwards);
        }

        public void OnEntityBumped(Frame f, EntityRef coinEntity, FPVector2 position, EntityRef bumpOwner, QBoolean fromBelow) {
            if (!f.Unsafe.TryGetPointer(coinEntity, out Coin* coin)
                || !f.Unsafe.TryGetPointer(coinEntity, out Transform2D* transform)
                || coin->IsCollected
                || coin->UncollectableFrames > 0) {
                return;
            }

            if (coin->IsCurrentlyDotted) {
                if (coin->DottedChangeFrames == 0) {
                    coin->DottedChangeFrames = 30;
                }
                return;
            } else if (!coin->IsCollected && f.Unsafe.TryGetPointer(bumpOwner, out MarioPlayer* mario)) {
                if (f.Unsafe.TryGetPointer(coinEntity, out ObjectiveCoin* objectiveCoin)) {
                    bool sameTeam = ((mario->GetTeam(f) + 1) ?? int.MinValue) == objectiveCoin->UncollectableByTeam;
                    if (mario->IsDead || (sameTeam && (!mario->CanCollectOwnTeamsObjectiveCoins || objectiveCoin->SpawnedViaSelfDamage))) {
                        return;
                    }
                }

                f.Signals.OnMarioPlayerCollectedCoin(bumpOwner, coinEntity, transform->Position, false, false);

                if (coin->CoinType.HasFlag(CoinType.BakedInStage)) {
                    coin->IsCollected = true;
                    f.Events.CoinChangeCollected(coinEntity, *coin, true);
                } else {
                    f.Destroy(coinEntity);
                }
            }
        }

        public void OnEntityCrushed(Frame f, EntityRef entity) {
            if (f.Unsafe.TryGetPointer(entity, out Coin* coin) && !coin->CoinType.HasFlag(CoinType.BakedInStage)) {
                coin->Lifetime = 0;
            }
        }
    }
}