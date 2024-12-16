using Photon.Deterministic;
using Quantum.Collections;
using UnityEngine;

namespace Quantum {
    public unsafe class MovingPlatformPhysicsQuerySystem : SystemMainThreadFilterStage<MovingPlatformSystem.Filter> {
        public override void Update(Frame f, ref MovingPlatformSystem.Filter filter, VersusStageData stage) {
            var platform = filter.Platform;
            var transform = filter.Transform;
            var shape = filter.Collider->Shape;

            FPVector2 effectivePosition = transform->Position;
            var queryList = f.ResolveList(platform->Queries);
            queryList.Clear();

            if (shape.Type == Shape2DType.Compound && shape.Compound.GetShapes(f, out Shape2D* shapes, out int shapeCount)) {
                for (int i = 0; i < shapeCount; i++) {
                    QueueVertical(f, effectivePosition, shapes[i], platform, queryList);
                }
            } else {
                QueueVertical(f, effectivePosition, shape, platform, queryList);
            }

            if (!filter.Platform->IgnoreMovement) {
                effectivePosition.Y += platform->Velocity.Y * f.DeltaTime;
            }

            if (shape.Type == Shape2DType.Compound && shape.Compound.GetShapes(f, out shapes, out shapeCount)) {
                for (int i = 0; i < shapeCount; i++) {
                    QueueHorizontal(f, effectivePosition, shapes[i], platform, queryList);
                }
            } else {
                QueueHorizontal(f, effectivePosition, shape, platform, queryList);
            }
        }

        private void QueueVertical(Frame f, FPVector2 position, in Shape2D shape, MovingPlatform* platform, QList<PhysicsQueryRef> queryList) {
            FPVector2 yMovement = new(0, platform->Velocity.Y * f.DeltaTime);
            /*
            if (yMovement.Y == 0) {
                return;
            }
            */

            PhysicsQueryRef q = f.Physics2D.AddShapeCastQuery(position, 0, shape, yMovement, false, f.Context.EntityAndPlayerMask, QueryOptions.HitAll | QueryOptions.ComputeDetailedInfo);
            queryList.Add(q);
        }

        private void QueueHorizontal(Frame f, FPVector2 position, in Shape2D shape, MovingPlatform* platform, QList<PhysicsQueryRef> queryList) {
            FPVector2 xMovement = new(platform->Velocity.X * f.DeltaTime, 0);
            /*
            if (xMovement.X == 0) {
                return;
            }
            */

            PhysicsQueryRef q = f.Physics2D.AddShapeCastQuery(position, 0, shape, xMovement, false, f.Context.EntityAndPlayerMask, QueryOptions.HitAll | QueryOptions.ComputeDetailedInfo);
            queryList.Add(q);
        }
    }
}