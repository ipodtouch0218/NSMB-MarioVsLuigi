using Quantum.Collections;
using Quantum.Physics2D;
using Quantum.Profiling;
using Quantum.Task;
using System;
using System.Collections.Generic;

namespace Quantum {

#if MULTITHREADED
    public unsafe class InteractionSystem : SystemArrayFilter<InteractionSystem.Filter> {
#else
    public unsafe class InteractionSystem : SystemMainThread, ISignalOnMarioPlayerGroundpoundedSolid {
#endif
        private List<PendingInteraction> pendingInteractions = new(16);
        private HashSet<EntityRefPair> alreadyInteracted = new(16);
        private TaskDelegateHandle executeInteractorsTaskDelegate;

        public struct Filter {
            public EntityRef Entity;
            public Transform2D* Transform;
            public Interactable* Interactable;
            public InteractionInitiator* Initiator;
            public PhysicsCollider2D* Collider;
        }

#if MULTITHREADED
        protected override void OnInitUser(Frame f) {
            f.Context.TaskContext.RegisterDelegate(ExecuteInteractors, $"{GetType().Name}.ExecuteInteractors", ref executeInteractorsTaskDelegate);
        }

        protected override TaskHandle Schedule(Frame f, TaskHandle taskHandle) {
            TaskHandle findInteractionsTask = base.Schedule(f, taskHandle);
            return f.Context.TaskContext.AddMainThreadTask(executeInteractorsTaskDelegate, null, findInteractionsTask);
        }
#endif

        public void ExecuteInteractors(FrameThreadSafe fts, int start, int count, void* arg) {
            Frame f = (Frame) fts;

            // pendingInteractions.Sort(new PendingInteractionComparer());
            // Log.Debug(string.Join(',', pendingInteractions.Select(x => x.EntityA + " - " + x.EntityB + " (" + x.InteractorIndex + ") " + (x.IsPlatformInteraction ? "platform" : "object"))));

            foreach (PendingInteraction interaction in pendingInteractions) {
                EntityRef entityA = interaction.EntityA;
                EntityRef entityB = interaction.EntityB;

                {
                    using var profilerScope2 = HostProfiler.Start("InteractionSystem.Contains");

                    EntityRefPair pair = new EntityRefPair { 
                        EntityA = entityA,
                        EntityB = entityB,
                    };
                    
                    if (alreadyInteracted.Contains(pair)) {
                        continue;
                    }
                    alreadyInteracted.Add(pair);
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
                        f.Context.Interactions.platformInteractors[interaction.InteractorIndex].Invoke(f, entityA, entityB, interaction.Contact);
                    } else {
                        f.Context.Interactions.hitboxInteractors[interaction.InteractorIndex].Invoke(f, entityA, entityB);
                    }
                }
            }

            using var profilerScope4 = HostProfiler.Start("InteractionSystem.Clear");
            pendingInteractions.Clear();
            alreadyInteracted.Clear();
        }

#if MULTITHREADED
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
#else
        public override void Update(Frame f) {
            FrameThreadSafe fts = (FrameThreadSafe) f;
            var entityFilter = f.Unsafe.FilterStruct<Filter>();
            Filter filter = default;
            while (entityFilter.Next(&filter)) {
                var interactable = filter.Interactable;
                var initiator = filter.Initiator;
                var entity = filter.Entity;

                if (interactable->ColliderDisabled
                    || (f.Unsafe.TryGetPointer(entity, out Enemy* enemy) && enemy->IsDead)
                    || (f.Unsafe.TryGetPointer(entity, out Freezable* freezable) && f.Exists(freezable->FrozenCubeEntity))) {
                    continue;
                }

                var shape = filter.Collider->Shape;
                var transform = filter.Transform;

                // Collide with hitboxes
                if (f.Physics2D.TryGetQueryHits(initiator->OverlapQueryRef, out HitCollection hits)) {
                    for (int i = 0; i < hits.Count; i++) {
                        TryCollideWithTriggerEntity(fts, entity, hits[i].Entity);
                    }
                }
                if (f.Physics2D.TryGetQueryHits(initiator->OverlapLevelSeamQueryRef, out hits)) {
                    for (int i = 0; i < hits.Count; i++) {
                        TryCollideWithTriggerEntity(fts, entity, hits[i].Entity);
                    }
                }

                // Collide with physical objects
                if (f.Unsafe.TryGetPointer(entity, out PhysicsObject* physicsObject)
                    && f.TryResolveList(physicsObject->Contacts, out QList<PhysicsContact> contacts)) {

                    foreach (var contact in contacts) {
                        if (!f.Exists(contact.Entity)) {
                            continue;
                        }

                        TryCollideWithSolidEntity(fts, entity, contact.Entity, contact);
                    }
                }
            }

            ExecuteInteractors(fts, 0, 0, (void*) null);
        }
#endif

        private void TryCollideWithTriggerEntity(FrameThreadSafe f, EntityRef entityA, EntityRef entityB) {
            using var profileScope = HostProfiler.Start("InteractionSystem.TryCollideWithTriggerEntity");
            if (entityA == entityB) {
                return;
            }

            if (!f.Exists(entityA) || !f.Exists(entityB)
                || (f.TryGetPointer(entityB, out Interactable* entityBInteractable) && entityBInteractable->ColliderDisabled)) {
                return;
            }

            PendingInteraction interaction = ((Frame) f).Context.Interactions.FindHitboxInteractor(entityA, f.GetComponentSet(entityA), entityB, f.GetComponentSet(entityB));
            if (interaction.InteractorIndex != -1) {
#if MULTITHREADED
                lock (pendingInteractions) {
                    pendingInteractions.Add(interaction);
                }
#else
                pendingInteractions.Add(interaction);
#endif
            }
        }

        private void TryCollideWithSolidEntity(FrameThreadSafe f, EntityRef entityA, EntityRef entityB, in PhysicsContact contact) {
            using var profileScope = HostProfiler.Start("InteractionSystem.TryCollideWithSolidEntity");
            if (entityA == entityB) {
                return;
            }

            if (f.TryGetPointer(entityB, out Interactable* entityBInteractable) && entityBInteractable->ColliderDisabled) {
                return;
            }

            PendingInteraction interaction = ((Frame) f).Context.Interactions.FindPlatformInteractor(entityA, f.GetComponentSet(entityA), entityB, f.GetComponentSet(entityB), contact);
            if (interaction.InteractorIndex != -1) {
#if MULTITHREADED
                lock (pendingInteractions) {
                    pendingInteractions.Add(interaction);
                }
#else
                pendingInteractions.Add(interaction);
#endif
            }
        }

        public void OnMarioPlayerGroundpoundedSolid(Frame f, EntityRef entityA, PhysicsContact contact, ref QBoolean continueGroundpound) {
            EntityRef entityB = contact.Entity;
            PendingInteraction interaction = f.Context.Interactions.FindPlatformInteractor(entityA, f.GetComponentSet(entityA), entityB, f.GetComponentSet(entityB), contact);
            if (interaction.InteractorIndex != -1) {
                bool continueInteraction = true;
                f.Signals.OnBeforeInteraction(entityA, &continueInteraction);
                f.Signals.OnBeforeInteraction(entityB, &continueInteraction);

                if (!continueInteraction) {
                    continueGroundpound = false;
                    return;
                }

                continueGroundpound = f.Context.Interactions.platformInteractors[interaction.InteractorIndex].Invoke(f, entityA, entityB, interaction.Contact);
            } else {
                continueGroundpound = false;
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

        public struct EntityRefPair : IEquatable<EntityRefPair>, IEqualityComparer<EntityRefPair> {

            public EntityRef EntityA, EntityB;


            public bool Equals(EntityRefPair other) {
                return (EntityA == other.EntityA && EntityB == other.EntityB) || (EntityA == other.EntityB && EntityB == other.EntityA);
            }

            public bool Equals(EntityRefPair x, EntityRefPair y) {
                return x.Equals(y);
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