using NSMB.Extensions;
using Quantum;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace NSMB.Loading {
    public class LoadingCanvas : MonoBehaviour {

        //---Serialized Variables
        [SerializeField] private AudioListener audioListener;
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private MarioLoader mario;

        [SerializeField] private Animator animator;
        [SerializeField] private CanvasGroup loadingGroup, readyGroup;
        [SerializeField] private Image readyBackground;

        //---Private Variables
        private bool initialized;
        private Coroutine fadeCoroutine;

        public void OnValidate() {
            this.SetIfNull(ref mario, UnityExtensions.GetComponentType.Children);
        }

        public void Awake() {
            QuantumEvent.Subscribe<EventGameStateChanged>(this, OnGameStateChanged);
        }

        public unsafe void Initialize(QuantumGame game) {
            initialized = true;

            int characterIndex = 0;

            Frame f = game.Frames.Predicted;
            List<PlayerRef> localPlayers = game.GetLocalPlayers();
            if (localPlayers.Count > 0) {
                PlayerRef player = localPlayers[0];
                var playerData = QuantumUtils.GetPlayerData(f, player);
                characterIndex = playerData->Character;
            }

            var characters = GlobalController.Instance.config.CharacterDatas;
            mario.Initialize(characters[characterIndex % characters.Length]);

            readyGroup.gameObject.SetActive(false);
            gameObject.SetActive(true);

            loadingGroup.alpha = 1;
            readyGroup.alpha = 0;
            readyBackground.color = Color.clear;

            animator.Play("waiting");

            audioSource.volume = 0;
            audioSource.Play();

            if (fadeCoroutine != null) {
                StopCoroutine(fadeCoroutine);
            }

            fadeCoroutine = StartCoroutine(FadeVolume(0.1f, true));

            //audioListener.enabled = true;
        }

        private void OnGameStateChanged(EventGameStateChanged e) {
            if (e.NewState == GameState.Starting) {
                EndLoading(e.Game);
            }
        }

        public unsafe void EndLoading(QuantumGame game) {
            Frame f = game.Frames.Predicted;

            bool validPlayer = game.GetLocalPlayers().Any(p => QuantumUtils.GetPlayerData(f, p)->IsSpectator);
            
            readyGroup.gameObject.SetActive(true);
            animator.SetTrigger(validPlayer ? "loaded" : "spectating");

            initialized = false;

            if (fadeCoroutine != null) {
                StopCoroutine(fadeCoroutine);
            }

            fadeCoroutine = StartCoroutine(FadeVolume(0.1f, false));
            //audioListener.enabled = false;
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

            if (!fadeIn) {
                audioSource.Stop();
            }
        }
    }
}
