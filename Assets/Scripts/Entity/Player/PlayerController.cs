using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using UnityEngine.Serialization;

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
        private static readonly List<GameObject> CollidedObjects = new(16);
        private static readonly List<LagCompensatedHit> LagCompensatedBuffer = new(16);
        private static readonly Vector3 ZeroPointFive = Vector3.one * 0.5f;

        private static readonly Vector2 GroundpoundStartUpwardsVelocity = Vector2.up * 1.5f;

        //---Networked Variables
        //-Player State
        [Networked] public Enums.PowerupState State { get; set; }
        [Networked] public Enums.PowerupState PreviousState { get; set; }
        [Networked] public Enums.PowerupState StoredPowerup { get; set; }
        [Networked] public byte Stars { get; set; }
        [Networked] public byte Coins { get; set; }
        [Networked] public sbyte Lives { get; set; }
        [Networked] private sbyte SpawnpointIndex { get; set; }
        //-Player Movement
        //Generic
        [Networked] public PlayerNetworkInput PreviousInputs { get; set; }
        [Networked] private int LastInputTick { get; set; }
        [Networked] public NetworkBool IsFunctionallyRunning { get; set; }
        [Networked] public NetworkBool IsOnGround { get; set; }
        [Networked] private NetworkBool PreviousTickIsOnGround { get; set; }
        [Networked] public NetworkBool IsCrouching { get; set; }
        [Networked] public NetworkBool IsSliding { get; set; }
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
        [Networked] private byte JumpAnimCounter { get; set; }
        [Networked] private byte JumpLandingAnimCounter { get; set; }
        [Networked] public NetworkBool IsJumping { get; set; }
        [Networked] public PlayerJumpState JumpState { get; set; }
        [Networked] public NetworkBool ProperJump { get; set; }
        [Networked] public NetworkBool DoEntityBounce { get; set; }
        [Networked] public NetworkBool BounceJump { get; set; }
        [Networked] public TickTimer JumpLandingTimer { get; set; }
        [Networked] public byte BlockBumpSoundCounter { get; set; }
        //Knockback
        [Networked] public NetworkBool IsInKnockback { get; set; }
        [Networked] private byte KnockbackAnimCounter { get; set; }
        [Networked] public NetworkBool IsWeakKnockback { get; set; }
        [Networked] public NetworkBool IsForwardsKnockback { get; set; }
        [Networked] private NetworkBool KnockbackWasOriginallyFacingRight { get; set; }
        [Networked] public int KnockbackTick { get; set; }
        [Networked] public NetworkObject KnockbackAttacker { get; set; }
        //Groundpound
        [Networked] public byte GroundpoundAnimCounter { get; set; }
        [Networked] public NetworkBool IsGroundpounding { get; set; }
        [Networked] public TickTimer GroundpoundStartTimer { get; set; }
        [Networked] public TickTimer GroundpoundCooldownTimer { get; set; }
        [Networked] private NetworkBool ContinueGroundpound { get; set; }
        //Spinner
        [Networked] public SpinnerAnimator OnSpinner { get; set; }
        [Networked] public NetworkBool IsSpinnerFlying { get; set; }
        [Networked] public byte SpinnerLaunchAnimCounter { get; set; }
        [Networked] public NetworkBool IsDrilling { get; set; }
        [Networked] public byte DustParticleAnimCounter { get; set; }
        //Pipes
        [Networked] public Vector2 PipeDirection { get; set; }
        [Networked] public PipeManager CurrentPipe { get; set; }
        [Networked] public NetworkBool PipeEntering { get; set; }
        [Networked] public TickTimer PipeTimer { get; set; }
        [Networked] public TickTimer PipeReentryTimer { get; set; }
        //Walljump
        [Networked] public TickTimer WallJumpTimer { get; set; }
        [Networked] public TickTimer WallSlideEndTimer { get; set; }
        [Networked] public NetworkBool WallSlideLeft { get; set; }
        [Networked] public NetworkBool WallSlideRight { get; set; }
        //Stuck
        [Networked] public NetworkBool IsStuckInBlock { get; set; }
        //Swimming
        [Networked] public NetworkBool SwimJump { get; set; }
        [Networked] public float SwimLeaveForceHoldJumpTime { get; set; }
        [Networked] public NetworkBool IsSwimming { get; set; }
        [Networked] public NetworkBool IsWaterWalking { get; set; }
        //-Death & Respawning
        [Networked] public NetworkBool Disconnected { get; set; }
        [Networked] public NetworkBool IsDead { get; set; }
        [Networked] public TickTimer DeathAnimationTimer { get; set; }
        [Networked] public NetworkBool IsRespawning { get; set; }
        [Networked] public NetworkBool FireDeath { get; set; }
        [Networked] public NetworkBool DeathplaneDeath { get; set; }
        [Networked] public TickTimer RespawnTimer { get; set; }
        [Networked] public TickTimer PreRespawnTimer { get; set; }

        //-Entity Interactions
        [Networked] public HoldableEntity HeldEntity { get; set; }
        [Networked] public byte ThrowAnimCounter { get; set; }
        [Networked] public float HoldStartTime { get; set; }
        [Networked] public TickTimer ShellSlowdownTimer { get; set; }
        [Networked] public TickTimer DamageInvincibilityTimer { get; set; }
        [Networked] private byte _StarCombo { get; set; }

        //-Powerup Stuffs
        [Networked] private byte FireballAnimCounter { get; set; }
        [Networked] public TickTimer FireballShootTimer { get; set; }
        [Networked] public TickTimer FireballDelayTimer { get; set; }
        [Networked] public NetworkBool CanShootAdditionalFireball { get; set; }
        [Networked] public TickTimer StarmanTimer { get; set; }
        [Networked] public NetworkBool IsPropellerFlying { get; set; }
        [Networked] public TickTimer PropellerLaunchTimer { get; set; }
        [Networked] public TickTimer PropellerSpinTimer { get; set; }
        [Networked] private TickTimer PropellerDrillCooldown { get; set; }
        [Networked] public NetworkBool UsedPropellerThisJump { get; set; }
        [Networked] public TickTimer MegaStartTimer { get; set; }
        [Networked] public TickTimer MegaTimer { get; set; }
        [Networked] public TickTimer MegaEndTimer { get; set; }
        [Networked] private bool IsStationaryMegaShrink { get; set; }
        [Networked] public NetworkBool IsInShell { get; set; }
        [Networked] public FrozenCube FrozenCube { get; set; }

        //---Properties
        public override bool IsFlying => IsSpinnerFlying || IsPropellerFlying; //doesn't work consistently?
        public override bool IsCarryable => true;
        public override Vector2 FrozenSize => State < Enums.PowerupState.Mushroom ? smallFrozenCubeSize : largeFrozenCubeSize;
        public override Vector2 FrozenOffset => Vector2.up * 0.1f;
        public bool HitLeft => body.Data.HitLeft;
        public bool HitRight => body.Data.HitRight;
        public bool WallSliding => WallSlideLeft || WallSlideRight;
        public bool InstakillsEnemies => IsStarmanInvincible || IsInShell || (IsSliding && Mathf.Abs(body.Velocity.x) > 0.1f) || State == Enums.PowerupState.MegaMushroom;
        public bool IsCrouchedInShell => State == Enums.PowerupState.BlueShell && IsCrouching && !IsInShell;
        public bool IsStarmanInvincible => StarmanTimer.IsActive(Runner);
        public bool IsDamageable => !IsStarmanInvincible && DamageInvincibilityTimer.ExpiredOrNotRunning(Runner);
        public int PlayerId => Data.PlayerId;
        public bool CanPickupItem => !FrozenCube && State != Enums.PowerupState.MiniMushroom && !IsSkidding && !IsTurnaround && !HeldEntity && PreviousInputs.buttons.IsSet(PlayerControls.Sprint) && !IsPropellerFlying && !IsSpinnerFlying && !IsCrouching && !IsDead && !WallSlideLeft && !WallSlideRight && JumpState < PlayerJumpState.DoubleJump && !IsGroundpounding && !(!HeldEntity && IsSwimming && PreviousInputs.buttons.IsSet(PlayerControls.Jump));
        public bool HasGroundpoundHitbox => (IsDrilling || (IsGroundpounding && GroundpoundStartTimer.ExpiredOrNotRunning(Runner))) && (!IsOnGround || (Runner.SimulationTime - TimeGrounded < 0.15f));
        public float RunningMaxSpeed => SPEED_STAGE_MAX[RUN_STAGE];
        public float WalkingMaxSpeed => SPEED_STAGE_MAX[WALK_STAGE];
        public BoxCollider2D MainHitbox => hitboxes[0];
        public Vector2 WorldHitboxSize => MainHitbox.size * transform.lossyScale;
        public Vector3 Spawnpoint => GameManager.Instance.GetSpawnpoint(SpawnpointIndex);
        private int MovementStage {
            get {
                float xVel = Mathf.Abs(body.Velocity.x) - 0.01f;
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
                float yVel = body.Velocity.y;
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
        public PlayerData Data { get; private set; }

        //---Components
        [SerializeField] public CameraController cameraController;
        [SerializeField] public PlayerAnimationController animationController;
        [SerializeField] private Animator animator;
        private BoxCollider2D[] hitboxes;

        //---Public Variables
        [HideInInspector] public float powerupFlash;

        //---Serialized Variables
        [SerializeField] private Vector2 smallFrozenCubeSize, largeFrozenCubeSize;
        [SerializeField] public float flyingGravity = 0.8f, flyingTerminalVelocity = 1.25f, drillVelocity = 7f, groundpoundTime = 0.25f, groundpoundVelocity = 10, blinkingSpeed = 0.25f, terminalVelocity = -7f, launchVelocity = 12f, wallslideSpeed = -4.25f, soundRange = 10f, slopeSlidingAngle = 12.5f, pickupTime = 0.5f;
        [SerializeField, FormerlySerializedAs("giantStartTime")] public float megaStartTime = 1.5f;
        [SerializeField] public float propellerLaunchVelocity = 6, propellerFallSpeed = 2, propellerSpinFallSpeed = 1.5f, propellerSpinTime = 0.75f, heightSmallModel = 0.42f, heightLargeModel = 0.82f;
        [SerializeField] public GameObject models;
        [SerializeField] public CharacterData character;

        //---Private Variables
        private Enums.Sounds footstepSound = Enums.Sounds.Player_Walk_Grass;
        private Enums.Particle footstepParticle = Enums.Particle.None;
        private bool footstepVariant;
        private PlayerNetworkInput onInputPreviousInputs;

        private int noLivesStarSpawnDirection;

        #region //---MOVEMENT STAGES & CONSTANTS
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

        #endregion

        #region Unity Methods

        public override void OnValidate() {
            base.OnValidate();

            cameraController = GetComponentInChildren<CameraController>();
            animator = GetComponentInChildren<Animator>();
            animationController = GetComponent<PlayerAnimationController>();
        }

        public void OnDisable() {
            ControlSystem.controls.Player.ReserveItem.performed -= OnReserveItem;
            NetworkHandler.OnInput -= OnInput;
        }

        public void OnBeforeSpawned(int spawnpoint) {
            SpawnpointIndex = (sbyte) spawnpoint;
        }

        public void BeforeTick() {
            HandleLayerState();
            IsOnGround = GroundSnapCheck();
        }

        public override void Spawned() {
            Runner.SetIsSimulated(Object, true);
            hitboxes = GetComponentsInChildren<BoxCollider2D>();

            body.Freeze = true;

            Data = Object.InputAuthority.GetPlayerData(Runner);
            if (HasInputAuthority) {
                //body.InterpolationDataSource = InterpolationDataSources.Predicted;

                GameManager.Instance.localPlayer = this;
                GameManager.Instance.spectationManager.Spectating = false;
                ControlSystem.controls.Player.ReserveItem.performed += OnReserveItem;
                NetworkHandler.OnInput += OnInput;
                NetworkHandler.Runner.ProvideInput = true;
            }

            if (FirstSpawn) {
                Lives = SessionData.Instance.Lives;
                Vector3 spawn = GameManager.Instance.GetSpawnpoint(SpawnpointIndex);
                body.Position = spawn;
                cameraController.Recenter(spawn);
            }

            cameraController.IsControllingCamera = HasInputAuthority;

            if (!GameManager.Instance.AlivePlayers.Contains(this)) {
                GameManager.Instance.AlivePlayers.Add(this);
            }

            ControlSystem.controls.Enable();

            base.Spawned();
        }

        public override void Despawned(NetworkRunner runner, bool hasState) {
            if (HasInputAuthority) {
                NetworkHandler.OnInput -= OnInput;
                NetworkHandler.Runner.ProvideInput = false;
            }

            if (GameManager.Instance && hasState)
                GameManager.Instance.AlivePlayers.Remove(this);
        }

        public override void Render() {
            base.Render();

            if (GameManager.Instance.GameState < Enums.GameState.Playing) {
                models.SetActive(false);
                return;
            }

            if (GameManager.Instance.GameEnded) {
                animator.enabled = false;
                return;
            }

            HandleLayerState();

            if (HeldEntity)
                SetHoldingOffset(true);

            if (cameraController.IsControllingCamera && PreRespawnTimer.IsRunning) {
                float timeTillRespawn = PreRespawnTimer.RemainingRenderTime(Runner) ?? 0f;
                if (timeTillRespawn < 0.43f && timeTillRespawn + Time.deltaTime > 0.43f) {
                    // Play fade out
                    GameManager.Instance.fadeManager.FadeOutAndIn(0.33f, 0.1f);
                }
            }
        }

        [Networked] private int UpHeldStart { get; set; }
        [Networked] private int DownHeldStart { get; set; }
        [Networked] private int LeftHeldStart { get; set; }
        [Networked] private int RightHeldStart { get; set; }
        [Networked] private int JumpHeldStart { get; set; }

        private void HandleButtonHolding(PlayerNetworkInput newInputs) {
            if (newInputs.buttons.WasPressed(PreviousInputs.buttons, PlayerControls.Up)) {
                UpHeldStart = Runner.Tick;
            } else if (!newInputs.buttons.IsSet(PlayerControls.Up)) {
                UpHeldStart = -1;
            }

            if (newInputs.buttons.WasPressed(PreviousInputs.buttons, PlayerControls.Down)) {
                DownHeldStart = Runner.Tick;
            } else if (!newInputs.buttons.IsSet(PlayerControls.Down)) {
                DownHeldStart = -1;
            }

            if (newInputs.buttons.WasPressed(PreviousInputs.buttons, PlayerControls.Left)) {
                LeftHeldStart = Runner.Tick;
            } else if (!newInputs.buttons.IsSet(PlayerControls.Left)) {
                LeftHeldStart = -1;
            }

            if (newInputs.buttons.WasPressed(PreviousInputs.buttons, PlayerControls.Right)) {
                RightHeldStart = Runner.Tick;
            } else if (!newInputs.buttons.IsSet(PlayerControls.Right)) {
                RightHeldStart = -1;
            }

            if (newInputs.buttons.WasPressed(PreviousInputs.buttons, PlayerControls.Jump)) {
                JumpHeldStart = Runner.Tick;
            } else if (!newInputs.buttons.IsSet(PlayerControls.Jump)) {
                JumpHeldStart = -1;
            }
        }

        private PlayerNetworkInput HandleMissingInputs() {
            if ((Runner.Tick - LastInputTick) > Runner.TickRate * 0.25f)
                return default;

            PlayerNetworkInput inputs = PreviousInputs;
            inputs.buttons.Set(PlayerControls.Up, inputs.buttons.IsSet(PlayerControls.Up) && UpHeldStart != -1 && (UpHeldStart == Runner.Tick || (Runner.Tick - UpHeldStart) > Runner.TickRate * 0.1f));
            inputs.buttons.Set(PlayerControls.Down, inputs.buttons.IsSet(PlayerControls.Down) && DownHeldStart != -1 && (DownHeldStart == Runner.Tick || (Runner.Tick - DownHeldStart) > Runner.TickRate * 0.1f));
            inputs.buttons.Set(PlayerControls.Left, inputs.buttons.IsSet(PlayerControls.Left) && LeftHeldStart != -1 && (LeftHeldStart == Runner.Tick || (Runner.Tick - LeftHeldStart) > Runner.TickRate * 0.1f));
            inputs.buttons.Set(PlayerControls.Right, inputs.buttons.IsSet(PlayerControls.Right) && RightHeldStart != -1 && (RightHeldStart == Runner.Tick || (Runner.Tick - RightHeldStart) > Runner.TickRate * 0.1f));
            inputs.buttons.Set(PlayerControls.Jump, inputs.buttons.IsSet(PlayerControls.Jump) && JumpHeldStart != -1 && (JumpHeldStart == Runner.Tick || (Runner.Tick - JumpHeldStart) > Runner.TickRate * 0.1f));

            return inputs;
        }

        public override void FixedUpdateNetwork() {
            if (GameManager.Instance.GameState < Enums.GameState.Playing) {
                return;
            }

            if (GameManager.Instance.GameEnded) {
                // Game ended, freeze.
                body.Velocity = Vector2.zero;
                body.Freeze = true;
                return;
            }

            if (!IsProxy) {

                if (IsDead) {
                    HandleDeathTimers();
                } else if (!IsFrozen) {
                    // If we can't get inputs from the player, just go based on their previous networked input state.
                    PlayerNetworkInput input;
                    if (GetInput(out PlayerNetworkInput currentInputs)) {
                        // Got the inputs from the player!
                        input = currentInputs;
                        HandleButtonHolding(input);
                        LastInputTick = Runner.Tick;
                    } else if (!IsProxy) {
                        // Didn't get the inputs, but we *need* them. Interpolate based on what it *could* be?
                        input = HandleMissingInputs();
                    } else {
                        // Just trust the server
                        input = PreviousInputs;
                    }

                    NetworkButtons heldButtons = input.buttons;
                    NetworkButtons pressedButtons = input.buttons.GetPressed(PreviousInputs.buttons);

                    if (!IsDead) {
                        HandleGroundCollision();
                        IsOnGround |= GroundSnapCheck();

                        if (IsOnGround)
                            IgnoreCoyoteTime = false;

                        if (PreviousTickIsOnGround) {

                            if (!IsOnGround) {
                                if (!IgnoreCoyoteTime)
                                    CoyoteTime = Runner.SimulationTime + 0.05f;

                                IgnoreCoyoteTime = false;
                            }
                        }

                        CheckForPowerupActions(input, PreviousInputs);
                        HandleMovement(heldButtons, pressedButtons);
                    }

                    //HandleBlockSnapping();
                    CheckForEntityCollision();

                    PreviousInputs = input;
                    PreviousTickIsOnGround = IsOnGround;
                }

                animationController.HandlePipeAnimation();
            }

            UpdateTileProperties();
            UpdateHitbox();

            if (!IsProxy) {
                // We can become stuck in a block after uncrouching
                if (!IsDead)
                    HandleStuckInBlock();
            }

            transform.localScale = CalculateScale(false);
        }
        #endregion

        internal Vector3 CalculateScale(bool render) {
            if (MegaEndTimer.IsActive(Runner)) {
                float endTimer = (render ? MegaEndTimer.RemainingRenderTime(Runner) : MegaEndTimer.RemainingTime(Runner)) ?? 0f;
                if (!IsStationaryMegaShrink)
                    endTimer *= 2;
                return Vector3.one + (Vector3.one * (Mathf.Min(1, endTimer / megaStartTime) * 2.6f));
            } else {
                float startTimer = (render ? MegaStartTimer.RemainingRenderTime(Runner) : MegaStartTimer.RemainingTime(Runner)) ?? 0f;

                return State switch {
                    Enums.PowerupState.MiniMushroom => ZeroPointFive,
                    Enums.PowerupState.MegaMushroom => Vector3.one + (Vector3.one * (Mathf.Min(1, 1 - (startTimer / megaStartTime)) * 2.6f)),
                    _ => Vector3.one,
                };
            }
        }

        private void HandleDeathTimers() {

            if (DeathAnimationTimer.Expired(Runner)) {
                if ((Lives == 0 || Disconnected) && Stars > 0) {
                    // Try to drop more stars.
                    SpawnStars(1, DeathplaneDeath);
                    DeathAnimationTimer = TickTimer.CreateFromSeconds(Runner, 0.5f);
                } else {
                    // Play the animation as normal
                    if (!DeathplaneDeath) {
                        body.Gravity = Vector2.down * 11.75f;
                        body.Velocity += Vector2.up * 7f;
                        body.Freeze = false;
                    }
                    DeathAnimationTimer = TickTimer.None;
                    if (Lives == 0) {
                        PreRespawnTimer = TickTimer.CreateFromSeconds(Runner, 2.4f);
                    }
                }
            }

            // Respawn timers

            if (PreRespawnTimer.Expired(Runner)) {
                PreRespawn();
                PreRespawnTimer = TickTimer.None;
            }

            if (RespawnTimer.Expired(Runner)) {
                Respawn();
                RespawnTimer = TickTimer.None;
            }
        }

        #region -- COLLISIONS --
        private void HandleGroundCollision() {
            IsOnGround = body.Data.OnGround && PropellerLaunchTimer.ExpiredOrNotRunning(Runner);

            OnSpinner = null;
            foreach (PhysicsDataStruct.ObjectContact objectContact in body.Data.ObjectsStandingOn) {
                NetworkObject obj = objectContact.GetNetworkObject(Runner);

                // Predictive objects don't have a NetworkID.
                if (!obj)
                    continue;

                if (obj.CompareTag("spinner") && obj.gameObject.TryGetComponent(out SpinnerAnimator spinner)) {
                    OnSpinner = spinner;
                    OnSpinner.HasPlayer = true;
                    break;
                }
            }
        }

        private void UpdateTileProperties() {
            OnIce = false;
            footstepSound = Enums.Sounds.Player_Walk_Grass;
            footstepParticle = Enums.Particle.None;
            foreach (PhysicsDataStruct.TileContact tile in body.Data.TilesStandingOn) {
                Vector2Int pos = tile.location;
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
            if (IsProxy || IsDead || IsFrozen || CurrentPipe)
                return;

            LagCompensatedBuffer.Clear();
            foreach (BoxCollider2D hitbox in hitboxes) {
                Runner.LagCompensation.OverlapBox(
                    body.Position + (hitbox.offset * transform.localScale),
                    ((Vector3) (hitbox.size * 0.5f) + Vector3.forward).Multiply(transform.localScale),
                    Quaternion.identity,
                    Object.InputAuthority,
                    LagCompensatedBuffer,
                    options: HitOptions.IgnoreInputAuthority | HitOptions.SubtickAccuracy | HitOptions.IgnoreInputAuthority | HitOptions.IncludeBox2D,
                    clearHits: false);
            }

            // Interact with overlapped entities
            CollidedObjects.Clear();
            foreach (LagCompensatedHit hit in LagCompensatedBuffer) {

                GameObject hitObject = hit.GameObject;

                if (!hit.Hitbox && hitObject.TryGetComponent<Hitbox>(out _))
                    continue;

                PhysicsDataStruct.ObjectContact contact = new();
                float angle = Vector2.SignedAngle(hit.Normal, Vector2.up);
                angle += 360 + 45;
                angle %= 360;

                contact.direction = angle switch {
                    < 90 => InteractionDirection.Up,
                    < 180 => InteractionDirection.Right,
                    < 270 => InteractionDirection.Down,
                    _ => InteractionDirection.Left
                };

                AttemptToInteractWithObject(hitObject, contact);
            }

            // Interact with touched objects
            foreach (PhysicsDataStruct.ObjectContact contact in body.Data.ObjectContacts) {
                NetworkObject obj = contact.GetNetworkObject(Runner);

                // Predictive objects will never have an ID so we can't reference them. Oh well...
                if (!obj)
                    continue;

                AttemptToInteractWithObject(obj.gameObject, contact);
            }
        }

        private void AttemptToInteractWithObject(GameObject collidedObject, PhysicsDataStruct.IContactStruct contact = null) {

            if (CollidedObjects.Contains(collidedObject))
                return;

            CollidedObjects.Add(collidedObject);

            // Don't interact with ourselves.
            if (collidedObject.transform.IsChildOf(transform))
                return;

            // Or objects we're holding.
            if (HeldEntity && HeldEntity.gameObject == collidedObject)
                return;

            // Or our own frozen cube
            if (FrozenCube && FrozenCube.gameObject == collidedObject)
                return;

            if (collidedObject.GetComponentInParent<IPlayerInteractable>() is IPlayerInteractable interactable) {
                // Or frozen entities.
                if (interactable is FreezableEntity freezable && freezable.IsFrozen)
                    return;

                // Or dead entities.
                if (interactable is KillableEntity killable && killable.IsDead)
                    return;

                // And don't predict the collection of stars / coins.
                if (interactable is CollectableEntity && IsProxy)
                    return;

                if (interactable is PlayerController pl) {
                    InteractWithPlayer(pl, contact);
                } else {
                    interactable.InteractWithPlayer(this, contact);
                }
            }
        }

        public void InteractWithPlayer(PlayerController other, PhysicsDataStruct.IContactStruct contact = null) {

            // Don't interact with ghosts
            if (IsDead || other.IsDead)
                return;

            // Or players in pipes
            if (CurrentPipe || other.CurrentPipe)
                return;

            // Or frozen players (we interact with the frozencube)
            if (IsFrozen || other.IsFrozen)
                return;

            // Or players with I-Frames
            if (DamageInvincibilityTimer.IsActive(Runner) || other.DamageInvincibilityTimer.IsActive(Runner))
                return;

            // Or players in the Mega Mushroom grow animation
            if (MegaStartTimer.IsActive(Runner) || other.MegaStartTimer.IsActive(Runner))
                return;

            // Hit players
            bool dropStars = Data.Team != other.Data.Team;

            Utils.Utils.UnwrapLocations(body.Position, other.body.Position, out Vector2 ours, out Vector2 theirs);
            bool fromRight = ours.x < theirs.x;

            float dot = Vector2.Dot((ours - theirs).normalized, Vector2.up);
            bool above = dot > 0.6f;
            bool otherAbove = dot < -0.6f;

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
                    // Both mega
                    if (above) {
                        DoEntityBounce = true;
                        IsGroundpounding = false;
                        IsDrilling = false;
                    } else if (!otherAbove) {
                        DoKnockback(fromRight, 0, true, other.Object);
                        other.DoKnockback(!fromRight, 0, true, Object);
                    }
                } else if (State == Enums.PowerupState.MegaMushroom) {
                    // Only we are mega
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
                    float dotRight = Vector2.Dot((body.Position - other.body.Position).normalized, Vector2.right);
                    FacingRight = dotRight > 0;
                    return;
                }
            }
            if (State == Enums.PowerupState.BlueShell && otherAbove && !other.IsGroundpounding && !other.IsDrilling && (IsCrouching || IsGroundpounding) && IsOnGround) {
                IsGroundpounding = false;

                bool goLeft = fromRight;
                Vector2Int tileLocation = Utils.Utils.WorldToTilemapPosition(body.Position);
                if (Utils.Utils.IsTileSolidAtTileLocation(tileLocation + Vector2Int.right)) {
                    // Tile to the right. Force go left.
                    goLeft = true;
                } else if (Utils.Utils.IsTileSolidAtTileLocation(tileLocation + Vector2Int.left)) {
                    // Tile to the left. Force go right.
                    goLeft = false;
                }

                body.Velocity = new(SPEED_STAGE_MAX[RUN_STAGE] * 0.9f * (goLeft ? -1 : 1), body.Velocity.y);
            }

            if (other.IsInShell && !above)
                return;

            if (above && other.State == Enums.PowerupState.BlueShell && !other.IsInShell && (other.IsCrouching || other.IsGroundpounding) && IsOnGround && !IsGroundpounding && !IsDrilling) {
                // They are blue shell
                DoEntityBounce = true;
                return;
            }

            if (above && other.IsDamageable) {
                // Hit them from above
                DoEntityBounce = !IsGroundpounding && !IsDrilling;
                bool groundpounded = HasGroundpoundHitbox;

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
                // Collided with them
                if (State == Enums.PowerupState.MiniMushroom || other.State == Enums.PowerupState.MiniMushroom) {
                    // Minis
                    if (State == Enums.PowerupState.MiniMushroom)
                        DoKnockback(fromRight, dropStars ? 1 : 0, false, other.Object);

                    if (other.State == Enums.PowerupState.MiniMushroom)
                        other.DoKnockback(!fromRight, dropStars ? 1 : 0, false, Object);

                } else if (Mathf.Abs(body.Velocity.x) > WalkingMaxSpeed || Mathf.Abs(other.body.Velocity.x) > WalkingMaxSpeed) {
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
                    float overlap = ((WorldHitboxSize.x * 0.5f) + (other.WorldHitboxSize.x * 0.5f) - Mathf.Abs(ours.x - theirs.x)) * 0.5f;

                    if (overlap > 0.02f) {
                        Vector2 ourNewPosition = new(body.Position.x + (overlap * directionToOtherPlayer), body.Position.y);
                        Vector2 theirNewPosition = new(other.body.Position.x + (overlap * -directionToOtherPlayer), other.body.Position.y);

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
                            body.Position = ourNewPosition;
                            other.body.Position = theirNewPosition;

                            float avgVel = (body.Velocity.x + other.body.Velocity.x) * 0.5f;
                            body.Velocity = new(avgVel, body.Velocity.y);
                            other.body.Velocity = new(avgVel, other.body.Velocity.y);
                        }
                    }
                }
            }
        }
        #endregion

        #region -- CONTROLLER FUNCTIONS --
        public void OnInput(NetworkRunner runner, NetworkInput input) {
            PlayerNetworkInput newInput = onInputPreviousInputs;

            // Input nothing when paused
            if (GameManager.Instance.paused) {
                newInput.buttons.SetAllUp();
                input.Set(newInput);
                return;
            }


            Vector2 joystick = ControlSystem.controls.Player.Movement.ReadValue<Vector2>();
            bool jump = ControlSystem.controls.Player.Jump.ReadValue<float>() >= 0.5f;
            bool powerup = ControlSystem.controls.Player.PowerupAction.ReadValue<float>() >= 0.5f;
            bool sprint = ControlSystem.controls.Player.Sprint.ReadValue<float>() >= 0.5f;

            Vector2 normalizedJoystick = joystick.normalized;
            //TODO: changeable deadzone?
            bool up = Vector2.Dot(normalizedJoystick, Vector2.up) > 0.6f;
            bool down = Vector2.Dot(normalizedJoystick, Vector2.down) > 0.6f;
            bool left = Vector2.Dot(normalizedJoystick, Vector2.left) > 0.4f;
            bool right = Vector2.Dot(normalizedJoystick, Vector2.right) > 0.4f;

            newInput.buttons.Set(PlayerControls.Up, up);
            newInput.buttons.Set(PlayerControls.Down, down);
            newInput.buttons.Set(PlayerControls.Left, left);
            newInput.buttons.Set(PlayerControls.Right, right);
            newInput.buttons.Set(PlayerControls.Jump, jump);
            newInput.buttons.Set(PlayerControls.Sprint, sprint ^ Settings.Instance.controlsAutoSprint);
            newInput.buttons.Set(PlayerControls.PowerupAction, powerup);

            // Powerup action counter avoids dropped inputs
            NetworkButtons pressed = newInput.buttons.GetPressed(onInputPreviousInputs.buttons);
            if (pressed.IsSet(PlayerControls.PowerupAction)
                || (pressed.IsSet(PlayerControls.Sprint) && Settings.Instance.controlsFireballSprint && (State == Enums.PowerupState.FireFlower || State == Enums.PowerupState.IceFlower))
                || (pressed.IsSet(PlayerControls.Jump) && !IsOnGround && Settings.Instance.controlsPropellerJump && State == Enums.PowerupState.PropellerMushroom))
                newInput.powerupActionCounter++;

            input.Set(newInput);
            onInputPreviousInputs = newInput;
        }

        private void CheckForPowerupActions(PlayerNetworkInput current, PlayerNetworkInput previous) {
            if (current.powerupActionCounter == previous.powerupActionCounter)
                return;

            ActivatePowerupAction();
        }

        private void ActivatePowerupAction() {
            if (IsDead || IsFrozen || IsInKnockback || CurrentPipe || GameManager.Instance.GameEnded || HeldEntity)
                return;

            switch (State) {
            case Enums.PowerupState.IceFlower:
            case Enums.PowerupState.FireFlower: {
                if (WallSliding || IsGroundpounding || JumpState == PlayerJumpState.TripleJump || IsSpinnerFlying || IsDrilling || IsCrouching || IsSliding || IsSkidding || IsTurnaround)
                    return;

                if (FireballDelayTimer.IsActive(Runner))
                    return;

                int activeFireballs = 0;
                foreach (var fireball in GameManager.Instance.PooledFireballs) {
                    if (fireball.IsActive && fireball.Owner == this)
                        activeFireballs++;
                }

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
                Vector2 spawnPos = body.Position + new Vector2(right ? 0.4f : -0.4f, 0.35f);

                Fireball inactiveFireball = null;
                for (int i = PlayerId * 6; i < (PlayerId + 1) * 6; i++) {
                    Fireball fireball = GameManager.Instance.PooledFireballs[i];
                    if (fireball.IsActive)
                        continue;

                    inactiveFireball = fireball;
                    break;
                }

                if (!inactiveFireball)
                    // No available fireball. This should never happen... so uh.....
                    break;

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
                body.Position += Vector2.up * 0.05f;
            }
            UsedPropellerThisJump = true;
        }

        public void OnReserveItem(InputAction.CallbackContext context) {
            if (GameManager.Instance.paused || GameManager.Instance.GameEnded)
                return;

            if (StoredPowerup == Enums.PowerupState.NoPowerup || IsDead || MegaStartTimer.IsActive(Runner) || (IsStationaryMegaShrink && MegaEndTimer.IsActive(Runner))) {
                PlaySound(Enums.Sounds.UI_Error);
                return;
            }

            Rpc_SpawnReserveItem();
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
            IsDrilling &= !IsPropellerFlying;
            IsPropellerFlying = false;
            IsInShell = false;
            PropellerLaunchTimer = TickTimer.None;
            PropellerSpinTimer = TickTimer.None;
            UsedPropellerThisJump = false;

            if (!IsDead) {
                DamageInvincibilityTimer = TickTimer.CreateFromSeconds(Runner, 2f);

                if (HasStateAuthority)
                    Rpc_PlaySound(Enums.Sounds.Player_Sound_Powerdown);
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
            body.Freeze = true;
            IsInKnockback = false;
            IsSkidding = false;
            IsDrilling = false;
            WallSlideLeft = false;
            WallSlideRight = false;
            IsPropellerFlying = false;
            body.Velocity = Vector2.zero;

            // This is ok.
            animator.Play("falling");
            animator.Update(0f);

            AttemptThrowHeldItem();

            PropellerLaunchTimer = TickTimer.None;
            IsSkidding = false;
        }

        public override void Unfreeze(UnfreezeReason reason) {
            if (!IsFrozen)
                return;

            IsFrozen = false;
            animator.enabled = true;
            body.Freeze = false;

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

        public override void BlockBump(BasicEntity bumper, Vector2Int tile, InteractionDirection direction) {
            if (IsInKnockback)
                return;

            Utils.Utils.UnwrapLocations(body.Position, bumper.body ? bumper.body.Position : bumper.transform.position, out Vector2 ourPos, out Vector2 theirPos);
            bool onRight = ourPos.x > theirPos.x;

            DoKnockback(!onRight, 1, false, Object);
        }

        public void SetReserveItem(Enums.PowerupState newItem) {
            PowerupScriptable currentReserve = StoredPowerup.GetPowerupScriptable();
            if (!currentReserve) {
                // We don't have a reserve item, so we can just set it
                StoredPowerup = newItem;
                return;
            }

            PowerupScriptable newReserve = newItem.GetPowerupScriptable();
            if (!newReserve) {
                // Not a valid powerup, so just clear our reserve item instead
                StoredPowerup = Enums.PowerupState.NoPowerup;
                return;
            }

            sbyte newItemPriority = newReserve ? newReserve.itemPriority : (sbyte) -1;
            sbyte currentItemPriority = currentReserve ? currentReserve.itemPriority : (sbyte) -1;

            if (newItemPriority < currentItemPriority) {
                // New item is less important than our current reserve item, so we don't want to replace it
                return;
            }

            // Replace our current reserve item with the new one
            StoredPowerup = newItem;
        }

        public void SpawnItem(NetworkPrefabRef prefab) {
            if (!Runner.IsServer)
                return;

            if (prefab == NetworkPrefabRef.Empty)
                prefab = Utils.Utils.GetRandomItem(this).prefab;

            Runner.Spawn(prefab, new(body.Position.x, body.Position.y, 0), onBeforeSpawned: (runner, obj) => {
                Powerup p = obj.GetComponent<Powerup>();
                p.OnBeforeSpawned(this);
                p.body.Position = body.Position;
            });
        }

        private void SpawnStars(int amount, bool deathplane) {

            if (!Runner.IsServer)
                return;

            GameManager gm = GameManager.Instance;
            bool fastStars = amount > 2 && Stars > 2;
            int starDirection = FacingRight ? 1 : 2;

            // If the level doesn't loop, don't have stars go towards the edges of the map
            if (!gm.loopingLevel) {
                if (body.Position.x > gm.LevelMaxX - 2.5f) {
                    starDirection = 1;
                } else if (body.Position.x < gm.LevelMinX + 2.5f) {
                    starDirection = 2;
                }
            }

            if (Lives == 0) {
                fastStars = true;
                starDirection = noLivesStarSpawnDirection++ % 4;

                if (starDirection == 2)
                    starDirection = 1;
                else if (starDirection == 1)
                    starDirection = 2;
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

                Runner.Spawn(PrefabList.Instance.Obj_BigStar, body.Position + Vector2.up * WorldHitboxSize.y, onBeforeSpawned: (runner, obj) => {
                    BigStar bouncer = obj.GetComponent<BigStar>();
                    bouncer.OnBeforeSpawned((byte) starDirection, false, deathplane);
                });

                starDirection = (starDirection + 1) % 4;
                Stars--;
                amount--;
            }
            GameManager.Instance.CheckForWinner();
        }

        #region -- DEATH / RESPAWNING --
        public void Death(bool deathplane, bool fire) {
            if (IsDead)
                return;

            if (IsProxy)
                return;

            IsDead = true;
            DeathplaneDeath = deathplane;
            FireDeath = fire;

            PreRespawnTimer = TickTimer.CreateFromSeconds(Runner, 3f);
            RespawnTimer = TickTimer.CreateFromSeconds(Runner, 4.3f);

            if ((Lives > 0 && --Lives == 0) || Disconnected) {
                // Last death - drop all stars at 0.5s each
                if (!GameManager.Instance.CheckForWinner())
                    SpawnStars(1, DeathplaneDeath);

                PreRespawnTimer = TickTimer.None;
                RespawnTimer = TickTimer.None;
                DeathAnimationTimer = TickTimer.CreateFromSeconds(Runner, (Stars > 0) ? 0.5f : 0.6f);
            } else {
                SpawnStars(1, DeathplaneDeath);
                DeathAnimationTimer = TickTimer.CreateFromSeconds(Runner, 0.6f);
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
            IsFrozen = false;

            if (FrozenCube)
                Runner.Despawn(FrozenCube.Object);

            body.Velocity = Vector2.zero;
            body.Freeze = true;
            AttemptThrowHeldItem(null, true);

            if (HasStateAuthority)
                Rpc_PlayDeathSound();
        }

        public void AttemptThrowHeldItem(bool? right = null, bool crouch = false) {
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
                GameManager.Instance.CheckForWinner();

                if (HasInputAuthority)
                    GameManager.Instance.spectationManager.Spectating = true;

                Runner.Despawn(Object);
                return;
            }

            Vector2 spawnpoint = Spawnpoint;
            body.Position = spawnpoint;
            cameraController.Recenter(spawnpoint);

            IsFrozen = false;
            IsRespawning = true;
            FacingRight = true;
            transform.localScale = Vector2.one;
            PreviousState = State = Enums.PowerupState.NoPowerup;
            animationController.DisableAllModels();
            StarmanTimer = TickTimer.None;
            MegaTimer = TickTimer.None;
            MegaEndTimer = TickTimer.None;
            MegaStartTimer = TickTimer.None;
            IsGroundpounding = false;
            body.Freeze = true;
            body.Velocity = Vector2.zero;
        }

        public void Respawn() {

            //gameObject.SetActive(true);
            IsFrozen = false;
            IsDead = false;
            IsRespawning = false;
            State = Enums.PowerupState.NoPowerup;
            PreviousState = Enums.PowerupState.NoPowerup;
            body.Velocity = Vector2.zero;
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
            MegaStartTimer = TickTimer.None;
            MegaEndTimer = TickTimer.None;
            MegaTimer = TickTimer.None;
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
            body.Freeze = false;
            body.Velocity = Vector2.zero;

            DamageInvincibilityTimer = TickTimer.CreateFromSeconds(Runner, 2f);

            if (HasInputAuthority)
                ScoreboardUpdater.Instance.OnRespawnToggle();
        }
        #endregion

        #region -- SOUNDS / PARTICLES --
        public void PlaySoundEverywhere(Enums.Sounds sound) {
            GameManager.Instance.sfx.PlayOneShot(sound, character);
        }
        public void PlaySound(Enums.Sounds sound, byte variant = 0, float volume = 1) {
            PlaySound(sound, character, variant, volume);
        }
        protected GameObject SpawnParticle(string particle, Vector2 worldPos, Quaternion? rot = null) {
            return Instantiate(Resources.Load(particle), worldPos, rot ?? Quaternion.identity) as GameObject;
        }
        protected GameObject SpawnParticle(GameObject particle, Vector2 worldPos, Quaternion? rot = null) {
            return Instantiate(particle, worldPos, rot ?? Quaternion.identity);
        }

        protected void PlayMegaFootstep() {
            if (IsSwimming)
                return;

            CameraController.ScreenShake = 0.15f;
            SpawnParticle(PrefabList.Instance.Particle_Groundpound, body.Position + new Vector2(FacingRight ? 0.5f : -0.5f, 0));
            PlaySound(Enums.Sounds.Powerup_MegaMushroom_Walk, (byte) (footstepVariant ? 1 : 2));
            GlobalController.Instance.rumbleManager.RumbleForSeconds(0.5f, 0f, 0.1f, RumbleManager.RumbleSetting.High);
            footstepVariant = !footstepVariant;
        }

        protected void Footstep() {
            if (IsSwimming || State == Enums.PowerupState.MegaMushroom)
                return;

            bool left = PreviousInputs.buttons.IsSet(PlayerControls.Left);
            bool right = PreviousInputs.buttons.IsSet(PlayerControls.Right);

            bool reverse = body.Velocity.x != 0 && ((left ? 1 : -1) == Mathf.Sign(body.Velocity.x));
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
                GameManager.Instance.particleManager.Play((Enums.Particle) ((int) footstepParticle + (FacingRight ? 1 : 0)), body.Position);

            if (!IsWaterWalking && Mathf.Abs(body.Velocity.x) < WalkingMaxSpeed)
                return;

            PlaySound(footstepSound, (byte) (footstepVariant ? 1 : 2), Mathf.Abs(body.Velocity.x) / (RunningMaxSpeed + 4));
            footstepVariant = !footstepVariant;
        }
        #endregion

        #region -- TILE COLLISIONS --
        private void HandleMegaTiles(bool pipes) {

            if (State != Enums.PowerupState.MegaMushroom || MegaStartTimer.IsActive(Runner))
                return;

            bool hitGroundTiles = body.Velocity.y < -8f && (IsOnGround || IsGroundpounding);
            Vector2 offset = Vector2.zero;
            if (hitGroundTiles)
                offset = Vector2.down * 0.25f;

            Vector2 checkSizeHalf = WorldHitboxSize * 0.5f;
            Vector2 checkPosition = body.Position + (Vector2.up * checkSizeHalf) + (Runner.DeltaTime * body.Velocity) + offset;

            Vector2Int minPos = Utils.Utils.WorldToTilemapPosition(checkPosition - (checkSizeHalf), wrap: false);
            Vector2Int size = Utils.Utils.WorldToTilemapPosition(checkPosition + (checkSizeHalf), wrap: false) - minPos;

            for (int x = 0; x <= size.x; x++) {
                for (int y = 0; y <= size.y; y++) {
                    Vector2Int tileLocation = new(minPos.x + x, minPos.y + y);
                    Vector2 worldPosCenter = Utils.Utils.TilemapToWorldPosition(tileLocation) + Vector3.one * 0.25f;
                    Utils.Utils.WrapTileLocation(ref tileLocation);

                    InteractionDirection dir = InteractionDirection.Up;
                    if (worldPosCenter.y + 0.25f <= body.Position.y) {
                        if (!hitGroundTiles)
                            continue;

                        dir = InteractionDirection.Down;
                    } else if (worldPosCenter.y >= body.Position.y + size.y) {
                        dir = InteractionDirection.Up;
                    } else if (worldPosCenter.x <= body.Position.x) {
                        dir = InteractionDirection.Left;
                    } else if (worldPosCenter.x >= body.Position.x) {
                        dir = InteractionDirection.Right;
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

                        InteractionDirection dir = InteractionDirection.Up;
                        if (worldPosCenter.y + 0.25f <= body.Position.y) {
                            if (!hitGroundTiles)
                                continue;

                            dir = InteractionDirection.Down;
                        } else if (worldPosCenter.x <= body.Position.x) {
                            dir = InteractionDirection.Left;
                        } else if (worldPosCenter.x >= body.Position.x) {
                            dir = InteractionDirection.Right;
                        }

                        if (GameManager.Instance.tileManager.GetTile(tileLocation, out BreakablePipeTile pipe)) {
                            if (!pipe.upsideDownPipe || dir == InteractionDirection.Up)
                                continue;
                        }

                        InteractWithTile(tileLocation, dir, out bool _, out bool _);
                    }
                }
            }
        }

        private bool InteractWithTile(Vector2Int tilePos, InteractionDirection direction, out bool interacted, out bool bumpSound) {

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

            if (GameManager.Instance.GameState != Enums.GameState.Playing || DamageInvincibilityTimer.IsActive(Runner) || CurrentPipe || IsFrozen || IsDead || MegaStartTimer.IsActive(Runner) || MegaEndTimer.IsActive(Runner))
                return;

            if (State == Enums.PowerupState.MiniMushroom && starsToDrop > 1) {
                SpawnStars(starsToDrop - 1, false);
                Powerdown(false);
                return;
            }

            if (IsInKnockback || IsWeakKnockback)
                starsToDrop = Mathf.Min(1, starsToDrop);

            IsInKnockback = true;
            KnockbackAnimCounter++;
            IsWeakKnockback = weak;
            IsForwardsKnockback = FacingRight != fromRight;
            KnockbackAttacker = attacker;
            KnockbackWasOriginallyFacingRight = FacingRight;
            KnockbackTick = Runner.Tick;

            //Vector2Int tileLoc = Utils.Utils.WorldToTilemapPosition(body.Position);
            //TileBase tile = Utils.Utils.GetTileAtTileLocation(tileLoc + (fromRight ? Vector2Int.left : Vector2Int.right));
            //if (!weak && tile)
            //    fromRight = !fromRight;

            body.Velocity = new Vector2(
                (fromRight ? -1 : 1) *
                ((starsToDrop + 1) / 2f) *
                4f *
                (State == Enums.PowerupState.MegaMushroom ? 3 : 1) *
                (State == Enums.PowerupState.MiniMushroom ? 2.5f : 1f) *
                (weak ? 0.5f : 1f),

                // don't go upwards if we got hit by a fireball
                (attacker && attacker.TryGetComponent(out Fireball _)) ? 0 : 4.5f
            );

            IsOnGround = false;
            PreviousTickIsOnGround = false;
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
            body.Velocity = new(RunningMaxSpeed * (fromRight ? -1 : 1), body.Velocity.y);
        }

        public void ResetKnockbackFromAnim() {
            ResetKnockback();
        }

        private void ResetKnockback() {
            DamageInvincibilityTimer = TickTimer.CreateFromSeconds(Runner, 1f);
            //DoEntityBounce = false;
            IsInKnockback = false;
            body.Velocity = new(0, body.Velocity.y);
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
                        body.Velocity = new Vector2(-Mathf.Sign(FloorAngle) * SPEED_SLIDE_MAX, 0);
                    } else {
                        body.Velocity = Vector2.zero;
                        if (!down || State == Enums.PowerupState.MegaMushroom) {
                            IsGroundpounding = false;
                            GroundpoundStartTimer = TickTimer.CreateFromSeconds(Runner, 0.2667f);
                        }
                    }
                }
                if (up && !GroundpoundStartTimer.IsActive(Runner)) {
                    IsGroundpounding = false;
                    body.Velocity = Vector2.down * groundpoundVelocity;
                    GroundpoundCooldownTimer = TickTimer.CreateFromSeconds(Runner, 0.2f);
                }
            }
            if (OnSlope && (!((FacingRight && HitRight) || (!FacingRight && HitLeft)) && IsCrouching && Mathf.Abs(FloorAngle) >= slopeSlidingAngle && !IsInShell && State != Enums.PowerupState.MegaMushroom)) {
                IsSliding = true;
                IsCrouching = false;
            }
            if (IsSliding && IsOnGround && Mathf.Abs(FloorAngle) > slopeSlidingAngle) {
                float angleDeg = FloorAngle * Mathf.Deg2Rad;

                bool uphill = Mathf.Sign(FloorAngle) == Mathf.Sign(body.Velocity.x);
                float speed = Runner.DeltaTime * 5f * (uphill ? Mathf.Clamp01(1f - (Mathf.Abs(body.Velocity.x) / RunningMaxSpeed)) : 4f);

                float newX = Mathf.Clamp(body.Velocity.x - (Mathf.Sin(angleDeg) * speed), -(RunningMaxSpeed * 1.3f), RunningMaxSpeed * 1.3f);
                float newY = (uphill ? 0 : -1.5f) * Mathf.Abs(newX);
                body.Velocity = new Vector2(newX, newY);
            }

            if (up || ((left ^ right) && !down) || (OnSlope && Mathf.Abs(FloorAngle) < slopeSlidingAngle && IsOnGround && body.Velocity.x == 0 && !down) || (FacingRight && HitRight) || (!FacingRight && HitLeft)) {
                IsSliding = false;
            }
        }

        private void HandleSlopes() {
            OnSlope = false;

            if (!IsOnGround) {
                FloorAngle = 0;
                return;
            }

            RaycastHit2D hit = Runner.GetPhysicsScene2D().BoxCast(body.Position + (Vector2.up * 0.05f), new Vector2((MainHitbox.size.x - Physics2D.defaultContactOffset * 2f) * transform.lossyScale.x, 0.1f), 0, body.Velocity.normalized, (body.Velocity * Runner.DeltaTime).magnitude, Layers.MaskAnyGround);
            if (hit) {
                // Hit ground
                float angle = Vector2.SignedAngle(Vector2.up, hit.normal);
                if (Mathf.Abs(angle) > 89)
                    return;

                TileWithProperties tile = Utils.Utils.GetTileAtWorldLocation(hit.point) as TileWithProperties;
                if (!tile && GameManager.Instance.semisolidTilemap)
                    tile = GameManager.Instance.semisolidTilemap.GetTile<TileWithProperties>((Vector3Int) Utils.Utils.WorldToTilemapPosition(hit.point));

                float x = Mathf.Abs(FloorAngle - angle) > 1f && Mathf.Abs(angle) > 1f ? body.PreviousTickVelocity.x : body.Velocity.x;

                FloorAngle = angle;

                float change = Mathf.Sin(angle * Mathf.Deg2Rad) * x * 1.1f;
                body.Velocity = new Vector2(x, change);
                IsOnGround = true;
                PreviousTickIsOnGround = true;
                OnSlope = tile ? tile.isSlope : false;
            } else {
                hit = Runner.GetPhysicsScene2D().BoxCast(body.Position + (Vector2.up * 0.05f), new Vector2((MainHitbox.size.x + Physics2D.defaultContactOffset * 3f) * transform.lossyScale.x, 0.1f), 0, Vector2.down, 0.3f, Layers.MaskAnyGround);
                if (hit) {
                    float angle = Vector2.SignedAngle(Vector2.up, hit.normal);
                    if (Mathf.Abs(angle) > 89)
                        return;

                    TileWithProperties tile = Utils.Utils.GetTileAtWorldLocation(hit.point) as TileWithProperties;
                    if (!tile && GameManager.Instance.semisolidTilemap)
                        tile = GameManager.Instance.semisolidTilemap.GetTile<TileWithProperties>((Vector3Int) Utils.Utils.WorldToTilemapPosition(hit.point));

                    float x = Mathf.Abs(FloorAngle - angle) > 1f && Mathf.Abs(angle) > 1f ? body.PreviousTickVelocity.x : body.Velocity.x;

                    FloorAngle = angle;

                    float change = Mathf.Sin(angle * Mathf.Deg2Rad) * x * 1.1f;
                    body.Velocity = new(x, change);
                    IsOnGround = true;
                    PreviousTickIsOnGround = true;
                    OnSlope = tile ? tile.isSlope : false;
                } else {
                    FloorAngle = 0;
                }
            }

            if (Mathf.Abs(body.Velocity.x) < 0.01f && body.Velocity.y < 0 && body.Velocity.y > -0.01f) {
                body.Velocity = Vector2.zero;
            }
        }

        private void HandleLayerState() {
            bool hitsNothing = CurrentPipe || IsFrozen || IsDead || IsStuckInBlock || MegaStartTimer.IsActive(Runner) || (MegaEndTimer.IsActive(Runner) && IsStationaryMegaShrink);

            MainHitbox.gameObject.layer = hitsNothing ? Layers.LayerHitsNothing : Layers.LayerPlayer;
        }

        private bool GroundSnapCheck() {
            if ((!IsOnGround && !PreviousTickIsOnGround) || IsDead || (body.Velocity.y > 0.1f && FloorAngle == 0) || PropellerLaunchTimer.IsActive(Runner) || CurrentPipe)
                return false;

            // TODO: improve
            RaycastHit2D hit;
            if (IsWaterWalking) {
                hit = Runner.GetPhysicsScene2D().BoxCast(body.Position + Vector2.up * 0.1f, new Vector2(WorldHitboxSize.x, 0.05f), 0, Vector2.down, 0.4f, 1 << Layers.LayerEntityHitbox);
                if (hit && hit.collider.gameObject.CompareTag("water")) {
                    body.Position = new(body.Position.x, hit.point.y + Physics2D.defaultContactOffset);
                    return true;
                } else {
                    IsWaterWalking = false;
                }
            }

            Vector2 startPos = body.Position + Vector2.up * 0.15f;
            hit = Runner.GetPhysicsScene2D().BoxCast(startPos, new Vector2(WorldHitboxSize.x, 0.05f), 0, Vector2.down, 0.4f, Layers.MaskAnyGround);
            if (hit) {
                body.Position = startPos + (Vector2.down * hit.distance);
                return true;
            }

            return false;
        }

        #region -- PIPES --
        public void EnterPipe(PipeManager pipe, Vector2 direction) {
            CurrentPipe = pipe;
            PipeEntering = true;
            PipeTimer = TickTimer.CreateFromSeconds(Runner, animationController.pipeDuration * 0.5f);
            body.Velocity = PipeDirection = direction;

            transform.position = body.Position = new Vector2(pipe.transform.position.x, transform.position.y);

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

            if (!IsCrouching && IsSwimming && Mathf.Abs(body.Velocity.x) > 0.03f)
                return;

            IsCrouching = ((IsOnGround && crouchInput && !IsGroundpounding) || (!IsOnGround && (crouchInput || (body.Velocity.y > 0 && State != Enums.PowerupState.BlueShell)) && IsCrouching && !IsSwimming) || (IsCrouching && ForceCrouchCheck())) && !HeldEntity;
        }

        public bool ForceCrouchCheck() {
            // Janky fortress ceiling check, mate
            if (State == Enums.PowerupState.BlueShell && IsOnGround && SceneManager.GetActiveScene().buildIndex != 4)
                return false;
            if (State <= Enums.PowerupState.MiniMushroom)
                return false;

            float width = MainHitbox.bounds.extents.x;
            float uncrouchHeight = GetHitboxSize(false).y * transform.lossyScale.y;

            bool ret = Runner.GetPhysicsScene2D().BoxCast(body.Position + Vector2.up * 0.1f, new(width - 0.05f, 0.05f), 0, Vector2.up, uncrouchHeight - 0.1f, Layers.MaskSolidGround);
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
                // Walljump check
                FacingRight = WallSlideLeft;
                if (jump && WallJumpTimer.ExpiredOrNotRunning(Runner) && !BounceJump) {
                    // Perform walljump

                    body.Velocity = new(WALLJUMP_HSPEED * (WallSlideLeft ? 1 : -1), State == Enums.PowerupState.MiniMushroom ? WALLJUMP_MINI_VSPEED : WALLJUMP_VSPEED);
                    JumpState = PlayerJumpState.SingleJump;
                    IsOnGround = false;
                    DoEntityBounce = false;
                    timeSinceLastBumpSound = 0;

                    Vector2 particleOffset = WorldHitboxSize * 0.5f;
                    Quaternion rot = Quaternion.identity;
                    if (WallSlideRight) {
                        rot = Quaternion.Euler(0, 180, 0);
                    } else {
                        particleOffset.x *= -1;
                    }

                    SpawnParticle(body.Position + particleOffset, Enums.PrefabParticle.Player_WallJump, rot);

                    WallJumpTimer = TickTimer.CreateFromSeconds(Runner, 16f / 60f);
                    WallSlideRight = false;
                    WallSlideLeft = false;
                    WallSlideEndTimer = TickTimer.None;
                }
            } else if (HitLeft || HitRight) {
                // Walljump starting check
                bool canWallslide = !IsInShell && body.Velocity.y < -0.1f && !IsGroundpounding && !IsOnGround && !HeldEntity && State != Enums.PowerupState.MegaMushroom && !IsSpinnerFlying && !IsDrilling && !IsCrouching && !IsSliding && !IsInKnockback && PropellerLaunchTimer.ExpiredOrNotRunning(Runner);
                if (!canWallslide)
                    return;

                // Check 1
                if (WallJumpTimer.IsActive(Runner))
                    return;

                // Check 2
                if (WallSlideEndTimer.IsActive(Runner))
                    return;

                // Check 4: already handled
                // Check 5.2: already handled

                //Check 6
                if (IsCrouching)
                    return;

                // Check 8
                if (!((currentWallDirection == Vector2.right && FacingRight) || (currentWallDirection == Vector2.left && !FacingRight)))
                    return;

                // Start wallslide
                WallSlideRight = currentWallDirection == Vector2.right && HitRight;
                WallSlideLeft = currentWallDirection == Vector2.left && HitLeft;
                WallSlideEndTimer = TickTimer.None;

                if (WallSlideRight || WallSlideLeft)
                    IsPropellerFlying = false;
            }
        }

        private static readonly Vector2 WallSlideLowerHeightOffset = new(0f, 0.2f);
        private void HandleWallSlideStopChecks(Vector2 wallDirection, bool right, bool left) {
            bool floorCheck = !Runner.GetPhysicsScene2D().Raycast(body.Position, Vector2.down, 0.1f, Layers.MaskAnyGround);
            bool moveDownCheck = body.Velocity.y < 0;
            bool heightLowerCheck = Runner.GetPhysicsScene2D().Raycast(body.Position + WallSlideLowerHeightOffset, wallDirection, MainHitbox.size.x * 2, Layers.MaskSolidGround);
            if (!floorCheck || !moveDownCheck || !heightLowerCheck) {
                WallSlideRight = false;
                WallSlideLeft = false;
                WallSlideEndTimer = TickTimer.None;
                return;
            }

            if ((wallDirection == Vector2.left && (!left || !HitLeft)) || (wallDirection == Vector2.right && (!right || !HitRight))) {
                if (WallSlideEndTimer.ExpiredOrNotRunning(Runner)) {
                    WallSlideEndTimer = TickTimer.CreateFromSeconds(Runner, 16 / 60f);
                }
            } else {
                WallSlideEndTimer = TickTimer.None;
            }
        }

        private void HandleJumping(bool jumpHeld, bool doJump, bool down) {

            if (!DoEntityBounce && (!doJump || IsInKnockback || (State == Enums.PowerupState.MegaMushroom && JumpState == PlayerJumpState.SingleJump) || WallSliding))
                return;

            if (!DoEntityBounce && OnSpinner && !HeldEntity) {
                // Jump of spinner
                body.Velocity = new(body.Velocity.x, launchVelocity);
                IsSpinnerFlying = true;
                SpinnerLaunchAnimCounter++;
                IsOnGround = false;
                PreviousTickIsOnGround = false;
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

            bool topSpeed = Mathf.Abs(body.Velocity.x) >= RunningMaxSpeed;
            bool canSpecialJump = !down && (doJump || (DoEntityBounce && jumpHeld)) && ProperJump && !IsSpinnerFlying && !IsPropellerFlying && topSpeed && ((Runner.SimulationTime - TimeGrounded < 0.2f) || DoEntityBounce) && !HeldEntity && JumpState != PlayerJumpState.TripleJump && !IsCrouching && !IsInShell && ((body.Velocity.x < 0 && !FacingRight) || (body.Velocity.x > 0 && FacingRight)) && !Runner.GetPhysicsScene2D().Raycast(body.Position + new Vector2(0, 0.1f), Vector2.up, 1f, Layers.MaskSolidGround);
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

            // Disable koyote time
            IgnoreCoyoteTime = true;
            IsOnGround = false;

            float t = Mathf.Clamp01(Mathf.Abs(body.Velocity.x) - SPEED_STAGE_MAX[1] + (SPEED_STAGE_MAX[1] * 0.5f));
            Enums.PowerupState effectiveState = State;
            if (effectiveState == Enums.PowerupState.MegaMushroom && DoEntityBounce)
                effectiveState = Enums.PowerupState.NoPowerup;

            float vel = effectiveState switch {
                Enums.PowerupState.MegaMushroom => 12.1875f + Mathf.Lerp(0, 0.52734375f, t),
                Enums.PowerupState.MiniMushroom => 5.408935546875f + Mathf.Lerp(0, 0.428466796875f, t),
                _ => 6.62109375f + Mathf.Lerp(0, 0.46875f, t),
            };
            vel += (Mathf.Sign(body.Velocity.x) != Mathf.Sign(FloorAngle)) ? 0 : Mathf.Abs(FloorAngle) * 0.01f * t;

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

            body.Velocity = new(body.Velocity.x, vel + jumpBoost);
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
                if ((WallJumpTimer.RemainingTime(Runner) ?? 0f) < 0.2f && (HitLeft || HitRight)) {
                    WallJumpTimer = TickTimer.None;
                } else {
                    body.Velocity = new(WALLJUMP_HSPEED * (FacingRight ? 1 : -1), body.Velocity.y);
                    return;
                }
            }

            if (IsGroundpounding || IsInKnockback || CurrentPipe || JumpLandingTimer.IsActive(Runner) || !(WallJumpTimer.ExpiredOrNotRunning(Runner) || IsOnGround || body.Velocity.y < 0))
                return;

            if (!IsOnGround)
                IsSkidding = false;

            if (IsInShell) {
                body.Velocity = new(SPEED_STAGE_MAX[RUN_STAGE] * 0.9f * (FacingRight ? 1 : -1) * (1f - (ShellSlowdownTimer.RemainingTime(Runner) ?? 0f)), body.Velocity.y);
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

            float sign = Mathf.Sign(body.Velocity.x);
            bool uphill = Mathf.Sign(FloorAngle) == sign;

            if (!IsOnGround)
                TurnaroundBoostTime = 0;

            if (TurnaroundBoostTime > 0) {
                TurnaroundBoostTime -= Runner.DeltaTime;
                body.Velocity = new(0, body.Velocity.y);
                if (TurnaroundBoostTime < 0) {
                    IsTurnaround = true;
                    TurnaroundBoostTime = 0;
                }

            } else if (IsTurnaround) {
                float newX = body.Velocity.x + (TURNAROUND_ACC * (FacingRight ? -1 : 1) * Runner.DeltaTime);
                IsTurnaround &= IsOnGround && !IsCrouching && Mathf.Abs(body.Velocity.x) < SPEED_STAGE_MAX[1] && !HitRight && !HitLeft;
                IsSkidding &= IsTurnaround;
                body.Velocity = new(newX, body.Velocity.y);

            } else if ((left ^ right) && (!IsCrouching || (IsCrouching && !IsOnGround && State != Enums.PowerupState.BlueShell)) && !IsInKnockback && !IsSliding) {
                // We can walk here

                float speed = Mathf.Abs(body.Velocity.x) - 0.01f;
                bool reverse = body.Velocity.x != 0 && ((left ? 1 : -1) == sign);

                // Check that we're not going above our limit
                float max = maxArray[maxStage] + CalculateSlopeMaxSpeedOffset(Mathf.Abs(FloorAngle) * (uphill ? 1 : -1));
                if (speed > max) {
                    float maxDeceleration = (speed - max) * Runner.TickRate;
                    acc = Mathf.Clamp(-acc, -maxDeceleration, maxDeceleration);
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
                float newX = body.Velocity.x + acc * Runner.DeltaTime * direction;

                if ((body.Velocity.x < max && newX > max) || (body.Velocity.x > -max && newX < -max)) {
                    newX = Mathf.Clamp(newX, -max, max);
                }

                if (IsSkidding && !IsTurnaround && (Mathf.Sign(newX) != sign || speed < 0.05f)) {
                    // Turnaround
                    TurnaroundBoostTime = 0.1667f;
                    newX = 0;
                }

                body.Velocity = new(newX, body.Velocity.y);

            } else if (IsOnGround || IsSwimming) {
                // Not holding anything, sliding, or holding both directions. decelerate
                IsSkidding = false;
                IsTurnaround = false;

                float angle = Mathf.Abs(FloorAngle);
                if (IsSwimming)
                    acc = -SWIM_BUTTON_RELEASE_DEC;
                else if (IsSliding) {
                    if (angle > slopeSlidingAngle) {
                        // Uphill / downhill
                        acc = (angle > 30 ? SLIDING_45_ACC : SLIDING_22_ACC) * (uphill ? -1 : 1);
                    } else {
                        // Flat ground
                        acc = -SPEED_STAGE_ACC[0];
                    }
                } else if (OnIce)
                    acc = -BUTTON_RELEASE_ICE_DEC[stage];
                else if (IsInKnockback)
                    acc = -KNOCKBACK_DEC;
                else
                    acc = -BUTTON_RELEASE_DEC;

                int direction = (int) Mathf.Sign(body.Velocity.x);
                float newX = body.Velocity.x + acc * Runner.DeltaTime * direction;

                float target = (angle > 30 && OnSlope) ? Math.Sign(FloorAngle) * -SPEED_STAGE_MAX[0] : 0;
                if ((direction == -1) ^ (newX <= target))
                    newX = target;

                if (IsSliding) {
                    newX = Mathf.Clamp(newX, -SPEED_SLIDE_MAX, SPEED_SLIDE_MAX);
                }

                body.Velocity = new(newX, body.Velocity.y);

                if (newX != 0)
                    FacingRight = newX > 0;
            }

            IsInShell |= State == Enums.PowerupState.BlueShell && !IsSliding && IsOnGround && IsFunctionallyRunning
                && !HeldEntity && Mathf.Abs(body.Velocity.x) >= SPEED_STAGE_MAX[RUN_STAGE] * 0.9f
                && (body.Velocity.x > 0) == FacingRight;
        }

        private float CalculateSlopeMaxSpeedOffset(float floorAngle) {
            return (float) (-0.0304687 * floorAngle);
        }


        private static readonly Vector2 StuckInBlockSizeCheck = new(0.9f, 0.9f);
        private bool HandleStuckInBlock() {
            if (IsFrozen || CurrentPipe || MegaStartTimer.IsActive(Runner) || (MegaEndTimer.IsActive(Runner) && IsStationaryMegaShrink))
                return false;

            Vector2 checkSize = WorldHitboxSize * StuckInBlockSizeCheck;
            Vector2 origin = body.Position + (Vector2.up * WorldHitboxSize * 0.5f);

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
                body.Position = body.PreviousTickPosition;
                origin = body.Position + (checkSize * 0.5f * Vector2.up);

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
                        body.Position = checkPos + (Vector2.down * checkSize * 0.5f);
                        IsStuckInBlock = false;
                        return false;
                    }
                }

                body.Position = body.PreviousTickPosition;
            }

            body.Gravity = Vector2.zero;
            body.Velocity = Vector2.right * 2f;
            return true;
        }

        public void FinishMegaMario(bool success) {
            if (success) {
                MegaTimer = TickTimer.CreateFromSeconds(Runner, 15f);
                body.Freeze = false;
            } else {
                // Hit a ceiling, cancel
                State = Enums.PowerupState.Mushroom;
                MegaEndTimer = TickTimer.CreateFromSeconds(Runner, megaStartTime - MegaStartTimer.RemainingTime(Runner) ?? 0f);
                MegaStartTimer = TickTimer.None;
                MegaTimer = TickTimer.None;
                body.Freeze = true;
                IsStationaryMegaShrink = true;
                StoredPowerup = Enums.PowerupState.MegaMushroom;
            }
        }

        private void HandleFacingDirection(bool left, bool right) {
            if (IsGroundpounding && !IsOnGround)
                return;

            if (WallJumpTimer.IsActive(Runner)) {
                FacingRight = body.Velocity.x > 0;
            } else if (!IsInShell && !IsSliding && !IsSkidding && !IsInKnockback && !(animator.GetCurrentAnimatorStateInfo(0).IsName("turnaround") || IsTurnaround)) {
                if (right ^ left)
                    FacingRight = right;
            } else if (MegaStartTimer.ExpiredOrNotRunning(Runner) && MegaEndTimer.ExpiredOrNotRunning(Runner) && !IsSkidding && !(animator.GetCurrentAnimatorStateInfo(0).IsName("turnaround") || IsTurnaround)) {
                if (IsInKnockback || (IsOnGround && State != Enums.PowerupState.MegaMushroom && Mathf.Abs(body.Velocity.x) > 0.05f && !IsCrouching)) {
                    FacingRight = body.Velocity.x > 0;
                } else if ((!IsInShell || MegaStartTimer.IsActive(Runner)) && (right || left)) {
                    FacingRight = right;
                }
                if (!IsInShell && ((Mathf.Abs(body.Velocity.x) < 0.5f && IsCrouching) || OnIce) && (right || left))
                    FacingRight = right;
            }
        }

        public void EndMega() {
            if (State != Enums.PowerupState.MegaMushroom)
                return;

            PreviousState = Enums.PowerupState.MegaMushroom;
            State = Enums.PowerupState.Mushroom;
            MegaEndTimer = TickTimer.CreateFromSeconds(Runner, megaStartTime * 0.5f);
            IsStationaryMegaShrink = false;
            DamageInvincibilityTimer = TickTimer.CreateFromSeconds(Runner, 2f);

            if (body.Velocity.y > 0)
                body.Velocity = new(body.Velocity.x, body.Velocity.y * 0.33f);
        }

        public void HandleBlockSnapping() {
            if (CurrentPipe || IsDrilling)
                return;

            if (body.Velocity.y > 0)
                // If we're about to be in the top 2 pixels of a block, snap up to it, (if we can fit)
                return;

            Vector2 nextPos = body.Position + Runner.DeltaTime * 2f * body.Velocity;

            if (!Utils.Utils.IsAnyTileSolidBetweenWorldBox(nextPos + WorldHitboxSize.y * 0.5f * Vector2.up, WorldHitboxSize))
                // We are not going to be inside a block next fixed update
                return;

            // We ARE inside a block. figure out the height of the contact
            // 32 pixels per unit
            RaycastHit2D contact = Runner.GetPhysicsScene2D().BoxCast(nextPos + 3f / 32f * Vector2.up, new(WorldHitboxSize.y, 1f / 32f), 0, Vector2.down, 3f / 32f, Layers.MaskAnyGround);

            if (!contact || contact.normal.y < 0.1f) {
                // We didn't hit the ground, we must've hit a ceiling or something.
                return;
            }

            float point = contact.point.y + Physics2D.defaultContactOffset;
            if (body.Position.y > point + Physics2D.defaultContactOffset) {
                // Dont snap when we're above the block
                return;
            }

            Vector2 newPosition = new(body.Position.x, point);

            if (Utils.Utils.IsAnyTileSolidBetweenWorldBox(newPosition + WorldHitboxSize.y * 0.5f * Vector2.up, WorldHitboxSize)) {
                // It's an invalid position anyway, we'd be inside something.
                return;
            }

            // Valid position, snap upwards
            body.Position = newPosition;
        }

        private void HandleMovement(NetworkButtons heldButtons, NetworkButtons pressedButtons) {
            float delta = Runner.DeltaTime;
            IsFunctionallyRunning = heldButtons.IsSet(PlayerControls.Sprint) || State == Enums.PowerupState.MegaMushroom || IsPropellerFlying;

            // Death via pit
            if (body.Position.y + transform.lossyScale.y < GameManager.Instance.LevelMinY) {
                Death(true, false);
                return;
            }

            if (HeldEntity && (IsFrozen || HeldEntity.IsDead || HeldEntity.IsFrozen))
                SetHeldEntity(null);

            #region // -- MEGA MUSHROOM START / END TIMERS
            if (MegaStartTimer.IsRunning) {

                body.Freeze = true;
                body.Velocity = Vector2.zero;

                if (MegaStartTimer.Expired(Runner)) {
                    FinishMegaMario(true);
                    MegaStartTimer = TickTimer.None;
                } else {
                    body.Freeze = true;

                    Vector2 checkExtents = WorldHitboxSize * new Vector2(0.375f, 0.55f);
                    Vector2 checkPosition = body.Position + Vector2.up * checkExtents;

                    Vector2Int minPos = Utils.Utils.WorldToTilemapPosition(checkPosition - checkExtents, wrap: false);
                    Vector2Int size = Utils.Utils.WorldToTilemapPosition(checkPosition + checkExtents, wrap: false) - minPos;

                    for (int x = 0; x <= size.x; x++) {
                        Vector2Int tileLocation = new(minPos.x + x, minPos.y + size.y);
                        Utils.Utils.WrapTileLocation(ref tileLocation);
                        TileBase tile = Utils.Utils.GetTileAtTileLocation(tileLocation);

                        bool cancelMega;
                        if (tile is BreakableBrickTile bbt)
                            cancelMega = !bbt.breakableByMegaMario;
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

            if (MegaEndTimer.IsRunning && IsStationaryMegaShrink) {
                body.Velocity = Vector2.zero;

                if (MegaEndTimer.Expired(Runner)) {
                    DamageInvincibilityTimer = TickTimer.CreateFromSeconds(Runner, 2f);
                    body.Velocity = Vector2.zero;
                    animator.enabled = true;
                    body.Freeze = false;
                    State = PreviousState;
                    MegaEndTimer = TickTimer.None;
                    IsStationaryMegaShrink = false;
                }
                return;
            }
            #endregion

            if (State == Enums.PowerupState.MegaMushroom) {
                HandleMegaTiles(true);
                if (Runner.IsForward && IsOnGround && JumpState == PlayerJumpState.SingleJump) {
                    DustParticleAnimCounter++;
                    CameraController.ScreenShake = 0.15f;
                    JumpState = PlayerJumpState.None;
                }
                StarmanTimer = TickTimer.None;

                if (MegaTimer.ExpiredOrNotRunning(Runner)) {
                    EndMega();
                    MegaTimer = TickTimer.None;
                }
            }

            // Pipes > stuck in block, else the animation gets janked.
            if (CurrentPipe || MegaStartTimer.IsActive(Runner) || (MegaEndTimer.IsActive(Runner) && IsStationaryMegaShrink) || animator.GetBool("pipe"))
                return;

            // Don't do anything if we're stuck in a block
            if (HandleStuckInBlock())
                return;

            if (IsInKnockback) {
                if (DoEntityBounce)
                    ResetKnockback();

                WallSlideLeft = false;
                WallSlideRight = false;
                IsCrouching = false;
                IsInShell = false;
                body.Velocity -= body.Velocity * (delta * 2f);

                float timeStunned = (Runner.Tick - KnockbackTick) * Runner.DeltaTime;

                if ((IsSwimming && timeStunned > 1.5f) || (!IsSwimming && IsOnGround && Mathf.Abs(body.Velocity.x) < 0.35f && timeStunned > 0.5f))
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

            // Jump Buffering
            if (pressedButtons.IsSet(PlayerControls.Jump) && !IsOnGround) {
                // 0.15s buffer time
                JumpBufferTime = Runner.SimulationTime + 0.15f;
            }

            bool jumpPressed = pressedButtons.IsSet(PlayerControls.Jump);
            bool canJump = jumpPressed || (Runner.SimulationTime <= JumpBufferTime && (IsOnGround || WallSliding));
            bool doJump = (canJump && (IsOnGround || Runner.SimulationTime <= CoyoteTime)) || (!IsSwimming && SwimJump);
            bool doWalljump = canJump && !IsOnGround && WallSliding;
            bool doGroundpound = pressedButtons.IsSet(PlayerControls.Down) || (IsPropellerFlying && heldButtons.IsSet(PlayerControls.Down));

            /*
            //GROUNDPOUND BUFFERING
            if (pressedButtons.IsSet(PlayerControls.Down)) {
                GroundpoundStartTime = Runner.SimulationTime + 0.08f;
                GroundpoundHeld = true;
            }
            //dont groundpound if we're holding another direction
            if (!down || (!IsPropellerFlying && (left || right || up)))
                GroundpoundHeld = false;
            bool doGroundpound = GroundpoundHeld && Runner.SimulationTime >= GroundpoundStartTime;
            */

            SwimJump = false;

            // Activate blocks jumped into
            if (body.Data.HitRoof && !IsStuckInBlock) {
                bool tempHitBlock = false;
                bool interactedAny = false;
                foreach (PhysicsDataStruct.TileContact tile in body.Data.TilesHitRoof) {
                    Vector2Int pos = tile.location;
                    tempHitBlock |= InteractWithTile(pos, InteractionDirection.Up, out bool interacted, out bool bumpSound);
                    if (bumpSound)
                        BlockBumpSoundCounter++;

                    interactedAny |= interacted;
                }
                if (!interactedAny) {
                    BlockBumpSoundCounter++;
                }

                body.Velocity = new(body.Velocity.x, Mathf.Min(body.Velocity.y, IsSwimming && !tempHitBlock ? -2f : -0.1f));
            }

            if (IsDrilling) {
                PropellerSpinTimer = TickTimer.None;
                if (IsPropellerFlying && PropellerDrillCooldown.ExpiredOrNotRunning(Runner)) {
                    if (!down) {
                        IsDrilling = false;
                        PropellerDrillCooldown = TickTimer.CreateFromSeconds(Runner, 0.2f);
                    }
                }
            }

            if (IsPropellerFlying) {
                if (PropellerLaunchTimer.IsActive(Runner)) {
                    IsSwimming = false;
                    float remainingTime = PropellerLaunchTimer.RemainingTime(Runner) ?? 0f;
                    if (remainingTime > 0.9f) {
                        body.Velocity = new(body.Velocity.x, propellerLaunchVelocity);
                    } else {
                        float targetVelocity = propellerLaunchVelocity - (remainingTime < 0.4f ? (1 - (remainingTime * 2.5f)) * propellerLaunchVelocity : 0);
                        body.Velocity = new(body.Velocity.x, Mathf.Min(body.Velocity.y + (24f * Runner.DeltaTime), targetVelocity));
                    }

                    if (IsOnGround)
                        body.Position += Vector2.up * 0.05f;
                } else if (((jumpHeld && Settings.Instance.controlsPropellerJump) || powerupAction) && !IsDrilling && body.Velocity.y < -0.1f && (PropellerSpinTimer.RemainingTime(Runner) ?? 0f) < propellerSpinTime * 0.25f) {
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

            IsWaterWalking &= State == Enums.PowerupState.MiniMushroom && (Mathf.Abs(body.Velocity.x) > 0.3f || left || right);
            if (IsSwimming) {
                bool paddle = pressedButtons.IsSet(PlayerControls.Jump);
                HandleSwimming(left, right, down, paddle, jumpHeld);
                return;
            }

            if (State == Enums.PowerupState.BlueShell) {
                IsInShell &= IsFunctionallyRunning;

                if (IsInShell) {
                    down = true;

                    if (HitLeft || HitRight) {
                        bool interactedAny = false;
                        foreach (PhysicsDataStruct.TileContact tile in body.Data.TilesHitSide) {
                            InteractWithTile(tile.location, tile.direction, out bool interacted, out bool bumpSound);
                            if (bumpSound)
                                BlockBumpSoundCounter++;

                            interactedAny |= interacted;
                        }
                        if (!interactedAny) {
                            BlockBumpSoundCounter++;
                        }
                        GlobalController.Instance.rumbleManager.RumbleForSeconds(0.3f, 0.5f, 0.2f, RumbleManager.RumbleSetting.Low);
                        FacingRight = HitLeft;
                    }
                }
            }

            // Ground
            if (IsOnGround) {
                CoyoteTime = -1;
                if (TimeGrounded == -1)
                    TimeGrounded = Runner.SimulationTime;

                if (Runner.SimulationTime - TimeGrounded > 0.15f)
                    JumpState = PlayerJumpState.None;

                if (body.Data.HitRoof && IsOnGround && body.Data.CrushableGround && body.Velocity.y <= 0.1 && State != Enums.PowerupState.MegaMushroom) {
                    // Crushed.
                    Powerdown(true);
                }

                UsedPropellerThisJump = false;
                WallSlideLeft = false;
                WallSlideRight = false;
                IsJumping = false;
                if (IsDrilling) {
                    DustParticleAnimCounter++;
                }

                if (OnSpinner && Mathf.Abs(body.Velocity.x) < 0.3f && !HeldEntity) {
                    Transform spnr = OnSpinner.transform;
                    float diff = body.Position.x - spnr.transform.position.x;
                    if (Mathf.Abs(diff) >= 0.02f)
                        body.Position += -0.6f * Mathf.Sign(diff) * delta * Vector2.right;
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

            if (IsOnGround) {
                if ((TimeGrounded == Runner.SimulationTime) && !IsGroundpounding && !IsCrouching && !IsInShell && !HeldEntity && State != Enums.PowerupState.MegaMushroom) {
                    bool edge = !Runner.GetPhysicsScene2D().BoxCast(body.Position, MainHitbox.size * 0.75f, 0, Vector2.down, 0, Layers.MaskAnyGround);
                    bool edgeLanding = false;
                    if (edge) {
                        bool rightEdge = edge && Utils.Utils.IsTileSolidAtWorldLocation(body.Position + new Vector2(0.25f, -0.25f));
                        bool leftEdge = edge && Utils.Utils.IsTileSolidAtWorldLocation(body.Position + new Vector2(-0.25f, -0.25f));
                        edgeLanding = (leftEdge || rightEdge) && ProperJump && edge && (FacingRight == rightEdge);
                    }

                    if ((JumpState == PlayerJumpState.TripleJump && !(left ^ right))
                        || edgeLanding
                        || (Mathf.Abs(body.Velocity.x) < 0.1f)) {

                        if (!OnIce)
                            body.Velocity = Vector2.zero;

                        JumpLandingAnimCounter++;

                        if (edgeLanding)
                            JumpLandingTimer = TickTimer.CreateFromSeconds(Runner, 0.15f);
                    }
                }
            }

            HandleSlopes();

            HandleGroundpound(doGroundpound, left, right);

            HandleSliding(up, down, left, right);

            if (!(IsGroundpounding && !IsOnGround)) {
                // Normal walking/running
                HandleWalkingRunning(left, right);
            }

            HandleSlopes();

            if (!(IsGroundpounding && !IsOnGround && !DoEntityBounce)) {
                // Jumping
                HandleJumping(jumpHeld, doJump, down);
            }

            HandleFacingDirection(left, right);

            HandleGravity(jumpHeld);

            if (IsOnGround) {
                if (IsPropellerFlying) {
                    float remainingTime = PropellerLaunchTimer.RemainingTime(Runner) ?? 0f;
                    if (remainingTime < 0.2f) {
                        IsPropellerFlying = false;
                        PropellerLaunchTimer = TickTimer.None;
                    }
                }
                IsSpinnerFlying = false;
                IsDrilling = false;
            }

            // Terminal velocity
            float terminalVelocityModifier = State switch {
                Enums.PowerupState.MiniMushroom => 0.625f,
                Enums.PowerupState.MegaMushroom => 2f,
                _ => 1f,
            };
            if (IsSpinnerFlying) {
                if (IsDrilling) {
                    body.Velocity = new(body.Velocity.x, Mathf.Max(body.Velocity.y, -drillVelocity));
                } else {
                    body.Velocity = new(body.Velocity.x, Mathf.Max(body.Velocity.y, -flyingTerminalVelocity));
                }
            } else if (IsPropellerFlying) {
                if (IsDrilling) {
                    body.Velocity = new(Mathf.Clamp(body.Velocity.x, -WalkingMaxSpeed * (1/4f), WalkingMaxSpeed * (1/4f)), Mathf.Max(body.Velocity.y, -drillVelocity));
                } else {
                    float remainingTime = PropellerLaunchTimer.RemainingTime(Runner) ?? 0f;
                    float htv = WalkingMaxSpeed * 1.18f + (remainingTime * 2f);
                    body.Velocity = new(Mathf.Clamp(body.Velocity.x, -htv, htv), Mathf.Max(body.Velocity.y, PropellerSpinTimer.IsActive(Runner) ? -propellerSpinFallSpeed : -propellerFallSpeed));
                }
            } else if (WallSliding) {
                body.Velocity = new(body.Velocity.x, Mathf.Max(body.Velocity.y, wallslideSpeed));
            } else if (IsGroundpounding) {
                body.Velocity = new(body.Velocity.x, Mathf.Max(body.Velocity.y, -groundpoundVelocity));
            } else {
                body.Velocity = new(body.Velocity.x, Mathf.Max(body.Velocity.y, terminalVelocity * terminalVelocityModifier));
            }

            if (IsCrouching || IsSliding || IsSkidding) {
                WallSlideLeft = false;
                WallSlideRight = false;
            }

            if (PreviousTickIsOnGround && !IsOnGround && !ProperJump) {
                if (IsCrouching && State != Enums.PowerupState.BlueShell && !IsGroundpounding)
                    body.Velocity = new(body.Velocity.x, -3.75f);
                else
                    body.Velocity = new(body.Velocity.x, 0f);
            }
        }

        private void HandleGravity(bool jumpHeld) {

            if ((IsGroundpounding || IsDrilling) && IsSwimming)
                return;

            if (IsOnGround) {
                body.Gravity = Vector2.up * GRAVITY_STAGE_ACC[^1];
                return;
            }

            float gravity;

            // Slow-rise check
            if (IsSpinnerFlying || IsPropellerFlying) {
                if (IsDrilling) {
                    gravity = GRAVITY_STAGE_ACC[^1];
                } else {
                    gravity = flyingGravity * Physics2D.gravity.y;
                }
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

            body.Gravity = Vector2.up * gravity;
        }

        private void HandleSwimming(bool left, bool right, bool down, bool jumpPressed, bool jumpHeld) {

            if (IsGroundpounding || IsDrilling) {
                body.Velocity = new(body.Velocity.x, body.Velocity.y + (SWIM_GROUNDPOUND_DEC * Runner.DeltaTime));
                if (body.Velocity.y >= SWIM_TERMINAL_VELOCITY_AHELD) {
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
                    body.Velocity = new(body.Velocity.x, SWIM_VSPEED);
                    DoEntityBounce = false;
                    IsOnGround = false;
                    IsCrouching = false;
                }

                if (jumpPressed) {
                    body.Velocity = new(body.Velocity.x, body.Velocity.y + SWIM_VSPEED);
                    if (IsOnGround)
                        body.Position += Vector2.up * 0.05f;

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
                body.Velocity = new(body.Velocity.x, Mathf.Clamp(body.Velocity.y, jumpHeld ? SWIM_TERMINAL_VELOCITY_AHELD : SWIM_TERMINAL_VELOCITY, SWIM_MAX_VSPEED));
        }

        private void SetHoldingOffset(bool renderTime = false) {
            if (HeldEntity is FrozenCube) {
                float time = Mathf.Clamp01(((renderTime ? Runner.LocalRenderTime : Runner.SimulationTime) - HoldStartTime) / pickupTime);
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

        private void HandleGroundpound(bool groundpoundInputted, bool left, bool right) {
            if (groundpoundInputted)
                TryStartGroundpound(left, right);

            HandleGroundpoundStartAnimation();
            HandleGroundpoundBlockCollision();
        }

        private void TryStartGroundpound(bool left, bool right) {

            if (IsOnGround || IsInKnockback || IsGroundpounding || IsDrilling
                || HeldEntity || IsCrouching || IsSliding || IsInShell
                || WallSliding || GroundpoundCooldownTimer.IsActive(Runner))
                return;

            if (!IsPropellerFlying && !IsSpinnerFlying && (left || right))
                return;

            if (IsSpinnerFlying) {
                // Start drill
                if (body.Velocity.y < 0) {
                    IsDrilling = true;
                    ContinueGroundpound = true;
                    body.Velocity = new(0, body.Velocity.y);
                }
            } else if (IsPropellerFlying) {
                // Start propeller drill
                float remainingTime = PropellerLaunchTimer.RemainingTime(Runner) ?? 0f;
                if (remainingTime < 0.2f && body.Velocity.y < 0 && PropellerDrillCooldown.ExpiredOrNotRunning(Runner)) {
                    IsDrilling = true;
                    PropellerLaunchTimer = TickTimer.None;
                    ContinueGroundpound = true;
                    PropellerDrillCooldown = TickTimer.CreateFromSeconds(Runner, 0.2f);
                }
            } else {
                // Start groundpound
                // Check if high enough above ground
                if (Runner.GetPhysicsScene().BoxCast(body.Position, WorldHitboxSize * Vector2.right * 0.5f, Vector3.down, out _, Quaternion.identity, 0.15f * (State == Enums.PowerupState.MegaMushroom ? 2.5f : 1), Layers.MaskAnyGround))
                    return;

                WallSlideLeft = false;
                WallSlideRight = false;
                IsGroundpounding = true;
                JumpState = PlayerJumpState.None;
                ContinueGroundpound = true;
                IsSliding = false;
                body.Velocity = Vector2.up * 1.5f;
                GroundpoundStartTimer = TickTimer.CreateFromSeconds(Runner, groundpoundTime * (State == Enums.PowerupState.MegaMushroom ? 1.5f : 1));
            }
        }

        private void HandleGroundpoundStartAnimation() {

            if (!IsGroundpounding || !GroundpoundStartTimer.IsRunning)
                return;

            if (GroundpoundStartTimer.Expired(Runner)) {
                body.Velocity = Vector2.down * groundpoundVelocity;
                GroundpoundStartTimer = TickTimer.None;
                return;
            }

            if (GroundpoundStartTimer.RemainingTime(Runner) > .066f) {
                body.Velocity = GroundpoundStartUpwardsVelocity;
            } else {
                body.Velocity = Vector2.zero;
            }
        }

        private void HandleGroundpoundBlockCollision() {

            if (!(IsOnGround && (IsGroundpounding || IsDrilling) && ContinueGroundpound))
                return;

            if (!IsDrilling)
                GroundpoundAnimCounter++;

            ContinueGroundpound = false;
            foreach (PhysicsDataStruct.TileContact tile in body.Data.TilesStandingOn) {
                ContinueGroundpound |= InteractWithTile(tile.location, InteractionDirection.Down, out bool _, out bool bumpSound);
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

        //---RPCs
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        public void Rpc_SpawnCoinEffects(Vector3 position, byte coins, bool final) {
            PlaySound(Enums.Sounds.World_Coin_Collect);

            if (cameraController.IsControllingCamera)
                GlobalController.Instance.rumbleManager.RumbleForSeconds(0f, 0.1f, 0.05f, RumbleManager.RumbleSetting.High);

            NumberParticle num = Instantiate(PrefabList.Instance.Particle_CoinNumber, position, Quaternion.identity).GetComponentInChildren<NumberParticle>();
            num.ApplyColorAndText(Utils.Utils.GetSymbolString(coins.ToString(), Utils.Utils.numberSymbols), animationController.GlowColor, final);
        }

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        public void Rpc_SpawnReserveItem() {

            if (StoredPowerup == Enums.PowerupState.NoPowerup || IsDead || MegaStartTimer.IsActive(Runner) || (IsStationaryMegaShrink && MegaEndTimer.IsActive(Runner)))
                return;

            SpawnItem(StoredPowerup.GetPowerupScriptable().prefab);
            StoredPowerup = Enums.PowerupState.NoPowerup;
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        public void Rpc_DisconnectDeath() {
            Disconnected = true;
            Lives = 0;
            Death(false, false);
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        public void Rpc_PlaySound(Enums.Sounds sound, byte variant = 0, float volume = 1) {
            PlaySound(sound, variant, volume);
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        public void Rpc_PlayDeathSound() {
            PlaySound(cameraController.IsControllingCamera ? Enums.Sounds.Player_Sound_Death : Enums.Sounds.Player_Sound_DeathOthers);
        }

        //---OnChangeds
        protected override void HandleRenderChanges(bool fillBuffer, ref NetworkBehaviourBuffer oldBuffer, ref NetworkBehaviourBuffer newBuffer) {
            base.HandleRenderChanges(fillBuffer, ref oldBuffer, ref newBuffer);

            foreach (var change in ChangesBuffer) {
                switch (change) {
                case nameof(IsGroundpounding): OnGroundpoundingChanged(); break;
                case nameof(GroundpoundAnimCounter): OnGroundpoundAnimCounterChanged(); break;
                case nameof(WallJumpTimer): {
                    OnWallJumpTimerChanged(GetPropertyReader<NetworkBool>(nameof(WallSlideLeft)).Read(oldBuffer));
                    break;
                }
                case nameof(IsDead): OnIsDeadChanged(); break;
                case nameof(DeathAnimationTimer): OnDeathAnimationTimerChanged(); break;
                case nameof(IsRespawning): OnIsRespawningChanged(); break;
                case nameof(FireballAnimCounter): OnFireballAnimCounterChanged(); break;
                case nameof(IsSliding): OnIsSlidingChanged(); break;
                case nameof(IsCrouching): OnIsCrouchingChanged(); break;
                case nameof(PipeTimer): OnPipeTimerChanged(); break;
                case nameof(PropellerLaunchTimer): OnPropellerLaunchTimerChanged(); break;
                case nameof(PropellerSpinTimer): OnPropellerSpinTimerChanged(); break;
                case nameof(SpinnerLaunchAnimCounter): OnSpinnerLaunchAnimCounterChanged(); break;
                case nameof(DustParticleAnimCounter): OnDustParticleAnimCounterChanged(); break;
                case nameof(JumpAnimCounter): OnJumpAnimCounterChanged(); break;
                case nameof(JumpLandingAnimCounter): OnJumpLandingAnimCounterChanged(); break;
                case nameof(IsWaterWalking): OnIsWaterWalkingChanged(); break;
                case nameof(KnockbackAnimCounter): OnKnockbackAnimCounterChanged(); break;
                case nameof(BlockBumpSoundCounter): OnBlockBumpSoundCounterChanged(); break;
                case nameof(ThrowAnimCounter): OnThrowAnimCounterChanged(); break;
                case nameof(MegaTimer): OnMegaTimerChanged(); break;
                case nameof(MegaStartTimer): OnMegaStartTimerChanged(); break;
                case nameof(IsStationaryMegaShrink): OnIsStationaryMegaShrinkChanged(); break;
                // case nameof(IsFrozen): OnIsFrozenChanged(); break;
                case nameof(HeldEntity): OnHeldEntityChanged(); break;
                }
            }
        }

        public void OnGroundpoundingChanged() {
            if (GameManager.Instance.GameState != Enums.GameState.Playing)
                return;

            if (!IsGroundpounding)
                return;

            PlaySound(Enums.Sounds.Player_Sound_GroundpoundStart);
        }

        public void OnGroundpoundAnimCounterChanged() {
            if (GameManager.Instance.GameState != Enums.GameState.Playing)
                return;

            // Groundpound
            if (State != Enums.PowerupState.MegaMushroom) {
                Enums.Sounds sound = State switch {
                    Enums.PowerupState.MiniMushroom => Enums.Sounds.Powerup_MiniMushroom_Groundpound,
                    _ => Enums.Sounds.Player_Sound_GroundpoundLanding,
                };
                PlaySound(sound);
                SpawnParticle(PrefabList.Instance.Particle_Groundpound, body.Position);

                if (cameraController.IsControllingCamera)
                    GlobalController.Instance.rumbleManager.RumbleForSeconds(0.3f, 0.5f, 0.2f, RumbleManager.RumbleSetting.Low);
            } else {
                CameraController.ScreenShake = 0.15f;
            }

            if (!ContinueGroundpound && State == Enums.PowerupState.MegaMushroom) {
                PlaySound(Enums.Sounds.Powerup_MegaMushroom_Groundpound);
                SpawnParticle(PrefabList.Instance.Particle_Groundpound, body.Position);
                CameraController.ScreenShake = 0.35f;

                if (cameraController.IsControllingCamera)
                    GlobalController.Instance.rumbleManager.RumbleForSeconds(0.8f, 0.3f, 0.5f, RumbleManager.RumbleSetting.Low);
            }
        }

        public void OnWallJumpTimerChanged(bool previousWallSlideLeft) {
            if (GameManager.Instance.GameState != Enums.GameState.Playing)
                return;

            if (!WallJumpTimer.IsRunning)
                return;

            Vector2 offset = MainHitbox.size * 0.5f;
            offset.x *= previousWallSlideLeft ? -1 : 1;

            PlaySound(Enums.Sounds.Player_Sound_WallJump);
            PlaySound(Enums.Sounds.Player_Voice_WallJump, (byte) GameManager.Instance.random.RangeExclusive(1, 3));

            animator.SetTrigger("walljump");
        }

        private float lastRespawnParticle;
        public void OnIsDeadChanged() {
            if (GameManager.Instance.GameState != Enums.GameState.Playing)
                return;

            animator.SetBool("dead", IsDead);

            if (IsDead) {
                if (GameManager.Instance.GameState < Enums.GameState.Playing)
                    return;

                animator.Play("deadstart");
                animator.SetBool("knockback", false);
                animator.SetBool("flying", false);
                animator.SetBool("firedeath", FireDeath);
                //PlaySound(cameraController.IsControllingCamera ? Enums.Sounds.Player_Sound_Death : Enums.Sounds.Player_Sound_DeathOthers);

                if (HasInputAuthority)
                    ScoreboardUpdater.Instance.OnDeathToggle();
            } else {
                // Respawn poof particle
                if (Mathf.Abs(lastRespawnParticle - Runner.SimulationTime) > 2) {
                    GameManager.Instance.particleManager.Play(Enums.Particle.Generic_Puff, Spawnpoint);
                    lastRespawnParticle = Runner.SimulationTime;
                }
            }
        }

        public void OnDeathAnimationTimerChanged() {
            if (GameManager.Instance.GameState != Enums.GameState.Playing)
                return;

            if (DeathAnimationTimer.IsRunning) {
                // Player initial death animation
                animator.Play("deadstart");

            } else if (!DeathplaneDeath) {
                // Play second half of death animation
                animator.SetTrigger("deathup");

                if (FireDeath) {
                    PlaySound(Enums.Sounds.Player_Voice_LavaDeath);
                    PlaySound(Enums.Sounds.Player_Sound_LavaHiss);
                }
            }
        }

        private RespawnParticle respawnParticle;
        public void OnIsRespawningChanged() {
            if (!IsRespawning || respawnParticle)
                return;

            respawnParticle = Instantiate(PrefabList.Instance.Particle_Respawn, Spawnpoint, Quaternion.identity);
            respawnParticle.player = this;
        }

        public void OnFireballAnimCounterChanged() {
            if (GameManager.Instance.GameState != Enums.GameState.Playing)
                return;

            animator.SetTrigger("fireball");
            sfx.PlayOneShot(State == Enums.PowerupState.IceFlower ? Enums.Sounds.Powerup_Iceball_Shoot : Enums.Sounds.Powerup_Fireball_Shoot);
        }

        public void OnIsSlidingChanged() {
            if (GameManager.Instance.GameState != Enums.GameState.Playing)
                return;

            if (IsSliding)
                return;

            if (!IsOnGround || Mathf.Abs(body.Velocity.x) > 0.2f)
                return;

            PlaySound(Enums.Sounds.Player_Sound_SlideEnd);
        }

        public void OnIsCrouchingChanged() {
            if (GameManager.Instance.GameState != Enums.GameState.Playing)
                return;

            if (!IsCrouching)
                return;

            PlaySound(State == Enums.PowerupState.BlueShell ? Enums.Sounds.Powerup_BlueShell_Enter : Enums.Sounds.Player_Sound_Crouch);
        }

        public void OnPipeTimerChanged() {
            if (GameManager.Instance.GameState != Enums.GameState.Playing)
                return;

            if (!PipeTimer.IsRunning)
                return;

            PlaySound(Enums.Sounds.Player_Sound_Powerdown);
        }

        public void OnPropellerLaunchTimerChanged() {
            if (GameManager.Instance.GameState != Enums.GameState.Playing)
                return;

            if (!PropellerLaunchTimer.IsRunning)
                return;

            PlaySound(Enums.Sounds.Powerup_PropellerMushroom_Start);
        }

        public void OnPropellerSpinTimerChanged() {
            if (GameManager.Instance.GameState != Enums.GameState.Playing)
                return;

            if (!PropellerSpinTimer.IsRunning)
                return;

            PlaySound(Enums.Sounds.Powerup_PropellerMushroom_Spin);
        }

        public void OnSpinnerLaunchAnimCounterChanged() {
            if (GameManager.Instance.GameState != Enums.GameState.Playing)
                return;

            PlaySound(Enums.Sounds.Player_Voice_SpinnerLaunch);
            PlaySound(Enums.Sounds.World_Spinner_Launch);
        }

        public void OnDustParticleAnimCounterChanged() {
            if (GameManager.Instance.GameState != Enums.GameState.Playing)
                return;

            SpawnParticle(PrefabList.Instance.Particle_Groundpound, body.Position);
        }

        public void OnJumpAnimCounterChanged() {
            if (GameManager.Instance.GameState != Enums.GameState.Playing)
                return;

            if (IsSwimming) {
                // Paddle
                PlaySound(Enums.Sounds.Player_Sound_Swim);
                animator.SetTrigger("paddle");
                return;
            }

            if (!IsJumping)
                return;

            // Voice SFX
            switch (JumpState) {
            case PlayerJumpState.DoubleJump:
                PlaySound(Enums.Sounds.Player_Voice_DoubleJump, (byte) GameManager.Instance.random.RangeExclusive(1, 3));
                break;
            case PlayerJumpState.TripleJump:
                PlaySound(Enums.Sounds.Player_Voice_TripleJump);
                break;
            }

            if (BounceJump) {
                PlaySound(Enums.Sounds.Enemy_Generic_Stomp);

                if (cameraController.IsControllingCamera)
                    GlobalController.Instance.rumbleManager.RumbleForSeconds(0.1f, 0.4f, 0.15f, RumbleManager.RumbleSetting.Low);
                return;
            }

            // Jump SFX
            Enums.Sounds sound = State switch {
                Enums.PowerupState.MiniMushroom => Enums.Sounds.Powerup_MiniMushroom_Jump,
                Enums.PowerupState.MegaMushroom => Enums.Sounds.Powerup_MegaMushroom_Jump,
                _ => Enums.Sounds.Player_Sound_Jump,
            };
            PlaySound(sound);
        }

        public void OnJumpLandingAnimCounterChanged() {
            animator.Play("jumplanding" + (JumpLandingTimer.IsActive(Runner) ? "-edge" : ""));
        }

        public void OnIsWaterWalkingChanged() {
            if (GameManager.Instance.GameState != Enums.GameState.Playing)
                return;
;
            if (!IsWaterWalking)
                return;

            PlaySound(Enums.Sounds.Powerup_MiniMushroom_WaterWalk);
        }

        public void OnKnockbackAnimCounterChanged() {
            if (GameManager.Instance.GameState != Enums.GameState.Playing)
                return;

            if (!IsInKnockback)
                return;

            if (KnockbackAttacker)
                SpawnParticle("Prefabs/Particle/PlayerBounce", KnockbackAttacker.transform.position);

            PlaySound(IsWeakKnockback ? Enums.Sounds.Player_Sound_Collision_Fireball : Enums.Sounds.Player_Sound_Collision, 0, 3);

            if (cameraController.IsControllingCamera)
                GlobalController.Instance.rumbleManager.RumbleForSeconds(0.3f, 0.6f, IsWeakKnockback ? 0.3f : 0.5f, RumbleManager.RumbleSetting.Low);
        }

        private float timeSinceLastBumpSound;
        public void OnBlockBumpSoundCounterChanged() {
            if (GameManager.Instance.GameState != Enums.GameState.Playing)
                return;

            if (timeSinceLastBumpSound + 0.2f > Runner.LocalRenderTime)
                return;

            PlaySound(Enums.Sounds.World_Block_Bump);
            timeSinceLastBumpSound = Runner.LocalRenderTime;
        }

        public void OnThrowAnimCounterChanged() {
            if (GameManager.Instance.GameState != Enums.GameState.Playing)
                return;

            PlaySound(Enums.Sounds.Player_Voice_WallJump, 2);
            animator.SetTrigger("throw");
        }

        public void OnMegaTimerChanged() {
            if (GameManager.Instance.GameState != Enums.GameState.Playing)
                return;

            if (IsDead)
                return;

            PlaySoundEverywhere(MegaTimer.IsRunning ? Enums.Sounds.Player_Voice_MegaMushroom : Enums.Sounds.Powerup_MegaMushroom_End);
        }

        public void OnMegaStartTimerChanged() {
            if (GameManager.Instance.GameState != Enums.GameState.Playing)
                return;

            if (MegaStartTimer.ExpiredOrNotRunning(Runner))
                return;

            animator.Play("mega-scale");
        }

        public void OnIsStationaryMegaShrinkChanged() {
            if (GameManager.Instance.GameState != Enums.GameState.Playing)
                return;

            if (!IsStationaryMegaShrink)
                return;

            animator.enabled = true;
            animator.Play("mega-cancel", 0, 1f - ((MegaEndTimer.RemainingTime(Runner) ?? 0f) / megaStartTime));
            PlaySound(Enums.Sounds.Player_Sound_PowerupReserveStore);
        }

        public override void OnIsFrozenChanged() {
            animator.enabled = !IsFrozen;
            animator.Play("falling");
            animator.Update(0f);

            if (!IsFrozen && cameraController.IsControllingCamera)
                GlobalController.Instance.rumbleManager.RumbleForSeconds(0f, 0.2f, 0.3f, RumbleManager.RumbleSetting.High);
        }

        public void OnHeldEntityChanged() {
            if (GameManager.Instance.GameState != Enums.GameState.Playing)
                return;

            if (!HeldEntity)
                return;

            if (HeldEntity is FrozenCube) {
                animator.Play("head-pickup");
                animator.ResetTrigger("fireball");
                PlaySound(Enums.Sounds.Player_Voice_DoubleJump, 2);
            }
            animator.ResetTrigger("throw");
        }

        //---Debug
#if UNITY_EDITOR
        private readonly List<Renderer> renderers = new();
        public void OnDrawGizmos() {
            if (!body || !Object)
                return;

            Gizmos.color = Color.white;
            Gizmos.DrawRay(body.Position, body.Velocity);
            Gizmos.DrawCube(body.Position + new Vector2(0, WorldHitboxSize.y * 0.5f) + (body.Velocity * Runner.DeltaTime), WorldHitboxSize);

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
