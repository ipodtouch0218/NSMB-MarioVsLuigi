// -----------------------------------------------------------------------------
// <copyright company="Exit Games GmbH">
// Photon Realtime API - Copyright (C) 2022 Exit Games GmbH
// </copyright>
// <summary>Join Random Arguments.</summary>
// <author>developer@photonengine.com</author>
// -----------------------------------------------------------------------------


namespace Photon.Realtime
{
    using System;
    using Photon.Client;

    /// <summary>
    /// Parameters for the matchmaking of JoinRandomRoom and JoinRandomOrCreateRoom.
    /// </summary>
    /// <remarks>
    /// More about matchmaking: <see href="https://doc.photonengine.com/en-us/pun/current/manuals-and-demos/matchmaking-and-lobby"/>.
    /// </remarks>
    public class JoinRandomRoomArgs
    {
        /// <summary>The custom room properties a room must have to fit. All key-values must be present to match. In SQL Lobby, use SqlLobbyFilter instead.</summary>
        public PhotonHashtable ExpectedCustomRoomProperties;
        /// <summary>Filters by the MaxPlayers value of rooms. Must be a positive number (or the client will not send it) and below the Max Players limit set for the AppId.</summary>
        public int ExpectedMaxPlayers;
        /// <summary>The MatchmakingMode affects how rooms get filled. By default, the server fills rooms.</summary>
        public MatchmakingMode MatchingType;
        /// <summary>The lobby in which to match. The type affects how filters are applied.</summary>
        public TypedLobby Lobby;
        /// <summary>SQL query to filter room matches. For default-typed lobbies, use ExpectedCustomRoomProperties instead.</summary>
        public string SqlLobbyFilter;
        /// <summary>The expected users list blocks player slots for your friends or team mates to join the room, too.</summary>
        /// <remarks>See: https://doc.photonengine.com/en-us/pun/v2/lobby-and-matchmaking/matchmaking-and-lobby#matchmaking_slot_reservation </remarks>
        public string[] ExpectedUsers;        
        /// <summary>Optionally, the server side may provide a matchmaking ticket, which defines some values for matchmaking. This is not readable by clients.</summary>
        public object Ticket;
    }


    /// <summary>Renamed to JoinRandomRoomArgs.</summary>
    [Obsolete("Use JoinRandomRoomArgs")]
    public class OpJoinRandomRoomParams : JoinRandomRoomArgs
    {
    }
}