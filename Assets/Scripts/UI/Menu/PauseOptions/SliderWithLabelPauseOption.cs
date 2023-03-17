using UnityEngine;
using TMPro;

namespace NSMB.UI.Pause.Options {

    public class SliderWithLabelPauseOption : SliderPauseOption {

        //---Serialized Variables
        [SerializeField] private TMP_Text valueLabel;
        [SerializeField] private float numberMultiplier = 1;
        [SerializeField] private string numberFormat = "F";
        [SerializeField] private string zeroOverride, maxOverride;

        public override void Awake() {
            base.Awake();
        }

        public override void OnEnable() {
            base.OnEnable();
            UpdateLabel(slider.value);
        }

        public override void OnSliderValueChanged(float newValue) {
            base.OnSliderValueChanged(newValue);
            UpdateLabel(newValue);
        }
        private void UpdateLabel(float value) {
            if (!string.IsNullOrEmpty(zeroOverride) && Mathf.Abs(value) < 0.01f)
                valueLabel.text = zeroOverride;
            else if (!string.IsNullOrEmpty(maxOverride) && Mathf.Abs(value - slider.maxValue) < 0.01f)
                valueLabel.text = maxOverride;
            else
                valueLabel.text = (value * numberMultiplier).ToString(numberFormat);
        }
    }
}
