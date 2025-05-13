#if UNITY_WEBGL || WEBSOCKET || WEBSOCKET_PROXYCONFIG

#if UNITY_WEBGL && !UNITY_EDITOR
#define PHOTON_WEBSOCKET_JS
#else
#define PHOTON_WEBSOCKET_CS
#endif

// --------------------------------------------------------------------------------------------------------------------
// <summary>
//   Provided originally by Unity to cover WebSocket support in WebGL and the Editor. Modified by Exit Games GmbH.
// </summary>
// <author>developer@exitgames.com</author>
// --------------------------------------------------------------------------------------------------------------------


namespace Photon.Client
{
    using System;

    #if PHOTON_WEBSOCKET_JS
    using System.Collections.Generic;
    using System.Runtime.InteropServices;
    using System.Text;
    using AOT;
    #else
    using WebSocketSharp;
    using System.Security.Authentication;
    #endif


    // changed mProxyAddress to ProxyAddress
    // changed mUrl to Url

    public partial class WebSocket
    {
        /// <summary>Server address</summary>
        public Uri Url { get; private set; }

        /// <summary>Only supported by WebSocket-sharp dll.</summary>
        public string ProxyAddress { get; private set; }

        /// <summary>Photon uses this to agree on a serialization protocol. Either: GpBinaryV16 or GpBinaryV18. Based on enum SerializationProtocol.</summary>
        private readonly string protocols = "GpBinaryV16";


        /// <summary>True after the websocket callback OnConnect until close or (permanent) error.</summary>
        public bool Connected { get; private set; }

        /// <summary>Null until some error happened in underlying websocket.</summary>
        public string Error { get; private set; }


        // callbacks to higher level
        private Action<byte[], int> recvCallback;
        private Action openCallback;
        private Action<int, string> errorCallback;
        private Action<int, string> closeCallback;
        // logging callback
        public Action<LogLevel, string> DebugReturn { get; set; }


        public WebSocket(Uri url, string proxyAddress, Action openCallback, Action<byte[], int>  recvCallback, Action<int, string> errorCallback, Action<int, string> closeCallback, string protocols = null)
        {
            this.Url = url;
            this.ProxyAddress = proxyAddress;

            this.recvCallback = recvCallback;
            this.openCallback = openCallback;
            this.errorCallback = errorCallback;
            this.closeCallback = closeCallback;

            if (!string.IsNullOrEmpty(protocols))
            {
                this.protocols = protocols;
            }

            string scheme = this.Url.Scheme;
            if (!scheme.Equals("ws") && !scheme.Equals("wss"))
            {
                throw new ArgumentException("Unsupported protocol: " + scheme);
            }
        }
    }


    // .net specific implementation using websocket-sharp.dll
    public partial class WebSocket
    {
        #if PHOTON_WEBSOCKET_CS

        public const string Implementation = "WebSocketSharp";

        WebSocketSharp.WebSocket m_Socket;


        public void Connect()
        {
            this.m_Socket = new WebSocketSharp.WebSocket(this.Url.ToString(), new string[] {this.protocols});
            this.m_Socket.Log.Output = (ld, f) =>
                                  {
                                      var s = string.Format("WebSocketSharp: {0}", ld.Message);
                                      switch (ld.Level)
                                      {
                                          case WebSocketSharp.LogLevel.Trace:
                                          case WebSocketSharp.LogLevel.Debug:
                                              DebugReturn(LogLevel.Debug, s);
                                              break;
                                          case WebSocketSharp.LogLevel.Info:
                                              DebugReturn(LogLevel.Info, s);
                                              break;
                                          case WebSocketSharp.LogLevel.Warn:
                                              DebugReturn(LogLevel.Warning, s);
                                              break;
                                          case WebSocketSharp.LogLevel.Error:
                                          case WebSocketSharp.LogLevel.Fatal:
                                              DebugReturn(LogLevel.Error, s);
                                              break;
                                      }
                                  };

            this.m_Socket.OnOpen += (sender, e) =>
                                    {
                                        this.Connected = true;
                                        this.openCallback();
                                    };
            this.m_Socket.OnMessage += (sender, e) =>
                                       {
                                           this.recvCallback(e.RawData, e.RawData.Length);
                                       };
            this.m_Socket.OnError += (sender, e) =>
                                     {
                                         this.Connected = false;
                                         this.Error = e.Message + (e.Exception == null ? "" : " / " + e.Exception);
                                         this.errorCallback(0, e.Message);
                                     };
            this.m_Socket.OnClose += (sender, e) =>
                                     {
                                         this.Connected = false;
                                         this.closeCallback(e.Code, e.Reason);
                                     };


            if (!String.IsNullOrEmpty(this.ProxyAddress))
            {
                string user = null;
                string pass = null;

                var authDelim = this.ProxyAddress.IndexOf("@");
                if (authDelim != -1)
                {
                    user = this.ProxyAddress.Substring(0, authDelim);
                    this.ProxyAddress = this.ProxyAddress.Substring(authDelim + 1);
                    var passDelim = user.IndexOf(":");
                    if (passDelim != -1)
                    {
                        pass = user.Substring(passDelim + 1);
                        user = user.Substring(0, passDelim);
                    }
                }

                // throws an exception, if scheme not specified
                this.m_Socket.SetProxy("http://" + this.ProxyAddress, user, pass);
            }

            if (this.m_Socket.IsSecure)
            {
                this.m_Socket.SslConfiguration.EnabledSslProtocols = this.m_Socket.SslConfiguration.EnabledSslProtocols | (SslProtocols)(3072 | 768);
            }


            this.m_Socket.ConnectAsync();
        }


        public void Close()
        {
            // at this low level we are fine with closing the socket async / non-blocking
            this.m_Socket.CloseAsync();
        }

        public void Send(byte[] buffer)
        {
            this.m_Socket.Send(buffer);
        }

        #endif
    }


    // js/native specific implementation
    public partial class WebSocket
    {
        #if PHOTON_WEBSOCKET_JS

        public const string Implementation = "JsLib";

        static Dictionary<int, WebSocket> instances = new Dictionary<int, WebSocket>();

        [DllImport("__Internal")]
        private static extern int SocketCreate(string url, string protocols, Action<int> openCallbackStatic,  Action<int, IntPtr, int> recvCallbackStatic, Action<int, int> errorCallbackStatic, Action<int, int> closeCallbackStatic);

        [DllImport("__Internal")]
        private static extern int SocketState (int socketInstance);

        [DllImport("__Internal")]
        private static extern void SocketSend (int socketInstance, byte[] ptr, int length);

        [DllImport("__Internal")]
        private static extern void SocketClose (int socketInstance);

        [DllImport("__Internal")]
        private static extern int SocketError (int socketInstance, byte[] ptr, int length);

        private int m_NativeRef = 0;

        // TODO: discuss if we need this anymore?!
        public bool ConnectedOLD
        {
            get { return SocketState(m_NativeRef) != 0; }
        }

        private const int SocketErrorBufferSize = 1024;
        private readonly byte[] socketErrorBuffer = new byte[SocketErrorBufferSize];

        // TODO: discuss if we need this anymore?!
        public string ErrorOLD
        {
            get {
                int result = SocketError (m_NativeRef, this.socketErrorBuffer, SocketErrorBufferSize);

                if (result == 0)
                    return null;

                return Encoding.UTF8.GetString (this.socketErrorBuffer);
            }
        }


        public void Connect()
        {
            m_NativeRef = SocketCreate (this.Url.ToString(), this.protocols, OpenCallbackStatic, RecvCallbackStatic, ErrorCallbackStatic, CloseCallbackStatic);
            instances[m_NativeRef] = this;
        }

        public void Close()
        {
            SocketClose(m_NativeRef);
        }


        public void Send(byte[] buffer)
        {
            SocketSend (m_NativeRef, buffer, buffer.Length);
        }


        [MonoPInvokeCallback(typeof(Action<int, IntPtr, int>))]
        public static void RecvCallbackStatic(int instance, IntPtr p, int len)
        {
            instances[instance].RecvCallbackInstance(p, len);
        }

        private byte[] receiveBuffer;

        public void RecvCallbackInstance(IntPtr p, int len)
        {
            if (this.receiveBuffer == null || this.receiveBuffer.Length < len)
            {
                this.receiveBuffer = new byte[len];
            }
            Marshal.Copy(p, this.receiveBuffer, 0, len);

            this.recvCallback(this.receiveBuffer, len);
        }



        [MonoPInvokeCallback(typeof(Action<int>))]
        public static void OpenCallbackStatic(int instance)
        {
            instances[instance].OpenCallbackInstance();
        }

        public void OpenCallbackInstance()
        {
            this.Connected = true;
            this.openCallback();
        }



        [MonoPInvokeCallback(typeof(Action<int, int>))]
        public static void ErrorCallbackStatic(int instance, int code)
        {
            string msg;
            switch (code)
            {
                case 1001:
                    msg = "Endpoint going away.";
                    break;
                case 1002:
                    msg = "Protocol error.";
                    break;
                case 1003:
                    msg = "Unsupported message.";
                    break;
                case 1005:
                    msg = "No status.";
                    break;
                case 1006:
                    msg = "Abnormal disconnection.";
                    break;
                case 1009:
                    msg = "Data frame too large.";
                    break;
                default:
                    msg = "Error " + code;
                    break;
            }

            instances[instance].ErrorCallbackInstance(code, msg);
        }

        public void ErrorCallbackInstance(int code, string msg)
        {
            this.Connected = false;
            this.errorCallback(code, msg);
        }


        [MonoPInvokeCallback(typeof(Action<int, int>))]
        public static void CloseCallbackStatic(int instance, int code)
        {
            string msg = "n/a from jslib";
            instances[instance].CloseCallbackInstance(code, msg);
        }

        public void CloseCallbackInstance(int code, string msg)
        {
            this.Connected = false;
            this.closeCallback(code, msg);
        }
        #endif
    }
}
#endif