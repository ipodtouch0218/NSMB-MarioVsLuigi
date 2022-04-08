using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class SettingButtonManager : MonoBehaviour {

    private int prevWidth = 1280;
    private int prevHeight = 720;

    public void SetVolumeMusic(Slider slider) {
        Settings settings = Settings.Instance;
        settings.VolumeMusic = slider.value;
        settings.SaveSettingsToPreferences();
    }
    public void SetVolumeSFX(Slider slider) {
        Settings settings = Settings.Instance;
        settings.VolumeSFX = slider.value;
        settings.SaveSettingsToPreferences();
    }
    public void SetVolumeMaster(Slider slider) {
        Settings settings = Settings.Instance;
        settings.VolumeMaster = slider.value;
        settings.SaveSettingsToPreferences();
    }
    public void OnNdsResolutionToggle(Toggle toggle) {
        Settings settings = Settings.Instance;
        settings.ndsResolution = toggle.isOn;
        settings.SaveSettingsToPreferences();
    }
    public void OnFireballToggle(Toggle toggle) {
        Settings settings = Settings.Instance;
        settings.fireballFromSprint = toggle.isOn;
        settings.SaveSettingsToPreferences();
    }
    public void OnFullscreenToggle(Toggle toggle) {
        bool value = toggle.isOn;
        if (value) {
            prevWidth = Screen.width;
            prevHeight = Screen.height;
            Resolution max = Screen.resolutions[^1];
            Screen.SetResolution(max.width, max.height, FullScreenMode.FullScreenWindow);
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
