using UnityEngine;

using Fusion;
using NSMB.Entities.Player;
using NSMB.Game;
using NSMB.Tiles;
using NSMB.Utils;

namespace NSMB.Entities.Enemies {
    public class BulletBillMover : KillableEntity {

        //---Serialized Variables
        [SerializeField] private float speed, playerSearchRadius = 4, despawnDistance = 8;
        [SerializeField] private ParticleSystem shootParticles, trailParticles;

        //---Private Variables
        private Vector2 searchVector;

        public override void OnValidate() {
            base.OnValidate();
            if (!nrb) nrb = GetComponent<NetworkRigidbody2D>();
        }

        public void Awake() {
            searchVector = new(playerSearchRadius * 2, playerSearchRadius * 4);
        }

        public override void FixedUpdateNetwork() {

            if (GameData.Instance.GameEnded) {
                body.velocity = Vector2.zero;
                body.angularVelocity = 0;
                legacyAnimation.enabled = false;
                body.isKinematic = true;
                return;
            }

            if (DespawnTimer.Expired(Runner)) {
                DespawnTimer = TickTimer.None;
                DespawnEntity();
            }

            if (IsDead || !IsActive) {
                gameObject.layer = Layers.LayerHitsNothing;
                body.isKinematic = false;
                if (!IsActive) {
                    body.rotation = 0;
                    body.angularVelocity = 0;
                    body.gravityScale = 0;
                }

            } else {
                gameObject.layer = Layers.LayerEntityHitbox;
                body.isKinematic = true;
                body.freezeRotation = true;
                body.rotation = 0;
                body.angularVelocity = 0;
                body.gravityScale = 0;
            }

            if (IsFrozen || IsDead)
                return;

            body.velocity = new(speed * (FacingRight ? 1 : -1), 0);
            DespawnCheck();
        }

        private void DespawnCheck() {
            foreach (PlayerController player in GameData.Instance.AlivePlayers) {
                if (!player)
                    continue;

                if (Utils.Utils.WrappedDistance(player.body.position, body.position) < despawnDistance)
                    return;
            }

            DespawnEntity();
        }

        //---IPlayerInteractable overrides
        public override void InteractWithPlayer(PlayerController player) {
            if (IsDead || IsFrozen || player.IsFrozen || (player.State == Enums.PowerupState.BlueShell && player.IsCrouchedInShell))
                return;

            Vector2 damageDirection = (player.body.position - body.position).normalized;
            bool attackedFromAbove = Vector2.Dot(damageDirection, Vector2.up) > 0f;

            if (player.InstakillsEnemies || ((player.IsGroundpounding || player.IsDrilling) && player.State != Enums.PowerupState.MiniMushroom && attackedFromAbove)) {

                if (player.IsDrilling) {
                    player.DoEntityBounce = true;
                    player.IsDrilling = false;
                }
                SpecialKill(false, true, player.StarCombo++);
                return;
            }
            if (attackedFromAbove) {
                if (!(player.State == Enums.PowerupState.MiniMushroom && !player.IsGroundpounding))
                    Kill();

                player.IsDrilling = false;
                player.IsGroundpounding = false;
                player.DoEntityBounce = true;
                return;
            }

            player.Powerdown(false);
        }

        //---IFireballInteractable overrides
        public override bool InteractWithFireball(FireballMover fireball) {
            //don't die to fireballs, but still destroy them.
            return true;
        }

        //---IBlockBumpable overrides
        public override void BlockBump(BasicEntity bumper, Vector2Int tile, InteractableTile.InteractionDirection direction) {
            //do nothing
        }

        //---KillableEntity overrides
        public override void Kill() {
            IsDead = true;
            body.velocity = Vector2.zero;
            body.constraints = RigidbodyConstraints2D.None;
            body.angularVelocity = 400f * (FacingRight ? -1 : 1);
            body.gravityScale = 1.5f;
            body.isKinematic = false;
        }

        public override void SpecialKill(bool right, bool groundpound, int combo) {
            IsDead = true;
            WasSpecialKilled = true;
            WasGroundpounded = groundpound;
            body.velocity = Vector2.zero;
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
            if (!GameManager.Instance)
                return;

            boxOffset ??= new Vector2(GameManager.Instance.LevelWidth, 0f);

            Gizmos.color = RedHalfAlpha;
            Gizmos.DrawCube(body.position, searchVector);
            // Left border check
            if (body.position.x - playerSearchRadius < GameManager.Instance.LevelMinX)
                Gizmos.DrawCube(body.position + boxOffset.Value, searchVector);
            // Right border check
            if (body.position.x + playerSearchRadius > GameManager.Instance.LevelMaxX)
                Gizmos.DrawCube(body.position - boxOffset.Value, searchVector);
        }
#endif
    }
}
