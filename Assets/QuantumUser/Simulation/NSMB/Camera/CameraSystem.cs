using Photon.Deterministic;

namespace Quantum {
    public unsafe class CameraSystem : SystemMainThreadFilterStage<CameraSystem.Filter> {
        public struct Filter {
            public EntityRef Entity;
            public Transform2D* Transform;
            public CameraController* Camera;
            public MarioPlayer* Mario;
            public PhysicsObject* PhysicsObject;
            public PhysicsCollider2D* Collider;
        }

        public override void Update(Frame f, ref Filter filter, VersusStageData stage) {
            if (!filter.Mario->IsDead) {
                filter.Camera->CurrentPosition = CalculateNewPosition(f, ref filter, stage);
            }
        }

        private FPVector2 CalculateNewPosition(Frame f, ref Filter filter, VersusStageData stage) {
            var camera = filter.Camera;
            var mario = filter.Mario;
            var transform = filter.Transform;
            var physicsObject = filter.PhysicsObject;

            if (!mario->IsDead && !mario->IsRespawning) {
                camera->LastPlayerPosition = transform->Position;
            }

            FP vOrtho = Constants._3_50;
            FP xOrtho = vOrtho * Constants.SixteenOverNine;
            FPVector2 newCameraPosition = camera->CurrentPosition;

            // Lagging camera movements
            bool validFloor;
            if (physicsObject->IsTouchingGround) {
                camera->LastFloorHeight = transform->Position.Y;
                validFloor = true;
            } else {
                validFloor = camera->LastFloorHeight < transform->Position.Y;
            }

            // Floor height
            if (validFloor) {
                // TODO change magic value
                newCameraPosition.Y = FPMath.Max(newCameraPosition.Y, camera->LastFloorHeight + FP._0_75);
            }

            // Floor Smoothing
            newCameraPosition = QuantumUtils.SmoothDamp(camera->CurrentPosition, newCameraPosition, ref camera->SmoothDampVelocity,
                FP._0_33, FP.UseableMax, f.DeltaTime);

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
            FP cameraTopMinDistance = (FP._5 / 14) * vOrtho;

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

            FPVector2 cameraMin = stage.CameraMinPosition;
            FPVector2 cameraMax = stage.CameraMaxPosition;
            FP heightY = cameraMax.Y - cameraMin.Y;
            FP maxY = heightY < vOrtho * 2 ? (cameraMin.Y + vOrtho) : (cameraMin.Y + heightY - vOrtho);
            if (newCameraPosition.Y >= maxY) {
                camera->SmoothDampVelocity = FPVector2.Zero;
            }

            newCameraPosition = Clamp(stage, newCameraPosition);

            return newCameraPosition;
        }

        public static FPVector2 Clamp(VersusStageData stage, FPVector2 position) {
            FP vOrtho = Constants._3_50;
            FP xOrtho = vOrtho * Constants.SixteenOverNine;

            // Clamping to within level bounds
            FPVector2 cameraMin = stage.CameraMinPosition;
            FPVector2 cameraMax = stage.CameraMaxPosition;
            FP heightY = cameraMax.Y - cameraMin.Y;

            FP maxY = heightY < vOrtho * 2 ? (cameraMin.Y + vOrtho) : (cameraMin.Y + heightY - vOrtho);

            position.X = FPMath.Clamp(position.X, cameraMin.X + xOrtho, cameraMax.X - xOrtho);
            position.Y = FPMath.Clamp(position.Y, cameraMin.Y + vOrtho, maxY);

            return position;
        }
    }
}