using UnityEngine;

using Fusion;

public class FloatingCoin : Coin {

    private SpriteRenderer spriteRenderer;
    private BoxCollider2D hitbox;

    public override void Awake() {
        //we dont use body so...
        //base.Awake();
        spriteRenderer = GetComponent<SpriteRenderer>();
        hitbox = GetComponent<BoxCollider2D>();
    }

    public static void OnCollect(Changed<FloatingCoin> changed) {
        FloatingCoin coin = changed.Behaviour;
        coin.spriteRenderer.enabled = !coin.IsCollected;
        coin.hitbox.enabled = !coin.IsCollected;
    }
}