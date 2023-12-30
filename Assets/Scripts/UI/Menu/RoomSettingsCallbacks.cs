using UnityEngine;
using UnityEngine.UI;
using TMPro;

using Fusion;
using NSMB.Translation;

namespace NSMB.UI.MainMenu {
    public class RoomSettingsCallbacks : MonoBehaviour {

        //---Serailized Variables
        [SerializeField] private TMP_Dropdown levelDropdown;
        [SerializeField] private TMP_InputField starsInputField, coinsInputField, livesInputField, timerInputField;
        [SerializeField] private TMP_Text playersCount, roomIdText, roomIdToggleButtonText;
        [SerializeField] private Slider playersSlider;
        [SerializeField] private Toggle privateEnabledToggle, timerEnabledToggle, livesEnabledToggle, drawEnabledToggle, teamsEnabledToggle, customPowerupsEnabledToggle;
        [SerializeField] private TeamChooser teamSelectorButton;

        //---Properties
        private NetworkRunner Runner => NetworkHandler.Runner;
        private SessionData Room => SessionData.Instance;

        //---Private Variables
        private bool isRoomCodeVisible;

        public void OnEnable() {
            TranslationManager.OnLanguageChanged += OnLanguageChanged;
        }

        public void OnDisable() {
            TranslationManager.OnLanguageChanged -= OnLanguageChanged;
        }

        public void UpdateAllSettings(SessionData roomData, bool level) {
            if (!roomData.Object) {
                return;
            }

            ChangePrivate(roomData.PrivateRoom);
            ChangeMaxPlayers(roomData.MaxPlayers);
            ChangeLevelIndex(roomData.Level, level);
            ChangeStarRequirement(roomData.StarRequirement);
            ChangeCoinRequirement(roomData.CoinRequirement);
            ChangeTeams(roomData.Teams);
            ChangeLives(roomData.Lives);
            ChangeTime(roomData.Timer);
            ChangeDrawOnTimeUp(roomData.DrawOnTimeUp);
            ChangeCustomPowerups(roomData.CustomPowerups);
            SetRoomIdVisibility(isRoomCodeVisible);

            if (MainMenuManager.Instance) {
                MainMenuManager.Instance.playerList.UpdateAllPlayerEntries();
                MainMenuManager.Instance.UpdateStartGameButton();
            }

            if (Runner.IsServer && Runner.Tick != 0) {
                Runner.PushHostMigrationSnapshot();
            }
        }

        #region Level Index
        public void SetLevelIndex() {
            if (!Runner.IsServer) {
                return;
            }

            int oldValue = Room.Level;
            int newValue = levelDropdown.value;
            if (newValue == oldValue || newValue < 0) {
                ChangeLevelIndex(oldValue, false);
                return;
            }

            Room.SetLevel((byte) newValue);
        }
        private void ChangeLevelIndex(int index, bool changed) {
            levelDropdown.SetValueWithoutNotify(index);
            if (changed && MainMenuManager.Instance is MainMenuManager mm) {
                ChatManager.Instance.AddSystemMessage("ui.inroom.chat.server.map", "map", mm.maps[index].translationKey);
                mm.PreviewLevel(index);
            }
        }
        #endregion

        #region Stars
        public void SetStarRequirement() {
            if (!Runner.IsServer) {
                return;
            }

            int oldValue = Room.StarRequirement;
            if (!int.TryParse(starsInputField.text, out int newValue)) {
                return;
            }

            newValue = Mathf.Clamp(newValue, 1, 25);

            if (oldValue == newValue) {
                ChangeStarRequirement(oldValue);
                return;
            }

            Room.SetStarRequirement((sbyte) newValue);
        }
        private void ChangeStarRequirement(int stars) {
            starsInputField.SetTextWithoutNotify(stars.ToString());
        }
        #endregion

        #region Coins
        public void SetCoinRequirement() {
            if (!Runner.IsServer) {
                return;
            }

            int oldValue = Room.CoinRequirement;
            if (!int.TryParse(coinsInputField.text, out int newValue)) {
                return;
            }

            newValue = Mathf.Clamp(newValue, 2, 25);

            if (newValue == oldValue) {
                ChangeCoinRequirement(oldValue);
                return;
            }

            Room.SetCoinRequirement((byte) newValue);
        }
        private void ChangeCoinRequirement(int coins) {
            coinsInputField.SetTextWithoutNotify(coins.ToString());
        }
        #endregion

        #region Lives
        public void SetLives() {
            if (!Runner.IsServer) {
                return;
            }

            int oldValue = Room.Lives;
            if (!int.TryParse(livesInputField.text, out int newValue)) {
                return;
            }

            newValue = Mathf.Clamp(newValue, 1, 25);

            if (newValue == oldValue) {
                ChangeLives(oldValue);
                return;
            }

            Room.SetLives((sbyte) newValue);
        }

        public void EnableLives() {
            if (!Runner.IsServer) {
                return;
            }

            int newValue = livesEnabledToggle.isOn ? int.Parse(livesInputField.text) : -1;

            Room.SetLives((sbyte) newValue);
        }

        private void ChangeLives(int lives) {
            bool enabled = lives != -1;
            livesEnabledToggle.SetIsOnWithoutNotify(enabled);
            livesInputField.interactable = enabled;

            if (enabled) {
                livesInputField.SetTextWithoutNotify(lives.ToString());
            }
        }
        #endregion

        #region Timer
        public void SetTime() {
            if (!Runner.IsServer) {
                return;
            }

            int oldValue = Room.Timer;
            if (!int.TryParse(timerInputField.text.Split(":")[0], out int newValue)) {
                return;
            }

            newValue = Mathf.Clamp(newValue, 1, 99);

            if (newValue == oldValue) {
                ChangeTime(oldValue);
                return;
            }

            Room.SetTimer((sbyte) newValue);
        }

        public void EnableTime() {
            if (!Runner.IsServer) {
                return;
            }

            if (!int.TryParse(timerInputField.text.Split(":")[0], out int newValue)) {
                return;
            }

            newValue = timerEnabledToggle.isOn ? Mathf.Clamp(newValue, 1, 99) : -1;

            Room.SetTimer((sbyte) newValue);
        }

        private void ChangeTime(int time) {
            timerEnabledToggle.SetIsOnWithoutNotify(time != -1);
            timerInputField.interactable = time != -1;
            drawEnabledToggle.interactable = time != -1;

            if (time == -1) {
                return;
            }

            timerInputField.SetTextWithoutNotify($"{time}:00");
        }
        #endregion

        #region DrawOnTimeUp
        public void SetDrawOnTimeUp() {
            if (!Runner.IsServer) {
                return;
            }

            bool newValue = drawEnabledToggle.isOn;

            Room.SetDrawOnTimeUp(newValue);
        }
        private void ChangeDrawOnTimeUp(bool value) {
            drawEnabledToggle.SetIsOnWithoutNotify(value);
        }
        #endregion

        #region Teams
        public void SetTeams() {
            if (!Runner.IsServer) {
                return;
            }

            bool newValue = teamsEnabledToggle.isOn;

            Room.SetTeams(newValue);

            if (MainMenuManager.Instance) {
                MainMenuManager.Instance.playerList.UpdateAllPlayerEntries();
            }
        }
        private void ChangeTeams(bool value) {
            teamsEnabledToggle.SetIsOnWithoutNotify(value);
            teamSelectorButton.SetEnabled(value);

            if (!teamsEnabledToggle.isOn && value) {
                MainMenuManager.Instance.playerList.UpdateAllPlayerEntries();
            }
        }
        #endregion

        #region Custom Powerups
        public void SetCustomPowerups() {
            if (!Runner.IsServer) {
                return;
            }

            bool newValue = customPowerupsEnabledToggle.isOn;

            Room.SetCustomPowerups(newValue);
        }
        private void ChangeCustomPowerups(bool value) {
            customPowerupsEnabledToggle.SetIsOnWithoutNotify(value);
        }
        #endregion

        #region Players
        public void SetMaxPlayers() {
            if (!Runner.IsServer) {
                return;
            }

            int oldValue = Room.MaxPlayers;
            int newValue = (int) playersSlider.value;

            newValue = Mathf.Clamp(newValue, Mathf.Max(2, Runner.SessionInfo.PlayerCount), 10);

            if (newValue == oldValue) {
                ChangeMaxPlayers(oldValue);
                return;
            }

            Room.SetMaxPlayers((byte) newValue);
            ChangeMaxPlayers(newValue);
        }
        private void ChangeMaxPlayers(int value) {
            playersSlider.SetValueWithoutNotify(value);
            playersCount.text = value.ToString();
        }
        #endregion

        #region Private
        public void SetPrivate() {
            if (!Runner.IsServer) {
                return;
            }

            bool newValue = privateEnabledToggle.isOn;

            Room.SetPrivateRoom(newValue);
        }
        private void ChangePrivate(bool value) {
            privateEnabledToggle.SetIsOnWithoutNotify(value);
        }
        public void CopyRoomCode() {
            TextEditor te = new() {
                text = Runner.SessionInfo.Name
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
            roomIdText.text = GlobalController.Instance.translationManager.GetTranslationWithReplacements("ui.inroom.settings.room.roomid", "id", isRoomCodeVisible ? Runner.SessionInfo.Name : "ui.inroom.settings.room.roomidhidden");
        }
        #endregion

        //---Callbacks
        private void OnLanguageChanged(TranslationManager tm) {
            SetRoomIdVisibility(isRoomCodeVisible);
            roomIdText.horizontalAlignment = tm.RightToLeft ? HorizontalAlignmentOptions.Right : HorizontalAlignmentOptions.Left;
        }
    }
}
