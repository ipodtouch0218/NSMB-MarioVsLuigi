using UnityEngine;
using TMPro;

namespace NSMB.UI.Pause.Options {

    public class SliderWithLabelPauseOption : SliderPauseOption {

        //---Serialized Variables
        [SerializeField] private TMP_Text valueLabel;
        [SerializeField] private float numberMultiplier = 1;
        [SerializeField] private string numberFormat = "F";
        [SerializeField] private string zeroOverride;

        public void Awake() {
            slider.onValueChanged.AddListener(OnValueChanged);
            OnValueChanged(slider.value);
        }

        private void OnValueChanged(float newValue) {
            if (!string.IsNullOrEmpty(zeroOverride) && Mathf.Abs(newValue) < 0.01f)
                valueLabel.text = zeroOverride;
            else
                valueLabel.text = (newValue * numberMultiplier).ToString(numberFormat);
        }
    }
}
