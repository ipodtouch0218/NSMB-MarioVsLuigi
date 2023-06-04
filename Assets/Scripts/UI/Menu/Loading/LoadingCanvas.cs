using System.Collections;
using UnityEngine;
using UnityEngine.UI;

using NSMB.Extensions;

namespace NSMB.Loading {
    public class LoadingCanvas : MonoBehaviour {

        //---Serialized Variables
        [SerializeField] private AudioListener audioListener;
        [SerializeField] private AudioSource audioSource;

        [SerializeField] private Animator animator;
        [SerializeField] private CanvasGroup loadingGroup, readyGroup;
        [SerializeField] private Image readyBackground;

        //---Private Variables
        private bool initialized;
        private Coroutine fadeCoroutine;

        public void Initialize() {
            if (initialized)
                return;

            initialized = true;

            readyGroup.gameObject.SetActive(false);
            gameObject.SetActive(true);

            loadingGroup.alpha = 1;
            readyGroup.alpha = 0;
            readyBackground.color = Color.clear;

            animator.Play("waiting");

            audioSource.volume = 0;
            audioSource.Play();

            if (fadeCoroutine != null)
                StopCoroutine(fadeCoroutine);
            fadeCoroutine = StartCoroutine(FadeVolume(0.1f, true));

            audioListener.enabled = true;
        }

        public void EndLoading() {
            bool spectator = NetworkHandler.Runner.GetLocalPlayerData().IsCurrentlySpectating;
            readyGroup.gameObject.SetActive(true);
            animator.SetTrigger(spectator ? "spectating" : "loaded");

            initialized = false;

            if (fadeCoroutine != null)
                StopCoroutine(fadeCoroutine);

            fadeCoroutine = StartCoroutine(FadeVolume(0.1f, false));
            audioListener.enabled = false;
        }

        public void EndAnimation() {
            gameObject.SetActive(false);
            initialized = false;
        }

        private IEnumerator FadeVolume(float fadeTime, bool fadeIn) {
            float currentVolume = audioSource.volume;
            float fadeRate = 1f / fadeTime;

            while (true) {
                currentVolume += fadeRate * Time.deltaTime * (fadeIn ? 1 : -1);

                if (currentVolume < 0 || currentVolume > 1) {
                    audioSource.volume = Mathf.Clamp01(currentVolume);
                    break;
                }

                audioSource.volume = currentVolume;
                yield return null;
            }

            if (!fadeIn)
                audioSource.Stop();
        }
    }
}
