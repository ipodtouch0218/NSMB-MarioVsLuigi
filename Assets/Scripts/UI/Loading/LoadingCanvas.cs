using NSMB.Utilities.Extensions;
using Quantum;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using static NSMB.Utilities.QuantumViewUtils;

namespace NSMB.UI.Loading {
    public unsafe class LoadingCanvas : MonoBehaviour {

        public static event Action<bool> OnLoadingEnded;

        //---Serialized Variables
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private MarioLoader mario;

        [SerializeField] private Animator animator;
        [SerializeField] private CanvasGroup loadingGroup, readyGroup;
        [SerializeField] private Image readyBackground, readyImage;

        [SerializeField] private CharacterAsset defaultCharacterAsset;

        //---Private Variables
        private Coroutine fadeVolumeCoroutine, endCoroutine;
        private bool running;

        public void OnValidate() {
            this.SetIfNull(ref mario, UnityExtensions.GetComponentType.Children);
        }

        public void Awake() {
            QuantumCallback.Subscribe<CallbackGameStarted>(this, OnGameStarted, onlyIfActiveAndEnabled: true);
            QuantumEvent.Subscribe<EventGameStateChanged>(this, OnGameStateChanged, onlyIfActiveAndEnabled: true);
        }

        public void Initialize(QuantumGame game) {
            if (running) {
                return;
            }

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
                character = f.FindAsset(characters[characterIndex % characters.Length]);
            }

            mario.Initialize(character);
            readyImage.sprite = character.ReadySprite;

            readyGroup.gameObject.SetActive(false);
            gameObject.SetActive(true);

            loadingGroup.alpha = 1;
            readyGroup.alpha = 0;
            readyBackground.color = Color.clear;

            animator.Play("waiting");

            audioSource.volume = 0;
            audioSource.Play();

            if (fadeVolumeCoroutine != null) {
                StopCoroutine(fadeVolumeCoroutine);
            }

            fadeVolumeCoroutine = StartCoroutine(FadeVolume(0.1f, true));
            running = true;
        }

        private void OnGameStarted(CallbackGameStarted e) {
            if (!IsReplay) {
                EndLoading(e.Game);
            }
        }

        private void OnGameStateChanged(EventGameStateChanged e) {
            if (e.NewState > GameState.WaitingForPlayers) {
                EndLoading(e.Game);
            }
        }

        public void EndLoading(QuantumGame game) {
            if (running && endCoroutine == null) {
                endCoroutine = StartCoroutine(EndLoadingRoutine(game, game.Frames.Predicted.Global->GameState));
            }
        } 

        public IEnumerator EndLoadingRoutine(QuantumGame game, GameState state) {
            if (!IsReplay) {
                yield return new WaitForSeconds(1);
            }

            Frame f = game.Frames.Predicted;

            bool longIntro = !IsReplay && (state <= GameState.Starting || game.GetLocalPlayers().Any(p => !(QuantumUtils.GetPlayerDataSafe(f, p)?.IsSpectator ?? true)));
            
            readyGroup.gameObject.SetActive(true);
            animator.SetTrigger(longIntro  ? "loaded" : "spectating");

            if (fadeVolumeCoroutine != null) {
                StopCoroutine(fadeVolumeCoroutine);
            }

            fadeVolumeCoroutine = StartCoroutine(FadeVolume(0.1f, false));
            //audioListener.enabled = false;

            OnLoadingEnded?.Invoke(longIntro);
            running = false;
            endCoroutine = null;
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
