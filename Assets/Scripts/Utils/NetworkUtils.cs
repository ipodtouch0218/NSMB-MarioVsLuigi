using System;
using System.Collections.Generic;

using Fusion;
using Fusion.Sockets;

namespace NSMB.Utils {
    public static class NetworkUtils {

        public static Dictionary<Enum, string> disconnectMessages = new() {

            [ShutdownReason.MaxCcuReached] = "Max player count reached in this region (100 players MAX)",
            [ShutdownReason.CustomAuthenticationFailed] = "Failed to authenticate with the auth server",
            [ShutdownReason.DisconnectedByPluginLogic] = "Disconnected: Server Timeout",
            //[ShutdownReason.Error] = "Disconnected: "
            //[ShutdownReason.]
            //[DisconnectCause.DisconnectByServerLogic] = "",

            [NetConnectFailedReason.Timeout] = "Failed to connect: Server Timeout",
            [NetConnectFailedReason.ServerFull] = "Failed to connect: Server is Full",
            [NetConnectFailedReason.ServerRefused] = "Failed to connect: You are banned",
        };

        public static Dictionary<string, SessionProperty> DefaultRoomProperties => new() {
            [Enums.NetRoomProperties.Level] = 0,
            [Enums.NetRoomProperties.StarRequirement] = 10,
            [Enums.NetRoomProperties.CoinRequirement] = 8,
            [Enums.NetRoomProperties.Lives] = -1,
            [Enums.NetRoomProperties.Time] = -1,
            [Enums.NetRoomProperties.CustomPowerups] = 1,
            [Enums.NetRoomProperties.GameStarted] = 0,
            [Enums.NetRoomProperties.Teams] = 0,
            [Enums.NetRoomProperties.MaxPlayers] = 10,
            [Enums.NetRoomProperties.HostName] = "noname",
        };
    }
}
