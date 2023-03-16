using System;
using UnityEngine;
using UnityEngine.Audio;

using NSMB.Utils;
using System.Linq;
using UnityEngine.Rendering.Universal;

public class Settings : Singleton<Settings> {

    //---Static Variables
    private Action[] VersionUpdaters;

    //---Properties

    private float _audioMasterVolume;
    public float AudioMasterVolume {
        get => _audioMasterVolume;
        set => SetAndApplyVolume(ref _audioMasterVolume, value);
    }
    private float _audioMusicVolume;
    public float AudioMusicVolume {
        get => _audioMusicVolume;
        set => SetAndApplyVolume(ref _audioMusicVolume, value);
    }
    private float _audioSFXVolume;
    public float AudioSFXVolume {
        get => _audioSFXVolume;
        set => SetAndApplyVolume(ref _audioSFXVolume, value);
    }
    private void SetAndApplyVolume(ref float variable, float value) {
        variable = Mathf.Clamp01(value);
        ApplyVolumeSettings();
    }

    private string _graphicsFullscreenResolution;
    public string GraphicsFullscreenResolution {
        get {
            if (Screen.fullScreenMode == FullScreenMode.Windowed)
                return _graphicsFullscreenResolution;

            Resolution currentRes = Screen.fullScreenMode == FullScreenMode.Windowed ? Screen.resolutions[^1] : Screen.currentResolution;
            return currentRes.width + "," + currentRes.height;
        }
        set {
            _graphicsFullscreenResolution = value;
            if (Screen.fullScreenMode == FullScreenMode.Windowed)
                return;

            string[] split = value.Split(',');
            if (split.Length != 2)
                return;

            int width = int.Parse(split[0]);
            int height = int.Parse(split[1]);
            Screen.SetResolution(width, height, Screen.fullScreenMode);
        }
    }

    public int GraphicsFullscreenMode {
        get => (int) Screen.fullScreenMode;
        set => Screen.fullScreenMode = (FullScreenMode) value;
    }

    public bool GraphicsVsync {
        get => QualitySettings.vSyncCount == 1;
        set => QualitySettings.vSyncCount = (value ? 1 : 0);
    }

    public int GraphicsMaxFps {
        get => Application.targetFrameRate;
        set => Application.targetFrameRate = (value <= 0 ? -1 : value);
    }

    public bool GraphicsPlayerOutlines {
        get => GlobalController.Instance.outlineFeature.isActive;
        set => GlobalController.Instance.outlineFeature.SetActive(value);
    }

    public bool ValidNickname => genericNickname.IsValidUsername(false);

    //---Public Variables
    public string genericNickname;
    public int genericCharacter, genericSkin;
    public bool genericScoreboardAlways, genericChatFiltering;

    public bool graphicsNdsEnabled, graphicsNdsForceAspect;

    public bool audioMuteMusicOnUnfocus, audioMuteSFXOnUnfocus, audioPanning;

    public bool controlsFireballSprint, controlsAutoSprint;

    //---Private Variables
    [SerializeField] private AudioMixer mixer;

    public void Awake() => Set(this);
    public void OnDestroy() => Release();

    public void Start() {
        VersionUpdaters = new Action[] { LoadFromVersion0, LoadFromVersion1 };
        LoadSettings();
    }

    public void SaveSettings() {
        //Generic
        PlayerPrefs.SetString("Generic.Nickname", genericNickname);
        PlayerPrefs.SetInt("Generic.ScoreboardAlwaysVisible", genericScoreboardAlways ? 1 : 0);
        PlayerPrefs.SetInt("Generic.ChatFilter", genericChatFiltering ? 1 : 0);
        PlayerPrefs.SetInt("Generic.Character", genericCharacter);
        PlayerPrefs.SetInt("Generic.Skin", genericSkin);

        //Graphics
        PlayerPrefs.SetString("Graphics.FullscreenResolution", Screen.currentResolution.width + "," + Screen.currentResolution.height);
        PlayerPrefs.SetInt("Graphics.FullscreenMode", GraphicsFullscreenMode);
        PlayerPrefs.SetInt("Graphics.NDS.Enabled", graphicsNdsEnabled ? 1 : 0);
        PlayerPrefs.SetInt("Graphics.NDS.ForceAspect", graphicsNdsForceAspect ? 1 : 0);
        PlayerPrefs.SetInt("Graphics.VSync", GraphicsVsync ? 1 : 0);
        PlayerPrefs.SetInt("Graphics.MaxFPS", GraphicsMaxFps);
        PlayerPrefs.SetInt("Graphics.PlayerOutlines", GraphicsPlayerOutlines ? 1 : 0);

        //Audio
        PlayerPrefs.SetFloat("Audio.MasterVolume", AudioMasterVolume);
        PlayerPrefs.SetFloat("Audio.MusicVolume", AudioMusicVolume);
        PlayerPrefs.SetFloat("Audio.SFXVolume", AudioSFXVolume);
        PlayerPrefs.SetInt("Audio.MuteMusicOnUnfocus", audioMuteMusicOnUnfocus ? 1 : 0);
        PlayerPrefs.SetInt("Audio.MuteSFXOnUnfocus", audioMuteSFXOnUnfocus ? 1 : 0);
        PlayerPrefs.SetInt("Audio.Panning", audioPanning ? 1 : 0);

        //Controls
        PlayerPrefs.SetInt("Controls.FireballFromSprint", controlsFireballSprint ? 1 : 0);
        PlayerPrefs.SetInt("Controls.AutoSprint", controlsAutoSprint ? 1 : 0);
        PlayerPrefs.Save();
    }

    public void ApplyVolumeSettings() {
        mixer.SetFloat("MasterVolume", Mathf.Log10(AudioMasterVolume) * 20);
        mixer.SetFloat("MusicVolume", Mathf.Log10(AudioMusicVolume) * 20);
        mixer.SetFloat("SoundVolume", Mathf.Log10(AudioSFXVolume) * 20);
    }

    public void LoadSettings() {
        int version = PlayerPrefs.GetInt("version", 0);
        for (int i = version; i < VersionUpdaters.Count(); i++) {
            VersionUpdaters[i]();
            SaveSettings();
        }
    }

    private void LoadFromVersion0() {
        /*
         * PlayerPrefs.SetString("Nickname",             nickname);
         * PlayerPrefs.SetFloat("volumeSFX",             VolumeSFX);
         * PlayerPrefs.SetFloat("volumeMusic",           VolumeMusic);
         * PlayerPrefs.SetFloat("volumeMaster",          VolumeMaster);
         * PlayerPrefs.SetInt("NDSResolution",           ndsResolution ? 1 : 0);
         * PlayerPrefs.SetInt("FireballFromSprint",      fireballFromSprint ? 1 : 0);
         * PlayerPrefs.SetInt("AutoSprint",              autoSprint ? 1 : 0);
         * PlayerPrefs.SetInt("VSync",                   vsync ? 1 : 0);
         * PlayerPrefs.SetInt("NDS4by3",                 fourByThreeRatio ? 1 : 0);
         * PlayerPrefs.SetInt("ScoreboardAlwaysVisible", scoreboardAlways ? 1 : 0);
         * PlayerPrefs.SetInt("ChatFilter",              chatFiltering ? 1 : 0);
         * PlayerPrefs.SetInt("Character",               character);
         * PlayerPrefs.SetInt("Skin",                    skin);
         */

        genericNickname = PlayerPrefs.GetString("Nickname");
        genericScoreboardAlways = PlayerPrefs.GetInt("ScoreboardAlwaysVisible", 1) == 1;
        genericChatFiltering = PlayerPrefs.GetInt("ChatFilter", 1) == 1;
        genericCharacter = PlayerPrefs.GetInt("Character", 0);
        genericSkin = PlayerPrefs.GetInt("Skin", 0);

        GraphicsFullscreenResolution = Screen.resolutions[^1].width + "," + Screen.resolutions[^1].height;
        GraphicsFullscreenMode = (int) Screen.fullScreenMode;
        graphicsNdsEnabled = PlayerPrefs.GetInt("NDSResolution", 0) == 1;
        graphicsNdsForceAspect = PlayerPrefs.GetInt("NDS4by3", 0) == 1;
        GraphicsVsync = PlayerPrefs.GetInt("VSync", 0) == 1;
        GraphicsMaxFps = -1;
        GraphicsPlayerOutlines = true;

        AudioMasterVolume = PlayerPrefs.GetFloat("volumeMaster", 0.75f);
        AudioMusicVolume = PlayerPrefs.GetFloat("volumeMusic", 0.5f);
        AudioSFXVolume = PlayerPrefs.GetFloat("volumeSFX", 0.75f);
        audioMuteMusicOnUnfocus = false;
        audioMuteSFXOnUnfocus = false;
        audioPanning = true;

        controlsFireballSprint = PlayerPrefs.GetInt("FireballFromSprint", 1) == 1;
        controlsAutoSprint = false;
    }

    public void LoadFromVersion1() {
        //Generic
        GetIfExists("Generic.Nickname", out genericNickname);
        GetIfExists("Generic.ScoreboardAlwaysVisible", out genericScoreboardAlways);
        GetIfExists("Generic.ChatFilter", out genericChatFiltering);
        GetIfExists("Generic.Character", out genericCharacter);
        GetIfExists("Generic.Skin", out genericSkin);

        //Graphics
        if (GetIfExists("Graphics.FullscreenMode", out int tempFullscreenMode)) GraphicsFullscreenMode = tempFullscreenMode;
        if (GetIfExists("Graphics.FullscreenResolution", out string tempFullscreenResolution)) GraphicsFullscreenResolution = tempFullscreenResolution;
        GetIfExists("Graphics.NDS.Enabled", out graphicsNdsEnabled);
        GetIfExists("Graphics.NDS.ForceAspect", out graphicsNdsForceAspect);
        if (GetIfExists("Graphics.MaxFPS", out int tempMaxFps)) GraphicsMaxFps = tempMaxFps;
        if (GetIfExists("Graphics.VSync", out bool tempVsync)) GraphicsVsync = tempVsync;
        if (GetIfExists("Graphics.PlayerOutlines", out bool tempOutlines)) GraphicsPlayerOutlines = tempOutlines;

        //Audio
        if (GetIfExists("Audio.MasterVolume", out float tempMasterVolume)) AudioMasterVolume = tempMasterVolume;
        if (GetIfExists("Audio.MusicVolume", out float tempMusicVolume)) AudioMusicVolume = tempMusicVolume;
        if (GetIfExists("Audio.SFXVolume", out float tempSFXVolume)) AudioSFXVolume = tempSFXVolume;
        GetIfExists("Audio.MuteMusicOnUnfocus", out audioMuteMusicOnUnfocus);
        GetIfExists("Audio.MuteSFXOnUnfocus", out audioMuteSFXOnUnfocus);
        GetIfExists("Audio.Panning", out audioPanning);

        //Controls
        GetIfExists("Controls.FireballFromSprint", out controlsFireballSprint);
        GetIfExists("Controls.AutoSprint", out controlsAutoSprint);
    }

    private bool GetIfExists(string key, out string value) {
        if (!PlayerPrefs.HasKey(key)) {
            value = default;
            return false;
        }

        value = PlayerPrefs.GetString(key);
        return true;
    }

    private bool GetIfExists(string key, out int value) {
        if (!PlayerPrefs.HasKey(key)) {
            value = default;
            return false;
        }

        value = PlayerPrefs.GetInt(key);
        return true;
    }

    private bool GetIfExists(string key, out float value) {
        if (!PlayerPrefs.HasKey(key)) {
            value = default;
            return false;
        }

        value = PlayerPrefs.GetFloat(key);
        return true;
    }

    private bool GetIfExists(string key, out bool value) {
        if (!PlayerPrefs.HasKey(key)) {
            value = default;
            return false;
        }

        value = PlayerPrefs.GetInt(key) == 1;
        return true;
    }
}
