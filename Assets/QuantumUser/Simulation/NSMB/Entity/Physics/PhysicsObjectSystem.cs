using Photon.Deterministic;
using Quantum.Collections;
using Quantum.Profiling;
using Quantum.Task;
using System;
using UnityEngine;

namespace Quantum {
    public unsafe class PhysicsObjectSystem : SystemArrayFilter<PhysicsObjectSystem.Filter>, ISignalOnTryLiquidSplash, ISignalOnEntityEnterExitLiquid {

        public static readonly FP RaycastSkin = FP.FromString("0.1");
        public static readonly FP Skin = FP.FromString("0.001");
        public static readonly FP GroundMaxAngle = FP.FromString("0.07612"); // 22.5 degrees

        private TaskDelegateHandle sendEventTaskHandle;

        public struct Filter {
            public EntityRef Entity;
            public Transform2D* Transform;
            public PhysicsObject* PhysicsObject;
            public PhysicsCollider2D* Collider;
        }

        protected override void OnInitUser(Frame f) {
            f.Context.EntityAndPlayerMask = ~f.Layers.GetLayerMask("Entity", "Player");
            f.Context.TaskContext.RegisterDelegate(SendEventsTask, $"{GetType().Name}.SendEvents", ref sendEventTaskHandle);
        }

        protected override TaskHandle Schedule(Frame f, TaskHandle taskHandle) {
            TaskHandle moveObjectsTask = base.Schedule(f, taskHandle);
            return f.Context.TaskContext.AddSingletonTask(sendEventTaskHandle, null, moveObjectsTask);
        }

        public override void Update(FrameThreadSafe f, ref Filter filter) {
            var stage = f.FindAsset<VersusStageData>(f.Map.UserAsset);
            var physicsObject = filter.PhysicsObject;
            if (physicsObject->IsFrozen) {
                return;
            }
            
            var transform = filter.Transform;
            var entity = filter.Entity;

            bool wasTouchingGround = physicsObject->IsTouchingGround;
            bool canSnap = wasTouchingGround && physicsObject->Velocity.Y <= physicsObject->PreviousFrameVelocity.Y;
            physicsObject->PreviousFrameVelocity = physicsObject->Velocity;

            QList<PhysicsContact> contacts = f.ResolveList(physicsObject->Contacts);
            MoveWithPlatform(f, ref filter, contacts);
            for (int i = 0; i < contacts.Count; i++) {
                var contact = contacts[i];
                if (contact.Frame < f.Number) {
                    contacts.RemoveAtUnordered(i);
                    i--;
                }
            }

            if (physicsObject->SlowInLiquids && physicsObject->IsUnderwater) {
                physicsObject->Velocity = FPVector2.Clamp(physicsObject->Velocity, 
                    new FPVector2(-Constants.OnePixelPerFrame, -Constants.OnePixelPerFrame),
                    new FPVector2(Constants.OnePixelPerFrame, Constants.OnePixelPerFrame));
            }

            FPVector2 previousPosition = transform->Position;
            if (FPMath.Abs(physicsObject->Velocity.X + physicsObject->ParentVelocity.X) > FPMath.Abs(physicsObject->Velocity.Y + physicsObject->ParentVelocity.Y)) {
                physicsObject->Velocity = MoveHorizontally(f, physicsObject->Velocity.X + physicsObject->ParentVelocity.X, entity, stage, contacts);
                physicsObject->Velocity = MoveVertically(f, physicsObject->Velocity.Y + physicsObject->ParentVelocity.Y, entity, stage, contacts);
            } else {
                physicsObject->Velocity = MoveVertically(f, physicsObject->Velocity.Y + physicsObject->ParentVelocity.Y, entity, stage, contacts);
                physicsObject->Velocity = MoveHorizontally(f, physicsObject->Velocity.X + physicsObject->ParentVelocity.X, entity, stage, contacts);
            }
            ResolveContacts(f, stage, physicsObject, contacts);

            if (!physicsObject->DisableCollision && canSnap && !physicsObject->IsTouchingGround) {
                // Try snapping
                FPVector2 previousVelocity = physicsObject->Velocity;
                physicsObject->Velocity = MoveVertically(f, -FP._0_25 * f.UpdateRate, entity, stage, contacts);
                ResolveContacts(f, stage, physicsObject, contacts);

                if (!physicsObject->IsTouchingGround) {
                    transform->Position.Y = previousPosition.Y;
                    physicsObject->Velocity = previousVelocity;
                    physicsObject->Velocity.Y = 0;
                    physicsObject->HoverFrames = 3;
                }
            }

            /*
#if DEBUG
            foreach (var contact in contacts) {
                Draw.Ray(contact.Position, contact.Normal, ColorRGBA.Red);
            }
#endif
            */

            if (QuantumUtils.Decrement(ref physicsObject->HoverFrames)) {
                // Apply gravity
                physicsObject->Velocity += physicsObject->Gravity * f.DeltaTime;
            }
            /*
            if (f.Has<MarioPlayer>(entity)) {
                Debug.Log(contacts.Count + " - " + wasTouchingGround + " -> " + physicsObject->IsTouchingGround);
            }
            */
            physicsObject->Velocity.Y = FPMath.Max(physicsObject->Velocity.Y, physicsObject->TerminalVelocity);
            physicsObject->WasTouchingGround = wasTouchingGround;
        }

        public void SendEventsTask(FrameThreadSafe f, int start, int count, void* arg) {
            var filter = f.Filter<PhysicsObject>();
            while (filter.NextUnsafe(out EntityRef entity, out PhysicsObject* physicsObject)) {

                if (!physicsObject->WasTouchingGround && physicsObject->IsTouchingGround) {
                    ((Frame) f).Events.PhysicsObjectLanded((Frame) f, entity);
                }
            }
        }

        private void MoveWithPlatform(FrameThreadSafe f, ref Filter filter, QList<PhysicsContact> contacts) {
            var physicsObject = filter.PhysicsObject;

            FP maxDot = -2;
            FPVector2? maxVelocity = null;
            foreach (var contact in contacts) {
                if (FPVector2.Dot(contact.Normal, -physicsObject->Gravity.Normalized) < GroundMaxAngle
                    || !f.TryGetPointer(contact.Entity, out MovingPlatform* platform)) {
                    continue;
                }

                FPVector2 vel = platform->Velocity;
                FP dot = FPVector2.Dot(vel.Normalized, -physicsObject->Gravity.Normalized);
                if (dot > maxDot || (dot == maxDot && maxVelocity.Value.SqrMagnitude > vel.SqrMagnitude)) {
                    maxDot = dot;
                    maxVelocity = vel;
                }
            }

            FPVector2 adjustment = physicsObject->ParentVelocity - (maxVelocity ?? FPVector2.Zero);
            adjustment.Y = 0;

            physicsObject->Velocity += adjustment;
            physicsObject->ParentVelocity = maxVelocity ?? FPVector2.Zero;
        }

        public static FPVector2 MoveVertically(FrameThreadSafe f, FP relativeVelocityY, EntityRef entity, VersusStageData stage, QList<PhysicsContact>? contacts = default) {
            var physicsObject = f.GetPointer<PhysicsObject>(entity);
            var mask = ((Frame) f).Context.EntityAndPlayerMask;

            FP velocityY = relativeVelocityY * f.DeltaTime;
            if (velocityY == 0) {
                return physicsObject->Velocity;
            }

            if (!contacts.HasValue) {
                contacts = f.ResolveList(physicsObject->Contacts);
            }

            var transform = f.GetPointer<Transform2D>(entity);

            FPVector2 directionVector = velocityY > 0 ? FPVector2.Up : FPVector2.Down;

            if (!physicsObject->DisableCollision) {
                var collider = f.GetPointer<PhysicsCollider2D>(entity);
                Shape2D shape = collider->Shape;

                FPVector2 position = transform->Position;
                FPVector2 raycastOrigin = position - (directionVector * RaycastSkin);
                FPVector2 raycastTranslation = new FPVector2(0, velocityY) + (directionVector * (RaycastSkin * 2 + Skin));

                var physicsHits = f.Physics2D.ShapeCastAll(raycastOrigin, 0, &shape, raycastTranslation, mask, QueryOptions.HitAll | QueryOptions.ComputeDetailedInfo);

                FP center = transform->Position.X + shape.Centroid.X;
                if (center < (stage.StageWorldMin.X + stage.StageWorldMax.X) / 2) {
                    // Left edge
                    FPVector2 wrappedRaycastOrigin = raycastOrigin;
                    wrappedRaycastOrigin.X += stage.TileDimensions.x / (FP) 2;
                    var wrappedHits = f.Physics2D.ShapeCastAll(wrappedRaycastOrigin, 0, &shape, raycastTranslation, mask, QueryOptions.HitAll | QueryOptions.ComputeDetailedInfo);
                    for (int i = 0; i < wrappedHits.Count; i++) {
                        physicsHits.Add(wrappedHits[i], f.Context);
                    }
                } else {
                    // Right edge
                    FPVector2 wrappedRaycastOrigin = raycastOrigin;
                    wrappedRaycastOrigin.X -= stage.TileDimensions.x / (FP) 2;
                    var wrappedHits = f.Physics2D.ShapeCastAll(wrappedRaycastOrigin, 0, &shape, raycastTranslation, mask, QueryOptions.HitAll | QueryOptions.ComputeDetailedInfo);
                    for (int i = 0; i < wrappedHits.Count; i++) {
                        physicsHits.Add(wrappedHits[i], f.Context);
                    }
                }

                physicsHits.SortCastDistance();

                position += shape.Centroid;
                FP checkPointY = position.Y + shape.Box.Extents.Y * (velocityY > 0 ? 1 : -1);
                FPVector2 leftWorldCheckPoint = new(position.X - shape.Box.Extents.X, checkPointY);
                FPVector2 rightWorldCheckPoint = new(position.X + shape.Box.Extents.X, checkPointY);

                // Move in the direction and check for any intersections with tiles.
                FP left = FPMath.Floor(leftWorldCheckPoint.X * 2) / 2;
                FP right = FPMath.Floor(rightWorldCheckPoint.X * 2) / 2;
                FP start = FPMath.Floor(checkPointY * 2) / 2;
                FP end = FPMath.Floor((checkPointY + velocityY + (directionVector.Y * Skin)) * 2) / 2;
                FP direction = directionVector.Y;

                Span<FPVector2> vertexBuffer = stackalloc FPVector2[128];
                Span<int> shapeVertexCountBuffer = stackalloc int[16];
                Span<PhysicsContact> contactBuffer = stackalloc PhysicsContact[32];

                for (FP y = start; (direction > 0 ? (y <= end) : (y >= end)); y += direction / 2) {

                    Span<PhysicsContact> potentialContacts = stackalloc PhysicsContact[32];
                    int potentialContactCount = 0;

                    for (FP x = left; x <= right; x += FP._0_50) {
                        FPVector2 worldPos = new FPVector2(x, y) + (FPVector2.One / 4);
                        StageTileInstance tile = stage.GetTileWorld((Frame) f, worldPos);
                        Vector2Int tilePos = QuantumUtils.WorldToRelativeTile(stage, worldPos);

                        tile.GetWorldPolygons(f, vertexBuffer, shapeVertexCountBuffer, out StageTile stageTile, worldPos);

                        int shapeIndex = 0;
                        int vertexIndex = 0;
                        int shapeVertexCount;
                        while ((shapeVertexCount = shapeVertexCountBuffer[shapeIndex++]) > 0) {
                            Span<FPVector2> polygon = vertexBuffer[vertexIndex..(vertexIndex + shapeVertexCount)];
                            vertexIndex += shapeVertexCount;

                            int polygonContacts = LineSweepPolygonIntersection(contactBuffer,
                                leftWorldCheckPoint, rightWorldCheckPoint, directionVector, polygon, stageTile.IsPolygon);

                            for (int i = 0; i < polygonContacts; i++) {
                                PhysicsContact newContact = contactBuffer[i];
                                newContact.Frame = f.Number;
                                newContact.TileX = tilePos.x;
                                newContact.TileY = tilePos.y;

                                potentialContacts[potentialContactCount++] = newContact;
                            }
                        }
                    }

                    for (int i = 0; i < physicsHits.Count; i++) {
                        var hit = physicsHits[i];
                        if (hit.Point.Y < y || hit.Point.Y > y + FP._0_50) {
                            // Not a valid hit
                            continue;
                        }
                        if (hit.IsDynamic && f.TryGetPointer(hit.Entity, out Liquid* liquid)) {
                            if (liquid->LiquidType != LiquidType.Water || !physicsObject->IsWaterSolid || FPVector2.Dot(hit.Normal, FPVector2.Up) < GroundMaxAngle) {
                                // Colliding with water and we cant interact
                                continue;
                            }
                        }
                        if (hit.IsDynamic && hit.TryGetShape(f, out Shape2D* hitShape)) {
                            FPVector2 upDirection = FPVector2.Rotate(FPVector2.Up, hitShape->LocalTransform.Rotation);
                            if (hitShape->Type == Shape2DType.Edge && FPVector2.Dot(hit.Normal, upDirection) <= GroundMaxAngle) {
                                // Not a valid hit (semisolid)
                                continue;
                            }
                        }

                        potentialContacts[potentialContactCount++] = new PhysicsContact {
                            Distance = FPMath.Abs(hit.CastDistanceNormalized * raycastTranslation.Y) - (RaycastSkin + Skin),
                            Normal = hit.Normal,
                            Position = hit.Point,
                            Frame = f.Number,
                            TileX = -1,
                            TileY = -1,
                            Entity = hit.Entity,
                        };
                    }

                    if (potentialContactCount == 0) {
                        continue;
                    }

                    // Get n-lowest contacts (within tolerance)
                    QuickSortSpan(potentialContacts, 0, potentialContactCount - 1);
                    FP tolerance = 0;
                    FP? min = null;
                    FPVector2 avgNormal = FPVector2.Zero;
                    int contactCount = 0;

                    Span<PhysicsContact> removedContacts = stackalloc PhysicsContact[32];
                    int removedContactCount = 0;

                    for (int i = 0; i < potentialContactCount; i++) {
                        var contact = potentialContacts[i];
                        bool earlyContinue = false;
                        for (int j = 0; j < removedContactCount; j++) {
                            if (contact.Equals(removedContacts[j])) {
                                earlyContinue = true;
                                break;
                            }
                        }

                        if (earlyContinue
                            || (min.HasValue && contact.Distance - min.Value > tolerance)
                            || contact.Distance > (FPMath.Abs(velocityY) + RaycastSkin + Skin)
                            /* || removedContacts.Contains(contact) */
                            /* || FPVector2.Dot(contact.Normal, directionVector) > 0 */) {
                            continue;
                        }
                        
                        bool keepContact = true;
                        foreach (var callback in ((Frame) f).Context.PreContactCallbacks) {
                            callback?.Invoke(f, stage, entity, contact, ref keepContact);
                        }
                        
                        if (keepContact) {
                            contacts.Value.Add(contact);
                            min ??= contact.Distance;
                            avgNormal += contact.Normal;
                            contactCount++;
                        } else {
                            removedContacts[removedContactCount++] = contact;
                        }
                    }

                    if (contactCount <= 0) {
                        continue;
                    }

                    avgNormal /= contactCount;

                    // Snap to point.
                    transform->Position += directionVector * (min.Value - Skin);

                    // Readjust the remaining velocity
                    min -= physicsObject->ParentVelocity.Y;
                    FP remainingVelocity = physicsObject->Velocity.Magnitude - min.Value;
                    FPVector2 newDirection = new(-avgNormal.Y, avgNormal.X);

                    // Only care about the Y aspect to not slide up/down hills via gravity
                    FPVector2 newVelocity = physicsObject->Velocity;
                    newVelocity.Y = Project(physicsObject->Velocity.Normalized * remainingVelocity, newDirection).Y;
                    return newVelocity;
                }
            }

            // Good to move
            transform->Position += directionVector * FPMath.Abs(velocityY);
            physicsObject->FloorAngle = 0;
            return physicsObject->Velocity;
        }

        public static FPVector2 MoveHorizontally(FrameThreadSafe f, FP velocityX, EntityRef entity, VersusStageData stage, QList<PhysicsContact>? contacts = null) {
            var physicsObject = f.GetPointer<PhysicsObject>(entity);
            var mask = ((Frame) f).Context.EntityAndPlayerMask;

            velocityX *= f.DeltaTime;
            if (velocityX == 0) {
                return physicsObject->Velocity;
            }

            if (!contacts.HasValue) {
                contacts = f.ResolveList(physicsObject->Contacts);
            }

            var transform = f.GetPointer<Transform2D>(entity);

            FPVector2 directionVector = velocityX > 0 ? FPVector2.Right : FPVector2.Left;

            if (!physicsObject->DisableCollision) {
                var collider = f.GetPointer<PhysicsCollider2D>(entity);
                Shape2D shape = collider->Shape;
                
                FPVector2 position = transform->Position;
                FPVector2 raycastOrigin = position - (directionVector * RaycastSkin);
                FPVector2 raycastTranslation = new FPVector2(velocityX, 0) + (directionVector * (RaycastSkin * 2 + Skin));

                var physicsHits = f.Physics2D.ShapeCastAll(raycastOrigin, 0, &shape, raycastTranslation, mask, QueryOptions.HitAll | QueryOptions.ComputeDetailedInfo);

                FP center = transform->Position.X + shape.Centroid.X;
                if (center < (stage.StageWorldMin.X + stage.StageWorldMax.X) / 2) {
                    // Left edge
                    FPVector2 wrappedRaycastOrigin = raycastOrigin;
                    wrappedRaycastOrigin.X += stage.TileDimensions.x / (FP) 2;
                    var wrappedHits = f.Physics2D.ShapeCastAll(wrappedRaycastOrigin, 0, &shape, raycastTranslation, mask, QueryOptions.HitAll | QueryOptions.ComputeDetailedInfo);
                    for (int i = 0; i < wrappedHits.Count; i++) {
                        physicsHits.Add(wrappedHits[i], f.Context);
                    }
                } else {
                    // Right edge
                    FPVector2 wrappedRaycastOrigin = raycastOrigin;
                    wrappedRaycastOrigin.X -= stage.TileDimensions.x / (FP) 2;
                    var wrappedHits = f.Physics2D.ShapeCastAll(wrappedRaycastOrigin, 0, &shape, raycastTranslation, mask, QueryOptions.HitAll | QueryOptions.ComputeDetailedInfo);
                    for (int i = 0; i < wrappedHits.Count; i++) {
                        physicsHits.Add(wrappedHits[i], f.Context);
                    }
                }

                physicsHits.SortCastDistance();

                position += shape.Centroid;
                FP checkPointX = position.X + shape.Box.Extents.X * (velocityX > 0 ? 1 : -1);
                FPVector2 bottomWorldCheckPoint = new(checkPointX, position.Y - shape.Box.Extents.Y);
                FPVector2 topWorldCheckPoint = new(checkPointX, position.Y + shape.Box.Extents.Y);

                // Move in the direction and check for any intersections with tiles.
                FP bottom = FPMath.Floor(bottomWorldCheckPoint.Y * 2) / 2;
                FP top = FPMath.Floor(topWorldCheckPoint.Y * 2) / 2;
                FP start = FPMath.Floor(checkPointX * 2) / 2;
                FP end = FPMath.Floor((checkPointX + velocityX + (directionVector.X * Skin)) * 2) / 2;
                FP direction = directionVector.X;

                Span<FPVector2> vertexBuffer = stackalloc FPVector2[128];
                Span<int> shapeVertexCountBuffer = stackalloc int[16];
                Span<PhysicsContact> contactBuffer = stackalloc PhysicsContact[32];

                for (FP x = start; (direction > 0 ? (x <= end) : (x >= end)); x += direction / 2) {
                    
                    Span<PhysicsContact> potentialContacts = stackalloc PhysicsContact[32];
                    int potentialContactCount = 0;

                    for (FP y = bottom; y <= top; y += FP._0_50) {
                        FPVector2 worldPos = new FPVector2(x, y) + (FPVector2.One / 4);
                        StageTileInstance tile = stage.GetTileWorld((Frame) f, worldPos);
                        Vector2Int tilePos = QuantumUtils.WorldToRelativeTile(stage, worldPos);

                        tile.GetWorldPolygons(f, vertexBuffer, shapeVertexCountBuffer, out StageTile stageTile, worldPos);

                        int shapeIndex = 0;
                        int vertexIndex = 0;
                        int shapeVertexCount;
                        while ((shapeVertexCount = shapeVertexCountBuffer[shapeIndex++]) > 0) {
                            Span<FPVector2> polygon = vertexBuffer[vertexIndex..(vertexIndex + shapeVertexCount)];
                            vertexIndex += shapeVertexCount;

                            int polygonContacts = LineSweepPolygonIntersection(contactBuffer, bottomWorldCheckPoint,
                                topWorldCheckPoint, directionVector, polygon, stageTile.IsPolygon);

                            for (int i = 0; i < polygonContacts; i++) {
                                PhysicsContact newContact = contactBuffer[i];
                                newContact.TileX = tilePos.x;
                                newContact.TileY = tilePos.y;

                                potentialContacts[potentialContactCount++] = newContact;
                            }
                        }
                    }

                    for (int i = 0; i < physicsHits.Count; i++) {
                        var hit = physicsHits[i];
                        FPVector2 wrappedPoint = QuantumUtils.WrapWorld(stage, hit.Point, out _);
                        if (wrappedPoint.X < x || wrappedPoint.X > x + FP._0_50) {
                            // Not a valid hit (for this tile)
                            continue;
                        }
                        if (hit.IsDynamic && f.TryGetPointer(hit.Entity, out Liquid* liquid)) {
                            if (liquid->LiquidType != LiquidType.Water || !physicsObject->IsWaterSolid || FPVector2.Dot(hit.Normal, FPVector2.Up) < GroundMaxAngle) {
                                // Colliding with water and we cant interact
                                continue;
                            }
                        }
                        if (hit.IsDynamic && hit.TryGetShape(f, out Shape2D* hitShape)) {
                            FPVector2 upDirection = FPVector2.Rotate(FPVector2.Up, hitShape->LocalTransform.Rotation * FP.Deg2Rad);
                            if (hitShape->Type == Shape2DType.Edge && FPVector2.Dot(hit.Normal, upDirection) <= GroundMaxAngle) {
                                // Not a valid hit (semisolid)
                                continue;
                            }
                        }

                        potentialContacts[potentialContactCount++] = new PhysicsContact {
                            Distance = FPMath.Abs(hit.CastDistanceNormalized * raycastTranslation.X) - (RaycastSkin + Skin),
                            Normal = hit.Normal,
                            Position = hit.Point,
                            Frame = f.Number,
                            TileX = -1,
                            TileY = -1,
                            Entity = hit.Entity,
                        };
                    }

                    if (potentialContactCount == 0) {
                        continue;
                    }

                    // Get n-lowest contacts (within tolerance)
                    QuickSortSpan(potentialContacts, 0, potentialContactCount - 1);
                    FP tolerance = 0;
                    FP? min = null;
                    FPVector2 avgNormal = FPVector2.Zero;
                    int contactCount = 0;

                    Span<PhysicsContact> removedContacts = stackalloc PhysicsContact[32];
                    int removedContactCount = 0;

                    for (int i = 0; i < potentialContactCount; i++) {
                        var contact = potentialContacts[i];
                        bool earlyContinue = false;
                        for (int j = 0; j < removedContactCount; j++) {
                            if (contact.Equals(removedContacts[j])) {
                                earlyContinue = true;
                                break;
                            }
                        }

                        if (earlyContinue
                            || (min.HasValue && contact.Distance - min.Value > tolerance)
                            || contact.Distance > (FPMath.Abs(velocityX) + RaycastSkin + Skin)
                            /* || removedContacts.Contains(contact) */
                            /* || FPVector2.Dot(contact.Normal, directionVector) > 0 */) {
                            continue;
                        }

                        bool keepContact = true;
                        foreach (var callback in ((Frame) f).Context.PreContactCallbacks) {
                            callback?.Invoke(f, stage, entity, contact, ref keepContact);
                        }

                        if (keepContact) {
                            contacts.Value.Add(contact);
                            min ??= contact.Distance;
                            avgNormal += contact.Normal;
                            contactCount++;
                        } else {
                            removedContacts[removedContactCount++] = contact;
                        }
                    }

                    if (contactCount <= 0) {
                        continue;
                    }

                    avgNormal /= contactCount;

                    // Snap to point.
                    transform->Position += directionVector * (min.Value - Skin);

                    // Readjust the remaining velocity
                    FP remainingVelocity = physicsObject->Velocity.Magnitude - min.Value;
                    FPVector2 newDirection = new(-avgNormal.Y, avgNormal.X);

                    FPVector2 newVelocity = Project(physicsObject->Velocity.Normalized * remainingVelocity, newDirection);
                    if (FPMath.Abs(FPVector2.Dot(newDirection, FPVector2.Right)) > GroundMaxAngle) {
                        newVelocity.X = velocityX / f.DeltaTime;
                    }
                    return newVelocity;
                }
            }

            // Good to move
            transform->Position += directionVector * FPMath.Abs(velocityX);
            return physicsObject->Velocity;
        }

        private void ResolveContacts(FrameThreadSafe f, VersusStageData stage, PhysicsObject* physicsObject, QList<PhysicsContact> contacts) {

            physicsObject->FloorAngle = 0;
            physicsObject->IsTouchingGround = false;
            physicsObject->IsTouchingCeiling = false;
            physicsObject->IsTouchingLeftWall = false;
            physicsObject->IsTouchingRightWall = false;
            physicsObject->IsOnSlideableGround = false;
            physicsObject->IsOnSlipperyGround = false;

            foreach (var contact in contacts) {
                FP horizontalDot = FPVector2.Dot(contact.Normal, FPVector2.Right);
                if (horizontalDot > (1 - GroundMaxAngle)) {
                    physicsObject->IsTouchingLeftWall = true;

                } else if (horizontalDot < -(1 - GroundMaxAngle)) {
                    physicsObject->IsTouchingRightWall = true;
                }

                FP verticalDot = FPVector2.Dot(contact.Normal, FPVector2.Up);
                if (verticalDot > GroundMaxAngle) {
                    physicsObject->IsTouchingGround = true;

                    FP angle = FPVector2.RadiansSignedSkipNormalize(contact.Normal, FPVector2.Up) * FP.Rad2Deg;
                    if (FPMath.Abs(physicsObject->FloorAngle) < FPMath.Abs(angle)) {
                        physicsObject->FloorAngle = angle;
                    }

                    if (contact.TileX != -1 && contact.TileY != -1 && f.TryFindAsset(stage.GetTileRelative((Frame) f, contact.TileX, contact.TileY).Tile, out StageTile tile)) {
                        physicsObject->IsOnSlideableGround |= tile.IsSlideableGround;
                        physicsObject->IsOnSlipperyGround |= tile.IsSlipperyGround;
                    }

                } else if (verticalDot < -GroundMaxAngle) {
                    physicsObject->IsTouchingCeiling = true;
                }
            }
        }

        public static bool Raycast(FrameThreadSafe f, VersusStageData stage, FPVector2 worldPos, FPVector2 direction, FP maxDistance, out PhysicsContact contact) {
            if (stage == null) {
                stage = f.FindAsset<VersusStageData>(f.Map.UserAsset);
            }

            contact = default;
            FPVector2 stepSize = new(
                direction.X == 0 ? 0 : FPMath.Sqrt(1 + (direction.Y / direction.X) * (direction.Y / direction.X)),
                direction.Y == 0 ? 0 : FPMath.Sqrt(1 + (direction.X / direction.Y) * (direction.X / direction.Y))
            );
            FPVector2 rayLength = default;
            Vector2Int step = default;

            if (direction.X < 0) {
                step.x = -1;
                rayLength.X = (worldPos.X - FPMath.Floor(worldPos.X * 2) / 2) * stepSize.X;
            } else if (direction.X > 0) {
                step.x = 1;
                rayLength.X = (FPMath.Floor(worldPos.X * 2 + 1) / 2 - worldPos.X) * stepSize.X;
            } else {
                step.x = 0;
                rayLength.X = maxDistance;
            }

            if (direction.Y < 0) {
                step.y = -1;
                rayLength.Y = (worldPos.Y - FPMath.Floor(worldPos.Y * 2) / 2) * stepSize.Y;
            } else if (direction.Y > 0) {
                step.y = 1;
                rayLength.Y = (FPMath.Floor(worldPos.Y * 2 + 1) / 2 - worldPos.Y) * stepSize.Y;
            } else {
                step.y = 0;
                rayLength.Y = maxDistance;
            }

            Span<FPVector2> vertexBuffer = stackalloc FPVector2[128];
            Span<int> shapeVertexCountBuffer = stackalloc int[16];
            Span<PhysicsContact> contactBuffer = stackalloc PhysicsContact[32];

            Vector2Int tilePosition = QuantumUtils.WorldToRelativeTile(stage, worldPos);
            FP distance = 0;
            while (distance < maxDistance) {
                if (rayLength.X < rayLength.Y) {
                    tilePosition.x += step.x;
                    distance = rayLength.X;
                    rayLength.X += stepSize.X;
                } else {
                    tilePosition.y += step.y;
                    distance = rayLength.Y;
                    rayLength.Y += stepSize.Y;
                }

                StageTileInstance tile = stage.GetTileRelative((Frame) f, tilePosition.x, tilePosition.y);
                tile.GetWorldPolygons(f, vertexBuffer, shapeVertexCountBuffer, out StageTile stageTile, QuantumUtils.RelativeTileToWorldRounded(stage, tilePosition));

                int shapeIndex = 0;
                int vertexIndex = 0;
                int shapeVertexCount;
                while ((shapeVertexCount = shapeVertexCountBuffer[shapeIndex++]) > 0) {
                    Span<FPVector2> polygon = vertexBuffer[vertexIndex..(vertexIndex + shapeVertexCount)];
                    vertexIndex += shapeVertexCount;

                    if (TryRayPolygonIntersection(worldPos, direction, polygon, stageTile.IsPolygon, out contact)) {
                        goto finish;
                    }
                }
            }

            finish:
            var nullableHit = f.Physics2D.Raycast(worldPos, direction, maxDistance, ((Frame) f).Context.EntityAndPlayerMask, QueryOptions.HitAll & ~QueryOptions.HitTriggers | QueryOptions.ComputeDetailedInfo);
            if (nullableHit.HasValue) {
                var hit = nullableHit.Value;
                FP hitDistance = hit.CastDistanceNormalized * maxDistance;
                if (hitDistance < contact.Distance || contact.Distance == 0) {
                    contact = new PhysicsContact {
                        Distance = distance,
                        Normal = hit.Normal,
                        Position = hit.Point,
                        TileX = -1,
                        TileY = -1,
                        Entity = hit.Entity,
                        Frame = f.Number,
                    };
                }
            }
            return contact.Distance > 0 && contact.Distance <= maxDistance;
        }

        private static int LineSweepPolygonIntersection(Span<PhysicsContact> contactBuffer, FPVector2 a, FPVector2 b, FPVector2 direction, Span<FPVector2> polygon, bool isPolygon) {
            if (polygon.Length <= 1) {
                throw new ArgumentException("Polygon must have at least 2 points!");
            }

            if (direction.X < 0 || direction.Y > 0) {
                (a, b) = (b, a);
            }

            int count = 0;

            // Raycast in the direction for both a and b first
            if (TryRayPolygonIntersection(a, direction, polygon, isPolygon, out var contact)) {
                if (FPVector2.Dot(contact.Normal, (b - contact.Position).Normalized) > 0) {
                    contactBuffer[count++] = contact;
                }
            }

            if (TryRayPolygonIntersection(b, direction, polygon, isPolygon, out contact)) {
                if (FPVector2.Dot(contact.Normal, (a - contact.Position).Normalized) > 0) {
                    contactBuffer[count++] = contact;
                }
            }

            // Then raycast in the opposite direction for all polygon vertices
            int length = polygon.Length;
            for (int i = 0; i < length; i++) {
                var point = polygon[i];
                if (!TryRayLineIntersection(point, -direction, b, a, out contact)) {
                    continue;
                }

                bool valid = false;
                if ((length == 2 || !isPolygon) && (i == 0 || i == length - 1)) {
                    /*
                    if (i == 0) {
                        valid = FPVector2.Dot(GetNormal(polygon[i], polygon[i + 1]), direction) < 0;
                    } else {
                        valid = FPVector2.Dot(GetNormal(polygon[i - 1], polygon[i]), direction) < 0;
                    }
                    */
                } else {
                    valid |= FPVector2.Dot(GetNormal(point, polygon[(i + 1) % polygon.Length]), direction) < 0;
                    valid |= FPVector2.Dot(GetNormal(polygon[(i - 1 + polygon.Length) % polygon.Length], point), direction) < 0;
                }

                if (valid) {
                    contact.Normal *= -1; // Inverted normals
                    contactBuffer[count++] = contact;
                }
            }

            return count;
        }

        private static FPVector2 GetNormal(FPVector2 a, FPVector2 b) {
            FPVector2 diff = b - a;
            return new FPVector2(-diff.Y, diff.X);
        }

        private static bool TryRayPolygonIntersection(FPVector2 rayOrigin, FPVector2 rayDirection, Span<FPVector2> polygon, bool isPolygon, out PhysicsContact contact) {
            bool hit = false;
            contact = default;
            contact.Distance = FP.MaxValue;

            int length = polygon.Length;
            if (length <= 1) {
                return false;
            }
            if (length == 2 || !isPolygon) {
                length--;
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

        public static bool PointIsInsideBox(FPVector2 boxOrigin, Shape2D box, FPVector2 testPosition) {
            FPVector2 extents = box.Box.Extents;
            FPVector2 origin = boxOrigin + box.Centroid;
            FPVector2 boxMin = origin - extents;
            FPVector2 boxMax = origin + extents;

            FPVector2[] boxCorners = {
                new(origin.X - extents.X, origin.Y + extents.Y),
                boxMax,
                new(origin.X + extents.X, origin.Y - extents.Y),
                boxMin,
            };

            return PointIsInsidePolygon(testPosition, boxCorners);
        }

        public static bool BoxInGround(FrameThreadSafe f, FPVector2 position, Shape2D shape, bool includeMegaBreakable = true, VersusStageData stage = null, EntityRef entity = default) {
            using var profilerScope = HostProfiler.Start("PhysicsObjectSystem.BoxInGround");
            // In a solid hitbox
            var hits = f.Physics2D.OverlapShape(position, 0, shape, ((Frame) f).Context.EntityAndPlayerMask, ~QueryOptions.HitTriggers);
            f.TryGetPointer(entity, out MarioPlayer* mario);
            for (int i = 0; i < hits.Count; i++) {
                var hit = hits.HitsBuffer[i];
                Shape2D* hitShape = hit.GetShape(f);

                if (hit.Entity != entity
                    && (mario == null || hit.Entity != mario->HeldEntity)
                    && (!includeMegaBreakable || !f.Has<IceBlock>(hit.Entity))
                    && (hitShape->Type != Shape2DType.Edge)) {

                    return true;
                }
            }

            if (!stage) {
                stage = f.FindAsset<VersusStageData>(f.Map.UserAsset);
            }
            var extents = shape.Box.Extents;

            FPVector2 origin = position + shape.Centroid;
            FPVector2 boxMin = origin - extents;
            FPVector2 boxMax = origin + extents;

            Span<FPVector2> boxCorners = stackalloc FPVector2[4];
            boxCorners[0] = new(origin.X - extents.X, origin.Y + extents.Y);
            boxCorners[1] = boxMax;
            boxCorners[2] = new(origin.X + extents.X, origin.Y - extents.Y);
            boxCorners[3] = boxMin;

            Span<FPVector2> vertexBuffer = stackalloc FPVector2[128];
            Span<int> shapeVertexCountBuffer = stackalloc int[16];
            Span<PhysicsContact> contactBuffer = stackalloc PhysicsContact[32];
            
            Span<LocationTilePair> tiles = stackalloc LocationTilePair[64];
            int overlappingTiles = GetTilesOverlappingHitbox(f, position, shape, tiles, stage);

            for (int i = 0; i < overlappingTiles; i++) {
                StageTileInstance tile = tiles[i].Tile;
                StageTile stageTile = f.FindAsset(tile.Tile);
                if (!stageTile
                    || !stageTile.IsPolygon
                    || (!includeMegaBreakable && stageTile is BreakableBrickTile breakable && breakable.BreakingRules.HasFlag(BreakableBrickTile.BreakableBy.MegaMario))) {
                    continue;
                }
                FPVector2 worldPos = QuantumUtils.RelativeTileToWorldRounded(stage, tiles[i].Position);
                tile.GetWorldPolygons(f, stage, stageTile, vertexBuffer, shapeVertexCountBuffer, worldPos);

                int shapeIndex = 0;
                int vertexIndex = 0;
                int shapeVertexCount;
                while ((shapeVertexCount = shapeVertexCountBuffer[shapeIndex++]) > 0) {
                    Span<FPVector2> polygon = vertexBuffer[vertexIndex..(vertexIndex + shapeVertexCount)];
                    vertexIndex += shapeVertexCount;

                    if (polygon.Length <= 2) {
                        continue;
                    }

                    foreach (var corner in boxCorners) {
                        if (PointIsInsidePolygon(corner, polygon)) {
                            return true;
                        }
                    }
                    for (int j = 0; j < polygon.Length; j++) {
                        if (LineIntersectsBox(polygon[j], polygon[(j + 1) % polygon.Length], boxMin, boxMax)) {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        public struct LocationTilePair {
            public Vector2Int Position;
            public StageTileInstance Tile;
        }

        public static int GetTilesOverlappingHitbox(FrameThreadSafe f, FPVector2 position, Shape2D shape, Span<LocationTilePair> buffer, VersusStageData stage = null) {
            using var profilerScope = HostProfiler.Start("PhysicsObjectSystem.GetTilesOverlappingHitbox");
            if (!stage) {
                stage = f.FindAsset<VersusStageData>(f.Map.UserAsset);
            }
            var extents = shape.Box.Extents;

            FPVector2 origin = position + shape.Centroid;
            Vector2Int min = QuantumUtils.WorldToRelativeTile(stage, origin - extents);
            Vector2Int max = QuantumUtils.WorldToRelativeTile(stage, origin + extents);

            int count = 0;
            for (int x = min.x; x <= max.x; x++) {
                for (int y = min.y; y <= max.y; y++) {
                    buffer[count++] = new LocationTilePair {
                        Position = new Vector2Int(x, y),
                        Tile = stage.GetTileRelative((Frame) f, x, y)
                    };

                    if (count == buffer.Length) {
                        return count;
                    }
                }
            }
            return count;
        }

        public static bool TryEject(FrameThreadSafe f, EntityRef entity, VersusStageData stage = null) {
            var transform = f.GetPointer<Transform2D>(entity);
            var collider = f.GetPointer<PhysicsCollider2D>(entity);

            if (!stage) {
                stage = f.FindAsset<VersusStageData>(f.Map.UserAsset);
            }

            if (!BoxInGround((FrameThreadSafe) f, transform->Position, collider->Shape, stage: stage, entity: entity)) {
                return true;
            }

            int angle = 45;
            int increments = 360 / angle;
            FP distIncrement = FP._0_10;
            FP distMax = FP._0_50 + FP._0_10;

            Span<FPVector2> offsets = stackalloc FPVector2[8];
            for (int i = 0; i < increments; i++) {
                FP radAngle = ((i * angle * 2) + ((i / 4) * angle) % 360) * FP.Deg2Rad;
                offsets[i] = new FPVector2(FPMath.Sin(radAngle), FPMath.Cos(radAngle));
            }

            FP dist = 0;
            while ((dist += distIncrement) < distMax) {
                for (int i = 0; i < increments; i++) {
                    FPVector2 checkPos = transform->Position + (offsets[i] * dist);
                    if (BoxInGround((FrameThreadSafe) f, checkPos, collider->Shape, stage: stage, entity: entity)) {
                        continue;
                    }

                    // Valid spot.
                    transform->Position = checkPos;
                    return true;
                }
            }

            return false;
        }

        private static bool PointIsInsidePolygon(FPVector2 point, Span<FPVector2> polygon) {
            using var profilerScope = HostProfiler.Start("PhysicsObjectSystem.PointIsInsidePolygon");
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
            return result ^ IsCounterClockWise(polygon);
        }

        public static bool IsCounterClockWise(Span<FPVector2> vertices) {
            FPVector2 fPVector = new FPVector2(vertices[1].X - vertices[0].X, vertices[1].Y - vertices[0].Y);
            FPVector2 fPVector2 = new FPVector2(vertices[2].X - vertices[1].X, vertices[2].Y - vertices[1].Y);
            return fPVector.X * fPVector2.Y - fPVector.Y * fPVector2.X >= 0;
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
            using var profilerScope = HostProfiler.Start("PhysicsObjectSystem.LineIntersectsBox");
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

        private static void QuickSortSpan(Span<PhysicsContact> span, int lo, int hi) {
            if (lo >= hi) {
                return;
            }

            long rawValue = span[hi].Distance.RawValue;
            int num = lo;
            PhysicsContact contact2;
            for (int i = lo; i < hi; i++) {
                if (span[i].Distance.RawValue < rawValue) {
                    contact2 = span[num];
                    span[num] = span[i];
                    span[i] = contact2;
                    num++;
                }
            }

            contact2 = span[num];
            span[num] = span[hi];
            span[hi] = contact2;
            int num2 = num;
            QuickSortSpan(span, lo, num2 - 1);
            QuickSortSpan(span, num2 + 1, hi);
        }

        public void OnTryLiquidSplash(Frame f, EntityRef entity, EntityRef liquidEntity, QBoolean exit, bool* doSplash) {
            if (!f.Unsafe.TryGetPointer(entity, out PhysicsObject* physicsObject)) {
                return;
            }

            var colliders = f.ResolveHashSet(physicsObject->LiquidContacts);
            if (exit) {
                colliders.Remove(liquidEntity);
            } else {
                colliders.Add(liquidEntity);
            }
        }

        public void OnEntityEnterExitLiquid(Frame f, EntityRef entity, EntityRef liquid, QBoolean underwater) {
            if (!f.Unsafe.TryGetPointer(entity, out PhysicsObject* physicsObject)) {
                return;
            }

            if (underwater) {
                if (physicsObject->UnderwaterCounter++ == 0) {
                    f.Signals.OnEntityChangeUnderwaterState(entity, liquid, true);
                }
            } else {
                if (QuantumUtils.Decrement(ref physicsObject->UnderwaterCounter)) {
                    f.Signals.OnEntityChangeUnderwaterState(entity, liquid, false);
                }
            }
        }
    }
}