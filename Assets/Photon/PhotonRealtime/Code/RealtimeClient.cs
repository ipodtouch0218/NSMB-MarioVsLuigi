// -----------------------------------------------------------------------
// <copyright file="RealtimeClient.cs" company="Exit Games GmbH">
// Photon Realtime API - Copyright (C) 2022 Exit Games GmbH
// </copyright>
// <summary>
// Provides state and operations for the Photon Realtime API.
// </summary>
// <author>developer@photonengine.com</author>
// ----------------------------------------------------------------------------

#if UNITY_2017_4_OR_NEWER
#define SUPPORTED_UNITY
#endif


namespace Photon.Realtime
{
    using System;
    using System.Net;
    using System.Text;
    using System.Collections;
    using System.Collections.Generic;
    using System.Diagnostics;
    using Photon.Client;

    #if SUPPORTED_UNITY
    using UnityEngine;
    using Debug = UnityEngine.Debug;
    #endif
    #if SUPPORTED_UNITY || NETFX_CORE
    using SupportClass = Photon.Client.SupportClass;
    #endif


    /// <summary>
    /// Main class of the Photon Realtime API.
    /// It keeps (connection) state and will automatically execute transitions from Name Server to Master Server and Game Servers.
    /// </summary>
    /// <remarks>
    /// This class (and the Player class) should be extended to implement your own game logic.
    /// You can override CreatePlayer as "factory" method for Players and return your own Player instances.
    /// The State of this class is essential to know when a client is in a lobby (or just on the master)
    /// and when in a game where the actual gameplay should take place.
    /// Extension notes:
    /// An extension of this class should override the methods of the IPhotonPeerListener, as they
    /// are called when the state changes. Call base.method first, then pick the operation or state you
    /// want to react to and put it in a switch-case.
    /// We try to provide demo to each platform where this api can be used, so lookout for those.
    /// </remarks>
    public partial class RealtimeClient : IPhotonPeerListener
    {
        /// <summary>
        /// The client uses a PhotonPeer to communicate with the server. Public for ease-of-use, as some settings are directly made on it.
        /// </summary>
        public readonly PhotonPeer RealtimePeer;

        /// <summary>Prefixes for all logging messages. Useful if there are multiple clients.</summary>
        public string LogPrefix;

        /// <summary>Level of logging this client does. Default: LogLevel.Warn. This is independent from LogLevelPeer.</summary>
        public LogLevel LogLevel = LogLevel.Warning;

        /// <summary>Level of logging for PhotonPeer (networking layer).</summary>
        public LogLevel LogLevelPeer
        {
            get
            {
                return this.RealtimePeer.LogLevel;
            }
            set
            {
                this.RealtimePeer.LogLevel = value;
            }
        }

        /// <summary>Millisecond interval at which stats are being logged if the LogLevel is at least Info. Default: 5000.</summary>
        public int LogStatsInterval = 5000;

        /// <summary>Used to measure interval of statistics logging (LogLevel.Debug).</summary>
        private int lastStatsLogTime;

        /// <summary>
        /// Gets or sets the binary protocol version used by this client
        /// </summary>
        /// <remarks>
        /// Use this always instead of setting it via <see cref="RealtimePeer"/>
        /// (<see cref="PhotonPeer.SerializationProtocolType"/>) directly, especially when WSS protocol is used.
        /// </remarks>
        public SerializationProtocol SerializationProtocol
        {
            get
            {
                return this.RealtimePeer.SerializationProtocolType;
            }
            set
            {
                this.RealtimePeer.SerializationProtocolType = value;
            }
        }

        /// <summary>Stores this client's AppSettings, as applied by ConnectUsingSettings().</summary>
        /// <remarks>This is a unique copy of the settings passed to ConnectUsingSettings().</remarks>
        public AppSettings AppSettings { get; private set; }


        /// <summary>The AppID for the Photon Cloud. If you host a Photon Server yourself, make up and use a GUID.</summary>
        [Obsolete("Use RealtimeClient.AppSettings.GetAppId(this.ClientType) instead.")]
        public string AppId
        {
            get
            {
                if (this.AppSettings == null)
                {
                    return null;
                }

                return this.AppSettings.GetAppId(this.ClientType);
            }
        }

        /// <summary>The AppVersion this client should use. Replaced by this.AppSettings.AppVersion.</summary>
        [Obsolete("Use AppSettings.AppVersion instead. ConnectUsingSettings() will overwrite the AppVersion!")]
        public string AppVersion
        {
            get
            {
                if (this.AppSettings == null)
                {
                    return null;
                }

                return this.AppSettings.AppVersion;
            }
        }


        /// <summary>The ClientAppType defines which sort of AppId should be expected. The RealtimeClient supports Realtime and Voice app types. Default: Realtime.</summary>
        public ClientAppType ClientType { get; set; }

        /// <summary>User authentication values to be sent to the Photon server right after connecting.</summary>
        /// <remarks>Set this property or pass AuthenticationValues by Connect(..., authValues).</remarks>
        public AuthenticationValues AuthValues { get; set; }

        /// <summary>Defines how the communication gets encrypted.</summary>
        public EncryptionMode EncryptionMode = EncryptionMode.PayloadEncryption;


        ///<summary>Simplifies getting the token for connect/init requests, if this feature is enabled.</summary>
        private object TokenForInit
        {
            get
            {
                if (this.AppSettings.AuthMode == AuthModeOption.Auth || this.AuthValues == null)
                {
                    return null;
                }
                return this.AuthValues.Token;
            }
        }

        /// <summary>Name Server Host Name for Photon Cloud. Without port and without any prefix.</summary>
        public string NameServerHost = "ns.photonengine.io";

        /// <summary>Name Server Address for Photon Cloud (based on current protocol). You can use the default values and usually won't have to set this value.</summary>
        public string NameServerAddress { get { return this.GetNameServerAddress(); } }

        /// <summary>Defines the ports to use per protocol and server-type.</summary>
        /// <remarks>
        /// ProtocolPorts define a port per protocol and Photon server type.
        /// At a minimum, the Name Server port for the selected protocol must be non zero.
        ///
        /// If ports for Master Server and Game Server are 0, the client will just use whatever address it got for the corresponding server.
        ///
        /// In case of using the AuthMode AutOnceWss, the Name Server protocol is wss, while udp or tcp will be used on the master server and game server.
        /// Set the ports accordingly per protocol and server.
        /// </remarks>
        public ProtocolPorts ProtocolPorts = new ProtocolPorts();

        /// <summary>The currently used server address (if any). The type of server is define by Server property.</summary>
        public string CurrentServerAddress { get { return this.RealtimePeer.ServerAddress; } }

        /// <summary>Your Master Server address. In PhotonCloud, call ConnectToRegionMaster() to find your Master Server.</summary>
        /// <remarks>
        /// In the Photon Cloud, explicit definition of a Master Server Address is not best practice.
        /// The Photon Cloud has a "Name Server" which redirects clients to a specific Master Server (per Region and AppId).
        /// </remarks>
        public string MasterServerAddress { get; set; }

        /// <summary>The game server's address for a particular room. In use temporarily, as assigned by master.</summary>
        public string GameServerAddress { get; protected internal set; }

        /// <summary>The server this client is currently connected or connecting to.</summary>
        /// <remarks>
        /// Each server (NameServer, MasterServer, GameServer) allow some operations and reject others.
        /// </remarks>
        public ServerConnection Server { get; private set; }

        /// <summary>Backing field for property.</summary>
        private ClientState state = ClientState.PeerCreated;

        /// <summary>Current state this client is in. Careful: several states are "transitions" that lead to other states.</summary>
        public ClientState State
        {
            get
            {
                return this.state;
            }

            set
            {
                if (this.state == value)
                {
                    return;
                }
                ClientState previousState = this.state;
                this.state = value;

                if (this.StateChanged != null)
                {
                    this.StateChanged(previousState, this.state);
                }
            }
        }

        /// <summary>
        /// The ConnectionHandler instance, which is used to keep alive the connection if this client's Service() does not get called regularly.
        /// </summary>
        public ConnectionHandler Handler
        {
            get;
            private set;
        }

        /// <summary>Returns if this client is currently connected or connecting to some type of server.</summary>
        /// <remarks>This is even true while switching servers. Use IsConnectedAndReady to check only for those states that enable you to send Operations.</remarks>
        public bool IsConnected { get { return this.State != ClientState.PeerCreated && this.State != ClientState.Disconnected; } }


        /// <summary>
        /// A refined version of IsConnected which is true only if your connection is ready to send operations.
        /// </summary>
        /// <remarks>
        /// Not all operations can be called on all types of servers. If an operation is unavailable on the currently connected server,
        /// this will result in a OperationResponse with ErrorCode != 0.
        ///
        /// Examples: The NameServer allows OpGetRegions which is not available anywhere else.
        /// The MasterServer does not allow you to send events (OpRaiseEvent) and on the GameServer you are unable to join a lobby (OpJoinLobby).
        ///
        /// To check which server you are on, use: <see cref="Server"/>.
        /// </remarks>
        public bool IsConnectedAndReady
        {
            get
            {
                switch (this.State)
                {
                    case ClientState.PeerCreated:
                    case ClientState.Disconnected:
                    case ClientState.Disconnecting:
                    case ClientState.DisconnectingFromGameServer:
                    case ClientState.DisconnectingFromMasterServer:
                    case ClientState.DisconnectingFromNameServer:
                    case ClientState.Authenticating:
                    case ClientState.ConnectingToGameServer:
                    case ClientState.ConnectingToMasterServer:
                    case ClientState.ConnectingToNameServer:
                    case ClientState.Joining:
                    case ClientState.Leaving:
                        return false;   // we are not ready to execute any operations
                }

                return true;
            }
        }


        /// <summary>Register a method to be called when this client's ClientState gets set. Better use the callbacks to react to specific situations.</summary>
        /// <remarks>This can be useful to react to being connected, joined into a room, etc.</remarks>
        public event Action<ClientState, ClientState> StateChanged;

        /// <summary>Register a method to be called when an event got dispatched. Gets called after the RealtimeClient handled the internal events first.</summary>
        /// <remarks>
        /// This is an alternative to extending RealtimeClient to override OnEvent().
        ///
        /// Note that OnEvent is calling EventReceived after it handled internal events first.
        /// That means for example: Joining players will already be in the player list but leaving
        /// players will already be removed from the room.
        /// </remarks>
        public event Action<EventData> EventReceived;
        /// <summary>Register a method to be called when a message got dispatched. Gets called after the RealtimeClient handled the message internally.</summary>
        public event Action<bool, object> MessageReceived;


        /// <summary>Register a method to be called when an operation response is received.</summary>
        /// <remarks>
        /// This is an alternative to extending RealtimeClient to override OnOperationResponse().
        ///
        /// Note that OnOperationResponse gets executed before your Action is called.
        /// That means for example: The OpJoinLobby response already set the state to "JoinedLobby"
        /// and the response to OpLeave already triggered the Disconnect before this is called.
        /// </remarks>
        public event Action<OperationResponse> OpResponseReceived;


        /// <summary>Wraps up the target objects for a group of callbacks, so they can be called conveniently.</summary>
        /// <remarks>By using Add or Remove, objects can "subscribe" or "unsubscribe" for this group  of callbacks.</remarks>
        /// <remarks>This is public for PUN to call for offline rooms.</remarks>
        internal ConnectionCallbacksContainer ConnectionCallbackTargets;

        /// <summary>Wraps up the target objects for a group of callbacks, so they can be called conveniently.</summary>
        /// <remarks>By using Add or Remove, objects can "subscribe" or "unsubscribe" for this group  of callbacks.</remarks>
        /// <remarks>This is public for PUN to call for offline rooms.</remarks>
        internal MatchMakingCallbacksContainer MatchMakingCallbackTargets;

        /// <summary>Wraps up the target objects for a group of callbacks, so they can be called conveniently.</summary>
        /// <remarks>By using Add or Remove, objects can "subscribe" or "unsubscribe" for this group  of callbacks.</remarks>
        internal InRoomCallbacksContainer InRoomCallbackTargets;

        /// <summary>Wraps up the target objects for a group of callbacks, so they can be called conveniently.</summary>
        /// <remarks>By using Add or Remove, objects can "subscribe" or "unsubscribe" for this group  of callbacks.</remarks>
        internal LobbyCallbacksContainer LobbyCallbackTargets;

        /// <summary>Wraps up the target objects for a group of callbacks, so they can be called conveniently.</summary>
        /// <remarks>By using Add or Remove, objects can "subscribe" or "unsubscribe" for this group  of callbacks.</remarks>
        internal ErrorInfoCallbacksContainer ErrorInfoCallbackTargets;


        /// <summary>Summarizes (aggregates) the different causes for disconnects of a client.</summary>
        /// <remarks>
        /// A disconnect can be caused by: errors in the network connection or some vital operation failing
        /// (which is considered "high level"). While operations always trigger a call to OnOperationResponse,
        /// connection related changes are treated in OnStatusChanged.
        /// The DisconnectCause is set in either case and summarizes the causes for any disconnect in a single
        /// state value which can be used to display (or debug) the cause for disconnection.
        /// </remarks>
        public DisconnectCause DisconnectedCause { get; protected set; }


        /// <summary>
        /// After a to a connection loss or timeout, this summarizes the most relevant system conditions which might have contributed to the loss.
        /// </summary>
        /// <remarks>
        /// </remarks>
        public SystemConnectionSummary SystemConnectionSummary;


        /// <summary>Returns CurrentLobby != null.</summary>
        public bool InLobby
        {
            get { return this.CurrentLobby != null; }
        }

        /// <summary>The lobby this client currently uses. Defined when joining a lobby or creating rooms</summary>
        public TypedLobby CurrentLobby { get; internal set; }

        /// <summary>Used by OpJoinLobby to store the target lobby until the server confirms joining it.</summary>
        private TypedLobby targetLobbyCache;

        /// <summary>Privately used to wrap up lobby stats provided in OnLobbyStatisticsUpdate().</summary>
        private readonly List<TypedLobbyInfo> lobbyStatistics = new List<TypedLobbyInfo>();


        /// <summary>The local player is never null but not valid unless the client is in a room, too. The ID will be -1 outside of rooms.</summary>
        public Player LocalPlayer { get; internal set; }

        /// <summary>
        /// The nickname of the player (synced with others). Same as client.LocalPlayer.NickName.
        /// </summary>
        public string NickName
        {
            get
            {
                return this.LocalPlayer.NickName;
            }

            set
            {
                if (this.LocalPlayer == null)
                {
                    return;
                }

                this.LocalPlayer.NickName = value;
            }
        }


        /// <summary>Access for AuthValues.UserId. Identifier for the player / user.</summary>
        /// <remarks>
        /// In best case unique and authenticated via Authentication Provider.
        /// If the UserId is not set before connect, the server applies a temporary ID.
        ///
        /// The UserId is used in FindFriends and for fetching data for your account (with WebHooks e.g.).
        ///
        /// By convention, set this ID before you connect, not while being connected.
        /// There is no error but the ID won't change while being connected.
        /// </remarks>
        public string UserId
        {
            get
            {
                if (this.AuthValues != null)
                {
                    return this.AuthValues.UserId;
                }
                return null;
            }
            set
            {
                if (this.AuthValues == null)
                {
                    this.AuthValues = new AuthenticationValues();
                }
                this.AuthValues.UserId = value;
            }
        }

        /// <summary>The current room this client is connected to (null if none available).</summary>
        public Room CurrentRoom { get; set; }


        /// <summary>Is true while being in a room (this.state == ClientState.Joined).</summary>
        /// <remarks>
        /// Aside from polling this value, game logic should implement IMatchmakingCallbacks in some class
        /// and react when that gets called.<br/>
        /// OpRaiseEvent, OpLeave and some other operations can only be used (successfully) when the client is in a room..
        /// </remarks>
        public bool InRoom
        {
            get
            {
                return this.state == ClientState.Joined && this.CurrentRoom != null;
            }
        }

        /// <summary>Statistic value available on master server: Players on master (looking for games).</summary>
        public int PlayersOnMasterCount { get; internal set; }

        /// <summary>Statistic value available on master server: Players in rooms (playing).</summary>
        public int PlayersInRoomsCount { get; internal set; }

        /// <summary>Statistic value available on master server: Rooms currently created.</summary>
        public int RoomsCount { get; internal set; }


        /// <summary>Internally used to decide if a room must be created or joined on game server.</summary>
        private JoinType lastJoinType;

        /// <summary>Used when the client arrives on the GS, to join the room with the correct values.</summary>
        private EnterRoomArgs enterRoomArgumentsCache;

        /// <summary>Used to cache a failed "enter room" operation on the Game Server, to return to the Master Server before calling a fail-callback.</summary>
        private OperationResponse failedRoomEntryOperation;


        /// <summary>Maximum of userIDs that can be sent in one friend list request.</summary>
        private const int FriendRequestListMax = 512;

        /// <summary>Contains the list of names of friends to look up their state on the server.</summary>
        private string[] friendListRequested;

        /// <summary>Internal flag to know if the client currently fetches a friend list.</summary>
        public bool IsFetchingFriendList { get { return this.friendListRequested != null; } }

        /// <summary>The cluster name provided by the Name Server.</summary>
        /// <remarks>
        /// The value is provided by the OpResponse for OpAuthenticate/OpAuthenticateOnce.
        /// Default: null. This value only ever updates from the Name Server authenticate response.
        /// </remarks>
        public string CurrentCluster { get; private set; }

        /// <summary>String code of the Region the client is connected to. Null if none. </summary>
        public string CurrentRegion { get; private set; }

        /// <summary>While online, this is the region the client connected to.</summary>
        [Obsolete("Use CurrentRegion instead.")]
        public string CloudRegion
        {
            get { return this.CurrentRegion; }
        }


        /// <summary>Contains the list if enabled regions this client may use. Null, unless the client got a response to OpGetRegions.</summary>
        public RegionHandler RegionHandler;

        /// <summary>Accesses this.RegionHandler?.SummaryToCache. May be null.</summary>
        public string SummaryToCache { get { return this.RegionHandler?.SummaryToCache; } }



        private readonly Queue<CallbackTargetChange> callbackTargetChanges = new Queue<CallbackTargetChange>();
        private readonly HashSet<object> callbackTargets = new HashSet<object>();


        /// <summary>Options for a "workflow". Default is to connect and get on the Master Server.</summary>
        private enum ClientWorkflowOption { Default, GetRegionsAndPing, GetRegionsOnly }

        /// <summary>Can be used to run a "workflow", aside from the usual connecting and calling operations. Used for GetRegions.</summary>
        private ClientWorkflowOption clientWorkflow;


        /// <summary>Creates a RealtimeClient with UDP protocol or the one specified.</summary>
        /// <param name="protocol">Specifies the network protocol to use for connections.</param>
        public RealtimeClient(ConnectionProtocol protocol = ConnectionProtocol.Udp)
        {
            this.AppSettings = new AppSettings();
            this.RealtimePeer = new PhotonPeer(this, protocol);

            this.ConnectionCallbackTargets = new ConnectionCallbacksContainer(this);
            this.MatchMakingCallbackTargets = new MatchMakingCallbacksContainer(this);
            this.InRoomCallbackTargets = new InRoomCallbacksContainer(this);
            this.LobbyCallbackTargets = new LobbyCallbacksContainer(this);
            this.ErrorInfoCallbackTargets = new ErrorInfoCallbacksContainer(this);

            this.SerializationProtocol = SerializationProtocol.GpBinaryV18;
            this.LocalPlayer = this.CreatePlayer(string.Empty, -1, true, null);

            #if SUPPORTED_UNITY
            CustomTypesUnity.Register();
            ConfigUnitySockets();
            #endif

            this.State = ClientState.PeerCreated;
        }


        #region Operations and Commands


        // Connecting to the Photon Cloud might fail due to:
        // - Network issues (OnStatusChanged() StatusCode.ExceptionOnConnect)
        // - Region not available (OnOperationResponse() for OpAuthenticate with ReturnCode == ErrorCode.InvalidRegion)
        // - Subscription CCU limit reached (OnOperationResponse() for OpAuthenticate with ReturnCode == ErrorCode.MaxCcuReached)


        /// <summary>Starts the "process" to connect as defined by the appSettings (AppId, AppVersion, Transport Protocol, Port and more).</summary>
        /// <remarks>
        /// A typical connection process wraps up these steps:<br/>
        /// - Low level connect and init (which establishes a connection that enables operations and responses for the Realtime API).<br/>
        /// - GetRegions and select best (unless FixedRegion is being used).<br/>
        /// - Authenticate user for a specific region (this provides a Master Server address to go to and a token).<br/>
        /// - Disconnect Name Server and connect to Master Server (using the token).<br/>
        /// - The callback OnConnectedToMaster gets called.<br/>
        /// <br/>
        /// Connecting to the servers is a process and this is a non-blocking method.<br/>
        /// Implement and register the IConnectionCallbacks interface to get callbacks about success or failing connects.<br/>
        /// <br/>
        /// Basically all settings for the connection, AppId and servers can be done via the provided parameter.<br/>
        /// <br/>
        /// Connecting to the Photon Cloud might fail due to:<br/>
        /// - Network issues<br/>
        /// - Region not available<br/>
        /// - Subscription CCU limit<br/>
        /// </remarks>
        /// <see cref="IConnectionCallbacks"/>
        /// <see cref="AuthValues"/>
        /// <param name="appSettings">Collection of settings defining this app and how to connect.</param>
        /// <returns>True if the client can attempt to connect.</returns>
        public virtual bool ConnectUsingSettings(AppSettings appSettings)
        {
            if (this.RealtimePeer.PeerState != PeerStateValue.Disconnected)
            {
                Log.Warn("ConnectUsingSettings() failed. Can only connect while in state 'Disconnected'. Current state: " + this.RealtimePeer.PeerState, this.LogLevel, this.LogPrefix);
                return false;
            }

            if (appSettings == null)
            {
                Log.Error("ConnectUsingSettings() failed. The appSettings can't be null.'", this.LogLevel, this.LogPrefix);
                return false;
            }


            this.AppSettings = new AppSettings(appSettings);

            if (!this.ClientTypeChecks())
            {
                Log.Error("Can not connect. AppId or ClientType not set correctly.", this.LogLevel, this.LogPrefix);
                return false;
            }

            this.GameServerAddress = String.Empty;
            this.MasterServerAddress = String.Empty;
            this.LogLevel = this.AppSettings.ClientLogging;
            this.RealtimePeer.LogLevel = this.AppSettings.NetworkLogging;
            this.CurrentRegion = null;
            this.DisconnectedCause = DisconnectCause.None;
            this.SystemConnectionSummary = null;

            if (this.AuthValues != null)
            {
                this.AuthValues.Token = null;
            }

            this.CheckConnectSetupWebGl();

            if (IPAddress.TryParse(this.AppSettings.Server, out IPAddress address))
            {
                // note: this check must be done after the protocol is assigned and after CheckConnectSetupWebGl()
                if (this.AppSettings.Protocol == ConnectionProtocol.WebSocket || this.AppSettings.Protocol == ConnectionProtocol.WebSocketSecure)
                {
                    Log.Error("AppSettings.Server is an IP address. Can not use WS or WSS protocols with IP addresses.", this.LogLevel, this.LogPrefix);
                    return false;
                }
                if (this.AppSettings.AuthMode == AuthModeOption.AuthOnceWss)
                {
                    Log.Warn("AppSettings.Server is an IP address. Changing this client's AuthMode to AuthOnce.", this.LogLevel, this.LogPrefix);
                    this.AppSettings.AuthMode = AuthModeOption.AuthOnce;
                }
            }

            if (this.AppSettings.AuthMode == AuthModeOption.AuthOnceWss)
            {
                this.RealtimePeer.TransportProtocol = ConnectionProtocol.WebSocketSecure;
            }
            else
            {
                this.RealtimePeer.TransportProtocol = this.AppSettings.Protocol;
            }



            if (this.Handler == null)
            {
                this.Handler = ConnectionHandler.BuildInstance(this, this.ClientType.ToString());
            }
            this.Handler.StartFallbackSendAckThread();


            if (this.AppSettings.UseNameServer)
            {
                this.Server = ServerConnection.NameServer;
                if (!appSettings.IsDefaultNameServer)
                {
                    this.NameServerHost = appSettings.Server;
                }

                if (!this.RealtimePeer.Connect(this.NameServerAddress, this.AppSettings.GetAppId(this.ClientType), photonToken: this.TokenForInit, proxyServerAddress: this.AppSettings.ProxyServer))
                {
                    return false;
                }

                this.State = ClientState.ConnectingToNameServer;
                Log.Info(this.ConnectLog("ConnectUsingSettings()"),  this.LogLevel, this.LogPrefix);
            }
            else
            {
                this.Server = ServerConnection.MasterServer;
                int portToUse = appSettings.IsDefaultPort ? this.ProtocolPorts.Get(this.RealtimePeer.TransportProtocol, ServerConnection.MasterServer) : appSettings.Port;
                this.MasterServerAddress = string.Format("{0}:{1}", appSettings.Server, portToUse);

                if (!this.RealtimePeer.Connect(this.MasterServerAddress, this.AppSettings.GetAppId(this.ClientType), photonToken: this.TokenForInit, proxyServerAddress: this.AppSettings.ProxyServer))
                {
                    return false;
                }

                this.State = ClientState.ConnectingToMasterServer;
                Log.Info(this.ConnectLog("ConnectUsingSettings()"),  this.LogLevel, this.LogPrefix);
            }

            return true;
        }


        private bool ClientTypeChecks()
        {
            if (this.ClientType == ClientAppType.Detect)
            {
                ClientAppType result = this.AppSettings.ClientTypeDetect();

                if (result == ClientAppType.Detect)
                {
                    Log.Error("ConnectUsingSettings requires that the AppSettings contain exactly one value set out of AppIdRealtime, AppIdFusion or AppIdQuantum.");
                    return false;
                }

                this.ClientType = result;
            }

            // TODO: add more checks and or log something in this case?
            return true;
        }

        /// <summary>Gets the region list for the app (by AppId) pings them. Will callback: OnRegionListReceived.</summary>
        /// <remarks>
        /// This uses ConnectUsingSettings, which will copy the appSettings before modifications to the copy.
        /// Needs to be offline. The client will disconnect before calling the callback.
        ///
        /// If the AppId is null or unknown OnCustomAuthenticationFailed gets called before the client disconnects.
        /// </remarks>
        /// <param name="appSettings">AppSettings to fetch the region list for.</param>
        /// <param name="ping">Optionally disables pinging the available regions. OnRegionListReceived gets called without pinging the regions. </param>
        public void GetRegions(AppSettings appSettings, bool ping = true)
        {
            if (this.State != ClientState.Disconnected && this.State != ClientState.PeerCreated)
            {
                Log.Warn("Can not GetRegions() while connected. Disconnect first.");
                return;
            }

            this.clientWorkflow = ping ? ClientWorkflowOption.GetRegionsAndPing : ClientWorkflowOption.GetRegionsOnly;
            this.ConnectUsingSettings(appSettings);
            this.AppSettings.BestRegionSummaryFromStorage = null;   // appSettings are COPIED from parameter to internal variable, so we can change this now without side-effects
        }



        [Conditional("UNITY_WEBGL")]
        private void CheckConnectSetupWebGl()
        {
            #if UNITY_WEBGL
            if (this.RealtimePeer.TransportProtocol != ConnectionProtocol.WebSocket && this.RealtimePeer.TransportProtocol != ConnectionProtocol.WebSocketSecure)
            {
                Log.Warn("WebGL requires WebSockets. Switching TransportProtocol to WebSocketSecure.", this.LogLevel, this.LogPrefix);
                this.AppSettings.Protocol = ConnectionProtocol.WebSocketSecure;
            }

            this.AppSettings.EnableProtocolFallback = false; // no fallback on WebGL
            #endif
        }


        /// <summary>
        /// Privately used only for reconnecting.
        /// </summary>
        private bool CallConnect(ServerConnection serverType)
        {
            #if SUPPORTED_UNITY
            if (ConnectionHandler.AppQuits)
            {
                return false;
            }
            #endif

            if (this.State == ClientState.Disconnecting)
            {
                Log.Error("CallConnect() failed. Can't connect while disconnecting (still). Current state: " + this.State, this.LogLevel, this.LogPrefix);
                return false;
            }


            if (serverType != ServerConnection.NameServer)
            {
                // when using authMode AuthOnce or AuthOnceWSS, the token must be available for the init request. if it's null in that case, don't connect
                if (this.AppSettings.AuthMode != AuthModeOption.Auth && this.TokenForInit == null)
                {
                    Log.Error("Connect() failed. Can't connect to " + serverType + " with Token == null in AuthMode: " + this.AppSettings.AuthMode, this.LogLevel, this.LogPrefix);
                    return false;
                }

                // especially when coming from the Name Server, switch the protocol back to whatever was selected via AppSettings.Protocol
                this.RealtimePeer.TransportProtocol = this.AppSettings.Protocol;
            }


            string serverAddress = null;
            ClientState stateOnSuccess = ClientState.Disconnected;

            switch (serverType)
            {
                case ServerConnection.NameServer:
                    serverAddress = this.GetNameServerAddress();
                    stateOnSuccess = ClientState.ConnectingToNameServer;

                    // on the NameServer never re-use an existing AuthToken
                    if (this.AuthValues != null)
                    {
                        this.AuthValues.Token = null;
                    }
                    // may have to use WSS as transport
                    this.RealtimePeer.TransportProtocol = ConnectionProtocol.WebSocketSecure;
                    break;

                case ServerConnection.MasterServer:
                    serverAddress = this.MasterServerAddress;
                    stateOnSuccess = ClientState.ConnectingToMasterServer;
                    break;

                case ServerConnection.GameServer:
                    serverAddress = this.GameServerAddress;
                    stateOnSuccess = ClientState.ConnectingToGameServer;
                    break;
            }


            // connect might fail, if the DNS name can't be resolved or if no network connection is available, etc.
            bool connecting = this.RealtimePeer.Connect(serverAddress, this.AppSettings.GetAppId(this.ClientType), photonToken: this.TokenForInit, proxyServerAddress: this.AppSettings.ProxyServer);

            if (connecting)
            {
                this.DisconnectedCause = DisconnectCause.None;
                this.SystemConnectionSummary = null;
                this.Server = serverType;
                this.State = stateOnSuccess;
            }

            return connecting;
        }


        /// <summary>Can be used to reconnect to the master server after a disconnect.</summary>
        /// <remarks>Common use case: Press the Lock Button on a iOS device and you get disconnected immediately.</remarks>
        public bool ReconnectToMaster()
        {
            if (this.RealtimePeer.PeerState != PeerStateValue.Disconnected)
            {
                Log.Warn("ReconnectToMaster() failed. Can only connect while in state 'Disconnected'. Current state: " + this.RealtimePeer.PeerState, this.LogLevel, this.LogPrefix);
                return false;
            }
            if (string.IsNullOrEmpty(this.MasterServerAddress))
            {
                Log.Warn("ReconnectToMaster() failed. MasterServerAddress is null or empty.", this.LogLevel, this.LogPrefix);
                return false;
            }
            if (this.AuthValues == null || this.AuthValues.Token == null)
            {
                Log.Warn("ReconnectToMaster() failed. It seems the client doesn't have any previous authentication-token to re-connect.", this.LogLevel, this.LogPrefix);
                return false;
            }

            return this.CallConnect(ServerConnection.MasterServer);
        }

        /// <summary>
        /// Can be used to return to a room quickly by directly reconnecting to a game server to rejoin a room.
        /// </summary>
        /// <remarks>
        /// Rejoining room will not send any player properties. Instead client will receive up-to-date ones from server.
        /// If you want to set new player properties, do it once rejoined.
        /// </remarks>
        /// <returns>False, if the conditions are not met. Then, this client does not attempt the ReconnectAndRejoin.</returns>
        public bool ReconnectAndRejoin()
        {
            if (this.RealtimePeer.PeerState != PeerStateValue.Disconnected)
            {
                Log.Warn("ReconnectAndRejoin() failed. Can only connect while in state 'Disconnected'. Current state: " + this.RealtimePeer.PeerState, this.LogLevel, this.LogPrefix);
                return false;
            }
            if (string.IsNullOrEmpty(this.GameServerAddress))
            {
                Log.Warn("ReconnectAndRejoin() failed. It seems the client wasn't connected to a game server before (no address).", this.LogLevel, this.LogPrefix);
                return false;
            }
            if (this.enterRoomArgumentsCache == null)
            {
                Log.Warn("ReconnectAndRejoin() failed. It seems the client doesn't have any previous room to re-join.", this.LogLevel, this.LogPrefix);
                return false;
            }
            if (this.AuthValues == null || this.AuthValues.Token == null)
            {
                Log.Warn("ReconnectAndRejoin() failed. It seems the client doesn't have any previous authentication token to re-connect.", this.LogLevel, this.LogPrefix);
                return false;
            }

            if (!string.IsNullOrEmpty(this.GameServerAddress) && this.enterRoomArgumentsCache != null)
            {
                this.lastJoinType = JoinType.JoinRoom;
                this.enterRoomArgumentsCache.JoinMode = JoinMode.RejoinOnly;
                return this.CallConnect(ServerConnection.GameServer);
            }

            return false;
        }


        /// <summary>Disconnects the peer from a server or stays disconnected. If the client / peer was connected, a callback will be triggered.</summary>
        /// <remarks>
        /// Disconnect will attempt to notify the server of the client closing the connection.
        ///
        /// Clients that are in a room, will leave the room. If the room's playerTTL &gt; 0, the player will just become inactive (and may rejoin).
        ///
        /// This method will not change the current State, if this client State is PeerCreated, Disconnecting or Disconnected.
        /// In those cases, there is also no callback for the disconnect. The DisconnectedCause will only change if the client was connected.
        /// </remarks>
        public void Disconnect(DisconnectCause cause = DisconnectCause.DisconnectByClientLogic)
        {
            if (this.State == ClientState.Disconnecting || this.State == ClientState.Disconnected || this.State == ClientState.PeerCreated)
            {
                Log.Info($"Disconnect() skipped because State is: {this.State}. Called for cause: {cause}. Current DisconnectedCause: {this.DisconnectedCause}.", this.LogLevel, this.LogPrefix);
                return;
            }

            this.State = ClientState.Disconnecting;
            this.DisconnectedCause = cause;
            this.RealtimePeer.Disconnect();
        }


        /// <summary>
        /// Private Disconnect variant that sets the state, too.
        /// </summary>
        private void DisconnectToReconnect()
        {
            switch (this.Server)
            {
                case ServerConnection.NameServer:
                    this.State = ClientState.DisconnectingFromNameServer;
                    break;
                case ServerConnection.MasterServer:
                    this.State = ClientState.DisconnectingFromMasterServer;
                    break;
                case ServerConnection.GameServer:
                    this.State = ClientState.DisconnectingFromGameServer;
                    break;
            }

            this.RealtimePeer.Disconnect();
        }

        /// <summary>
        /// Useful to test loss of connection which will end in a client timeout. This modifies RealtimePeer.NetworkSimulationSettings. Read remarks.
        /// </summary>
        /// <remarks>
        /// Use with care as this sets RealtimePeer.IsSimulationEnabled.<br/>
        /// Read RealtimePeer.IsSimulationEnabled to check if this is on or off, if needed.<br/>
        ///
        /// If simulateTimeout is true, RealtimePeer.NetworkSimulationSettings.IncomingLossPercentage and
        /// RealtimePeer.NetworkSimulationSettings.OutgoingLossPercentage will be set to 100.<br/>
        /// Obviously, this overrides any network simulation settings done before.<br/>
        ///
        /// If you want fine-grained network simulation control, use the NetworkSimulationSettings.<br/>
        ///
        /// The timeout will lead to a call to <see cref="IConnectionCallbacks.OnDisconnected"/>, as usual in a client timeout.
        ///
        /// You could modify this method (or use NetworkSimulationSettings) to deliberately run into a server timeout by
        /// just setting the OutgoingLossPercentage = 100 and the IncomingLossPercentage = 0.
        /// </remarks>
        /// <param name="simulateTimeout">If true, a connection loss is simulated. If false, the simulation ends.</param>
        public void SimulateConnectionLoss(bool simulateTimeout)
        {
            Log.Warn("SimulateConnectionLoss() set to: " + simulateTimeout, this.LogLevel, this.LogPrefix);

            if (simulateTimeout)
            {
                this.RealtimePeer.NetworkSimulationSettings.IncomingLossPercentage = 100;
                this.RealtimePeer.NetworkSimulationSettings.OutgoingLossPercentage = 100;
            }

            this.RealtimePeer.IsSimulationEnabled = simulateTimeout;
        }

        /// <summary>
        /// Authenticates user and sends this.CurrentRegion as target region, unless this.AppSettings.FixedRegion overrides it.
        /// </summary>
        /// <returns></returns>
        private bool CallAuthenticate()
        {
            if (this.AppSettings.UseNameServer && this.Server != ServerConnection.NameServer && (this.AuthValues == null || this.AuthValues.Token == null))
            {
                Log.Error(string.Format("Authenticate without Token is only allowed on Name Server. Will not authenticate on {0}: {1}. State: {2}", this.Server, this.CurrentServerAddress, this.State), this.LogLevel, this.LogPrefix);
                return false;
            }

            if (this.AuthValues != null && !this.AuthValues.AreValid())
            {
                Log.Warn($"AuthValues.AuthType is {this.AuthValues.AuthType} but not all mandatory parameters are set. Current GET parameters: {this.AuthValues.AuthGetParameters}", this.LogLevel, this.LogPrefix);
            }

            if (!string.IsNullOrEmpty(this.AppSettings.FixedRegion) && this.Server == ServerConnection.NameServer)
            {
                this.CurrentRegion = this.AppSettings.FixedRegion;
            }

            if (this.AppSettings.AuthMode == AuthModeOption.Auth)
            {
                if (!this.CheckIfOpCanBeSent(OperationCode.Authenticate, this.Server, "Authenticate"))
                {
                    return false;
                }
                return this.OpAuthenticate(this.AppSettings.GetAppId(this.ClientType), this.AppSettings.AppVersion, this.AuthValues, this.CurrentRegion, (this.AppSettings.EnableLobbyStatistics && this.Server == ServerConnection.MasterServer));
            }
            else
            {
                if (!this.CheckIfOpCanBeSent(OperationCode.AuthenticateOnce, this.Server, "AuthenticateOnce"))
                {
                    return false;
                }

                ConnectionProtocol targetProtocolPastNameServer = this.AppSettings.Protocol;    // no matter the current protocol, authenticate should aim for the definition in AppSettings.Protocol
                return this.OpAuthenticateOnce(this.AppSettings.GetAppId(this.ClientType), this.AppSettings.AppVersion, this.AuthValues, this.CurrentRegion, this.EncryptionMode, targetProtocolPastNameServer);
            }
        }


        /// <summary>
        /// This method dispatches all available incoming commands and then sends this client's outgoing commands.
        /// It uses DispatchIncomingCommands and SendOutgoingCommands to do that.
        /// </summary>
        /// <remarks>
        /// The Photon client libraries are designed to fit easily into a game or application. The application
        /// is in control of the context (thread) in which incoming events and responses are executed and has
        /// full control of the creation of UDP/TCP packages.
        ///
        /// Sending packages and dispatching received messages are two separate tasks. Service combines them
        /// into one method at the cost of control. It calls DispatchIncomingCommands and SendOutgoingCommands.
        ///
        /// Call this method regularly (10..50 times a second).
        ///
        /// This will Dispatch ANY received commands (unless a reliable command in-order is still missing) and
        /// events AND will send queued outgoing commands. Fewer calls might be more effective if a device
        /// cannot send many packets per second, as multiple operations might be combined into one package.
        /// </remarks>
        /// <example>
        /// You could replace Service by:
        ///
        ///     while (DispatchIncomingCommands()); //Dispatch until everything is Dispatched...
        ///     SendOutgoingCommands(); //Send a UDP/TCP package with outgoing messages
        /// </example>
        /// <seealso cref="PhotonPeer.DispatchIncomingCommands"/>
        /// <seealso cref="PhotonPeer.SendOutgoingCommands"/>
        public void Service()
        {
            this.RealtimePeer.Service();

            this.LogStats();
        }

        /// <summary>Dispatches a received message (operation response, event, etc) from the queue.</summary>
        /// <returns>If there is anything else queued, so you could call again.</returns>
        public bool DispatchIncomingCommands()
        {
            return this.RealtimePeer.DispatchIncomingCommands();
        }

        /// <summary>Sends messages or a datagram with multiple commands (operations, acks, etc.) from the outgoing queue.</summary>
        /// <returns>If there is anything else queued, so you could call again.</returns>
        public bool SendOutgoingCommands()
        {
            bool res = this.RealtimePeer.SendOutgoingCommands();

            this.LogStats();

            return res;
        }

        /// <summary>Logs vital stats in interval.</summary>
        private void LogStats()
        {
            if (this.LogLevel >= LogLevel.Info && this.LogStatsInterval != 0 && this.State != ClientState.Disconnected)
            {
                int delta = this.RealtimePeer.ConnectionTime - this.lastStatsLogTime;
                if (delta >= this.LogStatsInterval)
                {
                    this.lastStatsLogTime = this.RealtimePeer.ConnectionTime;
                    Log.Info(this.RealtimePeer.VitalStatsToString(false), this.LogLevel, this.LogPrefix);
                }
            }
        }

        #endregion


        #region Helpers


        /// <summary>Logs most important values we need to know for support.</summary>
        /// <param name="prefix">Prefix for this log (method calling this). There are several which will connect.</param>
        /// <returns>String to log.</returns>
        private string ConnectLog(string prefix)
        {
            StringBuilder sb = new StringBuilder();
            string appid = this.AppSettings.GetAppId(this.ClientType);

            string appIdShort = (!string.IsNullOrEmpty(appid) && appid.Length > 8) ? appid.Substring(0, 8) : appid;
            string authType = AuthValues != null ? AuthValues.AuthType.ToString() : "N/A";
            string region = string.IsNullOrEmpty(this.CurrentRegion) ? "" : $"({this.CurrentRegion})";
            string time = DateTime.UtcNow.ToShortTimeString();
            sb.Append($"{prefix} UTC: {time} AppID: \"{appIdShort}***\" AppVersion: \"{this.AppSettings.AppVersion}\" Auth: {authType} Client: v{PhotonPeer.Version} ({this.RealtimePeer.TargetFramework}, {this.RealtimePeer.SocketImplementation.Name}, {this.EncryptionMode}) Server: {this.CurrentServerAddress} {region}");

            return sb.ToString();
        }

        // Sets up the socket implementations to use, depending on platform
        [System.Diagnostics.Conditional("SUPPORTED_UNITY")]
        private void ConfigUnitySockets()
        {
            Type websocketType = null;
            #if (UNITY_XBOXONE || UNITY_GAMECORE) && !UNITY_EDITOR
            websocketType = Type.GetType("ExitGames.Client.Photon.SocketNativeSource, Assembly-CSharp", false);
            if (websocketType == null)
            {
                websocketType = Type.GetType("ExitGames.Client.Photon.SocketNativeSource, Assembly-CSharp-firstpass", false);
            }
            if (websocketType == null)
            {
                websocketType = Type.GetType("ExitGames.Client.Photon.SocketNativeSource, PhotonRealtime", false);
            }
            if (websocketType != null)
            {
                this.RealtimePeer.SocketImplementationConfig[ConnectionProtocol.Udp] = websocketType;    // on Xbox, the native socket plugin supports UDP as well
            }
            #else
            // to support WebGL export in Unity, we find and assign the SocketWebTcp class (if it's in the project).
            // alternatively class SocketWebTcp might be in the Photon3Unity3D.dll
            websocketType = Type.GetType("Photon.Client.SocketWebTcp, PhotonWebSocket", false);
            if (websocketType == null)
            {
                websocketType = Type.GetType("Photon.Client.SocketWebTcp, Assembly-CSharp-firstpass", false);
            }
            if (websocketType == null)
            {
                websocketType = Type.GetType("Photon.Client.SocketWebTcp, Assembly-CSharp", false);
            }
            #if UNITY_WEBGL
            if (websocketType == null && this.LogLevel >= LogLevel.Warning)
            {
                Log.Warn("SocketWebTcp type not found in the usual Assemblies. This is required as wrapper for the browser WebSocket API. Make sure to make the PhotonLibs\\WebSocket code available.", this.LogLevel, this.LogPrefix);
            }
            #endif
            #endif

            if (websocketType != null)
            {
                this.RealtimePeer.SocketImplementationConfig[ConnectionProtocol.WebSocket] = websocketType;
                this.RealtimePeer.SocketImplementationConfig[ConnectionProtocol.WebSocketSecure] = websocketType;
            }

            #if NET_4_6 && (UNITY_EDITOR || !ENABLE_IL2CPP) && !NETFX_CORE
            this.RealtimePeer.SocketImplementationConfig[ConnectionProtocol.Udp] = typeof(SocketUdpAsync);
            this.RealtimePeer.SocketImplementationConfig[ConnectionProtocol.Tcp] = typeof(SocketTcpAsync);
            #endif
        }


        /// <summary>
        /// Gets the NameServer Address (with prefix and port), based on the set protocol (this.RealtimePeer.UsedProtocol).
        /// </summary>
        /// <returns>NameServer Address (with prefix and port).</returns>
        private string GetNameServerAddress()
        {
            var protocolPort = this.ProtocolPorts.Get(this.RealtimePeer.TransportProtocol, ServerConnection.NameServer);

            if (this.AppSettings.UseNameServer && this.AppSettings.Port != 0)
            {
                Log.Info($"NameServer port becomes AppSettings.Port: {this.AppSettings.Port} with protocol: {this.RealtimePeer.TransportProtocol}", this.LogLevel, this.LogPrefix);
                protocolPort = this.AppSettings.Port;
            }

            switch (this.RealtimePeer.TransportProtocol)
            {
                case ConnectionProtocol.Udp:
                case ConnectionProtocol.Tcp:
                    return string.Format("{0}:{1}", NameServerHost, protocolPort);
                case ConnectionProtocol.WebSocket:
                    return string.Format("ws://{0}:{1}", NameServerHost, protocolPort);
                case ConnectionProtocol.WebSocketSecure:
                    return string.Format("wss://{0}:{1}", NameServerHost, protocolPort);
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }


        /// <summary>Internally used to replace a port value in an address with an alternative port (overriding the provided one).</summary>
        protected internal static string ReplacePortWithAlternative(string address, ushort replacementPort, string addressToReplace)
        {
            if (replacementPort == 0)
            {
                return address;
            }

            //Debug.LogWarning("Incoming MasterServer Address: "+this.MasterServerAddress);
            bool webSocket = address.StartsWith("ws");
            if (webSocket)
            {
                UriBuilder urib = new UriBuilder(address);
                urib.Port = replacementPort;
                //Debug.LogWarning("New MasterServer Address: "+this.MasterServerAddress);
                return urib.ToString();
            }
            else
            {
                UriBuilder urib = new UriBuilder($"scheme://{address}");
                //Debug.LogWarning("New MasterServer Address: "+this.MasterServerAddress);
                return string.Format("{0}:{1}", urib.Host, replacementPort);
            }
        }


        /// <summary>Creates a string useful to debug matchmaking. Includes a matchmaking hashcode ("MMH") and the lobby in use.</summary>
        /// <remarks>The matchmaking hashcode is generated from: AppId, AppVersion, CloudRegion and CurrentCluster.</remarks>
        /// <param name="lobbyInArgs">TypedLobby set in the join/create options of an operation.</param>
        /// <returns>Compact debug string for matchmaking.</returns>
        string GetMatchmakingHash(TypedLobby lobbyInArgs)
        {
            string lobbyString = "";
            if (lobbyInArgs != null)
            {
                lobbyString = lobbyInArgs.ToString();
            }
            else if (this.CurrentLobby != null)
            {
                lobbyString = this.CurrentLobby.ToString();
            }
            else
            {
                lobbyString = TypedLobby.Default.ToString();
            }

            string matchmakingHash = $"{this.AppSettings.GetAppId(this.ClientType)}{this.AppSettings.AppVersion}{this.CurrentRegion}{this.CurrentCluster}".GetStableHashCode().ToString("x");
            return $"MMH: {matchmakingHash} {lobbyString}";
        }


        /// <summary>
        /// Privately used to read-out properties coming from the server in events and operation responses (which might be a bit tricky).
        /// </summary>
        private void ReadoutProperties(PhotonHashtable gameProperties, PhotonHashtable actorProperties, int targetActorNr)
        {
            // read game properties and cache them locally
            if (this.CurrentRoom != null && gameProperties != null)
            {
                this.CurrentRoom.InternalCacheProperties(gameProperties);
                if (this.InRoom)
                {
                    this.InRoomCallbackTargets.OnRoomPropertiesUpdate(gameProperties);
                }
            }

            if (actorProperties == null || actorProperties.Count <= 0)
            {
                return;
            }

            if (targetActorNr > 0)
            {
                // we have a single entry in the actorProperties with one user's name
                // targets MUST exist before you set properties
                Player target = this.CurrentRoom.GetPlayer(targetActorNr);
                if (target != null)
                {
                    PhotonHashtable props = this.ReadoutPropertiesForActorNr(actorProperties, targetActorNr);
                    target.InternalCacheProperties(props);
                    this.InRoomCallbackTargets.OnPlayerPropertiesUpdate(target, props);
                }
            }
            else
            {
                // in this case, we've got a key-value pair per actor (each
                // value is a hashtable with the actor's properties then)
                int actorNr;
                Player target;
                string targetNick;
                PhotonHashtable targetProps;

                foreach (object key in actorProperties.Keys)
                {
                    actorNr = (int)key;
                    if (actorNr == 0)
                    {
                        continue;
                    }

                    targetProps = (PhotonHashtable)actorProperties[key];
                    targetNick = (string)targetProps[ActorProperties.NickName];

                    target = this.CurrentRoom.GetPlayer(actorNr);
                    if (target == null)
                    {
                        target = this.CreatePlayer(targetNick, actorNr, false, targetProps);
                        this.CurrentRoom.StorePlayer(target);
                    }
                    target.InternalCacheProperties(targetProps);
                }
            }
        }


        /// <summary>
        /// Privately used only to read properties for a distinct actor (which might be the hashtable OR a key-pair value IN the actorProperties).
        /// </summary>
        private PhotonHashtable ReadoutPropertiesForActorNr(PhotonHashtable actorProperties, int actorNr)
        {
            if (actorProperties.ContainsKey(actorNr))
            {
                return (PhotonHashtable)actorProperties[actorNr];
            }

            return actorProperties;
        }

        /// <summary>
        /// Internally used to set the LocalPlayer's ID (from -1 to the actual in-room ID).
        /// </summary>
        /// <param name="newID">New actor ID (a.k.a actorNr) assigned when joining a room.</param>
        public void ChangeLocalID(int newID)
        {
            if (this.LocalPlayer == null)
            {
                Log.Warn(string.Format("Local actor is null. CurrentRoom: {0} CurrentRoom.Players: {1} newID: {2}", this.CurrentRoom, this.CurrentRoom?.Players == null, newID), this.LogLevel, this.LogPrefix);
            }

            if (this.CurrentRoom == null)
            {
                // change to new actor/player ID and make sure the player does not have a room reference left
                this.LocalPlayer.ChangeLocalID(newID);
                this.LocalPlayer.RoomReference = null;
            }
            else
            {
                // remove old actorId from actor list
                this.CurrentRoom.RemovePlayer(this.LocalPlayer);

                // change to new actor/player ID
                this.LocalPlayer.ChangeLocalID(newID);

                // update the room's list with the new reference
                this.CurrentRoom.StorePlayer(this.LocalPlayer);
            }
        }


        /// <summary>
        /// Called internally, when a game was joined or created on the game server successfully.
        /// </summary>
        /// <remarks>
        /// This reads the response, finds out the local player's actorNumber (a.k.a. Player.ID) and applies properties of the room and players.
        /// Errors for these operations are to be handled before this method is called.
        /// </remarks>
        /// <param name="operationResponse">Contains the server's response for an operation called by this peer.</param>
        private void GameEnteredOnGameServer(OperationResponse operationResponse)
        {
            this.CurrentRoom = this.CreateRoom(this.enterRoomArgumentsCache.RoomName, this.enterRoomArgumentsCache.RoomOptions);
            this.CurrentRoom.RealtimeClient = this;
            this.CurrentRoom.Lobby = this.enterRoomArgumentsCache.Lobby;

            // first change the local id, instead of first updating the actorList since actorList uses ID to update itself

            // the local player's actor-properties are not returned in join-result. add this player to the list
            int localActorNr = (int)operationResponse[ParameterCode.ActorNr];
            this.ChangeLocalID(localActorNr);

            if (operationResponse.Parameters.ContainsKey(ParameterCode.ActorList))
            {
                int[] actorsInRoom = (int[])operationResponse.Parameters[ParameterCode.ActorList];
                this.UpdatedActorList(actorsInRoom);
            }

            if (operationResponse.Parameters.ContainsKey(ParameterCode.PluginName))
            {
                string plugin = (string)operationResponse.Parameters[ParameterCode.PluginName];
                if (!string.Equals(plugin, "webhooks", StringComparison.InvariantCultureIgnoreCase))
                {
                    Log.Info($"GameEnteredOnGameServer() plugin: {plugin}", this.LogLevel, this.LogPrefix);
                }
            }

            PhotonHashtable actorProperties = (PhotonHashtable)operationResponse[ParameterCode.PlayerProperties];
            PhotonHashtable gameProperties = (PhotonHashtable)operationResponse[ParameterCode.GameProperties];
            this.ReadoutProperties(gameProperties, actorProperties, 0);

            object temp;
            if (operationResponse.Parameters.TryGetValue(ParameterCode.RoomOptionFlags, out temp))
            {
                this.CurrentRoom.InternalCacheRoomFlags((int)temp);
            }


            // the callbacks OnCreatedRoom and OnJoinedRoom are called in the event join. it contains important info about the room and players.
            // unless there will be no room events (RoomOptions.SuppressRoomEvents = true)
            if (this.CurrentRoom.SuppressRoomEvents)
            {
                // ClientState.Joined might be set here or by an Event Join
                this.State = ClientState.Joined;
                this.LocalPlayer.UpdateNickNameOnJoined();

                if (this.lastJoinType == JoinType.CreateRoom || (this.lastJoinType == JoinType.JoinOrCreateRoom && this.LocalPlayer.ActorNumber == 1))
                {
                    this.MatchMakingCallbackTargets.OnCreatedRoom();
                }

                this.MatchMakingCallbackTargets.OnJoinedRoom();
            }
        }


        private void UpdatedActorList(int[] actorsInGame)
        {
            if (actorsInGame != null)
            {
                foreach (int actorNumber in actorsInGame)
                {
                    if (actorNumber == 0)
                    {
                        continue;
                    }

                    Player target = this.CurrentRoom.GetPlayer(actorNumber);
                    if (target == null)
                    {
                        this.CurrentRoom.StorePlayer(this.CreatePlayer(string.Empty, actorNumber, false, null));
                    }
                }
            }
        }

        /// <summary>
        /// Factory method to create a player instance - override to get your own player-type with custom features.
        /// </summary>
        /// <param name="nickName">The nickname of the player to be created.</param>
        /// <param name="actorNumber">The player ID (a.k.a. actorNumber) of the player to be created.</param>
        /// <param name="isLocal">Sets the distinction if the player to be created is your player or if its assigned to someone else.</param>
        /// <param name="actorProperties">The custom properties for this new player</param>
        /// <returns>The newly created player</returns>
        protected internal virtual Player CreatePlayer(string nickName, int actorNumber, bool isLocal, PhotonHashtable actorProperties)
        {
            Player newPlayer = new Player(nickName, actorNumber, isLocal, actorProperties);
            return newPlayer;
        }

        /// <summary>Internal "factory" method to create a room-instance.</summary>
        protected internal virtual Room CreateRoom(string roomName, RoomOptions opt)
        {
            Room r = new Room(roomName, opt);
            return r;
        }

        private bool CheckIfOpAllowedOnServer(byte opCode, ServerConnection serverConnection)
        {
            switch (serverConnection)
            {
                case ServerConnection.MasterServer:
                    switch (opCode)
                    {
                        case OperationCode.CreateGame:
                        case OperationCode.Authenticate:
                        case OperationCode.AuthenticateOnce:
                        case OperationCode.FindFriends:
                        case OperationCode.GetGameList:
                        case OperationCode.GetLobbyStats:
                        case OperationCode.JoinGame:
                        case OperationCode.JoinLobby:
                        case OperationCode.LeaveLobby:
                        case OperationCode.ServerSettings:
                        case OperationCode.JoinRandomGame:
                            return true;
                    }
                    break;
                case ServerConnection.GameServer:
                    switch (opCode)
                    {
                        case OperationCode.CreateGame:
                        case OperationCode.Authenticate:
                        case OperationCode.AuthenticateOnce:
                        case OperationCode.ChangeGroups:
                        case OperationCode.GetProperties:
                        case OperationCode.JoinGame:
                        case OperationCode.Leave:
                        case OperationCode.ServerSettings:
                        case OperationCode.SetProperties:
                        case OperationCode.RaiseEvent:
                            return true;
                    }
                    break;
                case ServerConnection.NameServer:
                    switch (opCode)
                    {
                        case OperationCode.Authenticate:
                        case OperationCode.AuthenticateOnce:
                        case OperationCode.GetRegions:
                        case OperationCode.ServerSettings:
                            return true;
                    }
                    break;
                default:
                    throw new ArgumentOutOfRangeException("serverConnection", serverConnection, null);
            }
            return false;
        }

        private bool CheckIfOpCanBeSent(byte opCode, ServerConnection serverConnection, string opName)
        {
            if (!this.CheckIfOpAllowedOnServer(opCode, serverConnection))
            {
                Log.Error(string.Format("Operation {0} ({1}) not allowed on current server ({2})", opName, opCode, serverConnection), this.LogLevel, this.LogPrefix);
                return false;
            }

            if (!this.CheckIfClientIsReadyToCallOperation(opCode))
            {
                if (opCode == OperationCode.RaiseEvent && (this.State == ClientState.Leaving || this.State == ClientState.Disconnecting || this.State == ClientState.DisconnectingFromGameServer))
                {
                    Log.Info(string.Format("Operation {0} ({1}) not called while leaving the room or game server. Client state: {2}", opName, opCode, Enum.GetName(typeof(ClientState), this.State)), this.LogLevel, this.LogPrefix);
                    return false;
                }

                Log.Error(string.Format("Operation {0} ({1}) not called because client is not connected or not ready yet. Client state: {2}", opName, opCode, Enum.GetName(typeof(ClientState), this.State)), this.LogLevel, this.LogPrefix);
                return false;
            }

            if (this.RealtimePeer.PeerState != PeerStateValue.Connected)
            {
                Log.Error(string.Format("Operation {0} ({1}) can't be sent because peer is not connected. Peer state: {2}", opName, opCode, this.RealtimePeer.PeerState), this.LogLevel, this.LogPrefix);
                return false;
            }
            return true;
        }

        private bool CheckIfClientIsReadyToCallOperation(byte opCode)
        {
            switch (opCode)
            {
                //case OperationCode.ServerSettings: // ??

                case OperationCode.Authenticate:
                case OperationCode.AuthenticateOnce:
                    return this.IsConnectedAndReady ||
                         this.State == ClientState.ConnectingToNameServer || // this is required since we do not set state to ConnectedToNameServer before authentication
                        this.State == ClientState.ConnectingToMasterServer || // this is required since we do not set state to ConnectedToMasterServer before authentication
                        this.State == ClientState.ConnectingToGameServer; // this is required since we do not set state to ConnectedToGameServer before authentication

                case OperationCode.ChangeGroups:
                case OperationCode.GetProperties:
                case OperationCode.SetProperties:
                case OperationCode.RaiseEvent:
                case OperationCode.Leave:
                    return this.InRoom;

                case OperationCode.JoinGame:
                case OperationCode.CreateGame:
                    return this.State == ClientState.ConnectedToMasterServer || this.InLobby || this.State == ClientState.ConnectedToGameServer; // CurrentRoom can be not null in case of quick rejoin

                case OperationCode.LeaveLobby:
                    return this.InLobby;

                case OperationCode.JoinRandomGame:
                case OperationCode.FindFriends:
                case OperationCode.GetGameList:
                case OperationCode.GetLobbyStats:
                case OperationCode.JoinLobby: // You don't have to explicitly leave a lobby to join another (client can be in one max, at any time)
                    return this.State == ClientState.ConnectedToMasterServer;
                case OperationCode.GetRegions:
                    return this.State == ClientState.ConnectedToNameServer;
            }
            return this.IsConnected;
        }

        #endregion

        #region Implementation of IPhotonPeerListener

        /// <summary>Debug output handling for the PhotonPeer. Use RealtimePeer.LogLevel if you want to check if a message should get logged.</summary>
        /// <remarks>The RealtimePeer will internally check its log level before writing any messages, so likely this just logs anything that comes through.</remarks>
        public virtual void DebugReturn(LogLevel level, string message)
        {
            switch (level)
            {
                case LogLevel.Off:
                    break;
                case LogLevel.Error:
                    Log.Error(message, this.RealtimePeer.LogLevel, this.LogPrefix);
                    break;
                case LogLevel.Warning:
                    Log.Warn(message, this.RealtimePeer.LogLevel, this.LogPrefix);
                    break;
                case LogLevel.Info:
                    Log.Info(message, this.RealtimePeer.LogLevel, this.LogPrefix);
                    break;
                case LogLevel.Debug:
                default:
                    Log.Debug(message, this.RealtimePeer.LogLevel, this.LogPrefix);
                    break;
            }
        }


        private void CallbackRoomEnterFailed(OperationResponse operationResponse)
        {
            if (operationResponse.ReturnCode != 0)
            {
                if (operationResponse.OperationCode == OperationCode.JoinGame)
                {
                    this.MatchMakingCallbackTargets.OnJoinRoomFailed(operationResponse.ReturnCode, operationResponse.DebugMessage);
                }
                else if (operationResponse.OperationCode == OperationCode.CreateGame)
                {
                    this.MatchMakingCallbackTargets.OnCreateRoomFailed(operationResponse.ReturnCode, operationResponse.DebugMessage);
                }
                else if (operationResponse.OperationCode == OperationCode.JoinRandomGame)
                {
                    this.MatchMakingCallbackTargets.OnJoinRandomFailed(operationResponse.ReturnCode, operationResponse.DebugMessage);
                }
            }
        }

        /// <summary>
        /// Uses the OperationResponses provided by the server to advance the internal state and call ops as needed.
        /// </summary>
        /// <remarks>
        /// When this method finishes, it will call your OnOpResponseAction (if any). This way, you can get any
        /// operation response without overriding this class.
        ///
        /// To implement a more complex game/app logic, you should implement your own class that inherits the
        /// RealtimeClient. Override this method to use your own operation-responses easily.
        ///
        /// This method is essential to update the internal state of a RealtimeClient, so overriding methods
        /// must call base.OnOperationResponse().
        /// </remarks>
        /// <param name="operationResponse">Contains the server's response for an operation called by this peer.</param>
        public virtual void OnOperationResponse(OperationResponse operationResponse)
        {
            // if (operationResponse.ReturnCode != 0) this.DebugReturn(LogLevel.Error, operationResponse.ToStringFull());

            // use the "secret" or "token" whenever we get it. doesn't really matter if it's in AuthResponse.
            if (operationResponse.Parameters.ContainsKey(ParameterCode.Token))
            {
                if (this.AuthValues == null)
                {
                    this.AuthValues = new AuthenticationValues();
                }

                this.AuthValues.Token = operationResponse.Parameters[ParameterCode.Token];
            }

            // if the operation limit was reached, disconnect (but still execute the operation response).
            if (operationResponse.ReturnCode == ErrorCode.OperationLimitReached)
            {
                this.Disconnect(DisconnectCause.DisconnectByOperationLimit);
            }

            switch (operationResponse.OperationCode)
            {
                //case OperationCode.SetProperties:
                //    Log.Info($"OnOperationResponse case SetProperties {operationResponse.ToStringFull()}", this.LogLevel, this.LogPrefix);
                //    break;
                case OperationCode.Authenticate:
                case OperationCode.AuthenticateOnce:
                    {
                        if (operationResponse.ReturnCode != 0)
                        {
                            Log.Error(string.Format("{0} Server: {1} Address: {2}", operationResponse.ToStringFull(), this.Server, this.RealtimePeer.ServerAddress), this.LogLevel, this.LogPrefix);

                            switch (operationResponse.ReturnCode)
                            {
                                case ErrorCode.InvalidAuthentication:
                                    this.DisconnectedCause = DisconnectCause.InvalidAuthentication;
                                    break;
                                case ErrorCode.CustomAuthenticationFailed:
                                    this.DisconnectedCause = DisconnectCause.CustomAuthenticationFailed;
                                    this.ConnectionCallbackTargets.OnCustomAuthenticationFailed(operationResponse.DebugMessage);
                                    break;
                                case ErrorCode.InvalidRegion:
                                    this.DisconnectedCause = DisconnectCause.InvalidRegion;
                                    break;
                                case ErrorCode.MaxCcuReached:
                                    this.DisconnectedCause = DisconnectCause.MaxCcuReached;
                                    break;
                                case ErrorCode.InvalidOperation:
                                case ErrorCode.OperationNotAllowedInCurrentState:
                                    this.DisconnectedCause = DisconnectCause.OperationNotAllowedInCurrentState;
                                    break;
                                case ErrorCode.AuthenticationTicketExpired:
                                    this.DisconnectedCause = DisconnectCause.AuthenticationTicketExpired;
                                    break;
                            }

                            this.Disconnect(this.DisconnectedCause);
                            break;  // if auth didn't succeed, we disconnect (above) and exit this operation's handling
                        }

                        if (this.Server == ServerConnection.NameServer || this.Server == ServerConnection.MasterServer)
                        {
                            if (operationResponse.Parameters.ContainsKey(ParameterCode.UserId))
                            {
                                string incomingId = (string)operationResponse.Parameters[ParameterCode.UserId];
                                if (!string.IsNullOrEmpty(incomingId))
                                {
                                    this.UserId = incomingId;
                                    this.LocalPlayer.UserId = incomingId;
                                    Log.Info($"Server sets UserID: {this.UserId}", this.LogLevel, this.LogPrefix);
                                }
                            }
                            if (operationResponse.Parameters.ContainsKey(ParameterCode.NickName))
                            {
                                this.NickName = (string)operationResponse.Parameters[ParameterCode.NickName];
                                Log.Info($"Server sets NickName: {this.NickName}", this.LogLevel, this.LogPrefix);
                            }

                            if (operationResponse.Parameters.ContainsKey(ParameterCode.EncryptionData))
                            {
                                this.SetupEncryption((Dictionary<byte, object>)operationResponse.Parameters[ParameterCode.EncryptionData]);
                            }
                        }

                        if (this.Server == ServerConnection.NameServer)
                        {
                            string receivedCluster = operationResponse[ParameterCode.Cluster] as string;
                            if (!string.IsNullOrEmpty(receivedCluster))
                            {
                                this.CurrentCluster = receivedCluster;
                            }

                            // on the NameServer, authenticate returns the MasterServer address for a region and we hop off to there
                            this.MasterServerAddress = operationResponse[ParameterCode.Address] as string;


                            if (this.AppSettings.AuthMode == AuthModeOption.AuthOnceWss && this.RealtimePeer.TransportProtocol != this.AppSettings.Protocol)
                            {
                                Log.Info($"AuthOnceWss response switches TransportProtocol to: {this.AppSettings.Protocol}.", this.LogLevel, this.LogPrefix);
                                this.RealtimePeer.TransportProtocol = this.AppSettings.Protocol;
                            }

                            ushort replacementPort = this.ProtocolPorts.Get(this.RealtimePeer.TransportProtocol, ServerConnection.MasterServer);
                            if (replacementPort != 0)
                            {
                                this.MasterServerAddress = ReplacePortWithAlternative(this.MasterServerAddress, replacementPort, "MasterServerAddress");
                            }

                            this.DisconnectToReconnect();
                        }
                        else if (this.Server == ServerConnection.MasterServer)
                        {
                            this.State = ClientState.ConnectedToMasterServer;
                            if (this.failedRoomEntryOperation == null)
                            {
                                this.ConnectionCallbackTargets.OnConnectedToMaster();
                            }
                            else
                            {
                                this.CallbackRoomEnterFailed(this.failedRoomEntryOperation);
                                this.failedRoomEntryOperation = null;
                            }

                            if (this.AppSettings.AuthMode != AuthModeOption.Auth)
                            {
                                this.OpSettings(this.AppSettings.EnableLobbyStatistics);
                            }
                        }
                        else if (this.Server == ServerConnection.GameServer)
                        {
                            this.State = ClientState.Joining;
                            this.enterRoomArgumentsCache.OnGameServer = true;

                            if (this.lastJoinType == JoinType.JoinRoom || this.lastJoinType == JoinType.JoinRandomRoom  || this.lastJoinType == JoinType.JoinRandomOrCreateRoom || this.lastJoinType == JoinType.JoinOrCreateRoom)
                            {
                                this.OpJoinRoomIntern(this.enterRoomArgumentsCache);
                            }
                            else if (this.lastJoinType == JoinType.CreateRoom)
                            {
                                this.OpCreateRoomIntern(this.enterRoomArgumentsCache);
                            }
                            break;
                        }

                        // optionally, OpAuth may return some data for the client to use. if it's available, call OnCustomAuthenticationResponse
                        Dictionary<string, object> data = (Dictionary<string, object>)operationResponse[ParameterCode.Data];
                        if (data != null)
                        {
                            this.ConnectionCallbackTargets.OnCustomAuthenticationResponse(data);
                        }
                        break;
                    }

                case OperationCode.GetRegions:
                    if (operationResponse.ReturnCode == ErrorCode.InvalidAuthentication)
                    {
                        Log.Error(string.Format("GetRegions failed. AppId is unknown on the (cloud) server. "+operationResponse.DebugMessage), this.LogLevel, this.LogPrefix);
                        this.Disconnect(DisconnectCause.InvalidAuthentication);
                        break;
                    }
                    if (operationResponse.ReturnCode != ErrorCode.Ok)
                    {
                        Log.Error(string.Format("GetRegions failed. Can't provide regions list. ReturnCode: {0}: {1}", operationResponse.ReturnCode, operationResponse.DebugMessage), this.LogLevel, this.LogPrefix);
                        this.Disconnect(DisconnectCause.InvalidAuthentication);
                        break;
                    }
                    if (this.RegionHandler == null)
                    {
                        this.RegionHandler = new RegionHandler(this.ProtocolPorts.Get(ConnectionProtocol.Udp, ServerConnection.MasterServer));
                    }

                    if (this.RegionHandler.IsPinging)
                    {
                        // in this particular case, we suppress the duplicate GetRegion response. we don't want a callback for this, cause there is a warning already.
                        Log.Warn("Received response for OpGetRegions while the RegionHandler is pinging regions already. Skipping this response in favor of completing the current region-pinging.", this.LogLevel, this.LogPrefix);
                        return;
                    }

                    this.RegionHandler.SetRegions(operationResponse);

                    if (this.clientWorkflow == ClientWorkflowOption.GetRegionsOnly)
                    {
                        this.Disconnect();
                        this.ConnectionCallbackTargets.OnRegionListReceived(this.RegionHandler);
                        return;
                    }

                    if (this.clientWorkflow == ClientWorkflowOption.Default)
                    {
                        this.ConnectionCallbackTargets.OnRegionListReceived(this.RegionHandler);
                    }

                    // unless the client got disconnected (e.g. in OnRegionListReceived) it should ping the relevant regions (it should have no FixedRegion, as it got the regions in the first place)
                    if (this.State == ClientState.ConnectedToNameServer)
                    {
                        // ping minimal regions (if one is known) and connect
                        this.RegionHandler.PingMinimumOfRegions(this.OnRegionPingCompleted, this.AppSettings.BestRegionSummaryFromStorage);
                    }
                    break;

                case OperationCode.JoinRandomGame:  // this happens only on the master server. on gameserver this is a "regular" join
                case OperationCode.CreateGame:
                case OperationCode.JoinGame:

                    if (operationResponse.ReturnCode != 0)
                    {
                        if (this.Server == ServerConnection.GameServer)
                        {
                            this.failedRoomEntryOperation = operationResponse;
                            this.DisconnectToReconnect();
                        }
                        else
                        {
                            this.State = (this.InLobby) ? ClientState.JoinedLobby : ClientState.ConnectedToMasterServer;    // TODO: JoinedLobby state is immediately lost when joining / creating rooms. This won't restore it.
                            this.CallbackRoomEnterFailed(operationResponse);
                        }
                    }
                    else
                    {
                        if (this.Server == ServerConnection.GameServer)
                        {
                            this.GameEnteredOnGameServer(operationResponse);
                        }
                        else
                        {
                            this.GameServerAddress = (string)operationResponse[ParameterCode.Address];

                            ushort replacementPort = this.ProtocolPorts.Get(this.RealtimePeer.TransportProtocol, ServerConnection.GameServer);
                            if (replacementPort != 0)
                            {
                                this.GameServerAddress = ReplacePortWithAlternative(this.GameServerAddress, replacementPort, "GameServerAddress");
                            }

                            string roomName = operationResponse[ParameterCode.RoomName] as string;
                            if (!string.IsNullOrEmpty(roomName))
                            {
                                this.enterRoomArgumentsCache.RoomName = roomName;
                            }

                            this.DisconnectToReconnect();
                        }
                    }
                    break;

                case OperationCode.GetGameList:
                    if (operationResponse.ReturnCode != 0)
                    {
                        Log.Error("GetGameList failed: " + operationResponse.ToStringFull(), this.LogLevel, this.LogPrefix);
                        break;
                    }

                    List<RoomInfo> _RoomInfoList = new List<RoomInfo>();

                    PhotonHashtable games = (PhotonHashtable)operationResponse[ParameterCode.GameList];
                    foreach (string gameName in games.Keys)
                    {
                        _RoomInfoList.Add(new RoomInfo(gameName, (PhotonHashtable)games[gameName]));
                    }

                    this.LobbyCallbackTargets.OnRoomListUpdate(_RoomInfoList);
                    break;

                case OperationCode.JoinLobby:
                    this.CurrentLobby = this.targetLobbyCache;
                    this.targetLobbyCache = null;
                    this.State = ClientState.JoinedLobby;
                    this.LobbyCallbackTargets.OnJoinedLobby();
                    break;

                case OperationCode.LeaveLobby:
                    this.CurrentLobby = null;
                    this.targetLobbyCache = null;
                    this.State = ClientState.ConnectedToMasterServer;
                    this.LobbyCallbackTargets.OnLeftLobby();
                    break;

                case OperationCode.Leave:
                    this.DisconnectToReconnect();
                    break;

                case OperationCode.FindFriends:
                    if (operationResponse.ReturnCode != 0)
                    {
                        Log.Error("OpFindFriends failed: " + operationResponse.ToStringFull(), this.LogLevel, this.LogPrefix);
                        this.friendListRequested = null;
                        break;
                    }

                    bool[] onlineList = operationResponse[ParameterCode.FindFriendsResponseOnlineList] as bool[];
                    string[] roomList = operationResponse[ParameterCode.FindFriendsResponseRoomIdList] as string[];

                    List<FriendInfo> friendList = new List<FriendInfo>(this.friendListRequested.Length);
                    for (int index = 0; index < this.friendListRequested.Length; index++)
                    {
                        FriendInfo friend = new FriendInfo();
                        friend.UserId = this.friendListRequested[index];
                        friend.Room = roomList[index];
                        friend.IsOnline = onlineList[index];
                        friendList.Insert(index, friend);
                    }

                    this.friendListRequested = null;

                    this.MatchMakingCallbackTargets.OnFriendListUpdate(friendList);
                    break;
            }

            if (this.OpResponseReceived != null) this.OpResponseReceived(operationResponse);
        }

        /// <summary>
        /// Uses the connection's statusCodes to advance the internal state and call operations as needed.
        /// </summary>
        /// <remarks>This method is essential to update the internal state of a RealtimeClient. Overriding methods must call base.OnStatusChanged.</remarks>
        public virtual void OnStatusChanged(StatusCode statusCode)
        {
            switch (statusCode)
            {
                case StatusCode.Connect:
                    if (this.LogLevel >= LogLevel.Debug)
                    {
                        this.lastStatsLogTime = this.RealtimePeer.ConnectionTime;
                    }

                    if (this.State == ClientState.ConnectingToNameServer)
                    {
                        this.Server = ServerConnection.NameServer;
                    }

                    if (this.State == ClientState.ConnectingToGameServer)
                    {
                        this.Server = ServerConnection.GameServer;
                    }

                    if (this.State == ClientState.ConnectingToMasterServer)
                    {
                        this.Server = ServerConnection.MasterServer;
                        this.ConnectionCallbackTargets.OnConnected(); // if initial connect
                    }


                    Log.Info($"Connected to {this.Server}: {this.CurrentServerAddress} ({this.CurrentRegion}) PeerID: {this.RealtimePeer.PeerID}", this.LogLevel, this.LogPrefix);

                    if (this.RealtimePeer.TransportProtocol != ConnectionProtocol.WebSocketSecure)
                    {
                        if (this.Server == ServerConnection.NameServer || this.AppSettings.AuthMode == AuthModeOption.Auth)
                        {
                            this.RealtimePeer.EstablishEncryption();
                        }
                    }
                    else
                    {
                        goto case StatusCode.EncryptionEstablished;
                    }

                    break;

                case StatusCode.EncryptionEstablished:
                    if (this.Server == ServerConnection.NameServer)
                    {
                        this.State = ClientState.ConnectedToNameServer;

                        // if there is no specific region to connect to, get available regions from the Name Server. the result triggers next actions in workflow
                        // GetRegions never has a FixedRegion in the AppSettings copy, so this also gets the regions
                        if (string.IsNullOrEmpty(this.AppSettings.FixedRegion))
                        {
                            this.OpGetRegions();
                            break;
                        }
                    }
                    else
                    {
                        // auth AuthOnce, no explicit authentication is needed on Master Server and Game Server. this is done via token, so: break
                        if (this.AppSettings.AuthMode == AuthModeOption.AuthOnce || this.AppSettings.AuthMode == AuthModeOption.AuthOnceWss)
                        {
                            break;
                        }
                    }

                    // authenticate in all other cases (using the CloudRegion, if available)
                    bool authenticating = this.CallAuthenticate();
                    if (authenticating)
                    {
                        this.State = ClientState.Authenticating;
                    }
                    else
                    {
                        Log.Error($"OpAuthenticate failed. Check log output and AuthValues. State: {this.State}", this.LogLevel, this.LogPrefix);
                    }
                    break;

                case StatusCode.Disconnect:
                    // disconnect due to connection exception is handled below (don't connect to GS or master in that case)
                    this.friendListRequested = null;

                    bool wasInRoom = this.CurrentRoom != null;
                    this.CurrentRoom = null;    // players get cleaned up inside this, too, except LocalPlayer (which we keep)
                    this.ChangeLocalID(-1);     // depends on this.CurrentRoom, so it must be called after updating that

                    this.CurrentLobby = null;
                    this.targetLobbyCache = null;

                    if (this.Server == ServerConnection.GameServer && wasInRoom)
                    {
                        this.MatchMakingCallbackTargets.OnLeftRoom();
                    }

                    if (this.RealtimePeer.TransportProtocol != this.AppSettings.Protocol)
                    {
                        Log.Info($"Disconnect switches TransportProtocol to: {this.AppSettings.Protocol}.", this.LogLevel, this.LogPrefix);
                        this.RealtimePeer.TransportProtocol = this.AppSettings.Protocol;
                    }

                    switch (this.State)
                    {
                        case ClientState.ConnectWithFallbackProtocol:
                            this.AppSettings.EnableProtocolFallback = false;        // the client does a fallback only one time
                            this.RealtimePeer.TransportProtocol = (this.RealtimePeer.TransportProtocol == ConnectionProtocol.Tcp) ? ConnectionProtocol.Udp : ConnectionProtocol.Tcp;
                            this.AppSettings.UseNameServer = true;                 // this does not affect the ServerSettings file, just this RealtimeClient
                            this.AppSettings.Port = 0;                             // this does not affect the ServerSettings file, just this RealtimeClient

                            if (!this.RealtimePeer.Connect(this.NameServerAddress, this.AppSettings.GetAppId(this.ClientType), photonToken: this.TokenForInit, proxyServerAddress: this.AppSettings.ProxyServer))
                            {
                                return;
                            }
                            this.State = ClientState.ConnectingToNameServer;
                            break;
                        case ClientState.PeerCreated:
                        case ClientState.Disconnecting:
                            this.Handler.StopFallbackSendAckThread();
                            this.State = ClientState.Disconnected;
                            this.ConnectionCallbackTargets.OnDisconnected(this.DisconnectedCause);
                            break;

                        case ClientState.DisconnectingFromNameServer:
                        case ClientState.DisconnectingFromGameServer:
                            this.CallConnect(ServerConnection.MasterServer);    // gets the client to the Master Server
                            break;

                        case ClientState.DisconnectingFromMasterServer:
                            this.CallConnect(ServerConnection.GameServer);      // connects the client with the Game Server (when joining/creating a room)
                            break;

                        case ClientState.Disconnected:
                            // this client is already Disconnected, so no further action is needed.
                            // this.DebugReturn(LogLevel.Info, "LBC.OnStatusChanged(Disconnect) this.State: " + this.State + ". Server: " + this.Server);
                            break;

                        default:
                            string stacktrace = "";
                            #if DEBUG && !NETFX_CORE
                            stacktrace = new System.Diagnostics.StackTrace(true).ToString();
                            #endif
                            Log.Warn(string.Format("Unexpected Disconnect. State: {0}. {1}: {2} Trace: {3}", this.State, this.Server, this.CurrentServerAddress, stacktrace), this.LogLevel, this.LogPrefix);

                            this.Handler.StopFallbackSendAckThread();
                            this.State = ClientState.Disconnected;
                            this.ConnectionCallbackTargets.OnDisconnected(this.DisconnectedCause);
                            break;
                    }
                    break;

                case StatusCode.DisconnectByServerUserLimit:
                    Log.Error("MaxCcuReached. Connection rejected due to the AppId CCU limit.", this.LogLevel, this.LogPrefix);
                    this.DisconnectedCause = DisconnectCause.MaxCcuReached;
                    this.State = ClientState.Disconnecting;
                    break;
                case StatusCode.DnsExceptionOnConnect:
                    this.DisconnectedCause = DisconnectCause.DnsExceptionOnConnect;
                    this.State = ClientState.Disconnecting;
                    break;
                case StatusCode.ServerAddressInvalid:
                    this.DisconnectedCause = DisconnectCause.ServerAddressInvalid;
                    this.State = ClientState.Disconnecting;
                    break;
                case StatusCode.ExceptionOnConnect:
                case StatusCode.SecurityExceptionOnConnect:
                case StatusCode.EncryptionFailedToEstablish:
                    this.SystemConnectionSummary = new SystemConnectionSummary(this);
                    this.DisconnectedCause = DisconnectCause.ExceptionOnConnect;

                    // if enabled, the client can attempt to connect with another networking-protocol to check if that connects
                    if (this.AppSettings.EnableProtocolFallback && this.State == ClientState.ConnectingToNameServer)
                    {
                        this.State = ClientState.ConnectWithFallbackProtocol;
                    }
                    else
                    {
                        this.State = ClientState.Disconnecting;
                    }
                    break;
                case StatusCode.Exception:
                case StatusCode.ExceptionOnReceive:
                case StatusCode.SendError:
                    this.SystemConnectionSummary = new SystemConnectionSummary(this);
                    this.DisconnectedCause = DisconnectCause.Exception;
                    this.State = ClientState.Disconnecting;
                    break;
                case StatusCode.DisconnectByServerTimeout:
                    this.SystemConnectionSummary = new SystemConnectionSummary(this);
                    this.DisconnectedCause = DisconnectCause.ServerTimeout;
                    this.State = ClientState.Disconnecting;
                    break;
                case StatusCode.DisconnectByServerLogic:
                    this.DisconnectedCause = DisconnectCause.DisconnectByServerLogic;
                    this.State = ClientState.Disconnecting;
                    break;
                case StatusCode.DisconnectByServerReasonUnknown:
                    this.DisconnectedCause = DisconnectCause.DisconnectByServerReasonUnknown;
                    this.State = ClientState.Disconnecting;
                    break;
                case StatusCode.TimeoutDisconnect:
                    this.SystemConnectionSummary = new SystemConnectionSummary(this);
                    this.DisconnectedCause = DisconnectCause.ClientTimeout;

                    // if enabled, the client can attempt to connect with another networking-protocol to check if that connects
                    if (this.AppSettings.EnableProtocolFallback && this.State == ClientState.ConnectingToNameServer)
                    {
                        this.State = ClientState.ConnectWithFallbackProtocol;
                    }
                    else
                    {
                        this.State = ClientState.Disconnecting;
                    }
                    break;
            }
        }


        /// <summary>
        /// Uses the photonEvent's provided by the server to advance the internal state and call ops as needed.
        /// </summary>
        /// <remarks>This method is essential to update the internal state of a RealtimeClient. Overriding methods must call base.OnEvent.</remarks>
        public virtual void OnEvent(EventData photonEvent)
        {
            int actorNr = photonEvent.Sender;
            Player originatingPlayer = (this.CurrentRoom != null) ? this.CurrentRoom.GetPlayer(actorNr) : null;

            switch (photonEvent.Code)
            {
                case EventCode.GameList:
                case EventCode.GameListUpdate:
                    List<RoomInfo> _RoomInfoList = new List<RoomInfo>();

                    PhotonHashtable games = (PhotonHashtable)photonEvent[ParameterCode.GameList];
                    foreach (string gameName in games.Keys)
                    {
                        _RoomInfoList.Add(new RoomInfo(gameName, (PhotonHashtable)games[gameName]));
                    }

                    this.LobbyCallbackTargets.OnRoomListUpdate(_RoomInfoList);

                    break;

                case EventCode.Join:
                    PhotonHashtable actorProperties = (PhotonHashtable)photonEvent[ParameterCode.PlayerProperties];

                    if (originatingPlayer == null)
                    {
                        if (actorNr > 0)
                        {
                            originatingPlayer = this.CreatePlayer(string.Empty, actorNr, false, actorProperties);
                            this.CurrentRoom.StorePlayer(originatingPlayer);
                        }
                    }
                    else
                    {
                        originatingPlayer.InternalCacheProperties(actorProperties);
                        originatingPlayer.IsInactive = false;
                        originatingPlayer.HasRejoined = actorNr != this.LocalPlayer.ActorNumber;    // event is for non-local player, who is known (by ActorNumber), so it's a returning player
                    }

                    if (actorNr == this.LocalPlayer.ActorNumber)
                    {
                        // in this player's own join event, we get a complete list of players in the room, so check if we know each of the
                        int[] actorsInRoom = (int[])photonEvent[ParameterCode.ActorList];
                        this.UpdatedActorList(actorsInRoom);

                        // any operation that does a "rejoin" will set this value to true. this can indicate if the local player returns to a room.
                        originatingPlayer.HasRejoined = this.enterRoomArgumentsCache.JoinMode == JoinMode.RejoinOnly;

                        // ClientState.Joined might be set here or by the operation response for Join or Create room
                        this.State = ClientState.Joined;
                        this.LocalPlayer.UpdateNickNameOnJoined();

                        // joinWithCreateOnDemand can turn an OpJoin into creating the room. Then actorNumber is 1 and callback: OnCreatedRoom()
                        if (this.lastJoinType == JoinType.CreateRoom || (this.lastJoinType == JoinType.JoinOrCreateRoom && this.LocalPlayer.ActorNumber == 1))
                        {
                            this.MatchMakingCallbackTargets.OnCreatedRoom();
                        }

                        this.MatchMakingCallbackTargets.OnJoinedRoom();
                    }
                    else
                    {
                        this.InRoomCallbackTargets.OnPlayerEnteredRoom(originatingPlayer);
                    }
                    break;

                case EventCode.Leave:
                    if (originatingPlayer != null)
                    {
                        bool isInactive = false;
                        if (photonEvent.Parameters.ContainsKey(ParameterCode.IsInactive))
                        {
                            isInactive = (bool)photonEvent.Parameters[ParameterCode.IsInactive];
                        }

                        originatingPlayer.IsInactive = isInactive;
                        originatingPlayer.HasRejoined = false;

                        if (!isInactive)
                        {
                            this.CurrentRoom.RemovePlayer(actorNr);
                        }
                    }

                    if (photonEvent.Parameters.ContainsKey(ParameterCode.MasterClientId))
                    {
                        int newMaster = (int)photonEvent[ParameterCode.MasterClientId];
                        if (newMaster != 0)
                        {
                            this.CurrentRoom.MasterClientId = newMaster;
                            this.InRoomCallbackTargets.OnMasterClientSwitched(this.CurrentRoom.GetPlayer(newMaster));
                        }
                    }
                    // finally, send notification that a player left
                    this.InRoomCallbackTargets.OnPlayerLeftRoom(originatingPlayer);
                    break;

                case EventCode.PropertiesChanged:
                    // whenever properties are sent in-room, they can be broadcast as event (which we handle here)
                    // we get PLAYERproperties if actorNr > 0 or ROOMproperties if actorNumber is not set or 0
                    int targetActorNr = 0;
                    if (photonEvent.Parameters.ContainsKey(ParameterCode.TargetActorNr))
                    {
                        targetActorNr = (int)photonEvent[ParameterCode.TargetActorNr];
                    }

                    PhotonHashtable gameProperties = null;
                    PhotonHashtable actorProps = null;
                    if (targetActorNr == 0)
                    {
                        gameProperties = (PhotonHashtable)photonEvent[ParameterCode.Properties];
                    }
                    else
                    {
                        actorProps = (PhotonHashtable)photonEvent[ParameterCode.Properties];
                    }

                    this.ReadoutProperties(gameProperties, actorProps, targetActorNr);
                    break;

                case EventCode.AppStats:
                    // only the master server sends these in (1 minute) intervals
                    this.PlayersInRoomsCount = (int)photonEvent[ParameterCode.PeerCount];
                    this.RoomsCount = (int)photonEvent[ParameterCode.GameCount];
                    this.PlayersOnMasterCount = (int)photonEvent[ParameterCode.MasterPeerCount];
                    break;

                case EventCode.LobbyStats:
                    string[] names = photonEvent[ParameterCode.LobbyName] as string[];
                    int[] peers = photonEvent[ParameterCode.PeerCount] as int[];
                    int[] rooms = photonEvent[ParameterCode.GameCount] as int[];

                    byte[] types;
                    ByteArraySlice slice = photonEvent[ParameterCode.LobbyType] as ByteArraySlice;
                    bool useByteArraySlice = slice != null;

                    if (useByteArraySlice)
                    {
                        types = slice.Buffer;
                    }
                    else
                    {
                        types = photonEvent[ParameterCode.LobbyType] as byte[];
                    }

                    this.lobbyStatistics.Clear();
                    for (int i = 0; i < names.Length; i++)
                    {
                        TypedLobbyInfo info = new TypedLobbyInfo(names[i], (LobbyType)types[i], peers[i], rooms[i]);
                        this.lobbyStatistics.Add(info);
                    }

                    if (useByteArraySlice)
                    {
                        slice.Release();
                    }

                    this.LobbyCallbackTargets.OnLobbyStatisticsUpdate(this.lobbyStatistics);
                    break;

                case EventCode.ErrorInfo:
                    this.ErrorInfoCallbackTargets.OnErrorInfo(new ErrorInfo(photonEvent));
                    break;

                case EventCode.AuthEvent:
                    if (this.AuthValues == null)
                    {
                        this.AuthValues = new AuthenticationValues();
                    }

                    this.AuthValues.Token = photonEvent[ParameterCode.Token];
                    break;

            }

            this.UpdateCallbackTargets();
            if (this.EventReceived != null)
            {
                this.EventReceived(photonEvent);
            }
        }


        /// <summary>Callback for messages.  Check documentation in interface. Raw messages are always provided as ArraySegment&lt;byte&gt;.</summary>
        public virtual void OnMessage(bool isRawMessage, object message)
        {
            this.UpdateCallbackTargets();
            if (this.MessageReceived != null)
            {
                this.MessageReceived(isRawMessage, message);
            }
        }


        /// <summary>Called when the client received a Disconnect Message from the server. Signals an error and provides a message to debug the case.</summary>
        public void OnDisconnectMessage(DisconnectMessage obj)
        {
            Log.Error(string.Format("OnDisconnectMessage. Code: {0} Msg: \"{1}\". Debug Info: {2}", obj.Code, obj.DebugMessage, obj.Parameters), this.LogLevel, this.LogPrefix);
            this.Disconnect(DisconnectCause.DisconnectByDisconnectMessage);
        }

        #endregion



        /// <summary>A callback of the RegionHandler, provided in OnRegionListReceived.</summary>
        /// <param name="regionHandler">The regionHandler wraps up best region and other region relevant info.</param>
        private void OnRegionPingCompleted(RegionHandler regionHandler)
        {
            if (this.LogLevel == LogLevel.Info)
            {
                Log.Info($"Region pinging summary: {SummaryToCache}", this.LogLevel, this.LogPrefix);
            }
            else if (this.LogLevel == LogLevel.Debug)
            {
                Log.Info($"Region pinging results: {regionHandler.GetResults()}", this.LogLevel, this.LogPrefix);
            }

            if (this.clientWorkflow == ClientWorkflowOption.GetRegionsAndPing)
            {
                this.Disconnect();
                this.ConnectionCallbackTargets.OnRegionListReceived(regionHandler);
                return;
            }

            this.CurrentRegion = regionHandler.BestRegion.Code;

            if (State == ClientState.ConnectedToNameServer)
            {
                this.CallAuthenticate();
            }
            else
            {
                // if not on the Name Server, connect to it with a CurrentRegion being set. this will authenticate for that region (or the FixedRegion if set)
                this.CallConnect(ServerConnection.NameServer);
            }
        }


        private void SetupEncryption(Dictionary<byte, object> encryptionData)
        {
            var mode = (EncryptionMode)(byte)encryptionData[EncryptionDataParameters.Mode];
            switch (mode)
            {
                case EncryptionMode.PayloadEncryption:
                    byte[] encryptionSecret = (byte[])encryptionData[EncryptionDataParameters.Secret1];
                    this.RealtimePeer.InitPayloadEncryption(encryptionSecret);
                    break;
                case EncryptionMode.DatagramEncryptionGCM:
                    {
                        byte[] secret1 = (byte[])encryptionData[EncryptionDataParameters.Secret1];
                        this.RealtimePeer.InitDatagramEncryption(secret1, null);
                    }
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }



        /// <summary>
        /// Provides callbacks via messages, which can be subscribed to individually.
        /// </summary>
        public EventBetter CallbackMessage = new EventBetter();

        /// <summary>
        /// Registers an object for callbacks for the implemented callback-interfaces.
        /// </summary>
        /// <remarks>
        /// Adding and removing callback targets is queued to not mess with callbacks in execution.
        /// Internally, this means that the addition/removal is done before the RealtimeClient
        /// calls the next callbacks. This detail should not affect a game's workflow.
        ///
        /// The covered callback interfaces are: IConnectionCallbacks, IMatchmakingCallbacks,
        /// ILobbyCallbacks, IInRoomCallbacks and IOnEventCallback.
        ///
        /// See: <a href="https://doc.photonengine.com/en-us/realtime/current/reference/dotnet-callbacks" target="_blank">.Net Callbacks</a>
        /// </remarks>
        /// <param name="target">The object that registers to get callbacks from this client.</param>
        public void AddCallbackTarget(object target)
        {
            this.callbackTargetChanges.Enqueue(new CallbackTargetChange(target, true));
        }

        /// <summary>
        /// Unregisters an object from callbacks for the implemented callback-interfaces.
        /// </summary>
        /// <remarks>
        /// Adding and removing callback targets is queued to not mess with callbacks in execution.
        /// Internally, this means that the addition/removal is done before the RealtimeClient
        /// calls the next callbacks. This detail should not affect a game's workflow.
        ///
        /// The covered callback interfaces are: IConnectionCallbacks, IMatchmakingCallbacks,
        /// ILobbyCallbacks, IInRoomCallbacks and IOnEventCallback.
        ///
        /// See: <a href="https://doc.photonengine.com/en-us/realtime/current/reference/dotnet-callbacks" target="_target">Callbacks</a>
        /// </remarks>
        /// <param name="target">The object that unregisters from getting callbacks.</param>
        public void RemoveCallbackTarget(object target)
        {
            this.callbackTargetChanges.Enqueue(new CallbackTargetChange(target, false));
        }


        /// <summary>
        /// Applies queued callback changes from a queue to the actual containers. Will cause exceptions if used while callbacks execute.
        /// </summary>
        /// <remarks>
        /// There is no explicit check that this is not called during callbacks, however the implemented, private logic takes care of this.
        /// </remarks>
        protected internal void UpdateCallbackTargets()
        {
            while (this.callbackTargetChanges.Count > 0)
            {
                CallbackTargetChange change = this.callbackTargetChanges.Dequeue();

                if (change.AddTarget)
                {
                    if (this.callbackTargets.Contains(change.Target))
                    {
                        //Debug.Log("UpdateCallbackTargets skipped adding a target, as the object is already registered. Target: " + change.Target);
                        continue;
                    }

                    this.callbackTargets.Add(change.Target);
                }
                else
                {
                    if (!this.callbackTargets.Contains(change.Target))
                    {
                        //Debug.Log("UpdateCallbackTargets skipped removing a target, as the object is not registered. Target: " + change.Target);
                        continue;
                    }

                    this.callbackTargets.Remove(change.Target);
                }

                this.UpdateCallbackTarget<IInRoomCallbacks>(change, this.InRoomCallbackTargets);
                this.UpdateCallbackTarget<IConnectionCallbacks>(change, this.ConnectionCallbackTargets);
                this.UpdateCallbackTarget<IMatchmakingCallbacks>(change, this.MatchMakingCallbackTargets);
                this.UpdateCallbackTarget<ILobbyCallbacks>(change, this.LobbyCallbackTargets);
                this.UpdateCallbackTarget<IErrorInfoCallback>(change, this.ErrorInfoCallbackTargets);

                IOnEventCallback onEventCallback = change.Target as IOnEventCallback;
                if (onEventCallback != null)
                {
                    if (change.AddTarget)
                    {
                        EventReceived += onEventCallback.OnEvent;
                    }
                    else
                    {
                        EventReceived -= onEventCallback.OnEvent;
                    }
                }
                IOnMessageCallback onMessageCallback = change.Target as IOnMessageCallback;
                if (onMessageCallback != null)
                {
                    if (change.AddTarget)
                    {
                        MessageReceived += onMessageCallback.OnMessage;
                    }
                    else
                    {
                        MessageReceived -= onMessageCallback.OnMessage;
                    }
                }
            }
        }

        /// <summary>Helper method to cast and apply a target per (interface) type.</summary>
        /// <typeparam name="T">Either of the interfaces for callbacks.</typeparam>
        /// <param name="change">The queued change to apply (add or remove) some target.</param>
        /// <param name="container">The container that calls callbacks on it's list of targets.</param>
        private void UpdateCallbackTarget<T>(CallbackTargetChange change, List<T> container) where T : class
        {
            T target = change.Target as T;
            if (target != null)
            {
                if (change.AddTarget)
                {
                    container.Add(target);
                }
                else
                {
                    container.Remove(target);
                }
            }
        }
    }
}


namespace ExitGames.Client.Photon
{
    /// <summary>Replace by RealtimeClient.</summary>
    [System.Obsolete("Use the RealtimeClient class instead. This was just renamed.")]
    public class LoadBalancingClient : global::Photon.Realtime.RealtimeClient
    {
        /// <summary>Replace by RealtimeClient.</summary>
        [System.Obsolete("Use the RealtimeClient class instead. This was just renamed.")]
        public LoadBalancingClient(global::Photon.Client.ConnectionProtocol protocol = global::Photon.Client.ConnectionProtocol.Udp) : base(protocol) { }
    }

    /// <summary>Replace by PhotonHashtable.</summary>
    [System.Obsolete("Use the PhotonHashtable class instead. This was just renamed.")]
    public class Hashtable : global::Photon.Client.PhotonHashtable
    {
    }
}

