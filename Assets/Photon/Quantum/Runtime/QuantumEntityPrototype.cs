#if QUANTUM_ENABLE_MIGRATION
using Quantum;
#endif
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Photon.Analyzer;
using Photon.Deterministic;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Quantum {

  /// <summary>
  /// Entity Prototypes are similar to blueprints or prefabs, they carry information
  /// to create a Quantum Entity with its components and initialized them with pre-configured data.
  /// </summary>
  [ScriptHelp(BackColor = ScriptHeaderBackColor.Blue)]
  public class QuantumEntityPrototype
#if QUANTUM_ENABLE_MIGRATION
#pragma warning disable CS0618
    : global::EntityPrototype { }
#pragma warning restore CS0618
} // namespace Quantum
  [Obsolete("Use QuantumEntityPrototype instead")]
  [LastSupportedVersion("3.0")]
  public abstract class EntityPrototype
#endif
    : QuantumMonoBehaviour, IQuantumPrototypeConvertible<MapEntityId> {

    /// <summary>
    /// Prototype settings for the <see cref="Transform2DVertical"/> component.
    /// </summary>
    [Serializable]
    public struct Transform2DVerticalInfo {
      /// <summary>
      /// Is the prototype toggled on.
      /// </summary>
      [HideInInspector]
      public bool IsEnabled;
      /// <summary>
      /// The height of the component.
      /// </summary>
      public FP Height;
      /// <summary>
      /// The current vertical position of offset of the component.
      /// </summary>
      public FP PositionOffset;
    }

    /// <summary>
    /// A all-purpose physics collider info.
    /// </summary>
    [Serializable]
    public struct PhysicsColliderGeneric {
      /// <summary>
      /// Is the collider a trigger.
      /// </summary>
      [DrawIf(nameof(SourceCollider), 0)]
      public bool IsTrigger;
      /// <summary>
      /// The physics material to be set on the collider component.
      /// </summary>
      public AssetRef<PhysicsMaterial> Material;
      /// <summary>
      /// The source collider to be used for the shape.
      /// </summary>
      public Component SourceCollider;
      /// <summary>
      /// Is this prototype toggled on.
      /// </summary>
      [HideInInspector]
      public bool IsEnabled;
      /// <summary>
      /// The 2D shape.
      /// </summary>
      [DisplayName("Type")]
      public Shape2DConfig Shape2D;
      /// <summary>
      /// The 3d shape.
      /// </summary>
      [DisplayName("Type")]
      public Shape3DConfig Shape3D;
      /// <summary>
      /// Used to scale the 2d shape to.
      /// </summary>
      [HideInInspector]
      public Shape2DConfig ScaledShape2D;
      /// <summary>
      /// Used to scale the 3d shape to.
      /// </summary>
      [HideInInspector]
      public Shape3DConfig ScaledShape3D;
      /// <summary>
      /// The source for the layer.
      /// </summary>
      public QuantumEntityPrototypeColliderLayerSource LayerSource;
      /// <summary>
      /// The initial physics layer.
      /// </summary>
      [Layer]
      [DrawIf(nameof(LayerSource), (Int32)QuantumEntityPrototypeColliderLayerSource.Explicit)]
      public int Layer;
      /// <summary>
      /// The callback flags.
      /// </summary>
      public QEnum32<CallbackFlags> CallbackFlags;
    }

    /// <summary>
    /// The all-purpose physics body info.
    /// </summary>
    [Serializable]
    public struct PhysicsBodyGeneric {
      /// <summary>
      /// Is the prototype toggled on.
      /// </summary>
      [HideInInspector]
      public bool IsEnabled;
      /// <summary>
      /// The 2d configuration flags.
      /// </summary>
      [DisplayName("Config")]
      public PhysicsBody2D.ConfigFlags Config2D;
      /// <summary>
      /// The 3d configuration flags.
      /// </summary>
      [DisplayName("Config")]
      public PhysicsBody3D.ConfigFlags Config3D;
      /// <summary>
      /// The freeze rotation configuration.
      /// </summary>
      public RotationFreezeFlags RotationFreeze;
      /// <summary>
      /// The body mass.
      /// </summary>
      public FP Mass;
      /// <summary>
      /// The body drag.
      /// </summary>
      public FP Drag;
      /// <summary>
      /// The body angular drag.
      /// </summary>
      public FP AngularDrag;
      /// <summary>
      /// The center of mass in 2d.
      /// </summary>
      [DisplayName("Center Of Mass")]
      public FPVector2 CenterOfMass2D;
      /// <summary>
      /// The center of mass in 3d.
      /// </summary>
      [DisplayName("Center Of Mass")]
      public FPVector3 CenterOfMass3D;
      /// <summary>
      /// The gravity scale.
      /// </summary>
      public NullableFP GravityScale;
    }

    /// <summary>
    /// The navmesh pathfinder info.
    /// </summary>
    [Serializable]
    public struct NavMeshPathfinderInfo {
      /// <summary>
      /// Is this prototype enabled.
      /// </summary>
      [HideInInspector]
      public bool IsEnabled;
      /// <summary>
      /// The navmesh agent configuration asset reference.
      /// </summary>
      public AssetRef<Quantum.NavMeshAgentConfig> NavMeshAgentConfig;
      /// <summary>
      /// The initial target to be set when the entity is spawned.
      /// </summary>
      [Optional("InitialTarget.IsEnabled")]
      public InitialNavMeshTargetInfo InitialTarget;
    }

    /// <summary>
    /// The initial navmesh target info.
    /// </summary>
    [Serializable]
    public struct InitialNavMeshTargetInfo {
      /// <summary>
      /// Is this prototype info enabled.
      /// </summary>
      [HideInInspector]
      public bool IsEnabled;
      /// <summary>
      /// The target transform, the transform position is saved during baking.
      /// </summary>
      public Transform Target;
      /// <summary>
      /// The world position of the target.
      /// </summary>
      [DrawIf("Target", 0)]
      public FPVector3 Position;
      /// <summary>
      /// The target navmesh.
      /// </summary>
      public NavMeshSpec NavMesh;
    }

    /// <summary>
    /// Navmesh specification for prototypes.
    /// </summary>
    [Serializable]
    public struct NavMeshSpec {
      /// <summary>
      /// Reference to a navmesh unity component.
      /// </summary>
      public QuantumMapNavMeshUnity Reference;
      /// <summary>
      /// Reference to a navmesh asset.
      /// </summary>
      public AssetRef<Quantum.NavMesh> Asset;
      /// <summary>
      /// The navmesh name.
      /// </summary>
      public string Name;
    }

    /// <summary>
    /// Navmesh steering agent info.
    /// </summary>
    [Serializable]
    public struct NavMeshSteeringAgentInfo {
      /// <summary>
      /// Is this prototype toggled on.
      /// </summary>
      [HideInInspector]
      public bool IsEnabled;
      /// <summary>
      /// Toggle on to set the initial max speed of the agent.
      /// </summary>
      [Optional("MaxSpeed.IsEnabled")]
      public OverrideFP MaxSpeed;
      /// <summary>
      /// Toggle on to set the initial acceleration of the agent.
      /// </summary>
      [Optional("Acceleration.IsEnabled")]
      public OverrideFP Acceleration;
    }

    /// <summary>
    /// Data object to store a bool and a fixed point to represent overriding default values.
    /// </summary>
    [Serializable]
    public struct OverrideFP {
      /// <summary>
      /// Is this info object toggled on.
      /// </summary>
      [HideInInspector]
      public bool IsEnabled;
      /// <summary>
      /// Value.
      /// </summary>
      public FP Value;
    }

    /// <summary>
    /// Navmesh avoidance agent info.
    /// </summary>
    [Serializable]
    public struct NavMeshAvoidanceAgentInfo {
      /// <summary>
      /// Is this prototype toggled on.
      /// </summary>
      [HideInInspector]
      public bool IsEnabled;
    }

    /// <summary>
    /// The prototype transform mode.
    /// </summary>
    public QuantumEntityPrototypeTransformMode TransformMode;
    /// <summary>
    /// The transform 2d info.
    /// </summary>
    [DrawIf("TransformMode", (int)QuantumEntityPrototypeTransformMode.Transform2D, mode: DrawIfMode.Hide)]
    public Transform2DVerticalInfo Transform2DVertical;
    /// <summary>
    /// The physics collider info.
    /// </summary>
    [DrawIf("TransformMode", (int)QuantumEntityPrototypeTransformMode.None, CompareOperator.NotEqual, DrawIfMode.Hide)]
    public PhysicsColliderGeneric PhysicsCollider;
    /// <summary>
    /// The physics body info.
    /// </summary>
    [DrawIf("PhysicsCollider.IsTrigger", 0, mode: DrawIfMode.Hide)]
    [DrawIf("PhysicsCollider.IsEnabled", 1, mode: DrawIfMode.Hide)]
    [DrawIf("TransformMode", (int)QuantumEntityPrototypeTransformMode.None, CompareOperator.NotEqual, DrawIfMode.Hide)]
    [Tooltip("To enable make sure PhysicsCollider is enabled and not a trigger")]
    public PhysicsBodyGeneric PhysicsBody = new PhysicsBodyGeneric() {
      Config2D = PhysicsBody2D.ConfigFlags.Default,
      Config3D = PhysicsBody3D.ConfigFlags.Default,
      Mass = 1,
      Drag = FP._0_50,
      AngularDrag = FP._0_50,
      CenterOfMass2D = FPVector2.Zero,
      CenterOfMass3D = FPVector3.Zero,
      GravityScale = new NullableFP() {
        _hasValue = 0,
        _value = FP._1
      },
    };
    /// <summary>
    /// The pathfinder agent info.
    /// </summary>
    [DrawIf("TransformMode", (int)QuantumEntityPrototypeTransformMode.None, CompareOperator.NotEqual, DrawIfMode.Hide)]
    public NavMeshPathfinderInfo NavMeshPathfinder;
    /// <summary>
    /// The navmesh steering agent info.
    /// </summary>
    [DrawIf("NavMeshPathfinder.IsEnabled", 1, mode: DrawIfMode.Hide)]
    [DrawIf("TransformMode", (int)QuantumEntityPrototypeTransformMode.None, CompareOperator.NotEqual, DrawIfMode.Hide)]
    public NavMeshSteeringAgentInfo NavMeshSteeringAgent;
    /// <summary>
    /// The navmesh avoidance agent info.
    /// </summary>
    [DrawIf("NavMeshPathfinder.IsEnabled", 1, mode: DrawIfMode.Hide)]
    [DrawIf("NavMeshSteeringAgent.IsEnabled", 1, mode: DrawIfMode.Hide)]
    [DrawIf("TransformMode", (int)QuantumEntityPrototypeTransformMode.None, CompareOperator.NotEqual, DrawIfMode.Hide)]
    public NavMeshAvoidanceAgentInfo NavMeshAvoidanceAgent;
    /// <summary>
    /// The entity view asset reference.
    /// </summary>
    public AssetRef<Quantum.EntityView> View;

    /// <summary>
    /// Post process the prototype based on configuration settings.
    /// </summary>
    public void PreSerialize() {
      if (TransformMode == QuantumEntityPrototypeTransformMode.Transform2D) {
        if (QPrototypePhysicsCollider2D.TrySetShapeConfigFromSourceCollider(PhysicsCollider.Shape2D, transform, PhysicsCollider.SourceCollider, out var isTrigger)) {
          PhysicsCollider.IsTrigger = isTrigger;

          if (PhysicsCollider.LayerSource != QuantumEntityPrototypeColliderLayerSource.Explicit) {
            PhysicsCollider.Layer = PhysicsCollider.SourceCollider.gameObject.layer;
          }
        } else if (PhysicsCollider.LayerSource == QuantumEntityPrototypeColliderLayerSource.GameObject) {
          PhysicsCollider.Layer = this.gameObject.layer;
        }

        ScaleShapeConfig2D(this.transform, PhysicsCollider.Shape2D, PhysicsCollider.ScaledShape2D);
      } else if (TransformMode == QuantumEntityPrototypeTransformMode.Transform3D) {
        if (QPrototypePhysicsCollider3D.TrySetShapeConfigFromSourceCollider(PhysicsCollider.Shape3D, transform, PhysicsCollider.SourceCollider, out var isTrigger)) {
          PhysicsCollider.IsTrigger = isTrigger;

          if (PhysicsCollider.LayerSource != QuantumEntityPrototypeColliderLayerSource.Explicit) {
            PhysicsCollider.Layer = PhysicsCollider.SourceCollider.gameObject.layer;
          }
        } else if (PhysicsCollider.LayerSource == QuantumEntityPrototypeColliderLayerSource.GameObject) {
          PhysicsCollider.Layer = this.gameObject.layer;
        }

        ScaleShapeConfig3D(this.transform, PhysicsCollider.Shape3D, PhysicsCollider.ScaledShape3D);
      }

      {
        if (PhysicsBody.IsEnabled) {
          if (TransformMode == QuantumEntityPrototypeTransformMode.Transform2D) {
            PhysicsBody.RotationFreeze = (PhysicsBody.Config2D & PhysicsBody2D.ConfigFlags.FreezeRotation) == PhysicsBody2D.ConfigFlags.FreezeRotation ? RotationFreezeFlags.FreezeAll : default;
          }
        }
      }

      if (NavMeshPathfinder.IsEnabled) {
        if (NavMeshPathfinder.InitialTarget.Target != null) {
          NavMeshPathfinder.InitialTarget.Position = NavMeshPathfinder.InitialTarget.Target.position.ToFPVector3();
        }

        if (NavMeshPathfinder.InitialTarget.NavMesh.Reference != null) {
          NavMeshPathfinder.InitialTarget.NavMesh.Asset = default;
          NavMeshPathfinder.InitialTarget.NavMesh.Name = NavMeshPathfinder.InitialTarget.NavMesh.Reference.name;
        }
      }
    }

    private static void ScaleShapeConfig2D(Transform t, Shape2DConfig from, Shape2DConfig scaledTo) {
      if (Shape2DConfig.Copy(from, scaledTo) == false) {
        return;
      }

      var scale = t.lossyScale.ToFPVector2();

      scaledTo.BoxExtents.X *= scale.X;
      scaledTo.BoxExtents.Y *= scale.Y;
      scaledTo.CircleRadius *= FPMath.Max(scale.X, scale.Y);
      scaledTo.EdgeExtent *= scale.X;

      scaledTo.CapsuleSize.X *= scale.X;
      scaledTo.CapsuleSize.Y *= scale.Y;

      scaledTo.PositionOffset.X *= scale.X;
      scaledTo.PositionOffset.Y *= scale.Y;

      if (scaledTo.CompoundShapes == null) {
        return;
      }

      Assert.Check(from.CompoundShapes != null);
      Assert.Check(from.CompoundShapes.Length == scaledTo.CompoundShapes.Length);

      for (int i = 0; i < scaledTo.CompoundShapes.Length; i++) {
        ref var scaledCompoundTo = ref scaledTo.CompoundShapes[i];

        scaledCompoundTo.BoxExtents.X *= scale.X;
        scaledCompoundTo.BoxExtents.Y *= scale.Y;
        scaledCompoundTo.CircleRadius *= FPMath.Max(scale.X, scale.Y);
        scaledCompoundTo.EdgeExtent *= scale.X;

        scaledCompoundTo.CapsuleSize.X *= scale.X;
        scaledCompoundTo.CapsuleSize.Y *= scale.Y;

        scaledCompoundTo.PositionOffset.X *= scale.X;
        scaledCompoundTo.PositionOffset.Y *= scale.Y;
      }
    }

    private static void ScaleShapeConfig3D(Transform t, Shape3DConfig from, Shape3DConfig scaledTo) {
      if (Shape3DConfig.Copy(from, scaledTo) == false) {
        return;
      }

      var scale = t.lossyScale.ToFPVector3();
      var sphereRadius = FPMath.Max(scale.X, scale.Y, scale.Z);

      scaledTo.BoxExtents.X *= scale.X;
      scaledTo.BoxExtents.Y *= scale.Y;
      scaledTo.BoxExtents.Z *= scale.Z;

      scaledTo.SphereRadius *= sphereRadius;
      scaledTo.CapsuleRadius *= FPMath.Max(scale.X, scale.Z);
      scaledTo.CapsuleHeight *= scale.Y;

      scaledTo.PositionOffset.X *= scale.X;
      scaledTo.PositionOffset.Y *= scale.Y;
      scaledTo.PositionOffset.Z *= scale.Z;

      if (scaledTo.CompoundShapes == null) {
        return;
      }

      Assert.Check(from.CompoundShapes != null);
      Assert.Check(from.CompoundShapes.Length == scaledTo.CompoundShapes.Length);

      for (int i = 0; i < scaledTo.CompoundShapes.Length; i++) {
        ref var scaledCompoundTo = ref scaledTo.CompoundShapes[i];

        scaledCompoundTo.BoxExtents.X *= scale.X;
        scaledCompoundTo.BoxExtents.Y *= scale.Y;
        scaledCompoundTo.BoxExtents.Z *= scale.Z;

        scaledCompoundTo.SphereRadius *= sphereRadius;
        scaledCompoundTo.CapsuleRadius *= sphereRadius;
        scaledCompoundTo.CapsuleHeight *= scale.Y;

        scaledCompoundTo.PositionOffset.X *= scale.X;
        scaledCompoundTo.PositionOffset.Y *= scale.Y;
        scaledCompoundTo.PositionOffset.Z *= scale.Z;
      }
    }

    /// <summary>
    /// Serializes known prototypes.
    /// </summary>
    /// <param name="result">Resulting component prototypes</param>
    /// <param name="selfView">Resulting entity view</param>
    public void SerializeImplicitComponents(List<ComponentPrototype> result, out QuantumEntityView selfView) {
      if (TransformMode == QuantumEntityPrototypeTransformMode.Transform2D) {
        result.Add(new Quantum.Prototypes.Transform2DPrototype() {
          Position = transform.position.ToFPVector2(),
          Rotation = transform.rotation.ToFPRotation2DDegrees(),
        });

        if (Transform2DVertical.IsEnabled) {
#if QUANTUM_XY
        var verticalScale = transform.lossyScale.z.ToFP();
#else
          var verticalScale = transform.lossyScale.y.ToFP();
#endif
          result.Add(new Quantum.Prototypes.Transform2DVerticalPrototype() {
            Position = transform.position.ToFPVerticalPosition() + (Transform2DVertical.PositionOffset * verticalScale),
            Height = Transform2DVertical.Height * verticalScale,
          });
        }

        if (PhysicsCollider.IsEnabled) {
          result.Add(new Quantum.Prototypes.PhysicsCollider2DPrototype() {
            IsTrigger = PhysicsCollider.IsTrigger,
            Layer = PhysicsCollider.Layer,
            PhysicsMaterial = PhysicsCollider.Material,
            ShapeConfig = PhysicsCollider.ScaledShape2D
          });

          result.Add(new Quantum.Prototypes.PhysicsCallbacks2DPrototype() {
            CallbackFlags = PhysicsCollider.CallbackFlags,
          });

          if (!PhysicsCollider.IsTrigger && PhysicsBody.IsEnabled) {
            result.Add(new Quantum.Prototypes.PhysicsBody2DPrototype() {
              Config = PhysicsBody.Config2D,
              AngularDrag = PhysicsBody.AngularDrag,
              Drag = PhysicsBody.Drag,
              Mass = PhysicsBody.Mass,
              CenterOfMass = PhysicsBody.CenterOfMass2D,
              GravityScale = PhysicsBody.GravityScale,
            });
          }
        }
      } else if (TransformMode == QuantumEntityPrototypeTransformMode.Transform3D) {
        result.Add(new Quantum.Prototypes.Transform3DPrototype() {
          Position = transform.position.ToFPVector3(),
          Rotation = transform.rotation.eulerAngles.ToFPVector3(),
        });

        if (PhysicsCollider.IsEnabled) {
          result.Add(new Quantum.Prototypes.PhysicsCollider3DPrototype() {
            IsTrigger = PhysicsCollider.IsTrigger,
            Layer = PhysicsCollider.Layer,
            PhysicsMaterial = PhysicsCollider.Material,
            ShapeConfig = PhysicsCollider.ScaledShape3D
          });

          result.Add(new Quantum.Prototypes.PhysicsCallbacks3DPrototype() {
            CallbackFlags = PhysicsCollider.CallbackFlags,
          });

          if (!PhysicsCollider.IsTrigger && PhysicsBody.IsEnabled) {
            result.Add(new Quantum.Prototypes.PhysicsBody3DPrototype() {
              Config = PhysicsBody.Config3D,
              AngularDrag = PhysicsBody.AngularDrag,
              Drag = PhysicsBody.Drag,
              Mass = PhysicsBody.Mass,
              RotationFreeze = PhysicsBody.RotationFreeze,
              CenterOfMass = PhysicsBody.CenterOfMass3D,
              GravityScale = PhysicsBody.GravityScale,
            });
          }
        }
      }

      if (NavMeshPathfinder.IsEnabled) {
        var pathfinder = new Quantum.Prototypes.NavMeshPathfinderPrototype() { AgentConfig = NavMeshPathfinder.NavMeshAgentConfig };

        if (NavMeshPathfinder.InitialTarget.IsEnabled) {
          pathfinder.InitialTarget = NavMeshPathfinder.InitialTarget.Position;
          pathfinder.InitialTargetNavMesh = NavMeshPathfinder.InitialTarget.NavMesh.Asset;
          pathfinder.InitialTargetNavMeshName = NavMeshPathfinder.InitialTarget.NavMesh.Name;
        }

        result.Add(pathfinder);

        if (NavMeshSteeringAgent.IsEnabled) {
          result.Add(new Quantum.Prototypes.NavMeshSteeringAgentPrototype() {
            OverrideMaxSpeed = NavMeshSteeringAgent.MaxSpeed.IsEnabled,
            OverrideAcceleration = NavMeshSteeringAgent.Acceleration.IsEnabled,
            MaxSpeed = NavMeshSteeringAgent.MaxSpeed.Value,
            Acceleration = NavMeshSteeringAgent.Acceleration.Value
          });

          if (NavMeshAvoidanceAgent.IsEnabled) {
            result.Add(new Quantum.Prototypes.NavMeshAvoidanceAgentPrototype());
          }
        }
      }

      selfView = GetComponent<QuantumEntityView>();

      if (selfView) {
        // self, don't emit view
      } else if (View.Id.IsValid) {
        result.Add(new Quantum.Prototypes.ViewPrototype() {
          Current = View,
        });
      }
    }

    /// <summary>
    /// Validation step.
    /// </summary>
    /// <param name="duplicateCallback">Action to call on duplicate entries</param>
    [Conditional("UNITY_EDITOR")]
    public void CheckComponentDuplicates(Action<string> duplicateCallback) {
      CheckComponentDuplicates((type, sources) => {
        duplicateCallback($"Following components add {type.Name} prototype: {string.Join(", ", sources.Select(x => x.GetType()))}. The last one will be used.");
      });
    }

    /// <summary>
    /// Validation step.
    /// </summary>
    /// <param name="duplicateCallback">Dictionary of duplication detection callbacks</param>
    [Conditional("UNITY_EDITOR")]
    public void CheckComponentDuplicates(Action<Type, List<Component>> duplicateCallback) {
      var typeToSource = new Dictionary<Type, List<Component>>();


      var implicitPrototypes = new List<ComponentPrototype>();
      SerializeImplicitComponents(implicitPrototypes, out var dummy);

      foreach (var prototype in implicitPrototypes) {
        if (!typeToSource.TryGetValue(prototype.GetType(), out var sources)) {
          sources = new List<Component>();
          typeToSource.Add(prototype.GetType(), sources);
        }

        sources.Add(this);
      }

      foreach (var component in GetComponents<QuantumUnityComponentPrototype>()) {
        if (!typeToSource.TryGetValue(component.PrototypeType, out var sources)) {
          sources = new List<Component>();
          typeToSource.Add(component.PrototypeType, sources);
        }

        sources.Add(component);
      }

      foreach (var kv in typeToSource) {
        if (kv.Value.Count > 1) {
          duplicateCallback(kv.Key, kv.Value);
        }
      }
    }

    [StaticField(StaticFieldResetMode.None)]
    private static readonly List<QuantumUnityComponentPrototype> behaviourBuffer = new List<QuantumUnityComponentPrototype>();

    [StaticField(StaticFieldResetMode.None)]
    private static readonly List<ComponentPrototype> prototypeBuffer = new List<ComponentPrototype>();

    /// <summary>
    /// Initialize the prototype after being loaded.
    /// </summary>
    /// <param name="assetObject">Entity prototype asset object</param>
    /// <param name="selfViewAsset">Associated view asset</param>
    public void InitializeAssetObject(Quantum.EntityPrototype assetObject, Quantum.EntityView selfViewAsset) {
      try {
        // get built-ins first
        PreSerialize();
        SerializeImplicitComponents(prototypeBuffer, out var selfView);

        if (selfView) {
          if (selfViewAsset != null) {
            prototypeBuffer.Add(new Quantum.Prototypes.ViewPrototype() { Current = new() { Id = selfViewAsset.Guid } });
          } else {
            Debug.LogError($"Self-view detected, but the no {nameof(Quantum.EntityView)} provided in {name}. Reimport the prefab.", this);
          }
        }

        var converter = new QuantumEntityPrototypeConverter((QuantumEntityPrototype)this);

        // now get custom ones
        GetComponents(behaviourBuffer);
        {
          foreach (var component in behaviourBuffer) {
            component.Refresh();
            prototypeBuffer.Add(component.CreatePrototype(converter));
          }
        }

        // store
        assetObject.Container = ComponentPrototypeSet.FromArray(prototypeBuffer.ToArray());

      } finally {
        behaviourBuffer.Clear();
        prototypeBuffer.Clear();
      }
    }

    MapEntityId IQuantumPrototypeConvertible<MapEntityId>.Convert(QuantumEntityPrototypeConverter converter) {
      converter.Convert(this, out MapEntityId mapId);
      return mapId;
    }

#if UNITY_EDITOR
    private void OnValidate() {
      try {
        PreSerialize();
      } catch (Exception ex) {
        Debug.LogError($"EntityPrototype validation error: {ex.Message}", this);
      }

      CheckComponentDuplicates(msg => Debug.LogWarning(msg, gameObject));
    }
#endif
  }

#if !QUANTUM_ENABLE_MIGRATION
} // 
#endif