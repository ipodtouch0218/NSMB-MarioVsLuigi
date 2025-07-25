using Photon.Deterministic;

namespace Quantum {
    [UnityEngine.Scripting.Preserve]
    public unsafe class CoinItemSystem : SystemMainThreadEntityFilter<CoinItem, CoinItemSystem.Filter>, ISignalOnStageReset {

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
                        if (PhysicsObjectSystem.BoxInGround(f, transform->Position, collider->Shape, false, stage, entity)) {
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
                if (!PhysicsObjectSystem.BoxInGround(f, transform->Position, collider->Shape, stage: stage)) {
                    physicsObject->DisableCollision = false;
                }
            }

            if (coinItem->Lifetime > 0 && QuantumUtils.Decrement(ref coinItem->Lifetime)) {
                f.Events.CollectableDespawned(entity, transform->Position, false);
                f.Destroy(entity);
            }
        }

        public void OnStageReset(Frame f, QBoolean full) {
            VersusStageData stage = f.FindAsset<VersusStageData>(f.Map.UserAsset);
            var filter = f.Filter<CoinItem, Transform2D, PhysicsCollider2D>();
            while (filter.NextUnsafe(out EntityRef entity, out var coinItem, out var transform, out var collider)) {
                if (coinItem->SpawnAnimationFrames > 0 || !collider->Enabled
                    || (f.Unsafe.TryGetPointer(entity, out PhysicsObject* physicsObject) && physicsObject->DisableCollision)) {
                    continue;
                }

                if (PhysicsObjectSystem.BoxInGround(f, transform->Position, collider->Shape, stage: stage, entity: entity)) {
                    // Insta-despawn. Crushed by blocks respawning.
                    coinItem->Lifetime = 1;
                }
            }
        }
    }
}