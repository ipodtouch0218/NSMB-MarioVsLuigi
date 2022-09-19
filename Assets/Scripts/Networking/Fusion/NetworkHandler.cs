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

    public NetworkRunner runner;
    private string currentRegion;

    #region NetworkRunner Callbacks
    public void OnConnectedToServer(NetworkRunner runner) {

        if (runner.LobbyInfo.IsValid) {
            //connected to a lobby
            Debug.Log($"[Network] Successfully connected to a Lobby");
        } else if (runner.SessionInfo.IsValid) {
            //connected to a session
            Debug.Log($"[Network] Successfully connected to a Room");
        }

    }

    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) {
        Debug.LogError($"[Network] Failed to connect to the server ({remoteAddress}): {reason}");
    }

    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) {
        Debug.Log($"[Network] Incoming connection request from {request.RemoteAddress} ({token})");
        //TODO: check for bans?
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
        Debug.Log($"[Network] Player joined room (UserId = {runner.GetPlayerUserId(player)})");

        if (runner.IsServer) {
            //create player data
            runner.Spawn(PrefabList.Net_PlayerData, inputAuthority: player);
        }

        if (MainMenuManager.Instance)
            MainMenuManager.Instance.OnPlayerJoined(runner, player);

        GlobalController.Instance.DiscordController.UpdateActivity();
    }

    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player) {
        PlayerData data = player.GetPlayerData(runner);
        Debug.Log($"[Network] {data.GetNickname()} ({player.GetPlayerData(runner).GetUserId()}) left the room");
        if (MainMenuManager.Instance)
            MainMenuManager.Instance.OnPlayerLeft(runner, player);

        GlobalController.Instance.DiscordController.UpdateActivity();
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
        runner.ProvideInput = true; //TODO: move to
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