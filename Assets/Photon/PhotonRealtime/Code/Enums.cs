// -----------------------------------------------------------------------
// <copyright file="Enums.cs" company="Exit Games GmbH">
// Photon Realtime API - Copyright (C) 2022 Exit Games GmbH
// </copyright>
// <summary>Enum defines for Photon Realtime API.</summary>
// <author>developer@photonengine.com</author>
// ----------------------------------------------------------------------------

#if UNITY_2017_4_OR_NEWER
#define SUPPORTED_UNITY
#endif


namespace Photon.Realtime
{
    using System;

    #if SUPPORTED_UNITY
    using UnityEngine;
    using Debug = UnityEngine.Debug;
    using SupportClass = Photon.Client.SupportClass;
    #endif

    /// <summary>
    /// State values for a client, which handles switching Photon server types, some operations, etc.
    /// </summary>
    /// \ingroup publicApi
    public enum ClientState
    {
        /// <summary>Peer is created but not used yet.</summary>
        PeerCreated,

        /// <summary>Transition state while connecting to a server. On the Photon Cloud this sends the AppId and AuthenticationValues (UserID).</summary>
        Authenticating,

        /// <summary>Not Used.</summary>
        Authenticated,

        /// <summary>The client sent an OpJoinLobby and if this was done on the Master Server, it will result in. Depending on the lobby, it gets room listings.</summary>
        JoiningLobby,

        /// <summary>The client is in a lobby, connected to the MasterServer. Depending on the lobby, it gets room listings.</summary>
        JoinedLobby,

        /// <summary>Transition from MasterServer to GameServer.</summary>
        DisconnectingFromMasterServer,

        /// <summary>Transition to GameServer (client authenticates and joins/creates a room).</summary>
        ConnectingToGameServer,

        /// <summary>Connected to GameServer (going to auth and join game).</summary>
        ConnectedToGameServer,

        /// <summary>Transition state while joining or creating a room on GameServer.</summary>
        Joining,

        /// <summary>The client entered a room. The CurrentRoom and Players are known and you can now raise events.</summary>
        Joined,

        /// <summary>Transition state when leaving a room.</summary>
        Leaving,

        /// <summary>Transition from GameServer to MasterServer (after leaving a room/game).</summary>
        DisconnectingFromGameServer,

        /// <summary>Connecting to MasterServer (includes sending authentication values).</summary>
        ConnectingToMasterServer,

        /// <summary>The client disconnects (from any server). This leads to state Disconnected.</summary>
        Disconnecting,

        /// <summary>The client is no longer connected (to any server). Connect to MasterServer to go on.</summary>
        Disconnected,

        /// <summary>Connected to MasterServer. You might use matchmaking or join a lobby now.</summary>
        ConnectedToMasterServer,

        /// <summary>Client connects to the NameServer. This process includes low level connecting and setting up encryption. When done, state becomes ConnectedToNameServer.</summary>
        ConnectingToNameServer,

        /// <summary>Client is connected to the NameServer and established encryption already. You should call OpGetRegions or ConnectToRegionMaster.</summary>
        ConnectedToNameServer,

        /// <summary>Clients disconnects (specifically) from the NameServer (usually to connect to the MasterServer).</summary>
        DisconnectingFromNameServer,

        /// <summary>Client was unable to connect to Name Server and will attempt to connect with an alternative network protocol (TCP).</summary>
        ConnectWithFallbackProtocol
    }


    /// <summary>Definition of parameters for encryption data (included in Authenticate operation response).</summary>
    internal class EncryptionDataParameters
    {
        /// <summary>
        /// Key for encryption mode
        /// </summary>
        public const byte Mode = 0;
        /// <summary>
        /// Key for first secret
        /// </summary>
        public const byte Secret1 = 1;
        /// <summary>
        /// Key for second secret
        /// </summary>
        public const byte Secret2 = 2;
    }

    /// <summary>
    /// Internal state, how this peer gets into a particular room (joining it or creating it).
    /// </summary>
    internal enum JoinType
    {
        /// <summary>This client creates a room, gets into it (no need to join) and can set room properties.</summary>
        CreateRoom,

        /// <summary>The room existed already and we join into it (not setting room properties).</summary>
        JoinRoom,

        /// <summary>Done on Master Server and (if successful) followed by a Join on Game Server.</summary>
        JoinRandomRoom,

        /// <summary>Done on Master Server and (if successful) followed by a Join or Create on Game Server.</summary>
        JoinRandomOrCreateRoom,

        /// <summary>Client is either joining or creating a room. On Master- and Game-Server.</summary>
        JoinOrCreateRoom
    }


    /// <summary>Enumeration of causes for Disconnects (used in RealtimeClient.DisconnectedCause).</summary>
    /// <remarks>Read the individual descriptions to find out what to do about this type of disconnect.</remarks>
    public enum DisconnectCause
    {
        /// <summary>No error was tracked.</summary>
        None,

        /// <summary>OnStatusChanged: The server is not available or the address is wrong. Make sure the port is provided and the server is up.</summary>
        ExceptionOnConnect,

        /// <summary>OnStatusChanged: Dns resolution for a hostname failed. The exception for this is being caught and logged with error level.</summary>
        DnsExceptionOnConnect,

        /// <summary>OnStatusChanged: The server address was parsed as IPv4 illegally. An illegal address would be e.g. 192.168.1.300. IPAddress.TryParse() will let this pass but our check won't.</summary>
        ServerAddressInvalid,

        /// <summary>OnStatusChanged: Some internal exception caused the socket code to fail. This may happen if you attempt to connect locally but the server is not available. In doubt: Contact Exit Games.</summary>
        Exception,

        /// <summary>OnStatusChanged: The server disconnected this client due to timing out (missing acknowledgement from the client).</summary>
        ServerTimeout,

        /// <summary>OnStatusChanged: This client detected that the server's responses are not received in due time.</summary>
        ClientTimeout,

        /// <summary>OnStatusChanged: The server disconnected this client from within the room's logic (the C# code).</summary>
        DisconnectByServerLogic,

        /// <summary>OnStatusChanged: The server disconnected this client for unknown reasons.</summary>
        DisconnectByServerReasonUnknown,

        /// <summary>OnOperationResponse: Authenticate in the Photon Cloud with invalid AppId. Update your subscription or contact Exit Games.</summary>
        InvalidAuthentication,

        /// <summary>OnOperationResponse: Authenticate in the Photon Cloud with invalid client values or custom authentication setup in Cloud Dashboard.</summary>
        CustomAuthenticationFailed,

        /// <summary>The authentication ticket should provide access to any Photon Cloud server without doing another authentication-service call. However, the ticket expired.</summary>
        AuthenticationTicketExpired,

        /// <summary>OnOperationResponse: Authenticate (temporarily) failed when using a Photon Cloud subscription without CCU Burst. Update your subscription.</summary>
        MaxCcuReached,

        /// <summary>OnOperationResponse: Authenticate when the app's Photon Cloud subscription is locked to some (other) region(s). Update your subscription or master server address.</summary>
        InvalidRegion,

        /// <summary>OnOperationResponse: Operation that's (currently) not available for this client (not authorized usually). Only tracked for op Authenticate.</summary>
        OperationNotAllowedInCurrentState,

        /// <summary>OnStatusChanged: The client disconnected from within the logic (the C# code).</summary>
        DisconnectByClientLogic,

        /// <summary>The client called an operation too frequently and got disconnected due to hitting the OperationLimit. This triggers a client-side disconnect, too.</summary>
        /// <remarks>To protect the server, some operations have a limit. When an OperationResponse fails with ErrorCode.OperationLimitReached, the client disconnects.</remarks>
        DisconnectByOperationLimit,

        /// <summary>The client received a "Disconnect Message" from the server. Check the debug logs for details.</summary>
        DisconnectByDisconnectMessage,

        /// <summary>Used in case the application quits. Can be useful to not load new scenes or re-connect in OnDisconnected.</summary>
        /// <remarks>ConnectionHandler.OnDisable() will use this, if the Unity engine already called OnApplicationQuit (ConnectionHandler.AppQuits = true).</remarks>
        ApplicationQuit,

        /// <summary>Used by the ConnectionHandler to end a connection for lack of RealtimeClient.Service calls.</summary>
        /// <remarks>
        /// Without calling Service (or SendOutgoingCommands and DispatchIncomingCommands), the network connection does not process
        /// messages and events. This can happen if apps are in the background or the main loop is paused (due to loading assets, etc).
        /// 
        /// ConnectionHandler.KeepAliveInBackground defines how long the connection is kept alive without calls to Service.
        /// Closing such connections prevents clients from staying connected "forever" without actually processing network updates
        /// (which would waste CCUs and connections).
        /// </remarks>
        ClientServiceInactivity
    }


    /// <summary>Available server (types) for internally used field: server.</summary>
    /// <remarks>Photon uses 3 different roles of servers: Name Server, Master Server and Game Server.</remarks>
    public enum ServerConnection
    {
        /// <summary>This server is where matchmaking gets done and where clients can get lists of rooms in lobbies.</summary>
        MasterServer,

        /// <summary>This server handles a number of rooms to execute and relay the messages between players (in a room).</summary>
        GameServer,

        /// <summary>This server is used initially to get the address (IP) of a Master Server for a specific region. Not used for Photon OnPremise (self hosted).</summary>
        NameServer
    }


    /// <summary>Defines which sort of app the RealtimeClient is used for: Realtime or Voice.</summary>
    public enum ClientAppType
    {
        /// <summary>Default client type. Detects type by checking AppSettings of one (and only one) is set of: AppIdRealtime, AppIdFusion or AppIdQuantum.</summary>
        Detect,

        /// <summary>Realtime apps are for gaming / interaction. Also used by PUN 2.</summary>
        Realtime,

        /// <summary>Voice apps stream audio.</summary>
        Voice,

        /// <summary>Chat apps distribute messages in channels for a large number of subscribers.</summary>
        Chat,

        /// <summary>Fusion clients are for matchmaking and relay in Photon Fusion.</summary>
        Fusion,

        /// <summary>Quantum clients are for matchmaking and relay in Photon Quantum.</summary>
        Quantum
    }


    /// <summary>
    /// Defines how the communication gets encrypted.
    /// </summary>
    public enum EncryptionMode
    {
        /// <summary>
        /// This is the default encryption mode: Messages get encrypted only on demand (when you send operations with the "encrypt" parameter set to true).
        /// </summary>
        PayloadEncryption,

        ///// <summary>
        ///// With this encryption mode for UDP, the connection gets setup and all further datagrams get encrypted almost entirely. On-demand message encryption (like in PayloadEncryption) is unavailable.
        ///// </summary>
        //DatagramEncryption = 10,
        ///// <summary>
        ///// With this encryption mode for UDP, the connection gets setup with random sequence numbers and all further datagrams get encrypted almost entirely. On-demand message encryption (like in PayloadEncryption) is unavailable.
        ///// </summary>
        //DatagramEncryptionRandomSequence = 11,
        ///// <summary>
        ///// Same as above except that GCM mode is used to encrypt data.
        ///// </summary>
        //DatagramEncryptionGCMRandomSequence = 12,
        /// <summary>
        /// Datagram Encryption with GCM.
        /// </summary>
        DatagramEncryptionGCM = 13,
    }


    /// <summary>Used in the RoomOptionFlags parameter, this bitmask toggles options in the room.</summary>
    internal enum RoomOptionBit : int
    {
        CheckUserOnJoin = 0x01,           // toggles a check of the UserId when joining (enabling returning to a game)
        DeleteCacheOnLeave = 0x02,        // deletes cache on leave
        SuppressRoomEvents = 0x04,        // suppresses all room events
        PublishUserId = 0x08,             // signals that we should publish userId
        DeleteNullProps = 0x10,           // signals that we should remove property if its value was set to null. see RoomOption to Delete Null Properties
        BroadcastPropsChangeToAll = 0x20, // signals that we should send PropertyChanged event to all room players including initiator
        SuppressPlayerInfo = 0x40,        // disables events join and leave from the server as well as property broadcasts in a room (to minimize traffic)
    }


    /// <summary>
    /// ErrorCode defines the default codes associated with Photon client/server communication.
    /// </summary>
    public class ErrorCode
    {
        /// <summary>(0) is always "OK", anything else an error or specific situation.</summary>
        public const int Ok = 0;

        // server - Photon low(er) level: <= 0

        /// <summary>
        /// (-3) Operation can't be executed yet (e.g. OpJoin can't be called before being authenticated, RaiseEvent cant be used before getting into a room).
        /// </summary>
        /// <remarks>
        /// Before you call any operations on the Cloud servers, the automated client workflow must complete its authorization.
        /// Wait until State is: JoinedLobby or ConnectedToMasterServer
        /// </remarks>
        public const int OperationNotAllowedInCurrentState = -3;

        /// <summary>(-2) The operation you called could not be executed on the server.</summary>
        /// <remarks>
        /// Make sure you are connected to the server you expect.
        ///
        /// This code is used in several cases:
        /// The arguments/parameters of the operation might be out of range, missing entirely or conflicting.
        /// The operation you called is not implemented on the server (application). Server-side plugins affect the available operations.
        /// </remarks>
        public const int InvalidOperation = -2;

        /// <summary>(-1) Something went wrong in the server. Try to reproduce and contact Exit Games.</summary>
        public const int InternalServerError = -1;

        // server - client: 0x7FFF and down
        // logic-level error codes start with short.max

        /// <summary>(32767) Authentication failed. Possible cause: AppId is unknown to Photon (in cloud service).</summary>
        public const int InvalidAuthentication = 0x7FFF;

        /// <summary>(32766) GameId (name) already in use (can't create another). Change name.</summary>
        public const int GameIdAlreadyExists = 0x7FFF - 1;

        /// <summary>(32765) Game is full. This rarely happens when some player joined the room before your join completed.</summary>
        public const int GameFull = 0x7FFF - 2;

        /// <summary>(32764) Game is closed and can't be joined. Join another game.</summary>
        public const int GameClosed = 0x7FFF - 3;

        /// <summary>(32762) All servers are busy. This is a temporary issue and the game logic should try again after a brief wait time.</summary>
        /// <remarks>
        /// This error may happen for all operations that create rooms. The operation response will contain this error code.
        ///
        /// This error is very unlikely to happen as we monitor load on all servers and add them on demand.
        /// However, it's good to be prepared for a shortage of machines or surge in CCUs.
        /// </remarks>
        public const int ServerFull = 0x7FFF - 5;

        /// <summary>(32761) Not in use currently.</summary>
        public const int UserBlocked = 0x7FFF - 6;

        /// <summary>(32760) Random matchmaking only succeeds if a room exists thats neither closed nor full. Repeat in a few seconds or create a new room.</summary>
        public const int NoRandomMatchFound = 0x7FFF - 7;

        /// <summary>(32758) Join can fail if the room (name) is not existing (anymore). This can happen when players leave while you join.</summary>
        public const int GameDoesNotExist = 0x7FFF - 9;

        /// <summary>(32757) Authorization on the Photon Cloud failed because the concurrent users (CCU) limit of the app's subscription is reached.</summary>
        /// <remarks>
        /// Unless you have a plan with "CCU Burst", clients might fail the authentication step during connect.
        /// Affected client are unable to call operations. Please note that players who end a game and return
        /// to the master server will disconnect and re-connect, which means that they just played and are rejected
        /// in the next minute / re-connect.
        /// This is a temporary measure. Once the CCU is below the limit, players will be able to connect an play again.
        ///
        /// OpAuthorize is part of connection workflow but only on the Photon Cloud, this error can happen.
        /// Self-hosted Photon servers with a CCU limited license won't let a client connect at all.
        /// </remarks>
        public const int MaxCcuReached = 0x7FFF - 10;

        /// <summary>(32756) Authorization on the Photon Cloud failed because the app's subscription does not allow to use a particular region's server.</summary>
        /// <remarks>
        /// Some subscription plans for the Photon Cloud are region-bound. Servers of other regions can't be used then.
        /// Check your master server address and compare it with your Photon Cloud Dashboard's info.
        /// https://dashboard.photonengine.com
        ///
        /// OpAuthorize is part of connection workflow but only on the Photon Cloud, this error can happen.
        /// Self-hosted Photon servers with a CCU limited license won't let a client connect at all.
        /// </remarks>
        public const int InvalidRegion = 0x7FFF - 11;

        /// <summary>
        /// (32755) Custom Authentication of the user failed due to setup reasons (see Cloud Dashboard) or the provided user data (like username or token). Check error message for details.
        /// </summary>
        public const int CustomAuthenticationFailed = 0x7FFF - 12;

        /// <summary>(32753) The Authentication ticket expired. Usually, this is refreshed behind the scenes. Connect (and authorize) again.</summary>
        public const int AuthenticationTicketExpired = 0x7FF1;

        /// <summary>
        /// (32752) A server-side plugin or WebHook failed and reported an error. Check the OperationResponse.DebugMessage.
        /// </summary>
        /// <remarks>A typical case is when a plugin prevents a user from creating or joining a room.
        /// If this is prohibited, that reports to the client as a plugin error.<br/>
        /// Same for WebHooks.</remarks>
        public const int PluginReportedError = 0x7FFF - 15;

        /// <summary>
        /// (32751) CreateGame/JoinGame/Join operation fails if expected plugin does not correspond to loaded one.
        /// </summary>
        public const int PluginMismatch = 0x7FFF - 16;

        /// <summary>
        /// (32750) for join requests. Indicates the current peer already called join and is joined to the room.
        /// </summary>
        public const int JoinFailedPeerAlreadyJoined = 32750; // 0x7FFF - 17,

        /// <summary>
        /// (32749)  for join requests. Indicates the list of InactiveActors already contains an actor with the requested ActorNr or UserId.
        /// </summary>
        public const int JoinFailedFoundInactiveJoiner = 32749; // 0x7FFF - 18,

        /// <summary>
        /// (32748) for join requests. Indicates the list of Actors (active and inactive) did not contain an actor with the requested ActorNr or UserId.
        /// </summary>
        public const int JoinFailedWithRejoinerNotFound = 32748; // 0x7FFF - 19,

        /// <summary>
        /// (32747) for join requests. Note: for future use - Indicates the requested UserId was found in the ExcludedList.
        /// </summary>
        public const int JoinFailedFoundExcludedUserId = 32747; // 0x7FFF - 20,

        /// <summary>
        /// (32746) for join requests. Indicates the list of ActiveActors already contains an actor with the requested ActorNr or UserId.
        /// </summary>
        public const int JoinFailedFoundActiveJoiner = 32746; // 0x7FFF - 21,

        /// <summary>
        /// (32745)  for SetProperties and RaiseEvent (if flag HttpForward is true) requests. Indicates the maximum allowed http requests per minute was reached.
        /// </summary>
        public const int HttpLimitReached = 32745; // 0x7FFF - 22,

        /// <summary>
        /// (32744) for WebRpc requests. Indicates the the call to the external service failed.
        /// </summary>
        public const int ExternalHttpCallFailed = 32744; // 0x7FFF - 23,

        /// <summary>
        /// (32743) for operations with defined limits (as in calls per second, content count or size).
        /// </summary>
        public const int OperationLimitReached = 32743; // 0x7FFF - 24,

        /// <summary>
        /// (32742) Server error during matchmaking with slot reservation. E.g. the reserved slots can not exceed MaxPlayers.
        /// </summary>
        public const int SlotError = 32742; // 0x7FFF - 25,

        /// <summary>
        /// (32741) Server will react with this error if invalid encryption parameters provided by token
        /// </summary>
        public const int InvalidEncryptionParameters = 32741; // 0x7FFF - 24,

    }


    /// <summary>
    /// Class for constants. These (byte) values define "well known" properties for an Actor / Player.
    /// </summary>
    /// <remarks>
    /// These constants are used internally.
    /// "Custom properties" have to use a string-type as key. They can be assigned at will.
    /// </remarks>
    public class ActorProperties
    {
        /// <summary>(255) Name of a player/actor.</summary>
        [Obsolete("Renamed. Use ActorProperties.NickName.")]
        public const byte PlayerName = 255;

        /// <summary>(255) NickName of a player/actor.</summary>
        public const byte NickName = 255;

        /// <summary>(254) Tells you if the player is currently in this game (getting events live).</summary>
        /// <remarks>A server-set value for async games, where players can leave the game and return later.</remarks>
        public const byte IsInactive = 254;

        /// <summary>(253) UserId of the player. Sent when room gets created with RoomOptions.PublishUserId = true.</summary>
        public const byte UserId = 253;
    }


    /// <summary>
    /// Class for constants. These (byte) values are for "well known" room/game properties used in Photon Realtime.
    /// </summary>
    /// <remarks>
    /// These constants are used internally.
    /// "Custom properties" have to use a string-type as key. They can be assigned at will.
    /// </remarks>
    public class GamePropertyKey
    {
        /// <summary>(255) Max number of players that "fit" into this room. 0 is for "unlimited".</summary>
        public const byte MaxPlayers = 255;

        /// <summary>(243) Integer-typed max number of players that "fit" into a room. 0 is for "unlimited". Important: Code changed. See remarks.</summary>
        /// <remarks>This was code 244 for a brief time (Realtime v4.1.7.2 to v4.1.7.4) and those versions must be replaced or edited!</remarks>
        public const byte MaxPlayersInt = 243;

        /// <summary>(254) Makes this room listed or not in the lobby on master.</summary>
        public const byte IsVisible = 254;

        /// <summary>(253) Allows more players to join a room (or not).</summary>
        public const byte IsOpen = 253;

        /// <summary>(252) Current count of players in the room. Used only in the lobby on master.</summary>
        public const byte PlayerCount = 252;

        /// <summary>(251) True if the room is to be removed from room listing (used in update to room list in lobby on master)</summary>
        public const byte Removed = 251;

        /// <summary>(250) A list of the room properties to pass to the RoomInfo list in a lobby. This is used in CreateRoom, which defines this list once per room.</summary>
        public const byte PropsListedInLobby = 250;

        /// <summary>(249) Equivalent of Operation Join parameter CleanupCacheOnLeave.</summary>
        public const byte CleanupCacheOnLeave = 249;

        /// <summary>(248) Code for MasterClientId, which is synced by server. When sent as op-parameter this is (byte)203. As room property this is (byte)248.</summary>
        /// <remarks>Tightly related to ParameterCode.MasterClientId.</remarks>
        public const byte MasterClientId = (byte)248;

        /// <summary>(247) Code for ExpectedUsers in a room. Matchmaking keeps a slot open for the players with these userIDs.</summary>
        public const byte ExpectedUsers = (byte)247;

        /// <summary>(246) Player Time To Live. How long any player can be inactive (due to disconnect or leave) before the user gets removed from the playerlist (freeing a slot).</summary>
        public const byte PlayerTtl = (byte)246;

        /// <summary>(245) Room Time To Live. How long a room stays available (and in server-memory), after the last player becomes inactive. After this time, the room gets persisted or destroyed.</summary>
        public const byte EmptyRoomTtl = (byte)245;
    }


    /// <summary>
    /// Class for constants. These values are for events defined by Photon Realtime.
    /// </summary>
    /// <remarks>They start at 255 and go DOWN. Your own in-game events can start at 0. These constants are used internally.</remarks>
    public class EventCode
    {
        /// <summary>(230) Initial list of RoomInfos (in lobby on Master)</summary>
        public const byte GameList = 230;

        /// <summary>(229) Update of RoomInfos to be merged into "initial" list (in lobby on Master)</summary>
        public const byte GameListUpdate = 229;

        /// <summary>(228) Currently not used. State of queueing in case of server-full</summary>
        public const byte QueueState = 228;

        /// <summary>(227) Currently not used. Event for matchmaking</summary>
        public const byte Match = 227;

        /// <summary>(226) Event with stats about this application (players, rooms, etc)</summary>
        public const byte AppStats = 226;

        /// <summary>(224) This event provides a list of lobbies with their player and game counts.</summary>
        public const byte LobbyStats = 224;

        /// <summary>(255) Event Join: someone joined the game. The new actorNumber is provided as well as the properties of that actor (if set in OpJoin).</summary>
        public const byte Join = (byte)255;

        /// <summary>(254) Event Leave: The player who left the game can be identified by the actorNumber.</summary>
        public const byte Leave = (byte)254;

        /// <summary>(253) When you call OpSetProperties with the broadcast option "on", this event is fired. It contains the properties being set.</summary>
        public const byte PropertiesChanged = (byte)253;

        /// (252) When player left game unexpected and the room has a playerTtl != 0, this event is fired to let everyone know about the timeout.
        /// Obsolete. Replaced by Leave. public const byte Disconnect = LiteEventCode.Disconnect;

        /// <summary>(251) Sent by Photon Cloud when a plugin-call or webhook-call failed or events cache limit exceeded. Usually, the execution on the server continues, despite the issue. Contains: ParameterCode.Info.</summary>
        /// <seealso href="https://doc.photonengine.com/en-us/realtime/current/reference/webhooks#options"/>
        public const byte ErrorInfo = 251;

        /// <summary>(250) Sent by Photon whent he event cache slice was changed. Done by OpRaiseEvent.</summary>
        public const byte CacheSliceChanged = 250;

        /// <summary>(223) Sent by Photon to update a token before it times out.</summary>
        public const byte AuthEvent = 223;
    }


    /// <summary>Class for constants. Codes for parameters of Operations and Events.</summary>
    /// <remarks>These constants are used internally.</remarks>
    public class ParameterCode
    {
        /// <summary>(237) A bool parameter for creating games. If set to true, no room events are sent to the clients on join and leave. Default: false (and not sent).</summary>
        public const byte SuppressRoomEvents = 237;

        /// <summary>(236) Time To Live (TTL) for a room when the last player leaves. Keeps room in memory for case a player re-joins soon. In milliseconds.</summary>
        public const byte EmptyRoomTTL = 236;

        /// <summary>(235) Time To Live (TTL) for an 'actor' in a room. If a client disconnects, this actor is inactive first and removed after this timeout. In milliseconds.</summary>
        public const byte PlayerTTL = 235;

        /// <summary>(234) Optional parameter of OpRaiseEvent and OpSetCustomProperties to forward the event/operation to a web-service.</summary>
        public const byte EventForward = 234;

        /// <summary>(233) Used in EvLeave to describe if a user is inactive (and might come back) or not. In rooms with PlayerTTL, becoming inactive is the default case.</summary>
        public const byte IsInactive = (byte)233;

        /// <summary>(232) Used when creating rooms to define if any userid can join the room only once.</summary>
        public const byte CheckUserOnJoin = (byte)232;

        /// <summary>(231) Code for "Check And Swap" (CAS) when changing properties.</summary>
        public const byte ExpectedValues = (byte)231;

        /// <summary>(230) Address of a (game) server to use.</summary>
        public const byte Address = 230;

        /// <summary>(229) Count of players in this application in a rooms (used in stats event)</summary>
        public const byte PeerCount = 229;

        /// <summary>(228) Count of games in this application (used in stats event)</summary>
        public const byte GameCount = 228;

        /// <summary>(227) Count of players on the master server (in this app, looking for rooms)</summary>
        public const byte MasterPeerCount = 227;

        /// <summary>(225) User's ID</summary>
        public const byte UserId = 225;

        /// <summary>(224) Your application's ID: a name on your own Photon or a GUID on the Photon Cloud</summary>
        public const byte ApplicationId = 224;

        /// <summary>(223) Not used currently (as "Position"). If you get queued before connect, this is your position</summary>
        public const byte Position = 223;

        /// <summary>(223) Modifies the matchmaking algorithm used for OpJoinRandom. Allowed parameter values are defined in enum MatchmakingMode.</summary>
        public const byte MatchMakingType = 223;

        /// <summary>(222) List of RoomInfos about open / listed rooms</summary>
        public const byte GameList = 222;

        /// <summary>(221) Internally used to establish encryption</summary>
        public const byte Token = 221;

        /// <summary>(220) Version of your application</summary>
        public const byte AppVersion = 220;

        /// <summary>(255) Code for the gameId/roomName (a unique name per room). Used in OpJoin and similar.</summary>
        public const byte RoomName = (byte)255;

        /// <summary>(250) Code for broadcast parameter of OpSetProperties method.</summary>
        public const byte Broadcast = (byte)250;

        /// <summary>(252) Code for list of players in a room.</summary>
        public const byte ActorList = (byte)252;

        /// <summary>(254) Code of the Actor of an operation. Used for property get and set.</summary>
        public const byte ActorNr = (byte)254;

        /// <summary>(249) Code for property set (PhotonHashtable).</summary>
        public const byte PlayerProperties = (byte)249;

        /// <summary>(245) Code of data/custom content of an event. Used in OpRaiseEvent.</summary>
        public const byte CustomEventContent = (byte)245;

        /// <summary>(245) Code of data of an event. Used in OpRaiseEvent.</summary>
        public const byte Data = (byte)245;

        /// <summary>(244) Code used when sending some code-related parameter, like OpRaiseEvent's event-code.</summary>
        /// <remarks>This is not the same as the Operation's code, which is no longer sent as part of the parameter Dictionary in Photon 3.</remarks>
        public const byte Code = (byte)244;

        /// <summary>(248) Code for property set (PhotonHashtable).</summary>
        public const byte GameProperties = (byte)248;

        /// <summary>
        /// (251) Code for property-set (PhotonHashtable). This key is used when sending only one set of properties.
        /// If either ActorProperties or GameProperties are used (or both), check those keys.
        /// </summary>
        public const byte Properties = (byte)251;

        /// <summary>(253) Code of the target Actor of an operation. Used for property set. Is 0 for game</summary>
        public const byte TargetActorNr = (byte)253;

        /// <summary>(246) Code to select the receivers of events (used in Lite, Operation RaiseEvent).</summary>
        public const byte ReceiverGroup = (byte)246;

        /// <summary>(247) Code for caching events while raising them.</summary>
        public const byte Cache = (byte)247;

        /// <summary>(241) Bool parameter of CreateGame Operation. If true, server cleans up roomcache of leaving players (their cached events get removed).</summary>
        public const byte CleanupCacheOnLeave = (byte)241;

        /// <summary>(240) Code for "group" operation-parameter (as used in Op RaiseEvent).</summary>
        public const byte Group = 240;

        /// <summary>(239) The "Remove" operation-parameter can be used to remove something from a list. E.g. remove groups from player's interest groups.</summary>
        public const byte Remove = 239;

        /// <summary>(239) Used in Op Join to define if UserIds of the players are broadcast in the room. Useful for FindFriends and reserving slots for expected users.</summary>
        public const byte PublishUserId = 239;

        /// <summary>(238) The "Add" operation-parameter can be used to add something to some list or set. E.g. add groups to player's interest groups.</summary>
        public const byte Add = 238;

        /// <summary>(218) Content for EventCode.ErrorInfo and internal debug operations.</summary>
        public const byte Info = 218;

        /// <summary>(217) This key's (byte) value defines the target custom authentication type/service the client connects with. Used in OpAuthenticate</summary>
        public const byte ClientAuthenticationType = 217;

        /// <summary>(216) This key's (string) value provides parameters sent to the custom authentication type/service the client connects with. Used in OpAuthenticate</summary>
        public const byte ClientAuthenticationParams = 216;

        /// <summary>(215) The JoinMode enum defines which variant of joining a room will be executed: Join only if available, create if not exists or re-join.</summary>
        public const byte JoinMode = 215;

        /// <summary>(214) This key's (string or byte[]) value provides parameters sent to the custom authentication service setup in Photon Dashboard. Used in OpAuthenticate</summary>
        public const byte ClientAuthenticationData = 214;

        /// <summary>(203) Code for MasterClientId, which is synced by server. When sent as op-parameter this is code 203.</summary>
        /// <remarks>Tightly related to GamePropertyKey.MasterClientId.</remarks>
        public const byte MasterClientId = (byte)203;

        /// <summary>(1) Used in Op FindFriends request. Value must be string[] of friends to look up.</summary>
        public const byte FindFriendsRequestList = (byte)1;

        /// <summary>(2) Used in Op FindFriends request. An integer containing option-flags to filter the results.</summary>
        public const byte FindFriendsOptions = (byte)2;

        /// <summary>(1) Used in Op FindFriends response. Contains bool[] list of online states (false if not online).</summary>
        public const byte FindFriendsResponseOnlineList = (byte)1;

        /// <summary>(2) Used in Op FindFriends response. Contains string[] of room names ("" where not known or no room joined).</summary>
        public const byte FindFriendsResponseRoomIdList = (byte)2;

        /// <summary>(213) Used in matchmaking-related methods and when creating a room to name a lobby (to join or to attach a room to).</summary>
        public const byte LobbyName = (byte)213;

        /// <summary>(212) Used in matchmaking-related methods and when creating a room to define the type of a lobby. Combined with the lobby name this identifies the lobby.</summary>
        public const byte LobbyType = (byte)212;

        /// <summary>(211) This (optional) parameter can be sent in Op Authenticate to turn on Lobby Stats (info about lobby names and their user- and game-counts).</summary>
        public const byte LobbyStats = (byte)211;

        /// <summary>(210) Used for region values in OpAuth and OpGetRegions.</summary>
        public const byte Region = (byte)210;

        /// <summary>(209) Path of the WebRPC that got called. Also known as "WebRpc Name". Type: string.</summary>
        public const byte UriPath = 209;

        /// <summary>(208) Parameters for a WebRPC as: Dictionary&lt;string, object&gt;. This will get serialized to JSon.</summary>
        public const byte WebRpcParameters = 208;

        /// <summary>(207) ReturnCode for the WebRPC, as sent by the web service (not by Photon, which uses ErrorCode). Type: byte.</summary>
        public const byte WebRpcReturnCode = 207;

        /// <summary>(206) Message returned by WebRPC server. Analog to Photon's debug message. Type: string.</summary>
        public const byte WebRpcReturnMessage = 206;

        /// <summary>(205) Used to define a "slice" for cached events. Slices can easily be removed from cache. Type: int.</summary>
        public const byte CacheSliceIndex = 205;

        /// <summary>(204) Informs the server of the expected plugin setup.</summary>
        /// <remarks>
        /// The operation will fail in case of a plugin mismatch returning error code PluginMismatch 32751(0x7FFF - 16).
        /// Setting string[]{} means the client expects no plugin to be setup.
        /// Note: for backwards compatibility null omits any check.
        /// </remarks>
        public const byte Plugins = 204;

        /// <summary>(202) Used by the server in Operation Responses, when it sends the nickname of the client (the user's nickname).</summary>
        public const byte NickName = 202;

        /// <summary>(201) Informs user about name of plugin load to game</summary>
        public const byte PluginName = 201;

        /// <summary>(200) Informs user about version of plugin load to game</summary>
        public const byte PluginVersion = 200;

        /// <summary>(196) Cluster info provided in OpAuthenticate/OpAuthenticateOnce responses.</summary>
        public const byte Cluster = 196;

        /// <summary>(195) Protocol which will be used by client to connect master/game servers. Used for nameserver.</summary>
        public const byte ExpectedProtocol = 195;

        /// <summary>(194) Set of custom parameters which are sent in auth request.</summary>
        public const byte CustomInitData = 194;

        /// <summary>(193) How are we going to encrypt data.</summary>
        public const byte EncryptionMode = 193;

        /// <summary>(192) Parameter of Authentication, which contains encryption keys (depends on AuthMode and EncryptionMode).</summary>
        public const byte EncryptionData = 192;

        /// <summary>(191) An int parameter summarizing several boolean room-options with bit-flags.</summary>
        public const byte RoomOptionFlags = 191;

        /// <summary>(190) A matchmaking ticket provided from the server side. With these tickets, the server can define various values to be used in matchmaking.</summary>
        public const byte Ticket = 190;

        /// <summary>(189) With this value in a Ticket, the server can define a party / group for matchmaking. Groups will be matched into the same game (as soon as one was found).</summary>
        /// <remarks>This is defined in tickets and not sent by the clients. Here for completeness.</remarks>
        public const byte MatchmakingGroupId = 189;
        
        /// <summary>(188) Parameter key to let the server know it may queue the client in low-ccu matchmaking situations.</summary>
        public const byte AllowRepeats = 188;
    }


    /// <summary>
    /// Class for constants. Contains operation codes.
    /// </summary>
    /// <remarks>These constants are used internally.</remarks>
    public class OperationCode
    {
        /// <summary>(231) Authenticates this peer and connects to a virtual application</summary>
        public const byte AuthenticateOnce = 231;

        /// <summary>(230) Authenticates this peer and connects to a virtual application</summary>
        public const byte Authenticate = 230;

        /// <summary>(229) Joins lobby (on master)</summary>
        public const byte JoinLobby = 229;

        /// <summary>(228) Leaves lobby (on master)</summary>
        public const byte LeaveLobby = 228;

        /// <summary>(227) Creates a game (or fails if name exists)</summary>
        public const byte CreateGame = 227;

        /// <summary>(226) Join game (by name)</summary>
        public const byte JoinGame = 226;

        /// <summary>(225) Joins random game (on master)</summary>
        public const byte JoinRandomGame = 225;

        // public const byte CancelJoinRandom = 224; // obsolete, cause JoinRandom no longer is a "process". now provides result immediately

        /// <summary>(254) Code for OpLeave, to get out of a room.</summary>
        public const byte Leave = (byte)254;

        /// <summary>(253) Raise event (in a room, for other actors/players)</summary>
        public const byte RaiseEvent = (byte)253;

        /// <summary>(252) Set Properties (of room or actor/player)</summary>
        public const byte SetProperties = (byte)252;

        /// <summary>(251) Get Properties</summary>
        public const byte GetProperties = (byte)251;

        /// <summary>(248) Operation code to change interest groups in Rooms (Lite application and extending ones).</summary>
        public const byte ChangeGroups = (byte)248;

        /// <summary>(222) Request the rooms and online status for a list of friends (by name, which should be unique).</summary>
        public const byte FindFriends = 222;

        /// <summary>(221) Request statistics about a specific list of lobbies (their user and game count).</summary>
        public const byte GetLobbyStats = 221;

        /// <summary>(220) Get list of regional servers from a NameServer.</summary>
        public const byte GetRegions = 220;

        /// <summary>(218) Operation to set some server settings. Used with different parameters on various servers.</summary>
        public const byte ServerSettings = 218;

        /// <summary>(217) Get the game list matching a supplied sql filter (SqlListLobby only) </summary>
        public const byte GetGameList = 217;
    }

    /// <summary>Defines possible values for OpJoinRoom and OpJoinOrCreate. It tells the server if the room can be only be joined normally, created implicitly or found on a web-service for Turnbased games.</summary>
    /// <remarks>These values are not directly used by a game but implicitly set.</remarks>
    public enum JoinMode : byte
    {
        /// <summary>Regular join. The room must exist.</summary>
        Default = 0,

        /// <summary>Join or create the room if it's not existing. Used for OpJoinOrCreate for example.</summary>
        CreateIfNotExists = 1,

        /// <summary>The room might be out of memory and should be loaded (if possible) from a Turnbased web-service.</summary>
        JoinOrRejoin = 2,

        /// <summary>Only re-join will be allowed. If the user is not yet in the room, this will fail.</summary>
        RejoinOnly = 3,
    }

    /// <summary>
    /// Options for matchmaking rules for OpJoinRandom.
    /// </summary>
    public enum MatchmakingMode : byte
    {
        /// <summary>Fills up rooms (oldest first) to get players together as fast as possible. Default.</summary>
        /// <remarks>Makes most sense with MaxPlayers > 0 and games that can only start with more players.</remarks>
        FillRoom = 0,

        /// <summary>Distributes players across available rooms sequentially but takes filter into account. Without filter, rooms get players evenly distributed.</summary>
        SerialMatching = 1,

        /// <summary>Joins a (fully) random room. Expected properties must match but aside from this, any available room might be selected.</summary>
        RandomMatching = 2
    }


    /// <summary>
    /// Lite - OpRaiseEvent lets you chose which actors in the room should receive events.
    /// By default, events are sent to "Others" but you can overrule this.
    /// </summary>
    public enum ReceiverGroup : byte
    {
        /// <summary>Default value (not sent). Anyone else gets my event.</summary>
        Others = 0,

        /// <summary>Everyone in the current room (including this peer) will get this event.</summary>
        All = 1,

        /// <summary>The server sends this event only to the actor with the lowest actorNumber.</summary>
        /// <remarks>The "master client" does not have special rights but is the one who is in this room the longest time.</remarks>
        MasterClient = 2,
    }

    /// <summary>
    /// Lite - OpRaiseEvent allows you to cache events and automatically send them to joining players in a room.
    /// Events are cached per event code and player: Event 100 (example!) can be stored once per player.
    /// Cached events can be modified, replaced and removed.
    /// </summary>
    /// <remarks>
    /// Caching works only combination with ReceiverGroup options Others and All.
    /// </remarks>
    public enum EventCaching : byte
    {
        /// <summary>Default value (not sent).</summary>
        DoNotCache = 0,

        ///// <summary>Will merge this event's keys with those already cached.</summary>
        //[Obsolete]
        //MergeCache = 1,

        ///// <summary>Replaces the event cache for this eventCode with this event's content.</summary>
        //[Obsolete]
        //ReplaceCache = 2,

        ///// <summary>Removes this event (by eventCode) from the cache.</summary>
        //[Obsolete]
        //RemoveCache = 3,

        /// <summary>Adds an event to the room's cache</summary>
        AddToRoomCache = 4,

        /// <summary>Adds this event to the cache for actor 0 (becoming a "globally owned" event in the cache).</summary>
        AddToRoomCacheGlobal = 5,

        /// <summary>Remove fitting event from the room's cache.</summary>
        RemoveFromRoomCache = 6,

        /// <summary>Removes events of players who already left the room (cleaning up).</summary>
        RemoveFromRoomCacheForActorsLeft = 7,

        /// <summary>Increase the index of the sliced cache.</summary>
        SliceIncreaseIndex = 10,

        /// <summary>Set the index of the sliced cache. You must set RaiseEventArgs.CacheSliceIndex for this.</summary>
        SliceSetIndex = 11,

        /// <summary>Purge cache slice with index. Exactly one slice is removed from cache. You must set RaiseEventArgs.CacheSliceIndex for this.</summary>
        SlicePurgeIndex = 12,

        /// <summary>Purge cache slices with specified index and anything lower than that. You must set RaiseEventArgs.CacheSliceIndex for this.</summary>
        SlicePurgeUpToIndex = 13,
    }

    /// <summary>
    /// Flags for "types of properties", being used as filter in OpGetProperties.
    /// </summary>
    [Flags]
    public enum PropertyTypeFlag : byte
    {
        /// <summary>(0x00) Flag type for no property type.</summary>
        None = 0x00,

        /// <summary>(0x01) Flag type for game-attached properties.</summary>
        Game = 0x01,

        /// <summary>(0x02) Flag type for actor related propeties.</summary>
        Actor = 0x02,

        /// <summary>(0x01) Flag type for game AND actor properties. Equal to 'Game'</summary>
        GameAndActor = Game | Actor
    }

    /// <summary>Types of lobbies define their behaviour and capabilities. Check each value for details.</summary>
    /// <remarks>Values of this enum must be matched by the server.</remarks>
    public enum LobbyType : byte
    {
        /// <summary>Standard type and behaviour: While joined to this lobby clients get room-lists and JoinRandomRoom can use a simple filter to match properties (perfectly).</summary>
        Default = 0,
        /// <summary>This lobby type lists rooms like Default but JoinRandom has a parameter for SQL-like "where" clauses for filtering. This allows bigger, less, or and and combinations.</summary>
        Sql = 2,
        /// <summary>Use LobbyType.Sql</summary>
        [Obsolete("Use LobbyType.Sql")]
        SqlLobby = 2,
        /// <summary>This lobby does not send lists of games. It is only used for OpJoinRandomRoom. It keeps rooms available for a while when there are only inactive users left.</summary>
        AsyncRandom = 3,
        /// <summary>Use LobbyType.AsyncRandom</summary>
        [Obsolete("Use LobbyType.AsyncRandom")]
        AsyncRandomLobby = 3
    }

    /// <summary>
    /// Options for authentication modes. From "classic" auth on each server to AuthOnce (on NameServer).
    /// </summary>
    public enum AuthModeOption
    {
        /// <summary>Authenticate on each server.</summary>
        Auth,
        /// <summary>Authenticate once on the Name Server and use Token otherwise.</summary>
        AuthOnce,
        /// <summary>Authenticate once on the Name Server and use Token otherwise. Connections to Name Server is WSS (secure).</summary>
        AuthOnceWss
    }


    /// <summary>
    /// Options for optional "Custom Authentication" services used with Photon. Used by OpAuthenticate after connecting to Photon.
    /// </summary>
    public enum CustomAuthenticationType : byte
    {
        /// <summary>Use a custom authentication service. Currently, the only implemented option.</summary>
        Custom = 0,

        /// <summary>Authenticates users by their Steam Account. Set Steam's ticket as "ticket" via AddAuthParameter().</summary>
        Steam = 1,

        /// <summary>Authenticates users by their Facebook Account. Set Facebook's token as "token" via AddAuthParameter().</summary>
        Facebook = 2,

        /// <summary>Authenticates users by their Oculus Account and token. Set Oculus' userid as "userid" and nonce as "nonce" via AddAuthParameter().</summary>
        Oculus = 3,

        /// <summary>Authenticates users by their PSN Account and token on PS4. Set token as "token", env as "env" and userName as "userName" via AddAuthParameter().</summary>
        PlayStation4 = 4,

        /// <summary>Authenticates users by their Xbox Account. Pass the XSTS token via SetAuthPostData(byte[]).</summary>
        Xbox = 5,

        /// <summary>Authenticates users by their HTC Viveport Account. Set userToken as "userToken" via AddAuthParameter().</summary>
        Viveport = 10,

        /// <summary>Authenticates users by their NSA ID. Set Nintendo's token as "token" and appversion as "appversion" via AddAuthParameter(). The appversion is optional.</summary>
        NintendoSwitch = 11,

        /// <summary>Authenticates users by their PSN Account and token on PS5. Set token as "token", env as "env" and userName as "userName" via AddAuthParameter().</summary>
        PlayStation5 = 12,

        /// <summary>Authenticates users with Epic Online Services (EOS). Set token as "token" and ownershipToken as "ownershipToken" via AddAuthParameter(). The ownershipToken is optional.</summary>
        Epic = 13,

        /// <summary>Authenticates users with Facebook Gaming api. Set Facebook's token as "token" via AddAuthParameter().</summary>
        FacebookGaming = 15,

        /// <summary>Disables custom authentication. Same as not providing any AuthenticationValues for connect (more precisely for: OpAuthenticate).</summary>
        None = byte.MaxValue
    }


}