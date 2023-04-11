using UnityEngine;
using UnityEngine.UI;
using TMPro;

using Fusion;
using NSMB.Utils;

public class RoomSettingsCallbacks : MonoBehaviour {

    //---Serailized Variables
    [SerializeField] private TMP_Dropdown levelDropdown;
    [SerializeField] private TMP_InputField starsInputField, coinsInputField, livesInputField, timerInputField;
    [SerializeField] private TMP_Text playersCount;
    [SerializeField] private Slider playersSlider;
    [SerializeField] private Toggle privateEnabledToggle, timerEnabledToggle, livesEnabledToggle, drawEnabledToggle, teamsEnabledToggle, customPowerupsEnabledToggle;
    [SerializeField] private TeamChooser teamSelectorButton;

    //---Properties
    private NetworkRunner Runner => NetworkHandler.Runner;
    private SessionData Room => SessionData.Instance;

    public void UpdateAllSettings(SessionData roomData, bool level) {
        if (!roomData.Object.IsValid)
            return;

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

        if (MainMenuManager.Instance is MainMenuManager mm)
            mm.UpdateStartGameButton();
    }

    #region Level Index
    public void SetLevelIndex() {
        if (!Runner.IsServer)
            return;

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
            mm.chat.AddSystemMessage("ui.inroom.chat.server.map", "map", levelDropdown.options[index].text);
            mm.PreviewLevel(index);
        }
    }
    #endregion

    #region Stars
    public void SetStarRequirement() {
        if (!Runner.IsServer)
            return;

        int oldValue = Room.StarRequirement;
        int.TryParse(starsInputField.text, out int newValue);

        if (newValue == oldValue || newValue < 1 || newValue > 99) {
            ChangeStarRequirement(oldValue);
            return;
        }

        Room.SetStarRequirement((sbyte) newValue);
    }
    private void ChangeStarRequirement(int stars) {
        starsInputField.text = stars.ToString();
    }
    #endregion

    #region Coins
    public void SetCoinRequirement() {
        if (!Runner.IsServer)
            return;

        int oldValue = Room.CoinRequirement;
        int.TryParse(coinsInputField.text, out int newValue);

        if (newValue == oldValue || newValue < 1 || newValue > 99) {
            ChangeCoinRequirement(oldValue);
            return;
        }

        Room.SetCoinRequirement((byte) newValue);
    }
    private void ChangeCoinRequirement(int coins) {
        coinsInputField.text = coins.ToString();
    }
    #endregion

    #region Lives
    public void SetLives() {
        if (!Runner.IsServer)
            return;

        int oldValue = Room.Lives;
        int.TryParse(livesInputField.text, out int newValue);
        if (newValue == -1 || newValue < 1 || newValue > 99) {
            ChangeLives(oldValue);
            return;
        }

        Room.SetLives((sbyte) newValue);
    }
    public void EnableLives() {
        if (!Runner.IsServer)
            return;

        int newValue = livesEnabledToggle.isOn ? int.Parse(livesInputField.text) : -1;

        Room.SetLives((sbyte) newValue);
    }

    private void ChangeLives(int lives) {
        bool enabled = lives != -1;
        livesEnabledToggle.SetIsOnWithoutNotify(enabled);
        livesInputField.interactable = enabled;

        if (enabled)
            livesInputField.SetTextWithoutNotify(lives.ToString());
    }
    #endregion

    #region Timer
    public void SetTime() {
        if (!Runner.IsServer)
            return;

        int oldValue = Room.Timer;
        int newValue = Utils.ParseTimeToSeconds(timerInputField.text);

        if (newValue == oldValue || newValue < 1 || newValue > 99) {
            ChangeTime(oldValue);
            return;
        }

        Room.SetTimer(newValue);
    }
    public void EnableTime() {
        if (!Runner.IsServer)
            return;

        int newValue = Utils.ParseTimeToSeconds(timerInputField.text);
        newValue = timerEnabledToggle.isOn ? newValue : -1;

        Room.SetTimer(newValue);
    }
    private void ChangeTime(int time) {
        timerEnabledToggle.SetIsOnWithoutNotify(time != -1);
        timerInputField.interactable = time != -1;
        drawEnabledToggle.interactable = time != -1;

        if (time == -1)
            return;

        int minutes = time / 60;
        int seconds = time % 60;
        timerInputField.SetTextWithoutNotify($"{minutes}:{seconds:D2}");
    }
    #endregion

    #region DrawOnTimeUp
    public void SetDrawOnTimeUp() {
        if (!Runner.IsServer)
            return;

        bool newValue = drawEnabledToggle.isOn;

        Room.SetDrawOnTimeUp(newValue);
    }
    private void ChangeDrawOnTimeUp(bool value) {
        drawEnabledToggle.SetIsOnWithoutNotify(value);
    }
    #endregion

    #region Teams
    public void SetTeams() {
        if (!Runner.IsServer)
            return;

        bool newValue = teamsEnabledToggle.isOn;

        Room.SetTeams(newValue);

        if (MainMenuManager.Instance)
            MainMenuManager.Instance.playerList.UpdateAllPlayerEntries();
    }
    private void ChangeTeams(bool value) {
        teamsEnabledToggle.SetIsOnWithoutNotify(value);
        teamSelectorButton.SetEnabled(value);
    }
    #endregion

    #region Custom Powerups
    public void SetCustomPowerups() {
        if (!Runner.IsServer)
            return;

        bool newValue = customPowerupsEnabledToggle.isOn;

        Room.SetCustomPowerups(newValue);
    }
    private void ChangeCustomPowerups(bool value) {
        customPowerupsEnabledToggle.SetIsOnWithoutNotify(value);
    }
    #endregion

    #region Players
    public void SetMaxPlayers() {
        if (!Runner.IsServer)
            return;

        int oldValue = Room.MaxPlayers;
        int newValue = (int) playersSlider.value;
        int currentPlayers = Runner.SessionInfo.PlayerCount;

        if (newValue < currentPlayers)
            newValue = currentPlayers;

        newValue = Mathf.Clamp(newValue, 2, 10);

        if (newValue == oldValue) {
            ChangeMaxPlayers(oldValue);
            return;
        }

        Room.SetMaxPlayers((byte) newValue);
    }
    private void ChangeMaxPlayers(int value) {
        playersSlider.SetValueWithoutNotify(value);
        playersCount.text = value.ToString();
    }
    #endregion

    #region Private
    public void SetPrivate() {
        if (!Runner.IsServer)
            return;

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
}
