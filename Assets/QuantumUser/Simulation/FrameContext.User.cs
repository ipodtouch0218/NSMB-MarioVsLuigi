using Photon.Deterministic;
using System.Collections.Generic;
using static Quantum.InteractionSystem;

namespace Quantum {
    public partial class FrameContextUser {

        //---Physics
        public LayerMask ExcludeEntityAndPlayerMask, PlayerOnlyMask;
        public Shape2D CircleRadiusTwo;

        public delegate void PreContactCallback(Frame f, VersusStageData stage, EntityRef entity, PhysicsContact contact, ref bool keepContacts);
        public readonly List<PreContactCallback> PreContactCallbacks = new();
        public void RegisterPreContactCallback(Frame f, PreContactCallback callback) {
            if (f.IsPredicted) {
                return;
            }

            PreContactCallbacks.Add(callback);
        }

        //---Culling
        public readonly List<EntityRef> CullingIgnoredEntities = new();
        public readonly List<FPVector2> CullingCameraPositions = new();
        public FP MaxCameraOrthoSize = 7;

        //---Interactions
        public readonly InteractionContext Interactions = new();

        public class InteractionContext {

            public delegate void HitboxInteractor(Frame f, EntityRef firstEntity, EntityRef secondEntity);
            public delegate bool PlatformInteractor(Frame f, EntityRef entity, EntityRef platformEntity, PhysicsContact contact);

            public readonly List<(int, int)> hitboxInteractorMap = new();
            public readonly List<HitboxInteractor> hitboxInteractors = new();

            public readonly List<(int, int)> platformInteractorMap = new();
            public readonly List<PlatformInteractor> platformInteractors = new();

            private static readonly PendingInteraction None = new PendingInteraction {
                InteractorIndex = -1
            };

            public PendingInteraction FindHitboxInteractor(EntityRef a, in ComponentSet ac, EntityRef b, in ComponentSet bc) {
                for (int i = 0; i < hitboxInteractorMap.Count; i++) {
                    var key = hitboxInteractorMap[i];
                    if (ac.IsSet(key.Item1)
                        && bc.IsSet(key.Item2)) {

                        return new PendingInteraction {
                            EntityA = a,
                            EntityB = b,
                            InteractorIndex = i,
                            IsPlatformInteraction = false,
                        };
                    } else if (ac.IsSet(key.Item2)
                        && bc.IsSet(key.Item1)) {

                        return new PendingInteraction {
                            EntityA = b,
                            EntityB = a,
                            InteractorIndex = i,
                            IsPlatformInteraction = false,
                        };
                    }
                }

                return None;
            }

            public PendingInteraction FindPlatformInteractor(EntityRef a, in ComponentSet ac, EntityRef b, in ComponentSet bc, in PhysicsContact contact) {
                for (int i = 0; i < platformInteractorMap.Count; i++) {
                    var key = platformInteractorMap[i];
                    if (ac.IsSet(key.Item1)
                        && bc.IsSet(key.Item2)) {

                        return new PendingInteraction {
                            EntityA = a,
                            EntityB = b,
                            InteractorIndex = i,
                            IsPlatformInteraction = true,
                            Contact = contact,
                        };
                    } else if (ac.IsSet(key.Item2)
                        && bc.IsSet(key.Item1)) {

                        return new PendingInteraction {
                            EntityA = b,
                            EntityB = a,
                            InteractorIndex = i,
                            IsPlatformInteraction = true,
                            Contact = contact,
                        };
                    }
                }

                return None;
            }

            public int Register<X, Y>(Frame f, HitboxInteractor interactor) where X : unmanaged, IComponent where Y : unmanaged, IComponent {
                if (f.IsPredicted) {
                    return -1;
                }

                var key = (ComponentTypeId.GetComponentIndex(typeof(X)), ComponentTypeId.GetComponentIndex(typeof(Y)));
                hitboxInteractorMap.Add(key);
                hitboxInteractors.Add(interactor);
                return hitboxInteractors.Count - 1;
            }

            public int Register<X, Y>(Frame f, PlatformInteractor interactor) where X : unmanaged, IComponent where Y : unmanaged, IComponent {
                if (f.IsPredicted) {
                    return -1;
                }

                var key = (ComponentTypeId.GetComponentIndex(typeof(X)), ComponentTypeId.GetComponentIndex(typeof(Y)));
                platformInteractorMap.Add(key);
                platformInteractors.Add(interactor);
                return platformInteractors.Count - 1;
            }
        }
    }
}