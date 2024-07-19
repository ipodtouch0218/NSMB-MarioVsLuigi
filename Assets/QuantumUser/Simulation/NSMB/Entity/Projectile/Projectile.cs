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
            if (asset.InheritShooterVelocity && f.TryGet(owner, out PhysicsObject ownerPhysicsObject)) {
                Speed += FPMath.Abs(ownerPhysicsObject.Velocity.X / 3);
            }

            if (asset.LockTo45Degrees) {
                physicsObject->TerminalVelocity = -Speed;
            }

            // Physics
            transform->Position = spawnpoint;
            physicsObject->Velocity = new(Speed * (FacingRight ? 1 : -1), -Speed);
        }
    }
}