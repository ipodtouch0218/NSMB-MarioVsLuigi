namespace NSMB.UI.Pause.Options {

    public class NonselectableOption : PauseOption {

        public override bool IsSelectable => false;

        public override void Selected() {
            if (label && !string.IsNullOrEmpty(translationKey))
                label.text = GlobalController.Instance.translationManager.GetTranslation(translationKey);
        }

        public override void Deselected() {
            if (label && !string.IsNullOrEmpty(translationKey))
                label.text = GlobalController.Instance.translationManager.GetTranslation(translationKey);
        }
    }
}
