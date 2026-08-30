// --------------------------------------------------------------------------------------------------------------------
// <copyright file="SocketWebTcp.cs" company="Exit Games GmbH">
//   Copyright (c) Exit Games GmbH.  All rights reserved.
// </copyright>
// <summary>
//   Internal class to encapsulate the network i/o functionality for the realtime library.
// </summary>
// <author>developer@exitgames.com</author>
// --------------------------------------------------------------------------------------------------------------------


#if UNITY_WEBGL || WEBSOCKET || WEBSOCKET_PROXYCONFIG

#if UNITY_WEBGL && !UNITY_EDITOR
#define PHOTON_WEBSOCKET_JS
#else
#define PHOTON_WEBSOCKET_CS
#endif


namespace Photon.Client
{
    using System;

    #if UNITY_2019_3_OR_NEWER
    using UnityEngine.Scripting;
    #endif

    /// <summary>
    /// Internal class to encapsulate the network i/o functionality for the realtime library.
    /// </summary>
    [Preserve]
    public class SocketWebTcp : PhotonSocket, IDisposable
    {
        private WebSocket sock;

        private readonly object syncer = new object();

        [Preserve]
        public SocketWebTcp(PeerBase npeer) : base(npeer)
        {
            this.ServerAddress = npeer.ServerAddress;
            this.ProxyServerAddress = npeer.ProxyServerAddress;
            if (this.ReportDebugOfLevel(LogLevel.Info))
            {
                this.Listener.DebugReturn(LogLevel.Info, "SocketWebTcp() "+ WebSocket.Implementation+". Server: " + this.ServerAddress + (String.IsNullOrEmpty(this.ProxyServerAddress) ? "" : ", Proxy: " + this.ProxyServerAddress));
            }

            this.PollReceive = false;
        }

        public void Dispose()
        {
            this.State = PhotonSocketState.Disconnecting;

            if (this.sock != null)
            {
                try
                {
                    if (this.sock.Connected)
                    {
                        this.sock.Close();
                    }
                }
                catch (Exception ex)
                {
                    this.EnqueueDebugReturn(LogLevel.Info, "Exception in SocketWebTcp.Dispose(): " + ex);
                }
            }

            this.sock = null;
            this.State = PhotonSocketState.Disconnected;
        }


        public override bool Connect()
        {
            this.State = PhotonSocketState.Connecting;


            if (!this.ConnectAddress.Contains("IPv6"))
            {
                this.ConnectAddress += "&IPv6"; // this makes the Photon Server return a host name for the next server (NS points to MS and MS points to GS)
            }

            // earlier, we read the proxy address/scheme and failed to connect entirely, if that wasn't successful...
            // it was either successful (using the resulting proxy address) or no connect at all...

            // we want:
            // WITH support: fail if the scheme is wrong or use it if possible
            // WITHOUT support: use proxy address, if it's a direct value (not a scheme we provide) or fail if it's a scheme

            string proxyServerAddress;
            if (!this.ReadProxyConfigScheme(this.ProxyServerAddress, this.ServerAddress, out proxyServerAddress))
            {
                this.Listener.DebugReturn(LogLevel.Info, "ReadProxyConfigScheme() failed. Using no proxy.");
            }

            this.ConnectAddress = this.ConnectAddress.Replace("//?", "/?");      // workaround for a bug in some versions of Photon .NET Client v4 and v5
            try
            {
                this.sock = new WebSocket(new Uri(this.ConnectAddress), proxyServerAddress, this.OpenCallback, this.ReceiveCallback, this.ErrorCallback, this.CloseCallback, this.SerializationProtocol);
                this.sock.DebugReturn = (LogLevel l, string s) =>
                                        {
                                            if (this.State != PhotonSocketState.Disconnected)
                                            {
                                                this.Listener.DebugReturn(l, this.State + " " + s);
                                            }
                                        };

                this.sock.Connect();
                return true;
            }
            catch (Exception e)
            {
                this.Listener.DebugReturn(LogLevel.Error, "SocketWebTcp.Connect() caught exception: " + e);
                return false;
            }
        }

        private void CloseCallback(int code, string reason)
        {
            if (this.State == PhotonSocketState.Connecting)
            {
                this.HandleException(StatusCode.ExceptionOnConnect); // sets state to Disconnecting
                return;
            }

            // passing-on close only if this socket is still used / expected to be connected
            if (this.State != PhotonSocketState.Disconnecting && this.State != PhotonSocketState.Disconnected)
            {
                this.Listener.DebugReturn(LogLevel.Error, "SocketWebTcp.CloseCallback(). Going to disconnect. Server: " + this.ServerAddress + " Error: " + code + " Reason: " + reason);
                this.HandleException(StatusCode.DisconnectByServerReasonUnknown); // sets state to Disconnecting
            }
        }

        // code can be from JsLib or WebSocket-Sharp, so it is not guaranteed to be the same in both cases
        private void ErrorCallback(int code, string message)
        {
            // passing-on errors only if this socket is still used / expected to be connected
            if (this.State != PhotonSocketState.Disconnecting && this.State != PhotonSocketState.Disconnected)
            {
                this.Listener.DebugReturn(LogLevel.Error, "SocketWebTcp.ErrorCallback(). Server: " + this.ServerAddress + " Error: " + code + " Message: " + message);

                #if PHOTON_WEBSOCKET_CS
                // websocket-sharp: only act during Connect — covers connect failures that don't produce an OnClose
                // after Connected, OnError is non-fatal
                if (this.State == PhotonSocketState.Connecting)
                {
                    this.HandleException(StatusCode.ExceptionOnConnect);
                }
                #else
                // JS: errors are always terminal in the browser
                this.HandleException(this.State != PhotonSocketState.Connected
                    ? StatusCode.ExceptionOnConnect
                    : StatusCode.ExceptionOnReceive);
                #endif
            }
        }

        private void OpenCallback()
        {
            if (State == PhotonSocketState.Connecting)
            {
                this.State = PhotonSocketState.Connected;
            }
        }


        /// <summary>
        /// Attempts to read a proxy configuration defined by a address prefix. Only available to Industries Circle members on demand.
        /// </summary>
        /// <remarks>
        /// Extended proxy support is available to Industries Circle members. Where available, proxy addresses may be defined as 'auto:', 'pac:' or 'system:'.
        /// In all other cases, the proxy address is used as is and fails to read configs (if one of the listed schemes is used).
        ///
        /// Requires file ProxyAutoConfig.cs and compile define: WEBSOCKET_PROXYCONFIG
        /// </remarks>
        /// <param name="proxyAddress">Proxy address from the server configuration.</param>
        /// <param name="url">Url to connect to (one of the Photon servers).</param>
        /// <param name="proxyUrl">Resulting proxy URL to use.</param>
        /// <returns>False if there is some error and the resulting proxy address should not be used.</returns>
        private bool ReadProxyConfigScheme(string proxyAddress, string url, out string proxyUrl)
        {
            proxyUrl = null;

            #if !WEBSOCKET_PROXYCONFIG

            if (!string.IsNullOrEmpty(proxyAddress))
            {
                if (proxyAddress.StartsWith("auto:") || proxyAddress.StartsWith("pac:") || proxyAddress.StartsWith("system:"))
                {
                    this.Listener.DebugReturn(LogLevel.Warning, "Proxy configuration via auto, pac or system is only supported with the WEBSOCKET_PROXYCONFIG define. Using no proxy instead.");
                    return true;
                }
                proxyUrl = proxyAddress;
            }

            return true;

            #else

            if (!string.IsNullOrEmpty(proxyAddress))
            {
                var httpUrl = url.ToString().Replace("ws://", "http://").Replace("wss://", "https://"); // http(s) schema required in GetProxyForUrlUsingPac call
                bool auto = proxyAddress.StartsWith("auto:", StringComparison.InvariantCultureIgnoreCase);
                bool pac = proxyAddress.StartsWith("pac:", StringComparison.InvariantCultureIgnoreCase);

                if (auto || pac)
                {
                    string pacUrl = "";
                    if (pac)
                    {
                        pacUrl = proxyAddress.Substring(4);
                        if (pacUrl.IndexOf("://") == -1)
                        {
                            pacUrl = "http://" + pacUrl; //default to http
                        }
                    }

                    string processTypeStr = auto ? "auto detect" : "pac url " + pacUrl;

                    this.Listener.DebugReturn(LogLevel.Info, "WebSocket Proxy: " + url + " " + processTypeStr);

                    string errDescr = "";
                    var err = ProxyAutoConfig.GetProxyForUrlUsingPac(httpUrl, pacUrl, out proxyUrl, out errDescr);

                    if (err != 0)
                    {
                        this.Listener.DebugReturn(LogLevel.Error, "WebSocket Proxy: " + url + " " + processTypeStr + " ProxyAutoConfig.GetProxyForUrlUsingPac() error: " + err + " (" + errDescr + ")");
                        return false;
                    }
                }
                else if (proxyAddress.StartsWith("system:", StringComparison.InvariantCultureIgnoreCase))
                {
                    this.Listener.DebugReturn(LogLevel.Info, "WebSocket Proxy: " + url + " system settings");
                    string proxyAutoConfigPacUrl;
                    var err = ProxySystemSettings.GetProxy(out proxyUrl, out proxyAutoConfigPacUrl);
                    if (err != 0)
                    {
                        this.Listener.DebugReturn(LogLevel.Error, "WebSocket Proxy: " + url + " system settings ProxySystemSettings.GetProxy() error: " + err);
                        return false;
                    }
                    if (proxyAutoConfigPacUrl != null)
                    {
                        if (proxyAutoConfigPacUrl.IndexOf("://") == -1)
                        {
                            proxyAutoConfigPacUrl = "http://" + proxyAutoConfigPacUrl; //default to http
                        }
                        this.Listener.DebugReturn(LogLevel.Info, "WebSocket Proxy: " + url + " system settings AutoConfigURL: " + proxyAutoConfigPacUrl);
                        string errDescr = "";
                        err = ProxyAutoConfig.GetProxyForUrlUsingPac(httpUrl, proxyAutoConfigPacUrl, out proxyUrl, out errDescr);

                        if (err != 0)
                        {
                            this.Listener.DebugReturn(LogLevel.Error, "WebSocket Proxy: " + url + " system settings AutoConfigURLerror: " + err + " (" + errDescr + ")");
                            return false;
                        }
                    }
                }
                else
                {
                    proxyUrl = proxyAddress;
                }

                this.Listener.DebugReturn(LogLevel.Info, "WebSocket Proxy: " + url + " -> " + (string.IsNullOrEmpty(proxyUrl) ? "DIRECT" : "PROXY " + proxyUrl));
            }

            return true;
            #endif
        }


        public override bool Disconnect()
        {
            if (this.ReportDebugOfLevel(LogLevel.Info))
            {
                this.Listener.DebugReturn(LogLevel.Info, "SocketWebTcp.Disconnect()");
            }

            this.State = PhotonSocketState.Disconnecting;

            lock (this.syncer)
            {
                if (this.sock != null)
                {
                    try
                    {
                        this.sock.Close();
                    }
                    catch (Exception ex)
                    {
                        this.Listener.DebugReturn(LogLevel.Error, "Exception in SocketWebTcp.Disconnect(): " + ex);
                    }

                    this.sock = null;
                }
            }

            this.State = PhotonSocketState.Disconnected;
            return true;
        }

        /// <summary>Used by TPeer</summary>
        public override PhotonSocketError Send(byte[] data, int length)
        {
            if (this.State != PhotonSocketState.Connected)
            {
                return PhotonSocketError.Skipped;
            }

            try
            {
                if (data.Length > length)
                {
                    byte[] trimmedData = new byte[length];
                    Buffer.BlockCopy(data, 0, trimmedData, 0, length);
                    data = trimmedData;
                }

                if (this.sock == null)
                {
                    // a Disconnect() may null the socket while a send is in progress: report Skipped instead of
                    // silently returning Success for a message that was never sent
                    return PhotonSocketError.Skipped;
                }

                this.sock.Send(data);
            }
            catch (Exception e)
            {
                this.Listener.DebugReturn(LogLevel.Error, "Cannot send to: " + this.ServerAddress + ". " + e.Message);

                this.HandleException(StatusCode.Exception);
                return PhotonSocketError.Exception;
            }

            return PhotonSocketError.Success;
        }


        public override PhotonSocketError Receive(out byte[] data)
        {
            data = null;
            return PhotonSocketError.NoData;
        }

        public void ReceiveCallback(byte[] buf, int len)
        {
            // once the websocket is disconnecting / disconnected, it should not receive anything anymore
            if (State == PhotonSocketState.Disconnecting || State == PhotonSocketState.Disconnected)
            {
                return;
            }

            try
            {
                // willBeReused must be true: the JsLib implementation re-uses its receiveBuffer across messages
                // (WebSocket.RecvCallbackInstance), so the DEBUG network-sim path has to copy before deferring.
                // websocket-sharp delivers a fresh array per message but can use the same (safe) flag.
                this.HandleReceivedDatagram(buf, len, true);
            }
            catch (Exception e)
            {
                if (this.State != PhotonSocketState.Disconnecting && this.State != PhotonSocketState.Disconnected)
                {
                    if (this.ReportDebugOfLevel(LogLevel.Error))
                    {
                        this.EnqueueDebugReturn(LogLevel.Error, "SocketWebTcp.ReceiveCallback() caught exception. Going to disconnect. State: " + this.State + ". Server: '" + this.ServerAddress + "' Exception: " + e);
                    }

                    this.HandleException(StatusCode.ExceptionOnReceive);
                }
            }
        }
    }
}

#endif