using NSMB.UI.Game;
using Quantum;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace NSMB.Utils {
    public class Utils {

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
            StringBuilder builder = new StringBuilder();
            builder.Append(number / 60).Append(':').AppendFormat((number % 60).ToString("00"));
            return builder.ToString();
        }

        public static float QuadraticEaseOut(float v) {
            return -1 * v * (v - 2);
        }

        public static float EaseInOut(float x) {
            return x < 0.5f ? 2 * x * x : 1 - ((-2 * x + 2) * (-2 * x + 2) / 2);
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

        private static StringBuilder symbolStringBuilder = new();
        public static string GetSymbolString(ReadOnlySpan<char> str, Dictionary<char, string> dict = null) {
            dict ??= uiSymbols;

            symbolStringBuilder.Clear();
            foreach (char c in str) {
                if (dict.TryGetValue(c, out string name)) {
                    symbolStringBuilder.Append("<sprite name=").Append(name).Append('>');
                } else {
                    symbolStringBuilder.Append(c);
                }
            }
            return symbolStringBuilder.ToString();
        }

        private static readonly Color spectatorColor = new(0.9f, 0.9f, 0.9f, 0.7f);
        public unsafe static Color GetPlayerColor(Frame f, PlayerRef player, float s = 1, float v = 1) {
            if (f == null || player == PlayerRef.None) {
                return spectatorColor;
            }

            // Prioritize spectator status
            if (!f.TryResolveDictionary(f.Global->PlayerDatas, out var playerDataDict)
                || !playerDataDict.TryGetValue(player, out EntityRef playerDataEntity)
                || !f.Unsafe.TryGetPointer(playerDataEntity, out PlayerData* playerData)
                || playerData->IsSpectator) {

                return spectatorColor;
            }

            // Or dead marios
            if (f.Global->GameState > GameState.WaitingForPlayers) {
                var marioFilter = f.Filter<MarioPlayer>();
                marioFilter.UseCulling = false;
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
                return GetTeamColor(f, playerData->Team, s, v);
            }

            // Then id based color
            PlayerData* ourPlayerData = QuantumUtils.GetPlayerData(f, player);
            int ourIndex = 0;
            int totalPlayers = 0;

            var playerFilter = f.Filter<PlayerData>();
            playerFilter.UseCulling = false;
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
            return ping switch {
                < 0 => "<sprite name=connection_disconnected>",
                0 => "<sprite name=connection_host>",
                < 80 => "<sprite name=connection_great>",
                < 130 => "<sprite name=connection_good>",
                < 180 => "<sprite name=connection_fair>",
                _ => "<sprite name=connection_bad>"
            };
        }

        public static string BytesToString(long byteCount) {
            string[] suf = { "B", "KB", "MB", "GB", "TB", "PB", "EB" }; // Longs run out around EB
            if (byteCount == 0) {
                return "0B";
            }

            long bytes = Math.Abs(byteCount);
            int place = Convert.ToInt32(Math.Floor(Math.Log(bytes, 1024)));
            double num = Math.Round(bytes / Math.Pow(1024, place), 1);
            return (Math.Sign(byteCount) * num).ToString() + suf[place];
        }

        public static Color SampleNicknameColor(ReadOnlySpan<char> color, out bool constant) {
            if (color == null || color.IsEmpty) {
                constant = true;
                return Color.white;
            }

            if (color[0] == '#') {
                constant = true;
                return new Color32(byte.Parse(color[1..3], System.Globalization.NumberStyles.HexNumber), byte.Parse(color[3..5], System.Globalization.NumberStyles.HexNumber), byte.Parse(color[5..7], System.Globalization.NumberStyles.HexNumber), 255);
            } else if (color == "rainbow") {
                constant = false;
                return GetRainbowColor();
            } else {
                constant = true;
                return Color.white;
            }
        }

        public static Color GetRainbowColor() {
            // Four seconds per revolution
            double time = (Time.timeAsDouble * 0.25d) % 1d;
            return GlobalController.Instance.rainbowGradient.Evaluate((float) time);
        }
    }
}
