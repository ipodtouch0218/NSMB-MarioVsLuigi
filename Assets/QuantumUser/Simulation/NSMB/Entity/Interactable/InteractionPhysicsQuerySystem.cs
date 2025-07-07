using Photon.Deterministic;

namespace Quantum {
#if OLD
    public unsafe class InteractionPhysicsQuerySystem : SystemMainThreadFilterStage<InteractionSystem.Filter> {
        public override void Update(Frame f, ref InteractionSystem.Filter filter, VersusStageData stage) {
            var interactable = filter.Interactable;
            var shape = filter.Collider->Shape;

            if (interactable->ColliderDisabled || interactable->IsPassive) {
                return;
            }

            Transform2D transformCopy = *filter.Transform;

            interactable->OverlapQueryRef = f.Physics2D.AddOverlapShapeQuery(transformCopy, shape);

            FP center = transformCopy.Position.X + shape.Centroid.X;
            if (center - shape.Box.Extents.X <= stage.StageWorldMin.X) {
                // Left edge
                transformCopy.Position.X += stage.TileDimensions.x * FP._0_50;
                interactable->OverlapLevelSeamQueryRef = f.Physics2D.AddOverlapShapeQuery(transformCopy, shape);

            } else if (center + shape.Box.Extents.X >= stage.StageWorldMax.X) {
                // Right edge
                transformCopy.Position.X -= stage.TileDimensions.x * FP._0_50;
                interactable->OverlapLevelSeamQueryRef = f.Physics2D.AddOverlapShapeQuery(transformCopy, shape);
            } else {
                interactable->OverlapLevelSeamQueryRef = PhysicsQueryRef.None;
            }
        }
    }
#else
    public unsafe class InteractionPhysicsQuerySystem : SystemMainThread {

        public struct Filter {
            public EntityRef Entity;
            public Transform2D* Transform;
            public InteractionInitiator* Initiator;
            public PhysicsCollider2D* Collider;
        }

        public override void Update(Frame f) {
            VersusStageData stage = f.FindAsset<VersusStageData>(f.Map.UserAsset);
            FP min = stage.StageWorldMin.X;
            FP max = stage.StageWorldMax.X;
            FP offset = stage.TileDimensions.X * FP._0_50;

            Filter filter = default;
            var it = f.Unsafe.FilterStruct<Filter>();
            while (it.Next(&filter)) {
                var interactable = filter.Initiator;
                var shape = filter.Collider->Shape;

                Transform2D transformCopy = *filter.Transform;
                if (f.Unsafe.TryGetPointer(filter.Entity, out PhysicsObject* physicsObject)) {
                    transformCopy.Position += physicsObject->Velocity * f.DeltaTime;
                }
                interactable->OverlapQueryRef = f.Physics2D.AddOverlapShapeQuery(transformCopy, shape);

                if (stage.IsWrappingLevel) {
                    FP center = transformCopy.Position.X + shape.Centroid.X;
                    if (center - shape.Box.Extents.X <= min) {
                        // Left edge
                        transformCopy.Position.X += offset;
                        interactable->OverlapLevelSeamQueryRef = f.Physics2D.AddOverlapShapeQuery(transformCopy, shape);
                    } else if (center + shape.Box.Extents.X >= max) {
                        // Right edge
                        transformCopy.Position.X -= offset;
                        interactable->OverlapLevelSeamQueryRef = f.Physics2D.AddOverlapShapeQuery(transformCopy, shape);
                    } else {
                        interactable->OverlapLevelSeamQueryRef = PhysicsQueryRef.None;
                    }
                }
            }
        }
    }
#endif
}