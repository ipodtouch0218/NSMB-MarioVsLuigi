using NSMB.UI.Translation;
using NSMB.Utilities.Extensions;
using Quantum;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace NSMB.UI.MainMenu.Submenus.Prompts {
    public class StageSelectionButton : Selectable, ISubmitHandler, IPointerClickHandler {

        //---Public Variables
        [NonSerialized] public Map map;
        [NonSerialized] public VersusStageData stage;

        //---Serialized Variables
        [SerializeField] private MainMenuCanvas canvas;
        [SerializeField] private ScrollRect scroll;
        [SerializeField] private Image stageImage;
        [SerializeField] private TMP_Text stageName, stageAuthor, stageComposer;

        public void Initialize(Map map, VersusStageData stage) {
            this.map = map;
            this.stage = stage;
            TranslationManager.OnLanguageChanged += OnLanguageChanged;
            UpdateText();
        }

        protected override void OnDestroy() {
            TranslationManager.OnLanguageChanged -= OnLanguageChanged;
        }

        public override void OnSelect(BaseEventData eventData) {
            base.OnSelect(eventData);
            scroll.verticalNormalizedPosition = scroll.ScrollToCenter((RectTransform) transform, false);
        }

        public unsafe void OnSubmit(BaseEventData eventData) {
            eventData.Use();

            CommandChangeRules cmd = new CommandChangeRules {
                EnabledChanges = CommandChangeRules.Rules.Stage,
                Stage = map
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

        public void OnPointerClick(PointerEventData eventData) {
            OnSubmit(eventData);
        }

        public void UpdateText() {
            stageImage.sprite = stage.Icon;
            stageName.text = GlobalController.Instance.translationManager.GetTranslation(stage.TranslationKey);

            stageAuthor.text = "";
            foreach (string split in stage.StageAuthor.Split(',')) {
                if (stageAuthor.text != "") {
                    stageAuthor.text += '\n';
                }
                stageAuthor.text += "<sprite name=level_author>" + split.Trim();
            }

            stageComposer.text = "";
            foreach (string split in stage.MusicComposer.Split(',')) {
                if (stageComposer.text != "") {
                    stageComposer.text += '\n';
                }
                stageComposer.text += "<sprite name=level_composer>" + split.Trim();
            }
        }

        private void OnLanguageChanged(TranslationManager tm) {
            UpdateText();
        }
    }
}
