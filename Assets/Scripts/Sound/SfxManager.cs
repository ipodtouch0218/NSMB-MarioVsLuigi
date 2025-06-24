using NSMB.Utilities.Extensions;
using Photon.Deterministic;
using Quantum;
using UnityEngine;
using static NSMB.Utilities.QuantumViewUtils;

namespace NSMB.Sound {
    public unsafe class SfxManager : QuantumSceneViewComponent {

        //---Serialized Variables
        [SerializeField] private AudioSource sfx;
        [SerializeField] private LoopingMusicPlayer musicPlayer;

        //---Private Variables
        private bool playedHurryUp;
        private int previousTimer;

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
                    sfx.PlayOneShot(SoundEffect.UI_HurryUp);
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

        private void OnGameResynced(CallbackGameResynced e) {
            Frame f = PredictedFrame;
            if (f.Global->Rules.IsTimerEnabled && f.Global->Timer < 60) {
                playedHurryUp = true;
            }
            previousTimer = 0;
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
