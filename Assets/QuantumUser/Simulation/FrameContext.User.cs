using Photon.Deterministic;
using System;
using System.Collections.Generic;

namespace Quantum {
    public partial class FrameContextUser {

        //---Physics
        public LayerMask EntityAndPlayerMask, PlayerOnlyMask;
        public Shape2D CircleRadiusTwo;

        public delegate void PreContactCallback(FrameThreadSafe f, VersusStageData stage, EntityRef entity, PhysicsContact contact, ref bool keepContacts);
        public List<PreContactCallback> PreContactCallbacks = new();

        //---Culling
        public List<FPVector2> CullingCameraPositions = new();
        public FP MaxCameraOrthoSize = 7;

        //---Interactions
        public delegate void HitboxInteractor(Frame f, EntityRef firstEntity, EntityRef secondEntity);
        public delegate void PlatformInteractor(Frame f, EntityRef entity, EntityRef platformEntity, PhysicsContact contact);

        public Dictionary<(Type, Type), HitboxInteractor> hitboxInteractors = new();
        public Dictionary<(Type, Type), PlatformInteractor> platformInteractors = new();
        public HashSet<(EntityRef, EntityRef)> alreadyCollided = new(new UnorderedTupleEqualityComparer<EntityRef>());

        //---Misc
        public Dictionary<int, int> TeamStarBuffer = new(10);


        public void RegisterPreContactCallback(PreContactCallback callback) {
            PreContactCallbacks.Add(callback);
        }

        public void RegisterInteraction<X, Y>(HitboxInteractor interactor) where X : unmanaged, IComponent where Y : unmanaged, IComponent {
            var key = (typeof(X), typeof(Y));

            if (!hitboxInteractors.TryAdd(key, interactor)) {
                Log.Warn($"[InteractionSystem] Already registered an interactor between {typeof(X).Name} and {typeof(Y).Name}.");
            }
        }

        public void RegisterInteraction<X, Y>(PlatformInteractor interactor) where X : unmanaged, IComponent where Y : unmanaged, IComponent {
            var key = (typeof(X), typeof(Y));

            if (!platformInteractors.TryAdd(key, interactor)) {
                Log.Warn($"[InteractionSystem] Already registered an interactor between {typeof(X).Name} and {typeof(Y).Name}.");
            }
        }
    }
}