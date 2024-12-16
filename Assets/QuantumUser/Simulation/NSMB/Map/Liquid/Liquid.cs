using Photon.Deterministic;

namespace Quantum {
    public unsafe partial struct Liquid {
        public FP GetSurfaceHeight(Transform2D* transform) {
            return transform->Position.Y + (HeightTiles * FP._0_50) - FP._0_10;
        }
    }
}