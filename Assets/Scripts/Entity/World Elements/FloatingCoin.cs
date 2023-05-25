using UnityEngine;

namespace NSMB.Entities.Collectable {
    public class FloatingCoin : Coin {

        //---Components
        [SerializeField] private BoxCollider2D hitbox;

        public override void OnValidate() {
            base.OnValidate();
            if (!hitbox) hitbox = GetComponent<BoxCollider2D>();
        }

        public override void FixedUpdateNetwork() {
            hitbox.enabled = !Collector;
        }

        public void ResetCoin() {
            Collector = null;
        }
    }
}
