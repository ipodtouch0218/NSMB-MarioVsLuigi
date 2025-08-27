using Photon.Deterministic;
using Quantum.Task;

namespace Quantum {
    public unsafe class MvLCullingSystem : SystemBase {

        private TaskDelegateHandle updateTaskHandle;

        protected override TaskHandle Schedule(Frame f, TaskHandle taskHandle) {
            if (f.IsVerified || f.ComponentCount<Cullable>() == 0 || f.Context.CullingCameraPositions.Count <= 0 || f.Map == null) {
                return taskHandle;
            }

            if (!updateTaskHandle.IsValid) {
                f.Context.TaskContext.RegisterDelegate(UpdateTask, $"{GetType().Name}.UpdateTask", ref updateTaskHandle);
            }

            return f.Context.TaskContext.AddMainThreadTask(updateTaskHandle, null, taskHandle);
        }

        public void UpdateTask(FrameThreadSafe fts, int off, int count, void* args) {
            Frame f = (Frame) fts;

            VersusStageData stage = stage = f.FindAsset<VersusStageData>(f.Map.UserAsset);
            if (stage == null) {
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