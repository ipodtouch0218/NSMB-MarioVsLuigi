using Photon.Deterministic;
using UnityEngine;

namespace Quantum {
    public unsafe class GenericMoverSystem : SystemMainThreadFilter<GenericMoverSystem.Filter> {
        public struct Filter {
            public EntityRef Entity;
            public Transform2D* Transform;
            public GenericMover* GenericMover;
            public MovingPlatform* Platform;
        }

        public override void Update(Frame f, ref Filter filter) {
            if (f.Global->GameState != GameState.Playing) {
                return;
            }

            var platform = filter.Platform;
            var genericMover = filter.GenericMover;
            var transform = filter.Transform;
            var asset = f.FindAsset(genericMover->MoverAsset);

            FP time = (f.Number - f.Global->StartFrame) * f.DeltaTime + genericMover->StartOffset;
            FP nextTime = (f.Number - f.Global->StartFrame + 1) * f.DeltaTime + genericMover->StartOffset;
            var curveX = asset.CurveX;
            var curveY = asset.CurveY;

            FP nextFrameX = nextTime % curveX.EndTime;
            FP currFrameX = time % curveX.EndTime;
            FP velX = curveX.Evaluate(nextFrameX) - curveX.Evaluate(currFrameX);
            FP nextFrameY = nextTime % curveY.EndTime;
            FP currFrameY = time % curveY.EndTime;
            FP velY = curveY.Evaluate(nextFrameY) - curveY.Evaluate(currFrameY);

            // This doesnt work.
            if (nextFrameX > curveX.Keys[^1].Time) {
                transform->Teleport(f, transform->Position + FPVector2.Right * velX);
                velX = 0;
            }

            // This doesnt work.
            if (nextFrameY > curveY.Keys[^1].Time) {
                transform->Teleport(f, transform->Position + FPVector2.Up * velY);
                velY = 0;
            }

            platform->Velocity = new FPVector2(velX, velY) * f.UpdateRate;
        }
    }
}
