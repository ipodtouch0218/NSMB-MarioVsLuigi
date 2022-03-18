using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class SettingButtonManager : MonoBehaviour {

    private Settings settings;
    private int prevWidth = 1280;
    private int prevHeight = 720;

    public void Start() {
        settings = Settings.Instance;
    }

    public void SetVolumeMusic(Slider slider) {
        settings.VolumeMusic = slider.value;
        settings.SaveSettingsToPreferences();
    }
    public void SetVolumeSFX(Slider slider) {
        settings.VolumeSFX = slider.value;
        settings.SaveSettingsToPreferences();
    }
    public void SetVolumeMaster(Slider slider) {
        settings.VolumeMaster = slider.value;
        settings.SaveSettingsToPreferences();
    }
    public void OnNdsResolutionToggle(Toggle toggle) {
        settings.ndsResolution = toggle.isOn;
        settings.SaveSettingsToPreferences();
    }
    public void OnFireballToggle(Toggle toggle) {
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

}
