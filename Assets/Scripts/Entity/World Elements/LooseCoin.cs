using UnityEngine;

using Fusion;
using NSMB.Entities.Player;
using NSMB.Extensions;
using NSMB.Game;
using NSMB.Utils;

namespace NSMB.Entities.Collectable {
    public class LooseCoin : Coin {

        //---Networked Variables
        [Networked] private int CollectableTick { get; set; }
        [Networked] private byte CoinBounceAnimCounter { get; set; }

        //---Serialized Variables
        [SerializeField] private float despawn = 8;

        //---Components
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private LegacyAnimateSpriteRenderer spriteAnimation;
        [SerializeField] private BoxCollider2D hitbox;

        public override void OnValidate() {
            base.OnValidate();
            if (!spriteRenderer) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            if (!spriteAnimation) spriteAnimation = GetComponentInChildren<LegacyAnimateSpriteRenderer>();
            if (!hitbox) hitbox = GetComponent<BoxCollider2D>();
        }

        public override void Spawned() {
            base.Spawned();
            CollectableTick = (int) (Runner.Tick + (0.2f * Runner.TickRate));
            DespawnTimer = TickTimer.CreateFromSeconds(Runner, despawn);

            body.Velocity = Vector2.up * GameData.Instance.random.RangeInclusive(5.5f, 6f);
        }

        public override void Render() {
            base.Render();
            float despawnTimeRemaining = DespawnTimer.RemainingRenderTime(Runner) ?? 0f;
            spriteRenderer.enabled = !(despawnTimeRemaining < 3 && despawnTimeRemaining % 0.3f >= 0.15f);
        }

        public override void FixedUpdateNetwork() {
            base.FixedUpdateNetwork();
            if (GameData.Instance && GameData.Instance.GameEnded) {
                body.Velocity = Vector2.zero;
                spriteAnimation.enabled = false;
                body.Freeze = true;
                return;
            }

            if (!Object)
                return;

            bool inWall = Utils.Utils.IsAnyTileSolidBetweenWorldBox(body.Position + hitbox.offset, hitbox.size * transform.lossyScale * 0.75f);
            gameObject.layer = inWall ? Layers.LayerHitsNothing : Layers.LayerEntityNoGroundEntity;

            PhysicsDataStruct data = body.Data;
            if (data.OnGround) {
                if (data.HitRoof) {
                    // Crushed
                    Runner.Despawn(Object);
                    return;
                }

                // Bounce
                body.Velocity = -body.PreviousTickVelocity * 0.5f;
                if (body.Velocity.y < 0.2f)
                    body.Velocity = new(body.Velocity.x, 0);

                // TODO: doesn't always trigger, even for host. Strange.
                // IsForward is ok, the sound isnt top priority
                if (body.PreviousTickVelocity.y < -0.5f * (Mathf.Sin(data.FloorAngle) + 1f))
                    CoinBounceAnimCounter++;
            }
        }

        //---IPlayerInteractable overrides
        public override void InteractWithPlayer(PlayerController player, PhysicsDataStruct.IContactStruct contact = null) {
            if (Runner.Tick < CollectableTick)
                return;

            base.InteractWithPlayer(player, contact);
            Runner.Despawn(Object);
        }

        //---OnChangeds
        protected override void HandleRenderChanges(bool fillBuffer, ref NetworkBehaviourBuffer oldBuffer, ref NetworkBehaviourBuffer newBuffer) {
            base.HandleRenderChanges(fillBuffer, ref oldBuffer, ref newBuffer);

            foreach (var change in ChangesBuffer) {
                switch (change) {
                case nameof(CoinBounceAnimCounter):
                    OnCoinBounceAnimCounterChanged();
                    break;
                }
            }
        }

        public void OnCoinBounceAnimCounterChanged() {
            PlaySound(Enums.Sounds.World_Coin_Drop);
        }
    }
}
