using System.Linq;
using UnityEngine;

using Fusion;
using NSMB.Tiles;
using NSMB.Utils;

public class MovingPowerup : CollectableEntity, IBlockBumpable {

    //---Static Variables
    private static LayerMask GroundMask;
    private static readonly int OriginalSortingOrder = 10;

    //---Networked Variables
    [Networked(OnChanged = nameof(OnReserveResultChanged))] private PowerupReserveResult ReserveResult { get; set; }
    [Networked] protected PlayerController FollowPlayer { get; set; }
    [Networked] private TickTimer IgnorePlayerTimer { get; set; }

    [Networked] private Vector2 BlockSpawnOrigin { get; set; }
    [Networked] private Vector2 BlockSpawnDestination { get; set; }
    [Networked] private NetworkBool BlockSpawn { get; set; }
    [Networked] private float BlockSpawnAnimationLength { get; set; }

    [Networked] private TickTimer SpawnAnimationTimer { get; set; }

    //---Public Variables
    public Powerup powerupScriptable;

    //---Serialized Variables
    [SerializeField] private float speed, bouncePower, terminalVelocity = 4, blinkingRate = 4;
    [SerializeField] private bool avoidPlayers;

    //---Components
    [SerializeField] private SpriteRenderer sRenderer;
    [SerializeField] protected PhysicsEntity physics;
    [SerializeField] private Animator childAnimator;
    [SerializeField] private Animation childAnimation;
    [SerializeField] private BoxCollider2D hitbox;
    private IPowerupCollect collectScript;

    public override void OnValidate() {
        base.OnValidate();
        if (!sRenderer) sRenderer = GetComponentInChildren<SpriteRenderer>();
        if (!physics) physics = GetComponent<PhysicsEntity>();
        if (!hitbox) hitbox = GetComponent<BoxCollider2D>();
        if (!childAnimator) childAnimator = GetComponentInChildren<Animator>();
    }

    public void Awake() {
        collectScript = GetComponent<IPowerupCollect>();

        if (GroundMask == 0)
            GroundMask = (1 << Layers.LayerGround) | (1 << Layers.LayerPassthrough);
    }

    public void OnBeforeSpawned(float pickupDelay) {
        SpawnAnimationTimer = TickTimer.CreateFromSeconds(Runner, pickupDelay);
        DespawnTimer = TickTimer.CreateFromSeconds(Runner, 10f + pickupDelay);
    }

    public void OnBeforeSpawned(PlayerController playerToFollow) {
        OnBeforeSpawned(1);

        FollowPlayer = playerToFollow;
        transform.position = body.position = new(playerToFollow.transform.position.x, playerToFollow.cameraController.currentPosition.y + 1.68f);
    }

    public void OnBeforeSpawned(float pickupDelay, Vector2 spawnOrigin, Vector2 spawnDestination) {
        OnBeforeSpawned(pickupDelay);

        BlockSpawn = true;
        BlockSpawnOrigin = spawnOrigin;
        BlockSpawnDestination = spawnDestination;
        BlockSpawnAnimationLength = pickupDelay;
        body.position = spawnOrigin;

        if (BlockSpawnDestination.y < BlockSpawnOrigin.y) {
            // Downwards powerup, adjust based on the powerup's height.
            BlockSpawnDestination = new(BlockSpawnOrigin.x, BlockSpawnOrigin.y - sRenderer.bounds.size.y);
        }
    }

    public override void Spawned() {
        base.Spawned();

        if (FollowPlayer) {
            // Spawned following a player.
            body.isKinematic = true;
            gameObject.layer = Layers.LayerHitsNothing;
            sRenderer.sortingOrder = 15;

            PlaySound(Enums.Sounds.Player_Sound_PowerupReserveUse);

        } else if (BlockSpawn) {
            // Spawned from a block.
            body.isKinematic = true;
            gameObject.layer = Layers.LayerHitsNothing;
            sRenderer.sortingOrder = -1000;

            PlaySound(powerupScriptable.powerupBlockEffect);

            if (childAnimation)
                childAnimation.Play();

        } else {
            // Spawned by any other means (blue koopa, usually.)
            body.isKinematic = false;
            gameObject.layer = Layers.LayerEntity;
            sRenderer.sortingOrder = OriginalSortingOrder;
        }

        FacingRight = true;
    }

    public override void Render() {
        if (childAnimator)
            childAnimator.SetBool("onGround", physics.Data.OnGround);
    }

    public override void FixedUpdateNetwork() {
        base.FixedUpdateNetwork();
        if (GameManager.Instance && GameManager.Instance.GameEnded) {
            body.velocity = Vector2.zero;
            body.isKinematic = true;
            return;
        }

        if (!Object || Collector)
            return;

        if (FollowPlayer) {
            // Attached to a player. Don't interact, and follow the player.
            body.position = new(FollowPlayer.body.position.x, FollowPlayer.cameraController.currentPosition.y + 1.68f);

            if (SpawnAnimationTimer.ExpiredOrNotRunning(Runner)) {
                SpawnAnimationTimer = TickTimer.None;
                FollowPlayer = null;
                body.isKinematic = false;
                sRenderer.sortingOrder = OriginalSortingOrder;
            } else {
                float timeRemaining = SpawnAnimationTimer.RemainingTime(Runner) ?? 0f;
                sRenderer.enabled = !(timeRemaining * blinkingRate % 1 < 0.5f);
                return;
            }
        } else if (BlockSpawn) {
            // Spawning from a block. Lerp between origin & destination.
            float remaining = SpawnAnimationTimer.RemainingTime(Runner) ?? 0f;
            float t = 1f - (remaining / BlockSpawnAnimationLength);
            body.position = Vector2.Lerp(BlockSpawnOrigin, BlockSpawnDestination, t);

            if (SpawnAnimationTimer.ExpiredOrNotRunning(Runner)) {
                SpawnAnimationTimer = TickTimer.None;
                BlockSpawn = false;
                sRenderer.sortingOrder = OriginalSortingOrder;
                body.isKinematic = false;
            } else {
                sRenderer.enabled = true;
                return;
            }

            return;
        }

        float despawnTimeRemaining = DespawnTimer.RemainingTime(Runner) ?? 0f;
        sRenderer.enabled = !(despawnTimeRemaining <= 1 && despawnTimeRemaining * blinkingRate % 1 < 0.5f);

        Vector2 size = hitbox.size * transform.lossyScale * 0.8f;
        Vector2 origin = body.position + hitbox.offset * transform.lossyScale;

        if (Utils.IsAnyTileSolidBetweenWorldBox(origin, size) || Runner.GetPhysicsScene2D().OverlapBox(origin, size, 0, GroundMask)) {
            gameObject.layer = Layers.LayerHitsNothing;
            return;
        } else {
            gameObject.layer = Layers.LayerEntity;
            HandleCollision();
        }

        if (avoidPlayers && physics.Data.OnGround) {
            PlayerController closest = GameManager.Instance.AlivePlayers.OrderBy(player => Utils.WrappedDistance(body.position, player.body.position)).FirstOrDefault();
            if (closest) {
                float dist = closest.body.position.x - body.position.x;
                FacingRight = dist < 0 || dist > GameManager.Instance.LevelWidth;
            }
        }

        body.velocity = new(body.velocity.x, Mathf.Max(-terminalVelocity, body.velocity.y));
    }

    public void HandleCollision() {
        PhysicsEntity.PhysicsDataStruct data = physics.UpdateCollisions();

        if (data.HitLeft || data.HitRight) {
            FacingRight = data.HitLeft;
            body.velocity = new(speed * (FacingRight ? 1 : -1), body.velocity.y);
        }

        if (data.OnGround) {
            body.velocity = new(speed * (FacingRight ? 1 : -1), Mathf.Max(body.velocity.y, bouncePower));

            if (data.HitRoof || (data.HitLeft && data.HitRight)) {
                DespawnEntity();
                return;
            }
        }
    }

    public override void Despawned(NetworkRunner runner, bool hasState) {
        if (!Collector)
            GameManager.Instance.particleManager.Play(Enums.Particle.Generic_Puff, body.position + hitbox.offset);
    }

    //---IBlockBumpable overrides
    public override void BlockBump(BasicEntity bumper, Vector2Int tile, InteractableTile.InteractionDirection direction) {
        if (direction == InteractableTile.InteractionDirection.Down || FollowPlayer)
            return;

        body.velocity = new(body.velocity.x, 5f);
    }

    //---IPlayerInteractable overrides
    public override void InteractWithPlayer(PlayerController player) {

        //fixes players hitting multiple colliders at once (propeller)
        if (!Object || !Object.IsValid)
            return;

        //don't be collectable if someone already collected us
        if (Collector)
            return;

        //don't be collectable if we're following a player
        if (!SpawnAnimationTimer.ExpiredOrNotRunning(Runner))
            return;

        //don't collect if we're ignoring players (usually, after blue shell spawns from a blue koopa,
        // so we dont collect it instantly)
        if (!IgnorePlayerTimer.ExpiredOrNotRunning(Runner))
            return;

        Collector = player;

        //change the player's powerup state
        Enums.PowerupState oldState = player.State;
        Powerup newPowerup = powerupScriptable;
        Enums.PowerupState newState = newPowerup.state;

        ReserveResult = collectScript.OnPowerupCollect(player, this);

        switch (ReserveResult) {
        case PowerupReserveResult.ReserveOldPowerup: {
            Collector.SetReserveItem(oldState);
            break;
        }
        case PowerupReserveResult.ReserveNewPowerup: {
            Collector.SetReserveItem(newState);
            break;
        }
        }

        DespawnTimer = TickTimer.CreateFromSeconds(Runner, 0.5f);
    }

    //---CollectableEntity overrides
    public override void OnCollectedChanged() {
        if (Collector) {
            sRenderer.enabled = false;
        } else {
            sRenderer.enabled = true;
        }
    }

    //---OnChangeds
    public static void OnReserveResultChanged(Changed<MovingPowerup> changed) {
        MovingPowerup powerup = changed.Behaviour;
        PlayerController collector = powerup.Collector;

        Powerup newPowerup = powerup.powerupScriptable;
        Enums.PowerupState newState = newPowerup.state;

        switch (powerup.ReserveResult) {
        case PowerupReserveResult.NoneButPlaySound: {
            //just play the collect sound
            collector.PlaySound(newPowerup.soundEffect);
            break;
        }
        case PowerupReserveResult.ReserveOldPowerup: {
            //reserve the powerup we just had
            if (newState == Enums.PowerupState.MegaMushroom)
                break;

            collector.PlaySound(newPowerup.soundEffect);
            break;
        }
        case PowerupReserveResult.ReserveNewPowerup: {
            //reserve the new powerup
            collector.PlaySound(Enums.Sounds.Player_Sound_PowerupReserveStore);
            break;
        }
        }
    }
}
