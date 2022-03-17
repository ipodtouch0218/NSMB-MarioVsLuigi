using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GlobalController : Singleton<GlobalController> {
    public Canvas ndsCanvas;
    public RenderTexture ndsTexture;
    public PlayerData[] characters;
    public Settings settings;
    void Awake() {
        if (!InstanceCheck()) 
            return;
        Instance = this;
    }
    void Start() {
        settings = GetComponent<Settings>();    
    }
    void Update() {
        ndsCanvas.enabled = Settings.Instance.ndsResolution && SceneManager.GetActiveScene().buildIndex != 0;
    }
}
