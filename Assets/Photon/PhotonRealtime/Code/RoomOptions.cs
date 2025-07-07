// -----------------------------------------------------------------------------
// <copyright company="Exit Games GmbH">
// Photon Realtime API - Copyright (C) 2022 Exit Games GmbH
// </copyright>
// <summary>Room creation options.</summary>
// <author>developer@photonengine.com</author>
// -----------------------------------------------------------------------------


namespace Photon.Realtime
{
    using Photon.Client;


    /// <summary>Wraps up common room properties needed when you create rooms. Read the individual entries for more details.</summary>
    /// <remarks>This directly maps to the fields in the Room class.</remarks>
    public class RoomOptions
    {
        /// <summary>Defines if this room is listed in the lobby. If not, it also is not joined randomly.</summary>
        /// <remarks>
        /// A room that is not visible will be excluded from the room lists that are sent to the clients in lobbies.
        /// An invisible room can be joined by name but is excluded from random matchmaking.
        ///
        /// Use this to "hide" a room and simulate "private rooms". Players can exchange a roomname and create it
        /// invisible to avoid anyone else joining it.
        /// </remarks>
        public bool IsVisible { get { return this.isVisible; } set { this.isVisible = value; } }
        private bool isVisible = true;

        /// <summary>Defines if this room can be joined at all.</summary>
        /// <remarks>
        /// If a room is closed, no player can join this. As example this makes sense when 3 of 4 possible players
        /// start their gameplay early and don't want anyone to join during the game.
        /// The room can still be listed in the lobby (set isVisible to control lobby-visibility).
        /// </remarks>
        public bool IsOpen { get { return this.isOpen; } set { this.isOpen = value; } }
        private bool isOpen = true;

        /// <summary>Max number of players that can be in the room at any time. 0 means "no limit".</summary>
        public int MaxPlayers;

        /// <summary>Time To Live (TTL) for an 'actor' in a room. If a client disconnects, this actor is inactive first and removed after this timeout. In milliseconds.</summary>
        public int PlayerTtl;

        /// <summary>Time To Live (TTL) for a room when the last player leaves. Keeps room in memory for case a player re-joins soon. In milliseconds.</summary>
        public int EmptyRoomTtl;

        /// <summary>Removes a user's events and properties from the room when a user leaves.</summary>
        /// <remarks>
        /// This makes sense when in rooms where players can't place items in the room and just vanish entirely.
        /// When you disable this, the event history can become too long to load if the room stays in use indefinitely.
        /// Default: true. Cleans up the cache and props of leaving users.
        /// </remarks>
        public bool CleanupCacheOnLeave { get { return this.cleanupCacheOnLeave; } set { this.cleanupCacheOnLeave = value; } }
        private bool cleanupCacheOnLeave = true;

        /// <summary>The room's custom properties to set. Use string keys!</summary>
        /// <remarks>
        /// Custom room properties are any key-values you need to define the game's setup.
        /// The shorter your keys are, the better.
        /// Example: Map, Mode (could be "m" when used with "Map"), TileSet (could be "t").
        /// </remarks>
        public PhotonHashtable CustomRoomProperties;

        /// <summary>Defines the custom room properties that get listed in the lobby.</summary>
        /// <remarks>
        /// Name the custom room properties that should be available to clients that are in a lobby.
        /// Use with care. Unless a custom property is essential for matchmaking or user info, it should
        /// not be sent to the lobby, which causes traffic and delays for clients in the lobby.
        ///
        /// Default: No custom properties are sent to the lobby.
        /// </remarks>
        public object[] CustomRoomPropertiesForLobby;

        /// <summary>Informs the server of the expected plugin setup.</summary>
        /// <remarks>
        /// The operation will fail in case of a plugin mismatch returning error code PluginMismatch 32757(0x7FFF - 10).
        /// Setting string[]{} means the client expects no plugin to be setup.
        /// Note: for backwards compatibility null omits any check.
        /// </remarks>
        public string[] Plugins;

        /// <summary>
        /// Tells the server to skip room events for joining and leaving players.
        /// </summary>
        /// <remarks>
        /// Using this makes the client unaware of the other players in a room.
        /// That can save some traffic if you have some server logic that updates players
        /// but it can also limit the client's usability.
        /// </remarks>
        public bool SuppressRoomEvents { get; set; }

        /// <summary>Disables events join and leave from the server as well as property broadcasts in a room (to minimize traffic)</summary>
        public bool SuppressPlayerInfo { get; set; }

        /// <summary>
        /// Defines if the UserIds of players get "published" in the room. Useful for FindFriends, if players want to play another game together.
        /// </summary>
        /// <remarks>
        /// When you set this to true, Photon will publish the UserIds of the players in that room.
        /// In that case, you can use PhotonPlayer.userId, to access any player's userID.
        /// This is useful for FindFriends and to set "expected users" to reserve slots in a room.
        /// </remarks>
        public bool PublishUserId { get; set; }

        /// <summary>Optionally, properties get deleted, when null gets assigned as value. Defaults to off / false.</summary>
        /// <remarks>
        /// When Op SetProperties is setting a key's value to null, the server and clients should remove the key/value from the Custom Properties.
        /// By default, the server keeps the keys (and null values) and sends them to joining players.
        ///
        /// Important: Only when SetProperties does a "broadcast", the change (key, value = null) is sent to clients to update accordingly.
        /// This applies to Custom Properties for rooms and actors/players.
        /// </remarks>
        public bool DeleteNullProperties { get; set; }

        /// <summary>By default, property changes are sent back to the client that's setting them to avoid de-sync when properties are set concurrently.</summary>
        /// <remarks>
        /// This option is enables by default to fix this scenario:
        ///
        /// 1) On server, room property ABC is set to value FOO, which triggers notifications to all the clients telling them that the property changed.
        /// 2) While that notification is in flight, a client sets the ABC property to value BAR.
        /// 3) Client receives notification from the server and changes it�s local copy of ABC to FOO.
        /// 4) Server receives the set operation and changes the official value of ABC to BAR, but never notifies the client that sent the set operation that the value is now BAR.
        ///
        /// Without this option, the client that set the value to BAR never hears from the server that the official copy has been updated to BAR, and thus gets stuck with a value of FOO.
        /// </remarks>
        public bool BroadcastPropsChangeToAll { get { return this.broadcastPropsChangeToAll; } set { this.broadcastPropsChangeToAll = value; } }
        private bool broadcastPropsChangeToAll = true;

        #if SERVERSDK
        public bool CheckUserOnJoin { get; set; }
        #endif
    }
}