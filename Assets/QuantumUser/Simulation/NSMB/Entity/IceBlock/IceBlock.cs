using Photon.Deterministic;

namespace Quantum {
    public unsafe partial struct IceBlock {
        public bool TimerEnabled(Frame f, EntityRef iceBlockEntity) {
            var childFreezable = f.Unsafe.GetPointer<Freezable>(Entity);
            var holdable = f.Unsafe.GetPointer<Holdable>(iceBlockEntity);
            var physicsObject = f.Unsafe.GetPointer<PhysicsObject>(iceBlockEntity);
            
            return childFreezable->AutoBreakWhileHeld
                || ((!physicsObject->IsUnderwater || InLiquidType == LiquidType.Water) && !IsSliding && !f.Exists(holdable->Holder));
        }

        public void Initialize(Frame f, EntityRef iceBlockEntity, EntityRef childEntity) {
            var transform = f.Unsafe.GetPointer<Transform2D>(iceBlockEntity);
            var physicsCollider = f.Unsafe.GetPointer<PhysicsCollider2D>(iceBlockEntity);
            var child = f.Unsafe.GetPointer<Freezable>(childEntity);
            var childTransform = f.Unsafe.GetPointer<Transform2D>(childEntity);
            var childPhysicsCollider = f.Unsafe.GetPointer<PhysicsCollider2D>(childEntity);

            Entity = childEntity;
            child->FrozenCubeEntity = iceBlockEntity;
            IsFlying = child->IsFlying;
            if (IsFlying) {
                var physicsObject = f.Unsafe.GetPointer<PhysicsObject>(iceBlockEntity);
                physicsObject->IsFrozen = true;
            }

            // Set location
            ChildOffset = new FPVector2(0, childPhysicsCollider->Shape.Centroid.Y - childPhysicsCollider->Shape.Box.Extents.Y - FP._0_05);
            transform->Position = childTransform->Position + ChildOffset + child->Offset + (FPVector2.Up * FP._0_05);

            // Set size
            FPVector2 extents = child->IceBlockSize / 2;
            physicsCollider->Shape.Box.Extents = extents;
            physicsCollider->Shape.Centroid.Y = extents.Y;
            Size = extents;

            // Set timer
            AutoBreakFrames = child->AutoBreakFrames;

            // Try to not spawn inside blocks/walls
            PhysicsObjectSystem.TryEject((FrameThreadSafe) f, iceBlockEntity);
        }
    }
}
