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
    public DiscordController discordController;
    public string controlsJson = null;

    public bool joinedAsSpectator = false;
    public DisconnectCause? disconnectCause = null;

    private int windowWidth, windowHeight;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    public static void CreateInstance() {
        Instantiate(Resources.Load("Prefabs/Static/GlobalController"));
    }

    void Awake() {
        if (!InstanceCheck())
            return;
        Instance = this;
        settings = GetComponent<Settings>();
        discordController = GetComponent<DiscordController>();
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
