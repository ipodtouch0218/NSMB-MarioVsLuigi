// -----------------------------------------------------------------------
// <copyright file="Callbacks.cs" company="Exit Games GmbH">
// Photon Realtime API - Copyright (C) 2022 Exit Games GmbH
// </copyright>
// <summary>
// Definitions of Interfaces and implementations of "Callback Containers".
// </summary>
// <author>developer@photonengine.com</author>
// ----------------------------------------------------------------------------

#if UNITY_2017_4_OR_NEWER
#define SUPPORTED_UNITY
#endif


namespace Photon.Realtime
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Diagnostics;
    using Photon.Client;

    #if SUPPORTED_UNITY
    using UnityEngine;
    using Debug = UnityEngine.Debug;
    using SupportClass = Photon.Client.SupportClass;
    #endif

    /// <summary>
    /// Collection of "organizational" callbacks for the Realtime Api to cover: Connection and Regions.
    /// </summary>
    /// <remarks>
    /// Classes that implement this interface must be registered to get callbacks for various situations.
    ///
    /// To register for callbacks, call <see cref="RealtimeClient.AddCallbackTarget"/> and pass the class implementing this interface
    /// To stop getting callbacks, call <see cref="RealtimeClient.RemoveCallbackTarget"/> and pass the class implementing this interface
    ///
    /// </remarks>
    /// \ingroup callbacks
    public interface IConnectionCallbacks
    {
        /// <summary>Obsolete. Replaced by better logging. Was: Called to signal that the "low level connection" got established.</summary>
        [Obsolete("Use OnConnectedToMaster or check the debug logging if you need to know if the client ever connected.")]
        void OnConnected();

        /// <summary>
        /// Called when the client is connected to the Master Server and ready for matchmaking and other tasks.
        /// </summary>
        /// <remarks>
        /// The list of available rooms won't become available unless you join a lobby via RealtimeClient.OpJoinLobby.
        /// You can join rooms and create them even without being in a lobby. The default lobby is used in that case.
        /// </remarks>
        void OnConnectedToMaster();

        /// <summary>
        /// Called after disconnecting from the Photon server. It could be a failure or an explicit disconnect call
        /// </summary>
        /// <remarks>
        ///  The reason for this disconnect is provided as DisconnectCause.
        /// </remarks>
        void OnDisconnected(DisconnectCause cause);

        /// <summary>
        /// Called when the Name Server provided a list of regions for your title.
        /// </summary>
        /// <remarks>
        /// This callback is called as soon as the list is available. No pings were sent for Best Region selection yet.
        /// If the client is set to connect to the Best Region (lowest ping), one or more regions get pinged.
        /// Not all regions are pinged. As soon as the results are final, the client will connect to the best region,
        /// so you can check the ping results when connected to the Master Server.
        ///
        /// Check the RegionHandler class description, to make use of the provided values.
        /// </remarks>
        /// <param name="regionHandler">The currently used RegionHandler.</param>
        void OnRegionListReceived(RegionHandler regionHandler);


        /// <summary>
        /// Called when your Custom Authentication service responds with additional data.
        /// </summary>
        /// <remarks>
        /// Custom Authentication services can include some custom data in their response.
        /// When present, that data is made available in this callback as Dictionary.
        /// While the keys of your data have to be strings, the values can be either string or a number (in Json).
        /// You need to make extra sure, that the value type is the one you expect. Numbers become (currently) int64.
        ///
        /// Example: void OnCustomAuthenticationResponse(Dictionary&lt;string, object&gt; data) { ... }
        /// </remarks>
        /// <see href="https://doc.photonengine.com/en-us/realtime/current/reference/custom-authentication" target="_blank">Custom Authentication</see>
        void OnCustomAuthenticationResponse(Dictionary<string, object> data);

        /// <summary>
        /// Called when the custom authentication failed. Followed by disconnect!
        /// </summary>
        /// <remarks>
        /// Custom Authentication can fail due to user-input, bad tokens/secrets.
        /// If authentication is successful, this method is not called. Implement OnJoinedLobby() or OnConnectedToMaster() (as usual).
        ///
        /// During development of a game, it might also fail due to wrong configuration on the server side.
        /// In those cases, logging the debugMessage is very important.
        ///
        /// Unless you setup a custom authentication service for your app (in the [Dashboard](https://dashboard.photonengine.com)),
        /// this won't be called!
        /// </remarks>
        /// <param name="debugMessage">Contains a debug message why authentication failed. This has to be fixed during development.</param>
        void OnCustomAuthenticationFailed(string debugMessage);
    }


    /// <summary>Callback message for OnConnected callback.</summary>
    [Obsolete("Rely on OnConnectedToMasterMsg or on the debug logging.")]
    public class OnConnectedMsg
    {
    }

    /// <summary>Callback message for OnConnectedToMaster callback.</summary>
    public class OnConnectedToMasterMsg
    {
    }

    /// <summary>Callback message for OnDisconnected callback.</summary>
    public class OnDisconnectedMsg
    {
        /// <summary>The cause for this disconnect.</summary>
        public DisconnectCause cause;
    }

    /// <summary>Callback message for OnRegionList callback.</summary>
    public class OnRegionListReceivedMsg
    {
        /// <summary>Current RegionHandler instance, containing the regions list.</summary>
        public RegionHandler regionHandler;
    }

    /// <summary>Callback message for OnCustomAuthenticationResponse callback.</summary>
    public class OnCustomAuthenticationResponseMsg
    {
        /// <summary>See OnCustomAuthenticationResponse callback.</summary>
        public Dictionary<string, object> data;
    }

    /// <summary>Callback message for OnCustomAuthenticationFailed callback.</summary>
    public class OnCustomAuthenticationFailedMsg
    {
        /// <summary>Debug message containing hints on why/how the authentication failed.</summary>
        public string debugMessage;
    }


    /// <summary>
    /// Collection of "organizational" callbacks for the Realtime Api to cover the Lobby.
    /// </summary>
    /// <remarks>
    /// Classes that implement this interface must be registered to get callbacks for various situations.
    ///
    /// To register for callbacks, call <see cref="RealtimeClient.AddCallbackTarget"/> and pass the class implementing this interface
    /// To stop getting callbacks, call <see cref="RealtimeClient.RemoveCallbackTarget"/> and pass the class implementing this interface
    ///
    /// </remarks>
    /// \ingroup callbacks
    public interface ILobbyCallbacks
    {

        /// <summary>
        /// Called on entering a lobby on the Master Server. The actual room-list updates will call OnRoomListUpdate.
        /// </summary>
        /// <remarks>
        /// While in the lobby, the roomlist is automatically updated in fixed intervals (which you can't modify in the public cloud).
        /// The room list gets available via OnRoomListUpdate.
        /// </remarks>
        void OnJoinedLobby();

        /// <summary>
        /// Called after leaving a lobby.
        /// </summary>
        /// <remarks>
        /// When you leave a lobby, [OpCreateRoom](@ref RealtimeClient.OpCreateRoom) and [OpJoinRandomRoom](@ref RealtimeClient.OpJoinRandomRoom)
        /// automatically refer to the default lobby.
        /// </remarks>
        void OnLeftLobby();

        /// <summary>
        /// Called for any update of the room-listing while in a lobby (InLobby) on the Master Server.
        /// </summary>
        /// <remarks>
        /// Each item is a RoomInfo which might include custom properties (provided you defined those as lobby-listed when creating a room).
        /// Not all types of lobbies provide a listing of rooms to the client. Some are silent and specialized for server-side matchmaking.
        ///
        /// The list is sorted using two criteria: open or closed, full or not. So the list is composed of three groups, in this order:
        ///
        /// first group: open and not full (joinable).<br/>
        /// second group: full but not closed (not joinable).<br/>
        /// third group: closed (not joinable, could be full or not).<br/>
        ///
        /// In each group, entries do not have any particular order (random).
        ///
        /// The list of rooms (or rooms' updates) is also limited in number, see Lobby Limits.
        /// </remarks>
        void OnRoomListUpdate(List<RoomInfo> roomList);

        /// <summary>
        /// Called when the Master Server sent an update for the Lobby Statistics.
        /// </summary>
        /// <remarks>
        /// This callback has two preconditions:
        /// EnableLobbyStatistics must be set to true, before this client connects.
        /// And the client has to be connected to the Master Server, which is providing the info about lobbies.
        /// </remarks>
        void OnLobbyStatisticsUpdate(List<TypedLobbyInfo> lobbyStatistics);
    }


    /// <summary>Callback message for OnJoinedLobby callback.</summary>
    public class OnJoinedLobbyMsg
    {
    }

    /// <summary>Callback message for OnLeftLobby callback.</summary>
    public class OnLeftLobbyMsg
    {
    }

    /// <summary>Callback message for OnRoomListUpdate callback.</summary>
    public class OnRoomListUpdateMsg
    {
        /// <summary>List of changes for this lobby update.</summary>
        public List<RoomInfo> roomList;

        /// <summary>Internal constructor.</summary>
        internal OnRoomListUpdateMsg(List<RoomInfo> roomList)
        {
            this.roomList = roomList;
        }
    }

    /// <summary>Callback message for OnLobbyStatisticsUpdate callback.</summary>
    public class OnLobbyStatisticsUpdateMsg
    {
        /// <summary>List of statistics about currently used lobbies.</summary>
        public List<TypedLobbyInfo> lobbyStatistics;

        /// <summary>Internal constructor.</summary>
        internal OnLobbyStatisticsUpdateMsg(List<TypedLobbyInfo> lobbyStatistics)
        {
            this.lobbyStatistics = lobbyStatistics;
        }
    }


    /// <summary>
    /// Collection of "organizational" callbacks for the Realtime Api to cover Matchmaking.
    /// </summary>
    /// <remarks>
    /// Classes that implement this interface must be registered to get callbacks for various situations.
    ///
    /// To register for callbacks, call <see cref="RealtimeClient.AddCallbackTarget"/> and pass the class implementing this interface
    /// To stop getting callbacks, call <see cref="RealtimeClient.RemoveCallbackTarget"/> and pass the class implementing this interface
    ///
    /// </remarks>
    /// \ingroup callbacks
    public interface IMatchmakingCallbacks
    {

        /// <summary>
        /// Called when the server sent the response to a FindFriends request.
        /// </summary>
        /// <remarks>
        /// After calling OpFindFriends, the Master Server will cache the friend list and send updates to the friend
        /// list. The friends includes the name, userId, online state and the room (if any) for each requested user/friend.
        ///
        /// Use the friendList to update your UI and store it, if the UI should highlight changes.
        /// </remarks>
        void OnFriendListUpdate(List<FriendInfo> friendList);

        /// <summary>
        /// Called when this client created a room and entered it. OnJoinedRoom() will be called as well.
        /// </summary>
        /// <remarks>
        /// This callback is only called on the client which created a room (see OpCreateRoom).
        ///
        /// As any client might close (or drop connection) anytime, there is a chance that the
        /// creator of a room does not execute OnCreatedRoom.
        ///
        /// If you need specific room properties or a "start signal", implement OnMasterClientSwitched()
        /// and make each new MasterClient check the room's state.
        /// </remarks>
        void OnCreatedRoom();

        /// <summary>
        /// Called when the server couldn't create a room (OpCreateRoom failed).
        /// </summary>
        /// <remarks>
        /// Creating a room may fail for various reasons. Most often, the room already exists (roomname in use) or
        /// the RoomOptions clash and it's impossible to create the room.
        ///
        /// When creating a room fails on a Game Server:
        /// The client will cache the failure internally and returns to the Master Server before it calls the fail-callback.
        /// This way, the client is ready to find/create a room at the moment of the callback.
        /// In this case, the client skips calling OnConnectedToMaster but returning to the Master Server will still call OnConnected.
        /// Treat callbacks of OnConnected as pure information that the client could connect.
        /// </remarks>
        /// <param name="returnCode">Operation ReturnCode from the server.</param>
        /// <param name="message">Debug message for the error.</param>
        void OnCreateRoomFailed(short returnCode, string message);

        /// <summary>
        /// Called when the RealtimeClient entered a room, no matter if this client created it or simply joined.
        /// </summary>
        /// <remarks>
        /// When this is called, you can access the existing players in Room.Players, their custom properties and Room.CustomProperties.
        ///
        /// In this callback, you could create player objects. For example in Unity, instantiate a prefab for the player.
        ///
        /// If you want a match to be started "actively", enable the user to signal "ready" (using OpRaiseEvent or a Custom Property).
        /// </remarks>
        void OnJoinedRoom();

        /// <summary>
        /// Called when a previous OpJoinRoom call failed on the server.
        /// </summary>
        /// <remarks>
        /// Joining a room may fail for various reasons. Most often, the room is full or does not exist anymore
        /// (due to someone else being faster or closing the room).
        ///
        /// When joining a room fails on a Game Server:
        /// The client will cache the failure internally and returns to the Master Server before it calls the fail-callback.
        /// This way, the client is ready to find/create a room at the moment of the callback.
        /// In this case, the client skips calling OnConnectedToMaster but returning to the Master Server will still call OnConnected.
        /// Treat callbacks of OnConnected as pure information that the client could connect.
        /// </remarks>
        /// <param name="returnCode">Operation ReturnCode from the server.</param>
        /// <param name="message">Debug message for the error.</param>
        void OnJoinRoomFailed(short returnCode, string message);

        /// <summary>
        /// Called when a previous OpJoinRandom call failed on the server.
        /// </summary>
        /// <remarks>
        /// The most common causes are that a room is full or does not exist (due to someone else being faster or closing the room).
        ///
        /// This operation is only ever sent to the Master Server. Once a room is found by the Master Server, the client will
        /// head off to the designated Game Server and use the operation Join on the Game Server.
        ///
        /// When using multiple lobbies (via OpJoinLobby or a TypedLobby parameter), another lobby might have more/fitting rooms.<br/>
        /// </remarks>
        /// <param name="returnCode">Operation ReturnCode from the server.</param>
        /// <param name="message">Debug message for the error.</param>
        void OnJoinRandomFailed(short returnCode, string message);

        /// <summary>
        /// Called when the local user/client left a room, so the game's logic can clean up it's internal state.
        /// </summary>
        /// <remarks>
        /// When leaving a room, the RealtimeClient will disconnect the Game Server and connect to the Master Server.
        /// This wraps up multiple internal actions.
        ///
        /// Wait for the callback OnConnectedToMaster, before you use lobbies and join or create rooms.
        ///
        /// OnLeftRoom also gets called, when the application quits.
        /// It makes sense to check static ConnectionHandler.AppQuits before loading scenes in OnLeftRoom().
        /// </remarks>
        void OnLeftRoom();
    }


    /// <summary>Callback message for OnFriendListUpdate callback.</summary>
    public class OnFriendListUpdateMsg
    {
        /// <summary>List of friends for this update.</summary>
        public List<FriendInfo> friendList;

        internal OnFriendListUpdateMsg(List<FriendInfo> friendList)
        {
            this.friendList = friendList;
        }
    }

    /// <summary>Callback message for OnCreatedRoom callback.</summary>
    public class OnCreatedRoomMsg
    {
    }

    /// <summary>Callback message for OnCreateRoomFailed callback.</summary>
    public class OnCreateRoomFailedMsg
    {
        /// <summary>Return code for this callback. See OnCreateRoomFailed.</summary>
        public short returnCode;
        /// <summary>Message for this callback. See OnCreateRoomFailed.</summary>
        public string message;

        internal OnCreateRoomFailedMsg(short returnCode, string message)
        {
            this.returnCode = returnCode;
            this.message = message;
        }

    }

    /// <summary>Callback message for OnJoinedRoom callback.</summary>
    public class OnJoinedRoomMsg
    {
    }

    /// <summary>Callback message for OnJoinRoomFailed callback.</summary>
    public class OnJoinRoomFailedMsg
    {
        /// <summary>Return code for this callback. See OnJoinRoomFailed.</summary>
        public short returnCode;
        /// <summary>Message for this callback. See OnJoinRoomFailed.</summary>
        public string message;

        internal OnJoinRoomFailedMsg(short returnCode, string message)
        {
            this.returnCode = returnCode;
            this.message = message;
        }
    }

    /// <summary>Callback message for OnJoinRandomFailed callback.</summary>
    public class OnJoinRandomFailedMsg
    {
        /// <summary>Return code for this callback. See OnJoinRandomFailed.</summary>
        public short returnCode;
        /// <summary>Message for this callback. See OnJoinRandomFailed.</summary>
        public string message;

        internal OnJoinRandomFailedMsg(short returnCode, string message)
        {
            this.returnCode = returnCode;
            this.message = message;
        }
    }

    /// <summary>Callback message for OnLeftRoom callback.</summary>
    public class OnLeftRoomMsg
    {
    }


    /// <summary>
    /// Collection of "in room" callbacks for the Realtime Api to cover: Players entering or leaving, property updates and Master Client switching.
    /// </summary>
    /// <remarks>
    /// Classes that implement this interface must be registered to get callbacks for various situations.
    ///
    /// To register for callbacks, call <see cref="RealtimeClient.AddCallbackTarget"/> and pass the class implementing this interface
    /// To stop getting callbacks, call <see cref="RealtimeClient.RemoveCallbackTarget"/> and pass the class implementing this interface
    ///
    /// </remarks>
    /// \ingroup callbacks
    public interface IInRoomCallbacks
    {
        /// <summary>
        /// Called when a remote player entered the room. This Player is already added to the playerlist.
        /// </summary>
        /// <remarks>
        /// If your game starts with a certain number of players, this callback can be useful to check the
        /// Room.playerCount and find out if you can start.
        /// </remarks>
        void OnPlayerEnteredRoom(Player newPlayer);

        /// <summary>
        /// Called when a remote player left the room or became inactive. Check otherPlayer.IsInactive.
        /// </summary>
        /// <remarks>
        /// If another player leaves the room or if the server detects a lost connection, this callback will
        /// be used to notify your game logic.
        ///
        /// Depending on the room's setup, players may become inactive, which means they may return and retake
        /// their spot in the room. In such cases, the Player stays in the Room.Players dictionary.
        ///
        /// If the player is not just inactive, it gets removed from the Room.Players dictionary, before
        /// the callback is called.
        /// </remarks>
        void OnPlayerLeftRoom(Player otherPlayer);


        /// <summary>
        /// Called when room properties changed. The propertiesThatChanged contain only the keys that changed.
        /// </summary>
        /// <remarks>
        /// In most cases, this method gets called when some player changes the Room Properties.
        /// However, there are also "Well Known Properties" (which use byte keys) and this callback may include them.
        /// Especially when entering a room, the server will also send the required Well Known Properties and they
        /// are not filtered out for the OnRoomPropertiesUpdate callback.
        ///
        /// You can safely ignore the byte typed keys in propertiesThatChanged.
        ///
        /// Changing properties is usually done by Room.SetCustomProperties.
        /// </remarks>
        /// <param name="propertiesThatChanged"></param>
        void OnRoomPropertiesUpdate(PhotonHashtable propertiesThatChanged);

        /// <summary>
        /// Called when custom player-properties are changed.
        /// </summary>
        /// <remarks>
        /// Changing properties must be done by Player.SetCustomProperties, which causes this callback locally, too.
        /// </remarks>
        /// <param name="targetPlayer">Contains Player that changed.</param>
        /// <param name="changedProps">Contains the properties that changed.</param>
        void OnPlayerPropertiesUpdate(Player targetPlayer, PhotonHashtable changedProps);

        /// <summary>
        /// Called after switching to a new MasterClient when the current one leaves.
        /// </summary>
        /// <remarks>
        /// This is not called when this client enters a room.
        /// The former MasterClient is still in the player list when this method get called.
        /// </remarks>
        void OnMasterClientSwitched(Player newMasterClient);
    }


    /// <summary>Callback message for OnPlayerEnteredRoom callback.</summary>
    public class OnPlayerEnteredRoomMsg
    {
        /// <summary>Reference of player who joined.</summary>
        public Player newPlayer;

        internal OnPlayerEnteredRoomMsg(Player newPlayer)
        {
            this.newPlayer = newPlayer;
        }
    }

    /// <summary>Callback message for OnPlayerLeftRoom callback.</summary>
    public class OnPlayerLeftRoomMsg
    {
        /// <summary>Reference of player who left.</summary>
        public Player otherPlayer;

        internal OnPlayerLeftRoomMsg(Player otherPlayer)
        {
            this.otherPlayer = otherPlayer;
        }
    }

    /// <summary>Callback message for OnRoomPropertiesUpdate callback.</summary>
    public class OnRoomPropertiesUpdateMsg
    {
        /// <summary>Hashtable of properties that changed.</summary>
        public PhotonHashtable changedProps;

        internal OnRoomPropertiesUpdateMsg(PhotonHashtable propertiesThatChanged)
        {
            this.changedProps = propertiesThatChanged;
        }

        /// <summary>Hashtable of properties that changed.</summary>
        [Obsolete("Use changedProps.")]
        public PhotonHashtable propertiesThatChanged
        {
            get { return this.changedProps; }
            set { this.changedProps = value; }
        }
    }

    /// <summary>Callback message for OnPlayerPropertiesUpdate callback.</summary>
    public class OnPlayerPropertiesUpdateMsg
    {
        /// <summary>Reference to player whose properties changed.</summary>
        public Player targetPlayer;
        /// <summary>Hashtable of properties that changed.</summary>
        public PhotonHashtable changedProps;

        internal OnPlayerPropertiesUpdateMsg(Player targetPlayer, PhotonHashtable changedProps)
        {
            this.targetPlayer = targetPlayer;
            this.changedProps = changedProps;
        }
    }

    /// <summary>Callback message for OnMasterClientSwitched callback.</summary>
    public class OnMasterClientSwitchedMsg
    {
        /// <summary>Reference of player who is now the Master Client.</summary>
        public Player newMasterClient;

        internal OnMasterClientSwitchedMsg(Player newMasterClient)
        {
            this.newMasterClient = newMasterClient;
        }
    }

    /// <summary>
    /// Interface for <see cref="EventCode.ErrorInfo"/> event callback for the Realtime Api.
    /// </summary>
    /// <remarks>
    /// Classes that implement this interface must be registered to get callbacks for various situations.
    ///
    /// To register for callbacks, call <see cref="RealtimeClient.AddCallbackTarget"/> and pass the class implementing this interface
    /// To stop getting callbacks, call <see cref="RealtimeClient.RemoveCallbackTarget"/> and pass the class implementing this interface
    ///
    /// </remarks>
    /// \ingroup callbacks
    public interface IErrorInfoCallback
    {
        /// <summary>
        /// Called when the client receives an event from the server indicating that an error happened there.
        /// </summary>
        /// <remarks>
        /// In most cases this could be either:
        /// 1. an error from webhooks plugin (if HasErrorInfo is enabled), read more here:
        /// https://doc.photonengine.com/en-us/realtime/current/gameplay/web-extensions/webhooks#options
        /// 2. an error sent from a custom server plugin via PluginHost.BroadcastErrorInfoEvent, see example here:
        /// https://doc.photonengine.com/en-us/server/current/plugins/manual#handling_http_response
        /// 3. an error sent from the server, for example, when the limit of cached events has been exceeded in the room
        /// (all clients will be disconnected and the room will be closed in this case)
        /// read more here: https://doc.photonengine.com/en-us/realtime/current/gameplay/cached-events#special_considerations
        ///
        /// If you implement <see cref="IOnEventCallback.OnEvent"/> or <see cref="RealtimeClient.EventReceived"/> you will also get this event.
        /// </remarks>
        /// <param name="errorInfo">Object containing information about the error</param>
        void OnErrorInfo(ErrorInfo errorInfo);
    }

    /// <summary>Callback message for OnErrorInfo callback.</summary>
    public class OnErrorInfoMsg
    {
        /// <summary>Debug / error message.</summary>
        public ErrorInfo errorInfo;

        internal OnErrorInfoMsg(ErrorInfo errorInfo)
        {
            this.errorInfo = errorInfo;
        }
    }


    /// <summary>
    /// Event callback for the Realtime Api. Covers events from the server and those sent by clients via OpRaiseEvent.
    /// </summary>
    /// <remarks>
    /// Classes that implement this interface must be registered to get callbacks for various situations.
    ///
    /// To register for callbacks, call <see cref="RealtimeClient.AddCallbackTarget"/> and pass the class implementing this interface
    /// To stop getting callbacks, call <see cref="RealtimeClient.RemoveCallbackTarget"/> and pass the class implementing this interface
    ///
    /// </remarks>
    /// \ingroup callbacks
    public interface IOnEventCallback
    {
        /// <summary>Called for any incoming events.</summary>
        /// <remarks>
        /// To receive events, implement IOnEventCallback in any class and register it via AddCallbackTarget
        /// (either in RealtimeClient or PhotonNetwork).
        ///
        /// With the EventData.Sender you can look up the Player who sent the event.
        ///
        /// It is best practice to assign an eventCode for each different type of content and action, so the Code
        /// will be essential to read the incoming events.
        /// </remarks>
        void OnEvent(EventData photonEvent);
    }


    /// <summary>
    /// Called for any incoming messages. For "raw" messages the message parameter is an ArraySegment&lt;byte&gt;.
    /// </summary>
    /// <remarks>
    /// Classes that implement this interface must be registered to get callbacks for various situations.
    ///
    /// To register for callbacks, call <see cref="RealtimeClient.AddCallbackTarget"/> and pass the class implementing this interface
    /// To stop getting callbacks, call <see cref="RealtimeClient.RemoveCallbackTarget"/> and pass the class implementing this interface
    ///
    /// </remarks>
    /// \ingroup callbacks
    public interface IOnMessageCallback
    {
        /// <summary>Called for any incoming messages. For "raw" messages the message parameter is an ArraySegment&lt;byte&gt;.</summary>
        /// <remarks>
        /// To receive events, implement IOnEventCallback in any class and register it via AddCallbackTarget.
        ///
        /// Raw messages do not contain a pre-defined structure.
        /// It is entirely up to the game logic to send and receive useful binary data.
        /// </remarks>
        void OnMessage(bool isRawMessage, object message);
    }



    internal class CallbackTargetChange
    {
        public readonly object Target;
        /// <summary>Add if true, remove if false.</summary>
        public readonly bool AddTarget;

        public CallbackTargetChange(object target, bool addTarget)
        {
            this.Target = target;
            this.AddTarget = addTarget;
        }
    }

    /// <summary>
    /// Container type for callbacks defined by IConnectionCallbacks. See AddCallbackTarget.
    /// </summary>
    /// <remarks>
    /// While the interfaces of callbacks wrap up the methods that will be called,
    /// the container classes implement a simple way to call a method on all registered objects.
    /// </remarks>
    public class ConnectionCallbacksContainer : List<IConnectionCallbacks>, IConnectionCallbacks
    {
        private readonly RealtimeClient client;

        /// <summary>Constructs a new container for the given client.</summary>
        /// <param name="client">The client which this container related to.</param>
        public ConnectionCallbacksContainer(RealtimeClient client)
        {
            this.client = client;
        }

        /// <summary>Interface implementation.</summary>
        public void OnConnected()
        {
            //this.client.UpdateCallbackTargets();

            //foreach (IConnectionCallbacks target in this)
            //{
            //    target.OnConnected();
            //}
            //this.client.CallbackMessage.Raise(new OnConnectedMsg());
        }

        /// <summary>Interface implementation.</summary>
        public void OnConnectedToMaster()
        {
            this.client.UpdateCallbackTargets();

            foreach (IConnectionCallbacks target in this)
            {
                target.OnConnectedToMaster();
            }
            this.client.CallbackMessage.Raise(new OnConnectedToMasterMsg());
        }

        /// <summary>Interface implementation.</summary>
        public void OnRegionListReceived(RegionHandler regionHandler)
        {
            string summaryString = this.client.AppSettings.BestRegionSummaryFromStorage ?? "N/A";
            Log.Info($"OnRegionListReceived({regionHandler.AvailableRegionCodes}) previous Summary: {summaryString}", this.client.LogLevel, this.client.LogPrefix);
            this.client.UpdateCallbackTargets();

            foreach (IConnectionCallbacks target in this)
            {
                target.OnRegionListReceived(regionHandler);
            }
            this.client.CallbackMessage.Raise(new OnRegionListReceivedMsg() {regionHandler = regionHandler});
        }

        /// <summary>Interface implementation.</summary>
        public void OnDisconnected(DisconnectCause cause)
        {
            if (this.client.Handler != null)
            {
                this.client.Handler.RemoveInstance();
            }

            string debugInfo = string.Empty;
            if (cause != DisconnectCause.ApplicationQuit && cause != DisconnectCause.DisconnectByClientLogic)
            {
                Log.Warn($"OnDisconnected({cause}) PeerId: {this.client.RealtimePeer.PeerID} SystemConnectionSummary: {this.client.SystemConnectionSummary} Server: {this.client.CurrentServerAddress}", this.client.LogLevel, this.client.LogPrefix);
            }
            else
            {
                Log.Info($"OnDisconnected({cause})", this.client.LogLevel, this.client.LogPrefix);
            }
            this.client.UpdateCallbackTargets();

            foreach (IConnectionCallbacks target in this)
            {
                target.OnDisconnected(cause);
            }
            this.client.CallbackMessage.Raise(new OnDisconnectedMsg() {cause = cause});
        }

        /// <summary>Interface implementation.</summary>
        public void OnCustomAuthenticationResponse(Dictionary<string, object> data)
        {
            Log.Info("OnCustomAuthenticationResponse()", this.client.LogLevel, this.client.LogPrefix);
            this.client.UpdateCallbackTargets();

            foreach (IConnectionCallbacks target in this)
            {
                target.OnCustomAuthenticationResponse(data);
            }
            this.client.CallbackMessage.Raise(new OnCustomAuthenticationResponseMsg() {data = data});
        }

        /// <summary>Interface implementation.</summary>
        public void OnCustomAuthenticationFailed(string debugMessage)
        {
            Log.Error($"OnCustomAuthenticationFailed() debugMessage: {debugMessage}", this.client.LogLevel, this.client.LogPrefix);
            this.client.UpdateCallbackTargets();

            foreach (IConnectionCallbacks target in this)
            {
                target.OnCustomAuthenticationFailed(debugMessage);
            }
            this.client.CallbackMessage.Raise(new OnCustomAuthenticationFailedMsg() {debugMessage = debugMessage});
        }
    }


    /// <summary>
    /// Container type for callbacks defined by IMatchmakingCallbacks. See MatchMakingCallbackTargets.
    /// </summary>
    /// <remarks>
    /// While the interfaces of callbacks wrap up the methods that will be called,
    /// the container classes implement a simple way to call a method on all registered objects.
    /// </remarks>
    public class MatchMakingCallbacksContainer : List<IMatchmakingCallbacks>, IMatchmakingCallbacks
    {
        private readonly RealtimeClient client;

        /// <summary>Constructs a new container for the given client.</summary>
        /// <param name="client">The client which this container related to.</param>
        public MatchMakingCallbacksContainer(RealtimeClient client)
        {
            this.client = client;
        }

        /// <summary>Interface implementation.</summary>
        public void OnCreatedRoom()
        {
            Log.Info($"OnCreatedRoom() name: {this.client.CurrentRoom}", this.client.LogLevel, this.client.LogPrefix);
            this.client.UpdateCallbackTargets();

            foreach (IMatchmakingCallbacks target in this)
            {
                target.OnCreatedRoom();
            }
            this.client.CallbackMessage.Raise(new OnCreatedRoomMsg());
        }

        /// <summary>Interface implementation.</summary>
        public void OnJoinedRoom()
        {
            Log.Info($"OnJoinedRoom() {this.client.CurrentRoom}", this.client.LogLevel, this.client.LogPrefix);
            this.client.UpdateCallbackTargets();

            foreach (IMatchmakingCallbacks target in this)
            {
                target.OnJoinedRoom();
            }
            this.client.CallbackMessage.Raise(new OnJoinedRoomMsg());
        }

        /// <summary>Interface implementation.</summary>
        public void OnCreateRoomFailed(short returnCode, string message)
        {
            Log.Error($"OnCreateRoomFailed({returnCode}, \"{message}\")", this.client.LogLevel, this.client.LogPrefix);
            this.client.UpdateCallbackTargets();

            foreach (IMatchmakingCallbacks target in this)
            {
                target.OnCreateRoomFailed(returnCode, message);
            }
            this.client.CallbackMessage.Raise(new OnCreateRoomFailedMsg(returnCode, message));
        }

        /// <summary>Interface implementation.</summary>
        public void OnJoinRandomFailed(short returnCode, string message)
        {
            Log.Warn($"OnJoinRandomFailed({returnCode}, \"{message}\")", this.client.LogLevel, this.client.LogPrefix);
            this.client.UpdateCallbackTargets();

            foreach (IMatchmakingCallbacks target in this)
            {
                target.OnJoinRandomFailed(returnCode, message);
            }
            this.client.CallbackMessage.Raise(new OnJoinRandomFailedMsg(returnCode, message));
        }

        /// <summary>Interface implementation.</summary>
        public void OnJoinRoomFailed(short returnCode, string message)
        {
            Log.Error($"OnJoinRoomFailed({returnCode}, \"{message}\")", this.client.LogLevel, this.client.LogPrefix);
            this.client.UpdateCallbackTargets();

            foreach (IMatchmakingCallbacks target in this)
            {
                target.OnJoinRoomFailed(returnCode, message);
            }
            this.client.CallbackMessage.Raise(new OnJoinRoomFailedMsg(returnCode, message));
        }

        /// <summary>Interface implementation.</summary>
        public void OnLeftRoom()
        {
            Log.Info("OnLeftRoom()", this.client.LogLevel, this.client.LogPrefix);
            this.client.UpdateCallbackTargets();

            foreach (IMatchmakingCallbacks target in this)
            {
                target.OnLeftRoom();
            }
            this.client.CallbackMessage.Raise(new OnLeftRoomMsg());
        }

        /// <summary>Interface implementation.</summary>
        public void OnFriendListUpdate(List<FriendInfo> friendList)
        {
            Log.Debug("OnFriendListUpdate()", this.client.LogLevel, this.client.LogPrefix);
            this.client.UpdateCallbackTargets();

            foreach (IMatchmakingCallbacks target in this)
            {
                target.OnFriendListUpdate(friendList);
            }
            this.client.CallbackMessage.Raise(new OnFriendListUpdateMsg(friendList: friendList));
        }
    }


    /// <summary>
    /// Container type for callbacks defined by IInRoomCallbacks. See InRoomCallbackTargets.
    /// </summary>
    /// <remarks>
    /// While the interfaces of callbacks wrap up the methods that will be called,
    /// the container classes implement a simple way to call a method on all registered objects.
    /// </remarks>
    internal class InRoomCallbacksContainer : List<IInRoomCallbacks>, IInRoomCallbacks
    {
        private readonly RealtimeClient client;

        public InRoomCallbacksContainer(RealtimeClient client)
        {
            this.client = client;
        }

        public void OnPlayerEnteredRoom(Player newPlayer)
        {
            Log.Info($"OnPlayerEnteredRoom() Player: {newPlayer}", this.client.LogLevel, this.client.LogPrefix);
            this.client.UpdateCallbackTargets();

            foreach (IInRoomCallbacks target in this)
            {
                target.OnPlayerEnteredRoom(newPlayer);
            }
            this.client.CallbackMessage.Raise(new OnPlayerEnteredRoomMsg(newPlayer: newPlayer));
        }

        public void OnPlayerLeftRoom(Player otherPlayer)
        {
            Log.Info($"OnPlayerLeftRoom() Player: {otherPlayer}", this.client.LogLevel, this.client.LogPrefix);
            this.client.UpdateCallbackTargets();

            foreach (IInRoomCallbacks target in this)
            {
                target.OnPlayerLeftRoom(otherPlayer);
            }
            this.client.CallbackMessage.Raise(new OnPlayerLeftRoomMsg(otherPlayer: otherPlayer));
        }

        public void OnRoomPropertiesUpdate(PhotonHashtable propertiesThatChanged)
        {
            Log.Debug("OnRoomPropertiesUpdate()", this.client.LogLevel, this.client.LogPrefix);
            this.client.UpdateCallbackTargets();

            foreach (IInRoomCallbacks target in this)
            {
                target.OnRoomPropertiesUpdate(propertiesThatChanged);
            }
            this.client.CallbackMessage.Raise(new OnRoomPropertiesUpdateMsg(propertiesThatChanged: propertiesThatChanged));
        }

        public void OnPlayerPropertiesUpdate(Player targetPlayer, PhotonHashtable changedProp)
        {
            Log.Debug("OnPlayerPropertiesUpdate()", this.client.LogLevel, this.client.LogPrefix);
            this.client.UpdateCallbackTargets();

            foreach (IInRoomCallbacks target in this)
            {
                target.OnPlayerPropertiesUpdate(targetPlayer, changedProp);
            }
            this.client.CallbackMessage.Raise(new OnPlayerPropertiesUpdateMsg(targetPlayer: targetPlayer, changedProps: changedProp));
        }

        public void OnMasterClientSwitched(Player newMasterClient)
        {
            Log.Info("OnMasterClientSwitched()", this.client.LogLevel, this.client.LogPrefix);
            this.client.UpdateCallbackTargets();

            foreach (IInRoomCallbacks target in this)
            {
                target.OnMasterClientSwitched(newMasterClient);
            }
            this.client.CallbackMessage.Raise(new OnMasterClientSwitchedMsg(newMasterClient: newMasterClient));
        }
    }


    /// <summary>
    /// Container type for callbacks defined by ILobbyCallbacks. See LobbyCallbackTargets.
    /// </summary>
    /// <remarks>
    /// While the interfaces of callbacks wrap up the methods that will be called,
    /// the container classes implement a simple way to call a method on all registered objects.
    /// </remarks>
    internal class LobbyCallbacksContainer : List<ILobbyCallbacks>, ILobbyCallbacks
    {
        private readonly RealtimeClient client;

        public LobbyCallbacksContainer(RealtimeClient client)
        {
            this.client = client;
        }

        public void OnJoinedLobby()
        {
            Log.Info("OnJoinedLobby()", this.client.LogLevel, this.client.LogPrefix);
            this.client.UpdateCallbackTargets();

            foreach (ILobbyCallbacks target in this)
            {
                target.OnJoinedLobby();
            }
            this.client.CallbackMessage.Raise(new OnJoinedLobbyMsg());
        }

        public void OnLeftLobby()
        {
            Log.Info("OnLeftLobby()", this.client.LogLevel, this.client.LogPrefix);
            this.client.UpdateCallbackTargets();

            foreach (ILobbyCallbacks target in this)
            {
                target.OnLeftLobby();
            }
            this.client.CallbackMessage.Raise(new OnLeftLobbyMsg());
        }

        public void OnRoomListUpdate(List<RoomInfo> roomList)
        {
            Log.Debug("OnRoomListUpdate()", this.client.LogLevel, this.client.LogPrefix);
            this.client.UpdateCallbackTargets();

            foreach (ILobbyCallbacks target in this)
            {
                target.OnRoomListUpdate(roomList);
            }
            this.client.CallbackMessage.Raise(new OnRoomListUpdateMsg(roomList: roomList));
        }

        public void OnLobbyStatisticsUpdate(List<TypedLobbyInfo> lobbyStatistics)
        {
            Log.Debug("OnLobbyStatisticsUpdate()", this.client.LogLevel, this.client.LogPrefix);
            this.client.UpdateCallbackTargets();

            foreach (ILobbyCallbacks target in this)
            {
                target.OnLobbyStatisticsUpdate(lobbyStatistics);
            }
            this.client.CallbackMessage.Raise(new OnLobbyStatisticsUpdateMsg(lobbyStatistics: lobbyStatistics));
        }
    }


    /// <summary>
    /// Container type for callbacks defined by <see cref="IErrorInfoCallback"/>. See <see cref="RealtimeClient.ErrorInfoCallbackTargets"/>.
    /// </summary>
    /// <remarks>
    /// While the interfaces of callbacks wrap up the methods that will be called,
    /// the container classes implement a simple way to call a method on all registered objects.
    /// </remarks>
    internal class ErrorInfoCallbacksContainer : List<IErrorInfoCallback>, IErrorInfoCallback
    {
        private RealtimeClient client;

        public ErrorInfoCallbacksContainer(RealtimeClient client)
        {
            this.client = client;
        }

        public void OnErrorInfo(ErrorInfo errorInfo)
        {
            Log.Error($"OnErrorInfo({errorInfo.Info})", this.client.LogLevel, this.client.LogPrefix);
            this.client.UpdateCallbackTargets();

            foreach (IErrorInfoCallback target in this)
            {
                target.OnErrorInfo(errorInfo);
            }
            this.client.CallbackMessage.Raise(new OnErrorInfoMsg(errorInfo));
        }
    }
}