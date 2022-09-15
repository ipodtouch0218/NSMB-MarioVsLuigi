using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

using NSMB.Utils;
using Fusion;
using Fusion.Sockets;
using Fusion.Photon.Realtime;

#pragma warning disable UNT0006 // "Incorrect message signature" for OnConnectedToServer
public class NetworkHandler : Singleton<NetworkHandler>, INetworkRunnerCallbacks, INetworkSceneManager {

    private static readonly string RoomIdValidChars = "BCDFGHJKLMNPRQSTVWXYZ";
    private static readonly int RoomIdLength = 8;

    private NetworkRunner runner;
    private AuthenticationValues authValues;

    #region NetworkRunner Callbacks
    public void OnConnectedToServer(NetworkRunner runner) { }

    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }

    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }

    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }

    public void OnDisconnectedFromServer(NetworkRunner runner) { }

    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }

    public void OnInput(NetworkRunner runner, NetworkInput input) {
        PlayerNetworkInput newInput = new();



        input.Set(newInput);
    }

    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }

    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player) { }

    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player) { }

    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ArraySegment<byte> data) { }

    public void OnSceneLoadDone(NetworkRunner runner) { }

    public void OnSceneLoadStart(NetworkRunner runner) { }

    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }

    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason) { }

    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
    #endregion

    #region NetworkSceneManager Callbacks
    public void Initialize(NetworkRunner runner) { }

    public void Shutdown(NetworkRunner runner) { }

    public bool IsReady(NetworkRunner runner) { return true; }
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
        runner.ProvideInput = true;
    }
    #endregion

    #region Room-Related Methods
    public void JoinLobby() {
        string id = PlayerPrefs.GetString("id", null);
        string token = PlayerPrefs.GetString("token", null);

        AuthenticationHandler.Authenticate(id, token, (auth) => {
            runner.JoinSessionLobby(SessionLobby.ClientServer, null, auth);
            authValues = auth;
        });
    }

    public async Task<StartGameResult> CreateRoom() {

        //create a random room id.
        //TODO: first char should correspond to region.

        //fill rest of the string with random chars
        StringBuilder idBuilder = new();
        for (int i = 0; i < RoomIdLength; i++)
            idBuilder.Append(RoomIdValidChars[UnityEngine.Random.Range(0, RoomIdValidChars.Length)]);

        //attempt to create the room
        return await runner.StartGame(new() {
            GameMode = GameMode.Host,
            SessionName = idBuilder.ToString(),
            SceneManager = this,
            AuthValues = authValues,
        });
    }

    public async Task<StartGameResult> JoinRoom(string roomId) {
        //make sure that we're on the right region...
        //TODO: change region based on first character

        //attempt to join the room
        return await runner.StartGame(new() {
            GameMode = GameMode.Client,
            SessionName = roomId,
            SceneManager = this,
            AuthValues = authValues,
            SessionProperties = NetworkUtils.DefaultRoomProperties,
        });
    }
    #endregion
}