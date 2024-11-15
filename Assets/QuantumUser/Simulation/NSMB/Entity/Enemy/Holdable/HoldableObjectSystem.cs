using Photon.Deterministic;

namespace Quantum {

    public unsafe class HoldableObjectSystem : SystemMainThreadFilter<HoldableObjectSystem.Filter>, ISignalOnBeforePhysicsCollision,
        ISignalOnComponentRemoved<Holdable>, ISignalOnTryLiquidSplash {
        
        public struct Filter {
            public EntityRef Entity;
            public Transform2D* Transform;
            public Holdable* Holdable;
            public PhysicsObject* PhysicsObject;
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

        public void OnBeforePhysicsCollision(Frame f, VersusStageData stage, EntityRef entity, PhysicsContact* contact, bool* allowCollision) {
            if (contact->Entity != EntityRef.None
                && f.Unsafe.TryGetPointer(contact->Entity, out Holdable* holdable)
                && (entity == holdable->Holder || (entity == holdable->PreviousHolder && holdable->IgnoreOwnerFrames > 0))) {

                *allowCollision = false;
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
    }
}