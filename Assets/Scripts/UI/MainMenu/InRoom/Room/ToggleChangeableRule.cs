using NSMB.UI.Translation;
using Quantum;

namespace NSMB.UI.MainMenu.Submenus.InRoom {
    public class ToggleChangeableRule : ChangeableRule {

        //---Properties
        public override bool CanIncreaseValue => !(bool) value;
        public override bool CanDecreaseValue => (bool) value;

        protected override void IncreaseValueInternal() {
            if (!(bool) value) {
                value = true;
                cursorSfx.Play();
                SendCommand();
            }
        }

        protected override unsafe void DecreaseValueInternal() {
            if ((bool) value) {
                value = false;
                cursorSfx.Play();
                SendCommand();
            }
        }

        private unsafe void SendCommand() {
            CommandChangeRules cmd = new CommandChangeRules {
                EnabledChanges = ruleType,
            };

            switch (ruleType) {
            case CommandChangeRules.Rules.CustomPowerupsEnabled:
                cmd.CustomPowerupsEnabled = (bool) value;
                break;
            case CommandChangeRules.Rules.TeamsEnabled:
                cmd.TeamsEnabled = (bool) value;
                break;
            }

            QuantumGame game = QuantumRunner.DefaultGame;
            int slot = game.GetLocalPlayerSlots()[game.GetLocalPlayers().IndexOf(game.Frames.Predicted.Global->Host)];
            game.SendCommand(slot, cmd);
        }

        protected override void UpdateLabel() {
            TranslationManager tm = GlobalController.Instance.translationManager;
            if (value is bool boolValue) {
                label.text = labelPrefix + tm.GetTranslation(boolValue ? "ui.generic.on" : "ui.generic.off");
            }
        }
    }
}
