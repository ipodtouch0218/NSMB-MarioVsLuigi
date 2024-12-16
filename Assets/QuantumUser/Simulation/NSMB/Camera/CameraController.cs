using Photon.Deterministic;

namespace Quantum {
    public unsafe partial struct CameraController {
        public void Recenter(VersusStageData stage, FPVector2 pos) {
            pos = CameraSystem.Clamp(stage, pos);
            LastPlayerPosition = CurrentPosition = pos;
            LastFloorHeight = CurrentPosition.Y;
            SmoothDampVelocity = FPVector2.Zero;
        }
    }
}