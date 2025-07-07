namespace NSMB.UI.Options.Loaders {
    public class SimpleSliderLoader : SimpleLoader<SliderPauseOption, float> {
        public override float GetValue(SliderPauseOption pauseOption) {
            return pauseOption.slider.value;
        }

        public override void SetValue(SliderPauseOption pauseOption, float value) {
            pauseOption.slider.SetValueWithoutNotify(value);
        }
    }
}
