using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SettingButtonManager : MonoBehaviour {

    private int prevWidth = 1280;
    private int prevHeight = 720;
    public bool ingameSettings;
    private Settings Settings => Settings.Instance;

    public void SetVolumeMusic(Slider slider) {
        Settings.VolumeMusic = slider.value;
        Settings.SaveSettingsToPreferences();
    }
    public void SetVolumeSFX(Slider slider) {
        Settings.VolumeSFX = slider.value;
        Settings.SaveSettingsToPreferences();
    }
    public void SetVolumeMaster(Slider slider) {
        Settings.VolumeMaster = slider.value;
        Settings.SaveSettingsToPreferences();
    }
    public void OnNdsResolutionToggle(Toggle toggle) {
        if (ingameSettings)
            GameManager.Instance.aspectToggle.interactable = Settings.ndsResolution = toggle.isOn;
        else
            MainMenuManager.Instance.aspectToggle.interactable = Settings.ndsResolution = toggle.isOn;
        Settings.SaveSettingsToPreferences();
    }

    public void OnAspectToggle(Toggle toggle) {
        Settings.fourByThreeRatio = toggle.isOn;
        Settings.SaveSettingsToPreferences();
    }

    public void OnFireballToggle(Toggle toggle) {
        Settings.fireballFromSprint = toggle.isOn;
        Settings.SaveSettingsToPreferences();
    }

    public void OnScoreboardToggle(Toggle toggle) {
        Settings.scoreboardAlways = toggle.isOn;
        Settings.SaveSettingsToPreferences();
    }

    public void OnChatFilterToggle(Toggle toggle) {
        Settings.filter = toggle.isOn;
        Settings.SaveSettingsToPreferences();
    }


    public void OnFullscreenToggle(Toggle toggle) {
        bool value = toggle.isOn;

        if (value) {
            prevWidth = Screen.width;
            prevHeight = Screen.height;
            Screen.SetResolution(Screen.mainWindowDisplayInfo.width, Screen.mainWindowDisplayInfo.height, FullScreenMode.FullScreenWindow);
        } else {
            Screen.SetResolution(prevWidth, prevHeight, FullScreenMode.Windowed);
        }
    }

    public void OnVsyncToggle(Toggle toggle) {
        Settings settings = Settings.Instance;
        settings.vsync = toggle.isOn;
        QualitySettings.vSyncCount = toggle.isOn ? 1 : 0;
        settings.SaveSettingsToPreferences();
    }
    public void SetMenuBackground(int index) {
        Settings.menuBackground = index;
        Settings.SaveSettingsToPreferences();
    }
    public void SetRedBackgroundColor(TMP_InputField input) {
        int.TryParse(input.text, out int newValue);
        if (newValue > 255) {
            newValue = 255;
            input.text = newValue.ToString();
        }
        if (newValue < 0) {
            newValue = 0;
            input.text = newValue.ToString();
        }
        if (input.text == null || input.text == "")
            input.text = "0";
        
        Settings.redBgColor = newValue;
        Settings.SaveSettingsToPreferences();
    }
    public void SetGreenBackgroundColor(TMP_InputField input) {
        int.TryParse(input.text, out int newValue);
        if (newValue > 255) {
            newValue = 255;
            input.text = newValue.ToString();
        }
        if (newValue < 0) {
            newValue = 0;
            input.text = newValue.ToString();
        }
        if (input.text == null || input.text == "")
            input.text = "0";

        Settings.greenBgColor = newValue;
        Settings.SaveSettingsToPreferences();
    }
    public void SetBlueBackgroundColor(TMP_InputField input) {
        int.TryParse(input.text, out int newValue);
        if (newValue > 255) {
            newValue = 255;
            input.text = newValue.ToString();
        }
        if (newValue < 0) {
            newValue = 0;
            input.text = newValue.ToString();
        }
        if (input.text == null || input.text == "")
            input.text = "0";

        Settings.blueBgColor = newValue;
        Settings.SaveSettingsToPreferences();
    }
    public void SetAlphaBackgroundColor(TMP_InputField input) {
        int.TryParse(input.text, out int newValue);
        if (newValue > 255) {
            newValue = 255;
            input.text = newValue.ToString();
        }
        if (newValue < 0) {
            newValue = 0;
            input.text = newValue.ToString();
        }
        if (input.text == null || input.text == "")
            input.text = "0";
        
        Settings.alphaBgColor = newValue;
        Settings.SaveSettingsToPreferences();
    }
}
