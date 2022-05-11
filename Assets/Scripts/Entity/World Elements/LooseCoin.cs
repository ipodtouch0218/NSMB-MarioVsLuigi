using UnityEngine;
using Photon.Pun;

public class LooseCoin : MonoBehaviourPun {

    public float speed = 1.25f, despawn = 10;
    private float despawnTimer;
    private Rigidbody2D body;
    private SpriteRenderer spriteRenderer;
    private PhysicsEntity physics;
    private Animator animator;
    void Start() {
        body = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        physics = GetComponent<PhysicsEntity>();
        animator = GetComponent<Animator>();
        body.velocity = new Vector2(Random.Range(-speed, speed), Random.Range(2, 4));
    }

    void FixedUpdate() {
        if (GameManager.Instance && GameManager.Instance.gameover) {
            body.velocity = Vector2.zero;
            animator.enabled = false;
            body.isKinematic = true;
            return;
        }

        physics.UpdateCollisions();
        if (physics.onGround) {
            body.velocity -= body.velocity * Time.fixedDeltaTime;
            if (physics.hitRoof && photonView.IsMine)
                PhotonNetwork.Destroy(photonView);
        }

        spriteRenderer.enabled = !(despawnTimer > despawn-3 && despawnTimer % 0.3f >= 0.15f);
        
        if ((despawnTimer += Time.deltaTime) >= despawn) {
            if (photonView.IsMine)
                PhotonNetwork.Destroy(photonView);
            return;
        }
    }
}
