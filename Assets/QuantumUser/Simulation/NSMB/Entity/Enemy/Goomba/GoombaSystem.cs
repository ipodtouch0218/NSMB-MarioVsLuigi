using Photon.Deterministic;
using Quantum.Physics2D;

namespace Quantum {
    public unsafe class GoombaSystem : SystemMainThreadFilterStage<GoombaSystem.Filter>, ISignalOnEntityBumped {
        public struct Filter {
			public EntityRef Entity;
			public Transform2D* Transform;
            public Enemy* Enemy;
			public Goomba* Goomba;
			public PhysicsObject* PhysicsObject;
            public PhysicsCollider2D* Collider;
		}

        public override void Update(Frame f, ref Filter filter, VersusStageData stage) {
            var enemy = filter.Enemy;
            var goomba = filter.Goomba;
            var transform = filter.Transform;
            var physicsObject = filter.PhysicsObject;

            // Death animation
            if (enemy->IsDead) {
                // Check if they're fully dead now.
                if (goomba->DeathAnimationFrames > 0 && QuantumUtils.Decrement(ref goomba->DeathAnimationFrames)) {
                    enemy->IsActive = false;
                    physicsObject->IsFrozen = true;
                }
                return;
            }

            // Inactive check
            if (!enemy->IsAlive) {
                return;
            }
            
            // Turn around when hitting a wall.
            if (physicsObject->IsTouchingLeftWall || physicsObject->IsTouchingRightWall) {
                enemy->FacingRight = physicsObject->IsTouchingLeftWall;
            }

            // Move
            physicsObject->Velocity.X = goomba->Speed * (enemy->FacingRight ? 1 : -1);

            // Collide
            var hits = f.Physics2D.OverlapShape(*transform, filter.Collider->Shape);
            for (int i = 0; i < hits.Count; i++) {
                OnCollision(f, ref filter, hits[i], stage);
            }
        }

        public void OnCollision(Frame f, ref Filter filter, Hit hit, VersusStageData stage) {
            if (hit.Entity == filter.Entity) {
                return;
            }

            var enemy = filter.Enemy;
            var goomba = filter.Goomba;
            var goombaTransform = filter.Transform;
            var collider = filter.Collider;

            if (f.Unsafe.TryGetPointer(hit.Entity, out MarioPlayer* mario)
                && f.Unsafe.TryGetPointer(hit.Entity, out Transform2D* marioTransform)
                && f.Unsafe.TryGetPointer(hit.Entity, out PhysicsObject* marioPhysicsObject)) {

                if (mario->IsDead) {
                    return;
                }

                // Mario touched an alive goomba.
                QuantumUtils.UnwrapWorldLocations(stage, goombaTransform->Position + FPVector2.Up * FP._0_10, marioTransform->Position, out FPVector2 ourPos, out FPVector2 theirPos);
                FPVector2 damageDirection = (theirPos - ourPos).Normalized;
                bool attackedFromAbove = FPVector2.Dot(damageDirection, FPVector2.Up) > FP._0_25;

                bool groundpounded = attackedFromAbove && mario->IsGroundpoundActive && mario->CurrentPowerupState != PowerupState.MiniMushroom;
                if (mario->InstakillsEnemies(*marioPhysicsObject) || groundpounded) {
                    if (mario->IsDrilling) {
                        goomba->Kill(f, filter.Entity, hit.Entity, false);
                        mario->DoEntityBounce = true;
                    } else {
                        goomba->Kill(f, filter.Entity, hit.Entity, true);
                    }
                    return;
                }

                if (attackedFromAbove) {
                    if (mario->CurrentPowerupState == PowerupState.MiniMushroom) {
                        if (mario->IsGroundpounding) {
                            mario->IsGroundpounding = false;
                            goomba->Kill(f, filter.Entity, hit.Entity, false);
                        }
                        mario->DoEntityBounce = true;
                    } else {
                        goomba->Kill(f, filter.Entity, hit.Entity, false);
                        mario->DoEntityBounce = !mario->IsGroundpounding;
                    }

                    mario->IsDrilling = false;

                } else if (mario->IsCrouchedInShell) {
                    mario->FacingRight = damageDirection.X < 0;
                    marioPhysicsObject->Velocity.X = 0;

                } else if (mario->IsDamageable) {
                    mario->Powerdown(f, hit.Entity, false);
                    enemy->FacingRight = damageDirection.X > 0;
                }
                return;
            }
        }


        public void OnEntityBumped(Frame f, EntityRef entity, EntityRef blockBump) {
            if (!f.Unsafe.TryGetPointer(entity, out Goomba* goomba)
                || !f.TryGet(entity, out Enemy enemy)
                || !enemy.IsAlive) {
                return;
            }

            goomba->Kill(f, entity, blockBump, true);
        }
    }
}