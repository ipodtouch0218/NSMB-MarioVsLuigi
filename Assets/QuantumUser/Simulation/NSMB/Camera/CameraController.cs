using Photon.Deterministic;

namespace Quantum {
    public unsafe partial struct CameraController {
        public void Recenter(FPVector2 pos) {
            LastPlayerPosition = CurrentPosition = pos + new FPVector2(0, FP.FromString("0.65"));
            LastFloorHeight = CurrentPosition.Y;
            SmoothDampVelocity = FPVector2.Zero;
        }
    }
}