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

        public delegate void EnemyInteractor(Frame f, EntityRef firstEntity, EntityRef secondEntity);
        private static readonly Dictionary<(Type, Type), EnemyInteractor> interactors = new();
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

            if ((f.TryGet(entity, out Enemy enemy) && !enemy.IsAlive)
                || (f.TryGet(entity, out MarioPlayer mario) && mario.IsDead && !f.Exists(mario.CurrentPipe))) {
                return;
            }

            var shape = filter.Collider->Shape;
            var transform = filter.Transform;

            // Collide
            var hits = f.Physics2D.OverlapShape(*transform, shape);
            EntityRef entityA = filter.Entity;
            for (int i = 0; i < hits.Count; i++) {
                EntityRef entityB = hits[i].Entity;
                var entities = (entityA, entityB);
                if (entityA == entityB
                    || alreadyCollided.Contains(entities)
                    || (f.TryGet(entityB, out Interactable entityBInteractable) && entityBInteractable.ColliderDisabled)
                    || (f.TryGet(entityB, out Enemy entityBEnemy) && !entityBEnemy.IsAlive)
                    || (f.TryGet(entityB, out MarioPlayer entityBMario) && entityBMario.IsDead && !f.Exists(entityBMario.CurrentPipe))) {
                    continue;
                }

                foreach ((var key, var interactor) in interactors) {
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
        }

        public static void RegisterInteraction<X, Y>(EnemyInteractor interactor) where X : unmanaged, IComponent where Y : unmanaged, IComponent {
            var key = (typeof(X), typeof(Y));

            if (interactors.ContainsKey(key)) {
                //Log.Warn($"[InteractionSystem] Already registered an interactor between {typeof(X).Name} and {typeof(Y).Name}.");
            } else {
                interactors[key] = interactor;
            }
        }
    }
}