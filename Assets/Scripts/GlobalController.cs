using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using Photon.Realtime;

public class GlobalController : Singleton<GlobalController> {
    public Canvas ndsCanvas;
    public RenderTexture ndsTexture;
    public PlayerData[] characters;
    public Settings settings;
    public string controlsJson = null;

    public bool joinedAsSpectator = false;
    public DisconnectCause? disconnectCause = null;

    private int windowWidth, windowHeight;

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

#if UNITY_STANDALONE
        int currentWidth = Screen.width;
        int currentHeight = Screen.height;
        if (Screen.fullScreenMode == FullScreenMode.Windowed && Keyboard.current[Key.LeftShift].isPressed && (windowWidth != currentWidth || windowHeight != currentHeight)) {
            currentHeight = (int) (currentWidth * (9f / 16f));
            Screen.SetResolution(currentWidth, currentHeight, FullScreenMode.Windowed);
        }
        windowWidth = currentWidth;
        windowHeight = currentHeight;
#endif
    }
}
