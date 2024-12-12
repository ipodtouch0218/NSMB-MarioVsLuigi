using Photon.Deterministic;

namespace Quantum {

    public unsafe class HoldableObjectSystem : SystemMainThreadFilter<HoldableObjectSystem.Filter>, ISignalOnComponentRemoved<Holdable>,
        ISignalOnTryLiquidSplash, ISignalOnEntityFreeze {
        
        public struct Filter {
            public EntityRef Entity;
            public Transform2D* Transform;
            public Holdable* Holdable;
            public PhysicsObject* PhysicsObject;
        }

        public override void OnInit(Frame f) {
            f.Context.RegisterPreContactCallback(OnPreContactCallback);
        }

        public override void Update(Frame f, ref Filter filter) {
            var holdable = filter.Holdable;

            QuantumUtils.Decrement(ref holdable->IgnoreOwnerFrames);

            if (!f.Exists(holdable->Holder)
                || !f.Unsafe.TryGetPointer(holdable->Holder, out Transform2D* holderTransform)) {
                
                holdable->Holder = EntityRef.None;
                return;
            }

            var mario = f.Unsafe.GetPointer<MarioPlayer>(holdable->Holder);

            if (!mario->CanHoldItem(f, holdable->Holder)) {
                holdable->Throw(f, filter.Entity);
                return;
            }

            filter.PhysicsObject->Velocity = FPVector2.Zero;
            filter.Transform->Position = holderTransform->Position + mario->GetHeldItemOffset(f, holdable->Holder);
        }

        public void OnPreContactCallback(FrameThreadSafe f, VersusStageData stage, EntityRef entity, PhysicsContact contact, ref bool keepContact) {
            if (contact.Entity != EntityRef.None
                && f.TryGetPointer(contact.Entity, out Holdable* holdable)
                && (entity == holdable->Holder || (entity == holdable->PreviousHolder && holdable->IgnoreOwnerFrames > 0))) {

                keepContact = false;
            }
        }

        public void OnRemoved(Frame f, EntityRef entity, Holdable* component) {
            if (f.Unsafe.TryGetPointer(component->Holder, out MarioPlayer* mario)) {
                mario->HeldEntity = EntityRef.None;
            }
        }

        public void OnTryLiquidSplash(Frame f, EntityRef entity, EntityRef liquid, QBoolean exit, bool* doSplash) {
            if (!f.Unsafe.TryGetPointer(entity, out Holdable* holdable)) {
                return;
            }

            if (f.Exists(holdable->Holder)) {
                *doSplash = false;
            }
        }

        public void OnEntityFreeze(Frame f, EntityRef entity, EntityRef iceBlock) {
            if (!f.Unsafe.TryGetPointer(entity, out Holdable* holdable)) {
                return;
            }

            if (f.Unsafe.TryGetPointer(holdable->Holder, out MarioPlayer* marioHolder)) {
                marioHolder->HeldEntity = EntityRef.None;
                holdable->Holder = EntityRef.None;
            }
        }
    }
}