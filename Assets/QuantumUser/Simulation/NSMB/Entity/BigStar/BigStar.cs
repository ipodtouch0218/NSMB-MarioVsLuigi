using Photon.Deterministic;

namespace Quantum {

    public unsafe partial struct BigStar {

        public void InitializeMovingStar(Frame f, EntityRef entity, int starDirection) {
            IsStationary = false;
            Speed *= (starDirection == 0 || starDirection == 3) ? 2 : 1;
            FacingRight = starDirection >= 2;
            PassthroughFrames = 60;
            UncollectableFrames = 30;

            var stage = f.FindAsset<VersusStageData>(f.Map.UserAsset);
            var transform = f.Get<Transform2D>(entity);
            var physicsObject = f.Unsafe.GetPointer<PhysicsObject>(entity);

            physicsObject->IsFrozen = false;
            physicsObject->DisableCollision = true;
            physicsObject->Velocity = new FPVector2 {
                X = Speed * (FacingRight ? 1 : -1),
                Y = FP.FromString("8.5"),
            };

            if (transform.Position.Y <= stage.StageWorldMin.Y + 1) {
                // Death boost
                physicsObject->Velocity.Y += 3;
            }
        }
    }
}