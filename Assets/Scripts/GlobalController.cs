using System;
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

    //---Events
    public event Action<RenderTexture> RenderTextureChanged;

    //---Public Variables
    public TranslationManager translationManager;
    public DiscordController discordController;
    public RumbleManager rumbleManager;
    public Gradient rainbowGradient;

    public PauseOptionMenuManager optionsManager;

    public ScriptableRendererFeature outlineFeature;
    public GameObject ndsCanvas, fourByThreeImage, anyAspectImage, graphy, connecting, fusionStatsTemplate;
    public LoadingCanvas loadingCanvas;

    public RectTransform ndsRect;

    public RenderTexture ndsTexture;

    public bool checkedForVersion = false, firstConnection = true;
    public int windowWidth = 1280, windowHeight = 720;

    public ConnectionToken connectionToken;

    //---Serialized Variables
    [SerializeField] private AudioMixer mixer;
    [SerializeField] private AudioSource sfx;

    //---Private Variables
    private Coroutine fadeMusicRoutine;
    private Coroutine fadeSfxRoutine;
    private GameObject fusionStats;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    public static void CreateInstance() {
        Instantiate(Resources.Load("Prefabs/Static/GlobalController"));
    }

    public void OnValidate() {
        if (!discordController) discordController = GetComponent<DiscordController>();
    }

    public void Awake() {
        Set(this);

        firstConnection = true;
        checkedForVersion = false;
    }

    public void Start() {
        AuthenticationHandler.IsAuthenticating = false;
        NetworkHandler.connecting = 0;

        if (!Application.isFocused) {
            if (Settings.Instance.audioMuteMusicOnUnfocus) mixer.SetFloat("MusicVolume", -80f);
            if (Settings.Instance.audioMuteSFXOnUnfocus) mixer.SetFloat("SoundVolume", -80f);
        }
        ControlSystem.controls.Enable();
        ControlSystem.controls.Debug.FPSMonitor.performed += ToggleFpsMonitor;

        NetworkHandler.OnShutdown += OnShutdown;
        NetworkHandler.OnHostMigration += OnHostMigration;

        CreateFusionStatsInstance();
    }

    public void OnDestroy() {
        ControlSystem.controls.Debug.FPSMonitor.performed -= ToggleFpsMonitor;
        ControlSystem.controls.Disable();

        NetworkHandler.OnShutdown -= OnShutdown;
        NetworkHandler.OnHostMigration -= OnHostMigration;
    }

    public void Update() {
        int windowWidth = Screen.width;
        int windowHeight = Screen.height;

        if (Settings.Instance.graphicsNdsEnabled && SceneManager.GetActiveScene().buildIndex != 0) {

            int targetHeight = 224;
            int targetWidth = Mathf.CeilToInt(targetHeight * (Settings.Instance.graphicsNdsForceAspect ? (4/3f) : (float) windowWidth / windowHeight));

            if (!ndsTexture || ndsTexture.width != targetWidth || ndsTexture.height != targetHeight) {
                if (ndsTexture)
                    ndsTexture.Release();

                ndsTexture = RenderTexture.GetTemporary(targetWidth, targetHeight);
                ndsTexture.filterMode = FilterMode.Point;
                ndsTexture.graphicsFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.B10G11R11_UFloatPack32;
                RenderTextureChanged?.Invoke(ndsTexture);
            }
            ndsCanvas.SetActive(true);
        } else {
            ndsCanvas.SetActive(false);
        }

        //todo: this jitters to hell
#if UNITY_STANDALONE
        if (Screen.fullScreenMode == FullScreenMode.Windowed && Keyboard.current[Key.LeftShift].isPressed && (this.windowWidth != windowWidth || this.windowHeight != windowHeight)) {
            windowHeight = (int) (windowWidth * (9f / 16f));
            Screen.SetResolution(windowWidth, windowHeight, FullScreenMode.Windowed);
        }
        this.windowWidth = windowWidth;
        this.windowHeight = windowHeight;
#endif
    }

#if UNITY_WEBGL
    int previousVsyncCount;
    int previousFrameRate;
#endif

    public void OnApplicationFocus(bool focus) {
        if (focus) {
            Settings.Instance.ApplyVolumeSettings();
            StopCoroutineNullable(ref fadeMusicRoutine);
            StopCoroutineNullable(ref fadeSfxRoutine);

#if UNITY_WEBGL
            // Lock framerate when losing focus to (hopefully) disable browsers slowing the game
            previousVsyncCount = QualitySettings.vSyncCount;
            previousFrameRate = Application.targetFrameRate;

            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 30;
#endif

        } else {
            if (Settings.Instance.audioMuteMusicOnUnfocus)
                fadeMusicRoutine ??= StartCoroutine(FadeVolume("MusicVolume"));
            if (Settings.Instance.audioMuteSFXOnUnfocus)
                fadeSfxRoutine ??= StartCoroutine(FadeVolume("SoundVolume"));

#if UNITY_WEBGL
            QualitySettings.vSyncCount = previousVsyncCount;
            Application.targetFrameRate = previousFrameRate;
#endif
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

    private void CreateFusionStatsInstance() {
        if (fusionStats)
            DestroyImmediate(fusionStats);

        fusionStats = Instantiate(fusionStatsTemplate, fusionStatsTemplate.transform.parent);
        fusionStats.SetActive(true);
    }

    private void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason) {
        CreateFusionStatsInstance();
    }

    private void OnHostMigration(NetworkRunner runner, HostMigrationToken token) {
        CreateFusionStatsInstance();
    }

    private void ToggleFpsMonitor(InputAction.CallbackContext obj) {
        graphy.SetActive(!graphy.activeSelf);
    }
}
