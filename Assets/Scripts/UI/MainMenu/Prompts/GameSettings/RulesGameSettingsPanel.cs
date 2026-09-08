using Quantum;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Scripting;
using UnityEngine.UI;
using Navigation = UnityEngine.UI.Navigation;

namespace NSMB.UI.MainMenu.Submenus.Prompts {
    public unsafe class RulesGameSettingsPanel : GameSettingsPanel {
        
        //---Serialized Variables
        [SerializeField] private List<RulesGameSettingsPanelTab> tabs;
        [SerializeField] private GameObject advancedRulesButton;
        [SerializeField] private SelectablePromptLabel advancedRulesLabel;

        //---Private Variables
        private RulesGameSettingsPanelTab currentTab;
        private bool currentAdvanced;

        public override void OnEnable() {
            // Show the tab for the selected gamemode
            QuantumGame game = QuantumRunner.DefaultGame;
            if (game != null) {
                Frame f = game.Frames.Predicted;
                AssetRef<GamemodeAsset> gamemode = f.Global->Rules.Gamemode;
                currentAdvanced = false;
                ShowTabForGamemode(gamemode);
            }
        }

        public void Start() {
            QuantumEvent.Subscribe<EventRulesChanged>(this, OnRulesChanged);
        }

        private void ShowTabForGamemode(AssetRef<GamemodeAsset> gamemode) {
            ShowTab(tabs.First(tab => tab.Gamemode == gamemode), false);
        }

        private void ShowTab(RulesGameSettingsPanelTab newTab, bool advanced) {
            foreach (var tab in tabs) {
                foreach (var go in tab.NormalRules) {
                    go.SetActive(false);
                }
                foreach (var go in tab.AdvancedRules) {
                    go.SetActive(false);
                }
            }

            List<GameObject> enabledRules, disabledRules;
            if (advanced) {
                enabledRules = newTab.AdvancedRules;
                disabledRules = newTab.NormalRules;
            } else {
                enabledRules = newTab.NormalRules;
                disabledRules = newTab.AdvancedRules;
            }

            foreach (var go in disabledRules) {
                go.SetActive(false);
            }

            List<GameObject> finalRuleOrder = new();
            if (advanced || enabledRules.Count > 0) {
                if (advanced) {
                    // Put advanced options at top
                    finalRuleOrder.Add(advancedRulesButton);
                    finalRuleOrder.AddRange(enabledRules);
                } else {
                    // Put advanced options at bottom
                    finalRuleOrder.AddRange(enabledRules);
                    finalRuleOrder.Add(advancedRulesButton);
                }
            } else {
                // No advanced options button
                finalRuleOrder.AddRange(enabledRules);
            }

            Selectable previous = null;
            foreach (var go in finalRuleOrder) {
                go.SetActive(true);
                go.transform.SetAsLastSibling();

                Selectable selectable = go.GetComponentInChildren<Selectable>();
                if (selectable) {
                    Navigation nav = selectable.navigation;
                    nav.mode = Navigation.Mode.Explicit;
                    nav.selectOnUp = previous;
                    selectable.navigation = nav;

                    if (previous) {
                        nav = previous.navigation;
                        nav.selectOnDown = selectable;
                        previous.navigation = nav;
                    }

                    previous = selectable;
                }
            }

            Selectable backButtonSelectable = BackButton.GetComponentInChildren<Selectable>();
            Navigation backButtonNav = backButtonSelectable.navigation;
            backButtonNav.selectOnUp = previous;
            backButtonSelectable.navigation = backButtonNav;

            if (previous) {
                Navigation nav = previous.navigation;
                nav.selectOnDown = backButtonSelectable;
                previous.navigation = nav;
            }

            if (enabledRules.Count > 0) {
                submenu.Canvas.EventSystem.SetSelectedGameObject(finalRuleOrder[0].GetComponentInChildren<Selectable>().gameObject);
            } else {
                submenu.Canvas.EventSystem.SetSelectedGameObject(BackButton.GetComponentInChildren<Selectable>().gameObject);
            }

            currentAdvanced = advanced;
            currentTab = newTab;

            advancedRulesLabel.translationKey = advanced ? "ui.inroom.settings.game.advanced.hide" : "ui.inroom.settings.game.advanced.show";
            advancedRulesLabel.UpdateLabel();
        }

        [Preserve]
        public void ToggleAdvancedSettings() {
            ShowTab(currentTab, !currentAdvanced);
            submenu.Canvas.PlayConfirmSound();
            submenu.Canvas.EventSystem.SetSelectedGameObject(advancedRulesButton.GetComponentInChildren<Selectable>().gameObject);
        }

        private void OnRulesChanged(EventRulesChanged e) {
            if (e.GamemodeChanged) {
                AssetRef<GamemodeAsset> gamemode = e.Game.Frames.Predicted.Global->Rules.Gamemode;
                ShowTabForGamemode(gamemode);
            }
        }

        [Serializable]
        public class RulesGameSettingsPanelTab {
            public AssetRef<GamemodeAsset> Gamemode;
            public List<GameObject> NormalRules;
            public List<GameObject> AdvancedRules;
        }






        /*

        //---Serialized Variables
        [SerializeField] private List<RulesGameSettingsPanelTab> tabs;
        [SerializeField] private List<RulesGameSettingsPanelGamemodeTab> gamemodeTabs;

        //---Private Variables
        private RulesGameSettingsPanelTab currentTab, previousTab;
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

        public void SetCurrentTab(RulesGameSettingsPanelTab newTab) {
            foreach (var tab in tabs) {
                tab.Root.SetActive(false);
            }
            newTab.Root.SetActive(true);
            currentTab = newTab;
            submenu.Canvas.EventSystem.SetSelectedGameObject(newTab.DefaultSelection);
        }

        public unsafe void EnableCorrectTab(Frame f) {
            foreach (var tab in tabs) {
                if (tab is RulesGameSettingsPanelGamemodeTab gamemodeTab) {
                    if (gamemodeTab.GameMode == f.Global->Rules.Gamemode) {
                        SetCurrentTab(gamemodeTab);
                        break;
                    }
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



        public void EnableTab(RulesGameSettingsPanelTab tab) {
            foreach (var gameObject in currentTab.ActiveGameObjects) {
                gameObject.SetActive(false);
            }
            foreach (var gameObject in tab.ActiveGameObjects) {
                gameObject.SetActive(true);
            }
            currentTab = tab;
        }

        [Preserve]
        public void ToggleAdvancedOptions() {

        }

        [Serializable]
        public class RulesGameSettingsPanelTab {
            public List<GameObject> ActiveGameObjects;
        }

        [Serializable]
        public class RulesGameSettingsPanelGamemodeTab : RulesGameSettingsPanelTab {
            public AssetRef<GamemodeAsset> GameMode;
        }
        */
    }
}