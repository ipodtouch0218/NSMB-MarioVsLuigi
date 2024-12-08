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
            "ui.options.graphics.windowmode.windowed"
        };

        //---Private Variables
        private FullScreenMode[] validModes;
        private PauseOption option;

        public void OnEnable() {
            TranslationManager.OnLanguageChanged += OnLanguageChanged;
        }

        public void OnDisable() {
            TranslationManager.OnLanguageChanged -= OnLanguageChanged;
        }

        public override void LoadOptions(PauseOption option) {
            if (option is not ScrollablePauseOption spo) {
                return;
            }

            this.option = option;
            spo.options.Clear();

            validModes ??= Application.platform switch {
                RuntimePlatform.WindowsPlayer or RuntimePlatform.WindowsEditor => new[] { FullScreenMode.Windowed, FullScreenMode.FullScreenWindow, FullScreenMode.ExclusiveFullScreen },
                RuntimePlatform.OSXPlayer or RuntimePlatform.OSXEditor => new[] { FullScreenMode.Windowed, FullScreenMode.FullScreenWindow, FullScreenMode.MaximizedWindow },
                RuntimePlatform.LinuxPlayer or RuntimePlatform.LinuxEditor => new[] { FullScreenMode.Windowed, FullScreenMode.FullScreenWindow },
                _ => new[] { FullScreenMode.FullScreenWindow },
            };

            spo.options.AddRange(validModes.Select(fsm => FullscreenDisplayKeys[(int) fsm]).Select(GlobalController.Instance.translationManager.GetTranslation));

            int index = Array.IndexOf(validModes, Screen.fullScreenMode);
            spo.SetValue(index, false);
        }

        public override void OnValueChanged(PauseOption option, object newValue) {
            if (option is not ScrollablePauseOption spo) {
                return;
            }

            int value = spo.value;
            FullScreenMode newMode = validModes[value];

            if (newMode == FullScreenMode.Windowed) {
                Screen.SetResolution(GlobalController.Instance.windowWidth, GlobalController.Instance.windowHeight, false);
            } else {
                Screen.SetResolution(Screen.currentResolution.width, Screen.currentResolution.height, newMode);
            }

            option.manager.RequireReconnect |= option.requireReconnect;
        }

        private void OnLanguageChanged(TranslationManager tm) {
            if (option) {
                LoadOptions(option);
            }
        }
    }
}
