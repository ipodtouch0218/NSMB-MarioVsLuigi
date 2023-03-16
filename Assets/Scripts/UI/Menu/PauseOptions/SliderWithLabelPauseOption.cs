using UnityEngine;
using TMPro;

namespace NSMB.UI.Pause.Options {

    public class SliderWithLabelPauseOption : SliderPauseOption {

        //---Serialized Variables
        [SerializeField] private TMP_Text valueLabel;
        [SerializeField] private float numberMultiplier = 1;
        [SerializeField] private string numberFormat = "F";
        [SerializeField] private string zeroOverride, maxOverride;

        public void Awake() {
            slider.onValueChanged.AddListener(OnValueChanged);
            OnValueChanged(slider.value);
        }

        private void OnValueChanged(float newValue) {
            if (!string.IsNullOrEmpty(zeroOverride) && Mathf.Abs(newValue) < 0.01f)
                valueLabel.text = zeroOverride;
            else if (!string.IsNullOrEmpty(maxOverride) && Mathf.Abs(newValue - slider.maxValue) < 0.01f)
                valueLabel.text = maxOverride;
            else
                valueLabel.text = (newValue * numberMultiplier).ToString(numberFormat);
        }
    }
}
