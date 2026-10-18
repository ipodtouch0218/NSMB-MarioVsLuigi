using JimmysUnityUtilities;
using NSMB.Utilities;
using NSMB.Utilities.Extensions;
using Quantum;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace NSMB.UI.Intro {
    public class IntroManager : MonoBehaviour {

        //---Serialized Variables
        [SerializeField] private GameObject others;
        [SerializeField] private Image fullscreenImage, logo;
        [SerializeField] private AudioSource sfx;
        [SerializeField] private List<SoundEffect> excludedSounds;
        [SerializeField] private float logoBounceDuration = 0.1f, logoBounceHeight = 15f;

        //---Private Variables
        private SoundEffect[] possibleSfx;
        private Coroutine logoBounceRoutine;
        //private bool doneLoadingBundles;

        public void Start() {
            if (GlobalController.Instance.bootedWithReplayArg) {
                gameObject.SetActive(false);
                return;
            }
            
            //StartCoroutine(LoadAssetBundles());
            StartCoroutine(IntroSequence());
            
            possibleSfx = ((SoundEffect[]) Enum.GetValues(typeof(SoundEffect)))
                .Where(se => !excludedSounds.Contains(se))
                .Where(se => !se.ToString().StartsWith("UI_"))
                .ToArray();
        }

        public void PlayRandomCharacterSound() {
            var possibleCharacters = AssetRepository<CharacterAsset>.AllAssetRefs;
            var randomCharacterRef = possibleCharacters[UnityEngine.Random.Range(0, possibleCharacters.Count)];
            var randomCharacter = QuantumUnityDB.GetGlobalAsset(randomCharacterRef);
            var randomSfx = possibleSfx[UnityEngine.Random.Range(0, possibleSfx.Length)];
            sfx.PlayOneShot(randomSfx, new List<ISoundOverrideProvider>() { randomCharacter });

            this.StopCoroutineNullable(ref logoBounceRoutine);
            logoBounceRoutine = StartCoroutine(LogoBounce());
        }

        private IEnumerator LogoBounce() {
            float time = logoBounceDuration;

            RectTransform logoTf = (RectTransform) logo.transform;
            while (time > 0) {
                time -= Time.deltaTime;
                time = Mathf.Max(0, time);

                logoTf.SetAnchoredPositionY(Mathf.Sin(time * Mathf.PI / logoBounceDuration) * logoBounceHeight);
                yield return null;
            }
            logoBounceRoutine = null;
        }

        /*
        private IEnumerator LoadAssetBundles() {
#if !UNITY_EDITOR
            string[] bundleNames = { "basegame-assets", "basegame-scenes" };

            foreach (var bundle in bundleNames) {
                if (AssetBundle.GetAllLoadedAssetBundles().Any(ab => ab.name == bundle)) {
                    // Ignore if already loaded
                    continue;
                }

                using var loadRequest = UnityWebRequestAssetBundle.GetAssetBundle(Application.streamingAssetsPath + "/" + bundle);
                yield return loadRequest.SendWebRequest();

                if (loadRequest.result != UnityWebRequest.Result.Success) {
                    // Throw error!
                    Debug.LogError($"[Bundles] Critical error! Failed to load bundle {bundle} from {Application.streamingAssetsPath + "/" + bundle}");
                    yield break;
                }

                var loadedBundle = DownloadHandlerAssetBundle.GetContent(loadRequest);
                Debug.Log($"[Bundles] Successfully loaded {loadedBundle.name} ({(loadedBundle.isStreamedSceneAssetBundle ? loadedBundle.GetAllScenePaths().Length + " scenes" : loadedBundle.GetAllAssetNames().Length + " assets")})");
            }
#endif

            Debug.Log("[Bundles] Loaded all base game content!");
            doneLoadingBundles = true;
            yield break;
        }
        */

        private IEnumerator IntroSequence() {
            yield return new WaitForSeconds(0.75f);
            sfx.Play();
            yield return FadeImageToValue(fullscreenImage, 0, 0.33f);
            yield return new WaitForSeconds(0.5f);

            /*
            while (!doneLoadingBundles) {
                yield return null;
            }
            */

#if !DISABLE_SCENE_CHANGE
            AsyncOperation sceneLoad = SceneManager.LoadSceneAsync("MainMenu", LoadSceneMode.Additive);
            sceneLoad.allowSceneActivation = false;
#endif

            yield return new WaitForSeconds(0.75f);
            fullscreenImage.color = new Color(0, 0, 0, 0);
            yield return FadeImageToValue(fullscreenImage, 1, 0.33f);

            EventSystem.current.gameObject.SetActive(false);
            
            yield return new WaitForSeconds(0.75f);

#if !DISABLE_SCENE_CHANGE
            while (sceneLoad.progress < 0.9f) {
                yield return null;
            }

            sceneLoad.allowSceneActivation = true;
            while (!sceneLoad.isDone) {
                yield return null;
            }
            others.SetActive(false);

            // Fuck this lag spike man
            yield return new WaitForSeconds(0.1f);
            do {
                yield return null;
            } while (Time.deltaTime >= Time.maximumDeltaTime);

            yield return FadeImageToValue(fullscreenImage, 0, 0.33f);
            yield return new WaitForSeconds(0.5f);
            SceneManager.UnloadSceneAsync(SceneManager.GetActiveScene());
#endif
        }

        private static IEnumerator FadeImageToValue(Image image, float newAlpha, float time) {
            float remainingTime = time;
            float startingAlpha = image.color.a;

            Color newColor = image.color;
            while ((remainingTime -= Time.deltaTime) > 0) {
                newColor.a = Mathf.Lerp(startingAlpha, newAlpha, 1f - (remainingTime / time));
                image.color = newColor;
                yield return null;
            }

            newColor.a = newAlpha;
            image.color = newColor;
        }
    }
}
