using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace NSMB.Utils {
    public static class ChatFilter {

        private static readonly string FILTER_BASE64 = "Y2hpbmFtYW4sY2hpbmFtZW4sY29vaWllLGNyaXAsY3VudCxkYWdvLGRheWdvLGRlZ28sZHlrZSxlc2tpbW8sZmFnLGZhZ2dvdCxnb29rLGdveSxnb3lpbSxoZWViLGthZmZlcixrYWZmaXIsa2FmZnJlLGthZmlyLGtpa2Usa3JhdXQsaWVzYm8sbWljayxuZWdyZXNzLG5lZ3JvLG5pZ3IsbmlnYSxuaWdnYSxuaWdnZXIsbmlnZ3IsbmlnZ3VoLG5pZyg/PVxXfCQpLG5pZ2cscGFqZWV0LHBha2koPz1cV3wkKSxwaWNrYW5pbm5pZSxwaWNrYW5pbm55LHByb3N0aXR1dGUscmV0YXJkLHJ0YXJkLHRhcmQsc2FtYm8sc2hlbWFpZSxza2FuayxzaXV0LHNveWJveSxzcGVyZyxzcXVhdyx0aXRzLHRyYW5uaWUsdHJhbm55LHdob3JlLHdpZ2dlcix6b2csZnVjayxmdWNrYSxmdWNrZXIsc2hpdCg/PVxXfCQpLHNoaXR0ZXIsc2hpdHR5LGNvY2sscGVuaXMsZGlsZG8sdmFnKD89XFd8JCksdmFnaW5hLHNwaWMsZnVrLGFzc2hvbGUsYWhvbGU=";
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

            string filteredInput = input.ToLower();
            filteredInput = ApplySubsitutions(filteredInput);

            StringBuilder result = new(input), filtered = new(filteredInput);

            foreach (string word in filteredWords) {

                Match match;
                while ((match = Regex.Match(filtered.ToString(), word)).Success) {
                    Capture mainCapture = match.Captures[0];

                    result.Remove(mainCapture.Index, mainCapture.Length);
                    result.Insert(mainCapture.Index, "***");
                    filtered.Remove(mainCapture.Index, mainCapture.Length);
                    filtered.Insert(mainCapture.Index, "***");
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
            for (int i = 0; i < filteredWords.Length; i++) {
                string word = filteredWords[i];
                string newWord = "";

                int j;
                for (j = 0; j < word.Length; j++) {
                    char c = word[j];
                    if (!char.IsLetter(c))
                        break;

                    newWord += c + "\\W*";
                }
                newWord = newWord[0..^3];
                newWord += word[j..];

                filteredWords[i] = newWord;
            }
        }
    }

    public class StringLengthComparer : IComparer<string> {
        public int Compare(string x, string y) {
            return y.Length - x.Length;
        }
    }
}
