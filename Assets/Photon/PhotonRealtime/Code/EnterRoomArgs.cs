// -----------------------------------------------------------------------------
// <copyright company="Exit Games GmbH">
// Photon Realtime API - Copyright (C) 2022 Exit Games GmbH
// </copyright>
// <summary>Room creation options.</summary>
// <author>developer@photonengine.com</author>
// -----------------------------------------------------------------------------


namespace Photon.Realtime
{
    using System;


    /// <summary>Parameters for creating rooms.</summary>
    public class EnterRoomArgs
    {
        /// <summary>The name of the room to create. If null, the server generates a unique name. If not null, it must be unique and new or will cause an error.</summary>
        public string RoomName;
        /// <summary>The RoomOptions define the optional behaviour of rooms.</summary>
        public RoomOptions RoomOptions;
        /// <summary>A lobby to attach the new room to. If set, this overrides a joined lobby (if any).</summary>
        public TypedLobby Lobby;
        /// <summary>A list of users who are expected to join the room along with this client. Reserves slots for rooms with MaxPlayers value.</summary>
        public string[] ExpectedUsers;

        /// <summary>Ticket for matchmaking. Provided by a plugin / server and contains a list of party members who should join the same room (among other things).</summary>
        public object Ticket;

        /// <summary>Internally used value to skip some values when the operation is sent to the Master Server.</summary>
        protected internal bool OnGameServer = true; // defaults to true! better send more parameter than too few (GS needs all)
        /// <summary>Internally used value to check which join mode we should call.</summary>
        protected internal JoinMode JoinMode;


        /// <summary>Creates a new instance of EnterRoomArgs with the same referenced values still referencing the same RoomOptions.</summary>
        /// <remarks>
        /// This instance contains a unique reference to the room name, which can be changed
        /// when the server assigns a room name - independent from other clients using the same EnterRoomArgs.
        ///
        /// RealtimeClient.enterRoomArgumentsCache should use a new instance of EnterRoomArgs,
        /// so you can run multiple clients and all share the same EnterRoomArgs initially
        /// but get assigned to individual rooms by the Master Server. If there is only
        /// one instance of EnterRoomArgs, they will all share that, which is a fairly hard to
        /// debug use case.
        /// 
        /// Hence this fix / workaround.
        /// 
        /// It is OK to re-use the RoomOptions on the other hand.
        /// </remarks>
        /// <param name="o">Original EnterRoomArgs to copy values from.</param>
        /// <returns>New instance of EnterRoomArgs, re-using the instance of RoomOptions.</returns>
        internal static EnterRoomArgs ShallowCopyToNewArgs(EnterRoomArgs o)
        {
            EnterRoomArgs result = new EnterRoomArgs();

            if (o != null)
            {
                result.RoomName = o.RoomName;
                result.RoomOptions = o.RoomOptions;
                result.Lobby = o.Lobby;
                result.ExpectedUsers = o.ExpectedUsers;
                result.Ticket = o.Ticket;
                result.JoinMode = o.JoinMode;
            }

            return result;
        }
    }



    /// <summary>Renamed to FindFriendsArgs.</summary>
    [Obsolete("Use EnterRoomArgs")]
    public class EnterRoomParams : EnterRoomArgs
    {
    }
}