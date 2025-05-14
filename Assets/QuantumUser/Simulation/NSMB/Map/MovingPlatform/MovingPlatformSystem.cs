using Photon.Deterministic;
using Quantum.Collections;
using UnityEngine;

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
            var queries = f.ResolveList(filter.Platform->Queries);
            TryMoveShape(f, ref filter, stage, queries, &filter.Collider->Shape, 0);

            var platform = filter.Platform;
            if (!platform->IgnoreMovement) {
                filter.Transform->Position += platform->Velocity * f.DeltaTime;
            }
        }

        private void TryMoveShape(Frame f, ref Filter filter, VersusStageData stage, QList<PhysicsQueryRef> queries, Shape2D* shape, int index) {
            FPVector2 velocity = filter.Platform->Velocity * f.DeltaTime;
            /*
            if (velocity == FPVector2.Zero) {
                return;
            }
            */

            if (shape->Type == Shape2DType.Compound) {
                shape->Compound.GetShapes(f, out Shape2D* subshapes, out int subshapeCount);
                for (int i = 0; i < subshapeCount; i++) {
                    TryMoveShape(f, ref filter, stage, queries, subshapes + i, i + index);
                }
                return;
            }
            
            var entity = filter.Entity;
            var hits = f.Physics2D.GetQueryHits(queries[index]);

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

                bool movingAway = FPVector2.Dot(physicsObject->Velocity.Normalized, velocity.Normalized) > 0;
                if (shape->Type == Shape2DType.Edge) {
                    // Semisolid logic
                    movingAway = !movingAway;
                    if (!movingAway) {
                        continue;
                    }
                }

                PhysicsContact newContact = new PhysicsContact {
                    Position = hit.Point,
                    Normal = -hit.Normal,
                    Distance = velocity.Magnitude * hit.CastDistanceNormalized,
                    Entity = entity,
                    Frame = f.Number,
                    Tile = new(-1, -1)
                };
                bool keepContact = true;
                foreach (var callback in f.Context.PreContactCallbacks) {
                    callback?.Invoke(f, stage, hit.Entity, newContact, ref keepContact);
                }

                if (!keepContact) {
                    continue;
                }

                FP moveDistance = hit.OverlapPenetration;
                FPVector2 moveVector = -hit.Normal * (moveDistance * f.UpdateRate);

                var contacts = f.ResolveList(physicsObject->Contacts);
                PhysicsObjectSystem.MoveVertically((FrameThreadSafe) f, moveVector, ref physicsSystemFilter, stage, contacts, out bool tempHit1);
                PhysicsObjectSystem.MoveHorizontally((FrameThreadSafe) f, moveVector, ref physicsSystemFilter, stage, contacts, out bool tempHit2);

                if (!movingAway) {
                    contacts.Add(newContact);
                }

                if (filter.Platform->CanCrushEntities && (tempHit1 || tempHit2) && shape->Type != Shape2DType.Edge) {
                    // Crushed
                    physicsObject->IsBeingCrushed = true;
                }
            }
        }
    }
}
