using System.Linq;
using UnityEngine;

using Fusion;
using NSMB.Utils;

public class MovingPowerup : CollectableEntity, IBlockBumpable {

    private static LayerMask GroundMask;

    //---Networked Variables
    [Networked] protected PlayerController FollowPlayer { get; set; }
    [Networked] private TickTimer FollowEndTimer { get; set; }
    [Networked] private TickTimer IgnorePlayerTimer { get; set; }
    [Networked] private PowerupReserveResult ReserveResult { get; set; }

    //---Public Variables
    public Powerup powerupScriptable;

    //---Serialized Variables
    [SerializeField] private float speed, bouncePower, terminalVelocity = 4, blinkingRate = 4;
    [SerializeField] private bool avoidPlayers;

    //---Components
    [SerializeField] private SpriteRenderer sRenderer;
    [SerializeField] protected PhysicsEntity physics;
    [SerializeField] private Animator childAnimator;
    [SerializeField] private BoxCollider2D hitbox;
    private IPowerupCollect collectScript;

    //---Misc Variables
    private int originalLayer;

    public override void OnValidate() {
        base.OnValidate();
        if (!sRenderer) sRenderer = GetComponentInChildren<SpriteRenderer>();
        if (!physics) physics = GetComponent<PhysicsEntity>();
        if (!hitbox) hitbox = GetComponent<BoxCollider2D>();
        if (!childAnimator) childAnimator = GetComponentInChildren<Animator>();
    }

    public void Awake() {
        originalLayer = sRenderer.sortingOrder;
        collectScript = GetComponent<IPowerupCollect>();

        if (GroundMask == 0)
            GroundMask = (1 << Layers.LayerGround) | (1 << Layers.LayerPassthrough);
    }

    public void OnBeforeSpawned(PlayerController playerToFollow, float pickupDelay) {
        FollowPlayer = playerToFollow;
        FollowEndTimer = TickTimer.CreateFromSeconds(Runner, pickupDelay);

        if (playerToFollow)
            transform.position = body.position = new(playerToFollow.transform.position.x, playerToFollow.cameraController.currentPosition.y + 1.68f);
    }

    public override void Spawned() {
        base.Spawned();

        if (FollowPlayer) {
            //spawned following a player
            FollowEndTimer = TickTimer.CreateFromSeconds(Runner, 1f);

            body.isKinematic = true;
            gameObject.layer = Layers.LayerHitsNothing;
            sRenderer.sortingOrder = 15;
        } else {
            //spawned as a normal item.
            gameObject.layer = Layers.LayerEntity;
            Vector2 size = hitbox.size * transform.lossyScale * 0.35f;
            Vector2 origin = body.position + hitbox.offset * transform.lossyScale;

            if (Runner.GetPhysicsScene2D().OverlapBox(origin, size, 0, GroundMask)) {
                DespawnEntity();
                return;
            }
        }

        FacingRight = true;
        DespawnTimer = TickTimer.CreateFromSeconds(Runner, 10f);
    }

    public override void Render() {
        if (childAnimator)
            childAnimator.SetBool("onGround", physics.OnGround);
    }

    public override void FixedUpdateNetwork() {
        base.FixedUpdateNetwork();
        if (GameManager.Instance && GameManager.Instance.GameEnded) {
            body.velocity = Vector2.zero;
            body.isKinematic = true;
            return;
        }

        if (!Object)
            return;

        if (FollowPlayer) {
            body.position = new(FollowPlayer.body.position.x, FollowPlayer.cameraController.currentPosition.y + 1.68f);

            if (FollowEndTimer.ExpiredOrNotRunning(Runner)) {
                FollowPlayer = null;
                sRenderer.sortingOrder = originalLayer;
                body.isKinematic = false;
            } else {
                float timeRemaining = FollowEndTimer.RemainingTime(Runner) ?? 0f;
                sRenderer.enabled = !(timeRemaining * blinkingRate % 1 < 0.5f);
                return;
            }
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

        if (avoidPlayers && physics.OnGround) {
            PlayerController closest = GameManager.Instance.AlivePlayers.OrderBy(player => Utils.WrappedDistance(body.position, player.body.position)).FirstOrDefault();
            if (closest) {
                float dist = closest.body.position.x - body.position.x;
                FacingRight = dist < 0 || dist > GameManager.Instance.LevelWidth;
            }
        }

        body.velocity = new(body.velocity.x, Mathf.Max(-terminalVelocity, body.velocity.y));
    }

    public void HandleCollision() {
        physics.UpdateCollisions();

        if (physics.HitLeft || physics.HitRight) {
            FacingRight = physics.HitLeft;
            body.velocity = new(speed * (FacingRight ? 1 : -1), body.velocity.y);
        }

        if (physics.OnGround) {
            body.velocity = new(speed * (FacingRight ? 1 : -1), Mathf.Max(body.velocity.y, bouncePower));

            if (physics.HitRoof || (physics.HitLeft && physics.HitRight)) {
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
        if (!FollowEndTimer.ExpiredOrNotRunning(Runner))
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

        Runner.Despawn(Object);
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
