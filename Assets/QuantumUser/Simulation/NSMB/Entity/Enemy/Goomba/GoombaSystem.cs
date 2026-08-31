using Photon.Deterministic;

namespace Quantum {
    public unsafe class GoombaSystem : SystemMainThreadEntityFilter<Goomba, GoombaSystem.Filter>, ISignalOnEntityBumped, ISignalOnBobombExplodeEntity,
        ISignalOnIceBlockBroken, ISignalOnEnemyKilledByStageReset, ISignalOnEntityCrushed, ISignalOnEnemyRespawned {

        public struct Filter {
			public EntityRef Entity;
			public Transform2D* Transform;
            public Enemy* Enemy;
			public Goomba* Goomba;
			public PhysicsObject* PhysicsObject;
            public PhysicsCollider2D* Collider;
            public Freezable* Freezable;
		}

        public override void OnInit(Frame f) {
            f.Context.Interactions.Register<Goomba, Goomba>(f, OnGoombaGoombaInteraction);
            f.Context.Interactions.Register<Goomba, PiranhaPlant>(f, EnemySystem.EnemyBumpTurnaroundOnlyFirst);
            f.Context.Interactions.Register<Goomba, MarioPlayer>(f, OnGoombaMarioInteraction);
            f.Context.Interactions.Register<Goomba, Projectile>(f, OnGoombaProjectileInteraction);
            f.Context.Interactions.Register<Goomba, IceBlock>(f, OnGoombaIceBlockInteraction);
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
            if (!enemy->IsAlive
                || filter.Freezable->IsFrozen(f)) {
                return;
            }

            // Turn around when hitting a wall.
            if (physicsObject->IsTouchingLeftWall || physicsObject->IsTouchingRightWall) {
                enemy->ChangeFacingRight(f, filter.Entity, physicsObject->IsTouchingLeftWall);
            }

            // Move
            physicsObject->Velocity.X = goomba->Speed * (enemy->FacingRight ? 1 : -1);
        }

        #region Interactions
        public static void OnGoombaGoombaInteraction(Frame f, EntityRef goombaEntityA, EntityRef goombaEntityB) {
            EnemySystem.EnemyBumpTurnaround(f, goombaEntityA, goombaEntityB);
        }

        public static void OnGoombaMarioInteraction(Frame f, EntityRef goombaEntity, EntityRef marioEntity) {
            var goomba = f.Unsafe.GetPointer<Goomba>(goombaEntity);
            var goombaTransform = f.Unsafe.GetPointer<Transform2D>(goombaEntity);
            var goombaEnemy = f.Unsafe.GetPointer<Enemy>(goombaEntity);
            var mario = f.Unsafe.GetPointer<MarioPlayer>(marioEntity);
            var marioTransform = f.Unsafe.GetPointer<Transform2D>(marioEntity);
            var marioPhysicsObject = f.Unsafe.GetPointer<PhysicsObject>(marioEntity);

            QuantumUtils.UnwrapWorldLocations(f, goombaTransform->Position + FPVector2.Up * FP._0_10, marioTransform->Position, out FPVector2 ourPos, out FPVector2 theirPos);
            FPVector2 damageDirection = (theirPos - ourPos).Normalized;
            bool attackedFromAbove = FPVector2.Dot(damageDirection, FPVector2.Up) > FP._0_25;

            bool instakills = mario->InstakillsEnemies(marioPhysicsObject, true);
            bool groundpounded = !instakills && attackedFromAbove && mario->IsGroundpoundActive && mario->CurrentPowerupState != PowerupState.MiniMushroom;
            if (instakills || groundpounded) {
                goomba->Kill(f, goombaEntity, marioEntity, groundpounded ? EnemyKillReason.Groundpounded : EnemyKillReason.Special);
                mario->DoEntityBounce |= !instakills && mario->IsDrilling;
                return;
            }

            if (attackedFromAbove) {
                if (mario->CurrentPowerupState == PowerupState.MiniMushroom) {
                    if (mario->IsGroundpounding) {
                        mario->IsGroundpounding = false;
                        goomba->Kill(f, goombaEntity, marioEntity, EnemyKillReason.Normal);
                    }
                    mario->DoEntityBounce = true;
                } else {
                    goomba->Kill(f, goombaEntity, marioEntity, EnemyKillReason.Normal);
                    mario->DoEntityBounce = !mario->IsGroundpounding;
                }

                mario->IsDrilling = false;

            } else if (mario->IsCrouchedInShell) {
                marioPhysicsObject->Velocity.X = 0;
                goombaEnemy->ChangeFacingRight(f, goombaEntity, ourPos.X > theirPos.X);

            } else if (mario->IsDamageable(f) && goombaEnemy->IntangibilityFrames == 0) {
                mario->Powerdown(f, marioEntity, false, goombaEntity);
                goombaEnemy->ChangeFacingRight(f, goombaEntity, damageDirection.X > 0);
            }
        }

        public static bool OnGoombaIceBlockInteraction(Frame f, EntityRef goombaEntity, EntityRef iceBlockEntity, PhysicsContact contact) {
            var goomba = f.Unsafe.GetPointer<Goomba>(goombaEntity);
            var iceBlock = f.Unsafe.GetPointer<IceBlock>(iceBlockEntity);

            FP upDot = FPVector2.Dot(contact.Normal, FPVector2.Up);
            if (iceBlock->IsSliding
                && upDot < Constants.PhysicsGroundMaxAngleCos) {

                goomba->Kill(f, goombaEntity, iceBlockEntity, EnemyKillReason.Special);
            }
            return false;
        }

        public static void OnGoombaProjectileInteraction(Frame f, EntityRef goombaEntity, EntityRef projectileEntity) {
            var projectileAsset = f.FindAsset(f.Unsafe.GetPointer<Projectile>(projectileEntity)->Asset);

            switch (projectileAsset.Effect) {
            case ProjectileEffectType.Hammer:
            case ProjectileEffectType.Boomerang:
            case ProjectileEffectType.Fire: {
                f.Unsafe.GetPointer<Goomba>(goombaEntity)->Kill(f, goombaEntity, projectileEntity, EnemyKillReason.Special);
                break;
            }
            case ProjectileEffectType.Freeze: {
                IceBlockSystem.Freeze(f, goombaEntity);
                break;
            }
            }

            f.Signals.OnProjectileHitEntity(projectileEntity, goombaEntity);
        }
        #endregion

        #region Signals
        public void OnEntityBumped(Frame f, EntityRef entity, FPVector2 position, EntityRef bumpOwner, QBoolean fromBelow) {
            if (!f.Unsafe.TryGetPointer(entity, out Goomba* goomba)
                || !f.Unsafe.TryGetPointer(entity, out Enemy* enemy)
                || !enemy->IsAlive) {
                return;
            }

            goomba->Kill(f, entity, bumpOwner, EnemyKillReason.Special);
        }

        public void OnBobombExplodeEntity(Frame f, EntityRef bobomb, EntityRef entity) {
            if (f.Unsafe.TryGetPointer(entity, out Goomba* goomba)) {
                goomba->Kill(f, entity, bobomb, EnemyKillReason.Special);
            }
        }

        public void OnIceBlockBroken(Frame f, EntityRef brokenIceBlock, IceBlockBreakReason breakReason, EntityRef attacker) {
            var iceBlock = f.Unsafe.GetPointer<IceBlock>(brokenIceBlock);
            if (f.Unsafe.TryGetPointer(iceBlock->Entity, out Goomba* goomba)) {
                goomba->Kill(f, iceBlock->Entity, brokenIceBlock, EnemyKillReason.Special);
            }
        }

        public void OnEnemyKilledByStageReset(Frame f, EntityRef entity) {
            if (f.Unsafe.TryGetPointer(entity, out Goomba* goomba)) {
                goomba->Kill(f, entity, EntityRef.None, EnemyKillReason.InWall);
            }
        }

        public void OnEntityCrushed(Frame f, EntityRef entity) {
            if (f.Unsafe.TryGetPointer(entity, out Goomba* goomba)) {
                goomba->Kill(f, entity, EntityRef.None, EnemyKillReason.InWall);
            }
        }

        public void OnEnemyRespawned(Frame f, EntityRef entity) {
            if (f.Unsafe.TryGetPointer(entity, out Goomba* goomba)) {
                goomba->Respawn(f, entity);
            }
        }
        #endregion
    }
}