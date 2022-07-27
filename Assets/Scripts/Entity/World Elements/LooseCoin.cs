using UnityEngine;
using Photon.Pun;

public class LooseCoin : MonoBehaviourPun {

    private static int COIN_LAYER = -1, HITSNOTHING_LAYER = -1;

    public float despawn = 10;
    private float despawnTimer;
    private Rigidbody2D body;
    private SpriteRenderer spriteRenderer;
    private PhysicsEntity physics;
    private Animator animator;
    private BoxCollider2D hitbox;

    void Start() {
        body = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        hitbox = GetComponent<BoxCollider2D>();
        physics = GetComponent<PhysicsEntity>();
        animator = GetComponent<Animator>();
        body.velocity = Vector2.up * Random.Range(2f, 3f);

        if (COIN_LAYER == -1) {
            COIN_LAYER = LayerMask.NameToLayer("LooseCoin");
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

        bool inWall = Utils.IsAnyTileSolidBetweenWorldBox(body.position + hitbox.offset, hitbox.size * transform.lossyScale * 0.5f);
        gameObject.layer = inWall ? HITSNOTHING_LAYER : COIN_LAYER;

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

    public void OnDrawGizmos() {
        Gizmos.color = new Color(1, 0, 0, 0.5f);
        Gizmos.DrawCube(body.position + hitbox.offset, hitbox.size * transform.lossyScale);
    }
}
