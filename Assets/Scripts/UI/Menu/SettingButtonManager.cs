using UnityEngine;
using UnityEngine.UI;

public class SettingButtonManager : MonoBehaviour {

    //---Private Variables
    private int prevWidth = 1280;
    private int prevHeight = 720;

    private Settings Settings => Settings.Instance;


    //---GRAPHICS CALLBACKS
    public void Graphics_Option_FullscreenResolution() {

    }

    public void Graphics_Option_WindowMode() {

    }

    public void Graphics_Toggle_NDSResolution() {

    }

    public void Graphics_Toggle_NDSResolutionFourByThree() {

    }

    public void Graphics_Toggle_VSync() {

    }

    public void Graphics_Slider_MaxFramerate() {

    }

    public void Graphics_Toggle_PlayerOutlines() {

    }

    //---AUDIO CALLBACKS
    public void Audio_Slider_MasterVolume(Slider slider) {
        Settings.AudioMasterVolume = slider.value;
        Settings.SaveSettings();
    }

    public void Audio_Slider_MusicVolume(Slider slider) {
        Settings.AudioMusicVolume = slider.value;
        Settings.SaveSettings();
    }

    public void Audio_Slider_SFXVolume(Slider slider) {
        Settings.AudioSFXVolume = slider.value;
        Settings.SaveSettings();
    }

    public void Audio_Toggle_MuteMusicOnUnfocus() {

    }

    public void Audio_Toggle_MuteSFXOnUnfocus() {

    }

    public void Audio_Toggle_DirectionalAudio() {

    }



















        public void SetVolumeMusic(Slider slider) {
        Settings.AudioMusicVolume = slider.value;
        Settings.SaveSettings();
    }

    public void SetVolumeSFX(Slider slider) {
        Settings.AudioSFXVolume = slider.value;
        Settings.SaveSettings();
    }

    public void SetVolumeMaster(Slider slider) {
        Settings.AudioMasterVolume = slider.value;
        Settings.SaveSettings();
    }

    public void OnNdsResolutionToggle(Toggle toggle) {
        MainMenuManager.Instance.aspectToggle.interactable = Settings.graphicsNdsEnabled = toggle.isOn;
        Settings.SaveSettings();
    }

    public void OnAspectToggle(Toggle toggle) {
        Settings.graphicsNdsForceAspect = toggle.isOn;
        Settings.SaveSettings();
    }

    public void OnFireballToggle(Toggle toggle) {
        Settings.controlsFireballSprint = toggle.isOn;
        Settings.SaveSettings();
    }

    public void OnAutoSprintToggle(Toggle toggle) {
        Settings.controlsAutoSprint = toggle.isOn;
        Settings.SaveSettings();
    }

    public void OnScoreboardToggle(Toggle toggle) {
        Settings.genericScoreboardAlways = toggle.isOn;
        Settings.SaveSettings();
    }

    public void OnChatFilterToggle(Toggle toggle) {
        Settings.genericChatFiltering = toggle.isOn;
        Settings.SaveSettings();
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
        //settings.vsync = toggle.isOn;
        QualitySettings.vSyncCount = toggle.isOn ? 1 : 0;
        settings.SaveSettings();
    }
}
