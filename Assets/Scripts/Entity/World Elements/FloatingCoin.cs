using UnityEngine;

public class FloatingCoin : Coin {

    private SpriteRenderer spriteRenderer;
    private BoxCollider2D hitbox;

    public void Awake() {
        spriteRenderer = GetComponent<SpriteRenderer>();
        hitbox = GetComponent<BoxCollider2D>();
    }

    public override void OnCoinCollected() {
        spriteRenderer.enabled = IsCollected;
        hitbox.enabled = IsCollected;
    }
}