namespace NSMB.UI.Options.Loaders {
    public class SimpleToggleLoader : SimpleLoader<TogglePauseOption, bool> {
        public override bool GetValue(TogglePauseOption pauseOption) {
            return pauseOption.toggle.isOn;
        }

        public override void SetValue(TogglePauseOption pauseOption, bool value) {
            pauseOption.toggle.SetIsOnWithoutNotify(value);
        }
    }
}
