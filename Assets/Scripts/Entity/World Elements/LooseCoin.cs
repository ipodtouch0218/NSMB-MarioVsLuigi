using UnityEngine;

using Fusion;
using NSMB.Utils;

public class LooseCoin : Coin {

    //---Networked Variables
    [Networked] private int CollectableTick { get; set; }

    //---Serialized Variables
    [SerializeField] private float despawn = 8;

    //---Components
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private PhysicsEntity physics;
    [SerializeField] private Animation spriteAnimation;
    [SerializeField] private BoxCollider2D hitbox;

    public override void OnValidate() {
        base.OnValidate();
        if (!spriteRenderer) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (!physics) physics = GetComponent<PhysicsEntity>();
        if (!spriteAnimation) spriteAnimation = GetComponentInChildren<Animation>();
        if (!hitbox) hitbox = GetComponent<BoxCollider2D>();
    }

    public override void Spawned() {
        base.Spawned();
        CollectableTick = (int) (Runner.Tick + (0.2f / Runner.DeltaTime));
        DespawnTimer = TickTimer.CreateFromSeconds(Runner, despawn);

        body.velocity = Vector2.up * GameManager.Instance.Random.RangeInclusive(5.5f, 6f);
    }

    public override void FixedUpdateNetwork() {
        base.FixedUpdateNetwork();
        if (GameManager.Instance && GameManager.Instance.GameEnded) {
            body.velocity = Vector2.zero;
            spriteAnimation.enabled = false;
            body.isKinematic = true;
            return;
        }

        if (!Object)
            return;

        bool inWall = Utils.IsAnyTileSolidBetweenWorldBox(body.position + hitbox.offset, hitbox.size * transform.lossyScale * 0.75f);
        gameObject.layer = inWall ? Layers.LayerHitsNothing : Layers.LayerEntity;

        PhysicsEntity.PhysicsDataStruct data = physics.UpdateCollisions();
        if (data.OnGround) {
            body.velocity -= body.velocity * Runner.DeltaTime;
            if (data.HitRoof) {
                Runner.Despawn(Object);
                return;
            }

            // TODO: doesn't always trigger, even for host. Strange.
            // IsForward is ok, the sound isnt top priority
            if (Runner.IsForward && physics.previousTickVelocity.y < -0.5f * (Mathf.Sin(physics.Data.FloorAngle) + 1f))
                PlaySound(Enums.Sounds.World_Coin_Drop);
        }

        float despawnTimeRemaining = DespawnTimer.RemainingTime(Runner) ?? 0f;
        spriteRenderer.enabled = !(despawnTimeRemaining < 3 && despawnTimeRemaining % 0.3f >= 0.15f);
    }

    //---IPlayerInteractable overrides
    public override void InteractWithPlayer(PlayerController player) {
        if (Runner.Tick < CollectableTick)
            return;

        base.InteractWithPlayer(player);
        Runner.Despawn(Object);
    }
}
