using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

using Fusion;
using NSMB.Entities.Collectable;
using NSMB.Entities.Collectable.Powerups;
using NSMB.Entities.World;
using NSMB.Extensions;
using NSMB.Game;
using NSMB.Tiles;
using NSMB.Utils;

namespace NSMB.Entities.Player {
    public class PlayerController : FreezableEntity, IPlayerInteractable, IBeforeTick {

        #region Variables

        //---Static Variables
        private static readonly Collider2D[] CollisionBuffer = new Collider2D[64];
        private static readonly Collider2D[] TempCollisionBuffer = new Collider2D[32];
        private static readonly ContactPoint2D[] TileContactBuffer = new ContactPoint2D[32];
        private static ContactFilter2D CollisionFilter;

        //---Networked Variables
        //-Player State
        [Networked(OnChanged = nameof(OnPowerupStateChanged))] public Enums.PowerupState State { get; set; }
        [Networked] public Enums.PowerupState PreviousState { get; set; }
        [Networked] public Enums.PowerupState StoredPowerup { get; set; }
        [Networked] public byte Stars { get; set; }
        [Networked] public byte Coins { get; set; }
        [Networked(OnChanged = nameof(OnLivesChanged))] public sbyte Lives { get; set; }
        [Networked] private sbyte SpawnpointIndex { get; set; }
        //-Player Movement
        //Generic
        [Networked] public PlayerNetworkInput PreviousInputs { get; set; }
        [Networked] public NetworkBool IsFunctionallyRunning { get; set; }
        [Networked] public NetworkBool IsOnGround { get; set; }
        [Networked(OnChanged = nameof(OnIsCrouchingChanged))] public NetworkBool IsCrouching { get; set; }
        [Networked(OnChanged = nameof(OnIsSlidingChanged))] public NetworkBool IsSliding { get; set; }
        [Networked] public NetworkBool IsSkidding { get; set; }
        [Networked] public NetworkBool IsTurnaround { get; set; }
        [Networked] private byte WalkingTurnaroundFrames { get; set; } //TODO: change somehow
        [Networked] private float TurnaroundBoostTime { get; set; } //TODO: change somehow
        [Networked] private float JumpBufferTime { get; set; }
        [Networked] public float CoyoteTime { get; set; }
        [Networked] private float TimeGrounded { get; set; }
        [Networked] public NetworkBool IgnoreCoyoteTime { get; set; }
        [Networked] public float FloorAngle { get; set; }
        [Networked] public NetworkBool OnSlope { get; set; }
        [Networked] public NetworkBool OnIce { get; set; }
        //Jumping
        [Networked(OnChanged = nameof(OnJumpAnimCounterChanged))] private byte JumpAnimCounter { get; set; }
        [Networked] public NetworkBool IsJumping { get; set; }
        [Networked] public PlayerJumpState JumpState { get; set; }
        [Networked] public NetworkBool ProperJump { get; set; }
        [Networked] public NetworkBool DoEntityBounce { get; set; }
        [Networked] public NetworkBool BounceJump { get; set; }
        [Networked] public TickTimer JumpLandingTimer { get; set; }
        [Networked(OnChanged = nameof(OnBlockBumpSoundCounterChanged))] public byte BlockBumpSoundCounter { get; set; }
        //Knockback
        [Networked(OnChanged = nameof(OnIsInKnockbackChanged))] public NetworkBool IsInKnockback { get; set; }
        [Networked] public NetworkBool IsWeakKnockback { get; set; }
        [Networked] public NetworkBool IsForwardsKnockback { get; set; }
        [Networked] private NetworkBool KnockbackWasOriginallyFacingRight { get; set; }
        [Networked] public TickTimer KnockbackTimer { get; set; }
        [Networked] public NetworkObject KnockbackAttacker { get; set; }
        //Groundpound
        [Networked(OnChanged = nameof(OnGroundpoundAnimCounterChanged))] private byte GroundpoundAnimCounter { get; set; }
        [Networked(OnChanged = nameof(OnGroundpoundingChanged))] public NetworkBool IsGroundpounding { get; set; }
        [Networked] public TickTimer GroundpoundStartTimer { get; set; }
        [Networked] public TickTimer GroundpoundCooldownTimer { get; set; }
        [Networked] private NetworkBool GroundpoundHeld { get; set; }
        [Networked] private float GroundpoundStartTime { get; set; }
        [Networked] private NetworkBool ContinueGroundpound { get; set; }
        //Spinner
        [Networked] public SpinnerAnimator OnSpinner { get; set; }
        [Networked] public NetworkBool IsSpinnerFlying { get; set; }
        [Networked(OnChanged = nameof(OnSpinnerLaunchAnimCounterChanged))] public NetworkBool SpinnerLaunchAnimCounter { get; set; }
        [Networked] public NetworkBool IsDrilling { get; set; }
        //Pipes
        [Networked] public Vector2 PipeDirection { get; set; }
        [Networked] public PipeManager CurrentPipe { get; set; }
        [Networked] public NetworkBool PipeEntering { get; set; }
        [Networked(OnChanged = nameof(OnPipeTimerChanged))] public TickTimer PipeTimer { get; set; }
        [Networked] public TickTimer PipeReentryTimer { get; set; }
        //Walljump
        [Networked(OnChanged = nameof(OnWallJumpTimerChanged))] public TickTimer WallJumpTimer { get; set; }
        [Networked] public TickTimer WallSlideEndTimer { get; set; }
        [Networked] public NetworkBool WallSlideLeft { get; set; }
        [Networked] public NetworkBool WallSlideRight { get; set; }
        //Stuck
        [Networked] public NetworkBool IsStuckInBlock { get; set; }
        //Swimming
        [Networked] public NetworkBool SwimJump { get; set; }
        [Networked] public float SwimLeaveForceHoldJumpTime { get; set; }
        [Networked] public NetworkBool IsSwimming { get; set; }
        [Networked(OnChanged = nameof(OnIsWaterWalkingChanged))] public NetworkBool IsWaterWalking { get; set; }
        //-Death & Respawning
        [Networked] private NetworkBool Disconnected { get; set; }
        [Networked(OnChanged = nameof(OnIsDeadChanged))] public NetworkBool IsDead { get; set; }
        [Networked(OnChanged = nameof(OnIsRespawningChanged))] public NetworkBool IsRespawning { get; set; }
        [Networked] public NetworkBool FireDeath { get; set; }
        [Networked] public TickTimer RespawnTimer { get; set; }
        [Networked] public TickTimer PreRespawnTimer { get; set; }

        //-Entity Interactions
        [Networked(OnChanged = nameof(OnHeldEntityChanged))] public HoldableEntity HeldEntity { get; set; }
        [Networked(OnChanged = nameof(OnThrowAnimCounterChanged))] public byte ThrowAnimCounter { get; set; }
        [Networked] public float HoldStartTime { get; set; }
        [Networked] public TickTimer ShellSlowdownTimer { get; set; }
        [Networked] public TickTimer DamageInvincibilityTimer { get; set; }
        [Networked] private byte _StarCombo { get; set; }

        //-Powerup Stuffs
        [Networked(OnChanged = nameof(OnFireballAnimCounterChanged))] private byte FireballAnimCounter { get; set; }
        [Networked] public TickTimer FireballShootTimer { get; set; }
        [Networked] public TickTimer FireballDelayTimer { get; set; }
        [Networked] public NetworkBool CanShootAdditionalFireball { get; set; }
        [Networked] public TickTimer StarmanTimer { get; set; }
        [Networked] public NetworkBool IsPropellerFlying { get; set; }
        [Networked(OnChanged = nameof(OnPropellerLaunchTimerChanged))] public TickTimer PropellerLaunchTimer { get; set; }
        [Networked(OnChanged = nameof(OnPropellerSpinTimerChanged))] public TickTimer PropellerSpinTimer { get; set; }
        [Networked] public NetworkBool UsedPropellerThisJump { get; set; }
        [Networked] public TickTimer GiantStartTimer { get; set; }
        [Networked(OnChanged = nameof(OnGiantTimerChanged))] public TickTimer GiantTimer { get; set; }
        [Networked] public TickTimer GiantEndTimer { get; set; }
        [Networked(OnChanged = nameof(OnIsStationaryGiantShrinkChanged))] private bool IsStationaryGiantShrink { get; set; }
        [Networked] public NetworkBool IsInShell { get; set; }
        [Networked] public FrozenCube FrozenCube { get; set; }

        //---Properties
        public override bool IsFlying => IsSpinnerFlying || IsPropellerFlying; //doesn't work consistently?
        public override bool IsCarryable => true;
        public bool WallSliding => WallSlideLeft || WallSlideRight;
        public bool InstakillsEnemies => IsStarmanInvincible || IsInShell || (IsSliding && Mathf.Abs(body.velocity.x) > 0.1f) || State == Enums.PowerupState.MegaMushroom;
        public bool IsCrouchedInShell => State == Enums.PowerupState.BlueShell && IsCrouching && !IsInShell;
        public bool IsStarmanInvincible => StarmanTimer.IsActive(Runner);
        public bool IsDamageable => !IsStarmanInvincible && DamageInvincibilityTimer.ExpiredOrNotRunning(Runner);
        public int PlayerId => data.PlayerId;
        public bool CanPickupItem => !FrozenCube && State != Enums.PowerupState.MiniMushroom && !IsSkidding && !IsTurnaround && !HeldEntity && PreviousInputs.buttons.IsSet(PlayerControls.Sprint) && !IsPropellerFlying && !IsSpinnerFlying && !IsCrouching && !IsDead && !WallSlideLeft && !WallSlideRight && JumpState < PlayerJumpState.DoubleJump && !IsGroundpounding && !(!HeldEntity && IsSwimming && PreviousInputs.buttons.IsSet(PlayerControls.Jump));
        public bool HasGroundpoundHitbox => (IsDrilling || (IsGroundpounding && GroundpoundStartTimer.ExpiredOrNotRunning(Runner))) && !IsOnGround;
        public float RunningMaxSpeed => SPEED_STAGE_MAX[RUN_STAGE];
        public float WalkingMaxSpeed => SPEED_STAGE_MAX[WALK_STAGE];
        public BoxCollider2D MainHitbox => hitboxes[0];
        public Vector2 WorldHitboxSize => MainHitbox.size * transform.lossyScale;
        public Vector3 Spawnpoint => GameData.Instance.GetSpawnpoint(SpawnpointIndex);
        private int MovementStage {
            get {
                float xVel = Mathf.Abs(body.velocity.x);
                float[] arr;
                if (IsSwimming) {
                    if (IsOnGround) {
                        if (State == Enums.PowerupState.BlueShell) {
                            arr = SWIM_WALK_SHELL_STAGE_MAX;
                        } else {
                            arr = SWIM_WALK_STAGE_MAX;
                        }
                    } else {
                        arr = SWIM_STAGE_MAX;
                    }
                } else if ((IsSpinnerFlying || IsPropellerFlying) && State != Enums.PowerupState.MegaMushroom) {
                    arr = SPEED_STAGE_SPINNER_MAX;
                } else {
                    arr = SPEED_STAGE_MAX;
                }

                for (int i = 0; i < arr.Length; i++) {
                    if (xVel <= arr[i])
                        return i;
                }
                return arr.Length - 1;
            }
        }
        private int GravityStage {
            get {
                float yVel = body.velocity.y;
                float?[] arr = IsSwimming ? GRAVITY_SWIM_MAX : (State == Enums.PowerupState.MegaMushroom ? GRAVITY_MEGA_MAX : (State == Enums.PowerupState.MiniMushroom ? GRAVITY_MINI_MAX : GRAVITY_STAGE_MAX));
                for (int i = 1; i < arr.Length; i++) {
                    if (yVel >= arr[i])
                        return i - 1;
                }
                return arr.Length - 1;
            }
        }
        public byte StarCombo {
            get => IsStarmanInvincible ? _StarCombo : (byte) 0;
            set => _StarCombo = IsStarmanInvincible ? value : (byte) 0;
        }

        //---Components
        private BoxCollider2D[] hitboxes;
        public FadeOutManager fadeOut;
        public AudioSource sfxBrick;
        private Animator animator;
        public NetworkRigidbody2D networkRigidbody;
        public CameraController cameraController;
        public PlayerAnimationController animationController;



        [SerializeField] public float flyingGravity = 0.8f, flyingTerminalVelocity = 1.25f, drillVelocity = 7f, groundpoundTime = 0.25f, groundpoundVelocity = 10, blinkingSpeed = 0.25f, terminalVelocity = -7f, launchVelocity = 12f, wallslideSpeed = -4.25f, giantStartTime = 1.5f, soundRange = 10f, slopeSlidingAngle = 12.5f, pickupTime = 0.5f;
        [SerializeField] public float propellerLaunchVelocity = 6, propellerFallSpeed = 2, propellerSpinFallSpeed = 1.5f, propellerSpinTime = 0.75f, propellerDrillBuffer, heightSmallModel = 0.42f, heightLargeModel = 0.82f;
        [SerializeField] public GameObject models;
        [SerializeField] public CharacterData character;

        public bool crushGround, hitRoof, groundpoundLastFrame, hitLeft, hitRight;
        public float powerupFlash;

        #region // MOVEMENT STAGES & CONSTANTS
        private static readonly int WALK_STAGE = 1, RUN_STAGE = 3, STAR_STAGE = 4;
        private static readonly float[] SPEED_STAGE_MAX = { 0.9375f, 2.8125f, 4.21875f, 5.625f, 8.4375f };
        private static readonly float SPEED_SLIDE_MAX = 7.5f;
        private static readonly float[] SPEED_STAGE_ACC = { 7.91015625f, 3.955081725f, 3.515625f, 2.63671875f, 84.375f };
        private static readonly float[] ICE_STAGE_ACC = { 1.9775390625f, 3.955081725f, 3.515625f, 2.63671875f, 84.375f };
        private static readonly float[] WALK_TURNAROUND_ACC = { 3.955078125f, 8.7890625f, 8.7890625f, 21.093756f };
        private static readonly float BUTTON_RELEASE_DEC = 3.9550781196f;
        private static readonly float SKIDDING_THRESHOLD = 4.6875f;
        private static readonly float SKIDDING_DEC = 10.54687536f;
        private static readonly float SKIDDING_STAR_DEC = SPEED_STAGE_ACC[^1];

        private static readonly float WALLJUMP_HSPEED = 4.21875f;
        private static readonly float WALLJUMP_VSPEED = 6.4453125f;
        private static readonly float WALLJUMP_MINI_VSPEED = 5.1708984375f;

        private static readonly float KNOCKBACK_DEC = 7.9101585f;

        private static readonly float[] SPEED_STAGE_SPINNER_MAX = { 1.12060546875f, 2.8125f };
        private static readonly float[] SPEED_STAGE_SPINNER_ACC = { 7.91015625f, 3.955078125f };

        private static readonly float[] SPEED_STAGE_MEGA_ACC = { 28.125f, 4.83398433f, 4.83398433f, 4.83398433f, 4.83398433f };
        private static readonly float[] WALK_TURNAROUND_MEGA_ACC = { 4.614257808f, 10.546875f, 21.09375f, 21.09375f };

        private static readonly float TURNAROUND_ACC = 28.125f;

        private static readonly float[] BUTTON_RELEASE_ICE_DEC = { 0.439453125f, 1.483154296875f, 1.483154296875f, 1.483154296875f, 1.483154296875f };
        private static readonly float SKIDDING_ICE_DEC = 3.955078125f;
        private static readonly float WALK_TURNAROUND_ICE_ACC = 2.63671875f;

        private static readonly float SLIDING_45_ACC = 13.1835975f;
        private static readonly float SLIDING_22_ACC = 5.2734375f;

        private static readonly float SWIM_VSPEED = 2.26318359375f;
        private static readonly float SWIM_MAX_VSPEED = 4.833984375f;
        private static readonly float SWIM_TERMINAL_VELOCITY_AHELD = -0.9375f;
        private static readonly float SWIM_TERMINAL_VELOCITY = -2.8125f;
        private static readonly float SWIM_BUTTON_RELEASE_DEC = 1.7578125f;

        private static readonly float[] SWIM_STAGE_MAX = { 0f, 2.109375f };
        private static readonly float[] SWIM_STAGE_ACC = { 1.7578125f, 3.076171875f, 0.439453125f };
        private static readonly float[] SWIM_SHELL_STAGE_MAX = { 3.1640625f };
        private static readonly float[] SWIM_SHELL_STAGE_ACC = { 6.15234375f, 6.15234375f };

        private static readonly float[] SWIM_WALK_STAGE_MAX = { 1.0546875f, 1.0546875f };
        private static readonly float[] SWIM_WALK_STAGE_ACC = { 3.07617875f, 1.7578125f };
        private static readonly float[] SWIM_WALK_SHELL_STAGE_MAX = { 1.58203125f, 1.58203125f };
        private static readonly float[] SWIM_WALK_SHELL_STAGE_ACC = { 6.15234375f, 6.15234375f };
        private static readonly float SWIM_GROUNDPOUND_DEC = 38.671875f;

        private static readonly float?[] GRAVITY_STAGE_MAX = { null, 4.16015625f, 2.109375f, 0f, -5.859375f };
        private static readonly float[] GRAVITY_STAGE_ACC = { -7.03125f, -28.125f, -38.671875f, -28.125f, -38.671875f };
        private static readonly float?[] GRAVITY_MINI_MAX = { null, 4.566650390625f, 2.633056640625f, 0f, -3.929443359375f };
        private static readonly float[] GRAVITY_MINI_ACC = { -4.833984375f, -7.03125f, -10.546875f, -7.03125f, -10.546875f };
        private static readonly float?[] GRAVITY_MEGA_MAX = { null, 4.04296875f, };
        private static readonly float[] GRAVITY_MEGA_ACC = { -28.125f, -38.671875f };
        private static readonly float?[] GRAVITY_SWIM_MAX = { null, 0f };
        private static readonly float[] GRAVITY_SWIM_ACC = { -4.833984375f, -3.076171875f };
        #endregion

        // Footstep Variables
        private Enums.Sounds footstepSound = Enums.Sounds.Player_Walk_Grass;
        private Enums.Particle footstepParticle = Enums.Particle.None;
        private bool footstepVariant;

        // Tile data
        private readonly List<Vector2Int> tilesStandingOn = new();
        private readonly List<Vector2Int> tilesJumpedInto = new();
        private readonly List<Vector2Int> tilesHitSide = new();

        // Previous Tick Variables
        private bool previousTickIsOnGround;
        public Vector2 previousTickVelocity, previousTickPosition;

        // Misc
        private TrackIcon icon;
        public PlayerData data;

        #endregion

        #region Unity Methods
        public void Awake() {
            cameraController = GetComponentInChildren<CameraController>();
            animator = GetComponentInChildren<Animator>();
            sfxBrick = GetComponents<AudioSource>()[1];
            animationController = GetComponent<PlayerAnimationController>();
            networkRigidbody = GetComponent<NetworkRigidbody2D>();
        }

        public override void Start() {
            fadeOut = GameObject.FindGameObjectWithTag("FadeUI").GetComponent<FadeOutManager>();
        }

        public void OnEnable() {
            NetworkHandler.OnInputMissing += OnInputMissing;
        }

        public void OnDisable() {
            ControlSystem.controls.Player.ReserveItem.performed -= OnReserveItem;
            NetworkHandler.OnInput -= OnInput;
            NetworkHandler.OnInputMissing -= OnInputMissing;
        }

        public void OnBeforeSpawned(int spawnpoint) {
            SpawnpointIndex = (sbyte) spawnpoint;
        }

        public void BeforeTick() {
            previousTickPosition = body.position;
            previousTickVelocity = body.velocity;
            previousTickIsOnGround = IsOnGround;
        }

        public override void Spawned() {
            base.Spawned();

            hitboxes = GetComponentsInChildren<BoxCollider2D>();
            icon = UIUpdater.Instance.CreateTrackIcon(this);
            transform.position = body.position = GameManager.Instance.spawnpoint;

            body.isKinematic = true;
            MainHitbox.isTrigger = false;

            data = Object.InputAuthority.GetPlayerData(Runner);
            if (Object.HasInputAuthority) {
                networkRigidbody.InterpolationDataSource = InterpolationDataSources.Predicted;
                networkRigidbody.InterpolateErrorCorrection = false;

                GameManager.Instance.localPlayer = this;
                GameManager.Instance.spectationManager.Spectating = false;
                ControlSystem.controls.Player.ReserveItem.performed += OnReserveItem;
                NetworkHandler.OnInput += OnInput;
            }
            /* else if (IsProxy) {
                networkRigidbody.InterpolationDataSource = InterpolationDataSources.Snapshots;
            }*/

            Lives = SessionData.Instance.Lives;

            //use |= as the spectate manager sets it first
            cameraController.IsControllingCamera = Object.HasInputAuthority;

            Vector3 spawnpoint = GameData.Instance.GetSpawnpoint(0, 1);
            networkRigidbody.TeleportToPosition(spawnpoint);
            cameraController.Recenter(spawnpoint);

            if (!GameData.Instance.AlivePlayers.Contains(this)) {
                GameData.Instance.AlivePlayers.Add(this);
            }
            GameManager.Instance.teamManager.AddPlayer(this);

            ControlSystem.controls.Enable();
        }

        public override void Despawned(NetworkRunner runner, bool hasState) {
            NetworkHandler.OnInput -= OnInput;
            NetworkHandler.OnInputMissing -= OnInputMissing;

            if (GameData.Instance && hasState)
                GameData.Instance.AlivePlayers.Remove(this);

            if (icon)
                Destroy(icon.gameObject);
        }

        public void OnInput(NetworkRunner runner, NetworkInput input) {
            PlayerNetworkInput newInput = new();

            //input nothing when paused
            if (GameManager.Instance.paused) {
                input.Set(newInput);
                return;
            }

            Vector2 joystick = ControlSystem.controls.Player.Movement.ReadValue<Vector2>();
            bool jump = ControlSystem.controls.Player.Jump.ReadValue<float>() >= 0.5f;
            bool powerup = ControlSystem.controls.Player.PowerupAction.ReadValue<float>() >= 0.5f;
            bool sprint = ControlSystem.controls.Player.Sprint.ReadValue<float>() >= 0.5f;

            //TODO: changeable deadzone?
            newInput.buttons.Set(PlayerControls.Up, joystick.y > 0.25f);
            newInput.buttons.Set(PlayerControls.Down, joystick.y < -0.25f);
            newInput.buttons.Set(PlayerControls.Left, joystick.x < -0.25f);
            newInput.buttons.Set(PlayerControls.Right, joystick.x > 0.25f);
            newInput.buttons.Set(PlayerControls.Jump, jump);
            newInput.buttons.Set(PlayerControls.PowerupAction, powerup);
            newInput.buttons.Set(PlayerControls.Sprint, sprint || Settings.Instance.controlsAutoSprint);
            newInput.buttons.Set(PlayerControls.SprintPowerupAction, sprint && Settings.Instance.controlsFireballSprint);

            input.Set(newInput);
        }

        public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) {
            if (Object.InputAuthority != player)
                return;

            // When we drop inputs, continue predicting the previous set of inputs.
            input.Set(PreviousInputs);
        }

        public override void Render() {
            HandleLayerState();
        }

        public override void FixedUpdateNetwork() {
            if (GameData.Instance.GameState < Enums.GameState.Playing) {
                models.SetActive(false);
                return;
            }

            if (GameData.Instance.GameEnded) {
                //game ended, freeze.
                body.velocity = Vector2.zero;
                animator.enabled = false;
                body.isKinematic = true;
                return;
            }

            SpinnerLaunchAnimCounter = false;

            if (IsDead) {
                HandleRespawnTimers();
            } else if (!IsFrozen) {

                // If we can't get inputs from the player, just go based on their previous networked input state.
                PlayerNetworkInput input;
                if (GetInput(out PlayerNetworkInput currentInputs)) {
                    input = currentInputs;
                } else {
                    input = PreviousInputs;
                }

                NetworkButtons heldButtons = input.buttons;
                NetworkButtons pressedButtons = input.buttons.GetPressed(PreviousInputs.buttons);

                // TODO: remove groundpoundLastFrame? Do we even need this anymore?
                groundpoundLastFrame = IsGroundpounding;

                //HandleBlockSnapping();
                CheckForEntityCollision();

                HandleGroundCollision();
                if (IsOnGround)
                    IgnoreCoyoteTime = false;

                if (previousTickIsOnGround) {
                    if (!IsOnGround) {
                        IsOnGround = GroundSnapCheck();
                    }

                    if (!IsOnGround) {
                        if (!IgnoreCoyoteTime)
                            CoyoteTime = Runner.SimulationTime + 0.05f;

                        IgnoreCoyoteTime = false;
                    }
                }

                UpdateTileProperties();
                CheckForPowerupActions(pressedButtons);
                HandleMovement(heldButtons, pressedButtons);

                PreviousInputs = input;
            }

            animationController.HandleDeathAnimation();
            animationController.HandlePipeAnimation();

            UpdateHitbox();

            // We can become stuck in a block after uncrouching
            if (!IsDead)
                HandleStuckInBlock();
        }
        #endregion

        private void HandleRespawnTimers() {
            if (PreRespawnTimer.Expired(Runner)) {
                PreRespawn();
                PreRespawnTimer = TickTimer.None;
            }

            if (RespawnTimer.Expired(Runner)) {
                Respawn();
                RespawnTimer = TickTimer.None;
            }
        }

        private void CheckForPowerupActions(NetworkButtons pressedButtons) {
            //powerup action button check
            bool checkSprintButton = State == Enums.PowerupState.FireFlower || State == Enums.PowerupState.IceFlower;
            if (pressedButtons.IsSet(PlayerControls.PowerupAction)
                || (pressedButtons.IsSet(PlayerControls.SprintPowerupAction) && checkSprintButton)) {

                ActivatePowerupAction();
            }

            if (Settings.Instance.controlsPropellerJump && pressedButtons.IsSet(PlayerControls.Jump) && !IsOnGround) {
                StartPropeller();
            }
        }

        #region -- COLLISIONS --
        private void HandleGroundCollision() {
            tilesJumpedInto.Clear();
            tilesStandingOn.Clear();
            tilesHitSide.Clear();
            crushGround = false;
            OnSpinner = null;

            if (IsStuckInBlock)
                return;

            int down = 0, left = 0, right = 0, up = 0;

            foreach (BoxCollider2D hitbox in hitboxes) {
                int collisionCount = hitbox.GetContacts(TileContactBuffer);

                for (int i = 0; i < collisionCount; i++) {
                    ContactPoint2D contact = TileContactBuffer[i];
                    GameObject go = contact.collider.gameObject;
                    Vector2 n = contact.normal;
                    Vector2 p = contact.point + (contact.normal * -0.15f);
                    if (n == Vector2.up && contact.point.y - 0.02f > body.position.y)
                        continue;

                    Vector2Int vec = Utils.Utils.WorldToTilemapPosition(p);
                    if (!contact.collider || contact.collider.CompareTag("Player"))
                        continue;

                    if (Vector2.Dot(n, Vector2.up) > .05f) {
                        if (Vector2.Dot(body.velocity.normalized, n) > 0.1f && !IsOnGround) {
                            if (!contact.rigidbody || contact.rigidbody.velocity.y < body.velocity.y)
                                //invalid flooring
                                continue;
                        }

                        crushGround |= !go.CompareTag("platform") && !go.CompareTag("frozencube");
                        down++;
                        if (go.CompareTag("spinner")) {
                            OnSpinner = go.GetComponentInParent<SpinnerAnimator>();
                            OnSpinner.HasPlayer = true;
                        }

                        if (!tilesStandingOn.Contains(vec))
                            tilesStandingOn.Add(vec);

                    } else if (((1 << contact.collider.gameObject.layer) & Layers.MaskSolidGround) != 0) {
                        if (Vector2.Dot(n, Vector2.down) > .9f) {
                            up++;
                            if (!tilesJumpedInto.Contains(vec))
                                tilesJumpedInto.Add(vec);
                        } else {
                            if (n.x < 0) {
                                right++;
                            } else {
                                left++;
                            }
                            if (!tilesHitSide.Contains(vec))
                                tilesHitSide.Add(vec);
                        }
                    }
                }
            }

            IsOnGround = down >= 1 && PropellerLaunchTimer.ExpiredOrNotRunning(Runner);
            hitLeft = left >= 1;
            hitRight = right >= 1;
            hitRoof = up >= 1 && body.velocity.y > -0.1f;

            crushGround &= IsOnGround;
        }

        private void UpdateTileProperties() {
            OnIce = false;
            footstepSound = Enums.Sounds.Player_Walk_Grass;
            footstepParticle = Enums.Particle.None;
            foreach (Vector2Int pos in tilesStandingOn) {
                if (GameManager.Instance.tileManager.GetTile(pos, out TileWithProperties propTile)) {
                    footstepSound = propTile.footstepSound;
                    footstepParticle = propTile.footstepParticle;
                    OnIce = propTile.iceSkidding;
                    break;
                }
            }
        }

        private void CheckForEntityCollision() {
            // Don't check for collisions if we're dead, frozen, in a pipe, etc.
            if (IsDead || IsFrozen || CurrentPipe)
                return;

            if (!CollisionFilter.useLayerMask)
                CollisionFilter.SetLayerMask((int) (((uint) (1 << Layers.LayerGround)) ^ 0xFFFFFFFF));

            int collisions = 0;
            foreach (BoxCollider2D hitbox in hitboxes) {
                int count = Runner.GetPhysicsScene2D().OverlapBox(body.position + (body.velocity * Runner.DeltaTime) + hitbox.offset * transform.localScale, hitbox.size * transform.localScale, 0, CollisionFilter, TempCollisionBuffer);
                Array.Copy(TempCollisionBuffer, 0, CollisionBuffer, collisions, count);
                collisions += count;
            }

            for (int i = 0; i < collisions; i++) {
                GameObject collidedObject = CollisionBuffer[i].gameObject;

                // Don't interact with ourselves.
                if (CollisionBuffer[i].attachedRigidbody == body)
                    continue;

                // Or objects we're holding.
                if (HeldEntity && HeldEntity.gameObject == collidedObject)
                    continue;

                // Or our own frozen cube
                if (FrozenCube && FrozenCube.gameObject == collidedObject)
                    continue;

                if (collidedObject.GetComponentInParent<IPlayerInteractable>() is IPlayerInteractable interactable) {
                    // Or frozen entities.
                    if (interactable is FreezableEntity freezable && freezable.IsFrozen)
                        continue;

                    // Or dead entities.
                    if (interactable is KillableEntity killable && killable.IsDead)
                        continue;

                    // And don't predict the collection of stars / coins.
                    if (interactable is CollectableEntity && IsProxy)
                        continue;

                    interactable.InteractWithPlayer(this);
                }
            }
        }

        public void InteractWithPlayer(PlayerController other) {

            if (DamageInvincibilityTimer.IsActive(Runner) || other.DamageInvincibilityTimer.IsActive(Runner))
                return;

            if (GiantStartTimer.IsActive(Runner) || other.GiantStartTimer.IsActive(Runner))
                return;

            // Hit players
            bool dropStars = data.Team != other.data.Team;

            Utils.Utils.UnwrapLocations(body.position, other.body.position, out Vector2 ours, out Vector2 theirs);
            bool fromRight = ours.x < theirs.x;

            float dot = Vector2.Dot((ours - theirs).normalized, Vector2.up);
            bool above = dot > 0.7f;
            bool otherAbove = dot < -0.7f;

            if (other.IsStarmanInvincible) {
                // They are invincible. let them decide if they've hit us.
                if (IsStarmanInvincible) {
                    // Oh, we both are. bonk.
                    DoKnockback(fromRight, dropStars ? 1 : 0, true, other.Object);
                    other.DoKnockback(!fromRight, dropStars ? 1 : 0, true, Object);
                }
                return;
            }

            if (IsStarmanInvincible) {
                // We are invincible. murder time :)
                if (other.State == Enums.PowerupState.MegaMushroom) {
                    // Wait fuck-
                    DoKnockback(fromRight, dropStars ? 1 : 0, true, other.Object);
                    return;
                }

                if (dropStars) {
                    other.Powerdown(false);
                } else {
                    other.DoKnockback(!fromRight, 0, true, Object);
                }
                return;
            }

            // Mega mushroom cases
            if (State == Enums.PowerupState.MegaMushroom || other.State == Enums.PowerupState.MegaMushroom) {
                if (State == Enums.PowerupState.MegaMushroom && other.State == Enums.PowerupState.MegaMushroom) {
                    // Both giant
                    if (above) {
                        DoEntityBounce = true;
                        IsGroundpounding = false;
                        IsDrilling = false;
                    } else if (!otherAbove) {
                        DoKnockback(fromRight, 0, true, other.Object);
                        other.DoKnockback(!fromRight, 0, true, Object);
                    }
                } else if (State == Enums.PowerupState.MegaMushroom) {
                    // Only we are giant
                    if (dropStars) {
                        other.Powerdown(false);
                    } else {
                        other.DoKnockback(!fromRight, 0, true, Object);
                    }
                }
                return;
            }

            // Blue shell cases
            if (IsInShell) {
                // We are blue shell
                if (!otherAbove) {
                    // Hit them, powerdown them
                    if (other.IsInShell) {
                        // Collide with both
                        DoKnockback(fromRight, dropStars ? 1 : 0, true, other.Object);
                        other.DoKnockback(!fromRight, dropStars ? 1 : 0, true, Object);
                    } else {
                        if (dropStars) {
                            other.Powerdown(false);
                        } else {
                            other.DoKnockback(!fromRight, 0, true, Object);
                        }
                    }
                    float dotRight = Vector2.Dot((body.position - other.body.position).normalized, Vector2.right);
                    FacingRight = dotRight > 0;
                    return;
                }
            }
            if (State == Enums.PowerupState.BlueShell && otherAbove && !other.IsGroundpounding && !other.IsDrilling && (IsCrouching || IsGroundpounding) && IsOnGround)
                body.velocity = new(SPEED_STAGE_MAX[RUN_STAGE] * 0.9f * (other.body.position.x < body.position.x ? 1 : -1), body.velocity.y);

            if (other.IsInShell && !above)
                return;

            if (!above && other.State == Enums.PowerupState.BlueShell && !other.IsInShell && other.IsCrouching && !IsGroundpounding && !IsDrilling) {
                // They are blue shell
                DoEntityBounce = true;
                return;
            }


            if (other.IsDamageable && above/* && (body.velocity.y < 0.1f || other.IsInShell)*/) {
                // Hit them from above
                DoEntityBounce = !IsGroundpounding && !IsDrilling;
                bool groundpounded = HasGroundpoundHitbox || IsDrilling;

                if (State == Enums.PowerupState.MiniMushroom && other.State != Enums.PowerupState.MiniMushroom) {
                    // We are mini, they arent. special rules.
                    if (groundpounded) {
                        other.DoKnockback(!fromRight, dropStars ? 3 : 0, false, Object);
                        IsGroundpounding = false;
                        DoEntityBounce = true;
                    }
                } else if (other.State == Enums.PowerupState.MiniMushroom && groundpounded) {
                    // We are big, groundpounding a mini opponent. squish.
                    other.DoKnockback(fromRight, dropStars ? 3 : 0, false, Object);
                    DoEntityBounce = false;
                } else {
                    if (other.State == Enums.PowerupState.MiniMushroom && groundpounded) {
                        other.Powerdown(false);
                    } else {
                        other.DoKnockback(!fromRight, dropStars ? (groundpounded ? 3 : 1) : 0, false, Object);
                    }
                }
                return;
            } else if (!IsInKnockback && !other.IsInKnockback && !otherAbove) {
                if (State == Enums.PowerupState.MiniMushroom || other.State == Enums.PowerupState.MiniMushroom) {

                    if (State == Enums.PowerupState.MiniMushroom)
                        DoKnockback(fromRight, dropStars ? 1 : 0, false, other.Object);

                    if (other.State == Enums.PowerupState.MiniMushroom)
                        other.DoKnockback(!fromRight, dropStars ? 1 : 0, false, Object);

                } else if (Mathf.Abs(previousTickVelocity.x) > WalkingMaxSpeed || Mathf.Abs(other.previousTickVelocity.x) > WalkingMaxSpeed) {
                    // Bump
                    if (IsOnGround)
                        DoKnockback(fromRight, dropStars ? 1 : 0, true, other.Object);
                    else
                        AirBonk(fromRight);

                    if (other.IsOnGround)
                        other.DoKnockback(!fromRight, dropStars ? 1 : 0, true, Object);
                    else
                        other.AirBonk(!fromRight);

                } else {
                    // Collide
                    int directionToOtherPlayer = fromRight ? -1 : 1;
                    float overlap = ((WorldHitboxSize.x * 0.5f) + (other.WorldHitboxSize.x * 0.5f) - Mathf.Abs(ours.x - theirs.x)) * 0.5f + 0.05f;

                    if (overlap > 0.02f) {
                        Vector2 ourNewPosition = new(body.position.x + (overlap * directionToOtherPlayer), body.position.y);
                        Vector2 theirNewPosition = new(other.body.position.x + (overlap * -directionToOtherPlayer), other.body.position.y);

                        int hits = 0;
                        RaycastHit2D hit;
                        if (hit = Runner.GetPhysicsScene2D().BoxCast(ourNewPosition + (WorldHitboxSize * Vector2.up * 0.55f), WorldHitboxSize, 0, Vector2.zero, Physics2D.defaultContactOffset, Layers.MaskSolidGround)) {
                            ourNewPosition.x = hit.point.x + hit.normal.x * (WorldHitboxSize.x * 0.5f + Physics2D.defaultContactOffset);
                            theirNewPosition.x = ourNewPosition.x + hit.normal.x * ((WorldHitboxSize.x + other.WorldHitboxSize.x) * 0.5f + Physics2D.defaultContactOffset);
                            hits++;
                        }
                        if (hit = Runner.GetPhysicsScene2D().BoxCast(theirNewPosition + (other.WorldHitboxSize * Vector2.up * 0.55f), other.WorldHitboxSize, 0, Vector2.zero, Physics2D.defaultContactOffset, Layers.MaskSolidGround)) {
                            theirNewPosition.x = hit.point.x + hit.normal.x * (other.WorldHitboxSize.x * 0.5f + Physics2D.defaultContactOffset);
                            ourNewPosition.x = theirNewPosition.x + hit.normal.x * ((WorldHitboxSize.x + other.WorldHitboxSize.x) * 0.5f + Physics2D.defaultContactOffset);
                            hits++;
                        }

                        if (hits < 2) {
                            body.position = ourNewPosition;
                            other.body.position = theirNewPosition;

                            float avgVel = (body.velocity.x + other.body.velocity.x) * 0.5f;
                            body.velocity = new(avgVel, body.velocity.y);
                            other.body.velocity = new(avgVel, other.body.velocity.y);
                        }
                    }
                }
            }
        }
        #endregion

        #region -- CONTROLLER FUNCTIONS --
        private void ActivatePowerupAction() {
            if (IsDead || IsFrozen || IsInKnockback || CurrentPipe || GameData.Instance.GameEnded || HeldEntity)
                return;

            switch (State) {
            case Enums.PowerupState.IceFlower:
            case Enums.PowerupState.FireFlower: {
                if (WallSliding || IsGroundpounding || JumpState == PlayerJumpState.TripleJump || IsSpinnerFlying || IsDrilling || IsCrouching || IsSliding)
                    return;

                if (FireballDelayTimer.IsActive(Runner))
                    return;

                int activeFireballs = GameData.Instance.PooledFireballs.Count(fm => fm.Owner == this && fm.IsActive);
                if (activeFireballs <= 1) {
                    FireballShootTimer = TickTimer.CreateFromSeconds(Runner, 1.25f);
                    CanShootAdditionalFireball = activeFireballs == 0;
                } else if (FireballShootTimer.ExpiredOrNotRunning(Runner)) {
                    FireballShootTimer = TickTimer.CreateFromSeconds(Runner, 1.25f);
                    CanShootAdditionalFireball = true;
                } else if (CanShootAdditionalFireball) {
                    CanShootAdditionalFireball = false;
                } else {
                    return;
                }

                bool ice = State == Enums.PowerupState.IceFlower;
                bool right = FacingRight ^ animator.GetCurrentAnimatorStateInfo(0).IsName("turnaround");
                Vector2 spawnPos = body.position + new Vector2(right ? 0.5f : -0.5f, 0.3f);

                FireballMover inactiveFireball = GameData.Instance.PooledFireballs.First(fm => !fm.IsActive);
                inactiveFireball.Initialize(this, spawnPos, ice, right);

                FireballDelayTimer = TickTimer.CreateFromSeconds(Runner, 0.1f);
                FireballAnimCounter++;

                // Weird interaction in the main game...
                WallJumpTimer = TickTimer.None;
                break;
            }
            case Enums.PowerupState.PropellerMushroom: {
                StartPropeller();
                break;
            }
            }
        }

        private void StartPropeller() {
            if (State != Enums.PowerupState.PropellerMushroom || IsGroundpounding || (IsSpinnerFlying && IsDrilling) || IsPropellerFlying || IsCrouching || IsSliding || WallJumpTimer.IsActive(Runner))
                return;

            if (UsedPropellerThisJump)
                return;

            PropellerLaunchTimer = TickTimer.CreateFromSeconds(Runner, 1f);

            IsPropellerFlying = true;
            IsSpinnerFlying = false;
            IsCrouching = false;
            JumpState = PlayerJumpState.None;

            WallSlideLeft = false;
            WallSlideRight = false;

            if (IsOnGround) {
                IsOnGround = false;
                body.position += Vector2.up * 0.05f;
            }
            UsedPropellerThisJump = true;
        }

        public void OnReserveItem(InputAction.CallbackContext context) {
            if (GameManager.Instance.paused || GameData.Instance.GameEnded)
                return;

            if (StoredPowerup == Enums.PowerupState.NoPowerup || IsDead) {
                PlaySound(Enums.Sounds.UI_Error);
                return;
            }

            RPC_SpawnReserveItem();
        }
        #endregion

        #region -- POWERUP / POWERDOWN --

        public bool Powerdown(bool ignoreInvincible) {
            if (!ignoreInvincible && !IsDamageable)
                return false;

            if (IsProxy)
                return false;

            PreviousState = State;

            switch (State) {
            case Enums.PowerupState.MiniMushroom:
            case Enums.PowerupState.NoPowerup: {
                Death(false, false);
                break;
            }
            case Enums.PowerupState.Mushroom: {
                State = Enums.PowerupState.NoPowerup;
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
            IsPropellerFlying = false;
            IsInShell = false;
            PropellerLaunchTimer = TickTimer.None;
            PropellerSpinTimer = TickTimer.None;
            UsedPropellerThisJump = false;

            if (!IsDead) {
                DamageInvincibilityTimer = TickTimer.CreateFromSeconds(Runner, 2f);
            }
            return true;
        }
        #endregion

        #region -- FREEZING --
        public override void Freeze(FrozenCube cube) {
            if (!cube || IsInKnockback || !IsDamageable || IsFrozen || State == Enums.PowerupState.MegaMushroom)
                return;

            IsFrozen = true;
            FrozenCube = cube;
            FrozenCube.AutoBreakTimer = TickTimer.CreateFromSeconds(Runner, 1.75f);
            animator.enabled = false;
            body.isKinematic = true;
            IsInKnockback = false;
            IsSkidding = false;
            IsDrilling = false;
            WallSlideLeft = false;
            WallSlideRight = false;
            IsPropellerFlying = false;

            AttemptThrowHeldItem();

            PropellerLaunchTimer = TickTimer.None;
            IsSkidding = false;
        }

        public override void Unfreeze(UnfreezeReason reason) {
            if (!IsFrozen)
                return;

            IsFrozen = false;
            animator.enabled = true;
            body.isKinematic = false;

            int knockbackStars = reason switch {
                UnfreezeReason.Timer => 0,
                UnfreezeReason.Groundpounded => 2,
                _ => 1
            };

            if (FrozenCube) {
                if (FrozenCube.Holder)
                    FrozenCube.Holder.DoKnockback(FrozenCube.Holder.FacingRight, 1, true, Object);
            }

            if (knockbackStars > 0) {
                DoKnockback(FacingRight, knockbackStars, true, null);
            } else {
                DamageInvincibilityTimer = TickTimer.CreateFromSeconds(Runner, 1f);
            }
        }
        #endregion

        public override void BlockBump(BasicEntity bumper, Vector2Int tile, InteractableTile.InteractionDirection direction) {
            if (IsInKnockback)
                return;

            Utils.Utils.UnwrapLocations(body.position, bumper.body ? bumper.body.position : bumper.transform.position, out Vector2 ourPos, out Vector2 theirPos);
            bool onRight = ourPos.x > theirPos.x;

            DoKnockback(!onRight, 1, false, Object);
        }

        #region -- COIN / STAR COLLECTION --
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        public void Rpc_SpawnCoinEffects(Vector3 position, byte coins, bool final) {
            PlaySound(Enums.Sounds.World_Coin_Collect);
            NumberParticle num = Instantiate(PrefabList.Instance.Particle_CoinNumber, position, Quaternion.identity).GetComponentInChildren<NumberParticle>();
            num.ApplyColorAndText(Utils.Utils.GetSymbolString(coins.ToString(), Utils.Utils.numberSymbols), animationController.GlowColor, final);
        }

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        public void RPC_SpawnReserveItem() {
            if (StoredPowerup == Enums.PowerupState.NoPowerup)
                return;

            SpawnItem(StoredPowerup.GetPowerupScriptable().prefab);
            StoredPowerup = Enums.PowerupState.NoPowerup;
        }

        public void SetReserveItem(Enums.PowerupState newItem) {
            Powerup currentReserve = StoredPowerup.GetPowerupScriptable();
            if (!currentReserve) {
                // We don't have a reserve item, so we can just set it
                StoredPowerup = newItem;
                return;
            }

            Powerup newReserve = newItem.GetPowerupScriptable();
            if (!newReserve) {
                // Not a valid powerup, so just clear our reserve item instead
                StoredPowerup = Enums.PowerupState.NoPowerup;
                return;
            }

            sbyte newStatePriority = newReserve ? newReserve.statePriority : (sbyte) -1;
            sbyte currentStatePriority = currentReserve ? currentReserve.statePriority : (sbyte) -1;

            if (newStatePriority < currentStatePriority) {
                // New item is less important than our current reserve item, so we don't want to replace it
                return;
            }

            // Replace our current reserve item with the new one
            StoredPowerup = newItem;
        }

        public void SpawnItem(NetworkPrefabRef prefab) {
            if (prefab == NetworkPrefabRef.Empty)
                prefab = Utils.Utils.GetRandomItem(this).prefab;

            Runner.Spawn(prefab, new(body.position.x, cameraController.currentPosition.y + 1.68f, 0), onBeforeSpawned: (runner, obj) => {
                obj.GetComponent<MovingPowerup>().OnBeforeSpawned(this);
            });
        }

        private void SpawnStars(int amount, bool deathplane) {

            GameManager gm = GameManager.Instance;
            bool fastStars = amount > 2 && Stars > 2;
            int starDirection = FacingRight ? 1 : 2;

            // If the level doesn't loop, don't have stars go towards the edges of the map
            if (!gm.loopingLevel) {
                if (body.position.x > gm.LevelMaxX - 2.5f) {
                    starDirection = 1;
                } else if (body.position.x < gm.LevelMinX + 2.5f) {
                    starDirection = 2;
                }
            }

            while (amount > 0) {
                if (Stars <= 0)
                    break;

                if (!fastStars) {
                    if (starDirection == 0)
                        starDirection = 2;
                    if (starDirection == 3)
                        starDirection = 1;
                }

                Runner.Spawn(PrefabList.Instance.Obj_BigStar, body.position + Vector2.up * WorldHitboxSize.y, onBeforeSpawned: (runner, obj) => {
                    StarBouncer bouncer = obj.GetComponent<StarBouncer>();
                    bouncer.OnBeforeSpawned((byte) starDirection, false, deathplane);
                });

                starDirection = (starDirection + 1) % 4;
                Stars--;
                amount--;
            }
            GameData.Instance.CheckForWinner();
        }
        #endregion

        #region -- DEATH / RESPAWNING --
        public void Death(bool deathplane, bool fire) {
            if (IsDead)
                return;

            if (IsProxy)
                return;

            IsDead = true;
            FireDeath = fire;
            PreRespawnTimer = TickTimer.CreateFromSeconds(Runner, 3f);
            RespawnTimer = TickTimer.CreateFromSeconds(Runner, 4.3f);

            if ((Lives > 0 && --Lives == 0) || Disconnected) {
                GameData.Instance.CheckForWinner();

                //spawn all stars
                SpawnStars(Stars, deathplane);
                RespawnTimer = TickTimer.None;

            } else {
                SpawnStars(1, deathplane);
            }

            OnSpinner = null;
            CurrentPipe = null;
            IsInShell = false;
            IsPropellerFlying = false;
            PropellerLaunchTimer = TickTimer.None;
            PropellerSpinTimer = TickTimer.None;
            IsSpinnerFlying = false;
            IsDrilling = false;
            IsSliding = false;
            IsCrouching = false;
            IsSkidding = false;
            IsTurnaround = false;
            IsGroundpounding = false;
            IsInKnockback = false;
            WallSlideRight = false;
            WallSlideLeft = false;
            IsSwimming = false;
            IsWaterWalking = false;

            body.velocity = Vector2.zero;
            body.isKinematic = false;
            AttemptThrowHeldItem(null, true);
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        public void Rpc_DisconnectDeath() {
            if (IsDead)
                return;

            Disconnected = true;
            Lives = 0;
            Death(false, false);
        }

        private void AttemptThrowHeldItem(bool? right = null, bool crouch = false) {
            right ??= FacingRight;

            if (HeldEntity) {
                HeldEntity.Throw(right.Value, crouch);
                FireballDelayTimer = TickTimer.CreateFromSeconds(Runner, 0.35f);
            }
            HeldEntity = null;
        }

        public void PreRespawn() {

            RespawnTimer = TickTimer.CreateFromSeconds(Runner, 1.3f);

            if (Lives == 0) {
                GameData.Instance.CheckForWinner();

                if (Object.HasInputAuthority)
                    GameManager.Instance.spectationManager.Spectating = true;

                Runner.Despawn(Object);
                return;
            }

            Vector2 spawnpoint = Spawnpoint;
            networkRigidbody.TeleportToPosition(spawnpoint);
            cameraController.Recenter(spawnpoint);

            IsRespawning = true;
            FacingRight = true;
            transform.localScale = Vector2.one;
            PreviousState = State = Enums.PowerupState.NoPowerup;
            animationController.DisableAllModels();
            animator.SetTrigger("respawn");
            StarmanTimer = TickTimer.None;
            GiantTimer = TickTimer.None;
            GiantEndTimer = TickTimer.None;
            GiantStartTimer = TickTimer.None;
            IsGroundpounding = false;
            body.isKinematic = true;
            body.velocity = Vector2.zero;
        }

        public void Respawn() {

            //gameObject.SetActive(true);
            IsDead = false;
            IsRespawning = false;
            State = Enums.PowerupState.NoPowerup;
            PreviousState = Enums.PowerupState.NoPowerup;
            body.velocity = Vector2.zero;
            WallSlideLeft = false;
            WallSlideRight = false;
            WallSlideEndTimer = TickTimer.None;
            WallJumpTimer = TickTimer.None;
            IsSpinnerFlying = false;
            FacingRight = true;

            IsPropellerFlying = false;
            UsedPropellerThisJump = false;
            PropellerLaunchTimer = TickTimer.None;
            PropellerSpinTimer = TickTimer.None;

            IsCrouching = false;
            IsOnGround = false;
            IsSliding = false;
            StarmanTimer = TickTimer.None;
            GiantStartTimer = TickTimer.None;
            GiantEndTimer = TickTimer.None;
            GiantTimer = TickTimer.None;
            JumpState = PlayerJumpState.None;
            IsTurnaround = false;
            IsInKnockback = false;
            DoEntityBounce = false;
            IsSkidding = false;
            IsGroundpounding = false;
            IsInShell = false;
            IsSwimming = false;
            IsWaterWalking = false;
            ResetKnockback();
            models.transform.rotation = Quaternion.Euler(0, 180, 0);
            body.isKinematic = false;
            body.velocity = Vector2.zero;

            if (Object.HasInputAuthority)
                ScoreboardUpdater.Instance.OnRespawnToggle();
        }
        #endregion

        #region -- SOUNDS / PARTICLES --
        public void PlaySoundEverywhere(Enums.Sounds sound) {
            GameManager.Instance.sfx.PlayOneShot(sound, character);
        }
        public void PlaySound(Enums.Sounds sound, byte variant = 0, float volume = 1) {
            if (sound == Enums.Sounds.Powerup_MegaMushroom_Break_Block) {
                sfxBrick.Stop();
                sfxBrick.clip = sound.GetClip(character, variant);
                sfxBrick.Play();
            } else {
                PlaySound(sound, character, variant, volume);
            }
        }
        protected GameObject SpawnParticle(string particle, Vector2 worldPos, Quaternion? rot = null) {
            return Instantiate(Resources.Load(particle), worldPos, rot ?? Quaternion.identity) as GameObject;
        }
        protected GameObject SpawnParticle(GameObject particle, Vector2 worldPos, Quaternion? rot = null) {
            return Instantiate(particle, worldPos, rot ?? Quaternion.identity);
        }

        protected void GiantFootstep() {
            CameraController.ScreenShake = 0.15f;
            SpawnParticle(PrefabList.Instance.Particle_Groundpound, body.position + new Vector2(FacingRight ? 0.5f : -0.5f, 0));
            PlaySound(Enums.Sounds.Powerup_MegaMushroom_Walk, (byte) (footstepVariant ? 1 : 2));
            footstepVariant = !footstepVariant;
        }

        protected void Footstep() {
            if (State == Enums.PowerupState.MegaMushroom)
                return;

            bool left = PreviousInputs.buttons.IsSet(PlayerControls.Left);
            bool right = PreviousInputs.buttons.IsSet(PlayerControls.Right);

            bool reverse = body.velocity.x != 0 && ((left ? 1 : -1) == Mathf.Sign(body.velocity.x));
            if (OnIce && (left ^ right) && reverse) {
                PlaySound(Enums.Sounds.World_Ice_Skidding);
                return;
            }
            if (IsPropellerFlying) {
                PlaySound(Enums.Sounds.Powerup_PropellerMushroom_Kick);
                return;
            }
            if (IsWaterWalking) {
                footstepSound = Enums.Sounds.Player_Walk_Water;
            }
            if (footstepParticle != Enums.Particle.None)
                GameManager.Instance.particleManager.Play((Enums.Particle) ((int) footstepParticle + (FacingRight ? 1 : 0)), body.position);

            if (!IsWaterWalking && Mathf.Abs(body.velocity.x) < WalkingMaxSpeed)
                return;

            PlaySound(footstepSound, (byte) (footstepVariant ? 1 : 2), Mathf.Abs(body.velocity.x) / (RunningMaxSpeed + 4));
            footstepVariant = !footstepVariant;
        }
        #endregion

        #region -- TILE COLLISIONS --
        private void HandleGiantTiles(bool pipes) {
            //TODO?
            if (State != Enums.PowerupState.MegaMushroom || GiantStartTimer.IsActive(Runner))
                return;

            Vector2 checkSize = WorldHitboxSize * 1.1f;

            bool grounded = previousTickVelocity.y < -8f && IsOnGround;
            Vector2 offset = Vector2.zero;
            if (grounded)
                offset = Vector2.down * 0.5f;

            Vector2 checkPosition = body.position + (Vector2.up * checkSize * 0.5f) + (2 * Runner.DeltaTime * body.velocity) + offset;

            Vector2Int minPos = Utils.Utils.WorldToTilemapPosition(checkPosition - (checkSize * 0.5f), wrap: false);
            Vector2Int size = Utils.Utils.WorldToTilemapPosition(checkPosition + (checkSize * 0.5f), wrap: false) - minPos;

            for (int x = 0; x <= size.x; x++) {
                for (int y = 0; y <= size.y; y++) {
                    Vector2Int tileLocation = new(minPos.x + x, minPos.y + y);
                    Vector2 worldPosCenter = Utils.Utils.TilemapToWorldPosition(tileLocation) + Vector3.one * 0.25f;
                    Utils.Utils.WrapTileLocation(ref tileLocation);

                    InteractableTile.InteractionDirection dir = InteractableTile.InteractionDirection.Up;
                    if (worldPosCenter.y - 0.25f + Physics2D.defaultContactOffset * 2f <= body.position.y) {
                        if (!grounded && !IsGroundpounding)
                            continue;

                        dir = InteractableTile.InteractionDirection.Down;
                    } else if (worldPosCenter.y + Physics2D.defaultContactOffset * 2f >= body.position.y + size.y) {
                        dir = InteractableTile.InteractionDirection.Up;
                    } else if (worldPosCenter.x <= body.position.x) {
                        dir = InteractableTile.InteractionDirection.Left;
                    } else if (worldPosCenter.x >= body.position.x) {
                        dir = InteractableTile.InteractionDirection.Right;
                    }

                    if (GameManager.Instance.tileManager.GetTile(tileLocation, out BreakablePipeTile pipe)) {
                        if (pipe.upsideDownPipe || !pipes || IsGroundpounding)
                            continue;
                    }

                    InteractWithTile(tileLocation, dir, out bool _, out bool _);
                }
            }
            if (pipes) {
                for (int x = 0; x <= size.x; x++) {
                    for (int y = size.y; y >= 0; y--) {
                        Vector2Int tileLocation = new(minPos.x + x, minPos.y + y);
                        Vector2 worldPosCenter = Utils.Utils.TilemapToWorldPosition(tileLocation) + Vector3.one * 0.25f;
                        Utils.Utils.WrapTileLocation(ref tileLocation);

                        InteractableTile.InteractionDirection dir = InteractableTile.InteractionDirection.Up;
                        if (worldPosCenter.y - 0.25f + Physics2D.defaultContactOffset * 2f <= body.position.y) {
                            if (!grounded && !IsGroundpounding)
                                continue;

                            dir = InteractableTile.InteractionDirection.Down;
                        } else if (worldPosCenter.x - 0.25f < checkPosition.x - checkSize.x * 0.5f) {
                            dir = InteractableTile.InteractionDirection.Left;
                        } else if (worldPosCenter.x + 0.25f > checkPosition.x + checkSize.x * 0.5f) {
                            dir = InteractableTile.InteractionDirection.Right;
                        }

                        if (GameManager.Instance.tileManager.GetTile(tileLocation, out BreakablePipeTile pipe)) {
                            if (!pipe.upsideDownPipe || dir == InteractableTile.InteractionDirection.Up)
                                continue;
                        }

                        InteractWithTile(tileLocation, dir, out bool _, out bool _);
                    }
                }
            }
        }

        private bool InteractWithTile(Vector2Int tilePos, InteractableTile.InteractionDirection direction, out bool interacted, out bool bumpSound) {

            if (interacted = GameManager.Instance.tileManager.GetTile(tilePos, out InteractableTile tile)) {
                return tile.Interact(this, direction, Utils.Utils.TilemapToWorldPosition(tilePos), out bumpSound);
            }

            bumpSound = false;
            return false;
        }
        #endregion

        #region -- KNOCKBACK --
        public void DoKnockback(bool fromRight, int starsToDrop, bool weak, NetworkObject attacker) {
            if ((weak && IsWeakKnockback && IsInKnockback) || (IsInKnockback && !IsWeakKnockback))
                return;

            if (GameData.Instance.GameState != Enums.GameState.Playing || DamageInvincibilityTimer.IsActive(Runner) || CurrentPipe || IsFrozen || IsDead || GiantStartTimer.IsActive(Runner) || GiantEndTimer.IsActive(Runner))
                return;

            if (State == Enums.PowerupState.MiniMushroom && starsToDrop > 1) {
                SpawnStars(starsToDrop - 1, false);
                Powerdown(false);
                return;
            }

            if (IsInKnockback || IsWeakKnockback)
                starsToDrop = Mathf.Min(1, starsToDrop);

            IsInKnockback = true;
            IsWeakKnockback = weak;
            IsForwardsKnockback = FacingRight != fromRight;
            KnockbackAttacker = attacker;
            KnockbackWasOriginallyFacingRight = FacingRight;
            KnockbackTimer = TickTimer.CreateFromSeconds(Runner, 0.5f);

            body.velocity = new Vector2(
                (fromRight ? -1 : 1) *
                ((starsToDrop + 1) / 2f) *
                4f *
                (State == Enums.PowerupState.MegaMushroom ? 3 : 1) *
                (State == Enums.PowerupState.MiniMushroom ? 2.5f : 1f) *
                (weak ? 0.5f : 1f),

                // don't go upwards if we got hit by a fireball
                (attacker && attacker.TryGetComponent(out FireballMover _)) ? 0 : 4.5f
            );

            IsOnGround = false;
            previousTickIsOnGround = false;
            IsInShell = false;
            IsGroundpounding = false;
            IsSpinnerFlying = false;
            IsPropellerFlying = false;
            PropellerLaunchTimer = TickTimer.None;
            PropellerSpinTimer = TickTimer.None;
            IsSliding = false;
            IsDrilling = false;
            WallSlideLeft = WallSlideRight = false;

            SpawnStars(starsToDrop, false);
            HandleLayerState();
        }

        public void AirBonk(bool fromRight) {
            body.velocity = new(RunningMaxSpeed * (fromRight ? -1 : 1), body.velocity.y);
        }

        public void ResetKnockbackFromAnim() {
            ResetKnockback();
        }

        private void ResetKnockback() {
            DamageInvincibilityTimer = TickTimer.CreateFromSeconds(Runner, 1f);
            KnockbackTimer = TickTimer.None;
            DoEntityBounce = false;
            IsInKnockback = false;
            body.velocity = new(0, body.velocity.y);
            FacingRight = KnockbackWasOriginallyFacingRight;
        }
        #endregion

        public void SetHeldEntity(HoldableEntity entity) {
            if (HeldEntity) {
                HeldEntity.Holder = null;
                HeldEntity.PreviousHolder = this;
            }

            HeldEntity = entity;

            if (HeldEntity) {
                HeldEntity.Holder = this;
                HeldEntity.PreviousHolder = null;
                HoldStartTime = Runner.SimulationTime;

                SetHoldingOffset();
            }
        }

        private void HandleSliding(bool up, bool down, bool left, bool right) {
            if (IsGroundpounding) {
                if (IsOnGround) {
                    if (State == Enums.PowerupState.MegaMushroom) {
                        IsGroundpounding = false;
                        GroundpoundStartTimer = TickTimer.CreateFromSeconds(Runner, 0.5f);
                        return;
                    }
                    if (!IsInShell && Mathf.Abs(FloorAngle) >= slopeSlidingAngle && OnSlope) {
                        IsGroundpounding = false;
                        IsSliding = true;
                        GroundpoundHeld = false;
                        body.velocity = new Vector2(-Mathf.Sign(FloorAngle) * SPEED_SLIDE_MAX, 0);
                    } else {
                        body.velocity = Vector2.zero;
                        if (!down || State == Enums.PowerupState.MegaMushroom) {
                            IsGroundpounding = false;
                            GroundpoundStartTimer = TickTimer.CreateFromSeconds(Runner, 0.2667f);
                        }
                    }
                }
                if (up && !GroundpoundStartTimer.IsActive(Runner)) {
                    IsGroundpounding = false;
                    body.velocity = Vector2.down * groundpoundVelocity;
                    GroundpoundCooldownTimer = TickTimer.CreateFromSeconds(Runner, 0.125f);
                }
            }
            if (OnSlope && (!((FacingRight && hitRight) || (!FacingRight && hitLeft)) && IsCrouching && Mathf.Abs(FloorAngle) >= slopeSlidingAngle && !IsInShell && State != Enums.PowerupState.MegaMushroom)) {
                IsSliding = true;
                GroundpoundHeld = false;
                IsCrouching = false;
            }
            if (IsSliding && IsOnGround && Mathf.Abs(FloorAngle) > slopeSlidingAngle) {
                float angleDeg = FloorAngle * Mathf.Deg2Rad;

                bool uphill = Mathf.Sign(FloorAngle) == Mathf.Sign(body.velocity.x);
                float speed = Runner.DeltaTime * 5f * (uphill ? Mathf.Clamp01(1f - (Mathf.Abs(body.velocity.x) / RunningMaxSpeed)) : 4f);

                float newX = Mathf.Clamp(body.velocity.x - (Mathf.Sin(angleDeg) * speed), -(RunningMaxSpeed * 1.3f), RunningMaxSpeed * 1.3f);
                float newY = Mathf.Sin(angleDeg) * newX + 0.4f;
                body.velocity = new Vector2(newX, newY);
            }

            if (up || ((left ^ right) && !down) || (OnSlope && Mathf.Abs(FloorAngle) < slopeSlidingAngle && IsOnGround && body.velocity.x == 0 && !down) || (FacingRight && hitRight) || (!FacingRight && hitLeft)) {
                IsSliding = false;
            }
        }

        private void HandleSlopes() {
            OnSlope = false;

            if (!IsOnGround) {
                FloorAngle = 0;
                return;
            }

            RaycastHit2D hit = Runner.GetPhysicsScene2D().BoxCast(body.position + (Vector2.up * 0.05f), new Vector2((MainHitbox.size.x - Physics2D.defaultContactOffset * 2f) * transform.lossyScale.x, 0.1f), 0, body.velocity.normalized, (body.velocity * Runner.DeltaTime).magnitude, Layers.MaskAnyGround);
            if (hit) {
                //hit ground
                float angle = Vector2.SignedAngle(Vector2.up, hit.normal);
                if (Mathf.Abs(angle) > 89)
                    return;

                TileWithProperties tile = Utils.Utils.GetTileAtWorldLocation(hit.point) as TileWithProperties;
                if (!tile && GameManager.Instance.semisolidTilemap)
                    tile = GameManager.Instance.semisolidTilemap.GetTile<TileWithProperties>((Vector3Int) Utils.Utils.WorldToTilemapPosition(hit.point));

                float x = Mathf.Abs(FloorAngle - angle) > 1f ? previousTickVelocity.x : body.velocity.x;

                FloorAngle = angle;

                float change = Mathf.Sin(angle * Mathf.Deg2Rad) * x * 1.1f;
                body.velocity = new Vector2(x, change);
                IsOnGround = true;
                previousTickIsOnGround = true;
                OnSlope = tile ? tile.isSlope : false;
            } else {
                hit = Runner.GetPhysicsScene2D().BoxCast(body.position + (Vector2.up * 0.05f), new Vector2((MainHitbox.size.x + Physics2D.defaultContactOffset * 3f) * transform.lossyScale.x, 0.1f), 0, Vector2.down, 0.3f, Layers.MaskAnyGround);
                if (hit) {
                    float angle = Vector2.SignedAngle(Vector2.up, hit.normal);
                    if (Mathf.Abs(angle) > 89)
                        return;

                    TileWithProperties tile = Utils.Utils.GetTileAtWorldLocation(hit.point) as TileWithProperties;
                    if (!tile && GameManager.Instance.semisolidTilemap)
                        tile = GameManager.Instance.semisolidTilemap.GetTile<TileWithProperties>((Vector3Int) Utils.Utils.WorldToTilemapPosition(hit.point));

                    float x = Mathf.Abs(FloorAngle - angle) > 1f ? previousTickVelocity.x : body.velocity.x;

                    FloorAngle = angle;

                    float change = Mathf.Sin(angle * Mathf.Deg2Rad) * x * 1.1f;
                    body.velocity = new(x, change);
                    IsOnGround = true;
                    previousTickIsOnGround = true;
                    OnSlope = tile ? tile.isSlope : false;
                } else {
                    FloorAngle = 0;
                }
            }

            if (Mathf.Abs(body.velocity.x) < 0.01f && body.velocity.y < 0 && body.velocity.y > -0.01f) {
                body.velocity = Vector2.zero;
            }
        }

        private void HandleLayerState() {
            bool hitsNothing = CurrentPipe || IsDead || IsStuckInBlock || GiantStartTimer.IsActive(Runner) || (GiantEndTimer.IsActive(Runner) && IsStationaryGiantShrink);

            MainHitbox.gameObject.layer = hitsNothing ? Layers.LayerHitsNothing : Layers.LayerPlayer;
        }

        private bool GroundSnapCheck() {
            if (IsDead || (body.velocity.y > 0.1f && FloorAngle == 0) || PropellerLaunchTimer.IsActive(Runner) || CurrentPipe)
                return false;

            // TODO: improve
            RaycastHit2D hit;
            if (IsWaterWalking) {
                hit = Runner.GetPhysicsScene2D().BoxCast(body.position + Vector2.up * 0.1f, new Vector2(WorldHitboxSize.x, 0.05f), 0, Vector2.down, 0.4f, 1 << Layers.LayerEntityHitbox);
                if (hit && hit.collider.gameObject.CompareTag("water")) {
                    body.position = new(body.position.x, hit.point.y + Physics2D.defaultContactOffset);
                    return true;
                } else {
                    IsWaterWalking = false;
                }
            }

            hit = Runner.GetPhysicsScene2D().BoxCast(body.position + Vector2.up * 0.15f, new Vector2(WorldHitboxSize.x, 0.05f), 0, Vector2.down, 0.4f, Layers.MaskAnyGround);
            if (hit) {
                body.position = new(body.position.x, hit.point.y + Physics2D.defaultContactOffset);

                if (hit.collider.gameObject.CompareTag("spinner")) {
                    OnSpinner = hit.collider.gameObject.GetComponentInParent<SpinnerAnimator>();
                    OnSpinner.HasPlayer = true;
                }
                return true;
            }

            return false;
        }

        #region -- PIPES --

        private void DownwardsPipeCheck(bool down) {
            if (!down || State == Enums.PowerupState.MegaMushroom || !IsOnGround || IsInKnockback || IsInShell || HeldEntity)
                return;

            foreach (RaycastHit2D hit in Physics2D.RaycastAll(body.position, Vector2.down, 0.1f)) {
                GameObject obj = hit.transform.gameObject;
                if (!obj.CompareTag("pipe"))
                    continue;
                PipeManager pipe = obj.GetComponent<PipeManager>();
                if (!pipe.entryAllowed || (pipe.miniOnly && State != Enums.PowerupState.MiniMushroom))
                    continue;

                //Enter pipe
                EnterPipe(pipe, Vector2.down);
                break;
            }
        }

        private void UpwardsPipeCheck(bool up) {
            if (!up || IsGroundpounding || !hitRoof || State == Enums.PowerupState.MegaMushroom || IsInKnockback || HeldEntity)
                return;

            //todo: change to nonalloc?
            foreach (RaycastHit2D hit in Physics2D.RaycastAll(body.position, Vector2.up, 1f)) {
                GameObject obj = hit.transform.gameObject;
                if (!obj.CompareTag("pipe"))
                    continue;
                PipeManager pipe = obj.GetComponent<PipeManager>();
                if (!pipe.entryAllowed || (pipe.miniOnly && State != Enums.PowerupState.MiniMushroom))
                    continue;

                //pipe found
                EnterPipe(pipe, Vector2.up);
                break;
            }
        }

        private void EnterPipe(PipeManager pipe, Vector2 direction) {
            CurrentPipe = pipe;
            PipeEntering = true;
            PipeTimer = TickTimer.CreateFromSeconds(Runner, animationController.pipeDuration * 0.5f);
            body.velocity = PipeDirection = direction;

            transform.position = body.position = new Vector2(pipe.transform.position.x, transform.position.y);

            IsCrouching = false;
            IsSliding = false;
            IsPropellerFlying = false;
            UsedPropellerThisJump = false;
            IsSpinnerFlying = false;
            IsInShell = false;
            if (StarmanTimer.IsActive(Runner))
                StarmanTimer = TickTimer.CreateFromSeconds(Runner, (StarmanTimer.RemainingTime(Runner) ?? 0) + animationController.pipeDuration);
        }
        #endregion

        private void HandleCrouching(bool crouchInput) {
            // Can't crouch while sliding, flying, or mega.
            if (IsSliding || IsPropellerFlying || IsSpinnerFlying || IsInKnockback || State == Enums.PowerupState.MegaMushroom) {
                IsCrouching = false;
                return;
            }

            if (!IsCrouching && IsSwimming && Mathf.Abs(body.velocity.x) > 0.03f)
                return;

            IsCrouching = ((IsOnGround && crouchInput && !IsGroundpounding) || (!IsOnGround && (crouchInput || (body.velocity.y > 0 && State != Enums.PowerupState.BlueShell)) && IsCrouching && !IsSwimming) || (IsCrouching && ForceCrouchCheck())) && !HeldEntity;
        }

        public bool ForceCrouchCheck() {
            //janky fortress ceiling check, m8
            if (State == Enums.PowerupState.BlueShell && IsOnGround && SceneManager.GetActiveScene().buildIndex != 4)
                return false;
            if (State <= Enums.PowerupState.MiniMushroom)
                return false;

            float width = MainHitbox.bounds.extents.x;
            float uncrouchHeight = GetHitboxSize(false).y * transform.lossyScale.y;

            bool ret = Runner.GetPhysicsScene2D().BoxCast(body.position + Vector2.up * 0.1f, new(width - 0.05f, 0.05f), 0, Vector2.up, uncrouchHeight - 0.1f, Layers.MaskSolidGround);
            return ret;
        }

        private void HandleWallslide(bool holdingLeft, bool holdingRight, bool jump) {

            Vector2 currentWallDirection;
            if (WallSlideLeft) {
                currentWallDirection = Vector2.left;
            } else if (WallSlideRight) {
                currentWallDirection = Vector2.right;
            } else if (holdingLeft) {
                currentWallDirection = Vector2.left;
            } else if (holdingRight) {
                currentWallDirection = Vector2.right;
            } else {
                return;
            }

            HandleWallSlideStopChecks(currentWallDirection, holdingRight, holdingLeft);

            if (WallSlideEndTimer.Expired(Runner)) {
                WallSlideRight = false;
                WallSlideLeft = false;
                WallSlideEndTimer = TickTimer.None;
                return;
            }

            if (WallSliding) {
                //walljump check
                FacingRight = WallSlideLeft;
                if (jump && WallJumpTimer.ExpiredOrNotRunning(Runner)) {
                    //perform walljump

                    hitRight = false;
                    hitLeft = false;
                    body.velocity = new(WALLJUMP_HSPEED * (WallSlideLeft ? 1 : -1), State == Enums.PowerupState.MiniMushroom ? WALLJUMP_MINI_VSPEED : WALLJUMP_VSPEED);
                    JumpState = PlayerJumpState.SingleJump;
                    IsOnGround = false;
                    DoEntityBounce = false;
                    timeSinceLastBumpSound = 0;

                    WallJumpTimer = TickTimer.CreateFromSeconds(Runner, 16f / 60f);
                    WallSlideRight = false;
                    WallSlideLeft = false;
                    WallSlideEndTimer = TickTimer.None;
                }
            } else if (hitLeft || hitRight) {
                //walljump starting check
                bool canWallslide = !IsInShell && body.velocity.y < -0.1f && !IsGroundpounding && !IsOnGround && !HeldEntity && State != Enums.PowerupState.MegaMushroom && !IsSpinnerFlying && !IsDrilling && !IsCrouching && !IsSliding && !IsInKnockback && PropellerLaunchTimer.ExpiredOrNotRunning(Runner);
                if (!canWallslide)
                    return;

                //Check 1
                if (WallJumpTimer.IsActive(Runner))
                    return;

                //Check 2
                if (WallSlideEndTimer.IsActive(Runner))
                    return;

                //Check 4: already handled
                //Check 5.2: already handled

                //Check 6
                if (IsCrouching)
                    return;

                //Check 8
                if (!((currentWallDirection == Vector2.right && FacingRight) || (currentWallDirection == Vector2.left && !FacingRight)))
                    return;

                //Start wallslide
                WallSlideRight = currentWallDirection == Vector2.right && hitRight;
                WallSlideLeft = currentWallDirection == Vector2.left && hitLeft;
                WallSlideEndTimer = TickTimer.None;

                if (WallSlideRight || WallSlideLeft)
                    IsPropellerFlying = false;
            }
        }

        private static readonly Vector2 WallSlideLowerHeightOffset = new(0f, 0.2f);
        private void HandleWallSlideStopChecks(Vector2 wallDirection, bool right, bool left) {
            bool floorCheck = !Runner.GetPhysicsScene2D().Raycast(body.position, Vector2.down, 0.3f, Layers.MaskAnyGround);
            bool moveDownCheck = body.velocity.y < 0;
            bool heightLowerCheck = Runner.GetPhysicsScene2D().Raycast(body.position + WallSlideLowerHeightOffset, wallDirection, MainHitbox.size.x * 2, Layers.MaskSolidGround);
            if (!floorCheck || !moveDownCheck || !heightLowerCheck) {
                WallSlideRight = false;
                WallSlideLeft = false;
                WallSlideEndTimer = TickTimer.None;
                return;
            }

            if ((wallDirection == Vector2.left && (!left || !hitLeft)) || (wallDirection == Vector2.right && (!right || !hitRight))) {
                if (WallSlideEndTimer.ExpiredOrNotRunning(Runner)) {
                    WallSlideEndTimer = TickTimer.CreateFromSeconds(Runner, 16 / 60f);
                }
            } else {
                WallSlideEndTimer = TickTimer.None;
            }
        }

        private void HandleJumping(bool jumpHeld, bool doJump, bool down) {
            if (IsInKnockback || IsDrilling || (State == Enums.PowerupState.MegaMushroom && JumpState == PlayerJumpState.SingleJump) || WallSliding)
                return;

            if (!DoEntityBounce && !doJump)
                return;

            if (!DoEntityBounce && OnSpinner && IsOnGround && !HeldEntity) {
                // Jump of spinner
                body.velocity = new(body.velocity.x, launchVelocity);
                IsSpinnerFlying = true;
                SpinnerLaunchAnimCounter = true;
                IsOnGround = false;
                previousTickIsOnGround = false;
                IsCrouching = false;
                IsInShell = false;
                IsSkidding = false;
                IsTurnaround = false;
                IsSliding = false;
                WallSlideEndTimer = TickTimer.None;
                IsGroundpounding = false;
                GroundpoundStartTimer = TickTimer.None;
                IsDrilling = false;
                IsPropellerFlying = false;
                OnSpinner.ArmPosition = 0;
                OnSpinner = null;
                return;
            }

            bool topSpeed = Mathf.Abs(body.velocity.x) >= RunningMaxSpeed;
            bool canSpecialJump = !down && (doJump || (DoEntityBounce && jumpHeld)) && ProperJump && !IsSpinnerFlying && !IsPropellerFlying && topSpeed && ((Runner.SimulationTime - TimeGrounded < 0.2f) || DoEntityBounce) && !HeldEntity && JumpState != PlayerJumpState.TripleJump && !IsCrouching && !IsInShell && ((body.velocity.x < 0 && !FacingRight) || (body.velocity.x > 0 && FacingRight)) && !Runner.GetPhysicsScene2D().Raycast(body.position + new Vector2(0, 0.1f), Vector2.up, 1f, Layers.MaskSolidGround);
            float jumpBoost = 0;

            IsSkidding = false;
            IsTurnaround = false;
            IsSliding = false;
            WallSlideEndTimer = TickTimer.None;
            IsGroundpounding = false;
            GroundpoundStartTimer = TickTimer.None;
            IsDrilling = false;
            IsSpinnerFlying &= DoEntityBounce;
            IsPropellerFlying &= DoEntityBounce;

            //disable koyote time
            IgnoreCoyoteTime = true;
            IsOnGround = false;

            float t = Mathf.Clamp01(Mathf.Abs(body.velocity.x) - SPEED_STAGE_MAX[1] + (SPEED_STAGE_MAX[1] * 0.5f));
            float vel = State switch {
                Enums.PowerupState.MegaMushroom => 12.1875f + Mathf.Lerp(0, 0.52734375f, t),
                Enums.PowerupState.MiniMushroom => 5.408935546875f + Mathf.Lerp(0, 0.428466796875f, t),
                _ => 6.62109375f + Mathf.Lerp(0, 0.46875f, t),
            };
            vel += (Mathf.Sign(body.velocity.x) != Mathf.Sign(FloorAngle)) ? 0 : Mathf.Abs(FloorAngle) * 0.01f * t;

            if (canSpecialJump && JumpState == PlayerJumpState.SingleJump) {
                //Double jump
                JumpState = PlayerJumpState.DoubleJump;
            } else if (canSpecialJump && JumpState == PlayerJumpState.DoubleJump) {
                //Triple Jump
                JumpState = PlayerJumpState.TripleJump;
                jumpBoost = 0.5f;
            } else {
                //Normal jump
                JumpState = PlayerJumpState.SingleJump;
            }

            body.velocity = new(body.velocity.x, vel + jumpBoost);
            ProperJump = true;
            IsJumping = true;
            JumpAnimCounter++;
            JumpBufferTime = -1;
            IsInKnockback = false;

            BounceJump = DoEntityBounce;
            DoEntityBounce = false;
            timeSinceLastBumpSound = 0;
        }

        public void UpdateHitbox() {
            bool crouchHitbox = State != Enums.PowerupState.MiniMushroom && CurrentPipe == null && ((IsCrouching && !IsGroundpounding) || IsInShell || IsSliding);
            Vector2 hitbox = GetHitboxSize(crouchHitbox);

            MainHitbox.size = hitbox;
            MainHitbox.offset = Vector2.up * 0.5f * hitbox;
            MainHitbox.isTrigger = IsDead;
        }

        public Vector2 GetHitboxSize(bool crouching) {
            float height;

            if (State <= Enums.PowerupState.MiniMushroom || (IsStarmanInvincible && !IsOnGround && !crouching && !IsSliding && !IsSpinnerFlying && !IsPropellerFlying) || IsGroundpounding) {
                height = heightSmallModel;
            } else {
                height = heightLargeModel;
            }

            if (crouching)
                height *= State <= Enums.PowerupState.MiniMushroom ? 0.7f : 0.5f;

            return new(MainHitbox.size.x, height);
        }

        private void HandleWalkingRunning(bool left, bool right) {

            if (WallJumpTimer.IsActive(Runner)) {
                if ((WallJumpTimer.RemainingTime(Runner) ?? 0f) < 0.2f && (hitLeft || hitRight)) {
                    WallJumpTimer = TickTimer.None;
                } else {
                    body.velocity = new(WALLJUMP_HSPEED * (FacingRight ? 1 : -1), body.velocity.y);
                    return;
                }
            }

            if (IsGroundpounding || IsInKnockback || CurrentPipe || JumpLandingTimer.IsActive(Runner) || !(WallJumpTimer.ExpiredOrNotRunning(Runner) || IsOnGround || body.velocity.y < 0))
                return;

            if (!IsOnGround)
                IsSkidding = false;

            if (IsInShell) {
                body.velocity = new(SPEED_STAGE_MAX[RUN_STAGE] * 0.9f * (FacingRight ? 1 : -1) * (1f - (ShellSlowdownTimer.RemainingTime(Runner) ?? 0f)), body.velocity.y);
                return;
            }

            bool run = IsFunctionallyRunning && (!IsSpinnerFlying || State == Enums.PowerupState.MegaMushroom);

            int maxStage;
            if (IsSwimming)
                if (State == Enums.PowerupState.BlueShell) {
                    maxStage = SWIM_SHELL_STAGE_MAX.Length - 1;
                } else {
                    maxStage = SWIM_STAGE_MAX.Length - 1;
                }
            else if (IsStarmanInvincible && run && IsOnGround)
                maxStage = STAR_STAGE;
            else if (run)
                maxStage = RUN_STAGE;
            else
                maxStage = WALK_STAGE;

            float[] maxArray = SPEED_STAGE_MAX;
            if (IsSwimming) {
                if (IsOnGround) {
                    if (State == Enums.PowerupState.BlueShell) {
                        maxArray = SWIM_WALK_SHELL_STAGE_MAX;
                    } else {
                        maxArray = SWIM_WALK_STAGE_MAX;
                    }
                } else {
                    if (State == Enums.PowerupState.BlueShell) {
                        maxArray = SWIM_SHELL_STAGE_MAX;
                    } else {
                        maxArray = SWIM_STAGE_MAX;
                    }
                }
            }
            int stage = MovementStage;

            float acc;
            if (IsSwimming) {
                if (IsOnGround) {
                    if (State == Enums.PowerupState.BlueShell) {
                        acc = SWIM_WALK_SHELL_STAGE_ACC[stage];
                    } else {
                        acc = SWIM_WALK_STAGE_ACC[stage];
                    }
                } else {
                    if (State == Enums.PowerupState.BlueShell) {
                        acc = SWIM_SHELL_STAGE_ACC[stage];
                    } else {
                        acc = SWIM_STAGE_ACC[stage];
                    }
                }
            } else if (OnIce) {
                acc = ICE_STAGE_ACC[stage];
            } else if (State == Enums.PowerupState.MegaMushroom) {
                acc = SPEED_STAGE_MEGA_ACC[stage];
            } else {
                acc = SPEED_STAGE_ACC[stage];
            }

            float sign = Mathf.Sign(body.velocity.x);
            bool uphill = Mathf.Sign(FloorAngle) == sign;

            if (!IsOnGround)
                TurnaroundBoostTime = 0;

            if (TurnaroundBoostTime > 0) {
                TurnaroundBoostTime -= Runner.DeltaTime;
                body.velocity = new(0, body.velocity.y);
                if (TurnaroundBoostTime < 0) {
                    IsTurnaround = true;
                    TurnaroundBoostTime = 0;
                }

            } else if (IsTurnaround) {
                float newX = body.velocity.x + (TURNAROUND_ACC * (FacingRight ? -1 : 1) * Runner.DeltaTime);
                IsTurnaround &= IsOnGround && !IsCrouching && Mathf.Abs(body.velocity.x) < SPEED_STAGE_MAX[1] && !hitRight && !hitLeft;
                IsSkidding &= IsTurnaround;
                body.velocity = new(newX, body.velocity.y);

            } else if ((left ^ right) && (!IsCrouching || (IsCrouching && !IsOnGround && State != Enums.PowerupState.BlueShell)) && !IsInKnockback && !IsSliding) {
                //we can walk here

                float speed = Mathf.Abs(body.velocity.x);
                bool reverse = body.velocity.x != 0 && ((left ? 1 : -1) == sign);

                //check that we're not going above our limit
                float max = maxArray[maxStage] + CalculateSlopeMaxSpeedOffset(Mathf.Abs(FloorAngle) * (uphill ? 1 : -1));
                //floating point & network accuracy bs means -0.01
                if (speed - 0.01f > max) {
                    acc = -acc;
                }

                if (reverse) {
                    IsTurnaround = false;
                    if (IsOnGround) {
                        if (!IsSwimming && speed >= SKIDDING_THRESHOLD && !HeldEntity && State != Enums.PowerupState.MegaMushroom) {
                            IsSkidding = true;
                            FacingRight = sign == 1;
                        }

                        if (IsSkidding) {
                            if (OnIce) {
                                acc = SKIDDING_ICE_DEC;
                            } else if (speed > maxArray[RUN_STAGE]) {
                                acc = SKIDDING_STAR_DEC;
                            } else {
                                acc = SKIDDING_DEC;
                            }
                            WalkingTurnaroundFrames = 0;
                        } else {
                            if (OnIce) {
                                acc = WALK_TURNAROUND_ICE_ACC;
                            } else {
                                WalkingTurnaroundFrames = (byte) Mathf.Clamp(WalkingTurnaroundFrames + 1, 0, WALK_TURNAROUND_ACC.Length - 1);
                                acc = State == Enums.PowerupState.MegaMushroom ? WALK_TURNAROUND_MEGA_ACC[WalkingTurnaroundFrames] : WALK_TURNAROUND_ACC[WalkingTurnaroundFrames];
                            }
                        }
                    } else {
                        acc = SPEED_STAGE_ACC[0] * 0.85f;
                    }
                } else {
                    WalkingTurnaroundFrames = 0;

                    if (IsSkidding && !IsTurnaround) {
                        IsSkidding = false;
                    }
                }

                int direction = left ? -1 : 1;
                float newX = body.velocity.x + acc * Runner.DeltaTime * direction;

                if (Mathf.Abs(newX) - speed > 0) {
                    //clamp only if accelerating
                    newX = Mathf.Clamp(newX, -max, max);
                }

                if (IsSkidding && !IsTurnaround && (Mathf.Sign(newX) != sign || speed < 0.05f)) {
                    //turnaround
                    TurnaroundBoostTime = 0.1667f;
                    newX = 0;
                }

                body.velocity = new(newX, body.velocity.y);

            } else if (IsOnGround || IsSwimming) {
                //not holding anything, sliding, or holding both directions. decelerate
                IsSkidding = false;
                IsTurnaround = false;

                float angle = Mathf.Abs(FloorAngle);
                if (IsSwimming)
                    acc = -SWIM_BUTTON_RELEASE_DEC;
                else if (IsSliding) {
                    if (angle > slopeSlidingAngle) {
                        //uphill / downhill
                        acc = (angle > 30 ? SLIDING_45_ACC : SLIDING_22_ACC) * (uphill ? -1 : 1);
                    } else {
                        //flat ground
                        acc = -SPEED_STAGE_ACC[0];
                    }
                } else if (OnIce)
                    acc = -BUTTON_RELEASE_ICE_DEC[stage];
                else if (IsInKnockback)
                    acc = -KNOCKBACK_DEC;
                else
                    acc = -BUTTON_RELEASE_DEC;

                int direction = (int) Mathf.Sign(body.velocity.x);
                float newX = body.velocity.x + acc * Runner.DeltaTime * direction;

                float target = (angle > 30 && OnSlope) ? Math.Sign(FloorAngle) * -SPEED_STAGE_MAX[0] : 0;
                if ((direction == -1) ^ (newX <= target))
                    newX = target;

                if (IsSliding) {
                    newX = Mathf.Clamp(newX, -SPEED_SLIDE_MAX, SPEED_SLIDE_MAX);
                }

                body.velocity = new(newX, body.velocity.y);

                if (newX != 0)
                    FacingRight = newX > 0;
            }

            IsInShell |= State == Enums.PowerupState.BlueShell && !IsSliding && IsOnGround && IsFunctionallyRunning
                && !HeldEntity && Mathf.Abs(body.velocity.x) >= SPEED_STAGE_MAX[RUN_STAGE] * 0.9f
                && (body.velocity.x > 0) == FacingRight;
            if (IsOnGround || previousTickIsOnGround)
                body.velocity = new(body.velocity.x, 0);
        }

        private float CalculateSlopeMaxSpeedOffset(float floorAngle) {
            return (float) (-0.0304687 * floorAngle);
        }


        private static readonly Vector2 StuckInBlockSizeCheck = new(1f, 0.95f);
        private bool HandleStuckInBlock() {
            if (!body || State == Enums.PowerupState.MegaMushroom)
                return false;

            Vector2 checkSize = WorldHitboxSize * StuckInBlockSizeCheck;
            Vector2 origin = body.position + (Vector2.up * checkSize * 0.5f);

            if (!Utils.Utils.IsAnyTileSolidBetweenWorldBox(origin, checkSize, true)) {
                IsStuckInBlock = false;
                return false;
            }

            bool wasStuckLastTick = IsStuckInBlock;
            IsStuckInBlock = true;
            IsGroundpounding = false;
            IsPropellerFlying = false;
            IsDrilling = false;
            IsSpinnerFlying = false;
            IsOnGround = false;
            IsSwimming = false;

            if (!wasStuckLastTick) {
                // Code for mario to instantly teleport to the closest free position when he gets stuck

                // Prevent mario from clipping to the floor if we got pushed in via our hitbox changing (shell on ice, for example)
                body.position = previousTickPosition;
                origin = body.position + (checkSize * 0.5f * Vector2.up);

                int angle = 45;
                int increments = 360 / angle;
                float distIncrement = 0.1f;
                float distMax = 0.6f;

                for (int i = 0; i < increments; i++) {

                    float radAngle = ((i * angle * 2) + ((i / 4) * angle) % 360) * Mathf.Deg2Rad;
                    float x = Mathf.Sin(radAngle);
                    float y = Mathf.Cos(radAngle);

                    float dist = 0;
                    while ((dist += distIncrement) < distMax) {

                        Vector2 checkPos = new(origin.x + (x * dist), origin.y + (y * dist));

                        if (Utils.Utils.IsAnyTileSolidBetweenWorldBox(checkPos, checkSize, true))
                            continue;

                        // Valid spot.
                        body.position = checkPos + (Vector2.down * checkSize * 0.5f);
                        IsStuckInBlock = false;
                        return false;
                    }
                }

                body.position = previousTickPosition;
            }

            body.velocity = Vector2.right * 2f;
            return true;
        }

        public void FinishMegaMario(bool success) {
            if (success) {
                GiantTimer = TickTimer.CreateFromSeconds(Runner, 15f);
            } else {
                // Hit a ceiling, cancel
                State = Enums.PowerupState.Mushroom;
                GiantEndTimer = TickTimer.CreateFromSeconds(Runner, giantStartTime - GiantStartTimer.RemainingTime(Runner) ?? 0f);
                GiantStartTimer = TickTimer.None;
                GiantTimer = TickTimer.None;
                IsStationaryGiantShrink = true;
                StoredPowerup = Enums.PowerupState.MegaMushroom;
            }
            body.isKinematic = false;
        }

        private void HandleFacingDirection(bool left, bool right) {
            if (IsGroundpounding && !IsOnGround)
                return;

            if (WallJumpTimer.IsActive(Runner)) {
                FacingRight = body.velocity.x > 0;
            } else if (!IsInShell && !IsSliding && !IsSkidding && !IsInKnockback && !(animator.GetCurrentAnimatorStateInfo(0).IsName("turnaround") || IsTurnaround)) {
                if (right ^ left)
                    FacingRight = right;
            } else if (GiantStartTimer.ExpiredOrNotRunning(Runner) && GiantEndTimer.ExpiredOrNotRunning(Runner) && !IsSkidding && !(animator.GetCurrentAnimatorStateInfo(0).IsName("turnaround") || IsTurnaround)) {
                if (IsInKnockback || (IsOnGround && State != Enums.PowerupState.MegaMushroom && Mathf.Abs(body.velocity.x) > 0.05f && !IsCrouching)) {
                    FacingRight = body.velocity.x > 0;
                } else if ((!IsInShell || GiantStartTimer.IsActive(Runner)) && (right || left)) {
                    FacingRight = right;
                }
                if (!IsInShell && ((Mathf.Abs(body.velocity.x) < 0.5f && IsCrouching) || OnIce) && (right || left))
                    FacingRight = right;
            }
        }

        public void EndMega() {
            if (State != Enums.PowerupState.MegaMushroom)
                return;

            State = Enums.PowerupState.Mushroom;
            GiantEndTimer = TickTimer.CreateFromSeconds(Runner, giantStartTime * 0.5f);
            IsStationaryGiantShrink = false;
            DamageInvincibilityTimer = TickTimer.CreateFromSeconds(Runner, 2f);

            if (body.velocity.y > 0)
                body.velocity = new(body.velocity.x, body.velocity.y * 0.33f);
        }

        public void HandleBlockSnapping() {
            if (CurrentPipe || IsDrilling)
                return;

            //if we're about to be in the top 2 pixels of a block, snap up to it, (if we can fit)

            if (body.velocity.y > 0)
                return;

            Vector2 nextPos = body.position + Runner.DeltaTime * 2f * body.velocity;

            if (!Utils.Utils.IsAnyTileSolidBetweenWorldBox(nextPos + WorldHitboxSize.y * 0.5f * Vector2.up, WorldHitboxSize))
                //we are not going to be inside a block next fixed update
                return;

            //we ARE inside a block. figure out the height of the contact
            // 32 pixels per unit
            RaycastHit2D contact = Runner.GetPhysicsScene2D().BoxCast(nextPos + 3f / 32f * Vector2.up, new(WorldHitboxSize.y, 1f / 32f), 0, Vector2.down, 3f / 32f, Layers.MaskAnyGround);

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

            if (Utils.Utils.IsAnyTileSolidBetweenWorldBox(newPosition + WorldHitboxSize.y * 0.5f * Vector2.up, WorldHitboxSize)) {
                //it's an invalid position anyway, we'd be inside something.
                return;
            }

            //valid position, snap upwards
            body.position = newPosition;
        }

        private void HandleMovement(NetworkButtons heldButtons, NetworkButtons pressedButtons) {
            float delta = Runner.DeltaTime;
            IsFunctionallyRunning = heldButtons.IsSet(PlayerControls.Sprint) || State == Enums.PowerupState.MegaMushroom || IsPropellerFlying;

            //death via pit
            if (body.position.y + transform.lossyScale.y < GameManager.Instance.LevelMinY) {
                Death(true, false);
                return;
            }

            if (IsFrozen) {
                if (!FrozenCube) {
                    Unfreeze(UnfreezeReason.Other);
                } else {
                    body.velocity = Vector2.zero;
                    return;
                }
            }

            if (HeldEntity && (IsFrozen || HeldEntity.IsDead || HeldEntity.IsFrozen))
                SetHeldEntity(null);

            if (GiantStartTimer.IsRunning) {

                body.isKinematic = true;
                body.velocity = Vector2.zero;

                if (GiantStartTimer.Expired(Runner)) {
                    FinishMegaMario(true);
                    GiantStartTimer = TickTimer.None;
                } else {
                    body.isKinematic = true;
                    if (animator.GetCurrentAnimatorClipInfo(0).Length <= 0 || animator.GetCurrentAnimatorClipInfo(0)[0].clip.name != "mega-scale")
                        animator.Play("mega-scale");


                    Vector2 checkExtents = WorldHitboxSize * new Vector2(0.375f, 0.55f);
                    Vector2 normalizedVelocity = body.velocity;
                    if (!IsGroundpounding)
                        normalizedVelocity.y = Mathf.Max(0, body.velocity.y);

                    Vector2 offset = Vector2.zero;
                    if (JumpState == PlayerJumpState.SingleJump && IsOnGround)
                        offset = Vector2.down / 2f;

                    Vector2 checkPosition = body.position + Vector2.up * checkExtents + offset;

                    Vector2Int minPos = Utils.Utils.WorldToTilemapPosition(checkPosition - checkExtents, wrap: false);
                    Vector2Int size = Utils.Utils.WorldToTilemapPosition(checkPosition + checkExtents, wrap: false) - minPos;

                    for (int x = 0; x <= size.x; x++) {
                        Vector2Int tileLocation = new(minPos.x + x, minPos.y + size.y);
                        Utils.Utils.WrapTileLocation(ref tileLocation);
                        TileBase tile = Utils.Utils.GetTileAtTileLocation(tileLocation);

                        bool cancelMega;
                        if (tile is BreakableBrickTile bbt)
                            cancelMega = !bbt.breakableByGiantMario;
                        else
                            cancelMega = Utils.Utils.IsTileSolidAtTileLocation(tileLocation);

                        if (cancelMega) {
                            FinishMegaMario(false);
                            return;
                        }
                    }
                }
                return;
            }

            if (GiantEndTimer.IsRunning && IsStationaryGiantShrink) {
                body.velocity = Vector2.zero;
                body.isKinematic = true;
                transform.position = body.position = previousTickPosition;

                if (GiantEndTimer.Expired(Runner)) {
                    DamageInvincibilityTimer = TickTimer.CreateFromSeconds(Runner, 2f);
                    body.velocity = Vector2.zero;
                    animator.enabled = true;
                    body.isKinematic = false;
                    State = PreviousState;
                    GiantEndTimer = TickTimer.None;
                }
                return;
            }

            if (State == Enums.PowerupState.MegaMushroom) {
                HandleGiantTiles(true);
                if (IsOnGround && JumpState == PlayerJumpState.SingleJump) {
                    SpawnParticle("Prefabs/Particle/GroundpoundDust", body.position);
                    CameraController.ScreenShake = 0.15f;
                    JumpState = PlayerJumpState.None;
                }
                StarmanTimer = TickTimer.None;
            }

            if (GiantTimer.Expired(Runner)) {
                EndMega();
                GiantTimer = TickTimer.None;
            }

            //pipes > stuck in block, else the animation gets janked.
            if (CurrentPipe || GiantStartTimer.IsActive(Runner) || (GiantEndTimer.IsActive(Runner) && IsStationaryGiantShrink) || animator.GetBool("pipe"))
                return;

            //don't do anything if we're stuck in a block
            if (HandleStuckInBlock())
                return;

            if (IsInKnockback) {
                if (DoEntityBounce)
                    ResetKnockback();

                WallSlideLeft = false;
                WallSlideRight = false;
                IsCrouching = false;
                IsInShell = false;
                body.velocity -= body.velocity * (delta * 2f);
                if (IsOnGround && Mathf.Abs(body.velocity.x) < 0.35f && KnockbackTimer.Expired(Runner))
                    ResetKnockback();

                AttemptThrowHeldItem();
            }

            //---HANDLE INPUTS
            bool right = heldButtons.IsSet(PlayerControls.Right);
            bool left = heldButtons.IsSet(PlayerControls.Left);
            bool down = heldButtons.IsSet(PlayerControls.Down);
            bool up = heldButtons.IsSet(PlayerControls.Up);
            bool jumpHeld = heldButtons.IsSet(PlayerControls.Jump) || SwimLeaveForceHoldJumpTime > Runner.SimulationTime;
            bool powerupAction = heldButtons.IsSet(PlayerControls.PowerupAction);

            //JUMP BUFFERING
            if (pressedButtons.IsSet(PlayerControls.Jump) && !IsOnGround) {
                //0.15s buffer time
                JumpBufferTime = Runner.SimulationTime + 0.15f;
            }

            bool jumpPressed = pressedButtons.IsSet(PlayerControls.Jump);
            bool canJump = jumpPressed || (Runner.SimulationTime <= JumpBufferTime && (IsOnGround || WallSliding));
            bool doJump = (canJump && (IsOnGround || Runner.SimulationTime <= CoyoteTime)) || (!IsSwimming && SwimJump);
            bool doWalljump = canJump && !IsOnGround && WallSliding;

            SwimJump = false;

            //GROUNDPOUND BUFFERING
            if (pressedButtons.IsSet(PlayerControls.Down)) {
                GroundpoundStartTime = Runner.SimulationTime + 0.08f;
                GroundpoundHeld = true;
            }
            //dont groundpound if we're holding another direction
            if (!down || (!IsPropellerFlying && (left || right || up)))
                GroundpoundHeld = false;

            bool doGroundpound = GroundpoundHeld && Runner.SimulationTime >= GroundpoundStartTime;

            //Pipes
            if (PipeReentryTimer.ExpiredOrNotRunning(Runner)) {
                DownwardsPipeCheck(down);
                UpwardsPipeCheck(up);
            }

            //activate blocks jumped into
            if (!IsProxy && hitRoof && !IsStuckInBlock) {
                bool tempHitBlock = false;
                bool interactedAny = false;
                foreach (Vector2Int tile in tilesJumpedInto) {
                    tempHitBlock |= InteractWithTile(tile, InteractableTile.InteractionDirection.Up, out bool interacted, out bool bumpSound);
                    if (bumpSound)
                        BlockBumpSoundCounter++;

                    interactedAny |= interacted;
                }
                if (!interactedAny) {
                    BlockBumpSoundCounter++;
                }

                body.velocity = new(body.velocity.x, Mathf.Min(body.velocity.y, IsSwimming && !tempHitBlock ? -2f : -0.1f));
            }

            if (IsDrilling) {
                PropellerSpinTimer = TickTimer.None;
                if (IsPropellerFlying) {
                    if (!down) {
                        Utils.Utils.TickTimer(ref propellerDrillBuffer, 0, Time.deltaTime);
                        if (propellerDrillBuffer <= 0)
                            IsDrilling = false;
                    } else {
                        propellerDrillBuffer = 0.15f;
                    }
                }
            }

            if (IsPropellerFlying) {
                if (PropellerLaunchTimer.IsActive(Runner)) {
                    IsSwimming = false;
                    float remainingTime = PropellerLaunchTimer.RemainingTime(Runner) ?? 0f;
                    float targetVelocity = propellerLaunchVelocity - (remainingTime < 0.4f ? (1 - (remainingTime * 2.5f)) * propellerLaunchVelocity : 0);
                    body.velocity = new(body.velocity.x, Mathf.Min(body.velocity.y + (24f * Runner.DeltaTime), targetVelocity));
                    if (IsOnGround)
                        body.position += Vector2.up * 0.05f;
                } else if (((jumpHeld && Settings.Instance.controlsPropellerJump) || powerupAction) && !IsDrilling && body.velocity.y < -0.1f && (PropellerSpinTimer.RemainingTime(Runner) ?? 0f) < propellerSpinTime * 0.25f) {
                    PropellerSpinTimer = TickTimer.CreateFromSeconds(Runner, propellerSpinTime);
                }
            }

            if (HeldEntity) {
                WallSlideLeft = false;
                WallSlideRight = false;
                SetHoldingOffset();

                // Throwing held item
                ThrowHeldItem(left, right, down);
            }

            IsWaterWalking &= State == Enums.PowerupState.MiniMushroom && (Mathf.Abs(body.velocity.x) > 0.3f || left || right);
            if (IsSwimming) {
                bool paddle = pressedButtons.IsSet(PlayerControls.Jump);
                HandleSwimming(left, right, down, paddle, jumpHeld);
                return;
            }

            if (State == Enums.PowerupState.BlueShell) {
                IsInShell &= IsFunctionallyRunning;

                if (IsInShell) {
                    down = true;

                    if (hitLeft || hitRight) {
                        bool interactedAny = false;
                        foreach (var tile in tilesHitSide) {
                            InteractWithTile(tile, InteractableTile.InteractionDirection.Up, out bool interacted, out bool bumpSound);
                            if (bumpSound)
                                BlockBumpSoundCounter++;

                            interactedAny |= interacted;
                        }
                        if (!interactedAny) {
                            BlockBumpSoundCounter++;
                        }
                        FacingRight = hitLeft;
                    }
                }
            }

            //Ground
            if (IsOnGround) {
                CoyoteTime = -1;
                if (TimeGrounded == -1)
                    TimeGrounded = Runner.SimulationTime;

                if (Runner.SimulationTime - TimeGrounded > 0.15f)
                    JumpState = PlayerJumpState.None;

                if (hitRoof && IsOnGround && crushGround && body.velocity.y <= 0.1 && State != Enums.PowerupState.MegaMushroom) {
                    //Crushed.
                    Powerdown(true);
                }

                UsedPropellerThisJump = false;
                WallSlideLeft = false;
                WallSlideRight = false;
                IsJumping = false;
                if (IsDrilling)
                    SpawnParticle("Prefabs/Particle/GroundpoundDust", body.position);

                if (OnSpinner && Mathf.Abs(body.velocity.x) < 0.3f && !HeldEntity) {
                    Transform spnr = OnSpinner.transform;
                    float diff = body.position.x - spnr.transform.position.x;
                    if (Mathf.Abs(diff) >= 0.02f)
                        body.position += -0.6f * Mathf.Sign(diff) * delta * Vector2.right;
                }
            } else {
                TimeGrounded = -1;
                IsSkidding = false;
                IsTurnaround = false;

                if (!IsJumping)
                    ProperJump = false;
            }

            //Crouching
            HandleCrouching(down);

            HandleWallslide(left, right, doWalljump);

            HandleSlopes();

            if (doGroundpound) {
                HandleGroundpoundStart(left, right);
            }

            HandleGroundpound();

            HandleSliding(up, down, left, right);

            if (IsOnGround) {
                if (IsPropellerFlying) {
                    float remainingTime = PropellerLaunchTimer.RemainingTime(Runner) ?? 0f;
                    if (remainingTime < 0.5f) {
                        IsPropellerFlying = false;
                        PropellerLaunchTimer = TickTimer.None;
                    }
                }
                IsSpinnerFlying = false;
                IsDrilling = false;
                if ((Runner.SimulationTime == TimeGrounded) && !IsGroundpounding && !IsCrouching && !IsInShell && !HeldEntity && State != Enums.PowerupState.MegaMushroom) {
                    bool edge = !Runner.GetPhysicsScene2D().BoxCast(body.position, MainHitbox.size * 0.75f, 0, Vector2.down, 0, Layers.MaskAnyGround);
                    bool edgeLanding = false;
                    if (edge) {
                        bool rightEdge = edge && Utils.Utils.IsTileSolidAtWorldLocation(body.position + new Vector2(0.25f, -0.25f));
                        bool leftEdge = edge && Utils.Utils.IsTileSolidAtWorldLocation(body.position + new Vector2(-0.25f, -0.25f));
                        edgeLanding = (leftEdge || rightEdge) && ProperJump && edge && (FacingRight == rightEdge);
                    }

                    if ((JumpState == PlayerJumpState.TripleJump && !(left ^ right))
                        || edgeLanding
                        || (Mathf.Abs(body.velocity.x) < 0.1f)) {

                        if (!OnIce)
                            body.velocity = Vector2.zero;

                        animator.Play("jumplanding" + (edgeLanding ? "-edge" : ""));
                        if (edgeLanding)
                            JumpLandingTimer = TickTimer.CreateFromSeconds(Runner, 0.15f);
                    }
                }
            }

            if (!(IsGroundpounding && !IsOnGround)) {
                //Normal walking/running
                HandleWalkingRunning(left, right);
            }

            if (!(IsGroundpounding && !IsOnGround && !DoEntityBounce)) {
                //Jumping
                HandleJumping(jumpHeld, doJump, down);
            }

            HandleSlopes();

            HandleFacingDirection(left, right);

            HandleGravity(jumpHeld);

            //Terminal velocity
            float terminalVelocityModifier = State switch {
                Enums.PowerupState.MiniMushroom => 0.625f,
                Enums.PowerupState.MegaMushroom => 2f,
                _ => 1f,
            };
            if (IsSpinnerFlying) {
                if (IsDrilling) {
                    body.velocity = new(body.velocity.x, -drillVelocity);
                } else {
                    body.velocity = new(body.velocity.x, Mathf.Max(body.velocity.y, -flyingTerminalVelocity));
                }
            } else if (IsPropellerFlying) {
                if (IsDrilling) {
                    body.velocity = new(Mathf.Clamp(body.velocity.x, -WalkingMaxSpeed, WalkingMaxSpeed), -drillVelocity);
                } else {
                    float remainingTime = PropellerLaunchTimer.RemainingTime(Runner) ?? 0f;
                    float htv = WalkingMaxSpeed * 1.18f + (remainingTime * 2f);
                    body.velocity = new(Mathf.Clamp(body.velocity.x, -htv, htv), Mathf.Max(body.velocity.y, PropellerSpinTimer.IsActive(Runner) ? -propellerSpinFallSpeed : -propellerFallSpeed));
                }
            } else if (WallSliding) {
                body.velocity = new(body.velocity.x, Mathf.Max(body.velocity.y, wallslideSpeed));
            } else if (IsGroundpounding) {
                body.velocity = new(body.velocity.x, Mathf.Max(body.velocity.y, -groundpoundVelocity));
            } else {
                body.velocity = new(body.velocity.x, Mathf.Max(body.velocity.y, terminalVelocity * terminalVelocityModifier));
            }

            if (IsCrouching || IsSliding || IsSkidding) {
                WallSlideLeft = false;
                WallSlideRight = false;
            }

            if (previousTickIsOnGround && !IsOnGround && !ProperJump && IsCrouching && !IsInShell && !IsGroundpounding)
                body.velocity = new(body.velocity.x, -3.75f);
        }

        private void HandleGravity(bool jumpHeld) {

            if ((IsGroundpounding || IsDrilling) && IsSwimming) {
                return;
            }

            if (IsOnGround)
                return;

            float gravity = 0;

            //slow-rise check
            if (IsSpinnerFlying || IsPropellerFlying) {
                gravity = flyingGravity * Physics2D.gravity.y;
            } else {
                if (IsGroundpounding) {
                    if (GroundpoundStartTimer.IsActive(Runner)) {
                        gravity = 0.15f;
                    } else {
                        gravity = GRAVITY_STAGE_ACC[^1];
                    }
                } else if (IsOnGround || (Runner.SimulationTime <= CoyoteTime - 0.02f)) {
                    gravity = 0.15f;
                } else {
                    int stage = GravityStage;
                    bool mega = State == Enums.PowerupState.MegaMushroom;
                    bool mini = State == Enums.PowerupState.MiniMushroom;

                    float?[] maxArr = IsSwimming ? GRAVITY_SWIM_MAX : (mega ? GRAVITY_MEGA_MAX : (mini ? GRAVITY_MINI_MAX : GRAVITY_STAGE_MAX));
                    float[] accArr = IsSwimming ? GRAVITY_SWIM_ACC : (mega ? GRAVITY_MEGA_ACC : (mini ? GRAVITY_MINI_ACC : GRAVITY_STAGE_ACC));

                    float acc = accArr[stage];
                    if (maxArr[stage] == null)
                        acc = (jumpHeld || IsSwimming) ? accArr[0] : accArr[^1];

                    gravity = acc;
                }
            }

            body.velocity += Vector2.up * gravity * Runner.DeltaTime;
        }

        private void HandleSwimming(bool left, bool right, bool down, bool jumpPressed, bool jumpHeld) {

            if (IsGroundpounding || IsDrilling) {
                body.velocity = new(body.velocity.x, body.velocity.y + (SWIM_GROUNDPOUND_DEC * Runner.DeltaTime));
                if (body.velocity.y >= SWIM_TERMINAL_VELOCITY_AHELD) {
                    IsGroundpounding = false;
                    IsDrilling = false;
                }
            }

            IsDrilling = false;
            WallSlideLeft = false;
            WallSlideRight = false;
            IsSliding = false;
            IsSkidding = false;
            IsTurnaround = false;
            IsSpinnerFlying = false;
            IsInShell = false;
            IsJumping = false;
            JumpState = PlayerJumpState.None;

            if (IsInKnockback) {
                jumpHeld = false;
            } else {
                HandleWalkingRunning(left, right);

                if (DoEntityBounce) {
                    body.velocity = new(body.velocity.x, SWIM_VSPEED);
                    DoEntityBounce = false;
                    IsOnGround = false;
                    IsCrouching = false;
                }

                if (jumpPressed) {
                    body.velocity = new(body.velocity.x, body.velocity.y + SWIM_VSPEED);
                    if (IsOnGround)
                        body.position += Vector2.up * 0.05f;

                    JumpAnimCounter++;
                    JumpBufferTime = -1;

                    IsOnGround = false;
                    IsCrouching = false;
                }
            }

            HandleSlopes();

            HandleFacingDirection(left, right);

            HandleGravity(jumpHeld);

            HandleCrouching(down);

            if (!(IsGroundpounding || IsDrilling))
                body.velocity = new(body.velocity.x, Mathf.Clamp(body.velocity.y, jumpHeld ? SWIM_TERMINAL_VELOCITY_AHELD : SWIM_TERMINAL_VELOCITY, SWIM_MAX_VSPEED));
        }

        private void SetHoldingOffset() {
            if (HeldEntity is FrozenCube) {
                float time = Mathf.Clamp01((Runner.SimulationTime - HoldStartTime) / pickupTime);
                HeldEntity.holderOffset = new(0, MainHitbox.size.y * (1f - Utils.Utils.QuadraticEaseOut(1f - time)), -2);
            } else {
                HeldEntity.holderOffset = new((FacingRight ? 1 : -1) * 0.25f, (State >= Enums.PowerupState.Mushroom ? 0.3f : 0.075f) - HeldEntity.sRenderer.localBounds.min.y, !FacingRight ? -0.09f : 0f);
            }
        }

        private void ThrowHeldItem(bool left, bool right, bool crouch) {
            if (IsFunctionallyRunning && State != Enums.PowerupState.MiniMushroom && State != Enums.PowerupState.MegaMushroom && !IsStarmanInvincible && !IsSpinnerFlying && !IsPropellerFlying)
                return;

            if (HeldEntity is FrozenCube && !IsSwimming && (Runner.SimulationTime - HoldStartTime) < pickupTime)
                return;

            bool throwRight = FacingRight;
            if (left ^ right)
                throwRight = right;

            crouch &= HeldEntity.canPlace;
            crouch &= IsOnGround;

            AttemptThrowHeldItem(throwRight, crouch);

            if (!crouch && !IsInKnockback) {
                ThrowAnimCounter++;
            }
        }

        private void HandleGroundpoundStart(bool left, bool right) {

            if (IsOnGround || IsInKnockback || IsGroundpounding || IsDrilling
                || HeldEntity || IsCrouching || IsSliding || IsInShell
                || WallSliding || GroundpoundCooldownTimer.IsActive(Runner))
                return;

            if (!IsPropellerFlying && !IsSpinnerFlying && (left || right))
                return;

            if (IsSpinnerFlying) {
                // Start drill
                if (body.velocity.y < 0) {
                    IsDrilling = true;
                    ContinueGroundpound = true;
                    body.velocity = new(0, body.velocity.y);
                }
            } else if (IsPropellerFlying) {
                // Start propeller drill
                float remainingTime = PropellerLaunchTimer.RemainingTime(Runner) ?? 0f;
                if (remainingTime < 0.6f && body.velocity.y < 4) {
                    IsDrilling = true;
                    PropellerLaunchTimer = TickTimer.None;
                    ContinueGroundpound = true;
                }
            } else {
                // Start groundpound
                // Check if high enough above ground
                if (Runner.GetPhysicsScene().BoxCast(body.position, WorldHitboxSize * Vector2.right * 0.5f, Vector3.down, out _, Quaternion.identity, 0.15f * (State == Enums.PowerupState.MegaMushroom ? 2.5f : 1), Layers.MaskAnyGround))
                    return;

                WallSlideLeft = false;
                WallSlideRight = false;
                IsGroundpounding = true;
                JumpState = PlayerJumpState.None;
                ContinueGroundpound = true;
                IsSliding = false;
                body.velocity = Vector2.up * 1.5f;
                GroundpoundHeld = false;
                GroundpoundStartTimer = TickTimer.CreateFromSeconds(Runner, groundpoundTime * (State == Enums.PowerupState.MegaMushroom ? 1.5f : 1));
            }
        }

        private void HandleGroundpound() {
            if (IsGroundpounding && GroundpoundStartTimer.IsActive(Runner)) {
                if (GroundpoundStartTimer.RemainingTime(Runner) <= .066f) {
                    body.velocity = Vector2.zero;
                } else {
                    body.velocity = Vector2.up * 1.5f;
                }
            }

            if (IsGroundpounding && GroundpoundStartTimer.Expired(Runner)) {
                body.velocity = Vector2.down * groundpoundVelocity;
                GroundpoundStartTimer = TickTimer.None;
            }

            if (!(IsOnGround && (IsGroundpounding || IsDrilling) && ContinueGroundpound))
                return;

            if (!IsDrilling)
                GroundpoundAnimCounter++;

            ContinueGroundpound = false;
            foreach (Vector2Int tile in tilesStandingOn) {
                ContinueGroundpound |= InteractWithTile(tile, InteractableTile.InteractionDirection.Down, out bool _, out bool bumpSound);
                if (bumpSound)
                    BlockBumpSoundCounter++;
            }

            if (IsDrilling) {
                IsSpinnerFlying &= ContinueGroundpound;
                IsPropellerFlying &= ContinueGroundpound;
                IsDrilling = ContinueGroundpound;
                if (ContinueGroundpound)
                    IsOnGround = false;
            }
        }

        //---OnChangeds
        public static void OnGroundpoundingChanged(Changed<PlayerController> changed) {
            PlayerController player = changed.Behaviour;
            if (!player.IsGroundpounding)
                return;

            player.PlaySound(Enums.Sounds.Player_Sound_GroundpoundStart);
        }

        private byte previousGroundpoundCounter;
        public static void OnGroundpoundAnimCounterChanged(Changed<PlayerController> changed) {
            PlayerController player = changed.Behaviour;

            //if (player.previousGroundpoundCounter - 1 >= player.GroundpoundAnimCounter) {
            //    return;
            //}
            //player.previousGroundpoundCounter = player.GroundpoundAnimCounter;

            //groundpound
            if (player.State != Enums.PowerupState.MegaMushroom) {
                //GroundpoundStartTimer = TickTimer.CreateFromSeconds(Runner, 0.2f);
                Enums.Sounds sound = player.State switch {
                    Enums.PowerupState.MiniMushroom => Enums.Sounds.Powerup_MiniMushroom_Groundpound,
                    _ => Enums.Sounds.Player_Sound_GroundpoundLanding,
                };
                player.PlaySound(sound);
                player.SpawnParticle(PrefabList.Instance.Particle_Groundpound, player.body.position);
            } else {
                CameraController.ScreenShake = 0.15f;
            }
            if (!player.ContinueGroundpound && player.State == Enums.PowerupState.MegaMushroom) {
                player.PlaySound(Enums.Sounds.Powerup_MegaMushroom_Groundpound);
                player.SpawnParticle(PrefabList.Instance.Particle_Groundpound, player.body.position);
                CameraController.ScreenShake = 0.35f;
            }
        }

        public static void OnWallJumpTimerChanged(Changed<PlayerController> changed) {
            PlayerController player = changed.Behaviour;

            if (!player.WallJumpTimer.IsRunning)
                return;

            Vector2 offset = player.MainHitbox.size * 0.5f;
            changed.LoadOld();
            offset.x *= changed.Behaviour.WallSlideLeft ? -1 : 1;

            player.PlaySound(Enums.Sounds.Player_Sound_WallJump);
            player.PlaySound(Enums.Sounds.Player_Voice_WallJump, (byte) GameData.Instance.Random.RangeExclusive(1, 3));
            player.SpawnParticle(PrefabList.Instance.Particle_Walljump, player.body.position + offset, player.WallSlideLeft ? Quaternion.identity : Quaternion.Euler(0, 180, 0));

            player.animator.SetTrigger("walljump");

            changed.LoadNew();
        }

        private float lastRespawnParticle;
        public static void OnIsDeadChanged(Changed<PlayerController> changed) {
            PlayerController player = changed.Behaviour;
            if (player.IsDead) {
                if (GameData.Instance.GameState < Enums.GameState.Playing)
                    return;

                player.animator.Play("deadstart");
                player.animator.SetBool("knockback", false);
                player.animator.SetBool("flying", false);
                player.animator.SetBool("firedeath", player.FireDeath);
                player.PlaySound(player.cameraController.IsControllingCamera ? Enums.Sounds.Player_Sound_Death : Enums.Sounds.Player_Sound_DeathOthers);

                if (player.Object.HasInputAuthority)
                    ScoreboardUpdater.Instance.OnDeathToggle();
            } else {
                //respawn poof particle
                if (Mathf.Abs(player.lastRespawnParticle - player.Runner.SimulationTime) > 2) {
                    GameManager.Instance.particleManager.Play(Enums.Particle.Generic_Puff, player.Spawnpoint);
                    player.lastRespawnParticle = player.Runner.SimulationTime;
                }

                player.animator.SetTrigger("respawn");
            }
        }

        private RespawnParticle respawnParticle;
        public static void OnIsRespawningChanged(Changed<PlayerController> changed) {
            PlayerController player = changed.Behaviour;
            if (!player.IsRespawning || player.respawnParticle)
                return;

            player.respawnParticle = Instantiate(PrefabList.Instance.Particle_Respawn, player.Spawnpoint, Quaternion.identity);
            player.respawnParticle.player = player;
        }

        public static void OnFireballAnimCounterChanged(Changed<PlayerController> changed) {
            PlayerController player = changed.Behaviour;
            player.animator.SetTrigger("fireball");
            player.sfx.PlayOneShot(player.State == Enums.PowerupState.IceFlower ? Enums.Sounds.Powerup_Iceball_Shoot : Enums.Sounds.Powerup_Fireball_Shoot);
        }

        public static void OnIsSlidingChanged(Changed<PlayerController> changed) {
            PlayerController player = changed.Behaviour;
            if (player.IsSliding)
                return;

            if (!player.IsOnGround || Mathf.Abs(player.body.velocity.x) > 0.1f)
                return;

            player.PlaySound(Enums.Sounds.Player_Sound_SlideEnd);
        }

        public static void OnIsCrouchingChanged(Changed<PlayerController> changed) {
            PlayerController player = changed.Behaviour;
            if (!player.IsCrouching)
                return;

            player.PlaySound(player.State == Enums.PowerupState.BlueShell ? Enums.Sounds.Powerup_BlueShell_Enter : Enums.Sounds.Player_Sound_Crouch);
        }

        public static void OnPipeTimerChanged(Changed<PlayerController> changed) {
            PlayerController player = changed.Behaviour;
            if (!player.PipeTimer.IsRunning)
                return;

            player.PlaySound(Enums.Sounds.Player_Sound_Powerdown);
        }

        public static void OnPropellerLaunchTimerChanged(Changed<PlayerController> changed) {
            PlayerController player = changed.Behaviour;
            if (!player.PropellerLaunchTimer.IsRunning)
                return;

            player.PlaySound(Enums.Sounds.Powerup_PropellerMushroom_Start);
        }

        public static void OnPropellerSpinTimerChanged(Changed<PlayerController> changed) {
            PlayerController player = changed.Behaviour;
            if (!player.PropellerSpinTimer.IsRunning)
                return;

            player.PlaySound(Enums.Sounds.Powerup_PropellerMushroom_Spin);
        }

        public static void OnSpinnerLaunchAnimCounterChanged(Changed<PlayerController> changed) {
            PlayerController player = changed.Behaviour;
            if (!player.SpinnerLaunchAnimCounter)
                return;

            player.PlaySound(Enums.Sounds.Player_Voice_SpinnerLaunch);
            player.PlaySound(Enums.Sounds.World_Spinner_Launch);
        }

        public static void OnJumpAnimCounterChanged(Changed<PlayerController> changed) {
            PlayerController player = changed.Behaviour;
            if (player.IsSwimming) {
                // Paddle
                player.PlaySound(Enums.Sounds.Player_Sound_Swim);
                player.animator.SetTrigger("paddle");
                return;
            }

            if (!player.IsJumping)
                return;

            // Voice SFX
            switch (player.JumpState) {
            case PlayerJumpState.DoubleJump:
                player.PlaySound(Enums.Sounds.Player_Voice_DoubleJump, (byte) GameData.Instance.Random.RangeExclusive(1, 3));
                break;
            case PlayerJumpState.TripleJump:
                player.PlaySound(Enums.Sounds.Player_Voice_TripleJump);
                break;
            }

            if (player.BounceJump) {
                player.PlaySound(Enums.Sounds.Enemy_Generic_Stomp);
                return;
            }

            // Jump SFX
            Enums.Sounds sound = player.State switch {
                Enums.PowerupState.MiniMushroom => Enums.Sounds.Powerup_MiniMushroom_Jump,
                Enums.PowerupState.MegaMushroom => Enums.Sounds.Powerup_MegaMushroom_Jump,
                _ => Enums.Sounds.Player_Sound_Jump,
            };
            player.PlaySound(sound);
        }

        public static void OnIsWaterWalkingChanged(Changed<PlayerController> changed) {
            PlayerController player = changed.Behaviour;
            if (!player.IsWaterWalking)
                return;

            player.PlaySound(Enums.Sounds.Powerup_MiniMushroom_WaterWalk);
        }

        public static void OnIsInKnockbackChanged(Changed<PlayerController> changed) {
            PlayerController player = changed.Behaviour;
            if (!player.IsInKnockback)
                return;

            if (player.KnockbackAttacker)
                player.SpawnParticle("Prefabs/Particle/PlayerBounce", player.KnockbackAttacker.transform.position);

            player.PlaySound(player.IsWeakKnockback ? Enums.Sounds.Player_Sound_Collision_Fireball : Enums.Sounds.Player_Sound_Collision, 0, 3);
        }

        public static void OnPowerupStateChanged(Changed<PlayerController> changed) {
            PlayerController player = changed.Behaviour;

            if (player.IsDead || player.IsRespawning)
                return;

            Enums.PowerupState previous = player.PreviousState;
            Enums.PowerupState current = player.State;

            // Don't worry about Mega Mushrooms.
            if (previous == Enums.PowerupState.MegaMushroom)
                return;

            // We've taken damage when we go from > mushroom to mushroom, or mushroom to no powerup
            if ((previous > Enums.PowerupState.Mushroom && current == Enums.PowerupState.Mushroom)
                || (previous == Enums.PowerupState.Mushroom && current == Enums.PowerupState.NoPowerup)) {
                // Taken damage
                player.PlaySound(Enums.Sounds.Player_Sound_Powerdown);
            } else {
                // Collected powerup is handled in the MovingPowerup class.
                // (because the sound is powerup dependent)
            }
        }

        private float timeSinceLastBumpSound;
        public static void OnBlockBumpSoundCounterChanged(Changed<PlayerController> changed) {
            PlayerController player = changed.Behaviour;

            if (player.timeSinceLastBumpSound + 0.2f > player.Runner.SimulationRenderTime)
                return;

            player.PlaySound(Enums.Sounds.World_Block_Bump);
            player.timeSinceLastBumpSound = player.Runner.SimulationRenderTime;
        }

        public static void OnLivesChanged(Changed<PlayerController> changed) {
            PlayerController player = changed.Behaviour;

            if (GameData.Instance.GameState < Enums.GameState.Playing || player.Disconnected)
                return;

            changed.LoadOld();
            sbyte previous = player.Lives;
            changed.LoadNew();

            if (player.Lives > previous) {
                player.PlaySound(Enums.Sounds.Powerup_Sound_1UP);
            }
        }

        public static void OnThrowAnimCounterChanged(Changed<PlayerController> changed) {
            PlayerController player = changed.Behaviour;

            player.PlaySound(Enums.Sounds.Player_Voice_WallJump, 2);
            player.animator.SetTrigger("throw");
        }

        public static void OnGiantTimerChanged(Changed<PlayerController> changed) {
            PlayerController player = changed.Behaviour;

            player.PlaySoundEverywhere(player.GiantTimer.IsRunning ? Enums.Sounds.Player_Voice_MegaMushroom : Enums.Sounds.Powerup_MegaMushroom_End);
        }

        public static void OnIsStationaryGiantShrinkChanged(Changed<PlayerController> changed) {
            PlayerController player = changed.Behaviour;

            if (!player.IsStationaryGiantShrink)
                return;

            player.animator.enabled = true;
            player.animator.Play("mega-cancel", 0, 1f - ((player.GiantEndTimer.RemainingTime(player.Runner) ?? 0f) / player.giantStartTime));
            player.PlaySound(Enums.Sounds.Player_Sound_PowerupReserveStore);
        }

        public override void OnIsFrozenChanged() {
            animator.enabled = !IsFrozen;
        }

        public static void OnHeldEntityChanged(Changed<PlayerController> changed) {
            PlayerController player = changed.Behaviour;

            if (!player.HeldEntity)
                return;

            if (player.HeldEntity is FrozenCube) {
                player.animator.Play("head-pickup");
                player.animator.ResetTrigger("fireball");
                player.PlaySound(Enums.Sounds.Player_Voice_DoubleJump, 2);
            }
            player.animator.ResetTrigger("throw");
        }

        //---Debug
#if UNITY_EDITOR
        private readonly List<Renderer> renderers = new();
        public void OnDrawGizmos() {
            if (!body)
                return;

            Gizmos.color = Color.white;
            Gizmos.DrawRay(body.position, body.velocity);
            Gizmos.DrawCube(body.position + new Vector2(0, WorldHitboxSize.y * 0.5f) + (body.velocity * Runner.DeltaTime), WorldHitboxSize);

            if (renderers.Count == 0) {
                GetComponentsInChildren(true, renderers);
                renderers.RemoveAll(r => r is ParticleSystemRenderer);
            }

            foreach (Renderer r in renderers)
                Gizmos.DrawWireCube(r.bounds.center, r.bounds.size);
        }
#endif

        public enum PlayerJumpState : byte {
            None,
            SingleJump,
            DoubleJump,
            TripleJump,
        }
    }
}
