namespace Quantum {
  using System;
  using System.Linq;
  using UnityEngine;
#if (QUANTUM_ADDRESSABLES || QUANTUM_ENABLE_ADDRESSABLES) && !QUANTUM_DISABLE_ADDRESSABLES
  using UnityEngine.AddressableAssets;
#endif
  using UnityEngine.Events;
  using UnityEngine.SceneManagement;
  using UnityEngine.Serialization;
  using static QuantumUnityExtensions;

  /// <summary>
  /// A Unity script that starts a local Quantum simulation from a gameplay scene.
  /// Requires a <see cref="QuantumMapData"/> in the hierarchy.
  /// Player are automatically added to the game which can be listed in <see cref="LocalPlayers"/>.
  /// The script disables itself when other scene were loaded before (which indicates a menu scene).
  /// Optionally the Quantum game can be started from a <see cref="SnapshotFile"/>.
  /// </summary>
  public class QuantumRunnerLocalDebug : QuantumMonoBehaviour {
    /// <summary>
    /// Set the <see cref="DeltaTimeType" /> to <see cref="SimulationUpdateTime.EngineDeltaTime" /> to not progress the
    /// simulation during break points.
    /// Has to be set before starting the runner and can only be changed on the runner directly during runtime: <see cref="SessionRunner.DeltaTimeType"/>.
    /// </summary>
    [InlineHelp]
    public SimulationUpdateTime DeltaTimeType = SimulationUpdateTime.EngineDeltaTime;
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
    /// If set to true, the <see cref="RuntimeConfig.Seed"/> seed will be set to a random value.
    /// </summary>
    [InlineHelp]
    public bool UseRandomSeed = false;
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
    /// Optionally start the game from a snapshot loaded from a file using <see cref="QuantumReplayFile"/>.
    /// </summary>
    [InlineHelp]
    public TextAsset SnapshotFile;
    /// <summary>
    /// Optionally starting the game using an asset db from a file.
    /// </summary>
    [InlineHelp]
    public TextAsset DatabaseFile;

    QuantumRunner _runner;
    
    /// <summary>
    /// Unity start event, will start the Quantum simulation.
    /// </summary>
    public async void Start()
    {
      if (QuantumRunner.Default != null || SceneManager.sceneCount > 1) {
        // Prevents to start the simulation (again/twice) when..
        // a) there already is a runner, because the scene is reloaded during Quantum Unity map loading (AutoLoadSceneFromMap) or
        // b) this scene is not the first scene that is ever loaded (most likely a menu scene is involved that starts the simulation itself)
        enabled = false;
        return;
      }

      var gameMenu = FindFirstObjectByType<QuantumStartUI>();
      if (gameMenu != null && gameMenu.gameObject.activeSelf) {
        // If a game menu / start GUI is present, we assume that the game is started from the menu code.
        // In this case, we don't want to start the simulation here, but rather let the menu scene handle it.
        enabled = false;
        return;
      }

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

      var arguments = default(SessionRunner.Arguments);
      var serializer = new QuantumUnityJsonSerializer();
      var assets = default(byte[]);

      if (SnapshotFile == null) {
        Log.Debug("### Starting Quantum in local debug mode ###");

        var mapData = FindFirstObjectByType<QuantumMapData>();
        Assert.Always(mapData != null, "No MapData object found, a local Quantum simulation cannot be started in this scene");

        // copy runtime config
        var runtimeConfig = serializer.CloneConfig(RuntimeConfig);

        // always randomize the Quantum simulation seed when UseRandomSeed is enabled and the simulation is started from frame 0
        if (UseRandomSeed) {
          runtimeConfig.Seed = UnityEngine.Random.Range(int.MinValue, int.MaxValue);
        }

        // set map to this maps asset
        runtimeConfig.Map = mapData.AssetRef;

        using var dynamicDB = new DynamicAssetDB(DynamicAssetDB.IsLegacyModeEnabled);
        DynamicAssetDB.OnInitialDynamicAssetsRequested?.Invoke(dynamicDB);

        // create start game parameter
        arguments = new SessionRunner.Arguments();
        arguments.InitForLocal(runtimeConfig);
        arguments.SessionConfig = SessionConfig != null ? SessionConfig.Config : arguments.SessionConfig;
        arguments.PlayerCount = MaxPlayerCount > 0 ? Math.Clamp(MaxPlayerCount, 1, Input.MAX_COUNT) : arguments.PlayerCount;
        arguments.InitialDynamicAssets = dynamicDB;
        arguments.InstantReplaySettings = InstantReplayConfig;
        arguments.DeltaTimeType = DeltaTimeType;
        arguments.RecordingFlags = RecordingFlags;
      } else {
        Log.Debug("### Starting Quantum in local debug mode from a snapshot ###");

        var snapshotFile = JsonUtility.FromJson<QuantumReplayFile>(SnapshotFile.text);

        arguments = new SessionRunner.Arguments();
        arguments.InitForSnapshot(snapshotFile, serializer, assets: DatabaseFile != null ? DatabaseFile.bytes : null);
        arguments.InstantReplaySettings = InstantReplayConfig;
        arguments.DeltaTimeType = DeltaTimeType;

        assets = snapshotFile.AssetDatabaseData?.Decode();
      }

      _runner = await SessionRunner.StartAsync(arguments) as QuantumRunner;

      if (LocalPlayers != null) {
        for (Int32 i = 0; i < LocalPlayers.Length; ++i) {
          _runner.Game.AddPlayer(i, LocalPlayers[i]);
        }
      }
    }

    /// <summary>
    /// Unity update event. Will update the simulation if a custom <see cref="SimulationSpeedMultiplier" /> was set.
    /// </summary>
    public void Update() {
      if (_runner?.Session != null) {
        _runner.IsSessionUpdateDisabled = SimulationSpeedMultiplier != 1.0f;
        if (_runner.IsSessionUpdateDisabled) {
          switch (_runner.DeltaTimeType) {
            case SimulationUpdateTime.Default:
            case SimulationUpdateTime.EngineUnscaledDeltaTime:
              _runner.Service(Time.unscaledDeltaTime * SimulationSpeedMultiplier);
              break;
            case SimulationUpdateTime.EngineDeltaTime:
              _runner.Service(Time.deltaTime * SimulationSpeedMultiplier);
              break;
          }
        }
      }
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