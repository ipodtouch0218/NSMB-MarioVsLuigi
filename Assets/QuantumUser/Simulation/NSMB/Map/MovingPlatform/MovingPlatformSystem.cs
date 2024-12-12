using Photon.Deterministic;
using Quantum.Collections;

namespace Quantum {
    public unsafe class MovingPlatformSystem : SystemMainThreadFilterStage<MovingPlatformSystem.Filter> {

        public struct Filter {
            public EntityRef Entity;
            public Transform2D* Transform;
            public MovingPlatform* Platform;
            public PhysicsCollider2D* Collider;
        }

        public override void OnInit(Frame f) {
            f.Context.EntityAndPlayerMask = ~f.Layers.GetLayerMask("Entity", "Player");
        }

        public override void Update(Frame f, ref Filter filter, VersusStageData stage) {
            var platform = filter.Platform;
            if (f.Unsafe.TryGetPointer(filter.Entity, out PhysicsObject* physicsObject)) {
                platform->Velocity = physicsObject->Velocity;
            }
            if (f.Unsafe.TryGetPointer(filter.Entity, out Holdable* holdable)
                && f.Exists(holdable->Holder)
                && f.Unsafe.TryGetPointer(holdable->Holder, out PhysicsObject* holderPhysicsObject)) {

                platform->Velocity = holderPhysicsObject->Velocity + holderPhysicsObject->ParentVelocity;
            }

            var queryList = f.ResolveList(platform->Queries);
            int queryIndex = 0;

            MoveVertically(f, ref filter, queryList, ref queryIndex, stage);
            MoveHorizontally(f, ref filter, queryList, ref queryIndex, stage);
        }

        private void MoveVertically(Frame f, ref Filter filter, QList<PhysicsQueryRef> queryList, ref int queryIndex, VersusStageData stage) {
            var platform = filter.Platform;
            var transform = filter.Transform;
            var shape = filter.Collider->Shape;

            if (shape.Type == Shape2DType.Compound && shape.Compound.GetShapes(f, out Shape2D* shapes, out int shapeCount)) {
                for (int i = 0; i < shapeCount; i++) {
                    RaycastVertically(f, ref filter, shapes[i], queryList, ref queryIndex, stage);
                }
            } else {
                RaycastVertically(f, ref filter, shape, queryList, ref queryIndex, stage);
            }

            if (!filter.Platform->IgnoreMovement) {
                transform->Position.Y += platform->Velocity.Y * f.DeltaTime;
            }
        }

        private void RaycastVertically(Frame f, ref Filter filter, Shape2D shape, QList<PhysicsQueryRef> queryList, ref int queryIndex, VersusStageData stage) {
            var platform = filter.Platform;
            var transform = filter.Transform;
            var collider = filter.Collider;
            var vertical = f.Physics2D.GetQueryHits(queryList[queryIndex++]);

            FPVector2 yMovement = new(0, platform->Velocity.Y * f.DeltaTime);
            if (yMovement.Y == 0) {
                return;
            }
            yMovement.Y += PhysicsObjectSystem.RaycastSkin * (yMovement.Y > 0 ? 1 : -1);

            FPVector2 position = transform->Position;
            if (shape.Type == Shape2DType.Box) {
                FPVector2 yExtents = shape.Box.Extents;
                yExtents.Y -= PhysicsObjectSystem.RaycastSkin;
                shape.Box.Extents = yExtents;
            } else if (shape.Type == Shape2DType.Edge) {
                position.Y -= PhysicsObjectSystem.RaycastSkin * (yMovement.Y > 0 ? 1 : -1);
            } else {
                return;
            }

            for (int i = 0; i < vertical.Count; i++) {
                var hit = vertical[i];
                if (!f.Unsafe.TryGetPointer(hit.Entity, out PhysicsObject* hitPhysicsObject)
                    || hitPhysicsObject->DisableCollision
                    || !f.Unsafe.TryGetPointer(hit.Entity, out Transform2D* hitTransform)) {
                    continue;
                }

                bool sameDirection = FPMath.Sign(hitPhysicsObject->Velocity.Y) == FPMath.Sign(yMovement.Y);
                if (/*shape.Type == Shape2DType.Edge && */ sameDirection && FPMath.Abs(hitPhysicsObject->Velocity.Y) > FPMath.Abs(yMovement.Y)) {
                    continue;
                }

                /*
                bool allowCollision = true;
                PhysicsContact contact = new PhysicsContact {
                    Frame = f.Number,
                    Distance = hit.CastDistanceNormalized * FPMath.Abs(yMovement.Y) - PhysicsObjectSystem.RaycastSkin,
                    Entity = filter.Entity,
                    Normal = hit.Normal,
                    Position = hit.Point,
                    TileX = -1,
                    TileY = -1
                };
                f.Signals.OnBeforePhysicsCollision(stage, hit.Entity, &contact, &allowCollision);
                if (!allowCollision) {
                    continue;
                }
                */

                FP movement = yMovement.Y * (1 - hit.CastDistanceNormalized);
                if (movement > 0) {
                    movement += PhysicsObjectSystem.Skin;
                } else {
                    movement -= PhysicsObjectSystem.Skin;
                }
                PhysicsObjectSystem.MoveVertically((FrameThreadSafe) f, movement * f.UpdateRate, hit.Entity, stage);

                /*
                if (!f.TryResolveList(hitPhysicsObject->Contacts, out QList<PhysicsContact> hitContacts)) {
                    hitContacts = f.AllocateList(out hitPhysicsObject->Contacts);
                }
                hitContacts.Add(new PhysicsContact {
                    Distance = (yMovement.Y * hit.CastDistanceNormalized) - PhysicsObjectSystem.RaycastSkin,
                    Normal = yMovement.Normalized,
                    Position = hit.Point,
                    TileX = -1,
                    TileY = -1,
                    Entity = filter.Entity,
                    Frame = f.Number,
                });
                */
            }
        }

        private void MoveHorizontally(Frame f, ref Filter filter, QList<PhysicsQueryRef> queryList, ref int queryIndex, VersusStageData stage) {
            var platform = filter.Platform;
            var transform = filter.Transform;
            var shape = filter.Collider->Shape;

            if (shape.Type == Shape2DType.Compound && shape.Compound.GetShapes(f, out Shape2D* shapes, out int shapeCount)) {
                for (int i = 0; i < shapeCount; i++) {
                    RaycastHorizontally(f, ref filter, shapes[i], queryList, ref queryIndex, stage);
                }
            } else {
                RaycastHorizontally(f, ref filter, shape, queryList, ref queryIndex, stage);
            }

            if (!filter.Platform->IgnoreMovement) {
                transform->Position.X += platform->Velocity.X * f.DeltaTime;
            }
        }

        private void RaycastHorizontally(Frame f, ref Filter filter, Shape2D shape, QList<PhysicsQueryRef> queryList, ref int queryIndex, VersusStageData stage) {
            var platform = filter.Platform;
            var transform = filter.Transform;
            var collider = filter.Collider;
            var horizontal = f.Physics2D.GetQueryHits(queryList[queryIndex++]);

            FPVector2 xMovement = new(platform->Velocity.X * f.DeltaTime, 0);
            if (xMovement.X == 0) {
                return;
            }

            xMovement.X += PhysicsObjectSystem.RaycastSkin * (xMovement.X > 0 ? 1 : -1);

            FPVector2 position = transform->Position;
            if (shape.Type == Shape2DType.Box) {
                FPVector2 xExtents = shape.Box.Extents;
                xExtents.X -= PhysicsObjectSystem.RaycastSkin;
                shape.Box.Extents = xExtents;
            } else if (shape.Type == Shape2DType.Edge) {
                position.Y -= PhysicsObjectSystem.RaycastSkin * (xMovement.X > 0 ? 1 : -1);
            } else {
                return;
            }

            for (int i = 0; i < horizontal.Count; i++) {
                var hit = horizontal[i];
                if (!f.Unsafe.TryGetPointer(hit.Entity, out PhysicsObject* hitPhysicsObject)
                    || hitPhysicsObject->DisableCollision
                    //|| ((FPMath.Sign(hitPhysicsObject->Velocity.X) == FPMath.Sign(xMovement.X)) && (FPMath.Abs(hitPhysicsObject->Velocity.X) > FPMath.Abs(xMovement.X)))
                    || !f.Unsafe.TryGetPointer(hit.Entity, out Transform2D* hitTransform)) {
                    continue;
                }

                /*
                bool allowCollision = true;
                PhysicsContact contact = new PhysicsContact {
                    Frame = f.Number,
                    Distance = hit.CastDistanceNormalized * FPMath.Abs(xMovement.X) - PhysicsObjectSystem.RaycastSkin,
                    Entity = filter.Entity,
                    Normal = hit.Normal,
                    Position = hit.Point,
                    TileX = -1,
                    TileY = -1
                };
                f.Signals.OnBeforePhysicsCollision(stage, hit.Entity, &contact, &allowCollision);
                if (!allowCollision) {
                    continue;
                }
                */

                FP movement = xMovement.X * (1 - hit.CastDistanceNormalized);
                if (movement > 0) {
                    movement += PhysicsObjectSystem.Skin;
                } else {
                    movement -= PhysicsObjectSystem.Skin;
                }
                PhysicsObjectSystem.MoveHorizontally((FrameThreadSafe) f, movement * f.UpdateRate, hit.Entity, stage);
                hitPhysicsObject->Velocity.X = 0;

                /*
                if (!f.TryResolveList(hitPhysicsObject->Contacts, out QList<PhysicsContact> hitContacts)) {
                    hitContacts = f.AllocateList(out hitPhysicsObject->Contacts);
                }
                hitContacts.Add(new PhysicsContact {
                    Distance = movement,
                    Normal = xMovement.Normalized,
                    Position = hit.Point,
                    TileX = -1,
                    TileY = -1,
                    Entity = filter.Entity,
                    Frame = f.Number,
                });
                */
            }
        }
    }
}
