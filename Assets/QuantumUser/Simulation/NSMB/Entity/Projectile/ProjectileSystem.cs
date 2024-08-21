using Photon.Deterministic;

namespace Quantum {
    public unsafe class ProjectileSystem : SystemMainThreadFilterStage<ProjectileSystem.Filter> {
        public struct Filter {
            public EntityRef Entity;
            public Transform2D* Transform;
            public Projectile* Projectile;
            public PhysicsObject* PhysicsObject;
            public PhysicsCollider2D* PhysicsCollider;
        }

        public override void Update(Frame f, ref Filter filter, VersusStageData stage) {
            var projectile = filter.Projectile;
            var collider = filter.PhysicsCollider;
            var transform = filter.Transform;
            var physicsObject = filter.PhysicsObject;

            var asset = f.FindAsset(projectile->Asset);

            if (filter.Transform->Position.Y + collider->Shape.Centroid.Y + collider->Shape.Box.Extents.Y < stage.StageWorldMin.Y) {
                Destroy(f, filter.Entity, ParticleEffect.None);
                return;
            }

            if (!physicsObject->DisableCollision && !projectile->CheckedCollision) {
                if (PhysicsObjectSystem.BoxInsideTile(f, transform->Position, collider->Shape)) {
                    Destroy(f, filter.Entity, asset.DestroyParticleEffect);
                    return;
                }
                projectile->CheckedCollision = true;
            }

            HandleTileCollision(f, filter, asset);

            physicsObject->Velocity.X = projectile->Speed * (projectile->FacingRight ? 1 : -1);

            if (asset.LockTo45Degrees) {
                physicsObject->TerminalVelocity = -projectile->Speed;
            }
        }

        public void HandleTileCollision(Frame f, Filter filter, ProjectileAsset asset) {
            var projectile = filter.Projectile;
            var physicsObject = filter.PhysicsObject;

            // Despawn
            if (physicsObject->IsTouchingLeftWall
                || physicsObject->IsTouchingRightWall
                || physicsObject->IsTouchingCeiling
                || (physicsObject->IsTouchingGround && (!asset.Bounce || (projectile->HasBounced && asset.DestroyOnSecondBounce)))
                || PhysicsObjectSystem.BoxInsideTile(f, filter.Transform->Position, filter.PhysicsCollider->Shape)) {

                Destroy(f, filter.Entity, asset.DestroyParticleEffect);
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
        }

        private bool TryDamagePlayer(Frame f, TriggerInfo2D info) {

            if (!f.Unsafe.TryGetPointer(info.Entity, out MarioPlayer* mario)
                || !f.TryGet(info.Entity, out PhysicsObject physicsObject) 
                || !f.Unsafe.TryGetPointer(info.Other, out Projectile* projectile)
                || !f.TryGet(projectile->Owner, out MarioPlayer ownerMario)) {
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

            if (mario->CurrentPowerupState == PowerupState.MegaMushroom
                || mario->IsStarmanInvincible
                || (asset.Effect == ProjectileEffectType.Freeze && mario->IsInKnockback)) {
                return true;
            }

            bool dropStars = mario->Team != ownerMario.Team;

            // Player state checks
            switch (mario->CurrentPowerupState) {
            case PowerupState.MiniMushroom:
                if (dropStars) {
                    mario->Death(f, info.Entity, false);
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

            // Normal collision is a GO

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

        public static void Destroy(Frame f, EntityRef entity, ParticleEffect particle) {
            f.Events.ProjectileDestroyed(f, entity, particle, f.Get<Transform2D>(entity).Position);
            f.Destroy(entity);
        }
    }
}