using Quantum.Collections;
using Quantum.Physics2D;
using Quantum.Profiling;
using System;
using System.Collections.Generic;

namespace Quantum {
    public unsafe class InteractionSystem : SystemMainThreadEntity<InteractionInitiator>, ISignalOnMarioPlayerGroundpoundedSolid {

        private HashSet<UnorderedEntityRefPair> alreadyInteracted = new(16);

        public override void Update(Frame f) {
            // Safety.
            alreadyInteracted.Clear();

            foreach ((var entity, var initiator) in f.Unsafe.GetComponentBlockIterator<InteractionInitiator>()) {
                if ((f.Unsafe.TryGetPointer(entity, out Interactable* interactable) && interactable->ColliderDisabled)
                    || (f.Unsafe.TryGetPointer(entity, out Enemy* enemy) && enemy->IsDead)
                    || (f.Unsafe.TryGetPointer(entity, out Freezable* freezable) && f.Exists(freezable->FrozenCubeEntity))) {
                    continue;
                }

                // Collide with physical objects
                if (f.Unsafe.TryGetPointer(entity, out PhysicsObject* physicsObject)
                    && f.TryResolveList(physicsObject->Contacts, out QList<PhysicsContact> contacts)) {

                    foreach (var contact in contacts) {
                        if (!f.Exists(contact.Entity)) {
                            continue;
                        }

                        CollideWithPlatform(f, entity, contact.Entity, contact);
                    }
                }

                // Collide with hitboxes
                if (f.Physics2D.TryGetQueryHits(initiator->OverlapQueryRef, out HitCollection hits)) {
                    for (int i = 0; i < hits.Count; i++) {
                        CollideWithHitbox(f, entity, hits[i].Entity);
                    }
                }
                if (f.Physics2D.TryGetQueryHits(initiator->OverlapLevelSeamQueryRef, out hits)) {
                    for (int i = 0; i < hits.Count; i++) {
                        CollideWithHitbox(f, entity, hits[i].Entity);
                    }
                }
            }

            alreadyInteracted.Clear();
        }

        private void CollideWithHitbox(Frame f, EntityRef entityA, EntityRef entityB) {
            using var profileScope = HostProfiler.Start("InteractionSystem.CollideWithHitbox");

            if (entityA.Equals(entityB)
                || !f.Exists(entityA)
                || !f.Exists(entityB)
                || (f.Unsafe.TryGetPointer(entityB, out Interactable* entityBInteractable) && entityBInteractable->ColliderDisabled)) {
                return;
            }

            UnorderedEntityRefPair pair = new(entityA, entityB);

            if (!alreadyInteracted.Add(pair)) {
                return;
            }

            PendingInteraction interaction = f.Context.Interactions.FindHitboxInteractor(entityA, f.GetComponentSet(entityA), entityB, f.GetComponentSet(entityB));
            if (interaction.InteractorIndex != -1) {
                bool continueInteraction = true;
                using (var profileScope2 = HostProfiler.Start("OnBeforeInteraction")) {
                    f.Signals.OnBeforeInteraction(entityA, &continueInteraction);
                    f.Signals.OnBeforeInteraction(entityB, &continueInteraction);
                }

                if (continueInteraction) {
                    using var profileScope3 = HostProfiler.Start("Execute Interactor");
                    f.Context.Interactions.hitboxInteractors[interaction.InteractorIndex].Invoke(f, interaction.EntityA, interaction.EntityB);
                }
            }
        }

        private void CollideWithPlatform(Frame f, EntityRef entityA, EntityRef entityB, in PhysicsContact contact) {
            using var profileScope = HostProfiler.Start("InteractionSystem.CollideWithPlatform");

            if (entityA.Equals(entityB)
                || (f.Unsafe.TryGetPointer(entityB, out Interactable* entityBInteractable) && entityBInteractable->ColliderDisabled)) {
                return;
            }

            UnorderedEntityRefPair pair = new(entityA, entityB);

            if (!alreadyInteracted.Add(pair)) {
                return;
            }

            PendingInteraction interaction = f.Context.Interactions.FindPlatformInteractor(entityA, f.GetComponentSet(entityA), entityB, f.GetComponentSet(entityB), contact);
            if (interaction.InteractorIndex != -1) {
                bool continueInteraction = true;
                using (var profileScope2 = HostProfiler.Start("OnBeforeInteraction")) {
                    f.Signals.OnBeforeInteraction(entityA, &continueInteraction);
                    f.Signals.OnBeforeInteraction(entityB, &continueInteraction);
                }

                if (continueInteraction) {
                    using var profileScope3 = HostProfiler.Start("Execute Interactor");
                    f.Context.Interactions.platformInteractors[interaction.InteractorIndex].Invoke(f, interaction.EntityA, interaction.EntityB, interaction.Contact);
                }
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

                continueGroundpound = f.Context.Interactions.platformInteractors[interaction.InteractorIndex].Invoke(f, interaction.EntityA, interaction.EntityB, interaction.Contact);
            } else {
                continueGroundpound = false;
            }
        }

        public struct PendingInteraction {
            public EntityRef EntityA, EntityB;
            public PhysicsContact Contact;
            public int InteractorIndex;
            public bool IsPlatformInteraction;

            public override int GetHashCode() {
                return HashCodeUtils.CombineHashCodes(
                    EntityA.GetHashCode(), EntityB.GetHashCode(),
                    HashCodeUtils.CombineHashCodes(
                        Contact.GetHashCode(), InteractorIndex, IsPlatformInteraction.GetHashCode()
                    )
                );
            }
        }

        public readonly struct UnorderedEntityRefPair : IEquatable<UnorderedEntityRefPair> {

            public readonly EntityRef EntityA, EntityB;

            public UnorderedEntityRefPair(EntityRef a, EntityRef b) {
                // Order via Index (asc) then Version (asc)
                int indexDiff = a.Index.CompareTo(b.Index);
                if (indexDiff > 0) {
                    (a, b) = (b, a);
                } else if (indexDiff == 0) {
                    // Both have the same index. Sort by version instead
                    int versionDiff = a.Version.CompareTo(b.Version);
                    if (versionDiff > 0) {
                        (a, b) = (b, a);
                    }
                }

                EntityA = a;
                EntityB = b;
            }

            public readonly bool Equals(UnorderedEntityRefPair other) {
                return EntityA.Equals(other.EntityA) && EntityB.Equals(other.EntityB);
            }

            public override readonly bool Equals(object obj) {
                return obj is UnorderedEntityRefPair other && Equals(other);
            }

            public override readonly int GetHashCode() {
                return HashCodeUtils.CombineHashCodes(EntityA.GetHashCode(), EntityB.GetHashCode());
            }
        }
    }
}