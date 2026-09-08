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
    public unsafe class InteractionPhysicsQuerySystem : SystemMainThreadEntityFilter<InteractionInitiator, InteractionPhysicsQuerySystem.Filter> {
        
        public struct Filter {
            public EntityRef Entity;
            public Transform2D* Transform;
            public InteractionInitiator* Initiator;
            public PhysicsCollider2D* Collider;
        }

        public override void Update(Frame f, ref Filter filter, VersusStageData stage) {
            var interactable = filter.Initiator;
            var shape = filter.Collider->Shape;

            Transform2D transformCopy = *filter.Transform;
            
            // Normal query
            interactable->OverlapQueryRef = f.Physics2D.AddOverlapShapeQuery(transformCopy, shape);

            // Wrapping queries
            FP tolerance = 1;

            if (stage.IsWrappingLevel) {
                FP center = transformCopy.Position.X + shape.Centroid.X;
                if (center - shape.Box.Extents.X - tolerance <= stage.StageWorldMin.X) {
                    // Left edge
                    transformCopy.Position.X += stage.TileDimensions.X * FP._0_50;
                    interactable->OverlapLevelSeamQueryRef = f.Physics2D.AddOverlapShapeQuery(transformCopy, shape);
                } else if (center + shape.Box.Extents.X + tolerance >= stage.StageWorldMax.X) {
                    // Right edge
                    transformCopy.Position.X -= stage.TileDimensions.X * FP._0_50;
                    interactable->OverlapLevelSeamQueryRef = f.Physics2D.AddOverlapShapeQuery(transformCopy, shape);
                } else {
                    interactable->OverlapLevelSeamQueryRef = PhysicsQueryRef.None;
                }
            }
        }
    }
#endif
}