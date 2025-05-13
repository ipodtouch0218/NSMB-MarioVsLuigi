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
    /// Extra settings for shapes
    /// </summary>
    [Serializable]
    public struct SourceShapeGenericSettings {
      /// <summary>
      /// If the settings baked from a source collider are already scaled by the source GameObject.
      /// This can be used to avoid scaling the settings multiple times when the source is a child of the prototype.
      /// </summary>
      public bool IsScaledBySource;
#if QUANTUM_ENABLE_PHYSICS2D && !QUANTUM_DISABLE_PHYSICS2D
      /// <summary>
      /// The world axis that define the direction of the capsule in 2D.
      /// </summary>
      public UnityEngine.CapsuleDirection2D CapsuleDirection2D;
#endif
      /// <summary>
      /// The world axis that define the direction of the capsule in 3D.
      /// </summary>
      public Quantum.CapsuleDirection3D CapsuleDirection3D;
    }

    /// <summary>
    /// A all-purpose physics collider info.
    /// </summary>
    [Serializable]
    public struct PhysicsColliderGeneric {
      /// <summary>
      /// Is this prototype toggled on.
      /// </summary>
      [HideInInspector]
      public bool IsEnabled;
      /// <summary>
      /// Is the collider a trigger.
      /// </summary>
      [DrawIf(nameof(SourceCollider), 0)]
      public bool IsTrigger;
      /// <summary>
      /// The physics material to be set on the collider component.
      /// </summary>
      // Keep Quantum namespace here for migration with Unity 6 purposes.
      public AssetRef<Quantum.PhysicsMaterial> Material;
      /// <summary>
      /// The source collider to be used for the shape.
      /// </summary>
      public Component SourceCollider;
      /// <summary>
      /// Specific settings for the any type of shape
      /// </summary>
      [HideInInspector]
      public SourceShapeGenericSettings SourceShapeSettings;
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
      /// 
      /// </summary>
      public Quantum.Prototypes.PhysicsBodyInertiaMode InertiaMode;
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
      /// 
      /// </summary>
      [DrawIf(nameof(InertiaMode), (int)Quantum.Prototypes.PhysicsBodyInertiaMode.Explicit, Hide = true)]
      [DisplayName("Explicit Inertia")]
      public FP ExplicitInertia2D;
      /// <summary>
      /// 
      /// </summary>
      [DrawIf(nameof(InertiaMode), (int)Quantum.Prototypes.PhysicsBodyInertiaMode.Explicit, Hide = true)]
      [DisplayName("Explicit Inertia Tensor")]
      public FPVector3 ExplicitInertia3D;
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
      /// 
      /// </summary>
      [DrawIf(nameof(InertiaMode), (int)Quantum.Prototypes.PhysicsBodyInertiaMode.ParametricShape, Hide = true)]
      [DisplayName("Parametric Inertia Shape")]
      public Shape2DConfig ParametricInertiaShape2D;
      /// <summary>
      /// 
      /// </summary>
      [DrawIf(nameof(InertiaMode), (int)Quantum.Prototypes.PhysicsBodyInertiaMode.ParametricShape, Hide = true)]
      [DisplayName("Parametric Inertia Shape")]
      public Shape3DConfig ParametricInertiaShape3D;
      /// <summary>
      /// The scale applied to the body's inertia tensor.
      /// </summary>
      public NullableFP InertiaScale;
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
    public Transform2DVerticalInfo Transform2DVertical;
    /// <summary>
    /// The physics collider info.
    /// </summary>
    public PhysicsColliderGeneric PhysicsCollider;
    /// <summary>
    /// The physics body info.
    /// </summary>
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
      InertiaScale = new NullableFP() {
        _hasValue = 0,
        _value = FP._1
      },
      InertiaMode = Quantum.Prototypes.PhysicsBodyInertiaMode.ColliderShape,
      ExplicitInertia2D = FP._1,
      ExplicitInertia3D = FPVector3.One,
      ParametricInertiaShape2D = null,
      ParametricInertiaShape3D = null,
    };
    /// <summary>
    /// The pathfinder agent info.
    /// </summary>
    public NavMeshPathfinderInfo NavMeshPathfinder;
    /// <summary>
    /// The navmesh steering agent info.
    /// </summary>
    public NavMeshSteeringAgentInfo NavMeshSteeringAgent;
    /// <summary>
    /// The navmesh avoidance agent info.
    /// </summary>
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
        if (QPrototypePhysicsCollider2D.TrySetShapeConfigFromSourceCollider(PhysicsCollider.Shape2D, ref PhysicsCollider.SourceShapeSettings, transform, PhysicsCollider.SourceCollider, out var isTrigger)) {
          PhysicsCollider.IsTrigger = isTrigger;

          if (PhysicsCollider.LayerSource != QuantumEntityPrototypeColliderLayerSource.Explicit) {
            PhysicsCollider.Layer = PhysicsCollider.SourceCollider.gameObject.layer;
          }
        } else if (PhysicsCollider.LayerSource == QuantumEntityPrototypeColliderLayerSource.GameObject) {
          PhysicsCollider.Layer = this.gameObject.layer;
        }
        if (PhysicsCollider.Shape2D != null) {
          PhysicsCollider.Shape2D.CircleRadius = FPMath.Clamp(PhysicsCollider.Shape2D.CircleRadius, 0, PhysicsCollider.Shape2D.CircleRadius);
          PhysicsCollider.Shape2D.CapsuleSize.X = FPMath.Clamp(PhysicsCollider.Shape2D.CapsuleSize.X, 0, PhysicsCollider.Shape2D.CapsuleSize.X);
          PhysicsCollider.Shape2D.CapsuleSize.Y = FPMath.Clamp(PhysicsCollider.Shape2D.CapsuleSize.Y, 0, PhysicsCollider.Shape2D.CapsuleSize.Y);
          PhysicsCollider.Shape2D.BoxExtents.X = FPMath.Clamp(PhysicsCollider.Shape2D.BoxExtents.X, 0, PhysicsCollider.Shape2D.BoxExtents.X);
          PhysicsCollider.Shape2D.BoxExtents.Y = FPMath.Clamp(PhysicsCollider.Shape2D.BoxExtents.Y, 0, PhysicsCollider.Shape2D.BoxExtents.Y);
        }

        Transform2DVertical.Height = FPMath.Clamp(Transform2DVertical.Height, 0, Transform2DVertical.Height);

      } else if (TransformMode == QuantumEntityPrototypeTransformMode.Transform3D) {
        if (QPrototypePhysicsCollider3D.TrySetShapeConfigFromSourceCollider(PhysicsCollider.Shape3D, ref PhysicsCollider.SourceShapeSettings, transform, PhysicsCollider.SourceCollider, out var isTrigger)) {
          PhysicsCollider.IsTrigger = isTrigger;

          if (PhysicsCollider.LayerSource != QuantumEntityPrototypeColliderLayerSource.Explicit) {
            PhysicsCollider.Layer = PhysicsCollider.SourceCollider.gameObject.layer;
          }
          if (PhysicsCollider.Shape3D != null) {
            PhysicsCollider.Shape3D.BoxExtents.X = FPMath.Abs(PhysicsCollider.Shape3D.BoxExtents.X);
            PhysicsCollider.Shape3D.BoxExtents.Y = FPMath.Abs(PhysicsCollider.Shape3D.BoxExtents.Y);
            PhysicsCollider.Shape3D.BoxExtents.Z = FPMath.Abs(PhysicsCollider.Shape3D.BoxExtents.Z);
          }
        } else if (PhysicsCollider.LayerSource == QuantumEntityPrototypeColliderLayerSource.GameObject) {
          PhysicsCollider.Layer = this.gameObject.layer;
          if (PhysicsCollider.Shape3D != null) {
            PhysicsCollider.Shape3D.BoxExtents.X = FPMath.Clamp(PhysicsCollider.Shape3D.BoxExtents.X, 0, PhysicsCollider.Shape3D.BoxExtents.X);
            PhysicsCollider.Shape3D.BoxExtents.Y = FPMath.Clamp(PhysicsCollider.Shape3D.BoxExtents.Y, 0, PhysicsCollider.Shape3D.BoxExtents.Y);
            PhysicsCollider.Shape3D.BoxExtents.Z = FPMath.Clamp(PhysicsCollider.Shape3D.BoxExtents.Z, 0, PhysicsCollider.Shape3D.BoxExtents.Z);
          }
        }
        if (PhysicsCollider.Shape3D != null) {
          PhysicsCollider.Shape3D.SphereRadius = FPMath.Clamp(PhysicsCollider.Shape3D.SphereRadius, 0, PhysicsCollider.Shape3D.SphereRadius);
          PhysicsCollider.Shape3D.CapsuleRadius = FPMath.Clamp(PhysicsCollider.Shape3D.CapsuleRadius, 0, PhysicsCollider.Shape3D.CapsuleRadius);
          PhysicsCollider.Shape3D.CapsuleHeight = FPMath.Clamp(PhysicsCollider.Shape3D.CapsuleHeight, 0, PhysicsCollider.Shape3D.CapsuleHeight);
        }
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

    internal Shape2DConfig GetScaledShape2DConfig(Shape2DConfig from) {
      var result = new Shape2DConfig();
      var settings = default(SourceShapeGenericSettings);
      ScaleShapeConfig2D(transform, from, ref settings, result);
      return result;
    }
    
    internal Shape3DConfig GetScaledShape3DConfig(Shape3DConfig from) {
      var result = new Shape3DConfig();
      var settings = default(SourceShapeGenericSettings);
      ScaleShapeConfig3D(transform, from, ref settings, result);
      return result;
    }

    internal Shape2DConfig GetScaledShape2DConfig(ref PhysicsColliderGeneric colliderGeneric) {
      var result = new Shape2DConfig();
      ScaleShapeConfig2D(transform, colliderGeneric.Shape2D, ref colliderGeneric.SourceShapeSettings, result);
      return result;
    }
    
    internal Shape3DConfig GetScaledShape3DConfig(ref PhysicsColliderGeneric colliderGeneric) {
      var result = new Shape3DConfig();
      ScaleShapeConfig3D(transform, colliderGeneric.Shape3D, ref colliderGeneric.SourceShapeSettings, result);
      return result;
    }
    
    private static void ScaleShapeConfig2D(Transform t, Shape2DConfig from, ref SourceShapeGenericSettings settings, Shape2DConfig scaledTo) {
      if (Shape2DConfig.Copy(from, scaledTo) == false) {
        return;
      }

      var scale = settings.IsScaledBySource ? FPVector2.One : t.lossyScale.ToRoundedFPVector2();

      var absScale = scale;
      absScale.X = FPMath.Abs(absScale.X);
      absScale.Y = FPMath.Abs(absScale.Y);

      scaledTo.BoxExtents.X *= absScale.X;
      scaledTo.BoxExtents.Y *= absScale.Y;
      scaledTo.CircleRadius *= FPMath.Max(absScale.X, absScale.Y);
      scaledTo.EdgeExtent *= absScale.X;

#if QUANTUM_ENABLE_PHYSICS2D && !QUANTUM_DISABLE_PHYSICS2D
      if(settings.CapsuleDirection2D == CapsuleDirection2D.Horizontal) {
        scaledTo.CapsuleSize.X *= absScale.Y;
        scaledTo.CapsuleSize.Y *= absScale.X;
      } else {
        scaledTo.CapsuleSize.X *= absScale.X;
        scaledTo.CapsuleSize.Y *= absScale.Y;
      }
#endif

      scaledTo.PositionOffset.X *= scale.X;
      scaledTo.PositionOffset.Y *= scale.Y;

      if (scaledTo.CompoundShapes == null) {
        return;
      }

      Assert.Check(from.CompoundShapes != null);
      Assert.Check(from.CompoundShapes.Length == scaledTo.CompoundShapes.Length);

      for (int i = 0; i < scaledTo.CompoundShapes.Length; i++) {
        ref var scaledCompoundTo = ref scaledTo.CompoundShapes[i];

        scaledCompoundTo.BoxExtents.X *= absScale.X;
        scaledCompoundTo.BoxExtents.Y *= absScale.Y;
        scaledCompoundTo.CircleRadius *= FPMath.Max(absScale.X, absScale.Y);
        scaledCompoundTo.EdgeExtent *= absScale.X;

#if QUANTUM_ENABLE_PHYSICS2D && !QUANTUM_DISABLE_PHYSICS2D
        if (settings.CapsuleDirection2D == CapsuleDirection2D.Horizontal) {
          scaledCompoundTo.CapsuleSize.X *= absScale.Y;
          scaledCompoundTo.CapsuleSize.Y *= absScale.X;
        } else {
          scaledCompoundTo.CapsuleSize.X *= absScale.X;
          scaledCompoundTo.CapsuleSize.Y *= absScale.Y;
        }

        scaledCompoundTo.PositionOffset.X *= absScale.X;
        scaledCompoundTo.PositionOffset.Y *= absScale.Y;
#endif
      }
    }

    private static void ScaleShapeConfig3D(Transform t, Shape3DConfig from, ref SourceShapeGenericSettings settings, Shape3DConfig scaledTo) {
      if (Shape3DConfig.Copy(from, scaledTo) == false) {
        return;
      }

      var scale = settings.IsScaledBySource ? FPVector3.One : t.lossyScale.ToRoundedFPVector3();

      var absScale = scale;
      absScale.X = FPMath.Abs(absScale.X);
      absScale.Y = FPMath.Abs(absScale.Y);
      absScale.Z = FPMath.Abs(absScale.Z);

      var sphereRadius = FPMath.Max(absScale.X, absScale.Y, absScale.Z);

      scaledTo.BoxExtents.X *= absScale.X;
      scaledTo.BoxExtents.Y *= absScale.Y;
      scaledTo.BoxExtents.Z *= absScale.Z;

      scaledTo.SphereRadius *= sphereRadius;

      switch (settings.CapsuleDirection3D) {
        case CapsuleDirection3D.X:
          scaledTo.CapsuleRadius *= FPMath.Max(absScale.Y, absScale.Z);
          scaledTo.CapsuleHeight *= absScale.X;
          break;
        case CapsuleDirection3D.Y:
          scaledTo.CapsuleRadius *= FPMath.Max(absScale.X, absScale.Z);
          scaledTo.CapsuleHeight *= absScale.Y;
          break;
        case CapsuleDirection3D.Z:
          scaledTo.CapsuleRadius *= FPMath.Max(absScale.X, absScale.Y);
          scaledTo.CapsuleHeight *= absScale.Z;
          break;
      }
      
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

        scaledCompoundTo.BoxExtents.X *= absScale.X;
        scaledCompoundTo.BoxExtents.Y *= absScale.Y;
        scaledCompoundTo.BoxExtents.Z *= absScale.Z;

        scaledCompoundTo.SphereRadius *= sphereRadius;

        switch (settings.CapsuleDirection3D) {
          case CapsuleDirection3D.X:
            scaledCompoundTo.CapsuleRadius *= FPMath.Max(absScale.Y, absScale.Z);
            scaledCompoundTo.CapsuleHeight *= absScale.X;
            break;
          case CapsuleDirection3D.Y:
            scaledCompoundTo.CapsuleRadius *= FPMath.Max(absScale.X, absScale.Z);
            scaledCompoundTo.CapsuleHeight *= absScale.Y;
            break;
          case CapsuleDirection3D.Z:
            scaledCompoundTo.CapsuleRadius *= FPMath.Max(absScale.X, absScale.Y);
            scaledCompoundTo.CapsuleHeight *= absScale.Z;
            break;
        }

        scaledCompoundTo.PositionOffset.X *= absScale.X;
        scaledCompoundTo.PositionOffset.Y *= absScale.Y;
        scaledCompoundTo.PositionOffset.Z *= absScale.Z;
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
          verticalScale = FPMath.Abs(verticalScale);

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
            ShapeConfig = GetScaledShape2DConfig(ref PhysicsCollider),
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
              InertiaScale = PhysicsBody.InertiaScale,
              InertiaMode = PhysicsBody.InertiaMode,
              ExplicitInertia = PhysicsBody.ExplicitInertia2D,
              ParametricInertiaShape = GetScaledShape2DConfig(PhysicsBody.ParametricInertiaShape2D),
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
            ShapeConfig = GetScaledShape3DConfig(ref PhysicsCollider),
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
              InertiaScale = PhysicsBody.InertiaScale,
              InertiaMode = PhysicsBody.InertiaMode,
              ExplicitInertiaTensor = PhysicsBody.ExplicitInertia3D,
              ParametricInertiaShape = GetScaledShape3DConfig(PhysicsBody.ParametricInertiaShape3D),
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