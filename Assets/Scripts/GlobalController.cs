using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GlobalController : Singleton<GlobalController> {
    public Canvas ndsCanvas;
    public RenderTexture ndsTexture;
    public PlayerData[] characters;
    void Awake() {
        if (!InstanceCheck()) 
            return;
        Instance = this;
    }
    void Update() {
        ndsCanvas.enabled = Settings.Instance.ndsResolution && SceneManager.GetActiveScene().buildIndex != 0;
    }
}
