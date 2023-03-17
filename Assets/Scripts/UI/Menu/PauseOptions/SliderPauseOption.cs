using UnityEngine;
using UnityEngine.UI;

namespace NSMB.UI.Pause.Options {

    public class SliderPauseOption : PauseOption {

        //---Serialized Variables
        [SerializeField] public Slider slider;

        public override void OnValidate() {
            base.OnValidate();
            if (!slider) slider = GetComponentInChildren<Slider>();
        }

        public virtual void Awake() {
            slider.onValueChanged.AddListener(OnSliderValueChanged);
        }

        public override void OnLeftPress() {
            if (slider.wholeNumbers)
                slider.value -= 1;
            else
                slider.value -= (slider.maxValue - slider.minValue) * 0.1f;
        }

        public override void OnRightPress() {
            if (slider.wholeNumbers)
                slider.value += 1;
            else
                slider.value += (slider.maxValue - slider.minValue) * 0.1f;
        }

        public virtual void OnSliderValueChanged(float newValue) {
            if (loader)
                loader.OnValueChanged(this, newValue);

            Settings.Instance.SaveSettings();
        }
    }
}
