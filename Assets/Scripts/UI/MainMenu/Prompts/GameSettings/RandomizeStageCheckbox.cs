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
    public class RandomizeStageCheckbox : MonoBehaviour, ISelectHandler
       {
        [SerializeField] private MainMenuCanvas canvas;
        [SerializeField] private ScrollRect scroll;


        private void Start() {
            GetComponent<Toggle>().onValueChanged.AddListener(OnValueChanged);

            QuantumEvent.Subscribe<EventPlayerAdded>(this, OnPlayerAdded);
            QuantumEvent.Subscribe<EventRulesChanged>(this, OnRulesChanged);
            QuantumEvent.Subscribe<EventGameEnded>(this, OnGameEnded);
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
                QuantumUtils.ChooseRandomLevel(game);
            } else {
                canvas.PlaySound(SoundEffect.UI_Error);
            }
        }

        public void OnSelect(BaseEventData eventData) {
            scroll.verticalNormalizedPosition = scroll.ScrollToCenter((RectTransform) transform, false);
        }

        private unsafe void OnPlayerAdded(EventPlayerAdded e) {
            if (e.Game.PlayerIsLocal(e.Player)) {
                 GetComponent<Toggle>().isOn = e.Game.Frames.Predicted.Global->Rules.RandomizeStage;
            }
        }

        private unsafe void OnRulesChanged(EventRulesChanged e) {
            GetComponent<Toggle>().isOn = e.Game.Frames.Predicted.Global->Rules.RandomizeStage;
        }

        private unsafe void OnGameEnded(EventGameEnded e) {
            if (e.Game.Frames.Predicted.Global->Rules.RandomizeStage) 
            {
                QuantumUtils.ChooseRandomLevel(e.Game);
            }
        }
    }
}
