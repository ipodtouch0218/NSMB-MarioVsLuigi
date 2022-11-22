using UnityEngine;
using UnityEngine.Audio;

public class Settings : Singleton<Settings> {


    private float _volumeMaster, _volumeMusic, _volumeSFX;
    public float VolumeMaster {
        get => _volumeMaster;
        set {
            _volumeMaster = Mathf.Clamp01(value);
            ApplyVolumeSettings();
        }
    }
    public float VolumeSFX {
        get => _volumeSFX;
        set {
            _volumeSFX = Mathf.Clamp01(value);
            ApplyVolumeSettings();
        }
    }
    public float VolumeMusic {
        get => _volumeMusic;
        set {
            _volumeMusic = Mathf.Clamp01(value);
            ApplyVolumeSettings();
        }
    }

    //---Public Variables
    public string nickname, controlsJsonString;
    public byte character, skin;
    public bool ndsResolution = false, fireballFromSprint = true, autoSprint = false, vsync = false, fourByThreeRatio = false;
    public bool scoreboardAlways = false, filter = true;

    //---Private Variables
    [SerializeField] private AudioMixer mixer;

    public void Awake() {
        if (!InstanceCheck())
            return;

        Instance = this;
        LoadSettingsFromPreferences();
        ApplyVolumeSettings();
    }

    public void LoadSettingsFromPreferences() {
        nickname = PlayerPrefs.GetString("Nickname");
        if (nickname == null || nickname == "")
            nickname = "Player" + Random.Range(1000, 10000);

        VolumeSFX = PlayerPrefs.GetFloat("volumeSFX", 0.5f);
        VolumeMusic = PlayerPrefs.GetFloat("volumeMusic", 0.25f);
        VolumeMaster = PlayerPrefs.GetFloat("volumeMaster", 1);
        ndsResolution = PlayerPrefs.GetInt("NDSResolution", 0) == 1;
        fireballFromSprint = PlayerPrefs.GetInt("FireballFromSprint", 1) == 1;
        autoSprint = PlayerPrefs.GetInt("AutoSprint", 0) == 1;
        vsync = PlayerPrefs.GetInt("VSync", 0) == 1;
        fourByThreeRatio = PlayerPrefs.GetInt("NDS4by3", 0) == 1;
        scoreboardAlways = PlayerPrefs.GetInt("ScoreboardAlwaysVisible", 1) == 1;
        filter = PlayerPrefs.GetInt("ChatFilter", 1) == 1;
        character = (byte) PlayerPrefs.GetInt("Character", 0);
        skin = (byte) PlayerPrefs.GetInt("Skin", 0);
    }

    public void SaveSettingsToPreferences() {
        PlayerPrefs.SetString("Nickname", nickname);
        PlayerPrefs.SetFloat("volumeSFX", VolumeSFX);
        PlayerPrefs.SetFloat("volumeMusic", VolumeMusic);
        PlayerPrefs.SetFloat("volumeMaster", VolumeMaster);
        PlayerPrefs.SetInt("NDSResolution", ndsResolution ? 1 : 0);
        PlayerPrefs.SetInt("FireballFromSprint", fireballFromSprint ? 1 : 0);
        PlayerPrefs.SetInt("AutoSprint", autoSprint ? 1 : 0);
        PlayerPrefs.SetInt("VSync", vsync ? 1 : 0);
        PlayerPrefs.SetInt("NDS4by3", fourByThreeRatio ? 1 : 0);
        PlayerPrefs.SetInt("ScoreboardAlwaysVisible", scoreboardAlways ? 1 : 0);
        PlayerPrefs.SetInt("ChatFilter", filter ? 1 : 0);
        PlayerPrefs.SetInt("Character", character);
        PlayerPrefs.SetInt("Skin", skin);
        PlayerPrefs.Save();
    }

    private void ApplyVolumeSettings() {
        mixer.SetFloat("MusicVolume", Mathf.Log10(VolumeMusic) * 20);
        mixer.SetFloat("SoundVolume", Mathf.Log10(VolumeSFX) * 20);
        mixer.SetFloat("MasterVolume", Mathf.Log10(VolumeMaster) * 20);
    }
}
