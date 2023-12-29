using System.Text.RegularExpressions;
using UnityEngine;

using NSMB.UI.MainMenu;

namespace NSMB.Utils {
    public static class StringExtensions {

        public static string Filter(this string input) {
            if (Settings.Instance.generalChatFiltering)
                return ChatFilter.FilterString(input);

            return input;
        }

        public static bool IsValidUsername(this string input, bool allowDiscriminator = true) {
            if (input == null)
                return false;

            string count = MainMenuManager.NicknameMin + "," + MainMenuManager.NicknameMax;
            return Regex.IsMatch(input, "^[0-9A-Za-z]{" + count + "}" + (allowDiscriminator ? "(\\([0-9]\\))?" : "") + "$");
        }

        public static string ToValidUsername(this string input, bool discrim = true) {

            string discriminator = input?.Length >= 3 ? input[^3..] : "";

            //valid characters
            input = Regex.Replace(input, @"(\([0-9]\))|[^A-Za-z0-9]", "");

            //name character maximum
            input = input.Substring(0, Mathf.Min(input.Length, MainMenuManager.NicknameMax));

            //name character minimum
            if (input.Length < MainMenuManager.NicknameMin)
                input = "noname";

            //name filtering
            input = input.Filter();

            if (discrim && Regex.IsMatch(discriminator, @"^\([0-9]\)$"))
                input += discriminator;

            return input;
        }
    }
}