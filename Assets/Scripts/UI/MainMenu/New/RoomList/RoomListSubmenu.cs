using Photon.Realtime;
using Quantum;
using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NSMB.UI.MainMenu.Submenus {
    public class RoomListSubmenu : MainMenuSubmenu {

        //---Properties
        public override float BackHoldTime => (GlobalController.Instance.connecting.activeSelf || regionDropdown.IsExpanded) ? 0 : base.BackHoldTime;

        //---Serailized Variables
        [SerializeField] private TMP_Dropdown regionDropdown;
        [SerializeField] private RoomListManager roomManager;
        [SerializeField] private GameObject reconnectBtn, createRoomBtn, joinPrivateRoomBtn;
        [SerializeField] private SpriteChangingToggle filterInProgressRooms, filterFullRooms;
        [SerializeField] private TMP_InputField usernameField;
        [SerializeField] private MainMenuSubmenu inRoomSubmenu;
        [SerializeField] private RectTransform sideMenu;

        public override void Initialize(MainMenuCanvas canvas) {
            base.Initialize(canvas);
            NetworkHandler.StateChanged += OnClientStateChanged;
            QuantumCallback.Subscribe<CallbackLocalPlayerAddConfirmed>(this, OnLocalPlayerAddConfirmed);
        }

        public void OnEnable() {
            // This is needed for some bullshit where the "username" box
            // has zero height in builds. But not in the editor.
            // Because fuck you.
            LayoutRebuilder.ForceRebuildLayoutImmediate(sideMenu);
        }

        public void OnDestroy() {
            NetworkHandler.StateChanged -= OnClientStateChanged;
        }

        public override void Show(bool first) {
            base.Show(first);

            if (first) {
                // Attempt connection.
                roomManager.ClearRooms();
                _ = NetworkHandler.ConnectToRegion(null);
            }

            filterInProgressRooms.SetIsOnWithoutNotify(Settings.Instance.miscFilterInProgressRooms);
            roomManager.FilterInProgressRooms = Settings.Instance.miscFilterInProgressRooms;
            filterFullRooms.SetIsOnWithoutNotify(Settings.Instance.miscFilterFullRooms);
            roomManager.FilterFullRooms = Settings.Instance.miscFilterFullRooms;
            usernameField.text = Settings.Instance.generalNickname;
        }

        public override void Hide(SubmenuHideReason hideReason) {
            base.Hide(hideReason);

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

        public void ChangeFilterInProgress() {
            Settings.Instance.miscFilterInProgressRooms = filterInProgressRooms.isOn;
            Settings.Instance.SaveSettings();

            roomManager.FilterFullRooms = filterInProgressRooms.isOn;
        }

        public void ChangeFilterFull() {
            Settings.Instance.miscFilterFullRooms = filterFullRooms.isOn;
            Settings.Instance.SaveSettings();

            roomManager.FilterFullRooms = filterFullRooms.isOn;
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

                if (NetworkHandler.Regions != null) {
                    foreach (var option in regionDropdown.options) {
                        if (option is RegionOption ro) {
                            ro.Ping = NetworkHandler.Regions.ElementAt(ro.Index).Ping;
                        }
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

        private void OnLocalPlayerAddConfirmed(CallbackLocalPlayerAddConfirmed e) {
            Canvas.OpenMenu(inRoomSubmenu);
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
                    if (!GlobalController.Instance.translationManager.TryGetTranslation("region." + Region, out string translation)) {
                        translation = Region;
                    }
                    text = "<align=left>" + translation + "<line-height=0>\n<align=right><font=\"PauseFont\">" + _ping + "ms " + Utils.Utils.GetPingSymbol(_ping);
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