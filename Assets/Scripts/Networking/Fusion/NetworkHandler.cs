using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEngine;

using Fusion;
using Fusion.Photon.Realtime;
using Fusion.Sockets;
using NSMB.Extensions;
using NSMB.Utils;

public class NetworkHandler : Singleton<NetworkHandler>, INetworkRunnerCallbacks {

    //---Static Variables
    public static readonly string[] Regions = { "asia", "eu", "jp", "kr", "sa", "us" };
    public static readonly string RoomIdValidChars = "BCDFGHJKLMNPRQSTVWXYZ";
    private static readonly int RoomIdLength = 8;

    public static string CurrentRegion;
    private static GameObject prefab;

    public static NetworkRunner Runner => Instance.runner;

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

    public delegate void OnJoinSessionFailedDelegate(NetworkRunner runner, ShutdownReason reason);
    public static event OnJoinSessionFailedDelegate OnJoinSessionFailed;

    public NetworkRunner runner;

    #region NetworkRunner Callbacks
    void INetworkRunnerCallbacks.OnConnectedToServer(NetworkRunner runner) {
        if (runner.LobbyInfo.IsValid) {
            //connected to a lobby
            Debug.Log($"[Network] Successfully connected to a Lobby");
        } else if (runner.SessionInfo.IsValid) {
            //connected to a session
            Debug.Log($"[Network] Successfully connected to a Room");
        }

        OnConnectedToServer?.Invoke(runner);
    }

    void INetworkRunnerCallbacks.OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) {
        Debug.LogError($"[Network] Failed to connect to the server ({remoteAddress}): {reason}");

        OnConnectFailed?.Invoke(runner, remoteAddress, reason);
    }

    void INetworkRunnerCallbacks.OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) {
        Debug.Log($"[Network] Incoming connection request from {request.RemoteAddress} ({token})");

        SessionInfo info = runner.SessionInfo;
        if (info.PlayerCount >= SessionData.Instance.MaxPlayers) {
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
        Debug.Log("[Network] Authentication Successful");

        if (data.ContainsKey("Token")) {
            PlayerPrefs.SetString("token", (string) data["Token"]);
            PlayerPrefs.Save();
        }

        OnCustomAuthenticationResponse?.Invoke(runner, data);
    }

    void INetworkRunnerCallbacks.OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) {
        OnHostMigration?.Invoke(runner, hostMigrationToken);
    }

    void INetworkRunnerCallbacks.OnInput(NetworkRunner runner, NetworkInput input) {
        OnInput?.Invoke(runner, input);
    }

    void INetworkRunnerCallbacks.OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) {
        OnInputMissing?.Invoke(runner, player, input);
    }

    void INetworkRunnerCallbacks.OnPlayerJoined(NetworkRunner runner, PlayerRef player) {
        Debug.Log($"[Network] Player joined room (UserId = {runner.GetPlayerUserId(player)})");

        if (runner.IsServer) {
            //create player data
            runner.Spawn(PrefabList.Instance.PlayerDataHolder, inputAuthority: player);

            if (player == Runner.LocalPlayer) {
                //create room data
                NetworkObject session = runner.Spawn(PrefabList.Instance.SessionDataHolder);
                SessionData.Instance = session.GetComponent<SessionData>();
            }
        }

        if (runner.LocalPlayer != player)
            GlobalController.Instance.DiscordController.UpdateActivity();

        OnPlayerJoined?.Invoke(runner, player);
    }

    void INetworkRunnerCallbacks.OnPlayerLeft(NetworkRunner runner, PlayerRef player) {
        PlayerData data = player.GetPlayerData(runner);
        Debug.Log($"[Network] {data.GetNickname()} ({player.GetPlayerData(runner).GetUserId()}) left the room");

        OnPlayerLeft(runner, player);

        GlobalController.Instance.DiscordController.UpdateActivity();
        runner.Despawn(data.Object);
        runner.SetPlayerObject(player, null);
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

        OnShutdown?.Invoke(runner, shutdownReason);
        RecreateInstance();
    }

    void INetworkRunnerCallbacks.OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) {
        OnUserSimulationMessage?.Invoke(runner, message);
    }
    #endregion

    #region Unity Callbacks
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    public static void RecreateInstance() {
        if (!prefab)
            prefab = (GameObject) Resources.Load("Prefabs/Static/NetworkingHandler");

        if (Instance)
            Destroy(Instance.gameObject);

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
        string id = PlayerPrefs.GetString("id", null);
        string token = PlayerPrefs.GetString("token", null);

        return await AuthenticationHandler.Authenticate(id, token);
    }

    public static async Task<StartGameResult> ConnectToRegion(string region = "") {
        //exit if we're already in a room
        if ((Runner.SessionInfo.IsValid || Runner.LobbyInfo.IsValid) && !Runner.IsShutdown)
            await Runner.Shutdown();

        if (string.IsNullOrEmpty(region)) {
            Debug.Log("[Network] Connecting to lobby with best ping.");
        } else {
            Debug.Log($"[Network] Connecting to lobby {region}");
        }

        //version separation
        AppSettings appSettings = new() {
            AppIdFusion = PhotonAppSettings.Instance.AppSettings.AppIdFusion,
            AppVersion = Regex.Match(Application.version, "^\\w*\\.\\w*\\.\\w*").Groups[0].Value,
            EnableLobbyStatistics = true,
            UseNameServer = true,
            FixedRegion = region,
        };

        //Authenticate
        AuthenticationValues authValues = await Authenticate();

        //And join lobby
        StartGameResult result = await Runner.JoinSessionLobby(SessionLobby.ClientServer, authentication: authValues, customAppSettings: appSettings);
        if (result.Ok) {
            CurrentRegion = Runner.LobbyInfo.Region;

            Debug.Log($"[Network] Connected to lobby in {CurrentRegion} region");

            //save id for later authentication
            PlayerPrefs.SetString("id", Runner.AuthenticationValues.UserId);
            PlayerPrefs.Save();

            OnLobbyConnect?.Invoke(Runner, Runner.LobbyInfo);
        } else {
            OnShutdown?.Invoke(Runner, result.ShutdownReason);
        }

        return result;
    }

    public static async Task<StartGameResult> CreateRoom(StartGameArgs args, GameMode gamemode = GameMode.Host, int players = 10) {
        //create a random room id.
        StringBuilder idBuilder = new();

        //first char should correspond to region.
        int index = Array.IndexOf(Regions, CurrentRegion);
        idBuilder.Append(RoomIdValidChars[index >= 0 ? index : 0]);

        //fill rest of the string with random chars
        for (int i = 1; i < RoomIdLength; i++)
            idBuilder.Append(RoomIdValidChars[UnityEngine.Random.Range(0, RoomIdValidChars.Length)]);

        args.GameMode = gamemode;
        args.SessionName = idBuilder.ToString();
        args.ConnectionToken = Encoding.UTF8.GetBytes(Settings.Instance.nickname);
        args.SessionProperties = NetworkUtils.DefaultRoomProperties;

        args.SessionProperties[Enums.NetRoomProperties.HostName] = Settings.Instance.nickname;
        args.SessionProperties[Enums.NetRoomProperties.MaxPlayers] = players;

        //attempt to create the room
        return await Runner.StartGame(args);
    }

    public static async Task<StartGameResult> JoinRoom(string roomId) {
        //make sure that we're on the right region...
        string originalRegion = CurrentRegion;
        string targetRegion = Regions[RoomIdValidChars.IndexOf(roomId[0])];

        if (CurrentRegion != targetRegion) {
            //change regions
            await ConnectToRegion(targetRegion);
        }

        Debug.Log($"[Network] Attempting to join game with ID: {roomId}");
        //attempt to join the room
        StartGameResult result = await Runner.StartGame(new() {
            GameMode = GameMode.Client,
            SessionName = roomId,
            ConnectionToken = Encoding.UTF8.GetBytes(Settings.Instance.nickname),
            DisableClientSessionCreation = true,
        });
        Debug.Log($"[Network] Failed to join game: {result.ShutdownReason}");
        if (!result.Ok) {
            OnJoinSessionFailed?.Invoke(Runner, result.ShutdownReason);
            //automatically go back to the lobby.
            await ConnectToRegion(originalRegion);
        }

        return result;
    }
    #endregion
}
