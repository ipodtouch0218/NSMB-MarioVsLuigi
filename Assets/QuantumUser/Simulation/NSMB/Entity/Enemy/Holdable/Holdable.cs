using System.Runtime.InteropServices;

namespace Quantum {
    public unsafe partial struct Holdable {
        public void Pickup(Frame f, EntityRef entity, EntityRef mario) {
            Holder = mario;
            PreviousHolder = mario;

            f.Unsafe.GetPointer<MarioPlayer>(mario)->HeldEntity = entity;
        }

        public void Drop(Frame f, EntityRef entity) {
            var mario = f.Unsafe.GetPointer<MarioPlayer>(Holder);
            mario->HeldEntity = default;
            PreviousHolder = Holder;
            Holder = default;

            f.Signals.OnThrowHoldable(entity, PreviousHolder, true);
        }

        public void Throw(Frame f, EntityRef entity) {
            var mario = f.Unsafe.GetPointer<MarioPlayer>(Holder);
            mario->HeldEntity = default;
            mario->ProjectileDelayFrames = 15;
            PreviousHolder = Holder;
            Holder = default;

            var transform = f.Unsafe.GetPointer<Transform2D>(entity);
            var collider = f.Get<PhysicsCollider2D>(entity);
            if (PhysicsObjectSystem.BoxInsideTile(f, transform->Position, collider.Shape)) {
                var marioTransform = f.Get<Transform2D>(entity);
                transform->Position.X = marioTransform.Position.X;
            }

            f.Signals.OnThrowHoldable(entity, PreviousHolder, f.GetPlayerInput(mario->PlayerRef)->Down.IsDown);
        }
    }
}