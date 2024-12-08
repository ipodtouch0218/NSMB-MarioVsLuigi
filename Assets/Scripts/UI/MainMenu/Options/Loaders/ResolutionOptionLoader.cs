using System.Collections.Generic;
using System.Linq;
using UnityEngine;

using NSMB.UI.Pause.Options;

namespace NSMB.UI.Pause.Loaders {

    public class ResolutionOptionLoader : PauseOptionLoader {

        private List<Resolution> resolutions;

        public override void LoadOptions(PauseOption option) {
            if (option is not ScrollablePauseOption spo) {
                return;
            }

            spo.options.Clear();

            resolutions = Screen.resolutions.Distinct(new ResolutionComparer()).ToList();

            spo.options.AddRange(
                resolutions.Select(res => {
                    int width = res.width;
                    int height = res.height;

                    return res.width + "x" + res.height + " (" + GetClosestAspectRatio(width, height) + ")";
                }));

            int index = Screen.resolutions.Length;
            for (int i = 0; i < Screen.resolutions.Length; i++) {
                var res = Screen.resolutions[i];
                if (Screen.currentResolution.width == res.width && Screen.currentResolution.height == res.height) {
                    index = i;
                    break;
                }
            }

            spo.SetValue(index, false);
            return;
        }

        public override void OnValueChanged(PauseOption option, object newValue) {
            if (option is not ScrollablePauseOption spo) {
                return;
            }

            if (Screen.fullScreenMode != FullScreenMode.Windowed) {
                var res = resolutions[spo.value];
                Screen.SetResolution(res.width, res.height, Screen.fullScreenMode);
            }

            option.manager.RequireReconnect |= option.requireReconnect;
        }

        public class ResolutionComparer : IEqualityComparer<Resolution> {
            public bool Equals(Resolution x, Resolution y) {
                return x.width == y.width && x.height == y.height;
            }

            public int GetHashCode(Resolution obj) {
                return obj.width.GetHashCode() ^ obj.height.GetHashCode();
            }
        }

        private readonly static Dictionary<float, string> ValidAspectRatios = new() {
            { 5f/4f, "5:4" },
            { 4f/3f, "4:3" },
            { 3f/2f, "3:2" },
            { 8f/5f, "8:5" },
            { 5f/3f, "5:3" },
            { 16f/9f, "16:9" },
            { 21f/9f, "21:9" },
            { 32f/9f, "32:9" }
        };
        private static string GetClosestAspectRatio(int width, int height) {
            int gcd = GreatestCommonDenominator(width, height);
            int aspectNumerator = width / gcd;
            int aspectDenominator = height / gcd;
            float aspectRatio = (float) aspectNumerator / aspectDenominator;

            string closest = null;
            float closestValue = float.MaxValue;
            foreach ((float aspect, string display) in ValidAspectRatios) {
                float difference = Mathf.Abs(aspectRatio - aspect);
                if (difference < closestValue) {
                    closest = display;
                    closestValue = difference;
                }
            }

            return closest;
        }

        private static int GreatestCommonDenominator(int a, int b) {
            return (b == 0) ? a : GreatestCommonDenominator(b, a % b);
        }
    }
}
