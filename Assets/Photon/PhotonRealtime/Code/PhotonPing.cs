// ----------------------------------------------------------------------------
// <copyright file="PhotonPing.cs" company="Exit Games GmbH">
// Photon Realtime API - Copyright (C) 2022 Exit Games GmbH
// </copyright>
// <summary>
// This file includes various PhotonPing implementations for different APIs,
// platforms and protocols.
// The RegionPinger class is the instance which selects the Ping implementation
// to use.
// </summary>
// <author>developer@exitgames.com</author>
// ----------------------------------------------------------------------------

#if UNITY_2017_4_OR_NEWER
#define SUPPORTED_UNITY
#endif


namespace Photon.Realtime
{
    using System;
    using System.Collections;
    using System.Threading;

    #if !NO_SOCKET
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Net.Sockets;
    #endif

    #if UNITY_WEBGL
    // import UnityWebRequest
    using UnityEngine.Networking;
    #endif

    /// <summary>
    /// Abstract implementation of PhotonPing, ase for pinging servers to find the "Best Region".
    /// </summary>
    public abstract class PhotonPing : IDisposable
    {
        /// <summary>Caches the last exception/error message, if any.</summary>
        public string DebugString = "";

        /// <summary>True of the ping was successful.</summary>
        public bool Successful;

        /// <summary>True if there was any result.</summary>
        protected internal bool GotResult;

        /// <summary>Length of a ping.</summary>
        protected internal int PingLength = 13;

        /// <summary>Bytes to send in a (Photon UDP) ping.</summary>
        protected internal byte[] PingBytes = new byte[] { 0x7d, 0x7d, 0x7d, 0x7d, 0x7d, 0x7d, 0x7d, 0x7d, 0x7d, 0x7d, 0x7d, 0x7d, 0x00 };

        /// <summary>Randomized number to identify a ping.</summary>
        protected internal byte PingId;

        private static readonly System.Random RandomIdProvider = new System.Random();

        /// <summary>Begins sending a ping.</summary>
        public abstract bool StartPing(string ip);

        /// <summary>Check if done.</summary>
        public abstract bool Done();

        /// <summary>Dispose of this ping.</summary>
        public abstract void Dispose();

        /// <summary>Initialize this ping (GotResult, Successful, PingId).</summary>
        protected internal void Init()
        {
            this.GotResult = false;
            this.Successful = false;
            this.PingId = (byte)(RandomIdProvider.Next(255));
        }
    }


    #if !NO_SOCKET
    /// <summary>Uses C# Socket class from System.Net.Sockets (as Unity usually does).</summary>
    /// <remarks>Incompatible with Windows 8 Store/Phone API.</remarks>
    public class PingMono : PhotonPing
    {
        private Socket sock;

        /// <summary>
        /// Sends a "Photon Ping" to a server.
        /// </summary>
        /// <param name="ip">Address in IPv4 or IPv6 format. An address containing a '.' will be interpreted as IPv4.</param>
        /// <returns>True if the Photon Ping could be sent.</returns>
        public override bool StartPing(string ip)
        {
            this.Init();

            try
            {
                if (this.sock == null)
                {
                    if (ip.Contains("."))
                    {
                        this.sock = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                    }
                    else
                    {
                        this.sock = new Socket(AddressFamily.InterNetworkV6, SocketType.Dgram, ProtocolType.Udp);
                    }

                    this.sock.ReceiveTimeout = 5000;
                    this.sock.Connect(ip, RegionHandler.UdpPortToPing);
                }


                this.PingBytes[this.PingBytes.Length - 1] = this.PingId;
                this.sock.Send(this.PingBytes);
                this.PingBytes[this.PingBytes.Length - 1] = (byte)(this.PingId+1);  // this buffer is re-used for the result/receive. invalidate the result now.
            }
            catch (Exception e)
            {
                this.sock = null;
                System.Diagnostics.Debug.WriteLine(e.ToString());

                // bubble up
                throw;
            }

            return false;
        }

        /// <summary>Check if done.</summary>
        public override bool Done()
        {
            if (this.GotResult || this.sock == null)
            {
                return true;    // this just indicates the ping is no longer waiting. this.Successful value defines if the roundtrip completed
            }

            int read = 0;
            try
            {
                if (!this.sock.Poll(0, SelectMode.SelectRead))
                {
                    return false;
                }

                read = this.sock.Receive(this.PingBytes, SocketFlags.None);
            }
            catch (Exception ex)
            {
                if (this.sock != null)
                {
                    this.sock.Close();
                    this.sock = null;
                }
                this.DebugString += " Exception of socket! " + ex.GetType() + " ";
                return true;    // this just indicates the ping is no longer waiting. this.Successful value defines if the roundtrip completed
            }

            bool replyMatch = this.PingBytes[this.PingBytes.Length - 1] == this.PingId && read == this.PingLength;
            if (!replyMatch)
            {
                this.DebugString += " ReplyMatch is false! ";
            }


            this.Successful = replyMatch;
            this.GotResult = true;
            return true;
        }

        /// <summary>Dispose of this ping.</summary>
        public override void Dispose()
        {
            if (this.sock == null) { return; }

            try
            {
                this.sock.Close();
            }
            catch
            {
            }

            this.sock = null;
        }

    }
    #endif


    #if NATIVE_SOCKETS
    /// <summary>Abstract base class to provide proper resource management for the below native ping implementations</summary>
    public abstract class PingNative : PhotonPing
    {
        // Native socket states - according to EnetConnect.h state definitions
        protected enum NativeSocketState : byte
        {
            Disconnected = 0,
            Connecting = 1,
            Connected = 2,
            ConnectionError = 3,
            SendError = 4,
            ReceiveError = 5,
            Disconnecting = 6
        }

        protected IntPtr pConnectionHandler = IntPtr.Zero;

        ~PingNative()
        {
            Dispose();
        }
    }

    /// <summary>Uses dynamic linked native Photon socket library via DllImport("PhotonSocketPlugin") attribute (as done by Unity Android and Unity PS3).</summary>
    public class PingNativeDynamic : PingNative
    {
        public override bool StartPing(string ip)
        {
            lock (SocketUdpNativeDynamic.syncer)
            {
                base.Init();

                if(pConnectionHandler == IntPtr.Zero)
                {
                    pConnectionHandler = SocketUdpNativeDynamic.egconnect(ip);
                    SocketUdpNativeDynamic.egservice(pConnectionHandler);
                    byte state = SocketUdpNativeDynamic.eggetState(pConnectionHandler);
                    while (state == (byte) NativeSocketState.Connecting)
                    {
                        SocketUdpNativeDynamic.egservice(pConnectionHandler);
                        state = SocketUdpNativeDynamic.eggetState(pConnectionHandler);
                    }
                }

                PingBytes[PingBytes.Length - 1] = PingId;
                SocketUdpNativeDynamic.egsend(pConnectionHandler, PingBytes, PingBytes.Length);
                SocketUdpNativeDynamic.egservice(pConnectionHandler);

                PingBytes[PingBytes.Length - 1] = (byte) (PingId - 1);
                return true;
            }
        }

        public override bool Done()
        {
            lock (SocketUdpNativeDynamic.syncer)
            {
                if (this.GotResult || pConnectionHandler == IntPtr.Zero)
                {
                    return true;
                }

                int available = SocketUdpNativeDynamic.egservice(pConnectionHandler);
                if (available < PingLength)
                {
                    return false;
                }

                int pingBytesLength = PingBytes.Length;
                int bytesInRemainginDatagrams = SocketUdpNativeDynamic.egread(pConnectionHandler, PingBytes, ref pingBytesLength);
                this.Successful = (PingBytes != null && PingBytes[PingBytes.Length - 1] == PingId);
                //Debug.Log("Successful: " + this.Successful + " bytesInRemainginDatagrams: " + bytesInRemainginDatagrams + " PingId: " + PingId);

                this.GotResult = true;
                return true;
            }
        }

        public override void Dispose()
        {
            lock (SocketUdpNativeDynamic.syncer)
            {
                if (this.pConnectionHandler != IntPtr.Zero)
                    SocketUdpNativeDynamic.egdisconnect(this.pConnectionHandler);
                this.pConnectionHandler = IntPtr.Zero;
            }
            GC.SuppressFinalize(this);
        }
    }

    #if NATIVE_SOCKETS && NATIVE_SOCKETS_STATIC
    /// <summary>Uses static linked native Photon socket library via DllImport("__Internal") attribute (as done by Unity iOS and Unity Switch).</summary>
    public class PingNativeStatic : PingNative
    {
        public override bool StartPing(string ip)
        {
            base.Init();

            lock (SocketUdpNativeStatic.syncer)
            {
                if(pConnectionHandler == IntPtr.Zero)
                {
                    pConnectionHandler = SocketUdpNativeStatic.egconnect(ip);
                    SocketUdpNativeStatic.egservice(pConnectionHandler);
                    byte state = SocketUdpNativeStatic.eggetState(pConnectionHandler);
                    while (state == (byte) NativeSocketState.Connecting)
                    {
                        SocketUdpNativeStatic.egservice(pConnectionHandler);
                        state = SocketUdpNativeStatic.eggetState(pConnectionHandler);
                        Thread.Sleep(0); // suspending execution for a moment is critical on Switch for the OS to update the socket
                    }
                }

                PingBytes[PingBytes.Length - 1] = PingId;
                SocketUdpNativeStatic.egsend(pConnectionHandler, PingBytes, PingBytes.Length);
                SocketUdpNativeStatic.egservice(pConnectionHandler);

                PingBytes[PingBytes.Length - 1] = (byte) (PingId - 1);
                return true;
            }
        }

        public override bool Done()
        {
            lock (SocketUdpNativeStatic.syncer)
            {
                if (this.GotResult || pConnectionHandler == IntPtr.Zero)
                {
                    return true;
                }

                int available = SocketUdpNativeStatic.egservice(pConnectionHandler);
                if (available < PingLength)
                {
                    return false;
                }

                int pingBytesLength = PingBytes.Length;
                int bytesInRemainginDatagrams = SocketUdpNativeStatic.egread(pConnectionHandler, PingBytes, ref pingBytesLength);
                this.Successful = (PingBytes != null && PingBytes[PingBytes.Length - 1] == PingId);
                //Debug.Log("Successful: " + this.Successful + " bytesInRemainginDatagrams: " + bytesInRemainginDatagrams + " PingId: " + PingId);

                this.GotResult = true;
                return true;
            }
        }

        public override void Dispose()
        {
            lock (SocketUdpNativeStatic.syncer)
            {
                if (pConnectionHandler != IntPtr.Zero)
                    SocketUdpNativeStatic.egdisconnect(pConnectionHandler);
                pConnectionHandler = IntPtr.Zero;
            }
            GC.SuppressFinalize(this);
        }
    }
    #endif
    #endif


    #if UNITY_WEBGL
    public class PingHttp : PhotonPing
    {
        private UnityWebRequest webRequest;

        public override bool StartPing(string address)
        {
            base.Init();

            // to work around an issue with UnityWebRequest in Editor (2021 at least), use http to ping in-Editor
            string scheme = UnityEngine.Application.isEditor ? "http://" : "https://";
            address = $"{scheme}{address}/photon/m/?ping&r={UnityEngine.Random.Range(0, 10000)}";

            this.webRequest = UnityWebRequest.Get(address);
            this.webRequest.SendWebRequest();
            return true;
        }

        public override bool Done()
        {
            if (this.webRequest.isDone)
            {
                Successful = true;
                return true;
            }

            return false;
        }

        public override void Dispose()
        {
            this.webRequest.Dispose();
        }
    }
    #endif
}