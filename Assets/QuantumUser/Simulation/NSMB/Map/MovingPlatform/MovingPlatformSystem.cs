using Photon.Deterministic;

namespace Quantum {
    public unsafe class MovingPlatformSystem : SystemMainThreadFilterStage<MovingPlatformSystem.Filter> {

        public struct Filter {
            public EntityRef Entity;
            public Transform2D* Transform;
            public MovingPlatform* Platform;
            public PhysicsCollider2D* Collider;
        }
        private ComponentGetter<PhysicsObjectSystem.Filter> PhysicsObjectSystemFilterGetter;

        public override void OnInit(Frame f) {
            f.Context.ExcludeEntityAndPlayerMask = ~f.Layers.GetLayerMask("Entity", "Player");
            PhysicsObjectSystemFilterGetter = f.Unsafe.ComponentGetter<PhysicsObjectSystem.Filter>();
        }

        public override void Update(Frame f, ref Filter filter, VersusStageData stage) {
            TryMove(f, ref filter, stage);
        }

        private void TryMove(Frame f, ref Filter filter, VersusStageData stage) {
            TryMoveShape(f, ref filter, stage, &filter.Collider->Shape);

            var platform = filter.Platform;
            if (!platform->IgnoreMovement) {
                filter.Transform->Position += platform->Velocity * f.DeltaTime;
            }
        }

        private void TryMoveShape(Frame f, ref Filter filter, VersusStageData stage, Shape2D* shape) {
            FPVector2 velocity = filter.Platform->Velocity * f.DeltaTime;
            if (velocity == FPVector2.Zero) {
                return;
            }

            if (shape->Type == Shape2DType.Compound) {
                shape->Compound.GetShapes(f, out Shape2D* subshapes, out int subshapeCount);
                for (int i = 0; i < subshapeCount; i++) {
                    TryMoveShape(f, ref filter, stage, subshapes + i);
                }
                return;
            }
            
            var entity = filter.Entity;
            FPVector2 shapecastOrigin = filter.Transform->Position /*+ (-velocity * PhysicsObjectSystem.RaycastSkin)*/;
            FPVector2 moveVelocity = velocity * (1 + PhysicsObjectSystem.RaycastSkin);

            var hits = f.Physics2D.ShapeCastAll(shapecastOrigin,
                0,
                shape,
                moveVelocity,
                ~f.Context.ExcludeEntityAndPlayerMask,
                QueryOptions.HitKinematics | QueryOptions.ComputeDetailedInfo | QueryOptions.DetectOverlapsAtCastOrigin);

            for (int i = 0; i < hits.Count; i++) {
                var hit = hits[i];
                
                if (hit.Entity == entity) {
                    continue;
                }
                if (!PhysicsObjectSystemFilterGetter.TryGet(f, hit.Entity, out var physicsSystemFilter)) {
                    continue;
                }
                var physicsObject = physicsSystemFilter.PhysicsObject;
                if (physicsObject->DisableCollision) {
                    continue;
                }

                if (shape->Type == Shape2DType.Edge) {
                    // Semisolid logic
                    if (FPVector2.Dot(physicsObject->Velocity, velocity) < 0) {
                        continue;
                    }
                }

                FP moveDistance = moveVelocity.Magnitude * (1 - hit.CastDistanceNormalized);
                var overlapHits = f.Physics2D.CheckOverlap(shape, filter.Transform, &hit);
                if (overlapHits.Count > 0) {
                    // overlap!
                    var overlapHit = overlapHits[0];
                    moveDistance += overlapHit.OverlapPenetration;
                }
                FPVector2 moveVector = -hit.Normal * (moveDistance * f.UpdateRate);

                var contacts = f.ResolveList(physicsObject->Contacts);
                PhysicsObjectSystem.MoveVertically((FrameThreadSafe) f, moveVector, ref physicsSystemFilter, stage, contacts, out bool tempHit1);
                PhysicsObjectSystem.MoveHorizontally((FrameThreadSafe) f, moveVector, ref physicsSystemFilter, stage, contacts, out bool tempHit2);
                bool hitObject = tempHit1 || tempHit2;

                if (hitObject && shape->Type != Shape2DType.Edge) {
                    // Crushed
                    physicsObject->IsBeingCrushed = true;
                }
            }
        }
    }
}
