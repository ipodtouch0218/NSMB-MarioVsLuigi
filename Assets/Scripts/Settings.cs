using System;
using System.IO;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.InputSystem;

using NSMB.Game;
using NSMB.Utils;

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

    public string GenericLocale {
        get => GlobalController.Instance.translationManager.CurrentLocale;
        set => GlobalController.Instance.translationManager.ChangeLanguage(value);
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
        set {
            FullScreenMode mode = (FullScreenMode) value;

            if (mode == FullScreenMode.Windowed) {
                if (Screen.fullScreenMode == FullScreenMode.Windowed)
                    return;

                Screen.SetResolution(GlobalController.Instance.windowWidth, GlobalController.Instance.windowHeight, mode);
                return;
            } else {
                string[] split = GraphicsFullscreenResolution.Split(',');
                if (split.Length != 2)
                    return;

                int width = int.Parse(split[0]);
                int height = int.Parse(split[1]);
                Screen.SetResolution(width, height, mode);
            }
        }
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

    private bool _graphicsPlayerNametags;
    public bool GraphicsPlayerNametags {
        get => _graphicsPlayerNametags;
        set {
            _graphicsPlayerNametags = value;
            if (GameManager.Instance)
                GameManager.Instance.nametagCanvas.gameObject.SetActive(value);
        }
    }

    public string ControlsBindings {
        get => ControlSystem.controls.asset.SaveBindingOverridesAsJson();
        set => ControlSystem.controls.asset.LoadBindingOverridesFromJson(value);
    }

    public bool ValidNickname => genericNickname.IsValidUsername(false);

    //---Public Variables
    public string genericNickname;
    public int genericCharacter, genericSkin;
    public bool genericScoreboardAlways, genericChatFiltering;

    public bool graphicsNdsEnabled, graphicsNdsForceAspect, graphicsNametags;

    public bool audioMuteMusicOnUnfocus, audioMuteSFXOnUnfocus, audioPanning;

    public bool controlsFireballSprint, controlsAutoSprint, controlsPropellerJump;

    //---Private Variables
    [SerializeField] private AudioMixer mixer;

    public void Awake() => Set(this);

    public void Start() {
        VersionUpdaters = new Action[] { LoadFromVersion0, LoadFromVersion1 };
        LoadSettings();

        // Potential duplicate bindings not activating fix?
        //InputSystem.settings.SetInternalFeatureFlag("DISABLE_SHORTCUT_SUPPORT", true);
    }

    public void SaveSettings() {
        // Generic
        PlayerPrefs.SetString("Generic_Nickname", genericNickname);
        PlayerPrefs.SetInt("Generic_ScoreboardAlwaysVisible", genericScoreboardAlways ? 1 : 0);
        PlayerPrefs.SetInt("Generic_ChatFilter", genericChatFiltering ? 1 : 0);
        PlayerPrefs.SetInt("Generic_Character", genericCharacter);
        PlayerPrefs.SetInt("Generic_Skin", genericSkin);
        PlayerPrefs.SetString("Generic_Locale", GenericLocale);

        // Graphics
        PlayerPrefs.SetString("Graphics_FullscreenResolution", Screen.currentResolution.width + "," + Screen.currentResolution.height);
        PlayerPrefs.SetInt("Graphics_FullscreenMode", GraphicsFullscreenMode);
        PlayerPrefs.SetInt("Graphics_NDS_Enabled", graphicsNdsEnabled ? 1 : 0);
        PlayerPrefs.SetInt("Graphics_NDS_ForceAspect", graphicsNdsForceAspect ? 1 : 0);
        PlayerPrefs.SetInt("Graphics_VSync", GraphicsVsync ? 1 : 0);
        PlayerPrefs.SetInt("Graphics_MaxFPS", GraphicsMaxFps);
        PlayerPrefs.SetInt("Graphics_PlayerOutlines", GraphicsPlayerOutlines ? 1 : 0);
        PlayerPrefs.SetInt("Graphics_Nametags", GraphicsPlayerNametags ? 1 : 0);

        // Audio
        PlayerPrefs.SetFloat("Audio_MasterVolume", AudioMasterVolume);
        PlayerPrefs.SetFloat("Audio_MusicVolume", AudioMusicVolume);
        PlayerPrefs.SetFloat("Audio_SFXVolume", AudioSFXVolume);
        PlayerPrefs.SetInt("Audio_MuteMusicOnUnfocus", audioMuteMusicOnUnfocus ? 1 : 0);
        PlayerPrefs.SetInt("Audio_MuteSFXOnUnfocus", audioMuteSFXOnUnfocus ? 1 : 0);
        PlayerPrefs.SetInt("Audio_Panning", audioPanning ? 1 : 0);

        // Controls
        PlayerPrefs.SetInt("Controls_FireballFromSprint", controlsFireballSprint ? 1 : 0);
        PlayerPrefs.SetInt("Controls_AutoSprint", controlsAutoSprint ? 1 : 0);
        PlayerPrefs.SetInt("Controls_PropellerJump", controlsPropellerJump ? 1 : 0);
        PlayerPrefs.SetString("Controls_Bindings", ControlsBindings);

        PlayerPrefs.Save();
    }

    public void ApplyVolumeSettings() {
        mixer.SetFloat("MasterVolume", Mathf.Log10(AudioMasterVolume) * 20);
        mixer.SetFloat("MusicVolume", Mathf.Log10(AudioMusicVolume) * 20);
        mixer.SetFloat("SoundVolume", Mathf.Log10(AudioSFXVolume) * 20);
    }

    public void LoadSettings() {
        for (int i = 0; i < VersionUpdaters.Length; i++) {
            VersionUpdaters[i]();
        }
        SaveSettings();
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
        GenericLocale = "en-US";

        GraphicsFullscreenResolution = Screen.resolutions[^1].width + "," + Screen.resolutions[^1].height;
        GraphicsFullscreenMode = (int) Screen.fullScreenMode;
        graphicsNdsEnabled = PlayerPrefs.GetInt("NDSResolution", 0) == 1;
        graphicsNdsForceAspect = PlayerPrefs.GetInt("NDS4by3", 0) == 1;
        GraphicsVsync = PlayerPrefs.GetInt("VSync", 0) == 1;
        GraphicsMaxFps = 0;
        GraphicsPlayerOutlines = true;
        GraphicsPlayerNametags = true;

        AudioMasterVolume = PlayerPrefs.GetFloat("volumeMaster", 0.75f);
        AudioMusicVolume = PlayerPrefs.GetFloat("volumeMusic", 0.5f);
        AudioSFXVolume = PlayerPrefs.GetFloat("volumeSFX", 0.75f);
        audioMuteMusicOnUnfocus = false;
        audioMuteSFXOnUnfocus = false;
        audioPanning = true;

        FileInfo bindingsFile = new(Application.persistentDataPath + "/controls.json");
        if (bindingsFile.Exists) {
            ControlsBindings = File.ReadAllText(bindingsFile.FullName);
            bindingsFile.Delete();
        }
        controlsFireballSprint = PlayerPrefs.GetInt("FireballFromSprint", 1) == 1;
        controlsAutoSprint = false;
        controlsPropellerJump = false;

        MassDeleteKeys("Nickname", "ScoreboardAlwaysVisible", "ChatFilter", "Character", "Skin", "NDSResolution",
            "NDS4by3", "VSync", "volumeMaster", "volumeMusic", "volumeSFX", "FireballFromSprint");
    }

    private void LoadFromVersion1() {
        //Generic
        GetIfExists("Generic_Nickname", out genericNickname);
        GetIfExists("Generic_ScoreboardAlwaysVisible", out genericScoreboardAlways);
        GetIfExists("Generic_ChatFilter", out genericChatFiltering);
        GetIfExists("Generic_Character", out genericCharacter);
        GetIfExists("Generic_Skin", out genericSkin);
        if (GetIfExists("Generic_Locale", out string tempLocale)) GenericLocale = tempLocale;

        //Graphics
        if (GetIfExists("Graphics_FullscreenMode", out int tempFullscreenMode)) GraphicsFullscreenMode = tempFullscreenMode;
        if (GetIfExists("Graphics_FullscreenResolution", out string tempFullscreenResolution)) GraphicsFullscreenResolution = tempFullscreenResolution;
        GetIfExists("Graphics_NDS_Enabled", out graphicsNdsEnabled);
        GetIfExists("Graphics_NDS_ForceAspect", out graphicsNdsForceAspect);
        if (GetIfExists("Graphics_MaxFPS", out int tempMaxFps)) GraphicsMaxFps = tempMaxFps;
        if (GetIfExists("Graphics_VSync", out bool tempVsync)) GraphicsVsync = tempVsync;
        if (GetIfExists("Graphics_PlayerOutlines", out bool tempOutlines)) GraphicsPlayerOutlines = tempOutlines;
        if (GetIfExists("Graphics_PlayerNametags", out bool tempNametags)) GraphicsPlayerNametags = tempNametags;

        //Audio
        if (GetIfExists("Audio_MasterVolume", out float tempMasterVolume)) AudioMasterVolume = tempMasterVolume;
        if (GetIfExists("Audio_MusicVolume", out float tempMusicVolume)) AudioMusicVolume = tempMusicVolume;
        if (GetIfExists("Audio_SFXVolume", out float tempSFXVolume)) AudioSFXVolume = tempSFXVolume;
        GetIfExists("Audio_MuteMusicOnUnfocus", out audioMuteMusicOnUnfocus);
        GetIfExists("Audio_MuteSFXOnUnfocus", out audioMuteSFXOnUnfocus);
        GetIfExists("Audio_Panning", out audioPanning);

        //Controls
        GetIfExists("Controls_FireballFromSprint", out controlsFireballSprint);
        GetIfExists("Controls_AutoSprint", out controlsAutoSprint);
        GetIfExists("Controls_PropellerJump", out controlsPropellerJump);
        if (GetIfExists("Controls_Bindings", out string tempBindings)) ControlsBindings = tempBindings;
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

    private void MassDeleteKeys(params string[] keys) {
        foreach (string key in keys)
            PlayerPrefs.DeleteKey(key);
        PlayerPrefs.Save();
    }
}
