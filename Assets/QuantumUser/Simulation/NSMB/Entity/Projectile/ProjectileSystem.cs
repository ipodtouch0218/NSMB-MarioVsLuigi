using Photon.Deterministic;

namespace Quantum {
    public unsafe class ProjectileSystem : SystemMainThreadFilterStage<ProjectileSystem.Filter>, ISignalOnTrigger2D {
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

            if (filter.Transform->Position.Y + collider->Shape.Centroid.Y + collider->Shape.Box.Extents.Y < stage.StageWorldMin.Y) {
                Destroy(f, filter.Entity, false);
                return;
            }


            if (!physicsObject->DisableCollision && !projectile->CheckedCollision) {
                if (PhysicsObjectSystem.BoxInsideTile(f, transform->Position, collider->Shape)) {
                    f.Destroy(filter.Entity);
                    return;
                }
                projectile->CheckedCollision = true;
            }

            var asset = f.FindAsset(projectile->Asset);
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
                || (physicsObject->IsTouchingGround && (!asset.Bounce || (projectile->HasBounced && asset.DestroyOnSecondBounce)))) {

                Destroy(f, filter.Entity, true);
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
            if (f.DestroyPending(info.Other)) {
                return;
            }

            if (TryDamagePlayer(f, info)
                || TryDamageEnemy(f, info)) {

                Destroy(f, info.Other, true);
            }
        }

        private bool TryDamageEnemy(Frame f, TriggerInfo2D info) {
            if (!f.Unsafe.TryGetPointer(info.Other, out Projectile* projectile)
                || !f.Unsafe.TryGetPointer(info.Entity, out Enemy* enemy)
                || !enemy->IsAlive) {
                return false;
            }

            

            if (f.Unsafe.TryGetPointer(info.Entity, out Goomba* goomba)) {
                var asset = f.FindAsset(projectile->Asset);
                switch (asset.Effect) {
                case ProjectileEffectType.Knockback: {
                    goomba->Kill(f, info.Entity, info.Other, true);
                    break;
                }
                }

                return true;

            } else if (f.Unsafe.TryGetPointer(info.Entity, out Koopa* koopa)) {
                var asset = f.FindAsset(projectile->Asset);
                switch (asset.Effect) {
                case ProjectileEffectType.Knockback: {
                    koopa->Kill(f, info.Entity, info.Other, true);
                    break;
                }
                }

                return true;
            }

            return false;
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

        private void Destroy(Frame f, EntityRef entity, bool playEffect) {
            f.Destroy(entity);
            f.Events.ProjectileDestroyed(f, entity, playEffect);
        }
    }
}