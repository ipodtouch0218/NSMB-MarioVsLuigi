using Photon.Deterministic;
using UnityEngine;

namespace Quantum {
    public unsafe class BulletBillSystem : SystemMainThread, ISignalOnBobombExplodeEntity, ISignalOnComponentRemoved<BulletBill>, ISignalOnIceBlockBroken {

        private static readonly FPVector2 SpawnOffset = new FPVector2(0, FP.FromString("-0.45"));

        public override void OnInit(Frame f) {
            f.Context.Interactions.Register<BulletBill, MarioPlayer>(f, OnBulletBillMarioInteraction);
            f.Context.Interactions.Register<BulletBill, Projectile>(f, OnBulletBillProjectileInteraction);
            f.Context.Interactions.Register<BulletBill, IceBlock>(f, OnBulletBillIceBlockInteraction);
        }

        public override void Update(Frame f) {
            VersusStageData stage = null;

            var launchers = f.Filter<BulletBillLauncher, BreakableObject, PhysicsCollider2D, Transform2D>();
            while (launchers.NextUnsafe(out EntityRef entity, out var launcher, out var breakable, out var collider, out var transform)) {
                if (breakable->IsBroken) {
                    continue;
                }
                if (launcher->BulletBillCount >= 3) {
                    continue;
                }

                FPVector2 spawnpoint = transform->Position + FPVector2.Up * (collider->Shape.Box.Extents.Y * 2) + SpawnOffset;
                var allPlayers = f.Filter<MarioPlayer, Transform2D>();
                FP smallestDistance = FP.UseableMax;
                bool tooClose = false;
                while (allPlayers.NextUnsafe(out _, out _, out Transform2D* marioTransform)) {
                    if (stage == null) {
                        stage = f.FindAsset<VersusStageData>(f.Map.UserAsset);
                    }
                    QuantumUtils.WrappedDistance(stage, spawnpoint, marioTransform->Position, out FP distance);
                    FP abs = FPMath.Abs(distance);

                    // Player is too close
                    if (abs < launcher->MinimumShootRadius) {
                        smallestDistance = FP.UseableMax;
                        tooClose = true;
                        break;
                    }

                    if (abs < FPMath.Abs(smallestDistance)) {
                        smallestDistance = distance;
                    }
                }

                if (FPMath.Abs(smallestDistance) > launcher->MaximumShootRadius) {
                    if (!tooClose) {
                        launcher->TimeToShootFrames = launcher->TimeToShoot;
                    }
                    continue;
                }

                if (QuantumUtils.Decrement(ref launcher->TimeToShootFrames)) {
                    // Attempt a shot
                    bool right = smallestDistance < 0;

                    EntityRef newBillEntity = f.Create(launcher->BulletBillPrototype);
                    var newBill = f.Unsafe.GetPointer<BulletBill>(newBillEntity);
                    var newBillTransform = f.Unsafe.GetPointer<Transform2D>(newBillEntity);
                    newBill->Initialize(f, newBillEntity, entity, right);
                    newBillTransform->Position = spawnpoint;

                    launcher->BulletBillCount++;
                    launcher->TimeToShootFrames = launcher->TimeToShoot;

                    f.Events.BulletBillLauncherShoot(entity, newBillEntity, right);
                }
            }

            var bulletBills = f.Filter<BulletBill, Transform2D, Enemy, PhysicsObject, Freezable>();
            while (bulletBills.NextUnsafe(out EntityRef entity, out var bulletBill, out var transform, out var enemy, out var physicsObject, out var freezable)) {
                if (!enemy->IsAlive) {
                    if (bulletBill->DespawnFrames == 0) {
                        bulletBill->DespawnFrames = 255;
                    }

                    if (QuantumUtils.Decrement(ref bulletBill->DespawnFrames)) {
                        f.Destroy(entity);
                    }
                    continue;
                }

                if (freezable->IsFrozen(f)) {
                    continue;
                }

                physicsObject->DisableCollision = true;
                physicsObject->Velocity.X = bulletBill->Speed * (enemy->FacingRight ? 1 : -1);

                DespawnCheck(f, entity, transform, bulletBill, stage);
            }
        }

        public void DespawnCheck(Frame f, EntityRef entity, Transform2D* transform, BulletBill* bulletBill, VersusStageData stage) {
            var allPlayers = f.Filter<MarioPlayer, Transform2D>();
            while (allPlayers.NextUnsafe(out _, out _, out Transform2D* marioTransform)) {
                QuantumUtils.WrappedDistance(stage, transform->Position, marioTransform->Position, out FP distance);
                if (FPMath.Abs(distance) < bulletBill->DespawnRadius) {
                    return;
                }
            }

            // Do despawn
            f.Destroy(entity);
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
            
            if (mario->InstakillsEnemies(marioPhysicsObject, true) || groundpounded) {
                bulletBill->Kill(f, bulletBillEntity, marioEntity, groundpounded ? KillReason.Groundpounded : KillReason.Special);
                mario->DoEntityBounce |= mario->IsDrilling;
                return;
            }

            if (attackedFromAbove) {
                if (mario->CurrentPowerupState == PowerupState.MiniMushroom) {
                    if (mario->IsGroundpounding) {
                        mario->IsGroundpounding = false;
                        bulletBill->Kill(f, bulletBillEntity, marioEntity, KillReason.Normal);
                    }
                    mario->DoEntityBounce = true;
                } else {
                    bulletBill->Kill(f, bulletBillEntity, marioEntity, KillReason.Normal);
                    mario->DoEntityBounce = !mario->IsGroundpounding;
                }

                mario->IsDrilling = false;

            } else if (!mario->IsCrouchedInShell && mario->IsDamageable) {
                mario->Powerdown(f, marioEntity, false, bulletBillEntity);
            }
        }

        public static bool OnBulletBillIceBlockInteraction(Frame f, EntityRef bulletBillEntity, EntityRef iceBlockEntity, PhysicsContact contact) {
            var bulletBill = f.Unsafe.GetPointer<BulletBill>(bulletBillEntity);
            var iceBlock = f.Unsafe.GetPointer<IceBlock>(iceBlockEntity);

            FP upDot = FPVector2.Dot(contact.Normal, FPVector2.Up);
            if (iceBlock->IsSliding
                && upDot < PhysicsObjectSystem.GroundMaxAngle) {

                bulletBill->Kill(f, bulletBillEntity, iceBlockEntity, KillReason.Special);
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
                f.Unsafe.GetPointer<BulletBill>(bulletBillEntity)->Kill(f, bulletBillEntity, projectileEntity, KillReason.Special);
            }

            f.Signals.OnProjectileHitEntity(f, projectileEntity, bulletBillEntity);
        }
        #endregion

        #region Signals
        public void OnBobombExplodeEntity(Frame f, EntityRef bobomb, EntityRef entity) {
            if (f.Unsafe.TryGetPointer(entity, out BulletBill* bulletBill)) {
                bulletBill->Kill(f, entity, bobomb, KillReason.Special);
            }
        }

        public void OnRemoved(Frame f, EntityRef entity, BulletBill* component) {
            if (f.Unsafe.TryGetPointer(component->Owner, out BulletBillLauncher* launcher)) {
                launcher->BulletBillCount--;
            }
        }

        public void OnIceBlockBroken(Frame f, EntityRef brokenIceBlock, IceBlockBreakReason breakReason) {
            var iceBlock = f.Unsafe.GetPointer<IceBlock>(brokenIceBlock);
            if (f.Unsafe.TryGetPointer(iceBlock->Entity, out BulletBill* bulletBill)) {
                bulletBill->Kill(f, iceBlock->Entity, brokenIceBlock, KillReason.Special);
                f.Events.PlayComboSound(iceBlock->Entity, 0);
            }
        }
        #endregion
    }
}
