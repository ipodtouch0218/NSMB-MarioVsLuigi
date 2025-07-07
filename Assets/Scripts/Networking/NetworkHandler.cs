using NSMB.Networking;
using Photon.Deterministic;
using Photon.Realtime;
using Quantum;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEngine;
using static NSMB.Utilities.NetworkUtils;

namespace NSMB.Networking {
    public class NetworkHandler : Singleton<NetworkHandler>, IMatchmakingCallbacks, IConnectionCallbacks {

        //---Events
        public static event Action<ClientState, ClientState> StateChanged;
        public static event Action<string, bool> OnError;

        //---Constants
        public static readonly string RoomIdValidChars = "BCDFGHJKLMNPRQSTVWXYZ";
        public static readonly int RoomIdLength = 4;
        private static readonly List<DisconnectCause> NonErrorDisconnectCauses = new() {
            DisconnectCause.None, DisconnectCause.DisconnectByClientLogic, DisconnectCause.ApplicationQuit,
        };

        //---Static Variables
        public static RealtimeClient Client => Instance ? Instance.realtimeClient : null;
        public static long? Ping => Client?.RealtimePeer.Stats.RoundtripTime;
        public static QuantumRunner Runner { get; set; }
        public static QuantumGame Game => Runner?.Game ?? QuantumRunner.DefaultGame;
        public static IEnumerable<Region> Regions => Client?.RegionHandler?.EnabledRegions?.OrderBy(r => r.Code);
        public static string Region => Client?.CurrentRegion ?? Instance.lastRegion;
        public static bool WasDisconnectedViaError { get; set; }

        //---Private Variables
        private RealtimeClient realtimeClient;
        private string lastRegion;
        private Coroutine pingUpdateCoroutine;

        public void Awake() {
            Set(this);
            StateChanged += OnClientStateChanged;

            realtimeClient = new();
            realtimeClient.StateChanged += (ClientState oldState, ClientState newState) => {
                StateChanged?.Invoke(oldState, newState);
            };
            realtimeClient.AddCallbackTarget(this);

            QuantumCallback.Subscribe<CallbackGameStarted>(this, OnGameStarted);
            QuantumCallback.Subscribe<CallbackPluginDisconnect>(this, OnPluginDisconnect);
            QuantumEvent.Subscribe<EventHostChanged>(this, OnHostChanged);
            QuantumEvent.Subscribe<EventGameStateChanged>(this, OnGameStateChanged);
            QuantumEvent.Subscribe<EventPlayerAdded>(this, OnPlayerAdded);
            QuantumEvent.Subscribe<EventRulesChanged>(this, OnRulesChanged);
            QuantumEvent.Subscribe<EventPlayerKickedFromRoom>(this, OnPlayerKickedFromRoom);
        }

        public void Update() {
            if (Client != null && Client.IsConnectedAndReady) {
                Client.Service();
            }
        }

        public void OnDestroy() {
            StateChanged -= OnClientStateChanged;
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
            if (Client == null) {
                return false;
            }
            if (Runner != null && Runner.IsRunning) {
                await Runner.ShutdownAsync();
            }

            StateChanged?.Invoke(ClientState.Disconnected, ClientState.Authenticating);
            region ??= Instance.lastRegion;
            Instance.lastRegion = region;
            Client.AuthValues = await AuthenticationHandler.Authenticate();

            if (Client == null) {
                return false;
            }

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
                    AppVersion = GameVersion.Parse(Application.version).ToStringIgnoreHotfix(),
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
            args.RoomOptions.CustomRoomPropertiesForLobby = DefaultRoomProperties.Keys.ToArray();

            Debug.Log($"[Network] Creating a game in {Region} with the ID {idBuilder}");
            return await Client.CreateAndJoinRoomAsync(args, false);
        }

        public static bool IsValidRoomId(string id, out int regionIndex) {
            if (id.Length <= 0) {
                regionIndex = -1;
                return false;
            }
            id = id.ToUpper();
            regionIndex = RoomIdValidChars.IndexOf(id[0]);
            return regionIndex >= 0 && regionIndex < Regions.Count() && Regex.IsMatch(id, $"[{RoomIdValidChars}]{{{RoomIdLength}}}");
        }

        public static async Task<short> JoinRoom(EnterRoomArgs args) {
            // Change to region if we need to
            args.RoomName = args.RoomName.ToUpper();
            await ConnectToRoomsRegion(args.RoomName);

            Debug.Log($"[Network] Attempting to join a game with the ID {args.RoomName}");
            return await Client.JoinRoomAsync(args, false);
        }

        private unsafe void UpdateRealtimeProperties() {
            if (!realtimeClient.InRoom) {
                return;
            }

            Frame f = Game.Frames.Predicted;
            PlayerRef host = f.Global->Host;
            if (!Game.PlayerIsLocal(host)) {
                return;
            }

            ref GameRules rules = ref f.Global->Rules;
            IntegerProperties intProperties = new IntegerProperties {
                StarRequirement = rules.StarsToWin,
                CoinRequirement = rules.CoinsForPowerup,
                Lives = rules.Lives,
                Timer = rules.TimerMinutes,
            };
            BooleanProperties boolProperties = new BooleanProperties {
                GameStarted = f.Global->GameState != GameState.PreGameRoom,
                CustomPowerups = rules.CustomPowerupsEnabled,
                Teams = rules.TeamsEnabled,
                DrawOnTimeUp = rules.DrawOnTimeUp,
            };

            RuntimePlayer hostData = f.GetPlayerData(host);
            Client.CurrentRoom.SetCustomProperties(new Photon.Client.PhotonHashtable {
                [Enums.NetRoomProperties.IntProperties] = (int) intProperties,
                [Enums.NetRoomProperties.BoolProperties] = (int) boolProperties,
                [Enums.NetRoomProperties.HostName] = hostData?.PlayerNickname ?? "noname",
                [Enums.NetRoomProperties.StageGuid] = rules.Stage.Id.ToString(),
                [Enums.NetRoomProperties.GamemodeGuid] = rules.Gamemode.Id.ToString(),
            });
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
                PlayerCount = Constants.MaxPlayers,
                Communicator = new QuantumNetworkCommunicator(Client),
            };

            try {
                Runner = await QuantumRunner.StartGameAsync(sessionRunnerArguments);
                Runner.Game.AddPlayer(new RuntimePlayer {
                    PlayerNickname = Settings.Instance.generalNickname ?? "noname",
                    UserId = Client.UserId,
                    UseColoredNickname = Settings.Instance.generalUseNicknameColor,
                    Character = (byte) Settings.Instance.generalCharacter,
                    Palette = (byte) Settings.Instance.generalPalette,
                });
            } catch { }

            GlobalController.Instance.connecting.SetActive(false);
        }

        public void OnJoinRoomFailed(short returnCode, string message) {
            Debug.Log($"[Network] Failed to join room: ({returnCode}) {message}");

            if (!RealtimeErrorCodes.TryGetValue(returnCode, out string errorTranslationKey)) {
                errorTranslationKey = $"{message} ({returnCode})";
            }

            ThrowError(errorTranslationKey, true);
        }

        public static void ThrowError(string key, bool network) {
            OnError?.Invoke(key, network);
        }

        public void OnJoinRandomFailed(short returnCode, string message) { }

        public void OnLeftRoom() {
            if (pingUpdateCoroutine != null) {
                StopCoroutine(pingUpdateCoroutine);
                pingUpdateCoroutine = null;
            }
        }

        private void OnPluginDisconnect(CallbackPluginDisconnect e) {
            Debug.Log($"[Network] Disconnected via server plugin: {e.Reason}");

            ThrowError(e.Reason, true);

            if (Runner) {
                Runner.Shutdown(ShutdownCause.SimulationStopped);
            }
        }

        private void OnHostChanged(EventHostChanged e) {
            UpdateRealtimeProperties();
        }

        private void OnRulesChanged(EventRulesChanged e) {
            UpdateRealtimeProperties();
        }

        private void OnPlayerKickedFromRoom(EventPlayerKickedFromRoom e) {
            if (e.Game.PlayerIsLocal(e.Player)) {
                Runner.Shutdown(ShutdownCause.Ok);
            }
        }

        private void OnPlayerAdded(EventPlayerAdded e) {
            Frame f = e.Game.Frames.Predicted;
            RuntimePlayer runtimePlayer = f.GetPlayerData(e.Player);
            Debug.Log($"[Network] {runtimePlayer.PlayerNickname} ({runtimePlayer.UserId}) joined the game.");
        }

        private void OnPlayerRemoved(EventPlayerRemoved e) {
            Frame f = e.Game.Frames.Predicted;
            RuntimePlayer runtimePlayer = f.GetPlayerData(e.Player);
            Debug.Log($"[Network] {runtimePlayer.PlayerNickname} ({runtimePlayer.UserId}) left the game.");
        }

        private void OnGameStateChanged(EventGameStateChanged e) {
            if (!Client.IsConnectedAndReady
                || !Client.LocalPlayer.IsMasterClient) {
                return;
            }

            BooleanProperties props = (int) Client.CurrentRoom.CustomProperties[Enums.NetRoomProperties.BoolProperties];
            props.GameStarted = e.NewState != GameState.PreGameRoom;

            Client.CurrentRoom.SetCustomProperties(new Photon.Client.PhotonHashtable {
            { Enums.NetRoomProperties.BoolProperties, (int) props }
        });
        }

        private unsafe void OnGameStarted(CallbackGameStarted e) {
            Frame f = e.Game.Frames.Verified;
            var bans = f.ResolveList(f.Global->BannedPlayerIds);
            foreach (var ban in bans) {
                if (ban.UserId == Client.UserId) {
                    QuantumRunner.Default.Shutdown(ShutdownCause.SessionError);
                    ThrowError("ui.error.join.banned", true);
                    return;
                }
            }
        }

        public void OnConnected() { }

        public void OnConnectedToMaster() { }

        public void OnDisconnected(DisconnectCause cause) {
            Debug.Log($"[Network] Disconnected. Reason: {cause}");

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
    }
}
