// -----------------------------------------------------------------------
// <copyright file="Lobby.cs" company="Exit Games GmbH">
// Photon Realtime API - Copyright (C) 2022 Exit Games GmbH
// </copyright>
// <summary>
// LobbyInfo and Lobby classes for Photon Realtime API.
// </summary>
// <author>developer@photonengine.com</author>
// ----------------------------------------------------------------------------

#if UNITY_2017_4_OR_NEWER
#define SUPPORTED_UNITY
#endif


namespace Photon.Realtime
{
    #if SUPPORTED_UNITY
    using UnityEngine;
    using Debug = UnityEngine.Debug;
    using SupportClass = Photon.Client.SupportClass;
    #endif


    /// <summary>
    /// Info for a lobby on the server. Used when <see cref="AppSettings.EnableLobbyStatistics"/> is true (can be passed to ConnectUsingSettings).
    /// </summary>
    public class TypedLobbyInfo : TypedLobby
    {
        /// <summary>Count of players that currently joined this lobby.</summary>
        public int PlayerCount { get; private set; }

        /// <summary>Count of rooms currently associated with this lobby.</summary>
        public int RoomCount { get; private set; }


        internal TypedLobbyInfo(string name, LobbyType type, int playerCount, int roomCount)
        {
            this.Name = name;
            this.Type = type;
            this.PlayerCount = playerCount;
            this.RoomCount = roomCount;
        }

        /// <summary>Returns a string representation of this TypedLobbyInfo.</summary>
        /// <returns>String representation of this TypedLobbyInfo.</returns>
        public override string ToString()
        {
            return $"LobbyInfo '{this.Name}'[{this.Type}] rooms: {RoomCount}, players: {PlayerCount}]";
        }
    }


    /// <summary>Refers to a specific lobby on the server.</summary>
    /// <remarks>
    /// Name and Type combined are the unique identifier for a lobby.<br/>
    /// The server will create lobbies "on demand", so no registration or setup is required.<br/>
    /// An empty or null Name always points to the "default lobby" as special case.
    /// </remarks>
    public class TypedLobby
    {
        /// <summary>
        /// Name of the lobby. Default: null, pointing to the "default lobby".
        /// </summary>
        /// <remarks>
        /// If Name is null or empty, a TypedLobby will point to the "default lobby". This ignores the Type value and always acts as  <see cref="LobbyType.Default"/>.
        /// </remarks>
        public string Name { get; protected set; }

        /// <summary>
        /// Type (and behaviour) of the lobby.
        /// </summary>
        /// <remarks>
        /// If Name is null or empty, a TypedLobby will point to the "default lobby". This ignores the Type value and always acts as  <see cref="LobbyType.Default"/>.
        /// </remarks>
        public LobbyType Type { get; protected set; }


        /// <summary>
        /// A reference to the default lobby which is the unique lobby that uses null as name and is of type <see cref="LobbyType.Default"/>.
        /// </summary>
        /// <remarks>
        /// There is only a single lobby with an empty name on the server. It is always of type <see cref="LobbyType.Default"/>.<br/>
        /// On the other hand, this is a shortcut and reusable reference to the default lobby.<br/>
        /// </remarks>
        public static TypedLobby Default
        {
            get
            {
                return DefaultLobby;
            }
        }

        private static readonly TypedLobby DefaultLobby = new TypedLobby();


        /// <summary>
        /// Returns if this instance points to the "default lobby" (<see cref="TypedLobby.Default"/>).
        /// </summary>
        /// <remarks>
        /// This comes up to checking if the Name is null or empty.
        /// <see cref="LobbyType.Default"/> is not the same thing as the "default lobby" (<see cref="TypedLobby.Default"/>).
        /// </remarks>
        public bool IsDefault
        {
            get { return string.IsNullOrEmpty(this.Name); }
        }

        
        /// <summary>
        /// Creates a new TypedLobby instance, initialized to the given values.
        /// </summary>
        /// <param name="name">Some string to identify a lobby. Should be non null and non empty.</param>
        /// <param name="type">The type of a lobby defines its behaviour.</param>
        public TypedLobby(string name, LobbyType type)
        {
            if (string.IsNullOrEmpty(name))
            {
                Log.Warn("Make sure to always set a name when creating a new Lobby!");
            }
            this.Name = name;
            this.Type = type;
        }

        /// <summary>
        /// Creates a new TypedLobby instance, initialized to the values of the given original.
        /// </summary>
        /// <param name="original">Used to initialize the new instance.</param>
        public TypedLobby(TypedLobby original = null)
        {
            if (original == null)
            {
                return;
            }

            this.Name = original.Name;
            this.Type = original.Type;
        }

        /// <summary>Returns a string representation of this TypedLobby.</summary>
        /// <returns>String representation of this TypedLobby.</returns>
        public override string ToString()
        {
            return $"'{this.Name}'[{this.Type}]";
        }
    }
}
