using Photon.Deterministic;

namespace Quantum {
    public unsafe class BreakableObjectSystem : SystemSignalsOnly, ISignalOnBeforePhysicsCollision, ISignalOnStageReset {

        public override void OnInit(Frame f) {
            f.Context.RegisterInteraction<BreakableObject, MarioPlayer>(OnBreakableObjectMarioInteraction);
        }

        public void OnBeforePhysicsCollision(Frame f, VersusStageData stage, EntityRef entity, PhysicsContact* contact, bool* allowCollision) {
            if (f.Has<MarioPlayer>(entity) && f.Has<BreakableObject>(contact->Entity)) {
                *allowCollision &= !TryInteraction(f, contact->Entity, entity, *contact);
            }
        }

        private void OnBreakableObjectMarioInteraction(Frame f, EntityRef breakableObjectEntity, EntityRef marioEntity) {
            TryInteraction(f, breakableObjectEntity, marioEntity);
        }

        private bool TryInteraction(Frame f, EntityRef breakableObjectEntity, EntityRef marioEntity, PhysicsContact? contact = null) {
            var mario = f.Unsafe.GetPointer<MarioPlayer>(marioEntity);
            if (mario->CurrentPowerupState != PowerupState.MegaMushroom || mario->IsDead) {
                return false;
            }

            var breakable = f.Unsafe.GetPointer<BreakableObject>(breakableObjectEntity);
            if (breakable->IsDestroyed || breakable->CurrentHeight <= breakable->MinimumHeight) {
                return false;
            }

            var breakableCollider = f.Unsafe.GetPointer<PhysicsCollider2D>(breakableObjectEntity);
            var breakableTransform = f.Unsafe.GetPointer<Transform2D>(breakableObjectEntity);
            FPVector2 breakableUp = FPVector2.Rotate(FPVector2.Up, breakableTransform->Rotation);

            FPVector2 effectiveNormal;
            if (contact != null) {
                effectiveNormal = contact.Value.Normal;
            } else {
                var marioTransform = f.Unsafe.GetPointer<Transform2D>(marioEntity);
                int direction = QuantumUtils.WrappedDirectionSign(f, breakableTransform->Position, marioTransform->Position);
                effectiveNormal = (direction == 1) ? FPVector2.Right : FPVector2.Left;
            }

            FP dot = FPVector2.Dot(effectiveNormal, breakableUp);
            if (dot > PhysicsObjectSystem.GroundMaxAngle) {
                // Hit the top of a pipe
                // Shrink by 1, if we can.
                if (breakable->IsStompable && breakable->CurrentHeight >= breakable->MinimumHeight + 1 && mario->JumpState != JumpState.None) {
                    ChangeHeight(f, breakableObjectEntity, breakable, breakableCollider, breakable->CurrentHeight - 1, null);
                    mario->JumpState = JumpState.None;
                }
            } else if (dot > -PhysicsObjectSystem.GroundMaxAngle) {
                // Hit the side of a pipe
                f.Events.BreakableObjectBroken(f, breakableObjectEntity, marioEntity, -effectiveNormal, breakable->CurrentHeight - breakable->MinimumHeight);
                ChangeHeight(f, breakableObjectEntity, breakable, breakableCollider, breakable->MinimumHeight, true);
                breakable->IsDestroyed = true;
                return true;
            }

            return false;
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
