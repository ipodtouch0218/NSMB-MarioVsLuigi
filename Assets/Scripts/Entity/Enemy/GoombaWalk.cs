using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;

public class GoombaWalk : KillableEntity {
    [SerializeField] float speed, deathTimer;
    private bool left = true;
    new void Start() {
        base.Start();
        base.body.velocity = new Vector2(speed * (left ? -1 : 1), body.velocity.y);
        animator.SetBool("dead", false);
    }

    void FixedUpdate() {
        if (GameManager.Instance && GameManager.Instance.gameover) {
            base.body.velocity = Vector2.zero;
            base.body.angularVelocity = 0;
            base.animator.enabled = false;
            base.body.isKinematic = true;
            return;
        }

        physics.Update();
        if (physics.hitLeft) {
            left = false;
        } else if (physics.hitRight) {
            left = true;
        }
        base.body.velocity = new Vector2(speed * (left ? -1 : 1), body.velocity.y);

        if (!photonView || photonView.IsMine) {
            animator.SetBool("left", left);
            base.sRenderer.flipX = !left;
        } else {
            base.sRenderer.flipX = !animator.GetBool("left");
        }

        if (photonView && !photonView.IsMine) {
            return;
        }

        if (base.animator.GetBool("dead")) {
            if ((deathTimer -= Time.fixedDeltaTime) < 0) {
                PhotonNetwork.Destroy(this.gameObject);
            }
            return;
        }
    }

    [PunRPC]
    public override void Kill() {
        base.body.velocity = Vector2.zero;
        base.body.isKinematic = true;
        speed = 0;
        base.dead = true;
        deathTimer = 0.5f;
        base.hitbox.enabled = false;
        base.animator.SetBool("dead", true);
    }
}