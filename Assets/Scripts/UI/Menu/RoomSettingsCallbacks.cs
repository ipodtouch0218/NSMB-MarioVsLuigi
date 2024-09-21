using NSMB.Translation;
using Photon.Client;
using Photon.Realtime;
using Quantum;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static NSMB.Utils.NetworkUtils;

namespace NSMB.UI.MainMenu {
    public class RoomSettingsCallbacks : MonoBehaviour, IInRoomCallbacks {

        //---Serailized Variables
        [SerializeField] private TMP_Dropdown levelDropdown;
        [SerializeField] private TMP_InputField starsInputField, coinsInputField, livesInputField, timerInputField;
        [SerializeField] private TMP_Text playersCount, roomIdText, roomIdToggleButtonText;
        [SerializeField] private Slider playersSlider;
        [SerializeField] private Toggle privateEnabledToggle, timerEnabledToggle, livesEnabledToggle, drawEnabledToggle, teamsEnabledToggle, customPowerupsEnabledToggle;
        [SerializeField] private TeamChooser teamSelectorButton;

        //---Private Variables
        private bool isRoomCodeVisible;

        public void OnEnable() {
            TranslationManager.OnLanguageChanged += OnLanguageChanged;
            NetworkHandler.Client.AddCallbackTarget(this);
        }

        public void OnDisable() {
            TranslationManager.OnLanguageChanged -= OnLanguageChanged;
            NetworkHandler.Client.RemoveCallbackTarget(this);
        }

        public void UpdateAllSettings(Room roomData, bool level) {
            GetCustomProperty(roomData.CustomProperties, Enums.NetRoomProperties.IntProperties, out int v);
            IntegerProperties intProperties = v;

            ChangePrivate(!roomData.IsVisible);
            ChangeMaxPlayers(roomData.MaxPlayers);
            ChangeLevelIndex(intProperties.Level, level);
            ChangeStarRequirement(intProperties.StarRequirement);
            ChangeCoinRequirement(intProperties.CoinRequirement);
            ChangeLives(intProperties.Lives);
            ChangeTime(intProperties.Timer);
            // ChangeTeams(roomData.Teams);
            // ChangeDrawOnTimeUp(roomData.DrawOnTimeUp);
            // ChangeCustomPowerups(roomData.CustomPowerups);
            SetRoomIdVisibility(isRoomCodeVisible);

            if (MainMenuManager.Instance) {
                MainMenuManager.Instance.playerList.UpdateAllPlayerEntries(QuantumRunner.DefaultGame);
                MainMenuManager.Instance.UpdateStartGameButton();
            }
        }

        #region Level Index
        public void SetLevelIndex() {
            Room room = NetworkHandler.Client.CurrentRoom;
            GetCustomProperty(room.CustomProperties, Enums.NetRoomProperties.IntProperties, out int v);
            IntegerProperties properties = v;

            int oldValue = properties.Level;
            int newValue = levelDropdown.value;
            if (newValue == oldValue || newValue < 0) {
                ChangeLevelIndex(oldValue, false);
                return;
            }

            properties.Level = newValue;
            room.SetCustomProperties(new PhotonHashtable {
                [Enums.NetRoomProperties.IntProperties] = (int) properties
            });
        }
        private void ChangeLevelIndex(int index, bool changed) {
            levelDropdown.SetValueWithoutNotify(index);
            if (levelDropdown.value != index && MainMenuManager.Instance is MainMenuManager mm) {
                if (changed) {
                    ChatManager.Instance.AddSystemMessage("ui.inroom.chat.server.map", ChatManager.Red, "map", mm.maps[index].translationKey);
                }
                mm.PreviewLevel(index);
            }
        }
        #endregion

        #region Stars
        public void SetStarRequirement() {
            Room room = NetworkHandler.Client.CurrentRoom;
            GetCustomProperty(room.CustomProperties, Enums.NetRoomProperties.IntProperties, out int v);
            IntegerProperties properties = v;

            int oldValue = properties.StarRequirement;
            if (!int.TryParse(starsInputField.text, out int newValue)) {
                return;
            }

            newValue = Mathf.Clamp(newValue, 1, 25);

            if (oldValue == newValue) {
                ChangeStarRequirement(oldValue);
                return;
            }

            properties.StarRequirement = newValue;
            room.SetCustomProperties(new PhotonHashtable {
                [Enums.NetRoomProperties.IntProperties] = (int) properties
            });
        }
        private void ChangeStarRequirement(int stars) {
            starsInputField.SetTextWithoutNotify(stars.ToString());
        }
        #endregion

        #region Coins
        public void SetCoinRequirement() {
            Room room = NetworkHandler.Client.CurrentRoom;
            GetCustomProperty(room.CustomProperties, Enums.NetRoomProperties.IntProperties, out int v);
            IntegerProperties properties = v;

            int oldValue = properties.CoinRequirement;
            if (!int.TryParse(coinsInputField.text, out int newValue)) {
                return;
            }

            newValue = Mathf.Clamp(newValue, 3, 25);

            if (newValue == oldValue) {
                ChangeCoinRequirement(oldValue);
                return;
            }

            properties.CoinRequirement = newValue;
            room.SetCustomProperties(new PhotonHashtable {
                [Enums.NetRoomProperties.IntProperties] = (int) properties
            });
        }
        private void ChangeCoinRequirement(int coins) {
            coinsInputField.SetTextWithoutNotify(coins.ToString());
        }
        #endregion

        #region Lives
        public void SetLives() {
            Room room = NetworkHandler.Client.CurrentRoom;
            GetCustomProperty(room.CustomProperties, Enums.NetRoomProperties.IntProperties, out int v);
            IntegerProperties properties = v;

            int oldValue = properties.Lives;
            if (!int.TryParse(livesInputField.text, out int newValue)) {
                return;
            }

            newValue = Mathf.Clamp(newValue, 1, 25);

            if (newValue == oldValue) {
                ChangeLives(oldValue);
                return;
            }

            properties.Lives = newValue;
            room.SetCustomProperties(new PhotonHashtable {
                [Enums.NetRoomProperties.IntProperties] = (int) properties
            });
        }

        public void EnableLives() {
            Room room = NetworkHandler.Client.CurrentRoom;
            GetCustomProperty(room.CustomProperties, Enums.NetRoomProperties.IntProperties, out int v);
            IntegerProperties properties = v;

            int newValue = livesEnabledToggle.isOn ? int.Parse(livesInputField.text) : 0;

            properties.Lives = newValue;
            room.SetCustomProperties(new PhotonHashtable {
                [Enums.NetRoomProperties.IntProperties] = (int) properties
            });
        }

        private void ChangeLives(int lives) {
            bool enabled = lives > 0;
            livesEnabledToggle.SetIsOnWithoutNotify(enabled);
            livesInputField.interactable = enabled;

            if (enabled) {
                livesInputField.SetTextWithoutNotify(lives.ToString());
            }
        }
        #endregion

        #region Timer
        public void SetTime() {
            Room room = NetworkHandler.Client.CurrentRoom;
            GetCustomProperty(room.CustomProperties, Enums.NetRoomProperties.IntProperties, out int v);
            IntegerProperties properties = v;

            int oldValue = properties.Timer;
            if (!int.TryParse(timerInputField.text.Split(':')[0], out int newValue)) {
                return;
            }

            newValue = Mathf.Clamp(newValue, 1, 99);

            if (newValue == oldValue) {
                ChangeTime(oldValue);
                return;
            }

            properties.Timer = newValue;
            room.SetCustomProperties(new PhotonHashtable {
                [Enums.NetRoomProperties.IntProperties] = (int) properties
            });
        }

        public void EnableTime() {
            Room room = NetworkHandler.Client.CurrentRoom;
            GetCustomProperty(room.CustomProperties, Enums.NetRoomProperties.IntProperties, out int v);
            IntegerProperties properties = v;

            if (!int.TryParse(timerInputField.text.Split(':')[0], out int newValue)) {
                return;
            }

            newValue = timerEnabledToggle.isOn ? Mathf.Clamp(newValue, 1, 99) : 0;

            properties.Timer = newValue;
            room.SetCustomProperties(new PhotonHashtable {
                [Enums.NetRoomProperties.IntProperties] = (int) properties
            });
        }

        private void ChangeTime(int time) {
            timerEnabledToggle.SetIsOnWithoutNotify(time > 0);
            timerInputField.interactable = time > 0;
            drawEnabledToggle.interactable = time > 0;

            if (time <= 0) {
                return;
            }

            timerInputField.SetTextWithoutNotify($"{time}:00");
        }
        #endregion

        #region DrawOnTimeUp
        public void SetDrawOnTimeUp() {
            Room room = NetworkHandler.Client.CurrentRoom;
            GetCustomProperty(room.CustomProperties, Enums.NetRoomProperties.BoolProperties, out int v);
            BooleanProperties properties = v;

            bool newValue = drawEnabledToggle.isOn;

            properties.DrawOnTimeUp = newValue;
            room.SetCustomProperties(new PhotonHashtable {
                [Enums.NetRoomProperties.BoolProperties] = (int) properties
            });
        }
        private void ChangeDrawOnTimeUp(bool value) {
            drawEnabledToggle.SetIsOnWithoutNotify(value);
        }
        #endregion

        #region Teams
        public void SetTeams() {
            Room room = NetworkHandler.Client.CurrentRoom;
            GetCustomProperty(room.CustomProperties, Enums.NetRoomProperties.BoolProperties, out int v);
            BooleanProperties properties = v;

            bool newValue = teamsEnabledToggle.isOn;

            properties.Teams = newValue;
            room.SetCustomProperties(new PhotonHashtable {
                [Enums.NetRoomProperties.BoolProperties] = (int) properties
            });

            if (MainMenuManager.Instance) {
                MainMenuManager.Instance.playerList.UpdateAllPlayerEntries(QuantumRunner.DefaultGame);
            }
        }
        private void ChangeTeams(bool value) {
            teamsEnabledToggle.SetIsOnWithoutNotify(value);
            teamSelectorButton.SetEnabled(value);

            if (!teamsEnabledToggle.isOn && value) {
                MainMenuManager.Instance.playerList.UpdateAllPlayerEntries(QuantumRunner.DefaultGame);
            }
        }
        #endregion

        #region Custom Powerups
        public void SetCustomPowerups() {
            Room room = NetworkHandler.Client.CurrentRoom;
            GetCustomProperty(room.CustomProperties, Enums.NetRoomProperties.BoolProperties, out int v);
            BooleanProperties properties = v;

            bool newValue = customPowerupsEnabledToggle.isOn;

            properties.CustomPowerups = newValue;
            room.SetCustomProperties(new PhotonHashtable {
                [Enums.NetRoomProperties.BoolProperties] = (int) properties
            });
        }
        private void ChangeCustomPowerups(bool value) {
            customPowerupsEnabledToggle.SetIsOnWithoutNotify(value);
        }
        #endregion

        #region Players
        public void SetMaxPlayers() {
            Room room = NetworkHandler.Client.CurrentRoom;

            int oldValue = room.MaxPlayers;
            int newValue = (int) playersSlider.value;

            newValue = Mathf.Clamp(newValue, Mathf.Max(2, room.PlayerCount), 10);

            if (newValue == oldValue) {
                ChangeMaxPlayers(oldValue);
                return;
            }

            room.MaxPlayers = (byte) newValue;
            ChangeMaxPlayers(newValue);
        }
        private void ChangeMaxPlayers(int value) {
            playersSlider.SetValueWithoutNotify(value);
            playersCount.text = value.ToString();
        }
        #endregion

        /*
        #region Win Counter
        public void ClearWinCounters() {
            if (!Room.HasStateAuthority) {
                return;
            }

            foreach ((_, PlayerData data) in Room.PlayerDatas) {
                data.Wins = 0;
            }
        }
        #endregion
        */

        #region Private
        public void SetPrivate() {
            bool newValue = privateEnabledToggle.isOn;
            NetworkHandler.Client.CurrentRoom.IsVisible = !newValue;
        }
        private void ChangePrivate(bool value) {
            privateEnabledToggle.SetIsOnWithoutNotify(value);
        }
        public void CopyRoomCode() {
            TextEditor te = new() {
                text = NetworkHandler.Client.CurrentRoom.Name
            };
            te.SelectAll();
            te.Copy();
        }
        #endregion

        #region Room ID
        public void ToggleRoomIdVisibility() {
            SetRoomIdVisibility(!isRoomCodeVisible);
        }

        public void SetRoomIdVisibility(bool newValue) {
            isRoomCodeVisible = newValue;
            roomIdToggleButtonText.text = GlobalController.Instance.translationManager.GetTranslation(isRoomCodeVisible ? "ui.generic.hide" : "ui.generic.show");
            roomIdText.text = GlobalController.Instance.translationManager.GetTranslationWithReplacements("ui.inroom.settings.room.roomid", "id", isRoomCodeVisible ? NetworkHandler.Client.CurrentRoom.Name : "ui.inroom.settings.room.roomidhidden");
        }
        #endregion

        //---Callbacks
        private void OnLanguageChanged(TranslationManager tm) {
            SetRoomIdVisibility(isRoomCodeVisible);
            roomIdText.horizontalAlignment = tm.RightToLeft ? HorizontalAlignmentOptions.Right : HorizontalAlignmentOptions.Left;
        }

        public void OnPlayerEnteredRoom(Player newPlayer) { }

        public void OnPlayerLeftRoom(Player otherPlayer) { }

        public void OnRoomPropertiesUpdate(PhotonHashtable propertiesThatChanged) {
            UpdateAllSettings(NetworkHandler.Client.CurrentRoom, true);
        }

        public void OnPlayerPropertiesUpdate(Player targetPlayer, PhotonHashtable changedProps) { }

        public void OnMasterClientSwitched(Player newMasterClient) { }
    }
}
