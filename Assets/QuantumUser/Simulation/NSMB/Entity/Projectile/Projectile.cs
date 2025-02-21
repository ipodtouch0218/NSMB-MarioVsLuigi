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

            // Speed
            Speed = asset.Speed;
            physicsObject->Gravity = asset.Gravity;
            f.Unsafe.TryGetPointer(owner, out PhysicsObject* ownerPhysicsObject);
            if (asset.InheritShooterVelocity
                && ownerPhysicsObject != null
                // Moving in same direction
                && FPMath.Sign(ownerPhysicsObject->Velocity.X) == 1 == FacingRight) {

                Speed += FPMath.Abs(ownerPhysicsObject->Velocity.X / 3);
            }

            if (asset.LockTo45Degrees) {
                physicsObject->TerminalVelocity = -Speed;
            }

            // Physics
            transform->Position = spawnpoint;

            if (playerHoldingUp) {
                Speed /= 2;
                physicsObject->Velocity.Y = asset.Speed * 2;
            } else {
                physicsObject->Velocity.Y = asset.Speed;
            }
            physicsObject->Velocity.X = Speed * (FacingRight ? 1 : -1);
            if (ownerPhysicsObject != null) {
                physicsObject->Velocity.Y += FPMath.Max(0, ownerPhysicsObject->Velocity.Y);
            }
        }
    }
}