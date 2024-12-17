using Photon.Deterministic;

namespace Quantum {
    public unsafe class MarioBrosPlatformSystem : SystemSignalsOnly {
        public override void OnInit(Frame f) {
            f.Context.RegisterInteraction<MarioBrosPlatform, MarioPlayer>(OnMarioBrosPlatformMarioPlayerInteraction);
        }

        public static void OnMarioBrosPlatformMarioPlayerInteraction(Frame f, EntityRef platformEntity, EntityRef marioEntity, PhysicsContact contact) {
            var transform = f.Unsafe.GetPointer<Transform2D>(platformEntity);

            FPVector2 down = FPVector2.Rotate(FPVector2.Down, transform->Rotation);
            FP dot = FPVector2.Dot(down, contact.Normal);

            if (dot > PhysicsObjectSystem.GroundMaxAngle) {
                // Hit the ground
                BlockBumpSystem.Bump(f, contact.Position, marioEntity, false);
                f.Events.MarioBrosPlatformBumped(f, platformEntity, contact.Position + (FPVector2.Down * FP._0_25));
            }
        }
    }
}