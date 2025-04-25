using NSMB.Extensions;
using NSMB.Sound;
using Photon.Deterministic;
using Quantum;
using UnityEngine;

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
        QuantumEvent.Subscribe<EventTimerExpired>(this, OnTimerExpired, NetworkHandler.FilterOutReplayFastForward);
        QuantumEvent.Subscribe<EventMarioPlayerPreRespawned>(this, OnMarioPlayerPreRespawned, NetworkHandler.FilterOutReplayFastForward);
    }

    public override void OnUpdateView() {
        if (NetworkHandler.IsReplayFastForwarding) {
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
}
