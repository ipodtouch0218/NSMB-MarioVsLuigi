using Quantum.Collections;
using Quantum.Physics2D;
using Quantum.Profiling;
using Quantum.Task;
using System.Collections.Generic;

namespace Quantum {
    public unsafe class InteractionSystem : SystemArrayFilter<InteractionSystem.Filter> {

        private SortedSet<PendingInteraction> pendingInteractions = new(new PendingInteractionComparer());
        private HashSet<EntityRefPair> alreadyInteracted = new(16);
        private TaskDelegateHandle executeInteractorsTaskDelegate;

        public struct Filter {
            public EntityRef Entity;
            public Transform2D* Transform;
            public Interactable* Interactable;
            public PhysicsCollider2D* Collider;
        }

        protected override void OnInitUser(Frame f) {
            f.Context.TaskContext.RegisterDelegate(ExecuteInteractors, $"{GetType().Name}.ExecuteInteractors", ref executeInteractorsTaskDelegate);
        }

        protected override TaskHandle Schedule(Frame f, TaskHandle taskHandle) {
            TaskHandle findInteractionsTask = base.Schedule(f, taskHandle);
            return f.Context.TaskContext.AddMainThreadTask(executeInteractorsTaskDelegate, null, findInteractionsTask);
        }

        public void ExecuteInteractors(FrameThreadSafe fts, int start, int count, void* arg) {
            Frame f = (Frame) fts;

            foreach (PendingInteraction interaction in pendingInteractions) {
                EntityRef entityA = interaction.EntityA;
                EntityRef entityB = interaction.EntityB;

                {
                    using var profilerScope2 = HostProfiler.Start("InteractionSystem.Contains");

                    EntityRefPair pair = new EntityRefPair { 
                        EntityA = entityA,
                        EntityB = entityB,
                    };
                    
                    if (!alreadyInteracted.Add(pair)) {
                        continue;
                    }
                }

                {
                    using var profilerScope5 = HostProfiler.Start("InteractionSystem.BeforeInteractionSignals");

                    bool continueInteraction = true;
                    f.Signals.OnBeforeInteraction(entityA, &continueInteraction);
                    f.Signals.OnBeforeInteraction(entityB, &continueInteraction);

                    if (!continueInteraction) {
                        continue;
                    }
                }

                {
                    using var profilerScope3 = HostProfiler.Start("InteractionSystem.ExecuteInteractors");
                    if (interaction.IsPlatformInteraction) {
                        f.Context.platformInteractors[interaction.InteractorIndex].Invoke(f, entityA, entityB, interaction.Contact);
                    } else {
                        f.Context.hitboxInteractors[interaction.InteractorIndex].Invoke(f, entityA, entityB);
                    }
                }
            }

            using var profilerScope4 = HostProfiler.Start("InteractionSystem.Clear");
            pendingInteractions.Clear();
            alreadyInteracted.Clear();
        }

        public override void Update(FrameThreadSafe f, ref Filter filter) {
            var interactable = filter.Interactable;
            var entity = filter.Entity;

            if (interactable->ColliderDisabled
                || (f.TryGetPointer(entity, out Enemy* enemy) && enemy->IsDead)
                || (f.TryGetPointer(entity, out Freezable* freezable) && f.Exists(freezable->FrozenCubeEntity))) {
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
            if (f.TryGetPointer(entity, out PhysicsObject* physicsObject)
                && f.TryResolveList(physicsObject->Contacts, out QList<PhysicsContact> contacts)) {

                foreach (var contact in contacts) {
                    if (!f.Exists(contact.Entity)) {
                        continue;
                    }

                    TryCollideWithEntity(f, entity, contact.Entity, contact);
                }
            }
        }

        private void TryCollideWithEntity(FrameThreadSafe f, EntityRef entityA, EntityRef entityB) {
            if (entityA == entityB) {
                return;
            }

            if (f.TryGetPointer(entityB, out Interactable* entityBInteractable)
                && entityBInteractable->ColliderDisabled) {
                return;
            }

            var interactors = ((Frame) f).Context.hitboxInteractorMap;
            for (int i = 0; i < interactors.Count; i++) {
                var key = interactors[i];
                int componentIdA = ComponentTypeId.GetComponentIndex(key.Item1);
                int componentIdB = ComponentTypeId.GetComponentIndex(key.Item2);

                if (f.Has(entityA, componentIdA)
                    && f.Has(entityB, componentIdB)) {

                    lock (pendingInteractions) {
                        pendingInteractions.Add(new PendingInteraction {
                            EntityA = entityA,
                            EntityB = entityB,
                            InteractorIndex = i,
                            IsPlatformInteraction = false,
                        });
                    }
                    break;
                }
            }
        }

        private void TryCollideWithEntity(FrameThreadSafe f, EntityRef entityA, EntityRef entityB, in PhysicsContact contact) {
            if (entityA == entityB) {
                return;
            }

            if (f.TryGetPointer(entityB, out Interactable* entityBInteractable)
                && entityBInteractable->ColliderDisabled) {
                return;
            }

            var interactors = ((Frame) f).Context.platformInteractorMap;
            for (int i = 0; i < interactors.Count; i++) {
                var key = interactors[i];
                int componentIdA = ComponentTypeId.GetComponentIndex(key.Item1);
                int componentIdB = ComponentTypeId.GetComponentIndex(key.Item2);

                if (f.Has(entityA, componentIdA) && f.Has(entityB, componentIdB)) {
                    pendingInteractions.Add(new PendingInteraction {
                        EntityA = entityA,
                        EntityB = entityB,
                        Contact = contact,
                        InteractorIndex = i,
                        IsPlatformInteraction = true,
                    });
                    break;
                }
            }
        }


        public struct PendingInteraction {
            public EntityRef EntityA, EntityB;
            public PhysicsContact Contact;
            public int InteractorIndex;
            public bool IsPlatformInteraction;
        }

        public class PendingInteractionComparer : IComparer<PendingInteraction> {
            public int Compare(PendingInteraction x, PendingInteraction y) {
                int diff = x.EntityA.Index - y.EntityB.Index;
                if (diff != 0) {
                    return diff;
                }

                return x.EntityB.Index - y.EntityB.Index;
            }
        }

        public struct EntityRefPair : IEqualityComparer<EntityRefPair> {

            public EntityRef EntityA, EntityB;

            public bool Equals(EntityRefPair x, EntityRefPair y) {
                return (x.EntityA == y.EntityA && x.EntityB == y.EntityB) || (x.EntityA == y.EntityB && x.EntityB == y.EntityA);
            }

            public int GetHashCode(EntityRefPair obj) {
                return (obj.EntityA.GetHashCode() * 37) + (obj.EntityB.GetHashCode() * 37);
            }

        }

        public class EntityRefPairComparer : IComparer<EntityRefPair> {
            public int Compare(EntityRefPair x, EntityRefPair y) {
                return x.EntityA.Index - y.EntityB.Index;
            }
        }
    }
}