using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace NSMB.Utils {
    public static class ChatFilter {

        private static readonly string FILTER_BASE64 = "YWJibyxhYm8sY2hpbmFtYW4sY2hpbmFtZW4sY2hpbmssY29vaWllLGNvb24sY3JhemllLGNyYXp5LGNyaXAsY3VudCxkYWdvLGRheWdvLGRlZ28sZHlrZSxlc2tpbW8sZmFnLGZhZ2dvdCxnYXNoLGdvbGxpd29nLGdvb2ssZ295LGdveWltLGd5cCxneXBzeSxoZWViLGthZmZlcixrYWZmaXIsa2FmZmlyLGthZmZyZSxrYWZpcixraWtlLGtyYXV0LGxlc2JvLGx1bmF0aWMsbWljayxuZWdyZXNzLG5lZ3JvLG5pZyxuaWctbm9nLG5pZ2dhLG5pZ2dlcixuaWdndWgsbmlwLHBhamVldCxwYWtpLHBpY2thbmlubmllLHBpY2thbmlubnkscHJvc3RpdHV0ZSxyYWdoZWFkLHJldGFyZCxzYW1ibyxzaGVtYWxlLHNrYW5rLHNsdXQsc295Ym95LHNwZXJnLHNwaWMsc3F1YXcsc3RyZWV0LXNoaXR0ZXIsdGFyZCx0aXRzLHRpdHQsdHJhbm5pZSx0cmFubnksd2V0YmFjayx3aG9yZSx3aWdnZXIsd29wLHlpZCx6b2c=";
        private static readonly Dictionary<char, char> REPLACEMENTS = new() {
            ['5'] = 's',
            ['0'] = 'o',
            ['3'] = 'e',
            ['6'] = 'g',
            ['l'] = 'i',
            ['1'] = 'i',
            ['!'] = 'i',
        };
        private static readonly StringLengthComparer STRING_LENGTH_COMPARER = new();
        private static string[] filteredWords;

        public static string Filter(this string input) {
            if (Settings.Instance.filter)
                return FilterString(input);

            return input;
        }

        public static string FilterString(string input) {
            DecodeFilter();

            input = input.ToLower();
            input = ApplySubsitutions(input);

            foreach (string word in filteredWords) {
                input = Regex.Replace(input, word, new string('*', word.Length), RegexOptions.IgnoreCase);
            }

            return input;
        }

        private static string ApplySubsitutions(string input) {
            foreach ((char replace, char replacement) in REPLACEMENTS)
                input = input.Replace(replace, replacement);

            return input;
        }

        private static void DecodeFilter() {
            if (filteredWords != null)
                return;

            filteredWords = Encoding.UTF8.GetString(Convert.FromBase64String(FILTER_BASE64)).Split(",");
            Array.Sort(filteredWords, STRING_LENGTH_COMPARER);
        }
    }

    public class StringLengthComparer : IComparer<string> {
        public int Compare(string x, string y) {
            return y.Length - x.Length;
        }
    }
}
