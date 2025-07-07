using NSMB.UI.Translation;
using TMPro;
using UnityEngine;

namespace NSMB.UI.Options {
    public class SliderWithLabelPauseOption : SliderPauseOption {

        //---Serialized Variables
        [SerializeField] private TMP_Text valueLabel;
        [SerializeField] public float numberMultiplier = 1;
        [SerializeField] private string numberFormat = "F";
        [SerializeField] private string zeroOverride, maxOverride;

        public override void OnEnable() {
            base.OnEnable();
            UpdateLabel(slider.value);
        }

        public override void OnSliderValueChanged(float newValue) {
            base.OnSliderValueChanged(newValue);
            UpdateLabel(newValue);
        }

        private void UpdateLabel(float value) {
            TranslationManager tm = GlobalController.Instance.translationManager;
            if (!string.IsNullOrEmpty(zeroOverride) && Mathf.Abs(value) < 0.01f) {
                valueLabel.text = tm.GetTranslation(zeroOverride);
            } else if (!string.IsNullOrEmpty(maxOverride) && Mathf.Abs(value - slider.maxValue) < 0.01f) {
                valueLabel.text = tm.GetTranslation(maxOverride);
            } else {
                valueLabel.text = (value * numberMultiplier).ToString(numberFormat);
            }

            valueLabel.horizontalAlignment = tm.RightToLeft ? HorizontalAlignmentOptions.Left : HorizontalAlignmentOptions.Right;
        }
    }
}
