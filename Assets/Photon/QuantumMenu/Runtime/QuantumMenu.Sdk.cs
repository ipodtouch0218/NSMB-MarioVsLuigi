#if !QUANTUM_DEV

#region Assets/Photon/QuantumMenu/Runtime/QuantumMenuConnectArgs.Partial.cs

namespace Quantum.Menu {
  using System;
  using Photon.Realtime;
  using UnityEngine;

  /// <summary>
  /// Quantum specific arguments to start the Quantum simulation after connecting to Photon.
  /// </summary>
  public partial class QuantumMenuConnectArgs {
    /// <summary>
    /// Getter to retrieve Photon Realtime <see cref="Photon.Realtime.AppSettings"/> from <see cref="ServerSettings"/>
    /// </summary>
    public AppSettings AppSettings => ServerSettings?.AppSettings;
    /// <summary>
    /// Photon Realtime authentication settings.
    /// </summary>
    [NonSerialized]
    public AuthenticationValues AuthValues;
    /// <summary>
    /// (optional) Photon Realtime connection object for reconnection.
    /// </summary>
    [NonSerialized]
    public RealtimeClient Client;
    /// <summary>
    /// The Photon plugin name. Default is "QuantumPlugin".
    /// </summary>
    [HideInInspector]
    public string PhotonPluginName = "QuantumPlugin";
    /// <summary>
    /// The Quantum client id. This is a secret between the client and the server and should not be shared with anyone else.
    /// It does not have to be the Photon UserId for example. It's used to reclaim the same player slot after a reconnection.
    /// If null, the <see cref="AuthenticationValues.UserId"/> is used.
    /// </summary>
    [NonSerialized]
    public string QuantumClientId;
    /// <summary>
    /// Set to true to try to perform a reconnect. <see cref="ReconnectInformation"/> must be available then.
    /// </summary>
    [NonSerialized]
    public bool Reconnecting;
    /// <summary>
    /// The reconnection information used to try to reconnect quickly to the same room.
    /// </summary>
    [NonSerialized]
    public MatchmakingReconnectInformation ReconnectInformation = new QuantumReconnectInformation();
    /// <summary>
    /// The runtime config of the Quantum simulation. Every client sends theirs to the server.
    /// This is controlled by <see cref="PhotonMenuSceneInfo.RuntimeConfig"/>.
    /// </summary>
    [NonSerialized]
    public RuntimeConfig RuntimeConfig;
    /// <summary>
    /// The RuntimePlayer which are automatically added to the simulation after is started.
    /// When empty a default player is created when connecting.
    /// </summary>
    public RuntimePlayer[] RuntimePlayers = new RuntimePlayer[] { new RuntimePlayer() };
    /// <summary>
    /// The session config used for the simulation. Every client sends theirs to the server. If null the global config will be searched.
    /// </summary>
    public QuantumDeterministicSessionConfigAsset SessionConfig;
    /// <summary>
    /// The server settings file used for the connection attempts. If null the global config will be searched.
    /// </summary>
    public PhotonServerSettings ServerSettings;
    /// <summary>
    /// Fine-tune what internals gets disposed when the connection is terminated.
    /// </summary>
    public QuantumMenuConnectionShutdownFlag ShutdownFlags = QuantumMenuConnectionShutdownFlag.All;
    /// <summary>
    /// Start Quantum game in recording mode.
    /// </summary>
    public RecordingFlags RecordingFlags = RecordingFlags.None;
    /// <summary>
    /// Instant replay settings.
    /// </summary>
    public InstantReplaySettings InstantReplaySettings = InstantReplaySettings.Default;
    /// <summary>
    /// How to update the session using <see cref="SimulationUpdateTime"/>. 
    /// Default is EngineDeltaTime.
    /// </summary>
    public SimulationUpdateTime DeltaTimeType = SimulationUpdateTime.EngineDeltaTime;
    /// <summary>
    /// A client timeout for the Quantum start game protocol, measured in seconds.
    /// Large snapshots and/or slow webhooks could make this go above the default value of 10 sec. Configure this value appropriately.
    /// </summary>
    public float StartGameTimeoutInSeconds = SessionRunner.Arguments.DefaultStartGameTimeoutInSeconds;

    /// <summary>
    /// Load members from Player Prefs
    /// </summary>
    /// <param name="keyPrefix">Player prefs key prefix</param>
    partial void LoadFromPlayerPrefsUser(string keyPrefix) {
    }

    /// <summary>
    /// Save members from Player Prefs
    /// </summary>
    /// <param name="keyPrefix">Player prefs key prefix</param>
    partial void SaveToPlayerPrefsUser(string keyPrefix) {
    }

    /// <summary>
    /// Set all values to their default.
    /// </summary>
    /// <param name="config"></param>
    partial void SetDefaultsUser(QuantumMenuConfig config) {
      // Set any map when no scene have been found in MenuConfig.AvailableScene for example. Prefer the default QuantumMap.
      if (string.IsNullOrEmpty(Scene.NameOrSceneName)) {
        var allMaps = UnityEngine.Resources.LoadAll<Map>("");
        var defaultMapIndex = Array.FindIndex(allMaps, m => m != null && m.Guid.IsValid && m.name == "QuantumMap");
        var mapIndex = defaultMapIndex >= 0 ? defaultMapIndex : Array.FindIndex(allMaps, m => m != null && m.Guid.IsValid);
        if (mapIndex >= 0) {
          Scene = new PhotonMenuSceneInfo {
            ScenePath = allMaps[mapIndex].ScenePath,
            Name = allMaps[mapIndex].name,
            Preview = null,
            RuntimeConfig = new RuntimeConfig {
              Map = allMaps[mapIndex].Guid,
            }
          };
        }
      }
    }
  }
}


#endregion


#region Assets/Photon/QuantumMenu/Runtime/QuantumMenuConnectFailReason.Partial.cs

namespace Quantum.Menu {
  /// <summary>
  /// Is used to convey some information about a connection error back to the caller.
  /// </summary>
  public partial class ConnectFailReason {
    /// <summary>
    /// The connection to Photon servers failed.
    /// </summary>
    public const int ConnectingFailed = 10;
    /// <summary>
    /// The Quantum map asset was not found.
    /// </summary>
    public const int MapNotFound = 11;
    /// <summary>
    /// The scene loading failed.
    /// </summary>
    public const int LoadingFailed = 12;
    /// <summary>
    /// Starting the runner failed.
    /// </summary>
    public const int RunnerFailed = 13;
    /// <summary>
    /// Plugin disconnected.
    /// </summary>
    public const int PluginError = 14;
  }
}

#endregion


#region Assets/Photon/QuantumMenu/Runtime/QuantumMenuConnectionShutdownFlag.cs

namespace Quantum.Menu {
  using System;

  /// <summary>
  /// Cleanup steps of <see cref="QuantumMenuConnection.DisconnectAsync(int)"/>
  /// </summary>
  [Serializable, Flags]
  public enum QuantumMenuConnectionShutdownFlag { 
    /// <summary>
    ///  Disconnect the connection
    /// </summary>
    Disconnect,
    /// <summary>
    /// Shutdown the runner
    /// </summary>
    ShutdownRunner,
    /// <summary>
    /// Unload the loaded scene
    /// </summary>
    UnloadScene,
    /// <summary>
    /// All flags
    /// </summary>
    All = Disconnect | ShutdownRunner | UnloadScene
  }
}

#endregion


#region Assets/Photon/QuantumMenu/Runtime/QuantumMenuSceneInfo.Partial.cs

namespace Quantum.Menu {
  using System.Runtime.InteropServices;

  /// <summary>
  /// Extends the shared PhotonMenuSceneInfo.
  /// </summary>
  [StructLayout(LayoutKind.Auto)]
  public partial struct PhotonMenuSceneInfo {
    /// <summary>
    /// When using a menu config the runtime config from the <see cref="QuantumMenuConnectArgs.RuntimeConfig"/> is always overwritten.
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


#region Assets/Photon/QuantumMenu/Runtime/QuantumMenuUIGameplay.Partial.cs

namespace Quantum.Menu {
  using System;
  using System.Threading.Tasks;
  using Photon.Realtime;

  /// <summary>
  /// Partial extension for the Quantum SDK menu.
  /// </summary>
  public partial class QuantumMenuUIGameplay {
    /// <summary>
    /// Shows reconnection 
    /// </summary>
    public bool IsReconnectionCheckEnabled;
    private IDisposable _photonDisconnectListener;
    private IDisposable _pluginDisconnectListener;

    /// <summary>
    /// Partial method used to extend shared Photon menu. 
    /// Creates a listener for connection disconnect during gameplay.
    /// </summary>
    partial void ShowUser() {
      var client = Connection.Client;
      _photonDisconnectListener = client.CallbackMessage.ListenManual<OnDisconnectedMsg>(OnDisconnect);
      _pluginDisconnectListener = QuantumCallback.SubscribeManual<CallbackPluginDisconnect>(OnPluginDisconnect);
    }

    /// <summary>
    /// Partial method used to extend shared Photon menu.
    /// </summary>
    partial void HideUser() {
      _photonDisconnectListener?.Dispose();
      _photonDisconnectListener = null;
      _pluginDisconnectListener?.Dispose();
      _pluginDisconnectListener = null;
    }

    /// <summary>
    /// React to plugin disconnects that are received by the protocol.
    /// </summary>
    /// <param name="callback"></param>
    private async void OnPluginDisconnect(CallbackPluginDisconnect callback) {
      await ProcessDisconnect(callback.Reason);
    }

    /// <summary>
    /// Connection signaled disconnect, stopping the menu connection object.
    /// If enabled trying to perform a reconnection
    /// </summary>
    /// <param name="msg">Photon disconnect message</param>
    private async void OnDisconnect(OnDisconnectedMsg msg) {
      if (msg.cause == DisconnectCause.DisconnectByClientLogic) {
        // Only handle disruption not caused by the user
        return;
      }

      await ProcessDisconnect(msg.cause.ToString());
    }

    /// <summary>
    /// Process the disconnect which also offers reconnection.
    /// </summary>
    /// <param name="disconnectReason">Disconnect reason shown to users.</param>
    /// <returns>When done</returns>
    private async Task ProcessDisconnect(string disconnectReason) {
      var reconnectInformation = QuantumReconnectInformation.Load();
      if (IsReconnectionCheckEnabled && reconnectInformation != null && reconnectInformation.HasTimedOut == false) {

        // If none set in the connection args, save the client object to use for reconnection
        var client = ConnectionArgs.Client == null ? Connection.Client : null;

        await Task.WhenAll(
          Connection.DisconnectAsync(ConnectFailReason.Disconnect),
          Controller.PopupAsync($"Network error '{disconnectReason}'. Trying to reconnect.", "Connection Error"));

        Controller.Show<QuantumMenuUILoading>();
        ConnectionArgs.Session = null;
        ConnectionArgs.Creating = false;
        ConnectionArgs.Client = client;
        ConnectionArgs.Reconnecting = true;
        ConnectionArgs.ReconnectInformation = reconnectInformation;

        var result = await Connection.ConnectAsync(ConnectionArgs);

        if (client != null) {
          // If it was just set for this reconnection attempts, forget the client again
          ConnectionArgs.Client = null;
        }

        await Controller.HandleConnectionResult(result, this.Controller);
      } else {
        await Task.WhenAll(
          Connection.DisconnectAsync(ConnectFailReason.Disconnect),
          Controller.PopupAsync($"Network error '{disconnectReason}'", "Connection Error"));
        Controller.Show<QuantumMenuUIMain>();
      }
    }
  }
}


#endregion


#region Assets/Photon/QuantumMenu/Runtime/QuantumMenuUIMain.Partial.cs

namespace Quantum.Menu {
  using System.Threading.Tasks;
  using UnityEngine;

  /// <summary>
  /// Partial extension for the Quantum SDK menu.
  /// </summary>
  public partial class QuantumMenuUIMain {
    /// <summary>
    /// Check if reconnect information is valid and try to reconnect when entering the linked screen.
    /// </summary>
    public bool IsReconnectionCheckEnabled;
    /// <summary>
    /// The number of the reconnection triggers when entering the screen.
    /// </summary>
    public int ConnectionAttemptsCount = 1;

    private int _connectionAttempt;

    partial void ShowUser() {
      ConnectionArgs.Reconnecting = false;

      if (_connectionAttempt++ < ConnectionAttemptsCount && IsReconnectionCheckEnabled) {
        RunReconnection();
      }
    }

    partial void HideUser() {
      // Disable all reconnection attempts, they should only trigger when starting the app
      ConnectionAttemptsCount = 0;
    }

    private async void RunReconnection() {
#if !UNITY_WEBGL
      await Task.Delay(200);
#endif
      var reconnectInformation = QuantumReconnectInformation.Load();
      Debug.Log("Checking for reconnect information");
      if (reconnectInformation != null && reconnectInformation.HasTimedOut == false) {
        Debug.Log("Found valid reconnect information, trying to reconnect");

        Controller.Show<QuantumMenuUILoading>();

        ConnectionArgs.Session = null;
        ConnectionArgs.Creating = false;
        ConnectionArgs.Reconnecting = true;
        ConnectionArgs.ReconnectInformation = reconnectInformation;
        
        var result = await Connection.ConnectAsync(ConnectionArgs);

        await Controller.HandleConnectionResult(result, this.Controller);
      }
    }
  }
}


#endregion


#region Assets/Photon/QuantumMenu/Runtime/QuantumMenuUISettings.Partial.cs

namespace Quantum.Menu {
  using System.Diagnostics;
  using System;
  using System.Reflection;

  /// <summary>
  /// Partial extension for the Quantum SDK menu settings view.
  /// </summary>
  public partial class QuantumMenuUISettings {
    partial void AwakeUser() {
      if (_sdkLabel != null) {
        _sdkLabel.text = null;
        try {
          string codeBase = Assembly.GetAssembly(typeof(Quantum.Map)).CodeBase;
          string path = Uri.UnescapeDataString(new UriBuilder(codeBase).Path);
          var fileVersionInfo = FileVersionInfo.GetVersionInfo(path);
          _sdkLabel.text = $"Quantum {fileVersionInfo.FileVersion}";
        } catch { }
      }
    }
  }
}


#endregion

#endif
