using Photon.Client;
using Photon.Realtime;
using System.Text;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using Photon.Deterministic;
using Quantum;

public class NetworkHandler : Singleton<NetworkHandler>, IMatchmakingCallbacks, IOnEventCallback {

    //---Constants
    public static readonly string RoomIdValidChars = "BCDFGHJKLMNPRQSTVWXYZ";
    private static readonly int RoomIdLength = 8;

    //---Static
    public static RealtimeClient Client => Instance ? Instance.realtimeClient : null;
    public static QuantumRunner Runner { get; private set; }
    public static List<Region> Regions => Client.RegionHandler.EnabledRegions;
    public static string Region => Client?.CurrentRegion ?? Instance.lastRegion;

    //---Private
    private RealtimeClient realtimeClient;
    private string lastRegion;
    private Coroutine pingUpdateCoroutine;

    public void Awake() {
        realtimeClient = new();
        realtimeClient.StateChanged += OnClientStateChanged;
        realtimeClient.AddCallbackTarget(this);
    }

    public void Update() {
        if (Client.IsConnectedAndReady) {
            Client.SendOutgoingCommands();
        }
    }

    public void OnDestroy() {
        realtimeClient.RemoveCallbackTarget(this);
    }

    public void OnClientStateChanged(ClientState oldState, ClientState newState) {
        // Jesus christ
        GlobalController.Instance.connecting.SetActive(
            newState is ClientState.Authenticating
                or ClientState.ConnectWithFallbackProtocol
                or ClientState.ConnectingToNameServer
                or ClientState.ConnectingToMasterServer
                or ClientState.ConnectingToGameServer
                or ClientState.Disconnecting
                or ClientState.DisconnectingFromNameServer
                or ClientState.DisconnectingFromMasterServer
                or ClientState.DisconnectingFromGameServer
                or ClientState.Joining
                or ClientState.JoiningLobby
                or ClientState.Leaving
                or ClientState.ConnectedToNameServer // Include this since we can't do anything and will auto-disconnect anyway
        );
    }

    public IEnumerator PingUpdateCoroutine() {
        WaitForSeconds seconds = new(1);
        while (true) {
            /*
            realtimeClient.LocalPlayer.SetCustomProperties(new PhotonHashtable() {
                [Enums.NetPlayerProperties.Ping] = realtimeClient.RealtimePeer.Stats.RoundtripTime
            });
            */
            yield return seconds;
        }
    }

    public static async Task<bool> ConnectToRegion(string region) {
        //region ??= Instance.lastRegion;
        Instance.lastRegion = region;
        Client.AuthValues = await AuthenticationHandler.Authenticate();

        if (Client.IsConnected) {
            await Client.DisconnectAsync();
        }

        if (region == null) {
            Debug.Log("[Network] Connecting to the best available region");
        } else {
            Debug.Log($"[Network] Connecting to region {region}");
        }

        try {
            await Client.ConnectUsingSettingsAsync(new AppSettings {
                AppIdQuantum = "6b4b72d0-57c3-4991-96c1-f3f36f9548e5",
                EnableLobbyStatistics = true,
                AuthMode = AuthModeOption.Auth,
                FixedRegion = region,
            });
            Instance.lastRegion = Client.CurrentRegion;
            return true;
        } catch {
            return false;
        }
    }

    public static async Task ConnectToRoomsRegion(string roomId) {
        int regionIndex = RoomIdValidChars.IndexOf(roomId[0]);
        string targetRegion = Regions[regionIndex].Code;

        if (Client.CurrentRegion != targetRegion) {
            // await Client.DisconnectAsync();
            await ConnectToRegion(targetRegion);
        }
    }

    public static async Task<short> CreateRoom(EnterRoomArgs args) {
        // Create a random room id.
        StringBuilder idBuilder = new();

        // First char should correspond to region.
        int index = Regions.Select(r => r.Code).ToList().IndexOf(Region); // Dirty linq hack
        idBuilder.Append(RoomIdValidChars[index >= 0 ? index : 0]);

        // Fill rest of the string with random chars
        UnityEngine.Random.InitState(unchecked((int) DateTime.UtcNow.Subtract(DateTime.UnixEpoch).TotalSeconds));
        for (int i = 1; i < RoomIdLength; i++) {
            idBuilder.Append(RoomIdValidChars[UnityEngine.Random.Range(0, RoomIdValidChars.Length)]);
        }

        Debug.Log($"[Network] Creating a game in {Region} with the ID {idBuilder}");
        return await Client.CreateAndJoinRoomAsync(args, false);
    }

    public static async Task<short> JoinRoom(EnterRoomArgs args) {
        // Change to region if we need to
        await ConnectToRoomsRegion(args.RoomName);

        Debug.Log($"[Network] Attempting to join a game with the ID {args.RoomName}");
        return await Client.JoinRoomAsync(args, false);
    }

    public void OnFriendListUpdate(List<FriendInfo> friendList) { }

    public void OnCreatedRoom() {
        pingUpdateCoroutine = StartCoroutine(PingUpdateCoroutine());
    }

    public void OnCreateRoomFailed(short returnCode, string message) { }

    public void OnJoinedRoom() {
        pingUpdateCoroutine = StartCoroutine(PingUpdateCoroutine());
    }

    public void OnJoinRoomFailed(short returnCode, string message) { }

    public void OnJoinRandomFailed(short returnCode, string message) { }

    public void OnLeftRoom() {
        if (pingUpdateCoroutine != null) {
            StopCoroutine(pingUpdateCoroutine);
            pingUpdateCoroutine = null;
        }
    }

    public async void OnEvent(EventData photonEvent) {
        if (photonEvent.Code == (byte) Enums.NetEvents.StartGame) {
            Debug.Log("Start game!");

            var sessionRunnerArguments = new SessionRunner.Arguments {
                RunnerFactory = QuantumRunnerUnityFactory.DefaultFactory,
                GameParameters = QuantumRunnerUnityFactory.CreateGameParameters,
                ClientId = Client.UserId,
                RuntimeConfig = null,
                SessionConfig = QuantumDeterministicSessionConfigAsset.DefaultConfig,
                GameMode = DeterministicGameMode.Multiplayer,
                PlayerCount = 1,
                StartGameTimeoutInSeconds = 10,
                Communicator = new QuantumNetworkCommunicator(Client),
            };

            Runner = (QuantumRunner) await SessionRunner.StartAsync(sessionRunnerArguments);
        }
    }
}
