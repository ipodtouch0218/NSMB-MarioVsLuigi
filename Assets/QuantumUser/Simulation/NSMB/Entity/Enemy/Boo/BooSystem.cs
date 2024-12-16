using Photon.Deterministic;

namespace Quantum {
    public unsafe class BooSystem : SystemMainThreadFilterStage<BooSystem.Filter>, ISignalOnEnemyRespawned, ISignalOnBobombExplodeEntity {
        private const byte BooUnscaredFrames = 12;

        public struct Filter {
            public EntityRef Entity;
            public Transform2D* Transform;
            public Boo* Boo;
            public Enemy* Enemy;
            public PhysicsObject* PhysicsObject;
        }

        public override void OnInit(Frame f) {
            f.Context.RegisterInteraction<Boo, MarioPlayer>(OnBooMarioPlayerInteraction);
            f.Context.RegisterInteraction<Boo, Projectile>(OnBooProjectileInteraction);
        }

        public override void Update(Frame f, ref Filter filter, VersusStageData stage) {
            var enemy = filter.Enemy;

            if (!enemy->IsAlive) {
                return;
            }

            var boo = filter.Boo;
            var physicsObject = filter.PhysicsObject;

            // Avoid jittery movement (and some lag!): change targets less frequently
            if ((f.Number + filter.Entity.Index) % 5 == 0) {
                boo->CurrentTarget = FindClosestPlayer(f, ref filter, stage);
            }

            // Don't move if we don't have a target.
            if (!f.Unsafe.TryGetPointer(boo->CurrentTarget, out MarioPlayer* targetMario)
                || !f.Unsafe.TryGetPointer(boo->CurrentTarget, out Transform2D* targetMarioTransform)
                || !f.Unsafe.TryGetPointer(boo->CurrentTarget, out PhysicsCollider2D* targetMarioCollider)) {
                boo->UnscaredFrames = BooUnscaredFrames;
                physicsObject->Velocity = FPVector2.Zero;
                return;
            }

            // Check if we're gonna become scared
            QuantumUtils.UnwrapWorldLocations(stage, filter.Transform->Position, targetMarioTransform->Position + targetMarioCollider->Shape.Centroid, out FPVector2 ourPosition, out FPVector2 marioPosition);
            bool targetOnRight = ourPosition.X < marioPosition.X;
            bool beingLookedAt = targetMario->FacingRight != targetOnRight;

            if (beingLookedAt) {
                // Become scared
                boo->UnscaredFrames = BooUnscaredFrames;
            } else {
                // Become unscared
                if (boo->UnscaredFrames > 0 && QuantumUtils.Decrement(ref boo->UnscaredFrames)) {
                    f.Events.BooBecomeActive(f, filter.Entity);
                }
            }

            bool scared = boo->UnscaredFrames > 0;
            if (scared) {
                physicsObject->Velocity = FPVector2.Zero;
            } else {
                // Target player
                enemy->ChangeFacingRight(f, filter.Entity, targetOnRight);

                FPVector2 directionToTarget = (marioPosition - ourPosition).Normalized;
                FPVector2 newVelocity;

                if (physicsObject->Velocity == FPVector2.Zero) {
                    // Initial movement
                    newVelocity = 2 * f.DeltaTime * directionToTarget;
                } else {
                    // Max angle: -45deg - 45deg
                    FP currentAngle = FPVector2.RadiansSigned(physicsObject->Velocity, FPVector2.Right) * FP.Rad2Deg;
                    FP targetAngle = FPVector2.RadiansSigned(directionToTarget, FPVector2.Right) * FP.Rad2Deg;
                    bool left = targetAngle > 90 || targetAngle < -90;
                    if (left) {
                        targetAngle = FPMath.Repeat(targetAngle, 360);
                        targetAngle = FPMath.Clamp(targetAngle, 135, 225);
                    } else {
                        targetAngle = FPMath.Clamp(targetAngle, -45, 45);
                    }
                    FP newAngle = QuantumUtils.MoveTowardsAngle(currentAngle, targetAngle, 90 * f.DeltaTime);

                    // Max speed: 1.5; acceleration = 2/
                    FP currentMagnitude = physicsObject->Velocity.Magnitude;
                    FP newMagnitude = FPMath.Min(currentMagnitude + (2 * f.DeltaTime), FP._1_50);

                    FPVector2 newDirection = (FPQuaternion.AngleAxis(-newAngle, FPVector3.Forward) * FPVector3.Right).XY;
                    newVelocity = newDirection * newMagnitude;
                }

                physicsObject->Velocity = newVelocity;
            }
        }

        public EntityRef FindClosestPlayer(Frame f, ref Filter filter, VersusStageData stage) {
            var allPlayers = f.Filter<MarioPlayer, Transform2D>();
            allPlayers.UseCulling = false;
            var boo = filter.Boo;
            var booPosition = filter.Transform->Position;

            FP closestDistance = boo->MaxRange;
            EntityRef closestPlayer = EntityRef.None;
            while (allPlayers.NextUnsafe(out EntityRef marioEntity, out MarioPlayer* mario, out Transform2D* marioTransform)) {
                if (mario->IsDead) {
                    continue;
                }

                FP newDistance = QuantumUtils.WrappedDistance(stage, booPosition, marioTransform->Position);

                if (newDistance <= closestDistance) {
                    closestPlayer = marioEntity;
                    closestDistance = newDistance;
                }
            }
            return closestPlayer;
        }

        public void OnBooMarioPlayerInteraction(Frame f, EntityRef booEntity, EntityRef marioEntity) {
            var mario = f.Unsafe.GetPointer<MarioPlayer>(marioEntity);
            var marioPhysicsObject = f.Unsafe.GetPointer<PhysicsObject>(marioEntity);

            if (mario->InstakillsEnemies(marioPhysicsObject, false)) {
                var boo = f.Unsafe.GetPointer<Boo>(booEntity);
                boo->Kill(f, booEntity, marioEntity, true);
            } else {
                mario->Powerdown(f, marioEntity, false);
            }
        }

        public void OnBooProjectileInteraction(Frame f, EntityRef booEntity, EntityRef projectileEntity) {
            var projectileAsset = f.FindAsset(f.Unsafe.GetPointer<Projectile>(projectileEntity)->Asset);

            if (projectileAsset.DestroyOnHit) {
                ProjectileSystem.Destroy(f, projectileEntity, projectileAsset.DestroyParticleEffect);
            }
        }

        public void OnEnemyRespawned(Frame f, EntityRef entity) {
            if (f.Unsafe.TryGetPointer(entity, out Boo* boo)) {
                boo->Respawn(f, entity);
            }
        }

        public void OnBobombExplodeEntity(Frame f, EntityRef bobomb, EntityRef entity) {
            if (f.Unsafe.TryGetPointer(entity, out Boo* boo)) {
                boo->Kill(f, entity, bobomb, true);
            }
        }
    }
}