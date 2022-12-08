using UnityEngine;

using Fusion;
using NSMB.Utils;

public class LooseCoin : Coin {

    //---Networked Variables
    [Networked] private TickTimer CollectableTimer { get; set; }
    [Networked] private TickTimer DespawnTimer { get; set; }
    [Networked] private Vector3 PreviousFrameVelocity { get; set; }

    //---Serialized Variables
    [SerializeField] private float despawn = 8;

    //---Components
    private SpriteRenderer spriteRenderer;
    private PhysicsEntity physics;
    private new Animation animation;
    private BoxCollider2D hitbox;

    public override void Awake() {
        base.Awake();
        hitbox = GetComponent<BoxCollider2D>();
        physics = GetComponent<PhysicsEntity>();
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        animation = GetComponentInChildren<Animation>();
    }

    public override void Spawned() {
        base.Spawned();
        CollectableTimer = TickTimer.CreateFromSeconds(Runner, 0.2f);
        DespawnTimer = TickTimer.CreateFromSeconds(Runner, despawn);

        body.velocity = Vector2.up * GameManager.Instance.Random.RangeInclusive(5.5f, 6f);
    }

    public override void FixedUpdateNetwork() {
        if (GameManager.Instance && GameManager.Instance.gameover) {
            body.velocity = Vector2.zero;
            animation.enabled = false;
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
        if (physics.OnGround) {
            body.velocity -= body.velocity * Runner.DeltaTime;
            if (physics.HitRoof) {
                Runner.Despawn(Object);
                return;
            }

            //isforward is ok, the sound isnt top priority
            if (PreviousFrameVelocity.y < -1.85f && Runner.IsForward)
                PlaySound(Enums.Sounds.World_Coin_Drop);
        }

        float despawnTimeRemaining = DespawnTimer.RemainingTime(Runner) ?? 0f;
        spriteRenderer.enabled = !(despawnTimeRemaining < 3 && despawnTimeRemaining % 0.3f >= 0.15f);

        PreviousFrameVelocity = body.velocity;
    }

    //---IPlayerInteractable overrides
    public override void InteractWithPlayer(PlayerController player) {
        if (!CollectableTimer.ExpiredOrNotRunning(Runner))
            return;

        base.InteractWithPlayer(player);
        Runner.Despawn(Object);
    }
}
