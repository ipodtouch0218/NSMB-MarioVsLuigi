using Quantum.Collections;
using Quantum.Physics2D;

namespace Quantum {
    public unsafe class InteractionSystem : SystemMainThreadFilterStage<InteractionSystem.Filter> {
        public struct Filter {
            public EntityRef Entity;
            public Transform2D* Transform;
            public Interactable* Interactable;
            public PhysicsCollider2D* Collider;
        }

        public override void BeforeUpdate(Frame f, VersusStageData stage) {
            f.Context.alreadyCollided.Clear();
        }

        public override void Update(Frame f, ref Filter filter, VersusStageData stage) {
            var interactable = filter.Interactable;
            var entity = filter.Entity;

            if (interactable->ColliderDisabled
                || (f.Unsafe.TryGetPointer(entity, out Enemy* enemy) && enemy->IsDead)
                || (f.Unsafe.TryGetPointer(entity, out Freezable* freezable) && f.Exists(freezable->FrozenCubeEntity))) {
                return;
            }

            var shape = filter.Collider->Shape;
            var transform = filter.Transform;

            // Collide with hitboxes
            if (f.Physics2D.TryGetQueryHits(interactable->OverlapQueryRef, out HitCollection hits)) {
                for (int i = 0; i < hits.Count; i++) {
                    TryCollideWithEntity(f, entity, hits[i].Entity);
                }
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
            if (entityA == entityB || f.Context.alreadyCollided.Contains(entities)) {
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

            foreach ((var key, var interactor) in f.Context.hitboxInteractors) {
                int componentIdA = ComponentTypeId.GetComponentIndex(key.Item1);
                int componentIdB = ComponentTypeId.GetComponentIndex(key.Item2);

                if (f.Has(entityA, componentIdA)
                    && f.Has(entityB, componentIdB)) {

                    interactor(f, entityA, entityB);
                    f.Context.alreadyCollided.Add(entities);
                    break;

                } else if (f.Has(entityB, componentIdA)
                    && f.Has(entityA, componentIdB)) {

                    interactor(f, entityB, entityA);
                    f.Context.alreadyCollided.Add(entities);
                    break;
                }
            }
        }

        private void TryCollideWithEntity(Frame f, EntityRef entityA, EntityRef entityB, in PhysicsContact contact) {
            var entities = (entityA, entityB);
            if (entityA == entityB
                || f.Context.alreadyCollided.Contains(entities)) {
                return;
            }

            if (f.Unsafe.TryGetPointer(entityB, out Interactable* entityBInteractable)
                && entityBInteractable->ColliderDisabled) {
                return;
            }

            bool allowInteraction = true;
            f.Signals.OnBeforeInteraction(entityA, &allowInteraction);
            if (!allowInteraction) {
                return;
            }
            f.Signals.OnBeforeInteraction(entityB, &allowInteraction);
            if (!allowInteraction) {
                return;
            }

            foreach ((var key, var interactor) in f.Context.platformInteractors) {
                int componentIdA = ComponentTypeId.GetComponentIndex(key.Item1);
                int componentIdB = ComponentTypeId.GetComponentIndex(key.Item2);

                if (f.Has(entityA, componentIdA)
                    && f.Has(entityB, componentIdB)) {

                    interactor(f, entityA, entityB, contact);
                    f.Context.alreadyCollided.Add(entities);
                    break;

                } else if (f.Has(entityB, componentIdA)
                    && f.Has(entityA, componentIdB)) {

                    interactor(f, entityB, entityA, contact);
                    f.Context.alreadyCollided.Add(entities);
                    break;
                }
            }
        }
    }
}