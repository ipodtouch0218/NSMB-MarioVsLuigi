using UnityEngine;
using Photon.Pun;

// another goomba reskin (but a bit different)
public class SplunkinWalk : KillableEntity {
    [SerializeField] float speed, deathTimer = -1, terminalVelocity = -8;
    public bool SplunkinDamaged = false;
    new void Start() {
        base.Start();
        body.velocity = new Vector2(speed * (left ? -1 : 1), body.velocity.y);
        animator.SetBool("dead", false);
    }

    new void FixedUpdate() {
        if (GameManager.Instance && GameManager.Instance.gameover) {
            body.velocity = Vector2.zero;
            body.angularVelocity = 0;
            animator.enabled = false;
            body.isKinematic = true;
            return;
        }

        base.FixedUpdate();
        if (dead) {
            if (deathTimer >= 0 && (photonView?.IsMine ?? true)) {
                Utils.TickTimer(ref deathTimer, 0, Time.fixedDeltaTime);
                if (deathTimer == 0)
                    PhotonNetwork.Destroy(gameObject);
            }
            return;
        }


        physics.UpdateCollisions();
        if (physics.hitLeft || physics.hitRight) {
            left = physics.hitRight;
        }
        body.velocity = new Vector2(speed * (left ? -1 : 1), Mathf.Max(terminalVelocity, body.velocity.y));
        sRenderer.flipX = !left;
        
    }
    public void DamageSplunkin() {
        SplunkinDamaged = true;
    }
    [PunRPC]
    public override void Kill() {
        if (SplunkinDamaged == true) {
            body.velocity = Vector2.zero;
            body.isKinematic = true;
            speed = 0;
            dead = true;
            deathTimer = 0.5f;
            hitbox.enabled = false;
            animator.SetBool("dead", true);
        } else {
            DamageSplunkin();
            animator.SetBool("damaged", true);
        }
        
    }
}