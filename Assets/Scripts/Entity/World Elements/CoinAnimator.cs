using NSMB.Extensions;
using Photon.Deterministic.Protocol;
using Quantum;
using UnityEngine;

public class CoinAnimator : MonoBehaviour {

    //---Serialized Variables
    [SerializeField] private QuantumEntityView entity;
    [SerializeField] private LegacyAnimateSpriteRenderer defaultCoinAnimate, dottedCoinAnimate;
    [SerializeField] private AudioSource sfx;
    [SerializeField] private SpriteRenderer sRenderer;

    public void OnValidate() {
        this.SetIfNull(ref entity);
        this.SetIfNull(ref sfx);
        this.SetIfNull(ref sRenderer);
    }

    public void Start() {
        QuantumCallback.Subscribe<CallbackUpdateView>(this, OnUpdateView);

        QuantumEvent.Subscribe<EventCoinChangedType>(this, OnCoinChangedType);
        QuantumEvent.Subscribe<EventCoinChangeCollected>(this, OnCoinChangedCollected);
        QuantumEvent.Subscribe<EventCoinBounced>(this, OnCoinBounced);
    }

    public void Initialize(QuantumGame game) {
        Frame f = game.Frames.Predicted;
        var coin = f.Get<Coin>(entity.EntityRef);

        bool dotted = coin.IsCurrentlyDotted;
        defaultCoinAnimate.isDisplaying = !dotted;
        dottedCoinAnimate.isDisplaying = dotted;
    }

    private void OnUpdateView(CallbackUpdateView e) {
        Frame f = e.Game.Frames.Predicted;
        if (!f.Exists(entity.EntityRef)) {
            return;
        }

        var coin = f.Get<Coin>(entity.EntityRef);
        if (!coin.IsFloating) {
            float despawnTimeRemaining = coin.Lifetime / 60f;
            sRenderer.enabled = !(despawnTimeRemaining < 3 && despawnTimeRemaining % 0.3f >= 0.15f);
        }
    }

    private void OnCoinBounced(EventCoinBounced e) {
        if (e.Entity != entity.EntityRef) {
            return;
        }

        sfx.PlayOneShot(SoundEffect.World_Coin_Drop);
    }

    private void OnCoinChangedCollected(EventCoinChangeCollected e) {
        if (e.Entity != entity.EntityRef) {
            return;
        }

        sRenderer.enabled = !e.Coin.IsCollected;
    }

    private void OnCoinChangedType(EventCoinChangedType e) {
        if (e.Entity != entity.EntityRef) {
            return;
        }

        bool dotted = e.Coin.IsCurrentlyDotted;
        defaultCoinAnimate.isDisplaying = !dotted;
        dottedCoinAnimate.isDisplaying = dotted;

        if (!dotted) {
            sfx.PlayOneShot(SoundEffect.World_Coin_Dotted_Spawn);
        }
    }
}