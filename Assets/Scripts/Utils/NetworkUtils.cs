using System.Collections.Generic;
using System.Linq;

using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;

namespace NSMB.Utils {
    public static class NetworkUtils {

        public static WebFlags forward = new(WebFlags.HttpForwardConst);

        public static Dictionary<DisconnectCause, string> disconnectMessages = new() {

            [DisconnectCause.MaxCcuReached] = "Max player count reached in this region (100 players MAX)",
            [DisconnectCause.CustomAuthenticationFailed] = "Failed to authenticate with the auth server",
            //[DisconnectCause.DisconnectByServerLogic] = "",

        };

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

        public static readonly RegionPingComparer PingComparer = new();
        public class RegionPingComparer : IComparer<Region> {
            public int Compare(Region r1, Region r2) {
                return r1.Ping - r2.Ping;
            }
        }

        public static readonly RegionNameComparer NameComparer = new();
        public class RegionNameComparer : IComparer<Region> {
            public int Compare(Region r1, Region r2) {
                return r1.Code.CompareTo(r2.Code);
            }
        }


        public static readonly PlayerIdComparer PlayerComparer = new();
        public class PlayerIdComparer : IComparer<KeyValuePair<int, Player>> {
            public int Compare(KeyValuePair<int, Player> r1, KeyValuePair<int, Player> r2) {
                return r1.Key - r2.Key;
            }
        }

        public static bool IsSpectator(this Player player) {
            bool valid = Utils.GetCustomProperty(Enums.NetPlayerProperties.Spectator, out bool value, player.CustomProperties);
            return valid && value;
        }

        public static readonly Dictionary<string, string> nicknameCache = new();

        public static string GetUniqueNickname(this Player player, bool checkCache = true) {
            if (checkCache && nicknameCache.ContainsKey(player.UserId ?? "none"))
                return nicknameCache[player.UserId ?? "none"];

            //generate valid username
            string nickname = player.NickName.ToValidUsername(false);

            //nickname duplicates
            List<KeyValuePair<int, Player>> players = PhotonNetwork.CurrentRoom.Players.ToList();
            players.Sort(PlayerComparer);

            int count = 0;
            foreach ((int id, Player pl) in players) {
                if (pl == player)
                    break;

                if (nickname == GetUniqueNickname(pl))
                    count++;
            }
            if (count > 0)
                nickname += $"({count})";

            //update cache
            nicknameCache[player.UserId ?? "none"] = nickname;

            return nickname;
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