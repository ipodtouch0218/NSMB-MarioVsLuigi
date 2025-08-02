using NSMB.UI.Translation;
using NSMB.Utilities.Extensions;
using Quantum;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace NSMB.UI.MainMenu.Submenus.Prompts {
    [RequireComponent(typeof(Toggle))]
    public class RandomizeStageCheckbox : MonoBehaviour
       {
        [SerializeField] private MainMenuCanvas canvas;

        private void Awake() {
            GetComponent<Toggle>().onValueChanged.AddListener(OnValueChanged);
        }
        private unsafe void OnValueChanged(bool newValue) 
        {
            CommandChangeRules cmd = new CommandChangeRules {
                EnabledChanges = CommandChangeRules.Rules.RandomizeStage,
                RandomizeStage = newValue
            };

            QuantumGame game = QuantumRunner.DefaultGame;
            int index = game.GetLocalPlayers().IndexOf(game.Frames.Predicted.Global->Host);
            if (index != -1) {
                int slot = game.GetLocalPlayerSlots()[index];
                game.SendCommand(slot, cmd);
                canvas.PlayConfirmSound();
            } else {
                canvas.PlaySound(SoundEffect.UI_Error);
            }
        }

        private unsafe void OnPlayerAdded(EventPlayerAdded e) {
            if (e.Game.PlayerIsLocal(e.Player)) {
                 GetComponent<Toggle>().Toggle(e.Game.Frames.Predicted.Global->Rules.RandomizeStage);
            }
        }

        private unsafe void OnRulesChanged(EventRulesChanged e) {
            GetComponent<Toggle>().Toggle(e.Game.Frames.Predicted.Global->Rules.RandomizeStage);
        }
    }
}
