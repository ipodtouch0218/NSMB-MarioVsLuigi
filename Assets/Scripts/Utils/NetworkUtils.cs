using ExitGames.Client.Photon;
using Photon.Realtime;

namespace NSMB.Utils {
    public static class NetworkUtils {

        public static RaiseEventOptions EventOthers { get; } = new() { Receivers = ReceiverGroup.Others };
        public static RaiseEventOptions EventAll { get; } = new() { Receivers = ReceiverGroup.All };
        public static RaiseEventOptions EventMasterClient { get; } = new() { Receivers = ReceiverGroup.MasterClient };

        private readonly static Hashtable _defaultRoomProperties = new() {
            [Enums.NetRoomProperties.Level] = 0,
            [Enums.NetRoomProperties.StarRequirement] = 10,
            [Enums.NetRoomProperties.CoinRequirement] = 8,
            [Enums.NetRoomProperties.Lives] = -1,
            [Enums.NetRoomProperties.Time] = -1,
            [Enums.NetRoomProperties.DrawTime] = false,
            [Enums.NetRoomProperties.NewPowerups] = true,
            [Enums.NetRoomProperties.GameStarted] = false,
            [Enums.NetRoomProperties.HostName] = "",
            [Enums.NetRoomProperties.Debug] = false,
            [Enums.NetRoomProperties.Mutes] = new string[0],
            [Enums.NetRoomProperties.Bans] = new object[0],
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
        Enums.NetRoomProperties.StarRequirement,
        Enums.NetRoomProperties.CoinRequirement,
        Enums.NetRoomProperties.Time,
        Enums.NetRoomProperties.NewPowerups,
        Enums.NetRoomProperties.GameStarted,
        Enums.NetRoomProperties.HostName,
    };

        public static readonly RegionComparer RegionPingComparer = new();
        public class RegionComparer : System.Collections.Generic.IComparer<Region> {
            public int Compare(Region r1, Region r2) {
                return r1.Ping - r2.Ping;
            }
        }
    }

    public class NameIdPair {
        public string name, userId;

        [System.Obsolete]
        public static object Deserialize(StreamBuffer inStream, short length) {
            byte[] buffer = new byte[length];
            inStream.Read(buffer, 0, length);

            string[] nameIdPair = ((string) Protocol.Deserialize(buffer)).Split(":");
            NameIdPair newPair = new() {
                name = nameIdPair[0],
                userId = nameIdPair[1],
            };
            return newPair;
        }

        [System.Obsolete]
        public static short Serialize(StreamBuffer outStream, object obj) {
            NameIdPair pair = (NameIdPair) obj;
            byte[] bytes = Protocol.Serialize(pair.name + ":" + pair.userId);
            outStream.Write(bytes, 0, bytes.Length);

            return (short) bytes.Length;
        }
    }
}