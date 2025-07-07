using UnityEngine;

namespace NSMB.UI.Options.Loaders {
    public class MaxTempReplaysLoader : PauseOptionLoader {

        public override void LoadOptions(PauseOption option) {
            if (option is not SliderWithLabelPauseOption swlpo) {
                return;
            }

            int value = Settings.Instance.generalMaxTempReplays <= 0 ? (int) swlpo.slider.maxValue : (Settings.Instance.generalMaxTempReplays / (int) swlpo.numberMultiplier);
            swlpo.slider.SetValueWithoutNotify(value);
        }

        public override void OnValueChanged(PauseOption option, object newValue) {
            if (option is not SliderWithLabelPauseOption swlpo) {
                return;
            }

            int value = Mathf.RoundToInt((float) newValue);
            if (value == (int) swlpo.slider.maxValue) {
                value = 0;
            }

            Settings.Instance.generalMaxTempReplays = value * (int) swlpo.numberMultiplier;
            option.manager.RequireReconnect |= option.requireReconnect;
        }
    }
}
