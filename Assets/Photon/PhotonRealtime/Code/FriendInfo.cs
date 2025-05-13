// ----------------------------------------------------------------------------
// <copyright file="FriendInfo.cs" company="Exit Games GmbH">
// Photon Realtime API - Copyright (C) 2022 Exit Games GmbH
// </copyright>
// <summary>
// Collection of values related to a user / friend.
// </summary>
// <author>developer@photonengine.com</author>
// ----------------------------------------------------------------------------

#if UNITY_2017_4_OR_NEWER
#define SUPPORTED_UNITY
#endif


namespace Photon.Realtime
{
    using Photon.Client;

    #if SUPPORTED_UNITY
    using SupportClass = Photon.Client.SupportClass;
    #endif


    /// <summary>Stores info about a friend's online state and in which room he/she is.</summary>
    /// <see cref="RealtimeClient.OpFindFriends"/>
    public class FriendInfo
    {
        /// <summary>The UserId of the friend. Should be set and authenticated with Custom Authentication.</summary>
        public string UserId { get; internal protected set; }

        /// <summary>True if the friend is online.</summary>
        public bool IsOnline { get; internal protected set; }

        /// <summary>The name of the room this friend is currently in. Could be used for JoinRoom. Null otherwise.</summary>
        public string Room { get; internal protected set; }

        /// <summary>True if the friend is in a room.</summary>
        public bool IsInRoom
        {
            get { return this.IsOnline && !string.IsNullOrEmpty(this.Room); }
        }

        /// <summary>Returns a string summarizing this FriendInfo.</summary>
        public override string ToString()
        {
            return string.Format("{0}\t is: {1}", this.UserId, (!this.IsOnline) ? "offline" : this.IsInRoom ? "playing" : "on master");
        }
    }
}
