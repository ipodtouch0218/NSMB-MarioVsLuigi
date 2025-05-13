namespace Quantum {
  using System;
  using System.Collections;
  using System.Linq;
  using Photon.Deterministic;
  using UnityEngine;
#if (QUANTUM_ADDRESSABLES || QUANTUM_ENABLE_ADDRESSABLES) && !QUANTUM_DISABLE_ADDRESSABLES
  using UnityEngine.AddressableAssets;
#endif
  using UnityEngine.Events;
  using UnityEngine.SceneManagement;
  using UnityEngine.Serialization;
  using static QuantumUnityExtensions;

  /// <summary>
  ///   A debug script that starts the Quantum simulation for <see cref="MaxPlayerCount" /> players when starting the game
  ///   from a gameplay scene.
  ///   Will add <see cref="LocalPlayers" /> as local players during simulation start.
  ///   The script will disable itself when it detects that other scene were loaded before this (to delegate adding players
  ///   to a menu scene / game bootstrap).
  /// </summary>
  public class QuantumRunnerLocalDebug : QuantumMonoBehaviour {
    /// <summary>
    /// Set the <see cref="DeltaTimeType" /> to <see cref="SimulationUpdateTime.EngineDeltaTime" /> to not progress the
    /// simulation during break points.
    /// Has to be set before starting the runner and can only be changed on the runner directly during runtime: <see cref="SessionRunner.DeltaTimeType"/>.
    /// </summary>
    [InlineHelp]
    [FormerlySerializedAs("DeltaTypeType")]
    public SimulationUpdateTime DeltaTimeType = SimulationUpdateTime.EngineDeltaTime;
    /// <summary>
    /// Use <see cref="DeltaTimeType" /> instead.
    /// </summary>
    [Obsolete("Renamed to DeltaTimeType")]
    public SimulationUpdateTime DeltaTypeType => DeltaTimeType;
    /// <summary>
    /// Set RecordingFlags of the local simulation to enable saving a replay.
    /// Caveat: Input recording allocates during runtime.
    /// </summary>
    [InlineHelp]
    public RecordingFlags RecordingFlags = RecordingFlags.None;
    /// <summary>
    /// Set InstantReplaySettings to enable instant replays.
    /// </summary>
    [InlineHelp]
    public InstantReplaySettings InstantReplayConfig = InstantReplaySettings.Default;
    /// <summary>
    /// Configure the RuntimeConfig used for the local simulation.
    /// </summary>
    [FormerlySerializedAs("Config")]
    [InlineHelp]
    public RuntimeConfig RuntimeConfig;
    /// <summary>
    /// If set to true, the <see cref="RuntimeConfig.Seed"/> seed will be set to a random value.
    /// </summary>
    [InlineHelp]
    public bool UseRandomSeed = false;
    /// <summary>
    /// Select the SessionConfig used for the local simulation. Will revert to the global default if not set.
    /// </summary>
    [InlineHelp]
    public QuantumDeterministicSessionConfigAsset SessionConfig;
    /// <summary>
    /// Configure the players added to the game after the simulation has started.
    /// </summary>
    [FormerlySerializedAs("Players")]
    [InlineHelp]
    public RuntimePlayer[] LocalPlayers;
    /// <summary>
    /// Overwrite the max player count for this simulation otherwise Quantum.Constants.PLAYER_COUNT is used. Default is 0.
    /// </summary>
    [InlineHelp]
    public int MaxPlayerCount;
    /// <summary>
    /// Set a factor to increase or decrease the simulation speed and update the simulation during Update(). Default is 1.
    /// </summary>
    [InlineHelp]
    public float SimulationSpeedMultiplier = 1.0f;
    /// <summary>
    /// Show the reload simulation button.
    /// </summary>
    [InlineHelp]
    public bool DisplaySaveAndReloadButton;
    /// <summary>
    /// Enabled loading Addressables before simulation start.
    /// </summary>
    [InlineHelp]
    public bool PreloadAddressables = false;
    /// <summary>
    /// Set a dynamic asset db.
    /// </summary>
    [InlineHelp]
    public DynamicAssetDBSettings DynamicAssetDB;
    /// <summary>
    /// Unity event that is called before the Quantum simulation is started.
    /// </summary>
    public UnityEvent<SessionRunner.Arguments> OnBeforeStart;
    
    /// <summary>
    /// Unity start event, will start the Quantum simulation.
    /// </summary>
#if (QUANTUM_ADDRESSABLES || QUANTUM_ENABLE_ADDRESSABLES) && !QUANTUM_DISABLE_ADDRESSABLES
    public async void Start()
#else
  public void Start()
#endif
    {
      if (QuantumRunner.Default != null || SceneManager.sceneCount > 1) {
        // Prevents to start the simulation (again/twice) when..
        // a) there already is a runner, because the scene is reloaded during Quantum Unity map loading (AutoLoadSceneFromMap) or
        // b) this scene is not the first scene that is ever loaded (most likely a menu scene is involved that starts the simulation itself)
        enabled = false;
        return;
      }

      // Subscribe to the game started callback to add players
      QuantumCallback.Subscribe(this, (CallbackGameStarted c) => OnGameStarted(c.Game, c.IsResync), game => game == QuantumRunner.Default.Game);

#if (QUANTUM_ADDRESSABLES || QUANTUM_ENABLE_ADDRESSABLES) && !QUANTUM_DISABLE_ADDRESSABLES
      if (PreloadAddressables) {
        // there's also an overload that accepts a target list parameter
        var addressableAssets = QuantumUnityDB.Global.Entries
         .Where(x => x.Source is QuantumAssetObjectSourceAddressable)
         .Select(x => (x.Guid, ((QuantumAssetObjectSourceAddressable)x.Source).RuntimeKey));
        
        // preload all the addressable assets
        foreach (var (assetRef, address) in addressableAssets) {
          // there are a few ways to load an asset with Addressables (by label, by IResourceLocation, by address etc.)
          // but it seems that they're not fully interchangeable, i.e. loading by label will not make loading by address
          // be reported as done immediately; hence the only way to preload an asset for Quantum is to replicate
          // what it does internally, i.e. load with the very same parameters
          await Addressables.LoadAssetAsync<UnityEngine.Object>(address).Task;
        }
      }
#endif

      StartWithFrame(0, null);
    }

    /// <summary>
    /// Start the Quantum simulation with a specific frame number and frame data.
    /// </summary>
    /// <param name="frameNumber">Frame number</param>
    /// <param name="frameData">Frame data to start from</param>
    /// <exception cref="Exception">Is raised when no map was found in the scene.</exception>
    public void StartWithFrame(int frameNumber = 0, byte[] frameData = null) {
      Log.Debug("### Starting quantum in local debug mode ###");

      var mapdata = FindFirstObjectByType<QuantumMapData>();
      if (mapdata == null) {
        throw new Exception("No MapData object found, can't debug start scene");
      }

      // copy runtime config
      var serializer = new QuantumUnityJsonSerializer();
      var runtimeConfig = serializer.CloneConfig(RuntimeConfig);

      // always randomize the Quantum simulation seed when UseRandomSeed is enabled and the simulation is started from frame 0
      if (frameData == null && frameNumber == 0 && UseRandomSeed) {
        runtimeConfig.Seed = UnityEngine.Random.Range(int.MinValue, int.MaxValue);
      }

      // set map to this maps asset
      runtimeConfig.Map = mapdata.AssetRef;

      // if not set, try to set simulation config from global default configs
      if (runtimeConfig.SimulationConfig.Id.IsValid == false && QuantumDefaultConfigs.TryGetGlobal(out var defaultConfigs)) {
        runtimeConfig.SimulationConfig = defaultConfigs.SimulationConfig;
      }

      using var dynamicDB = new DynamicAssetDB(new QuantumUnityNativeAllocator(), DynamicAssetDB.IsLegacyModeEnabled);
      DynamicAssetDB.OnInitialDynamicAssetsRequested?.Invoke(dynamicDB);

      // create start game parameter
      var arguments = new SessionRunner.Arguments {
        RunnerFactory         = QuantumRunnerUnityFactory.DefaultFactory,
        GameParameters        = QuantumRunnerUnityFactory.CreateGameParameters,
        RuntimeConfig         = runtimeConfig,
        SessionConfig         = SessionConfig?.Config ?? QuantumDeterministicSessionConfigAsset.DefaultConfig,
        ReplayProvider        = null,
        GameMode              = DeterministicGameMode.Local,
        InitialTick           = frameNumber,
        FrameData             = frameData,
        RunnerId              = "LOCALDEBUG",
        PlayerCount           = MaxPlayerCount > 0 ? Math.Min(MaxPlayerCount, Input.MAX_COUNT) : Input.MAX_COUNT,
        InstantReplaySettings = InstantReplayConfig,
        InitialDynamicAssets  = dynamicDB,
        DeltaTimeType         = DeltaTimeType,
        GameFlags             = 0,
        RecordingFlags        = RecordingFlags
      };
      
      OnBeforeStart?.Invoke(arguments);

      var runner = QuantumRunner.StartGame(arguments);
    }

    private void OnGameStarted(QuantumGame game, bool isResync) {
      for (Int32 i = 0; i < LocalPlayers.Length; ++i) {
        game.AddPlayer(i, LocalPlayers[i]);
      }
    }

    /// <summary>
    /// Unity OnGUI event updates the debug runner UI.
    /// </summary>
    public void OnGUI() {
      if (DisplaySaveAndReloadButton && QuantumRunner.Default != null && QuantumRunner.Default.Id == "LOCALDEBUG") {
        if (GUI.Button(new Rect(Screen.width - 150, 10, 140, 25), "Save And Reload")) {
          StartCoroutine(SaveAndReload());
        }
      }
    }

    /// <summary>
    /// Unity update event. Will update the simulation if a custom <see cref="SimulationSpeedMultiplier" /> was set.
    /// </summary>
    public void Update() {
      if (QuantumRunner.Default != null && QuantumRunner.Default.Session != null) {
        QuantumRunner.Default.IsSessionUpdateDisabled = SimulationSpeedMultiplier != 1.0f;
        if (QuantumRunner.Default.IsSessionUpdateDisabled) {
          switch (QuantumRunner.Default.DeltaTimeType) {
            case SimulationUpdateTime.Default:
            case SimulationUpdateTime.EngineUnscaledDeltaTime:
              QuantumRunner.Default.Service(Time.unscaledDeltaTime * SimulationSpeedMultiplier);
              QuantumUnityDB.UpdateGlobal();
              break;
            case SimulationUpdateTime.EngineDeltaTime:
              QuantumRunner.Default.Service(Time.deltaTime);
              QuantumUnityDB.UpdateGlobal();
              break;
          }
        }
      }
    }

    IEnumerator SaveAndReload() {
      var frameNumber = QuantumRunner.Default.Game.Frames.Verified.Number;
      var frameData = QuantumRunner.Default.Game.Frames.Verified.Serialize(DeterministicFrameSerializeMode.Blit);

      Log.Info($"Serialized Frame size: {frameData.Length} bytes");

      QuantumRunner.ShutdownAll();

      while (QuantumRunner.ActiveRunners.Any()) {
        yield return null;
      }

      StartWithFrame(frameNumber, frameData);
    }

    /// <summary>
    /// Settings used to initialize the dynamic db.
    /// </summary>
    [Serializable]
    public struct DynamicAssetDBSettings {
      /// <summary>
      /// A unity event passing the dynamic asset db.
      /// </summary>
      [Serializable]
      public class InitialDynamicAssetsRequestedUnityEvent : UnityEvent<DynamicAssetDB> {
      }
      
      /// <summary>
      /// A callback called after the dynamic asset db was created.
      /// </summary>
      public InitialDynamicAssetsRequestedUnityEvent OnInitialDynamicAssetsRequested;

      /// <inheritdoc cref="DynamicAssetDB.IsLegacyModeEnabled"/>
      [InlineHelp]
      public bool IsLegacyModeEnabled;
    }
  }
}