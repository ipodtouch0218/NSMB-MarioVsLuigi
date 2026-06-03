using NSMB.Sound;
using NSMB.Utilities;
using Quantum;
using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace NSMB {
    public class Settings : Singleton<Settings> {

        //---Static Variables
        private static Controls _controls;
        public static Controls Controls => _controls;
        public static event Action OnColorblindModeChanged, OnNametagVisibilityChanged, OnDisableChatChanged, OnNdsResolutionSettingChanged, OnDiscordIntegrationChanged, OnCondensedScoreboardChanged;
        public static event Action<bool> OnInputDisplayActiveChanged, OnReplaysEnabledChanged;
        public static event Action<float> OnHudScaleChanged;

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
                OnDiscordIntegrationChanged?.Invoke();
            }
        }

        private bool _generalReplaysEnabled;
        public bool GeneralReplaysEnabled {
            get => _generalReplaysEnabled;
            set {
                _generalReplaysEnabled = value;
                OnReplaysEnabledChanged?.Invoke(value);
            }
        }
        
        private bool _generalCondensedScoreboard;
        public bool GeneralCondensedScoreboard {
            get => _generalCondensedScoreboard;
            set {
                _generalCondensedScoreboard = value;
                OnCondensedScoreboardChanged?.Invoke();
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

        private bool _graphicsNdsEnabled;
        public bool GraphicsNdsEnabled {
            get => _graphicsNdsEnabled;
            set {
                _graphicsNdsEnabled = value;
                OnNdsResolutionSettingChanged?.Invoke();
            }
        }

        private bool _graphicsNdsForceAspect;
        public bool GraphicsNdsForceAspect {
            get => _graphicsNdsForceAspect;
            set {
                _graphicsNdsForceAspect = value;
                OnNdsResolutionSettingChanged?.Invoke();
            }
        }

        private bool _graphicsNdsPixelPerfect;
        public bool GraphicsNdsPixelPerfect {
            get => _graphicsNdsPixelPerfect;
            set {
                _graphicsNdsPixelPerfect = value;
                OnNdsResolutionSettingChanged?.Invoke();
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

        private float _graphicsHudScale;
        public float GraphicsHudScale {
            get => _graphicsHudScale;
            set {
                _graphicsHudScale = value;
                OnHudScaleChanged?.Invoke(value);
            }
        }

        private bool _graphicsPlayerNametags;
        public bool GraphicsPlayerNametags {
            get => _graphicsPlayerNametags;
            set {
                _graphicsPlayerNametags = value;
                OnNametagVisibilityChanged?.Invoke();
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
            get => Controls.asset.SaveBindingOverridesAsJson();
            set => Controls.asset.LoadBindingOverridesFromJson(value);
        }

        //---Public Variables
        public string generalNickname;
        public AssetRef<CharacterAsset> generalCharacter;
        public AssetRef<PaletteSet> generalPalette;
        public int generalMaxTempReplays;
        public bool generalScoreboardAlways, generalChatFiltering, generalUseNicknameColor;

        public bool graphicsNametags;

        public Enums.SpecialPowerupMusic audioSpecialPowerupMusic;
        public bool audioMuteMusicOnUnfocus, audioMuteSFXOnUnfocus, audioPanning, audioRestartMusicOnDeath;

        public RumbleManager.RumbleSetting controlsRumble;
        public bool controlsFireballSprint, controlsAutoSprint, controlsPropellerJump, controlsAllowGroundpoundWithLeftRight;

        public bool miscFilterFullRooms, miscFilterInProgressRooms, miscFilterAddons;

        //---Private Variables
        private Action[] VersionUpdaters;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSplashScreen)]
        public static void CreateInstance() {
            _controls = new();
        }

        public void Awake() {
            Set(this);
            VersionUpdaters = new Action[] { LoadFromVersion0, LoadFromVersion1, LoadFromVersion2 };
            LoadSettings();
            _controls.Enable();

            // Potential duplicate bindings not activating fix?
            InputSystem.settings.SetInternalFeatureFlag("DISABLE_SHORTCUT_SUPPORT", true);
        }

        public void OnDestroy() {
            _controls.Disable();
        }

        public void SaveSettings() {
            // Generic
            PlayerPrefs.SetString("General_Nickname", generalNickname);
            PlayerPrefs.SetInt("General_ScoreboardAlwaysVisible", generalScoreboardAlways ? 1 : 0);
            PlayerPrefs.SetInt("General_CondensedScoreboard", GeneralCondensedScoreboard ? 1 : 0);
            PlayerPrefs.SetInt("General_DisableChat", GeneralDisableChat ? 1 : 0);
            PlayerPrefs.SetInt("General_ChatFilter", generalChatFiltering ? 1 : 0);
            PlayerPrefs.SetString("General_Character", generalCharacter.Id.ToString());
            PlayerPrefs.SetString("General_Palette", generalPalette.Id.ToString());
            PlayerPrefs.SetString("General_Locale", GeneralLocale);
            PlayerPrefs.SetInt("General_UseNicknameColor", generalUseNicknameColor ? 1 : 0);
            PlayerPrefs.SetInt("General_ReplaysEnabled", GeneralReplaysEnabled ? 1 : 0);
            PlayerPrefs.SetInt("General_MaxTempReplays", generalMaxTempReplays);
            PlayerPrefs.SetInt("General_DiscordIntegration", GeneralDiscordIntegration ? 1 : 0);

            // Graphics
            PlayerPrefs.SetString("Graphics_FullscreenResolution", Screen.currentResolution.width + "," + Screen.currentResolution.height);
            PlayerPrefs.SetInt("Graphics_FullscreenMode", GraphicsFullscreenMode);
            PlayerPrefs.SetInt("Graphics_NDS_Enabled", GraphicsNdsEnabled ? 1 : 0);
            PlayerPrefs.SetInt("Graphics_NDS_ForceAspect", GraphicsNdsForceAspect ? 1 : 0);
            PlayerPrefs.SetInt("Graphics_NDS_PixelPerfect", GraphicsNdsPixelPerfect ? 1 : 0);
            PlayerPrefs.SetInt("Graphics_VSync", GraphicsVsync ? 1 : 0);
            PlayerPrefs.SetInt("Graphics_MaxFPS", GraphicsMaxFps);
            PlayerPrefs.SetFloat("Graphics_HudScale", GraphicsHudScale);
            PlayerPrefs.SetInt("Graphics_PlayerOutlines", GraphicsPlayerOutlines ? 1 : 0);
            PlayerPrefs.SetInt("Graphics_PlayerNametags", GraphicsPlayerNametags ? 1 : 0);
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
            PlayerPrefs.SetInt("Controls_AllowGroundpoundWithLeftRight", controlsAllowGroundpoundWithLeftRight ? 1 : 0);
            PlayerPrefs.SetString("Controls_Bindings", ControlsBindings);

            // Misc
            PlayerPrefs.SetInt("Misc_FilterFullRooms", miscFilterFullRooms ? 1 : 0);
            PlayerPrefs.SetInt("Misc_FilterInProgressRooms", miscFilterInProgressRooms ? 1 : 0);
            PlayerPrefs.SetInt("Misc_FilterAddons", miscFilterAddons ? 1 : 0);

            PlayerPrefs.Save();
        }

        public void ApplyVolumeSettings() {
            var mixerManager = GlobalController.Instance.audioMixerManager;
            mixerManager.SetFloat(AudioMixerManager.KeyMaster, Mathf.Log10(AudioMasterVolume) * 20);
            mixerManager.SetFloat(AudioMixerManager.KeyMusic, Mathf.Log10(AudioMusicVolume) * 20);
            mixerManager.SetFloat(AudioMixerManager.KeySfx, Mathf.Log10(AudioSFXVolume) * 20);
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
            GeneralCondensedScoreboard = false;
            GeneralDisableChat = false;
            generalChatFiltering = PlayerPrefs.GetInt("ChatFilter", 1) != 0;
            generalCharacter = Utils.IndexIntoOrDefault(AssetRepository<CharacterAsset>.AllAssetRefs, PlayerPrefs.GetInt("Character", 0), AssetRepository<CharacterAsset>.AllAssetRefs[0]);
            generalPalette = Utils.IndexIntoOrDefault(AssetRepository<PaletteSet>.AllAssets.Where(ps => ps.IsLegacy).ToList(), PlayerPrefs.GetInt("Skin", 0) - 1, default);
            GeneralLocale = "en-US";
            generalUseNicknameColor = true;
            GeneralReplaysEnabled = true;
            generalMaxTempReplays = 20;
            _generalDiscordIntegration = true;

            GraphicsFullscreenResolution = Screen.resolutions[^1].width + "," + Screen.resolutions[^1].height;
            GraphicsFullscreenMode = (int) Screen.fullScreenMode;
            GraphicsNdsEnabled = PlayerPrefs.GetInt("NDSResolution", 0) != 0;
            GraphicsNdsForceAspect = PlayerPrefs.GetInt("NDS4by3", 0) != 0;
            GraphicsNdsPixelPerfect = false;
            GraphicsVsync = PlayerPrefs.GetInt("VSync", 1) != 0;
            GraphicsMaxFps = 0;
            GraphicsHudScale = 8;
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
            controlsAllowGroundpoundWithLeftRight = false;
            controlsPropellerJump = false;

            miscFilterFullRooms = false;
            miscFilterInProgressRooms = false;
            miscFilterAddons = false;

            MassDeleteKeys("Nickname", "ScoreboardAlwaysVisible", "ChatFilter", "Character", "Skin", "NDSResolution",
                "NDS4by3", "VSync", "volumeMaster", "volumeMusic", "volumeSFX", "FireballFromSprint");
        }

        private void LoadFromVersion1() {
            // Generic
            TryGetSetting("General_Nickname", ref generalNickname);
            TryGetSetting("General_ScoreboardAlwaysVisible", ref generalScoreboardAlways);
            TryGetSetting<bool>("General_CondensedScoreboard", nameof(GeneralCondensedScoreboard));
            TryGetSetting<bool>("General_DisableChat", nameof(GeneralDisableChat));
            TryGetSetting("General_ChatFilter", ref generalChatFiltering);
            int generalCharacterOld = 0;
            if (TryGetSetting("General_Character", ref generalCharacterOld)) {
                generalCharacter = Utils.IndexIntoOrDefault(AssetRepository<CharacterAsset>.AllAssetRefs, generalCharacterOld, AssetRepository<CharacterAsset>.AllAssetRefs[0]);
            }
            int generalPaletteOld = 0;
            if (TryGetSetting("General_Palette", ref generalPaletteOld)) {
                generalPalette = Utils.IndexIntoOrDefault(AssetRepository<PaletteSet>.AllAssets.Where(ps => ps.IsLegacy).ToList(), generalPaletteOld - 1, default);
            }
            TryGetSetting<string>("General_Locale", nameof(GeneralLocale));
            TryGetSetting("General_UseNicknameColor", ref generalUseNicknameColor);
            TryGetSetting<bool>("General_ReplaysEnabled", nameof(GeneralReplaysEnabled));
            TryGetSetting("General_MaxTempReplays", ref generalMaxTempReplays);
            TryGetSetting<bool>("General_DiscordIntegration", nameof(GeneralDiscordIntegration));

            // Graphics
            TryGetSetting<int>("Graphics_FullscreenMode", nameof(GraphicsFullscreenMode));
            TryGetSetting<string>("Graphics_FullscreenResolution", nameof(GraphicsFullscreenResolution));
            TryGetSetting<bool>("Graphics_NDS_Enabled", nameof(GraphicsNdsEnabled));
            TryGetSetting<bool>("Graphics_NDS_ForceAspect", nameof(GraphicsNdsForceAspect));
            TryGetSetting<bool>("Graphics_NDS_PixelPerfect", nameof(GraphicsNdsPixelPerfect));
            TryGetSetting<int>("Graphics_MaxFPS", nameof(GraphicsMaxFps));
            TryGetSetting<bool>("Graphics_VSync", nameof(GraphicsVsync));
            TryGetSetting<float>("Graphics_HudScale", nameof(GraphicsHudScale));
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
            TryGetSetting("Controls_AllowGroundpoundWithLeftRight", ref controlsAllowGroundpoundWithLeftRight);
            TryGetSetting("Controls_Rumble", ref controlsRumble);
            TryGetSetting<string>("Controls_Bindings", nameof(ControlsBindings));

            // Misc
            TryGetSetting("Misc_FilterFullRooms", ref miscFilterFullRooms);
            TryGetSetting("Misc_FilterInProgressRooms", ref miscFilterInProgressRooms);
            TryGetSetting("Misc_FilterAddons", ref miscFilterAddons);
        }

        private void LoadFromVersion2() {
            // Generic
            TryGetSetting("General_Character", ref generalCharacter);
            TryGetSetting("General_Palette", ref generalPalette);
        }

        private bool TryGetSetting<T>(string key, string propertyName) {
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
            } else if (typeof(T) == typeof(AssetRef<>)) {
                if (AssetGuid.TryParse(PlayerPrefs.GetString(key), out var guid, false)) {
                    value = new AssetRef(guid);
                } else {
                    return false;
                }
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

        private bool TryGetSetting<T>(string key, ref AssetRef<T> value) where T : AssetObject {
            if (!PlayerPrefs.HasKey(key)) {
                return false;
            }

            if (!AssetGuid.TryParse(PlayerPrefs.GetString(key), out var guid, true)) {
                return false;
            }

            value = new(guid);
            return true;
        }

        private void MassDeleteKeys(params string[] keys) {
            foreach (string key in keys) {
                PlayerPrefs.DeleteKey(key);
            }

            PlayerPrefs.Save();
        }
    }
}
