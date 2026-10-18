using NSMB.UI.Translation;
using Quantum;

namespace NSMB.UI.MainMenu.Submenus.InRoom {
    public unsafe class TimerChangeableRule : NumberChangeableRule {

        public override bool CanDecreaseValue
            => base.CanDecreaseValue && ((int) value > 1 || CurrentGamemode.RequireTimer);

        private GamemodeAsset CurrentGamemode
            => QuantumUnityDB.GetGlobalAsset(QuantumRunner.DefaultGame.Frames.Predicted.Global->Rules.Gamemode);

        protected override void UpdateLabel() {
            TranslationManager tm = GlobalController.Instance.translationManager;
            if (value is int intValue) {
                label.text = labelPrefix + ((minimumValueIsOff && intValue == minValue) ? tm.GetTranslation("ui.generic.off") : (intValue + ":00"));
            }
        }
    }
}
