using System;
using System.Linq;
using UnityEngine;

using NSMB.Translation;
using NSMB.UI.Pause.Options;

namespace NSMB.UI.Pause.Loaders {
    public class FullscreenModeLoader : PauseOptionLoader {

        private static readonly string[] FullscreenDisplayKeys = {
            "ui.options.graphics.windowmode.fullscreen",
            "ui.options.graphics.windowmode.borderless",
            "ui.options.graphics.windowmode.maximized",
            "ui.options.graphics.windowmode.windowed" };

        //---Private Variables
        private FullScreenMode[] validModes;
        private PauseOption option;

        public void OnEnable() {
            GlobalController.Instance.translationManager.OnLanguageChanged += OnLanguageChanged;
        }

        public void OnDisable() {
            GlobalController.Instance.translationManager.OnLanguageChanged -= OnLanguageChanged;
        }

        public override void LoadOptions(PauseOption option) {
            if (option is not ScrollablePauseOption spo)
                return;

            this.option = option;
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
            }

            spo.options.AddRange(validModes.Select(fsm => FullscreenDisplayKeys[(int) fsm]).Select(GlobalController.Instance.translationManager.GetTranslation));

            int index = Array.IndexOf(validModes, Screen.fullScreenMode);
            spo.SetValue(index, false);
        }

        public override void OnValueChanged(PauseOption option, object newValue) {
            if (option is not ScrollablePauseOption spo)
                return;

            int value = spo.value;
            FullScreenMode newMode = validModes[value];

            if (newMode == FullScreenMode.Windowed) {
                Screen.SetResolution(GlobalController.Instance.windowWidth, GlobalController.Instance.windowHeight, false);
            } else {
                Screen.SetResolution(Screen.currentResolution.width, Screen.currentResolution.height, newMode);
            }
        }

        private void OnLanguageChanged(TranslationManager tm) {
            if (option)
                LoadOptions(option);
        }
    }
}
