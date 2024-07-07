// -----------------------------------------------------------------------------
// <copyright company="Exit Games GmbH">
// Photon Realtime API - Copyright (C) 2022 Exit Games GmbH
// </copyright>
// <summary>Arguments for FindFriends.</summary>
// <author>developer@photonengine.com</author>
// -----------------------------------------------------------------------------


namespace Photon.Realtime
{
    using System;

    /// <summary>
    /// Options for OpFindFriends can be combined to filter which rooms of friends are returned.
    /// </summary>
    public class FindFriendsArgs
    {
        /// <summary>Include a friend's room only if it is created and confirmed by the game server.</summary>
        public bool CreatedOnGs = false; //flag: 0x01
        /// <summary>Include a friend's room only if it is visible (using Room.IsVisible).</summary>
        public bool Visible = false; //flag: 0x02
        /// <summary>Include a friend's room only if it is open (using Room.IsOpen).</summary>
        public bool Open = false; //flag: 0x04

        /// <summary>Turns the bool args into an integer, which is sent as option flags for Op FindFriends.</summary>
        /// <returns>The args applied to bits of an integer.</returns>
        internal int ToIntFlags()
        {
            int optionFlags = 0;
            if (this.CreatedOnGs)
            {
                optionFlags = optionFlags | 0x1;
            }
            if (this.Visible)
            {
                optionFlags = optionFlags | 0x2;
            }
            if (this.Open)
            {
                optionFlags = optionFlags | 0x4;
            }
            return optionFlags;
        }
    }


    /// <summary>Renamed to FindFriendsArgs.</summary>
    [Obsolete("Use FindFriendsArgs")]
    public class FindFriendsOptions : FindFriendsArgs
    {
    }
}