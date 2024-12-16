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
using NSMB.UI.MainMenu;
using System.Text.RegularExpressions;

public class NetworkHandler : Singleton<NetworkHandler>, IMatchmakingCallbacks, IConnectionCallbacks {

    //---Events
    public static event Action<ClientState, ClientState> StateChanged;

    //---Constants
    public static readonly string RoomIdValidChars = "BCDFGHJKLMNPRQSTVWXYZ";
    private static readonly int RoomIdLength = 8;
    private static readonly List<DisconnectCause> NonErrorDisconnectCauses = new() {
        DisconnectCause.None, DisconnectCause.DisconnectByClientLogic
    };

    //---Static Variables
    public static RealtimeClient Client => Instance ? Instance.realtimeClient : null;
    public static long? Ping => Client?.RealtimePeer.Stats.RoundtripTime;
    public static QuantumRunner Runner { get; private set; }
    public static QuantumGame Game => Runner == null ? null : Runner.Game;
    public static IEnumerable<Region> Regions => Client.RegionHandler.EnabledRegions.OrderBy(r => r.Code);
    public static string Region => Client?.CurrentRegion ?? Instance.lastRegion;
    public static bool IsReplay { get; private set; }
    public static int ReplayStart { get; private set; }
    public static int ReplayLength { get; private set; }
    public static int ReplayEnd => ReplayStart + ReplayLength;
    public static bool IsReplayFastForwarding { get; set; }
    public static List<byte[]> ReplayFrameCache => Instance.replayFrameCache;
    public static bool WasDisconnectedViaError { get; set; }

    //---Private Variables
    private RealtimeClient realtimeClient;
    private string lastRegion;
    private Coroutine pingUpdateCoroutine;
    private readonly List<byte[]> replayFrameCache = new();

    public void Awake() {
        Set(this);
        StateChanged += OnClientStateChanged;

        realtimeClient = new();
        realtimeClient.StateChanged += (ClientState oldState, ClientState newState) => {
            StateChanged?.Invoke(oldState, newState);
        };
        realtimeClient.AddCallbackTarget(this);

        QuantumCallback.Subscribe<CallbackSimulateFinished>(this, OnSimulateFinished);
        QuantumCallback.Subscribe<CallbackGameResynced>(this, OnGameResynced);
        QuantumCallback.Subscribe<CallbackGameDestroyed>(this, OnGameDestroyed);
        QuantumCallback.Subscribe<CallbackPluginDisconnect>(this, OnPluginDisconnect);
        QuantumEvent.Subscribe<EventGameStateChanged>(this, OnGameStateChanged);
        QuantumEvent.Subscribe<EventPlayerAdded>(this, OnPlayerAdded);
        QuantumEvent.Subscribe<EventRecordingStarted>(this, OnRecordingStarted);
        QuantumEvent.Subscribe<EventGameEnded>(this, OnGameEnded);
    }

    public void Update() {
        if (Client != null && Client.IsConnectedAndReady) {
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
                || (Client.State == ClientState.Joined && (!Runner || (Runner.Game == null) || Runner.Game.GetLocalPlayers().Count == 0))
        );
    }

    public IEnumerator PingUpdateCoroutine() {
        WaitForSeconds seconds = new(1);
        CommandUpdatePing pingCommand = new();
        while (true) {
            QuantumGame game;
            if (Runner && (game = Runner.Game) != null) {
                pingCommand.PingMs = (int) Ping.Value;
                foreach (int slot in game.GetLocalPlayerSlots()) {
                    game.SendCommand(slot, pingCommand);
                }
            }
            yield return seconds;
        }
    }

    public static Task Disconnect() {
        return Client.DisconnectAsync();
    }

    public static async Task<bool> ConnectToRegion(string region) {
        StateChanged?.Invoke(ClientState.Disconnected, ClientState.Authenticating);
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
        int regionIndex = RoomIdValidChars.IndexOf(roomId.ToUpper()[0]);
        string targetRegion = Regions.ElementAt(regionIndex).Code;

        if (Client.CurrentRegion != targetRegion) {
            // await Client.DisconnectAsync();
            await ConnectToRegion(targetRegion);
        }
    }

    public static async Task<short> CreateRoom(EnterRoomArgs args) {
        // Create a random room id.
        StringBuilder idBuilder = new();

        // First char should correspond to region.
        int index = Regions.IndexOf(r => r.Code.Equals(Region, StringComparison.InvariantCultureIgnoreCase)); // Dirty linq hack
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

    public static bool IsValidRoomId(string id) {
        if (id.Length <= 0) {
            return false;
        }
        id = id.ToUpper();
        int regionIndex = RoomIdValidChars.IndexOf(id[0]);
        return regionIndex >= 0 && regionIndex < Regions.Count() && Regex.IsMatch(id, $"[{RoomIdValidChars}]{{8}}");
    }

    public static async Task<short> JoinRoom(EnterRoomArgs args) {
        // Change to region if we need to
        args.RoomName = args.RoomName.ToUpper();
        await ConnectToRoomsRegion(args.RoomName);

        Debug.Log($"[Network] Attempting to join a game with the ID {args.RoomName}");
        return await Client.JoinRoomAsync(args, false);
    }

    public void SaveReplay(QuantumGame game) {
#if UNITY_STANDALONE
        if (IsReplay || game.RecordInputStream == null) {
            return;
        }

        // JSON-friendly replay
        QuantumReplayFile jsonReplay = game.GetRecordedReplay();
        jsonReplay.InitialTick = initialFrame;
        jsonReplay.InitialFrameData = initialFrameData;
        initialFrame = 0;
        initialFrameData = null;

        // Create directories and open file
        string replayFolder = Path.Combine(Application.streamingAssetsPath, "replays", "temp");
        Directory.CreateDirectory(replayFolder);
        string finalFilePath = Path.Combine(replayFolder, "Replay-" + DateTimeOffset.Now.ToUnixTimeSeconds() + ".mvlreplay");
        using FileStream outputStream = new FileStream(finalFilePath, FileMode.Create);

        // Write binary replay
        BinaryReplayFile binaryReplay = BinaryReplayFile.FromReplayData(jsonReplay, map, players);
        long writtenBytes = binaryReplay.WriteToStream(outputStream);

        // Complete
        game.RecordInputStream.Dispose();
        game.RecordInputStream = null;
        Debug.Log($"[Replay] Saved new replay '{finalFilePath}' ({Utils.BytesToString(writtenBytes)})");
#endif
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
        Runner = await QuantumRunner.StartGameAsync(sessionRunnerArguments);
        Runner.Game.AddPlayer(new RuntimePlayer {
            PlayerNickname = Settings.Instance.generalNickname ?? "noname",
            UserId = default(Guid).ToString(),
            UseColoredNickname = Settings.Instance.generalUseNicknameColor,
            Character = (byte) Settings.Instance.generalCharacter,
            Palette = (byte) Settings.Instance.generalPalette,
        });

        ChatManager.Instance.mutedPlayers.Clear();
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

    private void OnPluginDisconnect(CallbackPluginDisconnect e) {
        Debug.Log($"[Network] Disconnected via server plugin: {e.Reason}");
        
        WasDisconnectedViaError = true;
        MainMenuManager.Instance.OpenNetworkErrorBox(DisconnectCause.DisconnectByServerLogic, "reason", e.Reason);

        if (Runner) {
            Runner.Shutdown(ShutdownCause.SimulationStopped);
        }
    }

    private void OnGameDestroyed(CallbackGameDestroyed e) {
        SaveReplay(e.Game);
    }

    private void OnGameEnded(EventGameEnded e) {
        SaveReplay(e.Game);
    }

    private void OnPlayerAdded(EventPlayerAdded e) {
        RuntimePlayer runtimePlayer = e.Frame.GetPlayerData(e.Player);
        Debug.Log($"[Network] {runtimePlayer.PlayerNickname} ({runtimePlayer.UserId}) joined the game.");
    }

    private void OnPlayerRemoved(EventPlayerRemoved e) {
        RuntimePlayer runtimePlayer = e.Frame.GetPlayerData(e.Player);
        Debug.Log($"[Network] {runtimePlayer.PlayerNickname} ({runtimePlayer.UserId}) left the game.");
    }

    private void OnGameStateChanged(EventGameStateChanged e) {
        if (!Client.IsConnectedAndReady
            || !Client.LocalPlayer.IsMasterClient) {
            return;
        }

        BooleanProperties props = (int) Client.CurrentRoom.CustomProperties[Enums.NetRoomProperties.BoolProperties];
        props.GameStarted = e.NewState != GameState.PreGameRoom;

        Client.OpSetCustomPropertiesOfRoom(new Photon.Client.PhotonHashtable {
            { Enums.NetRoomProperties.BoolProperties, (int) props }
        });
    }

    int initialFrame;
    byte[] initialFrameData;
    AssetRef<Map> map;
    byte players;
    private unsafe void OnRecordingStarted(EventRecordingStarted e) {
        QuantumGame game = e.Game;
        Frame startFrame = e.Frame;

        game.StartRecordingInput(startFrame.Number);
        initialFrameData = startFrame.Serialize(DeterministicFrameSerializeMode.Serialize);
        initialFrame = startFrame.Number;
        map = e.Frame.MapAssetRef;
        players = e.Frame.Global->RealPlayers;
        Debug.Log("[Replay] Started recording a new replay.");
    }

    public async static void StartReplay(BinaryReplayFile replay) {
        if (Client.IsConnected) {
            await Client.DisconnectAsync();
        }

        IsReplay = true;
        ReplayStart = replay.InitialFrameNumber;
        ReplayLength = replay.ReplayLengthInFrames;

        var serializer = new QuantumUnityJsonSerializer();
        var runtimeConfig = serializer.ConfigFromByteArray<RuntimeConfig>(replay.DecompressedRuntimeConfigData, compressed: true);
        var deterministicConfig = DeterministicSessionConfig.FromByteArray(replay.DecompressedDeterministicConfigData);
        var inputStream = new Photon.Deterministic.BitStream(replay.DecompressedInputData);
        var replayInputProvider = new BitStreamReplayInputProvider(inputStream, ReplayEnd);

        var arguments = new SessionRunner.Arguments {
            GameParameters = QuantumRunnerUnityFactory.CreateGameParameters,
            RuntimeConfig = runtimeConfig,
            SessionConfig = deterministicConfig,
            ReplayProvider = replayInputProvider,
            GameMode = DeterministicGameMode.Replay,
            RunnerId = "LOCALREPLAY",
            PlayerCount = deterministicConfig.PlayerCount,
            InitialTick = ReplayStart,
            FrameData = replay.DecompressedInitialFrameData,
            DeltaTimeType = SimulationUpdateTime.EngineDeltaTime
        };

        GlobalController.Instance.loadingCanvas.Initialize(null);
        ReplayFrameCache.Clear();
        ReplayFrameCache.Add(arguments.FrameData);
        Runner = await QuantumRunner.StartGameAsync(arguments);
    }

    private unsafe void OnGameResynced(CallbackGameResynced e) {
        if (e.Game.Frames.Verified.Global->GameState == GameState.Playing) {
            Frame startFrame = e.Game.Frames.Verified;

            e.Game.StartRecordingInput(startFrame.Number);
            initialFrameData = startFrame.Serialize(DeterministicFrameSerializeMode.Serialize);
            initialFrame = startFrame.Number;
        }
    }

    private void OnSimulateFinished(CallbackSimulateFinished e) {
        if (!IsReplay) {
            return;
        }

        Frame f = e.Frame;
        if ((f.Number - ReplayStart) % (5 * f.UpdateRate) == 0) {
            // Save this frame to the replay cache
            int index = (f.Number - ReplayStart) / (5 * f.UpdateRate);
            if (replayFrameCache.Count <= index) {
                byte[] serializedFrame = f.Serialize(DeterministicFrameSerializeMode.Serialize);
                byte[] copy = new byte[serializedFrame.Length];
                Array.Copy(serializedFrame, copy, serializedFrame.Length);
                replayFrameCache.Add(copy);
            }
        }
    }

    public void OnConnected() { }

    public void OnConnectedToMaster() { }

    public void OnDisconnected(DisconnectCause cause) {
        Debug.Log($"[Network] Disconnected. Reason: {cause}");
        WasDisconnectedViaError = true;

        if (!NonErrorDisconnectCauses.Contains(cause)) {
            MainMenuManager.Instance.OpenNetworkErrorBox(cause);
        }

        if (Runner) {
            Runner.Shutdown(ShutdownCause.SimulationStopped);
        }
    }

    public void OnRegionListReceived(RegionHandler regionHandler) { }

    public void OnCustomAuthenticationResponse(Dictionary<string, object> data) {
        PlayerPrefs.SetString("id", Client.AuthValues.UserId);

        if (data.TryGetValue("Token", out object token) && token is string tokenString) {
            PlayerPrefs.SetString("token", tokenString);
        }

        PlayerPrefs.Save();
    }

    public void OnCustomAuthenticationFailed(string debugMessage) { }

    public static bool FilterOutReplayFastForward(IDeterministicGame game) {
        return !IsReplayFastForwarding;
    }
}
