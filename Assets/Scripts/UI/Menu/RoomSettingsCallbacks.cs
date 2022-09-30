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
    [SerializeField] private Toggle privateEnabledToggle, timerEnabledToggle, livesEnabledToggle, drawEnabledToggle, customPowerupsEnabledToggle;

    //---Properties
    private NetworkRunner Runner => NetworkHandler.Instance.runner;

    #region Level Index
    public void SetLevelIndex() {
        if (Runner.IsClient)
            return;

        Utils.GetSessionProperty(Runner.SessionInfo, Enums.NetRoomProperties.Level, out int oldValue);
        int newValue = levelDropdown.value;
        if (newValue == oldValue || newValue < 0) {
            ChangeLevel(oldValue);
            return;
        }

        Runner.SessionInfo.UpdateCustomProperties(new() {
            [Enums.NetRoomProperties.Level] = newValue
        });
        ChangeLevel(newValue);
    }
    private void ChangeLevel(int index) {
        levelDropdown.SetValueWithoutNotify(index);
        MainMenuManager.Instance.LocalChatMessage("Map set to: " + levelDropdown.options[index].text, Color.red);
        Camera.main.transform.position = MainMenuManager.Instance.levelCameraPositions[index].transform.position;
    }
    #endregion

    #region Stars
    public void SetStarRequirement() {
        if (!Runner.IsClient)
            return;

        Utils.GetSessionProperty(Runner.SessionInfo, Enums.NetRoomProperties.StarRequirement, out int oldValue);
        int.TryParse(starsInputField.text, out int newValue);

        if (newValue == oldValue || newValue < 1 || newValue > 99) {
            ChangeStarRequirement(oldValue);
            return;
        }

        Runner.SessionInfo.UpdateCustomProperties(new() {
            [Enums.NetRoomProperties.StarRequirement] = newValue
        });
        ChangeStarRequirement(newValue);
    }
    private void ChangeStarRequirement(int stars) {
        starsInputField.text = stars.ToString();
    }
    #endregion

    #region Coins
    public void SetCoinRequirement() {
        if (!Runner.IsClient)
            return;

        Utils.GetSessionProperty(Runner.SessionInfo, Enums.NetRoomProperties.CoinRequirement, out int oldValue);
        int.TryParse(coinsInputField.text, out int newValue);

        if (newValue == oldValue || newValue < 1 || newValue > 99) {
            ChangeCoinRequirement(oldValue);
            return;
        }

        Runner.SessionInfo.UpdateCustomProperties(new() {
            [Enums.NetRoomProperties.CoinRequirement] = newValue
        });
        ChangeCoinRequirement(newValue);
    }
    private void ChangeCoinRequirement(int coins) {
        coinsInputField.text = coins.ToString();
    }
    #endregion

    #region Lives
    public void SetLives() {
        if (Runner.IsClient)
            return;

        Utils.GetSessionProperty(Runner.SessionInfo, Enums.NetRoomProperties.Lives, out int oldValue);
        int.TryParse(livesInputField.text, out int newValue);
        if (newValue == -1 || newValue < 1 || newValue > 99) {
            ChangeLives(oldValue);
            return;
        }

        Runner.SessionInfo.UpdateCustomProperties(new() {
            [Enums.NetRoomProperties.Lives] = newValue
        });
        ChangeLives(newValue);
    }
    public void EnableLives() {
        if (Runner.IsClient)
            return;

        int newValue = livesEnabledToggle.isOn ? int.Parse(livesInputField.text) : -1;

        Runner.SessionInfo.UpdateCustomProperties(new() {
            [Enums.NetRoomProperties.Lives] = newValue
        });
        ChangeLives(newValue);
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
        if (Runner.IsClient)
            return;

        Utils.GetSessionProperty(Runner.SessionInfo, Enums.NetRoomProperties.Time, out int oldValue);
        int newValue = Utils.ParseTimeToSeconds(timerInputField.text);

        if (newValue == oldValue || newValue < 1 || newValue > 99) {
            ChangeTime(oldValue);
            return;
        }

        Runner.SessionInfo.UpdateCustomProperties(new() {
            [Enums.NetRoomProperties.Time] = newValue
        });
        ChangeTime(newValue);
    }
    public void EnableTime() {
        if (Runner.IsClient)
            return;

        int newValue = Utils.ParseTimeToSeconds(timerInputField.text);
        newValue = timerEnabledToggle.isOn ? newValue : -1;
        Runner.SessionInfo.UpdateCustomProperties(new() {
            [Enums.NetRoomProperties.Time] = newValue
        });
        ChangeTime(newValue);
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
        if (Runner.IsClient)
            return;

        bool newValue = drawEnabledToggle.isOn;
        Runner.SessionInfo.UpdateCustomProperties(new() {
            [Enums.NetRoomProperties.DrawTime] = newValue ? 1 : 0
        });
        ChangeDrawOnTimeUp(newValue);
    }
    private void ChangeDrawOnTimeUp(bool value) {
        drawEnabledToggle.SetIsOnWithoutNotify(value);
    }
    #endregion

    #region Custom Powerups
    public void SetCustomPowerups() {
        if (Runner.IsClient)
            return;

        bool newValue = customPowerupsEnabledToggle.isOn;
        Runner.SessionInfo.UpdateCustomProperties(new() {
            [Enums.NetRoomProperties.CustomPowerups] = newValue ? 1 : 0
        });
        ChangeCustomPowerups(newValue);
    }
    private void ChangeCustomPowerups(bool value) {
        customPowerupsEnabledToggle.SetIsOnWithoutNotify(value);
    }
    #endregion

    #region Players
    public void SetMaxPlayers() {
        if (Runner.IsClient)
            return;

        Utils.GetSessionProperty(Runner.SessionInfo, Enums.NetRoomProperties.MaxPlayers, out int oldValue);
        int newValue = (int) playersSlider.value;
        int currentPlayers = Runner.SessionInfo.PlayerCount;

        if (newValue < currentPlayers)
            newValue = currentPlayers;

        newValue = Mathf.Clamp(newValue, 0, 9);

        if (newValue == oldValue) {
            ChangeMaxPlayers(oldValue);
            return;
        }

        Runner.SessionInfo.UpdateCustomProperties(new() {
            [Enums.NetRoomProperties.MaxPlayers] = newValue
        });
        ChangeMaxPlayers(newValue);
    }
    private void ChangeMaxPlayers(int value) {
        playersSlider.SetValueWithoutNotify(value);
        playersCount.text = value.ToString();
    }
    #endregion

    #region Private
    public void SetPrivate() {
        if (Runner.IsClient)
            return;

        bool newValue = privateEnabledToggle.isOn;
        Runner.SessionInfo.IsVisible = !newValue;
        ChangePrivate(newValue);
        //PhotonNetwork.RaiseEvent((byte) Enums.NetEventIds.ChangePrivate, null, NetworkUtils.EventAll, SendOptions.SendReliable);
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