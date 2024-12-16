using Photon.Client;
using Photon.Realtime;
using System.Collections.Generic;

namespace NSMB.Utils {
    public static class NetworkUtils {

        public static PhotonHashtable DefaultRoomProperties => new() {
            [Enums.NetRoomProperties.IntProperties] = (int) IntegerProperties.Default,
            [Enums.NetRoomProperties.BoolProperties] = (int) BooleanProperties.Default,
            [Enums.NetRoomProperties.StageGuid] = GlobalController.Instance.config.DefaultRules.Level.Id.ToString(),
        };

        public static Dictionary<short, string> RealtimeErrorCodes = new() {
            [ErrorCode.CustomAuthenticationFailed] = "ui.error.authentication",
            [ErrorCode.MaxCcuReached] = "ui.error.ccu",

            [ErrorCode.GameDoesNotExist] = "ui.error.join.notfound",
            [ErrorCode.GameClosed] = "ui.error.join.closed",
            [ErrorCode.GameFull] = "ui.error.join.full",
            [ErrorCode.JoinFailedFoundActiveJoiner] = "ui.error.join.alreadyingame",
        };

        public static Dictionary<DisconnectCause, string> RealtimeDisconnectCauses = new() {
            [DisconnectCause.CustomAuthenticationFailed] = "ui.error.authentication",
            [DisconnectCause.MaxCcuReached] = "ui.error.ccu",
            [DisconnectCause.DnsExceptionOnConnect] = "ui.error.connection",
            [DisconnectCause.ExceptionOnConnect] = "ui.error.connection",
            [DisconnectCause.ServerTimeout] = "ui.error.timeout",
            [DisconnectCause.ClientTimeout] = "ui.error.timeout",
            [DisconnectCause.Exception] = "ui.error.unknown",
            [DisconnectCause.DisconnectByServerLogic] = "ui.error.plugin",
        };

        public struct IntegerProperties {
            public static readonly IntegerProperties Default = new() {
                CoinRequirement = 8,
                StarRequirement = 10
            };

            // Level :: Value ranges from 0-63: 6 bits
            // Timer :: Value ranges from 0-99: 7 bits
            // Lives :: Value ranges from 1-25: 5 bits
            // CoinRequirement :: Value ranges from 3-25: 5 bits
            // StarRequirement :: Value ranges from 1-25: 5 bits
            // MaxPlayers :: Value ranges from 1-10: 4 bits

            // 31....26   25.....19   18...14   13...9   8...4   3..0
            // Level      Timer       Lives     Coins    Stars   Unused
            public int Level, Timer, Lives, CoinRequirement, StarRequirement;

            public static implicit operator int(IntegerProperties props) {
                int value = 0;

                value |= (props.Level & 0b111111) << 26;
                value |= (props.Timer & 0b1111111) << 19;
                value |= (props.Lives & 0b11111) << 14;
                value |= (props.CoinRequirement & 0b11111) << 9;
                value |= (props.StarRequirement & 0b11111) << 4;
                // value |= (props.MaxPlayers & 0b1111) << 0;

                return value;
            }

            public static implicit operator IntegerProperties(int bits) {
                IntegerProperties ret = new() {
                    Level = (bits >> 26) & 0b111111,
                    Timer = (bits >> 19) & 0b1111111,
                    Lives = (bits >> 14) & 0b11111,
                    CoinRequirement = (bits >> 9) & 0b11111,
                    StarRequirement = (bits >> 4) & 0b11111,
                    // MaxPlayers = (bits >> 0) & 0b1111,
                };
                return ret;
            }
        };

        public struct BooleanProperties {
            public static readonly BooleanProperties Default = new() {
                CustomPowerups = true
            };

            public bool CustomPowerups, Teams, DrawOnTimeUp, GameStarted;

            public static implicit operator int(BooleanProperties props) {
                int value = 0;

                Utils.BitSet(ref value, 0, props.CustomPowerups);
                Utils.BitSet(ref value, 1, props.Teams);
                Utils.BitSet(ref value, 2, props.DrawOnTimeUp);
                Utils.BitSet(ref value, 3, props.GameStarted);

                return value;
            }

            public static implicit operator BooleanProperties(int bits) {
                return new() {
                    CustomPowerups = Utils.BitTest(bits, 0),
                    Teams = Utils.BitTest(bits, 1),
                    DrawOnTimeUp = Utils.BitTest(bits, 2),
                    GameStarted = Utils.BitTest(bits, 3),
                };
            }
        };

        public static bool GetCustomProperty<T>(PhotonHashtable table, string key, out T value) {
            if (table.TryGetValue(key, out object objValue)) {
                value = (T) objValue;
                return true;
            }
            value = default;
            return false;
        }

        public static bool GetCustomProperty(PhotonHashtable table, string key, out bool value) {
            if (table.TryGetValue(key, out object objValue)) {
                value = (int) objValue == 1;
                return true;
            }
            value = default;
            return false;
        }
    }
}
