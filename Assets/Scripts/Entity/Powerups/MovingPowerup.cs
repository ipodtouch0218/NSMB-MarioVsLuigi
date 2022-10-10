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
    private SpriteRenderer sRenderer;
    private PhysicsEntity physics;
    private Animator childAnimator;
    private BoxCollider2D hitbox;

    //---Misc Variables
    private int originalLayer;

    public override void Awake() {
        base.Awake();
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

        if (playerToFollow)
            transform.position = body.position = new(playerToFollow.transform.position.x, playerToFollow.cameraController.currentPosition.y + 1.68f);
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
            Vector2 size = hitbox.size * transform.lossyScale * 0.5f;
            Vector2 origin = body.position + hitbox.offset * transform.lossyScale;

            if (Runner.GetPhysicsScene2D().OverlapBox(origin, size, 0, groundMask)) {
                DespawnWithPoof();
                return;
            }
        }

        FacingRight = true;
        DespawnTimer = TickTimer.CreateFromSeconds(Runner, 15f);
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

    public override void Bump(BasicEntity bumper, Vector3Int tile, InteractableTile.InteractionDirection direction) {
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
        GameManager.Instance.particleManager.Play(Enums.Particle.Generic_Puff, body.position);
        Runner.Despawn(Object, true);
    }


    public override void InteractWithPlayer(PlayerController player) {

        if (Collector)
            return;

        if (!FollowEndTimer.ExpiredOrNotRunning(Runner))
            return;

        if (!IgnorePlayerTimer.ExpiredOrNotRunning(Runner))
            return;

        Collector = player;

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
            player.IsGroundpounding = false;
            player.IsCrouching = false;
            player.IsPropellerFlying = false;
            player.usedPropellerThisJump = false;
            player.IsSpinnerFlying = false;
            player.IsDrilling = false;
            player.IsInShell = false;
            player.GiantTimer = TickTimer.CreateFromSeconds(Runner, 15f + player.giantStartTime);
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
            if (player.IsOnGround && Runner.GetPhysicsScene2D().Raycast(body.position, Vector2.up, 0.3f, Layers.MaskOnlyGround)) {
                reserve = true;
            }
        }

        if (reserve) {
            if (player.StoredPowerup == Enums.PowerupState.None || (player.StoredPowerup != Enums.PowerupState.None && Enums.PowerupStatePriority[player.StoredPowerup].statePriority <= pp.statePriority && !(player.State == Enums.PowerupState.Mushroom && newState != Enums.PowerupState.Mushroom))) {
                //dont reserve mushrooms
                player.StoredPowerup = newState;
            }
            player.PlaySound(Enums.Sounds.Player_Sound_PowerupReserveStore);
        } else {
            if (player.State != Enums.PowerupState.Small && (!(player.State == Enums.PowerupState.Mushroom && newState != Enums.PowerupState.Mushroom) && (player.StoredPowerup == Enums.PowerupState.None || Enums.PowerupStatePriority[player.StoredPowerup].statePriority <= cp.statePriority))) {
                player.StoredPowerup = player.State;
            }

            player.previousState = player.State;
            player.State = newState;
            player.powerupFlash = 2;
            player.IsCrouching |= player.ForceCrouchCheck();
            player.IsPropellerFlying = false;
            player.usedPropellerThisJump = false;
            player.IsDrilling &= player.IsSpinnerFlying;
            player.PropellerLaunchTimer = TickTimer.None;

            if (!soundPlayed)
                player.PlaySound(powerup.soundEffect);
        }

        Runner.Despawn(Object);
    }
}
