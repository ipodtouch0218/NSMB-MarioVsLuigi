using NSMB.Networking;
using NSMB.UI.MainMenu.Submenus.InRoom;
using NSMB.UI.Translation;
using Photon.Client;
using Photon.Realtime;
using Quantum;
using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace NSMB.UI.MainMenu.Submenus.Prompts {
    public class GameSettingsPromptSubmenu : PromptSubmenu, IInRoomCallbacks {

        //---Properties
        public bool RoomIdVisible {
            get => _roomIdVisible;
            set {
                roomIdLabel.text = value ? NetworkHandler.Client.CurrentRoom.Name : GlobalController.Instance.translationManager.GetTranslation("ui.inroom.settings.room.roomid.hidden");
                _roomIdVisible = value;
                LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform) roomIdLabel.transform.parent);
            }
        }
        public override GameObject BackButton => tabs[activeTab].BackButton;

        //---Serialized Variables
        [Header("Map Selection")]
        [SerializeField] private TMP_Text headerTemplate;
        [SerializeField] private GameObject horizontalTemplate;
        [SerializeField] private StageSelectionButton stageSelectionButtonTemplate;
        [SerializeField] private string[] headerOrder;

        [Header("Game Settings")]
        [SerializeField] private List<GameSettingsPanel> tabs;
        [SerializeField] private TMP_Text descriptionText;
        [SerializeField] private Color activeTabColor = Color.white, inactiveTabColor = Color.gray;

        [Header("Room Settings")]
        [SerializeField] private TMP_Text maxPlayerSliderValue;
        [SerializeField] private TMP_Text roomIdLabel;
        [SerializeField] private Slider maxPlayerSlider;
        [SerializeField] private Toggle privateToggle;

        //---Private Variables
        private int activeTab = 0;
        private readonly List<ChangeableRule> rules = new();
        private CommandChangeRules.Rules currentRule;
        private bool _roomIdVisible;

        public override void Initialize() {
            base.Initialize();

            GetComponentsInChildren(true, rules);
            foreach (var rule in rules) {
                rule.Initialize();
            }
            foreach (var tab in tabs) {
                tab.root.SetActive(false);
            }

            headerTemplate.gameObject.SetActive(false);
            horizontalTemplate.SetActive(false);
            stageSelectionButtonTemplate.gameObject.SetActive(false);

            var stages = GlobalController.Instance.config.AllStages;
            var stageGroups = stages.Select(QuantumUnityDB.GetGlobalAsset)
                .Select(m => (m, m ? (VersusStageData) QuantumUnityDB.GetGlobalAsset(m.UserAsset) : null))
                .Where(vsd => vsd.Item2)
                .GroupBy(vsd => vsd.Item2.GroupingTranslationKey).OrderBy(g => IndexOfNullIsMax(headerOrder, g.Key));

            TranslationManager tm = GlobalController.Instance.translationManager;
            List<StageSelectionButton> previousButtonRow = null;
            List<StageSelectionButton> currentButtonRow = null;
            foreach (var grouping in stageGroups) {
                TMP_Text newHeader = Instantiate(headerTemplate, headerTemplate.transform.parent);
                TMP_Translatable translatable = newHeader.GetComponent<TMP_Translatable>();
                translatable.key = grouping.Key;
                translatable.Run();
                newHeader.gameObject.SetActive(true);

                GameObject row = null;
                StageSelectionButton previousButton = null;
                int i = 0;
                foreach ((Map map, VersusStageData stage) in grouping) {
                    if ((i++) % 5 == 0) {
                        LinkButtonsAcrossRows(previousButtonRow, currentButtonRow);
                        previousButtonRow = currentButtonRow;
                        currentButtonRow = new();

                        row = Instantiate(horizontalTemplate, horizontalTemplate.transform.parent);
                        row.gameObject.SetActive(true);
                        previousButton = null;
                    }

                    StageSelectionButton newButton = Instantiate(stageSelectionButtonTemplate, row.transform);
                    newButton.Initialize(map, stage);
                    newButton.gameObject.SetActive(true);

                    if (previousButton) {
                        var prevNav = previousButton.navigation;
                        prevNav.selectOnRight = newButton;
                        previousButton.navigation = prevNav;

                        var newNav = newButton.navigation;
                        newNav.selectOnLeft = previousButton;
                        newButton.navigation = newNav;
                    }

                    previousButton = newButton;
                    currentButtonRow.Add(newButton);
                }
            }

            if (currentButtonRow != null) {
                LinkButtonsAcrossRows(previousButtonRow, currentButtonRow);

                var backButton = tabs[activeTab].BackButton.GetComponent<Selectable>();
                foreach (var button in currentButtonRow) {
                    var nav = button.navigation;
                    nav.selectOnDown = backButton;
                    button.navigation = nav;
                }

                var nav2 = backButton.navigation;
                nav2.selectOnUp = currentButtonRow[currentButtonRow.Count / 2];
                backButton.navigation = nav2;
            }
        }

        private int IndexOfNullIsMax<T>(IReadOnlyList<T> arr, T thing) where T : IComparable {
            int ret = arr.IndexOf(x => x.Equals(thing));
            if (ret == -1) {
                return int.MaxValue;
            }
            return ret;
        }

        private void LinkButtonsAcrossRows(List<StageSelectionButton> top, List<StageSelectionButton> bottom) {
            if (top == null || bottom == null) {
                return;
            }

            int row1Count = top.Count;
            int row2Count = bottom.Count;

            for (int x = 0; x < row1Count; x++) {
                int targetIndex = MapStageNavIndex(x, row1Count, row2Count);

                var nav = top[x].navigation;
                nav.selectOnDown = bottom[targetIndex];
                top[x].navigation = nav;
            }

            for (int x = 0; x < bottom.Count; x++) {
                int targetIndex = MapStageNavIndex(x, row2Count, row1Count);

                var nav = bottom[x].navigation;
                nav.selectOnUp = top[targetIndex];
                bottom[x].navigation = nav;
            }
        }

        public override void Show(bool first) {
            base.Show(first);

            Room currentRoom = NetworkHandler.Client.CurrentRoom;
            maxPlayerSlider.value = currentRoom.MaxPlayers;
            MaxPlayerSliderChanged();
            privateToggle.isOn = !currentRoom.IsVisible;
            RoomIdVisible = false;
            NetworkHandler.Client.AddCallbackTarget(this);

            Settings.Controls.UI.Next.performed += OnNext;
            Settings.Controls.UI.Previous.performed += OnPrevious;

            if (first) {
                ChangeTab(1, false);
            }
        }

        public override void Hide(SubmenuHideReason hideReason) {
            base.Hide(hideReason);
            NetworkHandler.Client.RemoveCallbackTarget(this);

            Settings.Controls.UI.Next.performed -= OnNext;
            Settings.Controls.UI.Previous.performed -= OnPrevious;
        }

        public void OpenOnTab(int tab) {
            Canvas.OpenMenu(this);
            ChangeTab(tab, false);
        }

        private void OnNext(InputAction.CallbackContext obj) {
            ChangeTab(activeTab + 1, true);
        }

        private void OnPrevious(InputAction.CallbackContext obj) {
            ChangeTab(activeTab - 1, true);
        }

        public void ChangeTabWithSound(int newTabIndex) {
            ChangeTab(newTabIndex, activeTab != newTabIndex);
        }

        public void AddTabWithSound(int increment) {
            ChangeTabWithSound(activeTab + increment);
        }

        public unsafe void ChangeTab(int newTabIndex, bool playSound) {
            if (newTabIndex < 0 || newTabIndex >= tabs.Count) {
                return;
            }

            var oldTab = tabs[activeTab];
            oldTab.header.color = inactiveTabColor;
            oldTab.root.SetActive(false);

            var newTab = tabs[newTabIndex];
            newTab.header.color = activeTabColor;
            newTab.root.SetActive(true);
            if (playSound) {
                Canvas.PlayCursorSound();
            }

            activeTab = newTabIndex;
        }

        private int MapStageNavIndex(int sourceIndex, int sourceCount, int targetCount) {
            float ratio = (float) (sourceIndex + 0.5f) / sourceCount;
            int mappedIndex = (int) (ratio * targetCount);
            return Math.Clamp(mappedIndex, 0, targetCount - 1);
        }

        public unsafe void PrivateToggleChanged() {
            Room currentRoom = NetworkHandler.Client.CurrentRoom;
            QuantumGame game = NetworkHandler.Game;
            PlayerRef host = game.Frames.Predicted.Global->Host;
            if (!game.PlayerIsLocal(host)) {
                Canvas.PlaySound(SoundEffect.UI_Error);
                privateToggle.isOn = !currentRoom.IsVisible;
                return;
            }

            currentRoom.IsVisible = !privateToggle.isOn;
            Canvas.PlayCursorSound();
        }

        public unsafe void MaxPlayerSliderChanged() {
            Room currentRoom = NetworkHandler.Client.CurrentRoom;
            QuantumGame game = NetworkHandler.Game;
            PlayerRef host = game.Frames.Predicted.Global->Host;
            if (!game.PlayerIsLocal(host)) {
                // Canvas.PlaySound(SoundEffect.UI_Error);
                maxPlayerSlider.SetValueWithoutNotify(currentRoom.MaxPlayers);
            } else {
                maxPlayerSlider.SetValueWithoutNotify(Mathf.Clamp((int) maxPlayerSlider.value, Mathf.Max(2, currentRoom.PlayerCount), Constants.MaxPlayers));
                currentRoom.MaxPlayers = (int) maxPlayerSlider.value;
            }

            maxPlayerSliderValue.text = ((int) maxPlayerSlider.value).ToString();
        }

        public void RoomIdClicked() {
            RoomIdVisible = !RoomIdVisible;
            Canvas.PlayCursorSound();
        }

        public void CopyRoomIdClicked() {
            TextEditor te = new TextEditor();
            te.text = NetworkHandler.Client.CurrentRoom.Name;
            te.SelectAll();
            te.Copy();
            Canvas.PlayConfirmSound();
        }

        public void OnPlayerEnteredRoom(Player newPlayer) { }

        public void OnPlayerLeftRoom(Player otherPlayer) { }

        public unsafe void OnRoomPropertiesUpdate(PhotonHashtable propertiesThatChanged) {
            Room currentRoom = NetworkHandler.Client.CurrentRoom;
            QuantumGame game = NetworkHandler.Game;
            PlayerRef host = game.Frames.Predicted.Global->Host;
            if (!game.PlayerIsLocal(host)) {
                maxPlayerSlider.SetValueWithoutNotify(currentRoom.MaxPlayers);
                privateToggle.SetIsOnWithoutNotify(!currentRoom.IsVisible);
                return;
            }
        }

        public void OnPlayerPropertiesUpdate(Player targetPlayer, PhotonHashtable changedProps) { }

        public void OnMasterClientSwitched(Player newMasterClient) { }
    }
}