using System;

namespace NSMB.Utils {
    public static class StringUtils {

        public static string ToBase64(byte[] input) {
            string base64 = Convert.ToBase64String(input);
            base64 = base64.Replace('+', '-').Replace('/', '_').Replace("=", "");

            return base64;
        }

        public static byte[] FromBase64(string input) {
            input = input.Replace("-", "+").Replace("_", "/");
            int paddings = input.Length % 4;
            if (paddings > 0)
                input += new string('=', 4 - paddings);

            return Convert.FromBase64String(input);
        }

    }
}
