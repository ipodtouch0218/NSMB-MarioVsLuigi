using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;
using Photon.Pun;
using TMPro;

public class GlobalController : Singleton<GlobalController> {
    public float volumeSFX = 1, volumeMusic = 1, volumeMaster = 1;
    public AudioMixer mixer;
    public int starRequirement = 15;
    public List<string> loadedPlayers = new List<string>();
    void Awake() {
        DontDestroyOnLoad(gameObject);

        PhotonNetwork.NickName = PlayerPrefs.GetString("Nickname", "Player" + (int) Random.Range(1000,10000));
        
        volumeSFX = PlayerPrefs.GetFloat("volumeSFX", 1);
        volumeMusic = PlayerPrefs.GetFloat("volumeMusic", 0.5f);
        volumeMaster = PlayerPrefs.GetFloat("volumeMaster", 1);
    }

    void Start() {
        SetVolumeSettings();
    }

    public void SetVolumeMusic(Slider slider) {
        volumeMusic = slider.value;
        PlayerPrefs.SetFloat("volumeMusic", volumeMusic);
        PlayerPrefs.Save();
        SetVolumeSettings();
    }

    public void SetVolumeSFX(Slider slider) {
        volumeSFX = slider.value;
        PlayerPrefs.SetFloat("volumeSFX", volumeSFX);
        PlayerPrefs.Save();
        SetVolumeSettings();
    }

    public void SetVolumeMaster(Slider slider) {
        volumeMaster = slider.value;
        PlayerPrefs.SetFloat("volumeMaster", volumeMaster);
        PlayerPrefs.Save();
        SetVolumeSettings();
    }

    void SetVolumeSettings() {
        mixer.SetFloat("MusicVolume", Mathf.Log10(volumeMusic) * 20);
        mixer.SetFloat("SoundVolume", Mathf.Log10(volumeSFX) * 20);
        mixer.SetFloat("MasterVolume", Mathf.Log10(volumeMaster) * 20);
    }
}
