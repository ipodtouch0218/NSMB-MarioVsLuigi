using NSMB.Networking;
using NSMB.UI.Elements;
using NSMB.UI.MainMenu.Submenus.Prompts;
using NSMB.UI.Translation;
using NSMB.Utilities;
using Photon.Realtime;
using Quantum;
using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NSMB.UI.MainMenu.Submenus.RoomList {
    public class RoomListSubmenu : MainMenuSubmenu {

        //---Properties
        public override float BackHoldTime => (GlobalController.Instance.connecting.activeSelf || regionDropdown.IsExpanded) ? 0 : base.BackHoldTime;

        //---Serailized Variables
        [SerializeField] private TMP_Dropdown regionDropdown;
        [SerializeField] private RoomListManager roomManager;
        [SerializeField] private GameObject reconnectBtn, createRoomBtn, joinPrivateRoomBtn;
        [SerializeField] private TMP_InputField usernameField;
        [SerializeField] private SpriteChangingToggle filterInProgressRooms, filterFullRooms;
        [SerializeField] private MainMenuSubmenu inRoomSubmenu;
        [SerializeField] private ErrorPromptSubmenu errorSubmenu;
        [SerializeField] private RectTransform sideMenu;
        [SerializeField] private Color invalidUsernameColor;

        //---Private Variables
        private Color defaultUsernameColor;
        private bool overlayed;
        private bool kickedFromPreviousGame, bannedFromPreviousGame;
    
        public override void Initialize() {
            base.Initialize();
            defaultUsernameColor = usernameField.targetGraphic.color;

            NetworkHandler.StateChanged += OnClientStateChanged;
            QuantumEvent.Subscribe<EventPlayerKickedFromRoom>(this, OnPlayerKickedFromRoom);
            QuantumEvent.Subscribe<EventPlayerAdded>(this, OnPlayerAdded);
        }

        public void OnEnable() {
            // This is needed for some bullshit where the "username" box
            // has zero height in builds. But not in the editor.
            // Because fuck you.
            LayoutRebuilder.ForceRebuildLayoutImmediate(sideMenu);
        }

        public override void OnDestroy() {
            NetworkHandler.StateChanged -= OnClientStateChanged;
        }

        public override void Show(bool first) {
            base.Show(first);

            if (!overlayed) {
                Reconnect();
            }
            
            filterInProgressRooms.SetIsOnWithoutNotify(Settings.Instance.miscFilterInProgressRooms);
            roomManager.FilterInProgressRooms = Settings.Instance.miscFilterInProgressRooms;
            filterFullRooms.SetIsOnWithoutNotify(Settings.Instance.miscFilterFullRooms);
            roomManager.FilterFullRooms = Settings.Instance.miscFilterFullRooms;
            if (string.IsNullOrWhiteSpace(Settings.Instance.generalNickname)) {
                UnityEngine.Random.InitState((int) DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
                Settings.Instance.generalNickname = "Player" + UnityEngine.Random.Range(1000, 10000);
            }
            usernameField.text = Settings.Instance.generalNickname;

            if (kickedFromPreviousGame) {
                errorSubmenu.OpenWithString("ui.error.kicked", false);
            } else if (bannedFromPreviousGame) {
                errorSubmenu.OpenWithString("ui.error.banned", false);
            }

            kickedFromPreviousGame = false;
            bannedFromPreviousGame = false;
        }

        public override void Hide(SubmenuHideReason hideReason) {
            base.Hide(hideReason);

            overlayed = hideReason == SubmenuHideReason.Overlayed;
            if (hideReason == SubmenuHideReason.Closed) {
                // Disconnect
                _ = NetworkHandler.Disconnect();
            }
        }

        public override bool TryGoBack(out bool playSound) {
            if (GlobalController.Instance.connecting.activeSelf || regionDropdown.IsExpanded) {
                playSound = false;
                return false;
            }

            return base.TryGoBack(out playSound);
        }

        public void ChangeRegion() {
            RegionOption selectedRegion = (RegionOption) regionDropdown.options[regionDropdown.value];
            string targetRegion = selectedRegion.Region;
            if (NetworkHandler.Region == targetRegion) {
                return;
            }

            roomManager.ClearRooms();
            _ = NetworkHandler.ConnectToRegion(targetRegion);
        }

        public void ChangeUsername() {
            usernameField.targetGraphic.color = usernameField.text.IsValidNickname() ? defaultUsernameColor : invalidUsernameColor;
            Settings.Instance.generalNickname = usernameField.text;
            Settings.Instance.SaveSettings();
        }

        public void ChangeFilterInProgress() {
            Settings.Instance.miscFilterInProgressRooms = filterInProgressRooms.isOn;
            Settings.Instance.SaveSettings();

            roomManager.FilterInProgressRooms = filterInProgressRooms.isOn;
        }

        public void ChangeFilterFull() {
            Settings.Instance.miscFilterFullRooms = filterFullRooms.isOn;
            Settings.Instance.SaveSettings();

            roomManager.FilterFullRooms = filterFullRooms.isOn;
        }

        public void OpenMenuIfUsernameIsValid(MainMenuSubmenu submenu) {
            if (!Settings.Instance.generalNickname.IsValidNickname()) {
                InvalidUsername();
                return;
            }
            Canvas.OpenMenu(submenu);
        }

        public void InvalidUsername() {
            Canvas.PlaySound(SoundEffect.UI_Error);
            Canvas.EventSystem.SetSelectedGameObject(usernameField.gameObject);
        }

        public async void Reconnect() {
            roomManager.ClearRooms();
            await NetworkHandler.ConnectToRegion(null);
        }

        private void UpdateRegionDropdown() {
            if (NetworkHandler.Regions == null) {
                return;
            }

            if (regionDropdown.options.Count == 0) {
                // Create brand-new options
                int i = 0;
                foreach (var region in NetworkHandler.Regions) {
                    regionDropdown.options.Add(new RegionOption(i++, region.Code, region.Ping));
                }
                regionDropdown.options.Sort();
            } else {
                // Update existing options
                RegionOption selected = (RegionOption) regionDropdown.options[regionDropdown.value];

                foreach (var option in regionDropdown.options) {
                    if (option is RegionOption ro) {
                        ro.Ping = NetworkHandler.Regions.ElementAt(ro.Index).Ping;
                    }
                }
                regionDropdown.options.Sort();
                regionDropdown.SetValueWithoutNotify(regionDropdown.options.IndexOf(selected));
            }
        }

        private void OnClientStateChanged(ClientState oldState, ClientState newState) {
            switch (newState) {
            case ClientState.DisconnectingFromNameServer:
                // Add regions to dropdown
                UpdateRegionDropdown();
                break;
            case ClientState.ConnectedToMasterServer:
                // Change region dropdown
                int index =
                    regionDropdown.options
                        .Cast<RegionOption>()
                        .IndexOf(ro => ro.Region == NetworkHandler.Region);

                if (index != -1) {
                    regionDropdown.SetValueWithoutNotify(index);
                    regionDropdown.RefreshShownValue();
                }
                break;
            }

            reconnectBtn.SetActive(newState == ClientState.Disconnected);
            joinPrivateRoomBtn.SetActive(newState == ClientState.JoinedLobby);
            createRoomBtn.SetActive(newState == ClientState.JoinedLobby);
            /*
            UpdateNickname();
            */
        }

        private void OnPlayerAdded(EventPlayerAdded e) {
            if (e.Game.PlayerIsLocal(e.Player) && !Canvas.IsSubmenuOpen(inRoomSubmenu)) {
                Canvas.OpenMenu(inRoomSubmenu);
            }
        }

        private void OnPlayerKickedFromRoom(EventPlayerKickedFromRoom e) {
            if (e.Game.PlayerIsLocal(e.Player)) {
                kickedFromPreviousGame = !e.Banned;
                bannedFromPreviousGame = e.Banned;
            }
        }

        private class RegionOption : TMP_Dropdown.OptionData, IComparable {
            public int Index { get; }
            public string Region { get; }
            private int _ping = -1;
            public int Ping {
                get => _ping;
                set {
                    if (value <= 0) {
                        value = -1;
                    }

                    _ping = value;
                    TranslationManager tm = GlobalController.Instance.translationManager;
                    if (!tm.TryGetTranslation("region." + Region, out string translation)) {
                        translation = Region;
                    }

                    if (tm.RightToLeft) {
                        text = "<align=right>" + translation + "<line-height=0>\n<align=left><font=\"PauseFont\">" + _ping + "ms " + Utilities.Utils.GetPingSymbol(_ping);
                    } else {
                        text = "<align=left>" + translation + "<line-height=0>\n<align=right><font=\"PauseFont\">" + _ping + "ms " + Utilities.Utils.GetPingSymbol(_ping);
                    }
                }
            }

            public RegionOption(int index, string region, int ping) {
                Index = index;
                Region = region;
                Ping = ping;
            }

            public int CompareTo(object other) {
                if (other is not RegionOption ro) {
                    return -1;
                }

                if (Ping <= 0) {
                    return 1;
                }

                if (ro.Ping <= 0) {
                    return -1;
                }

                return Ping.CompareTo(ro.Ping);
            }
        }
    }
}