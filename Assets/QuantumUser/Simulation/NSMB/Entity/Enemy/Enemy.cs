using Photon.Deterministic;

namespace Quantum {
    public unsafe partial struct Enemy {
        public bool IsAlive => !IsDead && IsActive;

        public void Respawn(Frame f, EntityRef entity) {
            var transform = f.Unsafe.GetPointer<Transform2D>(entity);
            var physicsObject = f.Unsafe.GetPointer<PhysicsObject>(entity);

            IsActive = true;
            IsDead = false;
            FacingRight = false;
            transform->Teleport(f, Spawnpoint);
            physicsObject->IsFrozen = false;
            physicsObject->Velocity = FPVector2.Zero;
            physicsObject->DisableCollision = false;
        }

        public void ChangeFacingRight(Frame f, EntityRef entity, bool newFacingRight) {
            if (FacingRight != newFacingRight) {
                FacingRight = newFacingRight;
                f.Signals.OnEnemyTurnaround(entity);
            }
        }
    }
}