using NSMB.UI.Game;
using System;
using System.IO;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.InputSystem;

public class Settings : Singleton<Settings> {

    //---Static Variables
    private Action[] VersionUpdaters;
    public static event Action OnColorblindModeChanged;
    public static event Action OnDisableChatChanged;
    public static event Action OnNdsBorderChanged;
    public static event Action<bool> OnInputDisplayActiveChanged;

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

    public string GeneralLocale {
        get => GlobalController.Instance.translationManager.CurrentLocale;
        set => GlobalController.Instance.translationManager.ChangeLanguage(value);
    }

    private bool _generalDisableChat;
    public bool GeneralDisableChat {
        get => _generalDisableChat;
        set {
            _generalDisableChat = value;
            OnDisableChatChanged?.Invoke();
        }
    }

    private bool _generalDiscordIntegration;
    public bool GeneralDiscordIntegration {
        get => _generalDiscordIntegration;
        set {
            _generalDiscordIntegration = value;
            GlobalController.Instance.discordController.UpdateActivity();
        }
    }


    private string _graphicsFullscreenResolution;
    public string GraphicsFullscreenResolution {
        get {
            if (Screen.fullScreenMode == FullScreenMode.Windowed) {
                return _graphicsFullscreenResolution;
            }

            Resolution currentRes = Screen.fullScreenMode == FullScreenMode.Windowed ? Screen.resolutions[^1] : Screen.currentResolution;
            return currentRes.width + "," + currentRes.height;
        }
        set {
            _graphicsFullscreenResolution = value;
            if (Screen.fullScreenMode == FullScreenMode.Windowed) {
                return;
            }

            string[] split = value.Split(',');
            if (split.Length != 2) {
                return;
            }

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
                if (Screen.fullScreenMode == FullScreenMode.Windowed) {
                    return;
                }

                Screen.SetResolution(GlobalController.Instance.windowWidth, GlobalController.Instance.windowHeight, mode);
                return;
            } else {
                string[] split = GraphicsFullscreenResolution.Split(',');
                if (split.Length != 2) {
                    return;
                }

                int width = int.Parse(split[0]);
                int height = int.Parse(split[1]);
                Screen.SetResolution(width, height, mode);
            }
        }
    }

    private int _graphicsNdsBorder = -1;
    public int GraphicsNdsBorder {
        get => _graphicsNdsBorder;
        set {
            int previous = _graphicsNdsBorder;
            _graphicsNdsBorder = value;

            if (previous != value) {
                OnNdsBorderChanged?.Invoke();
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
            foreach (var element in PlayerElements.AllPlayerElements) {
                element.nametagCanvas.SetActive(value);
            }
        }
    }

    private bool _graphicsColorblind;
    public bool GraphicsColorblind {
        get => _graphicsColorblind;
        set {
            bool oldValue = _graphicsColorblind;
            _graphicsColorblind = value;

            if (oldValue != value) {
                OnColorblindModeChanged?.Invoke();
            }
        }
    }

    private bool _graphicsInputDisplay;
    public bool GraphicsInputDisplay {
        get => _graphicsInputDisplay;
        set {
            bool oldValue = _graphicsInputDisplay;
            _graphicsInputDisplay = value;

            if (oldValue != value) {
                OnInputDisplayActiveChanged?.Invoke(value);
            }
        }
    }

    public string ControlsBindings {
        get => ControlSystem.controls.asset.SaveBindingOverridesAsJson();
        set => ControlSystem.controls.asset.LoadBindingOverridesFromJson(value);
    }

    //---Public Variables
    public string generalNickname;
    public int generalCharacter, generalPalette;
    public bool generalScoreboardAlways, generalChatFiltering, generalUseNicknameColor;

    public bool graphicsNdsEnabled, graphicsNdsForceAspect, graphicsNdsPixelPerfect, graphicsNametags;

    public Enums.SpecialPowerupMusic audioSpecialPowerupMusic;
    public bool audioMuteMusicOnUnfocus, audioMuteSFXOnUnfocus, audioPanning, audioRestartMusicOnDeath;

    public RumbleManager.RumbleSetting controlsRumble;
    public bool controlsFireballSprint, controlsAutoSprint, controlsPropellerJump;

    public bool miscFilterFullRooms, miscFilterInProgressRooms;

    //---Private Variables
    [SerializeField] private AudioMixer mixer;

    public void Awake() {
        Set(this);
    }

    public void Start() {
        VersionUpdaters = new Action[] { LoadFromVersion0, LoadFromVersion1 };
        LoadSettings();

        // Potential duplicate bindings not activating fix?
        InputSystem.settings.SetInternalFeatureFlag("DISABLE_SHORTCUT_SUPPORT", true);
    }

    public void SaveSettings() {
        // Generic
        PlayerPrefs.SetString("General_Nickname", generalNickname);
        PlayerPrefs.SetInt("General_ScoreboardAlwaysVisible", generalScoreboardAlways ? 1 : 0);
        PlayerPrefs.SetInt("General_DisableChat", GeneralDisableChat ? 1 : 0);
        PlayerPrefs.SetInt("General_ChatFilter", generalChatFiltering ? 1 : 0);
        PlayerPrefs.SetInt("General_Character", generalCharacter);
        PlayerPrefs.SetInt("General_Palette", generalPalette);
        PlayerPrefs.SetString("General_Locale", GeneralLocale);
        PlayerPrefs.SetInt("General_UseNicknameColor", generalUseNicknameColor ? 1 : 0);
        PlayerPrefs.SetInt("General_DiscordIntegration", GeneralDiscordIntegration ? 1 : 0);

        // Graphics
        PlayerPrefs.SetString("Graphics_FullscreenResolution", Screen.currentResolution.width + "," + Screen.currentResolution.height);
        PlayerPrefs.SetInt("Graphics_FullscreenMode", GraphicsFullscreenMode);
        PlayerPrefs.SetInt("Graphics_NDS_Enabled", graphicsNdsEnabled ? 1 : 0);
        PlayerPrefs.SetInt("Graphics_NDS_ForceAspect", graphicsNdsForceAspect ? 1 : 0);
        PlayerPrefs.SetInt("Graphics_NDS_PixelPerfect", graphicsNdsPixelPerfect ? 1 : 0);
        PlayerPrefs.SetInt("Graphics_NDS_Border", GraphicsNdsBorder);
        PlayerPrefs.SetInt("Graphics_VSync", GraphicsVsync ? 1 : 0);
        PlayerPrefs.SetInt("Graphics_MaxFPS", GraphicsMaxFps);
        PlayerPrefs.SetInt("Graphics_PlayerOutlines", GraphicsPlayerOutlines ? 1 : 0);
        PlayerPrefs.SetInt("Graphics_Nametags", GraphicsPlayerNametags ? 1 : 0);
        PlayerPrefs.SetInt("Graphics_Colorblind", GraphicsColorblind ? 1 : 0);
        PlayerPrefs.SetInt("Graphics_InputDisplay", GraphicsInputDisplay ? 1 : 0);

        // Audio
        PlayerPrefs.SetFloat("Audio_MasterVolume", AudioMasterVolume);
        PlayerPrefs.SetFloat("Audio_MusicVolume", AudioMusicVolume);
        PlayerPrefs.SetFloat("Audio_SFXVolume", AudioSFXVolume);
        PlayerPrefs.SetInt("Audio_MuteMusicOnUnfocus", audioMuteMusicOnUnfocus ? 1 : 0);
        PlayerPrefs.SetInt("Audio_MuteSFXOnUnfocus", audioMuteSFXOnUnfocus ? 1 : 0);
        PlayerPrefs.SetInt("Audio_Panning", audioPanning ? 1 : 0);
        PlayerPrefs.SetInt("Audio_RestartMusicOnDeath", audioRestartMusicOnDeath ? 1 : 0);
        PlayerPrefs.SetInt("Audio_SpecialPowerupMusic", (int) audioSpecialPowerupMusic);

        // Controls
        PlayerPrefs.SetInt("Controls_FireballFromSprint", controlsFireballSprint ? 1 : 0);
        PlayerPrefs.SetInt("Controls_AutoSprint", controlsAutoSprint ? 1 : 0);
        PlayerPrefs.SetInt("Controls_PropellerJump", controlsPropellerJump ? 1 : 0);
        PlayerPrefs.SetInt("Controls_Rumble", (int) controlsRumble);
        PlayerPrefs.SetString("Controls_Bindings", ControlsBindings);

        // Misc
        PlayerPrefs.SetInt("Misc_FilterFullRooms", miscFilterFullRooms ? 1 : 0);
        PlayerPrefs.SetInt("Misc_FilterInProgressRooms", miscFilterInProgressRooms ? 1 : 0);

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

        generalNickname = PlayerPrefs.GetString("Nickname");
        generalScoreboardAlways = PlayerPrefs.GetInt("ScoreboardAlwaysVisible", 1) != 0;
        GeneralDisableChat = false;
        generalChatFiltering = PlayerPrefs.GetInt("ChatFilter", 1) != 0;
        generalCharacter = PlayerPrefs.GetInt("Character", 0);
        generalPalette = PlayerPrefs.GetInt("Skin", 0);
        GeneralLocale = "en-US";
        generalUseNicknameColor = true;
        _generalDiscordIntegration = true;

        GraphicsFullscreenResolution = Screen.resolutions[^1].width + "," + Screen.resolutions[^1].height;
        GraphicsFullscreenMode = (int) Screen.fullScreenMode;
        graphicsNdsEnabled = PlayerPrefs.GetInt("NDSResolution", 0) != 0;
        graphicsNdsForceAspect = PlayerPrefs.GetInt("NDS4by3", 0) != 0;
        graphicsNdsPixelPerfect = false;
        GraphicsNdsBorder = 1;
        GraphicsVsync = PlayerPrefs.GetInt("VSync", 0) != 0;
        GraphicsMaxFps = 0;
        GraphicsPlayerOutlines = true;
        GraphicsPlayerNametags = true;
        GraphicsColorblind = false;
        GraphicsInputDisplay = false;

        AudioMasterVolume = PlayerPrefs.GetFloat("volumeMaster", 0.75f);
        AudioMusicVolume = PlayerPrefs.GetFloat("volumeMusic", 0.5f);
        AudioSFXVolume = PlayerPrefs.GetFloat("volumeSFX", 0.75f);
        audioMuteMusicOnUnfocus = false;
        audioMuteSFXOnUnfocus = false;
        audioPanning = true;
        audioRestartMusicOnDeath = false;
        audioSpecialPowerupMusic = Enums.SpecialPowerupMusic.Starman | Enums.SpecialPowerupMusic.MegaMushroom;

        FileInfo bindingsFile = new(Application.persistentDataPath + "/controls.json");
        if (bindingsFile.Exists) {
            ControlsBindings = File.ReadAllText(bindingsFile.FullName);
            bindingsFile.Delete();
        }
        controlsRumble = RumbleManager.RumbleSetting.None;
        controlsFireballSprint = PlayerPrefs.GetInt("FireballFromSprint", 1) == 1;
        controlsAutoSprint = false;
        controlsPropellerJump = false;

        miscFilterFullRooms = false;
        miscFilterInProgressRooms = false;

        MassDeleteKeys("Nickname", "ScoreboardAlwaysVisible", "ChatFilter", "Character", "Skin", "NDSResolution",
            "NDS4by3", "VSync", "volumeMaster", "volumeMusic", "volumeSFX", "FireballFromSprint");
    }

    private void LoadFromVersion1() {
        // Generic
        TryGetSetting("General_Nickname", ref generalNickname);
        TryGetSetting("General_ScoreboardAlwaysVisible", ref generalScoreboardAlways);
        TryGetSetting<bool>("General_DisableChat", nameof(GeneralDisableChat));
        TryGetSetting("General_ChatFilter", ref generalChatFiltering);
        TryGetSetting("General_Character", ref generalCharacter);
        TryGetSetting("General_Palette", ref generalPalette);
        TryGetSetting<string>("General_Locale", nameof(GeneralLocale));
        TryGetSetting("General_UseNicknameColor", ref generalUseNicknameColor);
        TryGetSetting<bool>("General_DiscordIntegration", nameof(GeneralDiscordIntegration));

        // Graphics
        TryGetSetting<int>("Graphics_FullscreenMode", nameof(GraphicsFullscreenMode));
        TryGetSetting<string>("Graphics_FullscreenResolution", nameof(GraphicsFullscreenResolution));
        TryGetSetting("Graphics_NDS_Enabled", ref graphicsNdsEnabled);
        TryGetSetting("Graphics_NDS_ForceAspect", ref graphicsNdsForceAspect);
        TryGetSetting("Graphics_NDS_PixelPerfect", ref graphicsNdsPixelPerfect);
        TryGetSetting<int>("Graphics_NDS_Border", nameof(GraphicsNdsBorder));
        TryGetSetting<int>("Graphics_MaxFPS", nameof(GraphicsMaxFps));
        TryGetSetting<bool>("Graphics_VSync", nameof(GraphicsVsync));
        TryGetSetting<bool>("Graphics_PlayerOutlines", nameof(GraphicsPlayerOutlines));
        TryGetSetting<bool>("Graphics_PlayerNametags", nameof(GraphicsPlayerNametags));
        TryGetSetting<bool>("Graphics_Colorblind", nameof(GraphicsColorblind));
        TryGetSetting<bool>("Graphics_InputDisplay", nameof(GraphicsInputDisplay));

        // Audio
        TryGetSetting<float>("Audio_MasterVolume", nameof(AudioMasterVolume));
        TryGetSetting<float>("Audio_MusicVolume", nameof(AudioMusicVolume));
        TryGetSetting<float>("Audio_SFXVolume", nameof(AudioSFXVolume));
        TryGetSetting("Audio_MuteMusicOnUnfocus", ref audioMuteMusicOnUnfocus);
        TryGetSetting("Audio_MuteSFXOnUnfocus", ref audioMuteSFXOnUnfocus);
        TryGetSetting("Audio_Panning", ref audioPanning);
        TryGetSetting("Audio_RestartMusicOnDeath", ref audioRestartMusicOnDeath);
        TryGetSetting("Audio_SpecialPowerupMusic", ref audioSpecialPowerupMusic);

        // Controls
        TryGetSetting("Controls_FireballFromSprint", ref controlsFireballSprint);
        TryGetSetting("Controls_AutoSprint", ref controlsAutoSprint);
        TryGetSetting("Controls_PropellerJump", ref controlsPropellerJump);
        TryGetSetting("Controls_Rumble", ref controlsRumble);
        TryGetSetting<string>("Controls_Bindings", nameof(ControlsBindings));

        // Misc
        TryGetSetting("Misc_FilterFullRooms", ref miscFilterFullRooms);
        TryGetSetting("Misc_FilterInProgressRooms", ref miscFilterInProgressRooms);
    }

    private bool TryGetSetting<T>(string key, string propertyName, T defaultValue = default) {
        if (!PlayerPrefs.HasKey(key)) {
            return false;
        }

        object value;

        // Gross... but there's no way to do it through a switch statement (afaik)
        if (typeof(T) == typeof(int)) {
            value = PlayerPrefs.GetInt(key);

        } else if (typeof(T) == typeof(float)) {
            value = PlayerPrefs.GetFloat(key);

        } else if (typeof(T) == typeof(string)) {
            value = PlayerPrefs.GetString(key);

        } else if (typeof(T) == typeof(bool)) {
            value = PlayerPrefs.GetInt(key) != 0;

        } else {
            throw new ArgumentException($"Type {typeof(T).Name} is not supported!");
        }

        var property = typeof(Settings).GetProperty(propertyName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
        property.SetValue(this, value);

        return true;
    }

    private bool TryGetSetting<T>(string key, ref T value) where T : Enum {
        int temp = 0;
        if (!TryGetSetting(key, ref temp)) {
            return false;
        }
        value = (T) (object) temp;
        return true;
    }

    private bool TryGetSetting(string key, ref string value) {
        if (!PlayerPrefs.HasKey(key)) {
            return false;
        }

        value = PlayerPrefs.GetString(key);
        return true;
    }

    private bool TryGetSetting(string key, ref int value) {
        if (!PlayerPrefs.HasKey(key)) {
            return false;
        }

        value = PlayerPrefs.GetInt(key);
        return true;
    }

    private bool TryGetSetting(string key, ref float value) {
        if (!PlayerPrefs.HasKey(key)) {
            return false;
        }

        value = PlayerPrefs.GetFloat(key);
        return true;
    }

    private bool TryGetSetting(string key, ref bool value) {
        if (!PlayerPrefs.HasKey(key)) {
            return false;
        }

        value = PlayerPrefs.GetInt(key) == 1;
        return true;
    }

    private void MassDeleteKeys(params string[] keys) {
        foreach (string key in keys) {
            PlayerPrefs.DeleteKey(key);
        }

        PlayerPrefs.Save();
    }
}
