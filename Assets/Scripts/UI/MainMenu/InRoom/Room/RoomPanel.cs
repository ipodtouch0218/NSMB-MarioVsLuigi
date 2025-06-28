using NSMB.UI.Translation;
using Quantum;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NSMB.UI.MainMenu.Submenus.InRoom {
    public class RoomPanel : InRoomSubmenuPanel {

        //---Properties
        public override bool IsInSubmenu => rules.Any(r => r.Editing);

        //---Serialized Variables
        [SerializeField] private Image stagePreviewImage;
        [SerializeField] private TMP_Text stageNameText;
        [SerializeField] private StagePreviewManager stagePreviewManager;
        [SerializeField] private MainMenuChat chat;

        //---Private Variables
        private readonly List<ChangeableRule> rules = new();
        private VersusStageData currentStage;

        public override void Initialize() {
            base.Initialize();
            GetComponentsInChildren(true, rules);
            foreach (var rule in rules) {
                rule.Initialize();
            }
            chat.Initialize();
        }

        public unsafe void Start() {
            var game = QuantumRunner.DefaultGame;
            if (game != null) {
                Frame f = game.Frames.Predicted;
                ChangeStage(f.FindAsset<VersusStageData>(f.FindAsset(f.Global->Rules.Stage).UserAsset));
            }

            QuantumCallback.Subscribe<CallbackGameStarted>(this, OnGameStarted);
            QuantumEvent.Subscribe<EventRulesChanged>(this, OnRulesChanged);
            TranslationManager.OnLanguageChanged += OnLanguageChanged;
        }

        public override void OnDestroy() {
            TranslationManager.OnLanguageChanged -= OnLanguageChanged;
            chat.OnDestroy();
        }

        public override bool TryGoBack(out bool playSound) {
            if (rules.Any(r => r.Editing)) {
                foreach (var rule in rules) {
                    rule.Editing = false;
                }
                playSound = true;
                return false;
            }
            return base.TryGoBack(out playSound);
        }

        private void ChangeStage(VersusStageData newStage) {
            stageNameText.text = GlobalController.Instance.translationManager.GetTranslation(newStage.TranslationKey);
            stagePreviewImage.sprite = newStage.Icon;
            currentStage = newStage;
        }

        private unsafe void OnGameStarted(CallbackGameStarted e) {
            Frame f = e.Game.Frames.Predicted;
            ChangeStage(f.FindAsset<VersusStageData>(f.FindAsset(f.Global->Rules.Stage).UserAsset));
        }

        private unsafe void OnRulesChanged(EventRulesChanged e) {
            Frame f = e.Game.Frames.Verified;
            ref GameRules rules = ref f.Global->Rules;

            if (e.MapChanged) {
                ChangeStage(f.FindAsset<VersusStageData>(f.FindAsset(rules.Stage).UserAsset));
            }
        }

        private void OnLanguageChanged(TranslationManager tm) {
            if (!currentStage) {
                return;
            }

            stageNameText.text = tm.GetTranslation(currentStage.TranslationKey);
        }
    }
}