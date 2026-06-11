using NSMB.UI.Translation;
using NSMB.Utilities.Extensions;
using Photon.Deterministic;
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
        [SerializeField] private Image stageImage, disabledImage;
        [SerializeField] private Material enabledMaterial, disabledMaterial;
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

        protected override void OnEnable() {
            base.OnEnable();
            UpdateEnabledVisuals();
        }

        protected override void Start() {
            base.Start();
            QuantumEvent.Subscribe<EventRandomStageToggled>(this, OnRandomStageToggled);
            QuantumEvent.Subscribe<EventRulesChanged>(this, OnRulesChanged);
        }

        public override void OnSelect(BaseEventData eventData) {
            base.OnSelect(eventData);
            scroll.verticalNormalizedPosition = scroll.ScrollToCenter((RectTransform) transform, false);
        }

        public unsafe void OnSubmit(BaseEventData eventData) {
            eventData.Use();

            QuantumGame game = QuantumRunner.DefaultGame;
            PlayerRef host = game.Frames.Predicted.Global->Host;
            if (!game.PlayerIsLocal(host)) {
                canvas.PlaySound(SoundEffect.UI_Error);
                return;
            }

            Frame f = game.Frames.Predicted;
            DeterministicCommand cmd;
            if (f.Global->Rules.ChooseMode == StageChooseMode.Choose) {
                cmd = new CommandChangeRules {
                    EnabledChanges = CommandChangeRules.Rules.Stage,
                    Stage = map
                };
            } else {
                cmd = new CommandToggleRandomStage {
                    Stage = map
                };
            }

            game.SendCommand(game.GetLocalPlayerSlots()[game.GetLocalPlayers().IndexOf(host)], cmd);
            canvas.PlayConfirmSound();
        }

        public void OnPointerClick(PointerEventData eventData) {
            OnSubmit(eventData);
        }

        private unsafe void UpdateEnabledVisuals(bool? isDisabledNullable = null) {
            QuantumGame game = QuantumRunner.DefaultGame;
            if (game == null) {
                SetEnableVisuals(true);
                return;
            }

            Frame f = QuantumRunner.DefaultGame.Frames.Predicted;
            if (f.Global->Rules.ChooseMode != StageChooseMode.Random) {
                SetEnableVisuals(true);
                return;
            } 
            
            if (!isDisabledNullable.HasValue) {
                if (f.TryResolveHashSet(f.Global->Rules.RandomDisabledStages, out var disabledStages)) {
                    isDisabledNullable = disabledStages.Contains(map);
                } else {
                    isDisabledNullable = false;
                }
            }

            SetEnableVisuals(!isDisabledNullable.Value);
        }

        private void SetEnableVisuals(bool enabled) {
            if (enabled) {
                stageImage.material = enabledMaterial;
                disabledImage.gameObject.SetActive(false);
            } else {
                stageImage.material = disabledMaterial;
                disabledImage.gameObject.SetActive(true);
            }
        }

        private void OnRulesChanged(EventRulesChanged e) {
            UpdateEnabledVisuals();
        }

        private void OnRandomStageToggled(EventRandomStageToggled e) {
            if (e.Stage != map) {
                return;
            }

            UpdateEnabledVisuals(e.IsDisabled);
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
