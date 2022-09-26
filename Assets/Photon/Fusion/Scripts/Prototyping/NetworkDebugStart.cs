using System;
using Fusion;
using Fusion.Sockets;
using System.Collections;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using System.Linq;

#if UNITY_EDITOR
using UnityEditor;
using Fusion.Editor;
#endif

/// <summary>
/// A Fusion prototyping class for starting up basic networking. Add this component to your startup scene, and supply a <see cref="RunnerPrefab"/>.
/// Can be set to automatically startup the network, display an in-game menu, or allow simplified start calls like <see cref="StartHost()"/>.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Fusion/Prototyping/Network Debug Start")]
[ScriptHelp(BackColor = EditorHeaderBackColor.Steel)]
public class NetworkDebugStart : Fusion.Behaviour {

  /// <summary>
  /// Selection for how <see cref="NetworkDebugStart"/> will behave at startup.
  /// </summary>
  public enum StartModes {
    UserInterface,
    Automatic,
    Manual
  }

  /// <summary>
  /// The current stage of connection or shutdown.
  /// </summary>
  public enum Stage {
    Disconnected,
    StartingUp,
    UnloadOriginalScene,
    ConnectingServer,
    ConnectingClients,
    AllConnected,
  }

  /// <summary>
  /// Supply a Prefab or a scene object which has the <see cref="NetworkRunner"/> component on it, 
  /// as well as any runner dependent components which implement <see cref="INetworkRunnerCallbacks"/>, 
  /// such as <see cref="NetworkEvents"/> or your own custom INetworkInput implementations.
  /// </summary>
  [InlineHelp]
  [WarnIf(nameof(RunnerPrefab), false, "No " + nameof(RunnerPrefab) + " supplied. Will search for a " + nameof(NetworkRunner) + " in the scene at startup.")]
  [MultiPropertyDrawersFix]
  public NetworkRunner RunnerPrefab;

  /// <summary>
  /// Select how network startup will be triggered. Automatically, by in-game menu selection, or exclusively by script.
  /// </summary>
  [InlineHelp]
  [MultiPropertyDrawersFix]
  [WarnIf(nameof(StartMode), (double)StartModes.Manual, "Start network by calling the methods " + nameof(StartHost) + "(), " + nameof(StartServer) + "(), " + nameof(StartClient) + "(), " + nameof(StartHostPlusClients) + "(), or " + nameof(StartServerPlusClients) + "()", MsgType = 1)]
  public StartModes StartMode = StartModes.UserInterface;

  /// <summary>
  /// When <see cref="StartMode"/> is set to <see cref="StartModes.Automatic"/>, this option selects if the <see cref="NetworkRunner"/> 
  /// will be started as a dedicated server, or as a host (which is a server with a local player).
  /// </summary>
  [InlineHelp]
  [UnityEngine.Serialization.FormerlySerializedAs("Server")]
  [DrawIf(nameof(StartMode), (long)StartModes.Automatic, Hide = true)]
  public GameMode AutoStartAs = GameMode.Shared;

  /// <summary>
  /// <see cref="NetworkDebugStartGUI"/> will not render GUI elements while <see cref="CurrentStage"/> == <see cref="Stage.AllConnected"/>.
  /// </summary>
  [InlineHelp]
  [DrawIf(nameof(StartMode), (long)StartModes.UserInterface, Hide = true)]
  public bool AutoHideGUI = true;

  /// <summary>
  /// The number of client <see cref="NetworkRunner"/> instances that will be created if running in Mulit-Peer Mode. 
  /// When using the Select start mode, this number will be the default value for the additional clients option box.
  /// </summary>
  [InlineHelp]
  [DrawIf(nameof(ShowAutoClients), Hide = true)]
  public int AutoClients = 1;


  /// <summary>
  /// The port that server/host <see cref="NetworkRunner"/> will use.
  /// </summary>
  [InlineHelp]
  public ushort ServerPort = 27015;

  /// <summary>
  /// The default room name to use when connecting to photon cloud.
  /// </summary>
  [InlineHelp]
  public string DefaultRoomName = ""; // empty/null means Random Room Name

  /// <summary>
  /// Will automatically enable <see cref="FusionStats"/> once peers have finished connecting.
  /// </summary>
  [InlineHelp]
  public bool AlwaysShowStats = false;

  [NonSerialized]
  NetworkRunner _server;

  /// <summary>
  /// The Scene that will be loaded after network shutdown completes (all peers have disconnected). 
  /// If this field is null or invalid, will be set to the current scene when <see cref="NetworkDebugStart"/> runs Awake().
  /// </summary>
  [InlineHelp]
  [ScenePath]
  [MultiPropertyDrawersFix]
  public string InitialScenePath;
  static string _initialScenePath;

  /// <summary>
  /// Indicates which step of the startup process <see cref="NetworkDebugStart"/> is currently in.
  /// </summary>
  [InlineHelp]
  [SerializeField]
  [EditorDisabled]
  [MultiPropertyDrawersFix]
  protected Stage _currentStage;

  /// <summary>
  /// Indicates which step of the startup process <see cref="NetworkDebugStart"/> is currently in.
  /// </summary>
  public Stage CurrentStage {
    get => _currentStage;
    internal set {
      _currentStage = value;
#if UNITY_EDITOR
      // Hack to force an inspector refresh when this value changes, as it affects which buttons are shown.
      EditorUtility.SetDirty(this);
#endif
    }
  }
  
  /// <summary>
  /// The index number used for the last created peer.
  /// </summary>
  public int LastCreatedClientIndex { get; internal set; }

  /// <summary>
  /// The server mode that was used for initial startup. Used to inform UI which client modes should be available.
  /// </summary>
  public GameMode CurrentServerMode { get; internal set; }

  protected bool CanAddClients          => CurrentStage == Stage.AllConnected && CurrentServerMode > 0 && CurrentServerMode != GameMode.Shared && CurrentServerMode != GameMode.Single;
  protected bool CanAddSharedClients    => CurrentStage == Stage.AllConnected && CurrentServerMode > 0 && CurrentServerMode == GameMode.Shared;
  protected bool IsShutdown             => CurrentStage == Stage.Disconnected;
  protected bool IsShutdownAndMultiPeer => CurrentStage == Stage.Disconnected && UsingMultiPeerMode;

  protected bool UsingMultiPeerMode => NetworkProjectConfig.Global.PeerMode == NetworkProjectConfig.PeerModes.Multiple;
  protected bool ShowAutoClients    => StartMode != StartModes.Manual && UsingMultiPeerMode && AutoStartAs != GameMode.Single;


#if UNITY_EDITOR
  protected virtual void Reset() {
    if (TryGetComponent<NetworkDebugStartGUI>(out var ndsg) == false) {
      ndsg = gameObject.AddComponent<NetworkDebugStartGUI>();
    }
  }

#endif


  protected virtual void Start() {

    if (_initialScenePath == null) {
      if (String.IsNullOrEmpty(InitialScenePath)) {
        var currentScene = SceneManager.GetActiveScene();
        if (currentScene.IsValid()) {
          _initialScenePath = currentScene.path;
        } else {
          // Last fallback is the first entry in the build settings
          _initialScenePath = SceneManager.GetSceneByBuildIndex(0).path;
        }
        InitialScenePath = _initialScenePath;
      } else {
        _initialScenePath = InitialScenePath;
      }
    }

    var config = NetworkProjectConfig.Global;
    var isMultiPeer = config.PeerMode == NetworkProjectConfig.PeerModes.Multiple;

    var existingrunner = FindObjectOfType<NetworkRunner>();

    if (existingrunner && existingrunner != RunnerPrefab) {
      if (existingrunner.State != NetworkRunner.States.Shutdown) {
        // disable
        enabled = false;

        // destroy this and GUI (if exists), and return
        var gui = GetComponent<NetworkDebugStartGUI>();
        if (gui) {
          Destroy(gui);
        }

        Destroy(this);
        return;
      } else {
        // If no RunnerPrefab is supplied, use the scene runner.
        if (RunnerPrefab == null) {
          RunnerPrefab = existingrunner;
        }
      }
    }

    if (StartMode == StartModes.Manual)
      return;

    //// Force this to select if auto not allowed.
    //if (StartMode == StartModes.Automatic && config.PeerMode != NetworkProjectConfig.PeerModes.Multiple && Server != ServerModes.Shared) {
    //  StartMode = StartModes.UserInterface;
    //}

    if (StartMode == StartModes.Automatic) {
      if (TryGetSceneRef(out var sceneRef)) {
        StartCoroutine(StartWithClients(AutoStartAs, sceneRef, isMultiPeer ? AutoClients : (AutoStartAs == GameMode.Client || AutoStartAs == GameMode.AutoHostOrClient ? 1 : 0)));
      }
    } else {
      if (TryGetComponent<NetworkDebugStartGUI>(out var _) == false) {
        gameObject.AddComponent<NetworkDebugStartGUI>();
      }
    }
  }

  protected bool TryGetSceneRef(out SceneRef sceneRef) {
    var activeScene = SceneManager.GetActiveScene();
    if (activeScene.buildIndex < 0 || activeScene.buildIndex >= SceneManager.sceneCountInBuildSettings) {
      sceneRef = default;
      return false;
    } else {
      sceneRef = activeScene.buildIndex;
      return true;
    }
  }

  /// <summary>
  /// Start a single player instance.
  /// </summary>
  [BehaviourButtonAction(nameof(StartSinglePlayer), true, false, conditionMember: nameof(IsShutdown))]
  public virtual void StartSinglePlayer() {
    if (TryGetSceneRef(out var sceneRef)) {
      StartCoroutine(StartWithClients(GameMode.Single, sceneRef, 0));
    }
  }


  /// <summary>
  /// Start a server instance.
  /// </summary>
  [BehaviourButtonAction(nameof(StartServer), true, false, conditionMember: nameof(IsShutdown))]
  public virtual void StartServer() {
    if (TryGetSceneRef(out var sceneRef)) {
      StartCoroutine(StartWithClients(GameMode.Server, sceneRef, 0));
    }
  }

  /// <summary>
  /// Start a host instance. This is a server instance, with a local player.
  /// </summary>
  [BehaviourButtonAction(nameof(StartHost), true, false, conditionMember: nameof(IsShutdown))]
  public virtual void StartHost() {
    if (TryGetSceneRef(out var sceneRef)) {
      StartCoroutine(StartWithClients(GameMode.Host, sceneRef, 0));
    }
  }

  /// <summary>
  /// Start a client instance.
  /// </summary>
  [BehaviourButtonAction("Start Client", true, false, conditionMember: nameof(IsShutdown))]
  public virtual void StartClient() {
    StartCoroutine(StartWithClients(GameMode.Client, default, 1));
  }

  [BehaviourButtonAction("Start Shared Client", true, false, conditionMember: nameof(IsShutdown))]
  public virtual void StartSharedClient() {
    if (TryGetSceneRef(out var sceneRef)) {
      StartCoroutine(StartWithClients(GameMode.Shared, sceneRef, 1));
    }
  }

  [BehaviourButtonAction("Start Auto Host Or Client", true, false, conditionMember: nameof(IsShutdown))]
  public virtual void StartAutoClient() {
    if (TryGetSceneRef(out var sceneRef)) {
      StartCoroutine(StartWithClients(GameMode.AutoHostOrClient, sceneRef, 1));
    }
  }

  /// <summary>
  /// Start a Fusion server instance, and the number of client instances indicated by <see cref="AutoClients"/>. 
  /// InstanceMode must be set to Multi-Peer mode, as this requires multiple <see cref="NetworkRunner"/> instances.
  /// </summary>
  [BehaviourButtonAction("Start Server Plus Clients", true, false, nameof(IsShutdownAndMultiPeer))]
  public virtual void StartServerPlusClients() {
    StartServerPlusClients(AutoClients);
  }

  /// <summary>
  /// Start a Fusion host instance, and the number of client instances indicated by <see cref="AutoClients"/>. 
  /// InstanceMode must be set to Multi-Peer mode, as this requires multiple <see cref="NetworkRunner"/> instances.
  /// </summary>
  [BehaviourButtonAction("Start Host Plus Clients", true, false, nameof(IsShutdownAndMultiPeer))]
  public void StartHostPlusClients() {
    StartHostPlusClients(AutoClients);
  }

  [BehaviourButtonAction("Shutdown", true, false, nameof(CurrentStage))]
  public void Shutdown() {
    ShutdownAll();
  }

  /// <summary>
  /// Start a Fusion server instance, and the indicated number of client instances. 
  /// InstanceMode must be set to Multi-Peer mode, as this requires multiple <see cref="NetworkRunner"/> instances.
  /// </summary>
  public virtual void StartServerPlusClients(int clientCount) {
    if (NetworkProjectConfig.Global.PeerMode == NetworkProjectConfig.PeerModes.Multiple) {
      if (TryGetSceneRef(out var sceneRef)) {
        StartCoroutine(StartWithClients(GameMode.Server, sceneRef, clientCount));
      }
    } else {
      Debug.LogWarning($"Unable to start multiple {nameof(NetworkRunner)}s in Unique Instance mode.");
    }
  }

  /// <summary>
  /// Start a Fusion host instance (server with local player), and the indicated number of additional client instances. 
  /// InstanceMode must be set to Multi-Peer mode, as this requires multiple <see cref="NetworkRunner"/> instances.
  /// </summary>
  public void StartHostPlusClients(int clientCount) {
    if (NetworkProjectConfig.Global.PeerMode == NetworkProjectConfig.PeerModes.Multiple) {
      if (TryGetSceneRef(out var sceneRef)) {
        StartCoroutine(StartWithClients(GameMode.Host, sceneRef, clientCount));
      }
    } else {
      Debug.LogWarning($"Unable to start multiple {nameof(NetworkRunner)}s in Unique Instance mode.");
    }
  }

  /// <summary>
  /// Start a Fusion host instance (server with local player), and the indicated number of additional client instances. 
  /// InstanceMode must be set to Multi-Peer mode, as this requires multiple <see cref="NetworkRunner"/> instances.
  /// </summary>
  public void StartMultipleClients(int clientCount) {
    if (NetworkProjectConfig.Global.PeerMode == NetworkProjectConfig.PeerModes.Multiple) {
      if (TryGetSceneRef(out var sceneRef)) {
        StartCoroutine(StartWithClients(GameMode.Client, sceneRef, clientCount));
      }
    } else {
      Debug.LogWarning($"Unable to start multiple {nameof(NetworkRunner)}s in Unique Instance mode.");
    }
  }

  /// <summary>
  /// Start as Room on the Photon cloud, and connects as one or more clients.
  /// </summary>
  /// <param name="clientCount"></param>
  public void StartMultipleSharedClients(int clientCount) {
    if (NetworkProjectConfig.Global.PeerMode == NetworkProjectConfig.PeerModes.Multiple) {
      if (TryGetSceneRef(out var sceneRef)) {
        StartCoroutine(StartWithClients(GameMode.Shared, sceneRef, clientCount));
      }
    } else {
      Debug.LogWarning($"Unable to start multiple {nameof(NetworkRunner)}s in Unique Instance mode.");
    }
  }

  public void StartMultipleAutoClients(int clientCount) {
    if (NetworkProjectConfig.Global.PeerMode == NetworkProjectConfig.PeerModes.Multiple) {
      if (TryGetSceneRef(out var sceneRef)) {
        StartCoroutine(StartWithClients(GameMode.AutoHostOrClient, sceneRef, clientCount));
      }
    } else {
      Debug.LogWarning($"Unable to start multiple {nameof(NetworkRunner)}s in Unique Instance mode.");
    }
  }

  public void ShutdownAll() {
    foreach (var runner in NetworkRunner.Instances.ToList()) {
      if (runner != null && runner.IsRunning) {
        runner.Shutdown();
      }
    }

    SceneManager.LoadSceneAsync(_initialScenePath);
    // Destroy our DontDestroyOnLoad objects to finish the reset
    Destroy(RunnerPrefab.gameObject);
    Destroy(gameObject);
    CurrentStage = Stage.Disconnected;
    CurrentServerMode = 0;
  }


  protected IEnumerator StartWithClients(GameMode serverMode, SceneRef sceneRef, int clientCount) {
    // Avoid double clicks or disallow multiple startup calls.
    if (CurrentStage != Stage.Disconnected) {
      yield break;
    }

    bool includesServerStart = serverMode != GameMode.Shared && serverMode != GameMode.Client && serverMode != GameMode.AutoHostOrClient;

    if (!includesServerStart && clientCount == 0) {
      Debug.LogError($"{nameof(GameMode)} is set to {serverMode}, and {nameof(clientCount)} is set to zero. Starting no network runners.");
      yield break;
    }

    CurrentStage = Stage.StartingUp;

    var currentScene = SceneManager.GetActiveScene();

    // must have a runner
    if (!RunnerPrefab) {
      Debug.LogError($"{nameof(RunnerPrefab)} not set, can't perform debug start.");
      yield break;
    }

    // Clone the RunnerPrefab so we can safely delete the startup scene (the prefab might be part of it, rather than an asset).
    RunnerPrefab = Instantiate(RunnerPrefab);
    DontDestroyOnLoad(RunnerPrefab);
    RunnerPrefab.name = "Temporary Runner Prefab";

    // Single-peer can't start more than one peer. Validate clientCount to make sure it complies with current PeerMode.
    var config = NetworkProjectConfig.Global;
    if (config.PeerMode != NetworkProjectConfig.PeerModes.Multiple) {
      int maxClientsAllowed = includesServerStart ? 0 : 1;
      if (clientCount > maxClientsAllowed) {
        Debug.LogWarning($"Instance mode must be set to {nameof(NetworkProjectConfig.PeerModes.Multiple)} to perform a debug start multiple peers. Restricting client count to {maxClientsAllowed}.");
        clientCount = maxClientsAllowed;
      }
    }

    // If NDS is starting more than 1 shared or auto client, they need to use the same Session Name, otherwise, they will end up on different Rooms
    // as Fusion creates a Random Session Name when no name is passed on the args
    if ((serverMode == GameMode.Shared || serverMode == GameMode.AutoHostOrClient || serverMode == GameMode.Server || serverMode == GameMode.Host) && 
         clientCount > 1 && config.PeerMode == NetworkProjectConfig.PeerModes.Multiple) {

      if (string.IsNullOrEmpty(DefaultRoomName)) {
        DefaultRoomName = Guid.NewGuid().ToString();
        Debug.Log($"Generated Session Name: {DefaultRoomName}");
      }
    }

    if (gameObject.transform.parent) {
      Debug.LogWarning($"{nameof(NetworkDebugStart)} can't be a child game object, un-parenting.");
      gameObject.transform.parent = null;
    }

    DontDestroyOnLoad(gameObject);
    CurrentServerMode = serverMode;

    // start server, just take address from it
    if (includesServerStart) {
      _server = Instantiate(RunnerPrefab);
      _server.name = serverMode.ToString();

      var serverTask = InitializeNetworkRunner(_server, serverMode, NetAddress.Any(ServerPort), sceneRef, (runner) => {
#if FUSION_DEV
        var name = _server.name; // closures do not capture values, need a local var to save it
        Debug.Log($"Server NetworkRunner '{name}' started.");
#endif
      });

      while(serverTask.IsCompleted == false) {
        yield return new WaitForSeconds(1f);
      }

      if (serverTask.IsFaulted) {
        Log.Debug($"Unable to start server: {serverTask.Exception}");

        ShutdownAll();
        yield break;
      }

      // this action is called after InitializeNetworkRunner for the server has completed startup
      yield return StartClients(clientCount, serverMode, sceneRef);

    } else {
      yield return StartClients(clientCount, serverMode, sceneRef);
    }

    // Add stats last, so any event systems that may be getting created are already in place.
    if (includesServerStart && AlwaysShowStats && serverMode != GameMode.Shared) {
      FusionStats.Create(runner: _server, screenLayout: FusionStats.DefaultLayouts.Left, objectLayout: FusionStats.DefaultLayouts.Left);
    }
  }

  [BehaviourButtonAction("Add Additional Client", conditionMember: nameof(CanAddClients))]
  public void AddClient() {
    if (TryGetSceneRef(out var sceneRef)) {
      AddClient(GameMode.Client, sceneRef);
    }
  }

  [BehaviourButtonAction("Add Additional Shared Client", conditionMember: nameof(CanAddSharedClients))]
  public void AddSharedClient() {
    if (TryGetSceneRef(out var sceneRef)) {
      AddClient(GameMode.Shared, sceneRef);
    }
  }

  public Task AddClient(GameMode serverMode, SceneRef sceneRef) {
    var client = Instantiate(RunnerPrefab);
    DontDestroyOnLoad(client);

    client.name = $"Client {(Char)(65 + LastCreatedClientIndex++)}";

    // if server mode is Shared or AutoHostOrClient, then game client mode is the same as the server, otherwise it is client
    var mode = GameMode.Client;
    switch (serverMode) {
      case GameMode.Shared:
      case GameMode.AutoHostOrClient:
        mode = serverMode;
        break;
    }

#if FUSION_DEV
      var clientTask = InitializeNetworkRunner(client, mode, NetAddress.Any(), sceneRef, (runner) => {
        var name = client.name; // closures do not capture values, need a local var to save it
        Debug.Log($"Client NetworkRunner '{name}' started.");
      });
#else
      var clientTask = InitializeNetworkRunner(client, mode, NetAddress.Any(), sceneRef, null);
#endif

    // Add stats last, so that event systems that may be getting created are already in place.
    if (AlwaysShowStats && LastCreatedClientIndex == 0) {
      FusionStats.Create(runner: client, screenLayout: FusionStats.DefaultLayouts.Right, objectLayout: FusionStats.DefaultLayouts.Right);
    }

    return clientTask;
  }

  protected IEnumerator StartClients(int clientCount, GameMode serverMode, SceneRef sceneRef = default) {

    CurrentStage = Stage.ConnectingClients;

    var clientTasks = new List<Task>();
    for (int i = 0; i < clientCount; ++i) {
      clientTasks.Add(AddClient(serverMode, sceneRef));
      yield return new WaitForSeconds(0.1f);
    }

    var clientsStartTask = Task.WhenAll(clientTasks);

    while (clientsStartTask.IsCompleted == false) {
      yield return new WaitForSeconds(1f);
    }

    if (clientsStartTask.IsFaulted) {
      Debug.LogWarning(clientsStartTask.Exception);
    }

    CurrentStage = Stage.AllConnected;
  }

  protected virtual Task InitializeNetworkRunner(NetworkRunner runner, GameMode gameMode, NetAddress address, SceneRef scene, Action<NetworkRunner> initialized) {
    
    var sceneManager = runner.GetComponents(typeof(MonoBehaviour)).OfType<INetworkSceneManager>().FirstOrDefault();
    if (sceneManager == null) {
      Debug.Log($"NetworkRunner does not have any component implementing {nameof(INetworkSceneManager)} interface, adding {nameof(NetworkSceneManagerDefault)}.", runner);
      sceneManager = runner.gameObject.AddComponent<NetworkSceneManagerDefault>();
    }

    return runner.StartGame(new StartGameArgs {
      GameMode = gameMode,
      Address = address,
      Scene = scene,
      SessionName = DefaultRoomName,
      Initialized = initialized,
      SceneManager = sceneManager
    });
  }

#if UNITY_EDITOR
  // Draws the button at the bottom of the inspector if scene currently is not added to Build Settings scene list.
  [BehaviourAction()]
  void DisplayAddToSceneButtonIfNeeded() {
    if (Application.isPlaying)
      return;
    var currentScene = SceneManager.GetActiveScene();
    if (currentScene.TryGetSceneIndexInBuildSettings(out var _) == false) {
      GUILayout.Space(4);
      var clicked = BehaviourEditorUtils.DrawWarnButton(new GUIContent("Add Scene To Settings", "Will add current scene to Unity Build Settings list."), MessageType.Warning);
      if (clicked) {
        if (currentScene.name == "") {
          UnityEditor.SceneManagement.EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
        }

        if (currentScene.name != "") {
          currentScene.AddSceneToBuildSettings();
        }
      }
    }
  }

#endif
}
