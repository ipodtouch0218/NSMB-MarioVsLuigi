namespace Quantum {
    public unsafe class InvisibleBlockSystem : SystemSignalsOnly {

        public override void OnInit(Frame f) {
            f.Context.RegisterPreContactCallback(f, OnPreContactCallback);
        }

        private void OnPreContactCallback(Frame f, VersusStageData stage, EntityRef entity, PhysicsContact contact, ref bool keepContacts) {
            if (!f.Has<InvisibleBlock>(contact.Entity)) {
                return;
            }

            // An object hit an invisible block.
            // Only allow collision if this is Mario.
            keepContacts = f.Has<MarioPlayer>(entity);
        }
    }
}
