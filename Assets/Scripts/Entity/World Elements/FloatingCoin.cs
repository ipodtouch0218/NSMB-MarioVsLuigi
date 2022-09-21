using UnityEngine;

using Fusion;

public class FloatingCoin : Coin {

    private SpriteRenderer spriteRenderer;
    private BoxCollider2D hitbox;

    public override void Awake() {
        base.Awake();
        spriteRenderer = GetComponent<SpriteRenderer>();
        hitbox = GetComponent<BoxCollider2D>();
    }

    public static void OnCollect(Changed<FloatingCoin> changed) {
        FloatingCoin coin = changed.Behaviour;
        coin.spriteRenderer.enabled = !coin.IsCollected;
        coin.hitbox.enabled = !coin.IsCollected;
    }
}