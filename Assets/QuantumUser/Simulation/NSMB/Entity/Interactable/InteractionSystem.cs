using Photon.Deterministic;
using Quantum.Collections;
using System;
using System.Collections.Generic;

namespace Quantum {
    public unsafe class InteractionSystem : SystemMainThreadFilterStage<InteractionSystem.Filter> {
        public struct Filter {
            public EntityRef Entity;
            public Transform2D* Transform;
            public Interactable* Interactable;
            public PhysicsCollider2D* Collider;
        }

        public delegate void HitboxInteractor(Frame f, EntityRef firstEntity, EntityRef secondEntity);
        public delegate void PlatformInteractor(Frame f, EntityRef entity, EntityRef platformEntity, PhysicsContact contact);

        private static readonly Dictionary<(Type, Type), HitboxInteractor> hitboxInteractors = new();
        private static readonly Dictionary<(Type, Type), PlatformInteractor> platformInteractors = new();
        private static readonly HashSet<(EntityRef, EntityRef)> alreadyCollided = new(new UnorderedTupleEqualityComparer<EntityRef>());

        public override void BeforeUpdate(Frame f, VersusStageData stage) {
            alreadyCollided.Clear();
        }

        public override void Update(Frame f, ref Filter filter, VersusStageData stage) {
            var interactable = filter.Interactable;
            var entity = filter.Entity;

            if (interactable->ColliderDisabled) {
                return;
            }

            bool allowInteraction = true;
            f.Signals.OnBeforeInteraction(entity, &allowInteraction);
            if (!allowInteraction) {
                return;
            }

            var shape = filter.Collider->Shape;
            var transform = filter.Transform;

            // Collide with hitboxes
            var hits = f.Physics2D.OverlapShape(*transform, shape);

            FP center = transform->Position.X + shape.Centroid.X;
            if (center - shape.Box.Extents.X < stage.StageWorldMin.X) {
                // Left edge
                Transform2D transformCopy = *transform;
                transformCopy.Position.X += stage.TileDimensions.x / (FP) 2;
                f.Physics2D.OverlapShape(&hits, transformCopy, shape);

            } else if (center + shape.Box.Extents.X > stage.StageWorldMax.X) {
                // Right edge
                Transform2D transformCopy = *transform;
                transformCopy.Position.X -= stage.TileDimensions.x / (FP) 2;
                f.Physics2D.OverlapShape(&hits, transformCopy, shape);
            }

            for (int i = 0; i < hits.Count; i++) {
                TryCollideWithEntity(f, entity, hits[i].Entity);
            }

            // Collide with physical objects
            if (f.Unsafe.TryGetPointer(entity, out PhysicsObject* physicsObject)
                && f.TryResolveList(physicsObject->Contacts, out QList<PhysicsContact> contacts)) {

                foreach (var contact in contacts) {
                    if (!f.Exists(contact.Entity)) {
                        continue;
                    }

                    TryCollideWithEntity(f, entity, contact.Entity, contact);
                }
            }
        }

        private void TryCollideWithEntity(Frame f, EntityRef entityA, EntityRef entityB) {
            var entities = (entityA, entityB);
            if (entityA == entityB
                || alreadyCollided.Contains(entities)) {
                return;
            }

            if (f.Unsafe.TryGetPointer(entityB, out Interactable* entityBInteractable)
                && entityBInteractable->ColliderDisabled) {
                return;
            }

            bool allowInteraction = true;
            f.Signals.OnBeforeInteraction(entityB, &allowInteraction);
            if (!allowInteraction) {
                return;
            }

            foreach ((var key, var interactor) in hitboxInteractors) {
                int componentIdA = ComponentTypeId.GetComponentIndex(key.Item1);
                int componentIdB = ComponentTypeId.GetComponentIndex(key.Item2);

                if (f.Has(entityA, componentIdA)
                    && f.Has(entityB, componentIdB)) {

                    interactor(f, entityA, entityB);
                    alreadyCollided.Add(entities);
                    break;

                } else if (f.Has(entityB, componentIdA)
                    && f.Has(entityA, componentIdB)) {

                    interactor(f, entityB, entityA);
                    alreadyCollided.Add(entities);
                    break;
                }
            }
        }

        private void TryCollideWithEntity(Frame f, EntityRef entityA, EntityRef entityB, PhysicsContact contact) {
            var entities = (entityA, entityB);
            if (entityA == entityB
                || alreadyCollided.Contains(entities)) {
                return;
            }

            if (f.Unsafe.TryGetPointer(entityB, out Interactable* entityBInteractable)
                && entityBInteractable->ColliderDisabled) {
                return;
            }

            bool allowInteraction = true;
            f.Signals.OnBeforeInteraction(entityB, &allowInteraction);
            if (!allowInteraction) {
                return;
            }

            foreach ((var key, var interactor) in platformInteractors) {
                int componentIdA = ComponentTypeId.GetComponentIndex(key.Item1);
                int componentIdB = ComponentTypeId.GetComponentIndex(key.Item2);

                if (f.Has(entityA, componentIdA)
                    && f.Has(entityB, componentIdB)) {

                    interactor(f, entityA, entityB, contact);
                    alreadyCollided.Add(entities);
                    break;

                } else if (f.Has(entityB, componentIdA)
                    && f.Has(entityA, componentIdB)) {

                    interactor(f, entityB, entityA, contact);
                    alreadyCollided.Add(entities);
                    break;
                }
            }
        }

        public static void RegisterInteraction<X, Y>(HitboxInteractor interactor) where X : unmanaged, IComponent where Y : unmanaged, IComponent {
            var key = (typeof(X), typeof(Y));

            if (hitboxInteractors.ContainsKey(key)) {
                //Log.Warn($"[InteractionSystem] Already registered an interactor between {typeof(X).Name} and {typeof(Y).Name}.");
            } else {
                hitboxInteractors[key] = interactor;
            }
        }

        public static void RegisterInteraction<X, Y>(PlatformInteractor interactor) where X : unmanaged, IComponent where Y : unmanaged, IComponent {
            var key = (typeof(X), typeof(Y));

            if (platformInteractors.ContainsKey(key)) {
                //Log.Warn($"[InteractionSystem] Already registered an interactor between {typeof(X).Name} and {typeof(Y).Name}.");
            } else {
                platformInteractors[key] = interactor;
            }
        }
    }
}