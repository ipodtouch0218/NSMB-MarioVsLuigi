using UnityEngine;

using Fusion;
using NSMB.Entities.Player;

namespace NSMB.Entities.Collectable {

    [ExecuteAlways]
    public class FloatingCoin : Coin {

        //---Networked Variables
        [Networked(OnChanged = nameof(OnIsDottedChanged))] private bool IsDotted { get; set; }
        [Networked] private TickTimer DottedTimer { get; set; }

        //---Serialized Variables
        [SerializeField] private bool dottedCoin;
        [SerializeField] private LegacyAnimateSpriteRenderer defaultCoinAnimate, dottedCoinAnimate;

        //---Components
        [SerializeField] private BoxCollider2D hitbox;

        public override void OnValidate() {
            base.OnValidate();
            if (!hitbox) hitbox = GetComponent<BoxCollider2D>();

            defaultCoinAnimate.isDisplaying = !dottedCoin;
            dottedCoinAnimate.isDisplaying = dottedCoin;
            sRenderer.sprite = (dottedCoin ? dottedCoinAnimate : defaultCoinAnimate).frames[0];
        }

        public override void Spawned() {
            if (dottedCoin)
                IsDotted = true;
        }

        public override void FixedUpdateNetwork() {
            hitbox.enabled = !Collector;

            if (DottedTimer.Expired(Runner)) {
                IsDotted = false;
                DottedTimer = TickTimer.None;
            }
        }

        public override void InteractWithPlayer(PlayerController player) {
            if (!IsDotted) {
                base.InteractWithPlayer(player);
                return;
            }

            if (!DottedTimer.IsRunning) {
                DottedTimer = TickTimer.CreateFromSeconds(Runner, 0.5f);
            }
        }

        public void ResetCoin() {
            IsDotted |= (dottedCoin && Collector);
            Collector = null;
        }

        //---OnChangeds
        public static void OnIsDottedChanged(Changed<FloatingCoin> changed) {
            FloatingCoin coin = changed.Behaviour;

            coin.defaultCoinAnimate.isDisplaying = !coin.IsDotted;
            coin.dottedCoinAnimate.isDisplaying = coin.IsDotted;

            if (!coin.IsDotted)
                coin.PlaySound(Enums.Sounds.World_Coin_Dotted_Spawn);
        }
    }
}
