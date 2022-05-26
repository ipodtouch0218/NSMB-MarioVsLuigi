using UnityEngine;
using Photon.Pun;

public class LooseCoin : MonoBehaviourPun {

    private static int ENTITY_LAYER = -1, HITSNOTHING_LAYER = -1;

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

        if (ENTITY_LAYER == -1) {
            ENTITY_LAYER = LayerMask.NameToLayer("Entity");
            HITSNOTHING_LAYER = LayerMask.NameToLayer("HitsNothing");
        }
    }

    void FixedUpdate() {
        if (GameManager.Instance && GameManager.Instance.gameover) {
            body.velocity = Vector2.zero;
            animator.enabled = false;
            body.isKinematic = true;
            return;
        }

        bool inWall = Utils.IsTileSolidAtWorldLocation(body.position + Vector2.up * 0.25f);
        gameObject.layer = inWall ? HITSNOTHING_LAYER : ENTITY_LAYER;

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
