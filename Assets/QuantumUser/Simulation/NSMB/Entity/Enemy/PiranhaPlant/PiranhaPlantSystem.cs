using Photon.Deterministic;
using UnityEngine;

namespace Quantum {
    public unsafe class PiranhaPlantSystem : SystemMainThreadFilterStage<PiranhaPlantSystem.Filter>, ISignalOnTileChanged, ISignalOnEnemyRespawned,
        ISignalOnBreakableObjectChangedHeight, ISignalOnIceBlockBroken {
        
        public struct Filter {
            public EntityRef Entity;
            public Transform2D* Transform;
            public PiranhaPlant* PiranhaPlant;
            public Enemy* Enemy;
            public PhysicsCollider2D* Collider;
            public Interactable* Interactable;
            public Freezable* Freezable;
        }

        public override void OnInit(Frame f) {
            f.Context.RegisterInteraction<PiranhaPlant, MarioPlayer>(OnPiranhaPlantMarioInteraction);
            f.Context.RegisterInteraction<PiranhaPlant, Projectile>(OnPiranhaPlantProjectileInteraction);
            f.Context.RegisterInteraction<PiranhaPlant, IceBlock>(OnPiranhaPlantIceBlockInteraction);
        }

        public override void Update(Frame f, ref Filter filter, VersusStageData stage) {
            var piranhaPlant = filter.PiranhaPlant;
            var transform = filter.Transform;
            var enemy = filter.Enemy;
            var freezable = filter.Freezable;

            if (!enemy->IsAlive
                || freezable->IsFrozen(f)) {
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

                    Physics2D.HitCollection playerHits = f.Physics2D.OverlapShape(*transform, playerOverlapShape, f.Context.PlayerOnlyMask);
                    if (playerHits.Count == 0) {
                        // No players nearby. pop up.
                        piranhaPlant->ChompFrames = 90;
                    }
                }
            }


            FP change = 2 * f.DeltaTime * (chomping ? 1 : -1);
            piranhaPlant->PopupAnimationTime = FPMath.Clamp01(piranhaPlant->PopupAnimationTime + change);
            filter.Interactable->ColliderDisabled = piranhaPlant->PopupAnimationTime < FP._0_10;
            FPVector2 offset = FPVector2.Up * (FP._0_25 + (piranhaPlant->PopupAnimationTime - 1) * FP._0_75);
            transform->Position = enemy->Spawnpoint + offset;

            freezable->IceBlockSize.Y = Constants._1_10 * piranhaPlant->PopupAnimationTime; 
        }

        public void OnPiranhaPlantIceBlockInteraction(Frame f, EntityRef piranhaPlantEntity, EntityRef iceBlockEntity) {
            var piranhaPlant = f.Unsafe.GetPointer<PiranhaPlant>(piranhaPlantEntity);
            var iceBlock = f.Unsafe.GetPointer<IceBlock>(iceBlockEntity);

            piranhaPlant->Kill(f, piranhaPlantEntity, iceBlockEntity, true);
        }

        public void OnPiranhaPlantProjectileInteraction(Frame f, EntityRef piranhaPlantEntity, EntityRef projectileEntity) {
            var projectileAsset = f.FindAsset(f.Unsafe.GetPointer<Projectile>(projectileEntity)->Asset);
            var piranhaPlant = f.Unsafe.GetPointer<PiranhaPlant>(piranhaPlantEntity);

            switch (projectileAsset.Effect) {
            case ProjectileEffectType.Knockback: {
                piranhaPlant->Kill(f, piranhaPlantEntity, projectileEntity, true);
                break;
            }
            case ProjectileEffectType.Freeze: {
                EntityRef newIceBlock = IceBlockSystem.Freeze(f, piranhaPlantEntity);
                var newIceBlockTransform = f.Unsafe.GetPointer<Transform2D>(newIceBlock);
                newIceBlockTransform->Position.Y += (1 - piranhaPlant->PopupAnimationTime) * FP._0_75;
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

            } else if (!mario->IsCrouchedInShell) {
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


        public void OnIceBlockBroken(Frame f, EntityRef brokenIceBlock, IceBlockBreakReason breakReason) {
            var iceBlock = f.Unsafe.GetPointer<IceBlock>(brokenIceBlock);
            if (f.Unsafe.TryGetPointer(iceBlock->Entity, out PiranhaPlant* piranhaPlant)) {
                piranhaPlant->Kill(f, iceBlock->Entity, brokenIceBlock, true);
            }
        }
    }
}