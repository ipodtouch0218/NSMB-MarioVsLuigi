namespace Quantum {
  using System;
  using Photon.Deterministic;
  using Quantum.Core;
  using Quantum.Allocator;
#if QUANTUM_UNITY
  using UnityEditor;
  using UnityEngine;
  using HideInInspector = UnityEngine.HideInInspector;
#endif

  /// <summary>
  /// The SimulationConfig holds parameters used in the ECS layer and inside core systems like physics and navigation.
  /// </summary>
  public partial class SimulationConfig : AssetObject {
    /// <summary>
    /// Obsolete: Don't use the hard coded guids instead reference the simulation config used in the RuntimeConfig.
    /// </summary>
    [Obsolete("Don't use the hard coded guids instead reference the simulation config used in the RuntimeConfig")]
    public const long DEFAULT_ID = (long)DefaultAssetGuids.SimulationConfig;

    /// <summary>
    /// The scene load mode to use when changing Quantum maps.
    /// <para>Will trigger for example for the initial map that is set in Quantum by <see cref="RuntimeConfig.Map"/> and on subsequent map changes.</para>
    /// <para>The Unity scene referenced by <see cref="Quantum.Map.Scene"/> will be loaded.</para>
    /// </summary>
    public enum AutoLoadSceneFromMapMode {
      /// <summary>
      /// Automatic scene loading disabled.
      /// </summary>
      Disabled,
      /// <summary>
      /// Obsolete: unused.
      /// </summary>
      [Obsolete]
      Legacy,
      /// <summary>
      /// Unload the current scene then load the new scene.
      /// </summary>
      UnloadPreviousSceneThenLoad,
      /// <summary>
      /// Load the new scene then unload the current scene.
      /// </summary>
      LoadThenUnloadPreviousScene
    }

    /// <summary>
    /// Global entities configuration
    /// </summary>
    [Space, InlineHelp]
    public FrameBase.EntitiesConfig Entities;
    
    /// <summary>
    /// Global physics configurations.
    /// </summary>
    [Space, InlineHelp]
    public PhysicsCommon.Config Physics;
    
    /// <summary>
    /// Global navmesh configurations.
    /// </summary>
    [Space, InlineHelp]
    public Navigation.Config Navigation;

    /// <summary>
    /// This option will trigger a Unity scene load during the Quantum start sequence.\n
    /// This might be convenient to start with but once the starting sequence is customized disable it and implement the scene loading by yourself.
    /// "Previous Scene" refers to a scene name in Quantum Map.
    /// </summary>
    [Space, InlineHelp]
    public AutoLoadSceneFromMapMode AutoLoadSceneFromMap = AutoLoadSceneFromMapMode.UnloadPreviousSceneThenLoad;

    /// <summary>
    /// Configure how the client tracks the time to progress the Quantum simulation from the QuantumRunner class.
    /// </summary>
    [Obsolete("Set on SessionRunner.Arguments.DeltaTimeType instead")]
    [HideInInspector]
    public SimulationUpdateTime DeltaTimeType = SimulationUpdateTime.Default;

    /// <summary>
    /// Override the number of threads used internally. Default is 2.
    /// </summary>
    [InlineHelp]
    [ErrorIf(nameof(ThreadCount), 0, "Thread Count must be greater than 0.", CompareOperator.LessOrEqual)]
    public int ThreadCount = 2;

    /// <summary>
    /// How long to store checksumed verified frames. The are used to generate a frame dump in case of a checksum error happening. Not used in Replay and Local mode. Default is 3.
    /// </summary>
    [InlineHelp]
    public FP ChecksumSnapshotHistoryLengthSeconds = 3;

    /// <summary>
    /// Additional options for checksum dumps, if the default settings don't provide a clear picture. 
    /// </summary>
    [InlineHelp]
    public SimulationConfigChecksumErrorDumpOptions ChecksumErrorDumpOptions;

    /// <summary>
    /// If and to which extent allocations in the Frame Heap should be tracked when in Debug mode.
    /// Recommended modes for development is `DetectLeaks`.
    /// While actively debugging a memory leak,`TraceAllocations` mode can be enabled (warning: tracing is very slow).
    /// </summary>
    [Header("Frame Heap Settings")]
    [InlineHelp]
    public HeapTrackingMode HeapTrackingMode = HeapTrackingMode.DetectLeaks;

    /// <summary>
    /// Define the max heap size for one page of memory the frame class uses for custom allocations like QList for example. The default is 15.
    /// </summary>
    /// <remarks>2^15 = 32.768 bytes</remarks>
    /// <remarks><code>TotalHeapSizeInBytes = (1 &lt;&lt; HeapPageShift) * HeapPageCount</code></remarks>
    [InlineHelp]
    public int HeapPageShift = 15;

    /// <summary>
    /// Define the max heap page count for memory the frame class uses for custom allocations like QList for example. Default is 256.
    /// </summary>
    /// <remarks><code>TotalHeapSizeInBytes = (1 &lt;&lt; HeapPageShift) * HeapPageCount</code></remarks>
    [InlineHelp]
    public int HeapPageCount = 256;

    /// <summary>
    /// Sets extra heaps to allocate for a session in case you need to
    /// create 'auxiliary' frames than actually required for the simulation itself.
    /// Default is 0.
    /// </summary>
    [InlineHelp]
    public int HeapExtraCount = 0;

    /// <summary>
    /// The asset loaded callback, caches fixed calculation results.
    /// </summary>
    /// <param name="resourceManager">Resource manager.</param>
    /// <param name="allocator">The allocator.</param>
    public override void Loaded(IResourceManager resourceManager, Native.Allocator allocator) {
      Physics.PenetrationCorrection = FPMath.Clamp01(Physics.PenetrationCorrection);
      ThreadCount = Math.Max(1, ThreadCount);
    }
    
#if QUANTUM_UNITY
    /// <summary>
    /// Unity Reset() method is used to initialized class fields with default values.
    /// </summary>
    public override void Reset() {
      Physics    = new PhysicsCommon.Config();
      Navigation = new Navigation.Config();

      ImportLayersFromUnity(PhysicsType.Physics3D);
    }

    void ImportLayersFromUnity3D() {
      ImportLayersFromUnity(PhysicsType.Physics3D);
#if UNITY_EDITOR
      EditorUtility.SetDirty(this);
#endif
    }
    
    void ImportLayersFromUnity2D() {
      ImportLayersFromUnity(PhysicsType.Physics2D);
#if UNITY_EDITOR
      EditorUtility.SetDirty(this);
#endif
    }
    
    /// <summary>
    /// Physics 2D or 3D used for importing layers from Unity.
    /// </summary>
    public enum PhysicsType {
      /// <summary>
      /// Quantum Physics 3D.
      /// </summary>
      Physics3D,
      /// <summary>
      /// Quantum Physics 2D.
      /// </summary>
      Physics2D
    }
    
    /// <summary>
    /// Import Unity physics layers.
    /// </summary>
    /// <param name="physicsType">The physics type to import from.</param>
    public void ImportLayersFromUnity(PhysicsType physicsType = PhysicsType.Physics3D) {
      Physics.Layers      = GetUnityLayerNameArray();
      Physics.LayerMatrix = GetUnityLayerMatrix(physicsType);
    }
    
    /// <summary>
    /// Creates 32 physics layer names from Unity.
    /// </summary>
    public static String[] GetUnityLayerNameArray() {
      var layers = new String[32];

      for (Int32 i = 0; i < layers.Length; ++i) {
        try {
          layers[i] = UnityEngine.LayerMask.LayerToName(i);
        } catch {
          // just eat exceptions
        }
      }

      return layers;
    }

    /// <summary>
    /// Creates 32 physics layer masks from Unity.
    /// </summary>
    /// <param name="physicsType"></param>
    public static Int32[] GetUnityLayerMatrix(PhysicsType physicsType) {
      var matrix = new Int32[32];

      for (Int32 a = 0; a < 32; ++a) {
        for (Int32 b = 0; b < 32; ++b) {
          bool ignoreLayerCollision = false;
          
          switch (physicsType) {
#if QUANTUM_ENABLE_PHYSICS3D && !QUANTUM_DISABLE_PHYSICS3D
            case PhysicsType.Physics3D:
              ignoreLayerCollision = UnityEngine.Physics.GetIgnoreLayerCollision(a, b);
              break;
#endif
#if QUANTUM_ENABLE_PHYSICS2D && !QUANTUM_DISABLE_PHYSICS2D
            case PhysicsType.Physics2D:
              ignoreLayerCollision = UnityEngine.Physics2D.GetIgnoreLayerCollision(a, b);
              break;
#endif
            default:
              break;
          }

          if (ignoreLayerCollision == false) {
            matrix[a] |= (1 << b);
            matrix[b] |= (1 << a);
          }
        }
      }

      return matrix;
    }    
#endif
  }

  /// <summary>
  /// Configuration options for checksum error dumps.
  /// </summary>
  [Flags]
  public enum SimulationConfigChecksumErrorDumpOptions {
    /// <summary>
    /// Sends asset db checksums.
    /// </summary>
    SendAssetDBChecksums = 1 << 0,
    /// <summary>
    /// Dumps readable information from the dynamic db.
    /// </summary>
    ReadableDynamicDB    = 1 << 1,
    /// <summary>
    /// Prints raw FP values.
    /// </summary>
    RawFPValues          = 1 << 2,
    /// <summary>
    /// Dumps component checksums.
    /// </summary>
    ComponentChecksums   = 1 << 3,
    /// <summary>
    /// Dumps 3D Physics SceneMesh metadata.
    /// </summary>
    SceneMesh3D          = 1 << 4,
  }

}
