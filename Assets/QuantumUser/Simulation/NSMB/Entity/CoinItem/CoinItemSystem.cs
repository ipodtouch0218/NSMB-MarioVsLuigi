using Photon.Deterministic;

namespace Quantum {
    public unsafe class CoinItemSystem : SystemMainThreadEntityFilter<CoinItem, CoinItemSystem.Filter> {

        public static readonly FP CameraYOffset = FP.FromString("1.68");

        public struct Filter {
            public EntityRef Entity;
            public CoinItem* CoinItem;
            public Transform2D* Transform;
            public PhysicsCollider2D* Collider;
        }

        public override void Update(Frame f, ref Filter filter, VersusStageData stage) {
            var coinItem = filter.CoinItem;
            var transform = filter.Transform;
            var collider = filter.Collider;
            var entity = filter.Entity;
            f.Unsafe.TryGetPointer(filter.Entity, out Interactable* interactable);
            f.Unsafe.TryGetPointer(filter.Entity, out PhysicsObject* physicsObject);

            if (coinItem->SpawnAnimationFrames > 0) {
                if (f.Exists(coinItem->ParentMarioPlayer)) {
                    // Attached to a player. Don't interact, and follow the player.
                    var marioTransform = f.Unsafe.GetPointer<Transform2D>(coinItem->ParentMarioPlayer);
                    var marioCamera = f.Unsafe.GetPointer<CameraController>(coinItem->ParentMarioPlayer);

                    // TODO magic value
                    transform->Position = new FPVector2(marioTransform->Position.X, marioCamera->CurrentPosition.Y + CameraYOffset);

                    if (QuantumUtils.Decrement(ref coinItem->SpawnAnimationFrames)) {
                        coinItem->ParentMarioPlayer = EntityRef.None;
                        if (interactable != null) {
                            interactable->ColliderDisabled = false;
                        }
                        if (physicsObject != null) {
                            physicsObject->IsFrozen = false;
                        }
                        f.Events.CoinItemBecameActive(entity);
                    } else {
                        return;
                    }
                } else if (coinItem->BlockSpawn) {
                    // Spawning from a block. Lerp between origin & destination.
                    FP t = 1 - ((FP) coinItem->SpawnAnimationFrames / (FP) coinItem->BlockSpawnAnimationLength);
                    transform->Position = FPVector2.Lerp(coinItem->BlockSpawnOrigin, coinItem->BlockSpawnDestination, t);

                    if (interactable != null && coinItem->SpawnAnimationFrames == 7) {
                        interactable->ColliderDisabled = false;
                    }

                    if (QuantumUtils.Decrement(ref coinItem->SpawnAnimationFrames)) {
                        if (PhysicsObjectSystem.BoxInGround((FrameThreadSafe) f, transform->Position, collider->Shape, false, stage, entity)) {
                            f.Events.CollectableDespawned(entity, f.Unsafe.GetPointer<Transform2D>(entity)->Position, false);
                            f.Destroy(entity);
                            return;
                        }
                        coinItem->BlockSpawn = false;
                        if (interactable != null) {
                            interactable->ColliderDisabled = false;
                        }
                        if (physicsObject != null) {
                            physicsObject->IsFrozen = false;
                        }
                        f.Events.CoinItemBecameActive(entity);
                    } else {
                        return;
                    }
                    return;
                } else if (coinItem->LaunchSpawn) {
                    // Back to normal layers
                    if (QuantumUtils.Decrement(ref coinItem->SpawnAnimationFrames)) {
                        coinItem->LaunchSpawn = false;
                        if (interactable != null) {
                            interactable->ColliderDisabled = false;
                        }
                        if (physicsObject != null) {
                            physicsObject->IsFrozen = false;
                        }
                    }
                } else {
                    if (QuantumUtils.Decrement(ref coinItem->SpawnAnimationFrames)) {
                        if (interactable != null) {
                            interactable->ColliderDisabled = false;
                        }
                    }
                }
            }

            if (physicsObject != null && coinItem->SpawnAnimationFrames == 0 && physicsObject->DisableCollision) {
                // Test that we're not in a wall anymore
                if (!PhysicsObjectSystem.BoxInGround((FrameThreadSafe) f, transform->Position, collider->Shape, stage: stage)) {
                    physicsObject->DisableCollision = false;
                }
            }

            if (coinItem->Lifetime > 0 && QuantumUtils.Decrement(ref coinItem->Lifetime)) {
                f.Events.CollectableDespawned(entity, transform->Position, false);
                f.Destroy(entity);
            }
        }
    }
}