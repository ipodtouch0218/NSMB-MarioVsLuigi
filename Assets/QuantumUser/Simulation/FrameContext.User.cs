using Photon.Deterministic;
using System;
using System.Collections.Generic;

namespace Quantum {
    public partial class FrameContextUser {

        //---Physics
        public LayerMask ExcludeEntityAndPlayerMask, PlayerOnlyMask;
        public Shape2D CircleRadiusTwo;

        public delegate void PreContactCallback(FrameThreadSafe f, VersusStageData stage, EntityRef entity, PhysicsContact contact, ref bool keepContacts);
        public List<PreContactCallback> PreContactCallbacks = new();

        //---Culling
        public List<EntityRef> CullingIgnoredEntities = new();
        public List<FPVector2> CullingCameraPositions = new();
        public FP MaxCameraOrthoSize = 7;

        //---Interactions
        public delegate void HitboxInteractor(Frame f, EntityRef firstEntity, EntityRef secondEntity);
        public delegate void PlatformInteractor(Frame f, EntityRef entity, EntityRef platformEntity, PhysicsContact contact);
        
        public List<(Type, Type)> hitboxInteractorMap = new();
        public List<HitboxInteractor> hitboxInteractors = new();
        public List<(Type, Type)> platformInteractorMap = new();
        public List<PlatformInteractor> platformInteractors = new();

        public void RegisterPreContactCallback(Frame f, PreContactCallback callback) {
            if (f.IsPredicted) {
                return;
            }

            PreContactCallbacks.Add(callback);
        }

        public void RegisterInteraction<X, Y>(Frame f, HitboxInteractor interactor) where X : unmanaged, IComponent where Y : unmanaged, IComponent {
            if (f.IsPredicted) {
                return;
            }

            var key = (typeof(X), typeof(Y));
            hitboxInteractorMap.Add(key);
            hitboxInteractors.Add(interactor);
        }

        public void RegisterInteraction<X, Y>(Frame f, PlatformInteractor interactor) where X : unmanaged, IComponent where Y : unmanaged, IComponent {
            if (f.IsPredicted) {
                return;
            }

            var key = (typeof(X), typeof(Y));
            platformInteractorMap.Add(key);
            platformInteractors.Add(interactor);
        }
    }
}