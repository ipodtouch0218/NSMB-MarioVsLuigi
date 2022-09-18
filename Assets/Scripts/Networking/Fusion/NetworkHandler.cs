using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

using NSMB.Utils;
using Fusion;
using Fusion.Sockets;
using Fusion.Photon.Realtime;
using UnityEngine.SceneManagement;

#pragma warning disable UNT0006 // "Incorrect message signature" for OnConnectedToServer
public class NetworkHandler : Singleton<NetworkHandler>, INetworkRunnerCallbacks {

    private static readonly string RoomIdValidChars = "BCDFGHJKLMNPRQSTVWXYZ";
    private static readonly int RoomIdLength = 8;

    public NetworkRunner runner;
    private AuthenticationValues authValues;

    #region NetworkRunner Callbacks
    public void OnConnectedToServer(NetworkRunner runner) {
        Debug.Log($"[Network] Successfully connected to the Lobby");
    }

    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) {
        Debug.LogError($"[Network] Failed to connect to the Lobby ({remoteAddress}): {reason}");
    }

    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) {
        Debug.Log($"[Network] Incoming connection request from {request.RemoteAddress} ({token})");
        request.Accept();
    }
    public void OnDisconnectedFromServer(NetworkRunner runner) {
        Debug.Log("[Network] Disconnected from Lobby");
    }

    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) {
        Debug.Log("[Network] Authentication Successful");

        PlayerPrefs.SetString("id", runner.AuthenticationValues.UserId);
        if (data.ContainsKey("Token"))
            PlayerPrefs.SetString("token", (string) data["Token"]);

        PlayerPrefs.Save();
    }

    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }

    public void OnInput(NetworkRunner runner, NetworkInput input) {
        PlayerNetworkInput newInput = new();

        Vector2 joystick = InputSystem.controls.Player.Movement.ReadValue<Vector2>();
        bool jump = InputSystem.controls.Player.Jump.ReadValue<bool>();
        bool sprint = InputSystem.controls.Player.Sprint.ReadValue<bool>();

        //TODO: deadzone?
        newInput.buttons.Set(PlayerControls.Right, joystick.x > 0.25f);
        newInput.buttons.Set(PlayerControls.Left, joystick.x < -0.25f);
        newInput.buttons.Set(PlayerControls.Up, joystick.y > 0.25f);
        newInput.buttons.Set(PlayerControls.Down, joystick.y < -0.25f);
        newInput.buttons.Set(PlayerControls.Jump, jump);
        newInput.buttons.Set(PlayerControls.Sprint, sprint);

        input.Set(newInput);
    }

    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }

    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player) {
        Debug.Log($"[Network] Player joined room: {player.PlayerId}");

        if (runner.IsServer) {
            //create player data
            runner.Spawn(PrefabList.Net_PlayerData, inputAuthority: player);
        }

        if (MainMenuManager.Instance)
            MainMenuManager.Instance.OnPlayerJoined(runner, player);
    }

    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player) {
        Debug.Log($"[Network] Player left room: {player.PlayerId}");
        if (MainMenuManager.Instance)
            MainMenuManager.Instance.OnPlayerLeft(runner, player);
    }

    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ArraySegment<byte> data) { }

    public void OnSceneLoadDone(NetworkRunner runner) { }

    public void OnSceneLoadStart(NetworkRunner runner) { }

    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) {
        if (MainMenuManager.Instance)
            MainMenuManager.Instance.OnRoomListUpdate(sessionList);
    }

    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason) {
        Debug.Log($"[Network] Network Shutdown: {shutdownReason}");

        //back to the main menu
        GlobalController.Instance.disconnectCause = shutdownReason;
        SceneManager.LoadScene(0);
    }

    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
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
    public void Authenticate() {
        string id = PlayerPrefs.GetString("id", null);
        string token = PlayerPrefs.GetString("token", null);

        AuthenticationHandler.Authenticate(id, token, (auth) => {
            runner.JoinSessionLobby(SessionLobby.ClientServer, null, auth);
            authValues = auth;
        });
    }

    public async Task<StartGameResult> CreateRoom(StartGameArgs args) {
        //create a random room id.
        //TODO: first char should correspond to region.

        //fill rest of the string with random chars
        StringBuilder idBuilder = new();
        for (int i = 0; i < RoomIdLength; i++)
            idBuilder.Append(RoomIdValidChars[UnityEngine.Random.Range(0, RoomIdValidChars.Length)]);

        args.GameMode = GameMode.Host;
        args.SessionName = idBuilder.ToString();
        args.AuthValues = authValues;
        args.SessionProperties = NetworkUtils.DefaultRoomProperties;
        args.Scene = 0;

        //attempt to create the room
        return await runner.StartGame(args);
    }

    public async Task<StartGameResult> JoinRoom(string roomId) {
        //make sure that we're on the right region...
        //TODO: change region based on first character

        //attempt to join the room
        return await runner.StartGame(new() {
            GameMode = GameMode.Client,
            SessionName = roomId,
            AuthValues = authValues,
        });
    }
    #endregion
}