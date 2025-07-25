namespace Quantum {
    [UnityEngine.Scripting.Preserve]
    public unsafe class PrePhysicsObjectSystem : SystemMainThreadEntityFilter<PhysicsObject, PrePhysicsObjectSystem.Filter> {
        public struct Filter {
            public EntityRef Entity;
            public PhysicsObject* PhysicsObject;
        }

        public override void Update(Frame f, ref Filter filter, VersusStageData stage) {
            filter.PhysicsObject->WasBeingCrushed = filter.PhysicsObject->IsBeingCrushed;
            filter.PhysicsObject->IsBeingCrushed = false;
        }
    }
}