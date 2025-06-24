using NSMB.UI.Translation;

namespace NSMB.UI.MainMenu.Submenus.InRoom {
    public class TimerChangeableRule : NumberChangeableRule {
        protected override void UpdateLabel() {
            TranslationManager tm = GlobalController.Instance.translationManager;
            if (value is int intValue) {
                label.text = labelPrefix + ((minimumValueIsOff && intValue == minValue) ? tm.GetTranslation("ui.generic.off") : (intValue + ":00"));
            }
        }
    }
}
