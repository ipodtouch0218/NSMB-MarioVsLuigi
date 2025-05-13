#if QUANTUM_ENABLE_MIGRATION
using Quantum;
#endif
#pragma warning disable IDE0065 // Misplaced using directive
using System;
using System.Collections.Generic;
using Photon.Deterministic;
using Quantum.Profiling;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;

#pragma warning restore IDE0065 // Misplaced using directive

namespace Quantum {
  /// <summary>
  /// The Quantum entity view component is the representation of the entity inside Unity.
  /// Instances will be created by the <see cref="EntityViewUpdater"/>.
  /// Quantum entity with the <see cref="View"/> component will references a Quantum <see cref="EntityView"/> asset which will in turn be instantiated as 
  /// <see cref="EntityView.Prefab"/> and the resulting game object includes this script.
  /// </summary>
  [DisallowMultipleComponent]
  [ScriptHelp(BackColor = ScriptHeaderBackColor.Orange)]
  public unsafe class QuantumEntityView
#if QUANTUM_ENABLE_MIGRATION
#pragma warning disable CS0618
    : global::EntityView { }
#pragma warning restore CS0618
} // namespace Quantum
  [Obsolete("Use QuantumEntityView instead")]
  [LastSupportedVersion("3.0")]
  public unsafe abstract class EntityView
#endif
    : QuantumMonoBehaviour {
    /// <summary>
    /// Wrapping UnityEvent(QuantumGame) into a class. Is used by the QuantumEntityView to make create and destroy publish Unity events.
    /// </summary>
    [Serializable]
    public class EntityUnityEvent : UnityEvent<QuantumGame> {
    }

    /// <summary>
    /// Will set to the <see cref="AssetObject.Guid"/> that the underlying <see cref="EntityView"/> asset has.
    /// Or receives a new Guid when binding this view script to a scene entity.
    /// </summary>
    [NonSerialized] public AssetGuid AssetGuid;

    /// <summary>
    /// References the Quantum entity that this view script is liked to.
    /// </summary>
    [NonSerialized] public EntityRef EntityRef;

    /// <summary>
    /// Set the entity view bind behaviour. If set to <see cref="QuantumEntityViewBindBehaviour.NonVerified"/> then the view is created during a predicted frame.
    /// Entity views created at that time can be subject to changes and even be destroyed because of misprediction.
    /// Entity views created during <see cref="QuantumEntityViewBindBehaviour.Verified"/> will be more stable but are always created at a later time, when the input has been confirmed by the server.
    /// </summary>
    [FormerlySerializedAs("CreateBehaviour")]
    [InlineHelp]
    public QuantumEntityViewBindBehaviour BindBehaviour;

    /// <summary>
    /// If enabled the QuantumEntityViewUpdater will not destroy (or disable, in case of map entities) this instance.
    /// The responsibility to destroy the game object is on the user.
    /// The <see cref="OnEntityDestroyed"/> callback will still be called.
    /// </summary>
    [FormerlySerializedAs("ManualDestroy")]
    [FormerlySerializedAs("ManualDiposal")]
    [InlineHelp]
    public bool ManualDisposal;

    /// <summary>
    /// Obsolete property, use ManualDisposal
    /// </summary>
    [Obsolete("Use ManualDisposal")] public bool ManualDiposal => ManualDisposal;

    /// <summary>
    /// Set the <see cref="QuantumEntityViewFlags"/> to further configure the entity view.
    /// </summary>
    [InlineHelp] public QuantumEntityViewFlags ViewFlags;

    /// <summary>
    /// Set the <see cref="QuantumEntityViewInterpolationMode"/> allowing the view timing to be switched between prediction and snapshot-interpolation.
    /// Requires the QuantumEntityViewFlags.SnapshotInterpolationEnabled flag to be set to have an effect.
    /// AUTO selects the mode dynamically based on the prediction-culled state of the entity. 
    /// </summary>
    [InlineHelp] public QuantumEntityViewInterpolationMode InterpolationMode;

    /// <summary>
    /// If set to true, the game object will be renamed to the EntityRef number.
    /// </summary>
    [Obsolete("Use QuantumEntityViewFlags.DisableEntityRefNaming")]
    public bool GameObjectNameIsEntityRef {
      get {
        return !HasViewFlag(QuantumEntityViewFlags.DisableEntityRefNaming);
      }
      set {
        SetViewFlag(QuantumEntityViewFlags.DisableEntityRefNaming, !value);
      }
    }

    /// <summary>
    ///   <para>
    ///     A factor with dimension of 1/s (Hz) that works as a lower limit for how much
    ///     of the accumulated prediction error is corrected every frame.
    ///     This factor affects both the position and the rotation correction.
    ///     Suggested values are greater than zero and smaller than
    ///     <see cref="ErrorCorrectionRateMax">ErrorCorrectionRateMax</see>.
    ///   </para>
    ///   <para>
    ///     E.g.: ErrorCorrectionRateMin = 3, rendering delta time = (1/60)s: at least 5% (3 * 1/60) of the accumulated error
    ///     will be corrected on this rendered frame.
    ///   </para>
    ///   <para>
    ///     This threshold might not be respected if the resultant correction magnitude is
    ///     below the <see cref="ErrorPositionMinCorrection">ErrorPositionMinCorrection</see>
    ///     or above the <see cref="ErrorPositionTeleportDistance">ErrorPositionTeleportDistance</see>, for the position error,
    ///     or above the <see cref="ErrorRotationTeleportDistance">ErrorRotationTeleportDistance</see>, for the rotation error.
    ///   </para>
    /// </summary>
    [Header("Prediction Error Correction")]
    [InlineHelp]
    public Single ErrorCorrectionRateMin = 3.3f;

    /// <summary>
    ///   <para>
    ///     A factor with dimension of 1/s (Hz) that works as a upper limit for how much
    ///     of the accumulated prediction error is corrected every frame.
    ///     This factor affects both the position and the rotation correction.
    ///     Suggested values are greater than <see cref="ErrorCorrectionRateMin">ErrorCorrectionRateMin</see>
    ///     and smaller than half of a target rendering rate.
    ///   </para>
    ///   <para>
    ///     E.g.: ErrorCorrectionRateMax = 15, rendering delta time = (1/60)s: at maximum 25% (15 * 1/60) of the accumulated
    ///     error
    ///     will be corrected on this rendered frame.
    ///   </para>
    ///   <para>
    ///     This threshold might not be respected if the resultant correction magnitude is
    ///     below the <see cref="ErrorPositionMinCorrection">ErrorPositionMinCorrection</see> or
    ///     above the <see cref="ErrorPositionTeleportDistance">ErrorPositionTeleportDistance</see>, for the position error,
    ///     or above the <see cref="ErrorRotationTeleportDistance">ErrorRotationTeleportDistance</see>, for the rotation error.
    ///   </para>
    /// </summary>
    [InlineHelp] public Single ErrorCorrectionRateMax = 10f;

    /// <summary>
    ///   <para>
    ///     The reference for the magnitude of the accumulated position error, in meters,
    ///     at which the position error will be corrected at the
    ///     <see cref="ErrorCorrectionRateMin">ErrorCorrectionRateMin</see>.
    ///     Suggested values are greater than <see cref="ErrorPositionMinCorrection">ErrorPositionMinCorrection</see>
    ///     and smaller than <see cref="ErrorPositionBlendEnd">ErrorPositionBlendEnd</see>.
    ///   </para>
    ///   <para>
    ///     In other words, if the magnitude of the accumulated error is equal to or smaller than this threshold,
    ///     it will be corrected at the <see cref="ErrorCorrectionRateMin">ErrorCorrectionRateMin</see>.
    ///     If, instead, the magnitude is between this threshold and
    ///     <see cref="ErrorPositionBlendEnd">ErrorPositionBlendEnd</see>,
    ///     the error is corrected at a rate between <see cref="ErrorCorrectionRateMin">ErrorCorrectionRateMin</see>
    ///     and <see cref="ErrorCorrectionRateMax">ErrorCorrectionRateMax</see>, proportionally.
    ///     If it is equal to or greater than <see cref="ErrorPositionBlendEnd">ErrorPositionBlendEnd</see>,
    ///     it will be corrected at the <see cref="ErrorCorrectionRateMax">ErrorCorrectionRateMax</see>.
    ///   </para>
    ///   <para>
    ///     Note: as the factor is expressed in distance units (meters), it might need to be scaled
    ///     proportionally to the overall scale of objects in the scene and speeds at which they move,
    ///     which are factors that affect the expected magnitude of prediction errors.
    ///   </para>
    /// </summary>
    [InlineHelp] public Single ErrorPositionBlendStart = 0.25f;

    /// <summary>
    ///   <para>
    ///     The reference for the magnitude of the accumulated position error, in meters,
    ///     at which the position error will be corrected at the
    ///     <see cref="ErrorCorrectionRateMax">ErrorCorrectionRateMax</see>.
    ///     Suggested values are greater than <see cref="ErrorPositionBlendStart">ErrorPositionBlendStart</see>
    ///     and smaller than <see cref="ErrorPositionTeleportDistance">ErrorPositionTeleportDistance</see>.
    ///   </para>
    ///   <para>
    ///     In other words, if the magnitude of the accumulated error is equal to or greater than this threshold,
    ///     it will be corrected at the <see cref="ErrorCorrectionRateMax">ErrorCorrectionRateMax</see>.
    ///     If, instead, the magnitude is between <see cref="ErrorPositionBlendStart">ErrorPositionBlendStart</see> and this
    ///     threshold,
    ///     the error is corrected at a rate between <see cref="ErrorCorrectionRateMin">ErrorCorrectionRateMin</see>
    ///     and <see cref="ErrorCorrectionRateMax">ErrorCorrectionRateMax</see>, proportionally.
    ///     If it is equal to or smaller than <see cref="ErrorPositionBlendStart">ErrorPositionBlendStart</see>,
    ///     it will be corrected at the <see cref="ErrorCorrectionRateMin">ErrorCorrectionRateMin</see>.
    ///   </para>
    ///   <para>
    ///     Note: as the factor is expressed in distance units (meters), it might need to be scaled
    ///     proportionally to the overall scale of objects in the scene and speeds at which they move,
    ///     which are factors that affect the expected magnitude of prediction errors.
    ///   </para>
    /// </summary>
    [InlineHelp] public Single ErrorPositionBlendEnd = 1f;

    /// <summary>
    ///   <para>
    ///     The reference for the magnitude of the accumulated rotation error, in radians,
    ///     at which the rotation error will be corrected at the
    ///     <see cref="ErrorCorrectionRateMin">ErrorCorrectionRateMin</see>.
    ///     Suggested values are smaller than <see cref="ErrorRotationBlendEnd">ErrorRotationBlendEnd</see>.
    ///   </para>
    ///   <para>
    ///     In other words, if the magnitude of the accumulated error is equal to or smaller than this threshold,
    ///     it will be corrected at the <see cref="ErrorCorrectionRateMin">ErrorCorrectionRateMin</see>.
    ///     If, instead, the magnitude is between this threshold and
    ///     <see cref="ErrorRotationBlendEnd">ErrorRotationBlendEnd</see>,
    ///     the error is corrected at a rate between <see cref="ErrorCorrectionRateMin">ErrorCorrectionRateMin</see>
    ///     and <see cref="ErrorCorrectionRateMax">ErrorCorrectionRateMax</see>, proportionally.
    ///     If it is equal to or greater than <see cref="ErrorRotationBlendEnd">ErrorRotationBlendEnd</see>,
    ///     it will be corrected at the <see cref="ErrorCorrectionRateMax">ErrorCorrectionRateMax</see>.
    ///   </para>
    /// </summary>
    [InlineHelp] public Single ErrorRotationBlendStart = 0.1f;

    /// <summary>
    ///   <para>
    ///     The reference for the magnitude of the accumulated rotation error, in radians,
    ///     at which the rotation error will be corrected at the
    ///     <see cref="ErrorCorrectionRateMax">ErrorCorrectionRateMax</see>.
    ///     Suggested values are greater than <see cref="ErrorRotationBlendStart">ErrorRotationBlendStart</see>
    ///     and smaller than <see cref="ErrorRotationTeleportDistance">ErrorRotationTeleportDistance</see>.
    ///   </para>
    ///   <para>
    ///     In other words, if the magnitude of the accumulated error is equal to or greater than this threshold,
    ///     it will be corrected at the <see cref="ErrorCorrectionRateMax">ErrorCorrectionRateMax</see>.
    ///     If, instead, the magnitude is between <see cref="ErrorRotationBlendStart">ErrorRotationBlendStart</see> and this
    ///     threshold,
    ///     the error is corrected at a rate between <see cref="ErrorCorrectionRateMin">ErrorCorrectionRateMin</see>
    ///     and <see cref="ErrorCorrectionRateMax">ErrorCorrectionRateMax</see>, proportionally.
    ///     If it is equal to or smaller than <see cref="ErrorRotationBlendStart">ErrorRotationBlendStart</see>,
    ///     it will be corrected at the <see cref="ErrorCorrectionRateMin">ErrorCorrectionRateMin</see>.
    ///   </para>
    /// </summary>
    [InlineHelp] public Single ErrorRotationBlendEnd = 0.5f;

    /// <summary>
    ///   <para>
    ///     The value, in meters, that represents the minimum magnitude of the accumulated position error
    ///     that will be corrected in a single frame, until it is fully corrected.
    ///   </para>
    ///   <para>
    ///     This setting has priority over the resultant correction rate, i.e. the restriction
    ///     will be respected even if it makes the effective correction rate be different than
    ///     the one computed according to the min/max rates and start/end blend values.
    ///     Suggested values are greater than zero and smaller than
    ///     <see cref="ErrorPositionBlendStart">ErrorPositionBlendStart</see>.
    ///   </para>
    ///   <para>
    ///     Note: as the factor is expressed in distance units (meters), it might need to be scaled
    ///     proportionally to the overall scale of objects in the scene and speeds at which they move,
    ///     which are factors that affect the expected magnitude of prediction errors.
    ///   </para>
    /// </summary>
    [InlineHelp] public Single ErrorPositionMinCorrection = 0.025f;

    /// <summary>
    ///   <para>
    ///     The value, in meters, that represents the magnitude of the accumulated
    ///     position error above which the error will be instantaneously corrected,
    ///     effectively teleporting the rendered object to its correct position.
    ///     Suggested values are greater than <see cref="ErrorPositionBlendEnd">ErrorPositionBlendEnd</see>.
    ///   </para>
    ///   <para>
    ///     This setting has priority over the resultant correction rate, i.e. the restriction
    ///     will be respected even if it makes the effective correction rate be different than
    ///     the one computed according to the min/max rates and start/end blend values.
    ///   </para>
    ///   <para>
    ///     Note: as the factor is expressed in distance units (meters), it might need to be scaled
    ///     proportionally to the overall scale of objects in the scene and speeds at which they move,
    ///     which are factors that affect the expected magnitude of prediction errors.
    ///   </para>
    /// </summary>
    [InlineHelp] public Single ErrorPositionTeleportDistance = 2f;

    /// <summary>
    ///   <para>
    ///     The value, in radians, that represents the magnitude of the accumulated
    ///     rotation error above which the error will be instantaneously corrected,
    ///     effectively teleporting the rendered object to its correct orientation.
    ///     Suggested values are greater than <see cref="ErrorRotationBlendEnd">ErrorRotationBlendEnd</see>.
    ///   </para>
    ///   <para>
    ///     This setting has priority over the resultant correction rate, i.e. the restriction
    ///     will be respected even if it makes the effective correction rate be different than
    ///     the one computed according to the min/max rates and start/end blend values.
    ///   </para>
    /// </summary>
    [InlineHelp] public Single ErrorRotationTeleportDistance = 0.5f;

    /// <summary>
    /// Is called after the entity view has been instantiated.
    /// </summary>
    [Header("Events")] public EntityUnityEvent OnEntityInstantiated;

    /// <summary>
    /// Is called before the entity view is destroyed.
    /// </summary>
    public EntityUnityEvent OnEntityDestroyed;

    /// <summary>
    /// Access the entity view components registered to this entity view.
    /// All view components found on this game object during creation are used.
    /// </summary>
    public IQuantumViewComponent[] ViewComponents => _viewComponents;

    /// <summary>
    /// A reference to the entity view updater that controls this entity view.
    /// </summary>
    public QuantumEntityViewUpdater EntityViewUpdater { get; private set; }

    /// <summary>
    /// A reference to the current game that this entity view belongs to <see cref="QuantumEntityViewUpdater.ObservedGame"/>.
    /// </summary>
    public QuantumGame Game { get; internal set; }

    /// <summary>
    /// All contexts found on the <see cref="EntityViewUpdater"/> game object accessible by their type.
    /// </summary>
    public Dictionary<Type, IQuantumViewContext> ViewContexts { get; private set; }

    /// <summary>
    /// Set the <see cref="QuantumEntityViewFlags"/> to further configure the entity view.
    /// </summary>
    /// <param name="flag">The flag enum value</param>
    /// <param name="isEnabled">Set or unset the flag.</param>
    public void SetViewFlag(QuantumEntityViewFlags flag, bool isEnabled) {
      if (isEnabled) {
        ViewFlags |= flag;
      } else {
        ViewFlags &= ~flag;
      }
    }

    /// <summary>
    /// Test if a view flag is set.
    /// </summary>
    /// <param name="flag"></param>
    /// <returns></returns>
    public bool HasViewFlag(QuantumEntityViewFlags flag) => (ViewFlags & flag) == flag;

    /// <summary>
    /// Access the transform of the entity view.
    /// In play mode the transform object will be cached to improve the performance.
    /// </summary>
    public Transform Transform {
      get {
#if UNITY_EDITOR
        if (Application.isPlaying == false)
          return base.transform;
#endif
        if (_transformCached == false) {
          _cachedTransform = base.transform;
          _transformCached = true;
        }

        return _cachedTransform;
      }
    }

    DispatcherSubscription _snapshotSubscription;
    FP _lastPredictedVerticalPosition2D;
    FPVector2 _lastPredictedPosition2D;
    FPVector3 _lastPredictedPosition3D;
    FP _lastPredictedRotation2D;
    FPQuaternion _lastPredictedRotation3D;
    Vector3 _errorVisualVector;
    Quaternion _errorVisualQuaternion;
    IQuantumViewComponent[] _viewComponents;
    Transform _cachedTransform;
    bool _transformCached;
    bool _useSnapshotInterpolation = false;
    QuantumSnapshotInterpolationTimer.InterpolationBuffer<QuantumSnapshotInterpolationTimer.QuantumTransformData> _interpolationBuffer;

    float InterpolationAlpha => _useSnapshotInterpolation ? EntityViewUpdater.SnapshotInterpolation.Alpha : Game.InterpolationFactor;
    int InterpolationFrameFrom => EntityViewUpdater.SnapshotInterpolation.CurrentFrom;

    /// <summary>
    /// The struct is used to gather all transform and interpolation data to apply new transform data to the entity view.
    /// <see cref="ApplyTransform(ref UpdatePositionParameter)"/>.
    /// </summary>
    public struct UpdatePositionParameter {
      /// <summary>
      /// The new position.
      /// </summary>
      public Vector3 NewPosition;
      /// <summary>
      /// The new rotation.
      /// </summary>
      public Quaternion NewRotation;
      /// <summary>
      /// The un-interpolated position.
      /// </summary>
      public Vector3 UninterpolatedPosition;
      /// <summary>
      /// The un-interpolated rotation.
      /// </summary>
      public Quaternion UninterpolatedRotation;
      /// <summary>
      /// The position error correction.
      /// </summary>
      public Vector3 ErrorVisualVector;
      /// <summary>
      /// The rotation error correction.
      /// </summary>
      public Quaternion ErrorVisualQuaternion;
      /// <summary>
      /// Is there a position error induced by misprediction.
      /// </summary>
      public bool PositionErrorTeleport;
      /// <summary>
      /// Is there a rotation error induced by misprediction.
      /// </summary>
      public bool RotationErrorTeleport;
      /// <summary>
      /// Is this a position teleport.
      /// </summary>
      public bool PositionTeleport;
      /// <summary>
      /// Is this a rotation teleport.
      /// </summary>
      public bool RotationTeleport;
    }

    /// <summary>
    /// A callback to override to add custom logic to the initialization of this entity view.
    /// </summary>
    public virtual void OnInitialize() { }
    /// <summary>
    /// A callback to override to add custom logic the activation of this entity view.
    /// </summary>
    /// <param name="frame">Frame</param>
    public virtual void OnActivate(Frame frame) { }
    /// <summary>
    /// A callback to override to add custom logic to the deactivation of this entity view.
    /// </summary>
    public virtual void OnDeactivate() { }
    /// <summary>
    /// A callback to override to add custom logic to the update method.
    /// </summary>
    public virtual void OnUpdateView() { }
    /// <summary>
    /// A callback to override to add custom logic to the late update method.
    /// </summary>
    public virtual void OnLateUpdateView() { }
    /// <summary>
    /// A callback to override to add custom logic when the associate game has changed on the connected <see cref="QuantumEntityViewUpdater"/>.
    /// </summary>
    public virtual void OnGameChanged() { }

    void Initialize(Dictionary<Type, IQuantumViewContext> contexts, QuantumEntityViewUpdater entityViewUpdater) {
      if ((ViewFlags & QuantumEntityViewFlags.DisableSearchChildrenForEntityViewComponents) > 0) {
        _viewComponents = GetComponents<IQuantumViewComponent>();
      } else {
        _viewComponents = GetComponentsInChildren<IQuantumViewComponent>(
          includeInactive: (ViewFlags & QuantumEntityViewFlags.DisableSearchInactiveForEntityViewComponents) == 0);
      }

      EntityViewUpdater = entityViewUpdater;

      OnInitialize();

      for (int i = 0; i < _viewComponents.Length; i++) {
        _viewComponents[i].Initialize(contexts);
      }
    }

    internal void TryInitSnapshotInterpolation() {
      if ((ViewFlags & QuantumEntityViewFlags.EnableSnapshotInterpolation) == 0) {
        return;
      }

      _interpolationBuffer ??= new QuantumSnapshotInterpolationTimer.InterpolationBuffer<
          QuantumSnapshotInterpolationTimer.QuantumTransformData>(32);

      _interpolationBuffer?.Reset();

      _snapshotSubscription =
          QuantumCallback.Subscribe(this, (CallbackSimulateFinished callback) => RegisterSnapshot(callback.Frame));
    }

    internal void Activate(QuantumGame game, Frame frame, Dictionary<Type, IQuantumViewContext> contexts, QuantumEntityViewUpdater entityViewUpdater) {
      // TODO: When the observed game is switched, this needs to be propagated here.
      _lastPredictedPosition2D = default(FPVector2);
      _lastPredictedRotation2D = default(FP);
      _lastPredictedPosition3D = default(FPVector3);
      _lastPredictedRotation3D = FPQuaternion.Identity;
      _errorVisualVector = default(Vector3);
      _errorVisualQuaternion = Quaternion.identity;

      if (frame.Has<Transform2D>(EntityRef)) {
        Game = game;
        UpdateFromTransform2D(game, false, false, isSpawning: true);
      } else if (frame.Has<Transform3D>(EntityRef)) {
        Game = game;
        UpdateFromTransform3D(game, false, false, isSpawning: true);
      }

      ViewContexts = contexts;

      if (_viewComponents == null) {
        Initialize(contexts, entityViewUpdater);
      }

      Game = game;
     
      OnActivate(frame);

      TryInitSnapshotInterpolation();

      for (int i = 0; i < _viewComponents.Length; i++) {
#if QUANTUM_ENABLE_MIGRATION
        _viewComponents[i].Activate(frame, Game, (QuantumEntityView)this);
#else
        _viewComponents[i].Activate(frame, Game, this);
#endif
      }
    }

    internal void RegisterSnapshot(Frame frame) {
      if (frame.IsVerified == false) return;
      QuantumSnapshotInterpolationTimer.QuantumTransformData data =
        new QuantumSnapshotInterpolationTimer.QuantumTransformData();
      if (frame.TryGet<Transform2D>(EntityRef, out data.Transform2D) || frame.TryGet<Transform3D>(EntityRef, out data.Transform3D)) {
        data.Has2DVertical = frame.TryGet<Transform2DVertical>(EntityRef, out data.Transform2DVertical);
        data.IsValid = true;
        _interpolationBuffer.Add(data, frame.Number);
      }
    }

    internal void GameChanged(QuantumGame game) {
      Game = game;

      OnGameChanged();

      for (int i = 0; i < _viewComponents.Length; i++) {
        _viewComponents[i].GameChanged(game);
      }
    }

    internal void Deactivate() {
      if (_viewComponents == null) {
        // GameObjects already destroyed
        return;
      }

      QuantumCallback.Unsubscribe(_snapshotSubscription);

      for (int i = 0; i < _viewComponents.Length; i++) {
        _viewComponents[i].Deactivate();
      }

      OnDeactivate();
    }

    internal void UpdateView(bool useClockAliasingInterpolation, bool useErrorCorrection) {
      if ((ViewFlags & QuantumEntityViewFlags.DisableUpdatePosition) == 0) {
        if (Game.Frames.Predicted.Has<Transform2D>(EntityRef)) {
          // update 2d transform
          UpdateFromTransform2D(Game, useClockAliasingInterpolation, useErrorCorrection, isSpawning: false);
        } else {
          // update 3d transform
          if (Game.Frames.Predicted.Has<Transform3D>(EntityRef)) {
            UpdateFromTransform3D(Game, useClockAliasingInterpolation, useErrorCorrection, isSpawning: false);
          }
        }
      }

      if ((ViewFlags & QuantumEntityViewFlags.DisableUpdateView) == 0) {
        

        OnUpdateView();

        for (int i = 0; i < _viewComponents.Length; i++) {
          if (_viewComponents[i].IsActiveAndEnabled) {
            _viewComponents[i].UpdateView();
          }
        }
      }
    }

    internal void LateUpdateView() {
      if ((ViewFlags & QuantumEntityViewFlags.DisableUpdateView) > 0) {
        return;
      }

      OnLateUpdateView();

      for (int i = 0; i < _viewComponents.Length; i++) {
        if (_viewComponents[i].IsActiveAndEnabled) {
          _viewComponents[i].LateUpdateView();
        }
      }
    }

    /// <summary>
    /// There are two sources how to get the transform 3D data from an entity.
    /// Using the Quantum predicted frames or a snapshot interpolation. Toggle the setting on <see cref="InterpolationMode"/>.
    /// </summary>
    /// <param name="timeRef">Time reference context</param>
    /// <param name="param">Transform parameter to be filled out</param>
    /// <param name="frameNumber">Resulting frame number</param>
    /// <param name="transform">Resulting transform</param>
    /// <param name="isSpawning">Is the entity spawning</param>
    /// <returns>True if data could be retrieved</returns>
    public bool TryGetTransform3DData(QuantumEntityViewTimeReference timeRef, ref UpdatePositionParameter param,
      out int frameNumber, out Transform3D transform, bool isSpawning) {
      transform = default;
      frameNumber = 0;


      Frame frame = null;
      if (Game == null) return false;

      if (isSpawning == false && _useSnapshotInterpolation) {
        var frameFrom = InterpolationFrameFrom;

        switch (timeRef) {
          case QuantumEntityViewTimeReference.To:
            frameNumber = frameFrom + 1;
            break;
          case QuantumEntityViewTimeReference.From:
            frameNumber = frameFrom;
            break;
          case QuantumEntityViewTimeReference.ErrorCorrection:
            return false;
        }

        if (_interpolationBuffer.TryGet(out var data, frameNumber) && data.IsValid) {
          transform = data.Transform3D;
          return true;
        }

        return false;
      } else {
        switch (timeRef) {
          case QuantumEntityViewTimeReference.To:
            frame = Game.Frames.Predicted;
            break;
          case QuantumEntityViewTimeReference.From:
            frame = Game.Frames.PredictedPrevious;
            break;
          case QuantumEntityViewTimeReference.ErrorCorrection:
            frame = Game.Frames.PreviousUpdatePredicted;
            break;
        }

        frameNumber = frame.Number;

        if (frame.TryGet<Transform3D>(EntityRef, out transform)) {
          return true;
        }
      }
      return false;
    }

    /// <summary>
    /// Apply new transform 3D data and interpolation.
    /// </summary>
    /// <param name="game">Game</param>
    /// <param name="useClockAliasingInterpolation">Use clock aliasing interpolation</param>
    /// <param name="useErrorCorrectionInterpolation">Use error correction interpolation</param>
    /// <param name="isSpawning">Is the entity just spawning</param>
    public void UpdateFromTransform3D(QuantumGame game, Boolean useClockAliasingInterpolation,
      Boolean useErrorCorrectionInterpolation, bool isSpawning) {
      if (game == null)
        return;

      UpdateUseSnapshotInterpolation();

      var param = new UpdatePositionParameter();

      if (TryGetTransform3DData(QuantumEntityViewTimeReference.To, ref param, out var frameTo, out var transform, isSpawning) ==
          false) return;

      param.NewPosition = transform.Position.ToUnityVector3();
      param.NewRotation = transform.Rotation.ToUnityQuaternion();

      param.PositionTeleport = transform.PositionTeleportFrame == frameTo;
      param.RotationTeleport = transform.RotationTeleportFrame == frameTo;
      param.UninterpolatedPosition = param.NewPosition;
      param.UninterpolatedRotation = param.NewRotation;

      if (TryGetTransform3DData(QuantumEntityViewTimeReference.From, ref param, out var frameFrom, out var transformPrevious, isSpawning)) {
        if (useClockAliasingInterpolation) {
          param.NewPosition = Vector3.Lerp(transformPrevious.Position.ToUnityVector3(), param.NewPosition,
            InterpolationAlpha);
          param.NewRotation = Quaternion.Slerp(transformPrevious.Rotation.ToUnityQuaternion(), param.NewRotation,
            InterpolationAlpha);
        }

        if (useErrorCorrectionInterpolation) {
          if (TryGetTransform3DData(QuantumEntityViewTimeReference.ErrorCorrection, ref param, out var frameOld, out var oldTransform, false)) {
            var errorPosition = _lastPredictedPosition3D - oldTransform.Position;
            var errorRotation = Quaternion.Inverse(oldTransform.Rotation.ToUnityQuaternion()) *
                                _lastPredictedRotation3D.ToUnityQuaternion();
            _errorVisualVector += errorPosition.ToUnityVector3();
            _errorVisualQuaternion = errorRotation * _errorVisualQuaternion;
          } else {
            _errorVisualVector = default;
            _errorVisualQuaternion = Quaternion.identity;
          }
        }
      }

      // update rendered position
      UpdateRenderPosition(ref param);

      // store current prediction information
      _lastPredictedPosition3D = transform.Position;
      _lastPredictedRotation3D = transform.Rotation;
    }

    /// <summary>
    /// There are two sources how to get the transform 3D data from an entity.
    /// Using the Quantum predicted frames or a snapshot interpolation. Toggle the setting on <see cref="InterpolationMode"/>.
    /// </summary>
    /// <param name="timeRef">Time reference context</param>
    /// <param name="param">Transform parameter to be filled out</param>
    /// <param name="frameNumber">Resulting frame number</param>
    /// <param name="transform">Resulting transform</param>
    /// <param name="transformVertical">Transform vertical component</param>
    /// <param name="hasVertical">Has a transform vertical 2D component</param>
    /// <param name="isSpawning">Is the entity spawning</param>
    /// <returns>True if data could be retrieved</returns>
    /// <returns></returns>
    public bool TryGetTransform2DData(QuantumEntityViewTimeReference timeRef, ref UpdatePositionParameter param,
      out int frameNumber, out Transform2D transform, out Transform2DVertical transformVertical, out bool hasVertical, bool isSpawning) {
      transform = default;
      transformVertical = default;
      frameNumber = 0;
      hasVertical = false;

      Frame frame = null;
      if (Game == null) return false;

      if (isSpawning == false && _useSnapshotInterpolation) {
        var frameFrom = InterpolationFrameFrom;

        switch (timeRef) {
          case QuantumEntityViewTimeReference.To:
            frameNumber = frameFrom + 1;
            break;
          case QuantumEntityViewTimeReference.From:
            frameNumber = frameFrom;
            break;
          case QuantumEntityViewTimeReference.ErrorCorrection:
            return false;
        }
        //Debug.Log(frameNumber + " " + (InterolationBuffer == null));
        if (_interpolationBuffer.TryGet(out var data, frameNumber) && data.IsValid) {
          transform = data.Transform2D;
          if (data.Has2DVertical) {
            transformVertical = data.Transform2DVertical;
            hasVertical = true;
          }
          return true;
        }

        return false;
      } else {
        switch (timeRef) {
          case QuantumEntityViewTimeReference.To:
            frame = Game.Frames.Predicted;
            break;
          case QuantumEntityViewTimeReference.From:
            frame = Game.Frames.PredictedPrevious;
            break;
          case QuantumEntityViewTimeReference.ErrorCorrection:
            frame = Game.Frames.PreviousUpdatePredicted;
            break;
        }

        frameNumber = frame.Number;
        
        hasVertical = frame.TryGet<Transform2DVertical>(EntityRef, out transformVertical);

          if (frame.TryGet<Transform2D>(EntityRef, out transform)) {
          return true;
        }
      }

      return false;
    }

    private void UpdateUseSnapshotInterpolation() {
      _useSnapshotInterpolation = false;
      if ((ViewFlags & QuantumEntityViewFlags.EnableSnapshotInterpolation) == QuantumEntityViewFlags.EnableSnapshotInterpolation) {
        var culled = Game.Frames.Predicted.IsCulled(EntityRef);
        _useSnapshotInterpolation = InterpolationMode == QuantumEntityViewInterpolationMode.SnapshotInterpolation ||
                                    (culled && InterpolationMode == QuantumEntityViewInterpolationMode.Auto);
      } else {
#if DEBUG
        // Issue a warning when the EnableSnapshotInterpolation flag is missing and disable the mode
        if (InterpolationMode == QuantumEntityViewInterpolationMode.SnapshotInterpolation ||
            InterpolationMode == QuantumEntityViewInterpolationMode.Auto) {
          Log.Warn($"EntityView {name} InterpolationMode {InterpolationMode} is only supported when the QuantumEntityViewFlags.EnableSnapshotInterpolation is enabled, setting Interpolation mode to Prediction");
          InterpolationMode = QuantumEntityViewInterpolationMode.Prediction;
        }
#endif
      }
    }

    /// <summary>
    /// Apply new transform 2D data and interpolation.
    /// </summary>
    /// <param name="game">Game</param>
    /// <param name="useClockAliasingInterpolation">Use clock aliasing interpolation</param>
    /// <param name="useErrorCorrectionInterpolation">Use error correction interpolation</param>
    /// <param name="isSpawning">Is the entity just spawning</param>
    public void UpdateFromTransform2D(QuantumGame game, Boolean useClockAliasingInterpolation,
      Boolean useErrorCorrectionInterpolation, bool isSpawning) {
      if (game == null)
        return;

      var param = new UpdatePositionParameter();

      UpdateUseSnapshotInterpolation();

      if (TryGetTransform2DData(QuantumEntityViewTimeReference.To, ref param, out var toFrame,
            out var transform, out var tVertical, out var hasVertical, isSpawning) == false) {
        return;
      }

      param.NewPosition = transform.Position.ToUnityVector3();
      param.NewRotation = transform.Rotation.ToUnityQuaternion();

      param.PositionTeleport = transform.PositionTeleportFrame == toFrame;
      param.RotationTeleport = transform.RotationTeleportFrame == toFrame;
      if (hasVertical) {
#if QUANTUM_XY
        param.NewPosition.z = -tVertical.Position.AsFloat;
#else
        param.NewPosition.y = tVertical.Position.AsFloat;
#endif
      }

      param.UninterpolatedPosition = param.NewPosition;
      param.UninterpolatedRotation = param.NewRotation;

      if (TryGetTransform2DData(QuantumEntityViewTimeReference.From, ref param, out var fromFrame,
            out var transformPrevious, out var tVerticalPrevious, out var hasVerticalPrevious, isSpawning)) {
        if (useClockAliasingInterpolation) {
          var previousPos = transformPrevious.Position.ToUnityVector3();
          if (hasVerticalPrevious) {
#if QUANTUM_XY
            previousPos.z = -tVerticalPrevious.Position.AsFloat;
#else
            previousPos.y = tVerticalPrevious.Position.AsFloat;
#endif
          }

          param.NewPosition = Vector3.Lerp(previousPos, param.NewPosition, InterpolationAlpha);
          param.NewRotation = Quaternion.Slerp(transformPrevious.Rotation.ToUnityQuaternion(), param.NewRotation,
            InterpolationAlpha);
        }

        if (useErrorCorrectionInterpolation) {
          if (TryGetTransform2DData(QuantumEntityViewTimeReference.ErrorCorrection, ref param, out var errorFrame,
                out var oldTransform, out var oldTransformVertical, out var hasVerticalOld, false)) {
            // position error
            var errorPosition = _lastPredictedPosition2D - oldTransform.Position;
            var errorVertical = _lastPredictedVerticalPosition2D;
            if (hasVerticalOld) {
              errorVertical -= oldTransformVertical.Position;
            }

            var errorVector = errorPosition.ToUnityVector3();
#if QUANTUM_XY
            errorVector.z = -errorVertical.AsFloat;
#else
            errorVector.y = errorVertical.AsFloat;
#endif

            _errorVisualVector += errorVector;

            // rotation error
            var errorRotation = _lastPredictedRotation2D - oldTransform.Rotation;
            _errorVisualQuaternion = errorRotation.ToUnityQuaternion() * _errorVisualQuaternion;
          } else {
            _errorVisualVector = default;
            _errorVisualQuaternion = Quaternion.identity;
          }
        }
      }

      // update rendered position
      UpdateRenderPosition(ref param);

      // store current prediction information
      _lastPredictedPosition2D = transform.Position;
      _lastPredictedVerticalPosition2D = hasVertical ? tVertical.Position : default;
      _lastPredictedRotation2D = transform.Rotation;
    }

    void UpdateRenderPosition(ref UpdatePositionParameter param) {
      var positionCorrectionRate = ErrorCorrectionRateMin;
      var rotationCorrectionRate = ErrorCorrectionRateMin;

      var positionErrorTeleport = false;
      var rotationErrorTeleport = false;

      // if we're going over teleport distance, we should just teleport
      var positionErrorMagnitude = _errorVisualVector.magnitude;
      if (positionErrorMagnitude > ErrorPositionTeleportDistance) {
        positionErrorTeleport = true;
        _errorVisualVector = default(Vector3);
        // we need to revert the alias interpolation when detecting a visual teleport
        param.NewPosition = param.UninterpolatedPosition;
      } else {
        var blendDiff = ErrorPositionBlendEnd - ErrorPositionBlendStart;
        var blendRate = Mathf.Clamp01((positionErrorMagnitude - ErrorPositionBlendStart) / blendDiff);
        positionCorrectionRate = Mathf.Lerp(ErrorCorrectionRateMin, ErrorCorrectionRateMax, blendRate);
      }

      var quatDot = Quaternion.Dot(_errorVisualQuaternion, Quaternion.identity);
      // ensuring we stay within acos domain
      quatDot = Mathf.Clamp(quatDot, -1, 1);

      // angle, in radians, between the two quaternions
      var rotationErrorMagnitude = Mathf.Acos(quatDot) * 2.0f;
      if (rotationErrorMagnitude > ErrorRotationTeleportDistance) {
        rotationErrorTeleport = true;
        _errorVisualQuaternion = Quaternion.identity;
        param.NewRotation = param.UninterpolatedRotation;
      } else {
        var blendDiff = ErrorRotationBlendEnd - ErrorRotationBlendStart;
        var blendRate = Mathf.Clamp01((rotationErrorMagnitude - ErrorRotationBlendStart) / blendDiff);
        rotationCorrectionRate = Mathf.Lerp(ErrorCorrectionRateMin, ErrorCorrectionRateMax, blendRate);
      }

      // apply new position (+ potential error correction)
      param.ErrorVisualVector = _errorVisualVector;
      param.ErrorVisualQuaternion = _errorVisualQuaternion;
      param.PositionErrorTeleport = positionErrorTeleport;
      param.RotationErrorTeleport = rotationErrorTeleport;

      using (HostProfiler.Start("QuantumEntityView.ApplyTransform")) {
        ApplyTransform(ref param);
      }

      // reduce position error
      var positionCorrectionMultiplier = 1f - (Time.deltaTime * positionCorrectionRate);
      var positionCorrectionAmount = _errorVisualVector * positionCorrectionMultiplier;
      if (positionCorrectionAmount.magnitude < ErrorPositionMinCorrection) {
        UpdateMinPositionCorrection(positionCorrectionMultiplier, positionCorrectionAmount);
      } else {
        _errorVisualVector *= positionCorrectionMultiplier;
      }

      // reduce rotation error
      _errorVisualQuaternion = Quaternion.Slerp(_errorVisualQuaternion, Quaternion.identity,
        Time.deltaTime * rotationCorrectionRate);
    }

    /// <summary>
    /// The method to override to apply the final position and rotation interpolation to the view transform.
    /// </summary>
    /// <param name="param"></param>
    protected virtual void ApplyTransform(ref UpdatePositionParameter param) {
      Vector3 newPosition;
      if (param.PositionTeleport) {
        newPosition = param.UninterpolatedPosition;
      } else {
        newPosition = param.NewPosition + param.ErrorVisualVector;
      }

      Quaternion newRotation;
      if (param.RotationTeleport) {
        newRotation = param.UninterpolatedRotation;
      } else {
        newRotation = param.ErrorVisualQuaternion * param.NewRotation;
      }

      if ((ViewFlags & QuantumEntityViewFlags.UseCachedTransform) > 0) {
        // Override this in subclass to change how the new position is applied to the transform.
        Transform.position = newPosition;

        // Unity's quaternion multiplication is equivalent to applying rhs then lhs (despite their doc saying the opposite)
        Transform.rotation = newRotation;
      } else {
        // Override this in subclass to change how the new position is applied to the transform.
        transform.position = newPosition;

        // Unity's quaternion multiplication is equivalent to applying rhs then lhs (despite their doc saying the opposite)
        transform.rotation = newRotation;
      }
    }

    void UpdateMinPositionCorrection(float positionCorrectionMultiplier, Vector3 positionCorrectionAmount) {
      if (_errorVisualVector.x == 0f && _errorVisualVector.y == 0f && _errorVisualVector.z == 0f) {
        return;
      }

      // calculate normalized vector
      var normalized = _errorVisualVector.normalized;

      // store signs so we know when we flip an axis
      var xSign = _errorVisualVector.x >= 0f;
      var ySign = _errorVisualVector.y >= 0f;
      var zSign = _errorVisualVector.z >= 0f;

      // subtract vector by normalized*ErrorPositionMinCorrection
      _errorVisualVector -= (normalized * ErrorPositionMinCorrection);

      // if sign flipped it means we passed zero
      if (xSign != (_errorVisualVector.x >= 0f)) {
        _errorVisualVector.x = 0f;
      }

      if (ySign != (_errorVisualVector.y >= 0f)) {
        _errorVisualVector.y = 0f;
      }

      if (zSign != (_errorVisualVector.z >= 0f)) {
        _errorVisualVector.z = 0f;
      }
    }
  }
#if !QUANTUM_ENABLE_MIGRATION
} // namespace Quantum
#endif