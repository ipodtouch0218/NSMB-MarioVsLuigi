using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEngine;

using static ConnectionToken;
using Fusion;
using Fusion.Photon.Realtime;
using Fusion.Sockets;
using NSMB.Extensions;
using NSMB.Utils;
using NSMB.UI.MainMenu;
using NSMB.Game;

public class NetworkHandler : Singleton<NetworkHandler>, INetworkRunnerCallbacks {

    //---Static Variables
    public static readonly string[] Regions = { "asia", "eu", "jp", "kr", "sa", "us", "usw" };
    public static readonly string[] RegionIps = { "15.235.132.46", "192.36.27.39", "139.162.127.196", "158.247.198.230", "200.25.36.72", "45.86.230.227", "45.145.148.21" };
    public static int[] RegionPings;

    public static readonly string RoomIdValidChars = "BCDFGHJKLMNPRQSTVWXYZ";
    private static readonly int RoomIdLength = 8;

    private static GameObject prefab;
    private static bool reattemptCreate;
    public static int connecting;

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

    public delegate void OnDisconnectedFromServerDelegate(NetworkRunner runner);
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

    public delegate void OnReliableDataReceivedDelegate(NetworkRunner runner, PlayerRef player, ArraySegment<byte> data);
    public static event OnReliableDataReceivedDelegate OnReliableDataReceived;

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
        Debug.LogError($"[Network] Failed to connect to the server ({remoteAddress}): {reason}");

        OnConnectFailed?.Invoke(runner, remoteAddress, reason);
    }

    void INetworkRunnerCallbacks.OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) {
        Debug.Log($"[Network] Incoming connection request from {request.RemoteAddress}");

        if (runner.SessionInfo.PlayerCount > SessionData.Instance.MaxPlayers) {
            request.Refuse();
            return;
        }

        bool accept = OnConnectRequest?.Invoke(runner, request, token) ?? true;
        if (accept)
            request.Accept();
        else
            request.Refuse();
    }

    void INetworkRunnerCallbacks.OnDisconnectedFromServer(NetworkRunner runner) {
        Debug.Log("[Network] Disconnected from Server");

        OnDisconnectedFromServer?.Invoke(runner);
        RecreateInstance();
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
                Debug.LogWarning("[Network] Authentication server responded with signed data, but it had an invalid signature");
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

        if (GameManager.Instance)
            ChatManager.Instance.AddSystemMessage("ui.inroom.chat.server.ended.hostdc");

        await runner.Shutdown(shutdownReason: ShutdownReason.HostMigration);
        RecreateInstance();

        StartGameResult result = await Runner.StartGame(new() {
            HostMigrationToken = hostMigrationToken,
            HostMigrationResume = HostMigrationResume,
            ConnectionToken = GlobalController.Instance.connectionToken.Serialize(),
            DisableNATPunchthrough = Settings.Instance.generalDisableNATPunchthrough,
            DisableClientSessionCreation = false,
            SceneManager = Runner.gameObject.AddComponent<MvLSceneManager>(),
            Scene = 0,
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
        Debug.Log($"[Network] Player joined room (UserId = {runner.GetPlayerUserId(player)})");

        if (runner.IsServer && !runner.IsSinglePlayer) {
            // Create player data
            PlayerData[] dataObjects = FindObjectsOfType<PlayerData>();
            PlayerData existingData = FindObjectsOfType<PlayerData>().Where(pd => pd.UserId.ToString() == runner.GetPlayerUserId(player)).SingleOrDefault();

            if (existingData) {
                runner.SetPlayerObject(player, existingData.Object);
                existingData.JoinTick = -1;
            } else {
                runner.Spawn(PrefabList.Instance.PlayerDataHolder, inputAuthority: player, onBeforeSpawned: (runner, obj) => obj.GetComponent<PlayerData>().OnBeforeSpawned());
            }

            if (player == runner.LocalPlayer) {
                if (SessionData.Instance) {
                    // Inherited room data, change the host name to ours.
                    runner.SessionInfo.UpdateCustomProperties(new() {
                        [Enums.NetRoomProperties.HostName] = Settings.Instance.generalNickname,
                    });
                } else {
                    // Create room data
                    NetworkObject session = runner.Spawn(PrefabList.Instance.SessionDataHolder);
                    SessionData.Instance = session.GetComponent<SessionData>();
                }
            }
            Runner.PushHostMigrationSnapshot();
        }

        if (player != runner.LocalPlayer)
            GlobalController.Instance.discordController.UpdateActivity();

        OnPlayerJoined?.Invoke(runner, player);
    }

    void INetworkRunnerCallbacks.OnPlayerLeft(NetworkRunner runner, PlayerRef player) {

        OnPlayerLeft?.Invoke(runner, player);

        PlayerData data = player.GetPlayerData(runner);
        if (data) {
            Debug.Log($"[Network] {data.GetNickname()} ({data.GetUserIdString()}) left the room");
            ChatManager.Instance.AddSystemMessage("ui.inroom.chat.player.quit", "playername", data.GetNickname());
            if (Runner.IsServer) {
                StartCoroutine(DestroyPlayerDataLater(data));
            }
        }

        GlobalController.Instance.discordController.UpdateActivity();
    }

    private IEnumerator DestroyPlayerDataLater(PlayerData data) {
        yield return new WaitForSecondsRealtime(1);
        runner.Despawn(data.Object);
    }

    void INetworkRunnerCallbacks.OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ArraySegment<byte> data) {
        OnReliableDataReceived?.Invoke(runner, player, data);
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

        if (shutdownReason == ShutdownReason.HostMigration)
            return;

        if (shutdownReason == ShutdownReason.ServerInRoom && reattemptCreate) {
            reattemptCreate = false;
            return;
        } else {
            reattemptCreate = false;
        }

        OnShutdown?.Invoke(runner, shutdownReason);
        RecreateInstance();
    }

    void INetworkRunnerCallbacks.OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) {
        OnUserSimulationMessage?.Invoke(runner, message);
    }
    #endregion

    #region Unity Callbacks
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    public static async void RecreateInstance() {
        if (!prefab)
            prefab = (GameObject) Resources.Load("Prefabs/Static/NetworkingHandler");

        if (Instance) {
            if (!Instance.runner.IsShutdown)
                await Instance.runner.Shutdown(shutdownReason: ShutdownReason.Ok);
            DestroyImmediate(Instance.gameObject);
        }

        Instance = Instantiate(prefab).GetComponent<NetworkHandler>();
        Instance.Initialize();
        Debug.Log("[Network] NetworkHandler created");
    }

    public void Initialize() {
        DontDestroyOnLoad(this);
        runner = GetComponent<NetworkRunner>();
        runner.ProvideInput = true;
        runner.AddCallbacks(this);
    }

    public void OnApplicationQuit() {
        if (Runner && !Runner.IsShutdown)
            Runner.Shutdown();
    }
    #endregion

    #region Room-Related Methods
    private static async Task<AuthenticationValues> Authenticate() {
        connecting++;
        string id = PlayerPrefs.GetString("id", null);
        string token = PlayerPrefs.GetString("token", null);
        //string id = null;
        //string token = null;

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
        AppSettings appSettings = new() {
            AppIdFusion = PhotonAppSettings.Instance.AppSettings.AppIdFusion,
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

            //save id for later authentication
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
            if (attempts != 0)
                reattemptCreate = true;

            // Create a random room id.
            StringBuilder idBuilder = new();

            // First char should correspond to region.
            int index = Array.IndexOf(Regions, CurrentRegion);
            idBuilder.Append(RoomIdValidChars[index >= 0 ? index : 0]);

            // Fill rest of the string with random chars
            UnityEngine.Random.InitState(unchecked((int) DateTime.UtcNow.Subtract(DateTime.UnixEpoch).TotalSeconds));
            for (int i = 1; i < RoomIdLength; i++)
                idBuilder.Append(RoomIdValidChars[UnityEngine.Random.Range(0, RoomIdValidChars.Length)]);

            AuthenticationValues authValues = await Authenticate();
            if (authValues == null) {
                connecting--;
                OnShutdown?.Invoke(Runner, ShutdownReason.CustomAuthenticationFailed);
                return null;
            }

            Debug.Log($"[Network] Creating a game in {CurrentRegion} with the ID {idBuilder}");
            args.AuthValues = authValues;
            args.GameMode = gamemode;
            args.SessionName = idBuilder.ToString();
            args.ConnectionToken = GlobalController.Instance.connectionToken.Serialize();
            args.DisableNATPunchthrough = Settings.Instance.generalDisableNATPunchthrough;
            args.SceneManager = Runner.gameObject.AddComponent<MvLSceneManager>();
            args.SessionProperties = NetworkUtils.DefaultRoomProperties;

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

        if (MainMenuManager.Instance)
            MainMenuManager.Instance.nonNetworkShutdown = true;

        Debug.Log($"[Network] Attempting to join game with ID: {roomId}");
        // Attempt to join the room
        StartGameResult result = await Runner.StartGame(new() {
            GameMode = GameMode.Client,
            SessionName = roomId,
            ConnectionToken = GlobalController.Instance.connectionToken.Serialize(),
            DisableNATPunchthrough = Settings.Instance.generalDisableNATPunchthrough,
            DisableClientSessionCreation = true,
            SceneManager = Runner.gameObject.AddComponent<MvLSceneManager>(),
        });
        if (!result.Ok) {
            Debug.Log($"[Network] Failed to join game: {result.ShutdownReason}");
            // Automatically go back to the lobby.
            await ConnectToRegion(originalRegion);
        }

        if (MainMenuManager.Instance)
            MainMenuManager.Instance.nonNetworkShutdown = false;

        connecting--;
        return result;
    }
    #endregion

    private static float pingStartTime;
    public static IEnumerator PingRegions() {
        RegionPings ??= new int[Regions.Length];
        for (int i = 0; i < RegionPings.Length; i++) {
            RegionPings[i] = 0;
        }

        pingStartTime = Time.time;
        Ping[] pingers = new Ping[Regions.Length];

        for (int i = 0; i < Regions.Length; i++) {
            string ip = RegionIps[i];
            pingers[i] = new Ping(ip);
        }

        bool anyRemaining = true;
        while (anyRemaining && Time.time - pingStartTime < 4) {
            anyRemaining = false;

            for (int i = 0; i < pingers.Length; i++) {
                Ping pinger = pingers[i];
                if (pinger == null)
                    continue;

                if (!pinger.isDone) {
                    anyRemaining = true;
                    continue;
                }

                // Ping done.
                RegionPings[i] = pinger.time;
                pinger.DestroyPing();
                pingers[i] = null;
            }
            yield return null;
        }

        OnRegionPingsUpdated?.Invoke();
    }

    private void HostMigrationResume(NetworkRunner runner) {

        foreach (var resumeNO in runner.GetResumeSnapshotNetworkObjects()) {

            if (resumeNO.TryGetComponent(out PlayerData data)) {
                if (resumeNO.InputAuthority == runner.SessionInfo.MaxPlayers - 1)
                    continue;

                PlayerRef newAuthority = resumeNO.InputAuthority;
                if (newAuthority != PlayerRef.None) {
                    if (newAuthority == runner.SessionInfo.MaxPlayers - 1)
                        newAuthority = PlayerRef.None;
                    else if (newAuthority == 0)
                        newAuthority = runner.SessionInfo.MaxPlayers - 1;
                    else
                        newAuthority -= 1;
                }

                runner.Spawn(resumeNO, inputAuthority: newAuthority, onBeforeSpawned: (runner, newNO) => {
                    newNO.CopyStateFrom(resumeNO);
                });
            }

            //if (resumeNO.TryGetComponent(out PlayerData data) && resumeNO.InputAuthority == runner.SessionInfo.MaxPlayers - 1) {
            //    // Don't bring over the old host's data.
            //    continue;
            //}

            //runner.Spawn(resumeNO, inputAuthority: newAuthority, onBeforeSpawned: (runner, newNO) => {
            //    newNO.CopyStateFrom(resumeNO);
            //});
        }

        //foreach ((var resumeNO, NetworkObjectHeaderPtr pointer) in runner.GetResumeSnapshotNetworkSceneObjects()) {
        //    Debug.Log(resumeNO.Name + " - " + pointer);
        //    resumeNO.CopyStateFromSceneObject(pointer);
        //}
    }
}
