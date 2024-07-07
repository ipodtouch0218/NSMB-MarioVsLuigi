// -----------------------------------------------------------------------
// <copyright file="AppSettings.cs" company="Exit Games GmbH">
// Photon Realtime API - Copyright (C) 2022 Exit Games GmbH
// </copyright>
// <summary>Settings for Photon application(s) and the server to connect to.</summary>
// <author>developer@photonengine.com</author>
// ----------------------------------------------------------------------------

#if UNITY_2017_4_OR_NEWER
#define SUPPORTED_UNITY
#endif

namespace Photon.Realtime
{
    using System;
    using Photon.Client;

    #if SUPPORTED_UNITY || NETFX_CORE
    using SupportClass = Photon.Client.SupportClass;
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
    public class AppSettings
    {
        /// <summary>AppId for Realtime or PUN.</summary>
        public string AppIdRealtime;

        /// <summary>AppId for Photon Fusion.</summary>
        public string AppIdFusion;

        /// <summary>AppId for Photon Quantum.</summary>
        public string AppIdQuantum;

        /// <summary>AppId for Photon Chat.</summary>
        public string AppIdChat;

        /// <summary>AppId for Photon Voice.</summary>
        public string AppIdVoice;

        /// <summary>The AppVersion can be used to identify builds and will split the AppId distinct "Virtual AppIds" (important for matchmaking).</summary>
        public string AppVersion;


        /// <summary>If false, the app will attempt to connect to a Master Server (which is obsolete but sometimes still necessary).</summary>
        /// <remarks>if true, Server points to a NameServer (or is null, using the default), else it points to a MasterServer.</remarks>
        public bool UseNameServer = true;

        /// <summary>Can be set to any of the Photon Cloud's region names to directly connect to that region.</summary>
        /// <remarks>if this IsNullOrEmpty() AND UseNameServer == true, use BestRegion. else, use a server</remarks>
        public string FixedRegion;

        /// <summary>Set to a previous BestRegionSummary value before connecting.</summary>
        /// <remarks>
        /// This is a value used when the client connects to the "Best Region".<br/>
        /// If this is null or empty, all regions gets pinged. Providing a previous summary on connect,
        /// speeds up best region selection and makes the previously selected region "sticky".<br/>
        ///
        /// Unity clients should store the BestRegionSummary in the PlayerPrefs.
        /// You can store the new result by implementing <see cref="IConnectionCallbacks.OnConnectedToMaster"/>.
        /// If <see cref="RealtimeClient.SummaryToCache"/> is not null, store this string.
        /// To avoid storing the value multiple times, you could set SummaryToCache to null.
        /// </remarks>
        #if SUPPORTED_UNITY
        [NonSerialized]
        #endif
        public string BestRegionSummaryFromStorage;

        /// <summary>The address (hostname or IP) of the server to connect to.</summary>
        public string Server;

        /// <summary>If not null, this sets the port of the first Photon server to connect to (that will "forward" the client as needed).</summary>
        public ushort Port;

        /// <summary>
        /// Defines a proxy URL for WebSocket connections. Can be the proxy or point to a .pac file.
        /// </summary>
        /// <remarks>
        /// This URL supports various definitions:
        ///
        /// "user:pass@proxyaddress:port"<br/>
        /// "proxyaddress:port"<br/>
        /// "system:"<br/>
        /// "pac:"<br/>
        /// "pac:http://host/path/pacfile.pac"<br/>
        ///
        /// Important: Don't define a protocol, except to point to a pac file. The proxy address should not begin with http:// or https://.
        /// </remarks>
        public string ProxyServer;

        /// <summary>The network level protocol to use.</summary>
        public ConnectionProtocol Protocol = ConnectionProtocol.Udp;

        /// <summary>Enables a fallback to another protocol in case a connect to the Name Server fails.</summary>
        /// <remarks>
        /// When connecting to the Name Server does not succeed, the client will select an alternative
        /// transport protocol and automatically try to connect with that.
        /// The fallback for TCP is UDP. All other protocols fallback to TCP.
        ///
        /// The fallback will use the default Name Server port as defined by ProtocolToNameServerPort.
        /// </remarks>
        public bool EnableProtocolFallback = true;

        /// <summary>Defines how authentication is done. On each system, once or once via a WSS connection (safe).</summary>
        public AuthModeOption AuthMode = AuthModeOption.AuthOnceWss;

        /// <summary>If true, the Master Server will send statistics for currently used lobbies. Defaults to false.</summary>
        /// <remarks>
        /// The lobby statistics can be useful if your title dynamically uses lobbies, depending (e.g.)
        /// on current player activity or such. The provided list of stats is capped to 500.
        ///
        /// Changing the value while being connected has no immediate effect. Set this before connecting.
        ///
        /// Implement ILobbyCallbacks.OnLobbyStatisticsUpdate to get the list of used lobbies.
        /// </remarks>
        public bool EnableLobbyStatistics;

        /// <summary>Log level for the PhotonPeer and connection. Useful to debug connection related issues.</summary>
        public LogLevel NetworkLogging = LogLevel.Error;
        
        /// <summary>Log level for the RealtimeClient and callbacks. Useful to get info about the client state, servers it uses and operations called.</summary>
        public LogLevel ClientLogging = LogLevel.Warning;


        /// <summary>If true, the Server field contains a Master Server address (if any address at all).</summary>
        public bool IsMasterServerAddress
        {
            get { return !this.UseNameServer; }
        }

        /// <summary>If true, the client should fetch the region list from the Name Server and find the one with best ping.</summary>
        /// <remarks>See "Best Region" in the online docs.</remarks>
        public bool IsBestRegion
        {
            get { return this.UseNameServer && string.IsNullOrEmpty(this.FixedRegion); }
        }

        /// <summary>If true, the default nameserver address for the Photon Cloud should be used.</summary>
        public bool IsDefaultNameServer
        {
            get { return this.UseNameServer && string.IsNullOrEmpty(this.Server); }
        }

        /// <summary>If true, the default ports for a protocol will be used.</summary>
        public bool IsDefaultPort
        {
            get { return this.Port <= 0; }
        }


        /// <summary>Creates an AppSettings instance with default values.</summary>
        public AppSettings()
        {
        }

        /// <summary>
        /// Initializes the AppSettings with default values or the provided original.
        /// </summary>
        /// <param name="original">If non-null, all values are copied from the original.</param>
        public AppSettings(AppSettings original = null)
        {
            if (original != null)
            {
                original.CopyTo(this);
            }
        }


        /// <summary>Gets the AppId for a specific type of client.</summary>
        public string GetAppId(ClientAppType ct)
        {
            switch (ct)
            {
                case ClientAppType.Realtime:
                    return this.AppIdRealtime;
                case ClientAppType.Fusion:
                    return this.AppIdFusion;
                case ClientAppType.Quantum:
                    return this.AppIdQuantum;
                case ClientAppType.Voice:
                    return this.AppIdVoice;
                case ClientAppType.Chat:
                    return this.AppIdChat;
                default:
                    return null;
            }
        }


        /// <summary>Tries to detect the ClientAppType, based on which AppId values are present. Can detect Realtime, Fusion or Quantum. Used when the RealtimeClient.ClientType is set to detect.</summary>
        /// <returns>Most likely to be used ClientAppType or ClientAppType.Detect in conflicts.</returns>
        public ClientAppType ClientTypeDetect()
        {
            bool ra = !string.IsNullOrEmpty(this.AppIdRealtime);
            bool fa = !string.IsNullOrEmpty(this.AppIdFusion);
            bool qa = !string.IsNullOrEmpty(this.AppIdQuantum);

            if (ra && !fa && !qa)
            {
                return ClientAppType.Realtime;
            }
            if (fa && !ra && !qa)
            {
                return ClientAppType.Fusion;
            }
            if (qa && !ra && !fa)
            {
                return ClientAppType.Quantum;
            }

            Log.Error("ConnectUsingSettings requires that the AppSettings contain exactly one value set out of AppIdRealtime, AppIdFusion or AppIdQuantum.");
            return ClientAppType.Detect;
        }


        /// <summary>ToString but with more details.</summary>
        public string ToStringFull()
        {
            return string.Format(
                                 "appId {0}{1}{2}{3}" +
                                 "use ns: {4}, reg: {5}, {9}, " +
                                 "{6}{7}{8}" +
                                 "auth: {10}",
                                 string.IsNullOrEmpty(this.AppIdRealtime) ? string.Empty : "Realtime/PUN: " + this.HideAppId(this.AppIdRealtime) + ", ",
                                 string.IsNullOrEmpty(this.AppIdFusion) ? string.Empty : "Fusion: " + this.HideAppId(this.AppIdFusion) + ", ",
                                 string.IsNullOrEmpty(this.AppIdQuantum) ? string.Empty : "Quantum: " + this.HideAppId(this.AppIdQuantum) + ", ",
                                 string.IsNullOrEmpty(this.AppIdChat) ? string.Empty : "Chat: " + this.HideAppId(this.AppIdChat) + ", ",
                                 string.IsNullOrEmpty(this.AppIdVoice) ? string.Empty : "Voice: " + this.HideAppId(this.AppIdVoice) + ", ",
                                 string.IsNullOrEmpty(this.AppVersion) ? string.Empty : "AppVersion: " + this.AppVersion + ", ",
                                 "UseNameServer: " + this.UseNameServer + ", ",
                                 "Fixed Region: " + this.FixedRegion + ", ",
                                 //this.BestRegionSummaryFromStorage,
                                 string.IsNullOrEmpty(this.Server) ? string.Empty : "Server: " + this.Server + ", ",
                                 this.IsDefaultPort ? string.Empty : "Port: " + this.Port + ", ",
                                 string.IsNullOrEmpty(this.ProxyServer) ? string.Empty : "Proxy: " + this.ProxyServer + ", ",
                                 this.Protocol,
                                 this.AuthMode
                                 //this.EnableLobbyStatistics,
                                 //this.NetworkLogging,
                                );
        }


        /// <summary>Checks if a string is a Guid by attempting to create one.</summary>
        /// <param name="val">The potential guid to check.</param>
        /// <returns>True if new Guid(val) did not fail.</returns>
        public static bool IsAppId(string val)
        {
            try
            {
                new Guid(val);
            }
            catch
            {
                return false;
            }

            return true;
        }


        private string HideAppId(string appId)
        {
            return string.IsNullOrEmpty(appId) || appId.Length < 8
                       ? appId
                       : string.Concat(appId.Substring(0, 8), "***");
        }

        /// <summary>Copies values of this instance to the target.</summary>
        /// <param name="target">Target instance.</param>
        /// <returns>The target.</returns>
        public AppSettings CopyTo(AppSettings target)
        {
            target.AppIdRealtime = this.AppIdRealtime;
            target.AppIdFusion = this.AppIdFusion;
            target.AppIdQuantum = this.AppIdQuantum;
            target.AppIdChat = this.AppIdChat;
            target.AppIdVoice = this.AppIdVoice;
            target.AppVersion = this.AppVersion;
            target.UseNameServer = this.UseNameServer;
            target.FixedRegion = this.FixedRegion;
            target.BestRegionSummaryFromStorage = this.BestRegionSummaryFromStorage;
            target.Server = this.Server;
            target.Port = this.Port;
            target.ProxyServer = this.ProxyServer;
            target.Protocol = this.Protocol;
            target.AuthMode = this.AuthMode;
            target.EnableLobbyStatistics = this.EnableLobbyStatistics;
            target.ClientLogging = this.ClientLogging;
            target.NetworkLogging = this.NetworkLogging;
            target.EnableProtocolFallback = this.EnableProtocolFallback;
            return target;
        }
    }
}
