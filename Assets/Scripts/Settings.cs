using UnityEngine;
using UnityEngine.Audio;
using Photon.Pun;

public class Settings : Singleton<Settings> {
    public AudioMixer mixer;

    private float _volumeMaster, _volumeMusic, _volumeSFX;
    public float VolumeMaster { 
        get {
            return _volumeMaster;
        } 
        set {
            _volumeMaster = Mathf.Clamp01(value);
            ApplyVolumeSettings();
        }
    }
    public float VolumeSFX { 
        get {
            return _volumeSFX;
        } 
        set {
            _volumeSFX = Mathf.Clamp01(value);
            ApplyVolumeSettings();
        }
    }
    public float VolumeMusic { 
        get {
            return _volumeMusic;
        } 
        set {
            _volumeMusic = Mathf.Clamp01(value);
            ApplyVolumeSettings();
        }
    }
    public bool ndsResolution = false;

    void Awake() {
        if (!base.InstanceCheck()) 
            return;
        Instance = this;
        LoadSettingsFromPreferences();
        ApplyVolumeSettings();
    }

    public void LoadSettingsFromPreferences() {
        PhotonNetwork.NickName = PlayerPrefs.GetString("Nickname", "Player" + (int) Random.Range(1000,10000));
        VolumeSFX = PlayerPrefs.GetFloat("volumeSFX", 1);
        VolumeMusic = PlayerPrefs.GetFloat("volumeMusic", 0.5f);
        VolumeMaster = PlayerPrefs.GetFloat("volumeMaster", 1);
        ndsResolution = PlayerPrefs.GetInt("NDSResolution", 0) == 1;
    }
    public void SaveSettingsToPreferences() {
        PlayerPrefs.SetString("Nickname", PhotonNetwork.NickName);
        PlayerPrefs.SetFloat("volumeSFX", VolumeSFX);
        PlayerPrefs.SetFloat("volumeMusic", VolumeMusic);
        PlayerPrefs.SetFloat("volumeMaster", VolumeMaster);
        PlayerPrefs.SetInt("NDSResolution", ndsResolution ? 1 : 0);
        PlayerPrefs.Save();
    }

    void ApplyVolumeSettings() {
        mixer.SetFloat("MusicVolume", Mathf.Log10(VolumeMusic) * 20);
        mixer.SetFloat("SoundVolume", Mathf.Log10(VolumeSFX) * 20);
        mixer.SetFloat("MasterVolume", Mathf.Log10(VolumeMaster) * 20);
    }
}