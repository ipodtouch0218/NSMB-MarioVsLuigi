using NSMB.Extensions;
using Quantum;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace NSMB.Loading {
    public class LoadingCanvas : MonoBehaviour {

        public static event Action<bool> OnLoadingEnded;

        //---Serialized Variables
        [SerializeField] private AudioListener audioListener;
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private MarioLoader mario;

        [SerializeField] private Animator animator;
        [SerializeField] private CanvasGroup loadingGroup, readyGroup;
        [SerializeField] private Image readyBackground;

        [SerializeField] private CharacterAsset defaultCharacterAsset;

        //---Private Variables
        private Coroutine fadeCoroutine;

        public void OnValidate() {
            this.SetIfNull(ref mario, UnityExtensions.GetComponentType.Children);
        }

        public void Awake() {
            QuantumEvent.Subscribe<EventGameStateChanged>(this, OnGameStateChanged, onlyIfActiveAndEnabled: true);
            QuantumCallback.Subscribe<CallbackGameStarted>(this, OnGameStarted, onlyIfActiveAndEnabled: true);
        }

        public unsafe void Initialize(QuantumGame game) {
            int characterIndex = 0;
            CharacterAsset character = defaultCharacterAsset;
            if (game != null) {
                Frame f = game.Frames.Predicted;
                List<PlayerRef> localPlayers = game.GetLocalPlayers();
                if (localPlayers.Count > 0) {
                    PlayerRef player = localPlayers[0];
                    var playerData = QuantumUtils.GetPlayerData(f, player);

                    if (playerData != null) {
                        characterIndex = playerData->Character;
                    } else {
                        characterIndex = Settings.Instance.generalCharacter;
                    }
                }

                var characters = f.SimulationConfig.CharacterDatas;
                character = characters[characterIndex % characters.Length];
            }

            mario.Initialize(character);

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

        private void OnGameStarted(CallbackGameStarted e) {
            EndLoading(e.Game);
        }

        private void OnGameStateChanged(EventGameStateChanged e) {
            if (e.NewState == GameState.Starting) {
                EndLoading(e.Game);
            }
        }

        public unsafe void EndLoading(QuantumGame game) {
            StartCoroutine(EndLoadingRoutine(game));
        } 

        public IEnumerator EndLoadingRoutine(QuantumGame game) {
            if (!NetworkHandler.IsReplay) {
                yield return new WaitForSeconds(1);
            }

            Frame f = game.Frames.Predicted;

            bool validPlayer = game.GetLocalPlayers().Any(p => !(QuantumUtils.GetPlayerDataSafe(f, p)?.IsSpectator ?? true));
            
            readyGroup.gameObject.SetActive(true);
            animator.SetTrigger(validPlayer ? "loaded" : "spectating");

            if (fadeCoroutine != null) {
                StopCoroutine(fadeCoroutine);
            }

            fadeCoroutine = StartCoroutine(FadeVolume(0.1f, false));
            //audioListener.enabled = false;

            OnLoadingEnded?.Invoke(validPlayer);
        }

        public void EndAnimation() {
            gameObject.SetActive(false);
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
