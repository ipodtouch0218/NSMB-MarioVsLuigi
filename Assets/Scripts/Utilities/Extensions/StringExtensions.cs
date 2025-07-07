using Quantum;
using System.Text.RegularExpressions;
using UnityEngine;

namespace NSMB.Utilities {
    public static class StringExtensions {

        public const int NicknameMin = 2, NicknameMax = 20;

        public static string Filter(this string input) {
            if (Settings.Instance.generalChatFiltering) {
                return ChatFilter.FilterString(input);
            }

            return input;
        }

        private static readonly string NicknameRegex = $"^[\\w]{{{NicknameMin},{NicknameMax}}}";
        public static bool IsValidNickname(this string input) {
            if (input == null) {
                return false;
            }

            return Regex.IsMatch(input, NicknameRegex);
        }

        public static unsafe string ToValidNickname(this string input, Frame f, PlayerRef player, bool discrim = true) {
            input ??= "";

            // Valid characters
            input = Regex.Replace(input, @"[^\w]", "");

            // Name character maximum
            input = input[..Mathf.Min(input.Length, NicknameMax)];

            // Name character minimum
            if (input.Length < NicknameMin) {
                input = "noname";
            }

            // Name filtering
            input = input.Filter();

            // Discriminator
            if (discrim) {
                int discriminator = 0;
                PlayerData* ourPlayerData = QuantumUtils.GetPlayerData(f, player);
                if (ourPlayerData != null) {
                    var filter = f.Filter<PlayerData>();
                    while (filter.NextUnsafe(out _, out PlayerData* otherPlayerData)) {
                        if (otherPlayerData->PlayerRef == player) {
                            // Ignore ourselves
                            continue;
                        }

                        if (otherPlayerData->JoinTick > ourPlayerData->JoinTick) {
                            // Ignore players that joined after us
                            continue;
                        }

                        RuntimePlayer otherRuntimePlayer = f.GetPlayerData(otherPlayerData->PlayerRef);
                        if (otherRuntimePlayer.PlayerNickname.Filter().Equals(input)) {
                            discriminator++;
                        }
                    }

                    if (discriminator > 0) {
                        input += $" ({discriminator})";
                    }
                }
            }

            return input;
        }
    }
}