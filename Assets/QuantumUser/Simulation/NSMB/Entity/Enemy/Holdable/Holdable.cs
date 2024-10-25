namespace Quantum {
    public unsafe partial struct Holdable {
        public void Pickup(Frame f, EntityRef entity, EntityRef marioEntity) {
            Holder = marioEntity;
            PreviousHolder = marioEntity;

            var mario = f.Unsafe.GetPointer<MarioPlayer>(marioEntity);
            mario->HeldEntity = entity;
            mario->HoldStartFrame = f.Number;
            f.Events.MarioPlayerPickedUpObject(f, marioEntity, mario, entity);
        }

        public void DropWithoutThrowing(Frame f, EntityRef entity) {
            var mario = f.Unsafe.GetPointer<MarioPlayer>(Holder);
            mario->HeldEntity = default;
            PreviousHolder = Holder;
            Holder = default;

            f.Signals.OnThrowHoldable(entity, PreviousHolder, true, true);
        }

        public void Throw(Frame f, EntityRef entity) {
            var mario = f.Unsafe.GetPointer<MarioPlayer>(Holder);
            mario->HeldEntity = default;
            mario->ProjectileDelayFrames = 15;

            var transform = f.Unsafe.GetPointer<Transform2D>(entity);
            var collider = f.Unsafe.GetPointer<PhysicsCollider2D>(entity);

            if (PhysicsObjectSystem.BoxInGround(f, transform->Position, collider->Shape)) {
                var marioTransform = f.Unsafe.GetPointer<Transform2D>(Holder);
                transform->Position.X = marioTransform->Position.X;
            }

            PreviousHolder = Holder;
            Holder = default;
            f.Signals.OnThrowHoldable(entity, PreviousHolder, f.GetPlayerInput(mario->PlayerRef)->Down.IsDown, false);
        }
    }
}