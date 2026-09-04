using Photon.Deterministic;

namespace Quantum {
    public unsafe partial struct Projectile {

        public void Initialize(Frame f, EntityRef thisEntity, EntityRef owner, FPVector2 spawnpoint, bool right) {
            var asset = f.FindAsset(Asset);
            var transform = f.Unsafe.GetPointer<Transform2D>(thisEntity);
            var physicsObject = f.Unsafe.GetPointer<PhysicsObject>(thisEntity);

            // Vars
            Owner = owner;
            FacingRight = right;

            // Speed
            Speed = asset.Speed;
            physicsObject->Gravity = asset.Gravity;
            if (asset.InheritShooterVelocity
                && f.Unsafe.TryGetPointer(owner, out PhysicsObject* ownerPhysicsObject)
                // Moving in same direction
                && FPMath.Sign(ownerPhysicsObject->Velocity.X) == 1 == FacingRight) { 

                Speed += FPMath.Abs(ownerPhysicsObject->Velocity.X / 3);
            }

            if (asset.LockTo45Degrees) {
                physicsObject->TerminalVelocity = -Speed;
            }

            // Physics
            transform->Position = spawnpoint;
            physicsObject->Velocity = new(Speed * (FacingRight ? 1 : -1), -Speed);
        }

        public void InitializeHammer(Frame f, EntityRef thisEntity, EntityRef owner, FPVector2 spawnpoint, bool right, bool playerHoldingUp) {
            var asset = f.FindAsset(Asset);
            var transform = f.Unsafe.GetPointer<Transform2D>(thisEntity);
            var physicsObject = f.Unsafe.GetPointer<PhysicsObject>(thisEntity);

            // Vars
            Owner = owner;
            FacingRight = right;

            // Initial Velocity
            FPVector2 velocity = playerHoldingUp ? new FPVector2(FP.FromString("3.8822"), FP.FromString("14.4888")) : new FPVector2(FP.FromString("6.25"), FP.FromString("7.5"));
            Speed = velocity.X;
            
            // Apply
            transform->Position = spawnpoint;
            physicsObject->Velocity = velocity;
            physicsObject->Gravity = FPVector2.Up * (playerHoldingUp ? FP.FromString("-37.512") : FP.FromString("-28.125"));
        }

        public void InitializeBoomerang(Frame f, EntityRef thisEntity, EntityRef owner, FPVector2 spawnpoint, bool right) {
            var asset = f.FindAsset(Asset);
            var transform = f.Unsafe.GetPointer<Transform2D>(thisEntity);
            var physicsObject = f.Unsafe.GetPointer<PhysicsObject>(thisEntity);

            // Vars
            Owner = owner;
            FacingRight = right;

            // Speed
            Speed = asset.Speed;
            /*
            if (asset.InheritShooterVelocity
                && f.Unsafe.TryGetPointer(owner, out PhysicsObject* ownerPhysicsObject)
                && FPMath.Sign(ownerPhysicsObject->Velocity.X) == 1 == FacingRight) {
                Speed += FPMath.Abs(ownerPhysicsObject->Velocity.X * 3);
            }
            */

            // Physics
            BoomerangPhase = 0;
            BoomerangFrame = 0;
            transform->Position = spawnpoint;
            physicsObject->Velocity = new(Speed * (FacingRight ? 1 : -1), 0);
        }

        public void UpdateBoomerang(Frame f, EntityRef thisEntity, PhysicsObject* physicsObject, VersusStageData stage) {
            BoomerangFrame++;
            var asset = f.FindAsset(Asset);

            if (BoomerangPhase == 0) {
                if (BoomerangFrame >= 15) {
                    BoomerangPhase = 1;
                    BoomerangFrame = 0;
                }
            } else if (BoomerangPhase == 1) {
                Speed = asset.Speed * (15 - BoomerangFrame) / 15;
                if (BoomerangFrame >= 15) {
                    BoomerangPhase = 2;
                    BoomerangFrame = 0;
                    Speed = 0;
                }
            } else if (BoomerangPhase == 2) {
                Speed = BoomerangFrame >= 15 ? asset.Speed : asset.Speed * BoomerangFrame / 15;

                var transform = f.Unsafe.GetPointer<Transform2D>(thisEntity);
                if (f.Unsafe.TryGetPointer(Owner, out Transform2D* ownerTransform)
                    && f.Unsafe.TryGetPointer(Owner, out PhysicsCollider2D* ownerCollider)) {
                    FPVector2 ownerCenter = ownerTransform->Position + ownerCollider->Shape.Centroid + new FPVector2(0, ownerCollider->Shape.Box.Extents.Y / 2);
                    QuantumUtils.UnwrapWorldLocations(stage, transform->Position, ownerCenter, out _, out FPVector2 closestOwner);
                    FPVector2 toOwner = closestOwner - transform->Position;

                    if (toOwner.SqrMagnitude < FP._0_25 * FP._0_25) {
                        ProjectileSystem.Destroy(f, thisEntity, asset.DestroyParticleEffect);
                        return;
                    }

                    FPVector2 direction = toOwner.Normalized;
                    physicsObject->Velocity = direction * Speed;
                }

                physicsObject->Gravity = FPVector2.Zero;
            }
        }
    }
}