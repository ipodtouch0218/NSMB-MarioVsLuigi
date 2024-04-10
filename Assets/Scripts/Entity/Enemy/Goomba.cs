using UnityEngine;

using Fusion;
using NSMB.Game;
using NSMB.Utils;

namespace NSMB.Entities.Enemies {
    public class Goomba : KillableEntity {

        //---Serialized Variables
        [SerializeField] private Sprite deadSprite;
        [SerializeField] private float speed, terminalVelocity = -8;

        public override void Spawned() {
            base.Spawned();
            body.Velocity = new(speed * (FacingRight ? 1 : -1), body.Velocity.y);
        }

        public override void FixedUpdateNetwork() {
            base.FixedUpdateNetwork();
            if (!Object) {
                return;
            }

            if (!IsActive) {
                body.Velocity = Vector2.zero;
                return;
            }

            if (GameManager.Instance.GameEnded) {
                body.Velocity = Vector2.zero;
                AngularVelocity = 0;
                legacyAnimation.enabled = false;
                body.Freeze = true;
                return;
            }

            if (IsDead && !WasSpecialKilled) {
                gameObject.layer = Layers.LayerEntity;
                return;
            }

            HandleWallCollisions();

            body.Velocity = new(speed * (FacingRight ? 1 : -1), Mathf.Max(terminalVelocity, body.Velocity.y));
        }

        private void HandleWallCollisions() {
            PhysicsDataStruct data = body.Data;

            if (data.HitLeft || data.HitRight) {
                FacingRight = data.HitLeft;
            }
        }

        //---KillableEntity overrides
        public override void Kill() {
            IsDead = true;

            body.Velocity = Vector2.zero;
            body.Freeze = true;

            DespawnTimer = TickTimer.CreateFromSeconds(Runner, 0.5f);
        }

        public override void OnIsDeadChanged() {
            base.OnIsDeadChanged();

            if (IsDead) {
                if (!WasSpecialKilled) {
                    legacyAnimation.enabled = false;
                    sRenderer.sprite = deadSprite;
                }
            } else {
                legacyAnimation.enabled = true;
            }
        }

        //---BasicEntity overrides
        public override void OnFacingRightChanged() {
            sRenderer.flipX = FacingRight;
        }
    }
}
