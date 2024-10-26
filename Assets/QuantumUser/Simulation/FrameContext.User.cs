using Photon.Deterministic;
using System;
using System.Collections.Generic;
using static Quantum.InteractionSystem;

namespace Quantum {
    public partial class FrameContextUser {

        public int EntityPlayerMask;
        public List<FPVector2> CullingCameraPositions = new();
        public FP MaxCameraOrthoSize = 7;

        public Dictionary<(Type, Type), HitboxInteractor> hitboxInteractors = new();
        public Dictionary<(Type, Type), PlatformInteractor> platformInteractors = new();
        public HashSet<(EntityRef, EntityRef)> alreadyCollided = new(new UnorderedTupleEqualityComparer<EntityRef>());

        public void RegisterInteraction<X, Y>(HitboxInteractor interactor) where X : unmanaged, IComponent where Y : unmanaged, IComponent {
            var key = (typeof(X), typeof(Y));

            if (hitboxInteractors.ContainsKey(key)) {
                Log.Warn($"[InteractionSystem] Already registered an interactor between {typeof(X).Name} and {typeof(Y).Name}.");
            } else {
                hitboxInteractors[key] = interactor;
            }
        }

        public void RegisterInteraction<X, Y>(PlatformInteractor interactor) where X : unmanaged, IComponent where Y : unmanaged, IComponent {
            var key = (typeof(X), typeof(Y));

            if (platformInteractors.ContainsKey(key)) {
                Log.Warn($"[InteractionSystem] Already registered an interactor between {typeof(X).Name} and {typeof(Y).Name}.");
            } else {
                platformInteractors[key] = interactor;
            }
        }
    }
}