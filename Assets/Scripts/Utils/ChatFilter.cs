using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace NSMB.Utils {
    public static class ChatFilter {

        private static readonly string FILTER_BASE64 = "YWJibyxjaGluYW1hbixjaGluYW1lbixjaGluayxjb29paWUsY29vbixjcmlwLGN1bnQsZGFnbyxkYXlnbyxkZWdvLGR5a2UsZXNraW1vLGZhZyxmYWdnb3QsZ2FzaCxnb2xsaXdvZyxnb29rLGdveSxnb3lpbSxneXAsZ3lwc3ksaGVlYixrYWZmZXIsa2FmZmlyLGthZmZpcixrYWZmcmUsa2FmaXIsa2lrZSxrcmF1dCxsZXNibyxtaWNrLG5lZ3Jlc3MsbmVncm8sbmlncixuaWdhLG5pZy1ub2csbmlnZ2EsbmlnZ2VyLG5pZ2dyLG5pZ2d1aCxwYWplZXQscGFraSxwaWNrYW5pbm5pZSxwaWNrYW5pbm55LHByb3N0aXR1dGUscmFnaGVhZCxyZXRhcmQsc2FtYm8sc2hlbWFsZSxza2FuayxzbHV0LHNveWJveSxzcGVyZyxzcGljLHNxdWF3LHN0cmVldC1zaGl0dGVyLHJ0YXJkLHRpdHMsdGl0dCx0cmFubmllLHRyYW5ueSx3ZXRiYWNrLHdob3JlLHdpZ2dlcix3b3AseWlkLHpvZyxuaWcoPz1cV3wkKSxuaWdn";
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
