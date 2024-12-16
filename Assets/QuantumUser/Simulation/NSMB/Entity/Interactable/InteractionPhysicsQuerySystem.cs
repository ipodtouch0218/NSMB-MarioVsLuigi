using Photon.Deterministic;

namespace Quantum {
    public unsafe class InteractionPhysicsQuerySystem : SystemMainThreadFilterStage<InteractionSystem.Filter> {
        public override void Update(Frame f, ref InteractionSystem.Filter filter, VersusStageData stage) {
            var interactable = filter.Interactable;
            var shape = filter.Collider->Shape;

            Transform2D transformCopy = *filter.Transform;

            interactable->OverlapQueryRef = f.Physics2D.AddOverlapShapeQuery(transformCopy, shape);

            FP center = transformCopy.Position.X + shape.Centroid.X;
            if (center - shape.Box.Extents.X < stage.StageWorldMin.X) {
                // Left edge
                transformCopy.Position.X += stage.TileDimensions.x * FP._0_50;
                interactable->OverlapLevelSeamQueryRef = f.Physics2D.AddOverlapShapeQuery(transformCopy, shape);

            } else if (center + shape.Box.Extents.X > stage.StageWorldMax.X) {
                // Right edge
                transformCopy.Position.X -= stage.TileDimensions.x * FP._0_50;
                interactable->OverlapLevelSeamQueryRef = f.Physics2D.AddOverlapShapeQuery(transformCopy, shape);
            }
        }
    }
}