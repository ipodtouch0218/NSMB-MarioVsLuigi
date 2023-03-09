using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

using Fusion;
using NSMB.Loading;
using System.Collections;

public class GlobalController : Singleton<GlobalController> {

    //---Public Properties
    public DiscordController DiscordController { get; private set; }

    //---Public Variables
    public Gradient rainbowGradient;

    public GameObject ndsCanvas, fourByThreeImage, anyAspectImage, graphy;
    public LoadingCanvas loadingCanvas;

    public RenderTexture ndsTexture;
    public string controlsJson = null;
    public string nickname;

    public bool checkedForVersion;
    public ShutdownReason? disconnectCause = null;

    //---Serialized Variables
    [SerializeField] private AudioMixer mixer;

    //---Private Variables
    private int windowWidth, windowHeight;
    private Coroutine fadeRoutine;



    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    public static void CreateInstance() {
        Instantiate(Resources.Load("Prefabs/Static/GlobalController"));
    }

    public void Awake() {
        if (!InstanceCheck())
            return;

        Instance = this;
        DiscordController = GetComponent<DiscordController>();
    }

    public void Start() {
        ControlSystem.controls.UI.DebugInfo.performed += (context) => {
            graphy.SetActive(!graphy.activeSelf);
        };
    }

    public void Update() {
        int currentWidth = Screen.width;
        int currentHeight = Screen.height;

        if (Settings.Instance.ndsResolution && SceneManager.GetActiveScene().buildIndex != 0) {
            float aspect = (float) currentWidth / currentHeight;
            int targetHeight = 224;
            int targetWidth = (int) (targetHeight * (Settings.Instance.fourByThreeRatio ? (4/3f) : aspect));
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

    public void OnApplicationFocus(bool focus) {
        if (focus) {
            Settings.Instance.ApplyVolumeSettings();
            if (fadeRoutine != null)
                StopCoroutine(fadeRoutine);
            fadeRoutine = null;
        } else {
            fadeRoutine ??= StartCoroutine(FadeMusic());
        }
    }

    private IEnumerator FadeMusic() {
        mixer.GetFloat("MusicVolume", out float currentVolume);
        currentVolume = ToLinearScale(currentVolume);
        float fadeRate = currentVolume * 2f;

        while (currentVolume > 0f) {
            currentVolume -= fadeRate * Time.deltaTime;
            mixer.SetFloat("MusicVolume", ToLogScale(currentVolume));
            yield return null;
        }
        mixer.SetFloat("MusicVolume", -80f);
    }

    private static float ToLinearScale(float x) {
        return Mathf.Pow(10, x / 20);
    }

    private static float ToLogScale(float x) {
        return 20 * Mathf.Log10(x);
    }
}
