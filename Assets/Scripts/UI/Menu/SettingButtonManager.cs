using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System;
using System.Diagnostics;
using ExitGames.Client.Photon;

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
        MainMenuManager.Instance.aspectToggle.interactable = settings.ndsResolution = toggle.isOn;
        settings.SaveSettingsToPreferences();
    }

    public void OnAspectToggle(Toggle toggle) {
        Settings settings = Settings.Instance;
        settings.fourByThreeRatio = toggle.isOn;
        settings.SaveSettingsToPreferences();
    }

    public void OnFireballToggle(Toggle toggle) {
        Settings settings = Settings.Instance;
        settings.fireballFromSprint = toggle.isOn;
        settings.SaveSettingsToPreferences();
    }

    public void OnFullscreenToggle(Toggle toggle) {
        #if !UNITY_ANDROID
        {
            bool value = toggle.isOn;

            if (value)
            {
                prevWidth = Screen.width;
                prevHeight = Screen.height;
                Screen.SetResolution(Screen.mainWindowDisplayInfo.width, Screen.mainWindowDisplayInfo.height, FullScreenMode.FullScreenWindow);
            }
            else
            {
                Screen.SetResolution(prevWidth, prevHeight, FullScreenMode.Windowed);
            }
        }
        #endif

    }

        public void OnVsyncToggle(Toggle toggle) {
        Settings settings = Settings.Instance;
        settings.vsync = toggle.isOn;
        QualitySettings.vSyncCount = toggle.isOn ? 1 : 0;
        settings.SaveSettingsToPreferences();
    }
}
