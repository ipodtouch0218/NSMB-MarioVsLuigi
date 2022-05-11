// -----------------------------------------------------------------------
// <copyright file="ChatAppSettings.cs" company="Exit Games GmbH">
//   Chat API for Photon - Copyright (C) 2018 Exit Games GmbH
// </copyright>
// <summary>Settings for Photon Chat application and the server to connect to.</summary>
// <author>developer@photonengine.com</author>
// ----------------------------------------------------------------------------

#if UNITY_4_7 || UNITY_5 || UNITY_5_3_OR_NEWER
#define SUPPORTED_UNITY
#endif


namespace Photon.Chat
{
    using System;
    using ExitGames.Client.Photon;
    #if SUPPORTED_UNITY
    using UnityEngine.Serialization;
    #endif

    /// <summary>
    /// Settings for Photon application(s) and the server to connect to.
    /// </summary>
    /// <remarks>
    /// This is Serializable for Unity, so it can be included in ScriptableObject instances.
    /// </remarks>
    #if !NETFX_CORE || SUPPORTED_UNITY
    [Serializable]
    #endif
    public class ChatAppSettings
    {
        /// <summary>AppId for the Chat Api.</summary>
        #if SUPPORTED_UNITY
        [FormerlySerializedAs("AppId")]
        #endif
        public string AppIdChat;

        /// <summary>The AppVersion can be used to identify builds and will split the AppId distinct "Virtual AppIds" (important for the users to find each other).</summary>
        public string AppVersion;

        /// <summary>Can be set to any of the Photon Cloud's region names to directly connect to that region.</summary>
        public string FixedRegion;

        /// <summary>The address (hostname or IP) of the server to connect to.</summary>
        public string Server;

        /// <summary>If not null, this sets the port of the first Photon server to connect to (that will "forward" the client as needed).</summary>
        public ushort Port;

        /// <summary>The network level protocol to use.</summary>
        public ConnectionProtocol Protocol = ConnectionProtocol.Udp;

        /// <summary>Enables a fallback to another protocol in case a connect to the Name Server fails.</summary>
        /// <remarks>See: LoadBalancingClient.EnableProtocolFallback.</remarks>
        public bool EnableProtocolFallback = true;

        /// <summary>Log level for the network lib.</summary>
        public DebugLevel NetworkLogging = DebugLevel.ERROR;

        /// <summary>If true, the default nameserver address for the Photon Cloud should be used.</summary>
        public bool IsDefaultNameServer { get { return string.IsNullOrEmpty(this.Server); } }


        /// <summary>Available to not immediately break compatibility.</summary>
        [Obsolete("Use AppIdChat instead.")]
        public string AppId
        {
            get { return this.AppIdChat; }
            set { this.AppIdChat = value; }
        }
    }
}