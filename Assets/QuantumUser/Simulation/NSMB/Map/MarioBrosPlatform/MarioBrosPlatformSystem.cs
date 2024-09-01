using Photon.Deterministic;

namespace Quantum {
    public unsafe class MarioBrosPlatformSystem : SystemSignalsOnly, ISignalOnBeforePhysicsCollision {
        public void OnBeforePhysicsCollision(Frame f, VersusStageData stage, EntityRef entity, PhysicsContact* contact, bool* allowCollision) {
            if (!f.TryGet(contact->Entity, out MarioBrosPlatform platform)
                || !f.TryGet(contact->Entity, out Transform2D transform)) {
                return;
            }

            FPVector2 down = FPVector2.Rotate(FPVector2.Down, transform.Rotation);
            FP dot = FPVector2.Dot(down, contact->Normal);

            if (dot > PhysicsObjectSystem.GroundMaxAngle) {
                // Hit the ground
                BlockBumpSystem.Bump(f, contact->Position - (contact->Normal / 2), entity);
                f.Events.MarioBrosPlatformBumped(f, contact->Entity, contact->Position);
            }
        }
    }
}