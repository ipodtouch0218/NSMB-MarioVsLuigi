using Photon.Deterministic;
using UnityEngine;

namespace Quantum {
    public unsafe class PiranhaPlantSystem : SystemMainThreadFilterStage<PiranhaPlantSystem.Filter>, ISignalOnTileChanged, ISignalOnEnemyRespawned, ISignalOnBreakableObjectChangedHeight {
        public struct Filter {
            public EntityRef Entity;
            public Transform2D* Transform;
            public PiranhaPlant* PiranhaPlant;
            public Enemy* Enemy;
            public PhysicsCollider2D* Collider;
            public Interactable* Interactable;
        }

        public override void OnInit(Frame f) {
            InteractionSystem.RegisterInteraction<PiranhaPlant, MarioPlayer>(OnPiranhaPlantMarioInteraction);
            InteractionSystem.RegisterInteraction<PiranhaPlant, Projectile>(OnPiranhaPlantProjectileInteraction);
        }

        public override void Update(Frame f, ref Filter filter, VersusStageData stage) {
            var piranhaPlant = filter.PiranhaPlant;
            var transform = filter.Transform;
            var enemy = filter.Enemy;

            if (!enemy->IsAlive) {
                return;
            }

            bool chomping = piranhaPlant->ChompFrames > 0;
            if (chomping) {
                // Currently chomping.
                if (QuantumUtils.Decrement(ref piranhaPlant->ChompFrames)) {
                    piranhaPlant->WaitingFrames = 216;
                }
            } else {
                // Not chomping, run the countdown timer.
                if (QuantumUtils.Decrement(ref piranhaPlant->WaitingFrames)) {
                    var playerOverlapShape = Shape2D.CreateCircle(FP._0_50, FPVector2.Up);
                    var playerOverlapMask = f.Layers.GetLayerMask("Player");

                    Physics2D.HitCollection playerHits = f.Physics2D.OverlapShape(*transform, playerOverlapShape, playerOverlapMask);
                    if (playerHits.Count == 0) {
                        // No players nearby. pop up.
                        piranhaPlant->ChompFrames = 90;
                    }
                }
            }

            FP change = 2 * f.DeltaTime * (chomping ? 1 : -1);
            piranhaPlant->PopupAnimationTime = FPMath.Clamp01(piranhaPlant->PopupAnimationTime + change);
            filter.Interactable->ColliderDisabled = piranhaPlant->PopupAnimationTime == 0;
            transform->Position = enemy->Spawnpoint + FPVector2.Up * (FP._0_25 + (piranhaPlant->PopupAnimationTime - 1) * FP._0_75);
        }

        public void OnPiranhaPlantProjectileInteraction(Frame f, EntityRef piranhaPlantEntity, EntityRef projectileEntity) {
            var projectileAsset = f.FindAsset(f.Unsafe.GetPointer<Projectile>(projectileEntity)->Asset);

            switch (projectileAsset.Effect) {
            case ProjectileEffectType.Knockback: {
                f.Unsafe.GetPointer<PiranhaPlant>(piranhaPlantEntity)->Kill(f, piranhaPlantEntity, projectileEntity, true);
                break;
            }
            case ProjectileEffectType.Freeze: {
                // TODO
                break;
            }
            }

            if (projectileAsset.DestroyOnHit) {
                ProjectileSystem.Destroy(f, projectileEntity, projectileAsset.DestroyParticleEffect);
            }
        }

        public void OnPiranhaPlantMarioInteraction(Frame f, EntityRef piranhaPlantEntity, EntityRef marioEntity) {
            var mario = f.Unsafe.GetPointer<MarioPlayer>(marioEntity);
            var marioPhysicsObject = f.Unsafe.GetPointer<PhysicsObject>(marioEntity);

            if (mario->InstakillsEnemies(marioPhysicsObject, false)) {
                var piranhaPlant = f.Unsafe.GetPointer<PiranhaPlant>(piranhaPlantEntity);
                piranhaPlant->Kill(f, piranhaPlantEntity, marioEntity, true);
            } else {
                mario->Powerdown(f, marioEntity, false);
            }
        }

        public void OnTileChanged(Frame f, int tileX, int tileY, StageTileInstance newTile) {
            var filter = f.Filter<Transform2D, PiranhaPlant, Enemy>();
            VersusStageData stage = f.FindAsset<VersusStageData>(f.Map.UserAsset);

            while (filter.NextUnsafe(out EntityRef entity, out Transform2D* transform, out PiranhaPlant* piranhaPlant, out Enemy* enemy)) {
                if (!enemy->IsAlive) {
                    continue;
                }

                Vector2Int tile = QuantumUtils.WorldToRelativeTile(stage, enemy->Spawnpoint);
                if (tile.x == tileX && tile.y == tileY) {
                    piranhaPlant->Kill(f, entity, EntityRef.None, true);
                }
            }
        }

        public void OnEnemyRespawned(Frame f, EntityRef entity) {
            if (f.Unsafe.TryGetPointer(entity, out PiranhaPlant* piranhaPlant)) {
                piranhaPlant->Respawn(f, entity);
            }
        }

        public void OnBreakableObjectChangedHeight(Frame f, EntityRef breakableEntity, FP newHeight) {
            var filter = f.Filter<Enemy, PiranhaPlant>();
            while (filter.NextUnsafe(out EntityRef piranhaPlantEntity, out Enemy* enemy, out PiranhaPlant* piranhaPlant)) {
                if (!enemy->IsAlive) {
                    continue;
                }
                var breakable = f.Unsafe.GetPointer<BreakableObject>(breakableEntity);
                if (piranhaPlant->Pipe == breakableEntity && newHeight != breakable->OriginalHeight) {
                    piranhaPlant->Kill(f, piranhaPlantEntity, EntityRef.None, true);
                }
            }
        }
    }
}