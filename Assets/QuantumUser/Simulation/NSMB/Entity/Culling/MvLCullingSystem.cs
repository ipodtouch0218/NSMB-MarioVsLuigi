using Photon.Deterministic;

namespace Quantum {
    public unsafe class MvLCullingSystem : SystemMainThread {
        public override void Update(Frame f) {
            f.ClearCulledState();
            VersusStageData stage;
            if (f.IsVerified || f.Context.CullingCameraPositions.Count <= 0 || !f.Map || !(stage = f.FindAsset<VersusStageData>(f.Map.UserAsset))) {
                return;
            }

            FP cameraRadius = Constants.SixteenOverNine * (f.Context.MaxCameraOrthoSize * 2);
            var cullables = f.Filter<Transform2D, Cullable>();
            cullables.UseCulling = false;
            while (cullables.NextUnsafe(out EntityRef entity, out Transform2D* transform, out Cullable* cullable)) {
                if (f.Context.CullingIgnoredEntities.Contains(entity)) {
                    continue;
                }

                FP cullingSize = cullable->BroadRadius + cameraRadius;

                bool cull = true;
                foreach (var cameraPosition in f.Context.CullingCameraPositions) {
                    FPVector2 diff = (transform->Position + cullable->Offset) - cameraPosition;
                    if (FPMath.Abs(diff.X) < cullingSize || FPMath.Abs(diff.Y) < cullingSize) {
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