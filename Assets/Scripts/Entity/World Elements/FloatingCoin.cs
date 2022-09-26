using UnityEngine;

public class FloatingCoin : Coin {

    private SpriteRenderer spriteRenderer;
    private BoxCollider2D hitbox;

    public override void Awake() {
        base.Awake();
        spriteRenderer = GetComponent<SpriteRenderer>();
        hitbox = GetComponent<BoxCollider2D>();
    }

    public override void OnCollectedChanged() {
        spriteRenderer.enabled = !IsCollected;
        hitbox.enabled = !IsCollected;
    }
}