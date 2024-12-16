using NSMB.UI.MainMenu;
using Quantum;
using System.Text.RegularExpressions;
using UnityEngine;

namespace NSMB.Utils {
    public static class StringExtensions {

        public static string Filter(this string input) {
            if (Settings.Instance.generalChatFiltering) {
                return ChatFilter.FilterString(input);
            }

            return input;
        }

        private static string UsernameRegex = null;
        public static bool IsValidUsername(this string input) {
            if (input == null) {
                return false;
            }

            UsernameRegex ??= $"^[0-9A-Za-z]{{{MainMenuManager.NicknameMin},{MainMenuManager.NicknameMax}}}";
            return Regex.IsMatch(input, UsernameRegex);
        }

        public static unsafe string ToValidUsername(this string input, Frame f, PlayerRef player, bool discrim = true) {
            input ??= "";

            // Valid characters
            input = Regex.Replace(input, @"[^A-Za-z0-9]", "");

            // Name character maximum
            input = input[..Mathf.Min(input.Length, MainMenuManager.NicknameMax)];

            // Name character minimum
            if (input.Length < MainMenuManager.NicknameMin) {
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