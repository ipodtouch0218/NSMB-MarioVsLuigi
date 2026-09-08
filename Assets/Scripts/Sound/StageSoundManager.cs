using NSMB.Quantum;
using NSMB.Utilities.Extensions;
using Photon.Deterministic;
using Quantum;
using System.Collections;
using System.Linq;
using UnityEngine;
using static NSMB.Utilities.QuantumViewUtils;

namespace NSMB.Sound {
    public unsafe class StageSoundManager : QuantumSceneViewComponent<StageContext> {

        //---Serialized Variables
        [SerializeField] private AudioSource sfx;
        [SerializeField] private LoopingMusicPlayer musicPlayer;
        [SerializeField] private MusicManager musicManager;

        //---Private Variables
        private bool playedHurryUp;
        private int previousTimer;
        private Coroutine hurryUpCoroutine;

        public void OnValidate() {
            this.SetIfNull(ref sfx);
        }

        public void Start() {
            QuantumCallback.Subscribe<CallbackGameResynced>(this, OnGameResynced);
            QuantumEvent.Subscribe<EventTimerExpired>(this, OnTimerExpired, FilterOutReplayFastForward);
            QuantumEvent.Subscribe<EventMarioPlayerPreRespawned>(this, OnMarioPlayerPreRespawned, FilterOutReplayFastForward);
            QuantumEvent.Subscribe<EventStageAutoRefresh>(this, OnStageAutoRefresh, FilterOutReplayFastForward);
        }

        public override void OnUpdateView() {
            if (IsReplayFastForwarding) {
                return;
            }

            Frame f = PredictedFrame;
            if (f.Global->Rules.IsTimerEnabled && f.Global->GameState == GameState.Playing) {
                FP timer = f.Global->Timer;

                if (!playedHurryUp && timer <= 60) {
                    this.StopCoroutineNullable(ref hurryUpCoroutine);
                    hurryUpCoroutine = StartCoroutine(HurryUpCoroutine());
                    playedHurryUp = true;
                }

                int timerHalfSeconds = Mathf.Max(0, FPMath.CeilToInt(timer * 2));
                if (timerHalfSeconds != previousTimer && timerHalfSeconds > 0) {
                    if (timerHalfSeconds <= 6) {
                        sfx.PlayOneShot(SoundEffect.UI_Countdown_0);
                    } else if (timerHalfSeconds <= 20 && (timerHalfSeconds % 2) == 0) {
                        sfx.PlayOneShot(SoundEffect.UI_Countdown_0);
                    }
                    previousTimer = timerHalfSeconds;
                }
            }
        }

        private IEnumerator HurryUpCoroutine() {
            var playedSounds = sfx.PlayOneShot(SoundEffect.UI_HurryUp);
            if (playedSounds.Count <= 0) {
                // No sounds played, no need to fade out/in.
                yield break;
            }

            float duration = playedSounds.Max(ac => ac.length);
            musicPlayer.AudioSource.volume = 0;
            yield return new WaitForSecondsRealtime(duration);

            // Fade back in
            const float fadeDuration = 0.5f;
            float timer = fadeDuration;
            while ((timer -= Time.deltaTime) > 0) {
                musicPlayer.AudioSource.volume =  1 - (timer / fadeDuration);
                yield return null;
            }
            musicPlayer.AudioSource.volume = 1;
            hurryUpCoroutine = null;
        }

        private void OnGameResynced(CallbackGameResynced e) {
            Frame f = PredictedFrame;
            if (f.Global->Rules.IsTimerEnabled && f.Global->Timer < 60) {
                playedHurryUp = true;
            }
            previousTimer = 0;
            musicPlayer.AudioSource.volume = 1;
            this.StopCoroutineNullable(ref hurryUpCoroutine);
        }

        private void OnTimerExpired(EventTimerExpired e) {
            sfx.PlayOneShot(SoundEffect.UI_Countdown_1);
        }

        private void OnMarioPlayerPreRespawned(EventMarioPlayerPreRespawned e) {
            Frame f = PredictedFrame;
            var mario = f.Unsafe.GetPointer<MarioPlayer>(e.Entity);

            if (Game.PlayerIsLocal(mario->PlayerRef) && !musicPlayer.IsPlaying) {
                sfx.PlayOneShot(SoundEffect.UI_StartGame);
            }
        }

        private void OnStageAutoRefresh(EventStageAutoRefresh e) {
            sfx.PlayOneShot(SoundEffect.World_Star_CollectOthers);
        }
    }
}
