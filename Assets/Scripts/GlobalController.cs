using NSMB.Addons;
using NSMB.Networking;
using NSMB.Quantum;
using NSMB.Sound;
using NSMB.UI;
using NSMB.UI.Game;
using NSMB.UI.Loading;
using NSMB.UI.Options;
using NSMB.UI.Translation;
using NSMB.Utilities;
using NSMB.Utilities.Extensions;
using Quantum;
using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

#if UNITY_STANDALONE && !UNITY_EDITOR 
using NSMB.Replay;
using System.IO;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
#endif

namespace NSMB {
    public class GlobalController : Singleton<GlobalController> {

        //---Public Variables
        public TranslationManager translationManager;
        public RumbleManager rumbleManager;
        public AnimatedFader fader;
        public AddonManager addonManager;
        public PauseOptionMenuManager optionsManager;
        public AudioMixerManager audioMixerManager;

        public ScriptableRendererFeature outlineFeature;
        public GameObject connecting;
        public LoadingCanvas loadingCanvas;
        public Image fullscreenFadeImage;
        public Sprite[] pingIndicators;
        public AudioSource sfx;

        public PlayerSlotInfo[] playerSlots;

        [NonSerialized] public bool bootedWithReplayArg;
        [NonSerialized] public int windowWidth = 1280, windowHeight = 720;

        //---Private Variables
        private Coroutine totalAudioFadeRoutine;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
#if UNITY_EDITOR || UNITY_STANDALONE
        public static void LoadAssetBundles() {
#else
        public static async void LoadAssetBundles() {
#endif
            string[] bundleNames = { "basegame-assets", "basegame-scenes" };

            foreach (var bundle in bundleNames) {
                if (AssetBundle.GetAllLoadedAssetBundles().Any(ab => ab.name == bundle)) {
                    // Ignore if already loaded
                    continue;
                }
                
#if UNITY_EDITOR || UNITY_STANDALONE
                var loadedBundle = AssetBundle.LoadFromFile(Application.streamingAssetsPath + "/" + bundle);
                if (loadedBundle == null) {
                    Debug.LogError($"[Bundles] Critical error! Failed to load bundle {bundle} from {Application.streamingAssetsPath + "/" + bundle}");
                    continue;
                }
#else
                using var loadRequest = UnityWebRequestAssetBundle.GetAssetBundle(Application.streamingAssetsPath + "/" + bundle);
                await loadRequest.SendWebRequest();

                if (loadRequest.result != UnityWebRequest.Result.Success) {
                    Debug.LogError($"[Bundles] Critical error! Failed to load bundle {bundle} from {Application.streamingAssetsPath + "/" + bundle}");
                    continue;
                }

                var loadedBundle = DownloadHandlerAssetBundle.GetContent(loadRequest);
#endif
                Debug.Log($"[Bundles] Successfully loaded {loadedBundle.name} ({(loadedBundle.isStreamedSceneAssetBundle ? loadedBundle.GetAllScenePaths().Length + " scenes" : loadedBundle.GetAllAssetNames().Length + " assets")})");
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void CreateInstance() {
            Instantiate(Resources.Load("Static/GlobalController"));
        }

        public void Awake() {
            Set(this);
        }

        public void Start() {
            AuthenticationHandler.IsAuthenticating = false;
            QuantumEvent.Subscribe<EventStartGameEndFade>(this, OnStartGameEndFade);
            QuantumCallback.Subscribe<CallbackUnitySceneLoadDone>(this, OnUnitySceneLoadDone);
            loadingCanvas.Startup();

#if UNITY_STANDALONE && !UNITY_EDITOR
            var commandLineArgs = Environment.GetCommandLineArgs();
            for (int i = 0; i < commandLineArgs.Length; i++) {
                if (commandLineArgs[i] == "-replay" && commandLineArgs.Length > i + 1) {
                    StartReplayFromArgs(commandLineArgs[i + 1]);
                    break;
                }
            }
#endif
        }

        public void Update() {
            int newWindowWidth = Screen.width;
            int newWindowHeight = Screen.height;

#if UNITY_STANDALONE && !UNITY_EDITOR
            //todo: this jitters to hell
            var keyboard = Keyboard.current;
            if (Screen.fullScreenMode == FullScreenMode.Windowed && keyboard.leftShiftKey.isPressed && (windowWidth != newWindowWidth || windowHeight != newWindowHeight)) {
                newWindowHeight = (int) (newWindowWidth * (9f / 16f));
                Screen.SetResolution(newWindowWidth, newWindowHeight, FullScreenMode.Windowed);
            }
#endif

            windowWidth = newWindowWidth;
            windowHeight = newWindowHeight;
        }

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

#if UNITY_STANDALONE && !UNITY_EDITOR
        private void StartReplayFromArgs(string argReplayPath) {
            using FileStream input = new(argReplayPath, FileMode.Open);
            if (BinaryReplayFile.TryLoadNewFromStream(input, true, out var result) != ReplayParseResult.Success) {
                Debug.LogError("[Replay] Failed to parse replay file when booting with cmdline args...");
                return;
            }
            bootedWithReplayArg = true;
            NetworkHandler.OnError += StartReplayFromArgsErrorCallback;
            ActiveReplayManager.Instance.StartReplayPlayback(result);
        }

        private void StartReplayFromArgsErrorCallback(string msg, bool networkError) {
            bootedWithReplayArg = false;
            SceneManager.LoadScene(0);
            NetworkHandler.OnError -= StartReplayFromArgsErrorCallback;
        }
#endif

        private void OnStartGameEndFade(EventStartGameEndFade e) {
            if (MvLSceneLoader.Instance.CurrentLoadedMap != null) {
                // In a game scene
                StartCoroutine(FadeFullscreenImage(1, 1/3f));
                totalAudioFadeRoutine = StartCoroutine(audioMixerManager.FadeOut(AudioMixerManager.KeyOverride));
            }
        }
    }
}
