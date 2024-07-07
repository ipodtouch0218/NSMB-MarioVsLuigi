namespace Quantum {
  using Photon.Deterministic;
  using UnityEditor;
  using UnityEngine;
#if QUANTUM_ENABLE_NEWTONSOFT
  using Newtonsoft.Json;
#endif

  /// <summary>
  /// An example of how to start a Quantum replay simulation from a replay file.
  /// </summary>
  public class QuantumRunnerLocalReplay : QuantumMonoBehaviour {
    /// <summary>
    /// Set the <see cref="DeltaTypeType" /> to <see cref="SimulationUpdateTime.EngineDeltaTime" /> to not progress the
    /// simulation time during break points.
    /// </summary>
    [Tooltip("Set the DeltaTimeType to EngineDeltaTime to not progress the simulation time during break points.")]
    public SimulationUpdateTime DeltaTypeType = SimulationUpdateTime.EngineDeltaTime;

    /// <summary>
    /// Replay JSON file.
    /// </summary>
    public TextAsset ReplayFile;
    /// <summary>
    /// Quantum asset database Json file.
    /// </summary>
    public TextAsset DatabaseFile;
    /// <summary>
    /// Simulation speed multiplier to playback the replay in a different speed.
    /// </summary>
    public float SimulationSpeedMultiplier = 1.0f;
    /// <summary>
    /// Toggle the replay gui label on/off.
    /// </summary>
    public bool ShowReplayLabel;
    /// <summary>
    /// Instant replay configurations to start the replay with.
    /// </summary>
    public InstantReplaySettings InstantReplayConfig = InstantReplaySettings.Default;
    /// <summary>
    /// Force Unity json deserialization of the replay file even when Newtonsoft is available.
    /// Newtonsoft is very slow when compiled with IL2CPP. But Unity Json deserialization expects the byte arrays to be int array instead on base64 strings.
    /// </summary>
    public bool ForceUsingUnityJson;

    QuantumRunner _runner;
    IResourceManager _resourceManager;
    Native.Allocator _resourceAllocator;
    IDeterministicReplayProvider _replayInputProvider;

    /// <summary>
    /// Unity start event, will start the Quantum runner and simulation after deserializing the replay file.
    /// </summary>
    public void Start() {
      if (_runner != null)
        return;

      if (ReplayFile == null) {
        Debug.LogError("QuantumRunnerLocalReplay - not replay file selected.");
        return;
      }

      var serializer = new QuantumUnityJsonSerializer();
      var replayFile = JsonUtility.FromJson<QuantumReplayFile>(ReplayFile.text);
      
      if (replayFile == null) {
        Debug.LogError("Failed to read replay file or file is empty.");
        return;
      }

      Debug.Log("### Starting quantum in local replay mode ###");

      // Create a new input provider from the replay file
      _replayInputProvider = replayFile.CreateInputProvider();
      if (_replayInputProvider == null) {
        Debug.LogError("Failed to load input history.");
        return;
      }

      var arguments = new SessionRunner.Arguments {
        RunnerFactory = QuantumRunnerUnityFactory.DefaultFactory,
        RuntimeConfig = serializer.ConfigFromByteArray<RuntimeConfig>(replayFile.RuntimeConfigData.Decode(), compressed: true),
        SessionConfig = replayFile.DeterministicConfig,
        ReplayProvider = _replayInputProvider,
        GameMode = DeterministicGameMode.Replay,
        RunnerId = "LOCALREPLAY",
        PlayerCount = replayFile.DeterministicConfig.PlayerCount,
        InstantReplaySettings = InstantReplayConfig,
        InitialTick = replayFile.InitialTick,
        FrameData = replayFile.InitialFrameData,
        DeltaTimeType = DeltaTypeType,
      };

      var assets = replayFile.AssetDatabaseData?.Decode();
      if (DatabaseFile != null) {
        assets = DatabaseFile.bytes;
      }

      if (assets?.Length > 0) {
        _resourceAllocator = new QuantumUnityNativeAllocator();
        _resourceManager = new ResourceManagerStatic(serializer.AssetsFromByteArray(assets), new QuantumUnityNativeAllocator());
        arguments.ResourceManager = _resourceManager;
      }

      _runner = QuantumRunner.StartGame(arguments);

      if (replayFile.Checksums?.Checksums != null) {
        _runner.Game.StartVerifyingChecksums(replayFile.Checksums);
      }
    }

    /// <summary>
    /// Unity Update event will update the simulation if a custom <see cref="SimulationSpeedMultiplier"/> was set.
    /// </summary>
    public void Update() {
      if (QuantumRunner.Default != null && QuantumRunner.Default.Session != null) {
        // Set the session ticking to manual to inject custom delta time.
        QuantumRunner.Default.IsSessionUpdateDisabled = SimulationSpeedMultiplier != 1.0f;
        if (QuantumRunner.Default.IsSessionUpdateDisabled) {
          switch (QuantumRunner.Default.DeltaTimeType) {
            case SimulationUpdateTime.Default:
            case SimulationUpdateTime.EngineUnscaledDeltaTime:
              QuantumRunner.Default.Service(Time.unscaledDeltaTime * SimulationSpeedMultiplier);
              QuantumUnityDB.UpdateGlobal();
              break;
            case SimulationUpdateTime.EngineDeltaTime:
              QuantumRunner.Default.Service(Time.deltaTime * SimulationSpeedMultiplier);
              QuantumUnityDB.UpdateGlobal();
              break;
          }
        }
      }

#if UNITY_EDITOR
      if (_replayInputProvider != null && _runner.Session?.IsReplayFinished == true) {
        EditorApplication.isPaused = true;
      }
#endif
    }

    private void OnDestroy() {
      _resourceManager?.Dispose();
      _resourceManager = null;
      _resourceAllocator?.Dispose();
      _resourceAllocator = null;
    }

#if UNITY_EDITOR
    private float guiTimer;

    void OnGUI() {
      if (ShowReplayLabel && _replayInputProvider != null && _runner.Session != null) {
        if (_runner.Session.IsReplayFinished) {
          GUI.contentColor = Color.red;
          GUI.Label(new Rect(10, 10, 200, 100), "REPLAY COMPLETED");
        } else {
          guiTimer += Time.deltaTime;
          if (guiTimer % 2.0f > 1.0f) {
            GUI.contentColor = Color.red;
            GUI.Label(new Rect(10, 10, 200, 100), "REPLAY PLAYING");
          }
        }
      }
    }
#endif
  }
}
