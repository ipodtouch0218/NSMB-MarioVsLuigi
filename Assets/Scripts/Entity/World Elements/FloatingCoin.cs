using UnityEngine;

public class FloatingCoin : Coin {

    //---Components
    private BoxCollider2D hitbox;

    public override void Awake() {
        base.Awake();
        hitbox = GetComponent<BoxCollider2D>();
    }

    public override void FixedUpdateNetwork() {
        hitbox.enabled = !Collector;
    }

    public void ResetCoin() {
        Collector = null;
    }
}
