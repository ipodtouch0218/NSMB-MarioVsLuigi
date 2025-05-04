using Quantum.Collections;

namespace Quantum {
    public unsafe class MovingPlatformPhysicsQuerySystem : SystemMainThreadFilterStage<MovingPlatformSystem.Filter> {
        public override void Update(Frame f, ref MovingPlatformSystem.Filter filter, VersusStageData stage) {
            var platform = filter.Platform;
            if (f.Unsafe.TryGetPointer(filter.Entity, out PhysicsObject* physicsObject)) {
                platform->Velocity = physicsObject->ParentVelocity + physicsObject->Velocity;
                platform->IgnoreMovement = true;
            }
            
            var shape = filter.Collider->Shape;
            var queryList = f.ResolveList(platform->Queries);
            queryList.Clear();

            Transform2D transformCopy = *filter.Transform;
            transformCopy.Position += platform->Velocity * f.DeltaTime;

            Queue(f, transformCopy, shape, queryList);
        }

        private void Queue(Frame f, Transform2D transform, Shape2D shape, QList<PhysicsQueryRef> queryList) {
            if (shape.Type == Shape2DType.Compound) {
                shape.Compound.GetShapes(f, out var buffer, out int shapes);
                for (int i = 0; i < shapes; i++) {
                    Queue(f, transform, buffer[i], queryList);
                }
                return;
            }
            
            queryList.Add(f.Physics2D.AddOverlapShapeQuery(transform, shape, ~f.Context.ExcludeEntityAndPlayerMask, QueryOptions.HitKinematics | QueryOptions.ComputeDetailedInfo));
        }
    }
}