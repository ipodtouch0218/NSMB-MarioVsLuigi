using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

using NSMB.Utils;
using NSMB.Extensions;
using Fusion;
using Fusion.Sockets;
using Fusion.Photon.Realtime;

#pragma warning disable UNT0006 // "Incorrect message signature" for OnConnectedToServer
public class NetworkHandler : Singleton<NetworkHandler>, INetworkRunnerCallbacks {

    public static readonly string[] Regions = { "asia", "eu", "jp", "kr", "sa", "us" };
    private static readonly string RoomIdValidChars = "BCDFGHJKLMNPRQSTVWXYZ";
    private static readonly int RoomIdLength = 8;

    //---Exposed callbacks for Events
    public delegate void OnConnectedToServerDelegate(NetworkRunner runner);
    public event OnConnectedToServerDelegate OnConnectedToServer;

    public delegate void OnConnectFailedDelegate(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason);
    public event OnConnectFailedDelegate OnConnectFailed;

    public delegate bool OnConnectRequestDelegate(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token);
    public event OnConnectRequestDelegate OnConnectRequest;

    public delegate void OnDisconnectedFromServerDelegate(NetworkRunner runner);
    public event OnDisconnectedFromServerDelegate OnDisconnectedFromServer;

    public delegate void OnCustomAuthenticationResponseDelegate(NetworkRunner runner, Dictionary<string, object> data);
    public event OnCustomAuthenticationResponseDelegate OnCustomAuthenticationResponse;

    public delegate void OnHostMigrationDelegate(NetworkRunner runner, HostMigrationToken hostMigrationToken);
    public event OnHostMigrationDelegate OnHostMigration;

    public delegate void OnInputDelegate(NetworkRunner runner, NetworkInput input);
    public event OnInputDelegate OnInput;

    public delegate void OnInputMissingDelegate(NetworkRunner runner, PlayerRef player, NetworkInput input);
    public event OnInputMissingDelegate OnInputMissing;

    public delegate void OnPlayerJoinedDelegate(NetworkRunner runner, PlayerRef player);
    public event OnPlayerJoinedDelegate OnPlayerJoined;

    public delegate void OnPlayerLeftDelegate(NetworkRunner runner, PlayerRef player);
    public event OnPlayerLeftDelegate OnPlayerLeft;

    public delegate void OnReliableDataReceivedDelegate(NetworkRunner runner, PlayerRef player, ArraySegment<byte> data);
    public event OnReliableDataReceivedDelegate OnReliableDataReceived;

    public delegate void OnSceneLoadDoneDelegate(NetworkRunner runner);
    public event OnSceneLoadDoneDelegate OnSceneLoadDone;

    public delegate void OnSceneLoadStartDelegate(NetworkRunner runner);
    public event OnSceneLoadStartDelegate OnSceneLoadStart;

    public delegate void OnSessionListUpdatedDelegate(NetworkRunner runner, List<SessionInfo> sessionList);
    public event OnSessionListUpdatedDelegate OnSessionListUpdated;

    public delegate void OnShutdownDelegate(NetworkRunner runner, ShutdownReason shutdownReason);
    public event OnShutdownDelegate OnShutdown;

    public delegate void OnUserSimulationMessageDelegate(NetworkRunner runner, SimulationMessagePtr message);
    public event OnUserSimulationMessageDelegate OnUserSimulationMessage;

    public NetworkRunner runner;
    private string currentRegion;

    #region NetworkRunner Callbacks
    void INetworkRunnerCallbacks.OnConnectedToServer(NetworkRunner runner) {
        if (runner.LobbyInfo.IsValid) {
            //connected to a lobby
            Debug.Log($"[Network] Successfully connected to a Lobby");
        } else if (runner.SessionInfo.IsValid) {
            //connected to a session
            Debug.Log($"[Network] Successfully connected to a Room");
        }

        OnConnectedToServer(runner);
    }

    void INetworkRunnerCallbacks.OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) {
        Debug.LogError($"[Network] Failed to connect to the server ({remoteAddress}): {reason}");

        OnConnectFailed(runner, remoteAddress, reason);
    }

    void INetworkRunnerCallbacks.OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) {
        Debug.Log($"[Network] Incoming connection request from {request.RemoteAddress} ({token})");
        //TODO: check for bans?
        request.Accept();

        bool accept = OnConnectRequest(runner, request, token);
        if (accept)
            request.Accept();
        else
            request.Refuse();
    }
    void INetworkRunnerCallbacks.OnDisconnectedFromServer(NetworkRunner runner) {
        Debug.Log("[Network] Disconnected from Lobby");

        OnDisconnectedFromServer(runner);
    }

    void INetworkRunnerCallbacks.OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) {
        Debug.Log("[Network] Authentication Successful");

        PlayerPrefs.SetString("id", runner.AuthenticationValues.UserId);
        if (data.ContainsKey("Token"))
            PlayerPrefs.SetString("token", (string) data["Token"]);

        PlayerPrefs.Save();

        OnCustomAuthenticationResponse(runner, data);
    }

    void INetworkRunnerCallbacks.OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) {
        OnHostMigration(runner, hostMigrationToken);
    }

    void INetworkRunnerCallbacks.OnInput(NetworkRunner runner, NetworkInput input) {
        OnInput(runner, input);
    }

    void INetworkRunnerCallbacks.OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) {
        OnInputMissing(runner, player, input);
    }

    void INetworkRunnerCallbacks.OnPlayerJoined(NetworkRunner runner, PlayerRef player) {
        Debug.Log($"[Network] Player joined room (UserId = {runner.GetPlayerUserId(player)})");

        if (runner.IsServer) {
            //create player data
            runner.Spawn(PrefabList.PlayerDataHolder, inputAuthority: player);
        }

        GlobalController.Instance.DiscordController.UpdateActivity();

        OnPlayerJoined(runner, player);
    }

    void INetworkRunnerCallbacks.OnPlayerLeft(NetworkRunner runner, PlayerRef player) {
        PlayerData data = player.GetPlayerData(runner);
        Debug.Log($"[Network] {data.GetNickname()} ({player.GetPlayerData(runner).GetUserId()}) left the room");

        GlobalController.Instance.DiscordController.UpdateActivity();

        OnPlayerLeft(runner, player);
    }

    void INetworkRunnerCallbacks.OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ArraySegment<byte> data) {
        OnReliableDataReceived(runner, player, data);
    }

    void INetworkRunnerCallbacks.OnSceneLoadDone(NetworkRunner runner) {
        OnSceneLoadDone(runner);
    }

    void INetworkRunnerCallbacks.OnSceneLoadStart(NetworkRunner runner) {
        OnSceneLoadStart(runner);
    }

    void INetworkRunnerCallbacks.OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) {
        OnSessionListUpdated(runner, sessionList);
    }

    void INetworkRunnerCallbacks.OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason) {
        Debug.Log($"[Network] Network Shutdown: {shutdownReason}");

        OnShutdown(runner, shutdownReason);
    }

    void INetworkRunnerCallbacks.OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) {
        OnUserSimulationMessage(runner, message);
    }
    #endregion

    #region Unity Callbacks
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    public static void CreateInstance() {
        Instantiate(Resources.Load("Prefabs/Static/NetworkingHandler"));
    }

    public void Awake() {
        if (!InstanceCheck())
            return;

        Instance = this;
    }

    public void Start() {
        runner = GetComponent<NetworkRunner>();
        runner.ProvideInput = true; //TODO: move to
        runner.AddCallbacks(this);
    }
    #endregion

    #region Room-Related Methods
    private async Task<AuthenticationValues> Authenticate() {
        string id = PlayerPrefs.GetString("id", null);
        string token = PlayerPrefs.GetString("token", null);

        return await AuthenticationHandler.Authenticate(id, token);
    }

    public async Task<StartGameResult> ConnectToRegion(string region) {

        //version separation
        PhotonAppSettings.Instance.AppSettings.AppVersion = Regex.Match(Application.version, "^\\w*\\.\\w*\\.\\w*").Groups[0].Value;
        PhotonAppSettings.Instance.AppSettings.EnableLobbyStatistics = true;
        PhotonAppSettings.Instance.AppSettings.FixedRegion = region;

        //Authenticate
        AuthenticationValues authValues = await Authenticate();
        //And join lobby
        return await runner.JoinSessionLobby(SessionLobby.ClientServer, authentication: authValues);
    }

    public async Task<StartGameResult> CreateRoom(StartGameArgs args) {
        //create a random room id.
        StringBuilder idBuilder = new();

        //first char should correspond to region.
        int index = Array.IndexOf(Regions, currentRegion);
        idBuilder.Append(RoomIdValidChars[index]);

        //fill rest of the string with random chars
        for (int i = 1; i < RoomIdLength; i++)
            idBuilder.Append(RoomIdValidChars[UnityEngine.Random.Range(0, RoomIdValidChars.Length)]);

        args.GameMode = GameMode.Host;
        args.SessionName = idBuilder.ToString();
        args.SessionProperties = NetworkUtils.DefaultRoomProperties;

        //attempt to create the room
        return await runner.StartGame(args);
    }

    public async Task<StartGameResult> JoinRoom(string roomId) {
        //make sure that we're on the right region...
        string targetRegion = Regions[RoomIdValidChars.IndexOf(roomId[0])];

        if (currentRegion != targetRegion) {
            //change regions
            await ConnectToRegion(targetRegion);
        }

        //attempt to join the room
        return await runner.StartGame(new() {
            GameMode = GameMode.Client,
            SessionName = roomId,
        });
    }
    #endregion
}