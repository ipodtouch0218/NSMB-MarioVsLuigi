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
        [Networked(OnChanged = nameof(OnCoinBounceAnimCounterChanged))] private byte CoinBounceAnimCounter { get; set; }

        //---Serialized Variables
        [SerializeField] private float despawn = 8;

        //---Components
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private PhysicsEntity physics;
        [SerializeField] private LegacyAnimateSpriteRenderer spriteAnimation;
        [SerializeField] private BoxCollider2D hitbox;

        public override void OnValidate() {
            base.OnValidate();
            if (!spriteRenderer) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            if (!physics) physics = GetComponent<PhysicsEntity>();
            if (!spriteAnimation) spriteAnimation = GetComponentInChildren<LegacyAnimateSpriteRenderer>();
            if (!hitbox) hitbox = GetComponent<BoxCollider2D>();
        }

        public override void Spawned() {
            base.Spawned();
            CollectableTick = (int) (Runner.Tick + (0.2f * Runner.Simulation.Config.TickRate));
            DespawnTimer = TickTimer.CreateFromSeconds(Runner, despawn);

            body.velocity = Vector2.up * GameData.Instance.random.RangeInclusive(5.5f, 6f);
        }

        public override void Render() {
            float despawnTimeRemaining = DespawnTimer.RemainingRenderTime(Runner) ?? 0f;
            spriteRenderer.enabled = !(despawnTimeRemaining < 3 && despawnTimeRemaining % 0.3f >= 0.15f);
        }

        public override void FixedUpdateNetwork() {
            base.FixedUpdateNetwork();
            if (GameData.Instance && GameData.Instance.GameEnded) {
                body.velocity = Vector2.zero;
                spriteAnimation.enabled = false;
                body.freeze = true;
                return;
            }

            if (!Object)
                return;

            bool inWall = Utils.Utils.IsAnyTileSolidBetweenWorldBox(body.position + hitbox.offset, hitbox.size * transform.lossyScale * 0.75f);
            gameObject.layer = inWall ? Layers.LayerHitsNothing : Layers.LayerEntityNoGroundEntity;

            PhysicsDataStruct data = physics.UpdateCollisions();
            if (data.OnGround) {
                body.velocity -= body.velocity * Runner.DeltaTime;
                if (data.HitRoof) {
                    Runner.Despawn(Object);
                    return;
                }

                // TODO: doesn't always trigger, even for host. Strange.
                // IsForward is ok, the sound isnt top priority
                if (physics.PreviousTickVelocity.y < -0.5f * (Mathf.Sin(physics.Data.FloorAngle) + 1f))
                    CoinBounceAnimCounter++;
            }
        }

        //---IPlayerInteractable overrides
        public override void InteractWithPlayer(PlayerController player) {
            if (Runner.Tick < CollectableTick)
                return;

            base.InteractWithPlayer(player);
            Runner.Despawn(Object);
        }

        //---OnChangeds
        public static void OnCoinBounceAnimCounterChanged(Changed<LooseCoin> changed) {
            changed.Behaviour.PlaySound(Enums.Sounds.World_Coin_Drop);
        }
    }
}
