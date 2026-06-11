using Quantum;
using System;

namespace NSMB.UI.MainMenu.Submenus.Prompts {
    public class ChangeGamemodeTextOnSelect : ChangeTextOnSelect {

        //---Private Variables
        private IDisposable eventSubscription;

        public override void OnEnable() {
            base.OnEnable();
            eventSubscription = QuantumEvent.SubscribeManual<EventRulesChanged>(this, OnRulesChanged);
        }

        public override void OnDisable() {
            base.OnDisable();
            eventSubscription.Dispose();
        }

        private void OnRulesChanged(EventRulesChanged e) {
            if (selected) {
                ApplyText();
            }
        }

        public override unsafe string GetText() {
            Frame f = QuantumRunner.DefaultGame.Frames.Predicted;
            var gamemodeAsset = f.FindAsset(f.Global->Rules.Gamemode);
            return GlobalController.Instance.translationManager.GetTranslation(gamemodeAsset.DescriptionTranslationKey);
        }
    }
}