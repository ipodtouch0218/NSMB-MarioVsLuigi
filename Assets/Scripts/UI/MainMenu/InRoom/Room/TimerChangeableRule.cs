using NSMB.UI.Translation;
using Quantum;
using UnityEngine;

namespace NSMB.UI.MainMenu.Submenus.InRoom {
    public unsafe class TimerChangeableRule : NumberChangeableRule {

        [SerializeField] private AssetRef<GamemodeAsset> coinRunnersGamemode;

        // Horrible, terrible bodge.
        public override bool CanDecreaseValue => base.CanDecreaseValue && ((int) value > 1 || QuantumRunner.DefaultGame.Frames.Predicted.Global->Rules.Gamemode != coinRunnersGamemode);

        protected override void UpdateLabel() {
            TranslationManager tm = GlobalController.Instance.translationManager;
            if (value is int intValue) {
                label.text = labelPrefix + ((minimumValueIsOff && intValue == minValue) ? tm.GetTranslation("ui.generic.off") : (intValue + ":00"));
            }
        }
    }
}
