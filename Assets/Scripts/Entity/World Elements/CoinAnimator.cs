using NSMB.Extensions;
using Quantum;
using Quantum.Profiling;
using UnityEngine;

public unsafe class CoinAnimator : QuantumEntityViewComponent {

    //---Serialized Variables
    [SerializeField] private LegacyAnimateSpriteRenderer defaultCoinAnimate, dottedCoinAnimate;
    [SerializeField] private AudioSource sfx;
    [SerializeField] private SpriteRenderer sRenderer;
    [SerializeField] private ParticleSystem sparkles;
    [SerializeField] private bool looseCoin;

    public void OnValidate() {
        this.SetIfNull(ref sfx);
        this.SetIfNull(ref sRenderer);
        this.SetIfNull(ref sparkles, UnityExtensions.GetComponentType.Children);
    }

    public void Start() {
        QuantumEvent.Subscribe<EventCoinChangedType>(this, OnCoinChangedType);
        QuantumEvent.Subscribe<EventCoinChangeCollected>(this, OnCoinChangedCollected);
        QuantumEvent.Subscribe<EventCoinBounced>(this, OnCoinBounced, NetworkHandler.FilterOutReplayFastForward);
    }

    public override void OnActivate(Frame f) {
        var coin = f.Unsafe.GetPointer<Coin>(EntityRef);

        bool dotted = coin->IsCurrentlyDotted;
        defaultCoinAnimate.isDisplaying = !dotted;
        dottedCoinAnimate.isDisplaying = dotted;
        sRenderer.enabled = true;
    }

    public override void OnDeactivate() {
        sRenderer.enabled = false;

        if (looseCoin) {
            sparkles.transform.SetParent(transform.parent);
            sparkles.gameObject.SetActive(true);
            sparkles.transform.position = sRenderer.transform.position;
            sparkles.Play();
        }
    }

    public override void OnUpdateView() {
        using var profilerScope = HostProfiler.Start("CoinAnimator.OnUpdateView");
        Frame f = PredictedFrame;
        if (!f.Exists(EntityRef)) {
            return;
        }

        var coin = f.Unsafe.GetPointer<Coin>(EntityRef);
        if (coin->IsFloating) {
            // Bodge: OnCoinChangedCollected doesnt work when collecting a coin at the exact same time as a level reset 
            sRenderer.enabled = !coin->IsCollected;
        } else {
            float despawnTimeRemaining = coin->Lifetime / 60f;
            sRenderer.enabled = !(despawnTimeRemaining < 3 && despawnTimeRemaining % 0.3f >= 0.15f);
        }
    }

    private void OnCoinBounced(EventCoinBounced e) {
        if (e.Entity != EntityRef) {
            return;
        }

        sfx.PlayOneShot(SoundEffect.World_Coin_Drop);
    }

    private void OnCoinChangedCollected(EventCoinChangeCollected e) {
        if (e.Entity != EntityRef) {
            return;
        }

        sRenderer.enabled = !e.Collected;
        if (e.Collected && !NetworkHandler.IsReplayFastForwarding) {
            if (looseCoin) {
                sparkles.transform.SetParent(transform.parent);
                sparkles.gameObject.SetActive(true);
                sparkles.transform.position = sRenderer.transform.position;
            }

            sparkles.Play();
        }
    }

    private void OnCoinChangedType(EventCoinChangedType e) {
        if (e.Entity != EntityRef) {
            return;
        }

        bool dotted = e.Coin.IsCurrentlyDotted;
        defaultCoinAnimate.isDisplaying = !dotted;
        dottedCoinAnimate.isDisplaying = dotted;

        if (!dotted && !NetworkHandler.IsReplayFastForwarding) {
            sfx.PlayOneShot(SoundEffect.World_Coin_Dotted_Spawn);
        }
    }
}