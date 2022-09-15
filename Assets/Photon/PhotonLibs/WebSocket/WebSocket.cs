#if UNITY_WEBGL || WEBSOCKET || WEBSOCKET_PROXYCONFIG

// --------------------------------------------------------------------------------------------------------------------
// <summary>
//   Provided originally by Unity to cover WebSocket support in WebGL and the Editor. Modified by Exit Games GmbH.
// </summary>
// <author>developer@exitgames.com</author>
// --------------------------------------------------------------------------------------------------------------------


namespace ExitGames.Client.Photon
{
    using System;
    using System.Text;
    using ExitGames.Client.Photon;

    #if UNITY_WEBGL && !UNITY_EDITOR
    using System.Runtime.InteropServices;
    #else
    using WebSocketSharp;
    using System.Collections.Generic;
    using System.Security.Authentication;
    #endif


    public class WebSocket
    {
        private Uri mUrl;
        private string mProxyAddress;

        /// <summary>Photon uses this to agree on a serialization protocol. Either: GpBinaryV16 or GpBinaryV18. Based on enum SerializationProtocol.</summary>
        private string protocols = "GpBinaryV16";

        public Action<DebugLevel, string> DebugReturn { get; set; }

        public WebSocket(Uri url, string proxyAddress, string protocols = null)
        {
            this.mUrl = url;
            this.mProxyAddress = proxyAddress;

            if (protocols != null)
            {
                this.protocols = protocols;
            }

            string protocol = mUrl.Scheme;
            if (!protocol.Equals("ws") && !protocol.Equals("wss"))
            {
                throw new ArgumentException("Unsupported protocol: " + protocol);
            }
        }

        public string ProxyAddress
        {
            get { return mProxyAddress; }
        }

        public void SendString(string str)
        {
            Send(Encoding.UTF8.GetBytes(str));
        }

        public string RecvString()
        {
            byte[] retval = Recv();
            if (retval == null)
                return null;
            return Encoding.UTF8.GetString(retval);
        }

        #if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern int SocketCreate (string url, string protocols);

        [DllImport("__Internal")]
        private static extern int SocketState (int socketInstance);

        [DllImport("__Internal")]
        private static extern void SocketSend (int socketInstance, byte[] ptr, int length);

        [DllImport("__Internal")]
        private static extern void SocketRecv (int socketInstance, byte[] ptr, int length);

        [DllImport("__Internal")]
        private static extern int SocketRecvLength (int socketInstance);

        [DllImport("__Internal")]
        private static extern void SocketClose (int socketInstance);

        [DllImport("__Internal")]
        private static extern int SocketError (int socketInstance, byte[] ptr, int length);

        int m_NativeRef = 0;

        public void Send(byte[] buffer)
        {
            SocketSend (m_NativeRef, buffer, buffer.Length);
        }

        public byte[] Recv()
        {
            int length = SocketRecvLength (m_NativeRef);
            if (length == 0)
                return null;
            byte[] buffer = new byte[length];
            SocketRecv (m_NativeRef, buffer, length);
            return buffer;
        }

        public void Connect()
        {
            m_NativeRef = SocketCreate (mUrl.ToString(), this.protocols);

            //while (SocketState(m_NativeRef) == 0)
            //    yield return 0;
        }

        public void Close()
        {
            SocketClose(m_NativeRef);
        }

        public bool Connected
        {
            get { return SocketState(m_NativeRef) != 0; }
        }

        public string Error
        {
            get {
                const int bufsize = 1024;
                byte[] buffer = new byte[bufsize];
                int result = SocketError (m_NativeRef, buffer, bufsize);

                if (result == 0)
                    return null;

                return Encoding.UTF8.GetString (buffer);
            }
        }
        #else
        WebSocketSharp.WebSocket m_Socket;
        Queue<byte[]> m_Messages = new Queue<byte[]>();
        bool m_IsConnected = false;
        string m_Error = null;


        public void Connect()
        {
            m_Socket = new WebSocketSharp.WebSocket(mUrl.ToString(), new string[] {this.protocols});
            m_Socket.Log.Output = (ld, f) =>
                                  {
                                      var s = string.Format("WebSocketSharp: {0}", ld.Message);
                                      switch (ld.Level)
                                      {
                                          case WebSocketSharp.LogLevel.Trace:
                                          case WebSocketSharp.LogLevel.Debug:
                                              DebugReturn(DebugLevel.ALL, s);
                                              break;
                                          case WebSocketSharp.LogLevel.Info:
                                              DebugReturn(DebugLevel.INFO, s);
                                              break;
                                          case WebSocketSharp.LogLevel.Warn:
                                              DebugReturn(DebugLevel.WARNING, s);
                                              break;
                                          case WebSocketSharp.LogLevel.Error:
                                          case WebSocketSharp.LogLevel.Fatal:
                                              DebugReturn(DebugLevel.ERROR, s);
                                              break;
                                      }
                                  };

            string user = null;
            string pass = null;

            if (!String.IsNullOrEmpty(mProxyAddress))
            {
                var authDelim = mProxyAddress.IndexOf("@");
                if (authDelim != -1)
                {
                    user = mProxyAddress.Substring(0, authDelim);
                    mProxyAddress = mProxyAddress.Substring(authDelim + 1);
                    var passDelim = user.IndexOf(":");
                    if (passDelim != -1)
                    {
                        pass = user.Substring(passDelim + 1);
                        user = user.Substring(0, passDelim);
                    }
                }

                // throws an exception, if scheme not specified
                m_Socket.SetProxy("http://" + mProxyAddress, user, pass);
            }

            if (m_Socket.IsSecure)
            {
                m_Socket.SslConfiguration.EnabledSslProtocols = m_Socket.SslConfiguration.EnabledSslProtocols | (SslProtocols)(3072 | 768);
            }

            m_Socket.OnMessage += (sender, e) => { m_Messages.Enqueue(e.RawData); };
            m_Socket.OnOpen += (sender, e) => { m_IsConnected = true; };
            m_Socket.OnError += (sender, e) => { m_Error = e.Message + (e.Exception == null ? "" : " / " + e.Exception); };

            this.m_Socket.OnClose += SocketOnClose;

            m_Socket.ConnectAsync();
        }

        private void SocketOnClose(object sender, CloseEventArgs e)
        {
            //UnityEngine.Debug.Log(e.Code.ToString());

            // this code is used for cases when the socket failed to get created (specifically used to detect "blocked by Windows firewall")
            // for some reason this situation is not calling OnError
            if (e.Code == 1006)
            {
                this.m_Error = e.Reason;
                this.m_IsConnected = false;
            }
        }

        public bool Connected
        {
            get { return m_IsConnected; }
        }


        public void Send(byte[] buffer)
        {
            m_Socket.Send(buffer);
        }

        public byte[] Recv()
        {
            if (m_Messages.Count == 0)
                return null;
            return m_Messages.Dequeue();
        }

        public void Close()
        {
            m_Socket.Close();
        }

        public string Error
        {
            get { return m_Error; }
        }
        #endif
    }
}
#endif