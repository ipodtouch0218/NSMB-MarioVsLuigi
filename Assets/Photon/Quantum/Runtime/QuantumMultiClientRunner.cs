namespace Quantum {
  using System;
  using System.Collections.Generic;
  using System.Linq;
  using System.Threading.Tasks;
  using Photon.Deterministic;
  using Photon.Realtime;
  using UnityEngine;
  using static QuantumUnityExtensions;

  /// <summary>
  ///   The script will can manage multiple online clients and Quantum players in your Editor. This means the remote view of
  ///   your player can be visualized in the same Unity instance.
  ///   Minimum settings:
  ///   * Requires a valid AppId and working network settings in Photon Server Settings
  ///   * Drag the QuantumMultiClientRunner prefab into you Quantum game scene (this works similar to the default Runner except it does not reload the Unity scene)
  ///   * Add game objects that belong to the regular Quantum scene to DisableOnStart (QuantumDefaultRunner, QuantumEntityViewUpdater, Your Input Script, CustomCallbacks)
  ///   * The PlayerInputTemplate is instantiated for each client to gather input by firing the Unity message PollInput(CallbackPollInput c). Implement input in the format below.
  ///   * Press "New Client" to add additional online players
  ///   I = toggle input of the player
  ///   V = toggle view of the player
  ///   G = toggle gizmos of the player
  ///   X = quit player
  ///   0-9 = Add a local player slot
  ///   SHIFT+0-9 = remove local player slot
  ///   * If you don't experience ghosting try a different cloud that if farther away from you (Fixed Region 'sa' for example)
  ///   * Enable AddAsLocalPlayers to add new players as local players instead of each having a separate connection.
  /// </summary>
  /// <example><code>
  /// public class QuantumMultiClientTestInput : QuantumMonoBehaviour {
  ///   private void PollInput(CallbackPollInput c) {
  ///     var i = new Quantum.Input();
  ///     i.Direction.X = 1;
  ///     i.Direction.Y = 0;
  ///     c.SetInput(i, DeterministicInputFlags.Repeatable);
  ///   }
  /// }
  /// </code></example>
  public class QuantumMultiClientRunner : QuantumMonoBehaviour {
    /// <summary>
    /// Get instantiated for each client and makes connection controls for that client available.
    /// </summary>
    public QuantumMultiClientPlayerView PlayerViewTemplate;
    /// <summary>
    /// The button to create a new client connection.
    /// </summary>
    public UnityEngine.UI.Button CreatePlayerBtn;

    /// <summary>
    /// Quantum scripts in your game scene that are part of the regular setup like QuantumEntityViewUpdater,
    /// Input and CustomCallbacks need to be disabled when using the MultiClientRunner, add them here.
    /// </summary>
    [InlineHelp]
    public List<GameObject> DisableOnStart = new List<GameObject>();

    /// <summary>
    /// Optionally provide non-default editor settings for all additional clients after the first one (to change the gizmo colors for example).
    /// </summary>
    public QuantumGameGizmosSettingsScriptableObject GizmosSettings;

    /// <summary>
    /// Optionally provide different non-default server app settings.
    /// </summary>
    public PhotonServerSettings ServerSettings;

    /// <summary>
    /// Add a session config here.
    /// </summary>
    public QuantumDeterministicSessionConfigAsset SessionConfig;

    /// <summary>
    /// Add custom runtime config settings here
    /// </summary>
    public RuntimeConfig RuntimeConfig;

    /// <summary>
    /// Set the max player count
    /// </summary>
    public int PlayerCount = 4;

    /// <summary>
    /// How many clients to start with when starting the app.
    /// </summary>
    public int InitialClientCount = 0;

    /// <summary>
    /// How many additional players per client to start with when starting the app.
    /// </summary>
    public int InitialPlayerCount = 0;

    /// <summary>
    /// Start initial clients and players with an extra delay.
    /// </summary>
    public float InitialPlayerDelayInSec = 0.0f;

    /// <summary>
    /// Add custom runtime player settings here.
    /// </summary>
    public RuntimePlayer[] RuntimePlayer;

    /// <summary>
    /// Provide a player input template that is instantiated for the clients. 
    /// A Unity script that has to implement void Unity message PollInput(CallbackPollInput c).
    /// </summary>
    public GameObject PlayerInputTemplate;

    /// <summary>
    /// Optionally provide a custom QuantumEntityViewUpdater game object template that is instantiated for the clients 
    /// (otherwise a new instance of QuantumEntityViewUpdater is created for each player).
    /// </summary>
    public QuantumEntityViewUpdater EntityViewUpdaterTemplate;

    /// <summary>
    /// Use random matchmaking or let subsequent players join the primary players room.
    /// </summary>
    public bool UseRandomMatchmaking = false;

    /// <summary>
    /// Use a private AppVersion when connecting to isolate matchmaking players
    /// </summary>
    public bool UsePrivateAppVersion = true;

    List<QuantumMultiClientPlayer> _players = new List<QuantumMultiClientPlayer>();
    string _currentRoomName;
    string _privateAppVersion;
    string _appGuid;

    private static int _clientIdCounter = 0;

    private int NewClientId {
      get {
        if (IsFirstPlayer) {
          _clientIdCounter = 0;
        }

        return _clientIdCounter++;
      }
    }

    private bool IsFirstPlayer => _players.Count == 0;

    /// <summary>
    /// Unity Start method. 
    /// Toggles game objects and create initial clients.
    /// </summary>
    public async void Start() {
      _appGuid = Guid.NewGuid().ToString();

      PlayerViewTemplate.gameObject.SetActive(false);

      foreach (var go in DisableOnStart) {
        go.SetActive(false);
      }

      for (int i = 0; i < InitialClientCount; ++i) {
        await CreateNewConnectedPlayer();

        // This is an extra case of adding new players to an existing connection and QuantumRunner.
        for (int j = 0; j < InitialPlayerCount; ++j) {
          var mainPlayer = _players.Last(p => p.MainPlayer == null);
          CreateNewLocalPlayer(mainPlayer);
#if !UNITY_WEBGL
          if ((int)(InitialPlayerDelayInSec * 1000.0f) > 0) {
            await System.Threading.Tasks.Task.Delay((int)(InitialPlayerDelayInSec * 1000.0f));
          }
#endif

          if (mainPlayer.Runner.IsRunning == false) {
            break;
          }
        }
      }
    }

    /// <summary>
    /// Unity OnEnabled method, subscribes to relevant Quantum callbacks.
    /// </summary>
    public void OnEnable() {
      CreatePlayerBtn.onClick.AddListener(CreateNewClient);
      QuantumCallback.Subscribe(this, (CallbackPollInput c) => OnCallbackPollInput(c));
      QuantumCallback.Subscribe(this, (CallbackLocalPlayerAddConfirmed c) => OnLocalPlayerAddConfirmed(c));
      QuantumCallback.Subscribe(this, (CallbackLocalPlayerRemoveConfirmed c) => OnLocalPlayerRemoveConfirmed(c));
      QuantumCallback.Subscribe(this, (CallbackLocalPlayerAddFailed c) => OnLocalPlayerAddFailed(c));
      QuantumCallback.Subscribe(this, (CallbackLocalPlayerRemoveFailed c) => OnLocalPlayerRemoveFailed(c));
    }

    private void OnCallbackPollInput(CallbackPollInput c) {
      var players = _players.FindAll(p => p.RunnerId == ((QuantumRunner)c.Game.Session.Runner).Id);
      foreach (var player in players) {
        if (c.PlayerSlot == player.PlayerSlot) {
          if (player.Input != null && player.Input.activeInHierarchy) {
            // Query input by using a Unity message
            // Alternative could be an interface here that would also solve the runner id, player slot checks more elegantly
            player.Input.SendMessage("PollInput", c, SendMessageOptions.DontRequireReceiver);
          } else {
            // Send Repeatable default input
            c.SetInput(new Quantum.Input(), DeterministicInputFlags.Repeatable);
          }
        }
      }
    }

    private void OnLocalPlayerAddConfirmed(CallbackLocalPlayerAddConfirmed c) {
      Debug.Log($"Added Player: {c.Player} slot {c.PlayerSlot} frame {c.Frame.Number}");
    }

    private void OnLocalPlayerRemoveConfirmed(CallbackLocalPlayerRemoveConfirmed c) {
      Debug.Log($"Removed Player: {c.Player} slot {c.PlayerSlot} frame {c.Frame.Number}");
    }

    private void OnLocalPlayerAddFailed(CallbackLocalPlayerAddFailed c) {
      Debug.LogError($"Failed Adding Player: slot {c.PlayerSlot} '{c.Message}'");
      var player = _players.Find(p => p.RunnerId == ((QuantumRunner)c.Game.Session.Runner).Id && p.PlayerSlot == c.PlayerSlot);
      if (player != null) {
        player.Destroy();
        _players.Remove(player);
        CreatePlayerBtn.interactable = _players.Count < PlayerCount;
      }
    }

    private void OnLocalPlayerRemoveFailed(CallbackLocalPlayerRemoveFailed c) {
      Debug.LogError($"Failed Removing Player: slot {c.PlayerSlot} '{c.Message}'");
    }

    /// <summary>
    /// Unity OnDisabled method.
    /// Removes subscriptions from GUI buttons.
    /// Quantum subscriptions are automatically removed.
    /// </summary>
    public void OnDisable() {
      CreatePlayerBtn.onClick.RemoveListener(CreateNewClient);
    }

    /// <summary>
    /// Create a new client.
    /// </summary>
    public async void CreateNewClient() {
      await CreateNewConnectedPlayer();
    }
    
    /// <summary>
    /// Create and connect a new client.
    /// </summary>
    private async Task<QuantumMultiClientPlayer> CreateNewConnectedPlayer() {
      if (UseRandomMatchmaking == false && IsFirstPlayer) {
        _currentRoomName = Guid.NewGuid().ToString();
      }

      if (UsePrivateAppVersion && IsFirstPlayer) {
        _privateAppVersion = Guid.NewGuid().ToString();
      }

      // Create player UI
      var playerName = $"Client {NewClientId:00}";
      var viewGo = Instantiate(PlayerViewTemplate.gameObject, transform.GetChild(0));
      viewGo.name = playerName;
      viewGo.SetActive(true);
      var playerUi = viewGo.GetComponent<QuantumMultiClientPlayerView>();
      playerUi.Label.text = playerName;
      playerUi.SetLoading();

      CreatePlayerBtn.interactable = false;
      CreatePlayerBtn.transform.parent.SetAsLastSibling();

      var appSettingsCopy = new AppSettings(ServerSettings.AppSettings);
      appSettingsCopy.AppVersion = _privateAppVersion;

      var connectionArguments = new MatchmakingArguments {
        PhotonSettings = appSettingsCopy,
        PluginName = "QuantumPlugin",
        MaxPlayers = PlayerCount,
        UserId = $"{_appGuid}_{playerName}",
        RoomName = _currentRoomName,
        CanOnlyJoin = IsFirstPlayer == false
      };

      // Connect to photon cloud
      var client = default(RealtimeClient);
      try {
        client = await MatchmakingExtensions.ConnectToRoomAsync(connectionArguments);
        Debug.Log($"Connected user {client.UserId} to room {client.CurrentRoom?.Name}");
      } catch (Exception e) {
        Debug.LogException(e);
        Destroy(viewGo);
        CreatePlayerBtn.interactable = true;
        return null;
      }

      var runtimeConfig = RuntimeConfig ?? new RuntimeConfig();
      var mapGuid = FindFirstObjectByType<QuantumMapData>().Asset.Guid;
      if (mapGuid.IsValid) {
        runtimeConfig.Map.Id = mapGuid;
      }

      var startArguments = new SessionRunner.Arguments {
        RunnerFactory = QuantumRunnerUnityFactory.DefaultFactory,
        GameParameters = QuantumRunnerUnityFactory.CreateGameParameters,
        RuntimeConfig = runtimeConfig,
        SessionConfig = SessionConfig.Config,
        ReplayProvider = null,
        GameMode = DeterministicGameMode.Multiplayer,
        InitialTick = 0,
        PlayerCount = client.CurrentRoom.MaxPlayers,
        Communicator = new QuantumNetworkCommunicator(client),
        RunnerId = playerName,
        ClientId = $"{_appGuid}_{playerName}",
        OnShutdown = OnRunnerShutdown
      };

      // Start the game
      var runner = default(QuantumRunner);
      try {
        runner = (QuantumRunner)await SessionRunner.StartAsync(startArguments);

      } catch (Exception e) {
        Debug.LogException(e);
        Destroy(viewGo);
        CreatePlayerBtn.interactable = true;
        await client.DisconnectAsync();
        return null;
      }

      // Create runtime player
      var runtimePlayer = default(RuntimePlayer);
      if (RuntimePlayer != null && RuntimePlayer.Length > _players.Count) {
        runtimePlayer = new QuantumUnityJsonSerializer().ClonePlayer(RuntimePlayer[_players.Count]);
      } else {
        runtimePlayer = new RuntimePlayer();
      }
      
      if (string.IsNullOrEmpty(runtimePlayer.PlayerNickname)) {
        runtimePlayer.PlayerNickname = playerName;
      }

      runner.Game.AddPlayer(0, runtimePlayer);
      runner.HideGizmos = IsFirstPlayer;
      runner.GizmoSettings = GizmosSettings?.Settings ?? runner.GizmoSettings;

      // Create multi client player state and logic
      var playerGO = new GameObject(playerName);
      playerGO.transform.parent = gameObject.transform;
      var player = playerGO.AddComponent<QuantumMultiClientPlayer>();
      player.Runner = runner;
      player.DestroyPlayerCallback = DestroyPlayer;
      player.CreateInput(PlayerInputTemplate);
      player.CreateEntityViewUpdater(EntityViewUpdaterTemplate, runner.Game);
      player.BindView(playerUi, IsFirstPlayer, true);

      // Link the add player button here
      playerUi.AddPlayer.onClick.AddListener(() => CreateNewLocalPlayer(player));

      _players.Add(player);

      CreatePlayerBtn.interactable = _players.Count < PlayerCount;

      // This is used to catch disconnect messages on the connection and is only added to the main player
      player.ShutdownHandler = runner.NetworkClient.CallbackMessage.ListenManual<OnDisconnectedMsg>(m => {
        if (m.cause != DisconnectCause.DisconnectByClientLogic) {
          Debug.Log($"Disconnect detected: {m.cause}");
          runner.Shutdown(ShutdownCause.NetworkError);
        }
      });

      return player;
    }

    /// <summary>
    /// Create a new player for a connected client.
    /// </summary>
    /// <param name="mainPlayer">The connection that the player should be added to</param>
    private QuantumMultiClientPlayer CreateNewLocalPlayer(QuantumMultiClientPlayer mainPlayer) {
      var playerSlot = mainPlayer.CreateFreeClientPlayerSlot(PlayerCount);
      if (playerSlot == -1) {
        throw new Exception("Not enough slots left to create a local player");
      }

      // Create player UI
      var playerName = $"Player {NewClientId:00}*";
      var viewGo = Instantiate(PlayerViewTemplate.gameObject, mainPlayer.View.transform.parent);
      viewGo.name = playerName;
      viewGo.SetActive(true);
      viewGo.transform.SetSiblingIndex(mainPlayer.GetHighestSiblingIndex() + 1);
      var playerUi = viewGo.GetComponent<QuantumMultiClientPlayerView>();
      playerUi.Label.text = playerName;
      playerUi.SetLoading();

      CreatePlayerBtn.interactable = false;
      CreatePlayerBtn.transform.parent.SetAsLastSibling();

      // Create runtime player
      var runtimePlayer = (RuntimePlayer != null && RuntimePlayer.Length > _players.Count) ? RuntimePlayer[_players.Count] : null;
      if (runtimePlayer == null) {
        runtimePlayer = new RuntimePlayer { PlayerNickname = playerName };
      }

      mainPlayer.Runner.Game.AddPlayer(playerSlot, runtimePlayer);

      // Create multi client player state and logic
      var playerGO = new GameObject(playerName);
      playerGO.transform.parent = gameObject.transform;
      var player = playerGO.AddComponent<QuantumMultiClientPlayer>();
      player.Runner = mainPlayer.Runner;
      player.MainPlayer = mainPlayer;
      player.PlayerSlot = playerSlot;
      player.DestroyPlayerCallback = DestroyPlayer;
      player.CreateInput(PlayerInputTemplate);
      player.CreateEntityViewUpdater(EntityViewUpdaterTemplate, mainPlayer.Runner.Game);
      player.BindView(playerUi, IsFirstPlayer, false);

      _players.Add(player);

      CreatePlayerBtn.interactable = _players.Count < PlayerCount;

      // Make a connection between the local player and the main player
      mainPlayer.LocalPlayers.Add(player);

      return player;
    }

    /// <summary>
    /// MultiClientPlayer class uses this to signal a player quits.
    /// </summary>
    /// <param name="player">Player class</param>
    private void DestroyPlayer(QuantumMultiClientPlayer player) {
      if (player.MainPlayer == null) {
        // This will trigger OnRunnerShutdown() eventually, removing all local players as well
        player.Runner?.Shutdown();
      } else {
        // Only destroy this local player
        player.Runner?.Game.RemovePlayer(player.PlayerSlot);
        player.Destroy();
        _players.Remove(player);
        CreatePlayerBtn.interactable = _players.Count < PlayerCount;
      }
    }

    /// <summary>
    /// Is called when the QuantumRunner terminates, on request or any connection error.
    /// </summary>
    /// <param name="cause">The shutdown cause</param>
    /// <param name="runner">The associated QuantumRunner</param>
    private void OnRunnerShutdown(ShutdownCause cause, SessionRunner runner) {
      var players = _players.FindAll(p => p.RunnerId == runner.Id);
      foreach (var player in players) {
        player.Destroy();
        _players.Remove(player);
        CreatePlayerBtn.interactable = _players.Count < PlayerCount;
      }
    }
  }
}