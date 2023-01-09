using System.Linq;
using UnityEngine;

using Fusion;
using NSMB.Utils;

public class MovingPowerup : CollectableEntity, IBlockBumpable {

    private static LayerMask GroundMask;

    //---Networked Variables
    [Networked] protected PlayerController FollowPlayer { get; set; }
    [Networked] private TickTimer FollowEndTimer { get; set; }
    [Networked] private TickTimer DespawnTimer { get; set; }
    [Networked] private TickTimer IgnorePlayerTimer { get; set; }

    //---Public Variables
    public Powerup powerupScriptable;

    //---Serialized Variables
    [SerializeField] private float speed, bouncePower, terminalVelocity = 4, blinkingRate = 4;
    [SerializeField] private bool avoidPlayers;

    //---Component Variables
    private SpriteRenderer sRenderer;
    protected PhysicsEntity physics;
    private Animator childAnimator;
    private BoxCollider2D hitbox;
    private IPowerupCollect collectScript;

    //---Misc Variables
    private int originalLayer;

    public override void Awake() {
        base.Awake();
        sRenderer = GetComponentInChildren<SpriteRenderer>();
        physics = GetComponent<PhysicsEntity>();
        hitbox = GetComponent<BoxCollider2D>();
        collectScript = GetComponent<IPowerupCollect>();
        childAnimator = GetComponentInChildren<Animator>();

        originalLayer = sRenderer.sortingOrder;

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
            Vector2 size = hitbox.size * transform.lossyScale * 0.5f;
            Vector2 origin = body.position + hitbox.offset * transform.lossyScale;

            if (Runner.GetPhysicsScene2D().OverlapBox(origin, size, 0, GroundMask)) {
                DespawnWithPoof();
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
        if (GameManager.Instance && GameManager.Instance.GameEnded) {
            body.velocity = Vector2.zero;
            body.isKinematic = true;
            return;
        }

        if (FollowPlayer) {
            body.position = new(FollowPlayer.transform.position.x, FollowPlayer.cameraController.currentPosition.y + 1.68f);

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

        if (DespawnTimer.Expired(Runner)) {
            DespawnWithPoof();
            return;
        } else {
            float timeRemaining = DespawnTimer.RemainingTime(Runner) ?? 0f;
            sRenderer.enabled = !(timeRemaining <= 1 && timeRemaining * blinkingRate % 1 < 0.5f);
        }

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
                DespawnWithPoof();
                return;
            }
        }
    }

    public void DespawnWithPoof() {
        GameManager.Instance.particleManager.Play(Enums.Particle.Generic_Puff, body.position + hitbox.offset);
        Runner.Despawn(Object, true);
    }

    //---IBlockBumpable overrides
    public override void BlockBump(BasicEntity bumper, Vector3Int tile, InteractableTile.InteractionDirection direction) {
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
        PowerupReserveResult reserve = collectScript.OnPowerupCollect(player, this);

        switch (reserve) {
        case PowerupReserveResult.NoneButPlaySound: {
            //just play the collect sound
            player.PlaySound(newPowerup.soundEffect);
            break;
        }
        case PowerupReserveResult.ReserveOldPowerup: {
            //reserve the powerup we just had
            player.SetReserveItem(oldState);
            if (newState == Enums.PowerupState.MegaMushroom)
                break;

            player.PlaySound(newPowerup.soundEffect);
            break;
        }
        case PowerupReserveResult.ReserveNewPowerup: {
            //reserve the new powerup
            player.SetReserveItem(newState);
            player.PlaySound(Enums.Sounds.Player_Sound_PowerupReserveStore);
            break;
        }
        }

        Runner.Despawn(Object);
    }
}
