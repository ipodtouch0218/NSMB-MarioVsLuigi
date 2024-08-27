using Photon.Deterministic;

namespace Quantum {
    public unsafe class BreakablePipeSystem : SystemSignalsOnly, ISignalOnBeforePhysicsCollision, ISignalOnStageReset {
        public void OnBeforePhysicsCollision(Frame f, VersusStageData stage, EntityRef entity, PhysicsContact* contact, bool* allowCollision) {
            if (!f.Unsafe.TryGetPointer(contact->Entity, out BreakablePipe* pipe)
                || pipe->CurrentHeight <= pipe->MinimumHeight
                || !f.Unsafe.TryGetPointer(entity, out MarioPlayer* mario)
                || mario->CurrentPowerupState != PowerupState.MegaMushroom) {
                return;
            }

            var pipeCollider = f.Unsafe.GetPointer<PhysicsCollider2D>(contact->Entity);
            var pipeTransform = f.Get<Transform2D>(contact->Entity);
            FPVector2 pipeUp = FPVector2.Rotate(FPVector2.Up, pipeTransform.Rotation);

            FP dot = FPVector2.Dot(contact->Normal, pipeUp);
            if (dot > PhysicsObjectSystem.GroundMaxAngle) {
                // Hit the top of a pipe
                // Shrink by 1, if we can.
                if (pipe->CurrentHeight >= 2 && mario->JumpState != JumpState.None) {
                    ChangeHeight(pipe, pipeCollider, pipe->CurrentHeight - 1, null);
                    mario->JumpState = JumpState.None;
                }
            } else if (dot > -PhysicsObjectSystem.GroundMaxAngle) {
                // Hit the side of a pipe
                f.Events.BreakablePipeBroken(f, contact->Entity, entity, -contact->Normal, pipe->CurrentHeight - pipe->MinimumHeight);
                ChangeHeight(pipe, pipeCollider, 0, true);
                *allowCollision = false;
            }
        }

        public void OnStageReset(Frame f, QBoolean full) {
            var filter = f.Filter<BreakablePipe, PhysicsCollider2D>();
            while (filter.NextUnsafe(out _, out BreakablePipe* pipe, out PhysicsCollider2D* collider)) {
                ChangeHeight(pipe, collider, pipe->OriginalHeight, false);
            }
        }

        public static void ChangeHeight(BreakablePipe* pipe, PhysicsCollider2D* collider, FP newHeight, bool? broken) {
            newHeight = FPMath.Max(newHeight, pipe->MinimumHeight);
            pipe->CurrentHeight = newHeight;
            if (broken.HasValue) {
                pipe->IsBroken = broken.Value;
            }

            collider->Shape.Box.Extents = new FPVector2(FP._0_50, newHeight / 4);
            collider->Shape.Centroid = FPVector2.Up * (newHeight / 4);
        }
    }
}
