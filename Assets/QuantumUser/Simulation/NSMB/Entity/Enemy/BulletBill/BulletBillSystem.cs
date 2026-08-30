using Photon.Deterministic;

namespace Quantum {
    public unsafe class BulletBillSystem : SystemMainThreadEntityFilter<BulletBill, BulletBillSystem.Filter>, ISignalOnBobombExplodeEntity, ISignalOnIceBlockBroken {
        public struct Filter {
            public EntityRef Entity;
            public BulletBill* BulletBill;
            public Transform2D* Transform;
            public Enemy* Enemy;
            public PhysicsObject* PhysicsObject;
            public Freezable* Freezable;
        }

        public override void OnInit(Frame f) {
            f.Context.Interactions.Register<BulletBill, MarioPlayer>(f, OnBulletBillMarioInteraction);
            f.Context.Interactions.Register<BulletBill, Projectile>(f, OnBulletBillProjectileInteraction);
            f.Context.Interactions.Register<BulletBill, IceBlock>(f, OnBulletBillIceBlockInteraction);
        }

        public override void Update(Frame f, ref Filter filter, VersusStageData stage) {
            var enemy = filter.Enemy;
            var bulletBill = filter.BulletBill;

            if (!enemy->IsAlive) {
                if (bulletBill->DespawnFrames == 0) {
                    // Just died.
                    bulletBill->DespawnFrames = 255;
                }

                if (QuantumUtils.Decrement(ref bulletBill->DespawnFrames)) {
                    f.Destroy(filter.Entity);
                }
                return;
            }

            if (filter.Freezable->IsFrozen(f)) {
                return;
            }

            var physicsObject = filter.PhysicsObject;
            physicsObject->DisableCollision = true;
            physicsObject->Velocity.X = bulletBill->Speed * (enemy->FacingRight ? 1 : -1);

            DespawnCheck(f, ref filter, stage);
        }

        public void DespawnCheck(Frame f, ref Filter filter, VersusStageData stage) {
            var transform = filter.Transform;
            var bulletBill = filter.BulletBill;
            var allPlayers = f.Filter<MarioPlayer, Transform2D>();
            while (allPlayers.NextUnsafe(out _, out _, out Transform2D* marioTransform)) {
                QuantumUtils.WrappedDistance(stage, transform->Position, marioTransform->Position, out FP distance);
                if (FPMath.Abs(distance) < bulletBill->DespawnRadius) {
                    return;
                }
            }

            // Do despawn
            f.Destroy(filter.Entity);
        }

        #region Interactions
        public static void OnBulletBillMarioInteraction(Frame f, EntityRef bulletBillEntity, EntityRef marioEntity) {
            var bulletBill = f.Unsafe.GetPointer<BulletBill>(bulletBillEntity);
            var bulletBillTransform = f.Unsafe.GetPointer<Transform2D>(bulletBillEntity);
            var bulletBillEnemy = f.Unsafe.GetPointer<Enemy>(bulletBillEntity);
            var mario = f.Unsafe.GetPointer<MarioPlayer>(marioEntity);
            var marioTransform = f.Unsafe.GetPointer<Transform2D>(marioEntity);
            var marioPhysicsObject = f.Unsafe.GetPointer<PhysicsObject>(marioEntity);

            QuantumUtils.UnwrapWorldLocations(f, bulletBillTransform->Position + FPVector2.Up * FP._0_10, marioTransform->Position, out FPVector2 ourPos, out FPVector2 theirPos);
            FPVector2 damageDirection = (theirPos - ourPos).Normalized;
            bool attackedFromAbove = FPVector2.Dot(damageDirection, FPVector2.Up) > 0;
            bool groundpounded = attackedFromAbove && mario->IsGroundpoundActive && mario->CurrentPowerupState != PowerupState.MiniMushroom;

            bool instakills = mario->InstakillsEnemies(marioPhysicsObject, true);
            if (instakills || groundpounded) {
                bulletBill->Kill(f, bulletBillEntity, marioEntity, groundpounded ? EnemyKillReason.Groundpounded : EnemyKillReason.Special);
                mario->DoEntityBounce |= !instakills && mario->IsDrilling;
                return;
            }

            if (attackedFromAbove) {
                if (mario->CurrentPowerupState == PowerupState.MiniMushroom) {
                    if (mario->IsGroundpounding) {
                        mario->IsGroundpounding = false;
                        bulletBill->Kill(f, bulletBillEntity, marioEntity, EnemyKillReason.Normal);
                    }
                    mario->DoEntityBounce = true;
                } else {
                    bulletBill->Kill(f, bulletBillEntity, marioEntity, EnemyKillReason.Normal);
                    mario->DoEntityBounce = !mario->IsGroundpounding;
                }

                mario->IsDrilling = false;

            } else if (!mario->IsCrouchedInShell && mario->IsDamageable(f)) {
                mario->Powerdown(f, marioEntity, false, bulletBillEntity);
            }
        }

        public static bool OnBulletBillIceBlockInteraction(Frame f, EntityRef bulletBillEntity, EntityRef iceBlockEntity, PhysicsContact contact) {
            var bulletBill = f.Unsafe.GetPointer<BulletBill>(bulletBillEntity);
            var iceBlock = f.Unsafe.GetPointer<IceBlock>(iceBlockEntity);

            FP upDot = FPVector2.Dot(contact.Normal, FPVector2.Up);
            if (iceBlock->IsSliding
                && upDot < Constants.PhysicsGroundMaxAngleCos) {

                bulletBill->Kill(f, bulletBillEntity, iceBlockEntity, EnemyKillReason.Special);
            }
            return false;
        }

        public static void OnBulletBillProjectileInteraction(Frame f, EntityRef bulletBillEntity, EntityRef projectileEntity) {
            var projectileAsset = f.FindAsset(f.Unsafe.GetPointer<Projectile>(projectileEntity)->Asset);

            if (projectileAsset.Effect == ProjectileEffectType.Freeze) {
                IceBlockSystem.Freeze(f, bulletBillEntity, true);
            } else if (projectileAsset.Effect == ProjectileEffectType.Fire) {
                f.Events.BulletBillHitByProjectile(bulletBillEntity);
            } else {
                f.Unsafe.GetPointer<BulletBill>(bulletBillEntity)->Kill(f, bulletBillEntity, projectileEntity, EnemyKillReason.Special);
            }

            f.Signals.OnProjectileHitEntity(projectileEntity, bulletBillEntity);
        }
        #endregion

        #region Signals
        public void OnBobombExplodeEntity(Frame f, EntityRef bobomb, EntityRef entity) {
            if (f.Unsafe.TryGetPointer(entity, out BulletBill* bulletBill)) {
                bulletBill->Kill(f, entity, bobomb, EnemyKillReason.Special);
            }
        }
        public void OnIceBlockBroken(Frame f, EntityRef brokenIceBlock, IceBlockBreakReason breakReason, EntityRef attacker) {
            var iceBlock = f.Unsafe.GetPointer<IceBlock>(brokenIceBlock);
            if (f.Unsafe.TryGetPointer(iceBlock->Entity, out BulletBill* bulletBill)) {
                bulletBill->Kill(f, iceBlock->Entity, brokenIceBlock, EnemyKillReason.Special);
                f.Events.PlayComboSound(iceBlock->Entity, 0);
            }
        }
        #endregion
    }
}
