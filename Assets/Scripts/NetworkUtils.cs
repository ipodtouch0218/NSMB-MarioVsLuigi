using UnityEngine;
using ExitGames.Client.Photon;
using Photon.Realtime;

public static class NetworkUtils {

    public static readonly RaiseEventOptions EventOthers = new() { Receivers = ReceiverGroup.Others };
    public static readonly RaiseEventOptions EventAll = new() { Receivers = ReceiverGroup.All };

    private readonly static Hashtable _defaultRoomProperties = new() {
        [Enums.NetRoomProperties.Level] = 0,
        [Enums.NetRoomProperties.StarRequirement] = 10,
        [Enums.NetRoomProperties.Lives] = -1,
        [Enums.NetRoomProperties.Time] = -1,
        [Enums.NetRoomProperties.NewPowerups] = true,
        [Enums.NetRoomProperties.GameStarted] = false,
        [Enums.NetRoomProperties.HostName] = "",
        [Enums.NetRoomProperties.Debug] = false,
        //[Enums.NetRoomProperties.Password] = "",
    };

    public static Hashtable DefaultRoomProperties {
        get {
            Hashtable ret = new();
            ret.Merge(_defaultRoomProperties);
            return ret;
        }
        private set { }
    }

    public static readonly string[] LobbyVisibleRoomProperties = new string[] {
        Enums.NetRoomProperties.Lives,
        Enums.NetRoomProperties.Time,
        Enums.NetRoomProperties.NewPowerups,
        Enums.NetRoomProperties.GameStarted,
        Enums.NetRoomProperties.HostName,
        //Enums.NetRoomProperties.Password,
    };

    public static readonly RegionComparer RegionPingComparer = new();
    public class RegionComparer : System.Collections.Generic.IComparer<Region> {
        public int Compare(Region r1, Region r2) {
            return r1.Ping - r2.Ping;
        }
    }
}