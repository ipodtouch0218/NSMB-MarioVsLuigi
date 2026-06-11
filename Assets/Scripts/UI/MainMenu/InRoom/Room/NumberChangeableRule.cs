using NSMB.UI.Translation;
using Quantum;
using System;
using System.Linq;
using UnityEngine;

namespace NSMB.UI.MainMenu.Submenus.InRoom {
    public class NumberChangeableRule : ChangeableRule {

        //---Properties
        public override bool CanIncreaseValue => (int) value < maxValue;
        public override bool CanDecreaseValue => (int) value > minValue;

        //---Serialized Variables
        [SerializeField] protected int minValue = 0, maxValue = 20, step = 1;
        [SerializeField] protected bool minimumValueIsOff, applyPrefixSuffixWhenOff = true;
        [SerializeField] private NumberValueTranslationOverride[] translationOverrides;

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
            case CommandChangeRules.Rules.StarFountain:
                cmd.StarFountain = (int) value;
                break;
            case CommandChangeRules.Rules.CoinDeathPenalty:
                cmd.CoinDeathPenalty = (int) value;
                break;
            case CommandChangeRules.Rules.TeamAttack:
                cmd.TeamAttack = (int) value;
                break;
            }

            QuantumGame game = QuantumRunner.DefaultGame;
            PlayerRef host = game.Frames.Predicted.Global->Host;
            if (game.PlayerIsLocal(host)) {
                game.SendCommand(game.GetLocalPlayerSlots()[game.GetLocalPlayers().IndexOf(host)], cmd);
            }
        }

        protected override void UpdateLabel() {
            TranslationManager tm = GlobalController.Instance.translationManager;
            if (value is int intValue) {
                string text;
                bool applyPrefixSuffix;
                if (translationOverrides.FirstOrDefault(to => to.Value == intValue) is { } translationOverride) {
                    text = tm.GetTranslation(translationOverride.Key);
                    applyPrefixSuffix = false;
                } else {
                    if (minimumValueIsOff && intValue == minValue) {
                        text = tm.GetTranslation("ui.generic.off");
                        applyPrefixSuffix = applyPrefixSuffixWhenOff;
                    } else {
                        text = intValue.ToString();
                        applyPrefixSuffix = true;
                    }
                }

                if (applyPrefixSuffix) {
                    text = labelPrefix + text + labelSuffix;
                }
                label.text = text;
            }
        }

        [Serializable]
        public class NumberValueTranslationOverride {
            public int Value;
            public string Key;
        }
    }
}