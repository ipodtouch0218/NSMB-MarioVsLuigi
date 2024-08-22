using Photon.Deterministic;

namespace Quantum {
    public unsafe class GoombaSystem : SystemMainThreadFilterStage<GoombaSystem.Filter>, ISignalOnEntityBumped, ISignalOnBobombExplodeEntity {
        public struct Filter {
			public EntityRef Entity;
			public Transform2D* Transform;
            public Enemy* Enemy;
			public Goomba* Goomba;
			public PhysicsObject* PhysicsObject;
            public PhysicsCollider2D* Collider;
		}

        public override void OnInit(Frame f) {
            InteractionSystem.RegisterInteraction<Goomba, Goomba>(OnGoombaGoombaInteraction);
            InteractionSystem.RegisterInteraction<Goomba, PiranhaPlant>(EnemySystem.EnemyBumpTurnaroundOnlyFirst);
            InteractionSystem.RegisterInteraction<Goomba, MarioPlayer>(OnGoombaMarioInteraction);
            InteractionSystem.RegisterInteraction<Goomba, Projectile>(OnGoombaProjectileInteraction);
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
        }

        public void OnGoombaGoombaInteraction(Frame f, EntityRef goombaEntityA, EntityRef goombaEntityB) {
            EnemySystem.EnemyBumpTurnaround(f, goombaEntityA, goombaEntityB);
        }

        public void OnGoombaMarioInteraction(Frame f, EntityRef goombaEntity, EntityRef marioEntity) {
            var goomba = f.Unsafe.GetPointer<Goomba>(goombaEntity);
            var goombaTransform = f.Get<Transform2D>(goombaEntity);
            var goombaEnemy = f.Unsafe.GetPointer<Enemy>(goombaEntity);
            var mario = f.Unsafe.GetPointer<MarioPlayer>(marioEntity);
            var marioTransform = f.Get<Transform2D>(marioEntity);
            var marioPhysicsObject = f.Unsafe.GetPointer<PhysicsObject>(marioEntity);

            QuantumUtils.UnwrapWorldLocations(f, goombaTransform.Position + FPVector2.Up * FP._0_10, marioTransform.Position, out FPVector2 ourPos, out FPVector2 theirPos);
            FPVector2 damageDirection = (theirPos - ourPos).Normalized;
            bool attackedFromAbove = FPVector2.Dot(damageDirection, FPVector2.Up) > FP._0_25;

            bool groundpounded = attackedFromAbove && mario->IsGroundpoundActive && mario->CurrentPowerupState != PowerupState.MiniMushroom;
            if (mario->InstakillsEnemies(*marioPhysicsObject, true) || groundpounded) {
                goomba->Kill(f, goombaEntity, marioEntity, true);
                mario->DoEntityBounce |= mario->IsDrilling;
                return;
            }

            if (attackedFromAbove) {
                if (mario->CurrentPowerupState == PowerupState.MiniMushroom) {
                    if (mario->IsGroundpounding) {
                        mario->IsGroundpounding = false;
                        goomba->Kill(f, goombaEntity, marioEntity, false);
                    }
                    mario->DoEntityBounce = true;
                } else {
                    goomba->Kill(f, goombaEntity, marioEntity, false);
                    mario->DoEntityBounce = !mario->IsGroundpounding;
                }

                mario->IsDrilling = false;

            } else if (mario->IsCrouchedInShell) {
                mario->FacingRight = damageDirection.X < 0;
                marioPhysicsObject->Velocity.X = 0;

            } else if (mario->IsDamageable) {
                mario->Powerdown(f, marioEntity, false);
                goombaEnemy->FacingRight = damageDirection.X > 0;
            }
        }

        public void OnGoombaProjectileInteraction(Frame f, EntityRef goombaEntity, EntityRef projectileEntity) {
            var projectileAsset = f.FindAsset(f.Get<Projectile>(projectileEntity).Asset);

            switch (projectileAsset.Effect) {
            case ProjectileEffectType.Knockback: {
                f.Unsafe.GetPointer<Goomba>(goombaEntity)->Kill(f, goombaEntity, projectileEntity, true);
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

        public void OnEntityBumped(Frame f, EntityRef entity, FPVector2 position, EntityRef bumpOwner) {
            if (!f.Unsafe.TryGetPointer(entity, out Goomba* goomba)
                || !f.TryGet(entity, out Enemy enemy)
                || !enemy.IsAlive) {
                return;
            }

            goomba->Kill(f, entity, bumpOwner, true);
        }

        public void OnBobombExplodeEntity(Frame f, EntityRef bobomb, EntityRef entity) {
            if (f.Unsafe.TryGetPointer(entity, out Goomba* goomba)) {
                goomba->Kill(f, entity, bobomb, true);
            }
        }
    }
}