using UnityEngine;

using Fusion;
using NSMB.Utils;

public class LooseCoin : Coin {

    //---Networked Variables
    [Networked] private TickTimer DespawnTimer { get; set; }

    //---Serialized Variables
    [SerializeField] private float despawn = 10;

    //---Components
    private SpriteRenderer spriteRenderer;
    private PhysicsEntity physics;
    private Animator animator;
    private BoxCollider2D hitbox;

    //---Misc Variables
    private Vector2 prevFrameVelocity;

    public override void Awake() {
        base.Awake();
        spriteRenderer = GetComponent<SpriteRenderer>();
        hitbox = GetComponent<BoxCollider2D>();
        physics = GetComponent<PhysicsEntity>();
        animator = GetComponent<Animator>();
        body.velocity = Vector2.up * GameManager.Instance.Random.RangeInclusive(2f, 3f);
    }

    public override void Spawned() {
        DespawnTimer = TickTimer.CreateFromSeconds(Runner, despawn);
    }

    public override void FixedUpdateNetwork() {
        if (GameManager.Instance && GameManager.Instance.gameover) {
            body.velocity = Vector2.zero;
            animator.enabled = false;
            body.isKinematic = true;
            return;
        }

        if (DespawnTimer.Expired(Runner)) {
            Runner.Despawn(Object);
            return;
        }

        bool inWall = Utils.IsAnyTileSolidBetweenWorldBox(body.position + hitbox.offset, hitbox.size * transform.lossyScale * 0.75f);
        gameObject.layer = inWall ? Layers.LayerHitsNothing : Layers.LayerLooseCoin;

        physics.UpdateCollisions();
        if (physics.onGround) {
            body.velocity -= body.velocity * Time.fixedDeltaTime;
            if (physics.hitRoof) {
                Runner.Despawn(Object);
                return;
            }

            if (prevFrameVelocity.y < -1f)
                PlaySound(Enums.Sounds.World_Coin_Drop);
        }

        float despawnTimeRemaining = DespawnTimer.RemainingTime(Runner) ?? 0f;
        spriteRenderer.enabled = !(despawnTimeRemaining < 3 && despawnTimeRemaining % 0.3f >= 0.15f);

        prevFrameVelocity = body.velocity;
    }

    public override void OnCollectedChanged() {
        if (Collector)
            Runner.Despawn(Object);
    }
}
