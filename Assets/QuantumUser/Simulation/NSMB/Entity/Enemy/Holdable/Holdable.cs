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
            PreviousHolder = Holder;
            Holder = default;

            f.Signals.OnThrowHoldable(entity, PreviousHolder, f.GetPlayerInput(mario->PlayerRef)->Down.IsDown);
        }
    }
}