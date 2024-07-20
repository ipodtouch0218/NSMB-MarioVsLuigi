using Photon.Deterministic;

namespace Quantum {
    public unsafe class CameraSystem : SystemMainThreadFilter<CameraSystem.Filter> {
        public struct Filter {
            public EntityRef Entity;
            public Transform2D* Transform;
            public CameraController* Camera;
            public MarioPlayer* Mario;
            public PhysicsObject* PhysicsObject;
            public PhysicsCollider2D* Collider;
        }

        public override void Update(Frame f, ref Filter filter) {
            filter.Camera->CurrentPosition = CalculateNewPosition(f, filter);
        }

        public void Recenter(Frame f, EntityRef entity, FPVector2 pos) {
            var camera = f.Unsafe.GetPointer<CameraController>(entity);

            camera->LastPlayerPosition = camera->CurrentPosition = pos + new FPVector2(0, FP.FromString("0.65"));
            camera->LastFloorHeight = camera->CurrentPosition.Y;
            camera->SmoothDampVelocity = FPVector2.Zero;
        }

        private FPVector2 CalculateNewPosition(Frame f, Filter filter) {
            var stage = f.FindAsset<VersusStageData>(f.Map.UserAsset);
            var camera = filter.Camera;
            var mario = *filter.Mario;
            var transform = *filter.Transform;
            var physicsObject = filter.PhysicsObject;

            if (!mario.IsDead && !mario.IsRespawning) {
                camera->LastPlayerPosition = transform.Position;
            }

            FP vOrtho = FP.FromString("3.5");
            FP xOrtho = vOrtho * FP.FromString("1.777777");
            FPVector2 newCameraPosition = camera->CurrentPosition;

            // Lagging camera movements
            bool validFloor;
            if (physicsObject->IsTouchingGround) {
                camera->LastFloorHeight = transform.Position.Y;
                validFloor = true;
            } else {
                validFloor = camera->LastFloorHeight < transform.Position.Y;
            }

            // Floor height
            if (validFloor) {
                // TODO change magic value
                newCameraPosition.Y = FPMath.Max(newCameraPosition.Y, camera->LastFloorHeight + FP._0_75);
            }

            // Floor Smoothing
            QuantumUtils.SmoothDamp(camera->CurrentPosition, newCameraPosition, ref camera->SmoothDampVelocity,
                FP._0_50, FP.UseableMax, f.DeltaTime);

            // Bottom camera clip
            FP cameraBottom = newCameraPosition.Y - vOrtho;
            FP cameraBottomDistanceToPlayer = camera->LastPlayerPosition.Y - cameraBottom;
            FP cameraBottomMinDistance = (FP._5 / 7) * vOrtho;

            if (cameraBottomDistanceToPlayer < cameraBottomMinDistance) {
                newCameraPosition.Y -= (cameraBottomMinDistance - cameraBottomDistanceToPlayer);
                camera->SmoothDampVelocity.Y = 0;
            }

            // Top camera clip
            FP playerHeight = filter.Collider->Shape.Box.Extents.Y * 2;
            FP cameraTop = newCameraPosition.Y + vOrtho;
            FP cameraTopDistanceToPlayer = cameraTop - (camera->LastPlayerPosition.Y + playerHeight);
            FP cameraTopMinDistance = (FP.FromString("2.5") / 7) * vOrtho;

            if (cameraTopDistanceToPlayer < cameraTopMinDistance) {
                newCameraPosition.Y += (cameraTopMinDistance - cameraTopDistanceToPlayer);
                camera->SmoothDampVelocity.Y = 0;
            }

            camera->LastPlayerPosition = QuantumUtils.WrapWorld(f, camera->LastPlayerPosition, out _);

            FP xDifference = FPVector2.Distance(FPVector2.Right * newCameraPosition.X, FPVector2.Right * camera->LastPlayerPosition.X);
            bool right = newCameraPosition.X > camera->LastPlayerPosition.X;

            if (xDifference >= 2) {
                newCameraPosition.X += (right ? -1 : 1) * stage.TileDimensions.x * FP._0_50;
                xDifference = FPVector2.Distance(FPVector2.Right * newCameraPosition.X, FPVector2.Right * camera->LastPlayerPosition.X);
                right = newCameraPosition.X > camera->LastPlayerPosition.X;
            }

            if (xDifference > FP._0_25) {
                newCameraPosition.X += (FP._0_25 - xDifference - FP._0_01) * (right ? 1 : -1);
            }

            // Clamping to within level bounds
            FPVector2 cameraMin = stage.CameraMinPosition;
            FPVector2 cameraMax = stage.CameraMaxPosition;
            FP heightY = cameraMax.Y - cameraMin.Y;

            FP maxY = heightY < vOrtho * 2 ? (cameraMin.Y + vOrtho) : (cameraMin.Y + heightY - vOrtho);
            if (newCameraPosition.Y > maxY) {
                camera->SmoothDampVelocity = FPVector2.Zero;
            }

            newCameraPosition.X = FPMath.Clamp(newCameraPosition.X, cameraMin.X + xOrtho, cameraMax.X - xOrtho);
            newCameraPosition.Y = FPMath.Clamp(newCameraPosition.Y, cameraMin.Y + vOrtho, maxY);

            return newCameraPosition;
        }

    }
}