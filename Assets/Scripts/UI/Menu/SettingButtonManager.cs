using UnityEngine;
using UnityEngine.UI;

public class SettingButtonManager : MonoBehaviour {

    private int prevWidth = 1280;
    private int prevHeight = 720;

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
}
