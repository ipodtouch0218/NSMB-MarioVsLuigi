using Photon.Deterministic;

namespace Quantum {
    public unsafe class GenericMoverSystem : SystemMainThreadFilter<GenericMoverSystem.Filter> {
        public struct Filter {
            public EntityRef Entity;
            public GenericMover* GenericMover;
            public MovingPlatform* Platform;
        }

        public override void Update(Frame f, ref Filter filter) {
            var platform = filter.Platform;
            var genericMover = filter.GenericMover;
            var asset = f.FindAsset(genericMover->MoverAsset);

            if (f.Global->GameState != GameState.Playing) {
                return;
            }

            FP time = (f.Number - f.Global->StartFrame) * f.DeltaTime;
            FP nextTime = (f.Number - f.Global->StartFrame + 1) * f.DeltaTime;
            var curveX = asset.CurveX;
            var curveY = asset.CurveY;

            platform->Velocity = new FPVector2(
                (curveX.Evaluate(nextTime % curveX.EndTime) - curveX.Evaluate(time % curveX.EndTime)) * f.UpdateRate,
                (curveY.Evaluate(nextTime % curveY.EndTime) - curveY.Evaluate(time % curveY.EndTime)) * f.UpdateRate
            );
        }
    }
}
