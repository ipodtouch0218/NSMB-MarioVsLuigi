using UnityEngine;

namespace Quantum {

    public unsafe class HoldableObjectSystem : SystemMainThreadFilter<HoldableObjectSystem.Filter> {
        public struct Filter {
            public EntityRef Entity;
            public Transform2D* Transform;
            public Holdable* Holdable;
            public PhysicsObject* PhysicsObject;
        }

        public override void Update(Frame f, ref Filter filter) {
            var holdable = filter.Holdable;    
            
            if (!f.Exists(holdable->Holder)
                || !f.TryGet(holdable->Holder, out Transform2D holderTransform)) {
                
                holdable->Holder = default;
                return;
            }

            var mario = f.Unsafe.GetPointer<MarioPlayer>(holdable->Holder);

            if (!mario->CanHoldItem(f, holdable->Holder)) {
                holdable->Throw(f, filter.Entity);
                return;
            }

            filter.PhysicsObject->Velocity = default;
            filter.Transform->Position = holderTransform.Position + mario->GetHeldItemOffset(f);
        }
    }
}