namespace Quantum {
  using Photon.Deterministic;
  using Photon.Realtime;
  using System;
  using System.Threading;
  using UnityEngine;
  using static QuantumUnityExtensions;

  /// <summary>
  /// The SDK implementation of the mini menu connection logic.
  /// Implements simple Photon connection and Quantum stating processes.
  /// Adds Quantum specific settings.
  /// </summary>
  public class QuantumStartUIConnection : QuantumStartUIConnectionBase {
    /// <summary>
    /// The Photon cloud setting to use.
    /// </summary>
    [Header("Photon Cloud Settings")]
    [InlineHelp] public PhotonServerSettings ServerSettings;
    /// <summary>
    /// The Quantum session config to use, will use <see cref="QuantumDeterministicSessionConfigAsset.DefaultConfig"/> if not set.
    /// </summary>
    [Header("Simulation Settings")]
    [InlineHelp] public QuantumDeterministicSessionConfigAsset SessionConfig;
    /// <summary>
    /// The Quantum runtime config settings to use.
    /// </summary>
    public RuntimeConfig RuntimeConfig;
    /// <summary>
    /// The Quantum runtime players to add.
    /// </summary>
    public RuntimePlayer[] Players;
    /// <summary>
    /// The update delta time type.
    /// </summary>
    [Header("View Settings")]
    public SimulationUpdateTime DeltaTimeType = SimulationUpdateTime.EngineDeltaTime;
    /// <summary>
    /// The recording flags to use for the session.
    /// </summary>
    public RecordingFlags RecordingFlags = RecordingFlags.None;
    /// <summary>
    /// The instant replay settings to this session.
    /// </summary>
    public InstantReplaySettings InstantReplayConfig = InstantReplaySettings.Default;

    /// <summary>
    /// The Realtime client connection object used to connect to the Photon cloud and enter a room.
    /// </summary>
    public RealtimeClient Client { get; private set; }
    /// <summary>
    /// The Quantum runner object used to start the game.
    /// </summary>
    public QuantumRunner Runner { get; private set; }

    CancellationTokenSource _cancellationTokenSource;
    CancellationTokenSource _linkedCancellationTokenSource;
    IDisposable _pluginDisconnectSubscription;

    /// <summary>
    /// The actual room name connected to.
    /// </summary>
    public override string RoomName => Client?.CurrentRoom?.Name;
    /// <summary>
    /// The actual region code to connected to.
    /// </summary>
    public override string Region => Client?.CurrentRegion;
    /// <summary>
    /// The current ping to the Photon server in milliseconds.
    /// </summary>
    public override int Ping => (int)(Client?.RealtimePeer?.Stats.RoundtripTime ?? 0);

    /// <summary>
    /// Connect and start the Quantum game session.
    /// This method throws exception on errors.
    /// </summary>
    /// <param name="startParameter">Mini menu start parameter</param>
    /// <returns>When the connection has been established and game started.</returns>
    public override async System.Threading.Tasks.Task ConnectAsync(StartParameter startParameter) {
      // The cancellation token is passed into the connection and runner start methods.
      // During shutdown from the outside during connecting/starting both processes are cancelled with this.
      _cancellationTokenSource = new CancellationTokenSource();

      // The linked cancellation token source also registers to the AsyncSetup.GlobalCancellationToken, which triggers when Unity Editor quits. 
      _linkedCancellationTokenSource = AsyncSetup.CreateLinkedSource(_cancellationTokenSource.Token);

      if (startParameter.IsOnline) {
        var serverSettings = ServerSettings;
        if (serverSettings == null) {
          PhotonServerSettings.TryGetGlobal(out serverSettings);
        }

        if (string.IsNullOrEmpty(serverSettings.AppSettings.AppIdQuantum)) {
          throw new InvalidOperationException("No Quantum AppId set.\nStop the game, open the Quantum Hub (Ctrl+H) and follow the instructions to create and set an AppId.");
        }

        var arguments = new MatchmakingArguments {
          PhotonSettings = new AppSettings(serverSettings.AppSettings) {
            AppVersion = AppVersion,
            FixedRegion = startParameter.Region
          },
          EmptyRoomTtlInSeconds = serverSettings.EmptyRoomTtlInSeconds,
          EnableCrc = serverSettings.EnableCrc,
          PlayerTtlInSeconds = serverSettings.PlayerTtlInSeconds,
          MaxPlayers = Input.MAX_COUNT,
          RoomName = startParameter.RoomName,
          PluginName = "QuantumPlugin",
          AuthValues = new AuthenticationValues(startParameter.PlayerName),
          AsyncConfig = new AsyncConfig() {
            TaskFactory = AsyncConfig.CreateUnityTaskFactory(),
            CancellationToken = _linkedCancellationTokenSource.Token,
          },
        };

        // Create and wait for a connection to the game server room
        Client = await MatchmakingExtensions.ConnectToRoomAsync(arguments);

        if (startParameter.OnConnectionError != null) {
          _pluginDisconnectSubscription = QuantumCallback.SubscribeManual<CallbackPluginDisconnect>(m =>
            startParameter.OnConnectionError(m.Reason)
          );
        }

#if QUANTUM_ENABLE_MPPM && UNITY_EDITOR
      if (EnableMultiplayerPlayMode) {
        QuantumMppm.MainEditor?.Send(new QuantumStartUIMppmConnectCommand { 
          Region = Region,
          RoomName = RoomName,
        });
      }
#endif
      }

      // Close the runtime config change it (map, simulation config)
      var runtimeConfig = new QuantumUnityJsonSerializer().CloneConfig(RuntimeConfig);

      var mapData = FindFirstObjectByType<QuantumMapData>();
      if (mapData != null) {
        runtimeConfig.Map = mapData.AssetRef;
      }

      if (runtimeConfig.SimulationConfig.Id.IsValid == false && QuantumDefaultConfigs.TryGetGlobal(out var defaultConfigs)) {
        runtimeConfig.SimulationConfig = defaultConfigs.SimulationConfig;
      }

      var sessionRunnerArguments = new SessionRunner.Arguments {
        RunnerFactory = QuantumRunnerUnityFactory.DefaultFactory,
        GameParameters = QuantumRunnerUnityFactory.CreateGameParameters,
        ClientId = startParameter.PlayerName,
        RuntimeConfig = runtimeConfig,
        SessionConfig = (SessionConfig != null ? SessionConfig.Config : null) ?? QuantumDeterministicSessionConfigAsset.DefaultConfig,
        PlayerCount = Input.MaxCount,
        GameMode = startParameter.IsOnline ? DeterministicGameMode.Multiplayer : DeterministicGameMode.Local,
        Communicator = startParameter.IsOnline ? new QuantumNetworkCommunicator(Client) : null,
        CancellationToken = _linkedCancellationTokenSource.Token,
        RecordingFlags = RecordingFlags,
        InstantReplaySettings = InstantReplayConfig,
        DeltaTimeType = DeltaTimeType,
      };

      // Commence and wait for the started local or online Quantum game session
      Runner = (QuantumRunner) await SessionRunner.StartAsync(sessionRunnerArguments);

      // Add players to the game.
      for (int i = 0; i < Players.Length; i++) {
        if (i == 0) {
          var runtimePlayer = JsonUtility.FromJson<RuntimePlayer>(JsonUtility.ToJson(Players[i]));
          runtimePlayer.PlayerNickname = startParameter.PlayerName;
          Runner.Game.AddPlayer(i, runtimePlayer);
        } else {
          Runner.Game.AddPlayer(i, Players[i]);
        }
      }

      _cancellationTokenSource?.Dispose();
      _cancellationTokenSource = null;

      _linkedCancellationTokenSource?.Dispose();
      _linkedCancellationTokenSource = null;
    }


    /// <summary>
    /// Disconnect and shutdown the Quantum game session.
    /// This method throws exception on errors.
    /// </summary>
    /// <returns>When the shutdown is completed</returns>
    public override async System.Threading.Tasks.Task DisconnectAsync() {
      if (_cancellationTokenSource != null) {
        _cancellationTokenSource.Cancel();
        _cancellationTokenSource = null;
      }

      _linkedCancellationTokenSource?.Dispose();
      _linkedCancellationTokenSource = null;

      _pluginDisconnectSubscription?.Dispose();
      _pluginDisconnectSubscription = null;

      if (Runner != null) {
        await Runner.ShutdownAsync();
        Runner = null;
      }

      if (Client != null) {
        await Client.DisconnectAsync();
        Client = null;
      }
    }
  }
}
