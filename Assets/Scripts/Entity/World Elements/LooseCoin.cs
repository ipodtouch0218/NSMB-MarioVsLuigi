using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;

public class LooseCoin : MonoBehaviourPun {

    public float speed = 1.25f, despawn = 10;
    private float despawnTimer;
    private float randSpeed;
    private Rigidbody2D body;
    private BoxCollider2D hitbox;
    private SpriteRenderer spriteRenderer;
    private PhysicsEntity physics;
    void Start() {
        body = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        hitbox = GetComponent<BoxCollider2D>();
        physics = GetComponent<PhysicsEntity>();
        body.velocity = new Vector2((randSpeed = Random.Range(-speed, speed)), Random.Range(2, 4));
    }

    void FixedUpdate() {
        if (GameManager.Instance && GameManager.Instance.gameover) {
            body.velocity = Vector2.zero;
            GetComponent<Animator>().enabled = false;
            body.isKinematic = true;
            return;
        }

        physics.Update();
        if (physics.onGround) {
            body.velocity -= (body.velocity * (Time.fixedDeltaTime));
            if (physics.hitRoof && photonView.IsMine) {
                PhotonNetwork.Destroy(photonView);
            }
        }

        spriteRenderer.enabled = !(despawnTimer > despawn-3 && despawnTimer % 0.3f >= 0.15f);
        
        if ((despawnTimer += Time.deltaTime) >= despawn) {
            if (photonView.IsMine)
                PhotonNetwork.Destroy(photonView);
            return;
        }
    }
}
