using Photon.Deterministic;

namespace Quantum {
    public unsafe class MvLCullingSystem : SystemMainThread {
        public override void Update(Frame f) {
            f.ClearCulledState();
            if (f.IsVerified || f.Context.CullingCameraPositions.Count <= 0) {
                return;
            }

            VersusStageData stage = f.FindAsset<VersusStageData>(f.Map.UserAsset);

            FP cameraRadius = Constants.SixteenOverNine * f.Context.MaxCameraOrthoSize;
            var cullables = f.Filter<Transform2D, Cullable>();
            cullables.UseCulling = false;
            while (cullables.NextUnsafe(out EntityRef entity, out Transform2D* transform, out Cullable* cullable)) {
                FP cullingRadius = cullable->BroadRadius + cameraRadius;

                bool cull = true;
                foreach (var cameraPosition in f.Context.CullingCameraPositions) {
                    if (QuantumUtils.WrappedDistanceSquared(stage, transform->Position + cullable->Offset, cameraPosition) < cullingRadius) {
                        cull = false;
                        break;
                    }
                }

                if (cull) {
                    f.Cull(entity);
                }
            }
        }
    }
}