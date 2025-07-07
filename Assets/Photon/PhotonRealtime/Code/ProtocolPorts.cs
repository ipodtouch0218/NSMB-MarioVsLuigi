// -----------------------------------------------------------------------------
// <copyright company="Exit Games GmbH">
// Photon Realtime API - Copyright (C) 2022 Exit Games GmbH
// </copyright>
// <summary>Storage of port per server definition.</summary>
// <author>developer@photonengine.com</author>
// -----------------------------------------------------------------------------


namespace Photon.Realtime
{
    using System;
    using Photon.Client;


    /// <summary>Container for port definitions for the RealtimeClient. Ports that are non-zero can be used to override assigned ports coming from the server.</summary>
    public class ProtocolPorts
    {
        /// <summary>Set of UDP server ports.</summary>
        public ServerPorts Udp = new ServerPorts() { NameServer = 27000, MasterServer = 27001, GameServer = 27002 };
        /// <summary>Set of TCP server ports.</summary>
        public ServerPorts Tcp = new ServerPorts() { NameServer = 4533 };
        /// <summary>Set of WebSocket server ports.</summary>
        public ServerPorts Ws = new ServerPorts() { NameServer = 80 };
        /// <summary>Set of WebSocket Secure server ports.</summary>
        public ServerPorts Wss = new ServerPorts() { NameServer = 443 };

        /// <summary>Gets the port for the given protocol and server type.</summary>
        public ushort Get(ConnectionProtocol protocol, ServerConnection serverType)
        {
            switch (protocol)
            {
                case ConnectionProtocol.Udp:
                    return this.Udp.Get(serverType);
                case ConnectionProtocol.Tcp:
                    return this.Tcp.Get(serverType);
                case ConnectionProtocol.WebSocket:
                    return this.Ws.Get(serverType);
                case ConnectionProtocol.WebSocketSecure:
                    return this.Wss.Get(serverType);
            }

            throw new ArgumentOutOfRangeException(nameof(protocol), protocol, null);
        }

        /// <summary>Configure this instance with default UDP server ports: 27000-27002.</summary>
        public void SetUdpDefault()
        {
            this.Udp = new ServerPorts() { NameServer = 27000, MasterServer = 27001, GameServer = 27002 };
        }

        /// <summary>Configure this instance with old UDP server ports: 5055-5058.</summary>
        public void SetUdpDefaultOld()
        {
            this.Udp = new ServerPorts() { NameServer = 5058, MasterServer = 5055, GameServer = 5056 };
        }

        /// <summary>Configure this instance with default TCP server ports: 4530-4533. Note: there is just one port range for TCP.</summary>
        public void SetTcpDefault()
        {
            this.Tcp = new ServerPorts() { NameServer = 4533, MasterServer = 4530, GameServer = 4531 };
        }

        /// <summary>Configure this instance with default WS server ports: 80.</summary>
        public void SetWsDefault()
        {
            this.Ws = new ServerPorts() { NameServer = 80, MasterServer = 80, GameServer = 80 };
        }

        /// <summary>Configure this instance with old Photon WS server ports: 9090-9093.</summary>
        public void SetWsDefaultOld()
        {
            this.Ws = new ServerPorts() { NameServer = 9093, MasterServer = 9090, GameServer = 9091 };
        }

        /// <summary>Configure this instance with default WSS server ports: 443.</summary>
        public void SetWssDefault()
        {
            this.Wss = new ServerPorts() { NameServer = 433, MasterServer = 443, GameServer = 443 };
        }

        /// <summary>Configure this instance with old Photon WSS server ports: 19090-19093.</summary>
        public void SetWssDefaultOld()
        {
            this.Wss = new ServerPorts() { NameServer = 19093, MasterServer = 19090, GameServer = 19091 };
        }

        /// <summary>Configure this instance with old Photon server ports for all protocols.</summary>
        public void SetOldDefaults()
        {
            this.SetUdpDefaultOld();
            this.SetTcpDefault();
            this.SetWsDefaultOld();
            this.SetWssDefaultOld();
        }
    }

    /// <summary>Struct to keep port-values per server.</summary>
    public struct ServerPorts
    {
        /// <summary>Typical ports: UDP: 5058 or 27000, TCP: 4533, WSS: 19093 or 443.</summary>
        public ushort NameServer;

        /// <summary>Typical ports: UDP: 5056 or 27002, TCP: 4530, WSS: 19090 or 443.</summary>
        public ushort MasterServer;

        /// <summary>Typical ports: UDP: 5055 or 27001, TCP: 4531, WSS: 19091 or 443.</summary>
        public ushort GameServer;

        /// <summary>Gets the stored port for a specific ServerConnection / type.</summary>
        /// <param name="serverType">Server type.</param>
        /// <returns>Port to user or zero as default (do not override with 0).</returns>
        /// <exception cref="ArgumentOutOfRangeException">If the server type is unknown.</exception>
        public ushort Get(ServerConnection serverType)
        {
            switch (serverType)
            {
                case ServerConnection.NameServer:
                    return this.NameServer;
                case ServerConnection.MasterServer:
                    return this.MasterServer;
                case ServerConnection.GameServer:
                    return this.GameServer;
            }
            throw new ArgumentOutOfRangeException(nameof(serverType), serverType, null);
        }
    }
}