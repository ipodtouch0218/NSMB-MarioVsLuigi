// ----------------------------------------------------------------------------
// <copyright file="RealtimeClientOps.cs" company="Exit Games GmbH">
// Photon Realtime API - Copyright (C) 2022 Exit Games GmbH
// </copyright>
// <summary>
// Provides operations implemented by Photon "Realtime" servers.
// </summary>
// <author>developer@photonengine.com</author>
// ----------------------------------------------------------------------------

#if UNITY_2017_4_OR_NEWER
#define SUPPORTED_UNITY
#endif


namespace Photon.Realtime
{
    using System;
    using Photon.Client;
    using System.Collections.Generic;

    #if SUPPORTED_UNITY
    using UnityEngine;
    using Debug = UnityEngine.Debug;
    using SupportClass = Photon.Client.SupportClass;
    #endif


    /// <summary>
    /// Provides the operations for the RealtimeClient.
    /// </summary>
    public partial class RealtimeClient
    {
        private readonly Pool<ParameterDictionary> paramDictionaryPool = new Pool<ParameterDictionary>(
            () => new ParameterDictionary(),
            x => x.Clear(),
            1);



        /// <summary>Operation to get the list of available regions (short names and their IPs to ping them) for the set AppId. Results via OnRegionListReceived. Only available on Name Server.</summary>
        /// <remarks>This operation is usually called automatically by the API workflow. Use ConnectUsingSettings.</remarks>
        /// <seealso cref="IConnectionCallbacks.OnRegionListReceived"/>
        /// <returns>If the operation will get sent.</returns>
        public virtual bool OpGetRegions()
        {
            if (!this.CheckIfOpCanBeSent(OperationCode.GetRegions, this.Server, "GetRegions"))
            {
                return false;
            }

            Log.Info("OpGetRegions()", this.LogLevel, this.LogPrefix);

            ParameterDictionary opParameters = this.paramDictionaryPool.Acquire();
            opParameters[(byte)ParameterCode.ApplicationId] = this.AppSettings.GetAppId(this.ClientType);


            bool sending = this.RealtimePeer.SendOperation(OperationCode.GetRegions, opParameters, new SendOptions() { Reliability = true, Encrypt = true });
            this.paramDictionaryPool.Release(opParameters);

            return sending;
        }


        /// <summary>
        /// Request the rooms and online status for a list of friends. All clients should set a unique UserId before connecting. The result is available in this.FriendList.
        /// </summary>
        /// <remarks>
        /// Used on Master Server to find the rooms played by a selected list of users.
        /// The result will be stored in RealtimeClient.FriendList, which is null before the first server response.
        ///
        /// Users identify themselves by setting a UserId in the RealtimeClient instance.
        /// This will send the ID in OpAuthenticate during connect (to master and game servers).
        /// Note: Changing a player's name doesn't make sense when using a friend list.
        ///
        /// The list of usernames must be fetched from some other source (not provided by Photon).
        ///
        ///
        /// Internal:<br/>
        /// The server response includes 2 arrays of info (each index matching a friend from the request):<br/>
        /// ParameterCode.FindFriendsResponseOnlineList = bool[] of online states<br/>
        /// ParameterCode.FindFriendsResponseRoomIdList = string[] of room names (empty string if not in a room)<br/>
        /// <br/>
        /// The args may be used to define which state a room must match to be returned.
        /// </remarks>
        /// <param name="friendsToFind">Array of friend's names (make sure they are unique).</param>
        /// <param name="args">Options that affect the result of the FindFriends operation.</param>
        /// <returns>If the operation could be sent (requires connection).</returns>
        public bool OpFindFriends(string[] friendsToFind, FindFriendsArgs args = null)
        {
            if (!this.CheckIfOpCanBeSent(OperationCode.FindFriends, this.Server, "FindFriends"))
            {
                return false;
            }
            if (this.IsFetchingFriendList)
            {
                Log.Warn("OpFindFriends skipped: already fetching friends list.", this.LogLevel, this.LogPrefix);
                return false;   // fetching friends currently, so don't do it again (avoid changing the list while fetching friends)
            }
            if (friendsToFind == null || friendsToFind.Length == 0)
            {
                Log.Error("OpFindFriends skipped: friendsToFind array is null or empty.", this.LogLevel, this.LogPrefix);
                return false;
            }
            if (friendsToFind.Length > FriendRequestListMax)
            {
                Log.Error(string.Format("OpFindFriends skipped: friendsToFind array exceeds allowed length of {0}.", FriendRequestListMax), this.LogLevel, this.LogPrefix);
                return false;
            }


            List<string> friendsList = new List<string>(friendsToFind.Length);
            for (int i = 0; i < friendsToFind.Length; i++)
            {
                string friendUserId = friendsToFind[i];
                if (string.IsNullOrEmpty(friendUserId))
                {
                    Log.Warn(string.Format("friendsToFind array contains a null or empty UserId, element at position {0} skipped.", i), this.LogLevel, this.LogPrefix);
                }
                else if (friendUserId.Equals(UserId))
                {
                    Log.Warn($"friendsToFind array contains local player's UserId \"{friendUserId}\", element at position {i} skipped.", this.LogLevel, this.LogPrefix);
                }
                else if (friendsList.Contains(friendUserId))
                {
                    Log.Warn($"friendsToFind array contains duplicate UserId \"{friendUserId}\", element at position {i} skipped.", this.LogLevel, this.LogPrefix);
                }
                else
                {
                    friendsList.Add(friendUserId);
                }
            }

            if (friendsList.Count == 0)
            {
                Log.Error("OpFindFriends failed. No friends to find (check warnings).", this.LogLevel, this.LogPrefix);
                return false;
            }


            Log.Info("OpFindFriends()", this.LogLevel, this.LogPrefix);

            string[] filteredArray = friendsList.ToArray();
            ParameterDictionary opParameters = this.paramDictionaryPool.Acquire();
            if (filteredArray != null && filteredArray.Length > 0)
            {
                opParameters[ParameterCode.FindFriendsRequestList] = filteredArray;
            }

            if (args != null)
            {
                opParameters[ParameterCode.FindFriendsOptions] = args.ToIntFlags();
            }


            bool sending = this.RealtimePeer.SendOperation(OperationCode.FindFriends, opParameters, SendOptions.SendReliable);
            this.paramDictionaryPool.Release(opParameters);

            this.friendListRequested = sending ? filteredArray : null;

            return sending;
        }


        /// <summary>
        /// Joins the lobby on the Master Server, where you get a list of RoomInfos of currently open rooms. This is an async request triggers an OnOperationResponse() call and the callback OnJoinedLobby().
        /// </summary>
        /// <param name="lobby">The lobby join to.</param>
        /// <returns>If the operation could be sent (has to be connected).</returns>
        public virtual bool OpJoinLobby(TypedLobby lobby = null)
        {
            if (!this.CheckIfOpCanBeSent(OperationCode.JoinLobby, this.Server, "JoinLobby"))
            {
                return false;
            }
            if (lobby == null)
            {
                lobby = TypedLobby.Default;
            }

            Log.Info($"OpJoinLobby() {lobby}", this.LogLevel, this.LogPrefix);


            ParameterDictionary opParameters = this.paramDictionaryPool.Acquire();
            if (lobby != null && !lobby.IsDefault)
            {
                opParameters[(byte)ParameterCode.LobbyName] = lobby.Name;
                opParameters[(byte)ParameterCode.LobbyType] = (byte)lobby.Type;
            }


            bool sending = this.RealtimePeer.SendOperation(OperationCode.JoinLobby, opParameters, SendOptions.SendReliable);
            this.paramDictionaryPool.Release(opParameters);

            if (sending)
            {
                this.targetLobbyCache = lobby;
                this.State = ClientState.JoiningLobby;
            }

            return sending;
        }


        /// <summary>Opposite of joining a lobby. You don't have to explicitly leave a lobby to join another (client can be in one max, at any time).</summary>
        /// <returns>If the operation could be sent (has to be connected).</returns>
        public bool OpLeaveLobby()
        {
            if (!this.CheckIfOpCanBeSent(OperationCode.LeaveLobby, this.Server, "LeaveLobby"))
            {
                return false;
            }

            Log.Info("OpLeaveLobby()", this.LogLevel, this.LogPrefix);

            return this.RealtimePeer.SendOperation(OperationCode.LeaveLobby, null, SendOptions.SendReliable);
        }


        /// <summary>Used by OpJoinRoom and by OpCreateRoom alike.</summary>
        private void RoomOptionsToOpParameters(ParameterDictionary op, RoomOptions roomOptions, bool usePropertiesKey = false)
        {
            if (roomOptions == null)
            {
                roomOptions = new RoomOptions();
            }

            PhotonHashtable gameProperties = new PhotonHashtable();
            gameProperties[GamePropertyKey.IsOpen] = roomOptions.IsOpen;
            gameProperties[GamePropertyKey.IsVisible] = roomOptions.IsVisible;
            gameProperties[GamePropertyKey.PropsListedInLobby] = (roomOptions.CustomRoomPropertiesForLobby == null) ? new object[0] : roomOptions.CustomRoomPropertiesForLobby;
            gameProperties.Merge(roomOptions.CustomRoomProperties);


            if (roomOptions.MaxPlayers > 0)
            {
                // the following code is for compatibility with old and new servers. old use MaxPlayers, which has to be byte typed. MaxPlayersInt is available on new servers to allow int typed MaxPlayer values.
                // added to server 5.0.19.xyz / 6.0.19.xyz respectively
                byte maxPlayersAsByte = roomOptions.MaxPlayers <= byte.MaxValue ? (byte)roomOptions.MaxPlayers : (byte)0;

                gameProperties[GamePropertyKey.MaxPlayers] = maxPlayersAsByte;
                gameProperties[GamePropertyKey.MaxPlayersInt] = roomOptions.MaxPlayers;
            }

            if (!usePropertiesKey)
            {
                op[ParameterCode.GameProperties] = gameProperties;  // typically, the key for game props is 248
            }
            else
            {
                op[ParameterCode.Properties] = gameProperties;      // when an op uses 248 as filter, the "create room" props can be set as 251
            }


            int flags = 0;  // a new way to send the room args as bitwise-flags

            if (roomOptions.CleanupCacheOnLeave)
            {
                op[ParameterCode.CleanupCacheOnLeave] = true;	                // this defines the server's room settings and logic
                flags = flags | (int)RoomOptionBit.DeleteCacheOnLeave;          // this defines the server's room settings and logic (for servers that support flags)
            }
            else
            {
                op[ParameterCode.CleanupCacheOnLeave] = false;	                // this defines the server's room settings and logic
                gameProperties[GamePropertyKey.CleanupCacheOnLeave] = false;    // this is only informational for the clients which join
            }

            #if SERVERSDK
            op[ParameterCode.CheckUserOnJoin] = roomOptions.CheckUserOnJoin;
            if (roomOptions.CheckUserOnJoin)
            {
                flags = flags | (int) RoomOptionBit.CheckUserOnJoin;
            }
            #else
            // in PUN v1.88 and PUN 2, CheckUserOnJoin is set by default:
            flags = flags | (int) RoomOptionBit.CheckUserOnJoin;
            op[ParameterCode.CheckUserOnJoin] = true;
            #endif

            if (roomOptions.PlayerTtl > 0 || roomOptions.PlayerTtl == -1)
            {
                op[ParameterCode.PlayerTTL] = roomOptions.PlayerTtl;    // TURNBASED
            }

            if (roomOptions.EmptyRoomTtl > 0)
            {
                op[ParameterCode.EmptyRoomTTL] = roomOptions.EmptyRoomTtl;   //TURNBASED
            }

            if (roomOptions.SuppressRoomEvents)
            {
                flags = flags | (int)RoomOptionBit.SuppressRoomEvents;
                op[ParameterCode.SuppressRoomEvents] = true;
            }
            if (roomOptions.SuppressPlayerInfo)
            {
                flags = flags | (int)RoomOptionBit.SuppressPlayerInfo;
            }

            if (roomOptions.Plugins != null)
            {
                op[ParameterCode.Plugins] = roomOptions.Plugins;
            }
            if (roomOptions.PublishUserId)
            {
                flags = flags | (int)RoomOptionBit.PublishUserId;
                op[ParameterCode.PublishUserId] = true;
            }
            if (roomOptions.DeleteNullProperties)
            {
                flags = flags | (int)RoomOptionBit.DeleteNullProps; // this is only settable as flag
            }
            if (roomOptions.BroadcastPropsChangeToAll)
            {
                flags = flags | (int)RoomOptionBit.BroadcastPropsChangeToAll; // this is only settable as flag
            }

            op[ParameterCode.RoomOptionFlags] = flags;
        }


        /// <summary>
        /// Joins a random room that matches the filter. Will callback: OnJoinedRoom or OnJoinRandomFailed.
        /// </summary>
        /// <remarks>
        /// Used for random matchmaking. You can join any room or one with specific properties defined in joinRandomRoomArgs.
        ///
        /// You can use expectedCustomRoomProperties and expectedMaxPlayers as filters for accepting rooms.
        /// If you set expectedCustomRoomProperties, a room must have the exact same key values set at Custom Properties.
        /// You need to define which Custom Room Properties will be available for matchmaking when you create a room.
        /// See: OpCreateRoom(string roomName, RoomOptions roomOptions, TypedLobby lobby)
        ///
        /// This operation fails if no rooms are fitting or available (all full, closed or not visible).
        /// It may also fail when actually joining the room which was found. Rooms may close, become full or empty anytime.
        ///
        /// This method can only be called while the client is connected to a Master Server so you should
        /// implement the callback OnConnectedToMaster.
        /// Check the return value to make sure the operation will be called on the server.
        /// Note: There will be no callbacks if this method returned false.
        ///
        ///
        /// This client's State is set to ClientState.Joining immediately, when the operation could
        /// be called. In the background, the client will switch servers and call various related operations.
        ///
        /// When you're in the room, this client's State will become ClientState.Joined.
        ///
        ///
        /// When entering a room, this client's Player Custom Properties will be sent to the room.
        /// Use LocalPlayer.SetCustomProperties to set them, even while not yet in the room.
        /// Note that the player properties will be cached locally and are not wiped when leaving a room.
        ///
        /// More about matchmaking:
        /// https://doc.photonengine.com/en-us/realtime/current/reference/matchmaking-and-lobby
        ///
        /// You can define an array of expectedUsers, to block player slots in the room for these users.
        /// The corresponding feature in Photon is called "Slot Reservation" and can be found in the doc pages.
        /// </remarks>
        /// <param name="joinRandomRoomArgs">Optional definition of properties to filter rooms in random matchmaking.</param>
        /// <returns>If the operation could be sent currently (requires connection to Master Server).</returns>
        public bool OpJoinRandomRoom(JoinRandomRoomArgs joinRandomRoomArgs = null)
        {
            if (!this.CheckIfOpCanBeSent(OperationCode.JoinRandomGame, this.Server, "JoinRandomGame"))
            {
                return false;
            }
            if (joinRandomRoomArgs == null)
            {
                joinRandomRoomArgs = new JoinRandomRoomArgs();
            }
            if (!joinRandomRoomArgs.ExpectedCustomRoomProperties.CustomPropKeyTypesValid(true))
            {
                Log.Error("OpJoinRandomRoom() expected properties must use key type of string or int.", this.LogLevel, this.LogPrefix);
                return false;
            }

            Log.Info($"OpJoinRandomRoom() {this.GetMatchmakingHash(joinRandomRoomArgs.Lobby)}", this.LogLevel, this.LogPrefix);


            PhotonHashtable expectedRoomProperties = new PhotonHashtable();
            expectedRoomProperties.Merge(joinRandomRoomArgs.ExpectedCustomRoomProperties);

            if (joinRandomRoomArgs.ExpectedMaxPlayers > 0)
            {
                // the following code is for compatibility with old and new servers. old use MaxPlayers, which has to be byte typed. MaxPlayersInt is available on new servers to allow int typed MaxPlayer values.
                // added to server 5.0.19.xyz / 6.0.19.xyz respectively
                byte maxPlayersAsByte = joinRandomRoomArgs.ExpectedMaxPlayers <= byte.MaxValue ? (byte)joinRandomRoomArgs.ExpectedMaxPlayers : (byte)0;

                expectedRoomProperties[GamePropertyKey.MaxPlayers] = maxPlayersAsByte;
                if (joinRandomRoomArgs.ExpectedMaxPlayers > byte.MaxValue)
                {
                    expectedRoomProperties[GamePropertyKey.MaxPlayersInt] = joinRandomRoomArgs.ExpectedMaxPlayers;
                }
            }

            ParameterDictionary opParameters = this.paramDictionaryPool.Acquire();
            if (expectedRoomProperties.Count > 0)
            {
                opParameters[ParameterCode.GameProperties] = expectedRoomProperties;
            }

            if (joinRandomRoomArgs.MatchingType != MatchmakingMode.FillRoom)
            {
                opParameters[ParameterCode.MatchMakingType] = (byte)joinRandomRoomArgs.MatchingType;
            }

            if (joinRandomRoomArgs.Lobby != null && !joinRandomRoomArgs.Lobby.IsDefault)
            {
                opParameters[ParameterCode.LobbyName] = joinRandomRoomArgs.Lobby.Name;
                opParameters[ParameterCode.LobbyType] = (byte)joinRandomRoomArgs.Lobby.Type;
            }

            if (!string.IsNullOrEmpty(joinRandomRoomArgs.SqlLobbyFilter))
            {
                opParameters[ParameterCode.Data] = joinRandomRoomArgs.SqlLobbyFilter;
            }

            if (joinRandomRoomArgs.ExpectedUsers != null && joinRandomRoomArgs.ExpectedUsers.Length > 0)
            {
                opParameters[ParameterCode.Add] = joinRandomRoomArgs.ExpectedUsers;
            }

            if (joinRandomRoomArgs.Ticket != null)
            {
                opParameters[ParameterCode.Ticket] = joinRandomRoomArgs.Ticket;
            }

            opParameters[ParameterCode.AllowRepeats] = true; // enables temporary queueing for low ccu matchmaking situations


            //this.Listener.DebugReturn(LogLevel.Info, "OpJoinRandomRoom: " + SupportClass.DictionaryToString(opParameters));
            bool sending = this.RealtimePeer.SendOperation(OperationCode.JoinRandomGame, opParameters, SendOptions.SendReliable);
            this.paramDictionaryPool.Release(opParameters);

            if (sending)
            {
                this.State = ClientState.Joining;
                this.lastJoinType = JoinType.JoinRandomRoom;

                this.enterRoomArgumentsCache = new EnterRoomArgs();
                this.enterRoomArgumentsCache.Lobby = this.CurrentLobby != null && !this.CurrentLobby.IsDefault && joinRandomRoomArgs.Lobby == null ? this.CurrentLobby : joinRandomRoomArgs.Lobby;
                this.enterRoomArgumentsCache.ExpectedUsers = joinRandomRoomArgs.ExpectedUsers;
                if (joinRandomRoomArgs.Ticket != null)
                {
                    this.enterRoomArgumentsCache.Ticket = joinRandomRoomArgs.Ticket;
                }
            }

            return sending;
        }


        /// <summary>
        /// Attempts to join a room that matches the specified filter and creates a room if none found.
        /// </summary>
        /// <remarks>
        /// This operation is a combination of filter-based random matchmaking with the option to create a new room,
        /// if no fitting room exists.
        /// The benefit of that is that the room creation is done by the same operation and the room can be found
        /// by the very next client, looking for similar rooms.
        ///
        /// There are separate parameters for joining and creating a room.
        ///
        /// Tickets: Both parameter types have a Ticket value. It is enough to set the joinRandomRoomArgs.Ticket.
        /// The createRoomArgs.Ticket will not be used.
        ///
        /// This method can only be called while connected to a Master Server.
        /// This client's State is set to ClientState.Joining immediately.
        ///
        /// Either IMatchmakingCallbacks.OnJoinedRoom or IMatchmakingCallbacks.OnCreatedRoom get called.
        ///
        /// More about matchmaking:
        /// https://doc.photonengine.com/en-us/realtime/current/reference/matchmaking-and-lobby
        ///
        /// Check the return value to make sure the operation will be called on the server.
        /// Note: There will be no callbacks if this method returned false.
        /// </remarks>
        /// <returns>If the operation will be sent (requires connection to Master Server).</returns>
        public bool OpJoinRandomOrCreateRoom(JoinRandomRoomArgs joinRandomRoomArgs = null, EnterRoomArgs createRoomArgs = null)
        {
            if (!this.CheckIfOpCanBeSent(OperationCode.JoinRandomGame, this.Server, "OpJoinRandomOrCreateRoom"))
            {
                return false;
            }
            if (joinRandomRoomArgs == null)
            {
                joinRandomRoomArgs = new JoinRandomRoomArgs();
            }
            if (createRoomArgs == null)
            {
                createRoomArgs = new EnterRoomArgs();
            }
            if (!joinRandomRoomArgs.ExpectedCustomRoomProperties.CustomPropKeyTypesValid(true))
            {
                Log.Error("OpJoinRandomOrCreateRoom() expected properties must use key type of string or int.", this.LogLevel, this.LogPrefix);
                return false;
            }


            Log.Info($"OpJoinRandomOrCreateRoom() {this.GetMatchmakingHash(joinRandomRoomArgs.Lobby)}", this.LogLevel, this.LogPrefix);



            createRoomArgs.JoinMode = JoinMode.CreateIfNotExists;


            // join random room parameters:

            PhotonHashtable expectedRoomProperties = new PhotonHashtable();
            expectedRoomProperties.Merge(joinRandomRoomArgs.ExpectedCustomRoomProperties);

            if (joinRandomRoomArgs.ExpectedMaxPlayers > 0)
            {
                // the following code is for compatibility with old and new servers. old use MaxPlayers, which has to be byte typed. MaxPlayersInt is available on new servers to allow int typed MaxPlayer values.
                // added to server 5.0.19.xyz / 6.0.19.xyz respectively
                byte maxPlayersAsByte = joinRandomRoomArgs.ExpectedMaxPlayers <= byte.MaxValue ? (byte)joinRandomRoomArgs.ExpectedMaxPlayers : (byte)0;

                expectedRoomProperties[GamePropertyKey.MaxPlayers] = maxPlayersAsByte;
                if (joinRandomRoomArgs.ExpectedMaxPlayers > byte.MaxValue)
                {
                    expectedRoomProperties[GamePropertyKey.MaxPlayersInt] = joinRandomRoomArgs.ExpectedMaxPlayers;
                }
            }

            ParameterDictionary opParameters = this.paramDictionaryPool.Acquire();
            if (expectedRoomProperties.Count > 0)
            {
                opParameters[ParameterCode.GameProperties] = expectedRoomProperties; // used as filter. below, RoomOptionsToOpParameters has usePropertiesKey = true
            }

            if (joinRandomRoomArgs.MatchingType != MatchmakingMode.FillRoom)
            {
                opParameters[ParameterCode.MatchMakingType] = (byte)joinRandomRoomArgs.MatchingType;
            }

            if (joinRandomRoomArgs.Lobby != null && !joinRandomRoomArgs.Lobby.IsDefault)
            {
                opParameters[ParameterCode.LobbyName] = joinRandomRoomArgs.Lobby.Name;
                opParameters[ParameterCode.LobbyType] = (byte)joinRandomRoomArgs.Lobby.Type;
            }

            if (!string.IsNullOrEmpty(joinRandomRoomArgs.SqlLobbyFilter))
            {
                opParameters[ParameterCode.Data] = joinRandomRoomArgs.SqlLobbyFilter;
            }

            if (joinRandomRoomArgs.ExpectedUsers != null && joinRandomRoomArgs.ExpectedUsers.Length > 0)
            {
                opParameters[ParameterCode.Add] = joinRandomRoomArgs.ExpectedUsers;
            }

            if (joinRandomRoomArgs.Ticket != null)
            {
                opParameters[ParameterCode.Ticket] = joinRandomRoomArgs.Ticket;
            }


            // parameters for creating a room if needed ("or create" part of the operation)
            // partial copy of OpCreateRoom

            opParameters[ParameterCode.JoinMode] = (byte)JoinMode.CreateIfNotExists;
            opParameters[ParameterCode.AllowRepeats] = true;    // enables temporary queueing for low ccu matchmaking situations


            if (!string.IsNullOrEmpty(createRoomArgs.RoomName))
            {
                opParameters[ParameterCode.RoomName] = createRoomArgs.RoomName;
            }


            //this.Listener.DebugReturn(LogLevel.Info, "OpJoinRandomOrCreateRoom: " + SupportClass.DictionaryToString(opParameters, false));
            bool sending = this.RealtimePeer.SendOperation(OperationCode.JoinRandomGame, opParameters, SendOptions.SendReliable);
            this.paramDictionaryPool.Release(opParameters);

            if (sending)
            {
                this.State = ClientState.Joining;
                this.lastJoinType = JoinType.JoinRandomOrCreateRoom;

                this.enterRoomArgumentsCache = EnterRoomArgs.ShallowCopyToNewArgs(createRoomArgs);
                this.enterRoomArgumentsCache.Lobby = this.CurrentLobby != null && !this.CurrentLobby.IsDefault && joinRandomRoomArgs.Lobby == null ? this.CurrentLobby : joinRandomRoomArgs.Lobby;
                this.enterRoomArgumentsCache.ExpectedUsers = joinRandomRoomArgs.ExpectedUsers;
                if (joinRandomRoomArgs.Ticket != null)
                {
                    this.enterRoomArgumentsCache.Ticket = joinRandomRoomArgs.Ticket;
                }
            }
            return sending;
        }


        /// <summary>
        /// Creates a new room. Will callback: OnCreatedRoom and OnJoinedRoom or OnCreateRoomFailed.
        /// </summary>
        /// <remarks>
        /// When successful, the client will enter the specified room and callback both OnCreatedRoom and OnJoinedRoom.
        /// In all error cases, OnCreateRoomFailed gets called.
        ///
        /// Creating a room will fail if the room name is already in use or when the RoomOptions clashing
        /// with one another. Check the EnterRoomArgs reference for the various room creation args.
        ///
        ///
        /// This method can only be called while the client is connected to a Master Server so you should
        /// implement the callback OnConnectedToMaster.
        /// Check the return value to make sure the operation will be called on the server.
        /// Note: There will be no callbacks if this method returned false.
        ///
        ///
        /// When you're in the room, this client's State will become ClientState.Joined.
        ///
        ///
        /// When entering a room, this client's Player Custom Properties will be sent to the room.
        /// Use LocalPlayer.SetCustomProperties to set them, even while not yet in the room.
        /// Note that the player properties will be cached locally and are not wiped when leaving a room.
        ///
        /// You can define an array of expectedUsers, to block player slots in the room for these users.
        /// The corresponding feature in Photon is called "Slot Reservation" and can be found in the doc pages.
        /// </remarks>
        /// <param name="enterRoomArgs">Definition of properties for the room to create.</param>
        /// <returns>If the operation could be sent currently (requires connection to Master Server).</returns>
        public bool OpCreateRoom(EnterRoomArgs enterRoomArgs)
        {
            if (!this.CheckIfOpCanBeSent(OperationCode.CreateGame, this.Server, "CreateGame"))
            {
                return false;
            }
            if (enterRoomArgs == null)
            {
                Log.Error("OpCreateRoom() failed. Parameter enterRoomArgs can not be null.", this.LogLevel, this.LogPrefix);
                return false;
            }

            bool onGameServer = this.Server == ServerConnection.GameServer;
            enterRoomArgs.OnGameServer = onGameServer;
            if (!onGameServer)
            {
                this.enterRoomArgumentsCache = enterRoomArgs;
                this.enterRoomArgumentsCache.Lobby = this.CurrentLobby != null && !this.CurrentLobby.IsDefault && enterRoomArgs.Lobby == null ? this.CurrentLobby : enterRoomArgs.Lobby;
            }

            Log.Info($"OpCreateRoom() {this.GetMatchmakingHash(enterRoomArgs.Lobby)}", this.LogLevel, this.LogPrefix);
            bool sending = this.OpCreateRoomIntern(enterRoomArgs);
            if (sending)
            {
                this.lastJoinType = JoinType.CreateRoom;
                this.State = ClientState.Joining;
            }
            return sending;
        }


        /// <summary>
        /// Joins a specific room by name and creates it on demand. Will callback: OnJoinedRoom or OnJoinRoomFailed.
        /// </summary>
        /// <remarks>
        /// Useful when players make up a room name to meet in:
        /// All involved clients call the same method and whoever is first, also creates the room.
        ///
        /// When successful, the client will enter the specified room.
        /// The client which creates the room, will callback both OnCreatedRoom and OnJoinedRoom.
        /// Clients that join an existing room will only callback OnJoinedRoom.
        /// In all error cases, OnJoinRoomFailed gets called.
        ///
        /// Joining a room will fail, if the room is full, closed or when the user
        /// already is present in the room (checked by userId).
        ///
        /// To return to a room, use OpRejoinRoom.
        ///
        /// This method can only be called while the client is connected to a Master Server so you should
        /// implement the callback OnConnectedToMaster.
        /// Check the return value to make sure the operation will be called on the server.
        /// Note: There will be no callbacks if this method returned false.
        ///
        /// This client's State is set to ClientState.Joining immediately, when the operation could
        /// be called. In the background, the client will switch servers and call various related operations.
        ///
        /// When you're in the room, this client's State will become ClientState.Joined.
        ///
        ///
        /// If you set room properties in roomOptions, they get ignored when the room is existing already.
        /// This avoids changing the room properties by late joining players.
        ///
        /// When entering a room, this client's Player Custom Properties will be sent to the room.
        /// Use LocalPlayer.SetCustomProperties to set them, even while not yet in the room.
        /// Note that the player properties will be cached locally and are not wiped when leaving a room.
        ///
        /// You can define an array of expectedUsers, to block player slots in the room for these users.
        /// The corresponding feature in Photon is called "Slot Reservation" and can be found in the doc pages.
        /// </remarks>
        /// <param name="enterRoomArgs">Definition of properties for the room to create or join.</param>
        /// <returns>If the operation could be sent currently (requires connection to Master Server).</returns>
        public bool OpJoinOrCreateRoom(EnterRoomArgs enterRoomArgs)
        {
            if (!this.CheckIfOpCanBeSent(OperationCode.JoinGame, this.Server, "JoinOrCreateRoom"))
            {
                return false;
            }
            if (enterRoomArgs == null)
            {
                Log.Error("OpJoinOrCreateRoom() failed. Parameter enterRoomArgs can not be null.", this.LogLevel, this.LogPrefix);
                return false;
            }

            bool onGameServer = this.Server == ServerConnection.GameServer;
            enterRoomArgs.JoinMode = JoinMode.CreateIfNotExists;
            enterRoomArgs.OnGameServer = onGameServer;
            if (!onGameServer)
            {
                Log.Info($"OpJoinOrCreateRoom({enterRoomArgs.RoomName}) {this.GetMatchmakingHash(enterRoomArgs.Lobby)}", this.LogLevel, this.LogPrefix);
                this.enterRoomArgumentsCache = enterRoomArgs;
                this.enterRoomArgumentsCache.Lobby = this.CurrentLobby != null && !this.CurrentLobby.IsDefault && enterRoomArgs.Lobby == null ? this.CurrentLobby : enterRoomArgs.Lobby;
                if (enterRoomArgs.Ticket != null)
                {
                    this.enterRoomArgumentsCache.Ticket = enterRoomArgs.Ticket;
                }
            }

            bool sending = this.OpJoinRoomIntern(enterRoomArgs);
            if (sending)
            {
                this.lastJoinType = JoinType.JoinOrCreateRoom;
                this.State = ClientState.Joining;
            }
            return sending;
        }


        /// <summary>
        /// Joins a room by name. Will callback: OnJoinedRoom or OnJoinRoomFailed.
        /// </summary>
        /// <remarks>
        /// Useful when using lobbies or when players follow friends or invite each other.
        ///
        /// When successful, the client will enter the specified room and callback via OnJoinedRoom.
        /// In all error cases, OnJoinRoomFailed gets called.
        ///
        /// Joining a room will fail if the room is full, closed, not existing or when the user
        /// already is present in the room (checked by userId).
        ///
        /// To return to a room, use OpRejoinRoom.
        /// When players invite each other and it's unclear who's first to respond, use OpJoinOrCreateRoom instead.
        ///
        /// This method can only be called while the client is connected to a Master Server so you should
        /// implement the callback OnConnectedToMaster.
        /// Check the return value to make sure the operation will be called on the server.
        /// Note: There will be no callbacks if this method returned false.
        ///
        /// A room's name has to be unique (per region, appid and gameversion).
        /// When your title uses a global matchmaking or invitations (e.g. an external solution),
        /// keep regions and the game versions in mind to join a room.
        ///
        ///
        /// This client's State is set to ClientState.Joining immediately, when the operation could
        /// be called. In the background, the client will switch servers and call various related operations.
        ///
        /// When you're in the room, this client's State will become ClientState.Joined.
        ///
        ///
        /// When entering a room, this client's Player Custom Properties will be sent to the room.
        /// Use LocalPlayer.SetCustomProperties to set them, even while not yet in the room.
        /// Note that the player properties will be cached locally and are not wiped when leaving a room.
        ///
        /// You can define an array of expectedUsers, to reserve player slots in the room for friends or party members.
        /// The corresponding feature in Photon is called "Slot Reservation" and can be found in the doc pages.
        /// </remarks>
        /// <param name="enterRoomArgs">Definition of properties for the room to join.</param>
        /// <returns>If the operation could be sent currently (requires connection to Master Server).</returns>
        public bool OpJoinRoom(EnterRoomArgs enterRoomArgs)
        {
            if (!this.CheckIfOpCanBeSent(OperationCode.JoinGame, this.Server, "JoinRoom"))
            {
                return false;
            }
            if (enterRoomArgs == null)
            {
                Log.Error("OpJoinRoom() failed. Parameter enterRoomArgs can not be null.", this.LogLevel, this.LogPrefix);
                return false;
            }

            bool onGameServer = this.Server == ServerConnection.GameServer;
            enterRoomArgs.OnGameServer = onGameServer;
            if (!onGameServer)
            {
                this.enterRoomArgumentsCache = enterRoomArgs;
                this.enterRoomArgumentsCache.Lobby = null;
            }

            Log.Info($"OpJoinRoom() {this.GetMatchmakingHash(null)}", this.LogLevel, this.LogPrefix);
            bool sending = this.OpJoinRoomIntern(enterRoomArgs);
            if (sending)
            {
                this.lastJoinType = enterRoomArgs.JoinMode == JoinMode.CreateIfNotExists ? JoinType.JoinOrCreateRoom : JoinType.JoinRoom;
                this.State = ClientState.Joining;
            }
            return sending;
        }


        /// <summary>
        /// Creates a room (on either Master or Game Server).
        /// The OperationResponse depends on the server the peer is connected to:
        /// Master will return a Game Server to connect to.
        /// Game Server will return the joined Room's data.
        /// This is an async request which triggers a OnOperationResponse() call.
        /// </summary>
        /// <remarks>
        /// If the room is already existing, the OperationResponse will have a returnCode of ErrorCode.GameAlreadyExists.
        /// </remarks>
        private bool OpCreateRoomIntern(EnterRoomArgs opArgs)
        {
            if (opArgs == null)
            {
                Log.Error("OpCreateRoom() failed. Parameter opArgs must be non null.", this.LogLevel, this.LogPrefix);
                return false;
            }

            if (opArgs.RoomOptions == null)
            {
                opArgs.RoomOptions = new RoomOptions();
            }

            if (!opArgs.RoomOptions.CustomRoomProperties.CustomPropKeyTypesValid(true))
            {
                Log.Error("OpCreateRoom() failed. Custom Room Properties contains key which is not string nor int.", this.LogLevel, this.LogPrefix);
                return false;
            }

            if (!opArgs.RoomOptions.CustomRoomPropertiesForLobby.CustomPropKeyTypesValid(true))
            {
                Log.Error("OpCreateRoom() failed. RoomOptions.CustomRoomPropertiesForLobby can be null, have zero items or all items must be int or string.", this.LogLevel, this.LogPrefix);
                return false;
            }


            Log.Info("OpCreateRoom()", this.LogLevel, this.LogPrefix);


            ParameterDictionary opParameters = this.paramDictionaryPool.Acquire();

            if (!string.IsNullOrEmpty(opArgs.RoomName))
            {
                opParameters[ParameterCode.RoomName] = opArgs.RoomName;
            }
            if (opArgs.Lobby != null && !opArgs.Lobby.IsDefault)
            {
                opParameters[ParameterCode.LobbyName] = opArgs.Lobby.Name;
                opParameters[ParameterCode.LobbyType] = (byte)opArgs.Lobby.Type;
            }

            if (opArgs.ExpectedUsers != null && opArgs.ExpectedUsers.Length > 0)
            {
                opParameters[ParameterCode.Add] = opArgs.ExpectedUsers;
            }
            if (opArgs.Ticket != null)
            {
                opParameters[ParameterCode.Ticket] = opArgs.Ticket;
            }


            if (opArgs.OnGameServer)
            {
                // send player props for GS if available
                if (this.LocalPlayer != null)
                {
                    if (!string.IsNullOrEmpty(this.LocalPlayer.NickName))
                    {
                        if (this.LocalPlayer.CustomProperties == null)
                        {
                            this.LocalPlayer.CustomProperties = new PhotonHashtable();
                        }
                        this.LocalPlayer.CustomProperties[ActorProperties.NickName] = this.LocalPlayer.NickName;
                    }

                    if (this.LocalPlayer.CustomProperties != null && this.LocalPlayer.CustomProperties.Count > 0)
                    {
                        opParameters[ParameterCode.PlayerProperties] = this.LocalPlayer.CustomProperties;
                    }
                }
                // ask GS to broadcast actor properties
                opParameters[ParameterCode.Broadcast] = true;

                // write the room properties / options
                this.RoomOptionsToOpParameters(opParameters, opArgs.RoomOptions);
            }


            //this.Listener.DebugReturn(LogLevel.Info, "OpCreateRoom: " + SupportClass.DictionaryToString(op));
            bool sending = this.RealtimePeer.SendOperation(OperationCode.CreateGame, opParameters, SendOptions.SendReliable);
            this.paramDictionaryPool.Release(opParameters);

            return sending;
        }


        /// <summary>
        /// Joins a room by name or creates new room if room with given name not exists.
        /// The OperationResponse depends on the server the peer is connected to:
        /// Master will return a Game Server to connect to.
        /// Game Server will return the joined Room's data.
        /// This is an async request which triggers a OnOperationResponse() call.
        /// </summary>
        /// <remarks>
        /// If the room is not existing (anymore), the OperationResponse will have a returnCode of ErrorCode.GameDoesNotExist.
        /// Other possible ErrorCodes are: GameClosed, GameFull.
        /// </remarks>
        /// <returns>If the operation could be sent (requires connection).</returns>
        private bool OpJoinRoomIntern(EnterRoomArgs opArgs)
        {
            if (opArgs == null)
            {
                Log.Error("OpJoinRoom() failed. Parameter opArgs must be non null.", this.LogLevel, this.LogPrefix);
                return false;
            }

            //Log.Info("OpJoinRoom()", this.LogLevel, this.LogPrefix);


            ParameterDictionary opParameters = this.paramDictionaryPool.Acquire();
            if (!string.IsNullOrEmpty(opArgs.RoomName))
            {
                opParameters[ParameterCode.RoomName] = opArgs.RoomName;
            }

            if (opArgs.JoinMode == JoinMode.CreateIfNotExists)
            {
                opParameters[ParameterCode.JoinMode] = (byte)JoinMode.CreateIfNotExists;
                if (opArgs.Lobby != null && !opArgs.Lobby.IsDefault)
                {
                    opParameters[ParameterCode.LobbyName] = opArgs.Lobby.Name;
                    opParameters[ParameterCode.LobbyType] = (byte)opArgs.Lobby.Type;
                }
            }
            else if (opArgs.JoinMode == JoinMode.RejoinOnly)
            {
                opParameters[ParameterCode.JoinMode] = (byte)JoinMode.RejoinOnly; // changed from JoinMode.JoinOrRejoin
            }

            if (opArgs.ExpectedUsers != null && opArgs.ExpectedUsers.Length > 0)
            {
                opParameters[ParameterCode.Add] = opArgs.ExpectedUsers;
            }
            if (opArgs.Ticket != null)
            {
                opParameters[ParameterCode.Ticket] = opArgs.Ticket;
            }

            if (opArgs.OnGameServer)
            {
                // send no player properties when rejoining!
                if (this.LocalPlayer != null && opArgs.JoinMode != JoinMode.RejoinOnly)
                {
                    if (!string.IsNullOrEmpty(this.LocalPlayer.NickName))
                    {
                        if (this.LocalPlayer.CustomProperties == null)
                        {
                            this.LocalPlayer.CustomProperties = new PhotonHashtable();
                        }
                        this.LocalPlayer.CustomProperties[ActorProperties.NickName] = this.LocalPlayer.NickName;
                    }

                    if (this.LocalPlayer.CustomProperties != null && this.LocalPlayer.CustomProperties.Count > 0)
                    {
                        opParameters[ParameterCode.PlayerProperties] = this.LocalPlayer.CustomProperties;
                    }
                }
                // ask GS to broadcast actor properties
                opParameters[ParameterCode.Broadcast] = true;

                // write the room properties / options
                this.RoomOptionsToOpParameters(opParameters, opArgs.RoomOptions);
            }

            //Log.Info("OpJoinRoomIntern: " + SupportClass.DictionaryToString(opParameters), this.LogLevel, this.LogPrefix);
            bool sending = this.RealtimePeer.SendOperation(OperationCode.JoinGame, opParameters, SendOptions.SendReliable);
            this.paramDictionaryPool.Release(opParameters);

            return sending;
        }


        /// <summary>
        /// Leaves the current room, optionally telling the server that the user is just becoming inactive. Will callback: OnLeftRoom.
        /// </summary>
        ///
        /// <remarks>
        /// OpLeaveRoom skips execution when the room is null or the server is not GameServer or the client is disconnecting from GS already.
        /// OpLeaveRoom returns false in those cases and won't change the state, so check return of this method.
        ///
        /// In some cases, this method will skip the OpLeave call and just call Disconnect(),
        /// which not only leaves the room but also the server. Disconnect also triggers a leave and so that workflow is is quicker.
        /// </remarks>
        /// <param name="becomeInactive">If true, this player becomes inactive in the game and can return later (if PlayerTTL of the room is != 0).</param>
        /// <returns>If the current room could be left (impossible while not in a room).</returns>
        public virtual bool OpLeaveRoom(bool becomeInactive)
        {
            if (!this.CheckIfOpCanBeSent(OperationCode.Leave, this.Server, "LeaveRoom"))
            {
                return false;
            }

            Log.Info(string.Format("OpLeaveRoom({0})", becomeInactive ? "inactive=true" : ""), this.LogLevel, this.LogPrefix);


            this.State = ClientState.Leaving;
            this.GameServerAddress = String.Empty;
            this.enterRoomArgumentsCache = null;


            ParameterDictionary opParameters = this.paramDictionaryPool.Acquire();
            if (becomeInactive)
            {
                opParameters[ParameterCode.IsInactive] = true;
            }

            bool sending = this.RealtimePeer.SendOperation(OperationCode.Leave, opParameters, SendOptions.SendReliable);
            this.paramDictionaryPool.Release(opParameters);

            return sending;
        }


        /// <summary>
        /// Rejoins a room by roomName (using the userID internally to return).  Will callback: OnJoinedRoom or OnJoinRoomFailed.
        /// </summary>
        /// <remarks>
        /// Used to return to a room, before this user was removed from the players list.
        /// Internally, the userID will be checked by the server, to make sure this user is in the room (active or inactice).
        ///
        /// In contrast to join, this operation never adds a players to a room. It will attempt to retake an existing
        /// spot in the playerlist or fail. This makes sure the client doean't accidentally join a room when the
        /// game logic meant to re-activate an existing actor in an existing room.
        ///
        /// This method will fail on the server, when the room does not exist, can't be loaded (persistent rooms) or
        /// when the userId is not in the player list of this room. This will lead to a callback OnJoinRoomFailed.
        ///
        /// Rejoining room will not send any player properties. Instead client will receive up-to-date ones from server.
        /// If you want to set new player properties, do it once rejoined.
        ///
        /// Tickets: If the server requires use of Tickets or if the room was entered with a Ticket initially,
        /// you will have to provide a fitting ticket. Ticket have an internal expiry date time, so they
        /// may become unusable for a rejoin.
        /// </remarks>
        public bool OpRejoinRoom(string roomName, object ticket = null)
        {
            if (!this.CheckIfOpCanBeSent(OperationCode.JoinGame, this.Server, "RejoinRoom"))
            {
                return false;
            }

            Log.Info(string.Format("OpRejoinRoom({0})", roomName), this.LogLevel, this.LogPrefix);


            bool onGameServer = this.Server == ServerConnection.GameServer;

            EnterRoomArgs opArgs = new EnterRoomArgs();
            opArgs.RoomName = roomName;
            opArgs.OnGameServer = onGameServer;
            opArgs.JoinMode = JoinMode.RejoinOnly;
            opArgs.Ticket = ticket;
            this.enterRoomArgumentsCache = opArgs;

            bool sending = this.OpJoinRoomIntern(opArgs);
            if (sending)
            {
                this.lastJoinType = JoinType.JoinRoom;
                this.State = ClientState.Joining;
            }
            return sending;
        }


        ///  <summary>
        ///  Updates and synchronizes a Player's Custom Properties. Optionally, expectedProperties can be provided as condition.
        ///  </summary>
        ///  <remarks>
        ///  Custom Properties are a set of string keys and arbitrary values which is synchronized
        ///  for the players in a Room. They are available when the client enters the room, as
        ///  they are in the response of OpJoin and OpCreate.
        ///
        ///  Custom Properties either relate to the (current) Room or a Player (in that Room).
        ///
        ///  Both classes locally cache the current key/values and make them available as
        ///  property: CustomProperties. This is provided only to read them.
        ///  You must use the method SetCustomProperties to set/modify them.
        ///
        ///  Any client can set any Custom Properties anytime (when in a room).
        ///  It's up to the game logic to organize how they are best used.
        ///
        ///  You should call SetCustomProperties only with key/values that are new or changed. This reduces
        ///  traffic and performance.
        ///
        ///  Unless you define some expectedProperties, setting key/values is always permitted.
        ///  In this case, the property-setting client will not receive the new values from the server but
        ///  instead update its local cache in SetCustomProperties.
        ///
        ///  If you define expectedProperties, the server will skip updates if the server property-cache
        ///  does not contain all expectedProperties with the same values.
        ///  In this case, the property-setting client will get an update from the server and update it's
        ///  cached key/values at about the same time as everyone else.
        ///
        ///  The benefit of using expectedProperties can be only one client successfully sets a key from
        ///  one known value to another.
        ///  As example: Store who owns an item in a Custom Property "ownedBy". It's 0 initally.
        ///  When multiple players reach the item, they all attempt to change "ownedBy" from 0 to their
        ///  actorNumber. If you use expectedProperties {"ownedBy", 0} as condition, the first player to
        ///  take the item will have it (and the others fail to set the ownership).
        ///
        ///  Properties get saved with the game state for Turnbased games (which use IsPersistent = true).
        ///  </remarks>
        /// <param name="actorNr">Defines which player the Custom Properties belong to. ActorID of a player.</param>
        /// <param name="propertiesToSet">PhotonHashtable of Custom Properties that changes.</param>
        /// <param name="expectedProperties">Provide some keys/values to use as condition for setting the new values. Client must be in room.</param>
        /// <returns>
        /// False if propertiesToSet is null or empty or have no keys (of allowed types).
        /// If not in a room, returns true if local player and expectedProperties are null.
        /// False if actorNr is lower than or equal to zero.
        /// Otherwise, returns if the operation could be sent to the server.
        /// </returns>
        public bool OpSetCustomPropertiesOfActor(int actorNr, PhotonHashtable propertiesToSet, PhotonHashtable expectedProperties = null)
        {
            if (!propertiesToSet.CustomPropKeyTypesValid())
            {
                Log.Error("OpSetCustomPropertiesOfActor() failed. Parameter propertiesToSet must be non-null, not empty and contain only int or string keys.", this.LogLevel, this.LogPrefix);
                return false;
            }

            if (this.CurrentRoom == null)
            {
                Log.Error("OpSetCustomPropertiesOfActor() failed because the client is not in a room. Use LocalPlayer.SetCustomProperties() to change this player's properties even while not in a room.", this.LogLevel, this.LogPrefix);
                return false;
            }

            return this.OpSetPropertiesOfActor(actorNr, propertiesToSet, expectedProperties);
        }


        /// <summary>Internally used to cache and set properties (including well known properties).</summary>
        /// <remarks>Requires being in a room (because this attempts to send an operation which will fail otherwise).</remarks>
        protected internal bool OpSetPropertiesOfActor(int actorNr, PhotonHashtable actorProperties, PhotonHashtable expectedProperties = null)
        {
            if (!this.CheckIfOpCanBeSent(OperationCode.SetProperties, this.Server, "SetProperties"))
            {
                return false;
            }
            if (actorNr <= 0 || actorProperties == null || actorProperties.Count == 0)
            {
                Log.Error("OpSetPropertiesOfActor() failed. Parameter actorProperties must be non-null and not empty.", this.LogLevel, this.LogPrefix);
                return false;
            }

            Log.Info("OpSetPropertiesOfActor()", this.LogLevel, this.LogPrefix);


            ParameterDictionary opParameters = this.paramDictionaryPool.Acquire();
            opParameters.Add(ParameterCode.Properties, actorProperties);
            opParameters.Add(ParameterCode.ActorNr, actorNr);
            opParameters.Add(ParameterCode.Broadcast, true);
            if (expectedProperties != null && expectedProperties.Count != 0)
            {
                opParameters.Add(ParameterCode.ExpectedValues, expectedProperties);
            }

            bool sending = this.RealtimePeer.SendOperation(OperationCode.SetProperties, opParameters, SendOptions.SendReliable);
            this.paramDictionaryPool.Release(opParameters);


            if (sending && !this.CurrentRoom.BroadcastPropertiesChangeToAll && (expectedProperties == null || expectedProperties.Count == 0))
            {
                Player target = this.CurrentRoom.GetPlayer(actorNr);
                if (target != null)
                {
                    target.InternalCacheProperties(actorProperties);
                    this.InRoomCallbackTargets.OnPlayerPropertiesUpdate(target, actorProperties);
                }
            }
            return sending;
        }


        ///  <summary>
        ///  Updates and synchronizes this Room's Custom Properties. Optionally, expectedProperties can be provided as condition.
        ///  </summary>
        ///  <remarks>
        ///  Custom Properties are a set of string keys and arbitrary values which is synchronized
        ///  for the players in a Room. They are available when the client enters the room, as
        ///  they are in the response of OpJoin and OpCreate.
        ///
        ///  Custom Properties either relate to the (current) Room or a Player (in that Room).
        ///
        ///  Both classes locally cache the current key/values and make them available as
        ///  property: CustomProperties. This is provided only to read them.
        ///  You must use the method SetCustomProperties to set/modify them.
        ///
        ///  Any client can set any Custom Properties anytime (when in a room).
        ///  It's up to the game logic to organize how they are best used.
        ///
        ///  You should call SetCustomProperties only with key/values that are new or changed. This reduces
        ///  traffic and performance.
        ///
        ///  Unless you define some expectedProperties, setting key/values is always permitted.
        ///  In this case, the property-setting client will not receive the new values from the server but
        ///  instead update its local cache in SetCustomProperties.
        ///
        ///  If you define expectedProperties, the server will skip updates if the server property-cache
        ///  does not contain all expectedProperties with the same values.
        ///  In this case, the property-setting client will get an update from the server and update it's
        ///  cached key/values at about the same time as everyone else.
        ///
        ///  The benefit of using expectedProperties can be only one client successfully sets a key from
        ///  one known value to another.
        ///  As example: Store who owns an item in a Custom Property "ownedBy". It's 0 initially.
        ///  When multiple players reach the item, they all attempt to change "ownedBy" from 0 to their
        ///  actorNumber. If you use expectedProperties {"ownedBy", 0} as condition, the first player to
        ///  take the item will have it (and the others fail to set the ownership).
        ///
        ///  Properties get saved with the game state for Turnbased games (which use IsPersistent = true).
        ///  </remarks>
        /// <param name="propertiesToSet">PhotonHashtable of Custom Properties to apply.</param>
        /// <param name="expectedProperties">Provide some keys/values to use as condition for setting the new values.</param>
        /// <returns>
        /// False if propertiesToSet is null or empty or have zero string keys.
        /// Otherwise, returns if the operation could be sent to the server.
        /// </returns>
        public bool OpSetCustomPropertiesOfRoom(PhotonHashtable propertiesToSet, PhotonHashtable expectedProperties = null)
        {
            if (!propertiesToSet.CustomPropKeyTypesValid())
            {
                Log.Error("OpSetCustomPropertiesOfRoom() failed. Parameter propertiesToSet must be non-null, not empty and contain only int or string keys.", this.LogLevel, this.LogPrefix);
                return false;
            }

            return this.OpSetPropertiesOfRoom(propertiesToSet, expectedProperties);
        }


        /// <summary>Internally used to set a single Well Known Property (e.g. MaxPlayers, IsVisible) of the room.</summary>
        /// <param name="propCode">Code of the Well Known Property.</param>
        /// <param name="value">Value to set (must meet typing of property.</param>
        /// <returns></returns>
        protected internal bool OpSetPropertyOfRoom(byte propCode, object value)
        {
            PhotonHashtable properties = new PhotonHashtable();
            properties[propCode] = value;
            return this.OpSetPropertiesOfRoom(properties);
        }


        /// <summary>Internally used to cache and set properties (including well known properties).</summary>
        /// <remarks>Requires being in a room (because this attempts to send an operation which will fail otherwise).</remarks>
        protected internal bool OpSetPropertiesOfRoom(PhotonHashtable gameProperties, PhotonHashtable expectedProperties = null)
        {
            if (!this.CheckIfOpCanBeSent(OperationCode.SetProperties, this.Server, "SetProperties"))
            {
                return false;
            }
            if (gameProperties == null || gameProperties.Count == 0)
            {
                Log.Error("OpSetPropertiesOfRoom() failed. Parameter gameProperties must not be null nor empty.", this.LogLevel, this.LogPrefix);
                return false;
            }

            Log.Info("OpSetPropertiesOfRoom()", this.LogLevel, this.LogPrefix);


            ParameterDictionary opParameters = this.paramDictionaryPool.Acquire();
            opParameters.Add(ParameterCode.Properties, gameProperties);
            opParameters.Add(ParameterCode.Broadcast, true);
            if (expectedProperties != null && expectedProperties.Count != 0)
            {
                opParameters.Add(ParameterCode.ExpectedValues, expectedProperties);
            }

            bool sending = this.RealtimePeer.SendOperation(OperationCode.SetProperties, opParameters, SendOptions.SendReliable);
            this.paramDictionaryPool.Release(opParameters);

            if (sending && !this.CurrentRoom.BroadcastPropertiesChangeToAll && (expectedProperties == null || expectedProperties.Count == 0))
            {
                this.CurrentRoom.InternalCacheProperties(gameProperties);
                this.InRoomCallbackTargets.OnRoomPropertiesUpdate(gameProperties);
            }
            return sending;
        }


        /// <summary>
        /// Operation to handle this client's interest groups (for events in room).
        /// </summary>
        /// <remarks>
        /// Note the difference between passing null and byte[0]:
        ///   null won't add/remove any groups.
        ///   byte[0] will add/remove all (existing) groups.
        /// First, removing groups is executed. This way, you could leave all groups and join only the ones provided.
        ///
        /// Changes become active not immediately but when the server executes this operation (approximately RTT/2).
        /// </remarks>
        /// <param name="groupsToRemove">Groups to remove from interest. Null will not remove any. A byte[0] will remove all.</param>
        /// <param name="groupsToAdd">Groups to add to interest. Null will not add any. A byte[0] will add all current.</param>
        /// <returns>If operation could be enqueued for sending. Sent when calling: Service or SendOutgoingCommands.</returns>
        public virtual bool OpChangeGroups(byte[] groupsToRemove, byte[] groupsToAdd)
        {
            if (!this.CheckIfOpCanBeSent(OperationCode.ChangeGroups, this.Server, "ChangeGroups"))
            {
                return false;
            }

            Log.Info("OpChangeGroups()", this.LogLevel, this.LogPrefix);


            ParameterDictionary opParameters = this.paramDictionaryPool.Acquire();
            if (groupsToRemove != null)
            {
                opParameters[(byte)ParameterCode.Remove] = groupsToRemove;
            }
            if (groupsToAdd != null)
            {
                opParameters[(byte)ParameterCode.Add] = groupsToAdd;
            }


            bool sending = this.RealtimePeer.SendOperation(OperationCode.ChangeGroups, opParameters, SendOptions.SendReliable);
            this.paramDictionaryPool.Release(opParameters);

            return sending;
        }




        /// <summary>Gets a list of rooms matching the (non empty) SQL filter for the given SQL-typed lobby.</summary>
        /// <remarks>
        /// Operation is only available for lobbies of type SqlLobby and the filter can not be empty.
        /// It will check those conditions and fail locally, returning false.
        ///
        /// This is an async request which triggers a OnOperationResponse() call.
        /// </remarks>
        /// <a href="https://doc.photonengine.com/en-us/realtime/current/reference/matchmaking-and-lobby" target="_blank">Matchmaking And Lobby</a>
        /// <param name="lobby">The lobby to query. Has to be of type SqlLobby.</param>
        /// <param name="queryData">The sql query statement.</param>
        /// <returns>If the operation could be sent (has to be connected).</returns>
        public virtual bool OpGetGameList(TypedLobby lobby, string queryData)
        {
            if (!this.CheckIfOpCanBeSent(OperationCode.GetGameList, this.Server, "GetGameList"))
            {
                return false;
            }
            if (string.IsNullOrEmpty(queryData))
            {
                Log.Error("Operation GetGameList requires a filter.", this.LogLevel, this.LogPrefix);
                return false;
            }
            if (lobby == null || lobby.Type != LobbyType.Sql || lobby.IsDefault)
            {
                Log.Error("Operation GetGameList can only be used for named lobbies of type SqlLobby.", this.LogLevel, this.LogPrefix);
                return false;
            }

            Log.Info("OpGetGameList()", this.LogLevel, this.LogPrefix);


            if (string.IsNullOrEmpty(queryData))
            {
                if (this.LogLevel >= LogLevel.Info)
                {
                    Log.Info("OpGetGameList not sent. queryData must be not null and not empty.", this.LogLevel, this.LogPrefix);
                }
                return false;
            }

            ParameterDictionary opParameters = this.paramDictionaryPool.Acquire();
            opParameters[(byte)ParameterCode.LobbyName] = lobby.Name;
            opParameters[(byte)ParameterCode.LobbyType] = (byte)lobby.Type;
            opParameters[(byte)ParameterCode.Data] = queryData;

            bool sending = this.RealtimePeer.SendOperation(OperationCode.GetGameList, opParameters, SendOptions.SendReliable);
            this.paramDictionaryPool.Release(opParameters);

            return sending;
        }

        /// <summary>Operation to set Custom Properties for a player / actor. Only available in a room on a Game Server.</summary>
        /// <remarks>Note that Custom Properties keys have to be of type string. If actorProperties contain keys of other types, the operation will not be queued.</remarks>
        /// <param name="actorNr">The actorNumber of the target player.</param>
        /// <param name="actorProperties">The properties key/values to set and or update.</param>
        /// <returns>If the operation will get sent. False if the actorProperties is null, empty or contains key types other than string or int.</returns>
        public bool OpSetCustomPropertiesOfActor(int actorNr, PhotonHashtable actorProperties)
        {
            if (!actorProperties.CustomPropKeyTypesValid())
            {
                Log.Error("For OpSetCustomPropertiesOfActor the actorProperties can only contain keys of type string.", this.LogLevel, this.LogPrefix);
                return false;
            }

            return this.OpSetPropertiesOfActor(actorNr, actorProperties, null);
        }


        /// <summary>Operation to set Custom Room Properties. Only available in a room on a Game Server.</summary>
        /// <remarks>Note that Custom Properties keys have to be of type string. If gameProperties contain keys of other types, the operation will not be enqueued.</remarks>
        /// <param name="gameProperties">The properties key/values to set and or update.</param>
        /// <returns>If the operation will get sent. False if the gameProperties contain keys not being string typed.</returns>
        public bool OpSetCustomPropertiesOfRoom(PhotonHashtable gameProperties)
        {
            if (!gameProperties.CustomPropKeyTypesValid())
            {
                Log.Error("For OpSetCustomPropertiesOfRoom the gameProperties can only contain keys of type string.", this.LogLevel, this.LogPrefix);
                return false;
            }

            return this.OpSetPropertiesOfRoom(gameProperties);
        }

        /// <summary>
        /// Sends this app's appId and appVersion to identify this application server side.
        /// This is an async request which triggers a OnOperationResponse() call.
        /// </summary>
        /// <remarks>
        /// This operation makes use of encryption, if that is established before.
        /// See: EstablishEncryption(). Check encryption with IsEncryptionAvailable.
        /// This operation is allowed only once per connection (multiple calls will have ErrorCode != Ok).
        /// </remarks>
        /// <param name="appId">Your application's name or ID to authenticate. This is assigned by Photon Cloud (webpage).</param>
        /// <param name="appVersion">The client's version (clients with differing client appVersions are separated and players don't meet).</param>
        /// <param name="authValues">Contains all values relevant for authentication. Even without account system (external Custom Auth), the clients are allowed to identify themselves.</param>
        /// <param name="regionCode">Optional region code, if the client should connect to a specific Photon Cloud Region.</param>
        /// <param name="getLobbyStatistics">Set to true on Master Server to receive "Lobby Statistics" events.</param>
        /// <returns>If the operation could be sent (has to be connected).</returns>
        public virtual bool OpAuthenticate(string appId, string appVersion, AuthenticationValues authValues, string regionCode, bool getLobbyStatistics)
        {
            Log.Debug("OpAuthenticate()", this.LogLevel, this.LogPrefix);

            ParameterDictionary opParameters = this.paramDictionaryPool.Acquire();
            bool sending;

            if (getLobbyStatistics)
            {
                // must be sent in operation, even if a Token is available
                opParameters[ParameterCode.LobbyStats] = true;
            }

            // shortcut, if we have a Token
            if (authValues != null && authValues.Token != null)
            {
                opParameters[ParameterCode.Token] = authValues.Token;
                sending = this.RealtimePeer.SendOperation(OperationCode.Authenticate, opParameters, SendOptions.SendReliable); // we don't have to encrypt, when we have a token (which is encrypted)

                this.paramDictionaryPool.Release(opParameters);
                return sending;
            }


            // without a token, we send a complete op auth

            opParameters[ParameterCode.AppVersion] = appVersion;
            opParameters[ParameterCode.ApplicationId] = appId;
            opParameters[ParameterCode.Region] = regionCode;

            if (authValues != null)
            {
                if (!string.IsNullOrEmpty(authValues.UserId))
                {
                    opParameters[ParameterCode.UserId] = authValues.UserId;
                }

                if (authValues.AuthType != CustomAuthenticationType.None)
                {
                    opParameters[ParameterCode.ClientAuthenticationType] = (byte)authValues.AuthType;
                    // if we had a token, the code above would use it. here, we send parameters:
                    if (!string.IsNullOrEmpty(authValues.AuthGetParameters))
                    {
                        opParameters[ParameterCode.ClientAuthenticationParams] = authValues.AuthGetParameters;
                    }
                    if (authValues.AuthPostData != null)
                    {
                        opParameters[ParameterCode.ClientAuthenticationData] = authValues.AuthPostData;
                    }
                }
            }

            sending = this.RealtimePeer.SendOperation(OperationCode.Authenticate, opParameters, new SendOptions() { Reliability = true, Encrypt = true });
            this.paramDictionaryPool.Release(opParameters);

            return sending;
        }


        /// <summary>
        /// Sends this app's appId and appVersion to identify this application server side.
        /// This is an async request which triggers a OnOperationResponse() call.
        /// </summary>
        /// <remarks>
        /// This operation makes use of encryption, if that is established before.
        /// See: EstablishEncryption(). Check encryption with IsEncryptionAvailable.
        /// This operation is allowed only once per connection (multiple calls will have ErrorCode != Ok).
        /// </remarks>
        /// <param name="appId">Your application's name or ID to authenticate. This is assigned by Photon Cloud (webpage).</param>
        /// <param name="appVersion">The client's version (clients with differing client appVersions are separated and players don't meet).</param>
        /// <param name="authValues">Optional authentication values. The client can set no values or a UserId or some parameters for Custom Authentication by a server.</param>
        /// <param name="regionCode">Optional region code, if the client should connect to a specific Photon Cloud Region.</param>
        /// <param name="encryptionMode"></param>
        /// <param name="expectedProtocol"></param>
        /// <returns>If the operation could be sent (has to be connected).</returns>
        public virtual bool OpAuthenticateOnce(string appId, string appVersion, AuthenticationValues authValues, string regionCode, EncryptionMode encryptionMode, ConnectionProtocol expectedProtocol)
        {
            if (encryptionMode == EncryptionMode.DatagramEncryptionGCM && expectedProtocol != ConnectionProtocol.Udp)
            {
                Log.Error($"OpAuthenticateOnce() failed. Can not use EncryptionMode '{encryptionMode}' on protocol {expectedProtocol}", this.LogLevel, this.LogPrefix);
                return false;
            }


            Log.Debug(string.Format("OpAuthenticateOnce() authValues = {0}, region = {1}, encryption = {2}", authValues, regionCode, encryptionMode), this.LogLevel, this.LogPrefix);

            ParameterDictionary opParameters = this.paramDictionaryPool.Acquire();
            bool sending;

            // shortcut, if we have a Token
            if (authValues != null && authValues.Token != null)
            {
                opParameters[ParameterCode.Token] = authValues.Token;
                sending = this.RealtimePeer.SendOperation(OperationCode.AuthenticateOnce, opParameters, SendOptions.SendReliable); // we don't have to encrypt, when we have a token (which is encrypted)

                this.paramDictionaryPool.Release(opParameters);
                return sending;
            }


            opParameters[ParameterCode.ExpectedProtocol] = (byte)expectedProtocol;
            opParameters[ParameterCode.EncryptionMode] = (byte)encryptionMode;

            opParameters[ParameterCode.AppVersion] = appVersion;
            opParameters[ParameterCode.ApplicationId] = appId;

            opParameters[ParameterCode.Region] = regionCode;


            if (authValues != null)
            {
                if (!string.IsNullOrEmpty(authValues.UserId))
                {
                    opParameters[ParameterCode.UserId] = authValues.UserId;
                }

                if (authValues.AuthType != CustomAuthenticationType.None)
                {
                    opParameters[ParameterCode.ClientAuthenticationType] = (byte)authValues.AuthType;
                    if (authValues.Token != null)
                    {
                        opParameters[ParameterCode.Token] = authValues.Token;
                    }
                    else
                    {
                        if (!string.IsNullOrEmpty(authValues.AuthGetParameters))
                        {
                            opParameters[ParameterCode.ClientAuthenticationParams] = authValues.AuthGetParameters;
                        }
                        if (authValues.AuthPostData != null)
                        {
                            opParameters[ParameterCode.ClientAuthenticationData] = authValues.AuthPostData;
                        }
                    }
                }
            }

            sending = this.RealtimePeer.SendOperation(OperationCode.AuthenticateOnce, opParameters, new SendOptions() { Reliability = true, Encrypt = true });
            this.paramDictionaryPool.Release(opParameters);

            return sending;
        }


        /// <summary>
        /// Send an event with custom code/type and any content to the other players in the same room.
        /// </summary>
        /// <param name="eventCode">Identifies this type of event (and the content). Your game's event codes can start with 0.</param>
        /// <param name="customEventContent">Any serializable datatype (including PhotonHashtable like the other OpRaiseEvent overloads).</param>
        /// <param name="raiseEventArgs">Contains used send args. If you pass null, the default args will be used.</param>
        /// <param name="sendOptions">Send args for reliable, encryption etc</param>
        /// <returns>If operation could be queued for sending. Sent when calling: Service or SendOutgoingCommands.</returns>
        public virtual bool OpRaiseEvent(byte eventCode, object customEventContent, RaiseEventArgs raiseEventArgs, SendOptions sendOptions)
        {
            if (!this.CheckIfOpCanBeSent(OperationCode.RaiseEvent, this.Server, "RaiseEvent"))
            {
                return false;
            }

            ParameterDictionary opParameters = this.paramDictionaryPool.Acquire();
            try
            {
                if (raiseEventArgs.CachingOption != EventCaching.DoNotCache)
                {
                    opParameters.Add(ParameterCode.Cache, (byte)raiseEventArgs.CachingOption);
                }
                switch (raiseEventArgs.CachingOption)
                {
                    case EventCaching.SliceSetIndex:
                    case EventCaching.SlicePurgeIndex:
                    case EventCaching.SlicePurgeUpToIndex:
                        //this.opParameters[(byte) ParameterCode.CacheSliceIndex] =
                        //    (byte) raiseEventArgs.CacheSliceIndex;
                        return this.RealtimePeer.SendOperation(OperationCode.RaiseEvent, opParameters, sendOptions);
                    case EventCaching.SliceIncreaseIndex:
                    case EventCaching.RemoveFromRoomCacheForActorsLeft:
                        return this.RealtimePeer.SendOperation(OperationCode.RaiseEvent, opParameters, sendOptions);
                    case EventCaching.RemoveFromRoomCache:
                        if (raiseEventArgs.TargetActors != null)
                        {
                            opParameters.Add(ParameterCode.ActorList, raiseEventArgs.TargetActors);
                        }
                        break;
                    default:
                        if (raiseEventArgs.TargetActors != null)
                        {
                            opParameters.Add(ParameterCode.ActorList, raiseEventArgs.TargetActors);
                        }
                        else if (raiseEventArgs.InterestGroup != 0)
                        {
                            opParameters.Add(ParameterCode.Group, (byte)raiseEventArgs.InterestGroup);
                        }
                        else if (raiseEventArgs.Receivers != ReceiverGroup.Others)
                        {
                            opParameters.Add(ParameterCode.ReceiverGroup, (byte)raiseEventArgs.Receivers);
                        }
                        break;
                }

                opParameters.Add(ParameterCode.Code, (byte)eventCode);
                if (customEventContent != null)
                {
                    opParameters.Add(ParameterCode.Data, (object)customEventContent);
                }
                return this.RealtimePeer.SendOperation(OperationCode.RaiseEvent, opParameters, sendOptions);
            }
            finally
            {
                this.paramDictionaryPool.Release(opParameters);
            }
        }


        /// <summary>
        /// Internally used operation to set some "per server" settings. This is for the Master Server.
        /// </summary>
        /// <param name="receiveLobbyStats">Set to true, to get Lobby Statistics (lists of existing lobbies).</param>
        /// <returns>False if the operation could not be sent.</returns>
        protected internal bool OpSettings(bool receiveLobbyStats)
        {
            if (!this.IsConnectedAndReady || this.Server != ServerConnection.MasterServer)
            {
                Log.Debug($"OpSettings() skipping because IsConnectedAndReady: {IsConnectedAndReady} / Server: {Server}", this.LogLevel, this.LogPrefix);
                return false;
            }

            Log.Info("OpSettings()", this.LogLevel, this.LogPrefix);

            ParameterDictionary opParameters = this.paramDictionaryPool.Acquire();
            opParameters[(byte)0] = receiveLobbyStats;


            bool sending = this.RealtimePeer.SendOperation(OperationCode.ServerSettings, opParameters, SendOptions.SendReliable);
            this.paramDictionaryPool.Release(opParameters);

            return sending;
        }
    }
}
