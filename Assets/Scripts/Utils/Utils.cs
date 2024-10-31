using Photon.Realtime;
using Quantum;
using System;
using System.Collections.Generic;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Text;
using UnityEngine;

namespace NSMB.Utils {
    public class Utils {

        //---Buffers
        private static readonly List<Vector2> PhysicsShapeBuffer = new(8);
        private static readonly Vector2[] BoxPointsBuffer = new Vector2[4];

        public static bool BitTest(long v, int index) {
            return (v & (1L << index)) != 0;
        }

        public static bool BitTest(ulong v, int index) {
            return (v & (1UL << index)) != 0;
        }

        public static void BitSet(ref byte v, int index, bool value) {
            if (value) {
                v |= (byte) (1 << index);
            } else {
                v &= (byte) ~(1 << index);
            }
        }

        public static void BitSet(ref int v, int index, bool value) {
            if (value) {
                v |= (1 << index);
            } else {
                v &= ~(1 << index);
            }
        }

        public static void BitSet(ref uint v, int index, bool value) {
            if (value) {
                v |= (1U << index);
            } else {
                v &= ~(1U << index);
            }
        }

        public static void BitSet(ref ulong v, int index, bool value) {
            if (value) {
                v |= (1UL << index);
            } else {
                v &= ~(1UL << index);
            }
        }

        public static string SecondsToMinuteSeconds(int number) {
            return (number / 60) + ":" + (number % 60).ToString("00");
        }

        public static float QuadraticEaseOut(float v) {
            return -1 * v * (v - 2);
        }

        private static readonly Dictionary<char, string> uiSymbols = new() {
            ['0'] = "hudnumber_0",
            ['1'] = "hudnumber_1",
            ['2'] = "hudnumber_2",
            ['3'] = "hudnumber_3",
            ['4'] = "hudnumber_4",
            ['5'] = "hudnumber_5",
            ['6'] = "hudnumber_6",
            ['7'] = "hudnumber_7",
            ['8'] = "hudnumber_8",
            ['9'] = "hudnumber_9",
            ['x'] = "hudnumber_x",
            ['C'] = "hudnumber_coin",
            ['S'] = "hudnumber_star",
            ['T'] = "hudnumber_timer",
            ['/'] = "hudnumber_slash",
            [':'] = "hudnumber_colon",
        };
        public static readonly Dictionary<char, string> numberSymbols = new() {
            ['0'] = "coinnumber_0",
            ['1'] = "coinnumber_1",
            ['2'] = "coinnumber_2",
            ['3'] = "coinnumber_3",
            ['4'] = "coinnumber_4",
            ['5'] = "coinnumber_5",
            ['6'] = "coinnumber_6",
            ['7'] = "coinnumber_7",
            ['8'] = "coinnumber_8",
            ['9'] = "coinnumber_9",
        };
        public static readonly Dictionary<char, string> smallSymbols = new() {
            ['0'] = "room_smallnumber_0",
            ['1'] = "room_smallnumber_1",
            ['2'] = "room_smallnumber_2",
            ['3'] = "room_smallnumber_3",
            ['4'] = "room_smallnumber_4",
            ['5'] = "room_smallnumber_5",
            ['6'] = "room_smallnumber_6",
            ['7'] = "room_smallnumber_7",
            ['8'] = "room_smallnumber_8",
            ['9'] = "room_smallnumber_9",
        };
        public static string GetSymbolString(string str, Dictionary<char, string> dict = null) {
            dict ??= uiSymbols;

            StringBuilder ret = new();
            foreach (char c in str) {
                if (dict.TryGetValue(c, out string name)) {
                    ret.Append("<sprite name=").Append(name).Append(">");
                } else {
                    ret.Append(c);
                }
            }
            return ret.ToString();
        }

        private static readonly Color spectatorColor = new(0.9f, 0.9f, 0.9f, 0.7f);
        public unsafe static Color GetPlayerColor(Frame f, PlayerRef player, float s = 1, float v = 1) {
            if (f == null) {
                return spectatorColor;
            }

            // Prioritize spectator status
            if (!f.TryResolveDictionary(f.Global->PlayerDatas, out var playerDataDict)
                || !playerDataDict.TryGetValue(player, out EntityRef playerDataEntity)
                || !f.TryGet(playerDataEntity, out PlayerData playerData)
                || playerData.IsSpectator) {

                return spectatorColor;
            }

            // Or dead marios
            if (f.Global->GameState > GameState.WaitingForPlayers) {
                var marioFilter = f.Filter<MarioPlayer>();
                bool hasMario = false;
                while (marioFilter.NextUnsafe(out _, out MarioPlayer* mario)) {
                    if (mario->PlayerRef == player) {
                        hasMario = true;
                        break;
                    }
                }

                if (!hasMario) {
                    return spectatorColor;
                }
            }

            // Then team
            if (f.Global->Rules.TeamsEnabled) {
                return GetTeamColor(f, playerData.Team, s, v);
            }

            // Then id based color
            PlayerData* ourPlayerData = QuantumUtils.GetPlayerData(f, player);
            int ourIndex = 0;
            int totalPlayers = 0;

            var playerFilter = f.Filter<PlayerData>();
            while (playerFilter.NextUnsafe(out _, out PlayerData* otherPlayerData)) {
                if (otherPlayerData->IsSpectator) {
                    continue;
                }

                totalPlayers++;
                if (otherPlayerData->JoinTick < ourPlayerData->JoinTick) {
                    ourIndex++;
                }
            }

            return Color.HSVToRGB(ourIndex / (totalPlayers + 1f), s, v);
        }

        public static Color GetTeamColor(Frame f, int team, float s = 1, float v = 1) {
            var teams = f.SimulationConfig.Teams;
            if (team < 0 || team >= teams.Length) {
                return spectatorColor;
            }

            Color color = teams[team].color;
            Color.RGBToHSV(color, out float hue, out float saturation, out float value);
            return Color.HSVToRGB(hue, saturation * s, value * v);
        }

        public static bool IsMarioLocal(EntityRef entity) {
            return PlayerElements.AllPlayerElements.Any(pe => pe.Entity == entity);
        }

        public static string GetPingSymbol(int ping) {
            string pingSymbol;
            if (ping < 0) {
                pingSymbol = "<sprite name=connection_disconnected>";
            } else if (ping == 0) {
                pingSymbol = "<sprite name=connection_host>";
            } else if (ping < 60) {
                pingSymbol = "<sprite name=connection_great>";
            } else if (ping < 110) {
                pingSymbol = "<sprite name=connection_good>";
            } else if (ping < 150) {
                pingSymbol = "<sprite name=connection_fair>";
            } else {
                pingSymbol = "<sprite name=connection_bad>";
            }
            return pingSymbol;
        }

        public static string BytesToString(long byteCount) {
            string[] suf = { "B", "KB", "MB", "GB", "TB", "PB", "EB" }; // Longs run out around EB
            if (byteCount == 0) {
                return "0" + suf[0];
            }

            long bytes = Math.Abs(byteCount);
            int place = Convert.ToInt32(Math.Floor(Math.Log(bytes, 1024)));
            double num = Math.Round(bytes / Math.Pow(1024, place), 1);
            return (Math.Sign(byteCount) * num).ToString() + suf[place];
        }

        public static Color GetRainbowColor() {
            // Four seconds per revolution
            double time = (Time.timeAsDouble * 0.25d) % 1d;
            return GlobalController.Instance.rainbowGradient.Evaluate((float) time);
        }

        public static int ParseTimeToSeconds(string time) {
            int minutes;
            int seconds;

            if (time.Contains(":")) {
                string[] split = time.Split(":");
                int.TryParse(split[0], out minutes);
                int.TryParse(split[1], out seconds);
            } else {
                minutes = 0;
                int.TryParse(time, out seconds);
            }

            if (seconds >= 60) {
                minutes += seconds / 60;
                seconds %= 60;
            }

            seconds = minutes * 60 + seconds;

            return seconds;
        }

        public static bool BufferContains<T>(T[] buffer, int bufferLength, T element) {
            for (int i = 0; i < bufferLength; i++) {
                if (element.Equals(buffer[i])) {
                    return true;
                }
            }
            return false;
        }

        public static void IntersectWithBuffer<T>(IList<T> collection, T[] buffer, int bufferLength) {
            for (int i = collection.Count - 1; i >= 0; i--) {
                if (!BufferContains(buffer, bufferLength, collection[i])) {
                    collection.RemoveAt(i);
                }
            }
        }
    }
}
