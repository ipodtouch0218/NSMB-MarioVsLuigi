using Photon.Deterministic;

namespace Quantum {
    public unsafe class ProjectileSystem : SystemMainThreadFilter<ProjectileSystem.Filter>, ISignalOnTrigger2D {
        public struct Filter {
            public EntityRef Entity;
            public Transform2D Transform;
            public Projectile* Projectile;
            public PhysicsObject* PhysicsObject;
        }

        public override void Update(Frame f, ref Filter filter) {
            var projectile = filter.Projectile;
            var asset = f.FindAsset(projectile->Asset);
            var stage = f.FindAsset<VersusStageData>(f.Map.UserAsset);

            if (filter.Transform.Position.Y < stage.StageWorldMin.Y) {
                f.Destroy(filter.Entity);
                return;
            }

            HandleTileCollision(f, filter, asset);
            filter.PhysicsObject->Velocity.X = projectile->Speed * (projectile->FacingRight ? 1 : -1);

            if (asset.LockTo45Degrees) {
                filter.PhysicsObject->TerminalVelocity = -projectile->Speed;
            }
        }

        public void HandleTileCollision(Frame f, Filter filter, ProjectileAsset asset) {
            var projectile = filter.Projectile;
            var physicsObject = filter.PhysicsObject;

            // Despawn
            if (physicsObject->IsTouchingLeftWall ||
                physicsObject->IsTouchingRightWall ||
                physicsObject->IsTouchingCeiling ||
                (physicsObject->IsTouchingGround && (!asset.Bounce || (projectile->HasBounced && asset.DestroyOnSecondBounce)))) {

                f.Destroy(filter.Entity);
                return;
            }

            // Bounce
            if (physicsObject->IsTouchingGround && asset.Bounce) {
                FP boost = asset.BounceStrength * FPMath.Abs(FPMath.Sin(physicsObject->FloorAngle * FP.Deg2Rad)) * FP._1_25;
                if ((physicsObject->FloorAngle > 0) == projectile->FacingRight) {
                    boost = 0;
                }

                physicsObject->Velocity.Y = asset.BounceStrength + boost;
                projectile->HasBounced = true;
            }


            /* TODO
            if (Utils.Utils.IsTileSolidAtWorldLocation(body.Position)) {
                DespawnEntity();
            }
            */
        }

        public void OnTrigger2D(Frame f, TriggerInfo2D info) {
            if (TryDamagePlayer(f, info)) {
                f.Destroy(info.Other);
                return;
            }

        }

        private bool TryDamagePlayer(Frame f, TriggerInfo2D info) {

            if (f.DestroyPending(info.Other) ||
                !f.Unsafe.TryGetPointer(info.Entity, out MarioPlayer* mario) ||
                !f.TryGet(info.Entity, out PhysicsObject physicsObject) ||
                !f.Unsafe.TryGetPointer(info.Other, out Projectile* projectile) ||
                !f.TryGet(projectile->Owner, out MarioPlayer ownerMario)) {
                return false;
            }

            // Check if they own us. If so, don't collide.
            if (projectile->Owner == info.Entity) {
                return false;
            }

            // If they have knockback invincibility, don't collide.
            if (mario->DamageInvincibilityFrames > 0) {
                return false;
            }

            var asset = f.FindAsset(projectile->Asset);

            if (mario->CurrentPowerupState == PowerupState.MegaMushroom || mario->IsStarmanInvincible || (asset.Effect == ProjectileEffectType.Freeze && mario->IsInKnockback)) {
                return true;
            }

            bool dropStars = mario->Team != ownerMario.Team;

            // Player state checks
            switch (mario->CurrentPowerupState) {
            case PowerupState.MiniMushroom:
                if (dropStars) {
                    // player.Death(false, false);
                } else {
                    // player.DoKnockback(!FacingRight, 0, true, Object);
                }
                return true;
            case PowerupState.BlueShell:
                if (asset.DoesntEffectBlueShell && (mario->IsInShell || mario->IsCrouching || mario->IsGroundpounding)) {
                    mario->ShellSlowdownFrames = asset.BlueShellSlowdownFrames;
                    return true;
                }
                break;
            }

            // Collision is a GO

            switch (asset.Effect) {
            case ProjectileEffectType.Knockback:
                //mario->DoKnockback(!projectile->FacingRight, dropStars ? 1 : 0, true, Object);
                break;
            case ProjectileEffectType.Freeze:
                /*
                if (!mario->IsFrozen) {
                    FrozenCube.FreezeEntity(Runner, player);
                }
                */
                break;
            }

            return true;
        }
    }
}