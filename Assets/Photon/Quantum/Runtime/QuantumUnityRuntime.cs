#if !QUANTUM_DEV

#region Assets/Photon/Quantum/Runtime/AssetTypes/AssetTypes.Partial.cs

namespace Quantum {
  using System;
  using System.Linq;
  using Photon.Deterministic;
  using Physics2D;
  using Physics3D;
  using UnityEngine;
  using UnityEngine.Serialization;

  public partial class QPrototypeNavMeshPathfinder {
    [LocalReference]
    [DrawIf("Prototype.InitialTargetNavMesh.Id.Value", 0)]
    public QuantumMapNavMeshUnity InitialTargetNavMeshReference;

    public override void Refresh() {
      if (InitialTargetNavMeshReference != null) {
        Prototype.InitialTargetNavMeshName = InitialTargetNavMeshReference.name;
      }
    }
  }

  public partial class QPrototypePhysicsCollider2D {
    [MultiTypeReference(new Type [] {
#if QUANTUM_ENABLE_PHYSICS2D && !QUANTUM_DISABLE_PHYSICS2D
      typeof(BoxCollider2D), typeof(CircleCollider2D),
#endif
#if QUANTUM_ENABLE_PHYSICS3D && !QUANTUM_DISABLE_PHYSICS3D
      typeof(BoxCollider), typeof(SphereCollider),
#endif
    })]
    public Component SourceCollider;

    public QuantumEntityPrototypeColliderLayerSource LayerSource = QuantumEntityPrototypeColliderLayerSource.GameObject;

    public override void Refresh() {
      if (TrySetShapeConfigFromSourceCollider(Prototype.ShapeConfig, transform, SourceCollider, out bool isTrigger)) {
        Prototype.IsTrigger = isTrigger;
        
        if (LayerSource != QuantumEntityPrototypeColliderLayerSource.Explicit) {
          Prototype.Layer = SourceCollider.gameObject.layer;
        }
      } else if (LayerSource == QuantumEntityPrototypeColliderLayerSource.GameObject) {
        Prototype.Layer = this.gameObject.layer;
      }
    }

    public static bool TrySetShapeConfigFromSourceCollider(Shape2DConfig config, Transform reference, Component collider, out bool isTrigger) {
      if (collider == null) {
        isTrigger = false;
        return false;
      }

      // if the source collider is child (same object, immediate- or deep-child),
      // avoid using the source's scale in order to not scale the settings twice
      Vector2 sourceScale2D;
      Vector3 sourceScale3D;
      if (collider.transform.IsChildOf(reference)) {
        sourceScale2D = Vector2.one;
        sourceScale3D = Vector3.one;
      } else {
        sourceScale2D = collider.transform.lossyScale.ToFPVector2().ToUnityVector2();
        sourceScale3D = collider.transform.lossyScale;
      }

      switch (collider) {
#if QUANTUM_ENABLE_PHYSICS3D && !QUANTUM_DISABLE_PHYSICS3D
        case BoxCollider box:
          config.ShapeType      = Shape2DType.Box;
          config.BoxExtents     = Vector3.Scale(box.size / 2, sourceScale3D).ToFPVector2();
          config.PositionOffset = reference.transform.InverseTransformPoint(box.transform.TransformPoint(box.center)).ToFPVector2();
          config.RotationOffset = (Quaternion.Inverse(reference.transform.rotation) * box.transform.rotation).ToFPRotation2DDegrees();
          isTrigger             = box.isTrigger;
          break;

        case SphereCollider sphere:
          config.ShapeType      = Shape2DType.Circle;
          config.CircleRadius   = (Math.Max(Math.Max(Math.Abs(sourceScale3D.x), Math.Abs(sourceScale3D.y)), Math.Abs(sourceScale3D.z)) * sphere.radius).ToFP();
          config.PositionOffset = reference.transform.InverseTransformPoint(sphere.transform.TransformPoint(sphere.center)).ToFPVector2();
          config.RotationOffset = (Quaternion.Inverse(reference.transform.rotation) * sphere.transform.rotation).ToFPRotation2DDegrees();
          isTrigger             = sphere.isTrigger;
          break;
#endif

#if QUANTUM_ENABLE_PHYSICS2D && !QUANTUM_DISABLE_PHYSICS2D
        case BoxCollider2D box:
          config.ShapeType      = Shape2DType.Box;
          config.BoxExtents     = Vector2.Scale(box.size / 2, sourceScale2D).ToFPVector2();
          config.PositionOffset = reference.transform.InverseTransformPoint(box.transform.TransformPoint(box.offset.ToFPVector2().ToUnityVector3())).ToFPVector2();

          var refBoxTransform2D = Transform2D.Create(reference.transform.position.ToFPVector2(), reference.transform.rotation.ToFPRotation2D());
          var boxTransform2D    = Transform2D.Create(box.transform.position.ToFPVector2(), box.transform.rotation.ToFPRotation2D());
          config.RotationOffset = (boxTransform2D.Rotation - refBoxTransform2D.Rotation) * FP.Rad2Deg;
          isTrigger             = box.isTrigger;
          break;

        case CircleCollider2D circle:
          config.ShapeType      = Shape2DType.Circle;
          config.CircleRadius   = (Math.Max(Math.Abs(sourceScale2D.x), Math.Abs(sourceScale2D.y)) * circle.radius).ToFP();
          config.PositionOffset = reference.transform.InverseTransformPoint(circle.transform.TransformPoint(circle.offset.ToFPVector2().ToUnityVector3())).ToFPVector2();

          var refCircleTransform2D = Transform2D.Create(reference.transform.position.ToFPVector2(), reference.transform.rotation.ToFPRotation2D());
          var circleTransform2D    = Transform2D.Create(circle.transform.position.ToFPVector2(), circle.transform.rotation.ToFPRotation2D());
          config.RotationOffset = (circleTransform2D.Rotation - refCircleTransform2D.Rotation) * FP.Rad2Deg;
          isTrigger             = circle.isTrigger;
          break;

        case CapsuleCollider2D capsule:
          config.ShapeType      = Shape2DType.Capsule;
          config.CapsuleSize.X   = (Math.Abs(sourceScale2D.x) * capsule.size.x).ToFP();
          config.CapsuleSize.Y   = (Math.Abs(sourceScale2D.y) * capsule.size.y).ToFP();
          config.PositionOffset = reference.transform.InverseTransformPoint(capsule.transform.TransformPoint(capsule.offset.ToFPVector2().ToUnityVector3())).ToFPVector2();

          var refCapsuleTransform2D = Transform2D.Create(reference.transform.position.ToFPVector2(), reference.transform.rotation.ToFPRotation2D());
          var capsuleTransform2D    = Transform2D.Create(capsule.transform.position.ToFPVector2(), capsule.transform.rotation.ToFPRotation2D());
          config.RotationOffset = (capsuleTransform2D.Rotation - refCapsuleTransform2D.Rotation) * FP.Rad2Deg;
          isTrigger             = capsule.isTrigger;
          break;
#endif

        default:
          throw new NotSupportedException($"Type {collider.GetType().FullName} not supported, needs to be one of: "
#if QUANTUM_ENABLE_PHYSICS2D && !QUANTUM_DISABLE_PHYSICS2D
            + $"{nameof(BoxCollider2D)} {nameof(CircleCollider2D)} "
#endif
#if QUANTUM_ENABLE_PHYSICS3D && !QUANTUM_DISABLE_PHYSICS3D
            + $"{nameof(BoxCollider)} {nameof(SphereCollider)}"
#endif
          );
      }
      
      return true;
    }
  }

  public partial class QPrototypePhysicsCollider3D {
#if QUANTUM_ENABLE_PHYSICS3D && !QUANTUM_DISABLE_PHYSICS3D
    [FormerlySerializedAs("SourceCollider3D")]
    [MultiTypeReference(typeof(BoxCollider), typeof(SphereCollider))]
    public Collider SourceCollider;
    
    public QuantumEntityPrototypeColliderLayerSource LayerSource = QuantumEntityPrototypeColliderLayerSource.GameObject;

    public override void Refresh() {
      if (TrySetShapeConfigFromSourceCollider(Prototype.ShapeConfig, transform, SourceCollider, out bool isTrigger)) {
        Prototype.IsTrigger = isTrigger;
        
        if (LayerSource != QuantumEntityPrototypeColliderLayerSource.Explicit) {
          Prototype.Layer = SourceCollider.gameObject.layer;
        }
      } else if (LayerSource == QuantumEntityPrototypeColliderLayerSource.GameObject) {
        Prototype.Layer = this.gameObject.layer;
      }
    }
#endif
    
    public static bool TrySetShapeConfigFromSourceCollider(Shape3DConfig config, Transform reference, Component collider, out bool isTrigger) {
      if (collider == null) {
        isTrigger = false;
        return false;
      }

      // if the source collider is child (same object, immediate- or deep-child),
      // avoid using the source's scale in order to not scale the settings twice
      var sourceScale = collider.transform.IsChildOf(reference) ? Vector3.one : collider.transform.lossyScale;

      switch (collider) {
#if QUANTUM_ENABLE_PHYSICS3D && !QUANTUM_DISABLE_PHYSICS3D
        case BoxCollider box:
          config.ShapeType      = Shape3DType.Box;
          config.BoxExtents     = Vector3.Scale(box.size / 2, sourceScale).ToFPVector3();
          config.PositionOffset = reference.transform.InverseTransformPoint(box.transform.TransformPoint(box.center)).ToFPVector3();
          config.RotationOffset = (Quaternion.Inverse(reference.transform.rotation) * box.transform.rotation).eulerAngles.ToFPVector3();
          isTrigger             = box.isTrigger;
          break;

        case SphereCollider sphere:
          config.ShapeType      = Shape3DType.Sphere;
          config.SphereRadius   = (Math.Max(Math.Max(Math.Abs(sourceScale.x), Math.Abs(sourceScale.y)), Math.Abs(sourceScale.z)) * sphere.radius).ToFP();
          config.PositionOffset = reference.transform.InverseTransformPoint(sphere.transform.TransformPoint(sphere.center)).ToFPVector3();
          config.RotationOffset = (Quaternion.Inverse(reference.transform.rotation) * sphere.transform.rotation).eulerAngles.ToFPVector3();
          isTrigger             = sphere.isTrigger;
          break;

        case CapsuleCollider capsule:
          config.ShapeType      = Shape3DType.Capsule;
          config.CapsuleRadius   = (Math.Max(Math.Max(Math.Abs(sourceScale.x), Math.Abs(sourceScale.y)), Math.Abs(sourceScale.z)) * capsule.radius).ToFP();
          config.CapsuleHeight  =  (Math.Abs(sourceScale.y) * capsule.height).ToFP();
          config.PositionOffset = reference.transform.InverseTransformPoint(capsule.transform.TransformPoint(capsule.center)).ToFPVector3();
          config.RotationOffset = (Quaternion.Inverse(reference.transform.rotation) * capsule.transform.rotation).eulerAngles.ToFPVector3();
          isTrigger             = capsule.isTrigger;
          break;
#endif

        default:
          throw new NotSupportedException($"Type {collider.GetType().FullName} not supported, needs to be one of: "
#if QUANTUM_ENABLE_PHYSICS3D && !QUANTUM_DISABLE_PHYSICS3D
            + $"{nameof(BoxCollider)}, {nameof(SphereCollider)}"
#endif
          );
      }
    
      return true;
    }
    
    private static string CreateTypeNotSupportedMessage(Type colliderType, params Type[] supportedTypes) {
      return $"Type {colliderType.FullName} not supported, needs to be one of {(string.Join(", ", supportedTypes.Select(x => x.Name)))}";
    }
  }

  [RequireComponent(typeof(QuantumEntityPrototype))]
  public partial class QPrototypePhysicsJoints2D {
    private void OnValidate() => AutoConfigureDistance();

    public override void Refresh() => AutoConfigureDistance();

    private void AutoConfigureDistance() {
      if (Prototype.JointConfigs == null) {
        return;
      }

      FPMathUtils.LoadLookupTables();

      foreach (var config in Prototype.JointConfigs) {
        if (config.AutoConfigureDistance && config.JointType != JointType.None) {
          var anchorPos    = transform.position.ToFPVector2() + FPVector2.Rotate(config.Anchor, transform.rotation.ToFPRotation2D());
          var connectedPos = config.ConnectedAnchor;

          if (config.ConnectedEntity != null) {
            var connectedTransform = config.ConnectedEntity.transform;
            connectedPos =  FPVector2.Rotate(connectedPos, connectedTransform.rotation.ToFPRotation2D());
            connectedPos += connectedTransform.position.ToFPVector2();
          }

          config.Distance    = FPVector2.Distance(anchorPos, connectedPos);
          config.MinDistance = config.Distance;
          config.MaxDistance = config.Distance;
        }

        if (config.MinDistance > config.MaxDistance) {
          config.MinDistance = config.MaxDistance;
        }
      }
    }
  }

  [RequireComponent(typeof(QuantumEntityPrototype))]
  public partial class QPrototypePhysicsJoints3D {
    public override void Refresh() {
      AutoConfigureDistance();
    }

    private void AutoConfigureDistance() {
      if (Prototype.JointConfigs == null) {
        return;
      }

      FPMathUtils.LoadLookupTables();

      foreach (var config in Prototype.JointConfigs) {
        if (config.AutoConfigureDistance && config.JointType != JointType3D.None) {
          var anchorPos    = transform.position.ToFPVector3() + transform.rotation.ToFPQuaternion() * config.Anchor;
          var connectedPos = config.ConnectedAnchor;

          if (config.ConnectedEntity != null) {
            var connectedTransform = config.ConnectedEntity.transform;
            connectedPos =  connectedTransform.rotation.ToFPQuaternion() * connectedPos;
            connectedPos += connectedTransform.position.ToFPVector3();
          }

          config.Distance    = FPVector3.Distance(anchorPos, connectedPos);
          config.MinDistance = config.Distance;
          config.MaxDistance = config.Distance;
        }

        if (config.MinDistance > config.MaxDistance) {
          config.MinDistance = config.MaxDistance;
        }
      }
    }
  }

  public partial class QPrototypeTransform2D {
    public bool AutoSetPosition = true;
    public bool AutoSetRotation = true;
    
    public override void Refresh() {
      if (AutoSetPosition) {
        Prototype.Position = transform.position.ToFPVector2();
      }

      if (AutoSetRotation) {
        Prototype.Rotation = transform.rotation.ToFPRotation2DDegrees();
      }
    }
  }

  public partial class QPrototypeTransform2DVertical {
    [Tooltip("If enabled, the lossy scale of the transform in the vertical Quantum asset will be used")]
    public bool AutoSetHeight = true;

    public bool AutoSetPosition = true;
    
    public override void Refresh() {
#if QUANTUM_XY
      var verticalScale = transform.lossyScale.z.ToFP();
      var verticalPos   = -transform.position.z.ToFP();
#else
      var verticalScale = transform.lossyScale.y.ToFP();
      var verticalPos   = transform.position.y.ToFP();
#endif

      if (AutoSetPosition) {
        // based this on MapDataBaker for colliders
        Prototype.Position = verticalPos * verticalScale;
      }

      if (AutoSetHeight) {
        Prototype.Height = verticalScale;
      }
    }
  }

  public partial class QPrototypeTransform3D {
    public bool AutoSetPosition = true;
    public bool AutoSetRotation = true;
    
    public override void Refresh() {
      if (AutoSetPosition) {
        Prototype.Position = transform.position.ToFPVector3();
      }

      if (AutoSetRotation) {
        Prototype.Rotation = transform.rotation.eulerAngles.ToFPVector3();
      }
    }
  }
}


#endregion


#region Assets/Photon/Quantum/Runtime/AssetTypes/QuantumUnityComponentPrototype.cs

namespace Quantum {
  using System;
  using UnityEditor;
  using UnityEngine;

  [RequireComponent(typeof(QuantumEntityPrototype))]
  [ScriptHelp(BackColor = ScriptHeaderBackColor.Olive)]
  public abstract class QuantumUnityComponentPrototype
#if QUANTUM_ENABLE_MIGRATION
#pragma warning disable CS0618
    : EntityComponentBase {}
#pragma warning restore CS0618
  
  [Obsolete("Use QuantumUnityComponentPrototype instead.")]
  [RequireComponent(typeof(QuantumEntityPrototype))]
  [ScriptHelp(BackColor = ScriptHeaderBackColor.Olive)]
  public abstract class EntityComponentBase 
#endif
    : QuantumMonoBehaviour {
    public abstract Type ComponentType { get; }
    public abstract Type PrototypeType { get; }

    private void OnValidate() {
      Refresh();
    }

    public virtual void Refresh() {
    }

    /// <summary>
    /// </summary>
    /// <param name="converter"></param>
    /// <returns></returns>
    public abstract ComponentPrototype CreatePrototype(QuantumEntityPrototypeConverter converter);

    protected ComponentPrototype ConvertPrototype(QuantumEntityPrototypeConverter converter, ComponentPrototype prototype) {
      return prototype;
    }

    protected ComponentPrototype ConvertPrototype(QuantumEntityPrototypeConverter converter, IQuantumUnityPrototypeAdapter prototypeAdapter) {
      return (ComponentPrototype)prototypeAdapter.Convert(converter);
    }

#if UNITY_EDITOR
    [Obsolete("Move custom inspector code to EntityComponentBaseEditor subclass.", true)]
    public virtual void OnInspectorGUI(SerializedObject so, IQuantumEditorGUI QuantumEditorGUI) {
      DrawPrototype(so, QuantumEditorGUI);
      DrawNonPrototypeFields(so, QuantumEditorGUI);
    }

    [Obsolete("Move custom inspector code to EntityComponentBaseEditor subclass.", true)]
    protected void DrawPrototype(SerializedObject so, IQuantumEditorGUI QuantumEditorGUI) {
    }

    [Obsolete("Move custom inspector code to EntityComponentBaseEditor subclass.", true)]
    protected void DrawNonPrototypeFields(SerializedObject so, IQuantumEditorGUI QuantumEditorGUI) {
    }
#endif
  }

  public abstract class QuantumUnityComponentPrototype<TPrototype>
#if QUANTUM_ENABLE_MIGRATION
#pragma warning disable CS0618
    : EntityComponentBase<TPrototype> where TPrototype : ComponentPrototype, new() { }
#pragma warning restore CS0618
  
  [Obsolete("Use QuantumUnityComponentPrototype<TPrototype> instead.")]
  public abstract class EntityComponentBase<TPrototype> 
#endif
    : QuantumUnityComponentPrototype
    where TPrototype : ComponentPrototype, new() {
    public override Type PrototypeType => typeof(TPrototype);
  }
  
  public interface IQuantumUnityPrototypeWrapperForComponent<T> where T : IComponent {
  }
}

#endregion


#region Assets/Photon/Quantum/Runtime/AssetTypes/QuantumUnityPrototypeAdapter.cs

namespace Quantum {
  using System;

  public interface IQuantumUnityPrototypeAdapter
#if QUANTUM_ENABLE_MIGRATION
#pragma warning disable CS0618
    : IPrototypeAdapter
#pragma warning restore CS0618
  {}
  
  [Obsolete("Use " + nameof(IQuantumUnityPrototypeAdapter) + " instead.")]
  public interface IPrototypeAdapter
#endif
  {
    Type       PrototypedType { get; }
    IPrototype Convert(QuantumEntityPrototypeConverter converter);
  }

  public abstract class QuantumUnityPrototypeAdapter<PrototypeType> 
#if QUANTUM_ENABLE_MIGRATION
#pragma warning disable CS0618
    : PrototypeAdapter<PrototypeType> where PrototypeType : IPrototype
#pragma warning restore CS0618
  {}
  
  [Obsolete("Use  QuantumUnityPrototypeAdapter instead.")]
  public abstract class PrototypeAdapter<PrototypeType>
#endif
    : IQuantumUnityPrototypeAdapter, IQuantumPrototypeConvertible<PrototypeType>
      where PrototypeType : IPrototype {
    public Type PrototypedType => typeof(PrototypeType);

    
    IPrototype
#if QUANTUM_ENABLE_MIGRATION
     IPrototypeAdapter.Convert
#else
     IQuantumUnityPrototypeAdapter.Convert
#endif
    (QuantumEntityPrototypeConverter converter) {
      return Convert(converter);
    }

    public abstract PrototypeType Convert(QuantumEntityPrototypeConverter converter);
  }
  
  public interface IQuantumPrototypeConvertible<T> {
    public T Convert(QuantumEntityPrototypeConverter converter);
  }
  
  public abstract class QuantumUnityUnionPrototypeAdapter<T> : QuantumUnityPrototypeAdapter<T> where T : IPrototype {
  }
}

#endregion


#region Assets/Photon/Quantum/Runtime/Dispatcher/IQuantumUnityDispatcher.cs

namespace Quantum {
  using UnityEngine;

  /// <summary>
  /// An interface marking a dispatcher as being a Quantum Unity dispatcher.
  /// </summary>
  public interface IQuantumUnityDispatcher {
  }

  /// <summary>
  /// Set of extension methods for <see cref="IQuantumUnityDispatcher"/>.
  /// </summary>
  // ReSharper disable once InconsistentNaming
  public static class IQuantumUnityDispatcherExtensions {
    
    const uint UnityDispatcherFlagIsUnityObject          = 1 << (DispatcherHandlerFlags.CustomFlagsShift + 0);
    const uint UnityDispatcherFlagOnlyIfActiveAndEnabled = 1 << (DispatcherHandlerFlags.CustomFlagsShift + 1);

    /// <summary>
    /// Gets the status of a specific listener. Depending on subscription flags and whether the listener is a Unity object,
    /// the listener activity status is determined. 
    /// </summary>
    /// <param name="self"></param>
    /// <param name="listener"></param>
    /// <param name="flags"></param>
    /// <returns></returns>
    internal static DispatcherBase.ListenerStatus GetUnityListenerStatus(this IQuantumUnityDispatcher self, object listener, uint flags) {
      if (listener == null) {
        return DispatcherBase.ListenerStatus.Dead;
      }

      if ((flags & UnityDispatcherFlagIsUnityObject) == 0) {
        // not a unity object, so can't be dead
        return DispatcherBase.ListenerStatus.Active;
      }

      // needs to be Unity object now
      Debug.Assert(listener is Object);

      var asUnityObject = (Object)listener;

      if (!asUnityObject) {
        return DispatcherBase.ListenerStatus.Dead;
      }

      if ((flags & UnityDispatcherFlagOnlyIfActiveAndEnabled) != 0) {
        if (listener is Behaviour behaviour) {
          return behaviour.isActiveAndEnabled ? DispatcherBase.ListenerStatus.Active : DispatcherBase.ListenerStatus.Inactive;
        } 
        
        if (listener is GameObject gameObject) {
          return gameObject.activeInHierarchy ? DispatcherBase.ListenerStatus.Active : DispatcherBase.ListenerStatus.Inactive;
        }
      }

      return DispatcherBase.ListenerStatus.Active;
    }

    internal static DispatcherSubscription Subscribe<TDispatcher, T>(this TDispatcher dispatcher, Object listener, DispatchableHandler<T> handler, bool once = false, bool onlyIfActiveAndEnabled = false, DispatchableFilter filter = null)
      where TDispatcher : DispatcherBase, IQuantumUnityDispatcher
      where T : IDispatchable {
      return dispatcher.Subscribe(listener, handler, once, UnityDispatcherFlagIsUnityObject | (onlyIfActiveAndEnabled ? UnityDispatcherFlagOnlyIfActiveAndEnabled : 0), filter: filter);
    }
  }
}

#endregion


#region Assets/Photon/Quantum/Runtime/Dispatcher/QuantumCallback.cs

namespace Quantum {
  using System;
  using System.Collections.Generic;
  using UnityEngine;

  /// <summary>
  /// Quantum callbacks are special types of events that are triggered internally by the Quantum simulation.
  /// For example CallbackUpdateView for Unity updates, CallbackPollInput that polls for player input.
  /// Use this class to subscribe and unsubscribe from Quantum callbacks.
  /// </summary>
  /// <example><code>
  /// // Use this signature when subscribing from a MonoBehaviour, the subscription will be automatically removed when the MonoBehaviour is destroyed.
  /// QuantumCallback.Subscribe(this, (CallbackUpdateView c) => { Log.Debug(c.Game.Frames.Verified.Number); });
  /// // Use this signature when manually disposing the subscription.
  /// var subscription = QuantumCallback.SubscribeManual((CallbackUpdateView c) => { Log.Debug(c.Game.Frames.Verified.Number); });
  /// subscription.Dispose();
  /// </code></example>
  public abstract partial class QuantumCallback : QuantumUnityStaticDispatcherAdapter<QuantumUnityCallbackDispatcher, CallbackBase> {
    private QuantumCallback() {
      throw new NotSupportedException();
    }

    [RuntimeInitializeOnLoadMethod]
    static void SetupDefaultHandlers() {
      // default callbacks handlers are initialised here; if you want them disabled, implement partial
      // method IsDefaultHandlerEnabled

      {
        bool enabled = true;
        IsDefaultHandlerEnabled(typeof(QuantumCallbackHandler_DebugDraw), ref enabled);
        // ReSharper disable once ConditionIsAlwaysTrueOrFalse
        if (enabled) {
          QuantumCallbackHandler_DebugDraw.Initialize();
        }
      }
      {
        bool enabled = true;
        IsDefaultHandlerEnabled(typeof(QuantumCallbackHandler_FrameDiffer), ref enabled);
        // ReSharper disable once ConditionIsAlwaysTrueOrFalse
        if (enabled) {
          QuantumCallbackHandler_FrameDiffer.Initialize();
        }
      }
      {
        bool enabled = true;
        IsDefaultHandlerEnabled(typeof(QuantumCallbackHandler_GameResult), ref enabled);
        // ReSharper disable once ConditionIsAlwaysTrueOrFalse
        if (enabled) {
          QuantumCallbackHandler_GameResult.Initialize();
        }
      }
      {
        bool enabled = true;
        IsDefaultHandlerEnabled(typeof(QuantumCallbackHandler_LegacyQuantumCallback), ref enabled);
        // ReSharper disable once ConditionIsAlwaysTrueOrFalse
        if (enabled) {
          QuantumCallbackHandler_LegacyQuantumCallback.Initialize();
        }
      }
      {
        bool enabled = true;
        IsDefaultHandlerEnabled(typeof(QuantumCallbackHandler_StartRecording), ref enabled);
        // ReSharper disable once ConditionIsAlwaysTrueOrFalse
        if (enabled) {
          QuantumCallbackHandler_StartRecording.Initialize();
        }
      }
      {
        bool enabled = true;
        IsDefaultHandlerEnabled(typeof(QuantumCallbackHandler_UnityCallbacks), ref enabled);
        // ReSharper disable once ConditionIsAlwaysTrueOrFalse
        if (enabled) {
          QuantumCallbackHandler_UnityCallbacks.Initialize();
        }
      }
    }
    
    /// <summary>
    /// Implement this partial method to disable default callback handlers.
    /// </summary>
    /// <param name="type"></param>
    /// <param name="enabled"></param>
    static partial void IsDefaultHandlerEnabled(Type type, ref bool enabled);
  }

  /// <summary>
  /// <see cref="Quantum.CallbackDispatcher"/> implementation for Unity. Adds Unity specific callback types. Additional user callback
  /// types can be added via partial method. 
  /// </summary>
  public partial class QuantumUnityCallbackDispatcher : CallbackDispatcher, IQuantumUnityDispatcher {
    
    /// <summary>
    /// Initializes the dispatcher with the built-in and user defined callback types.
    /// </summary>
    public QuantumUnityCallbackDispatcher() : base(GetCallbackTypes()) { }

    /// <inheritdoc cref="IQuantumUnityDispatcherExtensions.GetUnityListenerStatus"/>
    protected override ListenerStatus GetListenerStatus(object listener, uint flags) {
      return this.GetUnityListenerStatus(listener, flags);
    }

    /// <summary>
    /// Partial method to add user defined callback types. Custom IDs need to start from the initial <paramref name="dict"/> count. 
    /// </summary>
    /// <param name="dict">Target dictionary.</param>
    static partial void AddUserTypes(Dictionary<Type, int> dict);
    
    private static Dictionary<Type, Int32> GetCallbackTypes() {
      var types = GetBuiltInTypes();

      // unity-side callback types
      types.Add(typeof(CallbackUnitySceneLoadBegin), CallbackUnitySceneLoadBegin.ID);
      types.Add(typeof(CallbackUnitySceneLoadDone), CallbackUnitySceneLoadDone.ID);
      types.Add(typeof(CallbackUnitySceneUnloadBegin), CallbackUnitySceneUnloadBegin.ID);
      types.Add(typeof(CallbackUnitySceneUnloadDone), CallbackUnitySceneUnloadDone.ID);
      
      AddUserTypes(types);
      return types;
    }
  }
}

#endregion


#region Assets/Photon/Quantum/Runtime/Dispatcher/QuantumCallbackHandler_DebugDraw.cs

namespace Quantum {
  using System;
  using System.Collections.Generic;
  using UnityEngine;

  /// <summary>
  /// A handler object that registers to Quantum callbacks and draws debug shapes issued from the simulation.
  /// </summary>
  // ReSharper disable once InconsistentNaming
  public static class QuantumCallbackHandler_DebugDraw {
    /// <summary>
    /// Init and subscribe to Quantum callbacks.
    /// </summary>
    /// <returns>An object to dispose to unsubscribe from the callbacks</returns>
    public static IDisposable Initialize() {
      var disposables = new CompositeDisposable();
      
      try {
        disposables.Add(QuantumCallback.SubscribeManual((CallbackGameStarted _) => {
          DebugDraw.Clear();
        }));
        disposables.Add(QuantumCallback.SubscribeManual((CallbackGameDestroyed _) => {
          DebugDraw.Clear();
        }));
        disposables.Add(QuantumCallback.SubscribeManual((CallbackSimulateFinished _) => {
          DebugDraw.TakeAll();
        }));
      } catch {
        // if something goes wrong clean up subscriptions
        disposables.Dispose();
        throw;
      }

      return disposables;
    }

    private class CompositeDisposable : IDisposable {
      private List<IDisposable> _disposables = new List<IDisposable>();

      public void Add(IDisposable disposable) {
        _disposables.Add(disposable);
      }

      public void Dispose() {
        foreach (var disposable in _disposables) {
          try { disposable.Dispose(); } catch (Exception ex) { Debug.LogException(ex); }
        }
      }
    }
  }
}

#endregion


#region Assets/Photon/Quantum/Runtime/Dispatcher/QuantumCallbackHandler_FrameDiffer.cs

namespace Quantum {
  using System;
  using UnityEngine;

  /// <summary>
  /// A handler object to subscribe to Quantum callbacks to open the frame dump differ in builds
  /// after receiving a checksum error frame dumps.
  /// </summary>
  // ReSharper disable once InconsistentNaming
  public static class QuantumCallbackHandler_FrameDiffer {
    /// <summary>
    /// Init and subscribe to Quantum callbacks.
    /// </summary>
    /// <returns></returns>
    public static IDisposable Initialize() {
      if (Application.isEditor)
        return null;

      return QuantumCallback.SubscribeManual((CallbackChecksumErrorFrameDump c) => {
        var gameRunner = QuantumRunner.FindRunner(c.Game);
        if (gameRunner == null) {
          Debug.LogError("Could not find runner for game");
          return;
        }

        var differ    = QuantumFrameDiffer.Show();
        var actorName = QuantumFrameDiffer.TryGetPhotonNickname(gameRunner.NetworkClient, c.ActorId);
        differ.State.AddEntry(gameRunner.Id, c.ActorId, c.FrameNumber, c.FrameDump, actorName);
      });
    }
  }
}

#endregion


#region Assets/Photon/Quantum/Runtime/Dispatcher/QuantumCallbackHandler_GameResult.cs

namespace Quantum {
  using System;

  /// <summary>
  /// A handler object that subscribes to Quantum callbacks to send game results to the server.
  /// </summary>
  // ReSharper disable once InconsistentNaming
  public static class QuantumCallbackHandler_GameResult {
    /// <summary>
    /// Init and subscribe to Quantum callbacks.
    /// </summary>
    /// <returns>A disposable object to unsubscribe from callbacks again.</returns>
    public static IDisposable Initialize() {
      return QuantumEvent.SubscribeManual((EventGameResult e) => {
        if (e.Game?.Session == null) {
          return;
        }

        var bytes = e.Game.AssetSerializer.ResultToByteArray(e.GameResult, true);
        e.Game.Session.SendGameResult(bytes);
      });
    }
  }
}

#endregion


#region Assets/Photon/Quantum/Runtime/Dispatcher/QuantumCallbackHandler_LegacyQuantumCallback.cs

namespace Quantum {
  using System;
  using System.Collections.Generic;
  using System.Linq;
  using UnityEngine;

  /// <summary>
  /// A handler object that subscribes to Quantum callbacks to call legacy QuantumCallbacks in on Unity game objects.
  /// </summary>
  // ReSharper disable once InconsistentNaming
  public static class QuantumCallbackHandler_LegacyQuantumCallback {
    /// <summary>
    /// Init and subscribe to Quantum callbacks.
    /// </summary>
    /// <returns>An object to dispose to unsubscribe from the callbacks</returns>
    public static IDisposable Initialize() {
      var disposable = new CompositeDisposable();

      try {
#pragma warning disable CS0618 // Type or member is obsolete
        disposable.Add(QuantumCallback.SubscribeManual((CallbackChecksumError c) => {
          for (Int32 i = QuantumCallbacks.Instances.Count - 1; i >= 0; --i) {
            try {
              QuantumCallbacks.Instances[i].OnChecksumError(c.Game, c.Error, c.Frames);
            } catch (Exception exn) {
              Log.Exception(exn);
            }
          }
        }));

        disposable.Add(QuantumCallback.SubscribeManual((CallbackGameDestroyed c) => {
          var instancesCopy = QuantumCallbacks.Instances.ToList();
          for (Int32 i = instancesCopy.Count - 1; i >= 0; --i) {
            try {
              instancesCopy[i].OnGameDestroyed(c.Game);
            } catch (Exception exn) {
              Log.Exception(exn);
            }
          }
        }));

        disposable.Add(QuantumCallback.SubscribeManual((CallbackGameInit c) => {
          for (Int32 i = QuantumCallbacks.Instances.Count - 1; i >= 0; --i) {
            try {
              QuantumCallbacks.Instances[i].OnGameInit(c.Game, c.IsResync);
            } catch (Exception exn) {
              Log.Exception(exn);
            }
          }
        }));

        disposable.Add(QuantumCallback.SubscribeManual((CallbackGameStarted c) => {
          for (Int32 i = QuantumCallbacks.Instances.Count - 1; i >= 0; --i) {
            try {
              QuantumCallbacks.Instances[i].OnGameStart(c.Game);
              QuantumCallbacks.Instances[i].OnGameStart(c.Game, c.IsResync);
            } catch (Exception exn) {
              Log.Exception(exn);
            }
          }
        }));

        disposable.Add(QuantumCallback.SubscribeManual((CallbackGameResynced c) => {
          for (Int32 i = QuantumCallbacks.Instances.Count - 1; i >= 0; --i) {
            try {
              QuantumCallbacks.Instances[i].OnGameResync(c.Game);
            } catch (Exception exn) {
              Log.Exception(exn);
            }
          }
        }));

        disposable.Add(QuantumCallback.SubscribeManual((CallbackSimulateFinished c) => {
          for (Int32 i = QuantumCallbacks.Instances.Count - 1; i >= 0; --i) {
            try {
              QuantumCallbacks.Instances[i].OnSimulateFinished(c.Game, c.Frame);
            } catch (Exception exn) {
              Log.Exception(exn);
            }
          }
        }));

        disposable.Add(QuantumCallback.SubscribeManual((CallbackUpdateView c) => {
          for (Int32 i = QuantumCallbacks.Instances.Count - 1; i >= 0; --i) {
            try {
              QuantumCallbacks.Instances[i].OnUpdateView(c.Game);
            } catch (Exception exn) {
              Log.Exception(exn);
            }
          }
        }));

        disposable.Add(QuantumCallback.SubscribeManual((CallbackUnitySceneLoadBegin c) => {
          for (Int32 i = QuantumCallbacks.Instances.Count - 1; i >= 0; --i) {
            try {
              QuantumCallbacks.Instances[i].OnUnitySceneLoadBegin(c.Game);
            } catch (Exception exn) {
              Log.Exception(exn);
            }
          }
        }));

        disposable.Add(QuantumCallback.SubscribeManual((CallbackUnitySceneLoadDone c) => {
          for (Int32 i = QuantumCallbacks.Instances.Count - 1; i >= 0; --i) {
            try {
              QuantumCallbacks.Instances[i].OnUnitySceneLoadDone(c.Game);
            } catch (Exception exn) {
              Log.Exception(exn);
            }
          }
        }));

        disposable.Add(QuantumCallback.SubscribeManual((CallbackUnitySceneUnloadBegin c) => {
          for (Int32 i = QuantumCallbacks.Instances.Count - 1; i >= 0; --i) {
            try {
              QuantumCallbacks.Instances[i].OnUnitySceneUnloadBegin(c.Game);
            } catch (Exception exn) {
              Log.Exception(exn);
            }
          }
        }));

        disposable.Add(QuantumCallback.SubscribeManual((CallbackUnitySceneUnloadDone c) => {
          for (Int32 i = QuantumCallbacks.Instances.Count - 1; i >= 0; --i) {
            try {
              QuantumCallbacks.Instances[i].OnUnitySceneUnloadDone(c.Game);
            } catch (Exception exn) {
              Log.Exception(exn);
            }
          }
        }));
#pragma warning restore CS0618 // Type or member is obsolete
      } catch {
        // if something goes wrong clean up subscriptions
        disposable.Dispose();
        throw;
      }

      return disposable;
    }

    private class CompositeDisposable : IDisposable {
      private List<IDisposable> _disposables = new List<IDisposable>();

      public void Add(IDisposable disposable) {
        _disposables.Add(disposable);
      }

      public void Dispose() {
        foreach (var disposable in _disposables) {
          try { disposable.Dispose(); } catch (Exception ex) { Debug.LogException(ex); }
        }
      }
    }
  }
}

#endregion


#region Assets/Photon/Quantum/Runtime/Dispatcher/QuantumCallbackHandler_StartRecording.cs

namespace Quantum {
  using System;
  using System.Collections.Generic;
  using UnityEngine;

  /// <summary>
  /// A handler object that subscribes to Quantum callbacks to start recording input and checksums.
  /// </summary>
  // ReSharper disable once InconsistentNaming
  public class QuantumCallbackHandler_StartRecording {
    /// <summary>
    /// Init and subscribe to Quantum callbacks.
    /// </summary>
    /// <returns>An object to dispose to unsubscribe from the callbacks</returns>
    public static IDisposable Initialize() {
      var disposables = new CompositeDisposable();

      try {
        disposables.Add(QuantumCallback.SubscribeManual((CallbackGameStarted c) => {
          var runner = QuantumRunner.FindRunner(c.Game);
          Debug.Assert(runner);
          Assert.Check(runner.Session.IsPaused == false);

          if (c.IsResync) {
            if (runner.RecordingFlags.HasFlag(RecordingFlags.Input)) {
              // on a resync, start recording from the next frame on
              c.Game.StartRecordingInput(c.Game.Frames.Verified.Number + 1);
            }
          } else {
            if (runner.RecordingFlags.HasFlag(RecordingFlags.Input)) {
              c.Game.StartRecordingInput();
            }

            if (runner.RecordingFlags.HasFlag(RecordingFlags.Checksums)) {
              c.Game.StartRecordingChecksums();
            }
          }
        }));
      } catch {
        // if something goes wrong clean up subscriptions
        disposables.Dispose();
        throw;
      }

      return disposables;
    }

    private class CompositeDisposable : IDisposable {
      private List<IDisposable> _disposables = new List<IDisposable>();

      public void Add(IDisposable disposable) {
        _disposables.Add(disposable);
      }

      public void Dispose() {
        foreach (var disposable in _disposables) {
          try { disposable.Dispose(); } catch (Exception ex) { Debug.LogException(ex); }
        }
      }
    }
  }
}

#endregion


#region Assets/Photon/Quantum/Runtime/Dispatcher/QuantumCallbackHandler_UnityCallbacks.cs

//#define QUANTUM_UNITY_CALLBACKS_VERBOSE_LOG

namespace Quantum {
  using System;
  using System.Collections;
  using System.Diagnostics;
  using UnityEngine;
  using UnityEngine.SceneManagement;
  using Debug = UnityEngine.Debug;

  /// <summary>
  /// A handler object that subscribes to Quantum callbacks to handle Unity scene loading and unloading.
  /// </summary>
  // ReSharper disable once InconsistentNaming
  public class QuantumCallbackHandler_UnityCallbacks : IDisposable {
    private Coroutine _coroutine;
    private Map       _currentMap;
    private bool      _currentSceneNeedsCleanup;

    private readonly CallbackUnitySceneLoadBegin   _callbackUnitySceneLoadBegin;
    private readonly CallbackUnitySceneLoadDone    _callbackUnitySceneLoadDone;
    private readonly CallbackUnitySceneUnloadBegin _callbackUnitySceneUnloadBegin;
    private readonly CallbackUnitySceneUnloadDone  _callbackUnitySceneUnloadDone;

    /// <summary>
    /// Creates a new instance of the QuantumCallbackHandler_UnityCallbacks class.
    /// </summary>
    /// <param name="game">Referenced game</param>
    public QuantumCallbackHandler_UnityCallbacks(QuantumGame game) {
      _callbackUnitySceneLoadBegin   = new CallbackUnitySceneLoadBegin(game);
      _callbackUnitySceneLoadDone    = new CallbackUnitySceneLoadDone(game);
      _callbackUnitySceneUnloadBegin = new CallbackUnitySceneUnloadBegin(game);
      _callbackUnitySceneUnloadDone  = new CallbackUnitySceneUnloadDone(game);
    }

    /// <summary>
    /// Init and subscribe to Quantum callbacks.
    /// </summary>
    /// <returns>An object to dispose to unsubscribe from the callbacks</returns>
    public static IDisposable Initialize() {
      return QuantumCallback.SubscribeManual((CallbackGameStarted c) => {
        var runner = QuantumRunner.FindRunner(c.Game);
        if (runner != QuantumRunner.Default) {
          // only work for the default runner
          return;
        }

        var callbacksHost = new QuantumCallbackHandler_UnityCallbacks(c.Game);

        //callbacksHost._currentMap = runner.Game.Frames?.Verified?.Map;

        // TODO: this has a bug: disposing parent sub doesn't cancel following subscriptions
        QuantumCallback.Subscribe(runner.UnityObject, (CallbackGameDestroyed _) => callbacksHost.Dispose(), runner: runner);
        QuantumCallback.Subscribe(runner.UnityObject, (CallbackUpdateView cc) => callbacksHost.UpdateLoading(cc.Game), runner: runner);
      });
    }

    /// <summary>
    /// Dispose the object, unsubscribe from Quantum callbacks.
    /// Will log a warning if a map loading or unloading is still in progress.
    /// Will start a coroutine to unload the current scene if it was not unloaded yet.
    /// </summary>
    public void Dispose() {
      QuantumCallback.UnsubscribeListener(this);

      if (_coroutine != null) {
        Log.Warn("Map loading or unloading was still in progress when destroying the game");
      }

      if (_currentMap != null && _currentSceneNeedsCleanup) {
        _coroutine  = QuantumMapLoader.Instance?.StartCoroutine(UnloadScene(_currentMap.Scene));
        _currentMap = null;
      }
    }

    private static void PublishCallback<T>(T callback, string sceneName) where T : CallbackBase, ICallbackUnityScene {
      VerboseLog($"Publishing callback {typeof(T)} with {sceneName}");
      callback.SceneName = sceneName;
      QuantumCallback.Dispatcher.Publish(callback);
    }

    private IEnumerator SwitchScene(string previousSceneName, string newSceneName, bool unloadFirst) {
      if (string.IsNullOrEmpty(previousSceneName)) {
        throw new ArgumentException(nameof(previousSceneName));
      }

      if (string.IsNullOrEmpty(newSceneName)) {
        throw new ArgumentException(nameof(newSceneName));
      }

      VerboseLog($"Switching scenes from {previousSceneName} to {newSceneName} (unloadFirst: {unloadFirst})");

      try {
        LoadSceneMode loadSceneMode = LoadSceneMode.Additive;

        if (unloadFirst) {
          if (SceneManager.sceneCount == 1) {
            Debug.Assert(SceneManager.GetActiveScene().name == previousSceneName);
            VerboseLog($"Need to create a temporary scene, because {previousSceneName} is the only scene loaded.");

            SceneManager.CreateScene("QuantumTemporaryEmptyScene");
            loadSceneMode = LoadSceneMode.Single;
          }

          PublishCallback(_callbackUnitySceneUnloadBegin, previousSceneName);
          yield return SceneManager.UnloadSceneAsync(previousSceneName);
          PublishCallback(_callbackUnitySceneUnloadDone, previousSceneName);
        }

        PublishCallback(_callbackUnitySceneLoadBegin, newSceneName);
        yield return SceneManager.LoadSceneAsync(newSceneName, loadSceneMode);
        var newScene = SceneManager.GetSceneByName(newSceneName);
        if (newScene.IsValid()) {
          SceneManager.SetActiveScene(newScene);
        }

        PublishCallback(_callbackUnitySceneLoadDone, newSceneName);

        if (!unloadFirst) {
          PublishCallback(_callbackUnitySceneUnloadBegin, previousSceneName);
          yield return SceneManager.UnloadSceneAsync(previousSceneName);
          PublishCallback(_callbackUnitySceneUnloadDone, previousSceneName);
        }
      } finally {
        _coroutine = null;
      }
    }

    private IEnumerator LoadScene(string sceneName) {
      try {
        if (string.IsNullOrEmpty(sceneName)) {
          yield break;
        }
        PublishCallback(_callbackUnitySceneLoadBegin, sceneName);
        yield return SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
        SceneManager.SetActiveScene(SceneManager.GetSceneByName(sceneName));
        PublishCallback(_callbackUnitySceneLoadDone, sceneName);
      } finally {
        _coroutine = null;
      }
    }

    private IEnumerator UnloadScene(string sceneName) {
      try {
        if (string.IsNullOrEmpty(sceneName)) {
          yield break;
        }
        PublishCallback(_callbackUnitySceneUnloadBegin, sceneName);
        yield return SceneManager.UnloadSceneAsync(sceneName);
        PublishCallback(_callbackUnitySceneUnloadDone, sceneName);
      } finally {
        _coroutine = null;
      }
    }

    private void UpdateLoading(QuantumGame game) {
      var loadMode = game.Configurations.Simulation.AutoLoadSceneFromMap;
      if (loadMode == SimulationConfig.AutoLoadSceneFromMapMode.Disabled) {
        return;
      }

      if (_coroutine != null) {
        return;
      }

      var map = game.Frames.Verified.Map;
      if (map == _currentMap) {
        return;
      }

      bool isNewSceneLoaded = SceneManager.GetSceneByName(map.Scene).IsValid();
      if (isNewSceneLoaded) {
        VerboseLog($"Scene {map.Scene} appears to have been loaded externally.");
        _currentMap               = map;
        _currentSceneNeedsCleanup = false;
        return;
      }

      var coroHost = QuantumMapLoader.Instance;
      Debug.Assert(coroHost != null);

      string previousScene = _currentMap?.Scene ?? string.Empty;
      string newScene      = map.Scene;

      _currentMap               = map;
      _currentSceneNeedsCleanup = true;

      if (SceneManager.GetSceneByName(previousScene).IsValid()) {
        VerboseLog($"Previous scene \"{previousScene}\" was loaded, starting transition with mode {loadMode}");
        if (loadMode == SimulationConfig.AutoLoadSceneFromMapMode.LoadThenUnloadPreviousScene) {
          _coroutine  = coroHost.StartCoroutine(SwitchScene(previousScene, newScene, unloadFirst: false));
          _currentMap = map;
        } else if (loadMode == SimulationConfig.AutoLoadSceneFromMapMode.UnloadPreviousSceneThenLoad) {
          _coroutine  = coroHost.StartCoroutine(SwitchScene(previousScene, newScene, unloadFirst: true));
          _currentMap = map;
        } else {
          // legacy mode
          _coroutine  = coroHost.StartCoroutine(UnloadScene(previousScene));
          _currentMap = null;
        }
      } else {
        // simply load the scene async
        VerboseLog($"Previous scene \"{previousScene}\" was not loaded.");
        _coroutine  = coroHost.StartCoroutine(LoadScene(newScene));
        _currentMap = map;
      }
    }

    [Conditional("QUANTUM_UNITY_CALLBACKS_VERBOSE_LOG")]
    private static void VerboseLog(string msg) {
      Debug.LogFormat("QuantumUnityCallbacks: {0}", msg);
    }
  }
}

#endregion


#region Assets/Photon/Quantum/Runtime/Dispatcher/QuantumCallbacks.Unity.cs

namespace Quantum {
  /// <summary>
  /// IDs for Unity-specific callbacks.
  /// </summary>
  public enum UnityCallbackId {
    /// <summary>
    /// Scene load begins.
    /// </summary>
    UnitySceneLoadBegin = CallbackId.UserCallbackIdStart,
    /// <summary>
    /// Scene load is done.
    /// </summary>
    UnitySceneLoadDone,
    /// <summary>
    /// Scene unload begins.
    /// </summary>
    UnitySceneUnloadBegin,
    /// <summary>
    /// Scene unload is done.
    /// </summary>
    UnitySceneUnloadDone,
    /// <summary>
    /// Callback ID start for user callbacks.
    /// </summary>
    UserCallbackIdStart,
  }

  /// <summary>
  /// An interface for callbacks that are related to Unity scenes.
  /// </summary>
  public interface ICallbackUnityScene {
    /// <summary>
    /// Name of the scene.
    /// </summary>
    string SceneName { get; set; }
  }

  /// <summary>
  /// Callback sent when a Unity scene load begins.
  /// To enable this feature <see cref="SimulationConfig.AutoLoadSceneFromMap"/> must be toggled on.
  /// </summary>
  public class CallbackUnitySceneLoadBegin : QuantumGame.CallbackBase, ICallbackUnityScene {
    /// <summary>
    /// ID of the callback.
    /// </summary>
    public new const int ID = (int)UnityCallbackId.UnitySceneLoadBegin;
    /// <summary>
    /// Initializes a new instance.
    /// </summary>
    /// <param name="game"></param>
    public CallbackUnitySceneLoadBegin(QuantumGame game) : base(ID, game) { }
    /// <inheritdoc cref="ICallbackUnityScene.SceneName"/>
    public string SceneName { get; set; }
  }

  /// <summary>
  /// Callback sent when a Unity scene load is done.
  /// </summary>
  public class CallbackUnitySceneLoadDone : QuantumGame.CallbackBase, ICallbackUnityScene {
    /// <summary>
    /// ID of the callback.
    /// </summary>
    public new const int ID = (int)UnityCallbackId.UnitySceneLoadDone;
    /// <summary>
    /// Initializes a new instance.
    /// </summary>
    /// <param name="game"></param>
    public CallbackUnitySceneLoadDone(QuantumGame game) : base(ID, game) { }
    /// <inheritdoc cref="ICallbackUnityScene.SceneName"/>
    public string SceneName { get; set; }
  }

  /// <summary>
  /// Callback sent when a Unity scene unload begins.
  /// </summary>
  public class CallbackUnitySceneUnloadBegin : QuantumGame.CallbackBase, ICallbackUnityScene {
    /// <summary>
    /// ID of the callback.
    /// </summary>
    public new const int ID = (int)UnityCallbackId.UnitySceneUnloadBegin;
    /// <summary>
    /// Initializes a new instance.
    /// </summary>
    /// <param name="game"></param>
    public CallbackUnitySceneUnloadBegin(QuantumGame game) : base(ID, game) { }
    /// <inheritdoc cref="ICallbackUnityScene.SceneName"/>
    public string SceneName { get; set; }
  }

  /// <summary>
  /// Callback sent when a Unity scene unload is done.
  /// </summary>
  public class CallbackUnitySceneUnloadDone : QuantumGame.CallbackBase, ICallbackUnityScene {
    /// <summary>
    /// ID of the callback.
    /// </summary>
    public new const int ID = (int)UnityCallbackId.UnitySceneUnloadDone;
    /// <summary>
    /// Initializes a new instance.
    /// </summary>
    /// <param name="game"></param>
    public CallbackUnitySceneUnloadDone(QuantumGame game) : base(ID, game) { }
    /// <inheritdoc cref="ICallbackUnityScene.SceneName"/>
    public string SceneName { get; set; }
  }
}

#endregion


#region Assets/Photon/Quantum/Runtime/Dispatcher/QuantumEvent.cs

namespace Quantum {
  using System;

  /// <summary>
  /// Events are a fire-and-forget mechanism to transfer information from the simulation to the view.
  /// Use this class to subscribe and unsubscribe from Quantum events.
  /// <para>Events are mostly custom and code-generated by the Quantum DSL.</para>
  /// <para>Events do not synchronize anything between clients and they are fired by each client's own simulation.</para>
  /// <para>Since the same Frame can be simulated more than once (prediction, rollback), it is possible to have events being triggered multiple times. 
  /// To avoid undesired duplicated Events Quantum identifies duplicates using a hash code function over the Event data members, the Event id and the tick.</para>
  /// <para>Regular, non-synced, Events will be either cancelled or confirmed once the predicted Frame from which they were fired has been verified.</para>
  /// <para>Events are dispatched after all Frames have been simulated right after the OnUpdateView callback. Events are called in the same order they were invoked with 
  /// the exception of non-synced Events which can be skipped when identified as duplicated. Due to this timing, the targeted QuantumEntityView may already have been destroyed.</para>
  /// </summary>
  public abstract class QuantumEvent : QuantumUnityStaticDispatcherAdapter<QuantumUnityEventDispatcher, EventBase> {
    private QuantumEvent() {
      throw new NotSupportedException();
    }
  }

  /// <summary>
  /// <see cref="Quantum.EventDispatcher"/> implementation for Unity.
  /// </summary>
  public class QuantumUnityEventDispatcher : EventDispatcher, IQuantumUnityDispatcher {
    /// <inheritdoc cref="IQuantumUnityDispatcherExtensions.GetUnityListenerStatus"/>
    protected override ListenerStatus GetListenerStatus(object listener, uint flags) {
      return this.GetUnityListenerStatus(listener, flags);
    }
  }
}

#endregion


#region Assets/Photon/Quantum/Runtime/Dispatcher/QuantumUnityStaticDispatcherAdapter.cs

namespace Quantum {
  using System;
  using Photon.Analyzer;
  using Photon.Deterministic;
  using UnityEngine;
  using Object = UnityEngine.Object;
  
  internal sealed class QuantumUnityStaticDispatcherAdapterWorker : QuantumMonoBehaviour {
    public DispatcherBase Dispatcher;

    private void LateUpdate() {
      if (Dispatcher == null) {
        // this may happen when scripts get reloaded in editor
        Destroy(gameObject);
      } else {
        Dispatcher.RemoveDeadListeners();
      }
    }
  }

  /// <summary>
  /// Adapter for static dispatchers in Unity. Provides utility static methods, internal worker that removes dead listeners and means for creating a dispatcher.
  /// </summary>
  /// <typeparam name="TDispatcher"></typeparam>
  /// <typeparam name="TDispatchableBase"></typeparam>
  public abstract class QuantumUnityStaticDispatcherAdapter<TDispatcher, TDispatchableBase>
    where TDispatcher : DispatcherBase, IQuantumUnityDispatcher, new()
    where TDispatchableBase : IDispatchable {
    
    [StaticField]
    // ReSharper disable once StaticMemberInGenericType
    private static QuantumUnityStaticDispatcherAdapterWorker _worker;

    /// <summary>
    /// The dispatcher instance.
    /// </summary>
    [field: StaticField]
    public static TDispatcher Dispatcher { get; } = new TDispatcher();

    /// <summary>
    /// Removes all listeners and destroys the worker object.
    /// </summary>
    [StaticFieldResetMethod]
    public static void Clear() {
      Dispatcher.Clear();
      if (_worker) {
        Object.Destroy(_worker.gameObject);
        _worker = null;
      }
    }

    /// <summary>
    /// Removes dead listeners from the dispatcher.
    /// </summary>
    public static void RemoveDeadListeners() {
      Dispatcher.RemoveDeadListeners();
    }


    /// <summary>
    /// Creates a subscription. The subscription lifetime is tied to the listener object, unless explicitly unsubscribed.
    /// </summary>
    /// <typeparam name="TDispatchable"></typeparam>
    /// <param name="listener">An object listening. Used to unsubscribe all subscriptions when the object is destroyed.</param>
    /// <param name="handler">Actual event handler.</param>
    /// <param name="once">Call <paramref name="handler"/> only once.</param>
    /// <param name="filter">Optional event filter. If returns false, handler will not be invoked.</param>
    /// <param name="onlyIfActiveAndEnabled">Only invoke handler if the listener is active and enabled</param>
    /// <param name="onlyIfEntityViewBound">Only invoke handler if the listener <see cref="QuantumEntityView"/> component and it is bound to an entity</param>
    /// <returns>Subscription that can be stored and used in <see cref="Unsubscribe"/></returns>
    public static DispatcherSubscription Subscribe<TDispatchable>(Object listener, DispatchableHandler<TDispatchable> handler, DispatchableFilter filter = null,
      bool once = false, bool onlyIfActiveAndEnabled = false, bool onlyIfEntityViewBound = false)
      where TDispatchable : TDispatchableBase {
      if (onlyIfEntityViewBound) {
        QuantumEntityView view;
        if (listener is Component comp) {
          view = comp.GetComponentInParent<QuantumEntityView>();
        } else if (listener is GameObject go) {
          view = go.GetComponentInParent<QuantumEntityView>();
        } else {
          throw new ArgumentException($"To use {nameof(onlyIfEntityViewBound)} parameter, {nameof(listener)} needs to be a Component or a GameObject", nameof(listener));
        }

        if (view == null) {
          throw new ArgumentException($"Unable to find {nameof(EntityView)} component in {listener} or any of its parents", nameof(listener));
        }

        filter = ComposeFilters((_) => view.EntityRef.IsValid, filter);
      }

      EnsureWorkerExistsAndIsActive();
      return Dispatcher.Subscribe(listener, handler, once, onlyIfActiveAndEnabled, filter: filter);
    }
    
    /// <summary>
    /// Creates a subscription. The subscription lifetime is tied to the listener object, unless explicitly unsubscribed.
    /// </summary>
    /// <typeparam name="TDispatchable"></typeparam>
    /// <param name="listener">An object listening. Used to unsubscribe all subscriptions when the object is destroyed.</param>
    /// <param name="handler">Actual event handler.</param>
    /// <param name="once">Call <paramref name="handler"/> only once.</param>
    /// <param name="gameMode">Only invoke for a specific game mode</param>
    /// <param name="exclude">If true, the handler will be invoked for all game modes except the specified one</param>
    /// <param name="onlyIfActiveAndEnabled">Only invoke handler if the listener is active and enabled</param>
    /// <param name="onlyIfEntityViewBound">Only invoke handler if the listener <see cref="QuantumEntityView"/> component and it is bound to an entity</param>
    /// <returns>Subscription that can be stored and used in <see cref="Unsubscribe"/></returns>
    public static DispatcherSubscription Subscribe<TDispatchable>(Object listener, DispatchableHandler<TDispatchable> handler, DeterministicGameMode gameMode, bool exclude = false,
      bool once = false, bool onlyIfActiveAndEnabled = false, bool onlyIfEntityViewBound = false)
      where TDispatchable : TDispatchableBase {
      return Subscribe(listener, handler, (game) => (game.Session.GameMode == gameMode) ^ exclude, once, onlyIfActiveAndEnabled, onlyIfEntityViewBound);
    }

    /// <summary>
    /// Creates a subscription. The subscription lifetime is tied to the listener object, unless explicitly unsubscribed.
    /// </summary>
    /// <typeparam name="TDispatchable"></typeparam>
    /// <param name="listener">An object listening. Used to unsubscribe all subscriptions when the object is destroyed.</param>
    /// <param name="handler">Actual event handler.</param>
    /// <param name="once">Call <paramref name="handler"/> only once.</param>
    /// <param name="gameModes">Only invoke for specific game modes</param>
    /// <param name="exclude">If true, the handler will be invoked for all game modes except the specified ones</param>
    /// <param name="onlyIfActiveAndEnabled">Only invoke handler if the listener is active and enabled</param>
    /// <param name="onlyIfEntityViewBound">Only invoke handler if the listener <see cref="QuantumEntityView"/> component and it is bound to an entity</param>
    /// <returns>Subscription that can be stored and used in <see cref="Unsubscribe"/></returns>
    public static DispatcherSubscription Subscribe<TDispatchable>(Object listener, DispatchableHandler<TDispatchable> handler, DeterministicGameMode[] gameModes, bool exclude = false,
      bool once = false, bool onlyIfActiveAndEnabled = false, bool onlyIfEntityViewBound = false)
      where TDispatchable : TDispatchableBase {
      return Subscribe(listener, handler, (game) => (Array.IndexOf(gameModes, game.Session.GameMode) >= 0) ^ exclude, once, onlyIfActiveAndEnabled, onlyIfEntityViewBound);
    }
    
    /// <summary>
    /// Creates a subscription. The subscription lifetime is tied to the listener object, unless explicitly unsubscribed.
    /// </summary>
    /// <typeparam name="TDispatchable"></typeparam>
    /// <param name="listener">An object listening. Used to unsubscribe all subscriptions when the object is destroyed.</param>
    /// <param name="handler">Actual event handler.</param>
    /// <param name="once">Call <paramref name="handler"/> only once.</param>
    /// <param name="runnerId">Only invoke for a QuantumRunner with a specific ID</param>
    /// <param name="onlyIfActiveAndEnabled">Only invoke handler if the listener is active and enabled</param>
    /// <param name="onlyIfEntityViewBound">Only invoke handler if the listener <see cref="QuantumEntityView"/> component and it is bound to an entity</param>
    /// <returns>Subscription that can be stored and used in <see cref="Unsubscribe"/></returns>
    public static DispatcherSubscription Subscribe<TDispatchable>(Object listener, DispatchableHandler<TDispatchable> handler, string runnerId,
      bool once = false, bool onlyIfActiveAndEnabled = false, bool onlyIfEntityViewBound = false)
      where TDispatchable : TDispatchableBase {
      return Subscribe(listener, handler, (game) => QuantumRunnerRegistry.Global.FindRunner(game)?.Id == runnerId, once, onlyIfActiveAndEnabled, onlyIfEntityViewBound);
    }

    /// <summary>
    /// Creates a subscription. The subscription lifetime is tied to the listener object, unless explicitly unsubscribed.
    /// </summary>
    /// <typeparam name="TDispatchable"></typeparam>
    /// <param name="listener">An object listening. Used to unsubscribe all subscriptions when the object is destroyed.</param>
    /// <param name="handler">Actual event handler.</param>
    /// <param name="once">Call <paramref name="handler"/> only once.</param>
    /// <param name="runner">Only invoke for a QuantumRunner instance</param>
    /// <param name="onlyIfActiveAndEnabled">Only invoke handler if the listener is active and enabled</param>
    /// <param name="onlyIfEntityViewBound">Only invoke handler if the listener <see cref="QuantumEntityView"/> component and it is bound to an entity</param>
    /// <returns>Subscription that can be stored and used in <see cref="Unsubscribe"/></returns>
    public static DispatcherSubscription Subscribe<TDispatchable>(Object listener, DispatchableHandler<TDispatchable> handler, QuantumRunner runner,
      bool once = false, bool onlyIfActiveAndEnabled = false, bool onlyIfEntityViewBound = false)
      where TDispatchable : TDispatchableBase {
      var runnerId = runner.Id;
      return Subscribe(listener, handler, (game) => QuantumRunnerRegistry.Global.FindRunner(game)?.Id == runnerId, once, onlyIfActiveAndEnabled, onlyIfEntityViewBound);
    }

    /// <summary>
    /// Creates a subscription. The subscription lifetime is tied to the listener object, unless explicitly unsubscribed.
    /// </summary>
    /// <typeparam name="TDispatchable"></typeparam>
    /// <param name="listener">An object listening. Used to unsubscribe all subscriptions when the object is destroyed.</param>
    /// <param name="handler">Actual event handler.</param>
    /// <param name="once">Call <paramref name="handler"/> only once.</param>
    /// <param name="game">Only invoke for a QuantumGame instance</param>
    /// <param name="onlyIfActiveAndEnabled">Only invoke handler if the listener is active and enabled</param>
    /// <param name="onlyIfEntityViewBound">Only invoke handler if the listener <see cref="QuantumEntityView"/> component and it is bound to an entity</param>
    /// <returns>Subscription that can be stored and used in <see cref="Unsubscribe"/></returns>
    public static DispatcherSubscription Subscribe<TDispatchable>(Object listener, DispatchableHandler<TDispatchable> handler, QuantumGame game,
      bool once = false, bool onlyIfActiveAndEnabled = false, bool onlyIfEntityViewBound = false)
      where TDispatchable : TDispatchableBase {
      return Subscribe(listener, handler, g => g == game, once, onlyIfActiveAndEnabled, onlyIfEntityViewBound);
    }

    /// <inheritdoc cref="DispatcherBase.SubscribeManual{TDispatchable}(object,Quantum.DispatchableHandler{TDispatchable},bool,Quantum.DispatchableFilter)"/>
    public static IDisposable SubscribeManual<TDispatchable>(object listener, DispatchableHandler<TDispatchable> handler, DispatchableFilter filter = null, bool once = false)
      where TDispatchable : TDispatchableBase {
      return Dispatcher.SubscribeManual(listener, handler, once, filter);
    }

    /// <inheritdoc cref="DispatcherBase.SubscribeManual{TDispatchable}(Quantum.DispatchableHandler{TDispatchable},bool,Quantum.DispatchableFilter)"/>
    public static IDisposable SubscribeManual<TDispatchable>(DispatchableHandler<TDispatchable> handler, DispatchableFilter filter = null, bool once = false)
      where TDispatchable : TDispatchableBase {
      return Dispatcher.SubscribeManual(handler, once, filter);
    }
    
    /// <inheritdoc cref="DispatcherBase.SubscribeManual{TDispatchable}(Quantum.DispatchableHandler{TDispatchable},bool,Quantum.DispatchableFilter)"/>
    public static IDisposable SubscribeManual<TDispatchable>(DispatchableHandler<TDispatchable> handler, IDeterministicGame game, bool once = false)
      where TDispatchable : TDispatchableBase {
      return SubscribeManual(handler, g => g == game, once);
    }

    /// <inheritdoc cref="DispatcherBase.Unsubscribe"/>
    public static bool Unsubscribe(DispatcherSubscription subscription) {
      return Dispatcher.Unsubscribe(subscription);
    }

    /// <inheritdoc cref="DispatcherBase.Unsubscribe"/>
    public static bool Unsubscribe(ref DispatcherSubscription subscription) {
      var result = Dispatcher.Unsubscribe(subscription);
      subscription = default;
      return result;
    }
    
    /// <inheritdoc cref="DispatcherBase.UnsubscribeListener"/>
    public static bool UnsubscribeListener(object listener) {
      return Dispatcher.UnsubscribeListener(listener);
    }

    /// <inheritdoc cref="DispatcherBase.UnsubscribeListener{TDispatchable}"/>
    public static bool UnsubscribeListener<TDispatchable>(object listener) where TDispatchable : TDispatchableBase {
      return Dispatcher.UnsubscribeListener<TDispatchable>(listener);
    }
    
    public static bool IsSubscribed<TDispatchable>(object listener) where TDispatchable : TDispatchableBase {
      return Dispatcher.IsListenerSubscribed<TDispatchable>(listener);
    }
    
    private static void EnsureWorkerExistsAndIsActive() {
      if (_worker) {
        if (!_worker.isActiveAndEnabled)
          throw new InvalidOperationException($"{typeof(QuantumUnityStaticDispatcherAdapterWorker)} is disabled");

        return;
      }

      if (!Application.isPlaying) {
        return;
      }

      var go = new GameObject(typeof(TDispatcher).Name + nameof(QuantumUnityStaticDispatcherAdapterWorker), typeof(QuantumUnityStaticDispatcherAdapterWorker));
      go.hideFlags = HideFlags.HideAndDontSave;
      Object.DontDestroyOnLoad(go);

      _worker = go.GetComponent<QuantumUnityStaticDispatcherAdapterWorker>();
      if (!_worker)
        throw new InvalidOperationException($"Unable to create {typeof(QuantumUnityStaticDispatcherAdapterWorker)}");

      _worker.Dispatcher = Dispatcher;
    }

    private static DispatchableFilter ComposeFilters(DispatchableFilter first, DispatchableFilter second) {
      if (first == null && second == null) {
        throw new ArgumentException($"{nameof(first)} and {nameof(second)} can't both be null");
      } else if (first == null) {
        return second;
      } else if (second == null) {
        return first;
      } else {
        return x => first(x) && second(x);
      }
    }
  }
}

#endregion


#region Assets/Photon/Quantum/Runtime/EditorAttributes/MultiTypeReferenceAttribute.cs

namespace Quantum {
  using System;
  using UnityEngine;

  /// <summary>
  /// Adds one object picker per type to the inspector.
  /// </summary>
  [AttributeUsage(AttributeTargets.Field)]
  public class MultiTypeReferenceAttribute : PropertyAttribute {
    /// <summary>
    /// Initializes a new instance.
    /// </summary>
    /// <param name="types">Types of objects this field supports.</param>
    public MultiTypeReferenceAttribute(params Type[] types) {
      Types = types;
    }

    /// <summary>
    /// Types of objects this field supports.
    /// </summary>
    public readonly Type[] Types;
  }
}

#endregion


#region Assets/Photon/Quantum/Runtime/Entity/IQuantumEntityViewPool.cs

namespace Quantum {
  using UnityEngine;

  /// <summary>
  /// Interface to create custom implementation of the entity view pool that can be assigned to the <see cref="QuantumEntityViewUpdater.Pool"/>.
  /// </summary>
  public interface IQuantumEntityViewPool {
    /// <summary>
    /// Returns how many items are inside the pool in total.
    /// </summary>
    int PooledCount { get; }
    /// <summary>
    /// Returns how many pooled items are currently in use.
    /// </summary>
    int BorrowedCount { get; }

    /// <summary>
    /// Create a pooled game object and return the component of chose type.
    /// </summary>
    /// <typeparam name="T">Component type</typeparam>
    /// <param name="prefab">Prefab to instantiate</param>
    /// <param name="activate">Call SetActive() on the game object</param>
    /// <param name="createIfEmpty">Create a new entity if there is no suitable one found in the pool</param>
    /// <returns>Component on the created prefab instance, can be null</returns>
    T Create<T>(T prefab, bool activate = true, bool createIfEmpty = true) where T : Component;

    /// <summary>
    /// Create a pooled game object.
    /// </summary>
    /// <param name="prefab">Prefab to instantiate</param>
    /// <param name="activate">Call SetActive() on the game object</param>
    /// <param name="createIfEmpty">Create a new entity if there is no suitable one found in the pool</param>
    /// <returns>An instance of the prefab</returns>
    GameObject Create(GameObject prefab, bool activate = true, bool createIfEmpty = true);

    /// <summary>
    /// Create a pooled game object and return the component of chose type.
    /// </summary>
    /// <typeparam name="T">Component type</typeparam>
    /// <param name="prefab">Prefab to instantiate</param>
    /// <param name="parent">Calls SetParent(parent) on the new game object transform when set</param>
    /// <param name="activate">Call SetActive() on the game object</param>
    /// <param name="createIfEmpty">Create a new entity if there is no suitable one found in the pool</param>
    /// <returns>Component on the created prefab instance, can be null</returns>
    T Create<T>(T prefab, Transform parent, bool activate = true, bool createIfEmpty = true) where T : Component;

    /// <summary>
    /// Create a pooled game object.
    /// </summary>
    /// <param name="prefab">Prefab to instantiate</param>
    /// <param name="parent">Calls SetParent(parent) on the new game object transform when set</param>
    /// <param name="activate">Call SetActive() on the game object</param>
    /// <param name="createIfEmpty">Create a new entity if there is no suitable one found in the pool</param>
    /// <returns>An instance of the prefab</returns>
    GameObject Create(GameObject prefab, Transform parent, bool activate = true, bool createIfEmpty = true);

    /// <summary>
    /// Destroy or return the pooled game object that the component is attached to.
    /// </summary>
    /// <param name="component">Component that belongs to the pooled game object.</param>
    /// <param name="deactivate">Call SetActive(false) on the pooled game object before returning it to the pool</param>
    void Destroy(Component component, bool deactivate = true);

    /// <summary>
    /// Destroy or return the pooled game object.
    /// </summary>
    /// <param name="instance">Poole game object</param>
    /// <param name="deactivate">Call SetActive(false) on the pooled game object before returning it to the pool</param>
    void Destroy(GameObject instance, bool deactivate = true);

    /// <summary>
    /// Destroy or return the pooled game object after a delay.
    /// </summary>
    /// <param name="instance">Poole game object</param>
    /// <param name="delay">Delay in seconds to complete returning it to the pool</param>
    void Destroy(GameObject instance, float delay);

    /// <summary>
    /// Create prefab instances and fill the pool.
    /// </summary>
    /// <param name="prefab">Prefab to created pooled instances</param>
    /// <param name="desiredCount">The number of instances to create and add to the pool</param>
    void Prepare(GameObject prefab, int desiredCount);
  }
}


#endregion


#region Assets/Photon/Quantum/Runtime/Entity/IQuantumViewComponent.cs

namespace Quantum {
  using System;
  using System.Collections.Generic;

  /// <summary>
  /// The interface that the <see cref="QuantumEntityViewUpdater"/> uses to control the view components (<see cref="QuantumViewComponent{T}"/>).
  /// </summary>
  public interface IQuantumViewComponent {
    /// <summary>
    /// Is called when the entity view is enabled for the first time.
    /// </summary>
    /// <param name="contexts">All view contexts</param>
    void Initialize(Dictionary<Type, IQuantumViewContext> contexts);
    /// <summary>
    /// Is called when the entity view is activated after being created or reused from the pool.
    /// </summary>
    /// <param name="frame">Frame</param>
    /// <param name="game">Quantum game</param>
    /// <param name="entityView">Associated entity view</param>
    void Activate(Frame frame, QuantumGame game, QuantumEntityView entityView);
    /// <summary>
    /// Is called when the entity view is destroyed or returned to the pool.
    /// </summary>
    void Deactivate();
    /// <summary>
    /// Is called when the entity view is updated from the Unity update loop.
    /// </summary>
    void UpdateView();
    /// <summary>
    /// Is call on all entity views Unity late update.
    /// </summary>
    void LateUpdateView();
    /// <summary>
    /// Is called when the game has changed in the <see cref="QuantumEntityViewUpdater"/>.
    /// </summary>
    /// <param name="game"></param>
    void GameChanged(QuantumGame game);
    /// <summary>
    /// Is toggled during <see cref="Activate"/> and <see cref="Deactivate"/>."/>
    /// </summary>
    bool IsActive { get; }
    /// <summary>
    /// Returns <see cref="IsActive"/>
    /// </summary>
    bool IsActiveAndEnabled { get; }
    /// <summary>
    /// The initialized state has to kept track of by the view component to not call it multiple times.
    /// </summary>
    bool IsInitialized { get; }
  }
}



#endregion


#region Assets/Photon/Quantum/Runtime/Entity/IQuantumViewContext.cs

namespace Quantum {
  /// <summary>
  /// Use this interface to create view context classes that can be used inside concrete <see cref="QuantumEntityViewComponent{T}"/>.
  /// </summary>
  public interface IQuantumViewContext {
  }
}

#endregion


#region Assets/Photon/Quantum/Runtime/Entity/QuantumEntityPrototypeColliderLayerSource.cs

namespace Quantum {
  /// <summary>
  /// Defines the source of the physics collider layer information.
  /// </summary>
  public enum QuantumEntityPrototypeColliderLayerSource {
    /// <summary>
    /// The layer information is retrieved from the Source Collider's GameObject (if one is provided)
    /// or this Prototype's GameObject (otherwise).
    /// </summary>
    GameObject = 0,

    /// <summary>
    /// The layer is defined explicitly from a layer enumeration.
    /// </summary>
    Explicit = 1,
  }
}

#endregion


#region Assets/Photon/Quantum/Runtime/Entity/QuantumEntityPrototypeConverter.cs

namespace Quantum {
  using System;
  using UnityEngine;

  public unsafe partial class QuantumEntityPrototypeConverter {
    public readonly QuantumEntityPrototype[] OrderedMapPrototypes;
    public readonly QuantumEntityPrototype   AssetPrototype;
    public readonly QuantumMapData           Map;

    public QuantumEntityPrototypeConverter(QuantumMapData map, QuantumEntityPrototype[] orderedMapPrototypes) {
      Map = map;
      OrderedMapPrototypes = orderedMapPrototypes;
      InitUser();
    }

    public QuantumEntityPrototypeConverter(QuantumEntityPrototype prototypeAsset) {
      AssetPrototype = prototypeAsset;
      InitUser();
    }

    partial void InitUser();

    public void Convert<T>(T source, out T dest) {
      dest = source;
    }

    public void Convert<T>(IQuantumPrototypeConvertible<T> source, out T dest) {
      if (source == null) {
        dest = default;
      } else {
        dest = (T)source.Convert(this);
      }
    }

    public void Convert<T>(IQuantumPrototypeConvertible<T>[] source, out T[] dest) {
      if (source == null) {
        dest = Array.Empty<T>();
      } else {
        dest = new T[source.Length];
        for (int i = 0; i < source.Length; ++i) {
          if (source[i] == null) {
            dest[i] = default;
          } else {
            dest[i] = source[i].Convert(this);
          }
        }
      }
    }

    public void Convert(QuantumEntityPrototype prototype, out MapEntityId result) {
      if (AssetPrototype != null) {
        result = AssetPrototype == prototype ? MapEntityId.Create(0) : MapEntityId.Invalid;
      } else {
        var index = Array.IndexOf(OrderedMapPrototypes, prototype);
        result = index >= 0 ? MapEntityId.Create(index) : MapEntityId.Invalid;
      }
    }
    
    public void Convert(QuantumUnityComponentPrototype prototype, out MapEntityId result) {
      if (AssetPrototype != null && prototype != null) {
        result = AssetPrototype == prototype.GetComponent<QuantumEntityPrototype>() ? MapEntityId.Create(0) : MapEntityId.Invalid;
      } else {
        var index = Array.IndexOf(OrderedMapPrototypes, prototype);
        result = index >= 0 ? MapEntityId.Create(index) : MapEntityId.Invalid;
      }
    }

    public void Convert(QUnityEntityPrototypeRef unityEntityPrototype, out EntityPrototypeRef result) {
      var sceneReference = unityEntityPrototype.ScenePrototype;
      if (sceneReference != null && sceneReference.gameObject.scene.IsValid()) {
        Debug.Assert(Map != null);
        Debug.Assert(Map.gameObject.scene == sceneReference.gameObject.scene);

        var index = Array.IndexOf(OrderedMapPrototypes, sceneReference);
        if (index >= 0) {
          result = EntityPrototypeRef.FromMasterAsset(Map.Asset, index);
        } else {
          result = EntityPrototypeRef.Invalid;
        }
      } else if (unityEntityPrototype.AssetPrototype.Id.IsValid) {
        result = EntityPrototypeRef.FromPrototypeAsset(unityEntityPrototype.AssetPrototype);
      } else {
        result = default;
      }
    }
    
    public void Convert<T>(QUnityComponentPrototypeRef<T> prototype, out ComponentPrototypeRef result) where T : QuantumUnityComponentPrototype {
      if (prototype == null) {
        result = default;
        return;
      }

      var entityPrototypeRefPrototype = new QUnityEntityPrototypeRef() {
        AssetPrototype = prototype.AssetPrototype,
      };

      if (prototype.ScenePrototype) {
        entityPrototypeRefPrototype.ScenePrototype = prototype.ScenePrototype.GetComponent<QuantumEntityPrototype>();
      }

      Convert(entityPrototypeRefPrototype, out EntityPrototypeRef entityPrototypeRef);

      if (entityPrototypeRef.IsValid) {
        result = ComponentPrototypeRef.FromEntityPrototypeRefAndType(entityPrototypeRef, prototype.AssetComponentType);
      } else {
        result = default;
      }
    }
    
    public void Convert<T>(QUnityComponentPrototypeRef<T>[] source, out ComponentPrototypeRef[] result) where T : QuantumUnityComponentPrototype {
      result = new ComponentPrototypeRef[source.Length];
      for (int i = 0; i < source.Length; ++i) {
        Convert(source[i], out result[i]);
      }
    }
  }
}

#endregion


#region Assets/Photon/Quantum/Runtime/Entity/QuantumEntityPrototypeTransformMode.cs

namespace Quantum {
  /// <summary>
  /// Defines what kind of transform component a <see cref="QuantumEntityPrototype"/> has.
  /// </summary>
  public enum QuantumEntityPrototypeTransformMode {
    /// <summary>
    /// <see cref="Quantum.Transform2D"/>
    /// </summary>
    Transform2D = 0,
    /// <summary>
    /// <see cref="Quantum.Transform3D"/>
    /// </summary>
    Transform3D = 1,
    /// <summary>
    /// No transform component.
    /// </summary>
    None = 2,
  }
}

#endregion


#region Assets/Photon/Quantum/Runtime/Entity/QuantumEntityViewBindBehaviour.cs

namespace Quantum {
  /// <summary>
  /// The view bind behaviour controls when the view is created. For entities on the predicted or entities on the verified frame. 
  /// Because the verified frame is confirmed by the server this bind behaviour will show local entity views delayed.
  /// When using non-verified it may happen that they get destroyed when the frame is finally confirmed by the server.
  /// </summary>
  public enum QuantumEntityViewBindBehaviour {
    /// <summary>
    /// The entity view is created during a predicted frame.
    /// </summary>
    NonVerified,
    /// <summary>
    /// The entity view is created during a verified frame.
    /// </summary>
    Verified
  }
}

#endregion


#region Assets/Photon/Quantum/Runtime/Entity/QuantumEntityViewComponent.cs

namespace Quantum {
  /// <summary>
  /// The base class to inherit entity view components from.
  /// Entity view components can be used to add features to entity views and gain simple access to all relevant Quantum game API and the Quantum entity.
  /// </summary>
  /// <typeparam name="T">The type of the custom view context used by this view component. Can be `IQuantumEntityViewContext` if not required.</typeparam>
  public abstract class QuantumEntityViewComponent<T> : QuantumViewComponent<T> where T : IQuantumViewContext {
    /// <summary>
    /// The Game that the entity belongs to. This can change after the OnGameChanged() callback.
    /// Set before calling OnActivate(Frame).
    /// </summary>
    public override QuantumGame Game => _entityView?.Game;
    /// <summary>
    /// The Quantum EntityRef that the underlying entity view is attached to.
    /// </summary>
    public EntityRef EntityRef => _entityView.EntityRef;
    /// <summary>
    /// A reference to the parent class to access interesting game and entity data.
    /// </summary>
    public QuantumEntityView EntityView => _entityView;

    /// <summary>
    /// Checks the predicted frame if this Quantum entity has a particular Quantum entity.
    /// </summary>
    /// <typeparam name="TComponent">Quantum component type</typeparam>
    /// <returns>True, if the entity has the component</returns>
    public bool HasPredictedQuantumComponent<TComponent>() where TComponent : unmanaged, IComponent => PredictedFrame == null || EntityView == null ? false : PredictedFrame.Has<TComponent>(EntityRef);
    /// <summary>
    /// Checks the verified frame if this Quantum entity has a particular Quantum entity.
    /// </summary>
    /// <typeparam name="TComponent">Quantum component type</typeparam>
    /// <returns>True, if the entity has the component</returns>
    public bool HasVerifiedQuantumComponent<TComponent>() where TComponent : unmanaged, IComponent => VerifiedFrame == null || EntityView == null ? false : VerifiedFrame.Has<TComponent>(EntityRef);

    /// <summary>
    /// Returns the desired Quantum component from the entity of the the predicted frame.
    /// This method throws exceptions when the Frame or Entity ref are not assigned, as well as when the Quantum entity does not have the component.
    /// <see cref="Core.FrameBase.Get{T}(EntityRef)"/>
    /// </summary>
    /// <typeparam name="TComponent">Quantum component type</typeparam>
    /// <returns>The Quantum component</returns>
    public TComponent GetPredictedQuantumComponent<TComponent>() where TComponent : unmanaged, IComponent => PredictedFrame.Get<TComponent>(EntityRef);

    /// <summary>
    /// Returns the desired Quantum component from the entity of the the verified frame.
    /// This method throws exceptions when the Frame or Entity ref are not assigned, as well as when the Quantum entity does not have the component.
    /// <see cref="Core.FrameBase.Get{T}(EntityRef)"/>
    /// </summary>
    /// <typeparam name="TComponent">Quantum component type</typeparam>
    /// <returns>The Quantum component</returns>
    public TComponent GetVerifiedQuantumComponent<TComponent>() where TComponent : unmanaged, IComponent => VerifiedFrame.Get<TComponent>(EntityRef);

    /// <summary>
    /// Try to get the component from this Quantum entity and the predicted frame.
    /// <see cref="Core.FrameBase.TryGet{T}(EntityRef, out T)"/>
    /// </summary>
    /// <typeparam name="TComponent">Desired component type</typeparam>
    /// <param name="value">The resulting Quantum component instance.</param>
    /// <returns>True when the component was found.</returns>
    public bool TryGetPredictedQuantumComponent<TComponent>(out TComponent value) where TComponent : unmanaged, IComponent {
      if (PredictedFrame == null || EntityView == null) {
        value = default;
        return false;
      }
      return PredictedFrame.TryGet(EntityRef, out value);
    }

    /// <summary>
    /// Try to get the component from this Quantum entity and the verified frame.
    /// <see cref="Core.FrameBase.TryGet{T}(EntityRef, out T)"/>
    /// </summary>
    /// <typeparam name="TComponent">Desired component type</typeparam>
    /// <param name="value">The resulting Quantum component instance.</param>
    /// <returns>True when the component was found.</returns>
    public bool TryGetVerifiedQuantumComponent<TComponent>(out TComponent value) where TComponent : unmanaged, IComponent {
      if (VerifiedFrame == null || EntityView == null) {
        value = default;
        return false;
      }
      return VerifiedFrame.TryGet(EntityRef, out value);
    }
  }

  /// <summary>
  /// A entity view component without context type.
  /// </summary>
  public abstract class QuantumEntityViewComponent : QuantumEntityViewComponent<IQuantumViewContext> { 
  }
}

#endregion


#region Assets/Photon/Quantum/Runtime/Entity/QuantumEntityViewFlags.cs

namespace Quantum {
  using System;

  /// <summary>
  /// Additional configuration of the entity view that enables of disabled parts of the updating process.
  /// Either for performance reasons or when taking over control.
  /// </summary>
  [Flags]
  public enum QuantumEntityViewFlags {
    /// <summary>
    /// <see cref="QuantumEntityView.UpdateView(bool, bool)"/> and <see cref="QuantumEntityView.LateUpdateView()"/> are not processed and forwarded to entity view components.
    /// </summary>
    DisableUpdateView = 1 << 0,
    /// <summary>
    /// Will completely disable updating the entity view positions.
    /// </summary>
    DisableUpdatePosition = 1 << 1,
    /// <summary>
    /// Use cached transforms to improve the performance by not calling Transform properties.
    /// </summary>
    UseCachedTransform = 1 << 2,
    /// <summary>
    /// The entity game object will be named to resemble the EntityRef, set this flag to prevent naming.
    /// </summary>
    DisableEntityRefNaming = 1 << 3,
    /// <summary>
    /// Disable searching the entity view game object children for entity view components.
    /// </summary>
    DisableSearchChildrenForEntityViewComponents = 1 << 4,
    /// <summary>
    /// Inits a transform buffer, so updating with verified frames only can be switched on to guarantee smooth visuals. When in use, visuals are presented with latency proportional to ping.
    /// Turning this on only prepares the buffers and callbacks. Switching the interpolation mode is controlled with a separate toggle on the QuantumEntityView.
    /// </summary>
    EnableSnapshotInterpolation = 1 << 5,
  }
  
  /// <summary>
  /// Used when grabbing transform data for view interpolation.
  /// </summary>
  public enum QuantumEntityViewTimeReference {
    /// <summary>
    /// Either Predicted frame or closest frame data from verified (when using snapshot interpolation).
    /// </summary>
    To, 
    /// <summary>
    /// Either PredictedPrevious frame or farthest frame data from verified (when using snapshot interpolation).
    /// </summary>
    From,
    /// <summary>
    /// Previous update corrected frame (to compute total mis-prediction).
    /// </summary>
    ErrorCorrection,
  }

  /// <summary>
  /// Interpolation mode for the view.
  /// </summary>
  public enum QuantumEntityViewInterpolationMode {
    /// <summary>
    /// Default mode interpolated between PredictedPrevious and Predicted, also using mis-prediction error smoothing.
    /// </summary>
    Prediction, 
    /// <summary>
    /// Dynamically interpolates between two past verified frames. Timing is computed by EntityViewUpdater.
    /// Views using this mode are always seen "in the past", but are smooth and accurate, given mis-predictions are not possible.
    /// REQUIRES: QuantumEntityViewFlags must include EnableSnapshotInterpolation
    /// </summary>
    SnapshotInterpolation, 
    /// <summary>
    /// Dynamically switches between Prediction and SnapshotInterpolation based on culled status of the Entity.
    /// REQUIRES: QuantumEntityViewFlags must include EnableSnapshotInterpolation
    /// </summary>
    Auto
  }
}


#endregion


#region Assets/Photon/Quantum/Runtime/Entity/QuantumSceneViewComponent.cs

namespace Quantum {
  using static QuantumUnityExtensions;

  /// <summary>
  /// The SceneViewComponent is able to attach itself to the <see cref="QuantumEntityViewUpdater"/> and received updates from it.
  /// <para>Set <see cref="Updater"/> explicitly, or set <see cref="UseFindUpdater"/> or make this script a child of <see cref="QuantumEntityViewUpdater"/>.</para>
  /// </summary>
  /// <typeparam name="T"></typeparam>
  public abstract class QuantumSceneViewComponent<T> : QuantumViewComponent<T> where T : IQuantumViewContext {
    /// <summary>
    /// Will attach this view component to this EntityViewUpdater so it receives update callbacks from there.
    /// This field will not be set unless set explicitly or <see cref="UseFindUpdater"/> is true.
    /// </summary>
    [InlineHelp]
    public QuantumEntityViewUpdater Updater;
    /// <summary>
    /// Uses UnityEngine.Object.FindObjectOfType/FindObjectByType to find the <see cref="Updater"/>. This is very slow and not recommended.
    /// </summary>
    [InlineHelp]
    public bool UseFindUpdater;

    /// <summary>
    /// Unity OnEnabled, will try to attach this script to the <see cref="Updater"/>.
    /// </summary>
    public virtual void OnEnable() {
      if (Updater == null && UseFindUpdater) {
        Updater = FindFirstObjectByType<QuantumEntityViewUpdater>();
      }

      Updater?.AddViewComponent(this);
    }

    /// <summary>
    /// Unity OnDisabled, will try to detach the script from the <see cref="Updater"/>.
    /// </summary>
    public virtual void OnDisable() {
       Updater?.RemoveViewComponent(this);
    }
  }

  /// <summary>
  /// A Quantum scene view component without context.
  /// The SceneViewComponent is able to attach itself to the <see cref="QuantumEntityViewUpdater"/> and received updates from it.
  /// </summary>
  public abstract class QuantumSceneViewComponent : QuantumSceneViewComponent<IQuantumViewContext> {
  }
}

#endregion


#region Assets/Photon/Quantum/Runtime/Entity/QuantumSnapshotInterpolationTimer.cs

namespace Quantum {
  using System;
  using UnityEngine;

  /// <summary>
  /// Configuration of the snapshot interpolation mode when selected in <see cref="QuantumEntityView.InterpolationMode"/>.
  /// </summary>
  [Serializable]
  public class QuantumSnapshotInterpolationTimer {
    
    [Range(1.0f, 5.0f)]
    public float TimeDilationPercentage = 2;

    [Range(2.0f, 10.0f)]
    public float SnapshotReferenceLatencyTicks = 4;

    [Range(0.25f, 1.0f)]
    public float ElasticWindowTicks = 0.5f;

    [HideInInspector]
    public int CurrentFrom;

    [HideInInspector]
    public float Alpha = 0;

    int _verified;
    float _accumulatedDelta;
    float _diff = 0;
    float multiplier = 1;

    /// <summary>
    /// Advance the interpolation timer.
    /// </summary>
    /// <param name="verified">The verified frame number</param>
    /// <param name="fixedDelta">The fixed delta time</param>
    public void Advance(int verified, float fixedDelta) {
      _verified = verified;
      _accumulatedDelta += Time.deltaTime * multiplier;
      while (_accumulatedDelta > fixedDelta) {
        CurrentFrom++;
        _accumulatedDelta -= fixedDelta;
      }

      Alpha = _accumulatedDelta / fixedDelta;

      _diff = _verified - CurrentFrom + Alpha;

      if (_diff > SnapshotReferenceLatencyTicks + 3 || _diff <= 1) {
        CurrentFrom = _verified - (int)SnapshotReferenceLatencyTicks;
        _accumulatedDelta = 0;
        return;
      }

      if (_diff >= SnapshotReferenceLatencyTicks + ElasticWindowTicks) multiplier = (100f + TimeDilationPercentage) / 100;
      else if (_diff <= SnapshotReferenceLatencyTicks - ElasticWindowTicks) multiplier = (100f - TimeDilationPercentage) / 100;
      else multiplier = 1;
    }

    /// <summary>
    /// Data structure to hold transform data on an entity in a buffer.
    /// </summary>
    public struct QuantumTransformData {
      /// <summary>
      /// The transform 2d component.
      /// </summary>
      public Transform2D Transform2D;
      /// <summary>
      /// The transform 2d vertical component.
      /// </summary>
      public Transform2DVertical Transform2DVertical;
      /// <summary>
      /// The transform 3d component.
      /// </summary>
      public Transform3D Transform3D;
      /// <summary>
      /// Has 2d vertical component.
      /// </summary>
      public bool Has2DVertical;
      /// <summary>
      /// Has valid and useable data.
      /// </summary>
      public bool IsValid;
    }

    /// <summary>
    /// Simple ring buffer to store transform data.
    /// </summary>
    /// <typeparam name="T">Type of data to store</typeparam>
    public class InterpolationBuffer<T> {
      private T[] _buffer;
      private int _cursor;
      private int _tick;
      private int _size;
      private int _initialTick = 0;

      /// <summary>
      /// Create the collection with a given capacity.
      /// </summary>
      /// <param name="size">Capacity</param>
      public InterpolationBuffer(int size) {
        _buffer = new T[size];
        _size = size;
      }

      /// <summary>
      /// Reset the collection.
      /// </summary>
      public void Reset() {
        _tick = 0;
        _cursor = 0;
        _initialTick = 0;
        for (int i = 0; i < _size; i++) {
          _buffer[i] = default;
        }
      }

      /// <summary>
      /// Add a new item to the collection.
      /// </summary>
      /// <param name="t">The object</param>
      /// <param name="tick">The frame number</param>
      public void Add(T t, int tick) {
        _buffer[_cursor] = t;
        _cursor = (_cursor + 1) % _size;
        _tick = tick;
        if (_initialTick == 0) {
          _initialTick = tick;
        }
      }

      /// <summary>
      /// Try get a value from the collection for the given frame number.
      /// </summary>
      /// <param name="t">Resulting object</param>
      /// <param name="tick">Requested frame number</param>
      /// <returns>True if an object with that frame number was found</returns>
      public bool TryGet(out T t, int tick) {
        t = default;
        var diff = _tick - tick;
        if (diff >= _size || tick < _initialTick) {
          return false;
        }

        var index = (_cursor + _size - 1 - diff) % _size;
        t = _buffer[index];
        return true;
      }
    }
  }
}

#endregion


#region Assets/Photon/Quantum/Runtime/Entity/QuantumViewComponent.cs

namespace Quantum {
  using System;
  using System.Collections.Generic;
  using Profiling;
  using UnityEngine;

  /// <summary>
  /// The base class to inherit entity view components from.
  /// Entity view components can be used to add features to entity views and gain simple access to all relevant Quantum game API and the Quantum entity.
  /// The view component is not updated (OnUpdateView, OnLateUpdateView) if the behaviour has been disabled.
  /// </summary>
  /// <typeparam name="T">The type of the custom view context used by this view component. Can be `IQuantumEntityViewContext` if not required.</typeparam>
  public abstract class QuantumViewComponent<T> : QuantumMonoBehaviour, IQuantumViewComponent where T : IQuantumViewContext {
    /// <summary>
    /// The Game that the entity belongs to. This can change after the <see cref="OnGameChanged()"/> callback.
    /// Set before calling <see cref="OnActivate(Frame)"/>.
    /// </summary>
    public virtual QuantumGame Game => _game;
    /// <summary>
    /// The newest predicted frame.
    /// Set before calling <see cref="OnActivate(Frame)"/>.
    /// </summary>
    public Frame PredictedFrame => Game?.Frames.Predicted;
    /// <summary>
    /// The newest verified frame.
    /// Set before calling <see cref="OnActivate(Frame)"/>.
    /// </summary>
    public Frame VerifiedFrame => Game?.Frames.Verified;
    /// <summary>
    /// The newest predicted previous frame.
    /// Set before calling <see cref="OnActivate(Frame)"/>.
    /// </summary>
    public Frame PredictedPreviousFrame => Game?.Frames.PredictedPrevious;
    /// <summary>
    /// The view context of the <see cref="QuantumEntityViewUpdater"/> associated with this entity view component.
    /// </summary>
    public T ViewContext { get; private set; }
    /// <summary>
    /// Is the view component currently activated and not inside the pool.
    /// </summary>
    public bool IsActive { get; private set; }
    /// <summary>
    /// Returns <see langword="true"/> if the view component is <see cref="IsActive"/>, 
    /// not null and <see cref="Behaviour.enabled"/> and the gameObject not null and <see cref="GameObject.activeInHierarchy"/>.
    /// </summary>
    public bool IsActiveAndEnabled => IsActive && this != null && enabled && gameObject != null && gameObject.activeInHierarchy;
    /// <summary>
    /// Returns <see langword="true"/> if the view component has been initialized.
    /// </summary>
    public bool IsInitialized { get; private set; }

    /// <summary>
    /// Cached game object is updated during <see cref="Activate"/> and <see cref="GameChanged"/>.
    /// </summary>
    protected QuantumGame _game;
    /// <summary>
    /// Cached entity view is updated during <see cref="Activate"/>.
    /// </summary>
    protected QuantumEntityView _entityView;

    /// <summary>
    /// Is called when the entity view is enabled for the first time.
    /// The <see cref="ViewContext"/> is already set if available.
    /// Access to <see cref="Game"/>, <see cref="VerifiedFrame"/>, <see cref="PredictedFrame"/> and <see cref="PredictedPreviousFrame"/> is not available yet.
    /// </summary>
    public virtual void OnInitialize() { }
    /// <summary>
    /// Is called when the entity view is activated and the entity was created.
    /// </summary>
    /// <param name="frame">The frame that the entity was created with, can be predicted or verified base on the <see cref="QuantumEntityViewBindBehaviour"></see></param>.
    public virtual void OnActivate(Frame frame) { }
    /// <summary>
    /// Is called when the view component is deactivated.
    /// </summary>
    public virtual void OnDeactivate() { }
    /// <summary>
    /// Is called from the <see cref="QuantumEntityViewUpdater"/> on a Unity update.
    /// </summary>
    public virtual void OnUpdateView() { }
    /// <summary>
    /// Is called from the <see cref="QuantumEntityViewUpdater"/> on a Unity late update.
    /// </summary>
    public virtual void OnLateUpdateView() { }
    /// <summary>
    /// Is called from the <see cref="QuantumEntityViewUpdater"/> then the observed game is changed.
    /// </summary>
    public virtual void OnGameChanged() { }

    /// <summary>
    /// Is only called internally.
    /// Sets the view context of this entity view component.
    /// </summary>
    /// <param name="contexts">All of the different contexts of the EntityViewUpdater, will select the matching type.</param>
    public void Initialize(Dictionary<Type, IQuantumViewContext> contexts) {
      if (contexts.TryGetValue(typeof(T), out var viewContext)) {
        ViewContext = (T)viewContext;
      } else if (typeof(T) != typeof(IQuantumViewContext)) {
        Debug.LogError($"Cannot find context type {typeof(T)} when initializing the entity view component {name}", this);
      }

      OnInitialize();
      IsInitialized = true;
    }

    /// <summary>
    /// Is only called internally.
    /// Sets the entity view parent.
    /// </summary>
    /// <param name="frame"></param>
    /// <param name="game"></param>
    /// <param name="entityView"></param>
    public void Activate(Frame frame, QuantumGame game, QuantumEntityView entityView) {
      _game = game;
      _entityView = entityView;
      IsActive = true;
      OnActivate(frame);
    }

    /// <summary>
    /// Is only called internally.
    /// </summary>
    public void Deactivate() {
      OnDeactivate();
      IsActive = false;
    }

    /// <summary>
    /// Is only called internally.
    /// </summary>
    public void UpdateView() {
      using var profilerScope = HostProfiler.Start("QuantumViewComponent.UpdateView");
      OnUpdateView();
    }

    /// <summary>
    /// Is only called internally.
    /// </summary>
    public void LateUpdateView() {
      using var profilerScope = HostProfiler.Start("QuantumViewComponent.OnLateUpdateView");
      OnLateUpdateView();
    }

    /// <summary>
    /// Is only called internally.
    /// </summary>
    public void GameChanged(QuantumGame game) {
      _game = game;
      OnGameChanged();
    }
  }
}

#endregion


#region Assets/Photon/Quantum/Runtime/Entity/QUnityComponentPrototypeRef.cs

namespace Quantum {
  using System;
  using UnityEngine;
  using UnityEngine.Serialization;

  /// <summary>
  /// Obsolete, still defined to prevent data loss.
  /// </summary>
  [Serializable]
  public class QUnityComponentPrototypeRef : QUnityComponentPrototypeRef<QuantumUnityComponentPrototype> {
  }

  /// <summary>
  /// Obsolete, still defined to prevent data loss.
  /// </summary>
  [Serializable]
  public class QUnityComponentPrototypeRef<T> : ISerializationCallbackReceiver where T : QuantumUnityComponentPrototype {

    /// <summary>
    /// Asset prototype.
    /// </summary>
    public AssetRef<Quantum.EntityPrototype> AssetPrototype;
    /// <summary>
    /// Asset component type.
    /// </summary>
    public ComponentTypeRef AssetComponentType;

    /// <summary>
    /// Scene prototype.
    /// </summary>
    [LocalReference]
    [FormerlySerializedAs("_scenePrototype")]
    public T ScenePrototype;

    [Obsolete]
    [SerializeField]
    private string _componentTypeName = default;

#pragma warning disable CS0612
    void ISerializationCallbackReceiver.OnBeforeSerialize() {
    }

    void ISerializationCallbackReceiver.OnAfterDeserialize() {
      if (AssetPrototype != default) {
        // one at a time
        ScenePrototype = default;
      }

      if (!string.IsNullOrEmpty(_componentTypeName)) {
        AssetComponentType = ComponentTypeRef.FromTypeName(_componentTypeName);
        _componentTypeName = null;
      }

      if (ScenePrototype != null) {
        AssetComponentType = default;
      }
    }
#pragma warning restore CS0612
  }
}

#endregion


#region Assets/Photon/Quantum/Runtime/Entity/QUnityEntityPrototypeRef.cs

namespace Quantum {
  using System;

  [Serializable]
  public struct QUnityEntityPrototypeRef {
    [LocalReference]
    public QuantumEntityPrototype ScenePrototype;
    public Quantum.AssetRef<Quantum.EntityPrototype> AssetPrototype;
  }
}

#endregion


#region Assets/Photon/Quantum/Runtime/Gizmos/Config/GizmoIconColorAttribute.cs

namespace Quantum {
  using System;

  /// <summary>
  /// Defines the color of a gizmo header in the overlay.
  /// </summary>
  public class GizmoIconColorAttribute : Attribute {
    /// <summary>
    /// The color of the gizmo header.
    /// </summary>
    public ScriptHeaderBackColor Color { get; }
    
    /// <summary>
    /// Create a new instance of the <see cref="GizmoIconColorAttribute"/> class.
    /// </summary>
    /// <param name="color"></param>
    public GizmoIconColorAttribute(ScriptHeaderBackColor color) {
      Color = color;
    }
  }
}

#endregion


#region Assets/Photon/Quantum/Runtime/Gizmos/Config/QuantumGameGizmosSettings.cs

namespace Quantum {
  using System;
  using UnityEngine;

  /// <summary>
  /// The gizmo settings for the Quantum game.
  /// </summary>
  [Serializable]
  public partial class QuantumGameGizmosSettings {
    /// <summary>
    /// The Unity overlay UI id used for the Quantum gizmos.
    /// </summary>
    public const string ID = "QuantumGizmoSettings";

    /// <summary>
    /// Global scale for all gizmos.
    /// </summary>
    [Header("Global Settings"), Range(1, 5)]
    public float IconScale = 1;

    /// <summary>
    /// How bright the gizmos are when selected.
    /// </summary>
    [Range(1.1f, 2)] public float SelectedBrightness = 1.1f;

    [Header("Debug Draw"), GizmoIconColor(ScriptHeaderBackColor.Green)]
    public QuantumGizmoEntry DebugDraw = new QuantumGizmoEntry(Color.white) { Enabled = true };

    /// <summary>
    /// Draw the prediction area. Only available at runtime.
    /// </summary>
    [Header("Prediction Runtime Gizmos"), GizmoIconColor(ScriptHeaderBackColor.Red)]
    public QuantumGizmoEntry PredictionArea = new QuantumGizmoEntry(QuantumGizmoColors.TransparentRed) { Enabled = true, DisableFill = true, OnlyDrawSelected = false };

    /// <summary>
    /// Draw the CharacterController3D and CharacterController2D components.
    /// </summary>
    [Header("Physics Gizmos"), GizmoIconColor(ScriptHeaderBackColor.Orange)]
    public QuantumGizmoEntry CharacterController = new PhysicsComponentGizmoEntry(QuantumGizmoColors.TransparentYellow);

    /// <summary>
    /// Draw the colliders that are currently static.
    /// </summary>
    public QuantumGizmoEntry StaticColliders = new PhysicsComponentGizmoEntry(QuantumGizmoColors.TransparentSkyBlue);

    /// <summary>
    /// Draw the colliders that are currently dynamic.
    /// </summary>
    public QuantumGizmoEntry DynamicColliders = new PhysicsComponentGizmoEntry(QuantumGizmoColors.TransparentLimeGreen);

    /// <summary>
    /// Draw the colliders that are currently kinematic.
    /// </summary>
    public QuantumGizmoEntry KinematicColliders = new PhysicsComponentGizmoEntry(QuantumGizmoColors.TransparentWhite);

    /// <summary>
    /// Draw the colliders that are asleep.
    /// </summary>
    public QuantumGizmoEntry AsleepColliders = new PhysicsComponentGizmoEntry(QuantumGizmoColors.TransparentLightPurple);

    /// <summary>
    /// Draw the colliders that are disabled.
    /// </summary>
    public QuantumGizmoEntry DisabledColliders = new PhysicsComponentGizmoEntry(QuantumGizmoColors.TransparentGray);

    /// <summary>
    /// Draw the map's physics area.
    /// </summary>
    public QuantumGizmoEntry PhysicsArea = new QuantumGizmoEntry(QuantumGizmoColors.LightBlue);

    /// <summary>
    /// Draw the map's physics buckets.
    /// </summary>
    public QuantumGizmoEntry PhysicsBuckets = new QuantumGizmoEntry(QuantumGizmoColors.LightBlue);

    /// <summary>
    /// Draw the baked static mesh vertices.
    /// </summary>
    public QuantumGizmoEntry StaticMeshNormals = new QuantumGizmoEntry(QuantumGizmoColors.Red);

    /// <summary>
    /// Draw the baked static mesh vertices.
    /// </summary>
    public QuantumGizmoEntry StaticMeshTriangles = new QuantumGizmoEntry(QuantumGizmoColors.LightBlue);

    /// <summary>
    /// Draw the cells of the scene mesh.
    /// </summary>
    public QuantumGizmoEntry SceneMeshCells = new QuantumGizmoEntry(QuantumGizmoColors.LightBlue);

    /// <summary>
    /// Draw the triangles of the scene mesh.
    /// </summary>
    public QuantumGizmoEntry SceneMeshTriangles = new QuantumGizmoEntry(QuantumGizmoColors.LightBlue);

    /// <summary>
    /// Draw the entity's physics joints.
    /// </summary>
    public JointGizmoEntry PhysicsJoints = new JointGizmoEntry(
      QuantumGizmoColors.TransparentLimeGreen,
      secondaryColor: QuantumGizmoColors.TransparentYellow,
      warningColor: QuantumGizmoColors.TransparentRed) { Enabled = true, DisableFill = true, OnlyDrawSelected = true };

    /// <summary>
    /// Should NavMesh components be scaled with the agent radius?
    /// </summary>
    [Header("NavMesh Settings")] public Boolean ScaleComponentsWithAgentRadius = true;

    /// <summary>
    /// Draw the NavMesh. The QuantumMap game object will trigger DrawOnlySelected.
    /// </summary>
    [Header("NavMesh Gizmos"), GizmoIconColor(ScriptHeaderBackColor.Blue)]
    public NavMeshGizmoEntry NavMesh = new NavMeshGizmoEntry(QuantumGizmoColors.TransparentLightBlue, QuantumGizmoColors.TransparentMaroon) { Enabled = true, OnlyDrawSelected = true };

    /// <summary>
    /// Draw the border of the NavMesh.
    /// </summary>
    public NavMeshBorderGizmoEntry NavMeshBorders = new NavMeshBorderGizmoEntry(QuantumGizmoColors.Black, false, QuantumGizmoColors.Yellow) { Enabled = true, OnlyDrawSelected = true };

    /// <summary>
    /// Draw the NavMesh area. The QuantumMap game object will trigger DrawOnlySelected.
    /// </summary>
    public QuantumGizmoEntry NavMeshArea = new QuantumGizmoEntry(QuantumGizmoColors.TransparentLightBlue) { OnlyDrawSelected = true };

    /// <summary>
    /// Draw the NavMesh grid. The QuantumMap game object will trigger DrawOnlySelected.
    /// </summary>
    public QuantumGizmoEntry NavMeshGrid = new QuantumGizmoEntry(QuantumGizmoColors.TransparentLightGreen) { OnlyDrawSelected = true };

    /// <summary>
    /// Draw the NavMesh links.
    /// </summary>
    public QuantumGizmoEntry NavMeshLinks = new QuantumGizmoEntry(QuantumGizmoColors.Blue) { Enabled = true, OnlyDrawSelected = true };

    /// <summary>
    /// Draw the vertex normals of the NavMesh.
    /// </summary>
    public QuantumGizmoEntry NavMeshVertexNormals = new QuantumGizmoEntry(QuantumGizmoColors.Yellow) { OnlyDrawSelected = true };

    /// <summary>
    /// Draw the triangle ids of the NavMesh.
    /// </summary>
    public QuantumGizmoEntry NavMeshTriangleIds = new QuantumGizmoEntry(QuantumGizmoColors.TransparentLightBlue) { OnlyDrawSelected = true };

    /// <summary>
    /// Draw the region ids of the NavMesh.
    /// </summary>
    public QuantumGizmoEntry NavMeshRegionIds = new QuantumGizmoEntry(QuantumGizmoColors.TransparentMaroon) { OnlyDrawSelected = true };

    /// <summary>
    /// Draw the numerical vertex ids of the NavMesh.
    /// </summary>
    public QuantumGizmoEntry NavMeshVertexIds = new QuantumGizmoEntry(QuantumGizmoColors.Green) { OnlyDrawSelected = true };

    /// <summary>
    /// Draw the NavMesh pathfinder component. Only available at runtime.
    /// </summary>
    [Header("NavMesh Runtime Gizmos"), GizmoIconColor(ScriptHeaderBackColor.Cyan)]
    // components
    public NavMeshComponentGizmoEntry NavMeshPathfinder = new NavMeshComponentGizmoEntry(QuantumGizmoColors.Magenta);

    /// <summary>
    /// Draw the NavMesh steering agent component. Only available at runtime.
    /// </summary>
    public NavMeshComponentGizmoEntry NavMeshSteeringAgent = new NavMeshComponentGizmoEntry(QuantumGizmoColors.TransparentGreen);

    /// <summary>
    /// Draw the NavMesh avoidance agent component. Only available at runtime.
    /// </summary>
    public NavMeshComponentGizmoEntry NavMeshAvoidanceAgent = new NavMeshComponentGizmoEntry(QuantumGizmoColors.TransparentBlue);

    /// <summary>
    /// Draw the NavMesh avoidance obstacles component. Only available at runtime.
    /// </summary>
    public NavMeshComponentGizmoEntry NavMeshAvoidanceObstacles = new NavMeshComponentGizmoEntry(QuantumGizmoColors.TransparentRed);

    /// <summary>
    /// Draw the pathfinder path. Only available at runtime.
    /// </summary>
    public QuantumGizmoEntry PathfinderRawPath = new QuantumGizmoEntry(QuantumGizmoColors.Magenta);

    /// <summary>
    /// Draw the raw pathfinder triangle path. Only available at runtime.
    /// </summary>
    public QuantumGizmoEntry PathfinderRawTrianglePath = new QuantumGizmoEntry(QuantumGizmoColors.TransparentMagenta);

    /// <summary>
    /// Draw the pathfinder funnel. Only available at runtime.
    /// </summary>
    public QuantumGizmoEntry PathfinderFunnel = new QuantumGizmoEntry(QuantumGizmoColors.Green);

    private QuantumGizmoEntry GetEntryForBody3D(PhysicsBody3D? physicsBody) {
      var entry = default(QuantumGizmoEntry);
      if (physicsBody.HasValue == false) {
        return KinematicColliders;
      }

      var body = physicsBody.Value;

      if (body.IsKinematic) {
        entry = KinematicColliders;
      } else if (body.IsSleeping) {
        entry = AsleepColliders;
      } else if (!body.Enabled) {
        entry = DisabledColliders;
      } else {
        entry = DynamicColliders;
      }

      return entry;
    }

    private QuantumGizmoEntry GetEntryForBody2D(PhysicsBody2D? physicsBody) {
      if (physicsBody.HasValue == false) {
        return KinematicColliders;
      }

      var body = physicsBody.Value;

      var entry = default(QuantumGizmoEntry);
      if (body.IsKinematic) {
        entry = KinematicColliders;
      } else if (body.IsSleeping) {
        entry = AsleepColliders;
      } else if (!body.Enabled) {
        entry = DisabledColliders;
      } else {
        entry = DynamicColliders;
      }

      return entry;
    }

    /// <summary>
    /// Get the gizmo entry for a specific physics3d entity.
    /// </summary>
    /// <param name="frame"></param>
    /// <param name="handle"></param>
    /// <returns></returns>
    public QuantumGizmoEntry GetEntryForPhysicsEntity3D(Frame frame, EntityRef handle) {
      var body = default(PhysicsBody3D?);

      if (frame.TryGet(handle, out PhysicsBody3D physicsBody)) {
        body = physicsBody;
      }

      var entry = GetEntryForBody3D(body);
      return entry;
    }

    /// <summary>
    /// Get the gizmo entry for a specific physics2d entity.
    /// </summary>
    /// <param name="frame"></param>
    /// <param name="handle"></param>
    /// <returns></returns>
    public QuantumGizmoEntry GetEntryForPhysicsEntity2D(Frame frame, EntityRef handle) {
      var body = default(PhysicsBody2D?);

      if (frame.TryGet(handle, out PhysicsBody2D physicsBody)) {
        body = physicsBody;
      }

      var entry = GetEntryForBody2D(body);
      return entry;
    }
  }
}

#endregion


#region Assets/Photon/Quantum/Runtime/Gizmos/Config/QuantumGizmoCallbackAttribute.cs

namespace Quantum {
  using System;

  public class QuantumGizmoCallbackAttribute : Attribute {
    /// <summary>
    /// Should the gizmo be drawn only at runtime.
    /// </summary>
    public bool RuntimeOnly { get; set; }
    /// <summary>
    /// The name of the field to draw the gizmo for.
    /// </summary>
    public string FieldName { get; set; }
    /// <summary>
    /// The name of the method to call to validate the selection.
    /// </summary>
    public string SelectionValidation { get; set; }

    /// <summary>
    /// Attribute used to mark a method as a callback for drawing a gizmo in the Unity editor for Quantum user-defined gizmo entries.
    /// </summary>
    public QuantumGizmoCallbackAttribute(string fieldName, bool runtimeOnly = false, string selectionValidation = null) {
      FieldName = fieldName;
      SelectionValidation = selectionValidation;
      RuntimeOnly = runtimeOnly;
    }
  }
}

#endregion


#region Assets/Photon/Quantum/Runtime/Gizmos/Config/QuantumGizmoEntry.cs

namespace Quantum {
  using System;
  using UnityEngine;

  /// <summary>
  /// Allows optional gizmo values to be serialized in the inspector.
  /// </summary>
  [Serializable]
  public struct OptionalGizmoBool {
    [NonSerialized] private bool _hasValue;

    [SerializeField] private bool _value;

    /// <summary>
    /// Create a new optional gizmo value with the given value.
    /// </summary>
    /// <param name="value"></param>
    public OptionalGizmoBool(bool value) {
      _hasValue = true;
      this._value = value;
    }

    /// <summary>
    /// Does this optional value have a value.
    /// </summary>
    public bool HasValue => _hasValue;

    /// <summary>
    /// The value of this optional value.
    /// </summary>
    public bool Value {
      get => _hasValue ? _value : default;

      set => _value = value;
    }

    /// <summary>
    /// Implicitly convert an optional gizmo value to a bool.
    /// </summary>
    /// <param name="optional"></param>
    /// <returns></returns>
    public static implicit operator bool(OptionalGizmoBool optional) => optional.Value;

    /// <summary>
    /// Implicitly convert a bool to an optional gizmo value.
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    public static implicit operator OptionalGizmoBool(bool value) => new OptionalGizmoBool(value);
  }

  /// <summary>
  /// Individual entry for a specific section of Quantum.
  /// </summary>
  [Serializable]
  public class QuantumGizmoEntry {
    /// <summary>
    /// Is this gizmo enabled.
    /// </summary>
    public bool Enabled;

    /// <summary>
    /// The main color of the gizmo.
    /// </summary>
    public Color Color;

    /// <summary>
    /// The scale of the gizmo. Only available for gizmos that support scaling.
    /// 0 means disabled.
    /// </summary>
    [Range(0.1f, 5), DrawIf(nameof(Scale), 0f, CompareOperator.NotEqual, mode: DrawIfMode.Hide)]
    public float Scale;

    /// <summary>
    /// Only draw the gizmo when the object is selected.
    /// </summary>
    public OptionalGizmoBool OnlyDrawSelected;

    /// <summary>
    /// Draw the gizmo without fill. Only available for gizmos that support fill.
    /// </summary>
    public OptionalGizmoBool DisableFill;

    /// <summary>
    /// The color of the gizmo when it is inactive.
    /// </summary>
    public Color InactiveColor => Color.Desaturate();

    /// <summary>
    /// The transparent version of the gizmo color.
    /// </summary>
    public Color TransparentColor => Color.Alpha(0.5f);

    /// <summary>
    /// The style of the gizmo.
    /// </summary>
    public QuantumGizmoStyle Style => DisableFill ? QuantumGizmoStyle.FillDisabled : default;

    /// <summary>
    /// Create a new gizmo entry with the given color.
    /// </summary>
    /// <param name="color"></param>
    public QuantumGizmoEntry(Color color) {
      Color = color;
    }
  }

  /// <summary>
  /// User defined gizmo entry.
  /// </summary>
  [Serializable]
  public class QuantumUserGizmoEntry : QuantumGizmoEntry {
    /// <inheritdoc />
    public QuantumUserGizmoEntry(Color color) : base(color) {
      OnlyDrawSelected = false;
      Enabled = true;
    }
  }

  /// <summary>
  /// Individual entry for specifically the physics section of the gizmo overlay.
  /// </summary>
  [Serializable]
  public class PhysicsComponentGizmoEntry : QuantumGizmoEntry {
    /// <summary>
    /// Create a new physics component gizmo entry with the given color.
    /// </summary>
    /// <param name="color"></param>
    public PhysicsComponentGizmoEntry(Color color) : base(color) {
      Enabled = true;
      DisableFill = false;
      OnlyDrawSelected = false;
    }
  }

  /// <summary>
  /// Individual entry for specifically the joint section of the gizmo overlay.
  /// </summary>
  [Serializable]
  public class JointGizmoEntry : QuantumGizmoEntry {
    /// <summary>
    /// The secondary color of the joint gizmo.
    /// </summary>
    public Color SecondaryColor;

    /// <summary>
    /// The warning color of the joint gizmo.
    /// </summary>
    public Color WarningColor;

    /// <summary>
    /// Create a new joint gizmo entry with the given colors.
    /// </summary>
    /// <param name="color"></param>
    /// <param name="secondaryColor"></param>
    /// <param name="warningColor"></param>
    public JointGizmoEntry(Color color, Color secondaryColor, Color warningColor) : base(color) {
      SecondaryColor = secondaryColor;
      WarningColor = warningColor;
    }
  }

  /// <summary>
  /// Individual entry for specifically the NavMesh component section of the gizmo overlay.
  /// </summary>
  [Serializable]
  public class NavMeshComponentGizmoEntry : QuantumGizmoEntry {
    /// <summary>
    /// Default size for NavMesh component gizmos.
    /// </summary>
    private const float DefaultComponentGizmoSize = 0.5f;

    /// <summary>
    /// Create a new NavMesh component gizmo entry with the given color.
    /// </summary>
    /// <param name="color"></param>
    public NavMeshComponentGizmoEntry(Color color) : base(color) {
      Scale = DefaultComponentGizmoSize;
      DisableFill = true;
      OnlyDrawSelected = false;
    }
  }

  /// <summary>
  /// Individual entry for specifically the border of the navmesh section of the gizmo overlay.
  /// </summary>
  [Serializable]
  public class NavMeshBorderGizmoEntry : QuantumGizmoEntry {
    /// <summary>
    /// Should the normals of the border be drawn.
    /// </summary>
    public bool DrawNormals;

    /// <summary>
    /// The color of the border normals.
    /// </summary>
    public Color BorderNormalColor;

    /// <summary>
    /// Create a new NavMesh border gizmo entry with the given colors.
    /// </summary>
    /// <param name="color"></param>
    /// <param name="drawNormals"></param>
    /// <param name="borderNormalColor"></param>
    public NavMeshBorderGizmoEntry(Color color, bool drawNormals, Color borderNormalColor) : base(color) {
      DrawNormals = drawNormals;
      BorderNormalColor = borderNormalColor;
    }
  }

  /// <summary>
  /// Individual entry for specifically the navmesh section of the gizmo overlay.
  /// </summary>
  [Serializable]
  public class NavMeshGizmoEntry : QuantumGizmoEntry {
    /// <summary>
    /// The color of the navmesh region.
    /// </summary>
    public Color RegionColor;

    /// <summary>
    /// Create a new NavMesh gizmo entry with the given colors.
    /// </summary>
    /// <param name="color"></param>
    /// <param name="regionColor"></param>
    public NavMeshGizmoEntry(Color color, Color regionColor) : base(color) {
      RegionColor = regionColor;
    }
  }
}

#endregion


#region Assets/Photon/Quantum/Runtime/Gizmos/QuantumGameGizmos.cs

namespace Quantum {
#if UNITY_EDITOR
  using System;
  using System.Collections.Generic;
  using System.Reflection;
  using Photon.Analyzer;
  using UnityEditor;
  using UnityEngine;
  using UnityEngine.SceneManagement;
  using static UnityEngine.Object;
  using static QuantumUnityExtensions;

  internal struct GizmoNavmeshData {
    public Mesh GizmoMesh;
    public NavMeshRegionMask CurrentRegionMask;
  }

  internal class StaticMeshColliderGizmoData {
    public Vector3[] TrianglePoints = Array.Empty<Vector3>();
    public int[] TriangleSegments = Array.Empty<int>();
    public Vector3[] NormalPoints = Array.Empty<Vector3>();
  }

  internal struct QuantumGizmosJointInfo {
    public enum GizmosJointType {
      None = 0,

      DistanceJoint2D = 1,
      DistanceJoint3D = 2,

      SpringJoint2D = 3,
      SpringJoint3D = 4,

      HingeJoint2D = 5,
      HingeJoint3D = 6,
    }

    public GizmosJointType Type;
    public bool Selected;

    public Vector3 AnchorPos;
    public Vector3 ConnectedPos;

    public Quaternion JointRot;
    public Quaternion ConnectedRot;
    public Quaternion RelRotRef;

    public float MinDistance;
    public float MaxDistance;

    public Vector3 Axis;

    public bool UseAngleLimits;
    public float LowerAngle;
    public float UpperAngle;
  }

  internal class UserGizmoCallback {
    public QuantumGizmoCallbackAttribute Attribute;
    public MethodInfo Method;
    public MethodInfo SelectionValidation;
    public QuantumUserGizmoEntry Entry;
  }

  /// <summary>
  /// Draws gizmos for the Quantum simulation.
  /// </summary>
  public partial class QuantumGameGizmos : MapDataBakerCallback {
    private static QuantumGameGizmosSettings _settings => QuantumGameGizmosSettingsScriptableObject.Global.Settings;

    [StaticField] private static Dictionary<string, GizmoNavmeshData> _navmeshGizmoMap;

    [StaticField] private static readonly Dictionary<MonoBehaviour, StaticMeshColliderGizmoData> _meshGizmoData =
      new Dictionary<MonoBehaviour, StaticMeshColliderGizmoData>();

    [StaticField] private static UserGizmoCallback[] _userCallbacks;

    private static QuantumEntityViewUpdater _evu;
    private static QuantumMapData _mapData;

    static QuantumGameGizmos() {
      SceneManager.sceneLoaded += (arg0, mode) => {
        InvalidatePhysicsGizmos();
      };

      EditorApplication.update += InvokeGizmoUser;
    }

    [StaticFieldResetMethod]
    private static void InvokeGizmoUser() {
      var callbacks = GetUserCallbacks();
      var evu = GetEntityViewUpdater();

      var settings = QuantumGameGizmosSettingsScriptableObject.Global.Settings;

      if (evu == null) {
        return;
      }

      foreach (var callback in callbacks) {
        bool runtimeOnly = callback.Attribute.RuntimeOnly;

        if (runtimeOnly && !Application.isPlaying) {
          continue;
        }

        bool selected = callback.SelectionValidation == null || (bool)callback.SelectionValidation.Invoke(settings, null);

        if (ShouldDraw(callback.Entry, selected, false)) {
          callback.Method.Invoke(settings, null);
        }
      }
    }

    private static UserGizmoCallback[] GetUserCallbacks() {
      if (_userCallbacks == null) {
        var gizmoSettings = QuantumGameGizmosSettingsScriptableObject.Global.Settings;

        var userCallbacks = new List<UserGizmoCallback>();

        // check the gizmoSettings for the attribute
        var settingsType = gizmoSettings.GetType();
        var settingsMethods = settingsType.GetMethods(
          BindingFlags.Instance |
          BindingFlags.Public |
          BindingFlags.NonPublic |
          BindingFlags.Static
        );

        foreach (var method in settingsMethods) {
          var attributes = method.GetCustomAttributes(typeof(QuantumGizmoCallbackAttribute), false);
          if (attributes.Length > 0) {
            var attr = (QuantumGizmoCallbackAttribute)attributes[0];
            userCallbacks.Add(new UserGizmoCallback {
              Attribute = attr,
              Method = method,
              SelectionValidation = attr.SelectionValidation != null
                ? settingsType.GetMethod(attr.SelectionValidation, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                : null,
              Entry = (QuantumUserGizmoEntry)settingsType.GetField(attr.FieldName).GetValue(gizmoSettings)
            });
          }
        }

        _userCallbacks = userCallbacks.ToArray();
      }

      return _userCallbacks;
    }

    /// <summary>
    /// Invalidates the navmesh gizmos.
    /// </summary>
    [StaticFieldResetMethod]
    public static void InvalidateNavMeshGizmos() {
      _navmeshGizmoMap?.Clear();
    }

    /// <summary>
    /// Invalidates the physics gizmos.
    /// </summary>
    [StaticFieldResetMethod]
    public static void InvalidatePhysicsGizmos() {
      _meshGizmoData.Clear();
      _mapData = null;
    }

    private static QuantumEntityViewUpdater GetEntityViewUpdater() {
      if (_evu == null) {
        _evu = FindFirstObjectByType<QuantumEntityViewUpdater>();
      }

      return _evu;
    }

    private static QuantumMapData GetMapData() {
      if (_mapData == null) {
        _mapData = FindFirstObjectByType<QuantumMapData>();
      }

      return _mapData;
    }

    private static bool ShouldDraw(
      QuantumGizmoEntry entry,
      bool selected,
      bool hasStateDrawer = true) {
      if (entry.Enabled == false)
        return false;

      bool hasSelectedFlag = entry.OnlyDrawSelected is { HasValue: true, Value: true };

      if (Application.isPlaying) {
        if (hasStateDrawer) {
          // state drawer will take over
          return false;
        }
      }

      if (hasSelectedFlag) {
        return selected;
      }

      return true;
    }

    [DrawGizmo(GizmoType.Selected | GizmoType.Active | GizmoType.NonSelected)]
    static void DrawGizmos(QuantumRunnerBehaviour behaviour, GizmoType gizmoType) {
      if (behaviour.Runner?.Session == null) {
        return;
      }

      if (behaviour.Runner.HideGizmos) {
        return;
      }

      if (behaviour.Runner.Session.Game is not QuantumGame game) {
        return;
      }

      OnDrawGizmosInternal(
        game,
        gizmoType,
        behaviour.Runner.GizmoSettings ?? QuantumGameGizmosSettingsScriptableObject.Global.Settings
      );
    }

    static void OnDrawGizmosInternal(
      QuantumGame game,
      GizmoType type,
      QuantumGameGizmosSettings gizmosSettings) {
      var frame = game.Frames.Predicted;

      if (frame != null) {
        DrawMapGizmos(frame.Map, frame);

#if QUANTUM_ENABLE_AI && !QUANTUM_DISABLE_AI
        OnDrawGizmos_NavMesh(frame, gizmosSettings, type);
#endif

#if QUANTUM_ENABLE_PHYSICS3D && !QUANTUM_DISABLE_PHYSICS3D
        DrawRuntimePhysicsComponents_3D(gizmosSettings, frame);
#endif

#if QUANTUM_ENABLE_PHYSICS2D && !QUANTUM_DISABLE_PHYSICS2D
        DrawRuntimePhysicsComponents_2D(gizmosSettings, frame);
#endif

        OnDrawGizmos_Prediction(frame, type);
      }
    }

    /// <summary>
    /// On BeforeBake override, empty.
    /// </summary>
    public override void OnBeforeBake(QuantumMapData data) {
    }

    /// <summary>
    /// On Bake override, creates gizmos data for terrain collider.
    /// </summary>
    /// <param name="data"></param>
    public override void OnBake(QuantumMapData data) {
#if QUANTUM_ENABLE_PHYSICS3D && !QUANTUM_DISABLE_PHYSICS3D
      if (_settings.StaticMeshTriangles.Enabled || _settings.StaticMeshNormals.Enabled) {
        foreach (var behaviour in data.StaticCollider3DReferences) {
          if (behaviour is QuantumStaticMeshCollider3D or QuantumStaticTerrainCollider3D) {
            CreateStaticMeshData(behaviour);
          }
        }
      }
#endif
    }

    // shared between 2d and 3d
    private static void DrawGizmosJointInternal(
      ref QuantumGizmosJointInfo p,
      QuantumGameGizmosSettings settings,
      bool disableFill = false) {
      const float anchorRadiusFactor = 0.1f;
      const float barHalfLengthFactor = 0.1f;
      const float hingeRefAngleBarLengthFactor = 0.5f;

      // how much weaker the alpha of the color of hinge disc is relative to the its rim's alpha
      const float solidDiscAlphaRatio = 0.25f;

      if (p.Type == QuantumGizmosJointInfo.GizmosJointType.None) {
        return;
      }

      var gizmosScale = settings.IconScale;

      var jointEntry = _settings.PhysicsJoints;

      var primColor = jointEntry.Color;
      var secColor = jointEntry.SecondaryColor;
      var warningColor = jointEntry.WarningColor;

      if (p.Selected) {
        primColor = primColor.Brightness(settings.SelectedBrightness);
        secColor = secColor.Brightness(settings.SelectedBrightness);
        warningColor = warningColor.Brightness(settings.SelectedBrightness);
      }

      var style = disableFill ? QuantumGizmoStyle.FillDisabled : default;

      GizmoUtils.DrawGizmosSphere(p.AnchorPos, gizmosScale * anchorRadiusFactor, secColor, style: style);
      GizmoUtils.DrawGizmosSphere(p.ConnectedPos, gizmosScale * anchorRadiusFactor, secColor, style: style);

      Gizmos.color = secColor;
      Gizmos.DrawLine(p.AnchorPos, p.ConnectedPos);

      switch (p.Type) {
        case QuantumGizmosJointInfo.GizmosJointType.DistanceJoint2D:
        case QuantumGizmosJointInfo.GizmosJointType.DistanceJoint3D: {
          var connectedToAnchorDir = Vector3.Normalize(p.AnchorPos - p.ConnectedPos);
          var minDistanceMark = p.ConnectedPos + connectedToAnchorDir * p.MinDistance;
          var maxDistanceMark = p.ConnectedPos + connectedToAnchorDir * p.MaxDistance;

          Gizmos.color = Handles.color = primColor;

          Gizmos.DrawLine(minDistanceMark, maxDistanceMark);
          GizmoUtils.DrawGizmoDisc(minDistanceMark, connectedToAnchorDir, barHalfLengthFactor, primColor, style: style);
          GizmoUtils.DrawGizmoDisc(maxDistanceMark, connectedToAnchorDir, barHalfLengthFactor, primColor, style: style);

          Gizmos.color = Handles.color = Color.white;

          break;
        }

        case QuantumGizmosJointInfo.GizmosJointType.SpringJoint2D:
        case QuantumGizmosJointInfo.GizmosJointType.SpringJoint3D: {
          var connectedToAnchorDir = Vector3.Normalize(p.AnchorPos - p.ConnectedPos);
          var distanceMark = p.ConnectedPos + connectedToAnchorDir * p.MinDistance;

          Gizmos.color = Handles.color = primColor;

          Gizmos.DrawLine(p.ConnectedPos, distanceMark);
          GizmoUtils.DrawGizmoDisc(distanceMark, connectedToAnchorDir, barHalfLengthFactor, primColor, style: style);

          Gizmos.color = Handles.color = Color.white;

          break;
        }

        case QuantumGizmosJointInfo.GizmosJointType.HingeJoint2D: {
          var hingeRefAngleBarLength = hingeRefAngleBarLengthFactor * gizmosScale;
          var connectedAnchorRight = p.ConnectedRot * Vector3.right;
          var anchorRight = p.JointRot * Vector3.right;

          Gizmos.color = secColor;
          Gizmos.DrawRay(p.AnchorPos, anchorRight * hingeRefAngleBarLength);

          Gizmos.color = primColor;
          Gizmos.DrawRay(p.ConnectedPos, connectedAnchorRight * hingeRefAngleBarLength);

#if QUANTUM_XY
          var planeNormal = -Vector3.forward;
#else
          var planeNormal = Vector3.up;
#endif

          if (p.UseAngleLimits) {
            var fromDir = Quaternion.AngleAxis(p.LowerAngle, planeNormal) * connectedAnchorRight;
            var angleRange = p.UpperAngle - p.LowerAngle;
            var arcColor = angleRange < 0.0f ? warningColor : primColor;
            GizmoUtils.DrawGizmoArc(p.ConnectedPos, planeNormal, fromDir, angleRange, hingeRefAngleBarLength, arcColor,
              solidDiscAlphaRatio, style: style);
          } else {
            // Draw full disc
            GizmoUtils.DrawGizmoDisc(p.ConnectedPos, planeNormal, hingeRefAngleBarLength, primColor,
              solidDiscAlphaRatio, style: style);
          }

          Gizmos.color = Handles.color = Color.white;

          break;
        }

        case QuantumGizmosJointInfo.GizmosJointType.HingeJoint3D: {
          var hingeRefAngleBarLength = hingeRefAngleBarLengthFactor * gizmosScale;

          var hingeAxisLocal = p.Axis.sqrMagnitude > float.Epsilon ? p.Axis.normalized : Vector3.right;
          var hingeAxisWorld = p.JointRot * hingeAxisLocal;
          var hingeOrtho = Vector3.Cross(hingeAxisWorld, p.JointRot * Vector3.up);

          hingeOrtho = hingeOrtho.sqrMagnitude > float.Epsilon
            ? hingeOrtho.normalized
            : Vector3.Cross(hingeAxisWorld, p.JointRot * Vector3.forward).normalized;

          Gizmos.color = Handles.color = primColor;

          Gizmos.DrawRay(p.AnchorPos, hingeOrtho * hingeRefAngleBarLength);
          Handles.ArrowHandleCap(0, p.ConnectedPos, Quaternion.FromToRotation(Vector3.forward, hingeAxisWorld),
            hingeRefAngleBarLengthFactor * 1.5f, EventType.Repaint);

          if (p.UseAngleLimits) {
            var refAngle = ComputeRelativeAngleHingeJoint(hingeAxisWorld, p.JointRot, p.ConnectedRot, p.RelRotRef);
            var refOrtho = Quaternion.AngleAxis(refAngle, hingeAxisWorld) * hingeOrtho;
            var fromDir = Quaternion.AngleAxis(-p.LowerAngle, hingeAxisWorld) * refOrtho;
            var angleRange = p.UpperAngle - p.LowerAngle;
            var arcColor = angleRange < 0.0f ? warningColor : primColor;
            GizmoUtils.DrawGizmoArc(p.ConnectedPos, hingeAxisWorld, fromDir, -angleRange, hingeRefAngleBarLength,
              arcColor, solidDiscAlphaRatio, style: style);
          } else {
            // Draw full disc
            GizmoUtils.DrawGizmoDisc(p.ConnectedPos, hingeAxisWorld, hingeRefAngleBarLength, primColor,
              solidDiscAlphaRatio, style: style);
          }

          Gizmos.color = Handles.color = Color.white;

          break;
        }
      }
    }

    private static float ComputeRelativeAngleHingeJoint(Vector3 hingeAxis, Quaternion rotJoint,
      Quaternion rotConnectedAnchor, Quaternion relRotRef) {
      var rotDiff = rotConnectedAnchor * Quaternion.Inverse(rotJoint);
      var relRot = rotDiff * Quaternion.Inverse(relRotRef);

      var rotVector = new Vector3(relRot.x, relRot.y, relRot.z);
      var sinHalfRadAbs = rotVector.magnitude;
      var cosHalfRad = relRot.w;

      var hingeAngleRad = 2 * Mathf.Atan2(sinHalfRadAbs, Mathf.Sign(Vector3.Dot(rotVector, hingeAxis)) * cosHalfRad);

      // clamp to range [-Pi, Pi]
      if (hingeAngleRad < -Mathf.PI) {
        hingeAngleRad += 2 * Mathf.PI;
      }

      if (hingeAngleRad > Mathf.PI) {
        hingeAngleRad -= 2 * Mathf.PI;
      }

      return hingeAngleRad * Mathf.Rad2Deg;
    }
  }
#endif
}

#endregion


#region Assets/Photon/Quantum/Runtime/Gizmos/QuantumGameGizmos.EntityPrototype.cs

namespace Quantum {
#if UNITY_EDITOR
  using Photon.Deterministic;
  using UnityEditor;
  using UnityEngine;

  public partial class QuantumGameGizmos {
    [DrawGizmo(GizmoType.Selected | GizmoType.Active | GizmoType.NonSelected)]
    static void DrawGizmos_QuantumEntityPrototype(QuantumEntityPrototype behaviour, GizmoType gizmoType) {
      bool selected = gizmoType.HasFlag(GizmoType.Selected);

      FPMathUtils.LoadLookupTables();

      try {
        behaviour.PreSerialize();
      } catch {
        // ignored
      }

      Shape2DConfig config2D = null;
      Shape3DConfig config3D = null;
      bool isDynamic2D = false;
      bool isDynamic3D = false;
      float height = 0.0f;

      var transform = behaviour.transform;

      Vector3 position2D = transform.position;
      FP rotation2DDeg = transform.rotation.ToFPRotation2DDegrees();
      Vector3 position3D = transform.position;
      Quaternion rotation3D = transform.rotation;

      CharacterController2DConfig configCC2D = null;
      CharacterController3DConfig configCC3D = null;

      var transformMode = behaviour.TransformMode;
      var physicsBody = behaviour.PhysicsBody;
      var physicsCollider = behaviour.PhysicsCollider;

      if (behaviour.PhysicsCollider.IsEnabled) {
        if (behaviour.TransformMode == QuantumEntityPrototypeTransformMode.Transform2D) {
          config2D = behaviour.GetScaledShape2DConfig();
          isDynamic2D = physicsBody.IsEnabled && !physicsCollider.IsTrigger &&
                        (physicsBody.Config2D & PhysicsBody2D.ConfigFlags.IsKinematic) == default;
        } else if (transformMode == QuantumEntityPrototypeTransformMode.Transform3D) {
          config3D = behaviour.GetScaledShape3DConfig();
          isDynamic3D = physicsBody.IsEnabled && !physicsCollider.IsTrigger &&
                        (physicsBody.Config3D & PhysicsBody3D.ConfigFlags.IsKinematic) == default;
        }
      }

      if (behaviour.Transform2DVertical.IsEnabled) {
#if QUANTUM_XY
        var verticalScale = transform.lossyScale.z;
        height = -behaviour.Transform2DVertical.Height.AsFloat * verticalScale;
        position2D.z -= behaviour.Transform2DVertical.PositionOffset.AsFloat * verticalScale;
#else
        var verticalScale = transform.lossyScale.y;
        height = behaviour.Transform2DVertical.Height.AsFloat * verticalScale;
        position2D.y += behaviour.Transform2DVertical.PositionOffset.AsFloat * verticalScale;
#endif
      }

      // handle overriding from components
      {
        var vertical = SafeGetPrototype<Quantum.Prototypes.Transform2DVerticalPrototype>(behaviour);
        if (vertical != null) {
#if QUANTUM_XY
          var verticalScale = transform.lossyScale.z;
          height = -vertical.Height.AsFloat * verticalScale;
          position2D.z = -vertical.Position.AsFloat * verticalScale;
#else
          var verticalScale = transform.lossyScale.y;
          height = vertical.Height.AsFloat * verticalScale;
          position2D.y = vertical.Position.AsFloat * verticalScale;
#endif
        }

        var transform2D = SafeGetPrototype<Quantum.Prototypes.Transform2DPrototype>(behaviour);
        if (transformMode == QuantumEntityPrototypeTransformMode.Transform2D || transform2D != null) {
          if (transform2D != null) {
            position2D = transform2D.Position.ToUnityVector3();
            rotation2DDeg = transform2D.Rotation;
          }

          config2D = SafeGetPrototype<Quantum.Prototypes.PhysicsCollider2DPrototype>(behaviour)?.ShapeConfig ??
                     config2D;
          isDynamic2D |= behaviour.GetComponent<QPrototypePhysicsBody2D>();

          var cc = SafeGetPrototype<Quantum.Prototypes.CharacterController2DPrototype>(behaviour);
          if (cc != null) {
            QuantumUnityDB.TryGetGlobalAssetEditorInstance(cc.Config, out configCC2D);
          }
        }

        var transform3D = SafeGetPrototype<Quantum.Prototypes.Transform3DPrototype>(behaviour);
        if (behaviour.TransformMode == QuantumEntityPrototypeTransformMode.Transform3D || transform3D != null) {
          if (transform3D != null) {
            position3D = transform3D.Position.ToUnityVector3();
            rotation3D = UnityEngine.Quaternion.Euler(transform3D.Rotation.ToUnityVector3());
          }

          config3D = SafeGetPrototype<Quantum.Prototypes.PhysicsCollider3DPrototype>(behaviour)?.ShapeConfig ??
                     config3D;
          isDynamic3D |= behaviour.GetComponent<QPrototypePhysicsBody3D>();

          var cc = SafeGetPrototype<Quantum.Prototypes.CharacterController3DPrototype>(behaviour);
          if (cc != null) {
            QuantumUnityDB.TryGetGlobalAssetEditorInstance(cc.Config, out configCC3D);
          }
        }
      }
      
      bool shouldDrawCharacterController = ShouldDraw(_settings.CharacterController, selected);
      
#if QUANTUM_ENABLE_PHYSICS2D && !QUANTUM_DISABLE_PHYSICS2D
      bool shouldDraw = isDynamic2D
        ? ShouldDraw(_settings.DynamicColliders, selected)
        : ShouldDraw(_settings.KinematicColliders, selected);

      if (config2D != null && shouldDraw) {
        var color = isDynamic2D
          ? _settings.DynamicColliders.Color
          : _settings.KinematicColliders.Color;

        var disableFill = behaviour.PhysicsBody.Config2D.HasFlag(PhysicsBody2D.ConfigFlags.IsKinematic)
          ? _settings.KinematicColliders.DisableFill
          : _settings.DynamicColliders.DisableFill;

        var style = disableFill is { HasValue: true, Value: true }
          ? QuantumGizmoStyle.FillDisabled
          : default;
        
        if (config2D.ShapeType == Shape2DType.Polygon) {
          if (QuantumUnityDB.TryGetGlobalAsset(config2D.PolygonCollider, out Quantum.PolygonCollider collider)) {
            DrawShape2DGizmo(
              Shape2D.CreatePolygon(collider, config2D.PositionOffset,
                FP.FromRaw((config2D.RotationOffset.RawValue * FP.Raw.Deg2Rad) >> FPLut.PRECISION)),
              position2D,
              rotation2DDeg.ToUnityQuaternionDegrees(),
              color, height, null, style: style);
          }
        } else if (config2D.ShapeType == Shape2DType.Compound) {
          foreach (var shape in config2D.CompoundShapes) {
            // nested compound shapes are not supported on the editor yet
            if (shape.ShapeType == Shape2DType.Compound) {
              continue;
            }

            if (shape.ShapeType == Shape2DType.Polygon) {
              if (QuantumUnityDB.TryGetGlobalAsset(shape.PolygonCollider, out Quantum.PolygonCollider collider)) {
                DrawShape2DGizmo(
                  Shape2D.CreatePolygon(collider, shape.PositionOffset,
                    FP.FromRaw((shape.RotationOffset.RawValue * FP.Raw.Deg2Rad) >> FPLut.PRECISION)),
                  position2D,
                  rotation2DDeg.ToUnityQuaternionDegrees(),
                  color, height, null, style: style
                );
              }
            } else {
              DrawShape2DGizmo(
                shape.CreateShape(null),
                position2D,
                rotation2DDeg.ToUnityQuaternionDegrees(),
                color,
                height,
                null,
                style: style
              );
            }
          }
        } else {
          DrawShape2DGizmo(
            config2D.CreateShape(null),
            position2D,
            rotation2DDeg.ToUnityQuaternionDegrees(),
            color,
            height,
            null,
            style: style);
        }
      }

      if (configCC2D != null && shouldDrawCharacterController) {
        DrawCharacterController2DGizmo(
          position2D,
          configCC2D,
          _settings.GetSelectedColor(_settings.CharacterController.Color, selected),
          _settings.GetSelectedColor(_settings.AsleepColliders.Color, selected),
          disableFill: _settings.CharacterController.DisableFill
        );
      }
#endif
#if QUANTUM_ENABLE_PHYSICS3D && !QUANTUM_DISABLE_PHYSICS3D
      bool shouldDraw3D = isDynamic3D
        ? ShouldDraw(_settings.DynamicColliders, selected)
        : ShouldDraw(_settings.KinematicColliders, selected);

      if (config3D != null && shouldDraw3D) {
        var style = behaviour.PhysicsBody.Config3D.HasFlag(PhysicsBody3D.ConfigFlags.IsKinematic)
          ? _settings.KinematicColliders.Style
          : _settings.DynamicColliders.Style;

        var color = isDynamic3D
          ? _settings.DynamicColliders.Color
          : _settings.KinematicColliders.Color;
        if (config3D.ShapeType == Shape3DType.Compound) {
          foreach (var shape in config3D.CompoundShapes) {
            // nested compound shapes are not supported on the editor yet
            if (shape.ShapeType == Shape3DType.Compound) {
              continue;
            }

            DrawShape3DGizmo(shape.CreateShape(null), position3D, rotation3D, color, style: style);
          }
        } else {
          DrawShape3DGizmo(config3D.CreateShape(null), position3D, rotation3D, color, style: style);
        }
      }

      if (configCC3D != null && shouldDrawCharacterController) {
        DrawCharacterController3DGizmo(
          position3D,
          configCC3D,
          _settings.GetSelectedColor(_settings.CharacterController.Color, selected),
          _settings.GetSelectedColor(_settings.AsleepColliders.Color, selected),
          _settings.CharacterController.DisableFill
        );
      }
#endif
    }

    private static T SafeGetPrototype<T>(Behaviour behaviour) where T : ComponentPrototype, new() {
      var component = behaviour.GetComponent<QuantumUnityComponentPrototype<T>>();
      if (component == null) {
        return null;
      }

      return (T)component.CreatePrototype(null);
    }
  }
#endif
}

#endregion


#region Assets/Photon/Quantum/Runtime/Gizmos/QuantumGameGizmos.MapData.cs

namespace Quantum {
#if UNITY_EDITOR
  using System;
  using System.Collections.Generic;
  using Photon.Deterministic;
  using UnityEditor;
  using UnityEngine;

  public partial class QuantumGameGizmos {
    [DrawGizmo(GizmoType.Pickable | GizmoType.Selected | GizmoType.NonSelected)]
    static void DrawGizmos_MapData(QuantumMapData behaviour, GizmoType gizmoType) {
      if (Application.isPlaying) {
        return;
      }

      if (behaviour.Asset == null) {
        return;
      }

      FPMathUtils.LoadLookupTables();
      DrawMapGizmos(behaviour.Asset, null);

      var navmeshList = new List<NavMesh>();

      foreach (var navmeshLink in behaviour.Asset.NavMeshLinks) {
        if (navmeshLink.IsValid) {
          navmeshList.Add(QuantumUnityDB.GetGlobalAsset(navmeshLink));
        }
      }

      DrawMapNavMesh(behaviour.Asset, navmeshList, NavMeshRegionMask.Default, _settings);
    }

    private static unsafe void DrawMapGizmos(Map map, Frame frame) {
      if (map) {
        FPMathUtils.LoadLookupTables();

        var center = FPVector3.Zero;

#if QUANTUM_XY
        center = center.XZY;
#endif

        var worldSize = FPMath.Min(map.WorldSize, FP.UseableMax);
        var physicsArea = new FPVector2(worldSize, worldSize);

        if (map.SortingAxis == PhysicsCommon.SortAxis.X) {
          physicsArea.X = FPMath.Min(physicsArea.X, FP.UseableMax / 2);
        } else {
          physicsArea.Y = FPMath.Min(physicsArea.Y, FP.UseableMax / 2);
        }

        if (_settings.PhysicsArea.Enabled) {
          GizmoUtils.DrawGizmosBox(
            center.ToUnityVector3(),
            physicsArea.ToUnityVector3(),
            _settings.PhysicsArea.Color
          );
        }

        if (_settings.PhysicsBuckets.Enabled) {
          var bottomLeft = center.ToUnityVector3() - physicsArea.ToUnityVector3() / 2;

          if (map.BucketingAxis == PhysicsCommon.BucketAxis.X) {
            var bucketSize = physicsArea.X.AsFloat / map.BucketsCount;
            GizmoUtils.DrawGizmoGrid(
              bottomLeft,
              map.BucketsCount,
              1,
              bucketSize,
              physicsArea.Y.AsFloat,
              _settings.PhysicsBuckets.Color
            );
          } else {
            var bucketSize = physicsArea.Y.AsFloat / map.BucketsCount;
            GizmoUtils.DrawGizmoGrid(
              bottomLeft,
              1,
              map.BucketsCount,
              physicsArea.X.AsFloat,
              bucketSize,
              _settings.PhysicsBuckets.Color
            );
          }
        }
        
        bool selected = false;
        
        var mapData = GetMapData();
        
        if (mapData) {
          selected = Selection.activeGameObject == mapData.gameObject;
        }

        if (ShouldDraw(_settings.NavMeshGrid, selected, false)) {
          GizmoUtils.DrawGizmosBox(
            center.ToUnityVector3(),
            new FPVector2(map.WorldSizeX, map.WorldSizeY).ToUnityVector3(),
            _settings.NavMeshGrid.Color
          );
          
          var bottomLeft = center.ToUnityVector3() - (-map.WorldOffset).ToUnityVector3();
          GizmoUtils.DrawGizmoGrid(
            bottomLeft,
            map.GridSizeX,
            map.GridSizeY,
            map.GridNodeSize,
            _settings.NavMeshGrid.Color
          );
        }

        if (frame is { Physics3D: { SceneMesh: not null } }) {
          var mesh = frame.Physics3D.SceneMesh;
          if (mesh != null) {
            if (_settings.SceneMeshCells.Enabled) {
              mesh.VisitCells((x, y, z, tris, count) => {
                if (count > 0) {
                  var c = mesh.GetNodeCenter(x, y, z).ToUnityVector3();
                  var s = mesh.CellSize.AsFloat * Vector3.one;

                  GizmoUtils.DrawGizmosBox(
                    c,
                    s,
                    _settings.SceneMeshCells.Color,
                    style: QuantumGizmoStyle.FillDisabled
                  );
                }
              });
            }

            if (_settings.SceneMeshTriangles.Enabled) {
              mesh.VisitCells((x, y, z, tris, count) => {
                for (int i = 0; i < count; ++i) {
                  var t = mesh.GetTriangle(tris[i]);
                  Gizmos.color = _settings.SceneMeshTriangles.Color;
                  Gizmos.DrawLine(t->A.ToUnityVector3(), t->B.ToUnityVector3());
                  Gizmos.DrawLine(t->B.ToUnityVector3(), t->C.ToUnityVector3());
                  Gizmos.DrawLine(t->C.ToUnityVector3(), t->A.ToUnityVector3());
                }
              });
            }
          }
        }
      }
    }

    static void DrawMapNavMesh(
      Map map,
      List<NavMesh> navmeshList,
      NavMeshRegionMask mask,
      QuantumGameGizmosSettings gizmosSettings) {
#if QUANTUM_ENABLE_AI && !QUANTUM_DISABLE_AI
      
      var navMeshRegionMask = mask;

      bool selected = false;
      
      var mapData = GetMapData();
      
      if (mapData) {
        selected = Selection.activeGameObject == mapData.gameObject;
      }
      
      if (ShouldDraw(_settings.NavMeshArea, selected, false)) {
        GizmoUtils.DrawGizmosBox(
          Vector3.zero,
          new FPVector2(map.WorldSizeX, map.WorldSizeY).ToUnityVector3(),
          _settings.NavMeshArea.Color
        );
      }

      foreach (var navmesh in navmeshList) {
        if (_settings.NavMesh.Enabled) {
          CreateAndDrawNavMeshGizmo(navmesh, navMeshRegionMask);
        }

        if (_settings.NavMeshRegionIds.Enabled ||
            _settings.NavMeshTriangleIds.Enabled) {
          for (Int32 i = 0; i < navmesh.Triangles.Length; i++) {
            var t = navmesh.Triangles[i];

            // ################## NavMesh Triangle Ids ##################

            if (ShouldDraw(_settings.NavMeshTriangleIds, selected, false)) {
              Handles.color = _settings.NavMeshTriangleIds.Color;
              Handles.Label(t.Center.ToUnityVector3(true), i.ToString());
            }

            // ################## NavMesh Triangle Region Ids ##################

            if (ShouldDraw(_settings.NavMeshRegionIds, selected, false)) {
              if (t.Regions.HasValidRegions) {
                var s = string.Empty;
                for (int r = 0; r < map.Regions.Length; r++) {
                  if (t.Regions.IsRegionEnabled(r)) {
                    s += $"{map.Regions[r]} ({r})";
                  }
                }

                var vertex0 = navmesh.Vertices[t.Vertex0].Point.ToUnityVector3(true);
                var vertex1 = navmesh.Vertices[t.Vertex1].Point.ToUnityVector3(true);
                var vertex2 = navmesh.Vertices[t.Vertex2].Point.ToUnityVector3(true);
                Handles.Label((vertex0 + vertex1 + vertex2) / 3.0f, s);
              }
            }
          }
        }

        if (_settings.NavMeshVertexNormals.Enabled ||
            _settings.NavMeshVertexIds.Enabled) {
          for (Int32 v = 0; v < navmesh.Vertices.Length; ++v) {
            // ################## NavMesh Vertex Ids ##################

            if (ShouldDraw(_settings.NavMeshVertexIds, selected, false)) {
              Handles.color = _settings.NavMeshVertexIds.Color;
              Handles.Label(navmesh.Vertices[v].Point.ToUnityVector3(true), v.ToString());
            }

            // ################## NavMesh Vertex Normals ##################

            if (ShouldDraw(_settings.NavMeshVertexNormals, selected, false)) {
              if (navmesh.Vertices[v].Borders.Length >= 2) {
                var normal = NavMeshVertex.CalculateNormal(v, navmesh, navMeshRegionMask);
                if (normal != FPVector3.Zero) {
                  Gizmos.color = _settings.NavMeshVertexNormals.Color;
                  GizmoUtils.DrawGizmoVector(
                    navmesh.Vertices[v].Point.ToUnityVector3(true),
                    navmesh.Vertices[v].Point.ToUnityVector3(true) +
                    normal.ToUnityVector3(true) * gizmosSettings.IconScale * 0.33f,
                    GizmoUtils.DefaultArrowHeadLength * gizmosSettings.IconScale * 0.33f);
                }
              }
            }
          }
        }

        // ################## NavMesh Links ##################

        if (ShouldDraw(_settings.NavMeshLinks, selected, false)) {
          for (Int32 i = 0; i < navmesh.Links.Length; i++) {
            var color = _settings.NavMeshLinks.Color;
            if (navmesh.Links[i].Region.IsSubset(navMeshRegionMask) == false) {
              color = Color.gray;
            }

            Gizmos.color = color;
            GizmoUtils.DrawGizmoVector(
              navmesh.Links[i].Start.ToUnityVector3(),
              navmesh.Links[i].End.ToUnityVector3(),
              GizmoUtils.DefaultArrowHeadLength * gizmosSettings.IconScale);
            GizmoUtils.DrawGizmosCircle(navmesh.Links[i].Start.ToUnityVector3(), 0.1f * gizmosSettings.IconScale, color,
              style: _settings.NavMeshLinks.Style);
            GizmoUtils.DrawGizmosCircle(navmesh.Links[i].End.ToUnityVector3(), 0.1f * gizmosSettings.IconScale, color,
              style: _settings.NavMeshLinks.Style);
          }
        }

        // ################## NavMesh Borders ##################

        if (ShouldDraw(_settings.NavMeshBorders, selected, false)) {
          for (Int32 i = 0; i < navmesh.Borders.Length; i++) {
            Gizmos.color = _settings.NavMeshBorders.Color;
            var b = navmesh.Borders[i];
            if (navmesh.IsBorderActive(i, navMeshRegionMask) == false) {
              // grayed out?
              continue;
            }

            FPVector3 v0 = navmesh.Vertices[b.V0].Point;
            FPVector3 v1 = navmesh.Vertices[b.V1].Point;

            Gizmos.DrawLine(v0.ToUnityVector3(true), v1.ToUnityVector3(true));

            if (_settings.NavMeshBorders.Enabled && _settings.NavMeshBorders.DrawNormals) {
              var normal = b.Normal;
              
              Gizmos.color = _settings.NavMeshBorders.BorderNormalColor;
              var middle = (v0.ToUnityVector3(true) + v1.ToUnityVector3(true)) * 0.5f;
              GizmoUtils.DrawGizmoVector(middle,
                middle + normal.ToUnityVector3(true) * gizmosSettings.IconScale * 0.33f,
                gizmosSettings.IconScale * 0.33f * GizmoUtils.DefaultArrowHeadLength);
            }
          }
        }
      }
#endif
    }
  }
#endif
}

#endregion


#region Assets/Photon/Quantum/Runtime/Gizmos/QuantumGameGizmos.NavMesh.cs

namespace Quantum {
#if UNITY_EDITOR && QUANTUM_ENABLE_AI && !QUANTUM_DISABLE_AI
  using System;
  using System.Collections.Generic;
  using System.Linq;
  using Photon.Deterministic;
  using UnityEditor;
  using UnityEngine;

  public partial class QuantumGameGizmos {
    static unsafe void OnDrawGizmos_NavMesh(Frame frame, QuantumGameGizmosSettings gizmosSettings, GizmoType type) {
      if (frame.MapAssetRef == default) {
        return;
      }
      
      var navmeshList = new List<NavMesh>();
      navmeshList.AddRange(frame.Map.NavMeshes.Values);

      if (frame.DynamicAssetDB.IsEmpty == false) {
        navmeshList.AddRange(frame.DynamicAssetDB.Assets.OfType<NavMesh>().ToList());
      }

      DrawMapNavMesh(frame.Map, navmeshList, *frame.NavMeshRegionMask, gizmosSettings);
      DrawNavigationPaths(frame, gizmosSettings);

      bool tryDrawComponents = _settings.NavMeshPathfinder.Enabled ||
                               _settings.NavMeshSteeringAgent.Enabled ||
                               _settings.NavMeshAvoidanceAgent.Enabled;

      if (tryDrawComponents) {
        DrawRuntimeNavMeshComponents(frame, gizmosSettings);
      }

      if (_settings.NavMeshAvoidanceObstacles.Enabled) {
        DrawObstacles(frame, gizmosSettings);
      }
    }

    private static unsafe void DrawObstacles(Frame frame, QuantumGameGizmosSettings gizmosSettings) {
      foreach (var (entity, navmeshObstacles) in frame.GetComponentIterator<NavMeshAvoidanceObstacle>()) {
        var position = Vector3.zero;

        if (frame.Has<Transform2D>(entity)) {
          position = frame.Unsafe.GetPointer<Transform2D>(entity)->Position.ToUnityVector3();
        } else if (frame.Has<Transform3D>(entity)) {
          position = frame.Unsafe.GetPointer<Transform3D>(entity)->Position.ToUnityVector3();
        }

        var style = _settings.NavMeshAvoidanceAgent.Style;

        GizmoUtils.DrawGizmosCircle(
          position,
          navmeshObstacles.Radius.AsFloat,
          _settings.NavMeshAvoidanceAgent.Color,
          style: style
        );

        if (navmeshObstacles.Velocity != FPVector2.Zero) {
          GizmoUtils.DrawGizmoVector(
            position,
            position + navmeshObstacles.Velocity.XOY.ToUnityVector3().normalized,
            gizmosSettings.IconScale * _settings.NavMeshAvoidanceAgent.Scale
          );
        }
      }
    }


    private static float GetAgentRadius(NavMesh current) {
      var agentRadius = 0.25f;

      if (current != null) {
        agentRadius = current.MinAgentRadius.AsFloat;
      }
      
      return agentRadius;
    }

    private static void DrawPathfinder(Frame frame, QuantumGameGizmosSettings gizmosSettings, NavMeshPathfinder agent,
      NavMesh navMesh = null) {
      var scale = _settings.NavMeshPathfinder.Scale * gizmosSettings.IconScale;

      if (_settings.ScaleComponentsWithAgentRadius) {
        scale *= GetAgentRadius(navMesh);
      }

      // Draw target and internal target
      GizmoUtils.DrawGizmosCircle(
        agent.InternalTarget.ToUnityVector3(),
        scale,
        _settings.NavMeshPathfinder.Color,
        style: _settings.NavMeshPathfinder.Style
      );

      if (agent.Target != agent.InternalTarget) {
        var desaturated = _settings.NavMeshPathfinder.Color.Desaturate();

        GizmoUtils.DrawGizmosCircle(
          agent.Target.ToUnityVector3(),
          scale * 0.5f,
          desaturated,
          style: _settings.NavMeshPathfinder.Style
        );

        Gizmos.color = desaturated;

        Gizmos.DrawLine(
          agent.Target.ToUnityVector3(),
          agent.InternalTarget.ToUnityVector3()
        );
      }

      if (frame == null)
        return;

      // Draw waypoints
      for (int i = 0; i < agent.WaypointCount; i++) {
        var waypoint = agent.GetWaypoint(frame, i);
        var waypointFlags = agent.GetWaypointFlags(frame, i);
        if (i > 0) {
          var lastWaypoint = agent.GetWaypoint(frame, i - 1);
          Gizmos.color = _settings.NavMeshPathfinder.Color;
          Gizmos.DrawLine(lastWaypoint.ToUnityVector3(), waypoint.ToUnityVector3());
        }

        GizmoUtils.DrawGizmosCircle(waypoint.ToUnityVector3(), scale * 0.75f,
          _settings.NavMeshPathfinder.Color, style: _settings.NavMeshPathfinder.Style);
        if (i == agent.WaypointIndex) {
          GizmoUtils.DrawGizmosCircle(waypoint.ToUnityVector3(), scale * 0.8f, Color.black,
            style: QuantumGizmoStyle.FillDisabled);
        }
      }
    }

    private static void DrawNavigationPaths(Frame frame, QuantumGameGizmosSettings gizmosSettings) {
      if (frame.Navigation == null)
        return;

      // Iterate though task contexts:
      var threadCount = frame.Context.TaskContext.ThreadCount;
      for (int t = 0; t < threadCount; t++) {
        // Iterate through path finders:
        var pf = frame.Navigation.GetDebugInformation(t).Item0;
        if (pf.RawPathSize >= 2) {
          if (_settings.PathfinderRawPath.Enabled) {
            for (int i = 0; i < pf.RawPathSize; i++) {
              GizmoUtils.DrawGizmosCircle(
                pf.RawPath[i].Point.ToUnityVector3(true),
                0.1f * gizmosSettings.IconScale,
                pf.RawPath[i].Link >= 0 ? Color.black : _settings.PathfinderRawPath.Color
              );
              if (i > 0) {
                Gizmos.color = pf.RawPath[i].Link >= 0 &&
                               pf.RawPath[i].Link == pf.RawPath[i - 1].Link
                  ? Color.black
                  : _settings.PathfinderRawPath.Color;

                Gizmos.DrawLine(
                  pf.RawPath[i].Point.ToUnityVector3(true),
                  pf.RawPath[i - 1].Point.ToUnityVector3(true)
                );
              }
            }
          }

          if (_settings.PathfinderRawTrianglePath.Enabled) {
            var nmGuid = frame.Navigation.GetDebugInformation(t).Item1;
            if (!string.IsNullOrEmpty(nmGuid)) {
              QuantumUnityDB.TryGetGlobalAsset(nmGuid, out Quantum.NavMesh nm);
              for (int i = 0; i < pf.RawPathSize; i++) {
                var triangleIndex = pf.RawPath[i].Index;
                if (triangleIndex >= 0) {
                  var vertex0 = nm.Vertices[nm.Triangles[triangleIndex].Vertex0].Point.ToUnityVector3(true);
                  var vertex1 = nm.Vertices[nm.Triangles[triangleIndex].Vertex1].Point.ToUnityVector3(true);
                  var vertex2 = nm.Vertices[nm.Triangles[triangleIndex].Vertex2].Point.ToUnityVector3(true);
                  var color = _settings.PathfinderRawTrianglePath.Color;
                  GizmoUtils.DrawGizmosTriangle(vertex0, vertex1, vertex2,
                    gizmosSettings.GetSelectedColor(color, true));
                  Handles.color = color;
                  Handles.lighting = true;
                  Handles.DrawAAConvexPolygon(vertex0, vertex1, vertex2);
                }
              }
            }
          }

          // Draw funnel on top of raw path
          if (_settings.PathfinderFunnel.Enabled) {
            for (Int32 i = 0; i < pf.PathSize; i++) {
              GizmoUtils.DrawGizmosCircle(pf.Path[i].Point.ToUnityVector3(true), 0.05f * gizmosSettings.IconScale,
                pf.Path[i].Link >= 0 ? Color.green * 0.5f : Color.green);
              if (i > 0) {
                var color = _settings.PathfinderFunnel.Color;
                var altColor = _settings.PathfinderFunnel.TransparentColor;

                Gizmos.color = pf.Path[i].Link >= 0 && pf.Path[i].Link == pf.Path[i - 1].Link ? altColor : color;
                Gizmos.DrawLine(pf.Path[i].Point.ToUnityVector3(true), pf.Path[i - 1].Point.ToUnityVector3(true));
              }
            }
          }
        }
      }
    }

    /// <summary>
    ///   Creates a Unity mesh from the navmesh data and renders it as a gizmo. Uses submeshes to draw main mesh, regions and
    ///   deactivated regions in different colors.
    ///   The meshes are cached in a static dictionary by their NavMesh.Name. Call InvalidateGizmos() to reset the cache
    ///   manually.
    ///   New meshes are created when the region mask changed.
    /// </summary>
    public static void CreateAndDrawNavMeshGizmo(NavMesh navmesh, NavMeshRegionMask regionMask) {
      var mesh = CreateGizmoMesh(navmesh, regionMask);

      DrawNavMeshGizmoMesh(mesh,
        _settings.NavMesh.Color,
        _settings.NavMesh.RegionColor
      );
    }

    private static Mesh CreateGizmoMesh(NavMesh navmesh, NavMeshRegionMask regionMask) {
      _navmeshGizmoMap ??= new Dictionary<string, GizmoNavmeshData>();

      if (!_navmeshGizmoMap.TryGetValue(navmesh.Name, out GizmoNavmeshData gizmoNavmeshData) ||
          gizmoNavmeshData.CurrentRegionMask.Equals(regionMask) == false ||
          gizmoNavmeshData.GizmoMesh == null) {
        var mesh = new Mesh { subMeshCount = 3 };

#if QUANTUM_XY
        mesh.vertices = navmesh.Vertices.Select(x => new Vector3(x.Point.X.AsFloat, x.Point.Z.AsFloat, x.Point.Y.AsFloat)).ToArray();
#else
        mesh.vertices = navmesh.Vertices.Select(x => x.Point.ToUnityVector3()).ToArray();
#endif

        mesh.SetTriangles(
          navmesh.Triangles.SelectMany(x =>
            x.Regions.IsMainArea && x.Regions.IsSubset(regionMask)
              ? new int[] { x.Vertex0, x.Vertex1, x.Vertex2 }
              : Array.Empty<int>()).ToArray(), 0);
        mesh.SetTriangles(
          navmesh.Triangles.SelectMany(x =>
            x.Regions.HasValidNoneMainRegion && x.Regions.IsSubset(regionMask)
              ? new int[] { x.Vertex0, x.Vertex1, x.Vertex2 }
              : Array.Empty<int>()).ToArray(), 1);
        mesh.SetTriangles(
          navmesh.Triangles.SelectMany(x =>
              !x.Regions.IsSubset(regionMask) ? new int[] { x.Vertex0, x.Vertex1, x.Vertex2 } : Array.Empty<int>())
            .ToArray(), 2);
        mesh.RecalculateNormals();

        gizmoNavmeshData = new GizmoNavmeshData() { GizmoMesh = mesh, CurrentRegionMask = regionMask };
        _navmeshGizmoMap[navmesh.Name] = gizmoNavmeshData;
      }

      return gizmoNavmeshData.GizmoMesh;
    }

    private static void DrawNavMeshGizmoMesh(Mesh mesh, Color color, Color regionColor) {
      var originalColor = Gizmos.color;

      bool selected = false;

      var mapData = GetMapData();

      if (mapData != null) {
        selected = Selection.activeGameObject == _mapData.gameObject;
      }

      bool shouldDraw = ShouldDraw(_settings.NavMesh, selected, false);

      if (_settings.NavMesh.Enabled && shouldDraw) {
        Gizmos.color = color;

        if (_settings.NavMesh.Style.DisableFill == false) {
          Gizmos.DrawMesh(mesh, 0);
        }

        Gizmos.color = Gizmos.color.Alpha(Gizmos.color.a * 0.75f);
        Gizmos.DrawWireMesh(mesh, 0);
        
        Gizmos.color = regionColor;

        if (_settings.NavMesh.Style.DisableFill == false) {
          Gizmos.DrawMesh(mesh, 1);
          Gizmos.color = Gizmos.color.Alpha(Gizmos.color.a * 0.75f);
        }

        Gizmos.DrawWireMesh(mesh, 1);

        var greyValue = (Gizmos.color.r + Gizmos.color.g + Gizmos.color.b) / 3.0f;
        Gizmos.color = new Color(greyValue, greyValue, greyValue, Gizmos.color.a);
        Gizmos.DrawMesh(mesh, 2);
        Gizmos.DrawWireMesh(mesh, 2);
        Gizmos.color = originalColor;
      }
    }

    private static unsafe void DrawRuntimeNavMeshComponents(
      Frame frame,
      QuantumGameGizmosSettings gizmosSettings) {
      NavMesh current = null;

      var evu = GetEntityViewUpdater();

      foreach (var (entity, agent) in frame.GetComponentIterator<NavMeshPathfinder>()) {
        var position = Vector3.zero;
        if (frame.Has<Transform2D>(entity)) {
          position = frame.Unsafe.GetPointer<Transform2D>(entity)->Position.ToUnityVector3();
          if (frame.Has<Transform2DVertical>(entity)) {
            position.y = frame.Unsafe.GetPointer<Transform2DVertical>(entity)->Position.AsFloat;
          }
        } else if (frame.Has<Transform3D>(entity)) {
          position = frame.Unsafe.GetPointer<Transform3D>(entity)->Position.ToUnityVector3();
        }

        var config = frame.FindAsset<NavMeshAgentConfig>(agent.ConfigId);

        if (current == null || current.Identifier.Guid != agent.NavMeshGuid) {
          // cache the asset, it's likely other agents use the same 
          QuantumUnityDB.TryGetGlobalAsset(agent.NavMeshGuid, out current);
        }

        var agentRadius = GetAgentRadius(current);

        bool selected = false;

        if (evu != null) {
          var view = evu.GetView(entity);

          if (view != null) {
            selected = Selection.activeGameObject == view.gameObject;
          }
        }

        if (_settings.NavMeshPathfinder.Enabled &&
            agent.IsActive &&
            ShouldDraw(_settings.NavMeshPathfinder, selected, false)) {
          DrawPathfinder(frame, gizmosSettings, agent, current);
        }

        if (_settings.NavMeshSteeringAgent.Enabled &&
            ShouldDraw(_settings.NavMeshSteeringAgent, selected, false)) {
          var scale = _settings.NavMeshSteeringAgent.Scale * gizmosSettings.IconScale;

          if (_settings.ScaleComponentsWithAgentRadius) {
            scale *= agentRadius;
          }

          if (frame.Has<NavMeshSteeringAgent>(entity)) {
            var steeringAgent = frame.Get<NavMeshSteeringAgent>(entity);
            Gizmos.color = _settings.NavMeshSteeringAgent.Color;
            GizmoUtils.DrawGizmoVector(
              position,
              position + steeringAgent.Velocity.XOY.ToUnityVector3().normalized,
              scale);
          }

          if (config.AvoidanceType != Navigation.AvoidanceType.None && frame.Has<NavMeshAvoidanceAgent>(entity)) {
            GizmoUtils.DrawGizmosCircle(
              position,
              config.AvoidanceRadius.AsFloat,
              _settings.NavMeshSteeringAgent.Color,
              style:
              _settings.NavMeshSteeringAgent.Style
            );
          }

          GizmoUtils.DrawGizmosCircle(
            position,
            agentRadius,
            agent.IsActive
              ? _settings.NavMeshSteeringAgent.Color
              : _settings.NavMeshSteeringAgent.InactiveColor,
            style: _settings.NavMeshSteeringAgent.Style
          );
        }

        if (_settings.NavMeshAvoidanceAgent.Enabled &&
            ShouldDraw(_settings.NavMeshAvoidanceAgent, selected, false)) {
          if (config.AvoidanceType != Navigation.AvoidanceType.None && frame.Has<NavMeshAvoidanceAgent>(entity)) {
            GizmoUtils.DrawGizmosCircle(
              position,
              config.AvoidanceRadius.AsFloat,
              _settings.NavMeshAvoidanceAgent.Color,
              style: _settings.NavMeshAvoidanceAgent.Style
            );

            var avoidanceRange = frame.SimulationConfig.Navigation.AvoidanceRange;

            GizmoUtils.DrawGizmosCircle(
              position,
              avoidanceRange.AsFloat,
              _settings.NavMeshAvoidanceAgent.Color,
              style: QuantumGizmoStyle.FillDisabled
            );
          }
        }
      }
    }
  }
#endif
}

#endregion


#region Assets/Photon/Quantum/Runtime/Gizmos/QuantumGameGizmos.Physics2D.cs

namespace Quantum {
#if UNITY_EDITOR && QUANTUM_ENABLE_PHYSICS2D && !QUANTUM_DISABLE_PHYSICS2D
  using System;
  using Photon.Deterministic;
  using Physics2D;
  using UnityEditor;
  using UnityEngine;
  using Joint = Physics2D.Joint;

  public partial class QuantumGameGizmos {
    private static unsafe void DrawRuntimePhysicsComponents_2D(QuantumGameGizmosSettings settings, Frame frame) {
      // ################## Components: PhysicsCollider2D ##################
      foreach (var (handle, collider) in frame.GetComponentIterator<PhysicsCollider2D>()) {
        var entry = _settings.GetEntryForPhysicsEntity2D(frame, handle);
        if (ShouldDraw(entry, false, false)) {
          DrawCollider2DGizmo(
            frame,
            handle,
            &collider,
            entry.Color,
            entry.Style
          );
        }
      }

      // ################## Components: CharacterController2D ##################
      if (ShouldDraw(_settings.CharacterController, false, false)) {
        foreach (var (entity, cc) in frame.GetComponentIterator<CharacterController2D>()) {
          if (frame.Unsafe.TryGetPointer(entity, out Transform2D* t) &&
              frame.TryFindAsset(cc.Config, out CharacterController2DConfig config)) {
            DrawCharacterController2DGizmo(
              t->Position.ToUnityVector3(),
              config,
              _settings.CharacterController.Color,
              _settings.AsleepColliders.Color,
              _settings.CharacterController.DisableFill
            );
          }
        }
      }

      // ################## Components: PhysicsJoints2D ##################
      if (ShouldDraw(_settings.PhysicsJoints, false, false)) {
        foreach (var (handle, jointsComponent) in frame.Unsafe.GetComponentBlockIterator<PhysicsJoints2D>()) {
          if (frame.Unsafe.TryGetPointer(handle, out Transform2D* transform) &&
              jointsComponent->TryGetJoints(frame, out var jointsBuffer, out var jointsCount)) {
            for (var i = 0; i < jointsCount; i++) {
              var curJoint = jointsBuffer + i;
              frame.Unsafe.TryGetPointer(curJoint->ConnectedEntity, out Transform2D* connectedTransform);

              DrawGizmosJoint2D(
                curJoint,
                transform,
                connectedTransform,
                selected: false,
                settings,
                _settings.PhysicsJoints.DisableFill
              );
            }
          }
        }
      }
    }

    private static unsafe void DrawCharacterController2DGizmo(Vector3 position, CharacterController2DConfig config,
      Color radiusColor, Color extentsColor, bool disableFill) {
      var style = disableFill ? QuantumGizmoStyle.FillDisabled : default;

      GizmoUtils.DrawGizmosCircle(position + config.Offset.ToUnityVector3(),
        config.Radius.AsFloat, radiusColor, style: style);
      GizmoUtils.DrawGizmosCircle(position + config.Offset.ToUnityVector3(),
        config.Radius.AsFloat + config.Extent.AsFloat, extentsColor, style: style);
    }

    private static unsafe void DrawCollider2DGizmo(Frame frame, EntityRef handle, PhysicsCollider2D* collider,
      Color color, QuantumGizmoStyle style) {
      if (!frame.Unsafe.TryGetPointer(handle, out Transform2D* t)) {
        return;
      }

      var hasTransformVertical = frame.Unsafe.TryGetPointer<Transform2DVertical>(handle, out var tVertical);

      // Set 3d position of 2d object to simulate the vertical offset.
      var height = 0.0f;

#if QUANTUM_XY
      if (hasTransformVertical) {
        height = -tVertical->Height.AsFloat;
      }
#else
      if (hasTransformVertical) {
        height = tVertical->Height.AsFloat;
      }
#endif

      if (collider->Shape.Type == Shape2DType.Compound) {
        DrawCompoundShape2D(frame, &collider->Shape, t, tVertical, color, height, style);
      } else {
        var pos = t->Position.ToUnityVector3();
        var rot = t->Rotation.ToUnityQuaternion();

#if QUANTUM_XY
        if (hasTransformVertical) {
          pos.z = -tVertical->Position.AsFloat;
        }
#else
        if (hasTransformVertical) {
          pos.y = tVertical->Position.AsFloat;
        }
#endif

        DrawShape2DGizmo(collider->Shape, pos, rot, color, height, frame, style);
      }
    }

    /// <inheritdoc cref="QuantumGameGizmos.DrawShape3DGizmo"/> 
    public static unsafe void DrawShape2DGizmo(Shape2D s, Vector3 pos, Quaternion rot, Color color, float height,
      Frame currentFrame = null, QuantumGizmoStyle style = default) {
      var localOffset = s.LocalTransform.Position.ToUnityVector3();
      var localRotation = s.LocalTransform.Rotation.ToUnityQuaternion();

      pos += rot * localOffset;
      rot = rot * localRotation;

      switch (s.Type) {
        case Shape2DType.Circle:
          GizmoUtils.DrawGizmosCircle(pos, s.Circle.Radius.AsFloat, color, height: height, style: style);
          break;

        case Shape2DType.Box:
          var size = s.Box.Extents.ToUnityVector3() * 2.0f;
#if QUANTUM_XY
          size.z = height;
          pos.z += height * 0.5f;
#else
          size.y = height;
          pos.y += height * 0.5f;
#endif
          GizmoUtils.DrawGizmosBox(pos, size, color, rotation: rot, style: style);

          break;

        //TODO: check for the height
        case Shape2DType.Capsule:
          GizmoUtils.DrawGizmosCapsule2D(pos, s.Capsule.Radius.AsFloat, s.Capsule.Extent.AsFloat, color, rotation: rot,
            style: style);
          break;

        case Shape2DType.Polygon:
          PolygonCollider p;
          if (currentFrame != null) {
            p = currentFrame.FindAsset(s.Polygon.AssetRef);
          } else {
            QuantumUnityDB.TryGetGlobalAsset(s.Polygon.AssetRef, out p);
          }

          if (p != null) {
            GizmoUtils.DrawGizmoPolygon2D(pos, rot, p.Vertices, height, color, style: style);
          }

          break;


        case Shape2DType.Edge:
          var extent = rot * Vector3.right * s.Edge.Extent.AsFloat;
          GizmoUtils.DrawGizmosEdge(pos - extent, pos + extent, height, color);
          break;
      }
    }

    private static unsafe void DrawCompoundShape2D(Frame f, Shape2D* compoundShape, Transform2D* transform,
      Transform2DVertical* transformVertical, Color color, float height, QuantumGizmoStyle style = default) {
      Debug.Assert(compoundShape->Type == Shape2DType.Compound);

      if (compoundShape->Compound.GetShapes(f, out var shapesBuffer, out var count)) {
        for (var i = 0; i < count; i++) {
          var shape = shapesBuffer + i;

          if (shape->Type == Shape2DType.Compound) {
            DrawCompoundShape2D(f, shape, transform, transformVertical, color, height, style);
          } else {
            var pos = transform->Position.ToUnityVector3();
            var rot = transform->Rotation.ToUnityQuaternion();

#if QUANTUM_XY
            if (transformVertical != null) {
              pos.z = -transformVertical->Position.AsFloat;
            }
#else
            if (transformVertical != null) {
              pos.y = transformVertical->Position.AsFloat;
            }
#endif

            DrawShape2DGizmo(*shape, pos, rot, color, height, f, style);
          }
        }
      }
    }

    private static unsafe void DrawGizmosJoint2D(
      Joint* joint,
      Transform2D* jointTransform,
      Transform2D* connectedTransform,
      bool selected,
      QuantumGameGizmosSettings gizmosSettings,
      bool disableFill = true) {
      if (joint->Type == JointType.None) {
        return;
      }

      var param = default(QuantumGizmosJointInfo);
      param.Selected = selected;
      param.JointRot = jointTransform->Rotation.ToUnityQuaternion();
      param.AnchorPos = jointTransform->TransformPoint(joint->Anchor).ToUnityVector3();

      switch (joint->Type) {
        case JointType.DistanceJoint:
          param.Type = QuantumGizmosJointInfo.GizmosJointType.DistanceJoint2D;
          param.MinDistance = joint->DistanceJoint.MinDistance.AsFloat;
          param.MaxDistance = joint->DistanceJoint.MaxDistance.AsFloat;
          break;

        case JointType.SpringJoint:
          param.Type = QuantumGizmosJointInfo.GizmosJointType.SpringJoint2D;
          param.MinDistance = joint->SpringJoint.Distance.AsFloat;
          break;

        case JointType.HingeJoint:
          param.Type = QuantumGizmosJointInfo.GizmosJointType.HingeJoint2D;
          param.RelRotRef = Quaternion.Inverse(param.JointRot);
          param.UseAngleLimits = joint->HingeJoint.UseAngleLimits;
          param.LowerAngle = (joint->HingeJoint.LowerLimitRad * FP.Rad2Deg).AsFloat;
          param.UpperAngle = (joint->HingeJoint.UpperLimitRad * FP.Rad2Deg).AsFloat;
          break;
      }

      if (connectedTransform == null) {
        param.ConnectedRot = Quaternion.identity;
        param.ConnectedPos = joint->ConnectedAnchor.ToUnityVector3();
      } else {
        param.ConnectedRot = connectedTransform->Rotation.ToUnityQuaternion();
        param.ConnectedPos = connectedTransform->TransformPoint(joint->ConnectedAnchor).ToUnityVector3();
        param.RelRotRef = (param.ConnectedRot * param.RelRotRef).normalized;
      }

#if QUANTUM_XY
      param.Axis = Vector3.back;
#else
      param.Axis = Vector3.up;
#endif

      DrawGizmosJointInternal(ref param, gizmosSettings, disableFill);
    }

    [DrawGizmo(GizmoType.Selected | GizmoType.Active | GizmoType.NonSelected)]
    static void DrawGizmos_QuantumStaticPolygonCollider2D(QuantumStaticPolygonCollider2D behaviour,
      GizmoType gizmoType) {
      bool selected = gizmoType.HasFlag(GizmoType.Selected);

      if (Application.isPlaying == false) {
        behaviour.UpdateFromSourceCollider(updateVertices: false);
      }

      var gs = QuantumGameGizmosSettingsScriptableObject.Global.Settings;

      if (!ShouldDraw(_settings.StaticColliders, selected, false)) {
        return;
      }

      if (behaviour.BakeAsStaticEdges2D) {
        for (var i = 0; i < behaviour.Vertices.Length; i++) {
          var vertex = behaviour.Vertices[i];
          var localEnd = behaviour.Vertices[(i + 1) % behaviour.Vertices.Length];

          QuantumStaticEdgeCollider2D.GetEdgeGizmosSettings(
            behaviour.transform,
            behaviour.PositionOffset,
            behaviour.RotationOffset,
            vertex,
            localEnd,
            behaviour.Height,
            out var start,
            out var end,
            out var edgeHeight
          );

          GizmoUtils.DrawGizmosEdge(
            start,
            end,
            edgeHeight,
            gs.GetSelectedColor(_settings.StaticColliders.Color, selected),
            style: _settings.StaticColliders.Style
          );
        }

        return;
      }

      var t = behaviour.transform;

#if QUANTUM_XY
      var verticalScale = -t.lossyScale.z;
#else
      var verticalScale = t.lossyScale.y;
#endif

      var heightScaled = behaviour.Height.AsFloat * verticalScale;
      var matrix = Matrix4x4.TRS(
        t.TransformPoint(behaviour.PositionOffset.ToUnityVector3()),
        t.rotation * behaviour.RotationOffset.FlipRotation().ToUnityQuaternionDegrees(),
        t.lossyScale);
      GizmoUtils.DrawGizmoPolygon2D(matrix, behaviour.Vertices, heightScaled, selected,
        gs.GetSelectedColor(_settings.StaticColliders.Color, selected),
        _settings.StaticColliders.Style);
    }

    [DrawGizmo(GizmoType.Selected | GizmoType.Active | GizmoType.NonSelected)]
    static void DrawGizmos_QuantumStaticCapsuleCollider2D(QuantumStaticCapsuleCollider2D behaviour,
      GizmoType gizmoType) {
      bool selected = gizmoType.HasFlag(GizmoType.Selected);
      var transform = behaviour.transform;

      if (Application.isPlaying == false) {
        behaviour.UpdateFromSourceCollider();
      }

      if (!ShouldDraw(_settings.StaticColliders, selected, false)) {
        return;
      }

      var scale = transform.lossyScale;
      var size = behaviour.Size;

#if QUANTUM_XY
      var radius = (FPMath.Clamp(size.X,0,size.X) / FP._2).AsFloat * scale.x;
      var height = (FPMath.Clamp(size.Y - (size.X / FP._2 * FP._2),FP._0, size.Y) / FP._2).AsFloat * scale.y;
#else
      var radius = (FPMath.Clamp(size.X, 0, size.X) / FP._2).AsFloat * scale.x;
      var height = (FPMath.Clamp(size.Y - (size.X / FP._2 * FP._2), FP._0, size.Y) / FP._2).AsFloat * scale.z;
#endif

      GizmoUtils.DrawGizmosCapsule2D(
        transform.TransformPoint(behaviour.PositionOffset.ToUnityVector2()),
        radius,
        height,
        _settings.GetSelectedColor(_settings.StaticColliders.Color, selected),
        style: _settings.StaticColliders.Style
      );
    }

    [DrawGizmo(GizmoType.Selected | GizmoType.Active | GizmoType.NonSelected)]
    static void DrawGizmos_QuantumStaticBoxCollider2D(QuantumStaticBoxCollider2D behaviour,
      GizmoType gizmoType) {
      bool selected = gizmoType.HasFlag(GizmoType.Selected);

      if (Application.isPlaying == false) {
        behaviour.UpdateFromSourceCollider();
      }

      if (!ShouldDraw(_settings.StaticColliders, selected, false)) {
        return;
      }

      var size = behaviour.Size.ToUnityVector3();
      var height = behaviour.Height;
      var offset = Vector3.zero;

#if QUANTUM_XY
      size.z = -height.AsFloat;
      offset.z = size.z / 2.0f;
#else
      size.y = height.AsFloat;
      offset.y = size.y / 2.0f;
#endif

      var t = behaviour.transform;
      var tLossyScale = t.lossyScale;

      var matrix = Matrix4x4.TRS(
        t.TransformPoint(behaviour.PositionOffset.ToUnityVector3()),
        t.rotation * behaviour.RotationOffset.FlipRotation().ToUnityQuaternionDegrees(),
        tLossyScale) * Matrix4x4.Translate(offset);

      GizmoUtils.DrawGizmosBox(
        matrix,
        size,
        _settings.GetSelectedColor(_settings.StaticColliders.Color, selected),
        style: _settings.StaticColliders.Style
      );
    }

    [DrawGizmo(GizmoType.Selected | GizmoType.Active | GizmoType.NonSelected)]
    static void DrawGizmos_QuantumStaticEdgeCollider2D(QuantumStaticEdgeCollider2D behaviour,
      GizmoType gizmoType) {
      bool selected = gizmoType.HasFlag(GizmoType.Selected);

      if (Application.isPlaying == false) {
        behaviour.UpdateFromSourceCollider();
      }

      if (!ShouldDraw(_settings.StaticColliders, selected, false)) {
        return;
      }

      var transform = behaviour.transform;

      QuantumStaticEdgeCollider2D.GetEdgeGizmosSettings(
        transform,
        behaviour.PositionOffset,
        behaviour.RotationOffset,
        behaviour.VertexA,
        behaviour.VertexB,
        behaviour.Height,
        out var start,
        out var end,
        out var height);

      GizmoUtils.DrawGizmosEdge(
        start,
        end,
        height,
        _settings.GetSelectedColor(_settings.StaticColliders.Color, selected),
        style: _settings.StaticColliders.Style
      );
    }

    [DrawGizmo(GizmoType.Selected | GizmoType.Active | GizmoType.NonSelected)]
    static void DrawGizmos_QuantumStaticCircleCollider2D(QuantumStaticCircleCollider2D behaviour,
      GizmoType gizmoType) {
      bool selected = gizmoType.HasFlag(GizmoType.Selected);
      var transform = behaviour.transform;

      if (Application.isPlaying == false) {
        behaviour.UpdateFromSourceCollider();
      }

      if (!ShouldDraw(_settings.StaticColliders, selected, false)) {
        return;
      }

      var lossyScale = transform.lossyScale;
      var lossyScale2D = lossyScale.ToFPVector2();

#if QUANTUM_XY
      var heightScale = -lossyScale.z;
#else
      var heightScale = lossyScale.y;
#endif

      var heightScaled = behaviour.Height.AsFloat * heightScale;
      var radiusScaled = (behaviour.Radius * FPMath.Max(lossyScale2D.X, lossyScale2D.Y)).AsFloat;

      var t = transform;

      GizmoUtils.DrawGizmosCircle(
        t.TransformPoint(behaviour.PositionOffset.ToUnityVector3()),
        radiusScaled,
        _settings.GetSelectedColor(_settings.StaticColliders.Color, selected),
        heightScaled,
        style: _settings.StaticColliders.Style
      );
    }

    [DrawGizmo(GizmoType.Selected | GizmoType.Active | GizmoType.NonSelected)]
    static void DrawGizmos_JointPrototype2D(QPrototypePhysicsJoints2D behaviour, GizmoType gizmoType) {
      bool selected = gizmoType.HasFlag(GizmoType.Selected);

      if (!ShouldDraw(_settings.PhysicsJoints, selected)) {
        return;
      }

      var entity = behaviour.GetComponent<QuantumEntityPrototype>();

      if (entity == null || behaviour.Prototype.JointConfigs == null) {
        return;
      }

      FPMathUtils.LoadLookupTables();

      foreach (var prototype in behaviour.Prototype.JointConfigs) {
        if (prototype.JointType == JointType.None) {
          return;
        }

        QuantumGizmosJointInfo info;

        switch (prototype.JointType) {
          case JointType.DistanceJoint:
            info.Type = QuantumGizmosJointInfo.GizmosJointType.DistanceJoint2D;
            info.MinDistance = prototype.MinDistance.AsFloat;
            break;

          case JointType.SpringJoint:
            info.Type = QuantumGizmosJointInfo.GizmosJointType.SpringJoint2D;
            info.MinDistance = prototype.Distance.AsFloat;
            break;

          case JointType.HingeJoint:
            info.Type = QuantumGizmosJointInfo.GizmosJointType.HingeJoint2D;
            info.MinDistance = prototype.Distance.AsFloat;
            break;

          default:
            throw new NotSupportedException($"Unsupported joint type {prototype.JointType}");
        }

        var transform = behaviour.transform;

        info.Selected = selected;
        info.JointRot = transform.rotation;
        info.RelRotRef = Quaternion.Inverse(info.JointRot);
        info.AnchorPos = transform.position + info.JointRot * prototype.Anchor.ToUnityVector3();
        info.MaxDistance = prototype.MaxDistance.AsFloat;
        info.UseAngleLimits = prototype.UseAngleLimits;
        info.LowerAngle = prototype.LowerAngle.AsFloat;
        info.UpperAngle = prototype.UpperAngle.AsFloat;

        if (prototype.ConnectedEntity == null) {
          info.ConnectedRot = Quaternion.identity;
          info.ConnectedPos = prototype.ConnectedAnchor.ToUnityVector3();
        } else {
          info.ConnectedRot = prototype.ConnectedEntity.transform.rotation;
          info.ConnectedPos = prototype.ConnectedEntity.transform.position +
                              info.ConnectedRot * prototype.ConnectedAnchor.ToUnityVector3();
          info.RelRotRef = info.ConnectedRot * info.RelRotRef;
        }

#if QUANTUM_XY
        info.Axis = Vector3.back;
#else
        info.Axis = Vector3.up;
#endif

        DrawGizmosJointInternal(ref info, _settings, _settings.PhysicsJoints.DisableFill);
      }
    }
  }
#endif
}

#endregion


#region Assets/Photon/Quantum/Runtime/Gizmos/QuantumGameGizmos.Physics3D.cs

namespace Quantum {
#if UNITY_EDITOR && QUANTUM_ENABLE_PHYSICS3D && !QUANTUM_DISABLE_PHYSICS3D
  using System;
  using Photon.Deterministic;
  using Physics3D;
  using UnityEditor;
  using UnityEngine;

  public partial class QuantumGameGizmos {
    private static StaticMeshColliderGizmoData GetOrCreateGizmoData(MonoBehaviour behaviour) {
      if (!_meshGizmoData.TryGetValue(behaviour, out var data)) {
        data = new StaticMeshColliderGizmoData();
        CreateStaticMeshData(behaviour);
      }

      return data;
    }

    private static void CreateStaticMeshData(MonoBehaviour behaviour) {
      var data = new StaticMeshColliderGizmoData();

      MeshTriangleVerticesCcw meshTriangles = null;
      var mapData = GetMapData();

      if (mapData.StaticCollider3DReferences.Contains(behaviour) == false) {
        // don't draw gizmo if the collider has not been baked into the map
        _meshGizmoData.Remove(behaviour);
        return;
      }

      // just read current mesh data, don't bake it
      switch (behaviour) {
        case QuantumStaticMeshCollider3D collider3D:
          meshTriangles = collider3D.CreateMeshTriangles();
          break;
        case QuantumStaticTerrainCollider3D terrainCollider3D:
          meshTriangles = terrainCollider3D.Asset.CreateMeshTriangles();
          break;
      }

      if (meshTriangles is { Triangles: null }) {
        return;
      }

      ComputeTriangleGizmos(meshTriangles, ref data.TrianglePoints, ref data.TriangleSegments);
      ComputeNormalGizmos(meshTriangles, ref data.NormalPoints);

      _meshGizmoData[behaviour] = data;
    }

    private static void ComputeTriangleGizmos(
      MeshTriangleVerticesCcw mesh,
      ref Vector3[] triPoints,
      ref int[] triSegments) {
      var gizmosTrianglePointsCount = mesh.Vertices.Length;
      if (triPoints == null || triPoints.Length < gizmosTrianglePointsCount) {
        triPoints = new Vector3[gizmosTrianglePointsCount];
      }

      for (int i = 0; i < mesh.Vertices.Length; i++) {
        triPoints[i] = mesh.Vertices[i].ToUnityVector3();
      }

      var gizmosTriangleSegmentsCount = mesh.Triangles.Length * 6;
      if (triSegments == null || triSegments.Length != gizmosTriangleSegmentsCount) {
        triSegments = new int[gizmosTriangleSegmentsCount];
      }

      for (int i = 0; i < mesh.Triangles.Length; i++) {
        var tri = mesh.Triangles[i];
        var segmentIdx = 6 * i;

        triSegments[segmentIdx++] = tri.VertexA;
        triSegments[segmentIdx++] = tri.VertexB;

        triSegments[segmentIdx++] = tri.VertexB;
        triSegments[segmentIdx++] = tri.VertexC;

        triSegments[segmentIdx++] = tri.VertexC;
        triSegments[segmentIdx] = tri.VertexA;
      }
    }

    private static void ComputeNormalGizmos(MeshTriangleVerticesCcw mesh, ref Vector3[] normalPoints) {
      var gizmosNormalsPointsCount = mesh.Triangles.Length * 2;
      if (normalPoints == null || normalPoints.Length < gizmosNormalsPointsCount) {
        normalPoints = new Vector3[gizmosNormalsPointsCount];
      }

      for (int i = 0; i < mesh.Triangles.Length; i++) {
        var tri = mesh.Triangles[i];

        var vA = mesh.Vertices[tri.VertexA].ToUnityVector3();
        var vB = mesh.Vertices[tri.VertexB].ToUnityVector3();
        var vC = mesh.Vertices[tri.VertexC].ToUnityVector3();

        var center = (vA + vB + vC) / 3f;
        var normal = Vector3.Cross(vB - vA, vA - vC).normalized;

        var pointIdx = 2 * i;
        normalPoints[pointIdx++] = center;
        normalPoints[pointIdx] = center + normal;
      }
    }

    private static unsafe void DrawRuntimePhysicsComponents_3D(QuantumGameGizmosSettings settings, Frame frame) {
      // ################## Components: PhysicsCollider3D ##################
      foreach (var (handle, collider) in frame.GetComponentIterator<PhysicsCollider3D>()) {
        var entry = _settings.GetEntryForPhysicsEntity3D(frame, handle);
        if (ShouldDraw(entry, false, false)) {
          DrawCollider3DGizmo(
            frame,
            handle,
            &collider,
            entry.Color,
            entry.DisableFill
          );
        }
      }

      // ################## Components: CharacterController3D ##################
      if (ShouldDraw(_settings.CharacterController, false, false)) {
        foreach (var (entity, cc) in frame.GetComponentIterator<CharacterController3D>()) {
          if (frame.Unsafe.TryGetPointer(entity, out Transform3D* t) &&
              frame.TryFindAsset(cc.Config, out CharacterController3DConfig config)) {
            DrawCharacterController3DGizmo(
              t->Position.ToUnityVector3(),
              config,
              _settings.CharacterController.Color,
              _settings.CharacterController.InactiveColor,
              _settings.CharacterController.DisableFill
            );
          }
        }
      }

      // ################## Components: PhysicsJoints3D ##################
      if (ShouldDraw(_settings.PhysicsJoints, false, false)) {
        foreach (var (handle, jointsComponent) in frame.Unsafe.GetComponentBlockIterator<PhysicsJoints3D>()) {
          if (frame.Unsafe.TryGetPointer(handle, out Transform3D* transform) &&
              jointsComponent->TryGetJoints(frame, out var jointsBuffer, out var jointsCount)) {
            for (var i = 0; i < jointsCount; i++) {
              var curJoint = jointsBuffer + i;

              frame.Unsafe.TryGetPointer(curJoint->ConnectedEntity, out Transform3D* connectedTransform);

              DrawGizmosJoint3D(
                curJoint,
                transform,
                connectedTransform,
                selected: false,
                settings,
                _settings.PhysicsJoints.DisableFill
              );
            }
          }
        }
      }
    }

    [DrawGizmo(GizmoType.Selected | GizmoType.Active | GizmoType.NonSelected)]
    static void DrawGizmos_QuantumStaticSphereCollider3D(QuantumStaticSphereCollider3D behaviour, GizmoType gizmoType) {
      bool selected = gizmoType.HasFlag(GizmoType.Selected);

      if (Application.isPlaying == false) {
        behaviour.UpdateFromSourceCollider();
      }

      if (!ShouldDraw(_settings.StaticColliders, selected, false)) {
        return;
      }

      var transform = behaviour.transform;

      // the radius with which the sphere with be baked into the map
      var scale = transform.lossyScale;
      var radiusScale = Mathf.Max(Mathf.Max(scale.x, scale.y), scale.z);
      var radius = behaviour.Radius.AsFloat * radiusScale;

      GizmoUtils.DrawGizmosSphere(
        transform.TransformPoint(behaviour.PositionOffset.ToUnityVector3()),
        radius,
        _settings.GetSelectedColor(_settings.StaticColliders.Color, selected),
        style: _settings.StaticColliders.DisableFill ? QuantumGizmoStyle.FillDisabled : default
      );
    }

    [DrawGizmo(GizmoType.Selected | GizmoType.Active | GizmoType.NonSelected)]
    static void DrawGizmos_QuantumStaticCapsuleCollider3D(QuantumStaticCapsuleCollider3D behaviour,
      GizmoType gizmoType) {
      bool selected = gizmoType.HasFlag(GizmoType.Selected);

      if (Application.isPlaying == false) {
        behaviour.UpdateFromSourceCollider();
      }

      if (!ShouldDraw(_settings.StaticColliders, selected, false)) {
        return;
      }

      var t = behaviour.transform;
      var scale = t.lossyScale;
      var radiusScale = Mathf.Max(scale.x, scale.z);
      var extentScale = scale.y;

      var matrix = Matrix4x4.TRS(
        t.TransformPoint(behaviour.PositionOffset.ToUnityVector3()),
        Quaternion.Euler(t.rotation.eulerAngles + behaviour.RotationOffset.ToUnityVector3()),
        Vector3.one);

      var radius = Math.Max(behaviour.Radius.AsFloat, 0) * radiusScale;
      var extent = Math.Max((behaviour.Height.AsFloat / 2.0f) - radius, 0) * extentScale;

      GizmoUtils.DrawGizmosCapsule(
        matrix,
        radius,
        extent,
        _settings.GetSelectedColor(_settings.StaticColliders.Color, selected),
        style: _settings.StaticColliders.DisableFill ? QuantumGizmoStyle.FillDisabled : default
      );
    }

    [DrawGizmo(GizmoType.Selected | GizmoType.Active | GizmoType.NonSelected)]
    static void DrawGizmos_QuantumStaticBoxCollider3D(QuantumStaticBoxCollider3D behaviour, GizmoType gizmoType) {
      bool selected = gizmoType.HasFlag(GizmoType.Selected);

      if (Application.isPlaying == false) {
        behaviour.UpdateFromSourceCollider();
      }

      if (!ShouldDraw(_settings.StaticColliders, selected, false)) {
        return;
      }

      var t = behaviour.transform;

      var matrix = Matrix4x4.TRS(
        t.TransformPoint(behaviour.PositionOffset.ToUnityVector3()),
        t.rotation * Quaternion.Euler(behaviour.RotationOffset.ToUnityVector3()),
        t.lossyScale);

      GizmoUtils.DrawGizmosBox(
        matrix,
        behaviour.Size.ToUnityVector3(),
        _settings.GetSelectedColor(_settings.StaticColliders.Color, selected),
        style: _settings.StaticColliders.DisableFill ? QuantumGizmoStyle.FillDisabled : default
      );
    }

    [DrawGizmo(GizmoType.Selected | GizmoType.Active | GizmoType.NonSelected)]
    static void DrawGizmos_JointPrototype3D(QPrototypePhysicsJoints3D behaviour, GizmoType gizmoType) {
      bool selected = gizmoType.HasFlag(GizmoType.Selected);
      if (!ShouldDraw(_settings.PhysicsJoints, selected)) {
        return;
      }

      var entity = behaviour.GetComponent<QuantumEntityPrototype>();

      if (entity == null || behaviour.Prototype.JointConfigs == null) {
        return;
      }

      FPMathUtils.LoadLookupTables();

      foreach (var prototype in behaviour.Prototype.JointConfigs) {
        if (prototype.JointType == JointType3D.None) {
          return;
        }

        QuantumGizmosJointInfo info;

        switch (prototype.JointType) {
          case JointType3D.DistanceJoint:
            info.Type = QuantumGizmosJointInfo.GizmosJointType.DistanceJoint3D;
            info.MinDistance = prototype.MinDistance.AsFloat;
            break;

          case JointType3D.SpringJoint:
            info.Type = QuantumGizmosJointInfo.GizmosJointType.SpringJoint3D;
            info.MinDistance = prototype.Distance.AsFloat;
            break;

          case JointType3D.HingeJoint:
            info.Type = QuantumGizmosJointInfo.GizmosJointType.HingeJoint3D;
            info.MinDistance = prototype.Distance.AsFloat;
            break;

          default:
            throw new NotSupportedException($"Unsupported joint type {prototype.JointType}");
        }

        var transform = behaviour.transform;

        info.Selected = selected;
        info.JointRot = transform.rotation;
        info.RelRotRef = Quaternion.Inverse(info.JointRot);
        info.AnchorPos = transform.position + info.JointRot * prototype.Anchor.ToUnityVector3();
        info.MaxDistance = prototype.MaxDistance.AsFloat;
        info.Axis = prototype.Axis.ToUnityVector3();
        info.UseAngleLimits = prototype.UseAngleLimits;
        info.LowerAngle = prototype.LowerAngle.AsFloat;
        info.UpperAngle = prototype.UpperAngle.AsFloat;

        if (prototype.ConnectedEntity == null) {
          info.ConnectedRot = Quaternion.identity;
          info.ConnectedPos = prototype.ConnectedAnchor.ToUnityVector3();
        } else {
          info.ConnectedRot = prototype.ConnectedEntity.transform.rotation;
          info.ConnectedPos = prototype.ConnectedEntity.transform.position +
                              info.ConnectedRot * prototype.ConnectedAnchor.ToUnityVector3();
          info.RelRotRef = info.ConnectedRot * info.RelRotRef;
        }

        DrawGizmosJointInternal(ref info, _settings, _settings.PhysicsJoints.DisableFill);
      }
    }

    private static unsafe void DrawGizmosJoint3D(Joint3D* joint, Transform3D* jointTransform,
      Transform3D* connectedTransform, bool selected, QuantumGameGizmosSettings gizmosSettings,
      bool fill = false) {
      if (joint->Type == JointType3D.None) {
        return;
      }

      var param = default(QuantumGizmosJointInfo);
      param.Selected = selected;
      param.JointRot = jointTransform->Rotation.ToUnityQuaternion();
      param.AnchorPos = jointTransform->TransformPoint(joint->Anchor).ToUnityVector3();

      switch (joint->Type) {
        case JointType3D.DistanceJoint:
          param.Type = QuantumGizmosJointInfo.GizmosJointType.DistanceJoint3D;
          param.MinDistance = joint->DistanceJoint.MinDistance.AsFloat;
          param.MaxDistance = joint->DistanceJoint.MaxDistance.AsFloat;
          break;

        case JointType3D.SpringJoint:
          param.Type = QuantumGizmosJointInfo.GizmosJointType.SpringJoint3D;
          param.MinDistance = joint->SpringJoint.Distance.AsFloat;
          break;

        case JointType3D.HingeJoint:
          param.Type = QuantumGizmosJointInfo.GizmosJointType.HingeJoint3D;
          param.RelRotRef = joint->HingeJoint.RelativeRotationReference.ToUnityQuaternion();
          param.Axis = joint->HingeJoint.Axis.ToUnityVector3();
          param.UseAngleLimits = joint->HingeJoint.UseAngleLimits;
          param.LowerAngle = (joint->HingeJoint.LowerLimitRad * FP.Rad2Deg).AsFloat;
          param.UpperAngle = (joint->HingeJoint.UpperLimitRad * FP.Rad2Deg).AsFloat;
          break;
      }

      if (connectedTransform == null) {
        param.ConnectedRot = Quaternion.identity;
        param.ConnectedPos = joint->ConnectedAnchor.ToUnityVector3();
      } else {
        param.ConnectedRot = connectedTransform->Rotation.ToUnityQuaternion();
        param.ConnectedPos = connectedTransform->TransformPoint(joint->ConnectedAnchor).ToUnityVector3();
      }

      DrawGizmosJointInternal(ref param, gizmosSettings, fill);
    }

    /// <summary>
    /// Draws a gizmo of a given shape at the specified position and rotation
    /// </summary>
    public static unsafe void DrawShape3DGizmo(Shape3D s, Vector3 position, Quaternion rotation, Color color,
      QuantumGizmoStyle style = default) {
      var localOffset = s.LocalTransform.Position.ToUnityVector3();
      var localRotation = s.LocalTransform.Rotation.ToUnityQuaternion();

      position += rotation * localOffset;
      rotation *= localRotation;

      switch (s.Type) {
        case Shape3DType.Sphere:
          GizmoUtils.DrawGizmosSphere(position, s.Sphere.Radius.AsFloat, color, style: style);
          break;
        case Shape3DType.Box:
          GizmoUtils.DrawGizmosBox(position, s.Box.Extents.ToUnityVector3() * 2, color, style: style,
            rotation: rotation);
          break;
        case Shape3DType.Capsule:
          GizmoUtils.DrawGizmosCapsule(position, s.Capsule.Radius.AsFloat, s.Capsule.Extent.AsFloat, color,
            style: style, rotation: rotation);
          break;
      }
    }

    private static unsafe void DrawCollider3DGizmo(Frame frame, EntityRef handle, PhysicsCollider3D* collider,
      Color color, bool disableFill) {
      var style = disableFill ? QuantumGizmoStyle.FillDisabled : default;

      if (!frame.Unsafe.TryGetPointer(handle, out Transform3D* transform)) {
        return;
      }

      if (collider->Shape.Type == Shape3DType.Compound) {
        DrawCompoundShape3D(frame, &collider->Shape, transform, color, style);
      } else {
        DrawShape3DGizmo(collider->Shape, transform->Position.ToUnityVector3(),
          transform->Rotation.ToUnityQuaternion(), color, style);
      }
    }

    private static unsafe void DrawCharacterController3DGizmo(Vector3 position, CharacterController3DConfig config,
      Color radiusColor, Color extentsColor, bool disableFill) {
      var style = disableFill ? QuantumGizmoStyle.FillDisabled : default;

      GizmoUtils.DrawGizmosSphere(position + config.Offset.ToUnityVector3(),
        config.Radius.AsFloat, radiusColor, style: style);
      GizmoUtils.DrawGizmosSphere(position + config.Offset.ToUnityVector3(),
        config.Radius.AsFloat + config.Extent.AsFloat, extentsColor, style: style);
    }


    private static unsafe void DrawCompoundShape3D(Frame f, Shape3D* compoundShape, Transform3D* transform, Color color,
      QuantumGizmoStyle style = default) {
      Debug.Assert(compoundShape->Type == Shape3DType.Compound);

      if (compoundShape->Compound.GetShapes(f, out var shapesBuffer, out var count)) {
        for (var i = 0; i < count; i++) {
          var shape = shapesBuffer + i;

          if (shape->Type == Shape3DType.Compound) {
            DrawCompoundShape3D(f, shape, transform, color, style);
          } else {
            DrawShape3DGizmo(*shape, transform->Position.ToUnityVector3(), transform->Rotation.ToUnityQuaternion(),
              color, style);
          }
        }
      }
    }

    [DrawGizmo(GizmoType.Selected | GizmoType.Active | GizmoType.NonSelected)]
    static void DrawGizmos_QuantumStaticMeshCollider3D(QuantumStaticMeshCollider3D behaviour, GizmoType gizmoType) {
      DrawStaticMeshCollider(behaviour, gizmoType);
    }

    private static void DrawStaticMeshCollider(MonoBehaviour behaviour, GizmoType gizmoType) {
      bool selected = gizmoType.HasFlag(GizmoType.Selected);

      if (_settings.StaticMeshTriangles.Enabled == false && _settings.StaticMeshNormals.Enabled == false) {
        return;
      }

      var meshData = GetOrCreateGizmoData(behaviour);

      if (_settings.StaticMeshTriangles.Enabled) {
        Handles.color = _settings.GetSelectedColor(_settings.StaticMeshTriangles.Color, selected);
        Handles.matrix = Matrix4x4.identity;

        Handles.DrawLines(meshData.TrianglePoints, meshData.TriangleSegments);
        Handles.color = Color.white;
      }

      if (_settings.StaticMeshNormals.Enabled) {
        Handles.color = _settings.GetSelectedColor(_settings.StaticMeshNormals.Color, selected);
        Handles.matrix = Matrix4x4.identity;

        Handles.DrawLines(meshData.NormalPoints);
        Handles.color = Color.white;
      }
    }

#if QUANTUM_ENABLE_TERRAIN && !QUANTUM_DISABLE_TERRAIN
    [DrawGizmo(GizmoType.Selected | GizmoType.Active | GizmoType.NonSelected)]
    static void DrawGizmos_StaticTerrainCollider3D(QuantumStaticTerrainCollider3D behaviour, GizmoType gizmoType) {
      DrawStaticMeshCollider(behaviour, gizmoType);
    }
#endif
  }
#endif
}

#endregion


#region Assets/Photon/Quantum/Runtime/Gizmos/QuantumGameGizmos.Prediction.cs

namespace Quantum {
#if UNITY_EDITOR
  using Photon.Deterministic;
  using UnityEditor;

  public partial class QuantumGameGizmos {
    private static void OnDrawGizmos_Prediction(Frame frame, GizmoType type) {
      if (frame.Context.Culling != null) {
        bool selected = type.HasFlag(GizmoType.Selected);
        if (!ShouldDraw(_settings.PredictionArea, selected, false)) {
          return;
        }

        var context = frame.Context;
        if (context.PredictionAreaRadius != FP.UseableMax) {
#if QUANTUM_XY
          // The Quantum simulation does not know about QUANTUM_XY and always keeps the vector2 Y component in the vector3 Z component.
          var predictionAreaCenter = new UnityEngine.Vector3(context.PredictionAreaCenter.X.AsFloat, context.PredictionAreaCenter.Z.AsFloat, 0);
#else
          var predictionAreaCenter = context.PredictionAreaCenter.ToUnityVector3();
#endif
          GizmoUtils.DrawGizmosSphere(
            predictionAreaCenter,
            context.PredictionAreaRadius.AsFloat,
            _settings.PredictionArea.Color, 
            _settings.PredictionArea.Style
          );
        }
      }
    }
  }
#endif
}

#endregion


#region Assets/Photon/Quantum/Runtime/Gizmos/QuantumGizmoColors.cs

namespace Quantum {
  using UnityEngine;

  /// <summary>
  /// The default Quantum Gizmo colors.
  /// </summary>
  public static class QuantumGizmoColors {
    /// <summary>
    /// Black Gizmo color. RGBA: (0, 0, 0, 1)
    /// </summary>
    public static Color Black = Color.black;

    /// <summary>
    /// Yellow Gizmo color. RGBA: (1, 0.92, 0.016, 1)
    /// </summary>
    public static Color Yellow = Color.yellow;

    /// <summary>
    /// Magenta Gizmo color. RGBA: (1, 0, 1, 1)
    /// </summary>
    public static Color Magenta = Color.magenta;

    /// <summary>
    /// Blue Gizmo color. RGBA: (0, 0, 1, 1)
    /// </summary>
    public static Color Blue = Color.blue;

    /// <summary>
    /// Green Gizmo color. RGBA: (0, 1, 0, 1)
    /// </summary>
    public static Color Green = Color.green;
    
    /// <summary>
    /// White Gizmo color. RGBA: (1, 1, 1, 1)
    /// </summary>
    public static Color White = Color.white;
    
    /// <summary>
    /// Red Gizmo color. RGBA: (1, 0, 0, 1)
    /// </summary>
    public static Color Red = Color.red;
    
    /// <summary>
    /// Cyan Gizmo color. RGBA: (0, 1, 1, 1)
    /// </summary>
    public static Color Cyan = Color.cyan;
    
    /// <summary>
    /// Gray Gizmo color. RGBA: (0.5, 0.5, 0.5, 1)
    /// </summary>
    public static Color Gray = Color.gray;

    /// <summary>
    /// Light Green Gizmo color. RGBA: (0.4, 1, 0.7, 1)
    /// </summary>
    public static Color LightGreen = new Color(0.4f, 1.0f, 0.7f);
    
    /// <summary>
    /// Lime Green Gizmo color. RGBA: (0.4925605, 0.9176471, 0.5050631, 1)
    /// </summary>
    public static Color LimeGreen = new Color(0.4925605f, 0.9176471f, 0.5050631f);
    
    /// <summary>
    /// Light Blue Gizmo color. RGBA: (0, 0.75, 1, 1)
    /// </summary>
    public static Color LightBlue = new Color(0.0f, 0.75f, 1.0f);
    
    /// <summary>
    /// Sky Blue Gizmo color. RGBA: (0.4705882, 0.7371198, 1, 1)
    /// </summary>
    public static Color SkyBlue = new Color(0.4705882f, 0.7371198f, 1.0f);
    
    /// <summary>
    /// Maroon Gizmo color. RGBA: (1, 0, 0.5, 0.5)
    /// </summary>
    public static Color Maroon = new Color(1.0f, 0.0f, 0.5f, 0.5f);
    
    /// <summary>
    /// Light Purple Gizmo color. RGBA: (0.5192922, 0.4622621, 0.6985294, 1)
    /// </summary>
    public static Color LightPurple = new Color(0.5192922f, 0.4622621f, 0.6985294f);
    
    /// <summary>
    /// Transparent Magenta Gizmo color. RGBA: (1, 0, 1, 0.5)
    /// </summary>
    public static Color TransparentMagenta = Magenta.Alpha(0.5f);
    
    /// <summary>
    /// Transparent Gray Gizmo color. RGBA: (0.5, 0.5, 0.5, 0.5)
    /// </summary>
    public static Color TransparentGray = Gray.Alpha(0.5f);
    
    /// <summary>
    /// Transparent Light Purple Gizmo color. RGBA: (0.5192922, 0.4622621, 0.6985294, 0.5)
    /// </summary>
    public static Color TransparentLightPurple = LightPurple.Alpha(0.5f);
    
    /// <summary>
    /// Transparent Yellow Gizmo color. RGBA: (1, 0.92, 0.016, 0.5)
    /// </summary>
    public static Color TransparentYellow = Yellow.Alpha(0.5f);
    
    /// <summary>
    /// Transparent White Gizmo color. RGBA: (1, 1, 1, 0.5)
    /// </summary>
    public static Color TransparentWhite = White.Alpha(0.5f);
    
    /// <summary>
    /// Transparent Lime Green Gizmo color. RGBA: (0.4925605, 0.9176471, 0.5050631, 0.5)
    /// </summary>
    public static Color TransparentLimeGreen = LimeGreen.Alpha(0.5f);
    
    /// <summary>
    /// Transparent Green Gizmo color. RGBA: (0, 1, 0, 0.5)
    /// </summary>
    public static Color TransparentGreen = Green.Alpha(0.5f);
    
    /// <summary>
    /// Transparent Sky Blue Gizmo color. RGBA: (0.4705882, 0.7371198, 1, 0.5)
    /// </summary>
    public static Color TransparentSkyBlue = SkyBlue.Alpha(0.5f);
    
    /// <summary>
    /// Transparent Light Blue Gizmo color. RGBA: (0, 0.75, 1, 0.5)
    /// </summary>
    public static Color TransparentLightBlue = LightBlue.Alpha(0.5f);
    
    /// <summary>
    /// Transparent Black Gizmo color. RGBA: (0, 0, 0, 0.5)
    /// </summary>
    public static Color TransparentLightGreen = LightGreen.Alpha(0.5f);
    
    /// <summary>
    /// Transparent Blue Gizmo color. RGBA: (0, 0, 1, 0.5)
    /// </summary>
    public static Color TransparentBlue = Blue.Alpha(0.5f);
    
    /// <summary>
    /// Transparent Maroon Gizmo color. RGBA: (1, 0, 0.5, 0.5)
    /// </summary>
    public static Color TransparentMaroon = Maroon.Alpha(0.5f);
    
    /// <summary>
    /// Transparent Red Gizmo color. RGBA: (1, 0, 0, 0.5)
    /// </summary>
    public static Color TransparentRed = Red.Alpha(0.5f);

    /// <summary>
    /// Get the selected version of a given color.
    /// </summary>
    /// <param name="settings"></param>
    /// <param name="color"></param>
    /// <param name="selected"></param>
    /// <returns></returns>
    public static Color GetSelectedColor(this QuantumGameGizmosSettings settings, Color color, bool selected) {
      return selected ? color.Brightness(settings.SelectedBrightness) : color;
    }

    /// <summary>
    /// Desaturate a given color.
    /// </summary>
    /// <param name="c"></param>
    /// <param name="t"></param>
    /// <returns></returns>
    public static Color Desaturate(this Color c, float t = .25f) {
      return Color.Lerp(new Color(c.grayscale, c.grayscale, c.grayscale), c, t);
    }

    /// <summary>
    /// Darken a given color.
    /// </summary>
    /// <param name="color"></param>
    /// <param name="percentage"></param>
    /// <returns></returns>
    public static Color Darken(this Color color, float percentage = .25f) {
      percentage = Mathf.Clamp01(percentage);

      color.r *= 1 - percentage;
      color.g *= 1 - percentage;
      color.b *= 1 - percentage;

      return color;
    }
  }
}

#endregion


#region Assets/Photon/Quantum/Runtime/IQuantumAssetSource.cs

namespace Quantum {
  using System;

  public interface IQuantumAssetObjectSource {
    System.Type         AssetType { get; }
    void                Acquire(bool synchronous);
    void                Release();
    Quantum.AssetObject WaitForResult();
    bool                IsCompleted { get; }
    string              Description { get; }
    
#if UNITY_EDITOR
    Quantum.AssetObject EditorInstance { get; }
#endif
  }

  [Serializable]
  public class QuantumAssetObjectSourceStatic : QuantumAssetSourceStatic<Quantum.AssetObject>, IQuantumAssetObjectSource {
    public Type AssetType => Object.GetType();

    public QuantumAssetObjectSourceStatic() {
    }
    
    public QuantumAssetObjectSourceStatic(Quantum.AssetObject asset) {
      Object = asset;
    }
  }
  
  [Serializable]
  public class QuantumAssetObjectSourceStaticLazy : QuantumAssetSourceStaticLazy<Quantum.AssetObject>, IQuantumAssetObjectSource {
    public Type AssetType => Object.asset.GetType();
  }
  
  [Serializable]
  public class QuantumAssetObjectSourceResource : QuantumAssetSourceResource<Quantum.AssetObject>, IQuantumAssetObjectSource {
    public SerializableType<Quantum.AssetObject> SerializableAssetType;

    public Type AssetType => SerializableAssetType;
  }
  
#if (QUANTUM_ADDRESSABLES || QUANTUM_ENABLE_ADDRESSABLES) && !QUANTUM_DISABLE_ADDRESSABLES
  [Serializable]
  public class QuantumAssetObjectSourceAddressable : QuantumAssetSourceAddressable<Quantum.AssetObject>, IQuantumAssetObjectSource {
    public SerializableType<Quantum.AssetObject> SerializableAssetType;

    public QuantumAssetObjectSourceAddressable() {
    }

    public QuantumAssetObjectSourceAddressable(string path, Type assetType) {
      RuntimeKey = path;
      SerializableAssetType = assetType;
    }
    
    public Type AssetType => SerializableAssetType;
  }
#endif
}

#endregion


#region Assets/Photon/Quantum/Runtime/Legacy/IQuantumEditorGUI.cs

namespace Quantum {
  using System;
  using System.Reflection;
  using UnityEditor;
  using UnityEngine;

  public interface IQuantumEditorGUI {
#if UNITY_EDITOR
    [Obsolete("Use EditorGUILayout.PropertyField instead", true)]
    bool Inspector(SerializedProperty prop, GUIContent label = null, string[] filters = null, bool skipRoot = true, bool drawScript = false, QuantumEditorGUIPropertyCallback callback = null);
    [Obsolete("Use EditorGUILayout.PropertyField instead", true)]
    bool PropertyField(SerializedProperty property, GUIContent label, bool includeChildren, params GUILayoutOption[] options);
    [Obsolete("Use EditorGUILayout.PropertyField instead", true)]
    void MultiTypeObjectField(SerializedProperty prop, GUIContent label, Type[] types, params GUILayoutOption[] options);
#endif
  }

#if UNITY_EDITOR
  public static class IQuantumEditorGUIExtensions {
    [Obsolete("Use EditorGUILayout.PropertyField instead", true)]
    public static bool Inspector(this IQuantumEditorGUI gui, SerializedObject obj, string[] filters = null, QuantumEditorGUIPropertyCallback callback = null, bool drawScript = true) {
      return gui.Inspector(obj.GetIterator(), filters: filters, skipRoot: true, callback: callback, drawScript: drawScript);
    }

    [Obsolete("Use EditorGUILayout.PropertyField instead", true)]
    public static bool Inspector(this IQuantumEditorGUI gui, SerializedObject obj, string propertyPath, string[] filters = null, bool skipRoot = true, QuantumEditorGUIPropertyCallback callback = null, bool drawScript = false) {
      return gui.Inspector(obj.FindPropertyOrThrow(propertyPath), filters: filters, skipRoot: skipRoot, callback: callback, drawScript: drawScript);
    }

    [Obsolete("Use EditorGUILayout.PropertyField instead", true)]
    public static bool PropertyField(this IQuantumEditorGUI gui, SerializedProperty property, params GUILayoutOption[] options) {
      return gui.PropertyField(property, null, false, options);
    }

    [Obsolete("Use EditorGUILayout.PropertyField instead", true)]
    public static bool PropertyField(this IQuantumEditorGUI gui, SerializedProperty property, GUIContent label, params GUILayoutOption[] options) {
      return gui.PropertyField(property, label, false, options);
    }

    [Obsolete("Use EditorGUILayout.PropertyField instead", true)]
    public static void MultiTypeObjectField(this IQuantumEditorGUI gui, SerializedProperty prop, GUIContent label, params Type[] types) {
      gui.MultiTypeObjectField(prop, label, types);
    }
  }

  [Obsolete("Use EditorGUILayout.PropertyField instead", true)]
  public delegate bool QuantumEditorGUIPropertyCallback(SerializedProperty property, FieldInfo field, Type fieldType);
#endif
}

#endregion


#region Assets/Photon/Quantum/Runtime/Legacy/QuantumRunner.Legacy.cs

namespace Quantum {
  using System;
  using Photon.Deterministic;
  using Photon.Realtime;

  public partial class QuantumRunner {
    [Obsolete("Use Id instead")]
    public string name => Id;

    [Obsolete("The immediate param is not required anymore, use ShutdownAll()")]
    public static void ShutdownAll(bool immediate = false) {
      QuantumRunnerRegistry.Global.ShutdownAll();
    }

    [Obsolete("Use StartGameAsync(SessionRunner.Arguments)")]
    public static QuantumRunner StartGame(string clientId, StartParameters startParameters) {
      var arguments = startParameters.Arguments;
      arguments.ClientId = clientId;
      return StartGame(arguments);
    }

    [Obsolete("Use UnityRunnerFactory.Init()")]
    public static void Init(Boolean force = false) {
      QuantumRunnerUnityFactory.Init(force);
    }

    [Obsolete("Use QuantumRunner.IsSessionUpdateDisabled")]
    public bool OverrideUpdateSession {
      get => IsSessionUpdateDisabled;
      set => IsSessionUpdateDisabled = value;
    }

    [Obsolete("Not required anymore. Use SessionRunner.StartAsync() or SessionRunner.WaitForStartAsync() instead.")]
    public bool HasGameStartTimedOut => false;

    [Obsolete("Use SessionRunner.Arguments")]
    public struct StartParameters {
      public Arguments Arguments;

      public RuntimeConfig RuntimeConfig {
        get => (RuntimeConfig)Arguments.RuntimeConfig;
        set => Arguments.RuntimeConfig = value;
      }

      public DeterministicSessionConfig DeterministicConfig {
        get => Arguments.SessionConfig;
        set => Arguments.SessionConfig = value;
      }

      public IDeterministicReplayProvider ReplayProvider {
        get => Arguments.ReplayProvider;
        set => Arguments.ReplayProvider = value;
      }

      public DeterministicGameMode GameMode {
        get => Arguments.GameMode;
        set => Arguments.GameMode = value;
      }

      public Int32 InitialFrame {
        get => Arguments.InitialTick;
        set => Arguments.InitialTick = value;
      }

      public Byte[] FrameData {
        get => Arguments.FrameData;
        set => Arguments.FrameData = value;
      }

      public string RunnerId {
        get => Arguments.RunnerId;
        set => Arguments.RunnerId = value;
      }

      [Obsolete("Only accessible by the QuantumNetworkCommunicator")]
      public QuantumNetworkCommunicator.QuitBehaviour QuitBehaviour { get; set; }

      public Int32 PlayerCount {
        get => Arguments.PlayerCount;
        set => Arguments.PlayerCount = value;
      }

      [Obsolete("Has been replaced by adding players after game start by using Session.AddPlayer()")]
      public Int32 LocalPlayerCount => -1;

      [Obsolete("Use Communicator = new QuantumNetworkComminicator(RealtimeClient client)")]
      public RealtimeClient NetworkClient;

      public IResourceManager ResourceManagerOverride {
        get => Arguments.ResourceManager;
        set => Arguments.ResourceManager = value;
      }

      public InstantReplaySettings InstantReplayConfig {
        get => Arguments.InstantReplaySettings;
        set => Arguments.InstantReplaySettings = value;
      }

      public Int32 HeapExtraCount {
        get => Arguments.HeapExtraCount;
        set => Arguments.HeapExtraCount = value;
      }

      public DynamicAssetDB InitialDynamicAssets {
        get => Arguments.InitialDynamicAssets;
        set => Arguments.InitialDynamicAssets = value;
      }

      public float StartGameTimeoutInSeconds {
        get => Arguments.StartGameTimeoutInSeconds.HasValue ? Arguments.StartGameTimeoutInSeconds.Value : Arguments.DefaultStartGameTimeoutInSeconds;
        set => Arguments.StartGameTimeoutInSeconds = value;
      }

      [Obsolete("IsRejoin is not used anymore")]
      public bool IsRejoin;

      [Obsolete("The property moved to the SessionRunner")]
      public RecordingFlags RecordingFlags;
    }
  }
}

#endregion


#region Assets/Photon/Quantum/Runtime/Map/MapDataBakerCallback.cs

namespace Quantum {
  using System;
  using System.Collections.Generic;

  /// <summary>
  /// This assembly attribute needs to be set inside custom assemblies that required 
  /// static edit mode callbacks from the <see cref="MapDataBakerCallback"/> class.
  /// [assembly: QuantumMapBakeAssembly]
  /// </summary>
  [AttributeUsage(AttributeTargets.Assembly)]
  public class QuantumMapBakeAssemblyAttribute : System.Attribute {
    /// <summary>
    /// Enabled to explicitly ignore this assembly when searching for callback implementations.
    /// </summary>
    public bool Ignore;
  }

  /// <summary>
  /// Derive from the class to receive callbacks during Quantum map baking.
  /// Add the <see cref="QuantumMapBakeAssemblyAttribute"/> assembly attribute when the implementation
  /// is located in different assemblies.
  /// </summary>
  public abstract class MapDataBakerCallback {
    /// <summary>
    /// Is called in the beginning of map baking.
    /// </summary>
    /// <param name="data">The MapData object that is currently baked.</param>
    public abstract void OnBeforeBake(QuantumMapData data);

    /// <summary>
    /// Is called in the beginning of map baking similar to <see cref="OnBeforeBake(QuantumMapData)"/>
    /// with a different signature.
    /// </summary>
    /// <param name="data">Map data</param>
    /// <param name="buildTrigger">Originating build trigger</param>
    /// <param name="bakeFlags">Use build flags</param>
    public virtual void OnBeforeBake(QuantumMapData data, QuantumMapDataBaker.BuildTrigger buildTrigger, QuantumMapDataBakeFlags bakeFlags) { }
    /// <summary>
    /// Is called after map baking when colliders and prototypes have been baked and before navmesh baking.
    /// </summary>
    /// <param name="data"></param>
    /// 
    public abstract void OnBake(QuantumMapData data);
    /// <summary>
    /// Is called before any navmeshes are generated or any bake data is collected.
    /// </summary>
    /// <param name="data">The MapData object that is currently baked.</param>
    public virtual void OnBeforeBakeNavMesh(QuantumMapData data) { }

    /// <summary>
    /// Is called during navmesh baking with the current list of bake data retreived from Unity navmeshes flagged for Quantum
    /// navmesh baking.
    /// Add new BakeData objects to the navMeshBakeData list.
    /// </summary>
    /// <param name="data">The MapData object that is currently baked.</param>
    /// <param name="navMeshBakeData">Current list of bake data to be baked</param>
    public virtual void OnCollectNavMeshBakeData(QuantumMapData data, List<NavMeshBakeData> navMeshBakeData) { }

    /// <summary>
    /// Is called after navmesh baking before serializing them to assets.
    /// Add new NavMesh objects the navmeshes list.
    /// </summary>
    /// <param name="data">The MapData object that is currently baked.</param>
    /// <param name="navmeshes">Current list of baked navmeshes to be saved to assets.</param>
    public virtual void OnCollectNavMeshes(QuantumMapData data, List<Quantum.NavMesh> navmeshes) { }

    /// <summary>
    /// Is called after the navmesh generation has been completed.
    /// Navmeshes assets references are stored in data.Asset.Settings.NavMeshLinks.
    /// </summary>
    /// <param name="data">The MapData object that is currently baked.</param>
    public virtual void OnBakeNavMesh(QuantumMapData data) { }
  }

  /// <summary>
  /// The QuantumEditorAutoBaker script uses this enumeration to configure what steps to build
  /// on different automatic build triggers.
  /// </summary>
  [Flags, Serializable]
  public enum QuantumMapDataBakeFlags {
    /// <summary>
    /// Build nothing
    /// </summary>
    None,
    [Obsolete("Use BakeMapData instead")]
    Obsolete_BakeMapData = 1 << 0,
    /// <summary>
    /// Bake <see cref="QuantumMapDataBakeFlags.BakeMapPrototypes"/> and <see cref="QuantumMapDataBakeFlags.BakeMapColliders"/>
    /// </summary>
    BakeMapData = BakeMapPrototypes | BakeMapColliders,
    /// <summary>
    /// Bake map prototypes
    /// </summary>
    BakeMapPrototypes = 1 << 5,
    /// <summary>
    /// Bake map colliders
    /// </summary>
    BakeMapColliders = 1 << 6,
    /// <summary>
    /// Bake the Unity navmesh
    /// </summary>
    BakeUnityNavMesh = 1 << 3,
    /// <summary>
    /// Import the Unity navmesh into an intermediate navmesh data structure
    /// </summary>
    ImportUnityNavMesh = 1 << 2,
    /// <summary>
    /// Bake the Quantum navmesh using the intermediate navmesh data structure
    /// </summary>
    BakeNavMesh = 1 << 1,
    /// <summary>
    /// Clear and reset the Unity navmesh
    /// </summary>
    ClearUnityNavMesh = 1 << 8,
    /// <summary>
    /// Generate the Quantum Unity Asset DB
    /// </summary>
    GenerateAssetDB = 1 << 4,
    /// <summary>
    /// Save Unity assets during the baking process. Results in calling AssetDatabase.SaveAssets().
    /// </summary>
    SaveUnityAssets = 1 << 7,
  }
}

#endregion


#region Assets/Photon/Quantum/Runtime/Map/MapDataBakerCallbackAttribute.cs

namespace Quantum {
  using System;

  /// <summary>
  /// Add this attribute to implementations of <see cref="MapDataBakerCallback"/> to control the order 
  /// in which the callbacks are finally executed. Works across different assemblies.
  /// </summary>
  public class MapDataBakerCallbackAttribute : Attribute {
    /// <summary>
    /// The invoke order, higher means called earlier.
    /// </summary>
    public int InvokeOrder { get; private set; }

    /// <summary>
    /// Constructor.
    /// </summary>
    public MapDataBakerCallbackAttribute(int invokeOrder) {
      InvokeOrder = invokeOrder;
    }
  }
}

#endregion


#region Assets/Photon/Quantum/Runtime/Quantum.Runtime.Core.cs

namespace Quantum.Prototypes.Unity {
  [System.SerializableAttribute()]
  [Quantum.Prototypes.PrototypeAttribute(typeof(Quantum.PhysicsJoints2D))]
  public class PhysicsJoints2DPrototype : Quantum.QuantumUnityPrototypeAdapter<Quantum.Prototypes.PhysicsJoints2DPrototype> {
    [Quantum.DynamicCollectionAttribute()]
    public Joint2DConfig[] JointConfigs = System.Array.Empty<Joint2DConfig>();

    public sealed override Quantum.Prototypes.PhysicsJoints2DPrototype Convert(Quantum.QuantumEntityPrototypeConverter converter) {
      var result = new Quantum.Prototypes.PhysicsJoints2DPrototype();
      result.JointConfigs = System.Array.ConvertAll(this.JointConfigs, x => x.Convert(converter));
      return result;
    }
  }
  [System.SerializableAttribute()]
  [Quantum.Prototypes.PrototypeAttribute(typeof(Quantum.Physics2D.Joint))]
  public class Joint2DConfig : Quantum.QuantumUnityPrototypeAdapter<Quantum.Prototypes.Joint2DConfig> {
    [Quantum.DrawIfAttribute("JointType", 0, (Quantum.CompareOperator)0, (Quantum.DrawIfMode)0, Compare = (Quantum.CompareOperator)1, Hide = true)]
    [UnityEngine.TooltipAttribute("If the joint should be materialized with Enabled set to false, not being considered by the Physics Engine.")]
    public System.Boolean StartDisabled;
    [Quantum.DisplayNameAttribute("Type")]
    [UnityEngine.TooltipAttribute("The type of the joint, implying which constraints are applied.")]
    public Quantum.Physics2D.JointType JointType;
    [Quantum.DrawIfAttribute("JointType", 0, (Quantum.CompareOperator)0, (Quantum.DrawIfMode)0, Compare = (Quantum.CompareOperator)1, Hide = true)]
    [UnityEngine.TooltipAttribute("A numerical tag that can be used to identify a joint or a group of joints.")]
    public System.Int32 UserTag;
    [Quantum.DrawIfAttribute("JointType", 0, (Quantum.CompareOperator)0, (Quantum.DrawIfMode)0, Compare = (Quantum.CompareOperator)1, Hide = true)]
    [UnityEngine.TooltipAttribute("A Map Entity that the joint might be connected to.\nThe entity must have at least a Transform2D component.")]
    [Quantum.LocalReference]
    public Quantum.QuantumEntityPrototype ConnectedEntity;
    [Quantum.DrawIfAttribute("JointType", 0, (Quantum.CompareOperator)0, (Quantum.DrawIfMode)0, Compare = (Quantum.CompareOperator)1, Hide = true)]
    [UnityEngine.TooltipAttribute("The anchor point to which the joint connects to.\nIf a Connected Entity is provided, this represents an offset in its local space. Otherwise, the connected anchor is a position in world space.")]
    public Photon.Deterministic.FPVector2 ConnectedAnchor;
    [Quantum.DrawIfAttribute("JointType", 0, (Quantum.CompareOperator)0, (Quantum.DrawIfMode)0, Compare = (Quantum.CompareOperator)1, Hide = true)]
    [UnityEngine.TooltipAttribute("The anchor offset, in the local space of this joint entity's transform.\nThis is the point considered for the joint constraints and where the forces will be applied in the joint entity's body.")]
    public Photon.Deterministic.FPVector2 Anchor;
    [Quantum.DrawIfAttribute("JointType", 2, (Quantum.CompareOperator)0, (Quantum.DrawIfMode)0, Hide = true)]
    [UnityEngine.TooltipAttribute("The frequency in Hertz (Hz) at which the spring joint will attempt to oscillate.\nTypical values are below half the frequency of the simulation.")]
    public Photon.Deterministic.FP Frequency;
    [Quantum.DrawIfAttribute("JointType", 2, (Quantum.CompareOperator)0, (Quantum.DrawIfMode)0, Hide = true)]
    [UnityEngine.TooltipAttribute("A dimensionless value representing the damper capacity of suppressing the spring oscillation, typically between 0 and 1.")]
    public Photon.Deterministic.FP DampingRatio;
    [Quantum.DrawIfAttribute("JointType", 0, (Quantum.CompareOperator)0, (Quantum.DrawIfMode)0, Compare = (Quantum.CompareOperator)1, Hide = true)]
    [Quantum.DrawIfAttribute("JointType", 3, (Quantum.CompareOperator)0, (Quantum.DrawIfMode)0, Compare = (Quantum.CompareOperator)1, Hide = true)]
    [UnityEngine.TooltipAttribute("Automatically configure the target Distance to be the current distance between the anchor points in the scene.")]
    public System.Boolean AutoConfigureDistance;
    [Quantum.DrawIfAttribute("JointType", 2, (Quantum.CompareOperator)0, (Quantum.DrawIfMode)0, Hide = true)]
    [Quantum.DrawIfAttribute("AutoConfigureDistance", 0, (Quantum.CompareOperator)0, (Quantum.DrawIfMode)0, Mode = (Quantum.DrawIfMode)0)]
    [UnityEngine.TooltipAttribute("The distance between the anchor points that the joint will attempt to maintain.")]
    public Photon.Deterministic.FP Distance;
    [Quantum.DrawIfAttribute("JointType", 1, (Quantum.CompareOperator)0, (Quantum.DrawIfMode)0, Hide = true)]
    [Quantum.DrawIfAttribute("AutoConfigureDistance", 0, (Quantum.CompareOperator)0, (Quantum.DrawIfMode)0, Mode = (Quantum.DrawIfMode)0)]
    [UnityEngine.TooltipAttribute("The minimum distance between the anchor points that the joint will attempt to maintain.")]
    public Photon.Deterministic.FP MinDistance;
    [Quantum.DrawIfAttribute("JointType", 1, (Quantum.CompareOperator)0, (Quantum.DrawIfMode)0, Hide = true)]
    [Quantum.DrawIfAttribute("AutoConfigureDistance", 0, (Quantum.CompareOperator)0, (Quantum.DrawIfMode)0, Mode = (Quantum.DrawIfMode)0)]
    [UnityEngine.TooltipAttribute("The maximum distance between the anchor points that the joint will attempt to maintain.")]
    public Photon.Deterministic.FP MaxDistance;
    [Quantum.DrawIfAttribute("JointType", 3, (Quantum.CompareOperator)0, (Quantum.DrawIfMode)0, Hide = true)]
    [UnityEngine.TooltipAttribute("If the relative angle between the joint transform and its connected anchor should be limited by the hinge joint.\nSet this checkbox to configure the lower and upper limiting angles.")]
    public System.Boolean UseAngleLimits;
    [Quantum.UnitAttribute((Quantum.Units)10)]
    [Quantum.DrawIfAttribute("UseAngleLimits", 1, (Quantum.CompareOperator)0, (Quantum.DrawIfMode)0, Hide = true)]
    [Quantum.DrawIfAttribute("JointType", 3, (Quantum.CompareOperator)0, (Quantum.DrawIfMode)0, Hide = true)]
    [UnityEngine.TooltipAttribute("The lower limiting angle of the allowed arc of rotation around the connected anchor, in Unit(Units.Degrees).")]
    public Photon.Deterministic.FP LowerAngle;
    [Quantum.UnitAttribute((Quantum.Units)10)]
    [Quantum.DrawIfAttribute("UseAngleLimits", 1, (Quantum.CompareOperator)0, (Quantum.DrawIfMode)0, Hide = true)]
    [Quantum.DrawIfAttribute("JointType", 3, (Quantum.CompareOperator)0, (Quantum.DrawIfMode)0, Hide = true)]
    [UnityEngine.TooltipAttribute("The upper limiting  angle of the allowed arc of rotation around the connected anchor, in Unit(Units.Degrees).")]
    public Photon.Deterministic.FP UpperAngle;
    [Quantum.DrawIfAttribute("JointType", 3, (Quantum.CompareOperator)0, (Quantum.DrawIfMode)0, Hide = true)]
    [UnityEngine.TooltipAttribute("If the hinge joint uses a motor.\nSet this checkbox to configure the motor speed and max torque.")]
    public System.Boolean UseMotor;
    [Quantum.UnitAttribute((Quantum.Units)10)]
    [Quantum.DrawIfAttribute("UseMotor", 1, (Quantum.CompareOperator)0, (Quantum.DrawIfMode)0, Hide = true)]
    [Quantum.DrawIfAttribute("JointType", 3, (Quantum.CompareOperator)0, (Quantum.DrawIfMode)0, Hide = true)]
    [UnityEngine.TooltipAttribute("The speed at which the hinge motor will attempt to rotate, in angles per second.")]
    public Photon.Deterministic.FP MotorSpeed;
    [Quantum.DrawIfAttribute("UseMotor", 1, (Quantum.CompareOperator)0, (Quantum.DrawIfMode)0, Hide = true)]
    [Quantum.DrawIfAttribute("JointType", 3, (Quantum.CompareOperator)0, (Quantum.DrawIfMode)0, Hide = true)]
    [UnityEngine.TooltipAttribute("The maximum torque produced by the hinge motor in order to achieve the target motor speed.\nLeave this checkbox unchecked and the motor toque should not be limited.")]
    public Photon.Deterministic.NullableFP MaxMotorTorque;

    public sealed override Quantum.Prototypes.Joint2DConfig Convert(Quantum.QuantumEntityPrototypeConverter converter) {
      var result = new Quantum.Prototypes.Joint2DConfig();
      result.StartDisabled = this.StartDisabled;
      result.JointType = this.JointType;
      result.UserTag = this.UserTag;
      converter.Convert(this.ConnectedEntity, out result.ConnectedEntity);
      result.ConnectedAnchor = this.ConnectedAnchor;
      result.Anchor = this.Anchor;
      result.Frequency = this.Frequency;
      result.DampingRatio = this.DampingRatio;
      result.AutoConfigureDistance = this.AutoConfigureDistance;
      result.Distance = this.Distance;
      result.MinDistance = this.MinDistance;
      result.MaxDistance = this.MaxDistance;
      result.UseAngleLimits = this.UseAngleLimits;
      result.LowerAngle = this.LowerAngle;
      result.UpperAngle = this.UpperAngle;
      result.UseMotor = this.UseMotor;
      result.MotorSpeed = this.MotorSpeed;
      result.MaxMotorTorque = this.MaxMotorTorque;
      return result;
    }
  }
  [System.SerializableAttribute()]
  [Quantum.Prototypes.PrototypeAttribute(typeof(Quantum.PhysicsJoints3D))]
  public class PhysicsJoints3DPrototype : Quantum.QuantumUnityPrototypeAdapter<Quantum.Prototypes.PhysicsJoints3DPrototype> {
    [Quantum.DynamicCollectionAttribute()]
    public Joint3DConfig[] JointConfigs = System.Array.Empty<Joint3DConfig>();

    public sealed override Quantum.Prototypes.PhysicsJoints3DPrototype Convert(Quantum.QuantumEntityPrototypeConverter converter) {
      var result = new Quantum.Prototypes.PhysicsJoints3DPrototype();
      result.JointConfigs = System.Array.ConvertAll(this.JointConfigs, x => x.Convert(converter));
      return result;
    }
  }
  [System.SerializableAttribute()]
  [Quantum.Prototypes.PrototypeAttribute(typeof(Quantum.Physics3D.Joint3D))]
  public class Joint3DConfig : Quantum.QuantumUnityPrototypeAdapter<Quantum.Prototypes.Joint3DConfig> {
    [Quantum.DrawIfAttribute("JointType", 0, (Quantum.CompareOperator)0, (Quantum.DrawIfMode)0, Compare = (Quantum.CompareOperator)1, Hide = true)]
    [UnityEngine.TooltipAttribute("If the joint should be materialized with Enabled set to false, not being considered by the Physics Engine.")]
    public System.Boolean StartDisabled;
    [Quantum.DisplayNameAttribute("Type")]
    [UnityEngine.TooltipAttribute("The type of the joint, implying which constraints are applied.")]
    public Quantum.Physics3D.JointType3D JointType;
    [Quantum.DrawIfAttribute("JointType", 0, (Quantum.CompareOperator)0, (Quantum.DrawIfMode)0, Compare = (Quantum.CompareOperator)1, Hide = true)]
    [UnityEngine.TooltipAttribute("A numerical tag that can be used to identify a joint or a group of joints.")]
    public System.Int32 UserTag;
    [Quantum.DrawIfAttribute("JointType", 0, (Quantum.CompareOperator)0, (Quantum.DrawIfMode)0, Compare = (Quantum.CompareOperator)1, Hide = true)]
    [UnityEngine.TooltipAttribute("A Map Entity that the joint might be connected to.\nThe entity must have at least a transform component.")]
    [Quantum.LocalReference]
    public Quantum.QuantumEntityPrototype ConnectedEntity;
    [Quantum.DrawIfAttribute("JointType", 0, (Quantum.CompareOperator)0, (Quantum.DrawIfMode)0, Compare = (Quantum.CompareOperator)1, Hide = true)]
    [UnityEngine.TooltipAttribute("The anchor point to which the joint connects to.\nIf a Connected Entity is provided, this represents an offset in its local space. Otherwise, the connected anchor is a position in world space.")]
    public Photon.Deterministic.FPVector3 ConnectedAnchor;
    [Quantum.DrawIfAttribute("JointType", 0, (Quantum.CompareOperator)0, (Quantum.DrawIfMode)0, Compare = (Quantum.CompareOperator)1, Hide = true)]
    [UnityEngine.TooltipAttribute("The anchor offset, in the local space of this joint entity's transform.\nThis is the point considered for the joint constraints and where the forces will be applied in the joint entity's body.")]
    public Photon.Deterministic.FPVector3 Anchor;
    [Quantum.DrawIfAttribute("JointType", 3, (Quantum.CompareOperator)0, (Quantum.DrawIfMode)0, Hide = true)]
    [UnityEngine.TooltipAttribute("Axis around which the joint rotates, defined in the local space of the entity.\nThe vector is normalized before set. If zeroed, FPVector3.Right is used instead.")]
    public Photon.Deterministic.FPVector3 Axis;
    [Quantum.DrawIfAttribute("JointType", 2, (Quantum.CompareOperator)0, (Quantum.DrawIfMode)0, Hide = true)]
    [UnityEngine.TooltipAttribute("The frequency in Hertz (Hz) at which the spring joint will attempt to oscillate.\nTypical values are below half the frequency of the simulation.")]
    public Photon.Deterministic.FP Frequency;
    [Quantum.DrawIfAttribute("JointType", 2, (Quantum.CompareOperator)0, (Quantum.DrawIfMode)0, Hide = true)]
    [UnityEngine.TooltipAttribute("A dimensionless value representing the damper capacity of suppressing the spring oscillation, typically between 0 and 1.")]
    public Photon.Deterministic.FP DampingRatio;
    [Quantum.DrawIfAttribute("JointType", 0, (Quantum.CompareOperator)0, (Quantum.DrawIfMode)0, Compare = (Quantum.CompareOperator)1, Hide = true)]
    [Quantum.DrawIfAttribute("JointType", 3, (Quantum.CompareOperator)0, (Quantum.DrawIfMode)0, Compare = (Quantum.CompareOperator)1, Hide = true)]
    [UnityEngine.TooltipAttribute("Automatically configure the target Distance to be the current distance between the anchor points in the scene.")]
    public System.Boolean AutoConfigureDistance;
    [Quantum.DrawIfAttribute("JointType", 2, (Quantum.CompareOperator)0, (Quantum.DrawIfMode)0, Hide = true)]
    [Quantum.DrawIfAttribute("AutoConfigureDistance", 0, (Quantum.CompareOperator)0, (Quantum.DrawIfMode)0, Mode = (Quantum.DrawIfMode)0)]
    [UnityEngine.TooltipAttribute("The distance between the anchor points that the joint will attempt to maintain.")]
    public Photon.Deterministic.FP Distance;
    [Quantum.DrawIfAttribute("JointType", 1, (Quantum.CompareOperator)0, (Quantum.DrawIfMode)0, Hide = true)]
    [Quantum.DrawIfAttribute("AutoConfigureDistance", 0, (Quantum.CompareOperator)0, (Quantum.DrawIfMode)0, Mode = (Quantum.DrawIfMode)0)]
    [UnityEngine.TooltipAttribute("The minimum distance between the anchor points that the joint will attempt to maintain.")]
    public Photon.Deterministic.FP MinDistance;
    [Quantum.DrawIfAttribute("JointType", 1, (Quantum.CompareOperator)0, (Quantum.DrawIfMode)0, Hide = true)]
    [Quantum.DrawIfAttribute("AutoConfigureDistance", 0, (Quantum.CompareOperator)0, (Quantum.DrawIfMode)0, Mode = (Quantum.DrawIfMode)0)]
    [UnityEngine.TooltipAttribute("The maximum distance between the anchor points that the joint will attempt to maintain.")]
    public Photon.Deterministic.FP MaxDistance;
    [Quantum.DrawIfAttribute("JointType", 3, (Quantum.CompareOperator)0, (Quantum.DrawIfMode)0, Hide = true)]
    [UnityEngine.TooltipAttribute("If the relative angle between the joint transform and its connected anchor should be limited by the hinge joint.\nSet this checkbox to configure the lower and upper limiting angles.")]
    public System.Boolean UseAngleLimits;
    [Quantum.DrawIfAttribute("UseAngleLimits", 1, (Quantum.CompareOperator)0, (Quantum.DrawIfMode)0, Hide = true)]
    [Quantum.DrawIfAttribute("JointType", 3, (Quantum.CompareOperator)0, (Quantum.DrawIfMode)0, Hide = true)]
    [UnityEngine.TooltipAttribute("The lower limiting angle of the allowed arc of rotation around the connected anchor, in degrees.")]
    public Photon.Deterministic.FP LowerAngle;
    [Quantum.DrawIfAttribute("UseAngleLimits", 1, (Quantum.CompareOperator)0, (Quantum.DrawIfMode)0, Hide = true)]
    [Quantum.DrawIfAttribute("JointType", 3, (Quantum.CompareOperator)0, (Quantum.DrawIfMode)0, Hide = true)]
    [UnityEngine.TooltipAttribute("The upper limiting  angle of the allowed arc of rotation around the connected anchor, in degrees.")]
    public Photon.Deterministic.FP UpperAngle;
    [Quantum.DrawIfAttribute("JointType", 3, (Quantum.CompareOperator)0, (Quantum.DrawIfMode)0, Hide = true)]
    [UnityEngine.TooltipAttribute("If the hinge joint uses a motor.\nSet this checkbox to configure the motor speed and max torque.")]
    public System.Boolean UseMotor;
    [Quantum.DrawIfAttribute("UseMotor", 1, (Quantum.CompareOperator)0, (Quantum.DrawIfMode)0, Hide = true)]
    [Quantum.DrawIfAttribute("JointType", 3, (Quantum.CompareOperator)0, (Quantum.DrawIfMode)0, Hide = true)]
    [UnityEngine.TooltipAttribute("The speed at which the hinge motor will attempt to rotate, in angles per second.")]
    public Photon.Deterministic.FP MotorSpeed;
    [Quantum.DrawIfAttribute("UseMotor", 1, (Quantum.CompareOperator)0, (Quantum.DrawIfMode)0, Hide = true)]
    [Quantum.DrawIfAttribute("JointType", 3, (Quantum.CompareOperator)0, (Quantum.DrawIfMode)0, Hide = true)]
    [UnityEngine.TooltipAttribute("The maximum torque produced by the hinge motor in order to achieve the target motor speed.\nLeave this checkbox unchecked and the motor toque should not be limited.")]
    public Photon.Deterministic.NullableFP MaxMotorTorque;

    public sealed override Quantum.Prototypes.Joint3DConfig Convert(Quantum.QuantumEntityPrototypeConverter converter) {
      var result = new Quantum.Prototypes.Joint3DConfig();
      result.StartDisabled = this.StartDisabled;
      result.JointType = this.JointType;
      result.UserTag = this.UserTag;
      converter.Convert(this.ConnectedEntity, out result.ConnectedEntity);
      result.ConnectedAnchor = this.ConnectedAnchor;
      result.Anchor = this.Anchor;
      result.Axis = this.Axis;
      result.Frequency = this.Frequency;
      result.DampingRatio = this.DampingRatio;
      result.AutoConfigureDistance = this.AutoConfigureDistance;
      result.Distance = this.Distance;
      result.MinDistance = this.MinDistance;
      result.MaxDistance = this.MaxDistance;
      result.UseAngleLimits = this.UseAngleLimits;
      result.LowerAngle = this.LowerAngle;
      result.UpperAngle = this.UpperAngle;
      result.UseMotor = this.UseMotor;
      result.MotorSpeed = this.MotorSpeed;
      result.MaxMotorTorque = this.MaxMotorTorque;
      return result;
    }
  }

}

#endregion


#region Assets/Photon/Quantum/Runtime/QuantumAssetSource.Common.cs

// merged AssetSource

#region QuantumAssetSourceAddressable.cs

#if (QUANTUM_ADDRESSABLES || QUANTUM_ENABLE_ADDRESSABLES) && !QUANTUM_DISABLE_ADDRESSABLES
namespace Quantum {
  using System;
  using UnityEngine;
  using UnityEngine.AddressableAssets;
  using UnityEngine.ResourceManagement.AsyncOperations;
  using static InternalLogStreams;

  /// <summary>
  /// An Addressables-based implementation of the asset source pattern. The asset is loaded from the Addressables system.
  /// </summary>
  /// <typeparam name="T"></typeparam>
  [Serializable]
  public partial class QuantumAssetSourceAddressable<T> where T : UnityEngine.Object {
    
    /// <see cref="RuntimeKey"/>
    [Obsolete("Use RuntimeKey instead")]
    public AssetReference Address {
      get {
        if (string.IsNullOrEmpty(RuntimeKey)) {
          return default;
        }
        return QuantumAddressablesUtils.CreateAssetReference(RuntimeKey);
      }
      set {
        if (value.IsValid()) {
          RuntimeKey = (string)value.RuntimeKey;
        } else {
          RuntimeKey = string.Empty;
        }
      }
    }
    
    /// <summary>
    /// Addressables runtime key. Can be used in any form Addressables supports, such as asset name, label, or address.
    /// </summary>
    [UnityAddressablesRuntimeKey]
    public string RuntimeKey;
    
    [NonSerialized]
    private int _acquireCount;

    [NonSerialized] 
    private AsyncOperationHandle _op;

    /// <inheritdoc cref="QuantumAssetSourceResource{T}.Acquire"/>
    public void Acquire(bool synchronous) {
      if (_acquireCount == 0) {
        LoadInternal(synchronous);
      }
      _acquireCount++;
    }

    /// <inheritdoc cref="QuantumAssetSourceResource{T}.Release"/>
    public void Release() {
      if (_acquireCount <= 0) {
        throw new Exception("Asset is not loaded");
      }
      if (--_acquireCount == 0) {
        UnloadInternal();
      }
    }

    /// <inheritdoc cref="QuantumAssetSourceResource{T}.IsCompleted"/>
    public bool IsCompleted => _op.IsDone;

    /// <inheritdoc cref="QuantumAssetSourceResource{T}.WaitForResult"/>
    public T WaitForResult() {
      Assert.Check(_op.IsValid());
      if (!_op.IsDone) {
        try {
          _op.WaitForCompletion();
        } catch (Exception e) when (!Application.isPlaying && typeof(Exception) == e.GetType()) {
          LogError?.Log($"An exception was thrown when loading asset: {RuntimeKey}; since this method " +
                        $"was called from the editor, it may be due to the fact that Addressables don't have edit-time load support. Please use EditorInstance instead.");
          throw;
        }
      }
      
      if (_op.OperationException != null) {
        throw new InvalidOperationException($"Failed to load asset: {RuntimeKey}", _op.OperationException);
      }
      
      Assert.Check(_op.Result != null, "_op.Result != null");
      return ValidateResult(_op.Result);
    }
    
    private void LoadInternal(bool synchronous) {
      Assert.Check(!_op.IsValid());

      _op = Addressables.LoadAssetAsync<UnityEngine.Object>(RuntimeKey);
      if (!_op.IsValid()) {
        throw new Exception($"Failed to load asset: {RuntimeKey}");
      }
      if (_op.Status == AsyncOperationStatus.Failed) {
        throw new Exception($"Failed to load asset: {RuntimeKey}", _op.OperationException);
      }
      
      if (synchronous) {
        _op.WaitForCompletion();
      }
    }

    private void UnloadInternal() {
      if (_op.IsValid()) {
        var op = _op;
        _op = default;
        Addressables.Release(op);  
      }
    }

    private T ValidateResult(object result) {
      if (result == null) {
        throw new InvalidOperationException($"Failed to load asset: {RuntimeKey}; asset is null");
      }
      if (typeof(T).IsSubclassOf(typeof(Component))) {
        if (result is GameObject gameObject == false) {
          throw new InvalidOperationException($"Failed to load asset: {RuntimeKey}; asset is not a GameObject, but a {result.GetType()}");
        }
        
        var component = ((GameObject)result).GetComponent<T>();
        if (!component) {
          throw new InvalidOperationException($"Failed to load asset: {RuntimeKey}; asset does not contain component {typeof(T)}");
        }

        return component;
      }

      if (result is T asset) {
        return asset;
      }
      
      throw new InvalidOperationException($"Failed to load asset: {RuntimeKey}; asset is not of type {typeof(T)}, but {result.GetType()}");
    }
    
    /// <inheritdoc cref="QuantumAssetSourceResource{T}.Description"/>
    public string Description => "RuntimeKey: " + RuntimeKey;
    
#if UNITY_EDITOR
    /// <inheritdoc cref="QuantumAssetSourceResource{T}.EditorInstance"/>
    public T EditorInstance => (T)QuantumAddressablesUtils.LoadEditorInstance(RuntimeKey);
#endif
  }
}
#endif

#endregion


#region QuantumAssetSourceResource.cs

namespace Quantum {
  using System;
  using System.Runtime.ExceptionServices;
  using UnityEngine;
  using Object = UnityEngine.Object;
  using UnityResources = UnityEngine.Resources;

  /// <summary>
  /// Resources-based implementation of the asset source pattern.
  /// </summary>
  /// <typeparam name="T"></typeparam>
  [Serializable]
  public partial class QuantumAssetSourceResource<T> where T : UnityEngine.Object {
    
    /// <summary>
    /// Resource path. Note that this is a Unity resource path, not a file path.
    /// </summary>
    [UnityResourcePath(typeof(Object))]
    public string ResourcePath;
    /// <summary>
    /// Sub-object name. If empty, the main object is loaded.
    /// </summary>
    public string SubObjectName;

    [NonSerialized]
    private object _state;
    [NonSerialized]
    private int    _acquireCount;

    /// <summary>
    /// Loads the asset. In synchronous mode, the asset is loaded immediately. In asynchronous mode, the asset is loaded in the background.
    /// </summary>
    /// <param name="synchronous"></param>
    public void Acquire(bool synchronous) {
      if (_acquireCount == 0) {
        LoadInternal(synchronous);
      }
      _acquireCount++;
    }

    /// <summary>
    /// Unloads the asset. If the asset is not loaded, an exception is thrown. If the asset is loaded multiple times, it is only
    /// unloaded when the last acquire is released.
    /// </summary>
    /// <exception cref="Exception"></exception>
    public void Release() {
      if (_acquireCount <= 0) {
        throw new Exception("Asset is not loaded");
      }
      if (--_acquireCount == 0) {
        UnloadInternal();
      }
    }

    /// <summary>
    /// Returns <see langword="true"/> if the asset is loaded.
    /// </summary>
    public bool IsCompleted {
      get {
        if (_state == null) {
          // hasn't started
          return false;
        }
        
        if (_state is ResourceRequest asyncOp && !asyncOp.isDone) {
          // still loading, wait
          return false;
        }

        return true;
      }
    }

    /// <summary>
    /// Blocks until the asset is loaded. If the asset is not loaded, an exception is thrown.
    /// </summary>
    /// <returns>The loaded asset</returns>
    public T WaitForResult() {
      Assert.Check(_state != null);
      if (_state is ResourceRequest asyncOp) {
        if (asyncOp.isDone) {
          FinishAsyncOp(asyncOp);
        } else {
          // just load synchronously, then pass through
          _state = null;
          LoadInternal(synchronous: true);
        }
      }
      
      if (_state == null) {
        throw new InvalidOperationException($"Failed to load asset {typeof(T)}: {ResourcePath}[{SubObjectName}]. Asset is null.");  
      }

      if (_state is T asset) {
        return asset;
      }

      if (_state is ExceptionDispatchInfo exception) {
        exception.Throw();
        throw new NotSupportedException();
      }

      throw new InvalidOperationException($"Failed to load asset {typeof(T)}: {ResourcePath}, SubObjectName: {SubObjectName}");
    }

    private void FinishAsyncOp(ResourceRequest asyncOp) {
      try {
        var asset = string.IsNullOrEmpty(SubObjectName) ? asyncOp.asset : LoadNamedResource(ResourcePath, SubObjectName);
        if (asset) {
          _state = asset;
        } else {
          throw new InvalidOperationException($"Missing Resource: {ResourcePath}, SubObjectName: {SubObjectName}");
        }
      } catch (Exception ex) {
        _state = ExceptionDispatchInfo.Capture(ex);
      }
    }
    
    private static T LoadNamedResource(string resoucePath, string subObjectName) {
      var assets = UnityResources.LoadAll<T>(resoucePath);

      for (var i = 0; i < assets.Length; ++i) {
        var asset = assets[i];
        if (string.Equals(asset.name, subObjectName, StringComparison.Ordinal)) {
          return asset;
        }
      }

      return null;
    }
    
    private void LoadInternal(bool synchronous) {
      Assert.Check(_state == null);
      try {
        if (synchronous) {
          _state = string.IsNullOrEmpty(SubObjectName) ? UnityResources.Load<T>(ResourcePath) : LoadNamedResource(ResourcePath, SubObjectName);
        } else {
          _state = UnityResources.LoadAsync<T>(ResourcePath);
        }

        if (_state == null) {
          _state = new InvalidOperationException($"Missing Resource: {ResourcePath}, SubObjectName: {SubObjectName}");
        }
      } catch (Exception ex) {
        _state = ExceptionDispatchInfo.Capture(ex);
      }
    }

    private void UnloadInternal() {
      if (_state is ResourceRequest asyncOp) {
        asyncOp.completed += op => {
          // unload stuff
        };
      } else if (_state is Object) {
        // unload stuff
      }

      _state = null;
    }
    
    /// <summary>
    /// The description of the asset source. Used for debugging.
    /// </summary>
    public string Description => $"Resource: {ResourcePath}{(!string.IsNullOrEmpty(SubObjectName) ? $"[{SubObjectName}]" : "")}";
    
#if UNITY_EDITOR
    /// <summary>
    /// Returns the asset instance for Editor purposes. Does not call <see cref="Acquire"/>.
    /// </summary>
    public T EditorInstance => string.IsNullOrEmpty(SubObjectName) ? UnityResources.Load<T>(ResourcePath) : LoadNamedResource(ResourcePath, SubObjectName);
#endif
  }
}

#endregion


#region QuantumAssetSourceStatic.cs

namespace Quantum {
  using System;
  using UnityEngine.Serialization;

  /// <summary>
  /// Hard reference-based implementation of the asset source pattern. This asset source forms a hard reference to the asset and never releases it.
  /// This type is meant to be used at runtime. For edit-time, prefer <see cref="QuantumAssetSourceStaticLazy{T}"/>, as it delays
  /// actually loading the asset, improving the editor performance.
  /// </summary>
  /// <typeparam name="T"></typeparam>
  [Serializable]
  public partial class QuantumAssetSourceStatic<T> where T : UnityEngine.Object {

    /// <summary>
    /// The asset reference. Can point to an asset or to a runtime-created object.
    /// </summary>
    [FormerlySerializedAs("Prefab")]
    public T Object;
    
    /// <see cref="Object"/>
    [Obsolete("Use Asset instead")]
    public T Prefab {
      get => Object;
      set => Object = value;
    }
    
    /// <summary>
    /// Returns <see langword="true"/>.
    /// </summary>
    public bool IsCompleted => true;

    /// <summary>
    /// Does nothing, the asset is always loaded.
    /// </summary>
    public void Acquire(bool synchronous) {
      // do nothing
    }

    /// <summary>
    /// Does nothing, the asset is always loaded.
    /// </summary>
    public void Release() {
      // do nothing
    }

    /// <summary>
    /// Returns <seealso cref="Object"/> or throws an exception if the reference is missing.
    /// </summary>
    public T WaitForResult() {
      if (Object == null) {
        throw new InvalidOperationException("Missing static reference");
      }

      return Object;
    }
    
    /// <inheritdoc cref="QuantumAssetSourceResource{T}.Description"/>
    public string Description {
      get {
        if (Object) {
#if UNITY_EDITOR
          if (UnityEditor.AssetDatabase.TryGetGUIDAndLocalFileIdentifier(Object, out var guid, out long fileID)) {
            return $"Static: {guid}, fileID: {fileID}";
          }
#endif
          return "Static: " + Object;
        } else {
          return "Static: (null)";
        }
      }
    }
    
#if UNITY_EDITOR
    /// <summary>
    /// Returns <seealso cref="Object"/>.
    /// </summary>
    public T EditorInstance => Object;
#endif
  }
}

#endregion


#region QuantumAssetSourceStaticLazy.cs

namespace Quantum {
  using System;
  using UnityEngine;
  using UnityEngine.Serialization;

  /// <summary>
  /// An edit-time optimised version of <see cref="QuantumAssetSourceStatic{T}"/>, taking advantage of Unity's lazy loading of
  /// assets. At runtime, this type behaves exactly like <see cref="QuantumAssetSourceStatic{T}"/>, except for the inability
  /// to use runtime-created objects.
  /// </summary>
  /// <typeparam name="T"></typeparam>
  [Serializable]
  public partial class QuantumAssetSourceStaticLazy<T> where T : UnityEngine.Object {
    
    /// <summary>
    /// The asset reference. Can only point to an asset, runtime-created objects will not work.
    /// </summary>
    [FormerlySerializedAs("Prefab")] 
    public LazyLoadReference<T> Object;
    
    /// <inheritdoc cref="QuantumAssetSourceStatic{T}.Prefab"/>
    [Obsolete("Use Object instead")]
    public LazyLoadReference<T> Prefab {
      get => Object;
      set => Object = value;
    }
    
    /// <inheritdoc cref="QuantumAssetSourceStatic{T}.IsCompleted"/>
    public bool IsCompleted => true;
    
    /// <inheritdoc cref="QuantumAssetSourceStatic{T}.Acquire"/>
    public void Acquire(bool synchronous) {
      // do nothing
    }
    
    /// <inheritdoc cref="QuantumAssetSourceStatic{T}.Release"/>
    public void Release() {
      // do nothing
    }
    
    /// <inheritdoc cref="QuantumAssetSourceStatic{T}.WaitForResult"/>
    public T WaitForResult() {
      if (Object.asset == null) {
        throw new InvalidOperationException("Missing static reference");
      }

      return Object.asset;
    }
    
    /// <inheritdoc cref="QuantumAssetSourceStatic{T}.Description"/>
    public string Description {
      get {
        if (Object.isBroken) {
          return "Static: (broken)";
        } else if (Object.isSet) {
#if UNITY_EDITOR
          if (UnityEditor.AssetDatabase.TryGetGUIDAndLocalFileIdentifier(Object.instanceID, out var guid, out long fileID)) {
            return $"Static: {guid}, fileID: {fileID}";
          }
#endif
          return "Static: " + Object.asset;
        } else {
          return "Static: (null)";
        }
      }
    }
    
#if UNITY_EDITOR
    /// <inheritdoc cref="QuantumAssetSourceStatic{T}.EditorInstance"/>
    public T EditorInstance => Object.asset;
#endif
  }
}

#endregion


#region QuantumGlobalScriptableObjectAddressAttribute.cs

namespace Quantum {
  using System;
  using UnityEngine.Scripting;
#if (QUANTUM_ADDRESSABLES || QUANTUM_ENABLE_ADDRESSABLES) && !QUANTUM_DISABLE_ADDRESSABLES 
  using UnityEngine.AddressableAssets;
  using UnityEngine.ResourceManagement.AsyncOperations;
#endif
  using static InternalLogStreams;
  
  /// <summary>
  /// If applied at the assembly level, allows <see cref="QuantumGlobalScriptableObject{T}"/> to be loaded with Addressables.
  /// </summary>
  [Preserve]
  public class QuantumGlobalScriptableObjectAddressAttribute : QuantumGlobalScriptableObjectSourceAttribute {
    /// <param name="objectType">The type this attribute will attempt to load.</param>
    /// <param name="address">The address to load from.</param>
    public QuantumGlobalScriptableObjectAddressAttribute(Type objectType, string address) : base(objectType) {
      Address = address;
    }

    /// <summary>
    /// The address to load from.
    /// </summary>
    public string Address { get; }
    
    /// <summary>
    /// Loads the asset from the <see cref="Address"/>. Uses WaitForCompletion internally, so platforms that do not support it need
    /// to preload the address prior to loading.
    /// </summary>
    public override QuantumGlobalScriptableObjectLoadResult Load(Type type) {
#if (QUANTUM_ADDRESSABLES || QUANTUM_ENABLE_ADDRESSABLES) && !QUANTUM_DISABLE_ADDRESSABLES
      Assert.Check(!string.IsNullOrEmpty(Address));
      
      var op = Addressables.LoadAssetAsync<QuantumGlobalScriptableObject>(Address);
      var instance = op.WaitForCompletion();
      if (op.Status == AsyncOperationStatus.Succeeded) {
        Assert.Check(instance);
        return new (instance, x => Addressables.Release(op));
      }
      
      
      LogTrace?.Log($"Failed to load addressable at address {Address} for type {type.FullName}: {op.OperationException}");
      return default;
#else
      LogTrace?.Log($"Addressables are not enabled. Unable to load addressable for {type.FullName}");
      return default;
#endif
    }
  }
}

#endregion


#region QuantumGlobalScriptableObjectResourceAttribute.cs

namespace Quantum {
  using System;
  using System.IO;
  using System.Reflection;
  using UnityEngine;
  using UnityEngine.Scripting;
  using Object = UnityEngine.Object;
  using static InternalLogStreams;
  
  /// <summary>
  /// If applied at the assembly level, allows <see cref="QuantumGlobalScriptableObject{T}"/> to be loaded with Resources.
  /// There is a default registration for this attribute, which attempts to load the asset from Resources using path from
  /// <see cref="QuantumGlobalScriptableObjectAttribute"/>.
  /// </summary>
  [Preserve]
  public class QuantumGlobalScriptableObjectResourceAttribute : QuantumGlobalScriptableObjectSourceAttribute {
    /// <param name="objectType">The type this attribute will attempt to load.</param>
    /// <param name="resourcePath">Resources path or <see langword="null"/>/empty if path from <see cref="QuantumGlobalScriptableObjectAttribute"/>
    /// is to be used.</param>
    public QuantumGlobalScriptableObjectResourceAttribute(Type objectType, string resourcePath = "") : base(objectType) {
      ResourcePath = resourcePath;
    }
    
    /// <summary>
    /// Path in Resources.
    /// </summary>
    public string ResourcePath { get; }
    /// <summary>
    /// If loaded in the editor, should the result be instantiated instead of returning the asset itself? The default is <see langword="true"/>. 
    /// </summary>
    public bool InstantiateIfLoadedInEditor { get; set; } = true;
    
    /// <summary>
    /// Loads the asset from Resources synchronously.
    /// </summary>
    public override QuantumGlobalScriptableObjectLoadResult Load(Type type) {
      
      var attribute = type.GetCustomAttribute<QuantumGlobalScriptableObjectAttribute>();
      Assert.Check(attribute != null);

      string resourcePath;
      if (string.IsNullOrEmpty(ResourcePath)) {
        string defaultAssetPath = attribute.DefaultPath;
        var indexOfResources = defaultAssetPath.LastIndexOf("/Resources/", StringComparison.OrdinalIgnoreCase);
        if (indexOfResources < 0) {
          LogTrace?.Log($"The default path {defaultAssetPath} does not contain a /Resources/ folder. Unable to load resource for {type.FullName}.");
          return default;
        }

        // try to load from resources, maybe?
        resourcePath = defaultAssetPath.Substring(indexOfResources + "/Resources/".Length);

        // drop the extension
        if (Path.HasExtension(resourcePath)) {
          resourcePath = resourcePath.Substring(0, resourcePath.LastIndexOf('.'));
        }
      } else {
        resourcePath = ResourcePath;
      }

      var instance = UnityEngine.Resources.Load(resourcePath, type);
      if (!instance) {
        LogTrace?.Log($"Unable to load resource at path {resourcePath} for type {type.FullName}");
        return default;
      }

      if (InstantiateIfLoadedInEditor && Application.isEditor) {
        var clone = Object.Instantiate(instance);
        return new((QuantumGlobalScriptableObject)clone, x => Object.Destroy(clone));
      } else {
        return new((QuantumGlobalScriptableObject)instance, x => UnityEngine.Resources.UnloadAsset(instance));  
      }
    }
  }
}

#endregion



#endregion


#region Assets/Photon/Quantum/Runtime/QuantumAsyncOperationExtension.cs

namespace Quantum {
  using System;
  using System.Runtime.CompilerServices;
  using System.Threading.Tasks;
  using UnityEngine;

  public static class QuantumAsyncOperationExtension {
    public static TaskAwaiter GetAwaiter(this AsyncOperation asyncOperation) {
      return asyncOperation.ToTask().GetAwaiter();
    }

    public static System.Threading.Tasks.Task ToTask(this AsyncOperation asyncOperation) {
      if (asyncOperation == null) {
        return System.Threading.Tasks.Task.FromException(new Exception("Operation failed"));
      }

      var completionSource = new TaskCompletionSource<bool>();
      asyncOperation.completed += (a) => {
        completionSource.TrySetResult(true);
      };

      return (System.Threading.Tasks.Task)completionSource.Task;
    }
  }
}

#endregion


#region Assets/Photon/Quantum/Runtime/QuantumCallbacks.cs

namespace Quantum {
  using System;
  using System.Collections.Generic;
  using Photon.Analyzer;
  using Photon.Deterministic;

  /// <summary>
  /// A legacy way to hook into Quantum game callbacks.
  /// Use the publish subscribe pattern used by <see cref="QuantumCallbacks"/> instead.
  /// To use this callback class, derive from it and implement the methods you are interested in.
  /// The callback MonoBehaviour has to be added to the scene to work.
  /// </summary>
  public abstract class QuantumCallbacks : QuantumMonoBehaviour {
    /// <summary>
    /// Static list of all instances of QuantumCallbacks to call on Quantum callbacks.
    /// Populated on OnEnable and OnDisable.
    /// </summary>
    [StaticField]
    public static readonly List<QuantumCallbacks> Instances = new List<QuantumCallbacks>();

    /// <summary>
    /// Unity OnEnable event registers this instance to the static list called by the Quantum callbacks.
    /// </summary>
    protected virtual void OnEnable() {
      Instances.Add(this);
    }

    /// <summary>
    /// Unity OnDisable event removes itself from the static callback list.
    /// </summary>
    protected virtual void OnDisable() {
      Instances.Remove(this);
    }

    /// <summary>
    /// Is called by <see cref="CallbackGameInit"/> during <see cref="QuantumGame.OnGameStart(DeterministicFrame)"/>
    /// when the game is about to start.
    /// </summary>
    /// <param name="game">The Quantum game</param>
    /// <param name="isResync">Is true when the simulation is paused and waits for snapshot to commence the start.</param>
    public virtual void OnGameInit(QuantumGame game, bool isResync) { }
    /// <summary>
    /// Obsolete: use other OnGameStart overload.
    /// </summary>
    /// <param name="game"></param>
    [Obsolete("Use OnGameStart(QuantumGame game, bool isResync)")]
    public virtual void OnGameStart(QuantumGame game) { }
    /// <summary>
    /// Is called by <see cref="CallbackGameStarted"/> during 
    /// <see cref="QuantumGame.OnGameStart"/> or <see cref="QuantumGame.OnGameResync"/>"
    /// when the game is started after systems are initialized and the snapshot has arrived
    /// for late-joining clients.
    /// </summary>
    /// <param name="game">Quantum game</param>
    /// <param name="isResync">Is true if the game was started from a snapshot.</param>
    public virtual void OnGameStart(QuantumGame game, bool isResync) { }
    /// <summary>
    /// Is called by <see cref="CallbackGameResynced"/> during <see cref="QuantumGame.OnGameResync"/> when 
    /// the game has been re-synchronized from a snapshot and is about to start.
    /// </summary>
    /// <param name="game">Quantum game</param>
    public virtual void OnGameResync(QuantumGame game) { }
    /// <summary>
    /// Is called by <see cref="CallbackGameDestroyed"/> when the session has been destroyed."/>
    /// </summary>
    /// <param name="game">Quantum game</param>
    public virtual void OnGameDestroyed(QuantumGame game) { }
    /// <summary>
    /// Is called by <see cref="CallbackUpdateView"/> which is originally 
    /// called by <see cref="QuantumRunner.Update"/> and it is called every Unity frame.
    /// </summary>
    /// <param name="game">Quantum game</param>
    public virtual void OnUpdateView(QuantumGame game) { }
    /// <summary>
    /// Is called by <see cref="CallbackSimulateFinished"/> after completing the simulation of a frame.
    /// </summary>
    /// <param name="game">Quantum game</param>
    /// <param name="frame">Completed frame</param>
    public virtual void OnSimulateFinished(QuantumGame game, Frame frame) { }
    /// <summary>
    /// Is called by <see cref="CallbackUnitySceneLoadBegin"/> when a Unity scene is about to be loaded.
    /// To enable this feature <see cref="SimulationConfig.AutoLoadSceneFromMap"/> must be toggled on.
    /// </summary>
    /// <param name="game">Quantum game</param>
    public virtual void OnUnitySceneLoadBegin(QuantumGame game) { }
    /// <summary>
    /// Is called by <see cref="CallbackUnitySceneLoadDone"/> when a Unity scene has been loaded.
    /// To enable this feature <see cref="SimulationConfig.AutoLoadSceneFromMap"/> must be toggled on.
    /// </summary>
    /// <param name="game">Quantum game</param>
    public virtual void OnUnitySceneLoadDone(QuantumGame game) { }
    /// <summary>
    /// Is called by <see cref="CallbackUnitySceneUnloadBegin"/> when a Unity scene is about to be unloaded.
    /// To enable this feature <see cref="SimulationConfig.AutoLoadSceneFromMap"/> must be toggled on.
    /// </summary>
    /// <param name="game">Quantum game</param>
    public virtual void OnUnitySceneUnloadBegin(QuantumGame game) { }
    /// <summary>
    /// Is called by <see cref="CallbackUnitySceneUnloadDone"/> when a Unity scene has been unloaded.
    /// </summary>
    /// <param name="game">Quantum game</param>
    public virtual void OnUnitySceneUnloadDone(QuantumGame game) { }
    /// <summary>
    /// Is called by <see cref="CallbackChecksumError"/> when a checksum error is detected.
    /// To enable this feature <see cref="SimulationConfig.AutoLoadSceneFromMap"/> must be toggled on.
    /// </summary>
    /// <param name="game">Quantum game</param>
    /// <param name="error">Error description</param>
    /// <param name="frames">List of latest frames</param>
    public virtual void OnChecksumError(QuantumGame game, DeterministicTickChecksumError error, Frame[] frames) { }
  }
}

#endregion


#region Assets/Photon/Quantum/Runtime/QuantumFrameDifferGUI.cs

namespace Quantum {
  using System;
  using System.Collections.Generic;
  using System.Linq;
  using System.Text;
  using System.Threading.Tasks;
  using Photon.Deterministic;
  using UnityEngine;

  /// <summary>
  /// The actual GUI to render the frame dumps differences of multiple dumps send by different clients.
  /// Uses Unity Immediate Mode GUI.
  /// </summary>
  public abstract class QuantumFrameDifferGUI {
    const float HeaderHeight = 28.0f;

    /// <summary>
    /// The actor id to use as a reference state when comparing differences.
    /// </summary>
    public int ReferenceActorId = 0;

    /// <summary>
    /// True if the GUI is not rendered.
    /// </summary>
    protected Boolean _hidden;

    String _search = "";
    String _gameId;
    Int32 _scrollOffset;

    /// <summary>
    /// Create a new GUI frame differ instance using the given state.
    /// </summary>
    /// <param name="state"></param>
    protected QuantumFrameDifferGUI(FrameDifferState state) {
      State = state;
    }

    /// <summary>
    /// Get or set the state of the frame differ.
    /// </summary>
    public FrameDifferState State { get; set; }

    /// <summary>
    /// Returns true or derived classes if the GUI is running inside the Unity Editor.
    /// </summary>
    public virtual Boolean IsEditor {
      get { return false; }
    }

    /// <summary>
    /// Returns the text line height. Default is 16.
    /// Can be customized in derived classes.
    /// </summary>
    public virtual Int32 TextLineHeight {
      get { return 16; }
    }

    /// <summary>
    /// Returns the background style.
    /// Can be customized in derived classes.
    /// </summary>
    public virtual GUIStyle DiffBackground {
      get { return GUI.skin.box; }
    }

    /// <summary>
    /// Returns the header style.
    /// Can be customized in derived classes.
    /// </summary>
    public virtual GUIStyle DiffHeader {
      get { return GUI.skin.box; }
    }

    /// <summary>
    /// Returns the error header style.
    /// Can be customized in derived classes.
    /// </summary>
    public virtual GUIStyle DiffHeaderError {
      get { return GUI.skin.box; }
    }

    /// <summary>
    /// Returns the line overlay style.
    /// Can be customized in derived classes.
    /// </summary>
    public virtual GUIStyle DiffLineOverlay {
      get { return GUI.skin.textField; }
    }

    /// <summary>
    /// Returns the button style.
    /// Can be customized in derived classes.
    /// </summary>
    public virtual GUIStyle MiniButton {
      get { return GUI.skin.button; }
    }

    /// <summary>
    /// Returns the text label style.
    /// Can be customized in derived classes.
    /// </summary>
    public virtual GUIStyle TextLabel {
      get { return GUI.skin.label; }
    }

    /// <summary>
    /// Returns the bold label style.
    /// Can be customized in derived classes.
    /// </summary>
    public virtual GUIStyle BoldLabel {
      get { return GUI.skin.label; }
    }

    /// <summary>
    /// Returns the left button style.
    /// </summary>
    public virtual GUIStyle MiniButtonLeft {
      get { return GUI.skin.button; }
    }

    /// <summary>
    /// Returns the right button style.
    /// </summary>
    public virtual GUIStyle MiniButtonRight {
      get { return GUI.skin.button; }
    }

    /// <summary>
    /// Returns the position of the screen/window.
    /// </summary>
    public abstract Rect Position {
      get;
    }

    /// <summary>
    /// Returns the scroll width. Default is 16.
    /// </summary>
    public virtual float ScrollWidth => 16.0f;

    private StringComparer Comparer => StringComparer.InvariantCulture;

    /// <summary>
    /// Toggles a windows repaint.
    /// </summary>
    public virtual void Repaint() {
    }

    /// <summary>
    /// Draws the header of the GUI.
    /// </summary>
    public abstract void DrawHeader();


    /// <summary>
    /// Toggles the _hidden flag.
    /// </summary>
    public void Show() {
      _hidden = false;
    }

    /// <summary>
    /// Should be invoked from Unity OnGUI method to draw the GUI.
    /// </summary>
    public void OnGUI() {
      if (Event.current.type == EventType.ScrollWheel) {
        _scrollOffset += (int)(Event.current.delta.y * 1);
        Repaint();
      }

      DrawSelection();

      if (State?.RunnerIds.Any() != true) {
        DrawNoDumps();
        return;
      }

      DrawDiff();
    }

    void DrawNoDumps() {
      GUILayout.BeginVertical();
      GUILayout.FlexibleSpace();
      GUILayout.BeginHorizontal();
      GUILayout.FlexibleSpace();
      GUILayout.Label("No currently active diffs");
      GUILayout.FlexibleSpace();
      GUILayout.EndHorizontal();
      GUILayout.FlexibleSpace();
      GUILayout.EndVertical();
    }

    void DrawSelection() {
      GUILayout.Space(5);
      using (new GUILayout.HorizontalScope()) {
        try {
          DrawHeader();

          if (GUILayout.Button("Clear", MiniButton, GUILayout.Height(16))) {
            State.Clear();
          }

          if (_hidden) {
            return;
          }

          GUILayout.Space(16);

          GUIStyle styleSelectedButton;
          styleSelectedButton        = new GUIStyle(MiniButton);
          styleSelectedButton.normal = styleSelectedButton.active;

          // select the first game if not selected
          if (_gameId == null || !State.RunnerIds.Contains(_gameId)) {
            _gameId = State.RunnerIds.FirstOrDefault();
          }

          foreach (var gameId in State.RunnerIds) {
            if (GUILayout.Button(gameId, gameId == _gameId ? styleSelectedButton : MiniButton, GUILayout.Height(16))) {
              _gameId = gameId;
            }
          }
        } finally {
          GUILayout.FlexibleSpace();
        }
      }

      Rect topBarRect;
      topBarRect        =  CalculateTopBarRect();
      topBarRect.x      =  (topBarRect.width - 200) - 3;
      topBarRect.width  =  200;
      topBarRect.height =  18;
      topBarRect.y      += 3;

      var currentSearch = _search;

      _search = GUI.TextField(topBarRect, _search ?? "");

      if (currentSearch != _search) {
        Search(GetSelectedFrameData().Values.FirstOrDefault(), 0, +1);
      }

      Rect prevButtonRect;
      prevButtonRect        =  topBarRect;
      prevButtonRect.height =  16;
      prevButtonRect.width  =  50;
      prevButtonRect.x      -= 102;
      prevButtonRect.y      += 1;

      if (GUI.Button(prevButtonRect, "Prev", MiniButtonLeft)) {
        Search(GetSelectedFrameData().Values.FirstOrDefault(), _scrollOffset - 1, -1);
      }

      Rect nextButtonRect;
      nextButtonRect   =  prevButtonRect;
      nextButtonRect.x += 50;

      if (GUI.Button(nextButtonRect, "Next", MiniButtonRight)) {
        Search(GetSelectedFrameData().Values.FirstOrDefault(), _scrollOffset + 1, +1);
      }
    }

    void DrawDiff() {
      if (_hidden) {
        return;
      }

      var frameData = GetSelectedFrameData();
      if (frameData == null) {
        return;
      }

      // set of lines that are currently being drawn and have diffs
      List<Rect> modified = new List<Rect>();
      List<Rect> added    = new List<Rect>();
      List<Rect> removed  = new List<Rect>();

      // main background rect
      Rect mainRect;
      mainRect = CalculateMainRect(frameData.Count);

      var scrollBarRect = Position;
      scrollBarRect.y      =  25;
      scrollBarRect.height -= 25;
      scrollBarRect.x      =  scrollBarRect.width - ScrollWidth;
      scrollBarRect.width  =  ScrollWidth;

      // header rect for drawing title/prev/next background
      Rect headerRect;
      headerRect        =  Position;
      headerRect.x      =  4;
      headerRect.y      =  HeaderHeight;
      headerRect.width  -= ScrollWidth;
      headerRect.width  /= frameData.Count;
      headerRect.width  -= 8;
      headerRect.height =  23;

      if (!frameData.TryGetValue(ReferenceActorId, out var baseFrame)) {
        ReferenceActorId = frameData.Keys.OrderBy(x => x).First();
        baseFrame        = frameData[ReferenceActorId];
      }

      var visibleRows = Mathf.FloorToInt((mainRect.height - HeaderHeight) / TextLineHeight);
      var maxScroll   = Math.Max(0, baseFrame.Lines.Count - visibleRows);

      if (visibleRows > maxScroll) {
        _scrollOffset = 0;
        GUI.VerticalScrollbar(scrollBarRect, 0, 1, 0, 1);
      } else {
        _scrollOffset = Mathf.RoundToInt(GUI.VerticalScrollbar(scrollBarRect, _scrollOffset, visibleRows, 0, baseFrame.Lines.Count));
      }

      foreach (var kvp in frameData.OrderBy(x => x.Key)) {
        GUI.Box(mainRect, "", DiffBackground);

        // draw lines
        for (Int32 i = 0; i < 100; ++i) {
          var lineIndex = _scrollOffset + i;
          if (lineIndex < kvp.Value.Lines.Count) {
            var line     = kvp.Value.Lines[lineIndex];
            var baseLine = baseFrame.Lines[lineIndex];

            var r = CalculateLineRect(i, mainRect);

            // label
            if (line == null) {
              if (baseLine != null) {
                removed.Add(r);
              }
            } else {
              GUI.Label(r, line, TextLabel);
              if (baseLine == null) {
                added.Add(r);
              } else if (!Comparer.Equals(line, baseFrame.Lines[lineIndex])) {
                modified.Add(r);
              }
            }
          }
        }

        // draw header background
        if (kvp.Value.Diffs > 0) {
          GUI.Box(headerRect, "", DiffHeaderError);
        } else {
          GUI.Box(headerRect, "", DiffHeader);
        }

        // title label 
        Rect titleRect;
        titleRect       =  headerRect;
        titleRect.width =  headerRect.width / 2;
        titleRect.y     += 3;
        titleRect.x     += 3;

        var title = String.Format("Client {0}, Diffs: {1}", kvp.Key, kvp.Value.Diffs);
        if (string.IsNullOrEmpty(kvp.Value.Title) == false) {
          title = String.Format("{0}, Client {1}, Diffs {2}", kvp.Value.Title, kvp.Key, kvp.Value.Diffs);
        }

        GUI.Label(titleRect, title, BoldLabel);

        // disable group for prev/next buttons
        GUI.enabled = kvp.Value.Diffs > 0;

        // base button
        Rect setAsReferenceButton = titleRect;
        setAsReferenceButton.height = 15;
        setAsReferenceButton.width  = 60;
        setAsReferenceButton.x      = headerRect.x + (headerRect.width - 195);

        GUI.enabled = (ReferenceActorId != kvp.Key);
        if (GUI.Button(setAsReferenceButton, "Reference", MiniButton)) {
          ReferenceActorId = kvp.Key;
          Diff(frameData);
          GUIUtility.ExitGUI();
        }

        GUI.enabled = true;

        // next button
        Rect nextButtonRect;
        nextButtonRect   =  setAsReferenceButton;
        nextButtonRect.x += 65;

        if (GUI.Button(nextButtonRect, "Next Diff", MiniButton)) {
          SearchDiff(kvp.Value, baseFrame, _scrollOffset + 1, +1);
        }

        // prev button
        Rect prevButtonRect;
        prevButtonRect   =  nextButtonRect;
        prevButtonRect.x += 65;

        if (GUI.Button(prevButtonRect, "Prev Diff", MiniButton)) {
          SearchDiff(kvp.Value, baseFrame, _scrollOffset - 1, -1);
        }

        GUI.enabled = true;

        mainRect.x   += mainRect.width;
        headerRect.x += mainRect.width;
      }

      mainRect = CalculateMainRect(frameData.Count);


      // store gui color
      var c = GUI.color;

      // override with semi red & draw diffing lines overlays
      {
        GUI.color = new Color(1, 0.6f, 0, 0.25f);
        foreach (var diff in modified) {
          GUI.Box(diff, "", DiffLineOverlay);
        }
      }
      {
        GUI.color = new Color(0, 1, 0, 0.25f);
        foreach (var diff in added) {
          GUI.Box(diff, "", DiffLineOverlay);
        }
      }
      {
        GUI.color = new Color(1, 0, 0, 0.25f);
        foreach (var diff in removed) {
          GUI.Box(diff, "", DiffLineOverlay);
        }
      }

      // restore gui color
      GUI.color = c;
    }

    Rect CalculateLineRect(Int32 line, Rect mainRect) {
      Rect r = mainRect;
      r.height =  TextLineHeight;
      r.y      += HeaderHeight;
      r.y      += line * TextLineHeight;
      r.x      += 4;
      r.width  -= 8;

      return r;
    }

    Rect CalculateTopBarRect() {
      Rect mainRect;
      mainRect        = Position;
      mainRect.x      = 0;
      mainRect.y      = 0;
      mainRect.height = 25;
      return mainRect;
    }

    Rect CalculateMainRect(Int32 frameDataCount) {
      Rect mainRect;
      mainRect        =  Position;
      mainRect.x      =  0;
      mainRect.y      =  25;
      mainRect.width  -= ScrollWidth;
      mainRect.width  /= frameDataCount;
      mainRect.height -= mainRect.y;
      return mainRect;
    }

    void SearchDiff(FrameData frameData, FrameData baseFrame, Int32 startIndex, Int32 searchDirection) {
      for (Int32 i = startIndex; i >= 0 && i < frameData.Lines.Count; i += searchDirection) {
        if (!Comparer.Equals(baseFrame.Lines[i], frameData.Lines[i])) {
          _scrollOffset = i;
          break;
        }
      }
    }

    void Search(FrameData frameData, Int32 startIndex, Int32 searchDirection) {
      var term = _search ?? "";
      if (term.Length > 0) {
        for (Int32 i = startIndex; i >= 0 && i < frameData.Lines.Count; i += searchDirection) {
          if (frameData.Lines[i].Contains(term)) {
            _scrollOffset = i;
            break;
          }
        }
      }
    }


    Dictionary<Int32, FrameData> GetSelectedFrameData() {
      var frames = State.GetFirstFrameDiff(_gameId, out int frameNumber);
      if (frames == null)
        return null;

      foreach (var frame in frames.Values) {
        if (!frame.Initialized) {
          Diff(frames);
          break;
        }
      }

      return frames;
    }

    void Diff(Dictionary<Int32, FrameData> frames) {
      foreach (var frame in frames.Values) {
        frame.Initialized = false;
        frame.Diffs       = 0;
        frame.Lines.Clear();
      }

      // diff all lines
      if (!frames.TryGetValue(ReferenceActorId, out var baseFrame)) {
        ReferenceActorId = frames.Keys.OrderBy(x => x).First();
        baseFrame        = frames[ReferenceActorId];
      }

      var otherFrames = frames.Where(x => x.Key != ReferenceActorId).OrderBy(x => x.Key).Select(x => x.Value).ToArray();

      var splits    = new[] { "\r\n", "\r", "\n" };
      var baseLines = baseFrame.String.Split(splits, StringSplitOptions.None);

      var diffs = new List<ValueTuple<string, string>>[otherFrames.Length];

      // compute lcs
      Parallel.For(0, otherFrames.Length, () => new LongestCommonSequence(), (frameIndex, state, lcs) => {
        var frameLines = otherFrames[frameIndex].String.Split(splits, StringSplitOptions.None);
        otherFrames[frameIndex].Diffs = 0;

        var chunks = new List<LongestCommonSequence.DiffChunk>();
        lcs.Diff(baseLines, frameLines, Comparer, chunks);

        var diff = new List<ValueTuple<string, string>>();

        int baseLineIndex  = 0;
        int frameLineIndex = 0;

        foreach (var chunk in chunks) {
          int sameCount = chunk.StartA - baseLineIndex;
          Debug.Assert(chunk.StartB - frameLineIndex == sameCount);

          int modifiedCount = Mathf.Min(chunk.AddedA, chunk.AddedB);
          otherFrames[frameIndex].Diffs += Mathf.Max(chunk.AddedA, chunk.AddedB);

          for (int i = 0; i < sameCount + modifiedCount; ++i) {
            diff.Add((baseLines[baseLineIndex++], frameLines[frameLineIndex++]));
          }

          for (int i = 0; i < chunk.AddedA - modifiedCount; ++i) {
            diff.Add((baseLines[baseLineIndex++], default));
          }

          for (int i = 0; i < chunk.AddedB - modifiedCount; ++i) {
            diff.Add((default, frameLines[frameLineIndex++]));
          }
        }

        Debug.Assert(frameLines.Length - frameLineIndex == baseLines.Length - baseLineIndex);
        for (int i = 0; i < frameLines.Length - frameLineIndex; ++i) {
          diff.Add((baseLines[baseLineIndex + i], frameLines[frameLineIndex + i]));
        }

        diffs[frameIndex] = diff;
        return lcs;
      }, lcs => { });

      int[] prevIndices  = new int[otherFrames.Length];
      int[] paddingCount = new int[otherFrames.Length];

      // reconstruct
      for (int baseIndex = 0; baseIndex < baseLines.Length; ++baseIndex) {
        var baseLine = baseLines[baseIndex];
        for (int diffIndex = 0; diffIndex < diffs.Length; ++diffIndex) {
          var diff = diffs[diffIndex];

          int newLines  = 0;
          int prevIndex = prevIndices[diffIndex];

          for (int i = prevIndex; i < diff.Count; ++i, ++newLines) {
            if (diff[i].Item1 == null) {
              // skip
            } else {
              Debug.Assert(ReferenceEquals(diff[i].Item1, baseLine));
              break;
            }
          }

          paddingCount[diffIndex] = newLines;
        }

        // this is how many lines need to be insert
        int maxPadding = otherFrames.Length > 0 ? paddingCount.Max() : 0;
        Debug.Assert(maxPadding >= 0);

        for (int i = 0; i < maxPadding; ++i) {
          baseFrame.Lines.Add(null);
        }

        baseFrame.Lines.Add(baseLine);

        for (int diffIndex = 0; diffIndex < diffs.Length; ++diffIndex) {
          var diff    = diffs[diffIndex];
          var padding = paddingCount[diffIndex];

          for (int i = 0; i < padding; ++i) {
            otherFrames[diffIndex].Lines.Add(diff[prevIndices[diffIndex] + i].Item2);
          }

          for (int i = 0; i < maxPadding - padding; ++i) {
            otherFrames[diffIndex].Lines.Add(null);
          }

          otherFrames[diffIndex].Lines.Add(diff[prevIndices[diffIndex] + padding].Item2);

          prevIndices[diffIndex] += padding + 1;
        }
      }

      baseFrame.Initialized = true;
      foreach (var frame in otherFrames) {
        frame.Initialized = true;
      }
    }

    private class LongestCommonSequence {
      public struct DiffChunk {
        public int StartA;
        public int StartB;
        public int AddedA;
        public int AddedB;

        public override string ToString() {
          return $"{StartA}, {StartB}, {AddedA}, {AddedB}";
        }
      }


      private       ushort[,] m_c;
      private const int       MaxSlice = 5000;

      public LongestCommonSequence() {
      }

      public void Diff<T>(T[] x, T[] y, IEqualityComparer<T> comparer, List<DiffChunk> result) {
        //
        int lowerX = 0;
        int lowerY = 0;
        int upperX = x.Length;
        int upperY = y.Length;

        while (lowerX < upperX && lowerY < upperY && comparer.Equals(x[lowerX], y[lowerY])) {
          ++lowerX;
          ++lowerY;
        }

        while (lowerX < upperX && lowerY < upperY && comparer.Equals(x[upperX - 1], y[upperY - 1])) {
          // pending add
          --upperX;
          --upperY;
        }

        int x1;
        int y1;

        // this is not strictly correct, but LCS is memory hungry; let's just split into slices
        for (int x0 = lowerX, y0 = lowerY; x0 < upperX || y0 < upperY; x0 = x1, y0 = y1) {
          x1 = Mathf.Min(upperX, x0 + MaxSlice);
          y1 = Mathf.Min(upperY, y0 + MaxSlice);

          if (x0 == x1) {
            result.Add(new DiffChunk() {
              StartA = x0,
              StartB = y0,
              AddedB = y1 - y0
            });
          } else if (y0 == y1) {
            result.Add(new DiffChunk() {
              StartA = x0,
              StartB = y0,
              AddedA = x1 - x0
            });
          } else {
            var sx = new ArraySegment<T>(x, x0, x1 - x0);
            var sy = new ArraySegment<T>(y, y0, y1 - y0);

            AllocateMatrix(x1 - x0, y1 - y0);
            FillMatrix(m_c, sx, sy, comparer);
            FillDiff(m_c, sx, sy, comparer, result);
            var chunks = new List<DiffChunk>();
            FillDiff(m_c, sx, sy, comparer, chunks);
          }
        }
      }

      private void AllocateMatrix(int x, int y) {
        if (m_c == null) {
          m_c = new ushort[x + 1, y + 1];
        } else {
          int len0 = Math.Max(m_c.GetLength(0), x + 1);
          int len1 = Math.Max(m_c.GetLength(1), y + 1);
          if (len0 > m_c.GetLength(0) || len1 > m_c.GetLength(1)) {
            m_c = new ushort[len0, len1];
          }
        }
      }

      private static void FillMatrix<T>(ushort[,] c, ArraySegment<T> x, ArraySegment<T> y, IEqualityComparer<T> comparer) {
        int xCount  = x.Count;
        int yCount  = y.Count;
        int xOffset = x.Offset - 1;
        int yOffset = y.Offset - 1;

        for (int i = 1; i <= xCount; i++) {
          c[i, 0] = 0;
        }

        for (int i = 1; i <= yCount; i++) {
          c[0, i] = 0;
        }

        for (int i = 1; i <= xCount; i++) {
          for (int j = 1; j <= yCount; j++) {
            if (comparer.Equals(x.Array[i + xOffset], y.Array[j + yOffset])) {
              c[i, j] = (ushort)(c[i - 1, j - 1] + 1);
            } else {
              c[i, j] = Math.Max(c[i - 1, j], c[i, j - 1]);
            }
          }
        }
      }

      private static void FillDiff<T>(ushort[,] c, ArraySegment<T> x, ArraySegment<T> y, IEqualityComparer<T> comparer, List<DiffChunk> result) {
        int startIndex = result.Count;
        int i          = x.Count - 1;
        int j          = y.Count - 1;

        var chunk = new DiffChunk();
        chunk.StartA = x.Offset + x.Count;
        chunk.StartB = y.Offset + y.Count;

        while (i >= 0 || j >= 0) {
          if (i >= 0 && j >= 0 && comparer.Equals(x.Array[x.Offset + i], y.Array[y.Offset + j])) {
            if (chunk.AddedA != 0 || chunk.AddedB != 0) {
              result.Add(chunk);
              chunk = default;
            }

            chunk.StartA = i + x.Offset;
            chunk.StartB = j + y.Offset;
            --i;
            --j;
          } else if (j >= 0 && (i < 0 || c[i + 1, j] >= c[i, j + 1])) {
            Debug.Assert(chunk.AddedA == 0);
            chunk.AddedB++;
            chunk.StartB = j + y.Offset;
            --j;
          } else if (i >= 0 && (j < 0 || c[i + 1, j] < c[i, j + 1])) {
            chunk.AddedA++;
            chunk.StartA = i + x.Offset;
            --i;
          } else {
            throw new NotSupportedException();
          }
        }

        if (chunk.AddedA != 0 || chunk.AddedB != 0) {
          result.Add(chunk);
        }

        result.Reverse(startIndex, result.Count - startIndex);
      }
    }

    [Serializable]
    private class StateEntry {
      public string RunnerId;
      public int ActorId;
      public int FrameNumber;
      public string CompressedFrameDump;
      public string ActorName;

      [NonSerialized]
      public string FrameDump;
    }

    internal class FrameData {
      public String String;
      public Int32 Diffs;
      public List<string> Lines = new List<string>();
      public Boolean Initialized;
      public string Title;
    }

    /// <summary>
    /// The state saves multiple frame dumps.
    /// </summary>
    [Serializable]
    public class FrameDifferState : ISerializationCallbackReceiver {
      [SerializeField]
      private List<StateEntry> Entries = new List<StateEntry>();

      private Dictionary<string, Dictionary<int, Dictionary<int, FrameData>>> _byRunner = new Dictionary<string, Dictionary<int, Dictionary<int, FrameData>>>();

      /// <summary>
      /// Clear the sate.
      /// </summary>
      public void Clear() {
        Entries.Clear();
        _byRunner.Clear();
      }

      /// <summary>
      /// Add an entry (a frame dump) to the frame state.
      /// </summary>
      /// <param name="runnerId">The runner id</param>
      /// <param name="actorId">The actor id</param>
      /// <param name="frameNumber">The frame number</param>
      /// <param name="frameDump">The frame dump as text</param>
      /// <param name="actorName">The actor name if available</param>
      public void AddEntry(string runnerId, int actorId, int frameNumber, string frameDump, string actorName = null) {
        var entry = new StateEntry() {
          RunnerId = runnerId,
          ActorId = actorId,
          FrameDump = frameDump,
          FrameNumber = frameNumber,
          ActorName = actorName
        };
        Entries.Add(entry);
        OnEntryAdded(entry);
      }

      /// <summary>
      /// Is called after the state was loaded from a file to decompress the frame dumps.
      /// </summary>
      public void OnAfterDeserialize() {
        _byRunner.Clear();
        foreach (var entry in Entries) {
          if (!string.IsNullOrEmpty(entry.CompressedFrameDump)) {
            entry.FrameDump = ByteUtils.GZipDecompressString(ByteUtils.Base64Decode(entry.CompressedFrameDump), Encoding.UTF8);
          }

          OnEntryAdded(entry);
        }
      }

      /// <summary>
      /// Is called before the state is saved to a file to compress the frame dumps.
      /// </summary>
      public void OnBeforeSerialize() {
        foreach (var entry in Entries) {
          if (string.IsNullOrEmpty(entry.CompressedFrameDump)) {
            entry.CompressedFrameDump = ByteUtils.Base64Encode(ByteUtils.GZipCompressString(entry.FrameDump, Encoding.UTF8));
          }
        }
      }

      private void OnEntryAdded(StateEntry entry) {
        if (!_byRunner.TryGetValue(entry.RunnerId, out var byFrame)) {
          _byRunner.Add(entry.RunnerId, byFrame = new Dictionary<int, Dictionary<int, FrameData>>());
        }

        if (!byFrame.TryGetValue(entry.FrameNumber, out var byActor)) {
          byFrame.Add(entry.FrameNumber, byActor = new Dictionary<int, FrameData>());
        }

        if (!byActor.ContainsKey(entry.ActorId)) {
          byActor.Add(entry.ActorId, new FrameData() {
            String = entry.FrameDump,
            Title = entry.ActorName
          });
        }
      }

      /// <summary>
      /// Return all runner ids.
      /// </summary>
      public IEnumerable<string> RunnerIds => _byRunner.Keys;

      internal Dictionary<int, FrameData> GetFirstFrameDiff(string runnerId, out int frameNumber) {
        if (_byRunner.TryGetValue(runnerId, out var byFrame)) {
          frameNumber = byFrame.Keys.First();
          return byFrame[frameNumber];
        }

        frameNumber = 0;
        return null;
      }
    }

  }
}

#endregion


#region Assets/Photon/Quantum/Runtime/QuantumInstantReplay.cs

namespace Quantum {
  using System;
  using System.Collections.Generic;
  using System.IO;
  using Core;
  using Photon.Deterministic;
  using UnityEngine;

  /// <summary>
  /// A mode to seek running instant replays to a desired frame.
  /// </summary>
  public enum QuantumInstantReplaySeekMode {
    /// <summary>
    /// Feature disabled.
    /// </summary>
    Disabled,
    /// <summary>
    /// Start from the first snapshot and seek to the desired frame.
    /// </summary>
    FromStartSnapshot,
    /// <summary>
    /// Start from intermediate snapshots if available.
    /// </summary>
    FromIntermediateSnapshots,
  }

  /// <summary>
  /// The instant replay feature.
  /// It can be used as is or used as a base class for custom instant replay implementations.
  /// </summary>
  public sealed class QuantumInstantReplay : IDisposable {
    /// <summary>
    /// We need this to fast forward the simulation and wait until is fully initialized.
    /// </summary>
    public const int InitialFramesToSimulation = 4;

    private bool _loop;
    private QuantumRunner _replayRunner;
    private DeterministicFrameRingBuffer _rewindSnapshots;
    private MemoryStream _inputStream;

    /// <summary>
    /// Returns the frame number the instant replay started from.
    /// </summary>
    public int StartFrame { get; }
    /// <summary>
    /// Returns current frame number of the replay.
    /// </summary>
    public int CurrentFrame => _replayRunner.Game.Frames.Verified.Number;
    /// <summary>
    /// Returns the last frame number of the replay which is usually the end frame of the original game when the instant replay was started.
    /// </summary>
    public int EndFrame { get; }
    /// <summary>
    /// Returns true if the instant replay can seek to a desired frame.
    /// </summary>
    public bool CanSeek => _rewindSnapshots?.Count > 0;
    /// <summary>
    /// Returns true is the instant replay is running.
    /// </summary>
    public bool IsRunning => CurrentFrame < EndFrame;
    /// <summary>
    /// Returns the live Quantum Game.
    /// </summary>
    public QuantumGame LiveGame { get; }
    /// <summary>
    /// Returns the replay Quantum Game or null.
    /// </summary>
    public QuantumGame ReplayGame => _replayRunner?.Game;
    /// <summary>
    /// Returns the progress or normalized time [0..1] of the instant replay.
    /// </summary>
    public float NormalizedTime {
      get {
        var currentFrame = _replayRunner.Game.Frames.Verified.Number;
        float result = (currentFrame - StartFrame) / (float)(EndFrame - StartFrame);
        Debug.Assert(result >= 0.0f);
        return Mathf.Clamp01(result);
      }
    }

    /// <summary>
    /// Create and start an instant replay.
    /// </summary>
    /// <param name="liveGame">The original game.</param>
    /// <param name="length">The time in seconds to rewind the original game and start the instant replay from.</param>
    /// <param name="seekMode">An optional seek mode to seek and rewind the running instant replay.</param>
    /// <param name="loop">Automatically loop the instant replay and never stop.</param>
    /// <exception cref="ArgumentNullException">Is raised when the live game is null.</exception>
    /// <exception cref="ArgumentException">Is raised when no valid snapshot was found to start the replay from.</exception>
    public QuantumInstantReplay(QuantumGame liveGame, float length, QuantumInstantReplaySeekMode seekMode = QuantumInstantReplaySeekMode.Disabled, bool loop = false) {
      if (liveGame == null) {
        throw new ArgumentNullException(nameof(liveGame));
      }

      LiveGame = liveGame;
      EndFrame = liveGame.Frames.Verified.Number;

      var deterministicConfig = liveGame.Session.SessionConfig;
      var desiredReplayFrame = EndFrame - Mathf.FloorToInt(length * deterministicConfig.UpdateFPS);
      // clamp against actual start frame
      desiredReplayFrame = Mathf.Max(deterministicConfig.UpdateFPS, desiredReplayFrame);

      var snapshot = liveGame.GetInstantReplaySnapshot(desiredReplayFrame);
      if (snapshot == null) {
        throw new ArgumentException(nameof(liveGame), "Unable to find a snapshot for frame " + desiredReplayFrame);
      }

      // Chose replay input provider based on if delta compression is enabled.
      var replayInputProvider = default(IDeterministicReplayProvider);
      if (deterministicConfig.InputDeltaCompression) {
        if (liveGame.RecordInputStream != null) {
          liveGame.RecordInputStream.Flush();

          // Seek recorded stream to position 0.
          var recordSteamPosition = liveGame.RecordInputStream.Position;
          liveGame.RecordInputStream.SeekOrThrow(0, SeekOrigin.Begin);

          // Read from the recorded frame until we find the desired start frame.
          StreamReplayInputProvider.ForwardToFrame(liveGame.RecordInputStream, snapshot.Number);

          // Copy part into the memory stream
          _inputStream = new MemoryStream((int)(recordSteamPosition));
          liveGame.RecordInputStream.CopyTo(_inputStream);

          // Reset the recorded steam position
          liveGame.RecordInputStream.SeekOrThrow(recordSteamPosition, SeekOrigin.Begin);

          // Rewind the copied stream
          _inputStream.SeekOrThrow(0, SeekOrigin.Begin);
          replayInputProvider = new StreamReplayInputProvider(_inputStream, liveGame.Session.FrameVerified.Number);
        }
      } else {
        replayInputProvider = liveGame.Session.IsReplay ? liveGame.Session.ReplayProvider : liveGame.RecordedInputs;
      }

      if (replayInputProvider == null) {
        throw new ArgumentException(nameof(liveGame), "Can't run instant replays without an input provider. Start the game with StartParams including RecordingFlags.Input.");
      }

      StartFrame = Mathf.Max(snapshot.Number, desiredReplayFrame);

      List<Frame> snapshotsForRewind = null;
      if (seekMode == QuantumInstantReplaySeekMode.FromIntermediateSnapshots) {
        snapshotsForRewind = new List<Frame>();
        liveGame.GetInstantReplaySnapshots(desiredReplayFrame, EndFrame, snapshotsForRewind);
        Debug.Assert(snapshotsForRewind.Count >= 1);
      } else if (seekMode == QuantumInstantReplaySeekMode.FromStartSnapshot) {
        snapshotsForRewind = new List<Frame>() { snapshot };
      } else if (loop) {
        throw new ArgumentException(nameof(loop), $"Seek mode not compatible with looping: {seekMode}");
      }

      _loop = loop;

      // Create all required start parameters and serialize the snapshot as start data.
      var arguments = new SessionRunner.Arguments {
        RunnerFactory = QuantumRunnerUnityFactory.DefaultFactory,
        GameParameters = QuantumRunnerUnityFactory.CreateGameParameters,
        RuntimeConfig = liveGame.Configurations.Runtime,
        SessionConfig = deterministicConfig,
        ReplayProvider = replayInputProvider,
        GameMode = DeterministicGameMode.Replay,
        FrameData = snapshot.Serialize(DeterministicFrameSerializeMode.Blit),
        InitialTick = snapshot.Number,
        RunnerId = "InstantReplay",
        PlayerCount = deterministicConfig.PlayerCount,
        HeapExtraCount = snapshotsForRewind?.Count ?? 0,
      };

      _replayRunner = QuantumRunner.StartGame(arguments);
      _replayRunner.IsSessionUpdateDisabled = true;

      // Run a couple of frames until fully initialized (replayRunner.Session.FrameVerified is set and session state isRunning).
      for (int i = 0; i < InitialFramesToSimulation; i++) {
        _replayRunner.Session.Update(1.0f / deterministicConfig.UpdateFPS);
      }

      // clone the original snapshots
      Debug.Assert(_rewindSnapshots == null);
      if (snapshotsForRewind != null) {
        _rewindSnapshots = new DeterministicFrameRingBuffer(snapshotsForRewind.Count);
        foreach (var frame in snapshotsForRewind) {
          _rewindSnapshots.PushBack(frame, _replayRunner.Game.CreateFrame);
        }
      }

      if (desiredReplayFrame > CurrentFrame) {
        FastForward(desiredReplayFrame);
      }
    }

    /// <summary>
    /// Stop and dispose the instant replay by clearing cached snapshots and shutting down the replay runner.
    /// </summary>
    public void Dispose() {
      _rewindSnapshots?.Clear();
      _rewindSnapshots = null;
      _replayRunner?.Shutdown();
      _replayRunner = null;
    }

    /// <summary>
    /// Seek to a desired frame number during the running instant replay.
    /// </summary>
    /// <param name="frameNumber">Desired frame number.</param>
    /// <exception cref="InvalidOperationException">Is raised when the replay is not seek-able.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Is raised when the desired frame could not be reached by the cached snapshots.</exception>
    public void SeekFrame(int frameNumber) {
      if (!CanSeek) {
        throw new InvalidOperationException("Not seekable");
      }

      Debug.Assert(_rewindSnapshots != null);
      var frame = _rewindSnapshots.Find(frameNumber, DeterministicFrameSnapshotBufferFindMode.ClosestLessThanOrEqual);
      if (frame == null) {
        throw new ArgumentOutOfRangeException(nameof(frameNumber), $"Unable to find a frame with number less or equal to {frameNumber}.");
      }

      _replayRunner.Session.ResetReplay(frame);
      FastForward(frameNumber);
    }

    /// <summary>
    /// Seek the replay by inputting a normalized time [0..1].
    /// </summary>
    /// <param name="normalizedTime">Replay progress between 0 and 1.</param>
    public void SeekNormalizedTime(float normalizedTime) {
      var frame = Mathf.FloorToInt(Mathf.Lerp(StartFrame, EndFrame, normalizedTime));
      SeekFrame(frame);
    }

    /// <summary>
    /// Updates the instant replay session. Will loop the replay if enabled.
    /// </summary>
    /// <param name="deltaTime">Passed delta time in seconds.</param>
    /// <returns>Returns true is the replay is complete.</returns>
    public bool Update(float deltaTime) {
      _replayRunner.Session.Update(deltaTime);

      // Stop the running instant replay.
      if (_replayRunner.Game.Frames.Verified != null &&
          _replayRunner.Game.Frames.Verified.Number >= EndFrame) {
        if (_loop) {
          SeekFrame(StartFrame);
        } else {
          return false;
        }
      }

      return true;
    }

    private void FastForward(int frameNumber) {
      if (frameNumber < CurrentFrame) {
        throw new ArgumentException($"Can't seek backwards to {frameNumber} from {CurrentFrame}", nameof(frameNumber));
      } else if (frameNumber == CurrentFrame) {
        // nothing to do here
        return;
      }

      const int MaxAttempts = 3;
      for (int attemptsLeft = MaxAttempts; attemptsLeft > 0; --attemptsLeft) {
        int beforeUpdate = CurrentFrame;

        double deltaTime = GetDeltaTime(frameNumber - beforeUpdate, _replayRunner.Session.SessionConfig.UpdateFPS);
        _replayRunner.Session.Update(deltaTime);

        int afterUpdate = CurrentFrame;

        if (afterUpdate >= frameNumber) {
          if (afterUpdate > frameNumber) {
            Debug.LogWarning($"Seek after the target frame {frameNumber} (from {beforeUpdate}), got to {afterUpdate}.");
          }

          return;
        } else {
          Debug.LogWarning($"Failed to seek to frame {frameNumber} (from {beforeUpdate}), got to {afterUpdate}. {attemptsLeft} attempts left.");
        }
      }

      throw new InvalidOperationException($"Unable to seek to frame {frameNumber}, ended up on {CurrentFrame}");
    }

    private static double GetDeltaTime(int frames, int simulationRate) {
      // need repeated sum here, since internally Quantum performs repeated subtraction
      double delta = 1.0 / simulationRate;
      double result = 0;
      for (int i = 0; i < frames; ++i) {
        result += delta;
      }

      return result;
    }
  }
}

#endregion


#region Assets/Photon/Quantum/Runtime/QuantumLoadBalancingClient.cs

namespace Quantum {
  using System;
  using Photon.Client;
  using Photon.Realtime;

  /// <summary>
  /// Obsolete: Not used anymore. Replace by using RealtimeClient directly.
  /// </summary>
  [Obsolete("Not used anymore. Replace by using RealtimeClient directly.")]
  public class QuantumLoadBalancingClient : RealtimeClient {
    /// <summary>
    /// Constructor.
    /// </summary>
    public QuantumLoadBalancingClient(ConnectionProtocol protocol = ConnectionProtocol.Udp) : base(protocol) {
    }

    /// <summary>
    /// Overridden connect method.
    /// </summary>
    public virtual bool ConnectUsingSettings(AppSettings appSettings, string nickname) {
      return ConnectUsingSettings(appSettings);
    }
  }
}

#endregion


#region Assets/Photon/Quantum/Runtime/QuantumLogConstants.cs



namespace Quantum {
  static partial class QuantumLogConstants {

    /// <summary>
    /// The log level based on current defines. 
    /// </summary>
    public const LogLevel DefinedLogLevel =
#if QUANTUM_LOGLEVEL_DEBUG || QUANTUM_LOGLEVEL_TRACE
      LogLevel.Debug;
#elif QUANTUM_LOGLEVEL_INFO
      LogLevel.Info;
#elif QUANTUM_LOGLEVEL_WARN
      LogLevel.Warn;
#elif QUANTUM_LOGLEVEL_ERROR
      LogLevel.Error;
#elif QUANTUM_LOGLEVEL_NONE
      LogLevel.None;
#elif UNITY_EDITOR
      LogLevel.Error;
#elif DEBUG
      LogLevel.Debug;
#else
      LogLevel.Error;
#endif

    public const TraceChannels DefinedTraceChannels = 0
#if QUANTUM_TRACE_GLOBAL
      | TraceChannels.Global
#endif
#if QUANTUM_TRACE_PHYSICS2D
      | TraceChannels.Physics2D
#endif
#if QUANTUM_TRACE_PHYSICS3D
      | TraceChannels.Physics3D
#endif
#if QUANTUM_TRACE_ASSETS
      | TraceChannels.Assets
#endif
#if QUANTUM_TRACE_MEMORY
      | TraceChannels.Memory
#endif
#if QUANTUM_TRACE_INPUT
      | TraceChannels.Input
#endif
      ;
  }
}


#endregion


#region Assets/Photon/Quantum/Runtime/QuantumLogInitializer.Partial.cs

namespace Quantum {
  using System.Threading;

  partial class QuantumLogInitializer {
    static QuantumUnityLogger CreateLogger(bool isDarkMode) {
      return new QuantumUnityLogger(Thread.CurrentThread, isDarkMode);
    }

    static partial void InitializeUnityLoggerUser(ref QuantumUnityLogger logger);
  }

  /// <inheritdoc/>
  public class QuantumUnityLogger : QuantumUnityLoggerBase {
    /// <inheritdoc/>
    public QuantumUnityLogger(Thread mainThread, bool isDarkMode) : base(mainThread, isDarkMode) {
    }
  }
}

#endregion


#region Assets/Photon/Quantum/Runtime/QuantumMapDataBaker.cs

namespace Quantum {
  using System;
  using System.Collections.Generic;
  using System.IO;
  using System.Linq;
  using System.Reflection;
  using Photon.Analyzer;
  using Photon.Deterministic;
  using UnityEditor;
  using UnityEngine;
  using UnityEngine.SceneManagement;
  using Debug = UnityEngine.Debug;

  public class QuantumMapDataBaker {
    [StaticField(StaticFieldResetMode.None)]
    public static int NavMeshSerializationBufferSize = 1024 * 1024 * 60;

    public enum BuildTrigger {
      SceneSave,
      PlaymodeChange,
      Build,
      Manual
    }

    public static void BakeMapData(QuantumMapData data, Boolean inEditor, Boolean bakeColliders = true, Boolean bakePrototypes = true, QuantumMapDataBakeFlags bakeFlags = QuantumMapDataBakeFlags.None, BuildTrigger buildTrigger = BuildTrigger.Manual) {
      using var _ = TraceScope("BakeMapData");
      
      using (TraceScope("LoadLookupTables")) {
        FPMathUtils.LoadLookupTables();
      }

      if (inEditor == false && !data.Asset) {
        data.Asset = AssetObject.Create<Map>();
      }

#if UNITY_EDITOR
      if (inEditor) {
        // set scene name
        data.Asset.Scene = data.gameObject.scene.name;

        var path = data.gameObject.scene.path;
        data.Asset.ScenePath = path;
        if (string.IsNullOrEmpty(path)) {
          data.Asset.SceneGuid = string.Empty;
        } else {
          data.Asset.SceneGuid = AssetDatabase.AssetPathToGUID(path);
        }
        
        // map needs to be unloaded before it is modified; otherwise,
        // memory leaks might occur
        QuantumUnityDB.DisposeGlobalAsset(data.Asset.Guid, immediate: true);
      }
#endif

      using (TraceScope("OnBeforeBake")) {
        InvokeCallbacks("OnBeforeBake", data, buildTrigger, bakeFlags);
      }

      using (TraceScope("OnBeforeBake (legacy)")) {
        InvokeCallbacks("OnBeforeBake", data);
      }

      if (bakeColliders) {
        using (TraceScope("BakeColliders")) {
          BakeColliders(data, inEditor);
        }
      }

      if (bakePrototypes) {
        using (TraceScope("BakingPrototypes")) {
          BakePrototypes(data);
        }
      }

      using (TraceScope("OnBake")) {
        // invoke callbacks
        InvokeCallbacks("OnBake", data);
      }
    }

    public static void BakeMeshes(QuantumMapData data, Boolean inEditor) {
      if (inEditor) {
#if UNITY_EDITOR
        var dirPath   = Path.GetDirectoryName(AssetDatabase.GetAssetPath(data.Asset));
        var assetPath = Path.Combine(dirPath, data.Asset.name + "_mesh.asset");

        var binaryDataAsset = AssetDatabase.LoadAssetAtPath<Quantum.BinaryData>(assetPath);
        if (binaryDataAsset == null) {
          binaryDataAsset = ScriptableObject.CreateInstance<Quantum.BinaryData>();
          AssetDatabase.CreateAsset(binaryDataAsset, assetPath);
        }

        // Serialize to binary some of the data (max 20 megabytes for now)
        var bytestream = new ByteStream(new Byte[data.Asset.GetStaticColliderTrianglesSerializedSize(isWriting: true)]);
        data.Asset.SerializeStaticColliderTriangles(bytestream, allocator: null, true);

        binaryDataAsset.SetData(bytestream.ToArray(), binaryDataAsset.IsCompressed);
        EditorUtility.SetDirty(binaryDataAsset);

        data.Asset.StaticColliders3DTrianglesData = binaryDataAsset;
#endif
      }
    }

#if QUANTUM_ENABLE_AI && !QUANTUM_DISABLE_AI

    public static IEnumerable<Quantum.NavMesh> BakeNavMeshes(QuantumMapData data, Boolean inEditor) {
      FPMathUtils.LoadLookupTables();

      data.Asset.NavMeshLinks = new AssetRef<NavMesh>[0];
      data.Asset.Regions      = new string[0];

      InvokeCallbacks("OnBeforeBakeNavMesh", data);

      var navmeshes = BakeNavMeshesLoop(data).ToList();

      InvokeCallbacks("OnCollectNavMeshes", data, navmeshes);

      if (inEditor) {
#if UNITY_EDITOR
        var        dirPath    = Path.GetDirectoryName(AssetDatabase.GetAssetPath(data.Asset));
        ByteStream bytestream = null;
        foreach (var navmesh in navmeshes) {

          // create and write navmesh (binary) _data asset
          {
            var navmeshBinaryFilename = Path.Combine(dirPath, $"{data.Asset.name}_{navmesh.Name}_data.asset");
            var binaryDataAsset = AssetDatabase.LoadAssetAtPath<Quantum.BinaryData>(navmeshBinaryFilename);
            if (binaryDataAsset == null) {
              binaryDataAsset = ScriptableObject.CreateInstance<Quantum.BinaryData>();
              AssetDatabase.CreateAsset(binaryDataAsset, navmeshBinaryFilename);
            }

            // Serialize to binary some of the data (max 60 megabytes for now)
            if (bytestream == null) {
              bytestream = new ByteStream(new Byte[NavMeshSerializationBufferSize]);
            } else {
              bytestream.Reset();
            }

            navmesh.Serialize(bytestream, true);

            binaryDataAsset.SetData(bytestream.ToArray(), binaryDataAsset.IsCompressed);
            EditorUtility.SetDirty(binaryDataAsset);

            navmesh.DataAsset = binaryDataAsset;
          }

          // create and write navmesh Quantum asset
          {
            var navmeshAssetPath = Path.Combine(dirPath, $"{data.Asset.name}_{navmesh.Name}.asset");
            var navMeshAsset = AssetDatabase.LoadAssetAtPath<Quantum.NavMesh>(navmeshAssetPath);
            if (navMeshAsset == null) {
              navMeshAsset = ScriptableObject.CreateInstance<Quantum.NavMesh>();
              AssetDatabase.CreateAsset(navMeshAsset, navmeshAssetPath);
            }
            else {
              QuantumUnityDB.DisposeGlobalAsset(navMeshAsset.Guid, immediate: true);
              navmesh.Guid = navMeshAsset.Guid;
              navmesh.Path = QuantumUnityDB.CreateAssetPathFromUnityPath(navmeshAssetPath);
            }

            // Preprocessing CopySerialized
            navmesh.name = navMeshAsset.name;

            EditorUtility.CopySerialized(navmesh, navMeshAsset);
            EditorUtility.SetDirty(navMeshAsset);

            ArrayUtils.Add(ref data.Asset.NavMeshLinks, (Quantum.AssetRef<Quantum.NavMesh>)navMeshAsset);
            EditorUtility.SetDirty(data.Asset);
          }
        }
#endif
      } else {
        // When executing this during runtime the guids of the created navmesh are added to the map.
        // Binary navmesh files are not created because the fresh navmesh object has everything it needs.
        // Caveat: the returned navmeshes need to be added to the DB by either...
        // A) overwriting the navmesh inside an already existing QAssetNavMesh ScriptableObject or
        // B) Creating new QAssetNavMesh ScriptableObjects (see above) and inject them into the DB (use UnityDB.OnAssetLoad callback).
        foreach (var navmesh in navmeshes) {
          navmesh.Path = data.Asset.name + "_" + navmesh.Name;
          ArrayUtils.Add(ref data.Asset.NavMeshLinks, (Quantum.AssetRef<Quantum.NavMesh>)navmesh);
        }
      }

      InvokeCallbacks("OnBakeNavMesh", data);

      return navmeshes;
    }

#else 
    public static IEnumerable<Quantum.NavMesh> BakeNavMeshes(QuantumMapData data, Boolean inEditor) {
      return null;
    }
#endif

      static StaticColliderData GetStaticData(GameObject gameObject, QuantumStaticColliderSettings settings, int colliderId) {
      return new StaticColliderData {
        Asset         = settings.Asset,
        Name          = gameObject.name,
        Tag           = gameObject.tag,
        Layer         = gameObject.layer,
        IsTrigger     = settings.Trigger,
        ColliderIndex = colliderId,
        MutableMode   = settings.MutableMode,
      };
    }

    public static void BakeColliders(QuantumMapData data, Boolean inEditor) {
      var scene = data.gameObject.scene;
      Assert.Check(scene.IsValid(), "Scene is invalid");

      // clear existing colliders
      data.StaticCollider2DReferences = new List<MonoBehaviour>();
      data.StaticCollider3DReferences = new List<MonoBehaviour>();

      // 2D
      data.Asset.StaticColliders2D = new MapStaticCollider2D[0];
      var staticCollider2DList = new List<MapStaticCollider2D>();

#if QUANTUM_ENABLE_PHYSICS2D && !QUANTUM_DISABLE_PHYSICS2D
      // circle colliders
      foreach (var collider in FindLocalObjects<QuantumStaticCircleCollider2D>(scene)) {
        collider.BeforeBake();

        var scale = collider.transform.lossyScale;
        var scale2D = scale.ToFPVector2();

        staticCollider2DList.Add(new MapStaticCollider2D {
          Position = collider.transform.TransformPoint(collider.PositionOffset.ToUnityVector3()).ToFPVector2(),
          Rotation = collider.transform.rotation.ToFPRotation2D(),
#if QUANTUM_XY
          VerticalOffset = -collider.transform.position.z.ToFP(),
          Height         = collider.Height * scale.z.ToFP(),
#else
          VerticalOffset = collider.transform.position.y.ToFP(),
          Height         = collider.Height * scale.y.ToFP(),
#endif
          PhysicsMaterial = collider.Settings.PhysicsMaterial,
          StaticData      = GetStaticData(collider.gameObject, collider.Settings, staticCollider2DList.Count),
          Layer           = collider.gameObject.layer,

          // circle
          ShapeType    = Shape2DType.Circle,
          CircleRadius = collider.Radius * FPMath.Max(scale2D.X, scale2D.Y),
        });

        data.StaticCollider2DReferences.Add(collider);
      }

      // capsule colliders
      foreach (var collider in FindLocalObjects<QuantumStaticCapsuleCollider2D>(scene)) {
        collider.BeforeBake();

        var scale = collider.transform.lossyScale;
        

        staticCollider2DList.Add(new MapStaticCollider2D {
          Position        = collider.transform.TransformPoint(collider.PositionOffset.ToUnityVector2()).ToFPVector2(),
          Rotation        = collider.RotationOffset,

          PhysicsMaterial = collider.Settings.PhysicsMaterial,
          StaticData      = GetStaticData(collider.gameObject, collider.Settings, staticCollider2DList.Count),

          // capsule
          ShapeType    = Shape2DType.Capsule,
          CapsuleSize  = new FPVector2(FP.FromFloat_UNSAFE(collider.Size.X.AsFloat * scale.x),FP.FromFloat_UNSAFE(collider.Size.Y.AsFloat * scale.y))

        });

        data.StaticCollider3DReferences.Add(collider);
      }

      // polygon colliders
      foreach (var c in FindLocalObjects<QuantumStaticPolygonCollider2D>(scene)) {
        c.BeforeBake();

        if (c.BakeAsStaticEdges2D) {
          for (var i = 0; i < c.Vertices.Length; i++) {
            var staticEdge = BakeStaticEdge2D(c.transform, c.PositionOffset, c.RotationOffset, c.Vertices[i], c.Vertices[(i + 1) % c.Vertices.Length], c.Height, c.Settings, staticCollider2DList.Count);
            staticCollider2DList.Add(staticEdge);
            data.StaticCollider2DReferences.Add(c);
          }

          continue;
        }

        var s = c.transform.localScale;
        var vertices = c.Vertices.Select(x => {
          var v = x.ToUnityVector3();
          return new Vector3(v.x * s.x, v.y * s.y, v.z * s.z);
        }).Select(x => x.ToFPVector2()).ToArray();
        if (FPVector2.IsClockWise(vertices)) {
          FPVector2.MakeCounterClockWise(vertices);
        }


        var normals        = FPVector2.CalculatePolygonNormals(vertices);
        var rotation       = c.transform.rotation.ToFPRotation2D() + c.RotationOffset.FlipRotation() * FP.Deg2Rad;
        var positionOffset = FPVector2.Rotate(FPVector2.CalculatePolygonCentroid(vertices), rotation);

        staticCollider2DList.Add(new MapStaticCollider2D {
          Position = c.transform.TransformPoint(c.PositionOffset.ToUnityVector3()).ToFPVector2() + positionOffset,
          Rotation = rotation,
#if QUANTUM_XY
          VerticalOffset = -c.transform.position.z.ToFP(),
          Height         = c.Height * s.z.ToFP(),
#else
          VerticalOffset = c.transform.position.y.ToFP(),
          Height         = c.Height * s.y.ToFP(),
#endif
          PhysicsMaterial = c.Settings.PhysicsMaterial,
          StaticData      = GetStaticData(c.gameObject, c.Settings, staticCollider2DList.Count),
          Layer           = c.gameObject.layer,

          // polygon
          ShapeType = Shape2DType.Polygon,
          PolygonCollider = new MapStaticCollider2DPolygonData() {
            Vertices = FPVector2.RecenterPolygon(vertices),
            Normals = normals,
          },
        });

        data.StaticCollider2DReferences.Add(c);
      }

      // edge colliders
      foreach (var c in FindLocalObjects<QuantumStaticEdgeCollider2D>(scene)) {
        c.BeforeBake();

        staticCollider2DList.Add(BakeStaticEdge2D(c.transform, c.PositionOffset, c.RotationOffset, c.VertexA, c.VertexB, c.Height, c.Settings, staticCollider2DList.Count));
        data.StaticCollider2DReferences.Add(c);
      }

      // box colliders
      foreach (var collider in FindLocalObjects<QuantumStaticBoxCollider2D>(scene)) {
        collider.BeforeBake();

        var e = collider.Size.ToUnityVector3();
        var s = collider.transform.lossyScale;

        e.x *= s.x;
        e.y *= s.y;
        e.z *= s.z;

        staticCollider2DList.Add(new MapStaticCollider2D {
          Position = collider.transform.TransformPoint(collider.PositionOffset.ToUnityVector3()).ToFPVector2(),
          Rotation = collider.transform.rotation.ToFPRotation2D() + collider.RotationOffset.FlipRotation() * FP.Deg2Rad,
#if QUANTUM_XY
          VerticalOffset = -collider.transform.position.z.ToFP(),
          Height         = collider.Height * s.z.ToFP(),
#else
          VerticalOffset = collider.transform.position.y.ToFP(),
          Height         = collider.Height * s.y.ToFP(),
#endif
          PhysicsMaterial = collider.Settings.PhysicsMaterial,
          StaticData      = GetStaticData(collider.gameObject, collider.Settings, staticCollider2DList.Count),
          Layer           = collider.gameObject.layer,

          // polygon
          ShapeType  = Shape2DType.Box,
          BoxExtents = e.ToFPVector2() * FP._0_50
        });

        data.StaticCollider2DReferences.Add(collider);
      }

      data.Asset.StaticColliders2D = staticCollider2DList.ToArray();
#endif

      // 3D statics

      // clear existing colliders
      var staticCollider3DList = new List<MapStaticCollider3D>();

      // clear on mono behaviour and assets
      data.Asset.CollidersManagedTriangles = new SortedDictionary<int, MeshTriangleVerticesCcw>();
      data.Asset.StaticColliders3D = Array.Empty<MapStaticCollider3D>();

      // initialize collider references, add default null on offset 0
      data.StaticCollider3DReferences = new List<MonoBehaviour>();

#if QUANTUM_ENABLE_PHYSICS3D && !QUANTUM_DISABLE_PHYSICS3D

      // sphere colliders
      foreach (var collider in FindLocalObjects<QuantumStaticSphereCollider3D>(scene)) {
        collider.BeforeBake();

        var scale = collider.transform.lossyScale;
        var radiusScale = Mathf.Max(Mathf.Max(scale.x, scale.y), scale.z);

        var rot = collider.transform.rotation.ToFPQuaternion();
        staticCollider3DList.Add(new MapStaticCollider3D {
          Position        = collider.transform.TransformPoint(collider.PositionOffset.ToUnityVector3()).ToFPVector3(),
          Rotation        = rot,
          PhysicsMaterial = collider.Settings.PhysicsMaterial,
          StaticData      = GetStaticData(collider.gameObject, collider.Settings, staticCollider3DList.Count),

          // circle
          ShapeType    = Shape3DType.Sphere,
          SphereRadius = FP.FromFloat_UNSAFE(collider.Radius.AsFloat * radiusScale)
        });

        data.StaticCollider3DReferences.Add(collider);
      }

      // capsule colliders
      foreach (var collider in FindLocalObjects<QuantumStaticCapsuleCollider3D>(scene)) {
        collider.BeforeBake();

        var scale = collider.transform.lossyScale;
        float radiusScale = Mathf.Max(scale.x, scale.z);
        float heightScale = scale.y;
        

        staticCollider3DList.Add(new MapStaticCollider3D {
          Position        = collider.transform.TransformPoint(collider.PositionOffset.ToUnityVector3()).ToFPVector3(),
          Rotation        = FPQuaternion.Euler(collider.transform.rotation.eulerAngles.ToFPVector3() + collider.RotationOffset),

          PhysicsMaterial = collider.Settings.PhysicsMaterial,
          StaticData      = GetStaticData(collider.gameObject, collider.Settings, staticCollider3DList.Count),

          // capsule
          ShapeType    = Shape3DType.Capsule,
          CapsuleRadius = FP.FromFloat_UNSAFE(collider.Radius.AsFloat * radiusScale),
          CapsuleHeight = FP.FromFloat_UNSAFE(collider.Height.AsFloat * heightScale)
        });

        data.StaticCollider3DReferences.Add(collider);
      }

      // box colliders
      foreach (var collider in FindLocalObjects<QuantumStaticBoxCollider3D>(scene)) {
        collider.BeforeBake();

        var e = collider.Size.ToUnityVector3();
        var s = collider.transform.lossyScale;

        e.x *= s.x;
        e.y *= s.y;
        e.z *= s.z;

        staticCollider3DList.Add(new MapStaticCollider3D {
          Position        = collider.transform.TransformPoint(collider.PositionOffset.ToUnityVector3()).ToFPVector3(),
          Rotation        = (collider.transform.rotation * Quaternion.Euler(collider.RotationOffset.ToUnityVector3())).ToFPQuaternion(),
          PhysicsMaterial = collider.Settings.PhysicsMaterial,
          StaticData      = GetStaticData(collider.gameObject, collider.Settings, staticCollider3DList.Count),

          // box
          ShapeType  = Shape3DType.Box,
          BoxExtents = e.ToFPVector3() * FP._0_50
        });

        data.StaticCollider3DReferences.Add(collider);
      }

      var meshes = FindLocalObjects<QuantumStaticMeshCollider3D>(scene);

      // static 3D mesh colliders
      foreach (var collider in meshes) {
        // our assumed static collider index
        var staticColliderIndex = staticCollider3DList.Count;

        // bake mesh
        if (collider.Bake(staticColliderIndex)) {
          Assert.Check(staticColliderIndex == staticCollider3DList.Count);

          // add on list
          staticCollider3DList.Add(new MapStaticCollider3D {
            Position                   = collider.transform.position.ToFPVector3(),
            Rotation                   = collider.transform.rotation.ToFPQuaternion(),
            PhysicsMaterial            = collider.Settings.PhysicsMaterial,
            SmoothSphereMeshCollisions = collider.SmoothSphereMeshCollisions,

            // mesh
            ShapeType  = Shape3DType.Mesh,
            StaticData = GetStaticData(collider.gameObject, collider.Settings, staticColliderIndex),
          });

          // add to static collider lookup
          data.StaticCollider3DReferences.Add(collider);

          // add to static collider data
          data.Asset.CollidersManagedTriangles.Add(staticColliderIndex, collider.MeshTriangles);
        }
      }

#endif

      var terrains = FindLocalObjects<QuantumStaticTerrainCollider3D>(scene);

      // terrain colliders
      foreach (var terrain in terrains) {
        // our assumed static collider index
        var staticColliderIndex = staticCollider3DList.Count;

        // bake terrain
        terrain.Bake();

        // add to 3d collider list
        staticCollider3DList.Add(new MapStaticCollider3D {
          Position                   = default(FPVector3),
          Rotation                   = FPQuaternion.Identity,
          PhysicsMaterial            = terrain.Asset.PhysicsMaterial,
          SmoothSphereMeshCollisions = terrain.SmoothSphereMeshCollisions,

          // terrains are meshes
          ShapeType = Shape3DType.Mesh,

          // static data for terrain
          StaticData = GetStaticData(terrain.gameObject, terrain.Settings, staticColliderIndex),
        });

        // add to 
        data.StaticCollider3DReferences.Add(terrain);

        // load all triangles
        terrain.Asset.Bake(staticColliderIndex);

        // add to static collider data
        data.Asset.CollidersManagedTriangles.Add(staticColliderIndex, terrain.Asset.MeshTriangles);
      }

      // this has to hold
      Assert.Check(staticCollider3DList.Count == data.StaticCollider3DReferences.Count);

      // assign collider 3d array
      data.Asset.StaticColliders3D = staticCollider3DList.ToArray();

      // clear this so it's not re-used by accident
      staticCollider3DList = null;

      BakeMeshes(data, inEditor);

      if (inEditor) {
        QuantumEditorLog.LogImport($"Baked {data.Asset.StaticColliders2D.Length} 2D static colliders");
        QuantumEditorLog.LogImport($"Baked {data.Asset.StaticColliders3D.Length} 3D static primitive colliders");
        QuantumEditorLog.LogImport($"Baked {data.Asset.CollidersManagedTriangles.Select(x => x.Value.Triangles.Length).Sum()} 3D static triangles");
      }
    }

    public static void BakePrototypes(QuantumMapData data) {
      var scene = data.gameObject.scene;
      Assert.Check(scene.IsValid(), "Scene is invalid");

      data.MapEntityReferences.Clear();

      var components = new List<QuantumUnityComponentPrototype>();
      var prototypes = FindLocalObjects<QuantumEntityPrototype>(scene).ToArray();
      SortBySiblingIndex(prototypes);

      var converter = new QuantumEntityPrototypeConverter(data, prototypes);
      var buffer    = new List<ComponentPrototype>();
      
      ref var mapEntities = ref data.Asset.MapEntities;
      Array.Resize(ref mapEntities, prototypes.Length);
      Array.Clear(mapEntities, 0, mapEntities.Length);
      
#if UNITY_EDITOR
      // this is needed to clear up managed references
      using var so = new SerializedObject(data.Asset);
      so.Update();
#endif

      for (int i = 0; i < prototypes.Length; ++i) {
        var prototype = prototypes[i];

        prototype.GetComponents(components);
        
        prototype.PreSerialize();
        prototype.SerializeImplicitComponents(buffer, out var selfView);

        foreach (var component in components) {
          component.Refresh();
          var proto = component.CreatePrototype(converter);
          buffer.Add(proto);
        }

        mapEntities[i] = ComponentPrototypeSet.FromArray(buffer.ToArray());
        data.MapEntityReferences.Add(selfView);
        buffer.Clear();
        
#if UNITY_EDITOR
        UpdateManagedReferenceIds(data.Asset, prototype, mapEntities[i].Components);
#endif
      }
      
#if UNITY_EDITOR
      so.Update();
      so.ApplyModifiedProperties();
#endif
    }
    
    private static Lazy<Type[]> CallbackTypes = new Lazy<Type[]>(() => {
      List<Type> callbackTypes = new List<Type>();

      if (Application.isEditor) {
#if UNITY_EDITOR
        foreach (var t in TypeCache.GetTypesDerivedFrom(typeof(MapDataBakerCallback))) {
          var assemblyAttribute = t.Assembly.GetCustomAttribute<QuantumMapBakeAssemblyAttribute>();
          if (assemblyAttribute == null) {
            Log.Warn($"{nameof(MapDataBakerCallback)} found ({t.FullName}) in assembly {t.Assembly.FullName} which is not marked with {nameof(QuantumMapBakeAssemblyAttribute)}. " +
                     $"It will be ignored and not used for baking. Please mark the assembly with {nameof(QuantumMapBakeAssemblyAttribute)} if you want to use this callback or " +
                     $"if you want to get rid of this warning.");
            continue;
          }

          if (assemblyAttribute.Ignore) {
            continue;
          }
          callbackTypes.Add(t);
        }
#endif
      } else {
        var markedAssemblies = AppDomain.CurrentDomain.GetAssemblies()
          .Where(x => x.GetCustomAttribute<QuantumMapBakeAssemblyAttribute>()?.Ignore == false);

        foreach (var asm in markedAssemblies) {
          foreach (var t in asm.GetLoadableTypes()) {
            if (!t.IsSubclassOf(typeof(MapDataBakerCallback))) {
              continue;
            }

            callbackTypes.Add(t);
          }
        }
      }
      
      // remove non-instantiable types
      callbackTypes.RemoveAll(t => t.IsAbstract || t.IsGenericTypeDefinition);
      
      callbackTypes.Sort((a, b) => {
        var orderA = a.GetCustomAttribute<MapDataBakerCallbackAttribute>()?.InvokeOrder ?? 0;
        var orderB = b.GetCustomAttribute<MapDataBakerCallbackAttribute>()?.InvokeOrder ?? 0;
        return orderA - orderB;
      });

      return callbackTypes.ToArray();
    });

    private static void InvokeCallbacks(string callbackName, QuantumMapData data, BuildTrigger buildTrigger, QuantumMapDataBakeFlags bakeFlags) {
      foreach (var callback in CallbackTypes.Value) {
        try {
          switch (callbackName) {
            case "OnBeforeBake":
              (Activator.CreateInstance(callback) as MapDataBakerCallback).OnBeforeBake(data, buildTrigger, bakeFlags);
              break;
            default:
              Log.Warn($"Callback `{callbackName}` not found");
              break;
          }
        } catch (Exception exn) {
          Log.Exception(exn);
        }
      }
    }

    private static void InvokeCallbacks(string callbackName, QuantumMapData data) {
      foreach (var callback in CallbackTypes.Value) {
        try {
          switch (callbackName) {
            case "OnBeforeBake":
              (Activator.CreateInstance(callback) as MapDataBakerCallback).OnBeforeBake(data);
              break;
            case "OnBake":
              (Activator.CreateInstance(callback) as MapDataBakerCallback).OnBake(data);
              break;
            case "OnBeforeBakeNavMesh":
              (Activator.CreateInstance(callback) as MapDataBakerCallback).OnBeforeBakeNavMesh(data);
              break;
            case "OnBakeNavMesh":
              (Activator.CreateInstance(callback) as MapDataBakerCallback).OnBakeNavMesh(data);
              break;
            default:
              Log.Warn($"Callback `{callbackName}` not found");
              break;
          }
        } catch (Exception exn) {
          Log.Exception(exn);
        }
      }
    }

    private static void InvokeCallbacks(string callbackName, QuantumMapData data, List<NavMeshBakeData> bakeData) {
      foreach (var callback in CallbackTypes.Value) {
        try {
          switch (callbackName) {
            case "OnCollectNavMeshBakeData":
              (Activator.CreateInstance(callback) as MapDataBakerCallback).OnCollectNavMeshBakeData(data, bakeData);
              break;
            default:
              Log.Warn($"Callback `{callbackName}` not found");
              break;
          }
        } catch (Exception exn) {
          Log.Exception(exn);
        }
      }
    }

    private static void InvokeCallbacks(string callbackName, QuantumMapData data, List<Quantum.NavMesh> navmeshes) {
      foreach (var callback in CallbackTypes.Value) {
        try {
          switch (callbackName) {
            case "OnCollectNavMeshes":
              (Activator.CreateInstance(callback) as MapDataBakerCallback).OnCollectNavMeshes(data, navmeshes);
              break;
            default:
              Log.Warn($"Callback `{callbackName}` not found");
              break;
          }
        } catch (Exception exn) {
          Log.Exception(exn);
        }
      }
    }

#if QUANTUM_ENABLE_AI && !QUANTUM_DISABLE_AI

    static IEnumerable<Quantum.NavMesh> BakeNavMeshesLoop(QuantumMapData data) {

#if UNITY_EDITOR
      QuantumGameGizmos.InvalidateNavMeshGizmos();
#endif
      
      var scene = data.gameObject.scene;
      Assert.Check(scene.IsValid(), "Scene is invalid");

      var allBakeData = new List<NavMeshBakeData>();

      // Collect unity navmeshes
      {
        var unityNavmeshes = data.GetComponentsInChildren<QuantumMapNavMeshUnity>().ToList();

        // The sorting is important to always generate the same order of regions name list.
        unityNavmeshes.Sort((a, b) => string.Compare(a.name, b.name, StringComparison.Ordinal));

        for (int i = 0; i < unityNavmeshes.Count; i++) {
          // If NavMeshSurface installed, this will deactivate non linked surfaces 
          // to make the CalculateTriangulation work only with the selected Unity navmesh.
          List<GameObject> deactivatedObjects = new List<GameObject>();

          try {
            if (unityNavmeshes[i].NavMeshSurfaces != null && unityNavmeshes[i].NavMeshSurfaces.Length > 0) {
#if QUANTUM_ENABLE_AI_NAVIGATION
                var surfaces = FindLocalObjects<Unity.AI.Navigation.NavMeshSurface>(scene);
                foreach (var surface in surfaces) {
                  if (unityNavmeshes[i].NavMeshSurfaces.Contains(surface.gameObject) == false) {
                    surface.gameObject.SetActive(false);
                    deactivatedObjects.Add(surface.gameObject);
                  }
                }
#endif
            }

            var bakeData = QuantumNavMesh.ImportFromUnity(scene, unityNavmeshes[i].Settings, unityNavmeshes[i].name);
            if (bakeData == null) {
              Log.Error($"Could not import navmesh '{unityNavmeshes[i].name}'");
            } else {
              bakeData.Name                            = unityNavmeshes[i].name;
              bakeData.AgentRadius                     = QuantumNavMesh.FindSmallestAgentRadius(unityNavmeshes[i].NavMeshSurfaces);
              bakeData.EnableQuantum_XY                = unityNavmeshes[i].Settings.EnableQuantum_XY;
              bakeData.ClosestTriangleCalculation      = unityNavmeshes[i].Settings.ClosestTriangleCalculation;
              bakeData.ClosestTriangleCalculationDepth = unityNavmeshes[i].Settings.ClosestTriangleCalculationDepth;
              allBakeData.Add(bakeData);
            }
          } catch (Exception exn) {
            Log.Exception(exn);
          }

          foreach (var go in deactivatedObjects) {
            go.SetActive(true);
          }
        }
      }

      // Collect custom bake data
      InvokeCallbacks("OnCollectNavMeshBakeData", data, allBakeData);

      // Bake all collected bake data
      for (int i = 0; i < allBakeData.Count; i++) {
        var navmesh  = default(Quantum.NavMesh);
        var bakeData = allBakeData[i];
        if (bakeData == null) {
          Log.Error($"Navmesh bake data at index {i} is null");
          continue;
        }

        try {
          var p = default(IProgressBar);

          if (Log.Settings.Level <= LogLevel.Debug) {
            p = new NavMeshBakerBenchmarkerProgressBar($"Baking {bakeData.Name}");
          }
          
          navmesh = NavMeshBaker.BakeNavMesh(data.Asset, bakeData, progressBar: p);
          navmesh.SerializeType = data.NavMeshSerializeType;
          Log.Debug($"Baking Quantum NavMesh '{bakeData.Name}' complete ({i + 1}/{allBakeData.Count})");
        } catch (Exception exn) {
          Log.Exception(exn);
        }

        if (navmesh != null) {
          yield return navmesh;
        } else {
          Log.Error($"Baking Quantum NavMesh '{bakeData.Name}' failed");
        }
      }
    }

#endif

    private static void SortBySiblingIndex<T>(T[] array) where T : Component {
      // sort by sibling indices; this should be uniform across machines
      List<int> list0 = new List<int>();
      List<int> list1 = new List<int>();
      Array.Sort(array, (a, b) => CompareLists(GetSiblingIndexPath(a.transform, list0), GetSiblingIndexPath(b.transform, list1)));
    }

    static List<int> GetSiblingIndexPath(Transform t, List<int> buffer) {
      buffer.Clear();
      while (t != null) {
        buffer.Add(t.GetSiblingIndex());
        t = t.parent;
      }

      buffer.Reverse();
      return buffer;
    }

    static int CompareLists(List<int> left, List<int> right) {
      while (left.Count > 0 && right.Count > 0) {
        if (left[0] < right[0]) {
          return -1;
        }

        if (left[0] > right[0]) {
          return 1;
        }

        left.RemoveAt(0);
        right.RemoveAt(0);
      }

      return 0;
    }

#if QUANTUM_ENABLE_PHYSICS2D && !QUANTUM_DISABLE_PHYSICS2D
    static MapStaticCollider2D BakeStaticEdge2D(Transform t, FPVector2 positionOffset, FP rotationOffset, FPVector2 vertexA, FPVector2 vertexB, FP height, QuantumStaticColliderSettings settings, int colliderId) {
      QuantumStaticEdgeCollider2D.GetEdgeGizmosSettings(t, positionOffset, rotationOffset, vertexA, vertexB, height, out var start, out var end, out var scaledHeight);

      var startToEnd = end - start;

      var pos = (start + end) / 2.0f;
      var rot = Quaternion.FromToRotation(Vector3.right, startToEnd);

      return new MapStaticCollider2D {
        Position = pos.ToFPVector2(),
        Rotation = rot.ToFPRotation2D(),
#if QUANTUM_XY
        VerticalOffset = -t.position.z.ToFP(),
        Height         = scaledHeight.ToFP(),
#else
        VerticalOffset = t.position.y.ToFP(),
        Height         = scaledHeight.ToFP(),
#endif
        PhysicsMaterial = settings.PhysicsMaterial,
        StaticData      = GetStaticData(t.gameObject, settings, colliderId),
        Layer           = t.gameObject.layer,

        // edge
        ShapeType  = Shape2DType.Edge,
        EdgeExtent = (startToEnd.magnitude / 2.0f).ToFP(),
      };
    }
#endif

    public static List<T> FindLocalObjects<T>(Scene scene) where T : Component {
      List<T> partialResult = new List<T>();
      List<T> fullResult    = new List<T>();
      foreach (var gameObject in scene.GetRootGameObjects()) {
        // GetComponentsInChildren seems to clear the list first, but we're not going to depend
        // on this implementation detail
        if (!gameObject.activeInHierarchy)
          continue;
        partialResult.Clear();
        gameObject.GetComponentsInChildren(partialResult);
        fullResult.AddRange(partialResult);
      }

      return fullResult;
    }

    public static List<Component> FindLocalObjects(Scene scene, Type type) {
      List<Component> result = new List<Component>();
      foreach (var gameObject in scene.GetRootGameObjects()) {
        if (!gameObject.activeInHierarchy)
          continue;
        foreach (var component in gameObject.GetComponentsInChildren(type)) {
          result.Add(component);
        }
      }

      return result;
    }
    
#if UNITY_EDITOR
    public static void UpdateManagedReferenceIds(Quantum.Map context, QuantumEntityPrototype prototype, ComponentPrototype[] componentPrototypes) {
      
      var  id = GlobalObjectId.GetGlobalObjectIdSlow(prototype);

      uint hash = 0;
      
      hash = GetHashCodeDeterministic(id.identifierType, hash);
      hash = GetHashCodeDeterministic(id.assetGUID, hash);
      hash = GetHashCodeDeterministic(id.targetObjectId, hash);
      hash = GetHashCodeDeterministic(id.targetPrefabId, hash);
      
      // leave the highest bit intact, some negative refIds are used for special cases by Unity
      long refIdBase = (long)hash << 31; 
      for (int i = 0; i < componentPrototypes.Length; ++i) {
#if UNITY_2022_2_OR_NEWER
        UnityEngine.Serialization.ManagedReferenceUtility.SetManagedReferenceIdForObject(context, componentPrototypes[i], refIdBase + i);
#else
        SerializationUtility.SetManagedReferenceIdForObject(context, componentPrototypes[i], refIdBase + i);
#endif
      }
    }
    
    private static unsafe uint GetHashCodeDeterministic<T>(T data, uint initialHash = 0) where T : unmanaged {
      var hash = initialHash;
      
      var ptr  = (byte*)&data;
      for (var i = 0; i < sizeof(T); ++i) {
        hash = hash * 31 + ptr[i];
      }
      return hash;
    }
#endif
    
#if QUANTUM_MAP_BAKER_TRACE_ENABLED
    public readonly struct _LogScope : IDisposable {
      private readonly Stopwatch _stopwatch;
      private readonly string    _msg;
      private readonly bool      _trace;
      
      public _LogScope(string msg, bool trace) {
        _msg       = msg;
        _stopwatch = Stopwatch.StartNew();
        _trace     = trace;
      }
      
      public void Dispose() {
        if (_trace) {
          UnityEngine.Debug.Log($"{_msg} ({_stopwatch.Elapsed.TotalMilliseconds:0.00}ms)");
        } else {
          UnityEngine.Debug.Log($"{_msg} ({_stopwatch.Elapsed.TotalMilliseconds:0.00}ms)");  
        }
      }
    }
    
    public static _LogScope TraceScope(string msg) => new _LogScope(msg, true);
#else
    public static IDisposable TraceScope(string msg) => null;
#endif
  }
}


#endregion


#region Assets/Photon/Quantum/Runtime/QuantumMenuSceneInfo.Partial.cs

namespace Quantum {

  public partial class QuantumMenuSceneInfo {
    /// <summary>
    /// When using a menu config the runtime config from the QuantumMenuConnectArgs.RuntimeConfig is always overwritten.
    /// </summary>
    public RuntimeConfig RuntimeConfig;
    /// <summary>
    /// Quantum map that is loaded. Must be set.
    /// </summary>
    public AssetRef<Map> Map {
      get => RuntimeConfig.Map;
      set => RuntimeConfig.Map = value;
    }
    /// <summary>
    /// Override Quantum systems configuration for this scene. Can be null.
    /// If this is set it will overwrite the <see cref="RuntimeConfig.SystemsConfig"/> settings during the connection sequence.
    /// </summary>
    public AssetRef<SystemsConfig> SystemsConfig {
      get => RuntimeConfig.SystemsConfig;
      set => RuntimeConfig.SystemsConfig = value;
    }
  }
}


#endregion


#region Assets/Photon/Quantum/Runtime/QuantumMonoBehaviour.Partial.cs

namespace Quantum {
  partial class QuantumMonoBehaviour {
#if UNITY_EDITOR
    // TODO: this should be moved somewhere or renamed; the whole idea is that this stuff
    // is only used by behaviours, whereas for simulation gizmos runner can provide its own
    //  protected QuantumGameGizmosSettings GlobalGizmosSettings => QuantumGameGizmosSettingsScriptableObject.Global.Settings;
#endif
  }
}

#endregion


#region Assets/Photon/Quantum/Runtime/QuantumNavMesh.cs

namespace Quantum {
  using System;
  using System.Collections.Generic;
  using System.Linq;
  using Photon.Analyzer;
  using Photon.Deterministic;
  using UnityEngine;
  using UnityEngine.SceneManagement;
  using UnityEngine.Serialization;
  using Object = System.Object;
  using Plane = UnityEngine.Plane;
#if QUANTUM_ENABLE_AI && !QUANTUM_DISABLE_AI
  using UnityEngine.AI;
#endif

  /// <summary>
  /// How Quantum navmesh regions are imported from the Unity navmesh.
  /// </summary>
  public enum NavmeshRegionImportMode {
    /// <summary>
    /// Regions are not imported.
    /// </summary>
    Disabled = 0,
    /// <summary>
    /// Requires <see cref="QuantumNavMeshRegion"/> scripts to setup region data to increase the max region count to 64.
    /// Supports using different region names for different maps.
    /// </summary>
    Advanced = 1,
    /// <summary>
    /// Uses the Unity NavMesh areas to directly generate regions and use according area names. Limited to 30 different global regions.
    /// </summary>
    Simple = 2
  }

  /// <summary>
  /// This class is a collection of utility methods to import Unity NavMesh data into Quantum NavMesh data.
  /// </summary>
  public partial class QuantumNavMesh {

    #region Importing From Unity

#if QUANTUM_ENABLE_AI && !QUANTUM_DISABLE_AI
    /// <summary>
    /// Intermediate mesh vertex data structure.
    /// </summary>
    public struct Vertex {
      /// <summary>
      /// The vertex id.
      /// </summary>
      public String Id;
      /// <summary>
      /// The world position.
      /// </summary>
      public Vector3Double Position;

      /// <summary>
      /// Convert the vertex data to a Quantum NavMesh vertex.
      /// </summary>
      /// <returns></returns>
      public NavMeshBakeDataVertex Convert() {
        return new NavMeshBakeDataVertex {
          //Id       = this.Id,
          Position = this.Position.AsFPVector()
        };
      }
    }
#endif

    /// <summary>
    /// The default minimum agent radius. Will be updated during importing the Unity navmesh.
    /// </summary>
    [StaticField(StaticFieldResetMode.None)]
    public static float DefaultMinAgentRadius = 0.25f;

    /// <summary>
    /// The Unity navmesh import settings.
    /// </summary>
    [Serializable]
    public class ImportSettings {
      /// <summary>
      /// The Unity NavMesh is a collection of non - connected triangles, this option is very important and combines shared vertices.
      /// </summary>
      [InlineHelp]
      public bool WeldIdenticalVertices = true;
      /// <summary>
      /// Don't make the epsilon too small, vertices to fuse are missed, also don't make the value too big as it will deform your navmesh. Min = float.Epsilon.
      /// </summary>
      [InlineHelp]
      [Min(float.Epsilon)]
      [DrawIf("WeldIdenticalVertices", true)]
      public float WeldVertexEpsilon = 0.0001f;
      /// <summary>
      /// Post processes imported Unity navmesh with a Delaunay triangulation to reduce long triangles.
      /// </summary>
      [InlineHelp]
      public bool DelaunayTriangulation = false;
      /// <summary>
      /// In 3D the triangulation can deform the navmesh on slopes, check this option to restrict the triangulation to triangles that lie in the same plane.
      /// </summary>
      [InlineHelp]
      [DrawIf("DelaunayTriangulation", true)]
      public bool DelaunayTriangulationRestrictToPlanes = false;
      /// <summary>
      /// Sometimes vertices are lying on other triangle edges, this will lead to unwanted borders being detected, this option splits those vertices.
      /// </summary>
      [InlineHelp]
      public bool FixTrianglesOnEdges = true;
      /// <summary>
      /// Larger scaled navmeshes may require to increase this value (e.g. 0.001) when false-positive borders are detected. Min = float.Epsilon.
      /// </summary>
      [InlineHelp]
      [Min(float.Epsilon)]
      [DrawIf("FixTrianglesOnEdges", true)]
      public float FixTrianglesOnEdgesEpsilon = float.Epsilon;
      /// <summary>
      /// Make the height offset considerably larger than FixTrianglesOnEdgesEpsilon to better detect degenerate triangles. Is the navmesh becomes deformed chose a smaller epsilon. . Min = float.Epsilon. Default is 0.05.
      /// </summary>
      [InlineHelp]
      [Min(float.Epsilon)]
      [DrawIf("FixTrianglesOnEdges", true)]
      public float FixTrianglesOnEdgesHeightEpsilon = 0.05f;
      /// <summary>
      /// Automatically correct navmesh link position to the closest triangle by searching this distance (default is 0).
      /// </summary>
      [InlineHelp]
      public float LinkErrorCorrection = 0.0f;
      /// <summary>
      /// SpiralOut will be considerably faster but fallback triangles can be null.
      /// </summary>
      [InlineHelp]
      public NavMeshBakeDataFindClosestTriangle ClosestTriangleCalculation = NavMeshBakeDataFindClosestTriangle.None;
      /// <summary>
      /// Number of cells to search triangles in neighbors.
      /// </summary>
      [InlineHelp]
      [DrawIf("ClosestTriangleCalculation", (long)NavMeshBakeDataFindClosestTriangle.BruteForce, CompareOperator.NotEqual)]
      public int ClosestTriangleCalculationDepth = 3;
      /// <summary>
      /// Activate this and the navmesh baking will flip Y and Z to support navmeshes generated in the XY plane.
      /// </summary>
      [InlineHelp]
      public bool EnableQuantum_XY;
      /// <summary>
      /// The agent radius that the navmesh is build for. The value is retrieved from Unity settings when baking in Editor.
      /// </summary>
      [InlineHelp]
      public FP MinAgentRadius = FP._0_25;
      /// <summary>
      /// Toggle the Quantum region import.
      /// </summary>
      [Obsolete("Has been replaced by ImportRegionMode")]
      public bool ImportRegions => ImportRegionMode == NavmeshRegionImportMode.Advanced;
      /// <summary>
      /// Toggle the region import mode. Default is Simple.
      /// </summary>
      [InlineHelp]
      [FormerlySerializedAs("ImportRegions")]
      public NavmeshRegionImportMode ImportRegionMode = NavmeshRegionImportMode.Simple;
      /// <summary>
      /// The artificial margin is necessary because the Unity NavMesh does not fit the source size very well. The value is added to the navmesh area and checked against all Quantum Region scripts to select the correct region id.
      /// </summary>
      [InlineHelp]
      [DrawIf("ImportRegionMode", (int)NavmeshRegionImportMode.Advanced, mode: DrawIfMode.Hide)]
      public float RegionDetectionMargin = 0.4f;
      /// <summary>
      /// The region area ids to import.
      /// </summary>
      [HideInInspector]
      [DrawIf("ImportRegionMode", (int)NavmeshRegionImportMode.Disabled, compare: CompareOperator.Greater, mode: DrawIfMode.Hide)]
      public List<Int32> RegionAreaIds;
    }

#if QUANTUM_ENABLE_AI && !QUANTUM_DISABLE_AI

    /// <summary>
    /// The navmesh import utility methods.
    /// </summary>
    public static class ImportUtils {
      /// <summary>
      /// Tries to merge vertices that are very close to each other into a single vertex.
      /// </summary>
      /// <param name="vertices">Mesh vertices collection.</param>
      /// <param name="triangles">Mesh triangles collection.</param>
      /// <param name="cleanupEpsilon">The epsilon to detect identical vertices.</param>
      /// <param name="reporter">Progress bar.</param>
      public static void WeldIdenticalVertices(ref Vertex[] vertices, ref NavMeshBakeDataTriangle[] triangles, float cleanupEpsilon, Action<float> reporter) {
        int[] vertexRemapTable = new int[vertices.Length];
        for (int i = 0; i < vertexRemapTable.Length; ++i) {
          vertexRemapTable[i] = i;
        }

        for (int i = 0; i < vertices.Length; ++i) {
          reporter.Invoke(i / (float)vertices.Length);
          var v = vertices[i].Position;

          for (int j = i + 1; j < vertices.Length; ++j) {
            if (j != vertexRemapTable[j]) {
              continue;
            }

            var v2 = vertices[j].Position;
            if (Math.Abs(Vector3Double.SqrMagnitude(v2 - v)) <= cleanupEpsilon) {
              vertexRemapTable[j] = i;
            }
          }
        }

        for (int i = 0; i < triangles.Length; ++i) {
          for (int v = 0; v < 3; v++) {
            triangles[i].VertexIds[v] =
              vertexRemapTable[triangles[i].VertexIds[v]];
          }
        }
      }

      /// <summary>
      /// Removes unused vertices from the vertex array.
      /// </summary>
      /// <param name="vertices">Mesh vertices collection.</param>
      /// <param name="triangles">Mesh triangles collection.</param>
      /// <param name="reporter">Progress bar.</param>
      public static void RemoveUnusedVertices(ref Vertex[] vertices, ref NavMeshBakeDataTriangle[] triangles, Action<float> reporter) {
        var newVertices = new List<Vertex>();
        int[] remapArray = new int[vertices.Length];
        for (int i = 0; i < remapArray.Length; ++i) {
          remapArray[i] = -1;
        }

        for (int t = 0; t < triangles.Length; ++t) {
          reporter.Invoke(t / (float)triangles.Length);
          for (int v = 0; v < 3; v++) {
            int newIndex = remapArray[triangles[t].VertexIds[v]];
            if (newIndex < 0) {
              newIndex = newVertices.Count;
              remapArray[triangles[t].VertexIds[v]] = newIndex;
              newVertices.Add(vertices[triangles[t].VertexIds[v]]);
            }

            triangles[t].VertexIds[v] = newIndex;
          }
        }

        //Debug.Log("Removed Unused Vertices: " + (vertices.Length - newVertices.Count));

        vertices = newVertices.ToArray();
      }

      /// <summary>
      /// Use the Unity navmesh areas encoded into the Unity navmesh to generate Quantum regions.
      /// </summary>
      /// <param name="triangles">Imported Navmesh triangles</param>
      /// <param name="regionMap">The global region map, will add new regions</param>
      /// <param name="unityAreaMap">Map a Unity area id to a area name</param>
      /// <param name="regionIncludeList">Include these region ids</param>
      /// <param name="reporter">Progress bar</param>
      public static void ImportRegionsSimple(ref NavMeshBakeDataTriangle[] triangles, ref List<string> regionMap, Dictionary<int, string> unityAreaMap, List<int> regionIncludeList, Action<float> reporter) {
        for (int t = 0; t < triangles.Length; t++) {
          reporter?.Invoke(t / (float)triangles.Length);
          var areaId = triangles[t].Area;
          switch (areaId) {
            case 0:
              triangles[t].RegionId = "MainArea";
              break;
            case 1:
              // Skip pre-defined areas
              // Todo: should Jump be ignored?
              // case 2:
              break;
            default: {
                if (regionIncludeList?.Contains(areaId) == false) {
                  // Unselected areas are converted to main area, but they can keep the cost.
                  triangles[t].RegionId = "MainArea";
                  triangles[t].Cost = UnityEngine.AI.NavMesh.GetAreaCost(areaId).ToFP();
                  break;
                }
                if (unityAreaMap.TryGetValue(areaId, out var regionId) == false) {
                  Log.Error($"Failed to map Unity navmesh area {areaId}");
                  break;
                }
                if (regionMap.Contains(regionId) == false) {
                  regionMap.Add(regionId);
                }
                triangles[t].RegionId = regionId;
                triangles[t].Cost = UnityEngine.AI.NavMesh.GetAreaCost(areaId).ToFP();
                break;
              }
          }
        }
      }

      /// <summary>
      /// Tries to identify what region individual vertices belong to. Uses the original <see cref="QuantumNavMeshRegion"/> scripts to cast vertices against.
      /// </summary>
      /// <param name="scene">Unity scene.</param>
      /// <param name="vertices">Mesh vertices collection.</param>
      /// <param name="triangles">Mesh triangles collection.</param>
      /// <param name="t">The current triangle index to analyze.</param>
      /// <param name="regionMap">The list of regions already found.</param>
      /// <param name="regionDetectionMargin">The region detection margin used to enlarge the reference share from the <see cref="QuantumNavMeshRegion"/> script.</param>
      public static void ImportRegions(Scene scene, ref Vertex[] vertices, ref NavMeshBakeDataTriangle[] triangles, int t, ref List<string> regionMap, float regionDetectionMargin) {
        // Expand the triangle until we have an isolated island containing all connected triangles of the same region
        HashSet<int> island = new HashSet<int>();
        HashSet<int> vertexMap = new HashSet<int>();
        island.Add(t);
        vertexMap.Add(triangles[t].VertexIds[0]);
        vertexMap.Add(triangles[t].VertexIds[1]);
        vertexMap.Add(triangles[t].VertexIds[2]);
        bool isIslandComplete = false;
        while (!isIslandComplete) {
          isIslandComplete = true;
          for (int j = 0; j < triangles.Length; j++) {
            if (triangles[t].Area == triangles[j].Area && !island.Contains(j)) {
              for (int v = 0; v < 3; v++) {
                if (vertexMap.Contains(triangles[j].VertexIds[v])) {
                  island.Add(j);
                  vertexMap.Add(triangles[j].VertexIds[0]);
                  vertexMap.Add(triangles[j].VertexIds[1]);
                  vertexMap.Add(triangles[j].VertexIds[2]);
                  isIslandComplete = false;
                  break;
                }
              }
            }
          }
        }

        // Go through all MapNavMeshRegion scripts in the scene and check if all vertices of the islands
        // are within its bounds. Use the smallest possible bounds/region found. Use the RegionIndex from that for all triangles.
        if (island.Count > 0) {
          string regionId = string.Empty;
          FP regionCost = FP._1;
          float smallestRegionBounds = float.MaxValue;
          var regions = QuantumMapDataBaker.FindLocalObjects<QuantumNavMeshRegion>(scene);
          foreach (var region in regions) {
            if (region.CastRegion != QuantumNavMeshRegion.RegionCastType.CastRegion) {
              continue;
            }

            var meshRenderer = region.gameObject.GetComponent<MeshRenderer>();
            if (meshRenderer == null) {
              Debug.LogErrorFormat("MeshRenderer missing on MapNavMeshRegion object {0} with active RegionCasting", region.name);
            } else {
              var bounds = region.gameObject.GetComponent<MeshRenderer>().bounds;
              // Grow the bounds, because the generated map is not exact
              bounds.Expand(regionDetectionMargin);
              bool isInsideBounds = true;
              foreach (var triangleIndex in island) {
                for (int v = 0; v < 3; v++) {
                  var position = vertices[triangles[triangleIndex].VertexIds[v]].Position;
                  if (bounds.Contains(position.AsVector()) == false) {
                    isInsideBounds = false;
                    break;
                  }
                }
              }

              if (isInsideBounds) {
                float size = bounds.extents.sqrMagnitude;
                if (size < smallestRegionBounds) {
                  smallestRegionBounds = size;
                  regionId = region.Id;
                  regionCost = region.Cost;

                  if (region.OverwriteCost == false) {
                    // Grab the most recent area cost from Unity (ignore the one in the scene)
                    regionCost = UnityEngine.AI.NavMesh.GetAreaCost(triangles[t].Area).ToFP();
                  }
                }
              }
            }
          }

          // Save the toggle region index on the triangles imported from Unity
          if (string.IsNullOrEmpty(regionId) == false) {
            if (regionMap.Contains(regionId) == false) {
              if (regionMap.Count >= Navigation.Constants.MaxRegions) {
                // Still add to region map, but it won't be set on the triangles.
                Debug.LogErrorFormat("Failed to create region '{0}' because Quantum max region ({1}) reached. Reduce the number of regions.", regionId, Navigation.Constants.MaxRegions);
              }

              regionMap.Add(regionId);
            }

            foreach (var triangleIndex in island) {
              triangles[triangleIndex].RegionId = regionId;
              triangles[triangleIndex].Cost = regionCost;
            }
          } else {
            Debug.LogWarningFormat("A triangle island (count = {0}) can not be matched with any region bounds, try to increase the RegionDetectionMargin.\n Triangle Ids: {1}", island.Count, String.Join(", ", island.Select(sdfdsf => sdfdsf.ToString()).ToArray()));
          }
        }
      }

      /// <summary>
      /// Tries to detect degenerated triangles that are emit by the Unity navmesh triangulation. This detects when triangles have vertices on other triangle edges.
      /// </summary>
      /// <param name="vertices">Mesh vertices collection.</param>
      /// <param name="triangles">Mesh triangles collection.</param>
      /// <param name="t">The current triangle index to analyze.</param>
      /// <param name="v0">The first vertex.</param>
      /// <param name="epsilon">The epsilon to use when detecting vertex on edges.</param>
      /// <param name="epsilonHeight">The explicit height epsilon to use.</param>
      public static void FixTrianglesOnEdges(ref Vertex[] vertices, ref NavMeshBakeDataTriangle[] triangles, int t, int v0, float epsilon, float epsilonHeight) {
        int v1 = (v0 + 1) % 3;
        int vOther;
        int otherTriangle = FindTriangleOnEdge(ref vertices, ref triangles, t, triangles[t].VertexIds[v0], triangles[t].VertexIds[v1], epsilon, epsilonHeight, out vOther);
        if (otherTriangle >= 0) {
          SplitTriangle(ref triangles, t, v0, triangles[otherTriangle].VertexIds[vOther]);
          //Debug.LogFormat("Split triangle {0} at position {1}", t, vertices[triangles[otherTriangle].VertexIds[vOther]].Position);
        }
      }

      /// <summary>
      /// Detects if a triangle <paramref name="tri"/> has a vertex on the segment between <paramref name="v0"/> and <paramref name="v1"/>.
      /// </summary>
      /// <returns>The triangle that lies on the segment or -1.</returns>
      public static int FindTriangleOnEdge(ref Vertex[] vertices, ref NavMeshBakeDataTriangle[] triangles, int tri, int v0, int v1, float epsilon, float epsilonHeight, out int triangleVertexIndex) {
        triangleVertexIndex = -1;
        for (int t = 0; t < triangles.Length; ++t) {
          if (t == tri) {
            continue;
          }

          // Triangle shares at least one vertex?
          if (triangles[t].VertexIds[0] == v0 || triangles[t].VertexIds[1] == v0 ||
              triangles[t].VertexIds[2] == v0 || triangles[t].VertexIds[0] == v1 ||
              triangles[t].VertexIds[1] == v1 || triangles[t].VertexIds[2] == v1) {
            if (triangles[t].VertexIds[0] == v0 || triangles[t].VertexIds[1] == v0 || triangles[t].VertexIds[2] == v0) {
              if (triangles[t].VertexIds[0] == v1 || triangles[t].VertexIds[1] == v1 || triangles[t].VertexIds[2] == v1) {
                // Triangle shares two vertices, not interested in that
                return -1;
              }
            }

            if (Vector3Double.IsPointBetween(vertices[triangles[t].VertexIds[0]].Position, vertices[v0].Position, vertices[v1].Position, epsilon, epsilonHeight)) {
              // Returns the triangle that has a vertex on the provided segment and the vertex index that lies on it
              triangleVertexIndex = 0;
              return t;
            }

            if (Vector3Double.IsPointBetween(vertices[triangles[t].VertexIds[1]].Position, vertices[v0].Position, vertices[v1].Position, epsilon, epsilonHeight)) {
              triangleVertexIndex = 1;
              return t;
            }

            if (Vector3Double.IsPointBetween(vertices[triangles[t].VertexIds[2]].Position, vertices[v0].Position, vertices[v1].Position, epsilon, epsilonHeight)) {
              triangleVertexIndex = 2;
              return t;
            }
          }
        }

        return -1;
      }

      /// <summary>
      /// Splits a triangle into two triangles by inserting a new vertex at the edge between <paramref name="v0"/> and v0 + 1.
      /// </summary>
      public static void SplitTriangle(ref NavMeshBakeDataTriangle[] triangles, int t, int v0, int vNew) {
        // Split edge is between vertex index 0 and 1
        int v1 = (v0 + 1) % 3;
        // Vertex index 2 is opposite of split edge
        int v2 = (v0 + 2) % 3;

        var newTriangle = new NavMeshBakeDataTriangle {
          Area = triangles[t].Area,
          RegionId = triangles[t].RegionId,
          Cost = triangles[t].Cost,
          VertexIds = new int[3]
        };

        // Map new triangle
        newTriangle.VertexIds[0] = vNew;
        newTriangle.VertexIds[1] = triangles[t].VertexIds[v1];
        newTriangle.VertexIds[2] = triangles[t].VertexIds[v2];
        ArrayUtils.Add(ref triangles, newTriangle);

        // Remap old triangle
        triangles[t].VertexIds[v1] = vNew;
      }
    }

    /// <summary>
    /// Create a dictionary that maps Unity NavMesh area ids to area names.
    /// </summary>
    public static Dictionary<int, string> CreateUnityNavmeshAreaMap() {
      var unityNavmeshAreaMap = new Dictionary<int, string>();

#if UNITY_2023_3_OR_NEWER
      var unityAreaNames = UnityEngine.AI.NavMesh.GetAreaNames();
      for (int i = 0; i < unityAreaNames.Length; i++) {
        unityNavmeshAreaMap.Add(UnityEngine.AI.NavMesh.GetAreaFromName(unityAreaNames[i]), unityAreaNames[i]);
      }

      return unityNavmeshAreaMap;
#else
#if UNITY_EDITOR
      // Only supported for Editor
      var unityAreaNames = UnityEditor.GameObjectUtility.GetNavMeshAreaNames();
      for (int i = 0; i < unityAreaNames.Length; i++) {
        unityNavmeshAreaMap.Add(UnityEditor.GameObjectUtility.GetNavMeshAreaFromName(unityAreaNames[i]), unityAreaNames[i]);
      }
      return unityNavmeshAreaMap;
#else
      throw new Exception("GetNavMeshAreaNames is only supported in Editor or Unity version 2023.1+");
#endif
#endif
    }

    /// <summary>
    /// Import a Unity NavMesh into Quantum NavMesh data.
    /// </summary>
    /// <param name="scene">The Unity scene.</param>
    /// <param name="settings">The navmesh import settings.</param>
    /// <param name="name">The navmesh.</param>
    /// <returns>The resulting imported navmesh.</returns>
    public static NavMeshBakeData ImportFromUnity(Scene scene, ImportSettings settings, string name) {
      var result = new NavMeshBakeData();

      using (var progressBar = Log.Settings.Level <= LogLevel.Debug ? new ProgressBar("Importing Unity NavMesh", true) : null) {
        progressBar?.SetInfo("Calculate Triangulation");
        var unityNavMeshTriangulation = UnityEngine.AI.NavMesh.CalculateTriangulation();

        if (unityNavMeshTriangulation.vertices.Length == 0) {
          Debug.LogError("Unity NavMesh not found");
          return null;
        }

        progressBar?.SetInfo("Loading Vertices");
        var Vertices = new Vertex[unityNavMeshTriangulation.vertices.Length];
        for (int i = 0; i < Vertices.Length; ++i) {
          progressBar?.SetProgress(i / (float)Vertices.Length);
          Vertices[i].Position = new Vector3Double(unityNavMeshTriangulation.vertices[i]);
        }

        progressBar?.SetInfo("Loading Triangles");
        int triangleCount = unityNavMeshTriangulation.indices.Length / 3;
        var Triangles = new NavMeshBakeDataTriangle[triangleCount];
        for (int i = 0; i < triangleCount; ++i) {
          progressBar?.SetProgress(i / (float)triangleCount);
          int area = unityNavMeshTriangulation.areas[i];
          int baseIndex = i * 3;
          Triangles[i] = new NavMeshBakeDataTriangle() {
            VertexIds = new int[] { unityNavMeshTriangulation.indices[baseIndex + 0], unityNavMeshTriangulation.indices[baseIndex + 1], unityNavMeshTriangulation.indices[baseIndex + 2] },
            Area = area,
            RegionId = null,
            Cost = FP._1
          };
        }

        // Weld vertices
        if (settings.WeldIdenticalVertices) {
          progressBar?.SetInfo("Welding Identical Vertices");
          ImportUtils.WeldIdenticalVertices(ref Vertices, ref Triangles, settings.WeldVertexEpsilon, p => progressBar?.SetProgress(p));

          progressBar?.SetInfo("Removing Unused Vertices");
          ImportUtils.RemoveUnusedVertices(ref Vertices, ref Triangles, p => progressBar?.SetProgress(p));
        }

        // Merge vertices that lie on triangle edges
        if (settings.FixTrianglesOnEdges) {
          progressBar?.SetInfo("Fixing Triangles On Edges");
          var initialTriangleCount = Triangles.Length;
          for (int t = 0; t < initialTriangleCount; ++t) {
            progressBar?.SetProgress(t / (float)initialTriangleCount);
            for (int v = 0; v < 3; ++v) {
              ImportUtils.FixTrianglesOnEdges(ref Vertices, ref Triangles, t, v, settings.FixTrianglesOnEdgesEpsilon, settings.FixTrianglesOnEdgesHeightEpsilon);
            }
          }

          progressBar?.SetInfo("Removing Unused Vertices");
          ImportUtils.RemoveUnusedVertices(ref Vertices, ref Triangles, p => progressBar?.SetProgress(p));
        }

        if (settings.DelaunayTriangulation) {
          progressBar?.SetInfo("Delaunay Triangulation");
          progressBar?.SetProgress(0);
          var progressStep = 0.1f / (float)Triangles.Length;

          var triangles = new List<DelaunayTriangulation.Triangle>();

          for (int i = 0; i < Triangles.Length; i++) {
            if (progressBar != null) progressBar.Progress += progressStep;
            triangles.Add(new DelaunayTriangulation.Triangle {
              v1 = new DelaunayTriangulation.HalfEdgeVertex(Vertices[Triangles[i].VertexIds[0]].Position.AsVector(), Triangles[i].VertexIds[0]),
              v2 = new DelaunayTriangulation.HalfEdgeVertex(Vertices[Triangles[i].VertexIds[1]].Position.AsVector(), Triangles[i].VertexIds[1]),
              v3 = new DelaunayTriangulation.HalfEdgeVertex(Vertices[Triangles[i].VertexIds[2]].Position.AsVector(), Triangles[i].VertexIds[2]),
              t = i
            });
          }

          progressBar?.SetProgress(0.1f);
          triangles = DelaunayTriangulation.TriangulateByFlippingEdges(triangles, settings.DelaunayTriangulationRestrictToPlanes,
            () => progressBar?.SetProgress(Mathf.Min(progressBar.Progress + 0.1f, 0.9f)));

          progressBar?.SetProgress(0.9f);
          foreach (var t in triangles) {
            if (progressBar != null) progressBar.Progress += progressStep;
            Triangles[t.t].VertexIds[0] = t.v1.index;
            Triangles[t.t].VertexIds[1] = t.v2.index;
            Triangles[t.t].VertexIds[2] = t.v3.index;
          }

          progressBar?.SetProgress(1);
        }

        // Import regions
        var unityNavmeshAreaMap = settings.ImportRegionMode == NavmeshRegionImportMode.Simple ? CreateUnityNavmeshAreaMap() : new Dictionary<int, string>();
        List<string> regions = new List<string>() { "MainArea" };
        progressBar?.SetInfo("Importing Regions");

        switch (settings.ImportRegionMode) {
          case NavmeshRegionImportMode.Disabled:
            for (int t = 0; t < Triangles.Length; t++) {
              progressBar?.SetProgress(t / (float)Triangles.Length);
              Triangles[t].RegionId = "MainArea";
            }
            break;
          case NavmeshRegionImportMode.Simple:
            ImportUtils.ImportRegionsSimple(ref Triangles, ref regions, unityNavmeshAreaMap, settings.RegionAreaIds, (value) => progressBar?.SetProgress(value));
            break;
          case NavmeshRegionImportMode.Advanced:
            for (int t = 0; t < Triangles.Length; t++) {
              progressBar?.SetProgress(t / (float)Triangles.Length);
              if (settings.RegionAreaIds != null && settings.RegionAreaIds.Contains(Triangles[t].Area) && string.IsNullOrEmpty(Triangles[t].RegionId)) {
                ImportUtils.ImportRegions(scene, ref Vertices, ref Triangles, t, ref regions, settings.RegionDetectionMargin);
              }
            }

            for (int t = 0; t < Triangles.Length; t++) {
              if (Triangles[t].RegionId == null) {
                Triangles[t].RegionId = "MainArea";
              }
            }
            break;
        }

        // Set all vertex string ids (to work with manual editor)
        {
          progressBar?.SetInfo("Finalizing Vertices");
          progressBar?.SetProgress(0.5f);
          for (int v = 0; v < Vertices.Length; v++) {
            Vertices[v].Id = v.ToString();
          }
        }

        // Find links
        var links = new List<NavMeshLinkTemp>();
#if QUANTUM_ENABLE_AI_NAVIGATION
        links.AddRange(QuantumMapDataBaker.FindLocalObjects<Unity.AI.Navigation.NavMeshLink>(scene).Select(l => new NavMeshLinkTemp(l)));
#endif
#if !UNITY_2023_3_OR_NEWER
#pragma warning disable CS0618 // Type or member is obsolete
        links.AddRange(QuantumMapDataBaker.FindLocalObjects<OffMeshLink>(scene).Select(l => new NavMeshLinkTemp(l)));
#pragma warning restore CS0618
#endif
        result.Links = new NavMeshBakeDataLink[0];
        if (links.Count > 0) {
          progressBar?.SetInfo("Validating OffMeshLinks");
          progressBar?.SetProgress(0.0f);

          // Insert triangles into a temporary grid to optimize triangle searching
          var triangleGrid = new TriangleGrid(Vertices, Triangles);

          for (int l = 0; l < links.Count; l++) {
            if (links[l].IsEnabled == false) {
              // Skip links that are disabled or deactivated when importing
              continue;
            }

            var regionId = string.Empty;
            switch (settings.ImportRegionMode) {
              case NavmeshRegionImportMode.Simple:
                if (unityNavmeshAreaMap.TryGetValue(links[l].Area, out var areaName)) {
                  regionId = areaName;
                }
                break;
              case NavmeshRegionImportMode.Advanced:
                var navMeshRegion = links[l].Object.GetComponent<QuantumNavMeshRegion>();
                regionId = navMeshRegion != null && string.IsNullOrEmpty(navMeshRegion.Id) == false ? navMeshRegion.Id : string.Empty;
                break;
            }

            if (string.IsNullOrEmpty(regionId) == false && regions.Contains(regionId) == false) {
              // Add new region to global list
              regions.Add(regionId);
            }

            var startPosition = links[l].StartPoint;
            var startTriangle = FindTriangleIndex(Vertices, Triangles, settings.LinkErrorCorrection, triangleGrid, ref startPosition);

            var endPosition = links[l].EndPoint;
            var endTriangle = FindTriangleIndex(Vertices, Triangles, settings.LinkErrorCorrection, triangleGrid, ref endPosition);

            if (startTriangle == -1) {
              Debug.LogError($"Could not map start position {startPosition} of navmesh link to a triangle");
            } else if (endTriangle == -1) {
              Debug.LogError($"Could not map end position {endPosition} of navmesh link to a triangle");
            } else {
              // Add link
#if QUANTUM_XY
              if (settings.EnableQuantum_XY) {
                startPosition = new Vector3(startPosition.x, startPosition.z, startPosition.y);
                endPosition = new Vector3(endPosition.x, endPosition.z, endPosition.y);
              }
#endif
              ArrayUtils.Add(ref result.Links, new NavMeshBakeDataLink {
                Start = startPosition.ToFPVector3(),
                End = endPosition.ToFPVector3(),
                StartTriangle = startTriangle,
                EndTriangle = endTriangle,
                Bidirectional = links[l].Bidirectional,
                CostOverride = FP.FromFloat_UNSAFE(links[l].CostModifier),
                RegionId = regionId,
                Name = links[l].Object.name
              });
            }

            progressBar?.SetProgress((l + 1) / (float)links.Count);
          }
        }


        result.Vertices = Vertices.Select(v => v.Convert()).ToArray();
        result.Triangles = Triangles.ToArray();

        regions.Sort((a, b) => {
          if (a == "MainArea") return -1;
          else if (b == "MainArea") return 1;
          return string.CompareOrdinal(a, b);
        });
        result.Regions = regions.ToArray();

        Debug.LogFormat("Imported Unity NavMesh '{0}', cleaned up {1} vertices, found {2} region(s), found {3} link(s)", name, unityNavMeshTriangulation.vertices.Length - Vertices.Length, result.Regions.Length, result.Links.Length);
      }

      return result;
    }

    /// <summary>
    /// Iterates through all navmesh surfaces and detect the smallest agent radius used.
    /// </summary>
    /// <param name="navmeshSurfaces">List of navmesh surfaces to analyze.</param>
    /// <returns>The smallets agent radius in FP.</returns>
    public static FP FindSmallestAgentRadius(GameObject[] navmeshSurfaces) {
#if QUANTUM_ENABLE_AI_NAVIGATION      
      if (navmeshSurfaces != null) {
        // Try Unity Navmesh Surface tool
        float agentRadius = float.MaxValue;
        foreach (var surface in navmeshSurfaces) {
          var surfaceComponent = surface.GetComponent<Unity.AI.Navigation.NavMeshSurface>();
          if (surfaceComponent == null) {
            Debug.LogErrorFormat("No NavMeshSurface found on '{0}'", surface.name);
          } else {
            if (surfaceComponent.agentTypeID != -1) {
              var settings = UnityEngine.AI.NavMesh.GetSettingsByID(surfaceComponent.agentTypeID);
              if (settings.agentRadius < agentRadius) {
                agentRadius = settings.agentRadius;
              }
            }
          }
        }

        if (agentRadius < float.MaxValue) {
          return FP.FromFloat_UNSAFE(agentRadius);
        }
      }
#endif

      return FP.FromFloat_UNSAFE(DefaultMinAgentRadius);
    }

    private struct NavMeshLinkTemp {
      public Vector3 StartPoint;
      public Vector3 EndPoint;
      public float Width;
      public float CostModifier;
      public bool Bidirectional;
      public bool AutoUpdatePosition;
      public bool IsEnabled;
      public int Area;
      public GameObject Object;

#if QUANTUM_ENABLE_AI_NAVIGATION
      public NavMeshLinkTemp(Unity.AI.Navigation.NavMeshLink link) {
        StartPoint = link.transform != null ? link.transform.TransformPoint(link.startPoint) : link.startPoint;
        EndPoint   = link.transform != null ? link.transform.TransformPoint(link.endPoint)   : link.endPoint;
        Width = link.width;
        CostModifier = link.costModifier;
        Bidirectional = link.bidirectional;
        AutoUpdatePosition = link.autoUpdate;
        IsEnabled = link.enabled;
        Area = link.area;
        Object = link.gameObject;
      }
#endif

#if !UNITY_2023_3_OR_NEWER
#pragma warning disable CS0618 // Type or member is obsolete
      public NavMeshLinkTemp(OffMeshLink link) {
        Assert.Always(link.startTransform != null && link.endTransform != null, "Failed to import Off Mesh Link '{0}' start or end transforms are invalid", link.name);

        StartPoint = link.startTransform.position;
        EndPoint = link.endTransform.position;
        Width = 0;
        CostModifier = link.costOverride;
        Bidirectional = link.biDirectional;
        AutoUpdatePosition = link.autoUpdatePositions;
        IsEnabled = link.enabled && link.activated;
        Area = link.area;
        Object = link.gameObject;
      }
#pragma warning restore CS0618
#endif
    }

    private class TriangleGrid {
      public List<int>[] Grid { get; private set; }
      public int CellCount { get; private set; }
      public double CellSize { get; private set; }
      public Vector2Double MaxPosition { get; private set; }
      public Vector2Double MinPosition { get; private set; }

      public TriangleGrid(Vertex[] vertices, NavMeshBakeDataTriangle[] triangles, int gridCellCount = 100) {
        CellCount = gridCellCount;
        Grid = new List<int>[CellCount * CellCount];

        var maxPosition = new Vector2Double(double.MinValue, double.MinValue);
        var minPosition = new Vector2Double(double.MaxValue, double.MaxValue);
        for (int i = 0; i < vertices.Length; ++i) {
          maxPosition.X = Math.Max(maxPosition.X, vertices[i].Position.X);
          maxPosition.Y = Math.Max(maxPosition.Y, vertices[i].Position.Z);
          minPosition.X = Math.Min(minPosition.X, vertices[i].Position.X);
          minPosition.Y = Math.Min(minPosition.Y, vertices[i].Position.Z);
        }

        MaxPosition = maxPosition;
        MinPosition = minPosition;
        CellSize = Math.Max(MaxPosition.X - MinPosition.X, MaxPosition.Y - MinPosition.Y) / CellCount;

        for (int i = 0; i < triangles.Length; ++i) {
          int minCellIndexX = int.MaxValue, maxCellIndexX = int.MinValue, minCellIndexY = int.MaxValue, maxCellIndexY = int.MinValue;
          for (int j = 0; j < 3; j++) {
            var pos = vertices[triangles[i].VertexIds[j]].Position;
            minCellIndexX = Math.Min(minCellIndexX, (int)((pos.X - MinPosition.X) / CellSize));
            maxCellIndexX = Math.Max(maxCellIndexX, (int)((pos.X - MinPosition.X) / CellSize));
            minCellIndexY = Math.Min(minCellIndexY, (int)((pos.Z - MinPosition.Y) / CellSize));
            maxCellIndexY = Math.Max(maxCellIndexY, (int)((pos.Z - MinPosition.Y) / CellSize));
          }

          for (int x = minCellIndexX; x <= maxCellIndexX && x >= 0 && x < CellCount; x++) {
            for (int y = minCellIndexY; y <= maxCellIndexY && y >= 0 && y < CellCount; y++) {
              var gridCellIndex = y * CellCount + x;
              if (Grid[gridCellIndex] == null) {
                Grid[gridCellIndex] = new List<int>();
              }

              Grid[gridCellIndex].Add(i);
            }
          }
        }
      }
    }

    private static int FindTriangleIndex(Vertex[] Vertices, NavMeshBakeDataTriangle[] Triangles, float errorCorrection, TriangleGrid triangleGrid, ref Vector3 position) {
      var resultTriangleIndex = -1;
      var additionalCellsToCheck = errorCorrection > 0 ? Math.Max(1, (int)(errorCorrection / triangleGrid.CellSize)) : 0;
      var checkedTriangles = new HashSet<int>();
      var inputPosition = position;

      // find cell index (expand one cell for error correction)
      var _x = (int)((position.x - triangleGrid.MinPosition.X) / triangleGrid.CellSize);
      var _y = (int)((position.z - triangleGrid.MinPosition.Y) / triangleGrid.CellSize);
      var closestDistance = double.MaxValue;

      var xMin = Math.Max(0, _x - additionalCellsToCheck);
      var xMax = Math.Min(triangleGrid.CellCount - 1, _x + additionalCellsToCheck);
      var yMin = Math.Max(0, _y - additionalCellsToCheck);
      var yMax = Math.Min(triangleGrid.CellCount - 1, _y + additionalCellsToCheck);

      for (int x = xMin; x <= xMax; x++) {
        for (int z = yMin; z <= yMax; z++) {
          var c = (int)((z * triangleGrid.CellCount) + x);

          // no triangles in cell
          if (triangleGrid.Grid[c] == null) {
            continue;
          }

          for (int t = 0; t < triangleGrid.Grid[c].Count; t++) {
            var triangleIndex = triangleGrid.Grid[c][t];

            // already checked triangle
            if (checkedTriangles.Contains(triangleIndex)) {
              continue;
            }

            checkedTriangles.Add(triangleIndex);

            var closestPoint = new Vector3Double();
            var d = Vector3Double.ClosestDistanceToTriangle(new Vector3Double(inputPosition),
              Vertices[Triangles[triangleIndex].VertexIds[0]].Position,
              Vertices[Triangles[triangleIndex].VertexIds[1]].Position,
              Vertices[Triangles[triangleIndex].VertexIds[2]].Position,
              ref closestPoint);
            if (d < closestDistance) {
              closestDistance = d;
              resultTriangleIndex = triangleIndex;
              if (errorCorrection > 0) {
                position = closestPoint.AsVector();
              }
            }
          }
        }
      }

      return resultTriangleIndex;
    }

    /// <summary>
    /// Math class only used by <see cref="QuantumNavMesh"/> import calculations. Uses double precision.
    /// </summary>
    public struct Vector2Double {
      /// <summary>
      /// X component of the vector.
      /// </summary>
      public double X;
      /// <summary>
      /// Y component of the vector.
      /// </summary>
      public double Y;

      /// <summary>
      /// Create new vector.
      /// </summary>
      public Vector2Double(double x, double y) {
        X = x;
        Y = y;
      }

      /// <summary>
      /// Minus operator.
      /// </summary>
      public static Vector2Double operator -(Vector2Double a, Vector2Double b) {
        return new Vector2Double(a.X - b.X, a.Y - b.Y);
      }

      /// <summary>
      /// Calculate the distance between two points.
      /// </summary>
      /// <param name="a">Point a.</param>
      /// <param name="b">Point b.</param>
      /// <returns>The distance between to points.</returns>
      public static double Distance(Vector2Double a, Vector2Double b) {
        var v = a - b;
        return Math.Sqrt(v.X * v.X + v.Y * v.Y);
      }
    }

    /// <summary>
    /// Math class only used by <see cref="QuantumNavMesh"/> import calculations. Uses double precision.
    /// </summary>
    public struct Vector3Double {
      /// <summary>
      /// X component of the vector.
      /// </summary>
      public double X;
      /// <summary>
      /// Y component of the vector.
      /// </summary>
      public double Y;
      /// <summary>
      /// Z component of the vector.
      /// </summary>
      public double Z;

      /// <summary>
      /// Crate a new vector.
      /// </summary>
      public Vector3Double(double x, double y, double z) {
        X = x;
        Y = y;
        Z = z;
      }

      /// <summary>
      /// Create a new vector using a Quantum fixed point vector.
      /// </summary>
      /// <param name="v"></param>
      public Vector3Double(FPVector3 v) {
        X = v.X.AsDouble;
        Y = v.Y.AsDouble;
        Z = v.Z.AsDouble;
      }

      /// <summary>
      /// Create a new vector using a Unity vector.
      /// </summary>
      /// <param name="v"></param>
      public Vector3Double(Vector3 v) {
        X = v.x;
        Y = v.y;
        Z = v.z;
      }

      /// <summary>
      /// Returns a value indicating whether this instance is equal to a specified Vector3Double value.
      /// </summary>
      /// <param name="obj">An Vector3Double value to compare to this instance.</param>
      /// <returns><see langword="true"/> if other has the same value as this instance; otherwise, <see langword="false"/>.</returns>
      public override Boolean Equals(Object obj) {
        if (obj is Vector3Double) {
          return this == ((Vector3Double)obj);
        }

        return false;
      }

      /// <summary>
      /// Overrides the default hash function.
      /// </summary>
      /// <returns>A hash code for the current object.</returns>
      public override Int32 GetHashCode() {
        unchecked {
          var hash = 17;
          hash = hash * 31 + X.GetHashCode();
          hash = hash * 31 + Y.GetHashCode();
          hash = hash * 31 + Z.GetHashCode();
          return hash;
        }
      }

      /// <summary>
      /// Operator override for which checks if two instances of Vector3Double are equal.
      /// </summary>
      /// <returns><see langword="true"/> if the instances are equal.</returns>
      public static bool operator ==(Vector3Double a, Vector3Double b) {
        return a.X == b.X && a.Y == b.Y && a.Z == b.Z;
      }

      /// <summary>
      /// Operator override for which checks if two instances of REPLACE are not equal.
      /// </summary>
      /// <returns><see langword="true"/> if the instances are not equal.</returns>
      public static bool operator !=(Vector3Double a, Vector3Double b) {
        return a.X != b.X || a.Y != b.Y || a.Z != b.Z;
      }

      /// <summary>
      /// Subtracts two Vector3Double instances.
      /// </summary>
      public static Vector3Double operator -(Vector3Double a, Vector3Double b) {
        return new Vector3Double(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
      }

      /// <summary>
      /// Adds two Vector3Double instances.
      /// </summary>
      public static Vector3Double operator +(Vector3Double a, Vector3Double b) {
        return new Vector3Double(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
      }

      /// <summary>
      /// Multiplies a Vector3Double instance with a scalar.
      /// </summary>
      public static Vector3Double operator *(Vector3Double a, double b) {
        return new Vector3Double(a.X * b, a.Y * b, a.Z * b);
      }

      /// <summary>
      /// Multiplies a Vector3Double instance with a scalar.
      /// </summary>
      public static Vector3Double operator *(double b, Vector3Double a) {
        return new Vector3Double(a.X * b, a.Y * b, a.Z * b);
      }

      /// <summary>
      /// Converts into fixed point vector. Only safe during editor as it uses <see cref="FP.FromFloat_UNSAFE(float)"/>.
      /// </summary>
      /// <returns></returns>
      public FPVector3 AsFPVector() {
        return new FPVector3(FP.FromFloat_UNSAFE((float)X), FP.FromFloat_UNSAFE((float)Y), FP.FromFloat_UNSAFE((float)Z));
      }

      /// <summary>
      /// Converts into Unity vector.
      /// </summary>
      /// <returns></returns>
      public Vector3 AsVector() {
        return new Vector3((float)X, (float)Y, (float)Z);
      }

      /// <summary>
      /// Returns the square magnitude of the vector.
      /// </summary>
      public double SqrMagnitude() {
        return X * X + Y * Y + Z * Z;
      }

      /// <summary>
      /// Returns the square magnitude <paramref name="v"/>
      /// </summary>
      public static double SqrMagnitude(Vector3Double v) {
        return v.X * v.X + v.Y * v.Y + v.Z * v.Z;
      }

      /// <summary>
      /// Returns the magnitude of the vector.
      /// </summary>
      public double Magnitude() {
        return Math.Sqrt(X * X + Y * Y + Z * Z);
      }

      /// <summary>
      /// Returns the distance between two points.
      /// </summary>
      public static double Distance(Vector3Double a, Vector3Double b) {
        var v = a - b;
        return Math.Sqrt(v.X * v.X + v.Y * v.Y + v.Z * v.Z);
      }

      /// <summary>
      /// Normalized the vector.
      /// </summary>
      /// <exception cref="ArgumentException">Is raised when the magnitude is 0.</exception>
      public void Normalize() {
        var d = Math.Sqrt(X * X + Y * Y + Z * Z);

        if (d == 0) {
          throw new ArgumentException("Vector magnitude is null");
        }

        X = X / d;
        Y = Y / d;
        Z = Z / d;
      }

      /// <summary>
      /// Converts the numeric value of this instance to its equivalent string representation.
      /// </summary>
      public override string ToString() {
        return $"{X} {Y} {Z}";
      }

      /// <summary>
      /// Returns the dot product of two vectors.
      /// </summary>
      public static double Dot(Vector3Double a, Vector3Double b) {
        return a.X * b.X + a.Y * b.Y + a.Z * b.Z;
      }

      /// <summary>
      /// Returns the cross product of two vectors.
      /// </summary>
      public static Vector3Double Cross(Vector3Double a, Vector3Double b) {
        return new Vector3Double(
          a.Y * b.Z - a.Z * b.Y,
          a.Z * b.X - a.X * b.Z,
          a.X * b.Y - a.Y * b.X);
      }

      /// <summary>
      /// Calculates if the point <paramref name="p"/> is between the two points <paramref name="v0"/> and <paramref name="v1"/>.
      /// </summary>
      public static bool IsPointBetween(Vector3Double p, Vector3Double v0, Vector3Double v1, float epsilon, float epsilonHeight) {
        // We don't want to compare end points only is p is "really" in between
        if (p == v0 || p == v1 || v0 == v1)
          return false;

#if QUANTUM_XY
        var p0 = Vector2Double.Distance(new Vector2Double(p.X, p.Y), new Vector2Double(v0.X, v0.Y));
        var p1 = Vector2Double.Distance(new Vector2Double(p.X, p.Y), new Vector2Double(v1.X, v1.Y));
        var v = Vector2Double.Distance(new Vector2Double(v0.X, v0.Y), new Vector2Double(v1.X, v1.Y));
#else
        var p0 = Vector2Double.Distance(new Vector2Double(p.X, p.Z), new Vector2Double(v0.X, v0.Z));
        var p1 = Vector2Double.Distance(new Vector2Double(p.X, p.Z), new Vector2Double(v1.X, v1.Z));
        var v = Vector2Double.Distance(new Vector2Double(v0.X, v0.Z), new Vector2Double(v1.X, v1.Z));
#endif

        // Is between in 2D
        if (Math.Abs(p0 + p1 - v) > epsilon) {
          return false;
        }

        // Check height offset to edge
        var closestPoint = ClosestPointOnSegment(p, v0, v1);

#if QUANTUM_XY
        return Math.Abs(closestPoint.Z - p.Z) < epsilonHeight;
#else
        return Math.Abs(closestPoint.Y - p.Y) < epsilonHeight;
#endif
      }

      private static Vector3Double ClosestPointOnSegment(Vector3Double point, Vector3Double v0, Vector3Double v1) {
        var x = v0.X - v1.X;
        var y = v0.Y - v1.Y;
        var z = v0.Z - v1.Z;
        var l2 = x * x + y * y + z * z;

        if (l2 == 0) return v0;

        x = (point.X - v0.X) * (v1.X - v0.X);
        y = (point.Y - v0.Y) * (v1.Y - v0.Y);
        z = (point.Z - v0.Z) * (v1.Z - v0.Z);
        var t = Math.Max(0, Math.Min(1, (x + y + z) / l2));

        Vector3Double result;
        result.X = v0.X + t * (v1.X - v0.X);
        result.Y = v0.Y + t * (v1.Y - v0.Y);
        result.Z = v0.Z + t * (v1.Z - v0.Z);

        return result;
      }

      /// <summary>
      /// Calculates the closest distance from point <paramref name="p"/> to the triangle defined by <paramref name="v0"/>, <paramref name="v1"/> and <paramref name="v2"/>.
      /// </summary>
      public static double ClosestDistanceToTriangle(Vector3Double p, Vector3Double v0, Vector3Double v1, Vector3Double v2, ref Vector3Double closestPoint) {
        var diff = p - v0;
        var edge0 = v1 - v0;
        var edge1 = v2 - v0;
        var a00 = Dot(edge0, edge0);
        var a01 = Dot(edge0, edge1);
        var a11 = Dot(edge1, edge1);
        var b0 = -Dot(diff, edge0);
        var b1 = -Dot(diff, edge1);
        var det = a00 * a11 - a01 * a01;
        var t0 = a01 * b1 - a11 * b0;
        var t1 = a01 * b0 - a00 * b1;

        if (t0 + t1 <= det) {
          if (t0 < 0) {
            if (t1 < 0) {
              if (b0 < 0) {
                t1 = 0;
                if (-b0 >= a00) {
                  t0 = 1;
                } else {
                  t0 = -b0 / a00;
                }
              } else {
                t0 = 0;
                if (b1 >= 0) {
                  t1 = 0;
                } else if (-b1 >= a11) {
                  t1 = 1;
                } else {
                  t1 = -b1 / a11;
                }
              }
            } else {
              t0 = 0;
              if (b1 >= 0) {
                t1 = 0;
              } else if (-b1 >= a11) {
                t1 = 1;
              } else {
                t1 = -b1 / a11;
              }
            }
          } else if (t1 < 0) {
            t1 = 0;
            if (b0 >= 0) {
              t0 = 0;
            } else if (-b0 >= a00) {
              t0 = 1;
            } else {
              t0 = -b0 / a00;
            }
          } else {
            t0 /= det;
            t1 /= det;
          }
        } else {
          double tmp0, tmp1, numer, denom;

          if (t0 < 0) {
            tmp0 = a01 + b0;
            tmp1 = a11 + b1;
            if (tmp1 > tmp0) {
              numer = tmp1 - tmp0;
              denom = a00 - 2 * a01 + a11;
              if (numer >= denom) {
                t0 = 1;
                t1 = 0;
              } else {
                t0 = numer / denom;
                t1 = 1 - t0;
              }
            } else {
              t0 = 0;
              if (tmp1 <= 0) {
                t1 = 1;
              } else if (b1 >= 0) {
                t1 = 0;
              } else {
                t1 = -b1 / a11;
              }
            }
          } else if (t1 < 0) {
            tmp0 = a01 + b1;
            tmp1 = a00 + b0;
            if (tmp1 > tmp0) {
              numer = tmp1 - tmp0;
              denom = a00 - 2 * a01 + a11;
              if (numer >= denom) {
                t1 = 1;
                t0 = 0;
              } else {
                t1 = numer / denom;
                t0 = 1 - t1;
              }
            } else {
              t1 = 0;
              if (tmp1 <= 0) {
                t0 = 1;
              } else if (b0 >= 0) {
                t0 = 0;
              } else {
                t0 = -b0 / a00;
              }
            }
          } else {
            numer = a11 + b1 - a01 - b0;
            if (numer <= 0) {
              t0 = 0;
              t1 = 1;
            } else {
              denom = a00 - 2 * a01 + a11;
              if (numer >= denom) {
                t0 = 1;
                t1 = 0;
              } else {
                t0 = numer / denom;
                t1 = 1 - t0;
              }
            }
          }
        }

        closestPoint = v0 + t0 * edge0 + t1 * edge1;
        diff = p - closestPoint;
        return diff.SqrMagnitude();
      }
    }
#endif

    #endregion

    #region Gizmos

#if UNITY_EDITOR


#endif

    #endregion

    #region Delaunay Triangulation

#if QUANTUM_ENABLE_AI && !QUANTUM_DISABLE_AI

    //MIT License
    //Copyright(c) 2020 Erik Nordeus
    //Permission is hereby granted, free of charge, to any person obtaining a copy
    //of this software and associated documentation files (the "Software"), to deal
    //in the Software without restriction, including without limitation the rights
    //to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
    //copies of the Software, and to permit persons to whom the Software is
    //furnished to do so, subject to the following conditions:
    //The above copyright notice and this permission notice shall be included in all
    //copies or substantial portions of the Software.
    //THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
    //IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
    //FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
    //AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
    //LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
    //OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
    //SOFTWARE.
    public static class DelaunayTriangulation {
      //An edge going in a direction
      public class HalfEdge {
        //The vertex it points to
        public HalfEdgeVertex v;

        //The next half-edge inside the face (ordered clockwise)
        //The document says counter-clockwise but clockwise is easier because that's how Unity is displaying triangles
        public HalfEdge nextEdge;

        //The opposite half-edge belonging to the neighbor
        public HalfEdge oppositeEdge;

        //(optionally) the previous halfedge in the face
        //If we assume the face is closed, then we could identify this edge by walking forward
        //until we reach it
        public HalfEdge prevEdge;

        public Triangle t;

        public HalfEdge(HalfEdgeVertex v) {
          this.v = v;
        }
      }

      public class HalfEdgeVertex {
        //The position of the vertex
        public Vector3 position;

        public int index;

        //Each vertex references an half-edge that starts at this point
        //Might seem strange because each halfEdge references a vertex the edge is going to?
        public HalfEdge edge;

        public HalfEdgeVertex(Vector3 position, int index) {
          this.position = position;
          this.index = index;
        }
      }

      //To store triangle data to get cleaner code
      public class Triangle {
        //Corners of the triangle
        public HalfEdgeVertex v1, v2, v3;
        public int t;

        public HalfEdge edge;

        public void ChangeOrientation() {
          var temp = v1;
          v1 = v2;
          v2 = temp;
        }
      }


      //Alternative 1. Triangulate with some algorithm - then flip edges until we have a delaunay triangulation
      public static List<Triangle> TriangulateByFlippingEdges(List<Triangle> triangles, bool retrictToPlanes, Action reporter) {
        // Change the structure from triangle to half-edge to make it faster to flip edges
        List<HalfEdge> halfEdges = TransformFromTriangleToHalfEdge(triangles);

        //Flip edges until we have a delaunay triangulation
        int safety = 0;
        int flippedEdges = 0;
        while (true) {
          safety += 1;

          if (safety > 100000) {
            Debug.Log("Stuck in endless loop");

            break;
          }

          bool hasFlippedEdge = false;

          //Search through all edges to see if we can flip an edge
          for (int i = 0; i < halfEdges.Count; i++) {
            HalfEdge thisEdge = halfEdges[i];

            //Is this edge sharing an edge, otherwise its a border, and then we cant flip the edge
            if (thisEdge.oppositeEdge == null) {
              continue;
            }

            //The vertices belonging to the two triangles, c-a are the edge vertices, b belongs to this triangle
            var a = thisEdge.v;
            var b = thisEdge.nextEdge.v;
            var c = thisEdge.prevEdge.v;
            var d = thisEdge.oppositeEdge.nextEdge.v;

            if (retrictToPlanes) {
              // Both triangles must be in one plane
              var plane = new Plane(a.position, b.position, c.position);
              var isOnPlane = Mathf.Abs(plane.GetDistanceToPoint(d.position));
              if (isOnPlane > float.Epsilon) {
                continue;
              }
            }

            Vector2 aPos = new Vector2(a.position.x, a.position.z);
            Vector2 bPos = new Vector2(b.position.x, b.position.z);
            Vector2 cPos = new Vector2(c.position.x, c.position.z);
            Vector2 dPos = new Vector2(d.position.x, d.position.z);

            //Use the circle test to test if we need to flip this edge
            if (IsPointInsideOutsideOrOnCircle(aPos, bPos, cPos, dPos) < 0f) {
              //Are these the two triangles that share this edge forming a convex quadrilateral?
              //Otherwise the edge cant be flipped
              if (IsQuadrilateralConvex(aPos, bPos, cPos, dPos)) {
                //If the new triangle after a flip is not better, then dont flip
                //This will also stop the algoritm from ending up in an endless loop
                if (IsPointInsideOutsideOrOnCircle(bPos, cPos, dPos, aPos) < 0f) {
                  continue;
                }

                //Flip the edge
                flippedEdges += 1;

                hasFlippedEdge = true;

                FlipEdge(thisEdge);
              }
            }
          }

          reporter.Invoke();

          //We have searched through all edges and havent found an edge to flip, so we have a Delaunay triangulation!
          if (!hasFlippedEdge) {
            //Debug.Log("Found a delaunay triangulation");
            break;
          }
        }

        Debug.Log("Delaunay triangulation flipped edges: " + flippedEdges);

        //Dont have to convert from half edge to triangle because the algorithm will modify the objects, which belongs to the 
        //original triangles, so the triangles have the data we need

        return triangles;
      }

      //From triangle where each triangle has one vertex to half edge
      private static List<HalfEdge> TransformFromTriangleToHalfEdge(List<Triangle> triangles) {
        //Make sure the triangles have the same orientation
        OrientTrianglesClockwise(triangles);

        //First create a list with all possible half-edges
        List<HalfEdge> halfEdges = new List<HalfEdge>(triangles.Count * 3);

        for (int i = 0; i < triangles.Count; i++) {
          Triangle t = triangles[i];

          HalfEdge he1 = new HalfEdge(t.v1);
          HalfEdge he2 = new HalfEdge(t.v2);
          HalfEdge he3 = new HalfEdge(t.v3);

          he1.nextEdge = he2;
          he2.nextEdge = he3;
          he3.nextEdge = he1;

          he1.prevEdge = he3;
          he2.prevEdge = he1;
          he3.prevEdge = he2;

          //The vertex needs to know of an edge going from it
          he1.v.edge = he2;
          he2.v.edge = he3;
          he3.v.edge = he1;

          //The face the half-edge is connected to
          t.edge = he1;

          he1.t = t;
          he2.t = t;
          he3.t = t;

          //Add the half-edges to the list
          halfEdges.Add(he1);
          halfEdges.Add(he2);
          halfEdges.Add(he3);
        }

        //Find the half-edges going in the opposite direction
        for (int i = 0; i < halfEdges.Count; i++) {
          HalfEdge he = halfEdges[i];

          var goingToVertex = he.v;
          var goingFromVertex = he.prevEdge.v;

          for (int j = 0; j < halfEdges.Count; j++) {
            //Dont compare with itself
            if (i == j) {
              continue;
            }

            HalfEdge heOpposite = halfEdges[j];

            //Is this edge going between the vertices in the opposite direction
            if (goingFromVertex.position == heOpposite.v.position && goingToVertex.position == heOpposite.prevEdge.v.position) {
              he.oppositeEdge = heOpposite;

              break;
            }
          }
        }


        return halfEdges;
      }

      //Orient triangles so they have the correct orientation
      private static void OrientTrianglesClockwise(List<Triangle> triangles) {
        for (int i = 0; i < triangles.Count; i++) {
          Triangle tri = triangles[i];

          Vector2 v1 = new Vector2(tri.v1.position.x, tri.v1.position.z);
          Vector2 v2 = new Vector2(tri.v2.position.x, tri.v2.position.z);
          Vector2 v3 = new Vector2(tri.v3.position.x, tri.v3.position.z);

          if (!IsTriangleOrientedClockwise(v1, v2, v3)) {
            tri.ChangeOrientation();
          }
        }
      }

      //Is a triangle in 2d space oriented clockwise or counter-clockwise
      //https://math.stackexchange.com/questions/1324179/how-to-tell-if-3-connected-points-are-connected-clockwise-or-counter-clockwise
      //https://en.wikipedia.org/wiki/Curve_orientation
      private static bool IsTriangleOrientedClockwise(Vector2 p1, Vector2 p2, Vector2 p3) {
        bool isClockWise = true;

        float determinant = p1.x * p2.y + p3.x * p1.y + p2.x * p3.y - p1.x * p3.y - p3.x * p2.y - p2.x * p1.y;

        if (determinant > 0f) {
          isClockWise = false;
        }

        return isClockWise;
      }

      //Is a point d inside, outside or on the same circle as a, b, c
      //https://gamedev.stackexchange.com/questions/71328/how-can-i-add-and-subtract-convex-polygons
      //Returns positive if inside, negative if outside, and 0 if on the circle
      private static float IsPointInsideOutsideOrOnCircle(Vector2 aVec, Vector2 bVec, Vector2 cVec, Vector2 dVec) {
        //This first part will simplify how we calculate the determinant
        float a = aVec.x - dVec.x;
        float d = bVec.x - dVec.x;
        float g = cVec.x - dVec.x;

        float b = aVec.y - dVec.y;
        float e = bVec.y - dVec.y;
        float h = cVec.y - dVec.y;

        float c = a * a + b * b;
        float f = d * d + e * e;
        float i = g * g + h * h;

        float determinant = (a * e * i) + (b * f * g) + (c * d * h) - (g * e * c) - (h * f * a) - (i * d * b);

        return determinant;
      }

      //Is a quadrilateral convex? Assume no 3 points are colinear and the shape doesnt look like an hourglass
      private static bool IsQuadrilateralConvex(Vector2 a, Vector2 b, Vector2 c, Vector2 d) {
        bool isConvex = false;

        bool abc = IsTriangleOrientedClockwise(a, b, c);
        bool abd = IsTriangleOrientedClockwise(a, b, d);
        bool bcd = IsTriangleOrientedClockwise(b, c, d);
        bool cad = IsTriangleOrientedClockwise(c, a, d);

        if (abc && abd && bcd & !cad) {
          isConvex = true;
        } else if (abc && abd && !bcd & cad) {
          isConvex = true;
        } else if (abc && !abd && bcd & cad) {
          isConvex = true;
        }
        //The opposite sign, which makes everything inverted
        else if (!abc && !abd && !bcd & cad) {
          isConvex = true;
        } else if (!abc && !abd && bcd & !cad) {
          isConvex = true;
        } else if (!abc && abd && !bcd & !cad) {
          isConvex = true;
        }


        return isConvex;
      }

      //Flip an edge
      private static void FlipEdge(HalfEdge one) {
        //The data we need
        //This edge's triangle
        HalfEdge two = one.nextEdge;
        HalfEdge three = one.prevEdge;
        //The opposite edge's triangle
        HalfEdge four = one.oppositeEdge;
        HalfEdge five = one.oppositeEdge.nextEdge;
        HalfEdge six = one.oppositeEdge.prevEdge;
        //The vertices
        var a = one.v;
        var b = one.nextEdge.v;
        var c = one.prevEdge.v;
        var d = one.oppositeEdge.nextEdge.v;


        //Flip

        //Change vertex
        a.edge = one.nextEdge;
        c.edge = one.oppositeEdge.nextEdge;

        //Change half-edge
        //Half-edge - half-edge connections
        one.nextEdge = three;
        one.prevEdge = five;

        two.nextEdge = four;
        two.prevEdge = six;

        three.nextEdge = five;
        three.prevEdge = one;

        four.nextEdge = six;
        four.prevEdge = two;

        five.nextEdge = one;
        five.prevEdge = three;

        six.nextEdge = two;
        six.prevEdge = four;

        //Half-edge - vertex connection
        one.v = b;
        two.v = b;
        three.v = c;
        four.v = d;
        five.v = d;
        six.v = a;

        //Half-edge - triangle connection
        Triangle t1 = one.t;
        Triangle t2 = four.t;

        one.t = t1;
        three.t = t1;
        five.t = t1;

        two.t = t2;
        four.t = t2;
        six.t = t2;

        //Opposite-edges are not changing!

        //Triangle connection
        t1.v1 = b;
        t1.v2 = c;
        t1.v3 = d;

        t2.v1 = b;
        t2.v2 = d;
        t2.v3 = a;

        t1.edge = three;
        t2.edge = four;
      }
    }

#endif

    #endregion
  }
}

#endregion


#region Assets/Photon/Quantum/Runtime/QuantumNetworkCommunicator.cs

namespace Quantum {
  using System;
  using System.Collections.Generic;
  using Photon.Client;
  using Photon.Deterministic;
  using Photon.Realtime;

  /// <summary>
  /// This class implements the Quantum interface <see cref="ICommunicator"/> and uses Photon Realtime in Unity 
  /// to support sending <see cref="RaiseEvent(byte, byte[], int, bool, int[])"/> and receiving <see cref="AddEventListener(Photon.Deterministic.OnEventReceived)"/> 
  /// network messages from the Photon Cloud.
  /// </summary>
  public partial class QuantumNetworkCommunicator : ICommunicator {
    private readonly RealtimeClient _realtimeClient;
    private readonly Dictionary<Byte, Object> _parameters;
    readonly ByteArraySlice _sendSlice = new ByteArraySlice();
    private RaiseEventArgs _eventOptions;
    private Action<EventData> _lastEventCallback;

    /// <summary>
    /// When this class is assigned to a Quantum session this option configures what happens to the standing 
    /// online connecting when the Quantum simulation is completed. By default the connection is shutdown as well.
    /// Other options are staying in the Photon room or returning to the master server.
    /// </summary>
    public ShutdownConnectionOptions ShutdownConnectionOptions { get; set; }

    /// <summary>
    /// The Realtime client object which is assigned inside the constructor.
    /// </summary>
    public RealtimeClient NetworkClient => _realtimeClient;

    /// <summary>
    /// Returns <see cref="RealtimeClient.IsConnected"/>.
    /// </summary>
    public Boolean IsConnected {
      get {
        return _realtimeClient.IsConnected;
      }
    }

    /// <summary>
    /// Returns the RTT measured by the Realtime client.
    /// </summary>
    public Int32 RoundTripTime {
      get {
        return (int)_realtimeClient.RealtimePeer.Stats.RoundtripTime;
      }
    }

    /// <summary>
    /// Returns the Photon Actor Number that this client was assigned to.
    /// </summary>
    public Int32 ActorNumber => _realtimeClient.LocalPlayer.ActorNumber;

    /// <summary>
    /// Create instance to assign to <see cref="SessionRunner.Arguments.Communicator"/>.
    /// The client object is expected to be connected to a game server (joined a room).
    /// </summary>
    /// <param name="loadBalancingClient">The connected Realtime client object</param>
    /// <param name="shutdownConnectionOptions">Optionally chose the shutdown behaviour</param>
    public QuantumNetworkCommunicator(RealtimeClient loadBalancingClient, ShutdownConnectionOptions shutdownConnectionOptions = ShutdownConnectionOptions.Disconnect) {
      _realtimeClient = loadBalancingClient;
      _realtimeClient.RealtimePeer.PingInterval = 50;
      _realtimeClient.RealtimePeer.UseByteArraySlicePoolForEvents = true;

      _parameters = new Dictionary<Byte, Object>();
      _parameters[ParameterCode.ReceiverGroup] = (byte)ReceiverGroup.All;

      _eventOptions = new RaiseEventArgs();
      ShutdownConnectionOptions = shutdownConnectionOptions;
    }

    /// <summary>
    /// Called by Quantum to recycle incoming message objects.
    /// </summary>
    /// <param name="obj">Message object to recycle</param>
    public void DisposeEventObject(object obj) {
      if (obj is ByteArraySlice bas) {
        bas.Release();
      }
    }

    /// <summary>
    /// Called by Quantum to send messages.
    /// </summary>
    public void RaiseEvent(Byte eventCode, byte[] message, int messageLength, Boolean reliable, Int32[] toPlayers) {
      _sendSlice.Buffer = message;
      _sendSlice.Count = messageLength;
      _sendSlice.Offset = 0;

      _eventOptions.TargetActors = toPlayers;

      var sendOptions = new SendOptions {
        // Send all unreliable messages via channel 1
        Channel = reliable ? (byte)0 : (byte)1,
        // Send all unreliable messages as Unsequenced
        DeliveryMode = reliable ? DeliveryMode.Reliable : DeliveryMode.UnreliableUnsequenced
      };

      _realtimeClient.OpRaiseEvent(eventCode, _sendSlice, _eventOptions, sendOptions);

      // If multiple events are send during a "frame" this only has to be called once after raising them.
      _realtimeClient.RealtimePeer.SendOutgoingCommands();
    }

    /// <summary>
    /// Called by Quantum to subscribe to incoming messages.
    /// </summary>
    public void AddEventListener(OnEventReceived onEventReceived) {
      RemoveEventListener();

      // save callback we know how to de-register it
      _lastEventCallback = (eventData) => {
        var bas = eventData.CustomData as ByteArraySlice;
        if (bas != null) {
          onEventReceived(eventData.Code, bas.Buffer, bas.Count, bas);
        }
      };

      _realtimeClient.EventReceived += _lastEventCallback;
    }

    /// <summary>
    /// Called by Quantum to update the Realtime client.
    /// </summary>
    public void Service() {
      // Can be optimized by splitting into receiving and sending and called from Quantum accordingly
      _realtimeClient.Service();
    }

    /// <summary>
    /// Called by Quantum when the simulation is shut down.
    /// Also called by the <see cref="SessionRunner"/> when shutting down.
    /// </summary>
    public void OnDestroy() {
      RemoveEventListener();
      EndConnection(ShutdownConnectionOptions);
    }

    /// <summary>
    /// Called when the <see cref="SessionRunner"/> is shutting down async.
    /// </summary>
    public System.Threading.Tasks.Task OnDestroyAsync() {
      RemoveEventListener();
      return EndConnectionAsync(ShutdownConnectionOptions);
    }

    private void RemoveEventListener() {
      if (_lastEventCallback != null) {
        _realtimeClient.EventReceived -= _lastEventCallback;
        _lastEventCallback = null;
      }
    }

    private void EndConnection(ShutdownConnectionOptions option) {
      switch (option) {
        case ShutdownConnectionOptions.None:
          return;
        case ShutdownConnectionOptions.LeaveRoom:
        case ShutdownConnectionOptions.LeaveRoomAndBecomeInactive:
          if (_realtimeClient.State == ClientState.Joined) {
            _realtimeClient.OpLeaveRoom(option == ShutdownConnectionOptions.LeaveRoomAndBecomeInactive);
            return;
          }

          break;
      }

      _realtimeClient.Disconnect();
    }

    private System.Threading.Tasks.Task EndConnectionAsync(ShutdownConnectionOptions option) {
      switch (option) {
        case ShutdownConnectionOptions.None:
          return System.Threading.Tasks.Task.CompletedTask;
        case ShutdownConnectionOptions.LeaveRoom:
        case ShutdownConnectionOptions.LeaveRoomAndBecomeInactive:
          if (_realtimeClient.State == ClientState.Joined) {
            return _realtimeClient.LeaveRoomAsync(option == ShutdownConnectionOptions.LeaveRoomAndBecomeInactive);
          }

          break;
      }

      return _realtimeClient.DisconnectAsync();
    }

    #region Legacy

    [Obsolete("Use ShutdownConnectionOptions")]
    public QuitBehaviour ThisQuitBehaviour => QuitBehaviour.None;

    [Obsolete("Use ShutdownConnectionOptions")]
    public enum QuitBehaviour {
      LeaveRoom = ShutdownConnectionOptions.LeaveRoom,
      LeaveRoomAndBecomeInactive = ShutdownConnectionOptions.LeaveRoomAndBecomeInactive,
      Disconnect = ShutdownConnectionOptions.Disconnect,
      None = ShutdownConnectionOptions.None
    }

    #endregion
  }
}

#endregion


#region Assets/Photon/Quantum/Runtime/QuantumProfilingClient.cs

namespace Quantum {
  using System;
  using System.Collections.Generic;
  using System.Linq;
  using Photon.Deterministic;
  using UnityEngine;
#if QUANTUM_REMOTE_PROFILER
  using System.Diagnostics;
  using System.Net;
  using Quantum;
  using Quantum.Profiling;
  using LiteNetLib;
  using LiteNetLib.Utils;
#endif

  public static class QuantumProfilingClientConstants {
    public const string DISCOVER_TOKEN          = "QuantumProfiling/Discover";
    public const string DISCOVER_RESPONSE_TOKEN = "QuantumProfiling/DiscoverResponse";
    public const string CONNECT_TOKEN           = "QuantumProfiling/Connect";

    public const byte ClientInfoMessage = 0;
    public const byte FrameMessage      = 1;
  }

  [Serializable]
  public class QuantumProfilingClientInfo {
    [Serializable]
    public class CustomProperty {
      public string Name;
      public string Value;
    }

    public string                     ProfilerId;
    public DeterministicSessionConfig Config;
    public List<CustomProperty>       Properties = new List<CustomProperty>();

    public QuantumProfilingClientInfo() {
    }

    public QuantumProfilingClientInfo(string clientId, DeterministicSessionConfig config, DeterministicPlatformInfo platformInfo) {
      ProfilerId = Guid.NewGuid().ToString();
      Config     = config;

      Properties.Add(CreateProperty("ClientId", clientId));
      Properties.Add(CreateProperty("MachineName", Environment.MachineName));
      Properties.Add(CreateProperty("Architecture", platformInfo.Architecture));
      Properties.Add(CreateProperty("Platform", platformInfo.Platform));
      Properties.Add(CreateProperty("RuntimeHost", platformInfo.RuntimeHost));
      Properties.Add(CreateProperty("Runtime", platformInfo.Runtime));
      Properties.Add(CreateProperty("UnityVersion", Application.unityVersion));
      Properties.Add(CreateProperty("LogicalCoreCount", SystemInfo.processorCount));
      Properties.Add(CreateProperty("CpuFrequency", SystemInfo.processorFrequency));
      Properties.Add(CreateProperty("MemorySizeMB", SystemInfo.systemMemorySize));
      Properties.Add(CreateProperty("OperatingSystem", SystemInfo.operatingSystem));
      Properties.Add(CreateProperty("DeviceModel", SystemInfo.deviceModel));
      Properties.Add(CreateProperty("DeviceName", SystemInfo.deviceName));
      Properties.Add(CreateProperty("ProcessorType", SystemInfo.processorType));
    }


    public         string         GetProperty(string name, string defaultValue = "Unknown") => Properties.Where(x => x.Name == name).SingleOrDefault()?.Value ?? defaultValue;
    private static CustomProperty CreateProperty<T>(string name, T value)                   => CreateProperty(name, value.ToString());

    private static CustomProperty CreateProperty(string name, string value) {
      return new CustomProperty() {
        Name  = name,
        Value = value,
      };
    }
  }

#if QUANTUM_REMOTE_PROFILER
  public class QuantumProfilingClient : IDisposable {
    const double BROADCAST_INTERVAL = 1;

    QuantumProfilingClientInfo  _clientInfo;
    NetManager                  _manager;
    EventBasedNetListener       _listener;
    NetPeer                     _serverPeer;

    Stopwatch _broadcastTimer;
    double    _broadcastNext;
  

    public QuantumProfilingClient(string clientId, DeterministicSessionConfig config, DeterministicPlatformInfo platformInfo) {
      _clientInfo = new QuantumProfilingClientInfo(clientId, config, platformInfo);
      _broadcastTimer = Stopwatch.StartNew();

      _listener = new EventBasedNetListener();
      _manager = new NetManager(_listener);
      _manager.UnconnectedMessagesEnabled = true;
      _manager.Start(0);
    
      //_manager.Connect("192.168.2.199", 30000, NetDataWriter.FromString(QuantumProfilingServer.CONNECT_TOKEN));

      _listener.NetworkReceiveUnconnectedEvent += OnNetworkReceiveUnconnectedEvent;
      _listener.PeerConnectedEvent += OnPeerConnected;
      _listener.PeerDisconnectedEvent += OnPeerDisconnected;
    }

    void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectinfo) {
      Log.Info($"QuantumProfilingClient: Disconnected from {peer.EndPoint}");

      _serverPeer = null;
      _broadcastNext = 0;
    }

    void OnPeerConnected(NetPeer peer) {
      Log.Info($"QuantumProfilingClient: Connected to {peer.EndPoint}");
      _serverPeer = peer;

      var writer = new NetDataWriter();
      writer.Put(QuantumProfilingClientConstants.ClientInfoMessage);
      writer.Put(JsonUtility.ToJson(_clientInfo));
      _serverPeer.Send(writer, DeliveryMethod.ReliableUnordered);
    }

    void OnNetworkReceiveUnconnectedEvent(IPEndPoint remoteendpoint, NetPacketReader reader, UnconnectedMessageType messagetype) {
      if (reader.GetString() == QuantumProfilingClientConstants.DISCOVER_RESPONSE_TOKEN) {
        _manager.Connect(remoteendpoint, NetDataWriter.FromString(QuantumProfilingClientConstants.CONNECT_TOKEN));
      }
    }

    public void SendProfilingData(ProfilerContextData data) {
      if (_serverPeer == null) {
        return;
      }

      var writer = new NetDataWriter();
      writer.Put(QuantumProfilingClientConstants.FrameMessage);
      writer.Put(JsonUtility.ToJson(data));
      _serverPeer.Send(writer, DeliveryMethod.ReliableUnordered);
    }

    public void Update() {
      if (_serverPeer == null) {
        var now = _broadcastTimer.Elapsed.TotalSeconds;
        if (now > _broadcastNext) {
          _broadcastNext = now + BROADCAST_INTERVAL;
          _manager.SendBroadcast(NetDataWriter.FromString(QuantumProfilingClientConstants.DISCOVER_TOKEN), 30000);
          Log.Info("QuantumProfilingClient: Looking For Profiling Server");
        }
      }

      _manager.PollEvents();
    }

    public void Dispose() {
      if (_manager != null) {
        _manager.Stop();
        _manager = null;
      }
    }
  }
#endif
}

#endregion


#region Assets/Photon/Quantum/Runtime/QuantumReconnectInformation.cs

namespace Quantum {
  using Photon.Realtime;
  using UnityEngine;

  /// <summary>
  /// Implements <see cref="MatchmakingReconnectInformation"/> to save reconnect information to player prefs.
  /// This way the app can try to reconnect after app start.
  /// </summary>
  public class QuantumReconnectInformation : MatchmakingReconnectInformation {
    /// <summary>
    /// Load the matchmaking information from player prefs.
    /// <para>Always returns a valid object.</para>
    /// </summary>
    public static MatchmakingReconnectInformation Load() {
      var result = JsonUtility.FromJson<QuantumReconnectInformation>(PlayerPrefs.GetString("Quantum.ReconnectInformation"));
      if (result == null) {
        result = new QuantumReconnectInformation();
      }

      return result;
    }

    /// <summary>
    /// Is a callback from matchmaking that triggers a successful connect that can then be stored.
    /// </summary>
    /// <param name="client">Realtime client that created the connection.</param>
    public override void Set(RealtimeClient client) {
      base.Set(client);

      if (client != null) {
        Save(this);
      }
    }

    /// <summary>
    /// Reset the saved reconnect information.
    /// </summary>
    public static void Reset() {
      PlayerPrefs.SetString("Quantum.ReconnectInformation", string.Empty);
    }

    /// <summary>
    /// Save the reconnect information to player prefs.
    /// </summary>
    /// <param name="value">The info to store.</param>
    public static void Save(QuantumReconnectInformation value) {
      PlayerPrefs.SetString("Quantum.ReconnectInformation", JsonUtility.ToJson(value));
    }
  }
}

#endregion


#region Assets/Photon/Quantum/Runtime/QuantumRunner.cs

namespace Quantum {
  using System.Collections.Generic;
  using System.Linq;
  using System.Threading.Tasks;
  using Photon.Deterministic;
  using Photon.Realtime;
  using UnityEngine;

  public partial class QuantumRunner : SessionRunner {
    /// <summary>
    /// Access the QuantumGame from the default runner or null.
    /// </summary>
    public static QuantumGame DefaultGame => (QuantumGame)QuantumRunnerRegistry.Global.Default?.DeterministicGame;
    /// <summary>
    /// Return the global static default runner.
    /// </summary>
    public static QuantumRunner Default => (QuantumRunner)QuantumRunnerRegistry.Global.Default;
    /// <summary>
    /// Return all active QuantumRunners.
    /// </summary>
    public static IEnumerable<QuantumRunner> ActiveRunners => QuantumRunnerRegistry.Global.ActiveRunners.Select(r => (QuantumRunner)r);

    /// <summary>
    /// Find a QuantumRunner by id.
    /// </summary>
    /// <param name="id">Runner id</param>
    /// <returns>The QuantumRunner or null</returns>
    public static QuantumRunner FindRunner(string id) {
      return (QuantumRunner)QuantumRunnerRegistry.Global.FindRunner(id);
    }

    /// <summary>
    /// Find a QuantumRunner by game.
    /// </summary>
    /// <param name="game">Game</param>
    /// <returns>The runner that holds the input game or null.</returns>
    public static QuantumRunner FindRunner(IDeterministicGame game) {
      return (QuantumRunner)QuantumRunnerRegistry.Global.FindRunner(game);
    }

    /// <summary>
    /// Shutdown all runners.
    /// </summary>
    public static void ShutdownAll() {
      QuantumRunnerRegistry.Global.ShutdownAll();
    }

    /// <summary>
    /// Shutdown all runners asynchronously.
    /// </summary>
    /// <returns>Task to shutdown all runners</returns>
    public static System.Threading.Tasks.Task ShutdownAllAsync() {
      return QuantumRunnerRegistry.Global.ShutdownAllAsync();
    }

    /// <summary>
    /// Create and start a new QuantumRunner.
    /// Will set missing arguments to default values: CallbackDispatcher, AssetSerializer, EventDispatcher, ResourceManager and RunnerFactory.
    /// </summary>
    /// <param name="arguments">Start arguments</param>
    /// <returns>New QuantumRunner</returns>
    public static QuantumRunner StartGame(Arguments arguments) {
      arguments.CallbackDispatcher ??= QuantumCallback.Dispatcher;
      arguments.AssetSerializer ??= new QuantumUnityJsonSerializer();
      arguments.EventDispatcher ??= QuantumEvent.Dispatcher;
      arguments.ResourceManager ??= QuantumUnityDB.Global;
      arguments.RunnerFactory ??= QuantumRunnerUnityFactory.DefaultFactory;
      return (QuantumRunner)Start(arguments);
    }

    /// <summary>
    /// Create a start a new QuantumRunner asynchronously.
    /// The task will return only after the connection protocol is completed.
    /// Will set missing arguments to default values: CallbackDispatcher, AssetSerializer, EventDispatcher, ResourceManager and RunnerFactory.
    /// </summary>
    /// <param name="arguments">Start arguments</param>
    /// <returns>A task that creates and starts the QuantumRunner.</returns>
    public async static Task<QuantumRunner> StartGameAsync(Arguments arguments) {
      arguments.CallbackDispatcher ??= QuantumCallback.Dispatcher;
      arguments.AssetSerializer ??= new QuantumUnityJsonSerializer();
      arguments.EventDispatcher ??= QuantumEvent.Dispatcher;
      arguments.ResourceManager ??= QuantumUnityDB.Global;  
      arguments.RunnerFactory ??= QuantumRunnerUnityFactory.DefaultFactory;
      return (QuantumRunner)await StartAsync(arguments);
    }

    /// <summary>
    ///   Disable updating the runner completely. Useful when ticking the simulation by other means.
    /// </summary>
    public bool IsSessionUpdateDisabled;
    /// <summary>
    ///   Access the QuantumGame.
    /// </summary>
    public QuantumGame Game => (QuantumGame)DeterministicGame;
    /// <summary>
    ///   Hide Gizmos toggle.
    /// </summary>
    public bool HideGizmos { get; set; }
    /// <summary>
    ///   Gizmo settings for this runner.
    /// </summary>
    public QuantumGameGizmosSettings GizmoSettings { get; set; }
    /// <summary>
    ///   Access the network client through the Communicator.
    /// </summary>
    public RealtimeClient NetworkClient {
      get {
        if (Communicator != null) {
          return ((QuantumNetworkCommunicator)Communicator).NetworkClient;
        }

        return null;
      }
    }

    /// <summary>
    ///   The reference to the Unity object that is updating this runner.
    /// </summary>
    public GameObject UnityObject { get; private set; }

    /// <summary>
    /// Is used by the QuantumRunnerUnityFactory to create a new QuantumRunner that will be owned by the UnityObject.
    /// </summary>
    /// <param name="runnerScript">Unity script</param>
    public QuantumRunner(QuantumRunnerBehaviour runnerScript) {
      UnityObject = runnerScript.gameObject;
    }

    /// <summary>
    /// The runner shutdown callback is used to destroy the UnityObject.
    /// </summary>
    /// <param name="cause">Shutdown cause</param>
    protected override void OnShutdown(ShutdownCause cause) {
      QuantumRunnerRegistry.Global.RemoveRunner(this);
      if (UnityObject != null && UnityObject.gameObject != null) {
        GameObject.Destroy(UnityObject.gameObject);
      }
    }

    /// <summary>
    /// The runner update method.
    /// </summary>
    public void Update() {
      // Don't update the session, because updating it is done from another place.
      if (IsSessionUpdateDisabled) {
        return;
      }

      // TODO: Replace with AddToPlayerLoop, PlayerLoopSystem
      switch (DeltaTimeType) {
        case SimulationUpdateTime.Default:
          Service();
          break;
        case SimulationUpdateTime.EngineDeltaTime:
          Service(Time.deltaTime);
          break;
        case SimulationUpdateTime.EngineUnscaledDeltaTime:
          Service(Time.unscaledDeltaTime);
          break;
      }
    }
  }
}

#endregion


#region Assets/Photon/Quantum/Runtime/QuantumRunnerRegistry.cs

namespace Quantum {
  using System.Collections.Generic;
  using Photon.Deterministic;

  /// <summary>
  /// A registry to keep track of all active Quantum runners.
  /// </summary>
  public class QuantumRunnerRegistry {
    /// <summary>
    /// Singleton instance of the registry. Creates a new instance if none exists.
    /// </summary>
    public static QuantumRunnerRegistry Global {
      get {
        if (_instance == null) {
          _instance = new QuantumRunnerRegistry();
        }

        return _instance;
      }
    }

    private static QuantumRunnerRegistry _instance;

    /// <summary>
    /// The default runner.
    /// <para>If multiple runners exists it will return the first one.</para>
    /// </summary>
    public SessionRunner  Default => _activeRunners.Count == 0 ? default : _activeRunners[0];
    /// <summary>
    /// Returns all runners.
    /// </summary>
    public IEnumerable<SessionRunner> ActiveRunners => _activeRunners;

    private List<SessionRunner> _activeRunners = new List<SessionRunner>();


    [UnityEngine.RuntimeInitializeOnLoadMethod]
    private static void Reset() {
      _instance = null;
    }

    /// <summary>
    /// Calls <see cref="SessionRunner.Shutdown(ShutdownCause)"/> on all runners.
    /// </summary>
    public void ShutdownAll() {
      for (int i = _activeRunners.Count - 1; i >= 0; i--) {
        _activeRunners[i].Shutdown();
      }
    }

    /// <summary>
    /// Calls <see cref="SessionRunner.WaitForShutdownAsync(System.Threading.CancellationToken)"/> on all runners."/>
    /// </summary>
    public System.Threading.Tasks.Task ShutdownAllAsync() {
      var tasks = new List<System.Threading.Tasks.Task>();
      for (int i = 0; i < _activeRunners.Count; i++) {
        tasks.Add(_activeRunners[i].ShutdownAsync());
      }

      return System.Threading.Tasks.Task.WhenAll(tasks);
    }

    /// <summary>
    /// Add a runner.
    /// </summary>
    public void AddRunner(SessionRunner runner) {
      _activeRunners.Add(runner);
    }

    /// <summary>
    /// Remove a runner.
    /// </summary>
    public void RemoveRunner(SessionRunner runner) {
      _activeRunners.Remove(runner);
    }

    /// <summary>
    /// Find a runner by <see cref="SessionRunner.Id"/>.
    /// </summary>
    /// <param name="id">Runner id to search.</param>
    /// <returns>The runner with the given id or <see langword="null"/>.</returns>
    public SessionRunner FindRunner(string id) {
      for (int i = 0; i < _activeRunners.Count; ++i) {
        if (_activeRunners[i].Id == id)
          return _activeRunners[i];
      }

      return default(SessionRunner);
    }

    /// <summary>
    /// Find a runner by <see cref="SessionRunner.DeterministicGame"/>.
    /// </summary>
    /// <param name="game">The game that the runner belongs to.</param>
    /// <returns>The runner with the given game or <see langword="null"/>.</returns>
    public SessionRunner FindRunner(IDeterministicGame game) {
      for (int i = 0; i < _activeRunners.Count; ++i) {
        if (_activeRunners[i].DeterministicGame == game)
          return _activeRunners[i];
      }

      return default(SessionRunner);
    }
  }
}

#endregion


#region Assets/Photon/Quantum/Runtime/QuantumRunnerUnityFactory.cs

namespace Quantum {
  using System;
  using System.Threading.Tasks;
  using Photon.Analyzer;
  using Photon.Deterministic;
  using Photon.Realtime;
  using Profiling;
  using UnityEngine;

  /// <summary>
  /// Implements the runner factory to Unity platform.
  /// </summary>
  public class QuantumRunnerUnityFactory : IRunnerFactory {
    /// <summary>
    /// Statically keep one default factory around.
    /// </summary>
    [StaticField(StaticFieldResetMode.None)]
    public static IRunnerFactory DefaultFactory;

    /// <summary>
    /// Create game parameters and set default values based on the platform.
    /// </summary>
    public static QuantumGameStartParameters CreateGameParameters => new QuantumGameStartParameters {
      CallbackDispatcher = QuantumCallback.Dispatcher,
      AssetSerializer = new QuantumUnityJsonSerializer(),
      EventDispatcher = QuantumEvent.Dispatcher,
      ResourceManager = QuantumUnityDB.Global,
    };

    /// <summary>
    /// Create the Unity platform information object.
    /// </summary>
    public DeterministicPlatformInfo CreatePlaformInfo => CreatePlatformInfo();
    /// <summary>
    /// Assign a task factory that will be used by the runner to create and chain new tasks.
    /// </summary>
    public TaskFactory CreateTaskFactory => AsyncConfig.Global.TaskFactory;
    /// <summary>
    /// Assign an action to update the global database.
    /// </summary>
    public Action UpdateDB => QuantumUnityDB.UpdateGlobal;
    /// <summary>
    /// Creates a unity GameObject and attaches a QuantumRunnerBehaviour to it which will then update the actual session runner object. 
    /// </summary>
    /// <param name="arguments">Session arguments</param>
    /// <returns>A session runner object</returns>
    public SessionRunner CreateRunner(SessionRunner.Arguments arguments) {
      var go = new GameObject($"QuantumRunner ({arguments.RunnerId})");
      GameObject.DontDestroyOnLoad(go);
      var script = go.AddComponent<QuantumRunnerBehaviour>();
      script.Runner = new QuantumRunner(script);
      QuantumRunnerRegistry.Global.AddRunner(script.Runner);
      return script.Runner;
    }

    [RuntimeInitializeOnLoadMethod]
    private static void InitializeOnLoad() {
      Init();
    }

    /// <summary>
    /// Create a DeterministicGame instance.
    /// </summary>
    /// <param name="startParameters">Game start parameters</param>
    /// <returns>QuantumGame instance</returns>
    public IDeterministicGame CreateGame(QuantumGameStartParameters startParameters) {
      return new QuantumGame(startParameters);
    }

    /// <summary>
    /// Create and attach a remote profiler to the game.
    /// </summary>
    /// <param name="clientId">Client id</param>
    /// <param name="deterministicConfig">Deterministic config</param>
    /// <param name="platformInfo">Platform information</param>
    /// <param name="game">Game object</param>
    public void CreateProfiler(string clientId, DeterministicSessionConfig deterministicConfig,
      DeterministicPlatformInfo platformInfo, IDeterministicGame game) {
#if QUANTUM_REMOTE_PROFILER
      if (!Application.isEditor) {
        var client = new QuantumProfilingClient(clientId, deterministicConfig, platformInfo);

        var subscription = QuantumCallback.SubscribeManual((CallbackTaskProfilerReportGenerated callback) => {
          client.SendProfilingData(callback.Report);
          client.Update();
        }, game);

        QuantumCallback.SubscribeManual((CallbackGameDestroyed callback) => {
          subscription?.Dispose();
          subscription = null;
          client?.Dispose();
          client = null;
        }, game, once: true);
      }
#endif
    }

    /// <summary>
    /// Create the Unity platform information object.
    /// </summary>
    /// <returns>Platform info object</returns>
    public static DeterministicPlatformInfo CreatePlatformInfo() {
      DeterministicPlatformInfo info;
      info = new DeterministicPlatformInfo();
      info.Allocator = new QuantumUnityNativeAllocator();
      info.TaskRunner = QuantumTaskRunnerJobs.GetInstance();

#if UNITY_EDITOR

      info.Runtime = DeterministicPlatformInfo.Runtimes.Mono;
      info.RuntimeHost = DeterministicPlatformInfo.RuntimeHosts.UnityEditor;
      info.Architecture = DeterministicPlatformInfo.Architectures.x86;
#if UNITY_EDITOR_WIN
      info.Platform = DeterministicPlatformInfo.Platforms.Windows;
#elif UNITY_EDITOR_OSX
    info.Platform = DeterministicPlatformInfo.Platforms.OSX;
#endif

#else // UNITY_EDITOR
    info.RuntimeHost = DeterministicPlatformInfo.RuntimeHosts.Unity;
#if ENABLE_IL2CPP
    info.Runtime = DeterministicPlatformInfo.Runtimes.IL2CPP;
#else
    info.Runtime = DeterministicPlatformInfo.Runtimes.Mono;
#endif // ENABLE_IL2CPP

#if UNITY_STANDALONE_WIN
    info.Platform = DeterministicPlatformInfo.Platforms.Windows;
#elif UNITY_STANDALONE_OSX
    info.Platform = DeterministicPlatformInfo.Platforms.OSX;
#elif UNITY_STANDALONE_LINUX
    info.Platform = DeterministicPlatformInfo.Platforms.Linux;
#elif UNITY_IOS
    info.Platform = DeterministicPlatformInfo.Platforms.IOS;
#elif UNITY_ANDROID
    info.Platform = DeterministicPlatformInfo.Platforms.Android;
#elif UNITY_TVOS
    info.Platform = DeterministicPlatformInfo.Platforms.TVOS;
#elif UNITY_XBOXONE
    info.Platform = DeterministicPlatformInfo.Platforms.XboxOne;
#elif UNITY_PS4
    info.Platform = DeterministicPlatformInfo.Platforms.PlayStation4;
#elif UNITY_SWITCH
    info.Platform = DeterministicPlatformInfo.Platforms.Switch;
#elif UNITY_WEBGL
    info.Platform = DeterministicPlatformInfo.Platforms.WebGL;
#endif // UNITY_STANDALONE_WIN

#endif // UNITY_EDITOR

      return info;
    }

    /// <summary>
    /// Static initializer to initialize Quantum base systems required by the factory and Quantum in general.
    /// </summary>
    /// <param name="force">Force reload the LUT</param>
    public static void Init(Boolean force = false) {
      // verify using Unity unsafe utils
      MemoryLayoutVerifier.Platform = new QuantumUnityMemoryLayoutVerifierPlatform();

      // set native platform
      Native.Utils = new QuantumUnityNativeUtility();

      // load lookup table
      FPMathUtils.LoadLookupTables(force);

      // set runner factory and init Realtime.Async
      DefaultFactory = new QuantumRunnerUnityFactory();

#if ENABLE_PROFILER
      HostProfiler.Init(new QuantumUnityHostProfiler());
#endif
      
      // init debug draw functions
#if QUANTUM_DRAW_SHAPES || UNITY_EDITOR
      Draw.Init(DebugDraw.Ray, DebugDraw.Line, DebugDraw.Circle, DebugDraw.Sphere, DebugDraw.Rectangle, DebugDraw.Box, DebugDraw.Capsule, DebugDraw.Clear);
#endif
    }
  }
}

#endregion


#region Assets/Photon/Quantum/Runtime/QuantumStallWatcher.cs

namespace Quantum {
#if QUANTUM_STALL_WATCHER_ENABLED
  using System;
  using System.Runtime.CompilerServices;
  using System.Runtime.InteropServices;
  using System.Threading;
  using UnityEngine;


  public class QuantumStallWatcher : QuantumMonoBehaviour {

    public const QuantumStallWatcherCrashType DefaultPlayerCrashType =
#if UNITY_STANDALONE_WIN
      QuantumStallWatcherCrashType.DivideByZero;
#elif UNITY_STANDALONE_OSX
      QuantumStallWatcherCrashType.DivideByZero;
#elif UNITY_ANDROID
      QuantumStallWatcherCrashType.AccessViolation;
#elif UNITY_IOS
      QuantumStallWatcherCrashType.Abort;
#else
      QuantumStallWatcherCrashType.AccessViolation;
#endif

    public float Timeout = 10.0f;

    [Tooltip("How to crash if stalling in the Editor")]
    public QuantumStallWatcherCrashType EditorCrashType = QuantumStallWatcherCrashType.DivideByZero;

    [Tooltip("How to crash if stalling in the Player. Which crash types produce crash dump is platform-specific.")]
    public QuantumStallWatcherCrashType PlayerCrashType = DefaultPlayerCrashType;

    public new bool DontDestroyOnLoad = false;


    [Space]
    [InspectorButton("Editor_RestoreDefaultCrashType", "Reset Crash Type To The Target Platform's Default")]
    public bool Button_StartInstantReplay;

    private Worker _worker;
    private bool _started;

    private void Awake() {
      if (DontDestroyOnLoad) {
        DontDestroyOnLoad(gameObject);
      }
    }

    private void Start() {
      _started = true;
      OnEnable();
    }

    private void Update() {
      _worker.NotifyUpdate();
    }

    private void OnEnable() {
      if (!_started) {
        return;
      }
      _worker = new Worker(checked((int)(Timeout * 1000)), Application.isEditor ? EditorCrashType : PlayerCrashType);
    }

    private void OnDisable() {
      _worker.Dispose();
      _worker = null;
    }

    private static class Native {
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
      const string LibCName = "msvcrt.dll";
#else
      const string LibCName = "libc";
#endif

      [StructLayout(LayoutKind.Sequential)]
      public struct div_t {
        public int quot;
        public int rem;
      }

      [DllImport(LibCName, EntryPoint = "abort", CallingConvention = CallingConvention.Cdecl, SetLastError = false)]
      public static extern void LibCAbort();
      [DllImport(LibCName, EntryPoint = "memcpy", CallingConvention = CallingConvention.Cdecl, SetLastError = false)]
      public static extern IntPtr LibCMemcpy(IntPtr dest, IntPtr src, UIntPtr count);
      [DllImport(LibCName, EntryPoint = "div", CallingConvention = CallingConvention.Cdecl, SetLastError = false)]
      public static extern div_t LibCDiv(int numerator, int denominator);
    }


    private sealed class Worker : IDisposable {

      private Thread thread;
      private AutoResetEvent updateStarted = new AutoResetEvent(false);
      private AutoResetEvent shutdown = new AutoResetEvent(false);

      public Worker(int timeoutMills, QuantumStallWatcherCrashType crashType) {

        thread = new Thread(() => {

          var startedHandles = new WaitHandle[] { shutdown, updateStarted };

          for (; ; ) {
            // wait for the update to finish
            int index = WaitHandle.WaitAny(startedHandles, timeoutMills);
            if (index == 0) {
              // shutdown
              break;
            } else if (index == 1) {
              // ok
            } else {
              int crashResult = Crash(crashType);
              Debug.LogError($"Crash failed with result: {crashResult}");
              // a crash should have happened by now
              break;
            }

          }
        }) {
          Name = "QuantumStallWatcherWorker"
        };
        thread.Start();
      }

      public void NotifyUpdate() {
        updateStarted.Set();
      }

      public void Dispose() {
        shutdown.Set();
        if (thread.Join(1000) == false) {
          Debug.LogError($"Failed to join the {thread.Name}");
        }
      }

      [MethodImpl(MethodImplOptions.NoOptimization)]
      public int Crash(QuantumStallWatcherCrashType type, int zero = 0) {
        Debug.LogWarning($"Going to crash... mode: {type}");

        int result = -1;

        if (type == QuantumStallWatcherCrashType.Abort) {
          Native.LibCAbort();
          result = 0;
        } else if (type == QuantumStallWatcherCrashType.AccessViolation) {
          unsafe {
            int* data = stackalloc int[1];
            data[0] = 5;
            Native.LibCMemcpy(new IntPtr(zero), new IntPtr(data), new UIntPtr(sizeof(int)));
            result = 1;
          }
        } else if (type == QuantumStallWatcherCrashType.DivideByZero) {
          result = Native.LibCDiv(5, zero).quot;
        }

        return result;
      }

    }

    public void Editor_RestoreDefaultCrashType() {
      PlayerCrashType = DefaultPlayerCrashType;
    }
  }

  public enum QuantumStallWatcherCrashType {
    AccessViolation,
    Abort,
    DivideByZero
  }
#endif
}

#endregion


#region Assets/Photon/Quantum/Runtime/QuantumStaticColliderSettings.cs

namespace Quantum {
  using System;

  [Serializable]
  public class QuantumStaticColliderSettings {
    public PhysicsCommon.StaticColliderMutableMode MutableMode;
    public Quantum.AssetRef<Quantum.PhysicsMaterial> PhysicsMaterial;
    public AssetRef Asset;

    [DrawIf("^SourceCollider", 0, ErrorOnConditionMemberNotFound = false)]
    public Boolean Trigger;
  }
}

#endregion


#region Assets/Photon/Quantum/Runtime/QuantumUnityHostProfiler.cs

#if ENABLE_PROFILER
namespace Quantum {
  using Profiling;
  using Unity.Profiling;
  using Unity.Profiling.LowLevel;
  using Unity.Profiling.LowLevel.Unsafe;

  /// <summary>
  /// Profiler implementation for Unity.
  /// </summary>
  public class QuantumUnityHostProfiler : IHostProfiler {
    /// <inheritdoc cref="IHostProfiler.CreateMarker"/>
    public HostProfilerMarker CreateMarker(string name) {
      var ptr = ProfilerUnsafeUtility.CreateMarker(name, ProfilerCategory.Scripts, MarkerFlags.Default, 0);
      return new HostProfilerMarker(ptr);
    }

    /// <inheritdoc cref="IHostProfiler.StartMarker"/>
    public void StartMarker(HostProfilerMarker marker) {
      ProfilerUnsafeUtility.BeginSample(marker.RawValue);
    }

    /// <inheritdoc cref="IHostProfiler.EndMarker"/>
    public void EndMarker(HostProfilerMarker marker) {
      ProfilerUnsafeUtility.EndSample(marker.RawValue);
    }

    /// <inheritdoc cref="IHostProfiler.StartNamedMarker"/>
    public void StartNamedMarker(string markerName) {
      UnityEngine.Profiling.Profiler.BeginSample(markerName);
    }

    /// <inheritdoc cref="IHostProfiler.EndLastNamedMarker"/>
    public void EndLastNamedMarker() {
      UnityEngine.Profiling.Profiler.EndSample();
    }
  }
}
#endif

#endregion


#region Assets/Photon/Quantum/Runtime/QuantumUnityJsonSerializer.cs

namespace Quantum {
  using System;
  using System.Collections;
  using System.Collections.Generic;
  using System.IO;
  using System.Text;
  using Photon.Deterministic;
  using UnityEngine;
  using UnityEngine.Serialization;

  /// <summary>
  /// Asset serializer implementation based on <see cref="UnityEngine.JsonUtility"/> and a set of utility methods from
  /// <see cref="JsonUtilityExtensions"/>. Can use surrogates to replace asset types with more efficient
  /// representations (<see cref="RegisterSurrogate{AssetType,SurrogateType}"/>. Additionally, any Unity-object references
  /// are serialized as null by default (<see cref="NullifyUnityObjectReferences"/>).
  /// <p/>
  /// The output can be deserialized with Newtonsoft.Json-based deserializer from Quantum.Json assembly. If this interoperability is
  /// needed, consider enabling <see cref="IntegerEnquotingMinDigits"/> to ensure that large integers are enquoted and not treated as
  /// floating points.
  /// </summary>
  public class QuantumUnityJsonSerializer : IAssetSerializer {
    private Dictionary<Type, (Type SurrogateType, Delegate Factory)> _surrogateFactories = new();
    private Dictionary<Type, Type> _surrogateToAssetType = new();

    /// <summary>
    /// No longer used.
    /// </summary>
    [Obsolete("No longer used")]
    public bool PrettyPrintEnabled {
      get => false;
      set { }
    }
    
    /// <summary>
    /// No longer used.
    /// </summary>
    [Obsolete("No longer used")]
    public bool EntityViewPrefabResolvingEnabled { get => false; set {} }
    
    /// <summary>
    /// If true, all BinaryData assets will be decompressed during deserialization.
    /// </summary>
    public bool DecompressBinaryDataOnDeserialization { get; set; } = false;

    /// <summary>
    /// If set to a positive value, all uncompressed BinaryData assets with size over the value will be compressed
    /// during serialization.
    /// </summary>
    public int? CompressBinaryDataOnSerializationThreshold { get; set; } = 1024;
    
    
    /// <summary>
    /// How many digits should a number have to be enquoted.
    /// Some JSON parsers deserialize all numbers as floating points,  which in case of large integers (e.g. entity ids) can lead to precision loss.
    /// If this property is set to true (default), all integers with <see cref="IntegerEnquotingMinDigits"/> or more digits
    /// are enquoted.
    /// </summary>
    public int? IntegerEnquotingMinDigits { get; set; }
    
    /// <summary>
    /// Should all UnityEngine.Object references be nullified in the resulting JSON?
    /// If true, all UnityEngine.Object references will be serialized as null. Otherwise,
    /// they are serialized as { "instanceId": &lt;value&gt; }.
    ///
    /// True by default.
    /// </summary>
    public bool NullifyUnityObjectReferences { get; set; } = true;
    
    /// <summary>
    /// Custom resolver for EntityView prefabs.
    ///
    /// EntityViews are serialized without prefab references (as they are not JSON serializable). Resolving
    /// takes place during deserialization, by looking up the prefab in the global DB.
    /// </summary>
    public Func<AssetGuid, GameObject> EntityViewPrefabResolver { get; set; }
    
    /// <summary>
    /// Custom type resolver to be used during deserialization.
    ///
    /// If not set, <see cref="Type.GetType(string, bool)"/> will be used and an exception will be thrown on missing type.
    /// If set and returns null, the type object will be skipped and returned as null.
    /// </summary>
    public Func<string, Type, Type> TypeResolver { get; set; } 
    
    /// <summary>
    /// Creates a new instance of <see cref="QuantumUnityJsonSerializer"/>.
    /// </summary>
    public QuantumUnityJsonSerializer() {
      RegisterSurrogate((EntityView asset) => new EntityViewSurrogate() {
        Identifier = asset.Identifier,
      });
      RegisterSurrogate((BinaryData asset) => BinaryDataSurrogate.Create(asset, CompressBinaryDataOnSerializationThreshold));
    }
    
    /// <summary>
    /// Registers a surrogate type for the provided asset type. Surrogates are types that are serialized and deserialized instead of
    /// the original asset type. By default, the serializer only provides surrogates for <see cref="EntityView"/> and
    /// <see cref="BinaryData"/>, for a more efficient serialization.
    /// </summary>
    /// <param name="factory">Delegate to be used when an instance of a surrogate is needed.</param>
    public void RegisterSurrogate<AssetType, SurrogateType>(Func<AssetType, SurrogateType> factory) 
      where AssetType : AssetObject
      where SurrogateType : AssetObjectSurrogate {
      Assert.Check(factory != null);
      _surrogateFactories.Add(typeof(AssetType), (typeof(SurrogateType), factory));
      _surrogateToAssetType.Add(typeof(SurrogateType), typeof(AssetType));
    }
    
    /// <summary>
    /// Resolves the prefab associated with the provided AssetGuid by looking it up in the global DB.
    /// </summary>
    /// <param name="guid">The AssetGuid of the prefab to be resolved.</param>
    /// <returns>Returns the GameObject associated with the provided AssetGuid.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the prefab associated with the provided AssetGuid cannot be found.</exception>
    protected virtual GameObject ResolvePrefab(AssetGuid guid) {
      if (EntityViewPrefabResolver != null) {
        return EntityViewPrefabResolver(guid);
      }
      
      var globalEntityView = QuantumUnityDB.GetGlobalAsset(guid) as EntityView;
      if (globalEntityView == null) {
        throw new InvalidOperationException($"Unable to resolve prefab for guid {guid}");
      }
    
      return globalEntityView.Prefab;
    }
    
    /// <summary>
    /// Creates a new instance of <see cref="StreamReader"/> with UTF8 encoding. The underlying stream is not closed.
    /// </summary>
    /// <param name="stream"></param>
    /// <returns></returns>
    protected virtual TextReader CreateReader(Stream stream) => new StreamReader(stream, Encoding.UTF8, true, 1024, true);
    /// <summary>
    /// Creates a new instance of <see cref="StreamWriter"/> with UTF8 encoding. The underlying stream is not closed.
    /// </summary>
    /// <param name="stream"></param>
    /// <returns></returns>
    protected virtual TextWriter CreateWriter(Stream stream) => new StreamWriter(stream, new UTF8Encoding(false), 1024, true);
    
    /// <inheritdoc cref="IAssetSerializer.SerializeConfig"/>
    public void SerializeConfig(Stream stream, IRuntimeConfig config) {
      using (var writer = CreateWriter(stream)) {
        ToJson(config, writer);
      }
    }

    /// <inheritdoc cref="IAssetSerializer.SerializePlayer"/>
    public void SerializePlayer(Stream stream, IRuntimePlayer player) {
      using (var writer = CreateWriter(stream)) {
        ToJson(player, writer);
      }
    }

    /// <inheritdoc cref="IAssetSerializer.SerializeResult"/>
    public void SerializeResult(Stream stream, IGameResult config) {
      using (var writer = CreateWriter(stream)) {
        ToJson(config, writer);
      }
    }

    /// <inheritdoc cref="IAssetSerializer.SerializeAssets"/>
    public void SerializeAssets(Stream stream, AssetObject[] assets) {
      
      List<object> list = new List<object>(assets.Length);
      for (int i = 0; i < assets.Length; i++) {
        var asset = assets[i];

        if (_surrogateFactories.TryGetValue(asset.GetType(), out var entry)) {
          var surrogate = (AssetObjectSurrogate)entry.Factory.DynamicInvoke(asset);
          Assert.Check(surrogate != null);
          list.Add(surrogate);
        } else {
          list.Add(asset);
        }
      }

      using (var writer = CreateWriter(stream)) {
        ToJson(list, writer);
      }
    }
    
    /// <inheritdoc cref="IAssetSerializer.DeserializeConfig"/>
    public IRuntimeConfig DeserializeConfig(Stream stream) {
      using (var reader = CreateReader(stream)) {
        return (IRuntimeConfig)FromJson(reader, typeof(IRuntimeConfig));
      }
    }

    /// <inheritdoc cref="IAssetSerializer.DeserializePlayer"/>
    public IRuntimePlayer DeserializePlayer(Stream stream) {
      using (var reader = CreateReader(stream)) {
        return (IRuntimePlayer)FromJson(reader, typeof(IRuntimePlayer));
      }
    }

    /// <inheritdoc cref="IAssetSerializer.DeserializeResult"/>
    public IGameResult DeserializeResult(Stream stream) {
      using (var reader = CreateReader(stream)) {
        return (IGameResult)FromJson(reader, typeof(IGameResult));
      }
    }

    /// <inheritdoc cref="IAssetSerializer.DeserializeAssets"/>
    public AssetObject[] DeserializeAssets(Stream stream) {
      using (var reader = CreateReader(stream)) {
        var list = (IList)FromJson(reader, typeof(AssetObject));

        var result = new AssetObject[list.Count];
        for (int i = 0; i < list.Count; i++) {
          if (list[i] is AssetObjectSurrogate surrogate) {
            result[i] = surrogate.CreateAsset(this);
          } else {
            result[i] = (AssetObject)list[i];
          }
        }

        return result;
      }
    }

    /// <inheritdoc cref="IAssetSerializer.PrintObject"/>
    public string PrintObject(object obj) {
      return JsonUtility.ToJson(obj, true);
    }

    private void ToJson(object obj, TextWriter writer) {
      JsonUtilityExtensions.ToJsonWithTypeAnnotation(obj, writer, 
        integerEnquoteMinDigits: IntegerEnquotingMinDigits,
        typeSerializer: t => {
          if (_surrogateToAssetType.TryGetValue(t, out var assetType)) {
            return SerializableType.GetShortAssemblyQualifiedName(assetType);
          }
          return null;
        }, 
        instanceIDHandler: !NullifyUnityObjectReferences ? null : (_, id) => {
          return "null";
        });
    }
    
    private object FromJson(TextReader reader, Type expectedType) {
      var json = reader.ReadToEnd();
      return JsonUtilityExtensions.FromJsonWithTypeAnnotation(json, typeResolver: t => {

        Type type;
        if (TypeResolver != null) {
          type = TypeResolver(t, expectedType);
        } else {
          if (string.IsNullOrEmpty(t)) {
            // fallback for known types
            if (expectedType == typeof(IRuntimeConfig)) {
              type = typeof(RuntimeConfig);
            } else if (expectedType == typeof(IRuntimePlayer)) {
              type = typeof(RuntimePlayer);
            } else {
              throw new InvalidOperationException("Type name is empty. Does the JSON contain a $type property?");
            }
          } else {
            type = Type.GetType(t, throwOnError: true);
          }
        }
        
        // make sure surrogate type is created instead of the asset type, if needed
        if (_surrogateFactories.TryGetValue(type, out var value)) {
          return value.SurrogateType;
        }
        return type;
      });
    }
    
    /// <summary>
    /// Base class for asset object surrogates.
    /// </summary>
    [Serializable]
    public abstract class AssetObjectSurrogate {
      /// <summary>
      /// Asset identifier.
      /// </summary>
      public AssetObjectIdentifier Identifier;
      /// <summary>
      /// Creates an asset object from the surrogate.
      /// </summary>
      /// <param name="serializer"></param>
      /// <returns></returns>
      public abstract AssetObject CreateAsset(QuantumUnityJsonSerializer serializer);
    }
    
    /// <summary>
    /// <see cref="Quantum.EntityView"/> surrogate. Does not serialize the prefab reference, but only the identifier.
    /// <see cref="QuantumUnityJsonSerializer.ResolvePrefab"/> is used to resolve the prefab during deserialization.
    /// </summary>
    [Serializable]
    protected class EntityViewSurrogate : AssetObjectSurrogate {
      /// <inheritdoc cref="AssetObjectSurrogate.CreateAsset"/>
      public override AssetObject CreateAsset(QuantumUnityJsonSerializer serializer) {
        var result = AssetObject.Create<EntityView>();
        result.Identifier = Identifier;
        result.Prefab = serializer.ResolvePrefab(Identifier.Guid);
        return result;
      }
    }

    /// <summary>
    /// <see cref="Quantum.BinaryData"/> surrogate. Compresses the data if it is larger than the threshold and replaces the data with a base64 encoded string.
    /// </summary>
    [Serializable]
    protected class BinaryDataSurrogate : AssetObjectSurrogate {
      /// <summary>
      /// Is the data compressed.
      /// </summary>
      public bool IsCompressed;
      /// <summary>
      /// Binary data as a base64 encoded string.
      /// </summary>
      [FormerlySerializedAs("Base64Data")] public string Data;

      /// <summary>
      /// Creates a surrogate from the asset. Optionally compresses the data if it is larger than the threshold.
      /// </summary>
      /// <param name="asset"></param>
      /// <param name="compressThreshold"></param>
      /// <returns></returns>
      public static BinaryDataSurrogate Create(BinaryData asset, int? compressThreshold) {
        byte[] data = asset.Data ?? Array.Empty<byte>();
        bool isCompressed = asset.IsCompressed;
        if (!asset.IsCompressed && compressThreshold.HasValue && data.Length >= compressThreshold.Value) {
          data = ByteUtils.GZipCompressBytes(data);
          isCompressed = true;
        }

        return new BinaryDataSurrogate() {
          Identifier = asset.Identifier,
          Data = ByteUtils.Base64Encode(data),
          IsCompressed = isCompressed,
        };
      }
      
      /// <inheritdoc cref="AssetObjectSurrogate.CreateAsset"/>
      public override AssetObject CreateAsset(QuantumUnityJsonSerializer serializer) {
        var result = AssetObject.Create<BinaryData>();
        result.Identifier = Identifier;
        result.Data = ByteUtils.Base64Decode(Data);
        result.IsCompressed = IsCompressed;
        if (IsCompressed && serializer.DecompressBinaryDataOnDeserialization) {
          result.IsCompressed = false;
          result.Data = ByteUtils.GZipDecompressBytes(result.Data);
        }
        return result;
      }
    }
  }
}

#endregion


#region Assets/Photon/Quantum/Runtime/QuantumUnityLegacy.cs

#if QUANTUM_ENABLE_MIGRATION

[System.Obsolete("Renamed to Quantum.QuantumMapData")]
public abstract class MapData : Quantum.QuantumMapData { }

[System.Obsolete("Renamed to Quantum.QuantumMapDataBaker")]
public abstract class MapDataBaker : Quantum.QuantumMapDataBaker { }

[System.Obsolete("Renamed to Quantum.QuantumNavMeshRegion")]
public abstract class MapNavMeshRegion : Quantum.QuantumNavMeshRegion { }

[System.Obsolete("Navmesh editor gizmos are now rendered with the map")]
public abstract class MapNavMeshDebugDrawer : Quantum.QuantumNavMeshDebugDrawer { }

[System.Obsolete("Renamed to Quantum.QuantumMapNavMeshUnity")]
public abstract class MapNavMeshUnity : Quantum.QuantumMapNavMeshUnity { }

#endif

#endregion


#region Assets/Photon/Quantum/Runtime/QuantumUnityMemoryLayoutVerifierPlatform.cs

namespace Quantum {
  using System;
  using System.Reflection;
  using Unity.Collections.LowLevel.Unsafe;

  public class QuantumUnityMemoryLayoutVerifierPlatform : MemoryLayoutVerifier.IPlatform {
    public int FieldOffset(FieldInfo field) {
      return UnsafeUtility.GetFieldOffset(field);
    }

    public int SizeOf(Type type) {
      return UnsafeUtility.SizeOf(type);
    }

    public bool CanResolveEnumSize {
      get { return true; }
    }
  }
}

#endregion


#region Assets/Photon/Quantum/Runtime/QuantumUnityNativeImplementation.cs

namespace Quantum {
  using Photon.Analyzer;
  using Photon.Deterministic;
  using Unity.Collections.LowLevel.Unsafe;
  using UnityAllocator = global::Unity.Collections.Allocator;

#if ENABLE_IL2CPP
  using AOT;
  using System;
  using System.Collections.Generic;
#endif

#if ENABLE_IL2CPP
  /// <summary>
  /// Collection of native memory allocations used by Quantum when creating <see cref="QuantumUnityNativeAllocator.GetManagedVTable"/> under IL2CPP.
  /// </summary>
  internal sealed unsafe class QuantumUnityNativeAllocator_IL2CPP {
    static readonly HashSet<IntPtr> _allocated = new();
    static void TrackAlloc(IntPtr ptr) {
#if DEBUG
      lock (_allocated) {
        _allocated.Add(ptr);
      }
#endif
    }
    static void TrackFree(IntPtr ptr) {
#if DEBUG
      lock (_allocated) {
        if (_allocated.Remove(ptr) == false) {
          throw new Exception($"Tried to free {ptr} which was not allocated");
        }
      }
#endif
    }
    /// <inheritdoc cref="QuantumUnityNativeAllocator.Alloc(int)"/>
    [MonoPInvokeCallback(typeof(Native.AllocateDelegate))]
    public static IntPtr Allocate(UIntPtr size) {
      var ptr = (IntPtr)UnsafeUtility.Malloc((uint)size, 4, UnityAllocator.Persistent);
      TrackAlloc(ptr);
      return ptr;
    }
    /// <inheritdoc cref="QuantumUnityNativeAllocator.Free(void*)"/>
    [MonoPInvokeCallback(typeof(Native.FreeDelegate))]
    public static void Free(IntPtr ptr) {
      TrackFree(ptr);
      UnsafeUtility.Free((void*)ptr, UnityAllocator.Persistent);
    }
    /// <inheritdoc cref="QuantumUnityNativeUtility.Copy(void*, void*, int)"/>
    [MonoPInvokeCallback(typeof(Native.CopyDelegate))]
    public static void Copy(IntPtr dst, IntPtr src, UIntPtr size) {
      UnsafeUtility.MemCpy((void*)dst, (void*)src, (int)size);
    }
    /// <inheritdoc cref="QuantumUnityNativeUtility.Move(void*, void*, int)"/>
    [MonoPInvokeCallback(typeof(Native.MoveDelegate))]
    public static void Move(IntPtr dst, IntPtr src, UIntPtr size) {
      UnsafeUtility.MemMove((void*)dst, (void*)src, (int)size);
    }
    /// <inheritdoc cref="QuantumUnityNativeUtility.Set(void*, byte, int)"/>
    [MonoPInvokeCallback(typeof(Native.SetDelegate))]
    public static void Set(IntPtr ptr, byte value, UIntPtr size) {
      UnsafeUtility.MemSet((void*)ptr, value, (int)size);
    }
    /// <inheritdoc cref="QuantumUnityNativeUtility.Compare(void*, void*, int)"/>
    [MonoPInvokeCallback(typeof(Native.CompareDelegate))]
    public static int Compare(IntPtr ptr1, IntPtr ptr2, UIntPtr size) {
      return UnsafeUtility.MemCmp((void*)ptr1, (void*)ptr2, (int)size);
    }
  }
#endif

  /// <summary>
  /// The Unity implementation of the Quantum native memory allocator.
  /// </summary>
  public sealed unsafe class QuantumUnityNativeAllocator : Native.Allocator {
    /// <inheritdoc />
    public sealed override void* Alloc(int count) {
      var ptr = UnsafeUtility.Malloc((uint)count, 4, UnityAllocator.Persistent);
      TrackAlloc(ptr);
      return ptr;
    }

    /// <inheritdoc />
    public sealed override void* Alloc(int count, int alignment) {
      var ptr = UnsafeUtility.Malloc((uint)count, alignment, UnityAllocator.Persistent);
      TrackAlloc(ptr);
      return ptr;
    }

    /// <inheritdoc />
    public sealed override void Free(void* ptr) {
      TrackFree(ptr);
      UnsafeUtility.Free(ptr, UnityAllocator.Persistent);
    }

    /// <inheritdoc />
    protected sealed override void Clear(void* dest, int count) {
      UnsafeUtility.MemClear(dest, (uint)count);
    }

    /// <inheritdoc />
    public sealed override Native.AllocatorVTableManaged GetManagedVTable() {
#if ENABLE_IL2CPP
      // IL2CPP does not support marshaling delegates that point to instance methods to native code.
      return new Native.AllocatorVTableManaged(
        new Native.AllocateDelegate(QuantumUnityNativeAllocator_IL2CPP.Allocate),
        new Native.FreeDelegate(QuantumUnityNativeAllocator_IL2CPP.Free),
        new Native.CopyDelegate(QuantumUnityNativeAllocator_IL2CPP.Copy),
        new Native.MoveDelegate(QuantumUnityNativeAllocator_IL2CPP.Move),
        new Native.SetDelegate(QuantumUnityNativeAllocator_IL2CPP.Set),
        new Native.CompareDelegate(QuantumUnityNativeAllocator_IL2CPP.Compare)
      );
#else
      return new Native.AllocatorVTableManaged(this, Native.Utils);
#endif
    }
  }

  /// <summary>
  /// The Unity implementation of the Quantum native utility functions.
  /// </summary>
  public unsafe class QuantumUnityNativeUtility : Native.Utility {

    /// <inheritdoc />
    public override void Clear(void* dest, int count) {
      UnsafeUtility.MemClear(dest, (long)count);
    }

    /// <inheritdoc />
    public override void Copy(void* dest, void* src, int count) {
      UnsafeUtility.MemCpy(dest, src, (long)count);
    }

    /// <inheritdoc />
    public override void Move(void* dest, void* src, int count) {
      UnsafeUtility.MemMove(dest, src, (long)count);
    }

    /// <inheritdoc />
    public override void Set(void* dest, byte value, int count) {
      UnsafeUtility.MemSet(dest, value, count);
    }

    /// <inheritdoc />
    public override unsafe int Compare(void* ptr1, void* ptr2, int count) {
      return UnsafeUtility.MemCmp(ptr1, ptr2, count);
    }

    /// <summary>
    /// Reset statics. Currently does nothing.
    /// </summary>
    [StaticFieldResetMethod]
    public static void ResetStatics() {
    }
  }
}

#endregion


#region Assets/Photon/Quantum/Runtime/QuantumUnityTypes.Common.cs

// merged UnityTypes

#region QuantumGlobalScriptableObject.cs

namespace Quantum {
  using System;
  using System.Collections.Generic;
  using System.Linq;
  using System.Reflection;
  using UnityEngine;
  using static InternalLogStreams;
  
  /// <summary>
  /// A base class for ScriptableObjects that are meant to be globally accessible, at edit-time and runtime. The way such objects
  /// are loaded is driven by usages of <see cref="QuantumGlobalScriptableObjectSourceAttribute"/> attributes. 
  /// </summary>
  public abstract class QuantumGlobalScriptableObject : QuantumScriptableObject {
    private static IEnumerable<T> GetAssemblyAttributes<T>() where T : Attribute {
      foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies()) {
        foreach (var attr in assembly.GetCustomAttributes<T>()) {
          yield return attr;
        }
      }
    }
    
    internal static QuantumGlobalScriptableObjectSourceAttribute[] SourceAttributes => s_sourceAttributes.Value;

    private static readonly Lazy<QuantumGlobalScriptableObjectSourceAttribute[]> s_sourceAttributes = new Lazy<QuantumGlobalScriptableObjectSourceAttribute[]>(() => {
      return GetAssemblyAttributes<QuantumGlobalScriptableObjectSourceAttribute>().OrderBy(x => x.Order).ToArray();
    });
  }
  
  /// <inheritdoc cref="QuantumGlobalScriptableObject{T}"/>
  public abstract partial class QuantumGlobalScriptableObject<T> : QuantumGlobalScriptableObject where T : QuantumGlobalScriptableObject<T> {
    private static T                                                s_instance;
    private static QuantumGlobalScriptableObjectUnloadDelegate s_unloadHandler;
    
    /// <summary>
    /// Is this instance a global instance.
    /// </summary>
    public bool IsGlobal { get; private set; }
    
    /// <summary>
    /// Invoked when the instance is loaded as global.
    /// </summary>
    protected virtual void OnLoadedAsGlobal() {
    }

    /// <summary>
    /// Invoked when the instance is unloaded as global.
    /// </summary>
    /// <param name="destroyed"></param>
    protected virtual void OnUnloadedAsGlobal(bool destroyed) {
    }
    
    private static string LogPrefix => $"[Global {typeof(T).Name}]: ";
    private static string AsId(QuantumGlobalScriptableObject<T> obj) => obj ? $"[IID:{obj.GetInstanceID()}]" : "null";
    
    /// <summary>
    /// If the current instance is global, unsets <see cref="IsGlobal"/> and calls <see cref="OnUnloadedAsGlobal"/>
    /// </summary>
    protected virtual void OnDisable() {
      // OnDestroy is weird in ScriptableObjects; it can realistically only happen for fully runtime/scene bound ones. Addressables
      // seem to omit even OnDisable.
      
      if (!IsGlobal) {
        LogTrace?.Log($"{LogPrefix}OnDisable called for {AsId(this)}, but is not global");
        return;
      }

      if (s_unloadHandler != null) {
        LogTrace?.Log($"{LogPrefix}OnDisable called for {AsId(this)}, setting global instance to null. The unload handler is still set, not going to be used.");
      } else {
        LogTrace?.Log($"{LogPrefix}OnDisable called for {AsId(this)}, setting global instance to null.");
      }

      Assert.Check(object.ReferenceEquals(this, s_instance), $"Expected this to be the global instance");
      s_instance = null;
      s_unloadHandler = null;
      
      IsGlobal = false;
      OnUnloadedAsGlobal(true);
    }

    /// <summary>
    /// A singleton instance-like property. Loads or returns the current global instance. Derived classes can package it in a property
    /// with a different name. Throws if loading an instance failed.
    /// </summary>
    /// <exception cref="InvalidOperationException"></exception>
    protected static T GlobalInternal {
      get {
        var instance = GetOrLoadGlobalInstance();
        if (ReferenceEquals(instance, null)) {
          throw new InvalidOperationException($"Failed to load {typeof(T).Name}. If this happens in edit mode, make sure Quantum is properly installed in the Quantum HUB. " +
            $"Otherwise, if the default path does not exist or does not point to a Resource, you need to use " +
            $"{nameof(QuantumGlobalScriptableObjectAttribute)} attribute to point to a method that will perform the loading.");
        }

        return instance;
      }
      set {
        if (value == s_instance) {
          return;
        }

        SetGlobalInternal(value, null);
      }
    }
    
    /// <summary>
    /// Returns true if a global instance is loaded. Compared to <see cref="GlobalInternal"/>, it does not attempt to load an instance.
    /// </summary>
    protected static bool IsGlobalLoadedInternal {
      get => s_instance != null;
    }

    /// <summary>
    /// Loads or returns the current global instance. Returns <see langword="null"/> if loading an instance failed.
    /// </summary>
    /// <param name="global"></param>
    /// <returns></returns>
    protected static bool TryGetGlobalInternal(out T global) {
      var instance = GetOrLoadGlobalInstance();
      if (ReferenceEquals(instance, null)) {
        global = null;
        return false;
      }

      global = instance;
      return true;
    }

    /// <summary>
    /// Unloads the global instance if it is loaded.
    /// </summary>
    /// <returns><see langword="true"/> if an instance was unloaded</returns>
    protected static bool UnloadGlobalInternal() {
      
      var instance = s_instance;
      if (!instance) {
        return false;
      }

      Assert.Check(instance.IsGlobal);

      try {
        if (s_unloadHandler != null) {
          LogTrace?.Log($"{LogPrefix} Unloading global instance {AsId(instance)} with unloader");
          var unloader = s_unloadHandler;
          s_unloadHandler = null;
          unloader.Invoke(instance);
        } else {
          LogTrace?.Log($"{LogPrefix} Instance {AsId(instance)} has no unloader, simply nulling it out");
        }
      } finally {
        s_instance = null;

        if (instance.IsGlobal) {
          instance.IsGlobal = false;
          instance.OnUnloadedAsGlobal(false);
        }
      }

      return true;
    }

    private static T GetOrLoadGlobalInstance() {
      if (s_instance) {
        return s_instance;
      }
      
      T instance = null;
      QuantumGlobalScriptableObjectUnloadDelegate unloadHandler = null;
      
      instance = LoadPlayerInstance(out unloadHandler);

      if (instance) {
        SetGlobalInternal(instance, unloadHandler);
      }
      
      return instance;
    }
    
    private static T LoadPlayerInstance(out QuantumGlobalScriptableObjectUnloadDelegate unloadHandler) {
      
      foreach (var sourceAttribute in SourceAttributes) {
        if (Application.isEditor) {
          if (!Application.isPlaying && !sourceAttribute.AllowEditMode) {
            continue;
          }
        }

        if (sourceAttribute.ObjectType != typeof(T) && !typeof(T).IsSubclassOf(sourceAttribute.ObjectType)) {
          continue;
        }
        
        var result = sourceAttribute.Load(typeof(T));
        if (result.Object) {
          var instance = (T)result.Object;
          unloadHandler = result.Unloader;
          LogTrace?.Log($"{LogPrefix} Loader {sourceAttribute} was used to load {AsId(instance)}, has unloader: {unloadHandler != null}");
          return instance;
        }

        if (!sourceAttribute.AllowFallback) {
          // no fallback allowed
          break;
        }
      }

      LogTrace?.Log($"{LogPrefix} No source attribute was able to load the global instance");
      unloadHandler = default;
      return default;
    }
    
    private static void SetGlobalInternal(T value, QuantumGlobalScriptableObjectUnloadDelegate unloadHandler) {
      if (s_instance) {
        throw new InvalidOperationException($"Failed to set {typeof(T).Name} as global. A global instance is already loaded - it needs to be unloaded first");
      }

      Assert.Check(value, "Expected value to be non-null");

      if (object.ReferenceEquals(s_instance, null)) {
        Assert.Check(s_unloadHandler == null, "Expected unload handler to be null");
      }

      if (value) {
        s_instance = value;
        s_unloadHandler = unloadHandler;
        
        s_instance.IsGlobal = true;
        s_instance.OnLoadedAsGlobal();
      }
    }
  }
}

#endregion


#region QuantumGlobalScriptableObjectAttribute.cs

namespace Quantum {
  using System;

  /// <summary>
  /// Provides additional information for a global scriptable object.
  /// </summary>
  [AttributeUsage(AttributeTargets.Class)]
  public class QuantumGlobalScriptableObjectAttribute : Attribute {
    /// <summary>
    /// Creates a new instance.
    /// </summary>
    /// <param name="defaultPath">The default path for the asset.</param>
    public QuantumGlobalScriptableObjectAttribute(string defaultPath) {
      DefaultPath = defaultPath;
    }
    
    /// <summary>
    /// The default path for the asset.
    /// </summary>
    public string DefaultPath { get; }
    /// <summary>
    /// The default contents for the asset, if it is a TextAsset.
    /// </summary>
    public string DefaultContents { get; set; }
    /// <summary>
    /// Name of the method that is used to generate the default contents for the asset.
    /// </summary>
    public string DefaultContentsGeneratorMethod { get; set; }
  }
}

#endregion


#region QuantumGlobalScriptableObjectLoaderMethodAttribute.cs

namespace Quantum {
  using System;

  /// <summary>
  /// Base class for all attributes that can be used to load <see cref="QuantumGlobalScriptableObject"/>.
  /// Attributes need to be registered at the assembly level. For instance, this snippet is used to register a default loader,
  /// that attempts to load from Resources based on <see cref="QuantumGlobalScriptableObjectAttribute.DefaultPath"/>:
  /// <code>
  /// [assembly: Quantum.QuantumGlobalScriptableObjectResource(typeof(Quantum.QuantumGlobalScriptableObject), Order = 2000, AllowFallback = true)]
  /// </code>
  /// </summary>
  /// <seealso cref="QuantumGlobalScriptableObjectAddressAttribute"/>
  /// <seealso cref="QuantumGlobalScriptableObjectResourceAttribute"/>
  [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
  public abstract class QuantumGlobalScriptableObjectSourceAttribute : Attribute {
    /// <param name="objectType">Type or the base type of <see cref="QuantumGlobalScriptableObject"/> that this loader supports.</param>
    public QuantumGlobalScriptableObjectSourceAttribute(Type objectType) {
      ObjectType = objectType;
    }
    
    /// <summary>
    /// Type or the base type of <see cref="QuantumGlobalScriptableObject"/> that this loader supports.
    /// </summary>
    public Type ObjectType { get; }
    /// <summary>
    /// Order in which this loader will be executed. Lower values are executed first.
    /// </summary>
    public int Order { get; set; }
    /// <summary>
    /// Can this loader be used in edit mode.
    /// </summary>
    public bool AllowEditMode { get; set; } = false;
    /// <summary>
    /// Does this loader allow fallback to the next loader?
    /// </summary>
    public bool AllowFallback { get; set; } = false;

    /// <summary>
    /// Attempt to load the object of the specified type. Return <see langword="default"/> if the object cannot be loaded.
    /// </summary>
    /// <param name="type">The requested type</param>
    public abstract QuantumGlobalScriptableObjectLoadResult Load(Type type);
  }
  
  [Obsolete("Use one of QuantumGlobalScriptableObjectSourceAttribute-derived types instead", true)]
  [AttributeUsage(AttributeTargets.Method)]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
  public class QuantumGlobalScriptableObjectLoaderMethodAttribute : Attribute {
    public int Order { get; set; }
  }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
  
  /// <summary>
  /// A delegate that can be used to unload a <see cref="QuantumGlobalScriptableObject"/>.
  /// </summary>
  public delegate void QuantumGlobalScriptableObjectUnloadDelegate(QuantumGlobalScriptableObject instance);
  
  /// <summary>
  /// The result of <see cref="QuantumGlobalScriptableObjectSourceAttribute.Load"/>. Contains the loaded object and an optional
  /// unloader delegate.
  /// </summary>
  public readonly struct QuantumGlobalScriptableObjectLoadResult {
    /// <summary>
    /// Object instance.
    /// </summary>
    public readonly QuantumGlobalScriptableObject               Object;
    /// <summary>
    /// An optional delegate that is used to unload <see cref="Object"/>.
    /// </summary>
    public readonly QuantumGlobalScriptableObjectUnloadDelegate Unloader;
    
    /// <param name="obj">Object instance.</param>
    /// <param name="unloader">An optional delegate that is used to unload <paramref name="obj"/>.</param>
    public QuantumGlobalScriptableObjectLoadResult(QuantumGlobalScriptableObject obj, QuantumGlobalScriptableObjectUnloadDelegate unloader = null) {
      Object = obj;
      Unloader = unloader;
    }
    
    /// <summary>
    /// Implicitly converts a <see cref="QuantumGlobalScriptableObject"/> to a <see cref="QuantumGlobalScriptableObjectLoadResult"/>.
    /// </summary>
    public static implicit operator QuantumGlobalScriptableObjectLoadResult(QuantumGlobalScriptableObject result) => new QuantumGlobalScriptableObjectLoadResult(result, null);
  }
}

#endregion


#region QuantumMonoBehaviour.cs

namespace Quantum {
  using UnityEngine;

  /// <summary>
  /// Base class for all Quantum MonoBehaviours.
  /// </summary>
  public abstract partial class QuantumMonoBehaviour : MonoBehaviour {
    
  }
}

#endregion


#region QuantumScriptableObject.cs

namespace Quantum {
  using UnityEngine;

  /// <summary>
  /// Base class for all Quantum scriptable objects.
  /// </summary>
  public abstract partial class QuantumScriptableObject : ScriptableObject {
  }
}

#endregion



#endregion


#region Assets/Photon/Quantum/Runtime/QuantumUnityUtility.Common.cs

// merged UnityUtility

#region JsonUtilityExtensions.cs

namespace Quantum {
  using System;
  using System.Collections;
  using System.Collections.Generic;
  using System.IO;
  using System.Text;
  using System.Text.RegularExpressions;
  using UnityEngine;

  /// <summary>
  /// Extends capabilities of <see cref="JsonUtility"/> by adding type annotations to the serialized JSON, Unity object reference
  /// handling and integer enquotement.
  /// </summary>
  public static class JsonUtilityExtensions {
    
    /// <see cref="JsonUtilityExtensions.FromJsonWithTypeAnnotation"/>
    public delegate Type TypeResolverDelegate(string typeName);
    /// <see cref="JsonUtilityExtensions.ToJsonWithTypeAnnotation(object,Quantum.JsonUtilityExtensions.InstanceIDHandlerDelegate)"/>
    public delegate string TypeSerializerDelegate(Type type);
    /// <see cref="JsonUtilityExtensions.ToJsonWithTypeAnnotation(object,Quantum.JsonUtilityExtensions.InstanceIDHandlerDelegate)"/>
    public delegate string InstanceIDHandlerDelegate(object context, int value);
    
    private const string TypePropertyName = "$type";

    /// <summary>
    /// Enquotes integers in the JSON string that are at least <paramref name="minDigits"/> long. This is useful for parsers that
    /// interpret large integers as floating point numbers.
    /// </summary>
    /// <param name="json">JSON to process</param>
    /// <param name="minDigits">Digit threshold to perfom the enquoting</param>
    /// <returns><paramref name="json"/> with long integers enquoted.</returns>
    public static string EnquoteIntegers(string json, int minDigits = 8) {
      var result = Regex.Replace(json, $@"(?<="":\s*)(-?[0-9]{{{minDigits},}})(?=[,}}\n\r\s])", "\"$1\"", RegexOptions.Compiled);
      return result;
    }

    /// <summary>
    /// Converts the object to JSON with type annotations.
    /// </summary>
    /// <param name="obj">Object to be serialized.</param>
    /// <param name="instanceIDHandler">Handler for UnityEngine.Object references. If the handler returns an empty string,
    /// the reference is removed from the final result.</param>
    public static string ToJsonWithTypeAnnotation(object obj, InstanceIDHandlerDelegate instanceIDHandler = null) {
      var sb = new StringBuilder(1000);
      using (var writer = new StringWriter(sb)) {
        ToJsonWithTypeAnnotation(obj, writer, instanceIDHandler: instanceIDHandler);
      }
      return sb.ToString();
    }

    /// <summary>
    /// Converts the object/IList to JSON with type annotations.
    /// </summary>
    /// <param name="obj">Object to be serialized.</param>
    /// <param name="writer">The output TextWriter.</param>
    /// <param name="integerEnquoteMinDigits"><see cref="EnquoteIntegers"/></param>
    /// <param name="typeSerializer">Handler for obtaining serialized type names. If <see langword="null"/>, the short assembly
    /// qualified name (namespace + name + assembly name) will be used.</param>
    /// <param name="instanceIDHandler">Handler for UnityEngine.Object references. If the handler returns an empty string,
    /// the reference is removed from the final result.</param>
    public static void ToJsonWithTypeAnnotation(object obj, TextWriter writer, int? integerEnquoteMinDigits = null, TypeSerializerDelegate typeSerializer = null, InstanceIDHandlerDelegate instanceIDHandler = null) {
      if (obj == null) {
        writer.Write("null");
        return;
      }

      if (obj is IList list) {
        writer.Write("[");
        for (var i = 0; i < list.Count; ++i) {
          if (i > 0) {
            writer.Write(",");
          }

          ToJsonInternal(list[i], writer, integerEnquoteMinDigits, typeSerializer, instanceIDHandler);
        }

        writer.Write("]");
      } else {
        ToJsonInternal(obj, writer, integerEnquoteMinDigits, typeSerializer, instanceIDHandler);
      }
    }
    
    
    /// <summary>
    /// Converts JSON with type annotation to an instance of <typeparamref name="T"/>. If the JSON contains type annotations, they need to match
    /// the expected result type. If there are no type annotations, use <paramref name="typeResolver"/> to return the expected type.
    /// </summary>
    /// <param name="json">JSON to be parsed</param>
    /// <param name="typeResolver">Converts type name to a type instance.</param>
    public static T FromJsonWithTypeAnnotation<T>(string json, TypeResolverDelegate typeResolver = null) {
      if (typeof(T).IsArray) {
        var listType = typeof(List<>).MakeGenericType(typeof(T).GetElementType());
        var list = (IList)Activator.CreateInstance(listType);
        FromJsonWithTypeAnnotationInternal(json, typeResolver, list);

        var array = Array.CreateInstance(typeof(T).GetElementType(), list.Count);
        list.CopyTo(array, 0);
        return (T)(object)array;
      }

      if (typeof(T).GetInterface(typeof(IList).FullName) != null) {
        var list = (IList)Activator.CreateInstance(typeof(T));
        FromJsonWithTypeAnnotationInternal(json, typeResolver, list);
        return (T)list;
      }

      return (T)FromJsonWithTypeAnnotationInternal(json, typeResolver);
    }

    /// <summary>
    /// Converts JSON with type annotation. If there are no type annotations, use <paramref name="typeResolver"/> to return the expected type.
    /// </summary>
    /// <param name="json">JSON to be parsed</param>
    /// <param name="typeResolver">Converts type name to a type instance.</param>
    public static object FromJsonWithTypeAnnotation(string json, TypeResolverDelegate typeResolver = null) {
      Assert.Check(json != null);

      var i = SkipWhiteOrThrow(0);
      if (json[i] == '[') {
        var list = new List<object>();

        // list
        ++i;
        for (var expectComma = false;; expectComma = true) {
          i = SkipWhiteOrThrow(i);

          if (json[i] == ']') {
            break;
          }

          if (expectComma) {
            if (json[i] != ',') {
              throw new InvalidOperationException($"Malformed at {i}: expected ,");
            }
            i = SkipWhiteOrThrow(i + 1);
          }

          var item = FromJsonWithTypeAnnotationToObject(ref i, json, typeResolver);
          list.Add(item);
        }

        return list.ToArray();
      }

      return FromJsonWithTypeAnnotationToObject(ref i, json, typeResolver);

      int SkipWhiteOrThrow(int i) {
        while (i < json.Length && char.IsWhiteSpace(json[i])) {
          i++;
        }

        if (i == json.Length) {
          throw new InvalidOperationException($"Malformed at {i}: expected more");
        }

        return i;
      }
    }

    
    private static object FromJsonWithTypeAnnotationInternal(string json, TypeResolverDelegate typeResolver = null, IList targetList = null) {
      Assert.Check(json != null);

      var i = SkipWhiteOrThrow(0);
      if (json[i] == '[') {
        var list = targetList ?? new List<object>();

        // list
        ++i;
        for (var expectComma = false;; expectComma = true) {
          i = SkipWhiteOrThrow(i);

          if (json[i] == ']') {
            break;
          }

          if (expectComma) {
            if (json[i] != ',') {
              throw new InvalidOperationException($"Malformed at {i}: expected ,");
            }

            i = SkipWhiteOrThrow(i + 1);
          }

          var item = FromJsonWithTypeAnnotationToObject(ref i, json, typeResolver);
          list.Add(item);
        }

        return targetList ?? ((List<object>)list).ToArray();
      }

      if (targetList != null) {
        throw new InvalidOperationException($"Expected list, got {json[i]}");
      }

      return FromJsonWithTypeAnnotationToObject(ref i, json, typeResolver);

      int SkipWhiteOrThrow(int i) {
        while (i < json.Length && char.IsWhiteSpace(json[i])) {
          i++;
        }

        if (i == json.Length) {
          throw new InvalidOperationException($"Malformed at {i}: expected more");
        }

        return i;
      }
    }

    private static void ToJsonInternal(object obj, TextWriter writer, 
      int? integerEnquoteMinDigits = null,
      TypeSerializerDelegate typeResolver = null,
      InstanceIDHandlerDelegate instanceIDHandler = null) {
      Assert.Check(obj != null);
      Assert.Check(writer != null);

      var json = JsonUtility.ToJson(obj);
      if (integerEnquoteMinDigits.HasValue) {
        json = EnquoteIntegers(json, integerEnquoteMinDigits.Value);
      }
      
      var type = obj.GetType();

      writer.Write("{\"");
      writer.Write(TypePropertyName);
      writer.Write("\":\"");

      writer.Write(typeResolver?.Invoke(type) ?? SerializableType.GetShortAssemblyQualifiedName(type));

      writer.Write('\"');

      if (json == "{}") {
        writer.Write("}");
      } else {
        Assert.Check('{' == json[0]);
        Assert.Check('}' == json[^1]);
        writer.Write(',');
        
        if (instanceIDHandler != null) {
          int i = 1;
          
          for (;;) {
            const string prefix = "{\"instanceID\":";
            
            var nextInstanceId = json.IndexOf(prefix, i, StringComparison.Ordinal);
            if (nextInstanceId < 0) {
              break;
            }
            
            // parse the number that follows; may be negative
            var start = nextInstanceId + prefix.Length;
            var end = json.IndexOf('}', start);
            var instanceId = int.Parse(json.AsSpan(start, end - start));
            
            // append that part
            writer.Write(json.AsSpan(i, nextInstanceId - i));
            writer.Write(instanceIDHandler(obj, instanceId));
            i = end + 1;
          }
          
          writer.Write(json.AsSpan(i, json.Length - i));
        } else {
          writer.Write(json.AsSpan(1, json.Length - 1));
        }
      }
    }

    private static object FromJsonWithTypeAnnotationToObject(ref int i, string json, TypeResolverDelegate typeResolver) {
      if (json[i] == '{') {
        var endIndex = FindScopeEnd(json, i, '{', '}');
        if (endIndex < 0) {
          throw new InvalidOperationException($"Unable to find end of object's end (starting at {i})");
        }
        
        Assert.Check(endIndex > i);
        Assert.Check(json[endIndex] == '}');

        var part = json.Substring(i, endIndex - i + 1);
        i = endIndex + 1;

        // read the object, only care about the type; there's no way to map dollar-prefixed property to a C# field,
        // so some string replacing is necessary
        var typeInfo = JsonUtility.FromJson<TypeNameWrapper>(part.Replace(TypePropertyName, nameof(TypeNameWrapper.__TypeName), StringComparison.Ordinal));

        Type type;
        if (typeResolver != null) {
          type = typeResolver(typeInfo.__TypeName);
          if (type == null) {
            return null;
          }
        } else {
          Assert.Check(!string.IsNullOrEmpty(typeInfo?.__TypeName));
          type = Type.GetType(typeInfo.__TypeName, true);
        }
        
        if (type.IsSubclassOf(typeof(ScriptableObject))) {
          var instance = ScriptableObject.CreateInstance(type);
          JsonUtility.FromJsonOverwrite(part, instance);
          return instance;
        } else {
          var instance = JsonUtility.FromJson(part, type);
          return instance;
        }
      }

      if (i + 4 < json.Length && json.AsSpan(i, 4).SequenceEqual("null")) {
        // is this null?
        i += 4;
        return null;
      }

      throw new InvalidOperationException($"Malformed at {i}: expected {{ or null");
    }
    
    internal static int FindObjectEnd(string json, int start = 0) {
      return FindScopeEnd(json, start, '{', '}');
    }
    
    private static int FindScopeEnd(string json, int start, char cstart = '{', char cend = '}') {
      var depth = 0;
      
      if (json[start] != cstart) {
        return -1;
      }

      for (var i = start; i < json.Length; i++) {
        if (json[i] == '"') {
          // can't be escaped
          Assert.Check('\\' != json[i - 1]);
          // now skip until the first unescaped quote
          while (i < json.Length) {
            if (json[++i] == '"')
              // are we escaped?
            {
              if (json[i - 1] != '\\') {
                break;
              }
            }
          }
        } else if (json[i] == cstart) {
          depth++;
        } else if (json[i] == cend) {
          depth--;
          if (depth == 0) {
            return i;
          }
        }
      }

      return -1;
    }
    
    [Serializable]
    private class TypeNameWrapper {
#pragma warning disable CS0649 // Set by serialization
      // ReSharper disable once InconsistentNaming
      public string __TypeName;
#pragma warning restore CS0649
    }
  }
}

#endregion


#region QuantumAddressablesUtils.cs

#if (QUANTUM_ADDRESSABLES || QUANTUM_ENABLE_ADDRESSABLES) && !QUANTUM_DISABLE_ADDRESSABLES
namespace Quantum {
  using System;
  using UnityEngine.AddressableAssets;
  using Object = UnityEngine.Object;

  /// <summary>
  /// Utility class for addressables.
  /// </summary>
  public static class QuantumAddressablesUtils {
    /// <summary>
    /// Tries to parse the address into main part and sub object name.
    /// </summary>
    /// <param name="address">The address to parse.</param>
    /// <param name="mainPart">The main part of the address.</param>
    /// <param name="subObjectName">The sub object name.</param>
    /// <returns><see langword="true"/> if the address is successfully parsed; otherwise, <see langword="false"/>.</returns>
    public static bool TryParseAddress(string address, out string mainPart, out string subObjectName) {
      if (string.IsNullOrEmpty(address)) {
        mainPart = null;
        subObjectName = null;
        return false;
      }

      var indexOfSquareBracket = address.IndexOf('[');
      var indexOfClosingSquareBracket = address.IndexOf(']');

      // addresses can only use square brackets for sub object names
      // so only such usage is valid:
      // - mainAddress[SubObjectName]
      // this is not valid:
      // - mainAddress[SubObjectName
      // - mainAddressSubObjectName]
      // - mainAddress[SubObjectName]a
      // - mainAddress[]
      if ((indexOfSquareBracket == 0) ||
          (indexOfSquareBracket < 0 && (indexOfClosingSquareBracket >= 0)) ||
          (indexOfSquareBracket > 0 && (indexOfClosingSquareBracket != address.Length - 1)) ||
          (indexOfSquareBracket > 0 && (indexOfClosingSquareBracket - indexOfSquareBracket <= 1))) {
        mainPart = default;
        subObjectName = default;
        return false;
      }

      if (indexOfSquareBracket < 0) {
        mainPart = address;
        subObjectName = default;
        return true;
      }

      mainPart = address.Substring(0, indexOfSquareBracket);
      subObjectName = address.Substring(indexOfSquareBracket + 1, address.Length - indexOfSquareBracket - 2);
      return true;
    }

    /// <summary>
    /// Creates an asset reference from the given address.
    /// </summary>
    /// <param name="address">The address to create the asset reference from.</param>
    /// <returns>The created asset reference.</returns>
    /// <exception cref="System.ArgumentException">Thrown when the main part of the address is not a guid or the address is not valid.</exception>
    public static AssetReference CreateAssetReference(string address) {
      if (TryParseAddress(address, out var mainPart, out var subObjectName)) {
        if (System.Guid.TryParse(mainPart, out _)) {
          // ok, the main part is a guid, can create asset reference
          return new AssetReference(mainPart) {
            SubObjectName = subObjectName,
          };
        } else {
          throw new System.ArgumentException($"The main part of the address is not a guid: {mainPart}", nameof(address));
        }
      } else {
        throw new System.ArgumentException($"Not a valid address: {address}", nameof(address));
      }
    }

#if UNITY_EDITOR
    private static Func<string, Object> s_loadEditorInstance;

    /// <summary>
    /// Loads the editor instance for the given runtime key.
    /// </summary>
    /// <param name="runtimeKey">The runtime key.</param>
    /// <returns>The loaded editor instance.</returns>
    /// <exception cref="System.InvalidOperationException">Thrown when the load editor instance handler is not set.</exception>
    public static Object LoadEditorInstance(string runtimeKey) {
      Assert.Check(s_loadEditorInstance != null, $"Call {nameof(SetLoadEditorInstanceHandler)} before using this method");
      return s_loadEditorInstance(runtimeKey);
    }

    /// <summary>
    /// Sets the load editor instance handler.
    /// </summary>
    /// <param name="loadEditorInstance">The load editor instance handler.</param>
    public static void SetLoadEditorInstanceHandler(Func<string, Object> loadEditorInstance) {
      s_loadEditorInstance = loadEditorInstance;
    }
#endif
  }
}
#endif

#endregion


#region QuantumLogInitializer.cs

namespace Quantum {
  using System;
  using UnityEngine;
  
#if UNITY_EDITOR
  using UnityEditor;
  using UnityEditor.Build;
#endif
  
  /// <summary>
  /// Initializes the logging system for Quantum. Use <see cref="InitializeUser"/> to completely override the log level and trace channels or
  /// to provide a custom logger. Use <see cref="InitializeUnityLoggerUser"/> to override default Unity logger settings.
  /// </summary>
  public static partial class QuantumLogInitializer {
    /// <summary>
    /// Initializes the logging system for Quantum. This method is called automatically when the assembly is loaded.
    /// </summary>
#if UNITY_EDITOR
    [UnityEditor.InitializeOnLoadMethod]
#endif
    [RuntimeInitializeOnLoadMethod]
    public static void Initialize() {
      var isDark = false;
#if UNITY_EDITOR
      isDark = UnityEditor.EditorGUIUtility.isProSkin;
      QuantumEditorLog.Initialize(isDark);
#endif
      
      LogLevel logLevel = QuantumLogConstants.DefinedLogLevel;
      TraceChannels traceChannels = QuantumLogConstants.DefinedTraceChannels;
      InitializeUser(ref logLevel, ref traceChannels);

      if (Log.IsInitialized) {
        return;
      }

      var logger = CreateLogger(isDarkMode: isDark);
      InitializeUnityLoggerUser(ref logger);
      Log.Initialize(logLevel, logger.CreateLogStream, traceChannels);
    }
    
    static partial void InitializeUser(ref LogLevel logLevel, ref TraceChannels traceChannels);
  }
}

#endregion


#region QuantumMppm.cs

namespace Quantum {
  using System;
  using System.Diagnostics;
  using JetBrains.Annotations;
#if QUANTUM_ENABLE_MPPM
  using System.Collections.Generic;
  using System.IO;
  using System.Linq;
  using System.Text.RegularExpressions;
  using System.Threading;
  using UnityEditor;
#if UNITY_EDITOR
  using UnityEditor.MPE;
#endif
  using UnityEngine;
  using Debug = UnityEngine.Debug;
#endif
  
  // ReSharper disable once IdentifierTypo
  /// <summary>
  /// The current status of MPPM. If the package is not enabled, this will always be <see cref="QuantumMppmStatus.Disabled"/>.
  /// </summary>
  public enum QuantumMppmStatus {
    /// <summary>
    /// MPPM is not installed.
    /// </summary>
    Disabled,
    /// <summary>
    /// This instance is the main instance. Can use <see cref="QuantumMppm.Send{T}"/> to send commands.
    /// </summary>
    MainInstance,
    /// <summary>
    /// This instance is a virtual instance. Will receive commands from the main instance.
    /// </summary>
    VirtualInstance
  }
  
  /// <summary>
  /// Support for Multiplayer Play Mode (MPPM). It uses named pipes
  /// to communicate between the main Unity instance and virtual instances.
  /// </summary>
#if QUANTUM_ENABLE_MPPM && UNITY_EDITOR
  [InitializeOnLoad]
#endif
  // ReSharper disable once IdentifierTypo
  public partial class QuantumMppm {
    
    /// <summary>
    /// The current status of MPPM.
    /// </summary>
    public static readonly QuantumMppmStatus Status = QuantumMppmStatus.Disabled;
    
    /// <summary>
    /// If <see cref="Status"/> is <see cref="QuantumMppmStatus.MainInstance"/>, this static field can be used to send commands.
    /// </summary>
    [CanBeNull]
    public static readonly QuantumMppm MainEditor = null;

    /// <summary>
    /// Sends a command to all virtual instances. Use as:
    /// <code>QuantumMppm.MainEditor?.Send</code>
    /// </summary>
    /// <param name="data"></param>
    /// <typeparam name="T"></typeparam>
    [Conditional("UNITY_EDITOR")]
    public void Send<T>(T data) where T : QuantumMppmCommand {
#if QUANTUM_ENABLE_MPPM && UNITY_EDITOR
      Assert.Check(Status == QuantumMppmStatus.MainInstance, "Only the main instance can send commands");
      BroadcastInternal(data);
#endif
    }

    
    /// <summary>
    /// Broadcasts a command to all virtual instances.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="data"></param>
#if QUANTUM_ENABLE_MPPM
    [Conditional("UNITY_EDITOR")]
#else
    [Conditional("QUANTUM_ENABLE_MPPM")]
#endif
    [Obsolete("Use QuantumMppm.Broadcaster?.Send instead")]
    public static void Broadcast<T>(T data) where T : QuantumMppmCommand {
      MainEditor?.Send(data);
    }

    private QuantumMppm() {
      
    }
    
#if QUANTUM_ENABLE_MPPM && UNITY_EDITOR
    private static readonly string s_mainInstancePath = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
    
    private const string PersistentCommandsFolderPath = "Temp/QuantumMppm";
    private const string MpeChannelName = "QuantumMppm";
    
    private readonly int _mpeChannelId = ChannelService.ChannelNameToId(MpeChannelName);
    private readonly List<(int connectionId, string guid)> _acks = new List<(int, string)>();
    private readonly Regex _invalidFileCharactersRegex = new Regex(string.Format(@"([{0}]*\.+$)|([{0}]+)", Regex.Escape(new string(Path.GetInvalidFileNameChars()))));
    
    static QuantumMppm() {
      
      var indexOfMppmPrefix = Application.dataPath.LastIndexOf("/Library/VP/mppm", StringComparison.OrdinalIgnoreCase);
      Status = indexOfMppmPrefix < 0 ? QuantumMppmStatus.MainInstance : QuantumMppmStatus.VirtualInstance;
    
      // start MPE (this check is canonical)
      if (!ChannelService.IsRunning()) {
        ChannelService.Start();
      }
      
      QuantumEditorLog.TraceMppm($"Status: {Status}, MainInstancePath: {s_mainInstancePath}");
      
      if (Status == QuantumMppmStatus.MainInstance) {
        
        MainEditor = new QuantumMppm();
        // set up MPE channel
        var disconnect = ChannelService.GetOrCreateChannel(MpeChannelName, MainEditor.ReceiveAck);
        Debug.Assert(disconnect != null);
        
        // ... but since new instances need to e.g. receive all the dependency hashes, set up a folder;
        // it needs to be cleared on every Unity start but survive between domain reloads
        string folderOwnedKey = $"Owns_{PersistentCommandsFolderPath}";
        
        if (Directory.Exists(PersistentCommandsFolderPath) && !SessionState.GetBool(folderOwnedKey, false)) {
          QuantumEditorLog.TraceMppm($"Deleting leftover files from {PersistentCommandsFolderPath}");
          foreach (var file in Directory.GetFiles(PersistentCommandsFolderPath)) {
            File.Delete(file);
          }
        }
        
        if (!Directory.Exists(PersistentCommandsFolderPath)) {
          QuantumEditorLog.TraceMppm($"Creating command folder {PersistentCommandsFolderPath}");
          Directory.CreateDirectory(PersistentCommandsFolderPath);
        }
        SessionState.SetBool(folderOwnedKey, true);
        
      } else {
        // where is the main instance located?
        s_mainInstancePath = Application.dataPath.Substring(0, indexOfMppmPrefix);
        
        // start the MPE client to await commands
        var client = ChannelClient.GetOrCreateClient(MpeChannelName);
        client.Start(true);
        var disconnect = client.RegisterMessageHandler(data => {
          var json = System.Text.Encoding.UTF8.GetString(data);
          var message = JsonUtility.FromJson<CommandWrapper>(json);
          
          QuantumEditorLog.TraceMppm($"Received command {message.Data}");
          message.Data.Execute();
          if (message.Data.NeedsAck) {
            var ack = new AckMessage() {
              Guid = message.Guid
            };
            var ackJson = JsonUtility.ToJson(ack);
            QuantumEditorLog.TraceMppm($"Sending ack {ackJson}");
            var ackBytes = System.Text.Encoding.UTF8.GetBytes(ackJson);
            client.Send(ackBytes);
          }
        });
        Debug.Assert(disconnect != null);
        
        // read persistent commands from the main instance
        var mainInstanceCommandsFolderPath = Path.Combine(s_mainInstancePath, PersistentCommandsFolderPath);
        Debug.Assert(Directory.Exists(mainInstanceCommandsFolderPath));
        foreach (var file in Directory.GetFiles(mainInstanceCommandsFolderPath, "*.json")) {
          var json = File.ReadAllText(file);
          var wrapper = JsonUtility.FromJson<CommandWrapper>(json);
          QuantumEditorLog.TraceMppm($"Received persistent command {wrapper.Data}");
          wrapper.Data.Execute();
        }
      }
    }
    
    private void BroadcastInternal<T>(T data) where T : QuantumMppmCommand {
      Assert.Check(Status == QuantumMppmStatus.MainInstance, "Only the main instance can send commands");
      
      var guid = Guid.NewGuid().ToString();
      var wrapper = new CommandWrapper() {
        Guid = guid,
        Data = data
      };
      
      var str   = JsonUtility.ToJson(wrapper);
      var bytes = System.Text.Encoding.UTF8.GetBytes(str);
      
      QuantumEditorLog.TraceMppm($"Broadcasting command {str}");
      ChannelService.BroadcastBinary(_mpeChannelId, bytes);

      var persistentKey = data.PersistentKey;
      if (!string.IsNullOrEmpty(persistentKey)) {
        var fileName = $"{_invalidFileCharactersRegex.Replace(persistentKey, "_")}.json";
        var filePath = Path.Combine(PersistentCommandsFolderPath, fileName);
        QuantumEditorLog.TraceMppm($"Saving persistent command to {filePath}");
        File.WriteAllText(filePath, str);
      }
      
      if (data.NeedsAck) {
        // well, we need to wait
        var channels = ChannelService.GetChannelClientList();
        // how many acks do we need?
        var numAcks = channels.Count(x => x.name == MpeChannelName);
        WaitForAcks(numAcks, guid);
      }
    }
    
    private void ReceiveAck(int connectionId, byte[] data) {
      var json    = System.Text.Encoding.UTF8.GetString(data);
      var message = JsonUtility.FromJson<AckMessage>(json);
      lock (_acks) {
        _acks.Add((connectionId, message.Guid));
      }
      QuantumEditorLog.TraceMppm($"Received ack {json}");
    }
    
    private void WaitForAcks(int numAcks, string guid) {
      var timer   = Stopwatch.StartNew();
      var timeout = TimeSpan.FromSeconds(2);
      
      QuantumEditorLog.TraceMppm($"Waiting for {numAcks} acks for {guid}");
      
      while (timer.Elapsed < timeout) {
        for (int i = 0; numAcks > 0 && i < _acks.Count; i++) {
          var ack = _acks[i];
          if (ack.guid == guid) {
            _acks.RemoveAt(i);
            numAcks--;
              
            QuantumEditorLog.TraceMppm($"Received ack for {guid} from {ack.connectionId}, {numAcks} left");
          }
        }

        if (numAcks <= 0) {
          QuantumEditorLog.TraceMppm($"All acks received");
          return;
        }
          
        QuantumEditorLog.TraceMppm($"Waiting for {numAcks} acks");
        ChannelService.DispatchMessages();
        Thread.Sleep(10);
      }
      
      QuantumEditorLog.TraceMppm($"Timeout waiting for acks ({numAcks} left)");
    }
    
    [Serializable]
    private class CommandWrapper {
      public string Guid;
      [SerializeReference] public QuantumMppmCommand Data;
    }

    [Serializable]
    private class AckMessage {
      public string Guid;
    }
#endif
  }
  
  /// <summary>
  /// The base class for all Quantum MPPM commands.
  /// </summary>
  [Serializable]
  // ReSharper disable once IdentifierTypo
  public abstract class QuantumMppmCommand {
    /// <summary>
    /// Execute the command on a virtual instance.
    /// </summary>
    public abstract void Execute();
    /// <summary>
    /// Does the main instance need to wait for an ack?
    /// </summary>
    public virtual bool NeedsAck => false;
    /// <summary>
    /// If the command is persistent (i.e. needs to be executed on each domain reload), this key is used to store it.
    /// </summary>
    public virtual string PersistentKey => null;
  }
}

#endregion


#region QuantumMppmRegisterCustomDependencyCommand.cs

#if UNITY_EDITOR
namespace Quantum {
  using System;
  using UnityEngine;

  /// <summary>
  /// A command implementing a workaround for MPPM not syncing custom dependencies.
  /// </summary>
  [Serializable]
  public class QuantumMppmRegisterCustomDependencyCommand : QuantumMppmCommand {
    /// <summary>
    /// Name of the custom dependency.
    /// </summary>
    public string DependencyName;
    /// <summary>
    /// Hash of the custom dependency.
    /// </summary>
    public string Hash;
      
    /// <inheritdoc cref="QuantumMppmCommand.NeedsAck"/>
    public override bool NeedsAck => true;

    /// <inheritdoc cref="QuantumMppmCommand.PersistentKey"/>
    public override string PersistentKey => $"Dependency_{DependencyName}";
      
    /// <summary>
    /// Registers a custom dependency with the given name and hash.
    /// </summary>
    public override void Execute() {
      QuantumEditorLog.TraceMppm($"Registering custom dependency {DependencyName} with hash {Hash}");
      var hash = Hash128.Parse(Hash);
      UnityEditor.AssetDatabase.RegisterCustomDependency(DependencyName, hash);
    }
  }
}
#endif

#endregion


#region QuantumUnityExtensions.cs

namespace Quantum {
#if UNITY_2022_1_OR_NEWER && !UNITY_2022_2_OR_NEWER
  using UnityEngine;
#endif

  /// <summary>
  /// Provides backwards compatibility for Unity API.
  /// </summary>
  public static class QuantumUnityExtensions {
    
    #region New Find API

#if UNITY_2022_1_OR_NEWER && !UNITY_2022_2_OR_NEWER 
    public enum FindObjectsInactive {
      Exclude,
      Include,
    }

    public enum FindObjectsSortMode {
      None,
      InstanceID,
    }

    public static T FindFirstObjectByType<T>() where T : Object {
      return (T)FindFirstObjectByType(typeof(T), FindObjectsInactive.Exclude);
    }

    public static T FindAnyObjectByType<T>() where T : Object {
      return (T)FindAnyObjectByType(typeof(T), FindObjectsInactive.Exclude);
    }

    public static T FindFirstObjectByType<T>(FindObjectsInactive findObjectsInactive) where T : Object {
      return (T)FindFirstObjectByType(typeof(T), findObjectsInactive);
    }

    public static T FindAnyObjectByType<T>(FindObjectsInactive findObjectsInactive) where T : Object {
      return (T)FindAnyObjectByType(typeof(T), findObjectsInactive);
    }

    public static Object FindFirstObjectByType(System.Type type, FindObjectsInactive findObjectsInactive) {
      return Object.FindObjectOfType(type, findObjectsInactive == FindObjectsInactive.Include);
    }

    public static Object FindAnyObjectByType(System.Type type, FindObjectsInactive findObjectsInactive) {
      return Object.FindObjectOfType(type, findObjectsInactive == FindObjectsInactive.Include);
    }

    public static T[] FindObjectsByType<T>(FindObjectsSortMode sortMode) where T : Object {
      return ConvertObjects<T>(FindObjectsByType(typeof(T), FindObjectsInactive.Exclude, sortMode));
    }

    public static T[] FindObjectsByType<T>(
      FindObjectsInactive findObjectsInactive,
      FindObjectsSortMode sortMode)
      where T : Object {
      return ConvertObjects<T>(FindObjectsByType(typeof(T), findObjectsInactive, sortMode));
    }

    public static Object[] FindObjectsByType(System.Type type, FindObjectsSortMode sortMode) {
      return FindObjectsByType(type, FindObjectsInactive.Exclude, sortMode);
    }

    public static Object[] FindObjectsByType(System.Type type, FindObjectsInactive findObjectsInactive, FindObjectsSortMode sortMode) {
      return Object.FindObjectsOfType(type, findObjectsInactive == FindObjectsInactive.Include);
    }

    static T[] ConvertObjects<T>(Object[] rawObjects) where T : Object {
      if (rawObjects == null)
        return (T[])null;
      T[] objArray = new T[rawObjects.Length];
      for (int index = 0; index < objArray.Length; ++index)
        objArray[index] = (T)rawObjects[index];
      return objArray;
    }

#endif

    #endregion
  }
}

#endregion



#endregion


#region Assets/Photon/Quantum/Runtime/UnityDB/QuantumUnityDB.Editor.cs

namespace Quantum {
  using System;

  partial class QuantumUnityDB {
#if UNITY_EDITOR
    /// <inheritdoc cref="QuantumUnityDB.GetAssetEditorInstance(AssetRef)"/>
    public static AssetObject GetGlobalAssetEditorInstance(AssetRef assetRef) => Global.GetAssetEditorInstance(assetRef);
    
    /// <inheritdoc cref="GetAssetEditorInstance{T}(Quantum.AssetRef{T})"/>
    public static T GetGlobalAssetEditorInstance<T>(AssetRef<T> assetRef) where T : AssetObject => Global.GetAssetEditorInstance(assetRef);
    
    /// <inheritdoc cref="GetAssetEditorInstance{T}(Quantum.AssetRef)"/>
    public static T GetGlobalAssetEditorInstance<T>(AssetRef assetRef) where T : AssetObject => Global.GetAssetEditorInstance<T>(assetRef);

    /// <summary>
    /// Returns the editor instance of the asset with the given <paramref name="assetRef"/>. Use in editor code only, for inspectors
    /// and editors. Returned asset will not have its <see cref="AssetObject.Loaded"/> called, as instances are obtained
    /// from <see cref="IQuantumAssetObjectSource.EditorInstance"/>.
    /// </summary>
    /// <param name="assetRef"></param>
    /// <returns>Asset instance or <c>null</c> if not found.</returns>
    public AssetObject GetAssetEditorInstance(AssetRef assetRef) => GetAssetEditorInstanceInternal(assetRef.Id);
    
    /// <inheritdoc cref="GetAssetEditorInstance"/>
    public T GetAssetEditorInstance<T>(AssetRef<T> assetRef) where T : AssetObject => GetAssetEditorInstanceInternal(assetRef.Id) as T;
    
    /// <summary>
    /// Returns the editor instance of the asset with the given <paramref name="assetRef"/>. Use in editor code only, for inspectors
    /// and editors. Returned asset will not have its <see cref="AssetObject.Loaded"/> called, as instances are obtained
    /// from <see cref="IQuantumAssetObjectSource.EditorInstance"/>.
    /// </summary>
    /// <param name="assetRef"></param>
    /// <returns>Asset instance or <c>null</c> if not found or the type does not match.</returns>
    public T GetAssetEditorInstance<T>(AssetRef assetRef) where T : AssetObject => GetAssetEditorInstanceInternal(assetRef.Id) as T;
    
    private AssetObject GetAssetEditorInstanceInternal(AssetGuid guid) {
      var assetSource = GetAssetSource(guid);
      if (assetSource == null) {
        // not mapped in the resource container
        return default;
      }

      return assetSource.EditorInstance;
    }
    
    /// <inheritdoc cref="TryGetGlobalAssetEditorInstance{T}(Quantum.AssetRef,out T)"/>
    public static bool TryGetGlobalAssetEditorInstance<T>(AssetRef assetRef, out T result)
      where T : AssetObject {
      return Global.TryGetAssetObjectEditorInstance(assetRef, out result);
    }
    
    /// <inheritdoc cref="TryGetGlobalAssetEditorInstance{T}(Quantum.AssetRef{T},out T)"/>
    public static bool TryGetGlobalAssetEditorInstance<T>(AssetRef<T> assetRef, out T result)
      where T : AssetObject {
      return Global.TryGetAssetObjectEditorInstance(assetRef, out result);
    }
    
    /// <inheritdoc cref="TryGetAssetObjectEditorInstance{T}(Quantum.AssetRef,out T)"/>
    public bool TryGetAssetObjectEditorInstance<T>(AssetRef<T> assetRef, out T result)
      where T : AssetObject {
      return TryGetAssetObjectEditorInstance((AssetRef)assetRef.Id, out result);
    }

    /// <summary>
    /// Attempts to get the editor instance of the asset with the given <paramref name="assetRef"/>. Use in editor code only, for inspectors
    /// and editors. Returned asset will not have its <see cref="AssetObject.Loaded"/> called, as instances are obtained
    /// from <see cref="IQuantumAssetObjectSource.EditorInstance"/>.
    /// </summary>
    /// <param name="assetRef"></param>
    /// <param name="result"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns><c>true</c> if the asset was found and the type matches, <c>false</c> otherwise.</returns>
    public bool TryGetAssetObjectEditorInstance<T>(AssetRef assetRef, out T result)
      where T : AssetObject {

      var assetReference = GetAssetSource(assetRef.Id);
      if (assetReference == null) {
        result = null;
        return false;
      }

      var editorInstance = assetReference.EditorInstance;
      if (editorInstance is T assetT) {
        result = assetT;
        return true;
      }

      result = null;
      return false;
    }

    /// <summary>
    /// Creates Quantum asset path based on the Unity asset path. The resulting path will have its extension removed.
    /// If the path is not in the "Assets" folder, it will be made relative to it.
    /// </summary>
    /// <param name="unityAssetPath"></param>
    /// <param name="nestedName"></param>
    /// <returns></returns>
    public static string CreateAssetPathFromUnityPath(string unityAssetPath, string nestedName = null) {
      var path = PathUtils.GetPathWithoutExtension(unityAssetPath);
      
      if (!path.StartsWith("Packages/", StringComparison.Ordinal) && PathUtils.MakeRelativeToFolderFast(path, "Assets/", out var relativePath)) {
        path = relativePath;
      }

      if (nestedName != null) {
        path += NestedPathSeparator + nestedName;
      }

      return path;
    }

#endif
  }
}

#endregion


#region Assets/Photon/Quantum/Runtime/Utils/DebugDraw.cs

namespace Quantum {
  using System;
  using System.Collections.Generic;
  using Photon.Analyzer;
  using UnityEngine;

  /// <summary>
  /// This class will draw shapes issued by the simulation (e.g. <see cref="Draw.Sphere(Photon.Deterministic.FPVector3, Photon.Deterministic.FP, ColorRGBA?, bool)"/>)
  /// The shape drawing is based on the DEBUG define which is enabled in UnityEditor and development builds.
  /// Can be globally toggled of by using <see cref="IsEnabled"/>.
  /// </summary>
  public static class DebugDraw {
    /// <summary>
    /// Globally toggle on/off any simulation debug shape drawing.
    /// </summary>
    [StaticField] public static bool IsEnabled = true;

    [StaticField] static Queue<Draw.DebugRay> _rays = new Queue<Draw.DebugRay>();
    [StaticField] static Queue<Draw.DebugLine> _lines = new Queue<Draw.DebugLine>();
    [StaticField] static Queue<Draw.DebugCircle> _circles = new Queue<Draw.DebugCircle>();
    [StaticField] static Queue<Draw.DebugSphere> _spheres = new Queue<Draw.DebugSphere>();
    [StaticField] static Queue<Draw.DebugRectangle> _rectangles = new Queue<Draw.DebugRectangle>();
    [StaticField] static Queue<Draw.DebugBox> _boxes = new Queue<Draw.DebugBox>();
    [StaticField] static Queue<Draw.DebugCapsule> _capsules = new Queue<Draw.DebugCapsule>();
    [StaticField] static Dictionary<ColorRGBA, Material> _materials = new Dictionary<ColorRGBA, Material>(ColorRGBA.EqualityComparer.Instance);
    [StaticField] static Draw.DebugRay[] _raysArray = new Draw.DebugRay[64];
    [StaticField] static Draw.DebugLine[] _linesArray = new Draw.DebugLine[64];
    [StaticField] static Draw.DebugCircle[] _circlesArray = new Draw.DebugCircle[64];
    [StaticField] static Draw.DebugSphere[] _spheresArray = new Draw.DebugSphere[64];
    [StaticField] static Draw.DebugRectangle[] _rectanglesArray = new Draw.DebugRectangle[64];
    [StaticField] static Draw.DebugBox[] _boxesArray = new Draw.DebugBox[64];
    [StaticField] static Draw.DebugCapsule[] _capsuleArray = new Draw.DebugCapsule[64];
    [StaticField] static int _raysCount;
    [StaticField] static int _linesCount;
    [StaticField] static int _circlesCount;
    [StaticField] static int _spheresCount;
    [StaticField] static int _rectanglesCount;
    [StaticField] static int _boxesCount;
    [StaticField] static int _capsuleCount;
    [StaticField] static Vector3[] _circlePoints;

    const int CircleResolution = 64;

    static Vector3[] CirclePoints {
      get {
        if (_circlePoints == null) {
          _circlePoints = new Vector3[CircleResolution];
          for (int i = 0; i < CircleResolution; i++) {
            var theta = i / (float)CircleResolution * Mathf.PI * 2.0f;
            _circlePoints[i] = new Vector3(Mathf.Cos(theta), 0.0f, Mathf.Sin(theta));
          }
        }
        return _circlePoints;
      }
    }

    /// <summary>
    /// The action to call on Draw.Ray.
    /// </summary>
    /// <param name="ray">Ray to be drawn in the view.</param>
    public static void Ray(Draw.DebugRay ray) {
      if (IsEnabled == false) {
        return;
      }

      lock (_rays) {
        _rays.Enqueue(ray);
      }
    }

    /// <summary>
    /// The action to call on Draw.Line.
    /// </summary>
    /// <param name="line">The line information to draw in the view.</param>
    public static void Line(Draw.DebugLine line) {
      if (IsEnabled == false) {
        return;
      }

      lock (_lines) {
        _lines.Enqueue(line);
      }
    }

    /// <summary>
    /// The action to call on Draw.Circle.
    /// </summary>
    /// <param name="circle">Circle information</param>
    public static void Circle(Draw.DebugCircle circle) {
      if (IsEnabled == false) {
        return;
      }

      lock (_circles) {
        _circles.Enqueue(circle);
      }
    }

    /// <summary>
    /// The action to call on Draw.Sphere.
    /// </summary>
    /// <param name="sphere">Sphere information</param>
    public static void Sphere(Draw.DebugSphere sphere) {
      if (IsEnabled == false) {
        return;
      }

      lock (_spheres) {
        _spheres.Enqueue(sphere);
      }
    }

    /// <summary>
    /// The action to call on Draw.Rectangle.
    /// </summary>
    /// <param name="rectangle">Rectangle information</param>
    public static void Rectangle(Draw.DebugRectangle rectangle) {
      if (IsEnabled == false) {
        return;
      }

      lock (_rectangles) {
        _rectangles.Enqueue(rectangle);
      }
    }

    /// <summary>
    /// The action to call on Draw.Box.
    /// </summary>
    /// <param name="box">Boc information</param>
    public static void Box(Draw.DebugBox box) {
      if (IsEnabled == false) {
        return;
      }

      lock (_boxes) {
        _boxes.Enqueue(box);
      }
    }

    /// <summary>
    /// The action to call on Draw.Capsule.
    /// </summary>
    /// <param name="capsule">Capsule information</param>
    public static void Capsule(Draw.DebugCapsule capsule) {
      if (IsEnabled == false) {
        return;
      }

      lock (_capsules) {
        _capsules.Enqueue(capsule);
      }
    }

    /// <summary>
    /// Return the debug shape drawing material based on the color.
    /// Will set the main color on the material.
    /// </summary>
    /// <param name="color">Color</param>
    /// <returns>Material</returns>
    public static Material GetMaterial(ColorRGBA color) {
      if (_materials.TryGetValue(color, out var mat)) {
        if (mat != null) {
          return mat;
        }

        _materials.Remove(color);
      }

      mat = new Material(QuantumMeshCollection.Global.DebugMaterial);
      mat.SetColor("_Color", color.AsColor);

      _materials.Add(color, mat);
      return mat;
    }

    /// <summary>
    /// Clear everything still in the queues.
    /// </summary>
    [StaticFieldResetMethod]
    public static void Clear() {
      TakeAllFromQueueAndClearLocked(_rays, ref _raysArray);
      TakeAllFromQueueAndClearLocked(_lines, ref _linesArray);
      TakeAllFromQueueAndClearLocked(_circles, ref _circlesArray);
      TakeAllFromQueueAndClearLocked(_spheres, ref _spheresArray);
      TakeAllFromQueueAndClearLocked(_rectangles, ref _rectanglesArray);
      TakeAllFromQueueAndClearLocked(_boxes, ref _boxesArray);
      TakeAllFromQueueAndClearLocked(_capsules, ref _capsuleArray);

      _raysCount = 0;
      _linesCount = 0;
      _circlesCount = 0;
      _spheresCount = 0;
      _rectanglesCount = 0;
      _boxesCount = 0;
      _capsuleCount = 0;
    }

    /// <summary>
    /// Transfer all items from the locked queue to the internal draw shape arrays.
    /// </summary>
    public static void TakeAll() {
      _raysCount = TakeAllFromQueueAndClearLocked(_rays, ref _raysArray);
      _linesCount = TakeAllFromQueueAndClearLocked(_lines, ref _linesArray);
      _circlesCount = TakeAllFromQueueAndClearLocked(_circles, ref _circlesArray);
      _spheresCount = TakeAllFromQueueAndClearLocked(_spheres, ref _spheresArray);
      _rectanglesCount = TakeAllFromQueueAndClearLocked(_rectangles, ref _rectanglesArray);
      _boxesCount = TakeAllFromQueueAndClearLocked(_boxes, ref _boxesArray);
      _capsuleCount = TakeAllFromQueueAndClearLocked(_capsules, ref _capsuleArray);
    }

    /// <summary>
    /// Obsolete
    /// </summary>
    [Obsolete("Moved to OnPostRender because the debug shape drawing is now using GL commands")]
    public static void DrawAll() {
    }

    /// <summary>
    /// Must be called from Unity callback OnPostRenderInternal to draw all debug shapes on top of everything else.
    /// </summary>
    /// <param name="camera">The scene camera</param>
    public static void OnPostRender(Camera camera) {
      if (IsEnabled == false) {
        return;
      }

      for (Int32 i = 0; i < _raysCount; ++i) {
        DrawRay(_raysArray[i]);
      }

      for (Int32 i = 0; i < _linesCount; ++i) {
        DrawLine(_linesArray[i]);
      }

      for (Int32 i = 0; i < _circlesCount; ++i) {
        DrawCircle(_circlesArray[i]);
      }

      for (Int32 i = 0; i < _spheresCount; ++i) {
        DrawSphere(_spheresArray[i]);
      }

      for (Int32 i = 0; i < _rectanglesCount; ++i) {
        DrawRectangle(_rectanglesArray[i]);
      }

      for (Int32 i = 0; i < _boxesCount; ++i) {
        DrawCube(_boxesArray[i]);
      }

      for (Int32 i = 0; i < _capsuleCount; ++i) {
        DrawCapsule(_capsuleArray[i]);
      }
    }

    static void DrawRay(Draw.DebugRay ray) {
      GetMaterial(ray.Color).SetPass(0);
      GL.PushMatrix();
      GL.Begin(GL.LINES);
      GL.Vertex(ray.Origin.ToUnityVector3(true));
      GL.Vertex(ray.Origin.ToUnityVector3(true) + ray.Direction.ToUnityVector3(true));
      GL.End();
      GL.PopMatrix();
    }

    static void DrawLine(Draw.DebugLine line) {
      GetMaterial(line.Color).SetPass(0);
      GL.PushMatrix();
      GL.Begin(GL.LINES);
      GL.Vertex(line.Start.ToUnityVector3(true));
      GL.Vertex(line.End.ToUnityVector3(true));
      GL.End();
      GL.PopMatrix();
    }

    static void DrawSphere(Draw.DebugSphere sphere) {
      Matrix4x4 mat = Matrix4x4.TRS(sphere.Center.ToUnityVector3(true), Quaternion.identity, Vector3.one * (sphere.Radius.AsFloat + sphere.Radius.AsFloat));
      GetMaterial(sphere.Color).SetPass(0);
      GL.wireframe = sphere.Wire;
      Graphics.DrawMeshNow(QuantumMeshCollection.Global.Sphere, mat);
      GL.wireframe = false;
    }

    static void DrawCircle(Draw.DebugCircle circle) {
      GetMaterial(circle.Color).SetPass(0);

      if (circle.Wire) {
        var m = Matrix4x4.TRS(circle.Center.ToUnityVector3(true), circle.Rotation.ToUnityQuaternion(true), Vector3.one);
        GL.PushMatrix();
        GL.MultMatrix(m);
        GL.Begin(GL.LINE_STRIP);
#if QUANTUM_XY
        for (int i = 0; i < CirclePoints.Length; i++) {
          GL.Vertex3(CirclePoints[i].x * circle.Radius.AsFloat, CirclePoints[i].z * circle.Radius.AsFloat, 0.0f);
        }
        GL.Vertex3(CirclePoints[0].x * circle.Radius.AsFloat, CirclePoints[0].z * circle.Radius.AsFloat, 0.0f);
#else
        for (int i = 0; i < CirclePoints.Length; i++) {
          GL.Vertex3(CirclePoints[i].x * circle.Radius.AsFloat, 0.0f, CirclePoints[i].z * circle.Radius.AsFloat);
        }
        GL.Vertex3(CirclePoints[0].x * circle.Radius.AsFloat, 0.0f, CirclePoints[0].z * circle.Radius.AsFloat);
#endif
        GL.End();
        GL.PopMatrix();
      } else {
        Quaternion rot = Quaternion.identity;
#if QUANTUM_XY
        rot = Quaternion.Euler(180, 0, 0);
#else
        // TODO: Use non-XY circle as default
        rot = Quaternion.Euler(-90, 0, 0);
#endif
        var m = Matrix4x4.TRS(circle.Center.ToUnityVector3(true), circle.Rotation.ToUnityQuaternion(true) * rot, Vector3.one * (circle.Radius.AsFloat + circle.Radius.AsFloat));
        Graphics.DrawMeshNow(QuantumMeshCollection.Global.CircleXY, m);
      }
    }

    static void DrawRectangle(Draw.DebugRectangle rectangle) {
      GetMaterial(rectangle.Color).SetPass(0);

      if (rectangle.Is2D) {
        GL.MultMatrix(Matrix4x4.TRS(rectangle.Center.ToUnityVector3(true), rectangle.Rotation2D.ToUnityQuaternion(), rectangle.Size.ToUnityVector3(true)));
      } else {
        GL.MultMatrix(Matrix4x4.TRS(rectangle.Center.ToUnityVector3(true), rectangle.Rotation.ToUnityQuaternion(true), rectangle.Size.ToUnityVector3(true)));
      }

      GL.PushMatrix();
#if QUANTUM_XY
      if (rectangle.Wire) {
        GL.Begin(GL.LINE_STRIP);
        GL.Vertex3(0.5f, -0.5f, 0.0f);
        GL.Vertex3(-0.5f, -0.5f, 0.0f);
        GL.Vertex3(-0.5f, 0.5f, 0.0f);
        GL.Vertex3(0.5f, 0.5f, 0.0f);
        GL.Vertex3(0.5f, -0.5f, 0.0f);
      } else {
        GL.Begin(GL.QUADS);
        GL.Vertex3(0.5f, -0.5f, 0.0f);
        GL.Vertex3(-0.5f, -0.5f, 0.0f);
        GL.Vertex3(-0.5f, 0.5f, 0.0f);
        GL.Vertex3(0.5f, 0.5f, 0.0f);
      }
#else
      if (rectangle.Wire) {
        GL.Begin(GL.LINE_STRIP);
        GL.Vertex3(0.5f, 0.0f, -0.5f);
        GL.Vertex3(-0.5f, 0.0f, -0.5f);
        GL.Vertex3(-0.5f, 0.0f, 0.5f);
        GL.Vertex3(0.5f, 0.0f, 0.5f);
        GL.Vertex3(0.5f, 0.0f, -0.5f);
      } else {
        GL.Begin(GL.QUADS);
        GL.Vertex3(0.5f, 0.0f, -0.5f);
        GL.Vertex3(-0.5f, 0.0f, -0.5f);
        GL.Vertex3(-0.5f, 0.0f, 0.5f);
        GL.Vertex3(0.5f, 0.0f, 0.5f);
      }
#endif
      GL.End();
      GL.PopMatrix();
    }

    static void DrawCube(Draw.DebugBox cube) {
      GetMaterial(cube.Color).SetPass(0);

      var m = Matrix4x4.TRS(cube.Center.ToUnityVector3(true), cube.Rotation.ToUnityQuaternion(true), cube.Size.ToUnityVector3(true));

      if (cube.Wire) {
        GL.PushMatrix();
        GL.MultMatrix(m);
        GL.Begin(GL.LINE_STRIP);
        // top
        GL.Vertex3(0.5f, 0.5f, -0.5f);
        GL.Vertex3(-0.5f, 0.5f, -0.5f);
        GL.Vertex3(-0.5f, 0.5f, 0.5f);
        GL.Vertex3(0.5f, 0.5f, 0.5f);
        GL.Vertex3(0.5f, 0.5f, -0.5f);
        // bottom
        GL.Vertex3(0.5f, -0.5f, -0.5f);
        GL.Vertex3(-0.5f, -0.5f, -0.5f);
        GL.Vertex3(-0.5f, -0.5f, 0.5f);
        GL.Vertex3(0.5f, -0.5f, 0.5f);
        GL.Vertex3(0.5f, -0.5f, -0.5f);
        GL.End();
        // missing lines
        GL.Begin(GL.LINES);
        GL.Vertex3(-0.5f, 0.5f, -0.5f);
        GL.Vertex3(-0.5f, -0.5f, -0.5f);
        GL.Vertex3(-0.5f, 0.5f, 0.5f);
        GL.Vertex3(-0.5f, -0.5f, 0.5f);
        GL.Vertex3(0.5f, 0.5f, 0.5f);
        GL.Vertex3(0.5f, -0.5f, 0.5f);
        GL.End();
        GL.PopMatrix();
      } else {
        // TODO: QUADS would also work
        Graphics.DrawMeshNow(QuantumMeshCollection.Global.Cube, m);
      }
    }

    static void DrawCapsule(Draw.DebugCapsule capsule) {
      GetMaterial(capsule.Color).SetPass(0);

      if (capsule.Is2D) {
        var m = Matrix4x4.TRS(capsule.Center.ToUnityVector3(true), capsule.Rotation.ToUnityQuaternion(true), Vector3.one);

        // TODO: solid capsule shape, should probably be done with a texture
        //if (capsule.Wire) {
        GL.PushMatrix();
        GL.MultMatrix(m);
        GL.Begin(GL.LINE_STRIP);

        var extent = capsule.Extent.AsFloat;

        for (int i = 0; i < CircleResolution / 2; i++) {
#if QUANTUM_XY
          GL.Vertex3(CirclePoints[i].x * capsule.Radius.AsFloat, CirclePoints[i].z * capsule.Radius.AsFloat + extent, 0.0f);
#else
          GL.Vertex3(CirclePoints[i].x * capsule.Radius.AsFloat, 0.0f, CirclePoints[i].z * capsule.Radius.AsFloat + extent);
#endif
        }

#if QUANTUM_XY
        GL.Vertex3(-capsule.Radius.AsFloat, extent, 0.0f);
        GL.Vertex3(-capsule.Radius.AsFloat, -extent, 0.0f);
#else
        GL.Vertex3(-capsule.Radius.AsFloat, 0.0f, extent);
        GL.Vertex3(-capsule.Radius.AsFloat, 0.0f, -extent);
#endif

        for (int i = CircleResolution / 2; i < CircleResolution; i++) {
#if QUANTUM_XY
          GL.Vertex3(CirclePoints[i].x * capsule.Radius.AsFloat, CirclePoints[i].z * capsule.Radius.AsFloat - extent, 0.0f);
#else
          GL.Vertex3(CirclePoints[i].x * capsule.Radius.AsFloat, 0.0f, CirclePoints[i].z * capsule.Radius.AsFloat - extent);
#endif
        }

#if QUANTUM_XY
        GL.Vertex3(capsule.Radius.AsFloat, -extent, 0.0f);
        GL.Vertex3(capsule.Radius.AsFloat, extent, 0.0f);
#else
        GL.Vertex3(capsule.Radius.AsFloat, 0.0f, -extent);
        GL.Vertex3(capsule.Radius.AsFloat, 0.0f, extent);
#endif

        GL.End();
        GL.PopMatrix();
      } else {
        if (capsule.Wire) {
          GL.PushMatrix();

#if QUANTUM_XY
          var r = Quaternion.Euler(90, 0, 0);
#else
          var r = Quaternion.identity;
#endif

          var m = Matrix4x4.TRS(capsule.Center.ToUnityVector3(true), capsule.Rotation.ToUnityQuaternion(true) * r, Vector3.one);
          GL.MultMatrix(m);
          Draw2DCircle(new Vector3(0, capsule.Extent.AsFloat, 0), capsule.Radius.AsFloat);
          Draw2DCircle(new Vector3(0, -capsule.Extent.AsFloat, 0), capsule.Radius.AsFloat);

#if QUANTUM_XY
          r = Quaternion.identity;
#else
          r = Quaternion.Euler(90, 0, 0);
#endif

          m = Matrix4x4.TRS(capsule.Center.ToUnityVector3(true), capsule.Rotation.ToUnityQuaternion(true) * r, Vector3.one);
          GL.MultMatrix(m);
          Draw2DCapsuleShape(capsule.Extent.AsFloat, capsule.Radius.AsFloat);

#if QUANTUM_XY
          r = Quaternion.Euler(0, 90, 0);
#else
          r = Quaternion.Euler(90, 0, 90);
#endif

          m = Matrix4x4.TRS(capsule.Center.ToUnityVector3(true), capsule.Rotation.ToUnityQuaternion(true) * r, Vector3.one);
          GL.MultMatrix(m);
          Draw2DCapsuleShape(capsule.Extent.AsFloat, capsule.Radius.AsFloat);

          GL.PopMatrix();
        } else {
          var height = capsule.Height.AsFloat / 2.0f;
          var diameter = capsule.Diameter.AsFloat;
          var m = Matrix4x4.TRS(capsule.Center.ToUnityVector3(true), capsule.Rotation.ToUnityQuaternion(true), (Vector3.up * height) + (Vector3.right + Vector3.forward) * diameter);
          Graphics.DrawMeshNow(QuantumMeshCollection.Global.Capsule, m);
        }
      }
    }

    static void Draw2DCapsuleShape(float extent, float radius) {
      GL.Begin(GL.LINE_STRIP);

      for (int i = 0; i < CircleResolution / 2; i++) {
#if QUANTUM_XY
        GL.Vertex3(CirclePoints[i].x * radius, CirclePoints[i].z * radius + extent, 0.0f);
#else
        GL.Vertex3(CirclePoints[i].x * radius, 0.0f, CirclePoints[i].z * radius + extent);
#endif
      }

#if QUANTUM_XY
      GL.Vertex3(-radius, extent, 0.0f);
      GL.Vertex3(-radius, -extent, 0.0f);
#else
      GL.Vertex3(-radius, 0.0f, extent);
      GL.Vertex3(-radius, 0.0f, -extent);
#endif

      for (int i = CircleResolution / 2; i < CircleResolution; i++) {
#if QUANTUM_XY
        GL.Vertex3(CirclePoints[i].x * radius, CirclePoints[i].z * radius - extent, 0.0f);
#else
        GL.Vertex3(CirclePoints[i].x * radius, 0.0f, CirclePoints[i].z * radius - extent);
#endif
      }

#if QUANTUM_XY
      GL.Vertex3(radius, -extent, 0.0f);
      GL.Vertex3(radius, extent, 0.0f);
#else
      GL.Vertex3(radius, 0.0f, -extent);
      GL.Vertex3(radius, 0.0f, extent);
#endif

      GL.End();
    }

    static void Draw2DCircle(Vector3 center, float radius) {
      GL.Begin(GL.LINE_STRIP);
      var p = default(Vector3);
#if QUANTUM_XY
        for (int i = 0; i < CirclePoints.Length; i++) {
          p = CirclePoints[i] * radius;
          GL.Vertex3(p.x + center.x, p.z + center.z, p.y + center.y);
        }
        p = CirclePoints[0] * radius;
        GL.Vertex3(p.x + center.x, p.z + center.z, p.y + center.y);
#else
      for (int i = 0; i < CirclePoints.Length; i++) {
        p = CirclePoints[i] * radius;
        GL.Vertex3(p.x + center.x, p.y + center.y, p.z + center.z);
      }

      p = CirclePoints[0] * radius;
      GL.Vertex3(p.x + center.x, p.y + center.y, p.z + center.z);
#endif
      GL.End();
    }

    static Int32 TakeAllFromQueueAndClearLocked<T>(Queue<T> queue, ref T[] result) {
      lock (queue) {
        var count = 0;

        if (queue.Count > 0) {
          // if result array size is less than queue count
          if (result.Length < queue.Count) {
            // find the next new size that is a multiple of the current result size
            var newSize = result.Length;

            while (newSize < queue.Count) {
              newSize = newSize * 2;
            }

            // and re-size array
            Array.Resize(ref result, newSize);
          }

          // grab all
          while (queue.Count > 0) {
            result[count++] = queue.Dequeue();
          }

          // clear queue
          queue.Clear();
        }

        return count;
      }
    }
  }
}

#endregion


#region Assets/Photon/Quantum/Runtime/Utils/DebugMesh.cs

namespace Quantum {
  using System;
  using Photon.Analyzer;
  using UnityEngine;

  /// <summary>
  /// Access to Quantum debug mesh resources.
  /// Obsolete: has been replaced by QuantumMeshCollection.Global.
  /// </summary>
  [Obsolete("Use QuantumMeshCollection.Global instead")]
  public static class DebugMesh {
    [StaticField(StaticFieldResetMode.None)]
    private static Mesh _circleMesh;

    [StaticField(StaticFieldResetMode.None)]
    private static Mesh _sphereMesh;

    [StaticField(StaticFieldResetMode.None)]
    private static Mesh _quadMesh;

    [StaticField(StaticFieldResetMode.None)]
    private static Mesh _cylinderMesh;

    [StaticField(StaticFieldResetMode.None)]
    private static Mesh _cubeMesh;

    [StaticField(StaticFieldResetMode.None)]
    private static Mesh _capsuleMesh;

    [StaticField(StaticFieldResetMode.None)]
    private static Material _debugMaterial;

    [StaticField(StaticFieldResetMode.None)]
    private static Material _debugSolidMaterial;

    /// <summary>
    /// Load and return the QuantumGizmoCircleMesh asset and cache for later use.
    /// This circle mesh is aligned to the XY plane.
    /// </summary>
    public static Mesh CircleMesh {
      get {
        if (!_circleMesh) {
          _circleMesh = QuantumMeshCollection.Global.CircleXY;
        }

        return _circleMesh;
      }
    }

    /// <summary>
    /// Load and return the QuantumGizmoSphereMesh asset and cache for later use.
    /// </summary>
    public static Mesh SphereMesh {
      get {
        if (!_sphereMesh) {
          _sphereMesh = QuantumMeshCollection.Global.Sphere;
        }

        return _sphereMesh;
      }
    }

    /// <summary>
    /// Load and return the QuantumGizmoQuadMesh asset and cache for later use.
    /// </summary>
    public static Mesh QuadMesh {
      get {
        if (!_quadMesh) {
          _quadMesh = QuantumMeshCollection.Global.Quad;
        }

        return _quadMesh;
      }
    }

    /// <summary>
    /// Load and return the QuantumGizmoCubeMesh asset and cache for later use.
    /// </summary>
    public static Mesh CubeMesh {
      get {
        if (!_cubeMesh) {
          _cubeMesh = QuantumMeshCollection.Global.Cube;
        }

        return _cubeMesh;
      }
    }

    /// <summary>
    /// Load and return the QuantumGizmoCapsuleMesh asset and cache for later use.
    /// </summary>
    public static Mesh CapsuleMesh {
      get {
        if (!_capsuleMesh) {
          _capsuleMesh = QuantumMeshCollection.Global.Capsule;
        }

        return _capsuleMesh;
      }
    }

    /// <summary>
    /// Load and return the QuantumGizmoCylinderMesh asset and cache for later use.
    /// This cylinder mesh is aligned to the XY plane.
    /// </summary>
    public static Mesh CylinderMesh {
      get {
        if (!_cylinderMesh) {
          _cylinderMesh = QuantumMeshCollection.Global.CylinderXY;
        }

        return _cylinderMesh;
      }
    }

    /// <summary>
    /// The material used to draw transparent simulation debug shapes. 
    /// Replace by setting a material before it's ever used.
    /// </summary>
    public static Material DebugMaterial {
      get {
        if (!_debugMaterial) {
          _debugMaterial = QuantumMeshCollection.Global.DebugMaterial;
        }

        return _debugMaterial;
      }

      set {
        _debugMaterial = value;
      }
    }
  }
}

#endregion


#region Assets/Photon/Quantum/Runtime/Utils/EditorDefines.cs

namespace Quantum {
  using System;

  /// <summary>
  /// Quantum editor defines.
  /// </summary>
  public static class EditorDefines {
    /// <summary>
    /// Asset menu priority starting position.
    /// -1000 mean the items will be at the top of the list.
    /// </summary>
    public const int AssetMenuPriority               = -1000;
    /// <summary>
    /// Create assets menu order.
    /// </summary>
    public const int AssetMenuPriorityAssets         = AssetMenuPriority + 0;
    /// <summary>
    /// Create configuration menu order.
    /// </summary>
    public const int AssetMenuPriorityConfigurations = AssetMenuPriority + 100;
    /// <summary>
    /// Create scripts menu order.
    /// </summary>
    public const int AssetMenuPriorityScripts        = AssetMenuPriority + 200;

    /// <summary>
    /// Obsolete
    /// </summary>
    [Obsolete]
    public const int AssetMenuPrioritQtn             = AssetMenuPriorityScripts;
    /// <summary>
    /// Obsolete
    /// </summary>
    [Obsolete]
    public const int AssetMenuPriorityDemo           = AssetMenuPriority + 18;
    /// <summary>
    /// Obsolete
    /// </summary>
    [Obsolete]
    public const int AssetMenuPriorityStart          = AssetMenuPriority + 100;
  }
}

#endregion


#region Assets/Photon/Quantum/Runtime/Utils/FloatMinMax.cs

namespace Quantum {
  using System;
  using UnityEngine;

  /// <summary>
  /// A struct that holds min and max float values and comes with a few inspector tools
  /// </summary>
  [Serializable]
  public struct FloatMinMax {
    /// <summary>
    /// Min value.
    /// </summary>
    public Single Min;
    /// <summary>
    /// Max value.
    /// </summary>
    public Single Max;

    /// <summary>
    /// Create a new instance of FloatMinMax.
    /// </summary>
    /// <param name="min">Min</param>
    /// <param name="max">Max</param>
    public FloatMinMax(Single min, Single max) {
      Min = min;
      Max = max;
    }
  }

  /// <summary>
  /// An attribute to display a slider in the Unity inspector between a min and max value.
  /// </summary>
  [AttributeUsage(AttributeTargets.Field, Inherited = true, AllowMultiple = false)]
  public class MinMaxSliderAttribute : PropertyAttribute {
    /// <summary>
    /// Min value.
    /// </summary>
    public readonly float Min;
    /// <summary>
    /// Max value.
    /// </summary>
    public readonly float Max;

    /// <summary>
    /// Create a slider between 0 and 1.
    /// </summary>
    public MinMaxSliderAttribute()
      : this(0, 1) {
    }

    /// <summary>
    /// Create a slider between min and max.
    /// </summary>
    /// <param name="min">Min</param>
    /// <param name="max">Max</param>
    public MinMaxSliderAttribute(float min, float max) {
      Min = min;
      Max = max;
    }
  }
}

#endregion


#region Assets/Photon/Quantum/Runtime/Utils/FPMathUtils.cs

namespace Quantum {
  using System;
  using Photon.Deterministic;
  using UnityEngine;

  /// <summary>
  /// Unity specific FixedPoint math utilities.
  /// All conversions between Unity float into Quantum FP (e.g. Vector2 to FPVector2) 
  /// are considered non-deterministic and should never be used in the Quantum simulation directly.
  /// </summary>
  public static class FPMathUtils {
    /// <summary>
    /// Load the lookup tables from the resources folders.
    /// </summary>
    /// <param name="force">Will reload the table if set to true</param>
    public static void LoadLookupTables(Boolean force = false) {
      if (FPLut.IsLoaded && force == false) {
        return;
      }

      FPLut.Init(file => {
#if UNITY_EDITOR
        if (!Application.isPlaying) {
          var path = "Assets/Photon/Quantum/Resources/LUT/" + file + ".bytes";
          var textAsset = UnityEditor.AssetDatabase.LoadAssetAtPath<TextAsset>(path);
          if (textAsset) {
            return textAsset.bytes;
          }
        }
#endif
        return UnityEngine.Resources.Load<TextAsset>("LUT/" + file).bytes;
      });
    }

    /// <summary>
    /// Convert a float to an FP, with rounding towards zero.
    /// To round towards nearest representable FP, use <see cref="ToRoundedFP"/>.
    /// This is always considered to be unsafe and non-deterministic and should never be used in conjunction with the simulation.
    /// </summary>
    /// <seealso cref="FP.FromFloat_UNSAFE"/>
    public static FP ToFP(this Single v) {
      return FP.FromFloat_UNSAFE(v);
    }
    
    /// <summary>
    /// Convert a float to an FP, with rounding to the nearest representable FP.
    /// This is always considered to be unsafe and non-deterministic and should never be used in conjunction with the simulation.
    /// </summary>
    /// <seealso cref="FP.FromRoundedFloat_UNSAFE"/>
    public static FP ToRoundedFP(this Single v) {
      return FP.FromRoundedFloat_UNSAFE(v);
    }

    /// <summary>
    /// Inverts the FP if QUNATUM_XY is not defined.
    /// </summary>
    /// <param name="r">2D rotation</param>
    /// <returns>Inverted value.</returns>
    public static FP FlipRotation(this FP r) {
#if QUANTUM_XY
        return r;
#else
      return -r;
#endif
    }

    /// <summary>
    /// Create a Quaternion from a y rotation in degrees.
    /// Internally checks QUANTUM_XY to rotate around z instead.
    /// </summary>
    /// <param name="r">Rotation in degrees</param>
    /// <returns>Unity rotation</returns>
    public static Quaternion ToUnityQuaternionDegrees(this FP r) {
#if QUANTUM_XY
        return Quaternion.Euler(0, 0, r.AsFloat);
#else
      return Quaternion.Euler(0, -r.AsFloat, 0);
#endif
    }

    /// <summary>
    /// Creates a Quaternion from a y rotation in radians.
    /// Internally checks QUANTUM_XY to rotate around z instead.
    /// </summary>
    /// <param name="r">Rotation in radians</param>
    /// <returns>Unity rotation</returns>
    public static Quaternion ToUnityQuaternion(this FP r) {
#if QUANTUM_XY
        return Quaternion.Euler(0, 0, (r * FP.Rad2Deg).AsFloat);
#else
      return Quaternion.Euler(0, -(r * FP.Rad2Deg).AsFloat, 0);
#endif
    }

    /// <summary>
    /// Converts a Quantum FPQuaternion to a Unity Quaternion.
    /// </summary>
    /// <param name="r">Rotation</param>
    /// <returns>Unity rotation</returns>
    public static Quaternion ToUnityQuaternion(this FPQuaternion r) {
      Quaternion q;

      q.x = r.X.AsFloat;
      q.y = r.Y.AsFloat;
      q.z = r.Z.AsFloat;
      q.w = r.W.AsFloat;


      // calculate square magnitude
      var sqr = Mathf.Sqrt(Quaternion.Dot(q, q));
      if (sqr < Mathf.Epsilon) {
        return Quaternion.identity;
      }

      q.x /= sqr;
      q.y /= sqr;
      q.z /= sqr;
      q.w /= sqr;

      return q;
    }

    /// <summary>
    /// Converts a Quantum FPQuaternion to a Unity Quaternion with swizzling the y and z axis.
    /// </summary>
    /// <param name="r">Rotation</param>
    /// <param name="swizzle">True if the rotation should swizzle</param>
    /// <returns>Unity rotation</returns>
    public static Quaternion ToUnityQuaternion(this FPQuaternion r, bool swizzle) {
      var euler = r.AsEuler.ToUnityVector3(swizzle);
      return Quaternion.Euler(euler);
    }

    /// <summary>
    /// Convert a Unity quaternion to a Quantum FPQuaternion.
    /// </summary>
    /// <param name="r">Rotation</param>
    /// <returns>Quantum rotation</returns>
    public static FPQuaternion ToFPQuaternion(this Quaternion r) {
      FPQuaternion q;

      q.X = r.x.ToFP();
      q.Y = r.y.ToFP();
      q.Z = r.z.ToFP();
      q.W = r.w.ToFP();

      return q;
    }

    /// <summary>
    /// Converts a Unity quaternion to 2D rotation in degrees by only using the y 
    /// (or z axis if QUANTUM_XY is defined)
    /// </summary>
    /// <param name="r">Unity rotation</param>
    /// <returns>2D rotation in degree</returns>
    public static FP ToFPRotation2DDegrees(this Quaternion r) {
#if QUANTUM_XY
        return FP.FromFloat_UNSAFE(r.eulerAngles.z);
#else
      return -FP.FromFloat_UNSAFE(r.eulerAngles.y);
#endif
    }

    /// <summary>
    /// Converts a Unity quaternion to 2D rotation in radians by only using the y 
    /// (or z axis if QUANTUM_XY is defined)
    /// </summary>
    /// <param name="r">Unity rotation</param>
    /// <returns>2D rotation in radian</returns>
    public static FP ToFPRotation2D(this Quaternion r) {
#if QUANTUM_XY
        return FP.FromFloat_UNSAFE(r.eulerAngles.z * Mathf.Deg2Rad);
#else
      return -FP.FromFloat_UNSAFE(r.eulerAngles.y * Mathf.Deg2Rad);
#endif
    }

    /// <summary>
    /// Converts a Unity Vector2 to a Quantum FPVector2, with each component being rounded towards zero.
    /// To round towards the nearest representable FP, use <see cref="ToRoundedFPVector2"/>.
    /// </summary>
    /// <param name="v">Unity vector2</param>
    /// <returns>Quantum vector2</returns>
    public static FPVector2 ToFPVector2(this Vector2 v) {
      return new FPVector2(v.x.ToFP(), v.y.ToFP());
    }

    /// <summary>
    /// Converts a Quantum FPVector2 to a Unity Vector2.
    /// </summary>
    /// <param name="v">Quantum vector2</param>
    /// <returns>Unity vector2</returns>
    public static Vector2 ToUnityVector2(this FPVector2 v) {
      return new Vector2(v.X.AsFloat, v.Y.AsFloat);
    }

    /// <summary>
    /// Converts a Unity Vector3 to a Quantum FPVector2 by removing the y component 
    /// (removing the z component if QUANTUM_XY is defined). Each component is rounded towards zero.
    /// </summary>
    /// <param name="v">Unity vector3</param>
    /// <returns>Quantum vector2</returns>
    /// <seealso cref="FP.FromFloat_UNSAFE"/>
    public static FPVector2 ToFPVector2(this Vector3 v) {
#if QUANTUM_XY
      return new FPVector2(v.x.ToFP(), v.y.ToFP());
#else
      return new FPVector2(v.x.ToFP(), v.z.ToFP());
#endif
    }

    /// <summary>
    /// Converts a Unity Vector3 to a Quantum FPVector2 by removing the y component 
    /// (removing the z component if QUANTUM_XY is defined). Each component is rounded towards
    /// the nearest representable FP.
    /// </summary>
    /// <param name="v">Unity vector3</param>
    /// <returns>Quantum vector2</returns>
    /// <seealso cref="FP.FromRoundedFloat_UNSAFE"/>
    public static FPVector2 ToRoundedFPVector2(this Vector3 v) {
#if QUANTUM_XY
      return new FPVector2(v.x.ToRoundedFP(), v.y.ToRoundedFP());
#else
      return new FPVector2(v.x.ToRoundedFP(), v.z.ToRoundedFP());
#endif
    }
    
    /// <summary>
    /// Extracts the vertical position of a Unity Vector3 and converts it to a Quantum FP.
    /// Will use the inverse z component if QUANTUM_XY is defined.
    /// </summary>
    /// <param name="v">Unity vector3</param>
    /// <returns>The height component</returns>
    public static FP ToFPVerticalPosition(this Vector3 v) {
#if QUANTUM_XY
        return -v.z.ToFP();
#else
      return v.y.ToFP();
#endif
    }

    /// <summary>
    /// Converts a Unity Vector3 to a Quantum FPVector3, with each component being rounded towards zero.
    /// </summary>
    /// <param name="v">Unity vector3</param>
    /// <returns>Quantum vector3</returns>
    public static FPVector3 ToFPVector3(this Vector3 v) {
      return new FPVector3(v.x.ToFP(), v.y.ToFP(), v.z.ToFP());
    }

    /// <summary>
    /// Converts a Quantum IntVector3 object to a Unity Vector3Int.
    /// </summary>
    /// <param name="v">The IntVector3 object to convert.</param>
    /// <returns>The converted Unity Vector3Int.</returns>
    public static Vector3Int ToVector3Int(this IntVector3 v) {
      return new Vector3Int(v.X, v.Y, v.Z);
    }

    /// <summary>
    /// Converts a Quantum IntVector2 into a Unity Vector2Int.
    /// </summary>
    /// <param name="v">The Quantum IntVector2 to convert.</param>
    /// <returns>The converted Unity Vector2Int.</returns>
    public static Vector2Int ToVector2Int(this IntVector2 v) {
      return new Vector2Int(v.X, v.Y);
    }

    /// <summary>
    /// Converts a Unity Matrix4x4 to a Quantum FPMatrix4x4, with each component being rounded towards the nearest representable FP.
    /// </summary>
    /// <param name="m">The Unity Matrix4x4</param>
    /// <returns>Quantum Matrix4x4</returns>
    public static FPMatrix4x4 ToFPMatrix4X4(this Matrix4x4 m) {
      return new FPMatrix4x4 {
        M00 = m.m00.ToFP(),
        M01 = m.m01.ToFP(),
        M02 = m.m02.ToFP(),
        M03 = m.m03.ToFP(),
        
        M10 = m.m10.ToFP(),
        M11 = m.m11.ToFP(),
        M12 = m.m12.ToFP(),
        M13 = m.m13.ToFP(),
        
        M20 = m.m20.ToFP(),
        M21 = m.m21.ToFP(),
        M22 = m.m22.ToFP(),
        M23 = m.m23.ToFP(),
        
        M30 = m.m30.ToFP(),
        M31 = m.m31.ToFP(),
        M32 = m.m32.ToFP(),
        M33 = m.m33.ToFP(),
      };
    }

    /// <summary>
    /// Converts a Unity vector3 to a Quantum FPVector3, with each component being rounded towards the nearest representable FP.
    /// </summary>
    /// <param name="v">Unity vector3</param>
    /// <returns>Quantum vector3</returns>
    public static FPVector3 ToRoundedFPVector3(this Vector3 v) {
      return new FPVector3(v.x.ToRoundedFP(), v.y.ToRoundedFP(), v.z.ToRoundedFP());
    }
    
    /// <summary>
    /// Converts a Quantum vector2 to a Unity vector3 by setting the y component to 0.
    /// (sets the z component to 0 if QUANTUM_XY is defined).
    /// </summary>
    /// <param name="v">Quantum vector2</param>
    /// <returns>Unity vector3</returns>
    public static Vector3 ToUnityVector3(this FPVector2 v) {
#if QUANTUM_XY
        return new Vector3(v.X.AsFloat, v.Y.AsFloat, 0);
#else
      return new Vector3(v.X.AsFloat, 0, v.Y.AsFloat);
#endif
    }

    /// <summary>
    /// Converts a Quantum vector3 to a Unity vector3.
    /// </summary>
    /// <param name="v">Quantum vector3</param>
    /// <returns>Unity vector3</returns>
    public static Vector3 ToUnityVector3(this FPVector3 v) {
      return new Vector3(v.X.AsFloat, v.Y.AsFloat, v.Z.AsFloat);
    }

    /// <summary>
    ///   Use this version of ToUnityVector3() when converting a 3D position from the XZ plane in the simulation to the 2D XY
    ///   plane in Unity.
    /// </summary>
    public static Vector3 ToUnityVector3(this FPVector3 v, bool quantumXYSwizzle) {
#if QUANTUM_XY
        if (quantumXYSwizzle) { 
            return new Vector3(v.X.AsFloat, v.Z.AsFloat, v.Y.AsFloat);
        }
#endif

      return new Vector3(v.X.AsFloat, v.Y.AsFloat, v.Z.AsFloat);
    }

    /// <summary>
    /// Obsolete
    /// </summary>
    [Obsolete("unused and unusual convention")]
    public static Vector2 ToUnityVector2(this FPVector3 v) {
      return new Vector2(v.X.AsFloat, v.Y.AsFloat);
    }

    /// <summary>
    /// Rounds all components of the Unity vector to the nearest integer.
    /// </summary>
    /// <param name="v">Unity vector3</param>
    /// <returns>The rounded vector</returns>
    public static Vector3 RoundToInt(this Vector3 v) {
      v.x = Mathf.RoundToInt(v.x);
      v.y = Mathf.RoundToInt(v.y);
      v.z = Mathf.RoundToInt(v.z);
      return v;
    }

    /// <summary>
    /// Rounds all components of the Unity vector to the nearest integer.
    /// </summary>
    /// <param name="v">Unity vector2</param>
    /// <returns>The rounded vector</returns>
    public static Vector2 RoundToInt(this Vector2 v) {
      v.x = Mathf.RoundToInt(v.x);
      v.y = Mathf.RoundToInt(v.y);
      return v;
    }

    /// <summary>
    /// Obsolete
    /// </summary>
    [Obsolete]
    public static Color32 ToColor32(this ColorRGBA clr) {
      return (Color32)clr;
    }

    /// <summary>
    /// Obsolete
    /// </summary>
    [Obsolete]
    public static Color ToColor(this ColorRGBA clr) {
      return clr.AsColor;
    }
  }
}

#endregion


#region Assets/Photon/Quantum/Runtime/Utils/GameObjectUtils.cs

namespace Quantum {
  using UnityEngine;
  using UnityEngine.UI;

  /// <summary>
  /// Unity game object utility methods for Quantum.
  /// </summary>
  public static class GameObjectUtils {
    /// <summary>
    /// Showing will <see cref="GameObject.SetActive(bool)"/> to true on all game objects in the array.
    /// </summary>
    /// <param name="gameObjects">List of game objects to process</param>
    public static void Show(this GameObject[] gameObjects) {
      if (gameObjects != null) {
        for (int i = 0; i < gameObjects.Length; ++i) {
          gameObjects[i].SetActive(true);
        }
      }
    }
    /// <summary>
    /// Hiding will <see cref="GameObject.SetActive(bool)"/> to false on all game objects in the array.
    /// </summary>
    /// <param name="gameObjects">List of game objects to process</param>
    public static void Hide(this GameObject[] gameObjects) {
      if (gameObjects != null) {
        for (int i = 0; i < gameObjects.Length; ++i) {
          gameObjects[i].SetActive(false);
        }
      }
    }

    /// <summary>
    /// Will <see cref="GameObject.SetActive(bool)"/> to true if the game object is not null and not already active.
    /// </summary>
    /// <param name="gameObject">Game object to show</param>
    public static void Show(this GameObject gameObject) {
      if (gameObject && !gameObject.activeSelf) {
        gameObject.SetActive(true);
      }
    }

    /// <summary>
    /// Will <see cref="GameObject.SetActive(bool)"/> to false if the game object is not null and is active."/>
    /// </summary>
    /// <param name="gameObject">Game object to hide</param>
    public static void Hide(this GameObject gameObject) {
      if (gameObject && gameObject.activeSelf) {
        gameObject.SetActive(false);
      }
    }

    /// <summary>
    /// Toggle the game object's active state after checking for null.
    /// </summary>
    /// <param name="gameObject">Game object to toggle</param>
    /// <returns>Returns the final state of the game object active state of false if null</returns>
    public static bool Toggle(this GameObject gameObject) {
      if (gameObject) {
        return gameObject.Toggle(!gameObject.activeSelf);
      }

      return false;
    }

    /// <summary>
    /// Set game object active state into the desired state after checking for null.
    /// </summary>
    /// <param name="gameObject">Game object to toggle</param>
    /// <param name="state">The state to toggle into </param>
    /// <returns>Returns the final game object active state or false when null</returns>
    public static bool Toggle(this GameObject gameObject, bool state) {
      if (gameObject) {
        if (gameObject.activeSelf != state) {
          gameObject.SetActive(state);
        }

        return state;
      }

      return false;
    }

    /// <summary>
    /// Set the component's game object active state into the desired state after checking for null.
    /// </summary>
    /// <param name="component">Component to toggle its game object</param>
    /// <param name="state">The desired active state</param>
    /// <returns>The final active state of the components game object or false if null</returns>
    public static bool Toggle(this Component component, bool state) {
      if (component) {
        return component.gameObject.Toggle(state);
      }

      return false;
    }

    /// <summary>
    /// Sets the component game object to active after checking for null.
    /// </summary>
    /// <param name="component">Input component</param>
    public static void Show(this Component component) {
      if (component) {
        component.gameObject.Show();
      }
    }

    /// <summary>
    /// Sets the image sprite and sets the game object to active after checking the component for null.
    /// </summary>
    /// <param name="component">Image component</param>
    /// <param name="sprite">Sprite to set</param>
    public static void Show(this Image component, Sprite sprite) {
      if (component) {
        component.sprite = sprite;
        component.gameObject.SetActive(true);
      }
    }

    /// <summary>
    /// Set the component game object to inactive after checking for null.
    /// </summary>
    /// <param name="component">Input component</param>
    public static void Hide(this Component component) {
      if (component) {
        component.gameObject.Hide();
      }
    }

    /// <summary>
    /// Set all game objects found the component list to active after checking for null.
    /// </summary>
    /// <typeparam name="T">Type must be derived from component</typeparam>
    /// <param name="components">Component list to enabled game objects on</param>
    public static void Show<T>(this T[] components) where T : Component {
      if (components != null) {
        for (int i = 0; i < components.Length; ++i) {
          if (components[i].gameObject.activeSelf == false) {
            components[i].gameObject.SetActive(true);
          }
        }
      }
    }

    /// <summary>
    /// Set all game objects found the component list to in-active after checking for null.
    /// </summary>
    /// <typeparam name="T">Type must be derived from component</typeparam>
    /// <param name="components">Component list to disable game objects on</param>
    public static void Hide<T>(this T[] components) where T : Component {
      if (components != null) {
        for (int i = 0; i < components.Length; ++i) {
          if (components[i].gameObject.activeSelf) {
            components[i].gameObject.SetActive(false);
          }
        }
      }
    }
  }
}

#endregion


#region Assets/Photon/Quantum/Runtime/Utils/GizmoUtils.cs

namespace Quantum {
  using System;
  using System.Linq;
  using Photon.Deterministic;
  using UnityEditor;
  using UnityEngine;

  /// <summary>
  /// Utility class for drawing gizmos.
  /// </summary>
  public static class GizmoUtils {
    /// <summary>
    /// Adjusts the alpha channel of the color.
    /// </summary>
    /// <param name="color">The color to adjust.</param>
    /// <param name="a">The new alpha value.</param>
    /// <returns>The color with the alpha value adjusted.</returns>
    public static Color Alpha(this Color color, Single a) {
      color.a = a;
      return color;
    }

    /// <summary>
    /// Adjusts the brightness of a color.
    /// </summary>
    /// <param name="color">The color to adjust.</param>
    /// <param name="brightness">The brightness value. Values greater than 1 will increase brightness, while values less than 1 will decrease brightness.</param>
    /// <returns>The adjusted color with the specified brightness.</returns>
    public static Color Brightness(this Color color, float brightness) {
      Color.RGBToHSV(color, out var h, out var s, out var v);
      return Color.HSVToRGB(h, s, v * brightness).Alpha(color.a);
    }

    /// <summary>
    /// The default arrow head length.
    /// </summary>
    public const float DefaultArrowHeadLength = 0.25f;
    
    /// <summary>
    /// The default arrow head angle.
    /// </summary>
    public const float DefaultArrowHeadAngle  = 25.0f;

    /// <summary>
    /// Draws a gizmo box in the scene using the specified parameters.
    /// </summary>
    /// <param name="transform">The transform of the gizmo box.</param>
    /// <param name="size">The size of the gizmo box.</param>
    /// <param name="color">The color of the gizmo box.</param>
    /// <param name="offset">The offset position for the gizmo box (default: Vector3.zero).</param>
    /// <param name="style">The gizmo style to apply (default: QuantumGizmoStyle default value).</param>
    public static void DrawGizmosBox(Transform transform, Vector3 size, Color color, Vector3 offset = default, QuantumGizmoStyle style = default) {
      var matrix = transform.localToWorldMatrix * Matrix4x4.Translate(offset);
      DrawGizmosBox(matrix, size, color, style: style);
    }

    /// <summary>
    /// Draws a box gizmo with the given center, size, color, rotation, and style.
    /// </summary>
    /// <param name="center">The center position of the box.</param>
    /// <param name="size">The size of the box.</param>
    /// <param name="color">The color of the box.</param>
    /// <param name="rotation">The rotation of the box. Defaults to identity rotation if not provided.</param>
    /// <param name="style">The style of the gizmo. Defaults to default style if not provided.</param>
    public static void DrawGizmosBox(Vector3 center, Vector3 size, Color color, Quaternion? rotation = null, QuantumGizmoStyle style = default) {
      var matrix = Matrix4x4.TRS(center, rotation ?? Quaternion.identity, Vector3.one);
      DrawGizmosBox(matrix, size, color, style: style);
    }

    /// <summary>
    /// Draws a 2D capsule gizmo.
    /// </summary>
    /// <param name="center">The center of the capsule.</param>
    /// <param name="radius">The radius of the capsule.</param>
    /// <param name="height">The height of the capsule.</param>
    /// <param name="color">The color of the gizmo.</param>
    /// <param name="rotation">The rotation of the capsule. If null, identity rotation is used.</param>
    /// <param name="style">The style of the gizmo. If not provided, default style is used.</param>
    public static void DrawGizmosCapsule2D(Vector3 center, float radius, float height, Color color, Quaternion? rotation = null, QuantumGizmoStyle style = default) {
#if UNITY_EDITOR

      var matrix = Matrix4x4.TRS(center, rotation ?? Quaternion.identity, Vector3.one);

      Handles.matrix = matrix;
      Gizmos.color = color;
      Handles.color = Gizmos.color;
#if QUANTUM_XY
      var left = Vector3.left * radius;
      var right = Vector3.right * radius;
      Handles.DrawLine(left + Vector3.up * height, left + Vector3.down * height);
      Handles.DrawLine(right + Vector3.up * height, right + Vector3.down * height);
      Handles.DrawWireArc(Vector3.up * height, Vector3.forward, Vector3.right * radius, 180, radius);
      Handles.DrawWireArc(Vector3.down * height, Vector3.back, Vector3.left * radius, -180, radius);
#else
      var left = Vector3.left * radius;
      var right = Vector3.right * radius;
      Handles.DrawLine(left + Vector3.back * height, left + Vector3.forward * height);
      Handles.DrawLine(right + Vector3.back * height, right + Vector3.forward * height);
      Handles.DrawWireArc(Vector3.back * height, Vector3.up, Vector3.right * radius, 180, radius);
      Handles.DrawWireArc(Vector3.forward * height, Vector3.down, Vector3.left * radius, -180, radius);
#endif
      matrix = Matrix4x4.identity;
      Handles.color = Gizmos.color = Color.white;
      Handles.matrix = matrix;
#endif

    }

    /// <summary>
    /// Draws a box gizmo in the scene using the specified parameters.
    /// </summary>
    /// <param name="matrix">The matrix of the gizmo box.</param>
    /// <param name="size">The size of the box.</param>
    /// <param name="color">The color of the box.</param>
    /// <param name="style">The style of the gizmo. (Optional)</param>
    public static void DrawGizmosBox(Matrix4x4 matrix, Vector3 size, Color color, QuantumGizmoStyle style = default) {
      Gizmos.matrix = matrix;

      if (style.IsFillEnabled) {
        Gizmos.color = color;
        Gizmos.DrawCube(Vector3.zero, size);
      }

      if (style.IsWireframeEnabled) {
        Gizmos.color = color;
        Gizmos.DrawWireCube(Vector3.zero, size);
      }

      Gizmos.matrix = Matrix4x4.identity;
      Gizmos.color  = Color.white;
    }

    /// <summary>
    /// Draws a gizmo circle at the specified position with the given radius and color.
    /// </summary>
    /// <param name="position">The position of the circle.</param>
    /// <param name="radius">The radius of the circle.</param>
    /// <param name="color">The color of the circle.</param>
    /// <param name="height">The height of the circle. Default is 0.0.</param>
    /// <param name="style">The style of the gizmo. Default is QuantumGizmoStyle's default value.</param>
    public static void DrawGizmosCircle(Vector3 position, Single radius, Color color, Single height = 0.0f, QuantumGizmoStyle style = default) {
#if UNITY_EDITOR
      var s = Vector3.one;
      Vector3 up;
      Quaternion rot;

#if QUANTUM_XY
      rot = Quaternion.Euler(0, 0, 0);
      s = new Vector3(radius + radius, radius + radius, 1.0f);
      up = Vector3.forward;
#else
      rot = Quaternion.Euler(-90, 0, 0);
      s   = new Vector3(radius + radius, radius + radius, 1.0f);
      up  = Vector3.up;
#endif

      // TODO: Use non-XY circle as default
      var mesh = height != 0.0f ? QuantumMeshCollection.Global.CylinderXY : QuantumMeshCollection.Global.CircleXY;
      if (height != 0.0f) {
        s.z = height;
      }
      
      Gizmos.color  = color;
      Handles.color = Gizmos.color;

      if (style.IsWireframeEnabled) {
        if (!style.IsFillEnabled) {
          // draw mesh as invisible; this still lets selection to work
          Gizmos.color = default;
          Gizmos.DrawMesh(mesh, 0, position, rot, s);
        }

        Handles.DrawWireDisc(position, up, radius);
      }

      if (style.IsFillEnabled) {
        Gizmos.DrawMesh(mesh, 0, position, rot, s);
      }

      Handles.color = Gizmos.color = Color.white;
#endif
    }

    /// <summary>
    /// Draws a sphere gizmo in the scene.
    /// </summary>
    /// <param name="position">The position of the sphere.</param>
    /// <param name="radius">The radius of the sphere.</param>
    /// <param name="color">The color of the sphere.</param>
    /// <param name="style">The style of the gizmo.</param>
    public static void DrawGizmosSphere(Vector3 position, Single radius, Color color, QuantumGizmoStyle style = default) {
      Gizmos.color = color;
      if (style.IsFillEnabled) {
        Gizmos.DrawSphere(position, radius);
      } else {
        if (style.IsWireframeEnabled) {
          Gizmos.DrawWireSphere(position, radius);
        }
      }

      Gizmos.color = Color.white;
    }

    /// <summary>
    /// Draws a triangle gizmo using the given vertices and color.
    /// </summary>
    /// <param name="A">The first vertex of the triangle.</param>
    /// <param name="B">The second vertex of the triangle.</param>
    /// <param name="C">The third vertex of the triangle.</param>
    /// <param name="color">The color of the triangle.</param>
    public static void DrawGizmosTriangle(Vector3 A, Vector3 B, Vector3 C, Color color) {
      Gizmos.color = color;
      Gizmos.DrawLine(A, B);
      Gizmos.DrawLine(B, C);
      Gizmos.DrawLine(C, A);
      Gizmos.color = Color.white;
    }

    /// <summary>
    /// Draws a grid of gizmos in the Unity editor.
    /// </summary>
    /// <param name="bottomLeft">The bottom-left corner of the grid.</param>
    /// <param name="width">The number of horizontal nodes in the grid.</param>
    /// <param name="height">The number of vertical nodes in the grid.</param>
    /// <param name="nodeSize">The size of each grid node.</param>
    /// <param name="color">The color of the grid gizmos.</param>
    public static void DrawGizmoGrid(FPVector2 bottomLeft, Int32 width, Int32 height, Int32 nodeSize, Color color) {
      DrawGizmoGrid(bottomLeft.ToUnityVector3(), width, height, nodeSize, nodeSize, color);
    }

    /// <summary>
    /// Draw a grid of gizmos starting from a bottom-left position.
    /// </summary>
    /// <param name="bottomLeft">The bottom-left position of the grid.</param>
    /// <param name="width">The width of the grid in number of nodes.</param>
    /// <param name="height">The height of the grid in number of nodes.</param>
    /// <param name="nodeSize">The size of each individual node in the grid.</param>
    /// <param name="color">The color of the grid gizmos.</param>
    public static void DrawGizmoGrid(Vector3 bottomLeft, Int32 width, Int32 height, Int32 nodeSize, Color color) {
      DrawGizmoGrid(bottomLeft, width, height, nodeSize, nodeSize, color);
    }

    /// <summary>
    /// Draws a grid of gizmos in the scene.
    /// </summary>
    /// <param name="bottomLeft">The bottom left corner of the grid.</param>
    /// <param name="width">The number of columns in the grid.</param>
    /// <param name="height">The number of rows in the grid.</param>
    /// <param name="nodeWidth">The width of each grid node.</param>
    /// <param name="nodeHeight">The height of each grid node.</param>
    /// <param name="color">The color of the grid lines.</param>
    public static void DrawGizmoGrid(Vector3 bottomLeft, Int32 width, Int32 height, float nodeWidth, float nodeHeight, Color color) {
      Gizmos.color = color;

#if QUANTUM_XY
        for (Int32 z = 0; z <= height; ++z) {
            Gizmos.DrawLine(bottomLeft + new Vector3(0.0f, nodeHeight * z, 0.0f), bottomLeft + new Vector3(width * nodeWidth, nodeHeight * z, 0.0f));
        }

        for (Int32 x = 0; x <= width; ++x) {
            Gizmos.DrawLine(bottomLeft + new Vector3(nodeWidth * x, 0.0f, 0.0f), bottomLeft + new Vector3(nodeWidth * x, height * nodeHeight, 0.0f));
        }
#else
      for (Int32 z = 0; z <= height; ++z) {
        Gizmos.DrawLine(bottomLeft + new Vector3(0.0f, 0.0f, nodeHeight * z), bottomLeft + new Vector3(width * nodeWidth, 0.0f, nodeHeight * z));
      }

      for (Int32 x = 0; x <= width; ++x) {
        Gizmos.DrawLine(bottomLeft + new Vector3(nodeWidth * x, 0.0f, 0.0f), bottomLeft + new Vector3(nodeWidth * x, 0.0f, height * nodeHeight));
      }
#endif

      Gizmos.color = Color.white;
    }

    /// <summary>
    /// Draws a 2D polygon gizmo in the scene.
    /// </summary>
    /// <param name="position">The position of the polygon.</param>
    /// <param name="rotation">The rotation of the polygon.</param>
    /// <param name="vertices">The vertices of the polygon.</param>
    /// <param name="height">The height of the polygon.</param>
    /// <param name="color">The color of the polygon.</param>
    /// <param name="style">The style of the gizmo.</param>
    public static void DrawGizmoPolygon2D(Vector3 position, Quaternion rotation, FPVector2[] vertices, Single height, Color color, QuantumGizmoStyle style = default) {
      var matrix = Matrix4x4.TRS(position, rotation, Vector3.one);
      DrawGizmoPolygon2D(matrix, vertices, height, false, color, style: style);
    }

    /// <summary>
    /// Draws a 2D polygon gizmo in the scene.
    /// </summary>
    /// <param name="position">The position of the polygon.</param>
    /// <param name="rotation">The rotation of the polygon.</param>
    /// <param name="vertices">The array of vertices that define the polygon shape.</param>
    /// <param name="height">The height of the polygon.</param>
    /// <param name="drawNormals">Whether to draw normals for the polygon.</param>
    /// <param name="color">The color of the polygon.</param>
    /// <param name="style">The style of the gizmo.</param>
    public static void DrawGizmoPolygon2D(Vector3 position, Quaternion rotation, FPVector2[] vertices, Single height, bool drawNormals, Color color, QuantumGizmoStyle style = default) {
      var matrix = Matrix4x4.TRS(position, rotation, Vector3.one);
      DrawGizmoPolygon2D(matrix, vertices, height, drawNormals, color, style: style);
    }

    /// <summary>
    /// Draws a 2D polygon gizmo with the given parameters.
    /// </summary>
    /// <param name="transform">The transform of the polygon.</param>
    /// <param name="vertices">The vertices of the polygon.</param>
    /// <param name="height">The height of the polygon.</param>
    /// <param name="drawNormals">Whether to draw the polygon normal.</param>
    /// <param name="color">The color of the polygon.</param>
    /// <param name="style">The style of the gizmo.</param>
    public static void DrawGizmoPolygon2D(Transform transform, FPVector2[] vertices, Single height, bool drawNormals, Color color, QuantumGizmoStyle style = default) {
      var matrix = transform.localToWorldMatrix;
      DrawGizmoPolygon2D(matrix, vertices, height, drawNormals, color, style: style);
    }

    /// <inheritdoc cref="DrawGizmoPolygon2D(Vector3, Quaternion, FPVector2[], float, bool, Color, QuantumGizmoStyle)"/>
    public static void DrawGizmoPolygon2D(Matrix4x4 matrix, FPVector2[] vertices, Single height, bool drawNormals, Color color, QuantumGizmoStyle style = default) {

      if (vertices.Length < 3) return;

      FPMathUtils.LoadLookupTables();

      color = FPVector2.IsPolygonConvex(vertices) && FPVector2.PolygonNormalsAreValid(vertices) ? color : Color.red;

      var transformedVertices = vertices.Select(x => matrix.MultiplyPoint(x.ToUnityVector3())).ToArray();
      DrawGizmoPolygon2DInternal(transformedVertices, height, drawNormals, color, style: style);
    }

    /// <summary>
    /// Draws a 2D polygon gizmo.
    /// </summary>
    /// <param name="vertices">The vertices of the polygon in world space.</param>
    /// <param name="height">The height of the polygon.</param>
    /// <param name="drawNormals">Determines whether to draw normal.</param>
    /// <param name="color">The color of the polygon.</param>
    /// <param name="style">The gizmo style.</param>
    private static void DrawGizmoPolygon2DInternal(Vector3[] vertices, Single height, Boolean drawNormals, Color color, QuantumGizmoStyle style = default) {
#if UNITY_EDITOR
#if QUANTUM_XY
      var upVector = Vector3.forward;
#else
      var upVector = Vector3.up;
#endif
      Gizmos.color  = color;
      Handles.color = color;

      if (style.IsFillEnabled) {
        Handles.DrawAAConvexPolygon(vertices);

        if (height != 0.0f) {
          Handles.matrix = Matrix4x4.Translate(upVector * height);
          Handles.DrawAAConvexPolygon(vertices);
          Handles.matrix = Matrix4x4.identity;
        }
      }

      if (style.IsWireframeEnabled) {
        for (Int32 i = 0; i < vertices.Length; ++i) {
          var v1 = vertices[i];
          var v2 = vertices[(i + 1) % vertices.Length];

          Gizmos.DrawLine(v1, v2);

          if (height != 0.0f) {
            Gizmos.DrawLine(v1 + upVector * height, v2 + upVector * height);
            Gizmos.DrawLine(v1, v1 + upVector * height);
          }

          if (drawNormals) {
#if QUANTUM_XY
          var normal = Vector3.Cross(v2 - v1, upVector).normalized;
#else
            var normal = Vector3.Cross(v1 - v2, upVector).normalized;
#endif

            var center = Vector3.Lerp(v1, v2, 0.5f);
            DrawGizmoVector(center, center + (normal * 0.25f));
          }
        }
      }

      Gizmos.color = Handles.color = Color.white;
#endif
    }

    /// <summary>
    /// Draws a diamond gizmo with the given center and size.
    /// </summary>
    /// <param name="center">The center position of the diamond.</param>
    /// <param name="size">The size of the diamond.</param>
    public static void DrawGizmoDiamond(Vector3 center, Vector2 size) {
      var DiamondWidth = size.x * 0.5f;
      var DiamondHeight = size.y * 0.5f;

#if QUANTUM_XY
      Gizmos.DrawLine(center + Vector3.right * DiamondWidth, center + Vector3.up * DiamondHeight);
      Gizmos.DrawLine(center + Vector3.left * DiamondWidth, center + Vector3.up * DiamondHeight);
      Gizmos.DrawLine(center + Vector3.right * DiamondWidth, center + Vector3.down * DiamondHeight);
      Gizmos.DrawLine(center + Vector3.left * DiamondWidth, center + Vector3.down * DiamondHeight);
#else
      Gizmos.DrawLine(center + Vector3.right * DiamondWidth, center + Vector3.forward * DiamondHeight);
      Gizmos.DrawLine(center + Vector3.left * DiamondWidth, center + Vector3.forward * DiamondHeight);
      Gizmos.DrawLine(center + Vector3.right * DiamondWidth, center + Vector3.back * DiamondHeight);
      Gizmos.DrawLine(center + Vector3.left * DiamondWidth, center + Vector3.back * DiamondHeight);
#endif
    }

    /// <summary>
    /// Draws a 3D vector gizmo with an arrowhead from the specified start to end points.
    /// </summary>
    /// <param name="start">The starting point of the vector.</param>
    /// <param name="end">The ending point of the vector.</param>
    /// <param name="arrowHeadLength">The length of the arrowhead.</param>
    /// <param name="arrowHeadAngle">The angle of the arrowhead.</param>
    public static void DrawGizmoVector3D(Vector3 start, Vector3 end, float arrowHeadLength = 0.25f, float arrowHeadAngle = 25.0f) {
      Gizmos.DrawLine(start, end);
      var d = (end - start).normalized;
      Vector3 right = Quaternion.LookRotation(d) * Quaternion.Euler(0f, 180f + arrowHeadAngle, 0f) * new Vector3(0f, 0f, 1f);
      Vector3 left  = Quaternion.LookRotation(d) * Quaternion.Euler(0f, 180f - arrowHeadAngle, 0f) * new Vector3(0f, 0f, 1f);
      Gizmos.DrawLine(end, end + right * arrowHeadLength);
      Gizmos.DrawLine(end, end + left * arrowHeadLength);
    }

    /// <summary>
    /// Draws a vector gizmo from the specified start point to the specified end point.
    /// </summary>
    /// <param name="start">The starting point of the vector.</param>
    /// <param name="end">The ending point of the vector.</param>
    /// <param name="arrowHeadLength">The length of the arrow head (default is DefaultArrowHeadLength).</param>
    /// <param name="arrowHeadAngle">The angle of the arrow head (default is DefaultArrowHeadAngle).</param>
    public static void DrawGizmoVector(Vector3 start, Vector3 end, float arrowHeadLength = DefaultArrowHeadLength, float arrowHeadAngle = DefaultArrowHeadAngle) {
      Gizmos.DrawLine(start, end);

      var l = (start - end).magnitude;

      if (l < arrowHeadLength * 2) {
        arrowHeadLength = l / 2;
      }

      var d = (start - end).normalized;

      float cos = Mathf.Cos(arrowHeadAngle * Mathf.Deg2Rad);
      float sin = Mathf.Sin(arrowHeadAngle * Mathf.Deg2Rad);

      Vector3 left = Vector3.zero;
#if QUANTUM_XY
      left.x = d.x * cos - d.y * sin;
      left.y = d.x * sin + d.y * cos;
#else
      left.x = d.x * cos - d.z * sin;
      left.z = d.x * sin + d.z * cos;
#endif

      sin = -sin;

      Vector3 right = Vector3.zero;
#if QUANTUM_XY
      right.x = d.x * cos - d.y * sin;
      right.y = d.x * sin + d.y * cos;
#else
      right.x = d.x * cos - d.z * sin;
      right.z = d.x * sin + d.z * cos;
#endif

      Gizmos.DrawLine(end, end + left * arrowHeadLength);
      Gizmos.DrawLine(end, end + right * arrowHeadLength);
    }

    /// <summary>
    /// Draws a gizmo arc in the Unity editor.
    /// </summary>
    /// <param name="position">The position of the arc.</param>
    /// <param name="normal">The normal vector of the arc.</param>
    /// <param name="from">The starting direction vector of the arc.</param>
    /// <param name="angle">The angle of the arc.</param>
    /// <param name="radius">The radius of the arc.</param>
    /// <param name="color">The color of the arc.</param>
    /// <param name="alphaRatio">The alpha ratio of the arc.</param>
    /// <param name="style">The style of the arc.</param>
    public static void DrawGizmoArc(Vector3 position, Vector3 normal, Vector3 from, float angle, float radius, Color color, float alphaRatio = 1.0f, QuantumGizmoStyle style = default) {
#if UNITY_EDITOR
      Handles.color = color;
      Gizmos.color = color;

      if (style.IsWireframeEnabled) {
        Handles.DrawWireArc(position, normal, from, angle, radius);
        if (!style.IsFillEnabled) {
          var to = Quaternion.AngleAxis(angle, normal) * from;
          Gizmos.color = color.Alpha(color.a * alphaRatio);
          Gizmos.DrawRay(position, from * radius);
          Gizmos.DrawRay(position, to * radius);
        }
      }

      if (style.IsFillEnabled) {
        Handles.color = color.Alpha(color.a * alphaRatio);
        Handles.DrawSolidArc(position, normal, from, angle, radius);
      }

      Gizmos.color = Handles.color = Color.white;
#endif
    }

    /// <summary>
    /// Draws a gizmo disc at the specified position and orientation.
    /// </summary>
    /// <param name="position">The position of the disc.</param>
    /// <param name="normal">The orientation of the disc.</param>
    /// <param name="radius">The radius of the disc.</param>
    /// <param name="color">The color of the disc.</param>
    /// <param name="alphaRatio">The alpha ratio for the disc's color.</param>
    /// <param name="style">The style of the gizmo.</param>
    public static void DrawGizmoDisc(Vector3 position, Vector3 normal, float radius, Color color, float alphaRatio = 1.0f, QuantumGizmoStyle style = default) {
#if UNITY_EDITOR
      Handles.color = color;
      Gizmos.color = color;

      if (style.IsWireframeEnabled) {
        Handles.DrawWireDisc(position, normal, radius);
      }

      if (style.IsFillEnabled) {
        Handles.color = Handles.color.Alpha(Handles.color.a * alphaRatio);
        Handles.DrawSolidDisc(position, normal, radius);
      }

      Gizmos.color = Handles.color = Color.white;
#endif
    }

    /// <summary>
    /// Draws a gizmo edge from the specified start point to the end point.
    /// </summary>
    /// <param name="start">The starting point of the edge.</param>
    /// <param name="end">The ending point of the edge.</param>
    /// <param name="height">The height of the edge.</param>
    /// <param name="color">The color of the edge.</param>
    /// <param name="style">The gizmo style to use.</param>
    public static void DrawGizmosEdge(Vector3 start, Vector3 end, float height, Color color, QuantumGizmoStyle style = default) {
      Gizmos.color = color;

      if (Math.Abs(height) > float.Epsilon) {
        var startToEnd = end - start;
        var edgeSize   = startToEnd.magnitude;
        var size       = new Vector3(edgeSize, 0);
        var center     = start + startToEnd / 2;
#if QUANTUM_XY
        size.z = height;
        center.z += height / 2;
#else
        size.y   =  height;
        center.y += height / 2;
#endif
        DrawGizmosBox(center, size, color, rotation: Quaternion.FromToRotation(Vector3.right, startToEnd), style: style);
      } else {
        Gizmos.DrawLine(start, end);
      }

      Gizmos.color = Color.white;
    }

    /// <summary>
    /// Draws a capsule Gizmo in 3D space.
    /// </summary>
    /// <param name="center">The center position of the capsule.</param>
    /// <param name="radius">The radius of the capsule.</param>
    /// <param name="extent">The extent (length) of the capsule.</param>
    /// <param name="color">The color of the capsule.</param>
    /// <param name="rotation">The rotation of the capsule. If null, no rotation is applied.</param>
    /// <param name="style">The style of the Gizmo. Defaults to QuantumGizmoStyle.</param>
    public static void DrawGizmosCapsule(Vector3 center, float radius, float extent, Color color, Quaternion? rotation = null, QuantumGizmoStyle style = default) {
      var matrix = Matrix4x4.TRS(center, rotation ?? Quaternion.identity, Vector3.one);
      DrawGizmosCapsule(matrix, radius, extent, color, style: style);
    }

    /// <summary>
    /// Draws a capsule gizmo in the Scene view using Handles.
    /// </summary>
    /// <param name="matrix">The matrix of the capsule.</param>
    /// <param name="radius">The radius of the capsule.</param>
    /// <param name="extent">The height extent of the capsule.</param>
    /// <param name="color">The color of the capsule.</param>
    /// <param name="style">Optional gizmo style.</param>
    public static void DrawGizmosCapsule(Matrix4x4 matrix, float radius, float extent, Color color, QuantumGizmoStyle style = default) {
#if UNITY_EDITOR
      Handles.matrix = matrix;
      Handles.color = color;

      // TODO: handle QuantumGizmoStyle.IsFillEnabled (see Box gizmos for reference)
      
      var cylinderTop = Vector3.up * extent;
      var cylinderBottom = Vector3.down * extent;
      var radiusRight = Vector3.right * radius;
      var radiusForward = Vector3.forward * radius;

      Handles.DrawWireArc(cylinderTop, Vector3.up, Vector3.left, 360.0f, radius);
      Handles.DrawWireArc(cylinderBottom, Vector3.down, Vector3.left, 360.0f, radius);
      
      Handles.DrawWireArc(cylinderTop, Vector3.right, Vector3.back, 180.0f, radius);
      Handles.DrawWireArc(cylinderTop, Vector3.forward, Vector3.right, 180.0f, radius);
      Handles.DrawWireArc(cylinderBottom, Vector3.left, Vector3.back, 180.0f, radius);
      Handles.DrawWireArc(cylinderBottom, Vector3.back, Vector3.right, 180.0f, radius);
      
      Handles.DrawLine(cylinderTop + radiusRight, cylinderBottom + radiusRight);
      Handles.DrawLine(cylinderTop - radiusRight, cylinderBottom - radiusRight);
      Handles.DrawLine(cylinderTop + radiusForward, cylinderBottom + radiusForward);
      Handles.DrawLine(cylinderTop - radiusForward, cylinderBottom - radiusForward);

      Handles.matrix = Matrix4x4.identity;
      Handles.color = Color.white;
#endif
    }
  }

  /// <summary>
  /// The style of the gizmo.
  /// </summary>
  [Serializable]
  public struct QuantumGizmoStyle {
    /// <summary>
    /// The default gizmo style.
    /// </summary>
    public static QuantumGizmoStyle FillDisabled => new QuantumGizmoStyle() { DisableFill = true };

    /// <summary>
    /// If true, the gizmo will be filled.
    /// </summary>
    public bool DisableFill;

    /// <summary>
    /// Returns true if the gizmo fill is enabled.
    /// </summary>
    public bool IsFillEnabled => !DisableFill;
    
    /// <summary>
    /// Returns true if the gizmo wireframe is enabled.
    /// </summary>
    public bool IsWireframeEnabled => true;
  }
}

#endregion


#region Assets/Photon/Quantum/Runtime/Utils/NavMeshBakerBenchmarkerProgressBar.cs

namespace Quantum {
  using System.Collections.Generic;
  using System.Diagnostics;
#if UNITY_EDITOR
  using UnityEditor;
#endif

  /// <summary>
  /// An implementation of the <see cref="IProgressBar"/> used for the navmesh baking.
  /// The internal baking process will set additional information on this class.
  /// The progress bar is only showed when the LogLevel is set to Debug.
  /// </summary>
  public class NavMeshBakerBenchmarkerProgressBar : IProgressBar {
    /// <summary>
    /// Navmesh bake section used to display information on the progress bar.
    /// </summary>
    private class BakeSection {
      /// <summary>
      /// The section name.
      /// </summary>
      public string Name;
      /// <summary>
      /// The time spent in milliseconds in this section.
      /// </summary>
      public long TimeInMs;
    }

    /// <summary>
    /// Set to disable the Unity progress bar.
    /// </summary>
    public static bool EnableProgressBar = true;
    /// <summary>
    /// Set to disable the result log.
    /// </summary>
    public static bool EnableResultLog = true;

    private readonly List<BakeSection> _bakeSections = new List<BakeSection>();
    private readonly Stopwatch _stopwatch = new Stopwatch();
    private BakeSection _currentSection;
    private string _name;

    /// <summary>
    /// Create a new instance of the progress bar.
    /// </summary>
    /// <param name="name">Progress bar name</param>
    public NavMeshBakerBenchmarkerProgressBar(string name) {
      _name = name;
    }

    /// <summary>
    /// Complete the current section, add a new section to the progress bar and restart the timer.
    /// </summary>
    /// <param name="v">Section name</param>
    public void SetInfo(string v) {
      SaveSection();
      _currentSection = new BakeSection { Name = v };
    }

    /// <summary>
    /// Set the progress of the current section.
    /// </summary>
    /// <param name="v">Progress between 0..1</param>
    public void SetProgress(float v) {
#if UNITY_EDITOR
      if (EnableProgressBar) {
        EditorUtility.DisplayProgressBar(_name, _currentSection.Name, v);
      }
#endif
    }

    /// <summary>
    /// Complete, dispose the progress bar and logs a result.
    /// </summary>
    public void Dispose() {
#if UNITY_EDITOR
      if (EnableProgressBar) {
        EditorUtility.ClearProgressBar();
      }
#endif

      SaveSection();
      _currentSection = null;
      _stopwatch.Stop();

      if (EnableResultLog) {
        string result = $"NavMesh bake report for {_name}:\n";

        foreach (var section in _bakeSections) {
          result += ($"{section.Name} took {section.TimeInMs} ms" + "\n");
        }

        Log.Debug(result);
      }
    }

    private void SaveSection() {
      if (_currentSection != null) {
        _currentSection.TimeInMs = _stopwatch.ElapsedMilliseconds;
        _stopwatch.Restart();
        _bakeSections.Add(_currentSection);
      } else {
        _stopwatch.Start();
      }
    }
  }
}

#endregion


#region Assets/Photon/Quantum/Runtime/Utils/ProgressBar.cs

namespace Quantum {
  using System;
  using System.Diagnostics;
  using UnityEngine;
  using Debug = UnityEngine.Debug;
#if UNITY_EDITOR
  using UnityEditor;
#endif

  /// <summary>
  /// A progress bar implementation used by the navmesh importing.
  /// The progress bar is only showed when the LogLevel is set to Debug.
  /// </summary>
  public class ProgressBar : IDisposable {
    float  _progress;
    string _info;
#pragma warning disable CS0414 // The private field is assigned but its value is never used (#if UNITY_EDITOR)
    string _title;
    bool   _isCancelable;
#pragma warning restore CS0414 // The private field is assigned but its value is never used
    Stopwatch _sw;

    /// <summary>
    /// Create a progress bar instance.
    /// </summary>
    /// <param name="title">The title</param>
    /// <param name="isCancelable">Is the process cancelable</param>
    /// <param name="logStopwatch">Should the timer result be logged out periodically</param>
    public ProgressBar(string title, bool isCancelable = false, bool logStopwatch = false) {
      _title        = title;
      _isCancelable = isCancelable;
      if (logStopwatch) {
        _sw = Stopwatch.StartNew();
      }
    }

    /// <summary>
    /// Set the new sub headline for the progress bar and reset the progress.
    /// </summary>
    public string Info {
      set {
        DisplayStopwatch();
        _info     = value;
        _progress = 0.0f;
        Display();
      }
      get { 
        return _info; 
      }
    }

    /// <summary>
    /// Set the progress of the current task.
    /// </summary>
    public float Progress {
      set {
        bool hasChanged = Mathf.Abs(_progress - value) > 0.01f;
        if (!hasChanged)
          return;

        _progress = value;
        Display();
      }

      get {
        return _progress;
      }
    }

    /// <summary>
    /// Uses <see cref="Info"/> property.
    /// </summary>
    /// <param name="value">Into value</param>
    public void SetInfo(string value) {
      Info = value;
    }

    /// <summary>
    /// Uses <see cref="Progress"/> property."/>
    /// </summary>
    /// <param name="value">Progress value between 0..1</param>
    public void SetProgress(float value) {
      Progress = value;
    }

    /// <summary>
    /// Dispose, hide the progress bar UI.
    /// </summary>
    public void Dispose() {
#if UNITY_EDITOR
      EditorUtility.ClearProgressBar();
      DisplayStopwatch();
#endif
    }

    private void Display() {
#if UNITY_EDITOR
      if (_isCancelable) {
        bool isCanceled = EditorUtility.DisplayCancelableProgressBar(_title, _info, _progress);
        if (isCanceled) {
          throw new Exception(_title + " canceled");
        }
      } else {
        EditorUtility.DisplayProgressBar(_title, _info, _progress);
      }
#endif
    }

    private void DisplayStopwatch() {
      if (_sw != null && !string.IsNullOrEmpty(_info)) {
        Debug.LogFormat("'{0}' took {1} ms", _info, _sw.ElapsedMilliseconds);
        _sw.Reset();
        _sw.Start();
      }
    }
  }
}

#endregion


#region Assets/Photon/Quantum/Runtime/Utils/QuantumColor.cs

namespace Quantum {
  using UnityEngine;

  /// <summary>
  /// Quantum default colors.
  /// </summary>
  public static class QuantumColor {
    /// <summary>
    /// The color of the highlighted Quantum log can change depending on the dark/light mode.
    /// </summary>
    public static Color32 Log {
      get {
        bool isDarkMode = false;
#if UNITY_EDITOR
        isDarkMode = UnityEditor.EditorGUIUtility.isProSkin;
#endif
        return isDarkMode ? new Color32(32, 203, 145, 255) : new Color32(18, 75, 60, 255);
      }
    }
  }
}


#endregion


#region Assets/Photon/Quantum/Runtime/Utils/QuantumGlobalScriptableObject.cs

namespace Quantum {
  using System;

  partial class QuantumGlobalScriptableObject<T> {
    /// <summary>
    /// Obsolete
    /// </summary>
    [Obsolete("Use " + nameof(Global) + " instead.")]
    public static T Instance => Global;

    /// <summary>
    /// Get or set the Global instance of the scriptable object.
    /// </summary>
    public static T Global {
      get => GlobalInternal;
      protected set => GlobalInternal = value;
    } 

    /// <summary>
    /// Try get or load the global instance.
    /// </summary>
    /// <param name="global">Resulting global instance</param>
    /// <returns>True if the global instance was found</returns>
    public static bool TryGetGlobal(out T global) => TryGetGlobalInternal(out global);
  }
}

#endregion


#region Assets/Photon/Quantum/Runtime/Utils/RectExtensions.cs

namespace Quantum {
  using UnityEditor;
  using UnityEngine;

  /// <summary>
  /// Unity custom editor GUI utility functions.
  /// </summary>
  public static class EditorRectUtils {
    /// <summary>
    /// Set the width of rect.
    /// </summary>
    /// <param name="r">Rect</param>
    /// <param name="w">New width</param>
    /// <returns>The new rect</returns>
    public static Rect SetWidth(this Rect r, float w) {
      r.width = w;
      return r;
    }

    /// <summary>
    /// Set the height and the width of the rect.
    /// </summary>
    /// <param name="r">Rect</param>
    /// <param name="v">X component is set as width, y component is set as height</param>
    /// <returns>The new rect</returns>
    public static Rect SetWidthHeight(this Rect r, Vector2 v) {
      r.width  = v.x;
      r.height = v.y;
      return r;
    }

    /// <summary>
    /// Set the height and the width of the rect.
    /// </summary>
    /// <param name="r">Rect</param>
    /// <param name="w">The new width</param>
    /// <param name="h">The new height</param>
    /// <returns>The new rect</returns>
    public static Rect SetWidthHeight(this Rect r, float w, float h) {
      r.width  = w;
      r.height = h;
      return r;
    }

    /// <summary>
    /// Add a delta to the width of the rect.
    /// </summary>
    /// <param name="r">Rect</param>
    /// <param name="w">Width delta to apply</param>
    /// <returns>The new rect</returns>
    public static Rect AddWidth(this Rect r, float w) {
      r.width += w;
      return r;
    }

    /// <summary>
    /// Add a delta to the height of the rect.
    /// </summary>
    /// <param name="r">Rect</param>
    /// <param name="h">Height delta to apply</param>
    /// <returns>The new rect</returns>
    public static Rect AddHeight(this Rect r, float h) {
      r.height += h;
      return r;
    }

    /// <summary>
    /// Set the height of a rect.
    /// </summary>
    /// <param name="r">Rect</param>
    /// <param name="h">The new height value</param>
    /// <returns>The new rect</returns>
    public static Rect SetHeight(this Rect r, float h) {
      r.height = h;
      return r;
    }

    /// <summary>
    /// Add a delta to the position <see cref="Rect.x"/> and <see cref="Rect.y"/>.
    /// </summary>
    /// <param name="r">Rect</param>
    /// <param name="xy">Position delta to be added</param>
    /// <returns>The new rect</returns>
    public static Rect AddXY(this Rect r, Vector2 xy) {
      r.x += xy.x;
      r.y += xy.y;
      return r;
    }

    /// <summary>
    /// Add position delta to the rect.
    /// </summary>
    /// <param name="r">Rect</param>
    /// <param name="x">Add to <see cref="Rect.x"/></param>
    /// <param name="y">Add to <see cref="Rect.y"/></param>
    /// <returns>The new rect</returns>
    public static Rect AddXY(this Rect r, float x, float y) {
      r.x += x;
      r.y += y;
      return r;
    }

    /// <summary>
    /// Add to the x component of the rect.
    /// </summary>
    /// <param name="r">Rect</param>
    /// <param name="x">Value to add to <see cref="Rect.x"/></param>
    /// <returns>The new rect</returns>
    public static Rect AddX(this Rect r, float x) {
      r.x += x;
      return r;
    }

    /// <summary>
    /// Add to the y component of the rect.
    /// </summary>
    /// <param name="r">Rect</param>
    /// <param name="y">Value to add to <see cref="Rect.y"/></param>
    /// <returns>The new rect</returns>
    public static Rect AddY(this Rect r, float y) {
      r.y += y;
      return r;
    }

    /// <summary>
    /// Set the y component of the rect.
    /// </summary>
    /// <param name="r">Rect</param>
    /// <param name="y">Value to set as <see cref="Rect.y"/></param>
    /// <returns>The new rect</returns>
    public static Rect SetY(this Rect r, float y) {
      r.y = y;
      return r;
    }

    /// <summary>
    /// Set the x component of the rect.
    /// </summary>
    /// <param name="r">Rect</param>
    /// <param name="x">Value to set as <see cref="Rect.x"/></param>
    /// <returns>The new rect</returns>
    public static Rect SetX(this Rect r, float x) {
      r.x = x;
      return r;
    }

    /// <summary>
    /// Set the xMin component of the rect.
    /// </summary>
    /// <param name="r">Rect</param>
    /// <param name="x">Value to set as <see cref="Rect.xMin"/></param>
    /// <returns>The new rect</returns>
    public static Rect SetXMin(this Rect r, float x) {
      r.xMin = x;
      return r;
    }

    /// <summary>
    /// Set the xMin component of the rect.
    /// </summary>
    /// <param name="r">Rect</param>
    /// <param name="x">Value to set as <see cref="Rect.xMax"/></param>
    /// <returns>The new rect</returns>
    public static Rect SetXMax(this Rect r, float x) {
      r.xMax = x;
      return r;
    }

    /// <summary>
    /// Set the yMin component of the rect.
    /// </summary>
    /// <param name="r">Rect</param>
    /// <param name="y">Value to set as <see cref="Rect.yMin"/></param>
    /// <returns>The new rect</returns>
    public static Rect SetYMin(this Rect r, float y) {
      r.yMin = y;
      return r;
    }

    /// <summary>
    /// The set yMax component of the rect.
    /// </summary>
    /// <param name="r">Rect</param>
    /// <param name="y">Value to set as <see cref="Rect.yMax"/></param>
    /// <returns>The new rect</returns>
    public static Rect SetYMax(this Rect r, float y) {
      r.yMax = y;
      return r;
    }

    /// <summary>
    /// Add to the xMin component of the rect.
    /// </summary>
    /// <param name="r">Rect</param>
    /// <param name="x">Value to add to <see cref="Rect.xMin"/></param>
    /// <returns>The new rect</returns>
    public static Rect AddXMin(this Rect r, float x) {
      r.xMin += x;
      return r;
    }

    /// <summary>
    /// Add to the xMax component of the rect.
    /// </summary>
    /// <param name="r">Rect</param>
    /// <param name="x">Value to add to <see cref="Rect.xMax"/></param>
    /// <returns>The new rect</returns>
    public static Rect AddXMax(this Rect r, float x) {
      r.xMax += x;
      return r;
    }

    /// <summary>
    /// Add to the yMin component of the rect.
    /// </summary>
    /// <param name="r">Rect</param>
    /// <param name="y">Value to add to <see cref="Rect.yMin"/></param>
    /// <returns>The new rect</returns>
    public static Rect AddYMin(this Rect r, float y) {
      r.yMin += y;
      return r;
    }

    /// <summary>
    /// Add to the yMax component of the rect.
    /// </summary>
    /// <param name="r">Rect</param>
    /// <param name="y">The value to add to <see cref="Rect.yMax"/></param>
    /// <returns>The new rect</returns>
    public static Rect AddYMax(this Rect r, float y) {
      r.yMax += y;
      return r;
    }

    /// <summary>
    /// Add to x,y, width and height component of the rect.
    /// </summary>
    /// <param name="r">Rect</param>
    /// <param name="x">Value to add to <see cref="Rect.x"/></param>
    /// <param name="y">Value to add to <see cref="Rect.y"/></param>
    /// <param name="w">Value to add to <see cref="Rect.width"/></param>
    /// <param name="h">Value to add to <see cref="Rect.height"/></param>
    /// <returns>The new rect</returns>
    public static Rect Adjust(this Rect r, float x, float y, float w, float h) {
      r.x      += x;
      r.y      += y;
      r.width  += w;
      r.height += h;
      return r;
    }

    /// <summary>
    /// Create a rect with the given position and size.
    /// </summary>
    /// <param name="v">Rect position</param>
    /// <param name="w">Rect width</param>
    /// <param name="h">Rect height</param>
    /// <returns>The new rect</returns>
    public static Rect ToRect(this Vector2 v, float w, float h) {
      return new Rect(v.x, v.y, w, h);
    }

    /// <summary>
    /// Set the position to zero.
    /// </summary>
    /// <param name="r">Rect</param>
    /// <returns>The new rect</returns>
    public static Rect ZeroXY(this Rect r) {
      return new Rect(0, 0, r.width, r.height);
    }

    /// <summary>
    /// Convert the rect width and height to a vector2.
    /// </summary>
    /// <param name="r">Rect</param>
    /// <returns>The width (x) and height (y)</returns>
    public static Vector2 ToVector2(this Rect r) {
      return new Vector2(r.width, r.height);
    }

#if UNITY_EDITOR
    /// <summary>
    /// Increase the rect size by add "editor lines" to the rect.
    /// </summary>
    /// <param name="r">Rect</param>
    /// <param name="count">The number of lines to add</param>
    /// <returns>The new rect</returns>
    public static Rect AddLine(this Rect r, int count = 1) {
      return AddY(r, count * (EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing));
    }

    /// <summary>
    /// The the height of the rect based on the <see cref="EditorGUIUtility.singleLineHeight"/>.
    /// </summary>
    /// <param name="r">Rect</param>
    /// <returns>The new rect</returns>
    public static Rect SetLineHeight(this Rect r) {
      return SetHeight(r, EditorGUIUtility.singleLineHeight);
    }
#endif
  }
}

#endregion


#region Assets/Photon/Quantum/Runtime/Utils/ReflectionUtils.cs

namespace Quantum {
  using System;
  using System.Collections.Generic;
  using System.Linq;
  using System.Linq.Expressions;
  using System.Reflection;

  /// <summary>
  /// Quantum reflection utilities.
  /// </summary>
  public static class ReflectionUtils {
    /// <summary>
    /// The default binding flags.
    /// </summary>
    public const BindingFlags DefaultBindingFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;

    /// <summary>
    /// Comparer for types for sorting.
    /// </summary>
    public class TypeHierarchyComparer : IComparer<Type> {
      public int Compare(Type x, Type y) {
        if (x == y) {
          return 0;
        }
        if (x == null) {
          return -1;
        }
        if (y == null) {
          return 1;
        }
        if (x.IsSubclassOf(y) == true) {
          return -1;
        }
        if (y.IsSubclassOf(x) == true) {
          return 1;
        }
        return 0;
      }
      
      /// <summary>
      /// An instance to the comparer.
      /// </summary>
      public static readonly TypeHierarchyComparer Instance = new TypeHierarchyComparer();
    }

    public static Type GetUnityLeafType(this Type type) {
      if (type.HasElementType) {
        type = type.GetElementType();
      } else if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>)) {
        type = type.GetGenericArguments()[0];
      }

      return type;
    }

#if UNITY_EDITOR

    public static T CreateEditorMethodDelegate<T>(string editorAssemblyTypeName, string methodName, BindingFlags flags = DefaultBindingFlags) where T : Delegate {
      return CreateMethodDelegate<T>(typeof(UnityEditor.Editor).Assembly, editorAssemblyTypeName, methodName, flags);
    }

    public static Delegate CreateEditorMethodDelegate(string editorAssemblyTypeName, string methodName, BindingFlags flags, Type delegateType) {
      return CreateMethodDelegate(typeof(UnityEditor.Editor).Assembly, editorAssemblyTypeName, methodName, flags, delegateType);
    }

#endif

    public static T CreateMethodDelegate<T>(this Type type, string methodName, BindingFlags flags = DefaultBindingFlags) where T : Delegate {
      try {
        return CreateMethodDelegateInternal<T>(type, methodName, flags);
      } catch (Exception ex) {
        throw new InvalidOperationException(CreateMethodExceptionMessage<T>(type.Assembly, type.FullName, methodName, flags), ex);
      }
    }

    public static Delegate CreateMethodDelegate(this Type type, string methodName, BindingFlags flags, Type delegateType) {
      return CreateMethodDelegateInternal(type, methodName, flags, delegateType);
    }

    public static T CreateMethodDelegate<T>(Assembly assembly, string typeName, string methodName, BindingFlags flags = DefaultBindingFlags) where T : Delegate {
      try {
        var type = assembly.GetType(typeName, true);
        return CreateMethodDelegateInternal<T>(type, methodName, flags);
      } catch (Exception ex) {
        throw new InvalidOperationException(CreateMethodExceptionMessage<T>(assembly, typeName, methodName, flags), ex);
      }
    }

    public static Delegate CreateMethodDelegate(Assembly assembly, string typeName, string methodName, BindingFlags flags, Type delegateType) {
      try {
        var type = assembly.GetType(typeName, true);
        return CreateMethodDelegateInternal(type, methodName, flags, delegateType);
      } catch (Exception ex) {
        throw new InvalidOperationException(CreateMethodExceptionMessage(assembly, typeName, methodName, flags, delegateType), ex);
      }
    }

    public static T CreateMethodDelegate<T>(this Type type, string methodName, BindingFlags flags, Type delegateType, params DelegateSwizzle[] fallbackSwizzles) where T : Delegate {
      try {
        MethodInfo method = GetMethodOrThrow(type, methodName, flags, delegateType, fallbackSwizzles, out var swizzle);

        var delegateParameters = typeof(T).GetMethod("Invoke").GetParameters();
        var parameters         = new List<ParameterExpression>();

        for (int i = 0; i < delegateParameters.Length; ++i) {
          parameters.Add(Expression.Parameter(delegateParameters[i].ParameterType, $"param_{i}"));
        }

        var convertedParameters = new List<Expression>();
        {
          var methodParameters = method.GetParameters();
          if (swizzle == null) {
            for (int i = 0, j = method.IsStatic ? 0 : 1; i < methodParameters.Length; ++i, ++j) {
              convertedParameters.Add(Expression.Convert(parameters[j], methodParameters[i].ParameterType));
            }
          } else {
            var swizzledParameters = swizzle.Swizzle(parameters.ToArray());
            for (int i = 0, j = method.IsStatic ? 0 : 1; i < methodParameters.Length; ++i, ++j) {
              convertedParameters.Add(Expression.Convert(swizzledParameters[j], methodParameters[i].ParameterType));
            }
          }
        }

        MethodCallExpression callExpression;
        if (method.IsStatic) {
          callExpression = Expression.Call(method, convertedParameters);
        } else {
          var instance = Expression.Convert(parameters[0], method.DeclaringType);
          callExpression = Expression.Call(instance, method, convertedParameters);
        }

        var l   = Expression.Lambda(typeof(T), callExpression, parameters);
        var del = l.Compile();
        return (T)del;
      } catch (Exception ex) {
        throw new InvalidOperationException(CreateMethodExceptionMessage<T>(type.Assembly, type.FullName, methodName, flags), ex);
      }
    }

    public static T CreateConstructorDelegate<T>(this Type type, BindingFlags flags, Type delegateType, params DelegateSwizzle[] fallbackSwizzles) where T : Delegate {
      try {
        var constructor = GetConstructorOrThrow(type, flags, delegateType, fallbackSwizzles, out var swizzle);

        var delegateParameters = typeof(T).GetMethod("Invoke").GetParameters();
        var parameters         = new List<ParameterExpression>();

        for (int i = 0; i < delegateParameters.Length; ++i) {
          parameters.Add(Expression.Parameter(delegateParameters[i].ParameterType, $"param_{i}"));
        }

        var convertedParameters = new List<Expression>();
        {
          var constructorParameters = constructor.GetParameters();
          if (swizzle == null) {
            for (int i = 0, j = 0; i < constructorParameters.Length; ++i, ++j) {
              convertedParameters.Add(Expression.Convert(parameters[j], constructorParameters[i].ParameterType));
            }
          } else {
            var swizzledParameters = swizzle.Swizzle(parameters.ToArray());
            for (int i = 0, j = 0; i < constructorParameters.Length; ++i, ++j) {
              convertedParameters.Add(Expression.Convert(swizzledParameters[j], constructorParameters[i].ParameterType));
            }
          }
        }

        NewExpression newExpression = Expression.New(constructor, convertedParameters);
        var           l             = Expression.Lambda(typeof(T), newExpression, parameters);
        var           del           = l.Compile();
        return (T)del;
      } catch (Exception ex) {
        throw new InvalidOperationException(CreateConstructorExceptionMessage(type.Assembly, type.FullName, flags), ex);
      }
    }

    public static FieldInfo GetFieldOrThrow(this Type type, string fieldName, BindingFlags flags = DefaultBindingFlags) {
      var field = type.GetField(fieldName, flags);
      if (field == null) {
        throw new ArgumentOutOfRangeException(nameof(fieldName), CreateFieldExceptionMessage(type.Assembly, type.FullName, fieldName, flags));
      }

      return field;
    }

    public static FieldInfo GetFieldOrThrow<T>(this Type type, string fieldName, BindingFlags flags = DefaultBindingFlags) {
      return GetFieldOrThrow(type, fieldName, typeof(T), flags);
    }

    public static FieldInfo GetFieldOrThrow(this Type type, string fieldName, Type fieldType, BindingFlags flags = DefaultBindingFlags) {
      var field = type.GetField(fieldName, flags);
      if (field == null) {
        throw new ArgumentOutOfRangeException(nameof(fieldName), CreateFieldExceptionMessage(type.Assembly, type.FullName, fieldName, flags));
      }

      if (field.FieldType != fieldType) {
        throw new InvalidProgramException($"Field {type.FullName}.{fieldName} is of type {field.FieldType}, not expected {fieldType}");
      }

      return field;
    }

    public static PropertyInfo GetPropertyOrThrow<T>(this Type type, string propertyName, BindingFlags flags = DefaultBindingFlags) {
      return GetPropertyOrThrow(type, propertyName, typeof(T), flags);
    }

    public static PropertyInfo GetPropertyOrThrow(this Type type, string propertyName, Type propertyType, BindingFlags flags = DefaultBindingFlags) {
      var property = type.GetProperty(propertyName, flags);
      if (property == null) {
        throw new ArgumentOutOfRangeException(nameof(propertyName), CreateFieldExceptionMessage(type.Assembly, type.FullName, propertyName, flags));
      }

      if (property.PropertyType != propertyType) {
        throw new InvalidProgramException($"Property {type.FullName}.{propertyName} is of type {property.PropertyType}, not expected {propertyType}");
      }

      return property;
    }

    public static ConstructorInfo GetConstructorInfoOrThrow(this Type type, Type[] types, BindingFlags flags = DefaultBindingFlags) {
      var constructor = type.GetConstructor(flags, null, types, null);
      if (constructor == null) {
        throw new ArgumentOutOfRangeException(nameof(types), CreateConstructorExceptionMessage(type.Assembly, type.FullName, types, flags));
      }

      return constructor;
    }

    public static Type GetNestedTypeOrThrow(this Type type, string name, BindingFlags flags) {
      var result = type.GetNestedType(name, flags);
      if (result == null) {
        throw new ArgumentOutOfRangeException(nameof(name), CreateFieldExceptionMessage(type.Assembly, type.FullName, name, flags));
      }

      return result;
    }

    public static InstanceAccessor<FieldType> CreateFieldAccessor<FieldType>(this Type type, string fieldName, Type expectedFieldType = null, BindingFlags flags = DefaultBindingFlags) {
      var field = type.GetFieldOrThrow(fieldName, expectedFieldType ?? typeof(FieldType), BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
      return CreateAccessorInternal<FieldType>(field);
    }

    public static StaticAccessor<object> CreateStaticFieldAccessor(this Type type, string fieldName, Type expectedFieldType = null) {
      return CreateStaticFieldAccessor<object>(type, fieldName, expectedFieldType);
    }

    public static StaticAccessor<FieldType> CreateStaticFieldAccessor<FieldType>(this Type type, string fieldName, Type expectedFieldType = null) {
      var field = type.GetFieldOrThrow(fieldName, expectedFieldType ?? typeof(FieldType), BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
      return CreateStaticAccessorInternal<FieldType>(field);
    }

    public static InstanceAccessor<PropertyType> CreatePropertyAccessor<PropertyType>(this Type type, string fieldName, Type expectedPropertyType = null, BindingFlags flags = DefaultBindingFlags) {
      var field = type.GetPropertyOrThrow(fieldName, expectedPropertyType ?? typeof(PropertyType), BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
      return CreateAccessorInternal<PropertyType>(field);
    }

    public static StaticAccessor<object> CreateStaticPropertyAccessor(this Type type, string fieldName, Type expectedFieldType = null) {
      return CreateStaticPropertyAccessor<object>(type, fieldName, expectedFieldType);
    }

    public static StaticAccessor<FieldType> CreateStaticPropertyAccessor<FieldType>(this Type type, string fieldName, Type expectedFieldType = null) {
      var field = type.GetPropertyOrThrow(fieldName, expectedFieldType ?? typeof(FieldType), BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
      return CreateStaticAccessorInternal<FieldType>(field);
    }

    private static string CreateMethodExceptionMessage<T>(Assembly assembly, string typeName, string methodName, BindingFlags flags) {
      return CreateMethodExceptionMessage(assembly, typeName, methodName, flags, typeof(T));
    }

    private static string CreateMethodExceptionMessage(Assembly assembly, string typeName, string methodName, BindingFlags flags, Type delegateType) {
      return $"{assembly.FullName}.{typeName}.{methodName} with flags: {flags} and type: {delegateType}";
    }

    private static string CreateFieldExceptionMessage(Assembly assembly, string typeName, string fieldName, BindingFlags flags) {
      return $"{assembly.FullName}.{typeName}.{fieldName} with flags: {flags}";
    }

    private static string CreateConstructorExceptionMessage(Assembly assembly, string typeName, BindingFlags flags) {
      return $"{assembly.FullName}.{typeName}() with flags: {flags}";
    }

    private static string CreateConstructorExceptionMessage(Assembly assembly, string typeName, Type[] types, BindingFlags flags) {
      return $"{assembly.FullName}.{typeName}({(string.Join(", ", types.Select(x => x.FullName)))}) with flags: {flags}";
    }

    private static T CreateMethodDelegateInternal<T>(this Type type, string name, BindingFlags flags) where T : Delegate {
      return (T)CreateMethodDelegateInternal(type, name, flags, typeof(T));
    }

    private static Delegate CreateMethodDelegateInternal(this Type type, string name, BindingFlags flags, Type delegateType) {
      MethodInfo method = GetMethodOrThrow(type, name, flags, delegateType);
      return Delegate.CreateDelegate(delegateType, null, method);
    }

    private static MethodInfo GetMethodOrThrow(Type type, string name, BindingFlags flags, Type delegateType) {
      return GetMethodOrThrow(type, name, flags, delegateType, Array.Empty<DelegateSwizzle>(), out _);
    }

    private static MethodInfo FindMethod(Type type, string name, BindingFlags flags, Type returnType, params Type[] parameters) {
      var method = type.GetMethod(name, flags, null, parameters, null);

      if (method == null) {
        return null;
      }

      if (method.ReturnType != returnType) {
        return null;
      }

      return method;
    }

    private static ConstructorInfo GetConstructorOrThrow(Type type, BindingFlags flags, Type delegateType, DelegateSwizzle[] swizzles, out DelegateSwizzle firstMatchingSwizzle) {
      var delegateMethod = delegateType.GetMethod("Invoke");

      var allDelegateParameters = delegateMethod.GetParameters().Select(x => x.ParameterType).ToArray();

      var constructor = type.GetConstructor(flags, null, allDelegateParameters, null);
      if (constructor != null) {
        firstMatchingSwizzle = null;
        return constructor;
      }

      if (swizzles != null) {
        foreach (var swizzle in swizzles) {
          Type[] swizzled = swizzle.Swizzle(allDelegateParameters);
          constructor = type.GetConstructor(flags, null, swizzled, null);
          if (constructor != null) {
            firstMatchingSwizzle = swizzle;
            return constructor;
          }
        }
      }

      var constructors = type.GetConstructors(flags);
      throw new ArgumentOutOfRangeException(nameof(delegateType), $"No matching constructor found for {type}, " +
        $"signature \"{delegateType}\", " +
        $"flags \"{flags}\" and " +
        $"params: {string.Join(", ", allDelegateParameters.Select(x => x.FullName))}" +
        $", candidates are\n: {(string.Join("\n", constructors.Select(x => x.ToString())))}");
    }

    private static MethodInfo GetMethodOrThrow(Type type, string name, BindingFlags flags, Type delegateType, DelegateSwizzle[] swizzles, out DelegateSwizzle firstMatchingSwizzle) {
      var delegateMethod = delegateType.GetMethod("Invoke");

      var allDelegateParameters = delegateMethod.GetParameters().Select(x => x.ParameterType).ToArray();

      var method = FindMethod(type, name, flags, delegateMethod.ReturnType, flags.HasFlag(BindingFlags.Static) ? allDelegateParameters : allDelegateParameters.Skip(1).ToArray());
      if (method != null) {
        firstMatchingSwizzle = null;
        return method;
      }

      if (swizzles != null) {
        foreach (var swizzle in swizzles) {
          Type[] swizzled = swizzle.Swizzle(allDelegateParameters);
          if (!flags.HasFlag(BindingFlags.Static) && swizzled[0] != type) {
            throw new InvalidOperationException();
          }

          method = FindMethod(type, name, flags, delegateMethod.ReturnType, flags.HasFlag(BindingFlags.Static) ? swizzled : swizzled.Skip(1).ToArray());
          if (method != null) {
            firstMatchingSwizzle = swizzle;
            return method;
          }
        }
      }

      var methods = type.GetMethods(flags);
      throw new ArgumentOutOfRangeException(nameof(name), $"No method found matching name \"{name}\", " +
        $"signature \"{delegateType}\", " +
        $"flags \"{flags}\" and " +
        $"params: {string.Join(", ", allDelegateParameters.Select(x => x.FullName))}" +
        $", candidates are\n: {(string.Join("\n", methods.Select(x => x.ToString())))}");
    }

    public static bool IsArrayOrList(this Type listType) {
      if (listType.IsArray) {
        return true;
      } else if (listType.IsGenericType && listType.GetGenericTypeDefinition() == typeof(List<>)) {
        return true;
      }

      return false;
    }

    public static Type GetArrayOrListElementType(this Type listType) {
      if (listType.IsArray) {
        return listType.GetElementType();
      } else if (listType.IsGenericType && listType.GetGenericTypeDefinition() == typeof(List<>)) {
        return listType.GetGenericArguments()[0];
      }

      return null;
    }

    public static Type MakeFuncType(params Type[] types) {
      return GetFuncType(types.Length).MakeGenericType(types);
    }

    private static Type GetFuncType(int argumentCount) {
      switch (argumentCount) {
        case 1:  return typeof(Func<>);
        case 2:  return typeof(Func<,>);
        case 3:  return typeof(Func<,,>);
        case 4:  return typeof(Func<,,,>);
        case 5:  return typeof(Func<,,,,>);
        case 6:  return typeof(Func<,,,,,>);
        default: throw new ArgumentOutOfRangeException(nameof(argumentCount));
      }
    }

    public static Type MakeActionType(params Type[] types) {
      if (types.Length == 0) return typeof(Action);
      return GetActionType(types.Length).MakeGenericType(types);
    }

    private static Type GetActionType(int argumentCount) {
      switch (argumentCount) {
        case 1:  return typeof(Action<>);
        case 2:  return typeof(Action<,>);
        case 3:  return typeof(Action<,,>);
        case 4:  return typeof(Action<,,,>);
        case 5:  return typeof(Action<,,,,>);
        case 6:  return typeof(Action<,,,,,>);
        default: throw new ArgumentOutOfRangeException(nameof(argumentCount));
      }
    }

    private static StaticAccessor<T> CreateStaticAccessorInternal<T>(MemberInfo fieldOrProperty) {
      try {
        var  valueParameter = Expression.Parameter(typeof(T), "value");
        bool canWrite       = true;

        UnaryExpression  valueExpression;
        MemberExpression memberExpression;
        if (fieldOrProperty is PropertyInfo property) {
          valueExpression  = Expression.Convert(valueParameter, property.PropertyType);
          memberExpression = Expression.Property(null, property);
          canWrite         = property.CanWrite;
        } else {
          var field = (FieldInfo)fieldOrProperty;
          valueExpression  = Expression.Convert(valueParameter, field.FieldType);
          memberExpression = Expression.Field(null, field);
          canWrite         = field.IsInitOnly == false;
        }

        Func<T> getter;
        var     getExpression = Expression.Convert(memberExpression, typeof(T));
        var     getLambda     = Expression.Lambda<Func<T>>(getExpression);
        getter = getLambda.Compile();

        Action<T> setter = null;
        if (canWrite) {
          var setExpression = Expression.Assign(memberExpression, valueExpression);
          var setLambda     = Expression.Lambda<Action<T>>(setExpression, valueParameter);
          setter = setLambda.Compile();
        }

        return new StaticAccessor<T>() {
          GetValue = getter,
          SetValue = setter
        };
      } catch (Exception ex) {
        throw new InvalidOperationException($"Failed to create accessor for {fieldOrProperty.DeclaringType}.{fieldOrProperty.Name}", ex);
      }
    }

    private static InstanceAccessor<T> CreateAccessorInternal<T>(MemberInfo fieldOrProperty) {
      try {
        var instanceParameter  = Expression.Parameter(typeof(object), "instance");
        var instanceExpression = Expression.Convert(instanceParameter, fieldOrProperty.DeclaringType);

        var  valueParameter = Expression.Parameter(typeof(T), "value");
        bool canWrite       = true;

        UnaryExpression  valueExpression;
        MemberExpression memberExpression;
        if (fieldOrProperty is PropertyInfo property) {
          valueExpression  = Expression.Convert(valueParameter, property.PropertyType);
          memberExpression = Expression.Property(instanceExpression, property);
          canWrite         = property.CanWrite;
        } else {
          var field = (FieldInfo)fieldOrProperty;
          valueExpression  = Expression.Convert(valueParameter, field.FieldType);
          memberExpression = Expression.Field(instanceExpression, field);
          canWrite         = field.IsInitOnly == false;
        }

        Func<object, T> getter;

        var getExpression = Expression.Convert(memberExpression, typeof(T));
        var getLambda     = Expression.Lambda<Func<object, T>>(getExpression, instanceParameter);
        getter = getLambda.Compile();

        Action<object, T> setter = null;
        if (canWrite) {
          var setExpression = Expression.Assign(memberExpression, valueExpression);
          var setLambda     = Expression.Lambda<Action<object, T>>(setExpression, instanceParameter, valueParameter);
          setter = setLambda.Compile();
        }

        return new InstanceAccessor<T>() {
          GetValue = getter,
          SetValue = setter
        };
      } catch (Exception ex) {
        throw new InvalidOperationException($"Failed to create accessor for {fieldOrProperty.DeclaringType}.{fieldOrProperty.Name}", ex);
      }
    }

    public struct InstanceAccessor<TValue> {
      public Func<object, TValue>   GetValue;
      public Action<object, TValue> SetValue;
    }

    public struct StaticAccessor<TValue> {
      public Func<TValue>   GetValue;
      public Action<TValue> SetValue;
    }

    public class DelegateSwizzle {
      private int[] _args;

      public int Count => _args.Length;

      public DelegateSwizzle(params int[] args) {
        _args = args;
      }

      public T[] Swizzle<T>(T[] inputTypes) {
        T[] result = new T[_args.Length];

        for (int i = 0; i < _args.Length; ++i) {
          result[i] = inputTypes[_args[i]];
        }

        return result;
      }
    }
  }
}

#endregion


#region Assets/Photon/Quantum/Runtime/Utils/SerializedObjectExtensions.cs

#if UNITY_EDITOR
namespace Quantum {
  using System;
  using System.Collections;
  using System.Collections.Generic;
  using System.Linq.Expressions;
  using System.Reflection;
  using System.Text;
  using System.Text.RegularExpressions;
  using Photon.Analyzer;
  using UnityEditor;

  /// <summary>
  /// Quantum utilities to work with Unity serialized objects.
  /// </summary>
  public static class SerializedObjectExtensions {
    [StaticField(StaticFieldResetMode.None)]
    private static readonly Regex _arrayElementRegex = new Regex(@"\.Array\.data\[\d+\]$", RegexOptions.Compiled);

    /// <summary>
    /// Find a property in the serialized object or throw an exception if not found.
    /// </summary>
    /// <param name="so">ScriptableObject</param>
    /// <param name="propertyPath">Property path</param>
    /// <returns></returns>
    /// <exception cref="ArgumentOutOfRangeException">Is raised when not found</exception>
    public static SerializedProperty FindPropertyOrThrow(this SerializedObject so, string propertyPath) {
      var result = so.FindProperty(propertyPath);
      if (result == null)
        throw new ArgumentOutOfRangeException($"Property not found: {propertyPath}");
      return result;
    }

    /// <summary>
    /// Find a property at a relative path to the current property or throw an exception if not found.
    /// </summary>
    /// <param name="sp">Serialized property to start searching from</param>
    /// <param name="relativePropertyPath">Relative path to current property</param>
    /// <returns></returns>
    /// <exception cref="ArgumentOutOfRangeException">Is raised when not found</exception>
    public static SerializedProperty FindPropertyRelativeOrThrow(this SerializedProperty sp, string relativePropertyPath) {
      var result = sp.FindPropertyRelative(relativePropertyPath);
      if (result == null)
        throw new ArgumentOutOfRangeException($"Property not found: {relativePropertyPath} (in {sp.propertyPath})");
      return result;
    }

    /// <summary>
    /// Find a property at a relative path to the parent property
    /// </summary>
    /// <param name="property">Serialized property to start searching from</param>
    /// <param name="relativePath">Relative path from the parent</param>
    /// <returns>Found property or null</returns>
    public static SerializedProperty FindPropertyRelativeToParent(this SerializedProperty property, string relativePath) {
      SerializedProperty otherProperty;

      var path = property.propertyPath;

      // array element?
      if (path[path.Length - 1] == ']') {
        var match = _arrayElementRegex.Match(path);
        if (match.Success) {
          path = path.Substring(0, match.Index);
        }
      }

      var lastDotIndex = path.LastIndexOf('.');
      if (lastDotIndex < 0) {
        otherProperty = property.serializedObject.FindProperty(relativePath);
      } else {
        otherProperty = property.serializedObject.FindProperty(path.Substring(0, lastDotIndex));
        if (otherProperty != null) {
          otherProperty = otherProperty.FindPropertyRelative(relativePath);
        }
      }

      return otherProperty;
    }

    /// <summary>
    /// Find a property at a relative path to the parent property or throw an exception if not found.
    /// </summary>
    /// <param name="property">Serialized property to start searching from</param>
    /// <param name="relativePath">Relative path from the parent</param>
    /// <returns>Found property or null</returns>
    /// <exception cref="ArgumentOutOfRangeException">Is raised when not found</exception>
    public static SerializedProperty FindPropertyRelativeToParentOrThrow(this SerializedProperty property, string relativePath) {
      var result = property.FindPropertyRelativeToParent(relativePath);
      if (result == null) {
        throw new ArgumentOutOfRangeException($"Property relative to the parent of \"{property.propertyPath}\" not found: {relativePath}");
      }

      return result;
    }

    /// <summary>
    /// Convert different property types to an integer value.
    /// </summary>
    /// <param name="sp">Property</param>
    /// <returns>The converted int value or 0</returns>
    public static Int64 GetIntegerValue(this SerializedProperty sp) {
      switch (sp.type) {
        case "int":
        case "bool": return sp.intValue;
        case "long": return sp.longValue;
        case "FP":   return sp.FindPropertyRelative("RawValue").longValue;
        case "Enum": return sp.intValue;
        default:
          switch (sp.propertyType) {
            case SerializedPropertyType.ObjectReference:
              return sp.objectReferenceInstanceIDValue;
          }

          return 0;
      }
    }

    /// <summary>
    /// Set an integer value to a different serialized property types.
    /// </summary>
    /// <param name="sp">Property</param>
    /// <param name="value">Value to set</param>
    /// <exception cref="NotSupportedException">Is raised if setting an integer is not supported</exception>
    public static void SetIntegerValue(this SerializedProperty sp, long value) {
      switch (sp.type) {
        case "int":
          sp.intValue = (int)value;
          break;
        case "bool":
          sp.boolValue = value != 0;
          break;
        case "long":
          sp.longValue = value;
          break;
        case "FP":
          sp.FindPropertyRelative("RawValue").longValue = value;
          break;
        case "Enum":
          sp.intValue = (int)value;
          break;
        default:
          throw new NotSupportedException($"Type {sp.type} is not supported");
      }
    }

    public static SerializedPropertyEnumerable Children(this SerializedProperty property, bool visibleOnly = true) {
      return new SerializedPropertyEnumerable(property, visibleOnly);
    }

    public static string GetPropertyPath<T, U>(Expression<Func<T, U>> propertyLambda) {
      Expression    expression  = propertyLambda.Body;
      StringBuilder pathBuilder = new StringBuilder();

      for (;;) {
        var fieldExpression = expression as MemberExpression;
        if (fieldExpression?.Member is FieldInfo field) {
          if (pathBuilder.Length != 0) {
            pathBuilder.Insert(0, '.');
          }

          pathBuilder.Insert(0, field.Name);
          expression = fieldExpression.Expression;
        } else {
          if (expression is ParameterExpression parameterExpression) {
            return pathBuilder.ToString();
          } else {
            throw new ArgumentException($"Only field expressions allowed: {expression}");
          }
        }
      }
    }

    public static SerializedProperty GetArraySizePropertyOrThrow(this SerializedProperty prop) {
      if (prop == null) {
        throw new ArgumentNullException(nameof(prop));
      }

      if (!prop.isArray) {
        throw new ArgumentException("Not an array", nameof(prop));
      }

      var copy = prop.Copy();
      if (!copy.Next(true) || !copy.Next(true)) {
        throw new InvalidOperationException();
      }

      if (copy.propertyType != SerializedPropertyType.ArraySize) {
        throw new InvalidOperationException();
      }

      return copy;
    }

    public struct SerializedPropertyEnumerable : IEnumerable<SerializedProperty> {
      private SerializedProperty property;
      private bool               visible;

      public SerializedPropertyEnumerable(SerializedProperty property, bool visible) {
        this.property = property;
        this.visible  = visible;
      }

      public SerializedPropertyEnumerator GetEnumerator() {
        return new SerializedPropertyEnumerator(property, visible);
      }

      IEnumerator<SerializedProperty> IEnumerable<SerializedProperty>.GetEnumerator() {
        return GetEnumerator();
      }

      IEnumerator IEnumerable.GetEnumerator() {
        return GetEnumerator();
      }
    }

    public struct SerializedPropertyEnumerator : IEnumerator<SerializedProperty> {
      private SerializedProperty current;
      private bool               enterChildren;
      private bool               visible;
      private int                parentDepth;

      public SerializedPropertyEnumerator(SerializedProperty parent, bool visible) {
        current       = parent.Copy();
        enterChildren = true;
        parentDepth   = parent.depth;
        this.visible  = visible;
      }

      public SerializedProperty Current => current;

      SerializedProperty IEnumerator<SerializedProperty>.Current => current;

      object IEnumerator.Current => current;

      public void Dispose() {
        current.Dispose();
      }

      public bool MoveNext() {
        bool entered = visible ? current.NextVisible(enterChildren) : current.Next(enterChildren);
        enterChildren = false;
        if (!entered) {
          return false;
        }

        if (current.depth <= parentDepth) {
          return false;
        }

        return true;
      }

      public void Reset() {
        throw new NotImplementedException();
      }
    }
  }

  public class SerializedPropertyPathBuilder<T> {
    public static string GetPropertyPath<U>(Expression<Func<T, U>> expression) {
      return SerializedObjectExtensions.GetPropertyPath(expression);
    }
  }

  public class SerializedPropertyEqualityComparer : IEqualityComparer<SerializedProperty> {
    [StaticField(StaticFieldResetMode.None)]
    public static SerializedPropertyEqualityComparer Instance = new SerializedPropertyEqualityComparer();

    public bool Equals(SerializedProperty x, SerializedProperty y) {
      return SerializedProperty.DataEquals(x, y);
    }

    public int GetHashCode(SerializedProperty p) {
      bool enterChildren;
      bool isFirst  = true;
      int  hashCode = 0;
      int  minDepth = p.depth + 1;

      do {
        enterChildren = false;

        switch (p.propertyType) {
          case SerializedPropertyType.Integer:
            hashCode = HashCodeUtils.CombineHashCodes(hashCode, p.intValue);
            break;
          case SerializedPropertyType.Boolean:
            hashCode = HashCodeUtils.CombineHashCodes(hashCode, p.boolValue.GetHashCode());
            break;
          case SerializedPropertyType.Float:
            hashCode = HashCodeUtils.CombineHashCodes(hashCode, p.floatValue.GetHashCode());
            break;
          case SerializedPropertyType.String:
            hashCode = HashCodeUtils.CombineHashCodes(hashCode, p.stringValue.GetHashCode());
            break;
          case SerializedPropertyType.Color:
            hashCode = HashCodeUtils.CombineHashCodes(hashCode, p.colorValue.GetHashCode());
            break;
          case SerializedPropertyType.ObjectReference:
            hashCode = HashCodeUtils.CombineHashCodes(hashCode, p.objectReferenceInstanceIDValue);
            break;
          case SerializedPropertyType.LayerMask:
            hashCode = HashCodeUtils.CombineHashCodes(hashCode, p.intValue);
            break;
          case SerializedPropertyType.Enum:
            hashCode = HashCodeUtils.CombineHashCodes(hashCode, p.intValue);
            break;
          case SerializedPropertyType.Vector2:
            hashCode = HashCodeUtils.CombineHashCodes(hashCode, p.vector2Value.GetHashCode());
            break;
          case SerializedPropertyType.Vector3:
            hashCode = HashCodeUtils.CombineHashCodes(hashCode, p.vector3Value.GetHashCode());
            break;
          case SerializedPropertyType.Vector4:
            hashCode = HashCodeUtils.CombineHashCodes(hashCode, p.vector4Value.GetHashCode());
            break;
          case SerializedPropertyType.Vector2Int:
            hashCode = HashCodeUtils.CombineHashCodes(hashCode, p.vector2IntValue.GetHashCode());
            break;
          case SerializedPropertyType.Vector3Int:
            hashCode = HashCodeUtils.CombineHashCodes(hashCode, p.vector3IntValue.GetHashCode());
            break;
          case SerializedPropertyType.Rect:
            hashCode = HashCodeUtils.CombineHashCodes(hashCode, p.rectValue.GetHashCode());
            break;
          case SerializedPropertyType.RectInt:
            hashCode = HashCodeUtils.CombineHashCodes(hashCode, p.rectIntValue.GetHashCode());
            break;
          case SerializedPropertyType.ArraySize:
            hashCode = HashCodeUtils.CombineHashCodes(hashCode, p.intValue);
            break;
          case SerializedPropertyType.Character:
            hashCode = HashCodeUtils.CombineHashCodes(hashCode, p.intValue.GetHashCode());
            break;
          case SerializedPropertyType.AnimationCurve:
            hashCode = HashCodeUtils.CombineHashCodes(hashCode, p.animationCurveValue.GetHashCode());
            break;
          case SerializedPropertyType.Bounds:
            hashCode = HashCodeUtils.CombineHashCodes(hashCode, p.boundsValue.GetHashCode());
            break;
          case SerializedPropertyType.BoundsInt:
            hashCode = HashCodeUtils.CombineHashCodes(hashCode, p.boundsIntValue.GetHashCode());
            break;
          case SerializedPropertyType.ExposedReference:
            hashCode = HashCodeUtils.CombineHashCodes(hashCode, p.exposedReferenceValue.GetHashCode());
            break;
          default: {
            enterChildren = true;
            break;
          }
        }

        if (isFirst) {
          if (!enterChildren) {
            // no traverse needed
            return hashCode;
          }

          // since property is going to be traversed, a copy needs to be made
          p       = p.Copy();
          isFirst = false;
        }
      } while (p.Next(enterChildren) && p.depth >= minDepth);

      return hashCode;
    }
  }
}

#endif

#endregion

#endif
