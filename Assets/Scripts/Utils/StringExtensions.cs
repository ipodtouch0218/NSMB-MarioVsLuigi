using System.Text.RegularExpressions;
using UnityEngine;

namespace NSMB.Utils {
    public static class StringExtensions {

        public static string AsUsername(this string input) {
            input = Regex.Replace(input, "[^0-9A-Za-z_](?!$)(?!\\d*\\)$)", "");
            input = input.Substring(0, Mathf.Min(input.Length, MainMenuManager.NICKNAME_MAX));

            if (input.Length < MainMenuManager.NICKNAME_MIN)
                input = new string('*', MainMenuManager.NICKNAME_MIN - input.Length);

            return input.Filter();
        }

        public static string Filter(this string input) {
            if (Settings.Instance.filter)
                return ChatFilter.FilterString(input);

            return input;
        }
    }
}