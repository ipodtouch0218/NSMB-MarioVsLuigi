using System;
using System.Collections.Generic;

using Fusion;
using Fusion.Sockets;

namespace NSMB.Utils {
    public static class NetworkUtils {

        public static Dictionary<Enum, string> disconnectMessages = new() {

            [ShutdownReason.MaxCcuReached] = "ui.error.ccu",
            [ShutdownReason.CustomAuthenticationFailed] = "ui.error.authentication",
            [ShutdownReason.DisconnectedByPluginLogic] = "ui.error.hosttimeout",
            [ShutdownReason.PhotonCloudTimeout] = "ui.error.photontimeout",
            [ShutdownReason.ConnectionTimeout] = "ui.error.hosttimeout",
            [ShutdownReason.Error] = "ui.error.unknown",
            [ShutdownReason.GameClosed] = "ui.error.joinclosed",
            [ShutdownReason.GameNotFound] = "ui.error.joinnotfound",
            [ShutdownReason.GameIsFull] = "ui.error.joinfull",

            [NetConnectFailedReason.Timeout] = "ui.error.jointimeout",
            [NetConnectFailedReason.ServerFull] = "ui.error.joinfull",
            [NetConnectFailedReason.ServerRefused] = "ui.error.joinrefused",
        };

        public static Dictionary<string, SessionProperty> DefaultRoomProperties = new() {
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
