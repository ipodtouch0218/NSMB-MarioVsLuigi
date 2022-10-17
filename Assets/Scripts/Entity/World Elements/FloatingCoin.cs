using UnityEngine;

public class FloatingCoin : Coin {

    //---Components
    private SpriteRenderer spriteRenderer;
    private BoxCollider2D hitbox;

    public override void Awake() {
        base.Awake();
        spriteRenderer = GetComponent<SpriteRenderer>();
        hitbox = GetComponent<BoxCollider2D>();
    }

    //---Coin overrides
    public override void OnCollectedChanged() {
        spriteRenderer.enabled = !Collector;
        hitbox.enabled = !Collector;
    }
}
