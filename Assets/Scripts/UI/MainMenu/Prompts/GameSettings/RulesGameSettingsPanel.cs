using NSMB.UI.Translation;
using Quantum;
using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

namespace NSMB.UI.MainMenu.Submenus.Prompts {
    public class RulesGameSettingsPanel : GameSettingsPanel {

        //---Properties
        public override GameObject DefaultSelection => currentTab.DefaultSelection;
        public override GameObject BackButton => currentTab.BackButton;
        
        //---Serialized Variables
        [SerializeField] private List<GamemodeTab> tabs;
        [SerializeField] private List<RuleTranslationKey> ruleTranslationKeys;

        //---Private Variables
        private GamemodeTab currentTab;
        private CommandChangeRules.Rules currentRule;

        public override void OnEnable() {
            QuantumGame game = QuantumRunner.DefaultGame;
            Frame f;
            if (game != null && (f = game.Frames.Predicted) != null) {
                EnableCorrectTab(f);
            }

            TranslationManager.OnLanguageChanged += OnLanguageChanged;
            base.OnEnable();
        }

        public void Awake() {
            QuantumEvent.Subscribe<EventRulesChanged>(this, OnRulesChanged, onlyIfActiveAndEnabled: true);    
        }

        public void OnDisable() {
            TranslationManager.OnLanguageChanged -= OnLanguageChanged;
        }

        public void SetCurrentTab(GamemodeTab newTab) {
            foreach (var tab in tabs) {
                tab.Root.SetActive(false);
            }
            newTab.Root.SetActive(true);
            currentTab = newTab;
            submenu.Canvas.EventSystem.SetSelectedGameObject(newTab.DefaultSelection);
        }

        public unsafe void EnableCorrectTab(Frame f) {
            foreach (var tab in tabs) {
                if (tab.Gamemode == f.Global->Rules.Gamemode) {
                    SetCurrentTab(tab);
                    break;
                }
            }
        }

        public unsafe void UpdateDescription(CommandChangeRules.Rules ruleType) {
            string key;
            if (ruleType == CommandChangeRules.Rules.Gamemode) {
                if (QuantumUnityDB.TryGetGlobalAsset(QuantumRunner.DefaultGame.Frames.Predicted.Global->Rules.Gamemode, out GamemodeAsset gamemode)) {
                    key = gamemode.DescriptionTranslationKey;
                } else {
                    key = "???";
                }
            } else {
                key = ruleTranslationKeys.FirstOrDefault(rtk => rtk.Rule == ruleType)?.TranslationKey ?? "";
            }
            currentTab.Description.text = GlobalController.Instance.translationManager.GetTranslation(key);
            currentRule = ruleType;
        }

        private void OnLanguageChanged(TranslationManager tm) {
            UpdateDescription(currentRule);
        }


        private void OnRulesChanged(EventRulesChanged e) {
            if (e.GamemodeChanged) {
                EnableCorrectTab(e.Game.Frames.Predicted);
            }
        }

        [Serializable]
        public class GamemodeTab {
            public AssetRef<GamemodeAsset> Gamemode;
            public GameObject Root, DefaultSelection, BackButton;
            public TMP_Text Description;
        }


        [Serializable]
        public class RuleTranslationKey {
            public CommandChangeRules.Rules Rule;
            public string TranslationKey;
        }
    }
}