using Photon.Deterministic;

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

            FP currentTime = ((f.Number - f.Global->StartFrame) * f.DeltaTime) + genericMover->StartOffset;
            FP nextTime = ((f.Number - f.Global->StartFrame + 1) * f.DeltaTime) + genericMover->StartOffset;

            FPVector2 currentPos = SamplePosition(asset.ObjectPath, currentTime, asset.LoopMode);
            FPVector2 nextPos = SamplePosition(asset.ObjectPath, nextTime, asset.LoopMode);
            FPVector2 velocity = nextPos - currentPos;

            // This doesnt work.
            if (velocity.SqrMagnitude > 1) {
                transform->Teleport(f, transform->Position + velocity);
                velocity = FPVector2.Zero;
            }

            platform->Velocity = velocity * f.UpdateRate;
        }

        private static FPVector2 SamplePosition(GenericMoverAsset.PathNode[] positions, FP sample, GenericMoverAsset.LoopingMode loopMode) {
            FP totalDuration = 0;
            for (int i = 0; i < positions.Length - 1; i++) {
                totalDuration += positions[i].TravelDuration;
            }

            if (loopMode == GenericMoverAsset.LoopingMode.Loop) {
                sample %= totalDuration;
            } else if (loopMode == GenericMoverAsset.LoopingMode.Clamp) {
                sample = FPMath.Clamp(sample, 0, totalDuration);
            } else if (loopMode == GenericMoverAsset.LoopingMode.PingPong) {
                sample %= (totalDuration * 2); 
                if (sample > totalDuration) {
                    sample = (totalDuration * 2) - sample;
                }
            }

            for (int i = 1; i < positions.Length; i++) {
                GenericMoverAsset.PathNode previous = positions[i - 1];
                GenericMoverAsset.PathNode current = positions[i];

                if (sample > previous.TravelDuration) {
                    sample -= previous.TravelDuration;
                } else {
                    FP alpha = sample / previous.TravelDuration;
                    if (current.EaseIn && current.EaseOut) {
                        alpha = QuantumUtils.EaseInOut(alpha);
                    } else if (current.EaseIn) {
                        alpha = QuantumUtils.EaseIn(alpha);
                    } else if (current.EaseOut) {
                        alpha = QuantumUtils.EaseOut(alpha);
                    }
                    return FPVector2.Lerp(previous.Position, current.Position, alpha);
                }
            }

            return default;
        }

    }
}
