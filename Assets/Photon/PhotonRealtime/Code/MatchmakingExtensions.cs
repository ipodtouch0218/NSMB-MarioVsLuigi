// -----------------------------------------------------------------------------
// <copyright company="Exit Games GmbH">
// Photon Realtime API - Copyright (C) 2022 Exit Games GmbH
// </copyright>
// <summary>Extension methods for simple, async matchmaking.</summary>
// <author>developer@photonengine.com</author>
// -----------------------------------------------------------------------------


namespace Photon.Realtime
{
    using Photon.Client;
    using System;
    using System.Collections;
    #if UNITY_WEBGL
    using System.Diagnostics;
    #endif
    using System.Threading.Tasks;

    /// <summary>
    /// An extension class to wrap some of the most common Photon matchmaking calls.
    /// </summary>
    public static class MatchmakingExtensions
    {
        /// <summary>
        /// Is raised by the matchmaking extension methods operations failed.
        /// </summary>
        public class Exception : System.Exception
        {
            /// <summary>
            /// Create a new exception with a message and error code.
            /// </summary>
            /// <param name="message">Debug message.</param>
            /// <param name="errorCode">Error code.</param>
            public Exception(string message, short errorCode) : base(message)
            {
                ErrorCode = errorCode;
            }

            /// <summary>
            /// Photon error code <see cref="Realtime.ErrorCode"/>
            /// </summary>
            public short ErrorCode { get; set; }
        }

        /// <summary>
        /// Run the Photon connection logic to connect to a game server and enter a room. 
        /// This method only returns after successfully entered a room.
        /// This method throws an exception on any errors.
        /// </summary>
        /// <param name="client">Optionally client object to start the matchmaking with. Can be null.</param>
        /// <param name="arguments">Photon matchmaking arguments.</param>
        /// <returns>Client connection object that is connected to a Photon room</returns>
        /// <exception cref="ArgumentException">Arguments were incomplete.</exception>
        /// <exception cref="MatchmakingExtensions.Exception">Connection failed.</exception>
        /// <exception cref="DisconnectException">Is thrown when the connection terminated.</exception>
        /// <exception cref="AuthenticationFailedException">Is thrown when the authentication failed.</exception>
        /// <exception cref="OperationStartException">Is thrown when the operation could not be started.</exception>
        /// <exception cref="OperationException">Is thrown when the operation completed unsuccessfully.</exception>
        /// <exception cref="OperationTimeoutException">Is thrown when the operation timed out.</exception>
        /// <exception cref="OperationCanceledException">Is thrown when the operation have been canceled (AsyncConfig.CancellationSource).</exception>
        public static Task<RealtimeClient> ConnectToRoomAsync(this RealtimeClient client, MatchmakingArguments arguments)
        {
            return ConnectToRoomAsync(arguments, client);
        }

        /// <inheritdoc cref="ConnectToRoomAsync(RealtimeClient, MatchmakingArguments)"/>
        public static Task<RealtimeClient> ConnectToRoomAsync(MatchmakingArguments arguments, RealtimeClient client = null)
        {
            arguments.Validate();

            if (arguments.PhotonSettings != null)
            {
                Log.Info("Connecting to room", arguments.PhotonSettings.ClientLogging);
            }

            var asyncConfig = arguments.AsyncConfig ?? AsyncConfig.Global;

            return asyncConfig.TaskFactory.StartNew(async () =>
            {
                client = client ?? arguments.NetworkClient ?? new RealtimeClient();
                var isRandom = arguments.RoomName == null;
                var canCreate = arguments.CanOnlyJoin == false;

                if (arguments.AuthValues != null) {
                    client.AuthValues = arguments.AuthValues.CopyTo(new AuthenticationValues());
                }

                client.RealtimePeer.CrcEnabled = arguments.EnableCrc;

                await client.ConnectUsingSettingsAsync(arguments.PhotonSettings, asyncConfig);

                short result = 0;
                if (isRandom)
                {
                    if (canCreate)
                    {
                        result = await client.JoinRandomOrCreateRoomAsync(
                            arguments.BuildJoinRandomRoomArgs(), 
                            arguments.BuildEnterRoomArgs(),
                            config: asyncConfig);
                    }
                    else
                    {
                        result = await client.JoinRandomRoomAsync(
                            arguments.BuildJoinRandomRoomArgs(), 
                            config: asyncConfig);
                    }
                }
                else
                {
                    if (canCreate)
                    {
                        result = await client.JoinOrCreateRoomAsync(
                            arguments.BuildEnterRoomArgs(),
                            config: asyncConfig);
                    }
                    else
                    {
                        result = await client.JoinRoomAsync(
                            arguments.BuildEnterRoomArgs(),
                            config: asyncConfig);
                    }
                }

                if (result == ErrorCode.Ok)
                {
                    if (arguments.ReconnectInformation != null)
                    {
                        arguments.ReconnectInformation.Set(client);
                    }
                    return client;
                }

                throw new Exception($"Failed to connect and join with error '{result}'", result);
            }).Unwrap();
        }

        /// <summary>
        /// Run different Photon reconnection logic to reconnect to a game server and re-enter a room. 
        /// Requires <see cref="MatchmakingReconnectInformation"/> to be initialized.
        /// Different reconnection strategies are applied to rejoin a room. Also works after an app restart if the ReconnectInformation is correctly set up.
        /// This method only returns after successfully re-entered the room.
        /// This method throws an exception on any errors.
        /// </summary>
        /// <param name="client">Optionally client object to start the matchmaking with. Can be null.</param>
        /// <param name="arguments">Photon matchmaking arguments.</param>
        /// <returns>Client connection object that is connected to a Photon room</returns>
        /// <exception cref="ArgumentException">Arguments were incomplete, reconnection information not set or invalid.</exception>
        /// <exception cref="MatchmakingExtensions.Exception">Reconnection failed.</exception>
        /// <exception cref="DisconnectException">Is thrown when the connection terminated.</exception>
        /// <exception cref="AuthenticationFailedException">Is thrown when the authentication failed.</exception>
        /// <exception cref="OperationStartException">Is thrown when the operation could not be started.</exception>
        /// <exception cref="OperationException">Is thrown when the operation completed unsuccessfully.</exception>
        /// <exception cref="OperationTimeoutException">Is thrown when the operation timed out.</exception>
        /// <exception cref="OperationCanceledException">Is thrown when the operation have been canceled (AsyncConfig.CancellationSource).</exception>
        public static Task<RealtimeClient> ReconnectToRoomAsync(this RealtimeClient client, MatchmakingArguments arguments)
        {
            return ReconnectToRoomAsync(arguments, client);
        }

        /// <inheritdoc cref="ReconnectToRoomAsync(RealtimeClient, MatchmakingArguments)"/>
        public static Task<RealtimeClient> ReconnectToRoomAsync(MatchmakingArguments arguments, RealtimeClient client = null)
        {
            arguments.Validate();

            if (arguments.ReconnectInformation == null)
            {
                throw new ArgumentException("ReconnectInformation missing");
            }

            if (arguments.ReconnectInformation.AppVersion != arguments.PhotonSettings.AppVersion)
            {
                throw new ArgumentException("AppVersion mismatch");
            }

            if (string.IsNullOrEmpty(arguments.ReconnectInformation.UserId))
            {
                throw new ArgumentException("UserId not set");
            }

            if (arguments.AuthValues != null && arguments.ReconnectInformation.UserId != arguments.AuthValues.UserId)
            {
                throw new ArgumentException("UserId mismatch");
            }

            if (arguments.ReconnectInformation.HasTimedOut)
            {
                Log.Warn($"ReconnectInformation timed out: {arguments.ReconnectInformation.Timeout} (now = {DateTime.Now})");
            }

            Log.Info($"Reconnecting to room {arguments.ReconnectInformation.Room}");

            var asyncConfig = arguments.AsyncConfig ?? AsyncConfig.Global;
            var cancelToken = asyncConfig.CancellationToken;

            return asyncConfig.TaskFactory.StartNew(async () =>
            {
                client = (client ?? arguments.NetworkClient) ?? new RealtimeClient();

                client.RealtimePeer.CrcEnabled = arguments.EnableCrc;

                if (client.State == ClientState.PeerCreated)
                {
                    // New client object cannot ReconnectAndRejoin()
                    // TODO: there is a risk of a different master (rotation, cloud).
                    arguments.PhotonSettings.FixedRegion = arguments.ReconnectInformation.Region;
                    client.AuthValues = new AuthenticationValues { UserId = arguments.ReconnectInformation.UserId };
                    await client.ConnectUsingSettingsAsync(arguments.PhotonSettings, asyncConfig);
                }

                short? result = null;
                var canRejoin = arguments.CanRejoin;

                // ConnectedToMasterServer would mean we could skip this and continue with joining the room 
                if (client.State != ClientState.ConnectedToMasterServer)
                {
                    await client.DisconnectAsync(asyncConfig);
                    if (canRejoin)
                    {
                        // If PlayerTtlInSeconds > 0 try to use fast reconnect: ReconnectAndRejoinAsync
                        result = await client.ReconnectAndRejoinAsync(throwOnError: false, config: asyncConfig);
                    }
                    else
                    {
                        // Otherwise try to reconnect to master server
                        await client.ReconnectToMasterAsync(asyncConfig);
                    }
                }

#if UNITY_WEBGL
                var sw = new Stopwatch();
#endif
                var joinIterations = 0;
                while (joinIterations++ < 10)
                {
                    // -> GameDoesNotExist
                    // -> PluginReportedError (webhook failed)
                    // -> JoinFailedWithRejoinerNotFound (PlayerTTL ran out)
                    // -> JoinFailedFoundActiveJoiner (another client with the same user id is connected)
                    if (result.HasValue)
                    {
                        if (result.Value == ErrorCode.Ok)
                        {
                            // Success
                            break;
                        }
                        else if (result.Value == ErrorCode.JoinFailedFoundActiveJoiner)
                        {
                            // This will happen when the client created a new connection and the corresponding actor is still marked active in the room (10 second timeout).
                            // TODO: do we get same actor (playerTTL > 0)
#if UNITY_WEBGL
                            sw.Restart();
                            while (cancelToken.IsCancellationRequested == false && sw.ElapsedMilliseconds < 1000)
                            {
                                // Task.Delay() requires threading which is not supported on WebGL. Using this yield seems to be the simplest workaround.
                                await Task.Yield();
                            }
#else
                            await Task.Delay(1000, cancelToken);
#endif

                        }
                        else if (result.Value == ErrorCode.JoinFailedWithRejoinerNotFound)
                        {
                            // We tried to rejoin but there is not inactive actor in the room, try joining instead.
                            canRejoin = false;
                        }
                        else
                        {
                            // Error
                            throw new Exception($"Failed to join the room with error '{result}'", result.Value);
                        }
                    }

                    if (joinIterations > 1)
                    {
                        Log.Info($"Reconnection attempt ({joinIterations}/10)");
                    }

                    if (cancelToken.IsCancellationRequested)
                    {
                        throw new TaskCanceledException();
                    }

                    if (canRejoin)
                    {
                        result = await client.RejoinRoomAsync(arguments.ReconnectInformation.Room,
                            ticket: arguments.Ticket, 
                            throwOnError: false, 
                            config: asyncConfig);
                    }
                    else
                    {
                        result = await client.JoinRoomAsync(arguments.BuildEnterRoomArgs().SetRoomName(arguments.ReconnectInformation.Room), throwOnError: false, config: asyncConfig);
                    }
                }

                if (result.HasValue && result.Value == ErrorCode.Ok && arguments.ReconnectInformation != null)
                {
                    arguments.ReconnectInformation.Set(client);
                }

                return client;
            }).Unwrap();
        }

        private static EnterRoomArgs SetRoomName(this EnterRoomArgs args, string roomName)
        {
            args.RoomName = roomName;
            return args;
        }

        private static EnterRoomArgs BuildEnterRoomArgs(this MatchmakingArguments arguments)
        {
            return new EnterRoomArgs
            {
                RoomName = arguments.RoomName,
                Lobby = arguments.Lobby,
                Ticket = arguments.Ticket,
                ExpectedUsers = arguments.ExpectedUsers,
                RoomOptions = arguments.CustomRoomOptions ?? new RoomOptions()
                {
                    MaxPlayers = (byte)arguments.MaxPlayers,
                    IsOpen = arguments.IsRoomOpen.HasValue ? arguments.IsRoomOpen.Value : true,
                    IsVisible = arguments.IsRoomVisible.HasValue ? arguments.IsRoomVisible.Value : true,
                    DeleteNullProperties = true,
                    PlayerTtl = arguments.PlayerTtlInSeconds * 1000,
                    EmptyRoomTtl = arguments.EmptyRoomTtlInSeconds * 1000,
                    Plugins = arguments.Plugins,
                    SuppressRoomEvents = false,
                    SuppressPlayerInfo = false,
                    PublishUserId = true,
                    CustomRoomProperties = arguments.CustomProperties,
                    CustomRoomPropertiesForLobby = arguments.CustomLobbyProperties,
                }
            };
        }

        private static JoinRandomRoomArgs BuildJoinRandomRoomArgs(this MatchmakingArguments arguments)
        {
            return new JoinRandomRoomArgs
            {
                Ticket = arguments.Ticket,
                ExpectedUsers = arguments.ExpectedUsers,
                ExpectedCustomRoomProperties = arguments.CustomProperties,
                SqlLobbyFilter = arguments.SqlLobbyFilter,
                ExpectedMaxPlayers = arguments.MaxPlayers,
                Lobby = arguments.Lobby,
                MatchingType = arguments.RandomMatchingType
            };
        }
    }
}

namespace Photon.Realtime
{
    using Photon.Client;
    using System;
    using System.Collections;
#if NETCOREAPP3_1_OR_GREATER
    using System.Text.Json.Serialization;
#endif

    /// <summary>
    /// The arguments list for the matchmaking extension method ConnectToRoomAsync().
    /// </summary>
    [Serializable]
    public struct MatchmakingArguments
    {
        /// <summary>
        /// Photon Realtime <see cref="AppSettings"/>.
        /// </summary>
        public AppSettings PhotonSettings;
        /// <summary>
        /// Player TTL setting in seconds.
        /// Will be configured as <see cref="EnterRoomArgs.RoomOptions"/>.PlayerTtl when creating a Photon room.
        /// </summary>
        public int PlayerTtlInSeconds;
        /// <summary>
        /// Empty room TTL setting in seconds.
        /// Will be configured as <see cref="EnterRoomArgs.RoomOptions"/>.EmptyRoomTtl when creating a Photon room.
        /// </summary>
        public int EmptyRoomTtlInSeconds;
        /// <summary>
        /// Set a desired room name to create or join. If RoomName is null random matchmaking is used.
        /// Will be configured as <see cref="EnterRoomArgs.RoomOptions"/>.RoomName when creating a Photon room.
        /// </summary>
        public string RoomName;
        /// <summary>
        /// Max clients for the Photon room. 0 = unlimited. 
        /// Set on <see cref="JoinRandomRoomArgs.ExpectedMaxPlayers"/> and <see cref="EnterRoomArgs.RoomOptions"/>.MaxPlayers."/>
        /// </summary>
        public int MaxPlayers;
        /// <summary>
        /// Configure if the connect request can also create rooms or if it only tries to join.
        /// </summary>
        public bool CanOnlyJoin;
        /// <summary>
        /// Async configuration that include TaskFactory and global cancellation support. If null then <see cref="AsyncConfig.Global"/> is used.
        /// </summary>
        public AsyncConfig AsyncConfig;
        /// <summary>
        /// Optionally provide a client object. If null a new client object is created during the matchmaking process.
        /// </summary>
        public RealtimeClient NetworkClient;
        /// <summary>
        /// Provide authentication values for the Photon server connection. Use this in conjunction with custom authentication. This field is created when <see cref="UserId"/> is set.
        /// </summary>
        public AuthenticationValues AuthValues;
        /// <summary>
        /// Photon server plugin to connect to. 
        /// </summary>
        public string PluginName;
        /// <summary>
        /// Optional object to save and load reconnect information.
        /// </summary>
        public MatchmakingReconnectInformation ReconnectInformation;
        /// <summary>
        /// Custom room properties. 
        /// Set on <see cref="EnterRoomArgs.RoomOptions"/> and <see cref="JoinRandomRoomArgs.ExpectedCustomRoomProperties"/>
        /// </summary>
        public PhotonHashtable CustomProperties;
        /// <summary>
        /// An optional list of users users list blocks player slots for your friends or team mates to join the room, too.
        /// Set as <see cref="EnterRoomArgs.ExpectedUsers"/> or <see cref="JoinRandomRoomArgs.ExpectedUsers"/>.
        /// </summary>
        public string[] ExpectedUsers;
        /// <summary>
        /// Optional Photon Realtime lobby to use for matchmaking.
        /// Used for <see cref="JoinRandomRoomArgs.Lobby"/> and <see cref="EnterRoomArgs.Lobby"/>."
        /// </summary>
        public TypedLobby Lobby;
        /// <summary>
        /// Optional list of room properties that are used for lobby matchmaking.
        /// Will be configured as <see cref="EnterRoomArgs.RoomOptions"/>.
        /// </summary>
        public string[] CustomLobbyProperties;
        /// <summary>
        /// Optional SQL query to filter room matches used for <see cref="JoinRandomRoomArgs.SqlLobbyFilter"/>.
        /// </summary>
        public string SqlLobbyFilter;
        /// <summary>
        /// Optional Photon matchmaking ticket.
        /// Used for <see cref="JoinRandomRoomArgs.Ticket"/> and <see cref="EnterRoomArgs.Ticket"/>."/>
        /// </summary>
        public object Ticket;
        /// <summary>
        /// The MatchmakingMode affects how rooms get filled for random matchmaking. By default, the server fills rooms.
        /// Set on <see cref="JoinRandomRoomArgs.MatchingType"/>
        /// </summary>
        public MatchmakingMode RandomMatchingType;
        /// <summary>
        /// Optionally set explicit room options to use for <see cref="EnterRoomArgs.RoomOptions"/>.
        /// No properties will be set internally when this is set (see BuildEnterRoomArgs).
        /// </summary>
        public RoomOptions CustomRoomOptions;
        /// <summary>
        /// Optionally set an explicit initial <see cref="RoomOptions.IsVisible"/> value. Default is <see langword="true"/>
        /// </summary>
        public bool? IsRoomVisible;
        /// <summary>
        /// Optionally set an explicit initial <see cref="RoomOptions.IsOpen"/> value. Default is <see langword="true"/>
        /// </summary>
        public bool? IsRoomOpen;
        /// <summary>
        /// Enable CRC for Photon networking to detect corrupted packages.
        /// Building the checksum with <see cref="PhotonPeer.CrcEnabled"/> has a low processing overhead but increases integrity of sent and received data. 
        /// </summary>
        public bool EnableCrc;

        /// <summary>
        /// Creates authentication values object in field <see cref="AuthValues"/> and sets the <see cref="AuthenticationValues.UserId"/>.
        /// This property will be ignored in Json serialization.
        /// </summary>
#if NETCOREAPP3_1_OR_GREATER
      [JsonIgnore]
#endif
        public string UserId
        {
            get
            {
                return AuthValues?.UserId;
            }
            set
            {
                if (AuthValues == null)
                {
                    AuthValues = new AuthenticationValues();
                }
                AuthValues.UserId = value;
            }
        }

        /// <summary>
        /// Returns <see cref="PluginName"/> as a string array.
        /// </summary>
#if NETCOREAPP3_1_OR_GREATER
      [JsonIgnore]
#endif
        public string[] Plugins => string.IsNullOrEmpty(PluginName) ? new string[0] : new string[1] { PluginName };

        /// <summary>
        /// Returns if a client can rejoin a room which is only valid when <see cref="PlayerTtlInSeconds"/> is greater the 0.
        /// </summary>
#if NETCOREAPP3_1_OR_GREATER
      [JsonIgnore]
#endif
        public bool CanRejoin => PlayerTtlInSeconds > 0;

        /// <summary>
        /// Print some fields verbose.
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return $"PhotonSettings {PhotonSettings.ToStringFull()}\n" +
              $"PlayerTtlInSeconds: {PlayerTtlInSeconds}\n" +
              $"EmptyRoomTtlInSeconds${EmptyRoomTtlInSeconds}\n" +
              $"RoomName {RoomName}\n" +
              $"MaxPlayers {MaxPlayers}\n" +
              $"CanOnlyJoin {CanOnlyJoin}\n" +
              $"RealtimeClient {NetworkClient} \n" +
              $"AuthValues {AuthValues} \n" +
              $"ReconnectInformation {ReconnectInformation}";
        }

        /// <summary>
        /// Run validation and throw on missing fields.
        /// </summary>
        /// <exception cref="ArgumentException">Is thrown when some minimum configurations are missing.</exception>
        public void Validate()
        {
            Assert(MaxPlayers >= 0, "MaxPlayer must be greater or equal than 0");
            Assert(MaxPlayers < 256, "MaxPlayer must be less than 256");
            Assert(PhotonSettings != null, "PhotonSettings must be set");
        }

        private static void Assert(bool expected, string message)
        {
            if (!expected)
            {
                throw new ArgumentException(message);
            }
        }
    }
}

namespace Photon.Realtime
{
    using System;
#if NETCOREAPP3_1_OR_GREATER
    using System.Text.Json.Serialization;
#endif

    /// <summary>
    /// Reconnection information storage. 
    /// Internally runs <see cref="Set(RealtimeClient, TimeSpan)"/> during <see cref="MatchmakingExtensions.ConnectToRoomAsync(RealtimeClient, MatchmakingArguments)"/>.
    /// </summary>
    [Serializable]
    public class MatchmakingReconnectInformation
    {
        /// <summary>
        /// The room name the client was connected to.
        /// </summary>
        public string Room;
        /// <summary>
        /// The region the client was connected to.
        /// </summary>
        public string Region;
        /// <summary>
        /// The app version used in the former connection.
        /// </summary>
        public string AppVersion;
        /// <summary>
        /// The user id the client used to connect to the server.
        /// </summary>
        public string UserId;
        /// <summary>
        /// The timeout after this information is considered to useable.
        /// </summary>
        public long TimeoutInTicks;
        /// <summary>
        /// The default timeout that is used when <see cref="Set(RealtimeClient)"/> is called.
        /// </summary>
        public TimeSpan DefaultTimeout = TimeSpan.FromSeconds(20);

        /// <summary>
        /// Set and get <see cref="TimeoutInTicks"/>.
        /// </summary>
#if NETCOREAPP3_1_OR_GREATER
      [JsonIgnore]
#endif
        public DateTime Timeout
        {
            get => new DateTime(TimeoutInTicks);
            set => TimeoutInTicks = value.Ticks;
        }

        /// <summary>
        /// Checks the timeout and returns false when it ran out.
        /// </summary>
#if NETCOREAPP3_1_OR_GREATER
      [JsonIgnore]
#endif
        public bool HasTimedOut => Timeout < DateTime.Now;

        /// <summary>
        /// Set is called from the matchmaking when the connection has been successful.
        /// </summary>
        /// <param name="client">Photon client object.</param>
        public virtual void Set(RealtimeClient client) {
            Set(client, DefaultTimeout);
        }

        /// <summary>
        /// Update all info.
        /// </summary>
        /// <param name="client">Photon client object.</param>
        /// <param name="timeSpan">When is this data considered be too old to use.</param>
        public void Set(RealtimeClient client, TimeSpan timeSpan)
        {
            if (client == null)
            {
                return;
            }

            Room = client.CurrentRoom.Name;
            Region = client.CurrentRegion;
            Timeout = DateTime.Now + timeSpan;
            UserId = client.UserId;
            AppVersion = client.AppSettings.AppVersion;
        }

        /// <summary>
        /// Print readable information about its fields.
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return $"Room '{Room}' Region '{Region}' Timeout {Timeout}' AppVersion '{AppVersion}' UserId '{UserId}'";
        }
    }
}
