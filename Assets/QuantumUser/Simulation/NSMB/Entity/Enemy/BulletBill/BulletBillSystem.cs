using Photon.Deterministic;

namespace Quantum {
    public unsafe class BulletBillSystem : SystemMainThread, ISignalOnBobombExplodeEntity, ISignalOnComponentRemoved<BulletBill> {

        public override void OnInit(Frame f) {
            InteractionSystem.RegisterInteraction<BulletBill, MarioPlayer>(OnBulletBillMarioInteraction);
            InteractionSystem.RegisterInteraction<BulletBill, Projectile>(OnBulletBillProjectileInteraction);
        }

        public override void Update(Frame f) {
            var stage = f.FindAsset<VersusStageData>(f.Map.UserAsset);

            var launchers = f.Filter<BulletBillLauncher, BreakableObject, PhysicsCollider2D, Transform2D>();
            while (launchers.NextUnsafe(out EntityRef entity, out BulletBillLauncher* launcher, out BreakableObject* breakable, out PhysicsCollider2D* collider, out Transform2D* transform)) {
                if (breakable->IsBroken) {
                    continue;
                }
                if (launcher->BulletBillCount >= 3) {
                    continue;
                }
                if (!QuantumUtils.Decrement(ref launcher->TimeToShootFrames)) {
                    continue;
                }
                launcher->TimeToShootFrames = launcher->TimeToShoot;

                FPVector2 spawnpoint = transform->Position + FPVector2.Up * (collider->Shape.Box.Extents.Y * 2) + (FPVector2.Down * FP.FromString("0.45"));
                var allPlayers = f.Filter<MarioPlayer, Transform2D>();
                FP absDistance = 0;
                FP minDistance = FP.MaxValue;
                while (allPlayers.Next(out _, out _, out Transform2D marioTransform)) {
                    QuantumUtils.WrappedDistance(stage, spawnpoint, marioTransform.Position, out FP distance);
                    FP abs = FPMath.Abs(distance);
                    if (abs >= launcher->MinimumShootRadius && abs < minDistance) {
                        absDistance = abs;
                        minDistance = distance;
                    }
                }

                if (FPMath.Abs(minDistance) > launcher->MaximumShootRadius) {
                    continue;
                }

                // Attempt a shot
                bool right = minDistance < 0;

                EntityRef newBillEntity = f.Create(launcher->BulletBillPrototype);
                var newBill = f.Unsafe.GetPointer<BulletBill>(newBillEntity);
                var newBillTransform = f.Unsafe.GetPointer<Transform2D>(newBillEntity);
                newBill->Initialize(f, newBillEntity, entity, right);
                newBillTransform->Position = spawnpoint;

                launcher->BulletBillCount++;
            }

            var bulletBills = f.Filter<BulletBill, Transform2D, Enemy, PhysicsObject>();
            while (bulletBills.NextUnsafe(out EntityRef entity, out BulletBill* bulletBill, out Transform2D* transform, out Enemy* enemy, out PhysicsObject* physicsObject)) {
                if (!enemy->IsAlive) {
                    if (bulletBill->DespawnFrames == 0) {
                        bulletBill->DespawnFrames = 255;
                    }

                    if (QuantumUtils.Decrement(ref bulletBill->DespawnFrames)) {
                        f.Destroy(entity);
                    }
                    continue;
                }

                physicsObject->DisableCollision = true;
                physicsObject->Velocity.X = bulletBill->Speed * (enemy->FacingRight ? 1 : -1);

                DespawnCheck(f, entity, transform, bulletBill, stage);
            }
        }

        public void DespawnCheck(Frame f, EntityRef entity, Transform2D* transform, BulletBill* bulletBill, VersusStageData stage) {
            var allPlayers = f.Filter<MarioPlayer, Transform2D>();
            while (allPlayers.Next(out _, out _, out Transform2D marioTransform)) {
                QuantumUtils.WrappedDistance(stage, transform->Position, marioTransform.Position, out FP distance);
                if (FPMath.Abs(distance) < bulletBill->DespawnRadius) {
                    return;
                }
            }

            // Do despawn
            f.Destroy(entity);
        }

        public void OnBulletBillMarioInteraction(Frame f, EntityRef bulletBillEntity, EntityRef marioEntity) {
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
                bulletBill->Kill(f, bulletBillEntity, marioEntity, true);
                mario->DoEntityBounce |= mario->IsDrilling;
                return;
            }

            if (attackedFromAbove) {
                if (mario->CurrentPowerupState == PowerupState.MiniMushroom) {
                    if (mario->IsGroundpounding) {
                        mario->IsGroundpounding = false;
                        bulletBill->Kill(f, bulletBillEntity, marioEntity, false);
                    }
                    mario->DoEntityBounce = true;
                } else {
                    bulletBill->Kill(f, bulletBillEntity, marioEntity, false);
                    mario->DoEntityBounce = !mario->IsGroundpounding;
                }

                mario->IsDrilling = false;

            } else if (!mario->IsCrouchedInShell && mario->IsDamageable) {
                mario->Powerdown(f, marioEntity, false);
            }
        }

        public void OnBulletBillProjectileInteraction(Frame f, EntityRef bulletBillEntity, EntityRef projectileEntity) {
            var projectileAsset = f.FindAsset(f.Unsafe.GetPointer<Projectile>(projectileEntity)->Asset);

            if (projectileAsset.Effect == ProjectileEffectType.Freeze) {
                IceBlockSystem.Freeze(f, bulletBillEntity, true);
            }

            if (projectileAsset.DestroyOnHit) {
                ProjectileSystem.Destroy(f, projectileEntity, projectileAsset.DestroyParticleEffect);
            }
        }

        public void OnBobombExplodeEntity(Frame f, EntityRef bobomb, EntityRef entity) {
            if (f.Unsafe.TryGetPointer(entity, out BulletBill* bulletBill)) {
                bulletBill->Kill(f, entity, bobomb, false);
            }
        }

        public void OnRemoved(Frame f, EntityRef entity, BulletBill* component) {
            if (f.Unsafe.TryGetPointer(component->Owner, out BulletBillLauncher* launcher)) {
                launcher->BulletBillCount--;
            }
        }
    }
}
