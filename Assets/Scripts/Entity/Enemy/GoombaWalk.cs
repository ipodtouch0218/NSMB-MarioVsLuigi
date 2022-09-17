using UnityEngine;

using Fusion;

public class GoombaWalk : KillableEntity {

    //---Networked Variables
    [Networked] private TickTimer DespawnTimer { get; set; }

    //---Serialized Variables
    [SerializeField] private float speed, terminalVelocity = -8;

    public override void Spawned() {
        body.velocity = new(speed * (FacingRight ? 1 : -1), body.velocity.y);
        animator.SetBool("dead", false);
    }

    public override void FixedUpdateNetwork() {
        base.FixedUpdateNetwork();
        if (GameManager.Instance && GameManager.Instance.gameover) {
            body.velocity = Vector2.zero;
            body.angularVelocity = 0;
            animator.enabled = false;
            body.isKinematic = true;
            return;
        }

        if (Dead) {
            if (DespawnTimer.Expired(Runner))
                Runner.Despawn(Object);
            return;
        }

        HandleWallCollisions();

        body.velocity = new(speed * (FacingRight ? 1 : -1), Mathf.Max(terminalVelocity, body.velocity.y));
        sRenderer.flipX = FacingRight;
    }

    private void HandleWallCollisions() {
        physics.UpdateCollisions();
        if (physics.hitLeft || physics.hitRight)
            FacingRight = physics.hitLeft;
    }

    public override void Kill() {
        Dead = true;

        body.velocity = Vector2.zero;
        body.isKinematic = true;
        hitbox.enabled = false;

        DespawnTimer = TickTimer.CreateFromSeconds(Runner, 0.5f);
        animator.SetBool("dead", true);
    }
}