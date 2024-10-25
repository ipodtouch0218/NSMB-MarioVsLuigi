using Photon.Deterministic;

namespace Quantum {
    public unsafe class MvLCullingSystem : SystemMainThread {
        public override void Update(Frame f) {
            VersusStageData stage = null;

#if !DEBUG
            if (f.IsVerified) {
                return;
            }
#endif

            if (f.Context.CullingCameraPositions.Count <= 0) {
                return;
            }

            FP cameraRadius = (2 + FP._0_04) * f.Context.MaxCameraOrthoSize;
            var cullables = f.Filter<Transform2D, Cullable>();
            cullables.UseCulling = false;
            while (cullables.NextUnsafe(out EntityRef entity, out Transform2D* transform, out Cullable* cullable)) {
                bool cullingEnabled = !cullable->DisableCulling;
                f.SetCullable(entity, cullingEnabled);

#if !DEBUG
                if (!cullingEnabled) {
                    continue;
                }
#endif

                if (!stage) {
                    if (!(stage = f.FindAsset<VersusStageData>(f.Map.UserAsset))) {
                        // Couldn't find a stage.
                        return;
                    }
                }

                FP minDistanceToCamera = FP.UseableMax;
                foreach (var cameraPosition in f.Context.CullingCameraPositions) {
                    FP distance = QuantumUtils.WrappedDistance(stage, transform->Position + cullable->Offset, cameraPosition);

                    if (minDistanceToCamera > distance) {
                        minDistanceToCamera = distance;
                    }
                }

                bool cull = minDistanceToCamera - cullable->BroadRadius - cameraRadius > 0;
#if DEBUG
                bool originalCull = cull;
                cull &= f.IsPredicted;
#endif 
                if (cull) {
                    // Far enough, cull.
                    f.Cull(entity);
                }

#if DEBUG
                if (f.IsVerified) {
                    ColorRGBA color;
                    if (cullable->DisableCulling) {
                        color = new ColorRGBA(128, 128, 128, 100);
                    } else if (originalCull) {
                        color = new ColorRGBA(255, 0, 0, 100);
                    } else {
                        color = new ColorRGBA(0, 255, 0, 100);
                    }

                    Draw.Circle(transform->Position + cullable->Offset, cullable->BroadRadius, color, true);
                }
#endif
            }
        }
    }
}