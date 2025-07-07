using NSMB.UI.Translation;
using Quantum;
using UnityEngine;

namespace NSMB.UI.MainMenu.Submenus.InRoom {
    public class NumberChangeableRule : ChangeableRule {

        //---Properties
        public override bool CanIncreaseValue => (int) value < maxValue;
        public override bool CanDecreaseValue => (int) value > minValue;

        //---Serialized Variables
        [SerializeField] protected int minValue = 0, maxValue = 20, step = 1;
        [SerializeField] protected bool minimumValueIsOff;

        protected override void IncreaseValueInternal() {
            int intValue = (int) value;
            value = Mathf.Clamp(intValue + step, minValue, maxValue);

            if (intValue != (int) value) {
                cursorSfx.Play();
                SendCommand();
            }
        }

        protected override void DecreaseValueInternal() {
            int intValue = (int) value;
            value = Mathf.Clamp(intValue - step, minValue, maxValue);

            if (intValue != (int) value) {
                cursorSfx.Play();
                SendCommand();
            }
        }

        private unsafe void SendCommand() {
            CommandChangeRules cmd = new CommandChangeRules {
                EnabledChanges = ruleType,
            };

            switch (ruleType) {
            case CommandChangeRules.Rules.StarsToWin:
                cmd.StarsToWin = (int) value;
                break;
            case CommandChangeRules.Rules.CoinsForPowerup:
                cmd.CoinsForPowerup = (int) value;
                break;
            case CommandChangeRules.Rules.Lives:
                cmd.Lives = (int) value;
                break;
            case CommandChangeRules.Rules.TimerMinutes:
                cmd.TimerMinutes = (int) value;
                break;
            }

            QuantumGame game = QuantumRunner.DefaultGame;
            int slot = game.GetLocalPlayerSlots()[game.GetLocalPlayers().IndexOf(game.Frames.Predicted.Global->Host)];
            game.SendCommand(slot, cmd);
        }

        protected override void UpdateLabel() {
            TranslationManager tm = GlobalController.Instance.translationManager;
            if (value is int intValue) {
                label.text = labelPrefix + ((minimumValueIsOff && intValue == minValue) ? tm.GetTranslation("ui.generic.off") : intValue);
            }
        }
    }
}