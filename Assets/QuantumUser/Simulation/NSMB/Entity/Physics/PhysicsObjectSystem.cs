using Photon.Deterministic;
using Quantum.Collections;
using Quantum.Profiling;
using System;
using UnityEngine;

namespace Quantum {
#if MULTITHREADED
    [UnityEngine.Scripting.Preserve]
    public unsafe class PhysicsObjectSystem : SystemArrayFilter<PhysicsObjectSystem.Filter>, ISignalOnEntityEnterExitLiquid {
#else
    [UnityEngine.Scripting.Preserve]
    public unsafe class PhysicsObjectSystem : SystemMainThread, ISignalOnEntityEnterExitLiquid {
#endif

        public struct Filter {
            public EntityRef Entity;
            public Transform2D* Transform;
            public PhysicsObject* PhysicsObject;
            public PhysicsCollider2D* Collider;
        }

#if MULTITHREADED
        private TaskDelegateHandle sendEventTaskHandle;

        protected override void OnInitUser(Frame f) {
            f.Context.ExcludeEntityAndPlayerMask = ~f.Layers.GetLayerMask("Entity", "Player");
            f.Context.TaskContext.RegisterDelegate(SendEventsTask, $"{GetType().Name}.SendEvents", ref sendEventTaskHandle);
        }
        
        protected override TaskHandle Schedule(Frame f, TaskHandle taskHandle) {
            TaskHandle moveObjectsTask = base.Schedule(f, taskHandle);
            return f.Context.TaskContext.AddSingletonTask(sendEventTaskHandle, null, moveObjectsTask);
        }
#else
        public override void OnInit(Frame f) {
            f.Context.ExcludeEntityAndPlayerMask = ~f.Layers.GetLayerMask("Entity", "Player");
        }
#endif


#if MULTITHREADED
        public override void Update(FrameThreadSafe f, ref Filter filter) {
            var physicsObject = filter.PhysicsObject;
            if (physicsObject->IsFrozen) {
                return;
            }

            var transform = filter.Transform;
            var entity = filter.Entity;

            // bool canSnap = wasTouchingGround && physicsObject->Velocity.Y <= physicsObject->PreviousFrameVelocity.Y;
            physicsObject->PreviousFrameVelocity = physicsObject->Velocity;
            physicsObject->PreviousData = physicsObject->CurrentData;

            QList<PhysicsContact> contacts = f.ResolveList(physicsObject->Contacts);

            CeilingCrusherCheck((FrameThreadSafe) f, ref filter, stage, contacts);

            MoveWithPlatform((FrameThreadSafe) f, ref filter, contacts);
            for (int i = 0; i < contacts.Count; i++) {
                var contact = contacts[i];
                if (contact.Frame < f.Number) {
                    contacts.RemoveAtUnordered(i);
                    i--;
                }
            }

            if (physicsObject->WasBeingCrushed) {
                physicsObject->Velocity.Y = FPMath.Min(physicsObject->Velocity.Y, 0);
            }

            FPVector2 velocityModifier = FPVector2.One;
            if (physicsObject->SlowInLiquids && physicsObject->IsUnderwater) {
                velocityModifier.X = FP._0_50;
                if (FPMath.Abs(physicsObject->Velocity.Y) > Constants.OnePixelPerFrame) {
                    velocityModifier.Y = Constants.OnePixelPerFrame / FPMath.Abs(physicsObject->Velocity.Y);
                }
            }
            FPVector2 effectiveVelocity = new FPVector2(physicsObject->Velocity.X * velocityModifier.X, physicsObject->Velocity.Y * velocityModifier.Y);
            effectiveVelocity += physicsObject->ParentVelocity;

            FPVector2 previousPosition = transform->Position;
            effectiveVelocity = MoveVertically(f, effectiveVelocity, entity, stage, contacts);
            effectiveVelocity = MoveHorizontally(f, effectiveVelocity, entity, stage, contacts);
            ResolveContacts((FrameThreadSafe) f, stage, physicsObject, contacts);

            if (!physicsObject->DisableCollision && /* canSnap && */ physicsObject->WasTouchingGround && physicsObject->Velocity.Y <= physicsObject->PreviousFrameVelocity.Y && !physicsObject->IsTouchingGround) {
                // Try snapping
                FPVector2 previousVelocity = effectiveVelocity;
                FPVector2 testVelocity = effectiveVelocity;
                testVelocity.Y = -FP._0_25 * f.UpdateRate;
                effectiveVelocity = MoveVertically(f, testVelocity, entity, stage, contacts);
                ResolveContacts(f, stage, physicsObject, contacts);

                if (!physicsObject->IsTouchingGround) {
                    transform->Position.Y = previousPosition.Y;
                    effectiveVelocity = previousVelocity;
                    effectiveVelocity.Y = 0;
                    physicsObject->HoverFrames = 3;
                }
            }

            effectiveVelocity -= physicsObject->ParentVelocity;
            physicsObject->Velocity.X = effectiveVelocity.X / velocityModifier.X;
            physicsObject->Velocity.Y = effectiveVelocity.Y / velocityModifier.Y;

            CeilingCrusherCheck((FrameThreadSafe) f, ref filter, stage, contacts);

#if DEBUG
            foreach (var contact in contacts) {
                Draw.Ray(contact.Position, contact.Normal, ColorRGBA.Red);
            }
#endif

            if (QuantumUtils.Decrement(ref physicsObject->HoverFrames)) {
                // Apply gravity
                physicsObject->Velocity += physicsObject->Gravity * f.DeltaTime;
            }
            physicsObject->Velocity.Y = FPMath.Max(physicsObject->Velocity.Y, physicsObject->TerminalVelocity);
        }

#else

        public override void Update(Frame f) {
            VersusStageData stage = f.FindAsset<VersusStageData>(f.Map.UserAsset);

            Filter filter = default;
            var loop = f.Unsafe.FilterStruct<Filter>();
            while (loop.Next(&filter)) {
                var physicsObject = filter.PhysicsObject;
                if (physicsObject->IsFrozen) {
                    continue;
                }

                var transform = filter.Transform;
                var entity = filter.Entity;

                // bool canSnap = wasTouchingGround && physicsObject->Velocity.Y <= physicsObject->PreviousFrameVelocity.Y;
                physicsObject->PreviousFrameVelocity = physicsObject->Velocity;
                physicsObject->PreviousData = physicsObject->CurrentData;

                QList<PhysicsContact> contacts = f.ResolveList(physicsObject->Contacts);

                HandleCeilingCrushers(f, ref filter, contacts);

                MoveWithPlatform(f, ref filter, contacts);
                for (int i = 0; i < contacts.Count; i++) {
                    var contact = contacts[i];
                    if (contact.Frame < f.Number) {
                        contacts.RemoveAtUnordered(i);
                        i--;
                    }
                }

                if (physicsObject->WasBeingCrushed) {
                    physicsObject->Velocity.Y = FPMath.Min(physicsObject->Velocity.Y, 0);
                }

                FPVector2 velocityModifier = FPVector2.One;
                if (physicsObject->SlowInLiquids && physicsObject->IsUnderwater) {
                    velocityModifier.X = FP._0_50;
                    if (FPMath.Abs(physicsObject->Velocity.Y) > Constants.OnePixelPerFrame) {
                        velocityModifier.Y = Constants.OnePixelPerFrame / FPMath.Abs(physicsObject->Velocity.Y);
                    }
                }
                FPVector2 effectiveVelocity = new FPVector2(physicsObject->Velocity.X * velocityModifier.X, physicsObject->Velocity.Y * velocityModifier.Y);
                effectiveVelocity += physicsObject->ParentVelocity;

                FPVector2 previousPosition = transform->Position;
                effectiveVelocity = MoveVertically(f, effectiveVelocity, ref filter, stage, contacts, out _);
                effectiveVelocity = MoveHorizontally(f, effectiveVelocity, ref filter, stage, contacts, out _);
                ResolveContacts(f, stage, physicsObject, contacts);

                if (!physicsObject->DisableCollision /*&& !physicsObject->IsTouchingGround*/ && physicsObject->WasTouchingGround && (physicsObject->FloorAngle == 0 || FPMath.Sign(physicsObject->FloorAngle) == FPMath.Sign(physicsObject->Velocity.X)) && physicsObject->Velocity.Y <= physicsObject->PreviousFrameVelocity.Y) {
                    // Try snapping
                    FPVector2 previousVelocity = effectiveVelocity;
                    FPVector2 testVelocity = effectiveVelocity;
                    testVelocity.Y = -FP._0_25 * f.UpdateRate;
                    effectiveVelocity = MoveVertically(f, testVelocity, ref filter, stage, contacts, out bool snapped);
                    ResolveContacts(f, stage, physicsObject, contacts);

                    if (!snapped) {
                        transform->Position.Y = previousPosition.Y;
                        effectiveVelocity = previousVelocity;
                        effectiveVelocity.Y = 0;
                        physicsObject->HoverFrames = 3;
                    }
                }

                effectiveVelocity -= physicsObject->ParentVelocity;
                physicsObject->Velocity.X = effectiveVelocity.X / velocityModifier.X;
                physicsObject->Velocity.Y = effectiveVelocity.Y / velocityModifier.Y;

                HandleCeilingCrushers(f, ref filter, contacts);

#if DEBUG
                foreach (var contact in contacts) {
                    Draw.Ray(contact.Position, contact.Normal, ColorRGBA.Red);
                }
#endif

                if (QuantumUtils.Decrement(ref physicsObject->HoverFrames)) {
                    // Apply gravity
                    physicsObject->Velocity += physicsObject->Gravity * f.DeltaTime;
                }
                physicsObject->Velocity.Y = FPMath.Max(physicsObject->Velocity.Y, physicsObject->TerminalVelocity);
            }

            SendEventsTask((FrameThreadSafe) f);
        }
#endif

        public void SendEventsTask(FrameThreadSafe f) {
            var filter = f.Filter<PhysicsObject>();
            while (filter.NextUnsafe(out EntityRef entity, out PhysicsObject* physicsObject)) {
                if (!physicsObject->WasTouchingGround && physicsObject->IsTouchingGround) {
                    ((Frame) f).Events.PhysicsObjectLanded(entity);
                }
                if (!physicsObject->WasBeingCrushed && physicsObject->IsBeingCrushed) {
                    ((Frame) f).Signals.OnEntityCrushed(entity);
                }
            }
        }

        private void HandleCeilingCrushers(Frame f, ref Filter filter, QList<PhysicsContact> contacts) {
            var physicsObject = filter.PhysicsObject;
            if (physicsObject->DisableCollision || !physicsObject->IsBeingCrushed) {
                return;
            }
            var transform = filter.Transform;
            var collider = f.Unsafe.GetPointer<PhysicsCollider2D>(filter.Entity);
            Shape2D shape = collider->Shape;

            // Snap to ground.
            foreach (var contact in contacts) {
                if (FPVector2.Dot(contact.Normal, FPVector2.Up) < Constants.PhysicsGroundMaxAngleCos) {
                    continue;
                }

                transform->Position.Y = contact.Position.Y + shape.Box.Extents.Y - shape.Centroid.Y + Constants.PhysicsSkin;
                break;
            }
        }

        private void MoveWithPlatform(Frame f, ref Filter filter, QList<PhysicsContact> contacts) {
            var physicsObject = filter.PhysicsObject;
            EntityRef previousParent = physicsObject->Parent;
            physicsObject->Parent = EntityRef.None;

            FPVector2? maxVelocity = null;
            if (physicsObject->IsTouchingGround) {
                FP maxDot = -2;
                FPVector2 up = -physicsObject->Gravity.Normalized;
                foreach (var contact in contacts) {
                    if (!f.Unsafe.TryGetPointer(contact.Entity, out MovingPlatform* platform)
                        || FPVector2.Dot(contact.Normal, up) < Constants.PhysicsGroundMaxAngleCos) {
                        continue;
                    }

                    FPVector2 vel = platform->Velocity;
                    FP dot = FPVector2.Dot(vel.Normalized, up);
                    if (dot > maxDot || (dot == maxDot && maxVelocity.Value.SqrMagnitude > vel.SqrMagnitude)) {
                        maxDot = dot;
                        maxVelocity = vel;
                        physicsObject->Parent = contact.Entity;
                    }
                }
            }

            if (previousParent != physicsObject->Parent) {
                FPVector2 adjustment = physicsObject->ParentVelocity - (maxVelocity ?? FPVector2.Zero);
                adjustment.Y = 0; // Don't preserve vertical movement, it messes with jumps.
                
                physicsObject->Velocity += adjustment;
            }
            physicsObject->ParentVelocity = maxVelocity ?? FPVector2.Zero;
        }

        public static FPVector2 MoveVertically(Frame f, FPVector2 velocity, ref Filter filter, VersusStageData stage, QList<PhysicsContact>? contacts, out bool hitObject) {
            
            FP velocityY = velocity.Y * f.DeltaTime;
            if (velocityY == 0) {
                hitObject = false;
                return velocity;
            }

            var transform = filter.Transform;

            FPVector2 directionVector = velocityY > 0 ? FPVector2.Up : FPVector2.Down;

            var physicsObject = filter.PhysicsObject;

            if (!physicsObject->DisableCollision) {
                if (!contacts.HasValue) {
                    contacts = f.ResolveList(physicsObject->Contacts);
                }

                var collider = filter.Collider;
                Shape2D shape = collider->Shape;

                FPVector2 position = transform->Position;
                FPVector2 raycastOrigin = position - (directionVector * Constants.PhysicsRaycastSkin);
                FPVector2 raycastTranslation = new FPVector2(0, velocityY) + (directionVector * (Constants.PhysicsRaycastSkin * 2 + Constants.PhysicsSkin));

                var mask = ((Frame) f).Context.ExcludeEntityAndPlayerMask;
                var physicsHits = f.Physics2D.ShapeCastAll(raycastOrigin, 0, &shape, raycastTranslation, mask, QueryOptions.HitKinematics | QueryOptions.HitTriggers | QueryOptions.ComputeDetailedInfo);

                if (stage.IsWrappingLevel) {
                    FP center = transform->Position.X + shape.Centroid.X;
                    int closerEdge;
                    FP bounds;
                    if (center > (stage.StageWorldMin.X + stage.StageWorldMax.X) / 2) {
                        // Right edge
                        closerEdge = 1;
                        bounds = stage.StageWorldMax.X;
                    } else {
                        // Left edge
                        closerEdge = -1;
                        bounds = stage.StageWorldMin.X;
                    }

                    FP hitboxPosClosestEdge = center + shape.Box.Extents.X * closerEdge;
                    if (FPMath.Abs(hitboxPosClosestEdge - bounds) <= FPMath.Abs(raycastTranslation.X) + FP._0_50) {
                        // Close enough- check over the level seam.
                        FPVector2 wrappedRaycastOrigin = raycastOrigin;
                        wrappedRaycastOrigin.X += stage.TileDimensions.X * FP._0_50;

                        var wrappedHits = f.Physics2D.ShapeCastAll(wrappedRaycastOrigin, 0, &shape, raycastTranslation, mask, QueryOptions.HitKinematics | QueryOptions.HitTriggers | QueryOptions.ComputeDetailedInfo);
                        for (int i = 0; i < wrappedHits.Count; i++) {
                            physicsHits.Add(wrappedHits[i], f.Context);
                        }
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
                FP end = FPMath.Floor((checkPointY + velocityY + (directionVector.Y * Constants.PhysicsSkin)) * 2) / 2;
                FP direction = directionVector.Y;

                Span<FPVector2> vertexBuffer = stackalloc FPVector2[128];
                Span<int> shapeVertexCountBuffer = stackalloc int[16];
                Span<PhysicsContact> contactBuffer = stackalloc PhysicsContact[32];
                Span<PhysicsContact> removedContacts = stackalloc PhysicsContact[64];
                int removedContactCount = 0;

                for (FP y = start; (direction > 0 ? (y <= end) : (y >= end)); y += direction / 2) {

                    Span<PhysicsContact> potentialContacts = stackalloc PhysicsContact[32];
                    int potentialContactCount = 0;

                    for (FP x = left; x <= right; x += FP._0_50) {
                        FPVector2 worldPos = new FPVector2(x + FP._0_25, y + FP._0_25);
                        StageTileInstance tile = stage.GetTileWorld((Frame) f, worldPos);

                        if (!tile.GetWorldPolygons(f, stage, vertexBuffer, shapeVertexCountBuffer, out StageTile stageTile, worldPos)) {
                            continue;
                        }

                        IntVector2 tilePos = QuantumUtils.WorldToRelativeTile(stage, worldPos);

                        if (stageTile.CollisionData.IsFullTile) {
                            FPVector2 contactPos = new(FPMath.Clamp(position.X, x, x + FP._0_50), y + (direction < 0 ? FP._0_50 : 0));
                            potentialContacts[potentialContactCount++] = new PhysicsContact {
                                Position = contactPos,
                                Distance = FPMath.Abs(contactPos.Y - checkPointY),
                                Normal = new(0, -direction),
                                Frame = f.Number,
                                Tile = tilePos,
                            };
                        } else {
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
                                    newContact.Tile = tilePos;

                                    potentialContacts[potentialContactCount++] = newContact;
                                }
                            }
                        }
                    }

                    for (int i = 0; i < physicsHits.Count; i++) {
                        var hit = physicsHits[i];
                        if (hit.Point.Y < y || hit.Point.Y > y + FP._0_50) {
                            // Not a valid hit
                            continue;
                        }
                        if (hit.IsDynamic && f.Unsafe.TryGetPointer(hit.Entity, out Liquid* liquid)) {
                            if (liquid->LiquidType != LiquidType.Water || !physicsObject->IsWaterSolid || FPVector2.Dot(hit.Normal, FPVector2.Up) < Constants.PhysicsGroundMaxAngleCos) {
                                // Colliding with water and we cant interact
                                continue;
                            }
                        }
                        if (hit.IsDynamic && hit.TryGetShape(f, out Shape2D* hitShape)) {
                            FPVector2 upDirection = FPVector2.Rotate(FPVector2.Up, hitShape->LocalTransform.Rotation);
                            if (hitShape->Type == Shape2DType.Edge && FPVector2.Dot(hit.Normal, upDirection) <= Constants.PhysicsGroundMaxAngleCos) {
                                // Not a valid hit (semisolid)
                                continue;
                            }
                        }

                        FP distance = FPMath.Abs(hit.CastDistanceNormalized * raycastTranslation.Y) - Constants.PhysicsRaycastSkin;
                        if (distance > -(Constants.PhysicsRaycastSkin + Constants.PhysicsSkin)) {
                            potentialContacts[potentialContactCount++] = new PhysicsContact {
                                Distance = distance,
                                Normal = hit.Normal,
                                Position = hit.Point,
                                Frame = f.Number,
                                Tile = new(-1, -1),
                                Entity = hit.Entity,
                            };
                        }
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
                            || (min.HasValue && min.Value > 0 && contact.Distance - min.Value > tolerance)
                            || contact.Distance > FPMath.Abs(velocityY)
                            /* || removedContacts.Contains(contact) */
                            /* || FPVector2.Dot(contact.Normal, directionVector) > 0 */) {
                            continue;
                        }

                        bool keepContact = true;
                        foreach (var callback in ((Frame) f).Context.PreContactCallbacks) {
                            callback?.Invoke((Frame) f, stage, filter.Entity, contact, ref keepContact);
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
                    transform->Position += directionVector * (min.Value - Constants.PhysicsSkin);

                    // Readjust the remaining velocity
                    min -= physicsObject->ParentVelocity.Y;
                    FP remainingVelocity = velocity.Magnitude - min.Value;
                    FPVector2 newDirection = new(-avgNormal.Y, avgNormal.X);

                    // Only care about the Y aspect to not slide up/down hills via gravity
                    FPVector2 newVelocity = velocity;
                    newVelocity.Y = Project(newVelocity.Normalized * remainingVelocity, newDirection).Y;
                    hitObject = true;
                    return newVelocity;
                }
            }

            // Good to move
            transform->Position += directionVector * FPMath.Abs(velocityY);
            physicsObject->FloorAngle = 0;
            hitObject = false;
            return velocity;
        }

        public static FPVector2 MoveHorizontally(Frame f, FPVector2 velocity, ref Filter filter, VersusStageData stage, QList<PhysicsContact>? contacts, out bool hitObject) {
            
            FP velocityX = velocity.X * f.DeltaTime;
            if (velocityX == 0) {
                hitObject = false;
                return velocity;
            }
            var physicsObject = filter.PhysicsObject;
            if (!contacts.HasValue) {
                contacts = f.ResolveList(physicsObject->Contacts);
            }

            var transform = filter.Transform;

            FPVector2 directionVector = velocityX > 0 ? FPVector2.Right : FPVector2.Left;


            if (!physicsObject->DisableCollision) {
                var collider = filter.Collider;
                Shape2D shape = collider->Shape;
                
                FPVector2 position = transform->Position;
                FPVector2 raycastOrigin = position - (directionVector * Constants.PhysicsRaycastSkin);
                FPVector2 raycastTranslation = new FPVector2(velocityX, 0) + (directionVector * (Constants.PhysicsRaycastSkin * 2 + Constants.PhysicsSkin));

                var mask = f.Context.ExcludeEntityAndPlayerMask;

                var physicsHits = f.Physics2D.ShapeCastAll(raycastOrigin, 0, &shape, raycastTranslation, mask, QueryOptions.HitKinematics | QueryOptions.HitTriggers | QueryOptions.ComputeDetailedInfo);

                if (stage.IsWrappingLevel) {
                    FP center = transform->Position.X + shape.Centroid.X;
                    int closerEdge;
                    FP bounds;
                    if (center > (stage.StageWorldMin.X + stage.StageWorldMax.X) / 2) {
                        // Right edge
                        closerEdge = 1;
                        bounds = stage.StageWorldMax.X;
                    } else {
                        // Left edge
                        closerEdge = -1;
                        bounds = stage.StageWorldMin.X;
                    }

                    FP hitboxPosClosestEdge = center + shape.Box.Extents.X * closerEdge;
                    if (FPMath.Abs(hitboxPosClosestEdge - bounds) <= FPMath.Abs(raycastTranslation.X) + FP._0_50) {
                        // Close enough- check over the level seam.
                        FPVector2 wrappedRaycastOrigin = raycastOrigin;
                        wrappedRaycastOrigin.X += stage.TileDimensions.X * FP._0_50;

                        var wrappedHits = f.Physics2D.ShapeCastAll(wrappedRaycastOrigin, 0, &shape, raycastTranslation, mask, QueryOptions.HitKinematics | QueryOptions.HitTriggers | QueryOptions.ComputeDetailedInfo);
                        for (int i = 0; i < wrappedHits.Count; i++) {
                            physicsHits.Add(wrappedHits[i], f.Context);
                        }
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
                FP end = FPMath.Floor((checkPointX + velocityX + (directionVector.X * Constants.PhysicsSkin)) * 2) / 2;
                FP direction = directionVector.X;

                Span<FPVector2> vertexBuffer = stackalloc FPVector2[128];
                Span<int> shapeVertexCountBuffer = stackalloc int[16];
                Span<PhysicsContact> contactBuffer = stackalloc PhysicsContact[32];
                Span<PhysicsContact> removedContacts = stackalloc PhysicsContact[64];
                int removedContactCount = 0;

                for (FP x = start; (direction > 0 ? (x <= end) : (x >= end)); x += direction / 2) {
                    
                    Span<PhysicsContact> potentialContacts = stackalloc PhysicsContact[32];
                    int potentialContactCount = 0;

                    for (FP y = bottom; y <= top; y += FP._0_50) {
                        FPVector2 worldPos = new FPVector2(x + FP._0_25, y + FP._0_25);
                        StageTileInstance tile = stage.GetTileWorld((Frame) f, worldPos);

                        if (!tile.GetWorldPolygons(f, stage, vertexBuffer, shapeVertexCountBuffer, out StageTile stageTile, worldPos)) {
                            continue;
                        }

                        IntVector2 tilePos = QuantumUtils.WorldToRelativeTile(stage, worldPos);

                        if (stageTile.CollisionData.IsFullTile) {
                            FPVector2 contactPos = new(x + (direction < 0 ? FP._0_50 : 0), y + FP._0_25);
                            potentialContacts[potentialContactCount++] = new PhysicsContact {
                                Position = contactPos,
                                Distance = FPMath.Abs(contactPos.X - checkPointX),
                                Normal = new(-direction, 0),
                                Frame = f.Number,
                                Tile = tilePos,
                            };
                        } else {
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
                                    newContact.Tile = tilePos;
                                    potentialContacts[potentialContactCount++] = newContact;
                                }
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
                        if (hit.IsDynamic && f.Unsafe.TryGetPointer(hit.Entity, out Liquid* liquid)) {
                            if (liquid->LiquidType != LiquidType.Water || !physicsObject->IsWaterSolid || FPVector2.Dot(hit.Normal, FPVector2.Up) < Constants.PhysicsGroundMaxAngleCos) {
                                // Colliding with water and we cant interact
                                continue;
                            }
                        }
                        if (hit.IsDynamic && hit.TryGetShape(f, out Shape2D* hitShape)) {
                            FPVector2 upDirection = FPVector2.Rotate(FPVector2.Up, hitShape->LocalTransform.Rotation * FP.Deg2Rad);
                            if (hitShape->Type == Shape2DType.Edge && FPVector2.Dot(hit.Normal, upDirection) <= Constants.PhysicsGroundMaxAngleCos) {
                                // Not a valid hit (semisolid)
                                continue;
                            }
                        }

                        FP distance = FPMath.Abs(hit.CastDistanceNormalized * raycastTranslation.X) - Constants.PhysicsRaycastSkin;
                        if (distance > -(Constants.PhysicsRaycastSkin - Constants.PhysicsSkin)) {
                            potentialContacts[potentialContactCount++] = new PhysicsContact {
                                Distance = distance,
                                Normal = hit.Normal,
                                Position = hit.Point,
                                Frame = f.Number,
                                Tile = new(-1, -1),
                                Entity = hit.Entity,
                            };
                        }
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
                            || (min.HasValue && min.Value > 0 && contact.Distance - min.Value > tolerance)
                            || contact.Distance - Constants.PhysicsSkin > FPMath.Abs(velocityX) + Constants.PhysicsSkin
                            /* || removedContacts.Contains(contact) */
                            /* || FPVector2.Dot(contact.Normal, directionVector) > 0 */) {
                            continue;
                        }

                        bool keepContact = true;
                        foreach (var callback in ((Frame) f).Context.PreContactCallbacks) {
                            callback?.Invoke((Frame) f, stage, filter.Entity, contact, ref keepContact);
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
                    transform->Position += directionVector * (min.Value - Constants.PhysicsSkin);

                    // Readjust the remaining velocity
                    FPVector2 newVelocity = new(0, velocity.Y);
                    if (FPMath.Abs(FPVector2.Dot(avgNormal, FPVector2.Up)) > Constants.PhysicsGroundMaxAngleCos
                        /*&& FPVector2.Dot(avgNormal, velocity.Normalized) <= 0*/) {
                        // Slope/ground/ceiling
                        FPVector2 newDirection = new(avgNormal.Y, -avgNormal.X);
                        if ((avgNormal.X > 0) ^ (avgNormal.Y < 0)) {
                            newDirection *= -1;
                        }
                        FP speed = velocity.Y > 0 ? velocity.Magnitude : FPMath.Abs(velocity.X);
                        FPVector2 projected = newDirection * speed;
                        if (avgNormal.Y > 0) {
                            projected -= physicsObject->Gravity * f.DeltaTime;
                        }

                        newVelocity = projected;
                    }
                    hitObject = true;
                    
                    return newVelocity;
                }
            }
            
            // Good to move
            transform->Position += directionVector * FPMath.Abs(velocityX);
            hitObject = false;
            return velocity;
        }

        private void ResetContacts(ref PhysicsObjectData data) {
            data.FloorAngle = 0;
            // Reset all but IsBeingCrushed
            data.Flags &= PhysicsFlags.IsBeingCrushed;
        }

        private void ResolveContacts(Frame f, VersusStageData stage, PhysicsObject* physicsObject, QList<PhysicsContact> contacts) {

            ResetContacts(ref physicsObject->CurrentData);

            foreach (var contact in contacts) {
                FP horizontalDot = FPVector2.Dot(contact.Normal, FPVector2.Right);
                if (horizontalDot > (1 - Constants.PhysicsGroundMaxAngleCos)) {
                    physicsObject->IsTouchingLeftWall = true;

                } else if (horizontalDot < -(1 - Constants.PhysicsGroundMaxAngleCos)) {
                    physicsObject->IsTouchingRightWall = true;
                }

                FP verticalDot = FPVector2.Dot(contact.Normal, FPVector2.Up);
                if (verticalDot > Constants.PhysicsGroundMaxAngleCos) {
                    physicsObject->IsTouchingGround = true;

                    FP angle = FPVector2.RadiansSignedSkipNormalize(contact.Normal, FPVector2.Up) * FP.Rad2Deg;
                    if (FPMath.Abs(physicsObject->FloorAngle) < FPMath.Abs(angle)) {
                        physicsObject->FloorAngle = angle;
                    }

                    if (!f.Exists(contact.Entity)
                        && f.TryFindAsset(stage.GetTileRelative(f, contact.Tile).Tile, out StageTile tile)) {

                        physicsObject->IsOnSlideableGround |= tile.IsSlideableGround;
                        physicsObject->IsOnSlipperyGround |= tile.IsSlipperyGround;
                    }

                } else if (verticalDot < -Constants.PhysicsGroundMaxAngleCos) {
                    physicsObject->IsTouchingCeiling = true;
                }
            }
        }

        public static bool Raycast(Frame f, VersusStageData stage, FPVector2 worldPos, FPVector2 direction, FP maxDistance, out PhysicsContact contact) {
            if (stage == null) {
                stage = f.FindAsset<VersusStageData>(f.Map.UserAsset);
            }

            contact = default;
            direction = direction.Normalized;
            FPVector2 stepSize = new(
                direction.X == 0 ? 0 : FPMath.Sqrt(1 + (direction.Y / direction.X) * (direction.Y / direction.X)) / 2,
                direction.Y == 0 ? 0 : FPMath.Sqrt(1 + (direction.X / direction.Y) * (direction.X / direction.Y)) / 2
            );
            FPVector2 startLength = default;
            FPVector2 rayLength = default;
            Vector2Int step = default;

            if (direction.X < 0) {
                step.x = -1;
                startLength.X = rayLength.X = (worldPos.X - FPMath.Floor(worldPos.X * 2) / 2) * stepSize.X;
            } else if (direction.X > 0) {
                step.x = 1;
                startLength.X = rayLength.X = (FPMath.Floor(worldPos.X * 2 + 1) / 2 - worldPos.X) * stepSize.X;
            } else {
                step.x = 0;
                rayLength.X = maxDistance;
            }

            if (direction.Y < 0) {
                step.y = -1;
                startLength.X = rayLength.Y = (worldPos.Y - FPMath.Floor(worldPos.Y * 2) / 2) * stepSize.Y;
            } else if (direction.Y > 0) {
                step.y = 1;
                startLength.X = rayLength.Y = (FPMath.Floor(worldPos.Y * 2 + 1) / 2 - worldPos.Y) * stepSize.Y;
            } else {
                step.y = 0;
                rayLength.Y = maxDistance;
            }

            Span<FPVector2> vertexBuffer = stackalloc FPVector2[128];
            Span<int> shapeVertexCountBuffer = stackalloc int[16];
            Span<PhysicsContact> contactBuffer = stackalloc PhysicsContact[32];

            IntVector2 tilePosition = QuantumUtils.WorldToRelativeTile(stage, worldPos);
            FP distance = 0;

            // Check 0,0 as well.
            StageTileInstance tile = stage.GetTileRelative(f, tilePosition);
            if (tile.GetWorldPolygons(f, stage, vertexBuffer, shapeVertexCountBuffer, out StageTile stageTile, QuantumUtils.RelativeTileToWorldRounded(stage, tilePosition))) {
                if (!stageTile.CollisionData.IsFullTile) {
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
            }

            // Check later ones
            while (distance < maxDistance) {
                bool steppedX;
                if (rayLength.X < rayLength.Y) {
                    tilePosition.X += step.x;
                    distance = rayLength.X;
                    rayLength.X += stepSize.X;
                    steppedX = true;
                } else {
                    tilePosition.Y += step.y;
                    distance = rayLength.Y;
                    rayLength.Y += stepSize.Y;
                    steppedX = false;
                }

                tile = stage.GetTileRelative((Frame) f, tilePosition);
                if (!tile.GetWorldPolygons(f, stage, vertexBuffer, shapeVertexCountBuffer, out stageTile, QuantumUtils.RelativeTileToWorldRounded(stage, tilePosition))) {
                    continue;
                }

                if (stageTile.CollisionData.IsFullTile) {
                    FP trueDistance = distance;
                    if (steppedX) {
                        trueDistance -= startLength.X;
                    } else {
                        trueDistance -= startLength.Y;
                    }
                    contact = new PhysicsContact {
                        Position = worldPos + (direction * trueDistance),
                        Normal = (steppedX ? new(-step.x, 0) : new(0, -step.y)),
                        Distance = trueDistance,
                        Tile = tilePosition,
                        Frame = f.Number,
                    };
                    goto finish;
                } else {
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
            }

            finish:
            var nullableHit = f.Physics2D.Raycast(worldPos, direction, maxDistance, ((Frame) f).Context.ExcludeEntityAndPlayerMask, QueryOptions.HitAll & ~QueryOptions.HitTriggers | QueryOptions.ComputeDetailedInfo);
            if (nullableHit.HasValue) {
                var hit = nullableHit.Value;
                FP hitDistance = hit.CastDistanceNormalized * maxDistance;
                if (hitDistance < contact.Distance || contact.Distance == 0) {
                    contact = new PhysicsContact {
                        Distance = distance,
                        Normal = hit.Normal,
                        Position = hit.Point,
                        Tile = new(-1, -1),
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
            contact = default;
            contact.Distance = FP.MaxValue;

            int length = polygon.Length;
            if (length <= 1) {
                return false;
            }
            if (length == 2 || !isPolygon) {
                length--;
            }

            bool hit = false;

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

            FPVector2 v2 = y - x;
            FPVector2 v3 = new(-rayDirection.Y, rayDirection.X);

            FP dot = FPVector2.Dot(v2, v3);
            if (dot == 0) {
                return false;
            }

            FPVector2 v1 = rayOrigin - x;

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

        public static bool BoxInGround(Frame f, FPVector2 position, Shape2D shape, bool includeMegaBreakable = true, VersusStageData stage = null, EntityRef entity = default, bool includeCeilingCrushers = true) {
            using var profilerScope = HostProfiler.Start("PhysicsObjectSystem.BoxInGround");
            // In a solid hitbox
            var hits = f.Physics2D.OverlapShape(position, 0, shape, f.Context.ExcludeEntityAndPlayerMask, QueryOptions.HitKinematics | QueryOptions.ComputeDetailedInfo);
            f.Unsafe.TryGetPointer(entity, out MarioPlayer* mario);
            for (int i = 0; i < hits.Count; i++) {
                var hit = hits.HitsBuffer[i];
                Shape2D* hitShape = hit.GetShape(f);

                // Hit something.
                if (hitShape->Type == Shape2DType.Edge || hit.Entity == entity || (mario != null && hit.Entity == mario->HeldEntity)) {
                    continue;
                }

                if (f.Unsafe.TryGetPointer(hit.Entity, out IceBlock* iceBlock)) {
                    //if (!includeMegaBreakable || entity == iceBlock->Entity) {
                        continue;
                    //}
                }

                if (!includeCeilingCrushers && hitShape->UserTag == 1) {
                    continue;
                }

                return true;
            }

            if (stage == null) {
                stage = f.FindAsset<VersusStageData>(f.Map.UserAsset);
            }
            var extents = shape.Box.Extents;

            FPVector2 origin = position + shape.Centroid;
            FPVector2 boxMin = origin - extents;
            FPVector2 boxMax = origin + extents;

            Span<FPVector2> boxCorners = stackalloc FPVector2[4];
            boxCorners[0] = new(boxMin.X, boxMax.Y);
            boxCorners[1] = new(boxMax.X, boxMax.Y);
            boxCorners[2] = new(boxMax.X, boxMin.Y);
            boxCorners[3] = new(boxMin.X, boxMin.Y);

            Span<FPVector2> vertexBuffer = stackalloc FPVector2[128];
            Span<int> shapeVertexCountBuffer = stackalloc int[16];
            Span<PhysicsContact> contactBuffer = stackalloc PhysicsContact[32];
            
            Span<LocationTilePair> tiles = stackalloc LocationTilePair[64];
            int overlappingTiles = GetTilesOverlappingHitbox((Frame) f, position, shape, tiles, stage);

            for (int i = 0; i < overlappingTiles; i++) {
                StageTileInstance tile = tiles[i].Tile;
                StageTile stageTile = f.FindAsset(tile.Tile);
                IntVector2 location = tiles[i].Position;

                while (stageTile is TileInteractionRelocator tir) {
                    location = tir.RelocateTo;
                    tile = stage.GetTileRelative((Frame) f, location);
                    stageTile = f.FindAsset(tile.Tile);
                }

                if (stageTile == null
                    || !stageTile.IsPolygon
                    || (!includeMegaBreakable && stageTile is BreakableBrickTile breakable && breakable.BreakingRules.HasFlag(BreakableBrickTile.BreakableBy.MegaMario))) {
                    continue;
                }
                if (stageTile.CollisionData.IsFullTile) {
                    return true;
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
            public IntVector2 Position;
            public StageTileInstance Tile;
        }

        public static int GetTilesOverlappingHitbox(Frame f, FPVector2 position, Shape2D shape, Span<LocationTilePair> buffer, VersusStageData stage) {
            using var profilerScope = HostProfiler.Start("PhysicsObjectSystem.GetTilesOverlappingHitbox");
            var extents = shape.Box.Extents;

            FPVector2 origin = position + shape.Centroid;
            IntVector2 min = QuantumUtils.WorldToRelativeTile(stage, origin - extents, extend: false);
            IntVector2 max = QuantumUtils.WorldToRelativeTile(stage, origin + extents, extend: false);

            int count = 0;
            for (int x = min.X; x <= max.X; x++) {
                for (int y = min.Y; y <= max.Y; y++) {
                    IntVector2 pos = new IntVector2(x, y);
                    pos = QuantumUtils.WrapRelativeTile(stage, pos, out _);

                    buffer[count++] = new LocationTilePair {
                        Position = pos,
                        Tile = stage.GetTileRelative(f, pos)
                    };

                    if (count == buffer.Length) {
                        return count;
                    }
                }
            }
            return count;
        }

        public static bool TryEject(Frame f, EntityRef entity, VersusStageData stage = null) {
            var transform = f.Unsafe.GetPointer<Transform2D>(entity);
            var collider = f.Unsafe.GetPointer<PhysicsCollider2D>(entity);

            if (stage == null) {
                stage = f.FindAsset<VersusStageData>(f.Map.UserAsset);
            }

            if (!BoxInGround(f, transform->Position, collider->Shape, stage: stage, entity: entity)) {
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
                    if (BoxInGround(f, checkPos, collider->Shape, stage: stage, entity: entity)) {
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
        enum OutCode : byte {
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