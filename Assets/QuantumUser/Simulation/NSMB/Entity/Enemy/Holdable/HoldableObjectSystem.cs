using Photon.Deterministic;

namespace Quantum {

    public unsafe class HoldableObjectSystem : SystemMainThreadEntityFilter<Holdable, HoldableObjectSystem.Filter>, ISignalOnComponentRemoved<Holdable>,
        ISignalOnTryLiquidSplash, ISignalOnEntityFreeze {
        
        public struct Filter {
            public EntityRef Entity;
            public Transform2D* Transform;
            public Holdable* Holdable;
            public PhysicsObject* PhysicsObject;
        }

        public override void OnInit(Frame f) {
            f.Context.RegisterPreContactCallback(f, OnPreContactCallback);
        }

        public override void Update(Frame f, ref Filter filter, VersusStageData stage) {
            var holdable = filter.Holdable;

            QuantumUtils.Decrement(ref holdable->IgnoreOwnerFrames);

            if (!f.Exists(holdable->Holder)
                || !f.Unsafe.TryGetPointer(holdable->Holder, out Transform2D* holderTransform)) {
                
                holdable->Holder = EntityRef.None;
                return;
            }

            var mario = f.Unsafe.GetPointer<MarioPlayer>(holdable->Holder);

            if (!mario->CanHoldItem(f, holdable->Holder, filter.Entity)) {
                holdable->Throw(f, filter.Entity);
                return;
            }

            var physicsObject = filter.PhysicsObject;

            FPVector2 newVel = FPVector2.Zero;
            if (f.Unsafe.TryGetPointer(holdable->Holder, out PhysicsObject* holderPhysicsObject)) {
                newVel = holderPhysicsObject->Velocity + holderPhysicsObject->ParentVelocity;
            }
            physicsObject->Velocity = newVel;
            physicsObject->WasTouchingGround = false;
            physicsObject->IsTouchingGround = false;

            var transform = filter.Transform;
            FPVector2 newPos = holderTransform->Position + mario->GetHeldItemOffset(f, holdable->Holder);

            if (FPMath.Abs(transform->Position.X - newPos.X) > (FP) stage.TileDimensions.X / 4) {
                transform->Teleport(f, newPos);
            } else {
                transform->Position = newPos;
            }
        }

        public void OnPreContactCallback(Frame f, VersusStageData stage, EntityRef entity, PhysicsContact contact, ref bool keepContact) {
            if (contact.Entity != EntityRef.None
                && f.Unsafe.TryGetPointer(contact.Entity, out Holdable* holdable)
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