using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace NSMB.Utils {
    public static class ChatFilter {

        private static readonly string FILTER_BASE64 = "Y2hpbmFtYW4sY2hpbmFtZW4sY29vaWllLGN1bnQsZGFnbyxkYXlnbyxkZWdvLGR5a2UsZXNraW1vLGZhZyxmYWdnb3QsZ29vayxnb3ksZ295aW0saGVlYixrYWZmZXIsa2FmZmlyLGthZmZyZSxrYWZpcixraWtlLGtyYXV0LGllc2JvLG1pY2ssbmVncmVzcyxuZWdybyxuaWdyLG5pZ2EsbmlnZ2EsbmlnZ2VyLG5pZ2dyLG5pZ2d1aCxuaWcoPz1cV3wkKSxuaWdnLHBhamVldCxwYWtpKD89XFd8JCkscGlja2FuaW5uaWUscGlja2FuaW5ueSxwcm9zdGl0dXRlLHJldGFyZCxydGFyZCx0YXJkLHNhbWJvLHNoZW1haWUsc2thbmssc2l1dCxzb3lib3ksc3Blcmcsc3F1YXcsdHJhbm5pZSx0cmFubnksd2lnZ2VyLHpvZyxzcGlj";
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
