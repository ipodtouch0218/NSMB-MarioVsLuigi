using JimmysUnityUtilities;
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
        private SoundEffectDataAttribute[] possibleSfx;
        private CharacterAsset[] possibleCharacters;
        private Coroutine logoBounceRoutine;

        public void Start() {
            StartCoroutine(IntroSequence());
            possibleSfx = ((SoundEffect[]) Enum.GetValues(typeof(SoundEffect)))
                .Where(se => !excludedSounds.Contains(se))
                .Select(se => se.GetSoundData())
                .Where(sd => sd.Sound.Contains("{char}"))
                .ToArray();
            possibleCharacters = GlobalController.Instance.config.CharacterDatas
                .Select(ar => QuantumUnityDB.GetGlobalAsset(ar))
                .ToArray();
        }

        public void PlayRandomCharacterSound() {
            SoundEffectDataAttribute data = possibleSfx[UnityEngine.Random.Range(0, possibleSfx.Length)];
            CharacterAsset character = possibleCharacters[UnityEngine.Random.Range(0, possibleCharacters.Length)];
            int variant = data.Variants <= 1 ? 0 : UnityEngine.Random.Range(1, data.Variants + 1);

            sfx.PlayOneShot(data, character, variant);

            if (logoBounceRoutine != null) {
                StopCoroutine(logoBounceRoutine);
            }
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

        private IEnumerator IntroSequence() {
            yield return new WaitForSeconds(0.75f);
            sfx.Play();
            yield return FadeImageToValue(fullscreenImage, 0, 0.33f);
            yield return new WaitForSeconds(0.5f);

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
#endif
#if !DISABLE_SCENE_CHANGE
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
