namespace Quantum {
    public unsafe class PrePhysicsObjectSystem : SystemMainThreadFilter<PrePhysicsObjectSystem.Filter> {
        public struct Filter {
            public EntityRef Entity;
            public PhysicsObject* PhysicsObject;
        }

        public override void Update(Frame f, ref Filter filter) {
            filter.PhysicsObject->WasBeingCrushed = filter.PhysicsObject->IsBeingCrushed;
            filter.PhysicsObject->IsBeingCrushed = false;
        }
    }
}