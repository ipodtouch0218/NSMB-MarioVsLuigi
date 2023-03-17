using NSMB.UI.Pause.Options;

namespace NSMB.UI.Pause.Loaders {
    public class SimpleSliderLoader : SimpleLoader<SliderPauseOption, float> {
        public override float GetValue(SliderPauseOption pauseOption) {
            return pauseOption.slider.value;
        }

        public override void SetValue(SliderPauseOption pauseOption, float value) {
            pauseOption.slider.SetValueWithoutNotify(value);
        }
    }
}
