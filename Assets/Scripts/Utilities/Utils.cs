using NSMB.UI.Game;
using NSMB.Utilities.Extensions;
using Quantum;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace NSMB.Utilities {
    public static class Utils {

        public static T IndexIntoOrDefault<T>(IReadOnlyList<T> list, int index, T def) {
            if (index < 0 || index >= list.Count) {
                return def;
            }
            return list[index];
        }

        public static T IndexIntoOrDefault<T>(IList<T> list, int index, T def) {
            if (index < 0 || index >= list.Count) {
                return def;
            }
            return list[index];
        }

        public static T IndexIntoOrDefault<T>(List<T> list, int index, T def) {
            return IndexIntoOrDefault((IList<T>) list, index, def);
        }

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

        public static string SecondsToMinuteSeconds(int secondsInput) {
            StringBuilder builder = new StringBuilder();
            int seconds = secondsInput % 60;
            int minutes = (secondsInput / 60) % 60;
            int hours = secondsInput / 60 / 60;

            if (hours > 0) {
                builder.Append(hours).Append(':')
                    .Append((minutes).ToString("00")).Append(':')
                    .Append((seconds).ToString("00"));
            } else {
                builder.Append(minutes).Append(':')
                    .Append(seconds.ToString("00"));
            }

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
        public static string GetSymbolString(ReadOnlySpan<char> str, Dictionary<char, string> dict = null, int padUpToNLength = 0) {
            dict ??= uiSymbols;

            symbolStringBuilder.Clear();
            foreach (char c in str) {
                if (dict.TryGetValue(c, out string name)) {
                    symbolStringBuilder.Append("<sprite name=").Append(name).Append('>');
                } else {
                    symbolStringBuilder.Append(c);
                }
            }
            for (int i = str.Length; i < padUpToNLength; i++) {
                symbolStringBuilder.Append("<space=0.5em><size=0>.</size>"); // invisible character suffix because if <SPACE> is at the end of the text, the component would trim it.
            }
            
            return symbolStringBuilder.ToString();
        }

        public static unsafe int? GetPlayerSlotIndex(Frame f, PlayerRef player) {
            int ourIndex = 0;
            if (f.Global->GameState is GameState.PreGameRoom or GameState.WaitingForPlayers) {
                // use PlayerData here
                PlayerData* ourPlayerData = QuantumUtils.GetPlayerData(f, player);
                if (ourPlayerData == null) {
                    return null;
                }

                foreach ((_, var otherPlayerData) in f.Unsafe.GetComponentBlockIterator<PlayerData>()) { 
                    if (otherPlayerData->IsSpectator) {
                        continue;
                    }

                    if (otherPlayerData->JoinTick < ourPlayerData->JoinTick) {
                        ourIndex++;
                    }
                }
            } else {
                // use PlayerInformation here
                ourIndex = -1;
                var playerInfos = f.Global->PlayerInfo;
                for (int i = 0; i < f.Global->RealPlayers; i++) {
                    if (playerInfos[i].PlayerRef == player) {
                        ourIndex = i;
                        break;
                    }
                }
            }

            return ourIndex < 0 ? null : ourIndex;
        }

        public static PlayerSlotInfo GetPlayerSlotInfo(Frame f, PlayerRef player) {
            int? index = GetPlayerSlotIndex(f, player);
            return GetPlayerSlotInfo(index);
        }

        public static PlayerSlotInfo GetPlayerSlotInfo(int? index) {
            var slots = GlobalController.Instance.playerSlots;
            return index.HasValue && index < slots.Length ? slots[index.Value] : null;
        }

        public static string GetPlayerIcon(Frame f, PlayerRef player) {
            var slot = GetPlayerSlotInfo(f, player);
            if (slot) {
                return slot.Icon;
            } else {
                return "";
            }
        }

        private static readonly Color spectatorColor = new(0.8f, 0.8f, 0.8f, 0.7f);
        public unsafe static Color GetPlayerColor(Frame f, PlayerRef player, float s = 1, float v = 1, bool considerDisqualifications = true) {
            if (f == null || player == PlayerRef.None) {
                return spectatorColor;
            }

            // Prioritize spectator status
            var playerData = QuantumUtils.GetPlayerData(f, player);
            if (playerData == null || playerData->IsSpectator) {
                return spectatorColor;
            }

            // Or dead marios
            if (f.Global->GameState > GameState.WaitingForPlayers && considerDisqualifications) {
                MarioPlayer* existingMario = null;
                foreach ((_, var mario) in f.Unsafe.GetComponentBlockIterator<MarioPlayer>()) {
                    if (mario->PlayerRef == player) {
                        existingMario = mario;
                        break;
                    }
                }

                if (existingMario == null
                    || (f.Global->GameState >= GameState.Playing && !existingMario->IsValid(f))) {
                    return spectatorColor;
                }
            }

            // Then team
            if (f.Global->Rules.TeamsEnabled) {
                return GetTeamColor(f, f.Global->GameState == GameState.PreGameRoom ? playerData->RequestedTeam : playerData->RealTeam, s, v);
            }

            // Then id based color
            var slot = GetPlayerSlotInfo(f, player);
            if (slot) {
                return slot.GetModifiedColor(s, v);
            } else {
                return spectatorColor;
            }
        }

        public static Color GetTeamColor(Frame f, int team, float s = 1, float v = 1) {
            var teams = f.Context.GetAllAssets<TeamAsset>();
            if (team < 0 || team >= teams.Count) {
                return spectatorColor;
            }

            Color color = teams[team].color;
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

        /// <summary>
        /// Taken from https://stackoverflow.com/a/4975942/19635374
        /// </summary>
        /// <param name="byteCount"></param>
        /// <returns></returns>
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

        /// <summary>
        /// Taken from https://stackoverflow.com/a/596243/19635374
        /// </summary>
        /// <param name="color"></param>
        /// <returns></returns>
        public static float Luminance(Color color) {
            return 0.2126f * color.r + 0.7152f * color.g + 0.0722f * color.b;
        }

        public static bool Blink(float timer, float blinksPerSecond, float? blinkStartTime = null) {
            if (blinkStartTime.HasValue && timer > blinkStartTime.Value) {
                return true;
            }

            return timer % (1f / blinksPerSecond) < (0.5f / blinksPerSecond);
        }
    }
}
