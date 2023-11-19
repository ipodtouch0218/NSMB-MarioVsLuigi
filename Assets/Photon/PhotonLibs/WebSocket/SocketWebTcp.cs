#if UNITY_WEBGL || WEBSOCKET || WEBSOCKET_PROXYCONFIG

// --------------------------------------------------------------------------------------------------------------------
// <copyright file="SocketWebTcp.cs" company="Exit Games GmbH">
//   Copyright (c) Exit Games GmbH.  All rights reserved.
// </copyright>
// <summary>
//   Internal class to encapsulate the network i/o functionality for the realtime library.
// </summary>
// <author>developer@exitgames.com</author>
// --------------------------------------------------------------------------------------------------------------------


namespace ExitGames.Client.Photon
{
    using System;
    using System.Collections;
    using UnityEngine;
    using UnityEngine.Scripting;
    using SupportClassPun = SupportClass;


    /// <summary>
    /// Yield Instruction to Wait for real seconds. Very important to keep connection working if Time.TimeScale is altered, we still want accurate network events
    /// </summary>
    public sealed class WaitForRealSeconds : CustomYieldInstruction
    {
        private readonly float _endTime;

        public override bool keepWaiting
        {
            get { return this._endTime > Time.realtimeSinceStartup; }
        }

        public WaitForRealSeconds(float seconds)
        {
            this._endTime = Time.realtimeSinceStartup + seconds;
        }
    }


    /// <summary>
    /// Internal class to encapsulate the network i/o functionality for the realtime libary.
    /// </summary>
    public class SocketWebTcp : IPhotonSocket, IDisposable
    {
        private WebSocket sock;

        private readonly object syncer = new object();

        [Preserve]
        public SocketWebTcp(PeerBase npeer) : base(npeer)
        {
            this.ServerAddress = npeer.ServerAddress;
            this.ProxyServerAddress = npeer.ProxyServerAddress;
            if (this.ReportDebugOfLevel(DebugLevel.INFO))
            {
                this.Listener.DebugReturn(DebugLevel.INFO, "new SocketWebTcp() for Unity. Server: " + this.ServerAddress + (String.IsNullOrEmpty(this.ProxyServerAddress) ? "" : ", Proxy: " + this.ProxyServerAddress));
            }

            //this.Protocol = ConnectionProtocol.WebSocket;
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
                    this.EnqueueDebugReturn(DebugLevel.INFO, "Exception in SocketWebTcp.Dispose(): " + ex);
                }
            }

            this.sock = null;
            this.State = PhotonSocketState.Disconnected;
        }

        GameObject websocketConnectionObject;

        public override bool Connect()
        {
            //bool baseOk = base.Connect();
            //if (!baseOk)
            //{
            //    return false;
            //}


            this.State = PhotonSocketState.Connecting;


            if (this.websocketConnectionObject != null)
            {
                UnityEngine.Object.Destroy(this.websocketConnectionObject);
            }

            this.websocketConnectionObject = new GameObject("websocketConnectionObject");
            MonoBehaviour mb = this.websocketConnectionObject.AddComponent<MonoBehaviourExt>();
            this.websocketConnectionObject.hideFlags = HideFlags.HideInHierarchy;
            UnityEngine.Object.DontDestroyOnLoad(this.websocketConnectionObject);


            this.ConnectAddress += "&IPv6"; // this makes the Photon Server return a host name for the next server (NS points to MS and MS points to GS)


            // earlier, we read the proxy address/scheme and failed to connect entirely, if that wasn't successful...
            // it was either successful (using the resulting proxy address) or no connect at all...

            // we want:
            // WITH support: fail if the scheme is wrong or use it if possible
            // WITHOUT support: use proxy address, if it's a direct value (not a scheme we provide) or fail if it's a scheme

            string proxyServerAddress;
            if (!this.ReadProxyConfigScheme(this.ProxyServerAddress, this.ServerAddress, out proxyServerAddress))
            {
                this.Listener.DebugReturn(DebugLevel.INFO, "ReadProxyConfigScheme() failed. Using no proxy.");
            }


            try
            {
                this.sock = new WebSocket(new Uri(this.ConnectAddress), proxyServerAddress, this.SerializationProtocol);
                this.sock.DebugReturn = (DebugLevel l, string s) =>
                                        {
                                            if (this.State != PhotonSocketState.Disconnected)
                                            {
                                                this.Listener.DebugReturn(l, this.State + " " + s);
                                            }
                                        };

                this.sock.Connect();
                mb.StartCoroutine(this.ReceiveLoop());

                return true;
            }
            catch (Exception e)
            {
                this.Listener.DebugReturn(DebugLevel.ERROR, "SocketWebTcp.Connect() caught exception: " + e);
                return false;
            }
        }


        /// <summary>
        /// Attempts to read a proxy configuration defined by a address prefix. Only available to Industries Circle members on demand.
        /// </summary>
        /// <remarks>
        /// Extended proxy support is available to Industries Circle members. Where available, proxy addresses may be defined as 'auto:', 'pac:' or 'system:'.
        /// In all other cases, the proxy address is used as is and fails to read configs (if one of the listed schemes is used).
        ///
        /// Requires file ProxyAutoConfig.cs and compile define: WEBSOCKET_PROXYCONFIG_SUPPORT.
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
                    this.Listener.DebugReturn(DebugLevel.WARNING, "Proxy configuration via auto, pac or system is only supported with the WEBSOCKET_PROXYCONFIG define. Using no proxy instead.");
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

                    this.Listener.DebugReturn(DebugLevel.INFO, "WebSocket Proxy: " + url + " " + processTypeStr);

                    string errDescr = "";
                    var err = ProxyAutoConfig.GetProxyForUrlUsingPac(httpUrl, pacUrl, out proxyUrl, out errDescr);

                    if (err != 0)
                    {
                        this.Listener.DebugReturn(DebugLevel.ERROR, "WebSocket Proxy: " + url + " " + processTypeStr + " ProxyAutoConfig.GetProxyForUrlUsingPac() error: " + err + " (" + errDescr + ")");
                        return false;
                    }
                }
                else if (proxyAddress.StartsWith("system:", StringComparison.InvariantCultureIgnoreCase))
                {
                    this.Listener.DebugReturn(DebugLevel.INFO, "WebSocket Proxy: " + url + " system settings");
                    string proxyAutoConfigPacUrl;
                    var err = ProxySystemSettings.GetProxy(out proxyUrl, out proxyAutoConfigPacUrl);
                    if (err != 0)
                    {
                        this.Listener.DebugReturn(DebugLevel.ERROR, "WebSocket Proxy: " + url + " system settings ProxySystemSettings.GetProxy() error: " + err);
                        return false;
                    }
                    if (proxyAutoConfigPacUrl != null)
                    {
                        if (proxyAutoConfigPacUrl.IndexOf("://") == -1)
                        {
                            proxyAutoConfigPacUrl = "http://" + proxyAutoConfigPacUrl; //default to http
                        }
                        this.Listener.DebugReturn(DebugLevel.INFO, "WebSocket Proxy: " + url + " system settings AutoConfigURL: " + proxyAutoConfigPacUrl);
                        string errDescr = "";
                        err = ProxyAutoConfig.GetProxyForUrlUsingPac(httpUrl, proxyAutoConfigPacUrl, out proxyUrl, out errDescr);

                        if (err != 0)
                        {
                            this.Listener.DebugReturn(DebugLevel.ERROR, "WebSocket Proxy: " + url + " system settings AutoConfigURLerror: " + err + " (" + errDescr + ")");
                            return false;
                        }
                    }
                }
                else
                {
                    proxyUrl = proxyAddress;
                }

                this.Listener.DebugReturn(DebugLevel.INFO, "WebSocket Proxy: " + url + " -> " + (string.IsNullOrEmpty(proxyUrl) ? "DIRECT" : "PROXY " + proxyUrl));
            }

            return true;
            #endif
        }



        public override bool Disconnect()
        {
            if (this.ReportDebugOfLevel(DebugLevel.INFO))
            {
                this.Listener.DebugReturn(DebugLevel.INFO, "SocketWebTcp.Disconnect()");
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
                        this.Listener.DebugReturn(DebugLevel.ERROR, "Exception in SocketWebTcp.Disconnect(): " + ex);
                    }

                    this.sock = null;
                }
            }

            if (this.websocketConnectionObject != null)
            {
                UnityEngine.Object.Destroy(this.websocketConnectionObject);
            }

            this.State = PhotonSocketState.Disconnected;
            return true;
        }

        /// <summary>
        /// used by TPeer*
        /// </summary>
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

                //if (this.ReportDebugOfLevel(DebugLevel.ALL))
                //{
                //    this.Listener.DebugReturn(DebugLevel.ALL, "Sending: " + SupportClassPun.ByteArrayToString(data));
                //}

                if (this.sock != null)
                {
                    this.sock.Send(data);
                }
            }
            catch (Exception e)
            {
                this.Listener.DebugReturn(DebugLevel.ERROR, "Cannot send to: " + this.ServerAddress + ". " + e.Message);

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


        internal const int ALL_HEADER_BYTES = 9;
        internal const int TCP_HEADER_BYTES = 7;
        internal const int MSG_HEADER_BYTES = 2;

        public IEnumerator ReceiveLoop()
        {
            //this.Listener.DebugReturn(DebugLevel.INFO, "ReceiveLoop()");
            if (this.sock != null)
            {
                while (this.sock != null && !this.sock.Connected && this.sock.Error == null)
                {
                    yield return new WaitForRealSeconds(0.1f);
                }

                if (this.sock != null)
                {
                    if (this.sock.Error != null)
                    {
                        this.Listener.DebugReturn(DebugLevel.ERROR, "Exiting receive thread. Server: " + this.ServerAddress + " Error: " + this.sock.Error);
                        this.HandleException(StatusCode.ExceptionOnConnect);
                    }
                    else
                    {
                        // connected
                        if (this.ReportDebugOfLevel(DebugLevel.ALL))
                        {
                            this.Listener.DebugReturn(DebugLevel.ALL, "Receiving by websocket. this.State: " + this.State);
                        }

                        this.State = PhotonSocketState.Connected;
                        this.peerBase.OnConnect();

                        while (this.State == PhotonSocketState.Connected)
                        {
                            if (this.sock != null)
                            {
                                if (this.sock.Error != null)
                                {
                                    this.Listener.DebugReturn(DebugLevel.ERROR, "Exiting receive thread (inside loop). Server: " + this.ServerAddress + " Error: " + this.sock.Error);
                                    this.HandleException(StatusCode.ExceptionOnReceive);
                                    break;
                                }
                                else
                                {
                                    byte[] inBuff = this.sock.Recv();
                                    if (inBuff == null || inBuff.Length == 0)
                                    {
                                        // nothing received. wait a bit, try again
                                        yield return new WaitForRealSeconds(0.02f);
                                        continue;
                                    }

                                    //if (this.ReportDebugOfLevel(DebugLevel.ALL))
                                    //{
                                    //    this.Listener.DebugReturn(DebugLevel.ALL, "TCP << " + inBuff.Length + " = " + SupportClassPun.ByteArrayToString(inBuff));
                                    //}

                                    if (inBuff.Length > 0)
                                    {
                                        try
                                        {
                                            this.HandleReceivedDatagram(inBuff, inBuff.Length, false);
                                        }
                                        catch (Exception e)
                                        {
                                            if (this.State != PhotonSocketState.Disconnecting && this.State != PhotonSocketState.Disconnected)
                                            {
                                                if (this.ReportDebugOfLevel(DebugLevel.ERROR))
                                                {
                                                    this.EnqueueDebugReturn(DebugLevel.ERROR, "Receive issue. State: " + this.State + ". Server: '" + this.ServerAddress + "' Exception: " + e);
                                                }

                                                this.HandleException(StatusCode.ExceptionOnReceive);
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }

            this.Disconnect();
        }


        private class MonoBehaviourExt : MonoBehaviour
        {
        }
    }
}


#endif