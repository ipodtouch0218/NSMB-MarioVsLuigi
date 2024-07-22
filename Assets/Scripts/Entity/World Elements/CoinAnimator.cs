using NSMB.Extensions;
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
        QuantumEvent.Subscribe<EventCoinChangedType>(this, OnCoinChangedType);
        QuantumEvent.Subscribe<EventCoinChangeCollected>(this, OnCoinChangedCollected);
    }

    public void Initialize(QuantumGame game) {
        Frame f = game.Frames.Predicted;
        var coin = f.Get<Coin>(entity.EntityRef);

        bool dotted = coin.IsCurrentlyDotted;
        defaultCoinAnimate.isDisplaying = !dotted;
        dottedCoinAnimate.isDisplaying = dotted;
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