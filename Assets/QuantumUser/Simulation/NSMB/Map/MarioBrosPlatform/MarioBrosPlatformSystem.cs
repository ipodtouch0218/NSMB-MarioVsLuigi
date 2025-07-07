using Photon.Deterministic;

namespace Quantum {
    public unsafe class MarioBrosPlatformSystem : SystemSignalsOnly {
        public override void OnInit(Frame f) {
            f.Context.Interactions.Register<MarioPlayer, MarioBrosPlatform>(f, OnMarioBrosPlatformMarioPlayerInteraction);
        }

        public static bool OnMarioBrosPlatformMarioPlayerInteraction(Frame f, EntityRef marioEntity, EntityRef platformEntity, PhysicsContact contact) {
            var transform = f.Unsafe.GetPointer<Transform2D>(platformEntity);

            FPVector2 down = FPVector2.Rotate(FPVector2.Down, transform->Rotation);
            FP dot = FPVector2.Dot(down, contact.Normal);

            if (dot > PhysicsObjectSystem.GroundMaxAngle) {
                // Hit the ground
                BlockBumpSystem.Bump(f, contact.Position, marioEntity, false, true);
                f.Events.MarioBrosPlatformBumped(platformEntity, contact.Position + (FPVector2.Down * FP._0_25));
            }
            return false;
        }
    }
}