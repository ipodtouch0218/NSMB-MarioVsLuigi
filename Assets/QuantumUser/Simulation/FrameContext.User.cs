using Photon.Deterministic;
using System.Collections.Generic;

namespace Quantum {
    public partial class FrameContextUser {
        public int EntityPlayerMask;
        public List<FPVector2> CullingCameraPositions = new();
        public FP MaxCameraOrthoSize = 7;
    }
}