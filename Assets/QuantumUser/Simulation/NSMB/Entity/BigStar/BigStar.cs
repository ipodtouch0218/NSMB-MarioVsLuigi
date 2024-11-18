using Photon.Deterministic;

namespace Quantum {

    public unsafe partial struct BigStar {

        public void InitializeMovingStar(Frame f, VersusStageData stage, EntityRef entity, int starDirection) {
            IsStationary = false;
            Speed *= (starDirection == 0 || starDirection == 3) ? 2 : 1;
            FacingRight = starDirection >= 2;
            PassthroughFrames = 60;
            UncollectableFrames = 30;

            var transform = f.Unsafe.GetPointer<Transform2D>(entity);
            var physicsObject = f.Unsafe.GetPointer<PhysicsObject>(entity);

            physicsObject->IsFrozen = false;
            physicsObject->DisableCollision = true;
            physicsObject->Velocity = new FPVector2(
                Speed * (FacingRight ? 1 : -1),
                Constants._8_50
            );

            if (transform->Position.Y <= stage.CameraMinPosition.Y) {
                // Death boost
                physicsObject->Velocity.Y += 3;
                transform->Position.Y = stage.StageWorldMin.Y + 1;
            }
        }
    }
}