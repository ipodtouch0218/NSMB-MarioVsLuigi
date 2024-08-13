using Photon.Deterministic;

namespace Quantum {
    public unsafe class BulletBillSystem : SystemMainThread, ISignalOnBobombExplodeEntity, ISignalOnComponentRemoved<BulletBill> {

        public override void OnInit(Frame f) {
            EnemySystem.RegisterInteraction<BulletBill, MarioPlayer>(OnBulletBillMarioInteraction);
        }

        public override void Update(Frame f) {
            var stage = f.FindAsset<VersusStageData>(f.Map.UserAsset);

            var launchers = f.Filter<BulletBillLauncher, Transform2D>();
            while (launchers.NextUnsafe(out EntityRef entity, out BulletBillLauncher* launcher, out Transform2D* transform)) {
                if (launcher->BulletBillCount >= 3) {
                    continue;
                }

                if (!QuantumUtils.Decrement(ref launcher->TimeToShootFrames)) {
                    continue;
                }
                launcher->TimeToShootFrames = launcher->TimeToShoot;

                var allPlayers = f.Filter<MarioPlayer, Transform2D>();
                FP absDistance = 0;
                FP minDistance = FP.MaxValue;
                while (allPlayers.Next(out _, out _, out Transform2D marioTransform)) {
                    QuantumUtils.WrappedDistance(stage, transform->Position, marioTransform.Position, out FP xDifference);
                    FP abs = FPMath.Abs(xDifference);
                    if (abs >= launcher->MinimumShootRadius && abs < minDistance) {
                        absDistance = abs;
                        minDistance = xDifference;
                    }
                }

                if (FPMath.Abs(minDistance) > launcher->MaximumShootRadius) {
                    continue;
                }

                // Attempt a shot
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
                FP distance = QuantumUtils.WrappedDistance(stage, transform->Position, marioTransform.Position);
                if (distance < bulletBill->DespawnRadius) {
                    return;
                }
            }

            // Do despawn
            f.Destroy(entity);
        }

        public void OnBulletBillMarioInteraction(Frame f, EntityRef bulletBillEntity, EntityRef marioEntity) {
            var bulletBill = f.Unsafe.GetPointer<Goomba>(bulletBillEntity);
            var bulletBillTransform = f.Get<Transform2D>(bulletBillEntity);
            var bulletBillEnemy = f.Unsafe.GetPointer<Enemy>(bulletBillEntity);
            var mario = f.Unsafe.GetPointer<MarioPlayer>(marioEntity);
            var marioTransform = f.Get<Transform2D>(marioEntity);
            var marioPhysicsObject = f.Unsafe.GetPointer<PhysicsObject>(marioEntity);

            QuantumUtils.UnwrapWorldLocations(f, bulletBillTransform.Position + FPVector2.Up * FP._0_10, marioTransform.Position, out FPVector2 ourPos, out FPVector2 theirPos);
            FPVector2 damageDirection = (theirPos - ourPos).Normalized;
            bool attackedFromAbove = FPVector2.Dot(damageDirection, FPVector2.Up) > FP._0_25;
            bool groundpounded = attackedFromAbove && mario->IsGroundpoundActive && mario->CurrentPowerupState != PowerupState.MiniMushroom;
            
            if (mario->InstakillsEnemies(*marioPhysicsObject) || groundpounded) {
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
