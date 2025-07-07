using Photon.Deterministic;

namespace Quantum {
    public unsafe class WrappingObjectSystem : SystemMainThreadFilterStage<WrappingObjectSystem.Filter> {
        public struct Filter {
            public EntityRef Entity;
            public Transform2D* Transform;
            public WrappingObject* WrapObject;
        }

        public override void Update(Frame f, ref Filter filter, VersusStageData stage) {
            FPVector2 newPosition = QuantumUtils.WrapWorld(stage, filter.Transform->Position, out QuantumUtils.WrapDirection wrap);
            if (wrap != QuantumUtils.WrapDirection.NoWrap) {
                filter.Transform->Teleport(f, newPosition);
            }
        }
    }
}