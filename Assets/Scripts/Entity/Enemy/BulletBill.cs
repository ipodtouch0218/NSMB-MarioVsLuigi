using UnityEngine;

using Fusion;
using NSMB.Entities.Player;
using NSMB.Game;
using NSMB.Tiles;
using NSMB.Utils;

namespace NSMB.Entities.Enemies {
    public class BulletBill : KillableEntity {

        //---Serialized Variables
        [SerializeField] private float speed, playerSearchRadius = 4, despawnDistance = 8;
        [SerializeField] private ParticleSystem shootParticles, trailParticles;

        //---Private Variables
        private Vector2 searchVector;

        public void Awake() {
            searchVector = new(playerSearchRadius * 2, playerSearchRadius * 4);
        }

        public override void FixedUpdateNetwork() {

            if (GameManager.Instance.GameEnded) {
                body.Velocity = Vector2.zero;
                body.Freeze = true;
                legacyAnimation.enabled = false;
                return;
            }

            if (DespawnTimer.Expired(Runner)) {
                DespawnTimer = TickTimer.None;
                DespawnEntity();
            }

            gameObject.layer = (IsDead || !IsActive) ? Layers.LayerHitsNothing : Layers.LayerEntityHitbox;

            if (IsFrozen || IsDead) {
                return;
            }

            body.Velocity = new(speed * (FacingRight ? 1 : -1), 0);
            DespawnCheck();
        }

        private void DespawnCheck() {
            foreach (PlayerController player in GameManager.Instance.AlivePlayers) {
                if (!player) {
                    continue;
                }

                if (Utils.Utils.WrappedDistance(player.body.Position, body.Position) < despawnDistance) {
                    return;
                }
            }

            DespawnEntity();
        }

        //---IPlayerInteractable overrides
        public override void InteractWithPlayer(PlayerController player, PhysicsDataStruct.IContactStruct contact = null) {
            if (IsDead || IsFrozen || player.IsFrozen) {
                return;
            }

            Vector2 damageDirection = (player.body.Position - body.Position).normalized;
            bool attackedFromAbove = Vector2.Dot(damageDirection, Vector2.up) > 0f;

            if (player.InstakillsEnemies || ((player.IsGroundpounding || player.IsDrilling) && player.State != Enums.PowerupState.MiniMushroom && attackedFromAbove)) {

                if (player.IsDrilling) {
                    player.DoEntityBounce = true;
                    player.IsDrilling = false;
                }
                SpecialKill(false, true, player.State == Enums.PowerupState.MegaMushroom, player.StarCombo++);
                return;
            }
            if (attackedFromAbove) {
                if (!(player.State == Enums.PowerupState.MiniMushroom && !player.IsGroundpounding)) {
                    Kill();
                }

                player.IsDrilling = false;
                player.IsGroundpounding = false;
                player.DoEntityBounce = true;
                return;
            }

            player.Powerdown(false);
        }

        //---IFireballInteractable overrides
        public override bool InteractWithFireball(Fireball fireball) {
            // Don't die to fireballs, but still destroy them.
            return true;
        }

        //---IBlockBumpable overrides
        public override void BlockBump(BasicEntity bumper, Vector2Int tile, InteractionDirection direction) {
            // Do nothing
        }

        //---KillableEntity overrides
        public override void RespawnEntity() {
            base.RespawnEntity();

            body.Gravity = Vector2.zero;
            body.Freeze = false;
        }

        public override void Kill() {
            IsDead = true;
            AngularVelocity = 400f * (FacingRight ? -1 : 1);
            body.Velocity = Vector2.zero;
            body.Gravity = Vector2.down * 14.75f;
        }

        public override void SpecialKill(bool right, bool groundpound, bool mega, int combo) {
            if (IsFrozen) {
                Kill();
                return;
            }

            IsDead = true;
            WasSpecialKilled = true;
            WasGroundpounded = true;
            WasKilledByMega = mega;

            if (WasKilledByMega) {
                FacingRight = right;
                body.Velocity = new(2f * (FacingRight ? 1 : -1), 2.5f);
                AngularVelocity = 400f * (FacingRight ? 1 : -1);
                body.Gravity = Vector2.down * 14.75f;

            } else {
                body.Velocity = Vector2.zero;
            }

            DespawnTimer = TickTimer.CreateFromSeconds(Runner, 1f);
        }

        public override void OnIsDeadChanged() {
            base.OnIsDeadChanged();

            if (IsDead) {
                trailParticles.Stop();
                if (WasSpecialKilled) {
                    sRenderer.enabled = false;
                }
            } else {
                sRenderer.enabled = true;
                legacyAnimation.enabled = true;
                trailParticles.Play();
            }
        }

        public override void OnIsFrozenChanged() {
            base.OnIsFrozenChanged();

            if (IsFrozen) {
                trailParticles.Stop();
            } else if (!IsDead && IsActive) {
                trailParticles.Play();
            }
        }

        //---BasicEntity overrides
        public override void OnFacingRightChanged() {
            sRenderer.flipX = FacingRight;

            Vector2 pos = trailParticles.transform.localPosition;
            pos.x = Mathf.Abs(pos.x) * (FacingRight ? -1 : 1);
            trailParticles.transform.localPosition = pos;

            ParticleSystem.ShapeModule shape = shootParticles.shape;
            shape.rotation = new Vector3(0, 0, FacingRight ? -33 : 147);
        }

        public override void OnIsActiveChanged() {
            base.OnIsActiveChanged();
            if (IsActive) {
                shootParticles.Play();
                sfx.Play();
                legacyAnimation.enabled = true;
            }
        }

#if UNITY_EDITOR
        //---Debug
        private static readonly Color RedHalfAlpha = new(1f, 0f, 0f, 0.5f);
        private Vector2? boxOffset;
        public void OnDrawGizmosSelected() {
            if (!GameManager.Instance || !body.Object) {
                return;
            }

            boxOffset ??= new Vector2(GameManager.Instance.LevelWidth, 0f);

            Gizmos.color = RedHalfAlpha;
            Gizmos.DrawCube(body.Position, searchVector);
            // Left border check
            if (body.Position.x - playerSearchRadius < GameManager.Instance.LevelMinX) {
                Gizmos.DrawCube(body.Position + boxOffset.Value, searchVector);
            }
            // Right border check
            if (body.Position.x + playerSearchRadius > GameManager.Instance.LevelMaxX) {
                Gizmos.DrawCube(body.Position - boxOffset.Value, searchVector);
            }
        }
#endif
    }
}
