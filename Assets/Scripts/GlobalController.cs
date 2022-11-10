using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

using Fusion;
using NSMB.Loading;

public class GlobalController : Singleton<GlobalController> {

    public PrefabList prefabs;
    public Powerup[] powerups;

    public PlayerColorSet[] skins;
    public Gradient rainbowGradient;

    public GameObject ndsCanvas, fourByThreeImage, anyAspectImage, graphy;
    public LoadingCanvas loadingCanvas;

    public RenderTexture ndsTexture;
    public CharacterData[] characters;
    public Settings settings;
    public DiscordController DiscordController { get; private set; }
    public string controlsJson = null;

    public bool checkedForVersion;
    public ShutdownReason? disconnectCause = null;

    private int windowWidth, windowHeight;


    public string nickname;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    public static void CreateInstance() {
        Instantiate(Resources.Load("Prefabs/Static/GlobalController"));
    }

    public void Awake() {
        if (!InstanceCheck())
            return;

        Instance = this;
        settings = GetComponent<Settings>();
        DiscordController = GetComponent<DiscordController>();
    }

    public void Start() {
        InputSystem.controls.UI.DebugInfo.performed += (context) => {
            graphy.SetActive(!graphy.activeSelf);
        };
    }

    public void Update() {
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
