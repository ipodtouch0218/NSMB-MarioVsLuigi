using UnityEngine;
using UnityEngine.UI;

namespace NSMB.UI.Pause.Options {

    public class SliderPauseOption : PauseOption {

        //---Public Variables
        public Slider slider;

        //---Private Variables
        private float holdTime;

        public override void OnValidate() {
            base.OnValidate();
            if (!slider) slider = GetComponentInChildren<Slider>();
        }

        public virtual void Awake() {
            slider.onValueChanged.AddListener(OnSliderValueChanged);
        }

        public override void OnLeftPress() {
            if (slider.wholeNumbers)
                slider.value--;
            holdTime = 0;
        }

        public override void OnRightPress() {
            if (slider.wholeNumbers)
                slider.value++;
            holdTime = 0;
        }

        public override void OnLeftHeld() {
            holdTime += Time.deltaTime;
            if (slider.wholeNumbers) {
                if (holdTime > 0.15f) {
                    slider.value--;
                    holdTime = 0;
                }
            } else {
                slider.value -= (slider.maxValue - slider.minValue) * 0.5f * Time.deltaTime;
            }
        }

        public override void OnRightHeld() {
            holdTime += Time.deltaTime;
            if (slider.wholeNumbers) {
                if (holdTime > 0.15f) {
                    slider.value++;
                    holdTime = 0;
                }
            } else {
                slider.value += (slider.maxValue - slider.minValue) * 0.5f * Time.deltaTime;
            }
        }

        public virtual void OnSliderValueChanged(float newValue) {
            if (loader)
                loader.OnValueChanged(this, newValue);

            Settings.Instance.SaveSettings();
        }
    }
}
