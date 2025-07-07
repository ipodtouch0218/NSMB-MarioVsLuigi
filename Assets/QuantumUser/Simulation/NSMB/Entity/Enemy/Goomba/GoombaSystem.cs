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

            bool groundpounded = attackedFromAbove && mario->IsGroundpoundActive && mario->CurrentPowerupState != PowerupState.MiniMushroom;
            if (mario->InstakillsEnemies(marioPhysicsObject, true) || groundpounded) {
                goomba->Kill(f, goombaEntity, marioEntity, groundpounded ? KillReason.Groundpounded : KillReason.Special);
                mario->DoEntityBounce |= mario->IsDrilling;
                return;
            }

            if (attackedFromAbove) {
                if (mario->CurrentPowerupState == PowerupState.MiniMushroom) {
                    if (mario->IsGroundpounding) {
                        mario->IsGroundpounding = false;
                        goomba->Kill(f, goombaEntity, marioEntity, KillReason.Normal);
                    }
                    mario->DoEntityBounce = true;
                } else {
                    goomba->Kill(f, goombaEntity, marioEntity, KillReason.Normal);
                    mario->DoEntityBounce = !mario->IsGroundpounding;
                }

                mario->IsDrilling = false;

            } else if (mario->IsCrouchedInShell) {
                mario->FacingRight = damageDirection.X < 0;
                marioPhysicsObject->Velocity.X = 0;

            } else if (mario->IsDamageable) {
                mario->Powerdown(f, marioEntity, false, goombaEntity);
                goombaEnemy->ChangeFacingRight(f, goombaEntity, damageDirection.X > 0);
            }
        }

        public static bool OnGoombaIceBlockInteraction(Frame f, EntityRef goombaEntity, EntityRef iceBlockEntity, PhysicsContact contact) {
            var goomba = f.Unsafe.GetPointer<Goomba>(goombaEntity);
            var iceBlock = f.Unsafe.GetPointer<IceBlock>(iceBlockEntity);

            FP upDot = FPVector2.Dot(contact.Normal, FPVector2.Up);
            if (iceBlock->IsSliding
                && upDot < PhysicsObjectSystem.GroundMaxAngle) {

                goomba->Kill(f, goombaEntity, iceBlockEntity, KillReason.Special);
            }
            return false;
        }

        public static void OnGoombaProjectileInteraction(Frame f, EntityRef goombaEntity, EntityRef projectileEntity) {
            var projectileAsset = f.FindAsset(f.Unsafe.GetPointer<Projectile>(projectileEntity)->Asset);

            switch (projectileAsset.Effect) {
            case ProjectileEffectType.KillEnemiesAndSoftKnockbackPlayers:
            case ProjectileEffectType.Fire: {
                f.Unsafe.GetPointer<Goomba>(goombaEntity)->Kill(f, goombaEntity, projectileEntity, KillReason.Special);
                break;
            }
            case ProjectileEffectType.Freeze: {
                IceBlockSystem.Freeze(f, goombaEntity);
                break;
            }
            }

            f.Signals.OnProjectileHitEntity(f, projectileEntity, goombaEntity);
        }
        #endregion

        #region Signals
        public void OnEntityBumped(Frame f, EntityRef entity, FPVector2 position, EntityRef bumpOwner, QBoolean fromBelow) {
            if (!f.Unsafe.TryGetPointer(entity, out Goomba* goomba)
                || !f.Unsafe.TryGetPointer(entity, out Enemy* enemy)
                || !enemy->IsAlive) {
                return;
            }

            goomba->Kill(f, entity, bumpOwner, KillReason.Special);
        }

        public void OnBobombExplodeEntity(Frame f, EntityRef bobomb, EntityRef entity) {
            if (f.Unsafe.TryGetPointer(entity, out Goomba* goomba)) {
                goomba->Kill(f, entity, bobomb, KillReason.Special);
            }
        }

        public void OnIceBlockBroken(Frame f, EntityRef brokenIceBlock, IceBlockBreakReason breakReason) {
            var iceBlock = f.Unsafe.GetPointer<IceBlock>(brokenIceBlock);
            if (f.Unsafe.TryGetPointer(iceBlock->Entity, out Goomba* goomba)) {
                goomba->Kill(f, iceBlock->Entity, brokenIceBlock, KillReason.Special);
            }
        }

        public void OnEnemyKilledByStageReset(Frame f, EntityRef entity) {
            if (f.Unsafe.TryGetPointer(entity, out Goomba* goomba)) {
                goomba->Kill(f, entity, EntityRef.None, KillReason.InWall);
            }
        }

        public void OnEntityCrushed(Frame f, EntityRef entity) {
            if (f.Unsafe.TryGetPointer(entity, out Goomba* goomba)) {
                goomba->Kill(f, entity, EntityRef.None, KillReason.InWall);
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