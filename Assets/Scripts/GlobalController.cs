using NSMB.Extensions;
using NSMB.Loading;
using NSMB.Translation;
using NSMB.UI.Pause.Options;
using Quantum;
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering.Universal;

public class GlobalController : Singleton<GlobalController> {

    //---Events
    public event Action<RenderTexture> RenderTextureChanged;
    public static event Action ResolutionChanged;

    //---Public Variables
    public TranslationManager translationManager;
    public DiscordController discordController;
    public RumbleManager rumbleManager;
    public Gradient rainbowGradient;
    public SimulationConfig config;

    public PauseOptionMenuManager optionsManager;

    public ScriptableRendererFeature outlineFeature;
    public GameObject ndsCanvas, fourByThreeImage, anyAspectImage, graphy, connecting, fusionStatsTemplate;
    public LoadingCanvas loadingCanvas;
    public AudioSource sfx;

    public RectTransform ndsRect;

    public RenderTexture ndsTexture;

    public bool checkedForVersion = false, firstConnection = true;
    public int windowWidth = 1280, windowHeight = 720;

    //public ConnectionToken connectionToken;

    //---Serialized Variables
    [SerializeField] private AudioMixer mixer;

    //---Private Variables
    private Coroutine fadeMusicRoutine;
    private Coroutine fadeSfxRoutine;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    public static void CreateInstance() {
        Instantiate(Resources.Load("Prefabs/Static/GlobalController"));
    }

    public void OnValidate() {
        this.SetIfNull(ref discordController);
    }

    public void Awake() {
        Set(this);

        firstConnection = true;
        checkedForVersion = false;
    }

    public void Start() {
        AuthenticationHandler.IsAuthenticating = false;
        //NetworkHandler.connecting = 0;

        if (!Application.isFocused) {
            if (Settings.Instance.audioMuteMusicOnUnfocus) {
                mixer.SetFloat("MusicVolume", -80f);
            }

            if (Settings.Instance.audioMuteSFXOnUnfocus) {
                mixer.SetFloat("SoundVolume", -80f);
            }
        }
        ControlSystem.controls.Enable();
        ControlSystem.controls.Debug.FPSMonitor.performed += ToggleFpsMonitor;

        //NetworkHandler.OnShutdown += OnShutdown;
        //NetworkHandler.OnHostMigration += OnHostMigration;
        QuantumCallback.Subscribe<CallbackUnitySceneLoadDone>(this, OnUnitySceneLoadDone);
    }

    public void OnDestroy() {
        ControlSystem.controls.Debug.FPSMonitor.performed -= ToggleFpsMonitor;
        ControlSystem.controls.Disable();

        //NetworkHandler.OnShutdown -= OnShutdown;
        //NetworkHandler.OnHostMigration -= OnHostMigration;
    }

    public void Update() {
        int newWindowWidth = Screen.width;
        int newWindowHeight = Screen.height;

        if (windowWidth != newWindowWidth || windowHeight != newWindowHeight) {
            windowWidth = newWindowWidth;
            windowHeight = newWindowHeight;
            ResolutionChanged?.Invoke();
        }

        if (Settings.Instance.graphicsNdsEnabled && SceneManager.GetActiveScene().buildIndex != 0) {

            int targetHeight = 224;
            int targetWidth = Mathf.CeilToInt(targetHeight * (Settings.Instance.graphicsNdsForceAspect ? (4/3f) : (float) newWindowWidth / newWindowHeight));

            if (!ndsTexture || ndsTexture.width != targetWidth || ndsTexture.height != targetHeight) {
                if (ndsTexture) {
                    ndsTexture.Release();
                }

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
        if (Screen.fullScreenMode == FullScreenMode.Windowed && Keyboard.current[Key.LeftShift].isPressed && (windowWidth != newWindowWidth || windowHeight != newWindowHeight)) {
            newWindowHeight = (int) (newWindowWidth * (9f / 16f));
            Screen.SetResolution(newWindowWidth, newWindowHeight, FullScreenMode.Windowed);
        }
#endif

        if ((int) (Time.unscaledTime + Time.unscaledDeltaTime) > (int) Time.unscaledTime) {
            // Update discord every second
            discordController.UpdateActivity();
        }
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
            if (Settings.Instance.audioMuteMusicOnUnfocus) {
                fadeMusicRoutine ??= StartCoroutine(FadeVolume("MusicVolume"));
            }

            if (Settings.Instance.audioMuteSFXOnUnfocus) {
                fadeSfxRoutine ??= StartCoroutine(FadeVolume("SoundVolume"));
            }

#if UNITY_WEBGL
            QualitySettings.vSyncCount = previousVsyncCount;
            Application.targetFrameRate = previousFrameRate;
#endif
        }
    }

    public void OnUnitySceneLoadDone(CallbackUnitySceneLoadDone e) {
        foreach (int localPlayer in e.Game.GetLocalPlayerSlots()) {
            e.Game.SendCommand(localPlayer, new CommandPlayerLoaded());
        }

        discordController.UpdateActivity();
    }

    private void StopCoroutineNullable(ref Coroutine coroutine) {
        if (coroutine == null) {
            return;
        }

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

    public void PlaySound(SoundEffect soundEffect) {
        sfx.PlayOneShot(soundEffect);
    }

    private static float ToLinearScale(float x) {
        return Mathf.Pow(10, x / 20);
    }

    private static float ToLogScale(float x) {
        return 20 * Mathf.Log10(x);
    }

    private void ToggleFpsMonitor(InputAction.CallbackContext obj) {
        graphy.SetActive(!graphy.activeSelf);
    }
}
