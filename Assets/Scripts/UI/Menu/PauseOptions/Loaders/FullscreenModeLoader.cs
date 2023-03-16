using System;
using System.Linq;
using UnityEngine;

using NSMB.UI.Pause.Options;

namespace NSMB.UI.Pause.Loaders {
    public class FullscreenModeLoader : PauseOptionLoader {

        private static readonly string[] FullscreenDisplayNames = { "Exclusive Fullscreen", "Borderless Fullscreen", "Maximized Window", "Windowed" };
        private FullScreenMode[] validModes;

        public override void LoadOptions(PauseOption option) {
            if (option is not ScrollablePauseOption spo)
                return;

            spo.options.Clear();

            if (validModes == null) {
                switch (Application.platform) {
                case RuntimePlatform.WindowsPlayer:
                case RuntimePlatform.WindowsEditor:
                    validModes = new[] { FullScreenMode.Windowed, FullScreenMode.FullScreenWindow, FullScreenMode.ExclusiveFullScreen };
                    break;

                case RuntimePlatform.OSXPlayer:
                case RuntimePlatform.OSXEditor:
                    validModes = new[] { FullScreenMode.Windowed, FullScreenMode.FullScreenWindow, FullScreenMode.MaximizedWindow };
                    break;

                case RuntimePlatform.LinuxPlayer:
                case RuntimePlatform.LinuxEditor:
                    validModes = new[] { FullScreenMode.Windowed, FullScreenMode.FullScreenWindow };
                    break;

                default:
                    validModes = new[] { FullScreenMode.FullScreenWindow };
                    break;
                }
                return;
            }

            spo.options.AddRange(validModes.Select(fsm => FullscreenDisplayNames[(int) fsm]));

            int index = Array.IndexOf(validModes, Screen.fullScreenMode);
            spo.SetValue(index, false);
        }

        public override void OnValueChanged(PauseOption option, object previousValue) {
            if (option is not ScrollablePauseOption spo)
                return;

            int previous = (int) previousValue;
            int value = spo.value;
            FullScreenMode newMode = validModes[value];

            if (previous != value) {
                if (newMode == FullScreenMode.Windowed) {
                    Screen.SetResolution(GlobalController.Instance.windowWidth, GlobalController.Instance.windowHeight, false);
                } else {
                    Screen.SetResolution(Screen.currentResolution.width, Screen.currentResolution.height, newMode);
                }
            }
        }
    }
}
