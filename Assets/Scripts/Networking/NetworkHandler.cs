using static NSMB.Utils.NetworkUtils;
using NSMB.Utils;
using Photon.Deterministic;
using Photon.Realtime;
using Quantum;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

public class NetworkHandler : Singleton<NetworkHandler>, IMatchmakingCallbacks, IConnectionCallbacks {

    //---Events
    public static event Action<ClientState, ClientState> StateChanged;

    //---Constants
    public static readonly string RoomIdValidChars = "BCDFGHJKLMNPRQSTVWXYZ";
    private static readonly int RoomIdLength = 8;

    //---Static
    public static RealtimeClient Client => Instance ? Instance.realtimeClient : null;
    public static long? Ping => Client?.RealtimePeer.Stats.RoundtripTime;
    public static QuantumRunner Runner { get; private set; }
    public static List<Region> Regions => Client.RegionHandler.EnabledRegions;
    public static string Region => Client?.CurrentRegion ?? Instance.lastRegion;
    public static bool IsReplay { get; private set; }
    public static int ReplayLength { get; private set; }
    public static int ReplayStart { get; private set; }
    public static List<byte[]> ReplayFrameCache => Instance.replayFrameCache;

    //---Private
    private RealtimeClient realtimeClient;
    private string lastRegion;
    private Coroutine pingUpdateCoroutine;
    private List<byte[]> replayFrameCache;

    public void Awake() {
        StateChanged += OnClientStateChanged;

        realtimeClient = new();
        realtimeClient.StateChanged += (ClientState oldState, ClientState newState) => {
            StateChanged?.Invoke(oldState, newState);
        };
        realtimeClient.AddCallbackTarget(this);

        QuantumCallback.Subscribe<CallbackSimulateFinished>(this, OnSimulateFinished);
        QuantumEvent.Subscribe<EventGameStarted>(this, OnGameStarted);
        QuantumEvent.Subscribe<EventGameEnded>(this, OnGameEnded);
    }

    public void Update() {
        if (Client.IsConnectedAndReady) {
            Client.Service();
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
                or ClientState.ConnectedToMasterServer // We always join a lobby, so...
                or ClientState.Joining
                or ClientState.JoiningLobby
                or ClientState.Leaving
                or ClientState.ConnectedToNameServer // Include this since we can't do anything and will auto-disconnect anyway
                || Client.State == ClientState.Joined && !Runner
        );
    }

    public IEnumerator PingUpdateCoroutine() {
        WaitForSeconds seconds = new(1);
        while (true) {
            QuantumGame game;
            if (Runner && (game = Runner.Game) != null) {
                foreach (int slot in game.GetLocalPlayerSlots()) {
                    Runner.Game.SendCommand(slot, new CommandUpdatePing {
                        PingMs = (int) Ping.Value,
                    });
                }
            }
            yield return seconds;
        }
    }

    public static async Task<bool> ConnectToRegion(string region) {
        region ??= Instance.lastRegion;
        Instance.lastRegion = region;
        Client.AuthValues = await AuthenticationHandler.Authenticate();

        if (Client.AuthValues == null) {
            StateChanged?.Invoke(ClientState.ConnectingToMasterServer, ClientState.Disconnected);
            return false;
        }

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
            await Client.JoinLobbyAsync(TypedLobby.Default);
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

        args.RoomName = idBuilder.ToString();
        args.Lobby = TypedLobby.Default;
        args.RoomOptions.PublishUserId = true;
        args.RoomOptions.CustomRoomProperties = DefaultRoomProperties;
        args.RoomOptions.CustomRoomProperties[Enums.NetRoomProperties.HostName] = Settings.Instance.generalNickname;
        args.RoomOptions.CustomRoomPropertiesForLobby = new object[] {
            Enums.NetRoomProperties.HostName,
            Enums.NetRoomProperties.IntProperties,
            Enums.NetRoomProperties.BoolProperties
        };

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
        if (pingUpdateCoroutine != null) {
            StopCoroutine(pingUpdateCoroutine);
        }
        pingUpdateCoroutine = StartCoroutine(PingUpdateCoroutine());
    }

    public void OnCreateRoomFailed(short returnCode, string message) { }

    public async void OnJoinedRoom() {
        if (pingUpdateCoroutine != null) {
            StopCoroutine(pingUpdateCoroutine);
        }
        pingUpdateCoroutine = StartCoroutine(PingUpdateCoroutine());

        var sessionRunnerArguments = new SessionRunner.Arguments {
            RunnerFactory = QuantumRunnerUnityFactory.DefaultFactory,
            GameParameters = QuantumRunnerUnityFactory.CreateGameParameters,
            ClientId = Client.UserId,
            RuntimeConfig = new RuntimeConfig {
                SimulationConfig = GlobalController.Instance.config,
                Map = null,
                Seed = unchecked((int) DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()),
                IsRealGame = true,
            },
            SessionConfig = QuantumDeterministicSessionConfigAsset.DefaultConfig,
            GameMode = DeterministicGameMode.Multiplayer,
            PlayerCount = 10,
            Communicator = new QuantumNetworkCommunicator(Client),
        };

        IsReplay = false;
        Runner = (QuantumRunner) await SessionRunner.StartAsync(sessionRunnerArguments);
        Runner.Game.AddPlayer(new RuntimePlayer {
            PlayerNickname = Settings.Instance.generalNickname,
            UserId = Guid.Parse(Client.UserId),
        });

        GlobalController.Instance.connecting.SetActive(false);
    }

    public void OnJoinRoomFailed(short returnCode, string message) { }

    public void OnJoinRandomFailed(short returnCode, string message) { }

    public void OnLeftRoom() {
        if (pingUpdateCoroutine != null) {
            StopCoroutine(pingUpdateCoroutine);
            pingUpdateCoroutine = null;
        }
    }

    private void OnGameEnded(EventGameEnded e) {
#if UNITY_STANDALONE
        if (Runner.Session.IsReplay) {
            return;
        }

        QuantumGame game = e.Game;
        QuantumReplayFile replay = game.GetRecordedReplay();
        replay.InitialTick = initialFrame;
        replay.InitialFrameData = initialFrameData;
        initialFrame = 0;
        initialFrameData = null;

        string replayFolder = Path.Combine(Application.streamingAssetsPath, "replays");
        Directory.CreateDirectory(replayFolder);

        string json = JsonUtility.ToJson(replay);
        string finalFilePath = Path.Combine(replayFolder, "Replay-" + DateTimeOffset.Now.ToUnixTimeSeconds() + ".json");
        File.WriteAllText(finalFilePath, json);

        FileInfo writtenFile = new FileInfo(finalFilePath);
        Debug.Log($"[Replay] Saved new replay '{finalFilePath}' ({Utils.BytesToString(writtenFile.Length)})");
#endif
    }

    int initialFrame;
    byte[] initialFrameData;
    private void OnGameStarted(EventGameStarted e) {
        QuantumGame game = e.Game;

        game.StartRecordingInput(e.Frame.Number);
        initialFrameData = e.Frame.Serialize(DeterministicFrameSerializeMode.Serialize);
        initialFrame = e.Frame.Number;
        Debug.Log("[Replay] Started recording a new replay.");
    }

    public async static void StartReplay(QuantumReplayFile replay) {

        var serializer = new QuantumUnityJsonSerializer();
        var runtimeConfig = serializer.ConfigFromByteArray<RuntimeConfig>(replay.RuntimeConfigData.Decode(), compressed: true);
        var _replayInputProvider = replay.CreateInputProvider();

        var arguments = new SessionRunner.Arguments {
            RunnerFactory = QuantumRunnerUnityFactory.DefaultFactory,
            GameParameters = QuantumRunnerUnityFactory.CreateGameParameters,
            RuntimeConfig = runtimeConfig,
            SessionConfig = replay.DeterministicConfig,
            ReplayProvider = _replayInputProvider,
            GameMode = DeterministicGameMode.Replay,
            RunnerId = "LOCALREPLAY",
            PlayerCount = replay.DeterministicConfig.PlayerCount,
            //InstantReplaySettings = InstantReplayConfig,
            InitialTick = replay.InitialTick,
            FrameData = replay.InitialFrameData,
            DeltaTimeType = SimulationUpdateTime.EngineDeltaTime
        };

        GlobalController.Instance.loadingCanvas.Initialize(null);
        IsReplay = true;
        ReplayLength = replay.LastTick - replay.InitialTick;
        ReplayStart = replay.InitialTick;
        Instance.replayFrameCache = new() {
            replay.InitialFrameData
        };
        Runner = (QuantumRunner) await SessionRunner.StartAsync(arguments);
    }

    private void OnSimulateFinished(CallbackSimulateFinished e) {
        if (!IsReplay) {
            return;
        }

        if ((e.Frame.Number - ReplayStart) % (5 * QuantumRunner.Default.Session.SimulationRate) == 0) {
            // Save this frame to the replay cache
            Debug.Log("serialized frame #" + e.Frame.Number);
            byte[] serializedFrame = e.Frame.Serialize(DeterministicFrameSerializeMode.Serialize);
            byte[] copy = new byte[serializedFrame.Length];
            Array.Copy(serializedFrame, copy, serializedFrame.Length);
            replayFrameCache.Add(copy);
        }
    }

    public void OnConnected() { }

    public void OnConnectedToMaster() { }

    public void OnDisconnected(DisconnectCause cause) { }

    public void OnRegionListReceived(RegionHandler regionHandler) { }

    public void OnCustomAuthenticationResponse(Dictionary<string, object> data) {

        PlayerPrefs.SetString("id", Client.AuthValues.UserId);

        if (data.TryGetValue("Token", out object token) && token is string tokenString) {
            PlayerPrefs.SetString("token", tokenString);
        }

        PlayerPrefs.Save();

        /*
        if (data.TryGetValue("SignedData", out object value)) {
            SignedResultData signedData = JsonConvert.DeserializeObject<SignedResultData>((string) value);
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
        */
    }

    public void OnCustomAuthenticationFailed(string debugMessage) { }
}
