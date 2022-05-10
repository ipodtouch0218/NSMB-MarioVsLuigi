using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using Photon.Pun;
using Photon.Realtime;

public class GlobalController : Singleton<GlobalController> {

    public GameObject ndsCanvas, fourByThreeImage, anyAspectImage, graphy;

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

    void Start() {
        //Photon settings.
        PhotonNetwork.SerializationRate = 30;
        PhotonNetwork.MaxResendsBeforeDisconnect = 15;

        InputSystem.controls.UI.DebugInfo.performed += (context) => {
            graphy.SetActive(!graphy.activeSelf);
        };
    }

    void Update() {
        int currentWidth = Screen.width;
        int currentHeight = Screen.height;

        if (settings.ndsResolution && SceneManager.GetActiveScene().buildIndex != 0) {
            float aspect = (float) currentWidth / currentHeight;
            int targetHeight = 224;
            int targetWidth = (int) (targetHeight * (settings.fourByThreeRatio ? (4/3f) : aspect));
            if (ndsTexture == null || ndsTexture.width != targetWidth || ndsTexture.height != targetHeight) {
                if (ndsTexture != null)
                    ndsTexture.Release();
                ndsTexture = RenderTexture.GetTemporary(targetWidth, targetHeight);
                ndsTexture.filterMode = FilterMode.Point;
                ndsTexture.graphicsFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.B10G11R11_UFloatPack32;
                Debug.Log(ndsTexture);
            }
            ndsCanvas.SetActive(true);
        } else {
            ndsCanvas.SetActive(false);
        }

        //todo: this jitters to hell
#if UNITY_STANDALONE
        if (Screen.fullScreenMode == FullScreenMode.Windowed && Keyboard.current[Key.LeftShift].isPressed && (windowWidth != currentWidth || windowHeight != currentHeight)) {
            currentHeight = (int) (currentWidth * (9f / 16f));
            Screen.SetResolution(currentWidth, currentHeight, FullScreenMode.Windowed);
        }
        windowWidth = currentWidth;
        windowHeight = currentHeight;
#endif
    }
}
