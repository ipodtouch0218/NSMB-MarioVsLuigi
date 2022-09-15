using UnityEngine;

using Fusion;
using NSMB.Utils;

public class LooseCoin : Coin {

    [SerializeField] private float despawn = 10;

    [Networked] public TickTimer DespawnTimer { get; set; }

    private Rigidbody2D body;
    private SpriteRenderer spriteRenderer;
    private PhysicsEntity physics;
    private Animator animator;
    private BoxCollider2D hitbox;
    private AudioSource sfx;
    private Vector2 prevFrameVelocity;

    public void Awake() {
        body = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        hitbox = GetComponent<BoxCollider2D>();
        physics = GetComponent<PhysicsEntity>();
        animator = GetComponent<Animator>();
        sfx = GetComponent<AudioSource>();
        body.velocity = Vector2.up * Random.Range(2f, 3f);
    }

    public override void Spawned() {
        DespawnTimer = TickTimer.CreateFromSeconds(Runner, despawn);
    }

    public void FixedUpdate() {
        if (GameManager.Instance && GameManager.Instance.gameover) {
            body.velocity = Vector2.zero;
            animator.enabled = false;
            body.isKinematic = true;
            return;
        }

        bool inWall = Utils.IsAnyTileSolidBetweenWorldBox(body.position + hitbox.offset, hitbox.size * transform.lossyScale * 0.5f);
        gameObject.layer = inWall ? Layers.LayerHitsNothing : Layers.LayerLooseCoin;

        physics.UpdateCollisions();
        if (physics.onGround) {
            body.velocity -= body.velocity * Time.fixedDeltaTime;
            if (physics.hitRoof)
                Runner.Despawn(Object, true);

            if (prevFrameVelocity.y < -1f)
                sfx.PlayOneShot(Enums.Sounds.World_Coin_Drop.GetClip());
        }


        if (DespawnTimer.Expired(Runner)) {
            Runner.Despawn(Object, true);
            return;
        }

        float despawnTimeRemaining = DespawnTimer.RemainingTime(Runner) ?? 0f;
        spriteRenderer.enabled = !(despawnTimeRemaining < 3 && despawnTimeRemaining % 0.3f >= 0.15f);

        prevFrameVelocity = body.velocity;
    }
    public override void OnCoinCollected() {
        if (IsCollected)
            Runner.Despawn(Object);
    }

    // DEBUG & GIZMOS
    public void OnDrawGizmos() {
        Gizmos.color = new Color(1, 0, 0, 0.5f);
        Gizmos.DrawCube(body.position + hitbox.offset, hitbox.size * transform.lossyScale);
    }
}
