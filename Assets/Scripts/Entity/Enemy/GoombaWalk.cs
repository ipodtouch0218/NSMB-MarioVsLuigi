using UnityEngine;

using Fusion;

public class GoombaWalk : KillableEntity {

    //---Networked Variables
    [Networked] private TickTimer DespawnTimer { get; set; }

    //---Serialized Variables
    [SerializeField] private Sprite deadSprite;
    [SerializeField] private float speed, terminalVelocity = -8;

    public override void Spawned() {
        base.Spawned();
        body.velocity = new(speed * (FacingRight ? 1 : -1), body.velocity.y);
    }

    public override void FixedUpdateNetwork() {
        base.FixedUpdateNetwork();
        if (GameManager.Instance && GameManager.Instance.gameover) {
            body.velocity = Vector2.zero;
            body.angularVelocity = 0;
            legacyAnimation.enabled = false;
            body.isKinematic = true;
            return;
        }

        if (IsDead) {
            if (DespawnTimer.Expired(Runner)) {
                DespawnTimer = TickTimer.None;
                Runner.Despawn(Object);
            }
            return;
        }

        HandleWallCollisions();

        body.velocity = new(speed * (FacingRight ? 1 : -1), Mathf.Max(terminalVelocity, body.velocity.y));
        sRenderer.flipX = FacingRight;
    }

    private void HandleWallCollisions() {
        physics.UpdateCollisions();
        if (physics.HitLeft || physics.HitRight)
            FacingRight = physics.HitLeft;
    }

    //---KillableEntity overrides
    public override void Kill() {
        IsDead = true;

        body.velocity = Vector2.zero;
        body.isKinematic = true;

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
}
