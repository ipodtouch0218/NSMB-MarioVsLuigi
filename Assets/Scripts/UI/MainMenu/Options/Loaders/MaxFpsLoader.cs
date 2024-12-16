using NSMB.UI.Pause.Options;
using UnityEngine;

namespace NSMB.UI.Pause.Loaders {
    public class MaxFpsLoader : PauseOptionLoader {

        public override void LoadOptions(PauseOption option) {
            if (option is not SliderPauseOption spo) {
                return;
            }

            int value = Settings.Instance.GraphicsMaxFps <= 0 ? (int) spo.slider.maxValue : (Settings.Instance.GraphicsMaxFps / 5);
            spo.slider.SetValueWithoutNotify(value);
        }

        public override void OnValueChanged(PauseOption option, object newValue) {
            if (option is not SliderPauseOption spo) {
                return;
            }

            int value = Mathf.RoundToInt((float) newValue);
            if (value == (int) spo.slider.maxValue) {
                value = 0;
            }

            Settings.Instance.GraphicsMaxFps = value * 5;
            option.manager.RequireReconnect |= option.requireReconnect;
        }
    }
}
