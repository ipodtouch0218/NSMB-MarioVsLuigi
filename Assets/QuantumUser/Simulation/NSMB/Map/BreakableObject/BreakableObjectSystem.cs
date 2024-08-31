using Photon.Deterministic;

namespace Quantum {
    public unsafe class BreakableObjectSystem : SystemSignalsOnly, ISignalOnBeforePhysicsCollision, ISignalOnStageReset {
        public void OnBeforePhysicsCollision(Frame f, VersusStageData stage, EntityRef entity, PhysicsContact* contact, bool* allowCollision) {
            if (!f.Unsafe.TryGetPointer(contact->Entity, out BreakableObject* breakable)
                || breakable->IsDestroyed
                || breakable->CurrentHeight <= breakable->MinimumHeight
                || !f.Unsafe.TryGetPointer(entity, out MarioPlayer* mario)
                || mario->CurrentPowerupState != PowerupState.MegaMushroom) {
                return;
            }

            var breakableCollider = f.Unsafe.GetPointer<PhysicsCollider2D>(contact->Entity);
            var breakableTransform = f.Get<Transform2D>(contact->Entity);
            FPVector2 breakableUp = FPVector2.Rotate(FPVector2.Up, breakableTransform.Rotation);

            FP dot = FPVector2.Dot(contact->Normal, breakableUp);
            if (dot > PhysicsObjectSystem.GroundMaxAngle) {
                // Hit the top of a pipe
                // Shrink by 1, if we can.
                if (breakable->IsStompable && breakable->CurrentHeight >= breakable->MinimumHeight + 1 && mario->JumpState != JumpState.None) {
                    ChangeHeight(f, contact->Entity, breakable, breakableCollider, breakable->CurrentHeight - 1, null);
                    mario->JumpState = JumpState.None;
                }
            } else if (dot > -PhysicsObjectSystem.GroundMaxAngle) {
                // Hit the side of a pipe
                f.Events.BreakableObjectBroken(f, contact->Entity, entity, -contact->Normal, breakable->CurrentHeight - breakable->MinimumHeight);
                ChangeHeight(f, contact->Entity, breakable, breakableCollider, breakable->MinimumHeight, true);
                *allowCollision = false;
                breakable->IsDestroyed = true;
            }
        }

        public void OnStageReset(Frame f, QBoolean full) {
            var filter = f.Filter<BreakableObject, PhysicsCollider2D>();
            while (filter.NextUnsafe(out EntityRef entity, out BreakableObject* breakable, out PhysicsCollider2D* collider)) {
                ChangeHeight(f, entity, breakable, collider, breakable->OriginalHeight, false);
                breakable->IsDestroyed = false;
            }
        }

        public static void ChangeHeight(Frame f, EntityRef entity, BreakableObject* breakable, PhysicsCollider2D* collider, FP newHeight, bool? broken) {
            newHeight = FPMath.Max(newHeight, breakable->MinimumHeight);
            breakable->CurrentHeight = newHeight;
            if (broken.HasValue) {
                breakable->IsBroken = broken.Value;
            }

            collider->Shape.Box.Extents = new(collider->Shape.Box.Extents.X, newHeight / 4);
            collider->Shape.Centroid.Y = newHeight / 4;
            collider->Enabled = newHeight > 0;

            f.Signals.OnBreakableObjectChangedHeight(entity, newHeight);
        }
    }
}
