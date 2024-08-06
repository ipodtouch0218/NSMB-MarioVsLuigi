using Photon.Deterministic;
using UnityEngine;

namespace Quantum {
    public unsafe class PiranhaPlantSystem : SystemMainThreadFilterStage<PiranhaPlantSystem.Filter>, ISignalOnTileChanged, ISignalOnEnemyRespawned {
        public struct Filter {
            public EntityRef Entity;
            public Transform2D* Transform;
            public PiranhaPlant* PiranhaPlant;
            public Enemy* Enemy;
            public PhysicsCollider2D* Collider;
        }

        public override void OnInit(Frame f) {
            EnemySystem.RegisterInteraction<PiranhaPlant, MarioPlayer>(OnPiranhaPlantMarioInteraction);
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
                    var playerOverlapShape = Shape2D.CreateCircle(FP._0_50);
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
            enemy->ColliderDisabled = piranhaPlant->PopupAnimationTime == 0;
            transform->Position = enemy->Spawnpoint + FPVector2.Up * (FP._0_25 + (piranhaPlant->PopupAnimationTime - 1) * FP._0_75);
        }

        public void OnPiranhaPlantMarioInteraction(Frame f, EntityRef piranhaPlantEntity, EntityRef marioEntity) {
            var mario = f.Unsafe.GetPointer<MarioPlayer>(marioEntity);

            // Don't use player.InstakillsEnemies as we don't want sliding to kill us.
            if (mario->IsStarmanInvincible || mario->IsInShell || mario->CurrentPowerupState == PowerupState.MegaMushroom) {
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
    }
}