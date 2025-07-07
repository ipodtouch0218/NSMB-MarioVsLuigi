using Photon.Deterministic;
using Quantum.Collections;
using Quantum.Physics2D;
using UnityEngine;

namespace Quantum {
    public unsafe class MovingPlatformSystem : SystemMainThreadEntityFilter<MovingPlatform, MovingPlatformSystem.Filter> {

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
            if (shape->Type == Shape2DType.Compound) {
                shape->Compound.GetShapes(f, out Shape2D* subshapes, out int subshapeCount);
                for (int i = 0; i < subshapeCount; i++) {
                    TryMoveShape(f, ref filter, stage, queries, subshapes + i, (i * 2) + index);
                }
                return;
            }

            // normal
            var hits = f.Physics2D.GetQueryHits(queries[index]);
            for (int i = 0; i < hits.Count; i++) {
                ProcessHit(f, ref filter, shape, hits[i], stage);
            }

            if (!f.DestroyPending(filter.Entity) || !f.Exists(filter.Entity)) {
                // Destroyed in movement callback
                return;
            }

            // level wrap seam
            var hits2 = f.Physics2D.GetQueryHits(queries[index + 1]);
            for (int i = 0; i < hits2.Count; i++) {
                ProcessHit(f, ref filter, shape, hits2[i], stage);
            }
        }

        private void ProcessHit(Frame f, ref Filter filter, Shape2D* shape, Hit hit, VersusStageData stage) {
            var platform = filter.Platform;
            var entity = filter.Entity;
            FPVector2 velocity = platform->Velocity * f.DeltaTime;

            if (hit.Entity == entity) {
                return;
            }
            if (!PhysicsObjectSystemFilterGetter.TryGet(f, hit.Entity, out var physicsSystemFilter)) {
                return;
            }
            var physicsObject = physicsSystemFilter.PhysicsObject;
            if (physicsObject->IsFrozen || physicsObject->DisableCollision) {
                return;
            }

            bool movingAway = FPVector2.Dot(physicsObject->Velocity.Normalized, velocity.Normalized) >= 0;
            if (shape->Type == Shape2DType.Edge) {
                // Semisolid logic
                FP lowerEdge = physicsSystemFilter.Transform->Position.Y + physicsSystemFilter.Collider->Shape.Centroid.Y - physicsSystemFilter.Collider->Shape.Box.Extents.Y;
                bool below = lowerEdge < (hit.Point.Y - (platform->Velocity.Y * 2 * f.DeltaTime));
                if (movingAway || below) {
                    return;
                } else {
                    movingAway = !movingAway;
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
                return;
            }

            FP moveDistance = hit.OverlapPenetration;
            FPVector2 moveVector = -hit.Normal * (moveDistance * f.UpdateRate);

            var contacts = f.ResolveList(physicsObject->Contacts);
            PhysicsObjectSystem.MoveVertically((FrameThreadSafe) f, moveVector, ref physicsSystemFilter, stage, contacts, out bool tempHit1);
            PhysicsObjectSystem.MoveHorizontally((FrameThreadSafe) f, moveVector, ref physicsSystemFilter, stage, contacts, out bool tempHit2);

            bool addContact = !movingAway || FPVector3.Project(physicsObject->Velocity.XYO, platform->Velocity.Normalized.XYO).Magnitude < platform->Velocity.Magnitude;
            if (addContact) {
                contacts.Add(newContact);
            }

            if (platform->CanCrushEntities && (tempHit1 || tempHit2) && shape->Type != Shape2DType.Edge) {
                // Crushed
                physicsObject->IsBeingCrushed = true;
            }
        }
    }
}
