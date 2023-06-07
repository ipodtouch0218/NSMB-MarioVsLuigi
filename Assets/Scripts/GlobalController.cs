using System.Collections;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering.Universal;

using Fusion;
using NSMB.Extensions;
using NSMB.Loading;
using NSMB.Translation;
using NSMB.UI.Pause.Options;

public class GlobalController : Singleton<GlobalController> {

    //---Public Variables
    public TranslationManager translationManager;
    public DiscordController discordController;
    public Gradient rainbowGradient;

    public PauseOptionMenuManager optionsManager;

    public ScriptableRendererFeature outlineFeature;
    public GameObject ndsCanvas, fourByThreeImage, anyAspectImage, graphy;
    public LoadingCanvas loadingCanvas;

    public RenderTexture ndsTexture;

    public bool checkedForVersion;
    public ShutdownReason? disconnectCause = null;
    public int windowWidth = 1280, windowHeight = 720;

    //---Serialized Variables
    [SerializeField] private AudioMixer mixer;
    [SerializeField] private AudioSource sfx;

    //---Private Variables
    private Coroutine fadeMusicRoutine;
    private Coroutine fadeSfxRoutine;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    public static void CreateInstance() {
        Instantiate(Resources.Load("Prefabs/Static/GlobalController"));
    }

    public void OnValidate() {
        if (!discordController) discordController = GetComponent<DiscordController>();
    }

    public void Awake() => Set(this);

    public void Start() {
        if (!Application.isFocused) {
            if (Settings.Instance.audioMuteMusicOnUnfocus) mixer.SetFloat("MusicVolume", -80f);
            if (Settings.Instance.audioMuteSFXOnUnfocus) mixer.SetFloat("SoundVolume", -80f);
        }
        ControlSystem.controls.Debug.FPSMonitor.performed += (context) => {
            graphy.SetActive(!graphy.activeSelf);
        };
    }

    public void Update() {
        int currentWidth = Screen.width;
        int currentHeight = Screen.height;

        if (Settings.Instance.graphicsNdsEnabled && SceneManager.GetActiveScene().buildIndex != 0) {
            float aspect = (float) currentWidth / currentHeight;
            int targetHeight = 224;
            int targetWidth = Mathf.CeilToInt(targetHeight * (Settings.Instance.graphicsNdsForceAspect ? (4/3f) : aspect));
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
            StopCoroutineNullable(ref fadeMusicRoutine);
            StopCoroutineNullable(ref fadeSfxRoutine);
        } else {
            if (Settings.Instance.audioMuteMusicOnUnfocus)
                fadeMusicRoutine ??= StartCoroutine(FadeVolume("MusicVolume"));
            if (Settings.Instance.audioMuteSFXOnUnfocus)
                fadeSfxRoutine ??= StartCoroutine(FadeVolume("SoundVolume"));
        }
    }

    private void StopCoroutineNullable(ref Coroutine coroutine) {
        if (coroutine == null)
            return;

        StopCoroutine(coroutine);
        coroutine = null;
    }

    private IEnumerator FadeVolume(string key) {
        mixer.GetFloat(key, out float currentVolume);
        currentVolume = ToLinearScale(currentVolume);
        float fadeRate = currentVolume * 2f;

        while (currentVolume > 0f) {
            currentVolume -= fadeRate * Time.deltaTime;
            mixer.SetFloat(key, ToLogScale(currentVolume));
            yield return null;
        }
        mixer.SetFloat(key, -80f);
    }

    public void PlaySound(Enums.Sounds sound) {
        sfx.PlayOneShot(sound);
    }

    private static float ToLinearScale(float x) {
        return Mathf.Pow(10, x / 20);
    }

    private static float ToLogScale(float x) {
        return 20 * Mathf.Log10(x);
    }
}
