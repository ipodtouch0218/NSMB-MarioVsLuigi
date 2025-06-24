using NSMB.UI.Game;
using NSMB.Utilities.Extensions;
using Quantum;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace NSMB.Utilities {
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
            ['c'] = "hudnumber_objectivecoin",
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
        public static readonly Dictionary<char, string> resultsSymbols = new() {
            ['0'] = "results_0",
            ['1'] = "results_1",
            ['2'] = "results_2",
            ['3'] = "results_3",
            ['4'] = "results_4",
            ['5'] = "results_5",
            ['6'] = "results_6",
            ['7'] = "results_7",
            ['8'] = "results_8",
            ['9'] = "results_9",
            ['S'] = "results_star",
            ['O'] = "results_out",
            ['c'] = "hudnumber_objectivecoin",
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

        private static readonly Color spectatorColor = new(0.8f, 0.8f, 0.8f, 0.7f);
        public unsafe static Color GetPlayerColor(Frame f, PlayerRef player, float s = 1, float v = 1, bool considerDisqualifications = true) {
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
            if (f.Global->GameState > GameState.WaitingForPlayers && considerDisqualifications) {
                var marioFilter = f.Filter<MarioPlayer>();
                marioFilter.UseCulling = false;
                MarioPlayer* existingMario = null;
                while (marioFilter.NextUnsafe(out _, out MarioPlayer* mario)) {
                    if (mario->PlayerRef == player) {
                        existingMario = mario;
                        break;
                    }
                }

                if (existingMario == null
                    || (f.Global->GameState >= GameState.Playing && f.Global->Rules.IsLivesEnabled && existingMario->Lives <= 0)) {
                    return spectatorColor;
                }
            }

            // Then team
            if (f.Global->Rules.TeamsEnabled) {
                return GetTeamColor(f, f.Global->GameState == GameState.PreGameRoom ? playerData->RequestedTeam : playerData->RealTeam, s, v);
            }

            // Then id based color
            int ourIndex = 0;
            int totalPlayers = 0;
            if (f.Global->GameState == GameState.PreGameRoom) {
                // use PlayerData here
                PlayerData* ourPlayerData = QuantumUtils.GetPlayerData(f, player);

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
            } else {
                // use PlayerInformation here
                ourIndex = -1;
                totalPlayers = f.Global->RealPlayers;
                var playerInfos = f.Global->PlayerInfo;
                for (int i = 0; i < totalPlayers; i++) {
                    if (playerInfos[i].PlayerRef == player) {
                        ourIndex = i;
                        break;
                    }
                }

                if (ourIndex == -1) {
                    // Spectator
                    return spectatorColor;
                }
            }

            return Color.HSVToRGB(ourIndex / (totalPlayers + 1f), s, v);
        }

        public static Color GetTeamColor(Frame f, int team, float s = 1, float v = 1) {
            var teams = f.SimulationConfig.Teams;
            if (team < 0 || team >= teams.Length) {
                return spectatorColor;
            }

            Color color = f.FindAsset(teams[team]).color;
            Color.RGBToHSV(color, out float hue, out float saturation, out float value);
            return Color.HSVToRGB(hue, saturation * s, value * v);
        }

        public static string ColorToHex(Color32 color, bool includeAlpha) {
            StringBuilder builder = new(8);
            builder.Append(Convert.ToString(color.r, 16).PadLeft(2, '0'));
            builder.Append(Convert.ToString(color.g, 16).PadLeft(2, '0'));
            builder.Append(Convert.ToString(color.b, 16).PadLeft(2, '0'));
            if (includeAlpha) {
                builder.Append(Convert.ToString(color.a, 16).PadLeft(2, '0'));
            }
            return builder.ToString();
        }

        public static string GetPingSymbol(int ping) {
            return ping switch {
                < 0 => "<sprite name=connection_disconnected>",
                0 => "<sprite name=connection_host>",
                < 70 => "<sprite name=connection_great>",
                < 140 => "<sprite name=connection_good>",
                < 210 => "<sprite name=connection_fair>",
                _ => "<sprite name=connection_bad>"
            };
        }

        public static Sprite GetPingSprite(int ping) {
            int index = ping switch {
                < 0 => 0,
                0 => 1,
                < 70 => 2,
                < 140 => 3,
                < 210 => 4,
                _ => 5
            };
            return GlobalController.Instance.pingIndicators[index];
        }

        public static string BytesToString(long byteCount) {
            string[] suf = { "B", "KB", "MB", "GB", "TB", "PB", "EB" }; // Longs run out around EB
            if (byteCount == 0) {
                return "0B";
            }

            long bytes = Math.Abs(byteCount);
            int place = Convert.ToInt32(Math.Floor(Math.Log(bytes, 1024)));
            double num = Math.Round(bytes / Math.Pow(1024, place), 2);
            return (Math.Sign(byteCount) * num).ToString() + suf[place];
        }

        public static Color SampleIQGradient(float time, Vector3 a, Vector3 b, Vector3 c, Vector3 d) {
            Vector3 result = a + b.Multiply(Vector3Cos(Mathf.PI * 2 * (c * time + d)));
            return new Color(result.x, result.y, result.z);
        }

        private static Vector3 Vector3Cos(Vector3 vec) {
            return new(Mathf.Cos(vec.x), Mathf.Cos(vec.y), Mathf.Cos(vec.z));
        }

        public static float Luminance(Color color) {
            // https://stackoverflow.com/a/596243/19635374
            return 0.2126f * color.r + 0.7152f * color.g + 0.0722f * color.b;
        }
    }
}
