using NSMB.Extensions;
using Quantum;
using UnityEngine;

public class BreakableObjectAnimator : MonoBehaviour {

    //---Serialized Variables
    [SerializeField] private QuantumEntityView entity;
    [SerializeField] private SpriteRenderer sRenderer;
    [SerializeField] private Sprite unbrokenSprite, brokenSprite;
    [SerializeField] private SimplePhysicsMover breakPrefab;

    public void OnValidate() {
        this.SetIfNull(ref entity);
        this.SetIfNull(ref sRenderer);

        QuantumCallback.Subscribe<CallbackUpdateView>(this, OnUpdateView);
        QuantumEvent.Subscribe<EventBreakableObjectBroken>(this, OnBreakableObjectBroken);
    }

    public unsafe void OnUpdateView(CallbackUpdateView e) {
        QuantumGame game = e.Game;
        Frame f = game.Frames.Verified;
        if (!f.Exists(entity.EntityRef)
            || f.Global->GameState < GameState.Playing) {
            return;
        }

        var breakable = f.Get<BreakableObject>(entity.EntityRef);
        sRenderer.size = new Vector2(sRenderer.size.x, breakable.CurrentHeight.AsFloat);
        sRenderer.sprite = breakable.IsBroken ? brokenSprite : unbrokenSprite;
    }

    private void OnBreakableObjectBroken(EventBreakableObjectBroken e) {
        if (e.Entity != entity.EntityRef) {
            return;
        }

        var pipe = e.Frame.Get<BreakableObject>(e.Entity);

        SimplePhysicsMover particle = Instantiate(breakPrefab, transform.position, transform.rotation);
        //particle.transform.localScale = transform.localScale;
        particle.transform.position += particle.transform.up * ((pipe.MinimumHeight + (e.Height / 2)) / 2).AsFloat;
        SpriteRenderer sRenderer = particle.GetComponentInChildren<SpriteRenderer>();
        sRenderer.size = new Vector2(sRenderer.size.x, e.Height.AsFloat);
        sRenderer.transform.localPosition = new Vector2(0, -(e.Height / 2).AsFloat);

        particle.velocity = (e.LaunchDirection.ToUnityVector2() * 9.5f) + (Vector2.up * 3.5f);

        Vector2 a = particle.velocity;
        Vector2 b = transform.up;
        float angularVelocity = (a.x * b.y) - (b.x * a.y);
        particle.angularVelocity = angularVelocity * -(400f/5);
    }
}
