using Photon.Deterministic;

namespace Quantum {
    public unsafe partial struct CameraController {
        public void Recenter(VersusStageData stage, FPVector2 pos) {
            pos = CameraSystem.Clamp(stage, pos, OrthographicSize / 2);
            LastPlayerPosition = CurrentPosition = pos;
            LastFloorHeight = CurrentPosition.Y;
            SmoothDampVelocity = FPVector2.Zero;
        }
    }

    namespace Prototypes {
        public partial class CameraControllerPrototype {
            partial void MaterializeUser(Frame frame, ref CameraController result, in PrototypeMaterializationContext context) {
                result.OrthographicSize = 7;
            }
        }
    }
}