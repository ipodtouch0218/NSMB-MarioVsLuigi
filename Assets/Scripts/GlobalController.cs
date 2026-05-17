using NSMB.Addons;
using NSMB.Networking;
using NSMB.Quantum;
using NSMB.UI.Loading;
using NSMB.UI.Options;
using NSMB.UI.Translation;
using NSMB.Utilities.Extensions;
using Quantum;
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using NSMB.UI.Game;
using NSMB.Sound;
using NSMB.UI;
using System.IO;
using NSMB.Utilities;

#if UNITY_STANDALONE
using NSMB.UI.MainMenu.Submenus.Replays;
using UnityEngine.Profiling;
#endif

namespace NSMB {
    public class GlobalController : Singleton<GlobalController> {

        //---Public Variables
        public TranslationManager translationManager;
        public DiscordController discordController;
        public RumbleManager rumbleManager;
        public AnimatedFader fader;
        public AddonManager addonManager;
        public PauseOptionMenuManager optionsManager;
        public AudioMixerManager audioMixerManager;
        public SimulationConfig config;

        public ScriptableRendererFeature outlineFeature;
        public GameObject graphy, connecting;
        public LoadingCanvas loadingCanvas;
        public Image fullscreenFadeImage;
        public Sprite[] pingIndicators;
        public AudioSource sfx;

        public PlayerSlotInfo[] playerSlots;

        [NonSerialized] public bool checkedForVersion = false, firstConnection = true;
        [NonSerialized] public int windowWidth = 1280, windowHeight = 720;

        //---Private Variables
        private Coroutine totalAudioFadeRoutine;
#if IDLE_LOCK_30FPS
        private int previousVsyncCount, previousFrameRate;
#endif

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void CreateInstance() {
            Instantiate(Resources.Load("Static/GlobalController"));
        }

        public void Awake() {
            Set(this);

            firstConnection = true;
            checkedForVersion = false;
        }

        public void Start() {
            AuthenticationHandler.IsAuthenticating = false;
            Settings.Controls.Enable();
            Settings.Controls.Debug.FPSMonitor.performed += ToggleFpsMonitor;
            QuantumEvent.Subscribe<EventStartGameEndFade>(this, OnStartGameEndFade);
            QuantumCallback.Subscribe<CallbackUnitySceneLoadDone>(this, OnUnitySceneLoadDone);
            loadingCanvas.Startup();
        }

        public void OnDestroy() {
            Settings.Controls.Debug.FPSMonitor.performed -= ToggleFpsMonitor;
            Settings.Controls.Disable();
        }

        public void Update() {
            int newWindowWidth = Screen.width;
            int newWindowHeight = Screen.height;

            var keyboard = Keyboard.current;

            if (keyboard[Key.F3].wasPressedThisFrame) {
                var game = QuantumRunner.DefaultGame;
                if (game != null) {
                    Frame f = game.Frames.Predicted;
                    string dump = f.DumpFrame(Frame.DumpFlag_NoHeap);
                    string path = $"{Application.persistentDataPath}/frame_dump_{DateTimeOffset.Now.ToUnixTimeMilliseconds()}.txt";
                    Debug.Log($"[Debug] Created frame dump for frame {f.Number}- writing to {path}");
                    File.WriteAllText(path, dump);
                    PlaySound(SoundEffect.Player_Sound_PowerupCollect);
                } else {
                    PlaySound(SoundEffect.UI_Error);
                }
            }

#if UNITY_STANDALONE
    #if !UNITY_EDITOR
            //todo: this jitters to hell
            if (Screen.fullScreenMode == FullScreenMode.Windowed && keyboard.leftShiftKey.isPressed && (windowWidth != newWindowWidth || windowHeight != newWindowHeight)) {
                newWindowHeight = (int) (newWindowWidth * (9f / 16f));
                Screen.SetResolution(newWindowWidth, newWindowHeight, FullScreenMode.Windowed);
            }
    #endif


            if (keyboard[Key.F6].wasPressedThisFrame && !string.IsNullOrEmpty(Application.consoleLogPath)) {
                System.Diagnostics.Process.Start(Path.GetDirectoryName(Application.consoleLogPath));
                PlaySound(SoundEffect.Player_Sound_PowerupCollect);
            }

            if (keyboard[Key.F7].wasPressedThisFrame && !string.IsNullOrEmpty(ReplayListManager.ReplayDirectory)) {
                System.Diagnostics.Process.Start(ReplayListManager.ReplayDirectory);
                PlaySound(SoundEffect.Player_Sound_PowerupCollect);
            }
            
            if (keyboard[Key.F8].wasPressedThisFrame && addonManager.isActiveAndEnabled && !string.IsNullOrEmpty(AddonManager.LocalFolderPath)) {
                System.Diagnostics.Process.Start(AddonManager.LocalFolderPath);
                PlaySound(SoundEffect.Player_Sound_PowerupCollect);
            }

            if (Debug.isDebugBuild && keyboard[Key.F9].wasPressedThisFrame) {
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
#endif

            if (windowWidth != newWindowWidth || windowHeight != newWindowHeight) {
                windowWidth = newWindowWidth;
                windowHeight = newWindowHeight;
            }

        }

#if IDLE_LOCK_30FPS
        public void OnApplicationFocus(bool focus) {
            if (focus) {
                QualitySettings.vSyncCount = previousVsyncCount;
                Application.targetFrameRate = previousFrameRate;
            } else {
                // Lock framerate when losing focus to (hopefully) disable browsers slowing the game
                previousVsyncCount = QualitySettings.vSyncCount;
                previousFrameRate = Application.targetFrameRate;

                QualitySettings.vSyncCount = 0;
                Application.targetFrameRate = 30;
            }
        }
#endif

        public void OnUnitySceneLoadDone(CallbackUnitySceneLoadDone e) {
            if (e.SceneName != null) {
                foreach (int slot in e.Game.GetLocalPlayerSlots()) {
                    e.Game.SendCommand(slot, new CommandPlayerLoaded());
                }
            }

            this.StopCoroutineNullable(ref totalAudioFadeRoutine);
            audioMixerManager.SetFloat(AudioMixerManager.KeyOverride, 0f);
            StartCoroutine(FadeFullscreenImage(0, 1/3f, 0.1f));
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
            if (MvLSceneLoader.Instance.CurrentLoadedMap != null) {
                // In a game scene
                StartCoroutine(FadeFullscreenImage(1, 1/3f));
                totalAudioFadeRoutine = StartCoroutine(audioMixerManager.FadeOut(AudioMixerManager.KeyOverride));
            }
        }

        private void ToggleFpsMonitor(InputAction.CallbackContext obj) {
            graphy.SetActive(!graphy.activeSelf);
        }
    }
}
