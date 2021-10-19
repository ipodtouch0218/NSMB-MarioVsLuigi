using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;

public class BulletBillMover : KillableEntity {
    
    public float speed;
    public bool left = true;
    private SpriteRenderer spriteRenderer;
    new void Start() {
        base.Start();
        left = photonView && photonView.InstantiationData != null && (bool) photonView.InstantiationData[0];
        body.velocity = new Vector2(speed * (left ? -1 : 1), body.velocity.y);
        spriteRenderer = GetComponent<SpriteRenderer>();

        Transform t = transform.GetChild(1);
        ParticleSystem ps = t.GetComponent<ParticleSystem>();
        ParticleSystem.ShapeModule shape = ps.shape;
        if (!left) {
            Transform tf = transform.GetChild(0);
            tf.localPosition *= Vector2.left;
            shape.rotation = new Vector3(0, 0, -33);
        }

        ps.Play();
        spriteRenderer.flipX = !left;
    }

    void FixedUpdate() {
        if (GameManager.Instance && GameManager.Instance.gameover) {
            body.velocity = Vector2.zero;
            animator.enabled = false;
            body.isKinematic = true;
            return;
        }
    }

    [PunRPC]
    public void Kill() {
        base.SpecialKill();
    }
    
    [PunRPC]
    public override void SpecialKill(bool right, bool groundpound) {
        body.velocity = new Vector2(2.5f * (right ? 1 : -1), 2.5f);
        body.constraints = RigidbodyConstraints2D.None;
        body.angularVelocity = 400f * (right ? 1 : -1);
        body.gravityScale = 1.5f;
        body.isKinematic = false;
        hitbox.enabled = false;
        animator.speed = 0;
        gameObject.layer = LayerMask.NameToLayer("HitsNothing");
        if (groundpound)
            GameObject.Instantiate(Resources.Load("Prefabs/Particle/EnemySpecialKill"), transform.position + new Vector3(0, 0.5f, -5), Quaternion.identity);
        
        dead = true;
        photonView.RPC("PlaySound", RpcTarget.All, "enemy/shell_kick");
    } 
}