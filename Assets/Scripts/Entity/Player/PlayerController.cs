using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

using NSMB.Utils;
using Fusion;

public class PlayerController : FreezableEntity {

    #region Variables

    //---Networked Variables
    //Player State
    [Networked] public Enums.PowerupState State { get; set; } = Enums.PowerupState.Small;
    [Networked] public byte Stars { get; set; }
    [Networked] public byte Coins { get; set; }
    [Networked] public sbyte Lives { get; set; } = -1;
    [Networked] public Powerup StoredPowerup { get; set; }

    //Death & Respawning
    [Networked] public NetworkBool Dead { get; set; }
    [Networked] public TickTimer RespawnTimer { get; set; }

    //Entity Interactions
    [Networked] public HoldableEntity HeldEntity { get; set; }
    [Networked] public TickTimer ShellSlowdownTimer { get; set; }
    [Networked] public TickTimer DamageInvincibilityTimer { get; set; }

    //Powerup Stuffs
    [Networked] public TickTimer FireballShootTimer { get; set; }
    [Networked] public TickTimer StarmanTimer { get; set; }
    [Networked] public TickTimer GiantStartTimer { get; set; }
    [Networked] public TickTimer GiantEndTimer { get; set; }

    //---Properties
    public bool IsStarmanInvincible { get => !StarmanTimer.ExpiredOrNotRunning(Runner); }
    public bool IsDamageable { get => !(IsStarmanInvincible || DamageInvincibilityTimer.ExpiredOrNotRunning(Runner)); }

    //---Components
    private BoxCollider2D[] hitboxes;

    public Enums.PowerupState previousState;
    public FadeOutManager fadeOut;
    public AudioSource sfx, sfxBrick;
    private Animator animator;
    private NetworkRigidbody2D networkRigidbody;
    public CameraController cameraController;
    public PlayerAnimationController animationController;

    public int playerId = -1;
    public float slowriseGravity = 0.85f, normalGravity = 2.5f, flyingGravity = 0.8f, flyingTerminalVelocity = 1.25f, drillVelocity = 7f, groundpoundTime = 0.25f, groundpoundVelocity = 10, blinkingSpeed = 0.25f, terminalVelocity = -7f, jumpVelocity = 6.25f, megaJumpVelocity = 16f, launchVelocity = 12f, wallslideSpeed = -4.25f, giantStartTime = 1.5f, soundRange = 10f, slopeSlidingAngle = 12.5f, pickupTime = 0.5f;
    public float propellerLaunchVelocity = 6, propellerFallSpeed = 2, propellerSpinFallSpeed = 1.5f, propellerSpinTime = 0.75f, propellerDrillBuffer, heightSmallModel = 0.42f, heightLargeModel = 0.82f;

    private GameObject models;




    public bool onGround, previousOnGround, crushGround, doGroundSnap, jumping, properJump, hitRoof, skidding, turnaround, singlejump, doublejump, triplejump, bounce, crouching, groundpound, groundpoundLastFrame, sliding, knockback, hitBlock, functionallyRunning, flying, drill, inShell, hitLeft, hitRight, stuckInBlock, alreadyStuckInBlock, propeller, usedPropellerThisJump, stationaryGiantEnd, fireballKnockback, startedSliding, canShootProjectile;
    public float jumpLandingTimer, landing, koyoteTime, groundpoundCounter, groundpoundStartTimer, pickupTimer, groundpoundDelay, powerupFlash, jumpBuffer, propellerTimer, propellerSpinTimer;
    public float giantTimer, floorAngle, knockbackTimer, pipeTimer;

    //MOVEMENT STAGES
    private static readonly int WALK_STAGE = 1, RUN_STAGE = 3, STAR_STAGE = 4;
    private static readonly float[] SPEED_STAGE_MAX = { 0.9375f, 2.8125f, 4.21875f, 5.625f, 8.4375f };
    private static readonly float SPEED_SLIDE_MAX = 7.5f;
    private static readonly float[] SPEED_STAGE_ACC = { 0.131835975f, 0.06591802875f, 0.05859375f, 0.0439453125f, 1.40625f };
    private static readonly float[] WALK_TURNAROUND_ACC = { 0.0659179686f, 0.146484375f, 0.234375f };
    private static readonly float BUTTON_RELEASE_DEC = 0.0659179686f;
    private static readonly float SKIDDING_THRESHOLD = 4.6875f;
    private static readonly float SKIDDING_DEC = 0.17578125f;
    private static readonly float SKIDDING_STAR_DEC = 1.40625f;

    private static readonly float WALLJUMP_HSPEED = 4.21874f;
    private static readonly float WALLJUMP_VSPEED = 6.4453125f;

    private static readonly float KNOCKBACK_DEC = 0.131835975f;

    private static readonly float[] SPEED_STAGE_SPINNER_MAX = { 1.12060546875f, 2.8125f };
    private static readonly float[] SPEED_STAGE_SPINNER_ACC = { 0.1318359375f, 0.06591796875f };

    private static readonly float[] SPEED_STAGE_MEGA_ACC = { 0.46875f, 0.0805664061f, 0.0805664061f, 0.0805664061f, 0.0805664061f };
    private static readonly float[] WALK_TURNAROUND_MEGA_ACC = { 0.0769042968f, 0.17578125f, 0.3515625f };

    private static readonly float TURNAROUND_THRESHOLD = 2.8125f;
    private static readonly float TURNAROUND_ACC = 0.46875f;
    private float turnaroundFrames;
    private int turnaroundBoostFrames;

    private static readonly float[] BUTTON_RELEASE_ICE_DEC = { 0.00732421875f, 0.02471923828125f, 0.02471923828125f, 0.02471923828125f, 0.02471923828125f };
    private static readonly float SKIDDING_ICE_DEC = 0.06591796875f;
    private static readonly float WALK_TURNAROUND_ICE_ACC = 0.0439453125f;

    private static readonly float SLIDING_45_ACC = 0.2197265625f;
    private static readonly float SLIDING_22_ACC = 0.087890625f;

    public float RunningMaxSpeed => SPEED_STAGE_MAX[RUN_STAGE];
    public float WalkingMaxSpeed => SPEED_STAGE_MAX[WALK_STAGE];

    private int MovementStage {
        get {
            float xVel = Mathf.Abs(body.velocity.x);
            float[] arr = flying ? SPEED_STAGE_SPINNER_MAX : SPEED_STAGE_MAX;
            for (int i = 0; i < arr.Length; i++) {
                if (xVel <= arr[i])
                    return i;
            }
            return arr.Length - 1;
        }
    }

    //Walljumping variables
    private float wallSlideTimer, wallJumpTimer;
    public bool wallSlideLeft, wallSlideRight;

    private int _starCombo;
    public int StarCombo {
        get => IsStarmanInvincible ? _starCombo : 0;
        set => _starCombo = IsStarmanInvincible ? value : 0;
    }

    public Vector2 pipeDirection;

    public FrozenCube frozenObject;

    public Vector2 giantSavedVelocity, previousFrameVelocity, previousFramePosition;

    public GameObject onSpinner;
    public PipeManager pipeEntering;
    public bool step, alreadyGroundpounded;
    public CharacterData character;

    //Tile data
    private Enums.Sounds footstepSound = Enums.Sounds.Player_Walk_Grass;
    public bool onIce;
    private readonly List<Vector3Int> tilesStandingOn = new(), tilesJumpedInto = new(), tilesHitSide = new();

    private GameObject trackIcon;

    private bool initialKnockbackFacingRight = false;

    // == FREEZING VARIABLES ==
    public override bool IsCarryable => true;

    public override bool IsFlying => flying || propeller; //doesn't work consistently?


    public BoxCollider2D MainHitbox => hitboxes[0];
    public Vector2 WorldHitboxSize => MainHitbox.size * transform.lossyScale;

    private readonly Dictionary<GameObject, double> lastCollectTime = new();

    public PlayerNetworkInput currentInputs, previousInputs;

    #endregion

    #region Unity Methods
    public override void Awake() {
        base.Awake();

        cameraController = GetComponent<CameraController>();
        cameraController.IsControllingCamera = Object.HasInputAuthority;

        animator = GetComponentInChildren<Animator>();
        sfx = GetComponent<AudioSource>();
        sfxBrick = GetComponents<AudioSource>()[1];
        //hitboxManager = GetComponent<WrappingHitbox>();
        animationController = GetComponent<PlayerAnimationController>();
        networkRigidbody = GetComponent<NetworkRigidbody2D>();
        fadeOut = GameObject.FindGameObjectWithTag("FadeUI").GetComponent<FadeOutManager>();

        body.position = transform.position = GameManager.Instance.GetSpawnpoint(playerId);

        models = transform.Find("Models").gameObject;

        //TODO
        //int count = 0;
        //foreach (var player in PhotonNetwork.PlayerList) {

        //    Utils.GetCustomProperty(Enums.NetPlayerProperties.Spectator, out bool spectating, photonView.Owner.CustomProperties);
        //    if (spectating)
        //        continue;

        //    if (player == photonView.Owner)
        //        break;
        //    count++;
        //}
        //playerId = count;

        //Utils.GetCustomProperty(Enums.NetRoomProperties.Lives, out lives);


        GameManager.Instance.players.Add(this);
    }

    public override void Despawned(NetworkRunner runner, bool hasState) {
        GameManager.Instance.players.Remove(this);
    }

    public override void Spawned() {
        hitboxes = GetComponents<BoxCollider2D>();
        trackIcon = UIUpdater.Instance.CreatePlayerIcon(this);
        transform.position = body.position = GameManager.Instance.spawnpoint;

        cameraController.Recenter();

        if (Object.HasInputAuthority) {
            networkRigidbody.InterpolationDataSource = InterpolationDataSources.Predicted;
            GameManager.Instance.localPlayer = this;
            GameManager.Instance.spectationManager.Spectating = false;
        }
    }

    private void UpdateInputs() {
        previousInputs = currentInputs;
        currentInputs = GetInput<PlayerNetworkInput>() ?? new();
    }

    public override void FixedUpdateNetwork() {
        //game ended, freeze.

        if (!GameManager.Instance.musicEnabled) {
            models.SetActive(false);
            return;
        }
        if (GameManager.Instance.gameover) {
            body.velocity = Vector2.zero;
            animator.enabled = false;
            body.isKinematic = true;
            return;
        }

        UpdateInputs();

        if (!Dead) {

            CheckForPowerupActions();

            groundpoundLastFrame = groundpound;
            previousOnGround = onGround;
            HandleBlockSnapping();
            bool snapped = GroundSnapCheck();

            CheckForEntityCollision();

            HandleGroundCollision();
            onGround |= snapped;
            doGroundSnap = onGround;
            HandleTileProperties();
            TickCounters(Runner.DeltaTime);
            HandleMovement(Runner.DeltaTime);
            HandleGiantTiles(true);
            UpdateHitbox();
        }

        animationController.UpdateAnimatorStates();
        HandleLayerState();
        previousFrameVelocity = body.velocity;
        previousFramePosition = body.position;
    }
    #endregion

    private void CheckForPowerupActions() {
        //fireball shoot on sprint check
        if (Settings.Instance.fireballFromSprint && currentInputs.buttons.WasPressed(previousInputs.buttons, PlayerControls.Sprint)) {
            if (State == Enums.PowerupState.FireFlower || State == Enums.PowerupState.IceFlower) {
                ActivatePowerupAction();
            }
        }

        //powerup action button check
        if (currentInputs.buttons.WasPressed(previousInputs.buttons, PlayerControls.PowerupAction)) {
            ActivatePowerupAction();
        }
    }

    #region -- COLLISIONS --
    void HandleGroundCollision() {
        tilesJumpedInto.Clear();
        tilesStandingOn.Clear();
        tilesHitSide.Clear();

        bool ignoreRoof = false;
        int down = 0, left = 0, right = 0, up = 0;

        crushGround = false;
        foreach (BoxCollider2D hitbox in hitboxes) {
            ContactPoint2D[] contacts = new ContactPoint2D[20];
            int collisionCount = hitbox.GetContacts(contacts);

            for (int i = 0; i < collisionCount; i++) {
                ContactPoint2D contact = contacts[i];
                GameObject go = contact.collider.gameObject;
                Vector2 n = contact.normal;
                Vector2 p = contact.point + (contact.normal * -0.15f);
                if (n == Vector2.up && contact.point.y > body.position.y)
                    continue;

                Vector3Int vec = Utils.WorldToTilemapPosition(p);
                if (!contact.collider || contact.collider.CompareTag("Player"))
                    continue;

                if (Vector2.Dot(n, Vector2.up) > .05f) {
                    if (Vector2.Dot(body.velocity.normalized, n) > 0.1f && !onGround) {
                        if (!contact.rigidbody || contact.rigidbody.velocity.y < body.velocity.y)
                            //invalid flooring
                            continue;
                    }

                    crushGround |= !go.CompareTag("platform") && !go.CompareTag("frozencube");
                    down++;
                    tilesStandingOn.Add(vec);
                } else if (contact.collider.gameObject.layer == Layers.LayerGround) {
                    if (Vector2.Dot(n, Vector2.down) > .9f) {
                        up++;
                        tilesJumpedInto.Add(vec);
                    } else {
                        if (n.x < 0) {
                            right++;
                        } else {
                            left++;
                        }
                        tilesHitSide.Add(vec);
                    }
                }
            }
        }

        onGround = down >= 1;
        hitLeft = left >= 1;
        hitRight = right >= 1;
        hitRoof = !ignoreRoof && up > 1;
    }
    void HandleTileProperties() {
        onIce = false;
        footstepSound = Enums.Sounds.Player_Walk_Grass;
        foreach (Vector3Int pos in tilesStandingOn) {
            TileBase tile = Utils.GetTileAtTileLocation(pos);
            if (tile == null)
                continue;
            if (tile is TileWithProperties propTile) {
                footstepSound = propTile.footstepSound;
                onIce = propTile.iceSkidding;
            }
        }
    }

    private static readonly Vector2 hitboxOffset = Vector2.up * 0.5f;
    private readonly Collider2D[] results = new Collider2D[64];
    private readonly Collider2D[] tempResults = new Collider2D[64];
    private void CheckForEntityCollision() {
        //Don't check for collisions if we're dead, frozen, in a pipe, etc.
        if (Dead || IsFrozen || pipeEntering)
            return;

        int collisions = 0;
        foreach (BoxCollider2D hitbox in hitboxes) {
            int count = Runner.GetPhysicsScene2D().OverlapBox(body.position + hitbox.offset * transform.localScale, hitbox.size * transform.localScale, 0, default, tempResults);
            Array.Copy(tempResults, 0, results, collisions, count);
            collisions += count;
        }

        for (int i = 0; i < collisions; i++) {
            GameObject collidedObject = results[i].gameObject;

            //don't interact with objects we're holding.
            if (HeldEntity.gameObject == collidedObject)
                continue;

            if (collidedObject.TryGetComponent(out IPlayerInteractable interactable))
                interactable.InteractWithPlayer(this);
        }
    }

    private ContactPoint2D[] contacts = new ContactPoint2D[0];
    public void OnCollisionStay2D(Collision2D collision) {
        if ((knockback && !fireballKnockback) || IsFrozen)
            return;

        GameObject obj = collision.gameObject;

        //double time = PhotonNetwork.Time;
        //if (time - lastCollectTime.GetValueOrDefault(obj) < 0.5d)
        //    return;

        //lastCollectTime[obj] = time;

        switch (collision.gameObject.tag) {
        case "Player": {
            //hit players

            if (contacts.Length < collision.contactCount)
                contacts = new ContactPoint2D[collision.contactCount];
            collision.GetContacts(contacts);

            foreach (ContactPoint2D contact in contacts) {
                GameObject otherObj = collision.gameObject;
                PlayerController other = otherObj.GetComponent<PlayerController>();

                if (other.IsStarmanInvincible) {
                    //They are invincible. let them decide if they've hit us.
                    if (IsStarmanInvincible) {
                        //oh, we both are. bonk.
                        Knockback(otherObj.transform.position.x > body.position.x, 1, true, 0);
                        Knockback(otherObj.transform.position.x < body.position.x, 1, true, 0);
                    }
                    return;
                }

                if (IsStarmanInvincible) {
                    //we are invincible. murder time :)
                    if (other.State == Enums.PowerupState.MegaMushroom) {
                        //wait fuck-
                        Knockback(otherObj.transform.position.x > body.position.x, 1, true, 0);
                        return;
                    }

                    Powerdown(false);
                    body.velocity = previousFrameVelocity;
                    return;
                }

                float dot = Vector2.Dot((body.position - other.body.position).normalized, Vector2.up);
                bool above = dot > 0.7f;
                bool otherAbove = dot < -0.7f;

                //mega mushroom cases
                if (State == Enums.PowerupState.MegaMushroom || other.State == Enums.PowerupState.MegaMushroom) {
                    if (State == Enums.PowerupState.MegaMushroom && other.State == Enums.PowerupState.MegaMushroom) {
                        //both giant
                        if (above) {
                            bounce = true;
                            groundpound = false;
                            drill = false;
                            PlaySound(Enums.Sounds.Enemy_Generic_Stomp);
                        } else if (!otherAbove) {
                            Knockback(otherObj.transform.position.x < body.position.x, 0, true, 0);
                            Knockback(otherObj.transform.position.x > body.position.x, 0, true, 0);
                        }
                    } else if (State == Enums.PowerupState.MegaMushroom) {
                        //only we are giant
                        Powerdown(false);
                        body.velocity = previousFrameVelocity;
                    }
                    return;
                }

                //blue shell cases
                if (inShell) {
                    //we are blue shell
                    if (!otherAbove) {
                        //hit them. powerdown them
                        if (other.inShell) {
                            //collide with both
                            Knockback(otherObj.transform.position.x < body.position.x, 1, true, 0);
                            Knockback(otherObj.transform.position.x > body.position.x, 1, true, 0);
                        } else {
                            Powerdown(false);
                        }
                        float dotRight = Vector2.Dot((body.position - other.body.position).normalized, Vector2.right);
                        FacingRight = dotRight > 0;
                        return;
                    }
                }
                if (State == Enums.PowerupState.BlueShell && otherAbove && (!other.groundpound && !other.drill) && (crouching || groundpound)) {
                    body.velocity = new(SPEED_STAGE_MAX[RUN_STAGE] * 0.9f * (otherObj.transform.position.x < body.position.x ? 1 : -1), body.velocity.y);
                }
                if (other.inShell && !above)
                    return;

                if (!above && other.State == Enums.PowerupState.BlueShell && !other.inShell && other.crouching && !groundpound && !drill) {
                    //they are blue shell
                    bounce = true;
                    PlaySound(Enums.Sounds.Enemy_Generic_Stomp);
                    return;
                }

                if (above) {
                    //hit them from above
                    bounce = !groundpound && !drill;
                    bool groundpounded = groundpound || drill;

                    if (State == Enums.PowerupState.MiniMushroom && other.State != Enums.PowerupState.MiniMushroom) {
                        //we are mini, they arent. special rules.
                        if (groundpounded) {
                            Knockback(otherObj.transform.position.x < body.position.x, 1, false, 0);
                            groundpound = false;
                            bounce = true;
                        } else {
                            PlaySound(Enums.Sounds.Enemy_Generic_Stomp);
                        }
                    } else if (other.State == Enums.PowerupState.MiniMushroom && groundpounded) {
                        //we are big, groundpounding a mini opponent. squish.
                        Knockback(otherObj.transform.position.x > body.position.x, 3, false, 0);
                        bounce = false;
                    } else {
                        if (other.State == Enums.PowerupState.MiniMushroom && groundpounded) {
                            Powerdown(false);
                        } else {
                            Knockback(otherObj.transform.position.x < body.position.x, groundpounded ? 3 : 1, false, 0);
                        }
                    }
                    body.velocity = new Vector2(previousFrameVelocity.x, body.velocity.y);

                    return;
                } else if (!knockback && !other.knockback && !otherAbove && onGround && other.onGround && (Mathf.Abs(previousFrameVelocity.x) > WalkingMaxSpeed || Mathf.Abs(other.previousFrameVelocity.x) > WalkingMaxSpeed)) {
                    //bump

                    Knockback(otherObj.transform.position.x < body.position.x, 1, true, 0);
                    Knockback(otherObj.transform.position.x > body.position.x, 1, true, 0);
                }
            }
            break;
        }
        case "MarioBrosPlatform": {
            List<Vector2> points = new();
            foreach (ContactPoint2D c in collision.contacts) {
                if (c.normal != Vector2.down)
                    continue;

                points.Add(c.point);
            }
            if (points.Count == 0)
                return;

            Vector2 avg = new();
            foreach (Vector2 point in points)
                avg += point;
            avg /= points.Count;

            MarioBrosPlatform platform = obj.GetComponent<MarioBrosPlatform>();
            //TODO:
            platform.Bump(0, avg);
            break;
        }
        case "frozencube": {
            //if (HeldEntity == obj || (holdingOld == obj && throwInvincibility > 0))
            //    return;

            obj.GetComponent<FrozenCube>().InteractWithPlayer(this);
            break;
        }
        }
    }

    public void OnTriggerEnter2D(Collider2D collider) {
        if (Dead || IsFrozen || pipeEntering || !MainHitbox.IsTouching(collider))
            return;


        OnTriggerStay2D(collider);
    }

    protected void OnTriggerStay2D(Collider2D collider) {
        GameObject obj = collider.gameObject;
        if (obj.CompareTag("spinner")) {
            onSpinner = obj;
            return;
        }
    }

    protected void OnTriggerExit2D(Collider2D collider) {
        if (collider.CompareTag("spinner"))
            onSpinner = null;
    }
    #endregion

    #region -- CONTROLLER FUNCTIONS --
    private void ActivatePowerupAction() {
        if (Dead || IsFrozen || knockback || pipeEntering || GameManager.Instance.gameover || HeldEntity)
            return;

        switch (State) {
        case Enums.PowerupState.IceFlower:
        case Enums.PowerupState.FireFlower: {
            if (wallSlideLeft || wallSlideRight || groundpound || triplejump || flying || drill || crouching || sliding)
                return;

            int count = 0;
            foreach (FireballMover existingFire in FindObjectsOfType<FireballMover>()) {
                if (existingFire.owner == Object.InputAuthority && ++count >= 6)
                    return;
            }

            if (count <= 1) {
                FireballShootTimer = TickTimer.CreateFromSeconds(Runner, 1.25f);
                canShootProjectile = count == 0;
            } else if (FireballShootTimer.Expired(Runner)) {
                FireballShootTimer = TickTimer.CreateFromSeconds(Runner, 1.25f);
                canShootProjectile = true;
            } else if (canShootProjectile) {
                canShootProjectile = false;
            } else {
                return;
            }

            bool ice = State == Enums.PowerupState.IceFlower;
            FireballMover prefab = ice ? PrefabList.Iceball : PrefabList.Fireball;
            Enums.Sounds sound = ice ? Enums.Sounds.Powerup_Iceball_Shoot : Enums.Sounds.Powerup_Fireball_Shoot;

            Vector2 pos = body.position + new Vector2(FacingRight ^ animator.GetCurrentAnimatorStateInfo(0).IsName("turnaround") ? 0.5f : -0.5f, 0.3f);
            if (Utils.IsTileSolidAtWorldLocation(pos)) {
                Instantiate(prefab.wallHitPrefab, pos, Quaternion.identity);
            } else {
                Runner.Spawn(prefab, pos, onBeforeSpawned: (runner, obj) => {
                    FireballMover mover = obj.GetComponent<FireballMover>();
                    mover.OnBeforeSpawned(this, !FacingRight ^ animator.GetCurrentAnimatorStateInfo(0).IsName("turnaround"));
                });
            }
            PlaySound(sound);

            animator.SetTrigger("fireball");
            wallJumpTimer = 0;
            break;
        }
        case Enums.PowerupState.PropellerMushroom: {
            if (groundpound || (flying && drill) || propeller || crouching || sliding || wallJumpTimer > 0)
                return;

            StartPropeller();
            break;
        }
        }
    }

    protected void StartPropeller() {
        if (usedPropellerThisJump)
            return;

        body.velocity = new Vector2(body.velocity.x, propellerLaunchVelocity);
        propellerTimer = 1f;
        PlaySound(Enums.Sounds.Powerup_PropellerMushroom_Start);

        animator.SetTrigger("propeller_start");
        propeller = true;
        flying = false;
        crouching = false;

        singlejump = false;
        doublejump = false;
        triplejump = false;

        wallSlideLeft = false;
        wallSlideRight = false;

        if (onGround) {
            onGround = false;
            doGroundSnap = false;
            body.position += Vector2.up * 0.15f;
        }
        usedPropellerThisJump = true;
    }

    public void OnReserveItem(InputAction.CallbackContext context) {
        if (!Object.HasInputAuthority || GameManager.Instance.paused || GameManager.Instance.gameover)
            return;

        if (StoredPowerup == null || Dead) {
            PlaySound(Enums.Sounds.UI_Error);
            return;
        }

        RPC_SpawnReserveItem();
    }
    #endregion

    #region -- POWERUP / POWERDOWN --

    public void Powerdown(bool ignoreInvincible) {
        if (ignoreInvincible || !IsDamageable)
            return;

        previousState = State;
        bool nowDead = false;

        switch (State) {
        case Enums.PowerupState.MiniMushroom:
        case Enums.PowerupState.Small: {
            Death(false, false);
            nowDead = true;
            break;
        }
        case Enums.PowerupState.Mushroom: {
            State = Enums.PowerupState.Small;
            powerupFlash = 2f;
            SpawnStars(1, false);
            break;
        }
        case Enums.PowerupState.FireFlower:
        case Enums.PowerupState.IceFlower:
        case Enums.PowerupState.PropellerMushroom:
        case Enums.PowerupState.BlueShell: {
            State = Enums.PowerupState.Mushroom;
            powerupFlash = 2f;
            SpawnStars(1, false);
            break;
        }
        }
        propeller = false;
        propellerTimer = 0;
        propellerSpinTimer = 0;
        usedPropellerThisJump = false;

        if (!nowDead) {
            DamageInvincibilityTimer = TickTimer.CreateFromSeconds(Runner, 3f);
            PlaySound(Enums.Sounds.Player_Sound_Powerdown);
        }
    }
    #endregion

    #region -- FREEZING --

    public override void Freeze(FrozenCube cube) {
        if (knockback || !IsDamageable || IsFrozen || State == Enums.PowerupState.MegaMushroom)
            return;

        PlaySound(Enums.Sounds.Enemy_Generic_Freeze);
        frozenObject = cube;
        IsFrozen = true;
        frozenObject.AutoBreakTimer = TickTimer.CreateFromSeconds(Runner, 1.75f);
        animator.enabled = false;
        body.isKinematic = true;
        body.simulated = false;
        knockback = false;
        skidding = false;
        drill = false;
        wallSlideLeft = false;
        wallSlideRight = false;
        propeller = false;

        propellerTimer = 0;
        skidding = false;
    }

    public override void Unfreeze(byte reasonByte) {
        if (!IsFrozen)
            return;

        IsFrozen = false;
        animator.enabled = true;
        body.simulated = true;
        body.isKinematic = false;

        int knockbackStars = reasonByte switch {
            (byte) UnfreezeReason.Timer => 0,
            (byte) UnfreezeReason.Groundpounded => 2,
            _ => 1
        };

        if (frozenObject && frozenObject.Object.HasStateAuthority) {
            frozenObject.Holder?.Knockback(frozenObject.Holder.FacingRight, 1, true, 0);
            frozenObject.Kill();
        }

        if (knockbackStars > 0)
            Knockback(FacingRight, knockbackStars, true, -1);
        else
            DamageInvincibilityTimer = TickTimer.CreateFromSeconds(Runner, 1.5f);
    }
    #endregion

    #region -- COIN / STAR COLLECTION --



    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RPC_SpawnReserveItem() {
        if (StoredPowerup == null)
            return;

        Runner.Spawn(StoredPowerup.prefab, body.position + Vector2.up * 5f, Quaternion.identity, 0);
        PlaySound(Enums.Sounds.Player_Sound_PowerupReserveUse);
        StoredPowerup = null;
    }

    public void SpawnRandomItem() {
        Powerup randomItem = Utils.GetRandomItem(Runner, this);
        Runner.Spawn(randomItem.prefab, body.position + Vector2.up * 5f, Quaternion.identity, 0);

        PlaySound(Enums.Sounds.Player_Sound_PowerupReserveUse);
    }

    private void SpawnStars(int amount, bool deathplane) {

        bool fastStars = amount > 2 && Stars > 2;
        int starDirection = FacingRight ? 1 : 2;

        while (amount > 0) {
            if (Stars <= 0)
                break;

            if (!fastStars) {
                if (starDirection == 0)
                    starDirection = 2;
                if (starDirection == 3)
                    starDirection = 1;
            }

            Runner.Spawn(PrefabList.BigStar, body.position + Vector2.up * WorldHitboxSize.y, onBeforeSpawned: (runner, obj) => {
                StarBouncer bouncer = obj.GetComponent<StarBouncer>();
                bouncer.OnBeforeSpawned((byte) starDirection, false, deathplane);
            });

            starDirection = (starDirection + 1) % 4;
            Stars--;
            amount--;
        }
        GameManager.Instance.CheckForWinner();
    }
    #endregion

    #region -- DEATH / RESPAWNING --
    public void Death(bool deathplane, bool fire) {
        if (Dead)
            return;

        //TODO: idk?

        //if (info.Sender != photonView.Owner)
        //    return;

        animator.Play("deadstart");
        if (--Lives == 0) {
            GameManager.Instance.CheckForWinner();
        }

        Dead = true;
        onSpinner = null;
        pipeEntering = null;
        inShell = false;
        propeller = false;
        propellerSpinTimer = 0;
        flying = false;
        drill = false;
        sliding = false;
        crouching = false;
        skidding = false;
        turnaround = false;
        groundpound = false;
        knockback = false;
        wallSlideLeft = false;
        wallSlideRight = false;
        animator.SetBool("knockback", false);
        animator.SetBool("flying", false);
        animator.SetBool("firedeath", fire);

        PlaySound(cameraController.IsControllingCamera ? Enums.Sounds.Player_Sound_Death : Enums.Sounds.Player_Sound_DeathOthers);

        SpawnStars(1, deathplane);
        body.isKinematic = false;
        if (HeldEntity) {
            HeldEntity.Throw(FacingRight, true);
            HeldEntity = null;
        }

        if (Object.HasInputAuthority)
            ScoreboardUpdater.instance.OnDeathToggle();
    }

    public void PreRespawn() {

        sfx.enabled = true;
        if (Lives == 0) {
            GameManager.Instance.CheckForWinner();

            if (Object.HasInputAuthority)
                GameManager.Instance.spectationManager.Spectating = true;

            Runner.Despawn(Object);
            Destroy(trackIcon);
            return;
        }
        transform.localScale = Vector2.one;
        transform.position = body.position = GameManager.Instance.GetSpawnpoint(playerId);
        Dead = false;
        previousState = State = Enums.PowerupState.Small;
        animationController.DisableAllModels();
        animator.SetTrigger("respawn");
        StarmanTimer = TickTimer.None;
        giantTimer = 0;
        GiantEndTimer = TickTimer.None;
        GiantStartTimer = TickTimer.None;
        groundpound = false;
        body.isKinematic = false;

        GameObject particle = (GameObject) Instantiate(Resources.Load("Prefabs/Particle/Respawn"), body.position, Quaternion.identity);
        particle.GetComponent<RespawnParticle>().player = this;

        //gameObject.SetActive(false);
        cameraController.Recenter();
    }

    public void Respawn() {

        //gameObject.SetActive(true);
        Dead = false;
        State = Enums.PowerupState.Small;
        previousState = Enums.PowerupState.Small;
        body.velocity = Vector2.zero;
        wallSlideLeft = false;
        wallSlideRight = false;
        wallSlideTimer = 0;
        wallJumpTimer = 0;
        flying = false;

        propeller = false;
        propellerSpinTimer = 0;
        usedPropellerThisJump = false;
        propellerTimer = 0;

        crouching = false;
        onGround = false;
        sliding = false;
        koyoteTime = 1f;
        jumpBuffer = 0;
        StarmanTimer = TickTimer.None;
        GiantStartTimer = TickTimer.None;
        GiantEndTimer = TickTimer.None;
        giantTimer = 0;
        singlejump = false;
        doublejump = false;
        turnaround = false;
        triplejump = false;
        knockback = false;
        bounce = false;
        skidding = false;
        groundpound = false;
        inShell = false;
        landing = 0f;
        ResetKnockback();
        Instantiate(Resources.Load("Prefabs/Particle/Puff"), transform.position, Quaternion.identity);
        models.transform.rotation = Quaternion.Euler(0, 180, 0);

        if (Object.HasInputAuthority)
            ScoreboardUpdater.instance.OnRespawnToggle();

    }
    #endregion

    #region -- SOUNDS / PARTICLES --
    public void PlaySoundEverywhere(Enums.Sounds sound) {
        GameManager.Instance.sfx.PlayOneShot(sound.GetClip(character));
    }
    public void PlaySound(Enums.Sounds sound, byte variant, float volume) {
        if (sound == Enums.Sounds.Powerup_MegaMushroom_Break_Block) {
            sfxBrick.Stop();
            sfxBrick.clip = sound.GetClip(character, variant);
            sfxBrick.Play();
        } else {
            sfx.PlayOneShot(sound.GetClip(character, variant), volume);
        }
    }
    public void PlaySound(Enums.Sounds sound, byte variant) {
        PlaySound(sound, variant, 1);
    }
    public void PlaySound(Enums.Sounds sound) {
        PlaySound(sound, 0, 1);
    }
    protected void SpawnParticle(string particle, Vector2 worldPos) {
        Instantiate(Resources.Load(particle), worldPos, Quaternion.identity);
    }
    protected void SpawnParticle(string particle, Vector2 worldPos, Vector3 rot) {
        Instantiate(Resources.Load(particle), worldPos, Quaternion.Euler(rot));
    }

    protected void GiantFootstep() {
        CameraController.ScreenShake = 0.15f;
        SpawnParticle("Prefabs/Particle/GroundpoundDust", body.position + new Vector2(FacingRight ? 0.5f : -0.5f, 0));
        PlaySound(Enums.Sounds.Powerup_MegaMushroom_Walk, (byte) (step ? 1 : 2));
        step = !step;
    }

    protected void Footstep() {
        if (State == Enums.PowerupState.MegaMushroom)
            return;

        bool left = currentInputs.buttons.IsSet(PlayerControls.Left);
        bool right = currentInputs.buttons.IsSet(PlayerControls.Right);

        bool reverse = body.velocity.x != 0 && ((left ? 1 : -1) == Mathf.Sign(body.velocity.x));
        if (onIce && (left ^ right) && reverse) {
            PlaySound(Enums.Sounds.World_Ice_Skidding);
            return;
        }
        if (propeller) {
            PlaySound(Enums.Sounds.Powerup_PropellerMushroom_Kick);
            return;
        }
        if (Mathf.Abs(body.velocity.x) < WalkingMaxSpeed)
            return;

        PlaySound(footstepSound, (byte) (step ? 1 : 2), Mathf.Abs(body.velocity.x) / (RunningMaxSpeed + 4));
        step = !step;
    }
    #endregion

    #region -- TILE COLLISIONS --
    private void HandleGiantTiles(bool pipes) {
        //TODO?
        if (State != Enums.PowerupState.MegaMushroom || !GiantStartTimer.ExpiredOrNotRunning(Runner))
            return;

        Vector2 checkSize = WorldHitboxSize * 1.1f;

        bool grounded = previousFrameVelocity.y < -8f && onGround;
        Vector2 offset = Vector2.zero;
        if (grounded)
            offset = Vector2.down / 2f;

        Vector2 checkPosition = body.position + (Vector2.up * checkSize * 0.5f) + (2 * Time.fixedDeltaTime * body.velocity) + offset;

        Vector3Int minPos = Utils.WorldToTilemapPosition(checkPosition - (checkSize * 0.5f), wrap: false);
        Vector3Int size = Utils.WorldToTilemapPosition(checkPosition + (checkSize * 0.5f), wrap: false) - minPos;

        for (int x = 0; x <= size.x; x++) {
            for (int y = 0; y <= size.y; y++) {
                Vector3Int tileLocation = new(minPos.x + x, minPos.y + y, 0);
                Vector2 worldPosCenter = Utils.TilemapToWorldPosition(tileLocation) + Vector3.one * 0.25f;
                Utils.WrapTileLocation(ref tileLocation);

                InteractableTile.InteractionDirection dir = InteractableTile.InteractionDirection.Up;
                if (worldPosCenter.y - 0.25f + Physics2D.defaultContactOffset * 2f <= body.position.y) {
                    if (!grounded && !groundpound)
                        continue;

                    dir = InteractableTile.InteractionDirection.Down;
                } else if (worldPosCenter.y + Physics2D.defaultContactOffset * 2f >= body.position.y + size.y) {
                    dir = InteractableTile.InteractionDirection.Up;
                } else if (worldPosCenter.x <= body.position.x) {
                    dir = InteractableTile.InteractionDirection.Left;
                } else if (worldPosCenter.x >= body.position.x) {
                    dir = InteractableTile.InteractionDirection.Right;
                }

                BreakablePipeTile pipe = GameManager.Instance.tilemap.GetTile<BreakablePipeTile>(tileLocation);
                if (pipe && (pipe.upsideDownPipe || !pipes || groundpound))
                    continue;

                InteractWithTile(tileLocation, dir);
            }
        }
        if (pipes) {
            for (int x = 0; x <= size.x; x++) {
                for (int y = size.y; y >= 0; y--) {
                    Vector3Int tileLocation = new(minPos.x + x, minPos.y + y, 0);
                    Vector2 worldPosCenter = Utils.TilemapToWorldPosition(tileLocation) + Vector3.one * 0.25f;
                    Utils.WrapTileLocation(ref tileLocation);

                    InteractableTile.InteractionDirection dir = InteractableTile.InteractionDirection.Up;
                    if (worldPosCenter.y - 0.25f + Physics2D.defaultContactOffset * 2f <= body.position.y) {
                        if (!grounded && !groundpound)
                            continue;

                        dir = InteractableTile.InteractionDirection.Down;
                    } else if (worldPosCenter.x - 0.25f < checkPosition.x - checkSize.x * 0.5f) {
                        dir = InteractableTile.InteractionDirection.Left;
                    } else if (worldPosCenter.x + 0.25f > checkPosition.x + checkSize.x * 0.5f) {
                        dir = InteractableTile.InteractionDirection.Right;
                    }

                    BreakablePipeTile pipe = GameManager.Instance.tilemap.GetTile<BreakablePipeTile>(tileLocation);
                    if (!pipe || !pipe.upsideDownPipe || dir == InteractableTile.InteractionDirection.Up)
                        continue;

                    InteractWithTile(tileLocation, dir);
                }
            }
        }
    }

    int InteractWithTile(Vector3Int tilePos, InteractableTile.InteractionDirection direction) {
        //TODO:
        //if (!photonView.IsMine)
        //    return 0;

        TileBase tile = GameManager.Instance.tilemap.GetTile(tilePos);
        if (!tile)
            return 0;
        if (tile is InteractableTile it)
            return it.Interact(this, direction, Utils.TilemapToWorldPosition(tilePos)) ? 1 : 0;

        return 0;
    }
    #endregion

    #region -- KNOCKBACK --

    public void Knockback(bool fromRight, int starsToDrop, bool fireball, int attackerView) {
        if (fireball && fireballKnockback && knockback)
            return;
        if (knockback && !fireballKnockback)
            return;

        if (!GameManager.Instance.GameStarted || !DamageInvincibilityTimer.ExpiredOrNotRunning(Runner) || pipeEntering || IsFrozen || Dead || !GiantStartTimer.ExpiredOrNotRunning(Runner) || !GiantEndTimer.ExpiredOrNotRunning(Runner))
            return;

        if (State == Enums.PowerupState.MiniMushroom && starsToDrop > 1) {
            SpawnStars(2, false);
            Powerdown(false);
            return;
        }

        if (knockback || fireballKnockback)
            starsToDrop = Mathf.Min(1, starsToDrop);

        knockback = true;
        knockbackTimer = 0.5f;
        fireballKnockback = fireball;
        initialKnockbackFacingRight = FacingRight;

        //TODO:
        //PhotonView attacker = PhotonNetwork.GetPhotonView(attackerView);
        //if (attackerView >= 0) {
        //    if (attacker)
        //        SpawnParticle("Prefabs/Particle/PlayerBounce", attacker.transform.position);
        //
        //    if (fireballKnockback)
        //        PlaySound(Enums.Sounds.Player_Sound_Collision_Fireball, 0, 3);
        //    else
        //        PlaySound(Enums.Sounds.Player_Sound_Collision, 0, 3);
        //}
        animator.SetBool("fireballKnockback", fireball);
        animator.SetBool("knockforwards", FacingRight != fromRight);

        float megaVelo = State == Enums.PowerupState.MegaMushroom ? 3 : 1;
        body.velocity = new Vector2(
            (fromRight ? -1 : 1) *
            ((starsToDrop + 1) / 2f) *
            4f *
            megaVelo *
            (fireball ? 0.5f : 1f),

            fireball ? 0 : 4.5f
        );

        if (onGround && !fireball)
            body.position += Vector2.up * 0.15f;

        onGround = false;
        doGroundSnap = false;
        inShell = false;
        groundpound = false;
        flying = false;
        propeller = false;
        propellerTimer = 0;
        propellerSpinTimer = 0;
        sliding = false;
        drill = false;
        body.gravityScale = normalGravity;
        wallSlideLeft = wallSlideRight = false;

        SpawnStars(starsToDrop, false);
        HandleLayerState();
    }

    public void ResetKnockbackFromAnim() {
        ResetKnockback();
    }

    protected void ResetKnockback() {
        DamageInvincibilityTimer = TickTimer.CreateFromSeconds(Runner, 2f);
        bounce = false;
        knockback = false;
        body.velocity = new(0, body.velocity.y);
        FacingRight = initialKnockbackFacingRight;
    }
    #endregion

    #region -- ENTITY HOLDING --
    public void HoldingWakeup() {
        HeldEntity = null;
        //holdingOld = null;
        //throwInvincibility = 0;
        Powerdown(false);
    }

    public void SetHolding(HoldableEntity entity) {
        if (HeldEntity) {
            HeldEntity.Holder = null;
            HeldEntity.PreviousHolder = this;
        }

        HeldEntity = entity;
        HeldEntity.Holder = this;
        HeldEntity.PreviousHolder = null;

        if (HeldEntity != null) {
            if (HeldEntity is FrozenCube) {
                animator.Play("head-pickup");
                animator.ResetTrigger("fireball");
                PlaySound(Enums.Sounds.Player_Voice_DoubleJump, 2);
                pickupTimer = 0;
            } else {
                pickupTimer = pickupTime;
            }
            animator.ResetTrigger("throw");
            animator.SetBool("holding", true);

            SetHoldingOffset();
        }
    }
    #endregion

    private void HandleSliding(bool up, bool down, bool left, bool right) {
        startedSliding = false;
        if (groundpound) {
            if (onGround) {
                if (State == Enums.PowerupState.MegaMushroom) {
                    groundpound = false;
                    groundpoundCounter = 0.5f;
                    return;
                }
                if (!inShell && Mathf.Abs(floorAngle) >= slopeSlidingAngle) {
                    groundpound = false;
                    sliding = true;
                    alreadyGroundpounded = true;
                    body.velocity = new Vector2(-Mathf.Sign(floorAngle) * SPEED_SLIDE_MAX, 0);
                    startedSliding = true;
                } else {
                    body.velocity = Vector2.zero;
                    if (!down || State == Enums.PowerupState.MegaMushroom) {
                        groundpound = false;
                        groundpoundCounter = State == Enums.PowerupState.MegaMushroom ? 0.4f : 0.25f;
                    }
                }
            }
            if (up && groundpoundCounter <= 0.05f) {
                groundpound = false;
                body.velocity = Vector2.down * groundpoundVelocity;
            }
        }
        if (!((FacingRight && hitRight) || (!FacingRight && hitLeft)) && crouching && Mathf.Abs(floorAngle) >= slopeSlidingAngle && !inShell && State != Enums.PowerupState.MegaMushroom) {
            sliding = true;
            crouching = false;
            alreadyGroundpounded = true;
        }
        if (sliding && onGround && Mathf.Abs(floorAngle) > slopeSlidingAngle) {
            float angleDeg = floorAngle * Mathf.Deg2Rad;

            bool uphill = Mathf.Sign(floorAngle) == Mathf.Sign(body.velocity.x);
            float speed = Time.fixedDeltaTime * 5f * (uphill ? Mathf.Clamp01(1f - (Mathf.Abs(body.velocity.x) / RunningMaxSpeed)) : 4f);

            float newX = Mathf.Clamp(body.velocity.x - (Mathf.Sin(angleDeg) * speed), -(RunningMaxSpeed * 1.3f), RunningMaxSpeed * 1.3f);
            float newY = Mathf.Sin(angleDeg) * newX + 0.4f;
            body.velocity = new Vector2(newX, newY);

        }

        if (sliding && (up || ((left ^ right) && !down) || (Mathf.Abs(floorAngle) < slopeSlidingAngle && onGround && body.velocity.x == 0 && !down) || (FacingRight && hitRight) || (!FacingRight && hitLeft))) {
            sliding = false;
            if (body.velocity.x == 0 && onGround)
                PlaySound(Enums.Sounds.Player_Sound_SlideEnd);

            //alreadyGroundpounded = false;
        }
    }

    private void HandleSlopes() {
        if (!onGround) {
            floorAngle = 0;
            return;
        }

        RaycastHit2D hit = Physics2D.BoxCast(body.position + (Vector2.up * 0.05f), new Vector2((MainHitbox.size.x - Physics2D.defaultContactOffset * 2f) * transform.lossyScale.x, 0.1f), 0, body.velocity.normalized, (body.velocity * Time.fixedDeltaTime).magnitude, Layers.MaskAnyGround);
        if (hit) {
            //hit ground
            float angle = Vector2.SignedAngle(Vector2.up, hit.normal);
            if (Mathf.Abs(angle) > 89)
                return;

            float x = floorAngle != angle ? previousFrameVelocity.x : body.velocity.x;

            floorAngle = angle;

            float change = Mathf.Sin(angle * Mathf.Deg2Rad) * x * 1.25f;
            body.velocity = new Vector2(x, change);
            onGround = true;
            doGroundSnap = true;
        } else if (onGround) {
            hit = Physics2D.BoxCast(body.position + (Vector2.up * 0.05f), new Vector2((MainHitbox.size.x + Physics2D.defaultContactOffset * 3f) * transform.lossyScale.x, 0.1f), 0, Vector2.down, 0.3f, Layers.MaskAnyGround);
            if (hit) {
                float angle = Vector2.SignedAngle(Vector2.up, hit.normal);
                if (Mathf.Abs(angle) > 89)
                    return;

                float x = floorAngle != angle ? previousFrameVelocity.x : body.velocity.x;
                floorAngle = angle;

                float change = Mathf.Sin(angle * Mathf.Deg2Rad) * x * 1.25f;
                body.velocity = new Vector2(x, change);
                onGround = true;
                doGroundSnap = true;
            } else {
                floorAngle = 0;
            }
        }
    }

    void HandleLayerState() {
        bool hitsNothing = animator.GetBool("pipe") || Dead || stuckInBlock || !GiantStartTimer.ExpiredOrNotRunning(Runner) || (!GiantEndTimer.ExpiredOrNotRunning(Runner) && stationaryGiantEnd);
        //bool shouldntCollide = (DamageInvincibilityTimer > 0 && !IsStarmanInvincible) || (knockback && !fireballKnockback);

        int layer = Layers.LayerPlayer;
        if (hitsNothing) {
            layer = Layers.LayerHitsNothing;
        }
        //else if (shouldntCollide) {
        //    layer = Layers.LayerPassthrough;
        //}

        gameObject.layer = layer;
    }

    private bool GroundSnapCheck() {
        if (Dead || (body.velocity.y > 0 && !onGround) || !doGroundSnap || pipeEntering || gameObject.layer == Layers.LayerHitsNothing)
            return false;

        bool prev = Physics2D.queriesStartInColliders;
        Physics2D.queriesStartInColliders = false;
        RaycastHit2D hit = Physics2D.BoxCast(body.position + Vector2.up * 0.1f, new Vector2(WorldHitboxSize.x, 0.05f), 0, Vector2.down, 0.4f, Layers.MaskAnyGround);
        Physics2D.queriesStartInColliders = prev;
        if (hit) {
            body.position = new(body.position.x, hit.point.y + Physics2D.defaultContactOffset);
            return true;
        }
        return false;
    }

    #region -- PIPES --

    private void DownwardsPipeCheck(bool down) {
        if (!down || State == Enums.PowerupState.MegaMushroom || !onGround || knockback || inShell)
            return;

        foreach (RaycastHit2D hit in Physics2D.RaycastAll(body.position, Vector2.down, 0.1f)) {
            GameObject obj = hit.transform.gameObject;
            if (!obj.CompareTag("pipe"))
                continue;
            PipeManager pipe = obj.GetComponent<PipeManager>();
            if (pipe.miniOnly && State != Enums.PowerupState.MiniMushroom)
                continue;
            if (!pipe.entryAllowed)
                continue;

            //Enter pipe
            pipeEntering = pipe;
            pipeDirection = Vector2.down;

            body.velocity = Vector2.down;
            transform.position = body.position = new Vector2(obj.transform.position.x, transform.position.y);

            PlaySound(Enums.Sounds.Player_Sound_Powerdown);
            crouching = false;
            sliding = false;
            propeller = false;
            drill = false;
            usedPropellerThisJump = false;
            groundpound = false;
            inShell = false;
            break;
        }
    }

    private void UpwardsPipeCheck(bool up) {
        if (!up || groundpound || !hitRoof || State == Enums.PowerupState.MegaMushroom)
            return;

        //todo: change to nonalloc?
        foreach (RaycastHit2D hit in Physics2D.RaycastAll(body.position, Vector2.up, 1f)) {
            GameObject obj = hit.transform.gameObject;
            if (!obj.CompareTag("pipe"))
                continue;
            PipeManager pipe = obj.GetComponent<PipeManager>();
            if (pipe.miniOnly && State != Enums.PowerupState.MiniMushroom)
                continue;
            if (!pipe.entryAllowed)
                continue;

            //pipe found
            pipeEntering = pipe;
            pipeDirection = Vector2.up;

            body.velocity = Vector2.up;
            transform.position = body.position = new Vector2(obj.transform.position.x, transform.position.y);

            PlaySound(Enums.Sounds.Player_Sound_Powerdown);
            crouching = false;
            sliding = false;
            propeller = false;
            usedPropellerThisJump = false;
            flying = false;
            inShell = false;
            break;
        }
    }
    #endregion

    private void HandleCrouching(bool crouchInput) {
        if (sliding || propeller || knockback)
            return;

        if (State == Enums.PowerupState.MegaMushroom) {
            crouching = false;
            return;
        }
        bool prevCrouchState = crouching || groundpound;
        crouching = ((onGround && crouchInput && !groundpound) || (!onGround && crouchInput && crouching) || (crouching && ForceCrouchCheck())) && !HeldEntity;
        if (crouching && !prevCrouchState) {
            //crouch start sound
            PlaySound(State == Enums.PowerupState.BlueShell ? Enums.Sounds.Powerup_BlueShell_Enter : Enums.Sounds.Player_Sound_Crouch);
        }
    }

    public bool ForceCrouchCheck() {
        //janky fortress ceilingn check, m8
        if (State == Enums.PowerupState.BlueShell && onGround && SceneManager.GetActiveScene().buildIndex != 4)
            return false;
        if (State <= Enums.PowerupState.MiniMushroom)
            return false;

        float width = MainHitbox.bounds.extents.x;
        float uncrouchHeight = GetHitboxSize(false).y * transform.lossyScale.y;

        bool ret = Runner.GetPhysicsScene2D().BoxCast(body.position + Vector2.up * 0.1f, new(width - 0.05f, 0.05f), 0, Vector2.up, uncrouchHeight - 0.1f, Layers.MaskOnlyGround);
        return ret;
    }

    private void HandleWallslide(bool holdingLeft, bool holdingRight, bool jump) {

        Vector2 currentWallDirection;
        if (holdingLeft) {
            currentWallDirection = Vector2.left;
        } else if (holdingRight) {
            currentWallDirection = Vector2.right;
        } else if (wallSlideLeft) {
            currentWallDirection = Vector2.left;
        } else if (wallSlideRight) {
            currentWallDirection = Vector2.right;
        } else {
            return;
        }

        HandleWallSlideChecks(currentWallDirection, holdingRight, holdingLeft);

        wallSlideRight &= wallSlideTimer > 0 && hitRight;
        wallSlideLeft &= wallSlideTimer > 0 && hitLeft;

        if (wallSlideLeft || wallSlideRight) {
            //walljump check
            FacingRight = wallSlideLeft;
            if (jump && wallJumpTimer <= 0) {
                //perform walljump

                hitRight = false;
                hitLeft = false;
                body.velocity = new Vector2(WALLJUMP_HSPEED * (wallSlideLeft ? 1 : -1), WALLJUMP_VSPEED);
                singlejump = false;
                doublejump = false;
                triplejump = false;
                onGround = false;
                bounce = false;
                PlaySound(Enums.Sounds.Player_Sound_WallJump);
                PlaySound(Enums.Sounds.Player_Voice_WallJump, (byte) GameManager.Instance.Random.RangeExclusive(1, 3));

                Vector2 offset = new(MainHitbox.size.x / 2f * (wallSlideLeft ? -1 : 1), MainHitbox.size.y / 2f);
                SpawnParticle("Prefabs/Particle/WalljumpParticle", body.position + offset, wallSlideLeft ? Vector3.zero : Vector3.up * 180);

                wallJumpTimer = 16 / 60f;
                animator.SetTrigger("walljump");
                wallSlideTimer = 0;
            }
        } else {
            //walljump starting check
            bool canWallslide = !inShell && body.velocity.y < -0.1 && !groundpound && !onGround && !HeldEntity && State != Enums.PowerupState.MegaMushroom && !flying && !drill && !crouching && !sliding && !knockback;
            if (!canWallslide)
                return;

            //Check 1
            if (wallJumpTimer > 0)
                return;

            //Check 2
            if (wallSlideTimer - Time.fixedDeltaTime <= 0)
                return;

            //Check 4: already handled
            //Check 5.2: already handled

            //Check 6
            if (crouching)
                return;

            //Check 8
            if (!((currentWallDirection == Vector2.right && FacingRight) || (currentWallDirection == Vector2.left && !FacingRight)))
                return;

            //Start wallslide
            wallSlideRight = currentWallDirection == Vector2.right;
            wallSlideLeft = currentWallDirection == Vector2.left;
            propeller = false;
        }

        wallSlideRight &= wallSlideTimer > 0 && hitRight;
        wallSlideLeft &= wallSlideTimer > 0 && hitLeft;
    }

    private void HandleWallSlideChecks(Vector2 wallDirection, bool right, bool left) {
        bool floorCheck = !Physics2D.Raycast(body.position, Vector2.down, 0.3f, Layers.MaskAnyGround);
        if (!floorCheck) {
            wallSlideTimer = 0;
            return;
        }

        bool moveDownCheck = body.velocity.y < 0;
        if (!moveDownCheck)
            return;

        bool wallCollisionCheck = wallDirection == Vector2.left ? hitLeft : hitRight;
        if (!wallCollisionCheck)
            return;

        bool heightLowerCheck = Physics2D.Raycast(body.position + new Vector2(0, .2f), wallDirection, MainHitbox.size.x * 2, Layers.MaskOnlyGround);
        if (!heightLowerCheck)
            return;

        if ((wallDirection == Vector2.left && !left) || (wallDirection == Vector2.right && !right))
            return;

        wallSlideTimer = 16 / 60f;
    }

    private void HandleJumping(bool jumpPressed, bool forceJump) {
        if (knockback || drill || (State == Enums.PowerupState.MegaMushroom && singlejump))
            return;

        bool topSpeed = Mathf.Abs(body.velocity.x) >= RunningMaxSpeed;
        if (bounce || (jumpPressed && (onGround || (koyoteTime < 0.07f && !propeller)) && !startedSliding)) {

            bool canSpecialJump = (jumpPressed || (bounce && forceJump)) && properJump && !flying && !propeller && topSpeed && landing < 0.45f && !HeldEntity && !triplejump && !crouching && !inShell && ((body.velocity.x < 0 && !FacingRight) || (body.velocity.x > 0 && FacingRight)) && !Physics2D.Raycast(body.position + new Vector2(0, 0.1f), Vector2.up, 1f, Layers.MaskOnlyGround);
            float jumpBoost = 0;

            koyoteTime = 1;
            jumpBuffer = 0;
            skidding = false;
            turnaround = false;
            sliding = false;
            wallSlideLeft = false;
            wallSlideRight = false;
            //alreadyGroundpounded = false;
            groundpound = false;
            groundpoundCounter = 0;
            drill = false;
            flying &= bounce;
            propeller &= bounce;

            if (!bounce && onSpinner && !HeldEntity) {
                PlaySound(Enums.Sounds.Player_Voice_SpinnerLaunch);
                PlaySound(Enums.Sounds.World_Spinner_Launch);
                body.velocity = new Vector2(body.velocity.x, launchVelocity);
                flying = true;
                onGround = false;
                body.position += Vector2.up * 0.075f;
                doGroundSnap = false;
                previousOnGround = false;
                crouching = false;
                inShell = false;
                return;
            }

            float vel = State switch {
                Enums.PowerupState.MegaMushroom => megaJumpVelocity,
                _ => jumpVelocity + Mathf.Abs(body.velocity.x) / RunningMaxSpeed * 1.05f,
            };


            if (canSpecialJump && singlejump) {
                //Double jump
                singlejump = false;
                doublejump = true;
                triplejump = false;
                PlaySound(Enums.Sounds.Player_Voice_DoubleJump, (byte) GameManager.Instance.Random.RangeExclusive(1, 3));
            } else if (canSpecialJump && doublejump) {
                //Triple Jump
                singlejump = false;
                doublejump = false;
                triplejump = true;
                jumpBoost = 0.5f;
                PlaySound(Enums.Sounds.Player_Voice_TripleJump);
            } else {
                //Normal jump
                singlejump = true;
                doublejump = false;
                triplejump = false;
            }
            body.velocity = new Vector2(body.velocity.x, vel + jumpBoost);
            onGround = false;
            doGroundSnap = false;
            body.position += Vector2.up * 0.075f;
            groundpoundCounter = 0;
            properJump = true;
            jumping = true;

            if (!bounce) {
                //play jump sound
                Enums.Sounds sound = State switch {
                    Enums.PowerupState.MiniMushroom => Enums.Sounds.Powerup_MiniMushroom_Jump,
                    Enums.PowerupState.MegaMushroom => Enums.Sounds.Powerup_MegaMushroom_Jump,
                    _ => Enums.Sounds.Player_Sound_Jump,
                };
                PlaySound(sound);
            }
            bounce = false;
        }
    }


    public void UpdateHitbox() {
        bool crouchHitbox = State != Enums.PowerupState.MiniMushroom && pipeEntering == null && ((crouching && !groundpound) || inShell || sliding);
        Vector2 hitbox = GetHitboxSize(crouchHitbox);

        MainHitbox.size = hitbox;
        MainHitbox.offset = Vector2.up * 0.5f * hitbox;
    }

    public Vector2 GetHitboxSize(bool crouching) {
        float height;

        if (State <= Enums.PowerupState.Small || (IsStarmanInvincible && !onGround && !crouching && !sliding && !flying && !propeller) || groundpound) {
            height = heightSmallModel;
        } else {
            height = heightLargeModel;
        }

        if (crouching)
            height *= State <= Enums.PowerupState.Small ? 0.7f : 0.5f;

        return new(MainHitbox.size.x, height);
    }

    void HandleWalkingRunning(bool left, bool right) {

        if (wallJumpTimer > 0) {
            if (wallJumpTimer < (14 / 60f) && (hitLeft || hitRight)) {
                wallJumpTimer = 0;
            } else {
                body.velocity = new(WALLJUMP_HSPEED * (FacingRight ? 1 : -1), body.velocity.y);
                return;
            }
        }

        if (groundpound || groundpoundCounter > 0 || knockback || pipeEntering || jumpLandingTimer > 0 || !(wallJumpTimer <= 0 || onGround || body.velocity.y < 0))
            return;

        if (!onGround)
            skidding = false;

        if (inShell) {
            body.velocity = new(SPEED_STAGE_MAX[RUN_STAGE] * 0.9f * (FacingRight ? 1 : -1) * (1f - ShellSlowdownTimer.RemainingTime(Runner) ?? 0f), body.velocity.y);
            return;
        }

        bool run = functionallyRunning && (!flying || State == Enums.PowerupState.MegaMushroom);

        int maxStage;
        if (IsStarmanInvincible && run && onGround)
            maxStage = STAR_STAGE;
        else if (run)
            maxStage = RUN_STAGE;
        else
            maxStage = WALK_STAGE;

        int stage = MovementStage;
        float acc = State == Enums.PowerupState.MegaMushroom ? SPEED_STAGE_MEGA_ACC[stage] : SPEED_STAGE_ACC[stage];
        float sign = Mathf.Sign(body.velocity.x);

        if ((left ^ right) && (!crouching || (crouching && !onGround && State != Enums.PowerupState.BlueShell)) && !knockback && !sliding) {
            //we can walk here

            float speed = Mathf.Abs(body.velocity.x);
            bool reverse = body.velocity.x != 0 && ((left ? 1 : -1) == sign);

            //check that we're not going above our limit
            float max = SPEED_STAGE_MAX[maxStage];
            if (speed > max) {
                acc = -acc;
            }

            if (reverse) {
                turnaround = false;
                if (onGround) {
                    if (speed >= SKIDDING_THRESHOLD && !HeldEntity && State != Enums.PowerupState.MegaMushroom) {
                        skidding = true;
                        FacingRight = sign == 1;
                    }

                    if (skidding) {
                        if (onIce) {
                            acc = SKIDDING_ICE_DEC;
                        } else if (speed > SPEED_STAGE_MAX[RUN_STAGE]) {
                            acc = SKIDDING_STAR_DEC;
                        }  else {
                            acc = SKIDDING_DEC;
                        }
                        turnaroundFrames = 0;
                    } else {
                        if (onIce) {
                            acc = WALK_TURNAROUND_ICE_ACC;
                        } else {
                            turnaroundFrames = Mathf.Min(turnaroundFrames + 0.2f, WALK_TURNAROUND_ACC.Length - 1);
                            acc = State == Enums.PowerupState.MegaMushroom ? WALK_TURNAROUND_MEGA_ACC[(int) turnaroundFrames] : WALK_TURNAROUND_ACC[(int) turnaroundFrames];
                        }
                    }
                } else {
                    acc = SPEED_STAGE_ACC[0];
                }
            } else {

                if (skidding && !turnaround) {
                    skidding = false;
                }

                if (turnaround && turnaroundBoostFrames > 0 && speed != 0) {
                    turnaround = false;
                    skidding = false;
                }

                if (turnaround && speed < TURNAROUND_THRESHOLD) {
                    if (--turnaroundBoostFrames <= 0) {
                        acc = TURNAROUND_ACC;
                        skidding = false;
                    } else {
                        acc = 0;
                    }
                } else {
                    turnaround = false;
                }
            }

            int direction = left ? -1 : 1;
            float newX = body.velocity.x + acc * direction;

            if (Mathf.Abs(newX) - speed > 0) {
                //clamp only if accelerating
                newX = Mathf.Clamp(newX, -max, max);
            }

            if (skidding && !turnaround && Mathf.Sign(newX) != sign) {
                //turnaround
                turnaround = true;
                turnaroundBoostFrames = 5;
                newX = 0;
            }

            body.velocity = new(newX, body.velocity.y);

        } else if (onGround) {
            //not holding anything, sliding, or holding both directions. decelerate

            skidding = false;
            turnaround = false;

            if (body.velocity.x == 0)
                return;

            if (sliding) {
                float angle = Mathf.Abs(floorAngle);
                if (angle > slopeSlidingAngle) {
                    //uphill / downhill
                    acc = (angle > 30 ? SLIDING_45_ACC : SLIDING_22_ACC) * ((Mathf.Sign(floorAngle) == sign) ? -1 : 1);
                } else {
                    //flat ground
                    acc = -SPEED_STAGE_ACC[0];
                }
            } else if (onIce)
                acc = -BUTTON_RELEASE_ICE_DEC[stage];
            else if (knockback)
                acc = -KNOCKBACK_DEC;
            else
                acc = -BUTTON_RELEASE_DEC;

            int direction = (int) Mathf.Sign(body.velocity.x);
            float newX = body.velocity.x + acc * direction;

            if ((direction == -1) ^ (newX <= 0))
                newX = 0;

            if (sliding) {
                newX = Mathf.Clamp(newX, -SPEED_SLIDE_MAX, SPEED_SLIDE_MAX);
            }

            body.velocity = new(newX, body.velocity.y);

            if (newX != 0)
                FacingRight = newX > 0;
        }

        inShell |= State == Enums.PowerupState.BlueShell && !sliding && onGround && functionallyRunning && !HeldEntity && Mathf.Abs(body.velocity.x) >= SPEED_STAGE_MAX[RUN_STAGE] * 0.9f;
        if (onGround || previousOnGround)
            body.velocity = new(body.velocity.x, 0);
    }

    bool HandleStuckInBlock() {
        if (!body || State == Enums.PowerupState.MegaMushroom)
            return false;

        Vector2 checkSize = WorldHitboxSize * new Vector2(1, 0.75f);
        Vector2 checkPos = transform.position + (Vector3) (Vector2.up * checkSize / 2f);

        if (!Utils.IsAnyTileSolidBetweenWorldBox(checkPos, checkSize * 0.9f, false)) {
            alreadyStuckInBlock = stuckInBlock = false;
            return false;
        }
        stuckInBlock = true;
        body.gravityScale = 0;
        body.velocity = Vector2.zero;
        groundpound = false;
        propeller = false;
        drill = false;
        flying = false;
        onGround = true;

        if (!alreadyStuckInBlock) {
            // Code for mario to instantly teleport to the closest free position when he gets stuck

            //prevent mario from clipping to the floor if we got pushed in via our hitbox changing (shell on ice, for example)
            transform.position = body.position = previousFramePosition;
            checkPos = transform.position + (Vector3) (Vector2.up * checkSize / 2f);

            float distanceInterval = 0.025f;
            float minimDistance = 0.95f; // if the minimum actual distance is anything above this value this code will have no effect
            float travelDistance = 0;
            float targetInd = -1; // Basically represents the index of the interval that'll be chosen for mario to be popped out
            int angleInterval = 45;

            for (float i = 0; i < 360 / angleInterval; i ++) { // Test for every angle in the given interval
                float ang = i * angleInterval;
                float testDistance = 0;

                float radAngle = Mathf.PI * ang / 180;
                Vector2 testPos;

                // Calculate the distance mario would have to be moved on a certain angle to stop collisioning
                do {
                    testPos = checkPos + new Vector2(Mathf.Cos(radAngle) * testDistance, Mathf.Sin(radAngle) * testDistance);
                    testDistance += distanceInterval;
                }
                while (Utils.IsAnyTileSolidBetweenWorldBox(testPos, checkSize * 0.975f));

                // This is to give right angles more priority over others when deciding
                float adjustedDistance = testDistance * (1 + Mathf.Abs(Mathf.Sin(radAngle * 2) / 2));

                // Set the new minimum only if the new position is inside of the visible level
                if (testPos.y > GameManager.Instance.cameraMinY && testPos.x > GameManager.Instance.cameraMinX && testPos.x < GameManager.Instance.cameraMaxX){
                    if (adjustedDistance < minimDistance) {
                        minimDistance = adjustedDistance;
                        travelDistance = testDistance;
                        targetInd = i;
                    }
                }
            }

            // Move him
            if (targetInd != -1) {
                float radAngle = Mathf.PI * (targetInd * angleInterval) / 180;
                Vector2 lastPos = checkPos;
                checkPos += new Vector2(Mathf.Cos(radAngle) * travelDistance, Mathf.Sin(radAngle) * travelDistance);
                transform.position = body.position = new(checkPos.x, body.position.y + (checkPos.y - lastPos.y));
                stuckInBlock = false;
                return false; // Freed
            }
        }

        alreadyStuckInBlock = true;
        body.velocity = Vector2.right * 2f;
        return true;
    }

    void TickCounters(float delta) {
        //if (!pipeEntering)
        //    Utils.TickTimer(ref invincible, 0, delta);

        Utils.TickTimer(ref jumpBuffer, 0, delta);
        Utils.TickTimer(ref giantTimer, 0, delta);
        Utils.TickTimer(ref groundpoundCounter, 0, delta);
        Utils.TickTimer(ref groundpoundDelay, 0, delta);
        Utils.TickTimer(ref propellerSpinTimer, 0, delta);
        Utils.TickTimer(ref propellerTimer, 0, delta);
        Utils.TickTimer(ref knockbackTimer, 0, delta);
        Utils.TickTimer(ref pipeTimer, 0, delta);
        Utils.TickTimer(ref wallSlideTimer, 0, delta);
        Utils.TickTimer(ref wallJumpTimer, 0, delta);
        Utils.TickTimer(ref jumpLandingTimer, 0, delta);
        Utils.TickTimer(ref pickupTimer, 0, -delta, pickupTime);

        if (onGround)
            Utils.TickTimer(ref landing, 0, -delta);
    }

    public void FinishMegaMario(bool success) {
        if (success) {
            PlaySoundEverywhere(Enums.Sounds.Player_Voice_MegaMushroom);
        } else {
            //hit a ceiling, cancel
            giantSavedVelocity = Vector2.zero;
            State = Enums.PowerupState.Mushroom;
            GiantEndTimer = TickTimer.CreateFromSeconds(Runner, giantStartTime - GiantStartTimer.RemainingTime(Runner) ?? 0f);
            animator.enabled = true;
            animator.Play("mega-cancel", 0, 1f - (GiantEndTimer.RemainingTime(Runner) ?? 0f / giantStartTime));
            GiantStartTimer = TickTimer.None;
            stationaryGiantEnd = true;
            StoredPowerup = (Powerup) Resources.Load("Scriptables/Powerups/MegaMushroom");
            giantTimer = 0;
            PlaySound(Enums.Sounds.Player_Sound_PowerupReserveStore);
        }
        body.isKinematic = false;
    }

    private void HandleFacingDirection() {
        if (groundpound && !onGround)
            return;

        //Facing direction
        bool right = currentInputs.buttons.IsSet(PlayerControls.Right);
        bool left = currentInputs.buttons.IsSet(PlayerControls.Left);

        if (wallJumpTimer > 0) {
            FacingRight = body.velocity.x > 0;
        } else if (!inShell && !sliding && !skidding && !knockback && !(animator.GetCurrentAnimatorStateInfo(0).IsName("turnaround") || turnaround)) {
            if (right ^ left)
                FacingRight = right;
        } else if (GiantStartTimer.ExpiredOrNotRunning(Runner) && GiantEndTimer.ExpiredOrNotRunning(Runner) && !skidding && !(animator.GetCurrentAnimatorStateInfo(0).IsName("turnaround") || turnaround)) {
            if (knockback || (onGround && State != Enums.PowerupState.MegaMushroom && Mathf.Abs(body.velocity.x) > 0.05f)) {
                FacingRight = body.velocity.x > 0;
            } else if (((wallJumpTimer <= 0 && !inShell) || !GiantStartTimer.ExpiredOrNotRunning(Runner)) && (right || left)) {
                FacingRight = right;
            }
            if (!inShell && ((Mathf.Abs(body.velocity.x) < 0.5f && crouching) || onIce) && (right || left))
                FacingRight = right;
        }
    }

    public void EndMega() {
        GiantEndTimer = TickTimer.CreateFromSeconds(Runner, giantStartTime / 2f);
        State = Enums.PowerupState.Mushroom;
        stationaryGiantEnd = false;
        DamageInvincibilityTimer = TickTimer.CreateFromSeconds(Runner, 3f);
        PlaySoundEverywhere(Enums.Sounds.Powerup_MegaMushroom_End);
        body.velocity = new(body.velocity.x, body.velocity.y > 0 ? (body.velocity.y / 3f) : body.velocity.y);
    }

    public void HandleBlockSnapping() {
        if (pipeEntering || drill)
            return;

        //if we're about to be in the top 2 pixels of a block, snap up to it, (if we can fit)

        if (body.velocity.y > 0)
            return;

        Vector2 nextPos = body.position + Time.fixedDeltaTime * 2f * body.velocity;

        if (!Utils.IsAnyTileSolidBetweenWorldBox(nextPos + WorldHitboxSize.y * 0.5f * Vector2.up, WorldHitboxSize))
            //we are not going to be inside a block next fixed update
            return;

        //we ARE inside a block. figure out the height of the contact
        // 32 pixels per unit
        bool orig = Physics2D.queriesStartInColliders;
        Physics2D.queriesStartInColliders = true;
        RaycastHit2D contact = Physics2D.BoxCast(nextPos + 3f / 32f * Vector2.up, new(WorldHitboxSize.y, 1f / 32f), 0, Vector2.down, 3f / 32f, Layers.MaskAnyGround);
        Physics2D.queriesStartInColliders = orig;

        if (!contact || contact.normal.y < 0.1f) {
            //we didn't hit the ground, we must've hit a ceiling or something.
            return;
        }

        float point = contact.point.y + Physics2D.defaultContactOffset;
        if (body.position.y > point + Physics2D.defaultContactOffset) {
            //dont snap when we're above the block
            return;
        }

        Vector2 newPosition = new(body.position.x, point);

        if (Utils.IsAnyTileSolidBetweenWorldBox(newPosition + WorldHitboxSize.y * 0.5f * Vector2.up, WorldHitboxSize)) {
            //it's an invalid position anyway, we'd be inside something.
            return;
        }

        //valid position, snap upwards
        body.position = newPosition;
    }

    private void HandleMovement(float delta) {
        functionallyRunning = currentInputs.buttons.IsSet(PlayerControls.Sprint) || State == Enums.PowerupState.MegaMushroom || propeller;

        if (Dead)
            return;

        if (body.position.y + transform.lossyScale.y < GameManager.Instance.GetLevelMinY()) {
            //death via pit
            Death(true, false);
            return;
        }

        if (IsFrozen) {
            if (!frozenObject) {
                Unfreeze((byte) FreezableEntity.UnfreezeReason.Other);
            } else {
                body.velocity = Vector2.zero;
                return;
            }
        }

        if (HeldEntity && (HeldEntity.Dead || IsFrozen || HeldEntity.IsFrozen))
            SetHolding(null);

        //FrozenCube holdingCube;
        //if (((holdingCube = HeldEntity as FrozenCube) && holdingCube) || ((holdingCube = holdingOld as FrozenCube) && holdingCube)) {
        //    foreach (BoxCollider2D hitbox in hitboxes) {
        //        Physics2D.IgnoreCollision(hitbox, holdingCube.hitbox, throwInvincibility > 0);
        //    }
        //}

        if (GiantStartTimer.IsRunning) {
            body.velocity = Vector2.zero;
            transform.position = body.position = previousFramePosition;
            if (GiantStartTimer.Expired(Runner)) {
                FinishMegaMario(true);
                GiantStartTimer = TickTimer.None;
            } else {
                body.isKinematic = true;
                if (animator.GetCurrentAnimatorClipInfo(0).Length <= 0 || animator.GetCurrentAnimatorClipInfo(0)[0].clip.name != "mega-scale")
                    animator.Play("mega-scale");


                Vector2 checkSize = WorldHitboxSize * new Vector2(0.75f, 1.1f);
                Vector2 normalizedVelocity = body.velocity;
                if (!groundpound)
                    normalizedVelocity.y = Mathf.Max(0, body.velocity.y);

                Vector2 offset = Vector2.zero;
                if (singlejump && onGround)
                    offset = Vector2.down / 2f;

                Vector2 checkPosition = body.position + Vector2.up * checkSize / 2f + offset;

                Vector3Int minPos = Utils.WorldToTilemapPosition(checkPosition - (checkSize / 2), wrap: false);
                Vector3Int size = Utils.WorldToTilemapPosition(checkPosition + (checkSize / 2), wrap: false) - minPos;

                for (int x = 0; x <= size.x; x++) {
                    Vector3Int tileLocation = new(minPos.x + x, minPos.y + size.y, 0);
                    Utils.WrapTileLocation(ref tileLocation);
                    TileBase tile = Utils.GetTileAtTileLocation(tileLocation);

                    bool cancelMega;
                    if (tile is BreakableBrickTile bbt)
                        cancelMega = !bbt.breakableByGiantMario;
                    else
                        cancelMega = Utils.IsTileSolidAtTileLocation(tileLocation);

                    if (cancelMega) {
                        FinishMegaMario(false);
                        return;
                    }
                }
            }
            return;
        }
        if (GiantEndTimer.IsRunning && stationaryGiantEnd) {
            body.velocity = Vector2.zero;
            body.isKinematic = true;
            transform.position = body.position = previousFramePosition;

            if (GiantEndTimer.Expired(Runner)) {
                DamageInvincibilityTimer = TickTimer.CreateFromSeconds(Runner, 2f);
                body.velocity = giantSavedVelocity;
                animator.enabled = true;
                body.isKinematic = false;
                State = previousState;
                GiantEndTimer = TickTimer.None;
            }
            return;
        }

        if (State == Enums.PowerupState.MegaMushroom) {
            HandleGiantTiles(true);
            if (onGround && singlejump) {
                SpawnParticle("Prefabs/Particle/GroundpoundDust", body.position);
                CameraController.ScreenShake = 0.15f;
                singlejump = false;
            }
            StarmanTimer = TickTimer.None;
        }

        //pipes > stuck in block, else the animation gets janked.
        if (pipeEntering || !GiantStartTimer.ExpiredOrNotRunning(Runner) || (!GiantEndTimer.ExpiredOrNotRunning(Runner) && stationaryGiantEnd) || animator.GetBool("pipe"))
            return;
        if (HandleStuckInBlock())
            return;

        bool right = currentInputs.buttons.IsSet(PlayerControls.Right);
        bool left = currentInputs.buttons.IsSet(PlayerControls.Left);
        bool down = currentInputs.buttons.IsSet(PlayerControls.Down);
        bool up = currentInputs.buttons.IsSet(PlayerControls.Up);
        bool jump = currentInputs.buttons.IsSet(PlayerControls.Jump);

        bool doJump = jump || jumpBuffer > 0 && (onGround || koyoteTime < 0.07f || wallSlideLeft || wallSlideRight);

        //Pipes
        if (pipeTimer <= 0) {
            DownwardsPipeCheck(down);
            UpwardsPipeCheck(up);
        }

        if (knockback) {
            if (bounce)
                ResetKnockback();

            wallSlideLeft = false;
            wallSlideRight = false;
            crouching = false;
            inShell = false;
            body.velocity -= body.velocity * (delta * 2f);
            if (onGround && (Mathf.Abs(body.velocity.x) < 0.35f && knockbackTimer <= 0))
                ResetKnockback();

            if (HeldEntity) {
                HeldEntity.Throw(FacingRight, true);
                HeldEntity = null;
            }
        }

        //activate blocks jumped into
        if (hitRoof) {
            body.velocity = new Vector2(body.velocity.x, Mathf.Min(body.velocity.y, -0.1f));
            bool tempHitBlock = false;
            foreach (Vector3Int tile in tilesJumpedInto) {
                int temp = InteractWithTile(tile, InteractableTile.InteractionDirection.Up);
                if (temp != -1)
                    tempHitBlock |= temp == 1;
            }
            if (tempHitBlock && State == Enums.PowerupState.MegaMushroom) {
                CameraController.ScreenShake = 0.15f;
                PlaySound(Enums.Sounds.World_Block_Bump);
            }
        }


        alreadyGroundpounded &= down;

        if (drill) {
            propellerSpinTimer = 0;
            if (propeller) {
                if (!down) {
                    Utils.TickTimer(ref propellerDrillBuffer, 0, Time.deltaTime);
                    if (propellerDrillBuffer <= 0)
                        drill = false;
                } else {
                    propellerDrillBuffer = 0.15f;
                }
            }
        }

        if (propellerTimer > 0)
            body.velocity = new Vector2(body.velocity.x, propellerLaunchVelocity - (propellerTimer < .4f ? (1 - (propellerTimer / .4f)) * propellerLaunchVelocity : 0));

        if (currentInputs.buttons.IsSet(PlayerControls.PowerupAction) && wallJumpTimer <= 0 && (propeller || !usedPropellerThisJump)) {
            if (body.velocity.y < -0.1f && propeller && !drill && !wallSlideLeft && !wallSlideRight && propellerSpinTimer < propellerSpinTime / 4f) {
                propellerSpinTimer = propellerSpinTime;
                propeller = true;
                PlaySound(Enums.Sounds.Powerup_PropellerMushroom_Spin);
            }
        }

        if (HeldEntity) {
            wallSlideLeft = false;
            wallSlideRight = false;
            SetHoldingOffset();
        }

        //throwing held item
        ThrowHeldItem(left, right, down);

        //blue shell enter/exit
        if (State != Enums.PowerupState.BlueShell || !functionallyRunning)
            inShell = false;

        if (inShell) {
            down = true;
            if (hitLeft || hitRight) {
                foreach (var tile in tilesHitSide)
                    InteractWithTile(tile, InteractableTile.InteractionDirection.Up);
                FacingRight = hitLeft;
                PlaySound(Enums.Sounds.World_Block_Bump);
            }
        }

        //Ground
        if (onGround) {
            if (hitRoof && crushGround && body.velocity.y <= 0.1 && State != Enums.PowerupState.MegaMushroom) {
                //Crushed.
                Powerdown(true);
            }

            koyoteTime = 0;
            usedPropellerThisJump = false;
            wallSlideLeft = false;
            wallSlideRight = false;
            jumping = false;
            if (drill)
                SpawnParticle("Prefabs/Particle/GroundpoundDust", body.position);

            if (onSpinner && Mathf.Abs(body.velocity.x) < 0.3f && !HeldEntity) {
                Transform spnr = onSpinner.transform;
                float diff = body.position.x - spnr.transform.position.x;
                if (Mathf.Abs(diff) >= 0.02f)
                    body.position += -0.6f * Mathf.Sign(diff) * Time.fixedDeltaTime * Vector2.right;
            }
        } else {
            koyoteTime += delta;
            landing = 0;
            skidding = false;
            turnaround = false;
            if (!jumping)
                properJump = false;
        }

        //Crouching
        HandleCrouching(down);

        HandleWallslide(left, right, jump);

        HandleSlopes();

        if (down && !alreadyGroundpounded) {
            HandleGroundpoundStart(left, right);
        } else {
            groundpoundStartTimer = 0;
        }
        HandleGroundpound();

        HandleSliding(up, down, left, right);

        if (onGround) {
            if (propellerTimer < 0.5f) {
                propeller = false;
                propellerTimer = 0;
            }
            flying = false;
            drill = false;
            if (landing <= Time.fixedDeltaTime + 0.01f && !groundpound && !crouching && !inShell && !HeldEntity && State != Enums.PowerupState.MegaMushroom) {
                bool edge = !Physics2D.BoxCast(body.position, MainHitbox.size * 0.75f, 0, Vector2.down, 0, Layers.MaskAnyGround);
                bool edgeLanding = false;
                if (edge) {
                    bool rightEdge = edge && Utils.IsTileSolidAtWorldLocation(body.position + new Vector2(0.25f, -0.25f));
                    bool leftEdge = edge && Utils.IsTileSolidAtWorldLocation(body.position + new Vector2(-0.25f, -0.25f));
                    edgeLanding = (leftEdge || rightEdge) && properJump && edge && (FacingRight == rightEdge);
                }

                if ((triplejump && !(left ^ right))
                    || edgeLanding
                    || (Mathf.Abs(body.velocity.x) < 0.1f)) {

                    if (!onIce)
                        body.velocity = Vector2.zero;

                    animator.Play("jumplanding" + (edgeLanding ? "-edge" : ""));
                    if (edgeLanding)
                        jumpLandingTimer = 0.15f;
                }
            }
            if (landing > 0.2f) {
                singlejump = false;
                doublejump = false;
                triplejump = false;
            }
        }


        if (!(groundpound && !onGround)) {
            //Normal walking/running
            HandleWalkingRunning(left, right);

            //Jumping
            HandleJumping(jump, doJump);
        }


        if (State == Enums.PowerupState.MegaMushroom && giantTimer <= 0)
            EndMega();

        HandleSlopes();
        HandleFacingDirection();

        //slow-rise check
        if (flying || propeller) {
            body.gravityScale = flyingGravity;
        } else {
            float gravityModifier = State switch {
                Enums.PowerupState.MiniMushroom => 0.4f,
                _ => 1,
            };
            float slowriseModifier = State switch {
                Enums.PowerupState.MegaMushroom => 3f,
                _ => 1f,
            };
            if (groundpound)
                gravityModifier *= 1.5f;

            if (body.velocity.y > 2.5) {
                if (doJump || State == Enums.PowerupState.MegaMushroom) {
                    body.gravityScale = slowriseGravity * slowriseModifier;
                } else {
                    body.gravityScale = normalGravity * 1.5f * gravityModifier;
                }
            } else if (onGround || (groundpound && groundpoundCounter > 0)) {
                body.gravityScale = 0f;
            } else {
                body.gravityScale = normalGravity * (gravityModifier / 1.2f);
            }
        }

        //Terminal velocity
        float terminalVelocityModifier = State switch {
            Enums.PowerupState.MiniMushroom => 0.625f,
            Enums.PowerupState.MegaMushroom => 2f,
            _ => 1f,
        };
        if (flying) {
            if (drill) {
                body.velocity = new(body.velocity.x, -drillVelocity);
            } else {
                body.velocity = new(body.velocity.x, Mathf.Max(body.velocity.y, -flyingTerminalVelocity));
            }
        } else if (propeller) {
            if (drill) {
                body.velocity = new(Mathf.Clamp(body.velocity.x, -WalkingMaxSpeed, WalkingMaxSpeed), -drillVelocity);
            } else {
                float htv = WalkingMaxSpeed * 1.18f + (propellerTimer * 2f);
                body.velocity = new(Mathf.Clamp(body.velocity.x, -htv, htv), Mathf.Max(body.velocity.y, propellerSpinTimer > 0 ? -propellerSpinFallSpeed : -propellerFallSpeed));
            }
        } else if (wallSlideLeft || wallSlideRight) {
            body.velocity = new(body.velocity.x, Mathf.Max(body.velocity.y, wallslideSpeed));
        } else if (groundpound) {
            body.velocity = new(body.velocity.x, Mathf.Max(body.velocity.y, -groundpoundVelocity));
        } else {
            body.velocity = new(body.velocity.x, Mathf.Max(body.velocity.y, terminalVelocity * terminalVelocityModifier));
        }

        if (crouching || sliding || skidding) {
            wallSlideLeft = false;
            wallSlideRight = false;
        }

        if (previousOnGround && !onGround && !properJump && crouching && !inShell && !groundpound)
            body.velocity = new(body.velocity.x, -3.75f);
    }

    public void SetHoldingOffset() {
        if (HeldEntity is FrozenCube) {
            HeldEntity.holderOffset = new(0, MainHitbox.size.y * (1f - Utils.QuadraticEaseOut(1f - (pickupTimer / pickupTime))), -2);
        } else {
            HeldEntity.holderOffset = new((FacingRight ? 1 : -1) * 0.25f, State >= Enums.PowerupState.Mushroom ? 0.5f : 0.25f, !FacingRight ? -0.09f : 0f);
        }
    }

    private void ThrowHeldItem(bool left, bool right, bool crouch) {
        if (!((!functionallyRunning || State == Enums.PowerupState.MiniMushroom || State == Enums.PowerupState.MegaMushroom || IsStarmanInvincible || flying || propeller) && HeldEntity))
            return;

        bool throwRight = FacingRight;
        if (left ^ right)
            throwRight = right;

        crouch &= HeldEntity.canPlace;

        //holdingOld = HeldEntity;
        //throwInvincibility = 0.15f;

        //TODO:
        HeldEntity.Throw(throwRight, crouch);
        //holding.photonView.RPC(nameof(HoldableEntity.Throw), RpcTarget.All, throwLeft, crouch, body.position);

        if (!crouch && !knockback) {
            PlaySound(Enums.Sounds.Player_Voice_WallJump, 2);
            //throwInvincibility = 0.5f;
            animator.SetTrigger("throw");
        }

        HeldEntity = null;
    }

    private void HandleGroundpoundStart(bool left, bool right) {

        if (groundpoundStartTimer == 0)
            groundpoundStartTimer = 0.065f;

        Utils.TickTimer(ref groundpoundStartTimer, 0, Time.fixedDeltaTime);

        if (groundpoundStartTimer != 0)
            return;

        if (onGround || knockback || groundpound || drill
            || HeldEntity || crouching || sliding || inShell
            || wallSlideLeft || wallSlideRight || groundpoundDelay > 0)
            return;

        if (!propeller && !flying && (left || right))
            return;

        if (flying) {
            //start drill
            if (body.velocity.y < 0) {
                drill = true;
                hitBlock = true;
                body.velocity = new(0, body.velocity.y);
            }
        } else if (propeller) {
            //start propeller drill
            if (propellerTimer < 0.6f && body.velocity.y < 7) {
                drill = true;
                propellerTimer = 0;
                hitBlock = true;
            }
        } else {
            //start groundpound
            //check if high enough above ground
            if (Physics2D.BoxCast(body.position, MainHitbox.size * Vector2.right * transform.localScale, 0, Vector2.down, 0.15f * (State == Enums.PowerupState.MegaMushroom ? 2.5f : 1), Layers.MaskAnyGround))
                return;

            wallSlideLeft = false;
            wallSlideRight = false;
            groundpound = true;
            singlejump = false;
            doublejump = false;
            triplejump = false;
            hitBlock = true;
            sliding = false;
            body.velocity = Vector2.up * 1.5f;
            groundpoundCounter = groundpoundTime * (State == Enums.PowerupState.MegaMushroom ? 1.5f : 1);
            PlaySound(Enums.Sounds.Player_Sound_GroundpoundStart);
            alreadyGroundpounded = true;
            //groundpoundDelay = 0.75f;
        }
    }

    void HandleGroundpound() {
        if (groundpound && groundpoundCounter > 0 && groundpoundCounter <= .1f)
            body.velocity = Vector2.zero;

        if (groundpound && groundpoundCounter > 0 && groundpoundCounter - Time.fixedDeltaTime <= 0)
            body.velocity = Vector2.down * groundpoundVelocity;

        if (!(onGround && (groundpound || drill) && hitBlock))
            return;

        bool tempHitBlock = false, hitAnyBlock = false;
        foreach (Vector3Int tile in tilesStandingOn) {
            int temp = InteractWithTile(tile, InteractableTile.InteractionDirection.Down);
            if (temp != -1) {
                hitAnyBlock = true;
                tempHitBlock |= temp == 1;
            }
        }
        hitBlock = tempHitBlock;
        if (drill) {
            flying &= hitBlock;
            propeller &= hitBlock;
            drill = hitBlock;
            if (hitBlock)
                onGround = false;
        } else {
            //groundpound
            if (hitAnyBlock) {
                if (State != Enums.PowerupState.MegaMushroom) {
                    Enums.Sounds sound = State switch {
                        Enums.PowerupState.MiniMushroom => Enums.Sounds.Powerup_MiniMushroom_Groundpound,
                        _ => Enums.Sounds.Player_Sound_GroundpoundLanding,
                    };
                    PlaySound(sound);
                    SpawnParticle("Prefabs/Particle/GroundpoundDust", body.position);
                    groundpoundDelay = 0;
                } else {
                    CameraController.ScreenShake = 0.15f;
                }
            }
            if (hitBlock) {
                koyoteTime = 1.5f;
            } else if (State == Enums.PowerupState.MegaMushroom) {
                PlaySound(Enums.Sounds.Powerup_MegaMushroom_Groundpound);
                SpawnParticle("Prefabs/Particle/GroundpoundDust", body.position);
                CameraController.ScreenShake = 0.35f;
            }
        }
    }

    public bool CanPickup() {
        return State != Enums.PowerupState.MiniMushroom && !skidding && !turnaround && !HeldEntity && currentInputs.buttons.IsSet(PlayerControls.Sprint) && !propeller && !flying && !crouching && !Dead && !wallSlideLeft && !wallSlideRight && !doublejump && !triplejump && !groundpound;
    }

    public void OnDrawGizmos() {
        if (!body)
            return;

        Gizmos.DrawRay(body.position, body.velocity);
        Gizmos.DrawCube(body.position + new Vector2(0, WorldHitboxSize.y * 0.5f) + (body.velocity * Time.fixedDeltaTime), WorldHitboxSize);

        Gizmos.color = Color.white;
        foreach (Renderer r in GetComponentsInChildren<Renderer>()) {
            if (r is ParticleSystemRenderer)
                continue;

            Gizmos.DrawWireCube(r.bounds.center, r.bounds.size);
        }
    }
}
