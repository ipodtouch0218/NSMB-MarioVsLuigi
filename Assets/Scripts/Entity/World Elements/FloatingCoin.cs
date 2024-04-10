using UnityEngine;

using Fusion;
using NSMB.Entities.Player;
using NSMB.Extensions;

namespace NSMB.Entities.Collectable {

    [ExecuteAlways]
    public class FloatingCoin : Coin {

        //---Networked Variables
        [Networked] private bool IsDotted { get; set; }
        [Networked] private TickTimer DottedTimer { get; set; }

        //---Serialized Variables
        [SerializeField] private bool dottedCoin;
        [SerializeField] private LegacyAnimateSpriteRenderer defaultCoinAnimate, dottedCoinAnimate;

        //---Components
        [SerializeField] private BoxCollider2D hitbox;

        public override void OnValidate() {
            base.OnValidate();
            this.SetIfNull(ref hitbox);

            defaultCoinAnimate.isDisplaying = !dottedCoin;
            dottedCoinAnimate.isDisplaying = dottedCoin;
            sRenderer.sprite = (dottedCoin ? dottedCoinAnimate : defaultCoinAnimate).frames[0];
        }

        public override void Spawned() {
            base.Spawned();
            IsDotted = dottedCoin;
        }

        public override void FixedUpdateNetwork() {
            hitbox.enabled = !Collector;

            if (DottedTimer.Expired(Runner)) {
                IsDotted = false;
                DottedTimer = TickTimer.None;
            }
        }

        public override void InteractWithPlayer(PlayerController player, PhysicsDataStruct.IContactStruct contact = null) {
            if (!IsDotted) {
                base.InteractWithPlayer(player, contact);
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
        protected override void HandleRenderChanges(bool fillBuffer, ref NetworkBehaviourBuffer oldBuffer, ref NetworkBehaviourBuffer newBuffer) {
            base.HandleRenderChanges(fillBuffer, ref oldBuffer, ref newBuffer);

            foreach (var change in ChangesBuffer) {
                switch (change) {
                case nameof(IsDotted):
                    OnIsDottedChanged();
                    break;
                }
            }
        }

        public void OnIsDottedChanged() {
            defaultCoinAnimate.isDisplaying = !IsDotted;
            dottedCoinAnimate.isDisplaying = IsDotted;

            if (!IsDotted) {
                PlaySound(Enums.Sounds.World_Coin_Dotted_Spawn);
            }
        }
    }
}
