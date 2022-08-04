using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace NSMB.Utils {
    public static class ChatFilter {

        private static readonly string FILTER_BASE64 = "Y2hpbmFtYW4sY2hpbmFtZW4sY29vaWllLGNyaXAsY3VudCxkYWdvLGRheWdvLGRlZ28sZHlrZSxlc2tpbW8sZmFnLGZhZ2dvdCxnb29rLGdveSxnb3lpbSxoZWViLGthZmZlcixrYWZmaXIsa2FmZnJlLGthZmlyLGtpa2Usa3JhdXQsaWVzYm8sbWljayxuZWdyZXNzLG5lZ3JvLG5pZ3IsbmlnYSxuaWdnYSxuaWdnZXIsbmlnZ3IsbmlnZ3VoLG5pZyg/PVxXfCQpLG5pZ2cscGFqZWV0LHBha2koPz1cV3wkKSxwaWNrYW5pbm5pZSxwaWNrYW5pbm55LHByb3N0aXR1dGUscmFnaGVhZCxyZXRhcmQscnRhcmQsdGFyZCxzYW1ibyxzaGVtYWllLHNrYW5rLHNpdXQsc295Ym95LHNwZXJnLHNxdWF3LHRpdHMsdHJhbm5pZSx0cmFubnksd2hvcmUsd2lnZ2VyLHdvcCx5aWQsem9nLGZ1Y2ssZnVja2Esc2hpdCxjb2NrLHBlbmlzLHZhZyx2YWdpbmEsc3BpYw==";
        private static readonly Dictionary<char, char> REPLACEMENTS = new() {
            ['5'] = 's',
            ['0'] = 'o',
            ['3'] = 'e',
            ['6'] = 'g',
            ['l'] = 'i',
            ['1'] = 'i',
            ['|'] = 'i',
            ['@'] = 'a',
        };
        private static readonly StringLengthComparer STRING_LENGTH_COMPARER = new();
        private static string[] filteredWords;

        public static string FilterString(string input) {
            DecodeFilter();

            StringBuilder result = new(input);

            string filtered = input.ToLower();
            filtered = ApplySubsitutions(filtered);

            foreach (string word in filteredWords) {
                foreach (Match m in Regex.Matches(filtered, word)) {
                    foreach (Capture c in m.Captures) {
                        for (int i = c.Index; i < c.Index + c.Length; i++) {
                            result[i] = '*';
                        }
                    }
                }
            }

            return result.ToString();
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
