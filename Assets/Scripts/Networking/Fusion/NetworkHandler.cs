using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using Newtonsoft.Json;

using static ConnectionToken;
using Fusion;
using Fusion.Photon.Realtime;
using Fusion.Sockets;
using NSMB.Extensions;
using NSMB.Game;
using NSMB.Utils;
using NSMB.UI.MainMenu;

public class NetworkHandler : Singleton<NetworkHandler>, INetworkRunnerCallbacks {

    //---Static Variables
    public static readonly string[] Regions = { "asia", "eu", "jp", "kr", "sa", "us", "usw" };
    public static readonly Dictionary<string, string> RegionIPs = new() {
        ["asia"] = "15.235.132.46",
        ["eu"] = "192.36.27.39",
        ["jp"] = "139.162.127.196",
        ["kr"] = "158.247.198.230",
        ["sa"] = "200.25.36.72",
        ["us"] = "45.86.230.227",
        ["usw"] = "45.145.148.21",
    };
    public static readonly Dictionary<string, int> RegionPings = new();
    public static readonly string RoomIdValidChars = "BCDFGHJKLMNPRQSTVWXYZ";
    private static readonly int RoomIdLength = 8;


    private static GameObject prefab;
    private static bool reattemptCreate;
    public static int connecting;
    private static List<PlayerData> playerDatas = new();

    //---Properties
    public static string CurrentRegion { get; set; }
    public static NetworkRunner Runner => Instance.runner;
    public static bool Connecting => connecting > 0 || AuthenticationHandler.IsAuthenticating || (Runner && Runner.State == NetworkRunner.States.Starting && !Runner.IsCloudReady) || (Runner && Runner.State == NetworkRunner.States.Running && !Runner.IsConnectedToServer && !Runner.IsServer);
    public static bool Connected => !Connecting && Runner && (Runner.State == NetworkRunner.States.Running || Runner.IsCloudReady);
    public static bool Disconnected => !Connecting && !Connected;

    //---Public
    public NetworkRunner runner;

    #region Events
    //---Exposed callbacks for Events
    public delegate void OnConnectedToServerDelegate(NetworkRunner runner);
    public static event OnConnectedToServerDelegate OnConnectedToServer;

    public delegate void OnConnectFailedDelegate(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason);
    public static event OnConnectFailedDelegate OnConnectFailed;

    public delegate bool OnConnectRequestDelegate(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token);
    public static event OnConnectRequestDelegate OnConnectRequest;

    public delegate void OnDisconnectedFromServerDelegate(NetworkRunner runner, NetDisconnectReason disconnectReason);
    public static event OnDisconnectedFromServerDelegate OnDisconnectedFromServer;

    public delegate void OnCustomAuthenticationResponseDelegate(NetworkRunner runner, Dictionary<string, object> data);
    public static event OnCustomAuthenticationResponseDelegate OnCustomAuthenticationResponse;

    public delegate void OnHostMigrationDelegate(NetworkRunner runner, HostMigrationToken hostMigrationToken);
    public static event OnHostMigrationDelegate OnHostMigration;

    public delegate void OnInputDelegate(NetworkRunner runner, NetworkInput input);
    public static event OnInputDelegate OnInput;

    public delegate void OnInputMissingDelegate(NetworkRunner runner, PlayerRef player, NetworkInput input);
    public static event OnInputMissingDelegate OnInputMissing;

    public delegate void OnPlayerJoinedDelegate(NetworkRunner runner, PlayerRef player);
    public static event OnPlayerJoinedDelegate OnPlayerJoined;

    public delegate void OnPlayerLeftDelegate(NetworkRunner runner, PlayerRef player);
    public static event OnPlayerLeftDelegate OnPlayerLeft;

    public delegate void OnReliableDataReceivedDelegate(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data);
    public static event OnReliableDataReceivedDelegate OnReliableDataReceived;

    public delegate void OnReliableDataProgressDelegate(NetworkRunner runner, PlayerRef player, ReliableKey key, float what);
    public static event OnReliableDataProgressDelegate OnReliableDataProgress;

    public delegate void OnSceneLoadDoneDelegate(NetworkRunner runner);
    public static event OnSceneLoadDoneDelegate OnSceneLoadDone;

    public delegate void OnSceneLoadStartDelegate(NetworkRunner runner);
    public static event OnSceneLoadStartDelegate OnSceneLoadStart;

    public delegate void OnSessionListUpdatedDelegate(NetworkRunner runner, List<SessionInfo> sessionList);
    public static event OnSessionListUpdatedDelegate OnSessionListUpdated;

    public delegate void OnShutdownDelegate(NetworkRunner runner, ShutdownReason shutdownReason);
    public static event OnShutdownDelegate OnShutdown;

    public delegate void OnUserSimulationMessageDelegate(NetworkRunner runner, SimulationMessagePtr message);
    public static event OnUserSimulationMessageDelegate OnUserSimulationMessage;

    public delegate void OnLobbyConnectDelegate(NetworkRunner runner, LobbyInfo lobby);
    public static event OnLobbyConnectDelegate OnLobbyConnect;

    //---Custom Events
    public static event Action OnRegionPingsUpdated;
    #endregion

    #region NetworkRunner Callbacks
    void INetworkRunnerCallbacks.OnConnectedToServer(NetworkRunner runner) {
        if (runner.LobbyInfo.IsValid) {
            // Connected to a lobby
            Debug.Log($"[Network] Successfully connected to a Lobby ({runner.LobbyInfo.Name}, {runner.LobbyInfo.Region})");
        } else if (runner.SessionInfo.IsValid) {
            // Connected to a session
            Debug.Log($"[Network] Successfully connected to a Room ({runner.SessionInfo.Name}, {runner.SessionInfo.Region})");
        }

        OnConnectedToServer?.Invoke(runner);
    }

    void INetworkRunnerCallbacks.OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) {
        Debug.LogError($"[Network] Failed to connect to the server: {reason}");

        OnConnectFailed?.Invoke(runner, remoteAddress, reason);
    }

    void INetworkRunnerCallbacks.OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) {
        ConnectionToken connectionToken = Deserialize(token);
        Debug.Log($"[Network] Incoming connection request from {connectionToken.signedData.UserId}");

        if (runner.SessionInfo.PlayerCount > SessionData.Instance.MaxPlayers) {
            request.Refuse();
            return;
        }

        bool accept = OnConnectRequest?.Invoke(runner, request, token) ?? true;
        if (accept) {
            request.Accept();
        } else {
            request.Refuse();
        }
    }

    void ReturnToMainMenu(Action callback) {
        if (SceneManager.GetActiveScene().buildIndex == 0) {
            callback();
        } else {
            AsyncOperation op = SceneManager.LoadSceneAsync(0, LoadSceneMode.Single);
            // Weird null check for exiting play mode in the editor
            if (op != null) {
                op.completed += delegate (AsyncOperation operation) {
                    callback();
                };
            }
        }
    }

    void INetworkRunnerCallbacks.OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) {
        Debug.Log($"[Network] Disconnected from Server (Reason: {reason})");

        ReturnToMainMenu(() => {
            OnDisconnectedFromServer?.Invoke(runner, reason);
            RecreateInstance();
        });
    }

    void INetworkRunnerCallbacks.OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) {

        if (data.ContainsKey("Token") && data["Token"] != null) {
            PlayerPrefs.SetString("token", (string) data["Token"]);
            PlayerPrefs.Save();
        }

        if (data.ContainsKey("SignedData")) {
            SignedResultData signedData = JsonConvert.DeserializeObject<SignedResultData>((string) data["SignedData"]);
            ConnectionToken connectionToken = new() {
                signedData = signedData,
                signature = (string) data["Signature"],
            };
            if (connectionToken.HasValidSignature()) {
                // Good to go :)
                Debug.Log($"[Network] Authenication successful ({signedData.UserId}), server signature verified");
                GlobalController.Instance.connectionToken = connectionToken;
            } else {
                Debug.LogWarning("[Network] Authentication server responded with signed data, but it had an invalid signature. Possible server spoofing?");
            }
        } else {
            Debug.LogWarning("[Network] Authentication server did not respond with any signed data, ID Spoofing is possible");
        }

        OnCustomAuthenticationResponse?.Invoke(runner, data);
    }

    async void INetworkRunnerCallbacks.OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) {
        Debug.Log($"[Network] Starting host migration, we will become a {hostMigrationToken.GameMode}");
        MainMenuManager.WasHostMigration = true;
        GlobalController.Instance.connecting.SetActive(true);

        if (GameManager.Instance) {
            ChatManager.Instance.AddSystemMessage("ui.inroom.chat.server.ended.hostdc");
        }

        await runner.Shutdown(shutdownReason: ShutdownReason.HostMigration);
        RecreateInstance();

        _ = Runner.StartGame(new() {
            HostMigrationToken = hostMigrationToken,
            HostMigrationResume = HostMigrationResume,
            ConnectionToken = GlobalController.Instance.connectionToken.Serialize(),
            DisableNATPunchthrough = Settings.Instance.generalDisableNATPunchthrough,
            EnableClientSessionCreation = true,
            SceneManager = Runner.gameObject.AddComponent<MvLSceneManager>(),
            Scene = SceneRef.FromIndex(0)
        });

        OnHostMigration?.Invoke(runner, hostMigrationToken);
    }

    void INetworkRunnerCallbacks.OnInput(NetworkRunner runner, NetworkInput input) {
        try {
            OnInput?.Invoke(runner, input);
        } catch (Exception e) {
            Debug.LogError($"[Network] Caught an exception while handling OnInput: {e.Message}");
        }
    }

    void INetworkRunnerCallbacks.OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) {
        OnInputMissing?.Invoke(runner, player, input);
    }

    void INetworkRunnerCallbacks.OnPlayerJoined(NetworkRunner runner, PlayerRef player) {

        // Fusion 2.0.0 rc7 bug.
        if (!runner.IsPlayerValid(player)) {
            return;
        }

        // Handle PlayerDatas
        bool hadExistingData = false;
        if (runner.IsServer && !runner.IsSinglePlayer) {
            PlayerData existingData = FindObjectsByType<PlayerData>(FindObjectsSortMode.None)
                .Where(pd => pd.UserId.ToString() == runner.GetPlayerUserId(player))
                .SingleOrDefault();
            hadExistingData = existingData;

            if (hadExistingData) {
                existingData.ReassignPlayerData(player);
            } else {
                NetworkObject obj = runner.Spawn(PrefabList.Instance.PlayerDataHolder, inputAuthority: player, onBeforeSpawned: (runner, obj) => obj.GetComponent<PlayerData>().OnBeforeSpawned());
                playerDatas.Add(obj.GetComponent<PlayerData>());
            }

            if (runner.Tick != 0) {
                runner.PushHostMigrationSnapshot();
            }
        }

        // Please spare a join message, sir?
        if (!hadExistingData) {
            SessionData.PlayersNeedingJoinMessage.Add(player);
            PlayerData data = player.GetPlayerData(runner);
            if (data) {
                data.SendJoinMessageIfNeeded();
            }
        }

        // Update Discord integration
        if (player != runner.LocalPlayer) {
            GlobalController.Instance.discordController.UpdateActivity();
        }

        OnPlayerJoined?.Invoke(runner, player);
    }

    void INetworkRunnerCallbacks.OnPlayerLeft(NetworkRunner runner, PlayerRef player) {

        OnPlayerLeft?.Invoke(runner, player);

        PlayerData data = null;
        foreach (PlayerData d in playerDatas) {
            if (d.Object.InputAuthority == player) {
                data = d;
                break;
            }
        }

        if (data) {
            Debug.Log($"[Network] {data.GetNickname()} ({data.GetUserIdString()}) left the room");
            ChatManager.Instance.AddSystemMessage("ui.inroom.chat.player.quit", "playername", data.GetNickname());
            if (Runner.IsServer) {
                runner.Despawn(data.Object);
            }
            playerDatas.Remove(data);
        }

        GlobalController.Instance.discordController.UpdateActivity();
    }

    void INetworkRunnerCallbacks.OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) {
        OnReliableDataReceived?.Invoke(runner, player, key, data);
    }

    void INetworkRunnerCallbacks.OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float what) {
        OnReliableDataProgress?.Invoke(runner, player, key, what);
    }

    void INetworkRunnerCallbacks.OnSceneLoadDone(NetworkRunner runner) {
        OnSceneLoadDone?.Invoke(runner);
    }

    void INetworkRunnerCallbacks.OnSceneLoadStart(NetworkRunner runner) {
        OnSceneLoadStart?.Invoke(runner);
    }

    void INetworkRunnerCallbacks.OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) {
        OnSessionListUpdated?.Invoke(runner, sessionList);
    }

    void INetworkRunnerCallbacks.OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason) {
        Debug.Log($"[Network] Network Shutdown: {shutdownReason}");

        if (shutdownReason == ShutdownReason.HostMigration) {
            return;
        }

        if (shutdownReason == ShutdownReason.ServerInRoom && reattemptCreate) {
            reattemptCreate = false;
            return;
        } else {
            reattemptCreate = false;
        }

        ReturnToMainMenu(() => {
            OnShutdown?.Invoke(runner, shutdownReason);
            RecreateInstance();
        });
    }

    void INetworkRunnerCallbacks.OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) {
        OnUserSimulationMessage?.Invoke(runner, message);
    }

    void INetworkRunnerCallbacks.OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) {

    }

    void INetworkRunnerCallbacks.OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) {

    }
    #endregion

    #region Unity Callbacks
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    public static async void RecreateInstance() {
        if (!prefab) {
            prefab = (GameObject) Resources.Load("Prefabs/Static/NetworkingHandler");
        }

        if (Instance) {
            if (!Instance.runner.IsShutdown) {
                await Instance.runner.Shutdown(shutdownReason: ShutdownReason.Ok);
            }
            DestroyImmediate(Instance.gameObject);
        }

        Instance = Instantiate(prefab).GetComponent<NetworkHandler>();
        Instance.Initialize();
    }

    public void Initialize() {
        DontDestroyOnLoad(this);
        runner = GetComponent<NetworkRunner>();
        runner.ProvideInput = true;
        runner.AddCallbacks(this);
    }

    public void OnApplicationQuit() {
        if (Runner && !Runner.IsShutdown) {
            Runner.Shutdown();
        }
    }
    #endregion

    #region Room-Related Methods
    private static async Task<AuthenticationValues> Authenticate() {
        connecting++;

        string id = PlayerPrefs.GetString("id", null);
        string token = PlayerPrefs.GetString("token", null);

        AuthenticationValues authValues = await AuthenticationHandler.Authenticate(id, token);
        connecting--;
        return authValues;
    }

    public static async Task<StartGameResult> ConnectToSameRegion() {
        return await ConnectToRegion(CurrentRegion);
    }

    public static async Task<StartGameResult> ConnectToRegion(string region = "") {
        connecting++;

        // Exit if we're already in a room
        if ((Runner.SessionInfo.IsValid || Runner.LobbyInfo.IsValid) && !Runner.IsShutdown) {
            await Runner.Shutdown();
        } else {
            DestroyImmediate(Runner);
            Instance.runner = Instance.gameObject.AddComponent<NetworkRunner>();
        }

        if (string.IsNullOrEmpty(region)) {
            Debug.Log("[Network] Connecting to lobby with best ping.");
            region = "";
        } else {
            Debug.Log($"[Network] Connecting to lobby {region}");
        }

        // Version separation
        FusionAppSettings appSettings = new() {
            AppIdFusion = PhotonAppSettings.Global.AppSettings.AppIdFusion,
            AppVersion = Regex.Match(Application.version, "^\\w*\\.\\w*\\.\\w*").Groups[0].Value,
            EnableLobbyStatistics = true,
            UseNameServer = true,
            FixedRegion = region,
        };

        // Authenticate
        AuthenticationValues authValues = await Authenticate();
        if (authValues == null) {
            connecting--;
            OnShutdown?.Invoke(Runner, ShutdownReason.CustomAuthenticationFailed);
            return null;
        }

        // And join lobby
        StartGameResult result = await Runner.JoinSessionLobby(SessionLobby.ClientServer, authentication: authValues, customAppSettings: appSettings);
        if (result.Ok) {
            CurrentRegion = Runner.LobbyInfo.Region;

            Debug.Log($"[Network] Connected to a Lobby ({Runner.LobbyInfo.Name}, {CurrentRegion})");

            // Save id for later authentication
            PlayerPrefs.SetString("id", Runner.AuthenticationValues.UserId);
            PlayerPrefs.Save();

            OnLobbyConnect?.Invoke(Runner, Runner.LobbyInfo);
        } else {
            OnShutdown?.Invoke(Runner, result.ShutdownReason);
        }

        connecting--;
        return result;
    }

    public static async Task<StartGameResult> CreateRoom(StartGameArgs args, GameMode gamemode = GameMode.Host, int players = 10) {
        GlobalController.Instance.connectionToken.nickname = Settings.Instance.generalNickname;

        connecting++;
        int attempts = 3;

        while (attempts-- > 0) {
            if (attempts != 0) {
                reattemptCreate = true;
            }

            // Create a random room id.
            StringBuilder idBuilder = new();

            // First char should correspond to region.
            int index = Array.IndexOf(Regions, CurrentRegion);
            idBuilder.Append(RoomIdValidChars[index >= 0 ? index : 0]);

            // Fill rest of the string with random chars
            UnityEngine.Random.InitState(unchecked((int) DateTime.UtcNow.Subtract(DateTime.UnixEpoch).TotalSeconds));
            for (int i = 1; i < RoomIdLength; i++) {
                idBuilder.Append(RoomIdValidChars[UnityEngine.Random.Range(0, RoomIdValidChars.Length)]);
            }

            AuthenticationValues authValues = await Authenticate();
            if (authValues == null) {
                connecting--;
                OnShutdown?.Invoke(Runner, ShutdownReason.CustomAuthenticationFailed);
                return null;
            }

            if (gamemode == GameMode.Single) {
                Debug.Log($"[Network] Creating a singleplayer game");
            } else {
                Debug.Log($"[Network] Creating a game in {CurrentRegion} with the ID {idBuilder}");
            }

            args.AuthValues = authValues;
            args.GameMode = gamemode;
            args.SessionName = idBuilder.ToString();
            args.ConnectionToken = GlobalController.Instance.connectionToken.Serialize();
            args.DisableNATPunchthrough = Settings.Instance.generalDisableNATPunchthrough;
            args.SceneManager = Runner.gameObject.AddComponent<MvLSceneManager>();
            args.SessionProperties = NetworkUtils.DefaultRoomProperties;
            args.OnGameStarted = RoomInitialized;
            args.Scene = SceneRef.FromIndex(0);

            args.SessionProperties[Enums.NetRoomProperties.HostName] = Settings.Instance.generalNickname;
            args.SessionProperties[Enums.NetRoomProperties.MaxPlayers] = players;

            // Attempt to create the room
            StartGameResult results = await Runner.StartGame(args);
            if (results.Ok) {
                connecting--;
                return results;
            }

            if (results.ShutdownReason != ShutdownReason.GameIdAlreadyExists && results.ShutdownReason != ShutdownReason.ServerInRoom) {
                connecting--;
                return null;
            }

            Debug.Log($"[Network] Failed to create a game with the ID {idBuilder} (the id already exists.) Trying {attempts} more time(s)...");
        }

        connecting--;
        return null;
    }

    public static async Task<StartGameResult> JoinRoom(string roomId) {
        GlobalController.Instance.connectionToken.nickname = Settings.Instance.generalNickname;

        connecting++;

        // Make sure that we're on the right region...
        string originalRegion = CurrentRegion;
        string targetRegion = Regions[RoomIdValidChars.IndexOf(roomId[0])];

        if (CurrentRegion != targetRegion) {
            // Change regions
            await ConnectToRegion(targetRegion);
        }

        if (MainMenuManager.Instance) {
            MainMenuManager.Instance.nonNetworkShutdown = true;
        }

        Debug.Log($"[Network] Attempting to join game with ID: {roomId}");
        // Attempt to join the room
        StartGameResult result = await Runner.StartGame(new() {
            GameMode = GameMode.Client,
            SessionName = roomId,
            ConnectionToken = GlobalController.Instance.connectionToken.Serialize(),
            DisableNATPunchthrough = Settings.Instance.generalDisableNATPunchthrough,
            EnableClientSessionCreation = false,
            SceneManager = Runner.gameObject.AddComponent<MvLSceneManager>(),
            OnGameStarted = RoomInitialized,
        });
        if (!result.Ok) {
            Debug.Log($"[Network] Failed to join game: {result.ShutdownReason}");
            // Automatically go back to the lobby.
            await ConnectToRegion(originalRegion);
        }

        if (MainMenuManager.Instance) {
            MainMenuManager.Instance.nonNetworkShutdown = false;
        }

        connecting--;
        return result;
    }
    #endregion

    private static float pingStartTime;
    public static IEnumerator PingRegions() {
        foreach ((string region, _) in RegionIPs) {
            RegionPings[region] = 0;
        }

        pingStartTime = Time.time;
        Dictionary<string, Ping> pingers = new();

        foreach ((string region, string ip) in RegionIPs) {
            pingers[region] = new(ip);
        }

        bool anyRemaining = true;
        while (anyRemaining && Time.time - pingStartTime < 4) {
            anyRemaining = false;

            foreach ((string region, Ping pinger) in pingers) {
                if (pinger == null) {
                    continue;
                }

                if (!pinger.isDone) {
                    anyRemaining = true;
                    continue;
                }

                // Ping done.
                RegionPings[region] = pinger.time;
                pinger.DestroyPing();
            }
            yield return null;
        }

        OnRegionPingsUpdated?.Invoke();
    }

    private static void RoomInitialized(NetworkRunner runner) {

        playerDatas.Clear();
        SessionData.PlayersNeedingJoinMessage.Clear();

        if (runner.IsServer) {
            NetworkObject session = runner.Spawn(PrefabList.Instance.SessionDataHolder);
            SessionData.Instance = session.GetComponent<SessionData>();
        }
    }

    private static void HostMigrationResume(NetworkRunner runner) {

        if (!runner.IsServer) {
            return;
        }

        // Update the room's name to be our own.
        runner.SessionInfo.UpdateCustomProperties(new() {
            [Enums.NetRoomProperties.HostName] = Settings.Instance.generalNickname,
        });

        foreach (var resumeNO in runner.GetResumeSnapshotNetworkObjects()) {
            if (resumeNO.TryGetComponent(out SessionData _)) {
                runner.Spawn(resumeNO, onBeforeSpawned: (runner, newNO) => {
                    newNO.CopyStateFrom(resumeNO);
                });
            } else if (resumeNO.TryGetComponent(out PlayerData pd)) {

                // Don't respawn the PlayerData for the host that just left. Stupid.
                if (pd.Object.InputAuthority.AsIndex == runner.SessionInfo.MaxPlayers - 1) {
                    continue;
                }

                // Oh, and immediately assign our own. We're greedy :)
                // (not doing it breaks stuff cuz EnterRoom is called first...)
                PlayerRef? player = null;
                if (pd.GetUserIdString() == runner.GetPlayerUserId()) {
                    player = runner.LocalPlayer;
                }

                runner.Spawn(resumeNO, inputAuthority: player, onBeforeSpawned: (runner, newNO) => {
                    newNO.CopyStateFrom(resumeNO);
                });
            }
        }

        if (MainMenuManager.Instance) {
            MainMenuManager.WasHostMigration = true;
            MainMenuManager.Instance.EnterRoom(false);
        }
    }
}
