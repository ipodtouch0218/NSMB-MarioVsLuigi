using System;
using System.Collections.Generic;

using Fusion;
using Fusion.Sockets;

namespace NSMB.Utils {
    public static class NetworkUtils {

        public static Dictionary<Enum, string> disconnectMessages = new() {

            [ShutdownReason.MaxCcuReached] = "Max player count reached in this region (100 players). Try switching regions.",
            [ShutdownReason.CustomAuthenticationFailed] = "Failed to authenticate. Either the authentication server is restarting / down, or you are not connected to the internet.",
            [ShutdownReason.DisconnectedByPluginLogic] = "The server did not respond (timeout). Please try again.",
            [ShutdownReason.PhotonCloudTimeout] = "The server did not respond (timeout). Please try again.",
            [ShutdownReason.ConnectionTimeout] = "The server did not respond (timeout). Please try again.",
            [ShutdownReason.Error] = "Unknown error. Please try again.",
            [ShutdownReason.GameClosed] = "The room you attempted to join was closed.",
            [ShutdownReason.GameNotFound] = "The room you attempted to join does not exist.",
            [ShutdownReason.GameIsFull] = "The room you attempted to join is currently full.",

            [NetConnectFailedReason.Timeout] = "Failed to connect to the room: No response from the host (timeout).",
            [NetConnectFailedReason.ServerFull] = "Failed to connect to the room: The room is full.",
            [NetConnectFailedReason.ServerRefused] = "Failed to connect to the room: Either the room is full, you were banned, or a connection could not be made.",
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
