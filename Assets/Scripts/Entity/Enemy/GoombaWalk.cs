using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;

public class GoombaWalk : KillableEntity {
    [SerializeField] float speed, deathTimer;
    new void Start() {
        base.Start();
        body.velocity = new Vector2(speed * (left ? -1 : 1), body.velocity.y);
        animator.SetBool("dead", false);
    }

    void FixedUpdate() {
        if (GameManager.Instance && GameManager.Instance.gameover) {
            body.velocity = Vector2.zero;
            body.angularVelocity = 0;
            animator.enabled = false;
            body.isKinematic = true;
            return;
        }

        physics.UpdateCollisions();
        if (physics.hitLeft || physics.hitRight) {
            left = physics.hitRight;
        }
        body.velocity = new Vector2(speed * (left ? -1 : 1), body.velocity.y);
        sRenderer.flipX = !left;

        if (photonView && !photonView.IsMine) {
            return;
        }

        if (dead && (deathTimer -= Time.fixedDeltaTime) < 0)
            PhotonNetwork.Destroy(gameObject);
    }

    [PunRPC]
    public override void Kill() {
        body.velocity = Vector2.zero;
        body.isKinematic = true;
        speed = 0;
        dead = true;
        deathTimer = 0.5f;
        hitbox.enabled = false;
        animator.SetBool("dead", true);
    }
}