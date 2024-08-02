using Photon.Deterministic;
using Quantum.Collections;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Quantum {
    public unsafe class PhysicsObjectSystem : SystemMainThreadFilter<PhysicsObjectSystem.Filter> {

        private static readonly FP Skin = FP.FromString("0.001");

        public struct Filter {
            public EntityRef Entity;
            public Transform2D* Transform;
            public PhysicsObject* PhysicsObject;
            public PhysicsCollider2D* Collider;
        }

        public override void Update(Frame f, ref Filter filter) {
            var physicsObject = filter.PhysicsObject;
            if (physicsObject->IsFrozen) {
                return;
            }

            bool wasOnGround = physicsObject->IsTouchingGround && physicsObject->Velocity.Y == physicsObject->PreviousVelocity.Y;

            physicsObject->Velocity += physicsObject->Gravity * f.DeltaTime;
            physicsObject->Velocity.Y = FPMath.Max(physicsObject->Velocity.Y, physicsObject->TerminalVelocity);

            if (!f.TryResolveList(physicsObject->Contacts, out QList<PhysicsContact> contacts)) {
                contacts = f.AllocateList(out physicsObject->Contacts);
            }
            contacts.Clear();

            physicsObject->IsOnSlideableGround = false;
            physicsObject->IsOnSlipperyGround = false;
            MoveVertically(f, filter, contacts);
            MoveHorizontally(f, filter, contacts);
            ResolveContacts(filter.PhysicsObject, contacts);

            if (!physicsObject->DisableCollision && wasOnGround && !physicsObject->IsTouchingGround) {
                // Try snapping
                FPVector2 previousPosition = filter.Transform->Position;
                physicsObject->Velocity.Y = -FP._0_20 / f.DeltaTime;

                MoveVertically(f, filter, contacts);
                ResolveContacts(filter.PhysicsObject, contacts);
                if (!physicsObject->IsTouchingGround) {
                    physicsObject->Velocity.Y = 0;
                    filter.Transform->Position = previousPosition;
                }
            }

            physicsObject->PreviousVelocity = physicsObject->Velocity;
#if DEBUG
            foreach (var contact in contacts) {
                Draw.Ray(contact.Position, contact.Normal, ColorRGBA.Red);
            }
#endif
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
                    List<PhysicsContact> potentialContacts = new();

                    for (FP x = left; x <= right; x += FP._0_50) {
                        FPVector2 worldPos = new FPVector2(x, y) + (FPVector2.One / 4);
                        StageTileInstance tileInstance = stage.GetTileWorld(f, worldPos);
                        Vector2Int tilePos = QuantumUtils.WorldToRelativeTile(stage, worldPos);
                        StageTile tile = f.FindAsset(tileInstance.Tile);
                        FPVector2[][] polygons = tileInstance.GetWorldPolygons(tile, worldPos);
                        foreach (FPVector2[] polygon in polygons) {
                            HashSet<PhysicsContact> polygonContacts = LineSweepPolygonIntersection(
                                leftWorldCheckPoint,
                                rightWorldCheckPoint, directionVector, polygon);

                            foreach (var contact in polygonContacts) {
                                PhysicsContact newContact = contact;
                                newContact.TileX = tilePos.x;
                                newContact.TileY = tilePos.y;

                                potentialContacts.Add(newContact);
                            }
                        }
                    }

                    if (potentialContacts.Count == 0) {
                        continue;
                    }

                    // Get n-lowest contacts (within tolerance)
                    potentialContacts.Sort((a, b) => a.Distance.CompareTo(b.Distance));
                    FP tolerance = 0;
                    FP? min = null;
                    FPVector2 avgNormal = FPVector2.Zero;
                    int contactCount = 0;

                    HashSet<(int, int)> removedContacts = new();

                    foreach (var contact in potentialContacts) {
                        var coords = (contact.TileX, contact.TileY);
                        if ((min.HasValue && contact.Distance - min.Value > tolerance)
                            || contact.Distance > FPMath.Abs(velocityY)
                            || removedContacts.Contains(coords)
                            /* || FPVector2.Dot(contact.Normal, directionVector) > 0 */) {
                            continue;
                        }

                        bool keepContact = true;
                        f.Signals.OnPreTileCollide(stage, filter.Entity, &contact, &keepContact);
                        if (keepContact) {
                            contacts.Add(contact);
                            min ??= contact.Distance;
                            avgNormal += contact.Normal;
                            contactCount++;

                            StageTile tile = f.FindAsset(stage.GetTileRelative(f, contact.TileX, contact.TileY).Tile);
                            physicsObject->IsOnSlideableGround |= tile.IsSlideableGround;
                            physicsObject->IsOnSlipperyGround |= tile.IsSlipperyGround;

                        } else {
                            removedContacts.Add(coords);
                        }
                    }

                    if (contactCount <= 0) {
                        continue;
                    }

                    avgNormal /= contactCount;

                    // Snap to point.
                    filter.Transform->Position += directionVector * (min.Value - Skin);

                    // Readjust the remaining velocity
                    FP remainingVelocity = filter.PhysicsObject->Velocity.Magnitude - min.Value;
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
                    List<PhysicsContact> potentialContacts = new();

                    for (FP y = bottom; y <= top; y += FP._0_50) {
                        FPVector2 worldPos = new FPVector2(x, y) + (FPVector2.One / 4);
                        StageTileInstance tile = stage.GetTileWorld(f, worldPos);
                        Vector2Int tilePos = QuantumUtils.WorldToRelativeTile(stage, worldPos);
                        FPVector2[][] polygons = tile.GetWorldPolygons(f, worldPos);
                        foreach (FPVector2[] polygon in polygons) {
                            HashSet<PhysicsContact> polygonContacts = LineSweepPolygonIntersection(bottomWorldCheckPoint,
                                topWorldCheckPoint, directionVector, polygon);

                            foreach (var contact in polygonContacts) {
                                PhysicsContact newContact = contact;
                                newContact.TileX = tilePos.x;
                                newContact.TileY = tilePos.y;

                                potentialContacts.Add(newContact);
                            }
                        }
                    }

                    if (potentialContacts.Count == 0) {
                        continue;
                    }

                    // Get n-lowest contacts (within tolerance)
                    potentialContacts.Sort((a, b) => a.Distance.CompareTo(b.Distance));
                    FP tolerance = 0;
                    FP? min = null;
                    FPVector2 avgNormal = FPVector2.Zero;
                    int contactCount = 0;

                    HashSet<(int, int)> removedContacts = new();

                    foreach (var contact in potentialContacts) {
                        var coords = (contact.TileX, contact.TileY);
                        if ((min.HasValue && contact.Distance - min.Value > tolerance)
                            || contact.Distance > FPMath.Abs(velocityX)
                            || removedContacts.Contains(coords)
                            /* || FPVector2.Dot(contact.Normal, directionVector) > 0 */) {
                            continue;
                        }

                        bool keepContact = true;
                        f.Signals.OnPreTileCollide(stage, filter.Entity, &contact, &keepContact);

                        if (keepContact) {
                            contacts.Add(contact);
                            min ??= contact.Distance;
                            avgNormal += contact.Normal;
                            contactCount++;

                            StageTile tile = f.FindAsset(stage.GetTileRelative(f, contact.TileX, contact.TileY).Tile);
                            physicsObject->IsOnSlideableGround |= tile.IsSlideableGround;
                            physicsObject->IsOnSlipperyGround |= tile.IsSlipperyGround;

                        } else {
                            removedContacts.Add(coords);
                        }
                    }

                    if (contactCount <= 0) {
                        continue;
                    }

                    avgNormal /= contactCount;

                    // Snap to point.
                    filter.Transform->Position += directionVector * (min.Value - Skin);

                    // Readjust the remaining velocity
                    FP remainingVelocity = physicsObject->Velocity.Magnitude - min.Value;
                    FPVector2 newDirection = new(-avgNormal.Y, avgNormal.X);
                    
                    physicsObject->Velocity = Project(physicsObject->Velocity.Normalized * remainingVelocity, newDirection);
                    if (FPMath.Abs(FPVector2.Dot(newDirection, FPVector2.Right)) > FP._0_33) {
                        physicsObject->Velocity.X = velocityX / f.DeltaTime;
                    }
                    return;
                }
            }

            // Good to move
            filter.Transform->Position += directionVector * FPMath.Abs(velocityX);
        }

        private void ResolveContacts(PhysicsObject* physicsObject, QList<PhysicsContact> contacts) {

            physicsObject->FloorAngle = 0;
            physicsObject->IsTouchingGround = false;
            physicsObject->IsTouchingCeiling = false;
            physicsObject->IsTouchingLeftWall = false;
            physicsObject->IsTouchingRightWall = false;

            foreach (var contact in contacts) {
                FP horizontalDot = FPVector2.Dot(contact.Normal, FPVector2.Right);
                if (horizontalDot > FP._0_33 * 2) {
                    physicsObject->IsTouchingLeftWall = true;

                } else if (horizontalDot < -FP._0_33 * 2) {
                    physicsObject->IsTouchingRightWall = true;
                }

                FP verticalDot = FPVector2.Dot(contact.Normal, FPVector2.Up);
                if (verticalDot > FP._0_33 * 2) {
                    physicsObject->IsTouchingGround = true;

                    FP angle = FPVector2.RadiansSignedSkipNormalize(contact.Normal, FPVector2.Up) * FP.Rad2Deg;
                    if (FPMath.Abs(physicsObject->FloorAngle) < FPMath.Abs(angle)) {
                        physicsObject->FloorAngle = angle;
                    }

                } else if (verticalDot < -FP._0_33 * 2) {
                    physicsObject->IsTouchingCeiling = true;
                }
            }
        }

        public static bool Raycast(Frame f, VersusStageData stage, FPVector2 position, FPVector2 direction, FP maxDistance, out PhysicsContact contact) {
            FPVector2 stepSize = new(
                direction.X == 0 ? 0 : FPMath.Sqrt(1 + (direction.Y / direction.X) * (direction.Y / direction.X)),
                direction.Y == 0 ? 0 : FPMath.Sqrt(1 + (direction.X / direction.Y) * (direction.X / direction.Y))
            );
            FPVector2 rayLength = default;
            Vector2Int step = default;

            if (direction.X < 0) {
                step.x = -1;
                rayLength.X = (position.X - FPMath.Floor(position.X * 2) / 2) * stepSize.X;
            } else if (direction.X > 0) {
                step.x = 1;
                rayLength.X = (FPMath.Floor(position.X * 2 + 1) / 2 - position.X) * stepSize.X;
            } else {
                step.x = 0;
                rayLength.X = maxDistance;
            }

            if (direction.Y < 0) {
                step.y = -1;
                rayLength.Y = (position.Y - FPMath.Floor(position.Y * 2) / 2) * stepSize.Y;
            } else if (direction.Y > 0) {
                step.y = 1;
                rayLength.Y = (FPMath.Floor(position.Y * 2 + 1) / 2 - position.Y) * stepSize.Y;
            } else {
                step.y = 0;
                rayLength.Y = maxDistance;
            }

            Vector2Int tile = QuantumUtils.WorldToRelativeTile(stage, position);
            FP distance = 0;
            while (distance < maxDistance) {
                if (rayLength.X < rayLength.Y) {
                    tile.x += step.x;
                    distance = rayLength.X;
                    rayLength.X += stepSize.X;
                } else {
                    tile.y += step.y;
                    distance = rayLength.Y;
                    rayLength.Y += stepSize.Y;
                }

                StageTileInstance tileInstance = stage.GetTileRelative(f, tile.x, tile.y);
                FPVector2[][] polygons = tileInstance.GetWorldPolygons(f, QuantumUtils.RelativeTileToWorldRounded(stage, tile));
                foreach (var polygon in polygons) {
                    if (TryRayPolygonIntersection(position, direction, polygon, out contact)) {
                        return contact.Distance <= maxDistance;
                    }
                }
            }

            contact = default;
            return false;
        }

        private static HashSet<PhysicsContact> LineSweepPolygonIntersection(FPVector2 a, FPVector2 b, FPVector2 direction, FPVector2[] polygon) {
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
            int length = polygon.Length;
            for (int i = 0; i < length; i++) {
                var point = polygon[i];
                if (!TryRayLineIntersection(point, -direction, b, a, out contact)) {
                    continue;
                }

                bool valid = false;
                if (length == 2) {
                    valid = FPVector2.Dot(GetNormal(polygon[0], polygon[1]), direction) < 0;
                } else {
                    valid |= FPVector2.Dot(GetNormal(point, polygon[(i + 1) % polygon.Length]), direction) < 0;
                    valid |= FPVector2.Dot(GetNormal(polygon[(i - 1 + polygon.Length) % polygon.Length], point), direction) < 0;
                }

                if (valid) {
                    contact.Normal *= -1; // Inverted normals
                    possibleContacts.Add(contact);
                }
            }

            return possibleContacts;
        }

        private static FPVector2 GetNormal(FPVector2 a, FPVector2 b) {
            FPVector2 diff = b - a;
            return new FPVector2(-diff.Y, diff.X);
        }

        private static bool TryRayPolygonIntersection(FPVector2 rayOrigin, FPVector2 rayDirection, FPVector2[] polygon, out PhysicsContact contact) {
            bool hit = false;
            contact = default;
            contact.Distance = FP.MaxValue;

            int length = polygon.Length;
            if (length <= 1) {
                return false;
            }
            if (length == 2) {
                length = 1;
            }
            for (int i = 0; i < length; i++) {
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

        private static bool TryRayLineIntersection(FPVector2 rayOrigin, FPVector2 rayDirection, FPVector2 x, FPVector2 y, out PhysicsContact contact) {
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

        public static bool BoxInsideTile(Frame f, FPVector2 position, Shape2D shape) {
            var stage = f.FindAsset<VersusStageData>(f.Map.UserAsset);
            var extents = shape.Box.Extents;

            FPVector2 origin = position + shape.Centroid;
            FPVector2 boxMin = origin - extents;
            FPVector2 boxMax = origin + extents;

            /*
            FPVector2[] boxCorners = {
                new(origin.X - extents.X, origin.Y + extents.Y),
                boxMax,
                new(origin.X + extents.X, origin.Y - extents.Y),
                boxMin,
            };
            */

            for (int x = FPMath.FloorToInt((origin.X - extents.X) * 2); x <= FPMath.FloorToInt((origin.X + extents.X) * 2); x++) {
                for (int y = FPMath.FloorToInt((origin.Y - extents.Y) * 2); y <= FPMath.FloorToInt((origin.Y + extents.Y) * 2); y++) {
                    FPVector2[][] tilePolygons = stage.GetTileRelative(f, x, y).GetWorldPolygons(f, QuantumUtils.RelativeTileToWorldRounded(stage, new Vector2Int(x, y)));

                    foreach (var polygon in tilePolygons) {
                    /*
                        foreach (var corner in boxCorners) {
                            if (PointIsInsidePolygon(corner, polygon)) {
                                return true;
                            }
                        }
                        */
                        for (int i = 0; i < polygon.Length; i++) {
                            if (LineIntersectsBox(polygon[i], polygon[(i + 1) % polygon.Length], boxMin, boxMax)) {
                                return true;
                            }
                        }
                    }
                }
            }

            return false;
        }

        private static bool PointIsInsidePolygon(FPVector2 point, FPVector2[] polygon) {
            // Can't be inside a line...
            if (polygon.Length < 3) {
                return false;
            }

            // https://stackoverflow.com/a/49434625/19635374
            bool result = false;
            var a = polygon[^1];
            foreach (var b in polygon) {
                if ((b.X == point.X) && (b.Y == point.Y)) {
                    return true;
                }

                if ((b.Y == a.Y) && (point.Y == a.Y)) {
                    if ((a.X <= point.X) && (point.X <= b.X)) {
                        return true;
                    }

                    if ((b.X <= point.X) && (point.X <= a.X)) {
                        return true;
                    }
                }

                if ((b.Y < point.Y) && (a.Y >= point.Y) || (a.Y < point.Y) && (b.Y >= point.Y)) {
                    if (b.X + (point.Y - b.Y) / (a.Y - b.Y) * (a.X - b.X) <= point.X) {
                        result = !result;
                    }
                }
                a = b;
            }

            // Invert result if counterclockwise (outside)
            return result ^ FPVector2.IsCounterClockWise(polygon);
        }

        // ------------ https://en.wikipedia.org/wiki/Cohen%E2%80%93Sutherland_algorithm ------------ //
        [Flags]
        enum OutCode {
            Inside = 0,
            Left = 1 << 0,
            Right = 1 << 1,
            Bottom = 1 << 2,
            Top = 1 << 3,
        }

        private static OutCode ComputeOutCode(FPVector2 point, FPVector2 boxMin, FPVector2 boxMax) {
            OutCode code = OutCode.Inside;  // initialised as being inside of clip window

            if (point.X < boxMin.X) {
                code |= OutCode.Left;
            } else if (point.X > boxMax.X) {
                code |= OutCode.Right;
            }
            if (point.Y < boxMin.Y) {
                code |= OutCode.Bottom;
            } else if (point.Y > boxMax.Y) {
                code |= OutCode.Top;
            }

            return code;
        }

        // Cohen–Sutherland clipping algorithm clips a line from
        // P0 = (x0, y0) to P1 = (x1, y1) against a rectangle with 
        // diagonal from (xmin, ymin) to (xmax, ymax).
        private static bool LineIntersectsBox(FPVector2 a, FPVector2 b, FPVector2 boxMin, FPVector2 boxMax) {
            // compute outcodes for P0, P1, and whatever point lies outside the clip rectangle
            int outcode0 = (int) ComputeOutCode(a, boxMin, boxMax);
            int outcode1 = (int) ComputeOutCode(b, boxMin, boxMax);
            bool accept = false;

            while (true) {
                if ((outcode0 | outcode1) == 0) {
                    // bitwise OR is 0: both points inside window; trivially accept and exit loop
                    accept = true;
                    break;
                } else if ((outcode0 & outcode1) != 0) {
                    // bitwise AND is not 0: both points share an outside zone (LEFT, RIGHT, TOP,
                    // or BOTTOM), so both must be outside window; exit loop (accept is false)
                    break;
                } else {
                    // failed both tests, so calculate the line segment to clip
                    // from an outside point to an intersection with clip edge
                    FP x = 0, y = 0;

                    // At least one endpoint is outside the clip rectangle; pick it.
                    int outcodeOut = (outcode1 > outcode0) ? outcode1 : outcode0;

                    // Now find the intersection point;
                    // use formulas:
                    //   slope = (y1 - y0) / (x1 - x0)
                    //   x = x0 + (1 / slope) * (ym - y0), where ym is ymin or ymax
                    //   y = y0 + slope * (xm - x0), where xm is xmin or xmax
                    // No need to worry about divide-by-zero because, in each case, the
                    // outcode bit being tested guarantees the denominator is non-zero
                    if (((OutCode) outcodeOut).HasFlag(OutCode.Top)) {           // point is above the clip window
                        x = a.X + (b.X - a.X) * (boxMax.Y - a.Y) / (b.Y - a.Y);
                        y = boxMax.Y;
                    } else if (((OutCode) outcodeOut).HasFlag(OutCode.Bottom)) { // point is below the clip window
                        x = a.X + (b.X - a.X) * (boxMin.Y - a.Y) / (b.Y - a.Y);
                        y = boxMin.Y;
                    } else if (((OutCode) outcodeOut).HasFlag(OutCode.Right)) {  // point is to the right of clip window
                        y = a.Y + (b.Y - a.Y) * (boxMax.X - a.X) / (b.X - a.X);
                        x = boxMax.X;
                    } else if (((OutCode) outcodeOut).HasFlag(OutCode.Left)) {   // point is to the left of clip window
                        y = a.Y + (b.Y - a.Y) * (boxMin.X - a.X) / (b.X - a.X);
                        x = boxMin.X;
                    }

                    // Now we move outside point to intersection point to clip
                    // and get ready for next pass.
                    if (outcodeOut == outcode0) {
                        a.X = x;
                        a.Y = y;
                        outcode0 = (int) ComputeOutCode(a, boxMin, boxMax);
                    } else {
                        b.X = x;
                        b.Y = y;
                        outcode1 = (int) ComputeOutCode(b, boxMin, boxMax);
                    }
                }
            }
            return accept;
        }
    }
}