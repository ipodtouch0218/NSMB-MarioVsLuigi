using System.Linq;
using UnityEngine;

using NSMB.Utils;
using Fusion;

public class MovingPowerup : CollectableEntity, IBlockBumpable {

    private static int groundMask = -1;

    //---Networked Variables
    [Networked] private PlayerController FollowPlayer { get; set; }
    [Networked] private TickTimer FollowEndTimer { get; set; }
    [Networked] private TickTimer DespawnTimer { get; set; }
    [Networked] private TickTimer IgnorePlayerTimer { get; set; }

    //---Public Variables
    public Powerup powerupScriptable;

    //---Serialized Variables
    [SerializeField] private float speed, bouncePower, terminalVelocity = 4, blinkingRate = 4;
    [SerializeField] private bool avoidPlayers;

    //---Component Variables
    private Rigidbody2D body;
    private SpriteRenderer sRenderer;
    private PhysicsEntity physics;
    private Animator childAnimator;
    private BoxCollider2D hitbox;

    //---Misc Variables
    private int originalLayer;

    public void Awake() {
        body = GetComponent<Rigidbody2D>();
        sRenderer = GetComponentInChildren<SpriteRenderer>();
        physics = GetComponent<PhysicsEntity>();
        childAnimator = GetComponentInChildren<Animator>();
        hitbox = GetComponent<BoxCollider2D>();

        originalLayer = sRenderer.sortingOrder;

        if (groundMask == -1)
            groundMask = LayerMask.GetMask("Ground", "PassthroughInvalid");
    }

    public void OnBeforeSpawned(PlayerController playerToFollow, float pickupDelay) {
        FollowPlayer = playerToFollow;
        FollowEndTimer = TickTimer.CreateFromSeconds(Runner, pickupDelay);
    }

    public override void Spawned() {
        if (FollowPlayer) {
            //spawned following a player
            FollowEndTimer = TickTimer.CreateFromSeconds(Runner, 1f);

            body.isKinematic = true;
            gameObject.layer = Layers.LayerHitsNothing;
            sRenderer.sortingOrder = 15;
        } else {
            //spawned as a normal item.
            gameObject.layer = Layers.LayerEntity;
            Vector2 size = hitbox.size * transform.lossyScale * 0.8f;
            Vector2 origin = body.position + hitbox.offset * transform.lossyScale;

            if (Utils.IsAnyTileSolidBetweenWorldBox(origin, size) || Physics2D.OverlapBox(origin, size, 0, groundMask)) {
                DespawnWithPoof();
                return;
            }
        }
    }

    public override void FixedUpdateNetwork() {
        base.FixedUpdateNetwork();
        if (GameManager.Instance && GameManager.Instance.gameover) {
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
                return;
            }
        }

        if (DespawnTimer.Expired(Runner)) {
            DespawnWithPoof();
            return;
        } else {
            float timeRemaining = DespawnTimer.RemainingTime(Runner) ?? 0f;
            sRenderer.enabled = !(timeRemaining <= 3 && timeRemaining * blinkingRate % 1 < 0.5f);
        }

        Vector2 size = hitbox.size * transform.lossyScale * 0.8f;
        Vector2 origin = body.position + hitbox.offset * transform.lossyScale;

        if (Utils.IsAnyTileSolidBetweenWorldBox(origin, size) || Runner.GetPhysicsScene2D().OverlapBox(origin, size, 0, groundMask)) {
            gameObject.layer = Layers.LayerHitsNothing;
            return;
        } else {
            gameObject.layer = Layers.LayerEntity;
            HandleCollision();
        }

        if (physics.onGround && childAnimator) {
            childAnimator.SetTrigger("trigger");
            hitbox.enabled = false;
            body.isKinematic = true;
            body.gravityScale = 0;
        }

        if (avoidPlayers && physics.onGround) {
            PlayerController closest = GameManager.Instance.players.OrderBy(player => Utils.WrappedDistance(body.position, player.body.position)).FirstOrDefault();
            if (closest) {
                float dist = closest.body.position.x - body.position.x;
                FacingRight = dist < 0 || dist > GameManager.Instance.levelWidthTile * 0.5f;
            }
        }

        body.velocity = new(body.velocity.x, Mathf.Max(-terminalVelocity, body.velocity.y));
    }

    public override void Bump(InteractableTile.InteractionDirection direction) {
        if (FollowPlayer)
            return;

        if (direction == InteractableTile.InteractionDirection.Down)
            return;

        body.velocity = new(body.velocity.x, 5f);
    }

    public void HandleCollision() {
        physics.UpdateCollisions();
        if (physics.hitLeft || physics.hitRight) {
            FacingRight = physics.hitLeft;
            body.velocity = new(speed * (FacingRight ? 1 : -1), body.velocity.y);
        }

        if (physics.onGround) {
            body.velocity = new(speed * (FacingRight ? 1 : -1), Mathf.Max(body.velocity.y, bouncePower));

            if (physics.hitRoof || (physics.hitLeft && physics.hitRight)) {
                DespawnWithPoof();
                return;
            }
        }
    }

    public void DespawnWithPoof() {
        Runner.Despawn(Object, true);
        Instantiate(Resources.Load("Prefabs/Particle/Puff"), transform.GetChild(0).position, Quaternion.identity);
    }


    public override void InteractWithPlayer(PlayerController player) {

        if (!IgnorePlayerTimer.ExpiredOrNotRunning(Runner))
            return;

        Powerup powerup = powerupScriptable;
        Enums.PowerupState newState = powerup.state;
        Enums.PriorityPair pp = Enums.PowerupStatePriority[powerup.state];
        Enums.PriorityPair cp = Enums.PowerupStatePriority[player.State];
        bool reserve = cp.statePriority > pp.itemPriority || player.State == newState;
        bool soundPlayed = false;

        //TODO: refactor
        if (powerup.state == Enums.PowerupState.MegaMushroom && player.State != Enums.PowerupState.MegaMushroom) {

            player.GiantStartTimer = TickTimer.CreateFromSeconds(Runner, player.giantStartTime);
            player.IsInKnockback = false;
            player.groundpound = false;
            player.crouching = false;
            player.propeller = false;
            player.usedPropellerThisJump = false;
            player.flying = false;
            player.drill = false;
            player.inShell = false;
            player.GiantTimer = TickTimer.CreateFromSeconds(Runner, 15f);
            transform.localScale = Vector3.one;
            Instantiate(PrefabList.Instance.Particle_Giant, transform.position, Quaternion.identity);

            player.PlaySoundEverywhere(powerup.soundEffect);
            soundPlayed = true;

        } else if (powerup.prefab == PrefabList.Instance.Powerup_Starman) {
            //starman
            if (!player.IsStarmanInvincible)
                player.StarCombo = 0;

            player.StarmanTimer = TickTimer.CreateFromSeconds(Runner, 10f);
            player.PlaySound(powerup.soundEffect);

            if (player.HeldEntity) {
                player.HeldEntity.SpecialKill(FacingRight, false, 0);
                player.SetHolding(null);
            }

            Runner.Despawn(Object);
            return;

        } else if (powerup.prefab == PrefabList.Instance.Powerup_1Up) {
            player.Lives++;

            Instantiate(PrefabList.Instance.Particle_1Up, transform.position, Quaternion.identity);
            player.PlaySound(powerup.soundEffect);
            Runner.Despawn(Object);
            return;

        } else if (player.State == Enums.PowerupState.MiniMushroom) {
            //check if we're in a mini area to avoid crushing ourselves
            if (player.onGround && Runner.GetPhysicsScene2D().Raycast(body.position, Vector2.up, 0.3f, Layers.MaskOnlyGround)) {
                reserve = true;
            }
        }

        if (reserve) {
            if (player.StoredPowerup == null || (player.StoredPowerup != null && Enums.PowerupStatePriority[player.StoredPowerup.state].statePriority <= pp.statePriority && !(player.State == Enums.PowerupState.Mushroom && newState != Enums.PowerupState.Mushroom))) {
                //dont reserve mushrooms
                player.StoredPowerup = powerup;
            }
            player.PlaySound(Enums.Sounds.Player_Sound_PowerupReserveStore);
        } else {
            if (!(player.State == Enums.PowerupState.Mushroom && newState != Enums.PowerupState.Mushroom) && (player.StoredPowerup == null || Enums.PowerupStatePriority[player.StoredPowerup.state].statePriority <= cp.statePriority)) {
                player.StoredPowerup = (Powerup) Resources.Load("Scriptables/Powerups/" + player.State);
            }

            player.previousState = player.State;
            player.State = newState;
            player.powerupFlash = 2;
            player.crouching |= player.ForceCrouchCheck();
            player.propeller = false;
            player.usedPropellerThisJump = false;
            player.drill &= player.flying;
            player.PropellerLaunchTimer = TickTimer.None;

            if (!soundPlayed)
                player.PlaySound(powerup.soundEffect);
        }

        Runner.Despawn(Object);
    }
}
