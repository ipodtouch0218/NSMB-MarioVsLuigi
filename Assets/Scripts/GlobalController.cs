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
using UnityEngine.Profiling;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

public class GlobalController : Singleton<GlobalController> {

    //---Events
    public static event Action ResolutionChanged;

    //---Public Variables
    public TranslationManager translationManager;
    public DiscordController discordController;
    public RumbleManager rumbleManager;
    public Gradient rainbowGradient;
    public Sprite[] pingIndicators;
    public SimulationConfig config;

    public PauseOptionMenuManager optionsManager;

    public ScriptableRendererFeature outlineFeature;
    public GameObject graphy, connecting;
    public LoadingCanvas loadingCanvas;
    public Image fullscreenFadeImage;
    public AudioSource sfx;

    [NonSerialized] public bool checkedForVersion = false, firstConnection = true;
    [NonSerialized] public int windowWidth = 1280, windowHeight = 720;

 
    //---Serialized Variables
    [SerializeField] private AudioMixer mixer;

    //---Private Variables
    private Coroutine fadeMusicRoutine, fadeSfxRoutine, totalFadeRoutine;

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

        if (!Application.isFocused) {
            if (Settings.Instance.audioMuteMusicOnUnfocus) {
                mixer.SetFloat("MusicVolume", -80f);
            }

            if (Settings.Instance.audioMuteSFXOnUnfocus) {
                mixer.SetFloat("SoundVolume", -80f);
            }
        }
        Settings.Controls.Enable();
        Settings.Controls.Debug.FPSMonitor.performed += ToggleFpsMonitor;
        QuantumEvent.Subscribe<EventStartGameEndFade>(this, OnStartGameEndFade);
        QuantumCallback.Subscribe<CallbackUnitySceneLoadDone>(this, OnUnitySceneLoadDone);
    }

    public void OnDestroy() {
        Settings.Controls.Debug.FPSMonitor.performed -= ToggleFpsMonitor;
        Settings.Controls.Disable();
    }

    public void Update() {
        int newWindowWidth = Screen.width;
        int newWindowHeight = Screen.height;

        //todo: this jitters to hell
#if UNITY_STANDALONE
        if (Screen.fullScreenMode == FullScreenMode.Windowed && UnityEngine.Input.GetKey(KeyCode.LeftShift) && (windowWidth != newWindowWidth || windowHeight != newWindowHeight)) {
            newWindowHeight = (int) (newWindowWidth * (9f / 16f));
            Screen.SetResolution(newWindowWidth, newWindowHeight, FullScreenMode.Windowed);
        }

        //if (Debug.isDebugBuild) {
            if (UnityEngine.Input.GetKeyDown(KeyCode.F9)) {
                if (Profiler.enabled) {
                    Profiler.enabled = false;
                    PlaySound(SoundEffect.Player_Sound_Powerdown);
                } else {
                    Profiler.maxUsedMemory = 256 * 1024 * 1024;
                    Profiler.logFile = "profile-" + DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                    Profiler.enableBinaryLog = true;
                    Profiler.enabled = true;
                    PlaySound(SoundEffect.Player_Sound_PowerupCollect);
                }
            }
        //}

        if (UnityEngine.Input.GetKeyDown(KeyCode.F6)) {
            System.Diagnostics.Process.Start(Application.consoleLogPath);
        }
#endif

        if (windowWidth != newWindowWidth || windowHeight != newWindowHeight) {
            windowWidth = newWindowWidth;
            windowHeight = newWindowHeight;
            ResolutionChanged?.Invoke();
        }

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
            this.StopCoroutineNullable(ref fadeMusicRoutine);
            this.StopCoroutineNullable(ref fadeSfxRoutine);

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
        this.StopCoroutineNullable(ref totalFadeRoutine);
        mixer.SetFloat("OverrideVolume", 0f);
        StartCoroutine(FadeFullscreenImage(0, 1/3f, 0.1f));
    }

    private IEnumerator FadeVolume(string key) {
        mixer.GetFloat(key, out float currentVolume);
        currentVolume = ToLinearScale(currentVolume);
        float fadeRate = currentVolume * 2f;

        while (currentVolume > 0f) {
            currentVolume -= fadeRate * Time.fixedDeltaTime;
            mixer.SetFloat(key, ToLogScale(currentVolume));
            yield return null;
        }
        mixer.SetFloat(key, -80f);
    }

    public void PlaySound(SoundEffect soundEffect) {
        sfx.PlayOneShot(soundEffect);
    }

    private IEnumerator FadeFullscreenImage(float target, float fadeDuration, float delay = 0) {
        float original = fullscreenFadeImage.color.a;
        float timer = fadeDuration;
        if (delay > 0) {
            yield return new WaitForSeconds(delay);
        }

        Color color = fullscreenFadeImage.color;
        while (timer > 0) {
            timer -= Time.deltaTime;
            color.a = Mathf.Lerp(original, target, 1 - (timer / fadeDuration));
            fullscreenFadeImage.color = color;
            yield return null;
        }
    }

    private void OnStartGameEndFade(EventStartGameEndFade e) {
        StartCoroutine(FadeFullscreenImage(1, 1/3f));
        totalFadeRoutine = StartCoroutine(FadeVolume("OverrideVolume"));
    }

    private void ToggleFpsMonitor(InputAction.CallbackContext obj) {
        graphy.SetActive(!graphy.activeSelf);
    }

    private static float ToLinearScale(float x) {
        return Mathf.Pow(10, x / 20);
    }

    private static float ToLogScale(float x) {
        return 20 * Mathf.Log10(x);
    }
}
