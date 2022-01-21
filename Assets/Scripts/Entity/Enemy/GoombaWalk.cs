using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;

public class GoombaWalk : KillableEntity {
    [SerializeField] float speed, deathTimer;
    private bool left = true;
    private SpriteRenderer spriteRenderer;
    new void Start() {
        base.Start();
        body.velocity = new Vector2(speed * (left ? -1 : 1), body.velocity.y);
        animator.SetBool("dead", false);
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    void FixedUpdate() {
        if (GameManager.Instance && GameManager.Instance.gameover) {
            body.velocity = Vector2.zero;
            animator.enabled = false;
            body.isKinematic = true;
            return;
        }

        physics.Update();
        if (physics.hitLeft) {
            left = false;
        } else if (physics.hitRight) {
            left = true;
        }
        body.velocity = new Vector2(speed * (left ? -1 : 1), body.velocity.y);

        if (!photonView || photonView.IsMine) {
            animator.SetBool("left", left);
            spriteRenderer.flipX = !left;
        } else {
            spriteRenderer.flipX = !animator.GetBool("left");
        }

        if (photonView && !photonView.IsMine) {
            return;
        }

        if (animator.GetBool("dead")) {
            if ((deathTimer -= Time.fixedDeltaTime) < 0) {
                PhotonNetwork.Destroy(this.gameObject);
            }
            return;
        }
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