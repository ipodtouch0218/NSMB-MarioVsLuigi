using Photon.Deterministic;
using Quantum.Collections;
using System;

namespace Quantum {
    public unsafe class PhysicsObjectSystem : SystemMainThreadFilter<PhysicsObjectSystem.Filter>, ISignalOnComponentAdded<PhysicsObject>, ISignalOnComponentRemoved<PhysicsObject> {

        private static readonly FP Skin = FP.FromString("0.001");

        public struct Filter {
            public EntityRef EntityRef;
            public Transform2D* Transform;
            public PhysicsObject* PhysicsObject;
            public PhysicsCollider2D* Collider;
        }

        public override void Update(Frame f, ref Filter filter) {
            var physicsObject = filter.PhysicsObject;
            if (physicsObject->IsFrozen) {
                return;
            }

            physicsObject->Velocity += physicsObject->Gravity * f.DeltaTime;
            physicsObject->Velocity.Y = FPMath.Max(physicsObject->Velocity.Y, physicsObject->TerminalVelocity);
            QList<TileContact> contacts = f.ResolveList(physicsObject->Contacts);
            contacts.Clear();

            MoveVertically(f, filter, contacts);
            MoveHorizontally(f, filter, contacts);
        }

        private void MoveVertically(Frame f, Filter filter, QList<TileContact> contacts) {
            var physicsObject = filter.PhysicsObject;

            physicsObject->IsTouchingGround = false;
            physicsObject->IsTouchingCeiling = false;

            FP velocityY = physicsObject->Velocity.Y * f.DeltaTime;
            if (velocityY == 0) {
                return;
            }

            FPVector2 directionVector = velocityY > 0 ? FPVector2.Up : FPVector2.Down;

            if (!physicsObject->DisableCollision) {
                var stage = f.FindAsset<VersusStageData>(f.Map.UserAsset);
                FPVector2 position = filter.Transform->Position;
                Shape2D.BoxShape collisionShape = filter.Collider->Shape.Box;
                position += filter.Collider->Shape.Centroid;

                FP checkPointY = position.Y + collisionShape.Extents.Y * (velocityY > 0 ? 1 : -1);
                FPVector2 leftWorldCheckPoint = new(position.X - collisionShape.Extents.X, checkPointY);
                FPVector2 rightWorldCheckPoint = new(position.X + collisionShape.Extents.X, checkPointY);

                // Move in the direction and check for any intersections with tiles.
                FP left = FPMath.Floor(leftWorldCheckPoint.X * 2) / 2;
                FP right = FPMath.Floor(rightWorldCheckPoint.X * 2) / 2;
                FP start = FPMath.Floor(checkPointY * 2) / 2;
                FP end = FPMath.Floor((checkPointY + velocityY) * 2) / 2;
                FP direction = directionVector.Y;

                for (FP y = start; (direction > 0 ? (y <= end) : (y >= end)); y += direction / 2) {
                    FP? min = null;
                    FPVector2 normal = FPVector2.Zero;
                    for (FP x = left; x <= right; x += FP._0_50) {
                        FPVector2 worldPos = new FPVector2(x, y) + (FPVector2.One / 4);
                        StageTileInstance tile = stage.GetTileWorld(f, worldPos);
                        FPVector2[][] polygons = tile.GetWorldPolygons(f, worldPos);
                        foreach (FPVector2[] polygon in polygons) {
                            FP? distance = LineSweepPolygonIntersection(leftWorldCheckPoint, rightWorldCheckPoint,
                                directionVector,
                                polygon, out FPVector2 thisNormal);

                            if (MinFpVector2Distance(ref min, distance)) {
                                // This is the new min
                                normal = thisNormal;

                                // Flip normals if needed
                                if (tile.Scale.X < 0 ^ tile.Scale.Y < 0) {
                                    normal = -normal;
                                }
                            }
                        }
                    }

                    if (!min.HasValue || min.Value >= FPMath.Abs(velocityY)) {
                        continue;
                    }

                    // Snap to point.
                    filter.Transform->Position += directionVector * (min.Value - Skin);

                    // Readjust the remaining velocity
                    FP remainingVelocity = filter.PhysicsObject->Velocity.Magnitude - min.Value;
                    FPVector2 newDirection = new(-normal.Y, normal.X);

                    // Only care about the Y aspect to not slide up/down hills via gravity
                    physicsObject->Velocity.Y =
                        Project(filter.PhysicsObject->Velocity.Normalized * remainingVelocity, newDirection).Y;

                    physicsObject->IsTouchingGround = FPVector2.Dot(normal, FPVector2.Up) > 0;
                    if (physicsObject->IsTouchingGround) {
                        physicsObject->FloorAngle =
                            FPVector2.RadiansSignedSkipNormalize(normal, FPVector2.Up) * FP.Rad2Deg;
                    }

                    physicsObject->IsTouchingCeiling = FPVector2.Dot(normal, FPVector2.Down) > 0;
                    return;
                }
            }

            // Good to move
            filter.Transform->Position += directionVector * FPMath.Abs(velocityY);
            physicsObject->FloorAngle = 0;
        }

        private void MoveHorizontally(Frame f, Filter filter, QList<TileContact> contacts) {
            var physicsObject = filter.PhysicsObject;

            physicsObject->IsTouchingLeftWall = false;
            physicsObject->IsTouchingRightWall = false;

            FP velocityX = physicsObject->Velocity.X * f.DeltaTime;
            if (velocityX == 0) {
                return;
            }

            FPVector2 directionVector = velocityX > 0 ? FPVector2.Right : FPVector2.Left;

            if (!physicsObject->DisableCollision) {
                var stage = f.FindAsset<VersusStageData>(f.Map.UserAsset);
                FPVector2 position = filter.Transform->Position;
                Shape2D.BoxShape collisionShape = filter.Collider->Shape.Box;
                position += filter.Collider->Shape.Centroid;

                FP checkPointX = position.X + collisionShape.Extents.X * (velocityX > 0 ? 1 : -1);
                FPVector2 bottomWorldCheckPoint = new(checkPointX, position.Y - collisionShape.Extents.Y);
                FPVector2 topWorldCheckPoint = new(checkPointX, position.Y + collisionShape.Extents.Y);


                // Move in the direction and check for any intersections with tiles.
                FP bottom = FPMath.Floor(bottomWorldCheckPoint.Y * 2) / 2;
                FP top = FPMath.Floor(topWorldCheckPoint.Y * 2) / 2;
                FP start = FPMath.Floor(checkPointX * 2) / 2;
                FP end = FPMath.Floor((checkPointX + velocityX) * 2) / 2;
                FP direction = directionVector.X;

                for (FP x = start; (direction > 0 ? (x <= end) : (x >= end)); x += direction / 2) {
                    FP? min = null;
                    FPVector2 normal = FPVector2.Zero;
                    for (FP y = bottom; y <= top; y += FP._0_50) {
                        FPVector2 worldPos = new FPVector2(x, y) + (FPVector2.One / 4);
                        StageTileInstance tile = stage.GetTileWorld(f, worldPos);
                        FPVector2[][] polygons = tile.GetWorldPolygons(f, worldPos);
                        foreach (FPVector2[] polygon in polygons) {
                            FP? distance = LineSweepPolygonIntersection(bottomWorldCheckPoint, topWorldCheckPoint,
                                directionVector,
                                polygon, out FPVector2 thisNormal);
                            if (MinFpVector2Distance(ref min, distance)) {
                                // This is the new min
                                normal = thisNormal;

                                // Flip normals if needed
                                if (tile.Scale.X < 0 ^ tile.Scale.Y < 0) {
                                    normal = -normal;
                                }
                            }
                        }
                    }

                    if (!min.HasValue || min.Value >= FPMath.Abs(velocityX)) {
                        continue;
                    }

                    // Snap to point.
                    filter.Transform->Position += directionVector * (min.Value - Skin);

                    // Readjust the remaining velocity
                    FP remainingVelocity = physicsObject->Velocity.Magnitude - min.Value;
                    FPVector2 newDirection = new(-normal.Y, normal.X);

                    physicsObject->Velocity =
                        Project(physicsObject->Velocity.Normalized * remainingVelocity, newDirection);

                    physicsObject->IsTouchingLeftWall = FPVector2.Dot(normal, FPVector2.Right) > 0;
                    physicsObject->IsTouchingRightWall = FPVector2.Dot(normal, FPVector2.Left) > 0;
                    return;
                }
            }

            // Good to move
            filter.Transform->Position += directionVector * FPMath.Abs(velocityX);
        }

        private FP? LineSweepPolygonIntersection(FPVector2 a, FPVector2 b, FPVector2 direction, FPVector2[] polygon, out FPVector2 normal) {
            if (polygon.Length <= 1) {
                throw new ArgumentException("Polygon must have at least 2 points!");
            }

            if (direction.X < 0 || direction.Y > 0) {
                (a, b) = (b, a);
            }

            FP? minDistance = null;
            normal = FPVector2.Zero;
            // Raycast in the direction for both a and b first
            FPVector2 tempNormal;
            if (MinFpVector2Distance(ref minDistance,
                    RayPolygonIntersection(a, direction, polygon, out tempNormal))) {
                normal = tempNormal;
            }
            if (MinFpVector2Distance(ref minDistance,
                    RayPolygonIntersection(b, direction, polygon, out tempNormal))) {
                normal = tempNormal;
            }

            // Then raycast in the opposite direction for all polygon vertices
            foreach (var point in polygon) {
                if (MinFpVector2Distance(ref minDistance,
                        RayLineIntersection(point, -direction, a, b, out tempNormal))) {
                    normal = tempNormal;
                }
            }

            return minDistance;
        }

        private FP? RayPolygonIntersection(FPVector2 rayOrigin, FPVector2 rayDirection, FPVector2[] polygon, out FPVector2 normal) {
            FP? minIntersection = null;
            normal = FPVector2.Zero;
            for (int i = 0; i < polygon.Length; i++) {
                if (MinFpVector2Distance(ref minIntersection,
                        RayLineIntersection(rayOrigin, rayDirection, polygon[i], polygon[(i + 1) % polygon.Length],
                            out FPVector2 tempNormal))) {
                    normal = tempNormal;
                }
            }

            return minIntersection;
        }

        private bool MinFpVector2Distance(ref FP? min, FP? value) {
            if (!value.HasValue) {
                return false;
            }

            if (!min.HasValue) {
                min = value.Value;
                return true;
            }

            if (value.Value < min.Value) {
                min = value.Value;
                return true;
            }

            return false;
        }

        private FP? RayLineIntersection(FPVector2 rayOrigin, FPVector2 rayDirection, FPVector2 x, FPVector2 y, out FPVector2 normal) {
            normal = FPVector2.Zero;
            FPVector2 v1 = rayOrigin - x;
            FPVector2 v2 = y - x;
            FPVector2 v3 = new(-rayDirection.Y, rayDirection.X);

            FP dot = FPVector2.Dot(v2, v3);
            if (dot == 0) {
                return null;
            }

            var t1 = FPVector2.Cross(v2, v1) / dot;
            var t2 = FPVector2.Dot(v1, v3) / dot;

            if (t1 >= 0 && (t2 >= 0 && t2 <= 1)) {
                normal = FPVector2.Normalize(new FPVector2(-v2.Y, v2.X));
                return t1;
            }

            return null;
        }

        private static FPVector2 Project(FPVector2 a, FPVector2 b) {
            return b * (FPVector2.Dot(a, b) / b.Magnitude);
        }

        public void OnAdded(Frame f, EntityRef entity, PhysicsObject* component) {
            f.AllocateList(out component->Contacts);
        }

        public void OnRemoved(Frame f, EntityRef entity, PhysicsObject* component) {
            f.FreeList(component->Contacts);
        }
    }
}