namespace Quantum.Menu {
  using System;
  using System.Collections.Generic;
  using System.Linq;
  using System.Threading;
  using System.Threading.Tasks;
  using Photon.Deterministic;
  using Photon.Realtime;
  using UnityEngine;
  using UnityEngine.SceneManagement;
  using Input = Quantum.Input;

  partial class QuantumMenuConnectionBehaviour {
    /// <summary>
    /// Add the realtime client to the base connection behaviour.
    /// </summary>
    public virtual RealtimeClient Client { get; }
  }
  
  /// <summary>
  /// A wrapper for the connection object. Derive this class to add more functionality.
  /// </summary>
  public class QuantumMenuConnectionBehaviourSDK : QuantumMenuConnectionBehaviour {
    private CancellationTokenSource _cancellation;
    private CancellationTokenSource _linkedCancellation;
    private string _loadedScene;
    private QuantumMenuConnectionShutdownFlag _shutdownFlags;
    private DisconnectCause _disconnectCause;
    private IDisposable _disconnectSubscription;
    private RealtimeClient _client;

    /// <summary>
    /// Return the actual room name of the connection.
    /// </summary>
    public override string SessionName => Client?.CurrentRoom?.Name;
    /// <summary>
    /// Return the actual region connected to.
    /// </summary>
    public override string Region => Client?.CurrentRegion;
    /// <summary>
    /// Return the actual AppVersion that is used.
    /// </summary>
    public override string AppVersion => Client?.AppSettings?.AppVersion;
    /// <summary>
    /// Return the max player count for the Photon room.
    /// </summary>
    public override int MaxPlayerCount => Client?.CurrentRoom != null ? Client.CurrentRoom.MaxPlayers : 0;
    
    /// <summary>
    /// Return a list a Photon client names also connected to the room.
    /// </summary>
    public override List<string> Usernames {
      get {
        var frame = Runner?.Game?.Frames?.Verified;
        if (frame != null) {
          var result = new List<string>(frame.PlayerCount);
          for (int i = 0; i < frame.PlayerCount; i++) {
            var isPlayerConnected = (frame.GetPlayerInputFlags(i) & DeterministicInputFlags.PlayerNotPresent) == 0;
            if (isPlayerConnected) {
              var playerNickname = frame.GetPlayerData(i)?.PlayerNickname;
              if (string.IsNullOrEmpty(playerNickname)) {
                playerNickname = $"Player{i:02}";
              }
              result.Add(playerNickname);
            } else {
              result.Add(null);
            }
          }
          return result;
        }
        return null;
      }
    }
    /// <summary>
    /// Return true if connecting or connected to any server.
    /// </summary>
    public override bool IsConnected => Client == null ? false : Client.IsConnected;
    /// <summary>
    /// Return the current ping.
    /// </summary>
    public override int Ping => Runner?.Session != null ? Runner.Session.Stats.Ping : 0;

    /// <summary>
    /// The RealtimeClient object that is operated on.
    /// </summary>
    public override RealtimeClient Client => _client;

    /// <summary>
    /// The QuantumRunner object that is created and started.
    /// </summary>
    public QuantumRunner Runner { get; private set; }

    /// <inheritdoc/>
    protected override async Task<ConnectResult> ConnectAsyncInternal(QuantumMenuConnectArgs connectArgs) {
      PatchConnectArgs(connectArgs);

      if (_cancellation != null) {
        throw new Exception("Connection instance still in use");
      }

      // CONNECT ---------------------------------------------------------------

      // Cancellation is used to stop the connection process at any time.
      _cancellation = new CancellationTokenSource();
      _linkedCancellation = AsyncSetup.CreateLinkedSource(_cancellation.Token);
      _shutdownFlags = connectArgs.ShutdownFlags;
      _disconnectCause = DisconnectCause.None;

      var arguments = new MatchmakingArguments {
        PhotonSettings = new AppSettings(connectArgs.AppSettings) { 
          AppVersion = connectArgs.AppVersion,
          FixedRegion = connectArgs.PreferredRegion
        },
        ReconnectInformation = connectArgs.ReconnectInformation,
        EmptyRoomTtlInSeconds = connectArgs.ServerSettings.EmptyRoomTtlInSeconds,
        PlayerTtlInSeconds = connectArgs.ServerSettings.PlayerTtlInSeconds,
        MaxPlayers = connectArgs.MaxPlayerCount,
        RoomName = connectArgs.Session,
        CanOnlyJoin = string.IsNullOrEmpty(connectArgs.Session) == false && !connectArgs.Creating,
        PluginName = connectArgs.PhotonPluginName,
        AsyncConfig = new AsyncConfig() {
          TaskFactory = AsyncConfig.CreateUnityTaskFactory(),
          CancellationToken = _linkedCancellation.Token
        },
        NetworkClient = connectArgs.Client,
        AuthValues = connectArgs.AuthValues,
      };


      // Connect to Photon
      try {
        if (connectArgs.Reconnecting == false) {
          ReportProgress("Connecting..");
          _client = await MatchmakingExtensions.ConnectToRoomAsync(arguments);
        } else {
          ReportProgress("Reconnecting..");
          _client = await MatchmakingExtensions.ReconnectToRoomAsync(arguments);
        }
      } catch (Exception e) {
        Debug.LogException(e);
        return new ConnectResult {
          FailReason =
            AsyncConfig.Global.IsCancellationRequested ? ConnectFailReason.ApplicationQuit :
            _disconnectCause == DisconnectCause.None ? ConnectFailReason.RunnerFailed : ConnectFailReason.Disconnect,
          DisconnectCause = (int)_disconnectCause,
          DebugMessage = e.Message,
          WaitForCleanup = CleanupAsync()};
      }

      // Save region summary
      if (!string.IsNullOrEmpty(Client.SummaryToCache)) {
        connectArgs.ServerSettings.BestRegionSummary = Client.SummaryToCache;
      }

      //  Make sure to notice socket disconnects during the rest of the connection/start process
      _disconnectSubscription = Client.CallbackMessage.ListenManual<OnDisconnectedMsg>(m => {
        if (_cancellation != null && _cancellation.IsCancellationRequested == false) {
          _disconnectCause = m.cause;
          _cancellation.Cancel();
        }
      });

      // LOAD SCENE ---------------------------------------------------------------

      var preloadMap = false;
      if (connectArgs.RuntimeConfig != null 
        && connectArgs.RuntimeConfig.Map.Id.IsValid 
        && connectArgs.RuntimeConfig.SimulationConfig.Id.IsValid) {
        if (QuantumUnityDB.TryGetGlobalAsset(connectArgs.RuntimeConfig.SimulationConfig, out Quantum.SimulationConfig simulationConfigAsset)) {
          // Only preload the scene if SimulationConfig.AutoLoadSceneFromMap is not enabled.
          // Caveat: preloading the scene here only works if the runtime config is not expected to change (e.g. by other clients/random matchmaking or webhooks)
          preloadMap = simulationConfigAsset.AutoLoadSceneFromMap == SimulationConfig.AutoLoadSceneFromMapMode.Disabled;
        }
      }

      if (preloadMap) {

        ReportProgress("Loading..");
        
        if (QuantumUnityDB.TryGetGlobalAsset(connectArgs.RuntimeConfig.Map, out Quantum.Map map)) {
          return new ConnectResult {
            FailReason = ConnectFailReason.MapNotFound,
            DebugMessage = $"Requested map {connectArgs.RuntimeConfig.Map} not found.",
            WaitForCleanup = CleanupAsync()
          };
        }

        using (new ConnectionServiceScope(Client)) {
          try {
            // Load Unity scene async
            await SceneManager.LoadSceneAsync(map.Scene, LoadSceneMode.Additive);
            SceneManager.SetActiveScene(SceneManager.GetSceneByName(map.Scene));
            _loadedScene = map.Scene;

            // Check if cancellation was triggered while loading the map
            if (_linkedCancellation.Token.IsCancellationRequested) {
              throw new TaskCanceledException();
            }

          } catch (Exception e) {
            Debug.LogException(e);
            return new ConnectResult {
              FailReason =
                AsyncConfig.Global.IsCancellationRequested ? ConnectFailReason.ApplicationQuit :
                _disconnectCause == DisconnectCause.None ? ConnectFailReason.RunnerFailed : ConnectFailReason.Disconnect,
              DisconnectCause = (int)_disconnectCause,
              DebugMessage = e.Message,
              WaitForCleanup = CleanupAsync()
            };
          }

          SceneManager.SetActiveScene(SceneManager.GetSceneByName(map.Scene));
        }
      }

      // START GAME ---------------------------------------------------------------

      ReportProgress("Starting..");

      var sessionRunnerArguments = new SessionRunner.Arguments {
        RunnerFactory = QuantumRunnerUnityFactory.DefaultFactory,
        GameParameters = QuantumRunnerUnityFactory.CreateGameParameters,
        ClientId = 
          // Use client id from connection args first
          string.IsNullOrEmpty(connectArgs.QuantumClientId) == false ? connectArgs.QuantumClientId :
          // Then chose the user id that was returned by the authentication
          string.IsNullOrEmpty(Client.UserId) == false ? Client.UserId :
          // Or create a random id
          Guid.NewGuid().ToString(),
        RuntimeConfig = connectArgs.RuntimeConfig,
        SessionConfig = connectArgs.SessionConfig?.Config ?? QuantumDeterministicSessionConfigAsset.DefaultConfig,
        GameMode = DeterministicGameMode.Multiplayer,
        PlayerCount = connectArgs.MaxPlayerCount,
        Communicator = new QuantumNetworkCommunicator(Client),
        CancellationToken = _linkedCancellation.Token,
        RecordingFlags = connectArgs.RecordingFlags,
        InstantReplaySettings = connectArgs.InstantReplaySettings,
        DeltaTimeType = connectArgs.DeltaTimeType,
        StartGameTimeoutInSeconds = connectArgs.StartGameTimeoutInSeconds,
      };
      
      QuantumMppm.MainEditor?.Send(new QuantumMenuMppmJoinCommand() {
        AppVersion = connectArgs.AppVersion,
        Session = Client.CurrentRoom.Name,
        Region = Client.CurrentRegion,
      });
      
      // Register to plugin disconnect messages to display errors
      string pluginDisconnectReason = null;
      var pluginDisconnectListener = QuantumCallback.SubscribeManual<CallbackPluginDisconnect>(m => pluginDisconnectReason = m.Reason);

      try {
        // Start Quantum and wait for the start protocol to complete
        Runner = (QuantumRunner)await SessionRunner.StartAsync(sessionRunnerArguments);
      } catch (Exception e) {
        pluginDisconnectListener.Dispose();
        Debug.LogException(e);
        return new ConnectResult {
          FailReason = DetermineFailReason(_disconnectCause, pluginDisconnectReason),
          DisconnectCause = (int)_disconnectCause,
          DebugMessage = pluginDisconnectReason ?? e.Message,
          WaitForCleanup = CleanupAsync()
        };
      }

      pluginDisconnectListener.Dispose();
      _cancellation.Dispose();
      _cancellation = null;
      _linkedCancellation.Dispose();
      _linkedCancellation = null;
      _disconnectSubscription.Dispose();
      _disconnectSubscription = null;

      for (int i = 0; i < connectArgs.RuntimePlayers.Length; i++) { 
        Runner.Game.AddPlayer(i, connectArgs.RuntimePlayers[i]);
      }

      return new ConnectResult { Success = true }; 
    }

    /// <inheritdoc/>
    protected override Task DisconnectAsyncInternal(int reason) {
      if (_cancellation != null) {
        // Cancel connection logic and let the code handle cancel errors
        _cancellation.Cancel();
        return Task.CompletedTask;
      } else {
        if (reason == ConnectFailReason.UserRequest) {
          QuantumReconnectInformation.Reset();
        }

        // Stop the running game
        return CleanupAsync();
      }
    }

    /// <inheritdoc/>
    public override async Task<List<QuantumMenuOnlineRegion>> RequestAvailableOnlineRegionsAsync(QuantumMenuConnectArgs connectArgs) {
      // TODO: fix when implemented in realtime.
      try {
        var client        = connectArgs.Client ?? new RealtimeClient();
        var appSettings   = connectArgs.AppSettings ?? PhotonServerSettings.Global.AppSettings;
        var regionHandler = await client.ConnectToNameserverAndWaitForRegionsAsync(appSettings);
        return regionHandler.EnabledRegions.Select(r => new QuantumMenuOnlineRegion { Code = r.Code, Ping = r.Ping }).ToList();
      } catch (Exception e) {
        Debug.LogException(e);
        return null;
      }
    }

    private static void PatchConnectArgs(QuantumMenuConnectArgs connectArgs)
    {
      // set global configs for ServerSettings and SessionConfig when null
      connectArgs.ServerSettings = connectArgs.ServerSettings ?? PhotonServerSettings.Global;
      connectArgs.SessionConfig = connectArgs.SessionConfig ?? QuantumDeterministicSessionConfigAsset.Global;

      // limit player count
      connectArgs.MaxPlayerCount = Math.Min(connectArgs.MaxPlayerCount, Input.MaxCount);

      // runtime config alterations
      {
        connectArgs.RuntimeConfig = connectArgs.Scene.RuntimeConfig;

        // always re roll the seed if 0.
        if (connectArgs.RuntimeConfig.Seed == 0) {
          connectArgs.RuntimeConfig.Seed = Guid.NewGuid().GetHashCode();
        }

        // if SimulationConfig not set, try to get from global default configs
        if (connectArgs.RuntimeConfig.SimulationConfig.Id.IsValid == false && QuantumDefaultConfigs.TryGetGlobal(out var defaultConfigs)) {
          connectArgs.RuntimeConfig.SimulationConfig = defaultConfigs.SimulationConfig;
        }
      }

      // runtime player alterations
      {
        if (string.IsNullOrEmpty(connectArgs.Username) == false && 
            connectArgs.RuntimePlayers?.Length > 0) {
          // Always overwrite nickname, set ConnectionArgs.Username to null to avoid
          connectArgs.RuntimePlayers[0].PlayerNickname = connectArgs.Username;
        }
      }

      // auth values
      if (connectArgs.AuthValues == null ||
          (connectArgs.AuthValues.AuthType == Photon.Realtime.CustomAuthenticationType.None &&
           string.IsNullOrEmpty(connectArgs.AuthValues.UserId))) {
        // Set the UserId to the username if no authtype is set
        connectArgs.AuthValues ??= new Photon.Realtime.AuthenticationValues();
        connectArgs.AuthValues.UserId = $"{connectArgs.Username}({new System.Random().Next(99999999):00000000}";
      }
    }
    
    /// <summary>
    /// Match errors to one error number.
    /// </summary>
    /// <param name="disconnectCause">Photon disconnect reason</param>
    /// <param name="pluginDisconnectReason">Plugin disconnect message</param>
    /// <returns></returns>
    public static int DetermineFailReason(DisconnectCause disconnectCause, string pluginDisconnectReason) {
      if (AsyncConfig.Global.IsCancellationRequested) {
        return ConnectFailReason.ApplicationQuit;
      }

      switch (disconnectCause) {
        case DisconnectCause.None:
          return ConnectFailReason.RunnerFailed;
        case DisconnectCause.DisconnectByClientLogic:
          if (string.IsNullOrEmpty(pluginDisconnectReason) == false) {
            return ConnectFailReason.PluginError;
          }
          return ConnectFailReason.Disconnect;
        default:
          return ConnectFailReason.Disconnect;
      }
    }
    
    private async Task CleanupAsync() {
      _cancellation?.Dispose();
      _cancellation = null;
      _linkedCancellation?.Dispose();
      _linkedCancellation = null;
      _disconnectSubscription?.Dispose();
      _disconnectSubscription = null;

      if (Runner != null && (_shutdownFlags & QuantumMenuConnectionShutdownFlag.ShutdownRunner) >= 0) {
        try {
          if (AsyncConfig.Global.IsCancellationRequested) {
            Runner.Shutdown();
          } else {
            await Runner.ShutdownAsync();
          }
        } catch (Exception e) {
          Debug.LogException(e);
        }
      }
      Runner = null;

      if (Client != null && (_shutdownFlags & QuantumMenuConnectionShutdownFlag.Disconnect) >= 0) {
        try {
          if (AsyncConfig.Global.IsCancellationRequested) {
            Client.Disconnect();
          } else {
            await Client.DisconnectAsync();
          }
        } catch (Exception e) {
          Debug.LogException(e);
        }
      }
      _client = null;

      if (string.IsNullOrEmpty(_loadedScene) == false &&
          (_shutdownFlags & QuantumMenuConnectionShutdownFlag.ShutdownRunner) >= 0 &&
          AsyncConfig.Global.IsCancellationRequested == false) {
        try {
          await SceneManager.UnloadSceneAsync(_loadedScene);
        } catch (Exception e) {
          Debug.LogException(e);
        }
      }
      _loadedScene = null;
    }
  }
}
