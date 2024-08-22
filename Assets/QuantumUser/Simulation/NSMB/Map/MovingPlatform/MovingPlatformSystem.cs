using Photon.Deterministic;
using Quantum.Collections;
using UnityEngine;

namespace Quantum {
    public unsafe class MovingPlatformSystem : SystemMainThreadFilterStage<MovingPlatformSystem.Filter> { 
        private static int EntityMask;

        public struct Filter {
            public EntityRef Entity;
            public Transform2D* Transform;
            public MovingPlatform* Platform;
            public PhysicsCollider2D* Collider;
        }

        public override void OnInit(Frame f) {
            EntityMask = f.Layers.GetLayerMask("Entity");
        }

        public override void Update(Frame f, ref Filter filter, VersusStageData stage) {
            MoveVertically(f, ref filter, stage);
            MoveHorizontally(f, ref filter, stage);
        }

        private void MoveVertically(Frame f, ref Filter filter, VersusStageData stage) {
            var platform = filter.Platform;
            var transform = filter.Transform;
            var shape = filter.Collider->Shape;

            if (shape.Type == Shape2DType.Compound && shape.Compound.GetShapes(f, out Shape2D* shapes, out int shapeCount)) {
                for (int i = 0; i < shapeCount; i++) {
                    RaycastVertically(f, ref filter, shapes[i], stage);
                }
            } else if (shape.Type == Shape2DType.Box) {
                RaycastVertically(f, ref filter, shape, stage);
            }

            transform->Position.Y += platform->Velocity.Y * f.DeltaTime;
        }

        private void RaycastVertically(Frame f, ref Filter filter, Shape2D shape, VersusStageData stage) {
            var platform = filter.Platform;
            var transform = filter.Transform;
            var collider = filter.Collider;

            FPVector2 yExtents = shape.Box.Extents;
            yExtents.Y -= PhysicsObjectSystem.RaycastSkin;
            shape.Box.Extents = yExtents;

            FPVector2 yMovement = new(0, platform->Velocity.Y * f.DeltaTime);
            if (yMovement.Y > 0) {
                yMovement.Y += PhysicsObjectSystem.RaycastSkin;
            } else {
                yMovement.Y -= PhysicsObjectSystem.RaycastSkin;
            }
            var vertical = f.Physics2D.ShapeCastAll(transform->Position, 0, &shape, yMovement, EntityMask, QueryOptions.HitAll | QueryOptions.ComputeDetailedInfo/* | QueryOptions.DetectOverlapsAtCastOrigin*/);
            for (int i = 0; i < vertical.Count; i++) {
                var hit = vertical[i];
                if (!f.Unsafe.TryGetPointer(hit.Entity, out PhysicsObject* hitPhysicsObject)
                    || hitPhysicsObject->DisableCollision
                    || !f.Unsafe.TryGetPointer(hit.Entity, out Transform2D* hitTransform)) {
                    continue;
                }

                FP movement = yMovement.Y * (1 - hit.CastDistanceNormalized);
                if (movement > 0) {
                    movement += PhysicsObjectSystem.Skin;
                } else {
                    movement -= PhysicsObjectSystem.Skin;
                }
                PhysicsObjectSystem.MoveVertically(f, movement * f.UpdateRate, hit.Entity, stage);
                //hitTransform->Position.Y += movement;

                if (!f.TryResolveList(hitPhysicsObject->Contacts, out QList<PhysicsContact> hitContacts)) {
                    hitContacts = f.AllocateList(out hitPhysicsObject->Contacts);
                }
                hitContacts.Add(new PhysicsContact {
                    Distance = movement,
                    Normal = yMovement.Normalized,
                    TileX = -1,
                    TileY = -1,
                    Entity = filter.Entity,
                    Frame = f.Number,
                });
            }
        }

        private void MoveHorizontally(Frame f, ref Filter filter, VersusStageData stage) {
            var platform = filter.Platform;
            var transform = filter.Transform;
            var shape = filter.Collider->Shape;

            if (shape.Type == Shape2DType.Compound && shape.Compound.GetShapes(f, out Shape2D* shapes, out int shapeCount)) {
                for (int i = 0; i < shapeCount; i++) {
                    RaycastHorizontally(f, ref filter, shapes[i], stage);
                }
            } else if (shape.Type == Shape2DType.Box) {
                RaycastHorizontally(f, ref filter, shape, stage);
            }

            transform->Position.X += platform->Velocity.X * f.DeltaTime;
        }

        private void RaycastHorizontally(Frame f, ref Filter filter, Shape2D shape, VersusStageData stage) {
            var platform = filter.Platform;
            var transform = filter.Transform;
            var collider = filter.Collider;

            FPVector2 xExtents = shape.Box.Extents;
            xExtents.X -= PhysicsObjectSystem.RaycastSkin;
            shape.Box.Extents = xExtents;

            FPVector2 xMovement = new(platform->Velocity.X * f.DeltaTime, 0);
            if (xMovement.X > 0) {
                xMovement.X += PhysicsObjectSystem.RaycastSkin;
            } else {
                xMovement.X -= PhysicsObjectSystem.RaycastSkin;
            }
            var horizontal = f.Physics2D.ShapeCastAll(transform->Position, 0, &shape, xMovement, EntityMask, QueryOptions.HitAll | QueryOptions.ComputeDetailedInfo/* | QueryOptions.DetectOverlapsAtCastOrigin*/);
            for (int i = 0; i < horizontal.Count; i++) {
                var hit = horizontal[i];
                if (!f.Unsafe.TryGetPointer(hit.Entity, out PhysicsObject* hitPhysicsObject)
                    || hitPhysicsObject->DisableCollision
                    || !f.Unsafe.TryGetPointer(hit.Entity, out Transform2D* hitTransform)) {
                    continue;
                }

                FP movement = xMovement.X * (1 - hit.CastDistanceNormalized);
                if (movement > 0) {
                    movement += PhysicsObjectSystem.Skin;
                } else {
                    movement -= PhysicsObjectSystem.Skin;
                }
                PhysicsObjectSystem.MoveHorizontally(f, movement * f.UpdateRate, hit.Entity, stage);

                if (!f.TryResolveList(hitPhysicsObject->Contacts, out QList<PhysicsContact> hitContacts)) {
                    hitContacts = f.AllocateList(out hitPhysicsObject->Contacts);
                }
                hitContacts.Add(new PhysicsContact {
                    Distance = movement,
                    Normal = xMovement.Normalized,
                    TileX = -1,
                    TileY = -1,
                    Entity = filter.Entity,
                    Frame = f.Number,
                });
            }
        }
    }
}