using Photon.Deterministic;

namespace Quantum {
    public unsafe partial struct FrozenCube {
        public void Initialize(Frame f, EntityRef cubeEntity, EntityRef frozenEntity) {
            var transform = f.Unsafe.GetPointer<Transform2D>(cubeEntity);
            var physicsCollider = f.Unsafe.GetPointer<PhysicsCollider2D>(cubeEntity);
            var child = f.Unsafe.GetPointer<Freezable>(frozenEntity);
            var childTransform = f.Unsafe.GetPointer<Transform2D>(frozenEntity);
            var childPhysicsCollider = f.Unsafe.GetPointer<PhysicsCollider2D>(frozenEntity);

            Entity = frozenEntity;
            child->FrozenCubeEntity = cubeEntity;
            child->IsFrozen = true;

            // Set location
            FP bottom = childTransform->Position.Y + childPhysicsCollider->Shape.Centroid.Y - childPhysicsCollider->Shape.Box.Extents.Y;
            transform->Position = new FPVector2(childTransform->Position.X, bottom);

            // Set size
            FPVector2 extents = childPhysicsCollider->Shape.Box.Extents;
            // ...
            physicsCollider->Shape.Box.Extents = extents;
            physicsCollider->Shape.Centroid.Y = extents.Y;
            Size = extents;
        }
    }
}
