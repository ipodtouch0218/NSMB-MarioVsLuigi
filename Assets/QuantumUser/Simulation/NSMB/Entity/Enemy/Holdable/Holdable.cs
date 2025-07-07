namespace Quantum {
    public unsafe partial struct Holdable {
        public void Pickup(Frame f, EntityRef entity, EntityRef marioEntity) {
            Holder = marioEntity;
            PreviousHolder = marioEntity;

            var mario = f.Unsafe.GetPointer<MarioPlayer>(marioEntity);
            mario->HeldEntity = entity;
            mario->HoldStartFrame = f.Number;
            f.Events.MarioPlayerPickedUpObject(marioEntity, entity, HoldAboveHead);
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
            var marioPhysicsObject = f.Unsafe.GetPointer<PhysicsObject>(Holder);
            mario->HeldEntity = default;
            mario->ProjectileDelayFrames = 15;

            var transform = f.Unsafe.GetPointer<Transform2D>(entity);
            var collider = f.Unsafe.GetPointer<PhysicsCollider2D>(entity);

            if (PhysicsObjectSystem.BoxInGround((FrameThreadSafe) f, transform->Position, collider->Shape)) {
                var marioTransform = f.Unsafe.GetPointer<Transform2D>(Holder);
                transform->Position = marioTransform->Position;
                transform->Position.Y -= collider->Shape.Centroid.Y;
                transform->Position.Y += collider->Shape.Box.Extents.Y;
            }

            PreviousHolder = Holder;
            Holder = default;

            bool softDrop = false;
            var rawInput = f.GetPlayerInput(mario->PlayerRef);
            if (rawInput != null) {
                softDrop = rawInput->Down.IsDown;
            }
            softDrop &= !marioPhysicsObject->IsUnderwater;

            f.Signals.OnThrowHoldable(entity, PreviousHolder, softDrop, false);
        }
    }
}