using Photon.Deterministic;
using Quantum.Collections;
using System;
using System.Collections.Generic;

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
            QList<PhysicsContact> contacts = f.ResolveList(physicsObject->Contacts);
            contacts.Clear();

            physicsObject->IsOnSlideableGround = false;
            physicsObject->IsOnSlipperyGround = false;
            MoveVertically(f, filter, contacts);
            MoveHorizontally(f, filter, contacts);
            ResolveContacts(filter.PhysicsObject, contacts);
        }

        private void MoveVertically(Frame f, Filter filter, QList<PhysicsContact> contacts) {
            var physicsObject = filter.PhysicsObject;

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
                    List<PhysicsContact> tempContacts = new();

                    for (FP x = left; x <= right; x += FP._0_50) {
                        FPVector2 worldPos = new FPVector2(x, y) + (FPVector2.One / 4);
                        StageTileInstance tileInstance = stage.GetTileWorld(f, worldPos);
                        FPVector2 tilePos = QuantumUtils.WorldToRelativeTile(stage, worldPos);
                        StageTile tile = f.FindAsset(tileInstance.Tile);
                        FPVector2[][] polygons = tileInstance.GetWorldPolygons(tile, worldPos);
                        foreach (FPVector2[] polygon in polygons) {
                            HashSet<PhysicsContact> polygonContacts = LineSweepPolygonIntersection(
                                leftWorldCheckPoint,
                                rightWorldCheckPoint, directionVector, polygon);

                            foreach (var contact in polygonContacts) {
                                PhysicsContact newContact = contact;
                                newContact.TileX = tilePos.X.AsInt;
                                newContact.TileY = tilePos.Y.AsInt;
                                tempContacts.Add(newContact);

                                physicsObject->IsOnSlideableGround |= tile.IsSlideableGround;
                                physicsObject->IsOnSlipperyGround |= tile.IsSlipperyGround;
                            }
                        }
                    }

                    if (tempContacts.Count == 0) {
                        continue;
                    }

                    // Get n-lowest contacts (within tolerance)
                    tempContacts.Sort((a, b) => a.Distance.CompareTo(b.Distance));
                    FP tolerance = FP._0_05;
                    FP min = tempContacts[0].Distance;
                    FPVector2 avgNormal = FPVector2.Zero;
                    int contactCount = 0;

                    foreach (var contact in tempContacts) {
                        if (contact.Distance - min > tolerance) {
                            break;
                        }

                        contacts.Add(contact);
                        avgNormal += contact.Normal;
                        contactCount++;
                    }

                    avgNormal /= contactCount;

                    // Snap to point.
                    filter.Transform->Position += directionVector * (min - Skin);

                    // Readjust the remaining velocity
                    FP remainingVelocity = filter.PhysicsObject->Velocity.Magnitude - min;
                    FPVector2 newDirection = new(-avgNormal.Y, avgNormal.X);

                    // Only care about the Y aspect to not slide up/down hills via gravity
                    physicsObject->Velocity.Y =
                        Project(filter.PhysicsObject->Velocity.Normalized * remainingVelocity, newDirection).Y;

                    return;
                }
            }

            // Good to move
            filter.Transform->Position += directionVector * FPMath.Abs(velocityY);
            physicsObject->FloorAngle = 0;
        }

        private void MoveHorizontally(Frame f, Filter filter, QList<PhysicsContact> contacts) {
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
                    List<PhysicsContact> tempContacts = new();

                    for (FP y = bottom; y <= top; y += FP._0_50) {
                        FPVector2 worldPos = new FPVector2(x, y) + (FPVector2.One / 4);
                        StageTileInstance tile = stage.GetTileWorld(f, worldPos);
                        FPVector2 tilePos = QuantumUtils.WorldToRelativeTile(stage, worldPos);
                        FPVector2[][] polygons = tile.GetWorldPolygons(f, worldPos);
                        foreach (FPVector2[] polygon in polygons) {
                            HashSet<PhysicsContact> polygonContacts = LineSweepPolygonIntersection(bottomWorldCheckPoint,
                                topWorldCheckPoint, directionVector, polygon);

                            foreach (var contact in polygonContacts) {
                                PhysicsContact newContact = contact;
                                newContact.TileX = tilePos.X.AsInt;
                                newContact.TileY = tilePos.Y.AsInt;
                                tempContacts.Add(newContact);
                            }
                        }
                    }

                    if (tempContacts.Count == 0) {
                        continue;
                    }

                    // Get n-lowest contacts (within tolerance)
                    tempContacts.Sort((a, b) => a.Distance.CompareTo(b.Distance));
                    FP tolerance = FP._0_05;
                    FP min = tempContacts[0].Distance;
                    FPVector2 avgNormal = FPVector2.Zero;
                    int contactCount = 0;

                    foreach (var contact in tempContacts) {
                        if (contact.Distance - min > tolerance) {
                            break;
                        }

                        contacts.Add(contact);
                        avgNormal += contact.Normal;
                        contactCount++;
                    }

                    avgNormal /= contactCount;

                    // Snap to point.
                    filter.Transform->Position += directionVector * (min - Skin);

                    // Readjust the remaining velocity
                    FP remainingVelocity = physicsObject->Velocity.Magnitude - min;
                    FPVector2 newDirection = new(-avgNormal.Y, avgNormal.X);

                    physicsObject->Velocity =
                        Project(physicsObject->Velocity.Normalized * remainingVelocity, newDirection);
                    return;
                }
            }

            // Good to move
            filter.Transform->Position += directionVector * FPMath.Abs(velocityX);
        }

        public void ResolveContacts(PhysicsObject* physicsObject, QList<PhysicsContact> contacts) {

            physicsObject->FloorAngle = 0;
            physicsObject->IsTouchingGround = false;
            physicsObject->IsTouchingCeiling = false;
            physicsObject->IsTouchingLeftWall = false;
            physicsObject->IsTouchingRightWall = false;

            foreach (var contact in contacts) {
                FP horizontalDot = FPVector2.Dot(contact.Normal, FPVector2.Right);
                if (horizontalDot > FP._0_75) {
                    physicsObject->IsTouchingLeftWall = true;

                } else if (horizontalDot < -FP._0_75) {
                    physicsObject->IsTouchingRightWall = true;
                }

                FP verticalDot = FPVector2.Dot(contact.Normal, FPVector2.Up);
                if (verticalDot > FP._0_75) {
                    physicsObject->IsTouchingGround = true;

                    FP angle = FPVector2.RadiansSignedSkipNormalize(contact.Normal, FPVector2.Up) * FP.Rad2Deg;
                    physicsObject->FloorAngle = FPMath.Max(physicsObject->FloorAngle, angle);

                } else if (verticalDot < -FP._0_75) {
                    physicsObject->IsTouchingCeiling = true;
                }
            }
        }

        private HashSet<PhysicsContact> LineSweepPolygonIntersection(FPVector2 a, FPVector2 b, FPVector2 direction, FPVector2[] polygon) {
            if (polygon.Length <= 1) {
                throw new ArgumentException("Polygon must have at least 2 points!");
            }

            if (direction.X < 0 || direction.Y > 0) {
                (a, b) = (b, a);
            }

            // TODO change to nonalloc-ing
            HashSet<PhysicsContact> possibleContacts = new();

            // Raycast in the direction for both a and b first
            if (TryRayPolygonIntersection(a, direction, polygon, out var contact)) {
                possibleContacts.Add(contact);
            }

            if (TryRayPolygonIntersection(b, direction, polygon, out contact)) {
                possibleContacts.Add(contact);
            }

            // Then raycast in the opposite direction for all polygon vertices
            foreach (var point in polygon) {
                if (!TryRayLineIntersection(point, -direction, b, a, out contact)) {
                    continue;
                }

                contact.Normal *= -1; // Inverted normals
                possibleContacts.Add(contact);
            }

            return possibleContacts;
        }

        private bool TryRayPolygonIntersection(FPVector2 rayOrigin, FPVector2 rayDirection, FPVector2[] polygon, out PhysicsContact contact) {
            bool hit = false;
            contact = default;
            contact.Distance = FP.MaxValue;

            for (int i = 0; i < polygon.Length; i++) {
                if (!TryRayLineIntersection(rayOrigin, rayDirection, polygon[i], polygon[(i + 1) % polygon.Length], out var newContact)) {
                    continue;
                }

                if (newContact.Distance >= contact.Distance) {
                    continue;
                }

                // New least distance
                contact = newContact;
                hit = true;
            }

            return hit;
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

        private bool TryRayLineIntersection(FPVector2 rayOrigin, FPVector2 rayDirection, FPVector2 x, FPVector2 y, out PhysicsContact contact) {
            contact = default;
            contact.Distance = FP.MaxValue;

            FPVector2 v1 = rayOrigin - x;
            FPVector2 v2 = y - x;
            FPVector2 v3 = new(-rayDirection.Y, rayDirection.X);

            FP dot = FPVector2.Dot(v2, v3);
            if (dot == 0) {
                return false;
            }

            var t1 = FPVector2.Cross(v2, v1) / dot;
            var t2 = FPVector2.Dot(v1, v3) / dot;

            if (t1 < 0 || (t2 < 0 || t2 > 1)) {
                return false;
            }

            FPVector2 normal = FPVector2.Normalize(new FPVector2(-v2.Y, v2.X));

            // Don't hit internal edges
            if (FPVector2.Dot(rayDirection, normal) > 0) {
                return false;
            }

            contact = new PhysicsContact {
                Position = rayOrigin + rayDirection * t1,
                Normal = normal,
                Distance = t1,
            };
            return true;
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