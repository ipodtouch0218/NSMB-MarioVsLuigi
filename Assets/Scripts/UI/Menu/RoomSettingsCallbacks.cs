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
    private LobbyData Lobby => LobbyData.Instance;

    public void UpdateAllSettings(LobbyData data, bool level) {
        if (!data.Object.IsValid)
            return;

        ChangePrivate(data.PrivateRoom);
        ChangeMaxPlayers(data.MaxPlayers);
        ChangeLevelIndex(data.Level, level);
        ChangeStarRequirement(data.StarRequirement);
        ChangeCoinRequirement(data.CoinRequirement);
        ChangeTeams(data.Teams);
        ChangeLives(data.Lives);
        ChangeTime(data.Timer);
        ChangeDrawOnTimeUp(data.DrawOnTimeUp);
        ChangeCustomPowerups(data.CustomPowerups);
    }

    #region Level Index
    public void SetLevelIndex() {
        if (!Runner.IsServer)
            return;

        int oldValue = Lobby.Level;
        int newValue = levelDropdown.value;
        if (newValue == oldValue || newValue < 0) {
            ChangeLevelIndex(oldValue, false);
            return;
        }

        Lobby.SetLevel((byte) newValue);
    }
    private void ChangeLevelIndex(int index, bool changed) {
        levelDropdown.SetValueWithoutNotify(index);
        if (changed) {
            MainMenuManager.Instance.chat.AddChatMessage("Map set to: " + levelDropdown.options[index].text, Color.red);
            Camera.main.transform.position = MainMenuManager.Instance.levelCameraPositions[index].transform.position;
        }
    }
    #endregion

    #region Stars
    public void SetStarRequirement() {
        if (!Runner.IsServer)
            return;

        int oldValue = Lobby.StarRequirement;
        int.TryParse(starsInputField.text, out int newValue);

        if (newValue == oldValue || newValue < 1 || newValue > 99) {
            ChangeStarRequirement(oldValue);
            return;
        }

        Lobby.SetStarRequirement((sbyte) newValue);
    }
    private void ChangeStarRequirement(int stars) {
        starsInputField.text = stars.ToString();
    }
    #endregion

    #region Coins
    public void SetCoinRequirement() {
        if (!Runner.IsServer)
            return;

        int oldValue = Lobby.CoinRequirement;
        int.TryParse(coinsInputField.text, out int newValue);

        if (newValue == oldValue || newValue < 1 || newValue > 99) {
            ChangeCoinRequirement(oldValue);
            return;
        }

        Lobby.SetCoinRequirement((byte) newValue);
    }
    private void ChangeCoinRequirement(int coins) {
        coinsInputField.text = coins.ToString();
    }
    #endregion

    #region Lives
    public void SetLives() {
        if (!Runner.IsServer)
            return;

        int oldValue = Lobby.Lives;
        int.TryParse(livesInputField.text, out int newValue);
        if (newValue == -1 || newValue < 1 || newValue > 99) {
            ChangeLives(oldValue);
            return;
        }

        Lobby.SetLives((sbyte) newValue);
    }
    public void EnableLives() {
        if (!Runner.IsServer)
            return;

        int newValue = livesEnabledToggle.isOn ? int.Parse(livesInputField.text) : -1;

        Lobby.SetLives((sbyte) newValue);
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

        int oldValue = Lobby.Timer;
        int newValue = Utils.ParseTimeToSeconds(timerInputField.text);

        if (newValue == oldValue || newValue < 1 || newValue > 99) {
            ChangeTime(oldValue);
            return;
        }

        Lobby.SetTimer(newValue);
    }
    public void EnableTime() {
        if (!Runner.IsServer)
            return;

        int newValue = Utils.ParseTimeToSeconds(timerInputField.text);
        newValue = timerEnabledToggle.isOn ? newValue : -1;

        Lobby.SetTimer(newValue);
    }
    private void ChangeTime(int time) {
        timerEnabledToggle.SetIsOnWithoutNotify(time != -1);
        timerInputField.interactable = time != -1;

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

        Lobby.SetDrawOnTimeUp(newValue);
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

        Lobby.SetTeams(newValue);

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

        Lobby.SetCustomPowerups(newValue);
    }
    private void ChangeCustomPowerups(bool value) {
        customPowerupsEnabledToggle.SetIsOnWithoutNotify(value);
    }
    #endregion

    #region Players
    public void SetMaxPlayers() {
        if (!Runner.IsServer)
            return;

        int oldValue = Lobby.MaxPlayers;
        int newValue = (int) playersSlider.value;
        int currentPlayers = Runner.SessionInfo.PlayerCount;

        if (newValue < currentPlayers)
            newValue = currentPlayers;

        newValue = Mathf.Clamp(newValue, 2, 10);

        if (newValue == oldValue) {
            ChangeMaxPlayers(oldValue);
            return;
        }

        Lobby.SetMaxPlayers((byte) newValue);
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

        Runner.SessionInfo.IsVisible = !newValue;
    }
    private void ChangePrivate(bool value) {
        privateEnabledToggle.SetIsOnWithoutNotify(value);
    }
    public void CopyRoomCode() {
        TextEditor te = new();
        te.text = Runner.SessionInfo.Name;
        te.SelectAll();
        te.Copy();
    }
    #endregion
}