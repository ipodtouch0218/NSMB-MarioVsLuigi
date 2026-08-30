using NSMB.Cameras;
using NSMB.Particles;
using NSMB.Quantum;
using NSMB.Sound;
using NSMB.UI;
using NSMB.UI.Game;
using NSMB.Utilities;
using NSMB.Utilities.Extensions;
using Photon.Deterministic;
using Quantum;
using Quantum.Profiling;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Scripting;
using static NSMB.Utilities.QuantumViewUtils;
using Input = Quantum.Input;

namespace NSMB.Entities.Player {
    public unsafe class MarioPlayerAnimator : QuantumEntityViewComponent<StageContext> {

        //---Static
        public static readonly HashSet<MarioPlayerAnimator> AllMarioPlayers = new();
        public static event Action<QuantumGame, Frame, MarioPlayerAnimator> MarioPlayerInitialized;
        public static event Action<QuantumGame, Frame, MarioPlayerAnimator> MarioPlayerDestroyed;
        public static event Action<EntityRef> OnStartBlink;

        //---Static Variables
        private static readonly WaitForSeconds BlinkDelay = new(0.1f);

        #region Animator & Shader Hashes
        private static readonly int ParamEyeState = Shader.PropertyToID("_EyeState");
        private static readonly int ParamModelScale = Shader.PropertyToID("ModelScale");
        private static readonly int ParamMultiplyColor = Shader.PropertyToID("_MultiplyColor");
        private static readonly int ParamOverallsColor = Shader.PropertyToID("_OverallsColor");
        private static readonly int ParamShirtColor = Shader.PropertyToID("_ShirtColor");
        private static readonly int ParamCapUsesOverallsColor = Shader.PropertyToID("_CapUsesOverallsColor");
        private static readonly int ParamGlowColor = Shader.PropertyToID("GlowColor");

        private static readonly int StateFalling = Animator.StringToHash("falling");
        private static readonly int StateMegaIdle = Animator.StringToHash("mega-idle");
        private static readonly int StateMegaScale = Animator.StringToHash("mega-scale");
        private static readonly int StateMegaCancel = Animator.StringToHash("mega-cancel");
        private static readonly int StateJumplanding = Animator.StringToHash("jumplanding");
        private static readonly int StateJumplandingEdge = Animator.StringToHash("jumplanding-edge");

        private static readonly int ParamVelocityX = Animator.StringToHash("velocityX");
        private static readonly int ParamVelocityY = Animator.StringToHash("velocityY");
        private static readonly int ParamVelocityMagnitude = Animator.StringToHash("velocityMagnitude");
        private static readonly int ParamDead = Animator.StringToHash("dead");
        private static readonly int ParamOnLeft = Animator.StringToHash("onLeft");
        private static readonly int ParamOnRight = Animator.StringToHash("onRight");
        private static readonly int ParamOnGround = Animator.StringToHash("onGround");
        private static readonly int ParamInvincible = Animator.StringToHash("invincible");
        private static readonly int ParamSkidding = Animator.StringToHash("skidding");
        private static readonly int ParamPropeller = Animator.StringToHash("propeller");
        private static readonly int ParamPropellerSpin = Animator.StringToHash("propellerSpin");
        private static readonly int ParamPropellerStart = Animator.StringToHash("propellerStart");
        private static readonly int ParamCrouching = Animator.StringToHash("crouching");
        private static readonly int ParamGroundpound = Animator.StringToHash("groundpound");
        private static readonly int ParamSliding = Animator.StringToHash("sliding");
        private static readonly int ParamKnockback = Animator.StringToHash("knockback");
        private static readonly int ParamFacingRight = Animator.StringToHash("facingRight");
        private static readonly int ParamFlying = Animator.StringToHash("flying");
        private static readonly int ParamDrill = Animator.StringToHash("drill");
        private static readonly int ParamDoubleJump = Animator.StringToHash("doublejump");
        private static readonly int ParamTripleJump = Animator.StringToHash("triplejump");
        private static readonly int ParamHolding = Animator.StringToHash("holding");
        private static readonly int ParamHeadCarry = Animator.StringToHash("head carry");
        private static readonly int ParamCarryStart = Animator.StringToHash("carry_start");
        private static readonly int ParamPipe = Animator.StringToHash("pipe");
        private static readonly int ParamBlueShell = Animator.StringToHash("blueshell");
        private static readonly int ParamMini = Animator.StringToHash("mini");
        private static readonly int ParamMega = Animator.StringToHash("mega");
        private static readonly int ParamInShell = Animator.StringToHash("inShell");
        private static readonly int ParamTurnaround = Animator.StringToHash("turnaround");
        private static readonly int ParamSwimming = Animator.StringToHash("swimming");
        private static readonly int ParamAHeld = Animator.StringToHash("a_held");
        private static readonly int ParamFireDeath = Animator.StringToHash("firedeath");
        private static readonly int ParamFireballKnockback = Animator.StringToHash("fireballKnockback");
        private static readonly int ParamKnockforwards = Animator.StringToHash("knockforwards");
        private static readonly int ParamPushing = Animator.StringToHash("pushing");
        private static readonly int ParamFrozen = Animator.StringToHash("frozen");
        private static readonly int ParamPaddle = Animator.StringToHash("paddle");
        private static readonly int ParamThrow = Animator.StringToHash("throw");
        private static readonly int ParamHeadPickup = Animator.StringToHash("head-pickup");
        private static readonly int ParamFireball = Animator.StringToHash("fireball");
        private static readonly int ParamTaunt = Animator.StringToHash("taunt");
        #endregion

        //---Public Variables
        [NonSerialized] public bool wasTurnaround;

        //---Serialized Variables
        [SerializeField] private CharacterAsset character;

        [Header("Animation + Rigging")]
        [SerializeField] private GameObject modelRoot;
        [SerializeField] private Animator animator;
        [SerializeField] private GameObject largeShellExclude, propeller;

        [Header("Prefabs")]
        [SerializeField] private GameObject coinNumberParticle;
        [SerializeField] private GameObject coinFromBlockParticle, respawnParticle, starCollectParticle;

        [Header("Shaders")]
        [SerializeField] private Shader normalShader;
        [SerializeField] private Shader rainbowShader;

        [Header("Sound")]
        [SerializeField] private AudioSource sfx;
        [SerializeField] private AudioSource coinSfx;
        [SerializeField] private AudioClip normalDrill, propellerDrill;
        [SerializeField] private LoopingSoundPlayer dustPlayer, drillPlayer;
        [SerializeField] private LoopingSoundData wallSlideData, shellSlideData, spinnerDrillData, propellerDrillData;

        [Header("Gold Block")]
        [SerializeField] private Transform smallGoldBlockBone;
        [SerializeField] private Transform largeGoldBlockBone;
        [SerializeField] private Mesh goldBlockMesh;

        [Header("Particle Systems")]
        [SerializeField] private ParticleSystem dust;
        [SerializeField] private ParticleSystem sparkles, drillParticle, giantParticle, fireParticle, bubblesParticle, iceSkiddingParticle, waterRunningParticle, waterSkiddingParticle;

        [Header("Powerup Visuals")]
        [SerializeField] private PowerupVisuals fallbackPowerupVisuals;
        [SerializeField] private PowerupVisuals[] powerupVisuals;

        //---Components
        private readonly List<Renderer> renderers = new();
        private readonly Dictionary<Material, Material> clonedMaterials = new();

        //---Properties
        public Color GlowColor { get; private set; }
        public bool DisableHeadwear { get; set; }
        public Transform ActiveGoldBlockBone => smallGoldBlockBone.gameObject.activeInHierarchy ? smallGoldBlockBone : largeGoldBlockBone;
        public Mesh GoldBlockMesh => goldBlockMesh;
        public GameObject PropellerBlades => propeller;
        public Animator Animator => animator;
        public GameObject ModelRoot => modelRoot;
        public bool IsBelowDeathplane => transform.position.y <= ViewContext.Stage.StageWorldMin.Y.AsFloat;
        
        //---Private Variables
        private Enums.PlayerEyeState eyeState;
        private Quaternion modelRotationTarget;
        private bool modelRotateInstantly;
        private int footstepCounter;
        private CharacterSpecificPalette skin;
        private float lastBumpSound;
        private MaterialPropertyBlock materialBlock;
        private float teammateStompTimer;
        private float lastStompSoundTime = -1;
        private float waterSurfaceMovementDistance;
        private Vector3 previousPosition;
        private bool forceUpdate;
        private GameObject activeRespawnParticle;

        private bool previousStarmanEnabled;
        private PowerupVisuals previousPowerupVisuals;

        public void OnValidate() {
            this.SetIfNull(ref animator);
        }

        public void Awake() {
            // This has to go in awake, otherwise OnUpdateView will get called first and mess up the un-cloned materials
            renderers.AddRange(GetComponentsInChildren<MeshRenderer>(true));
            renderers.AddRange(GetComponentsInChildren<SkinnedMeshRenderer>(true));
            foreach (Renderer r in renderers) {
                // Get a copy of all materials.
                List<Material> sharedMaterials = new();
                r.GetSharedMaterials(sharedMaterials);
                for (int i = 0; i < sharedMaterials.Count; i++) {
                    Material material = sharedMaterials[i];
                    if (!clonedMaterials.TryGetValue(material, out Material clonedMaterial)) {
                        clonedMaterials[material] = clonedMaterial = Instantiate(material);
                    }
                    sharedMaterials[i] = clonedMaterial;
                }
                r.SetSharedMaterials(sharedMaterials);
            }
            foreach (PowerupVisuals visual in powerupVisuals) {
                visual.InitializeMaterials(clonedMaterials);
            }
            fallbackPowerupVisuals.InitializeMaterials(clonedMaterials);
        }

        public void Start() {
            modelRotationTarget = modelRoot.transform.rotation;

            StartCoroutine(BlinkRoutine());

            RenderPipelineManager.beginCameraRendering += URPOnPreRender;

            QuantumCallback.Subscribe<CallbackGameResynced>(this, OnGameResynced);
            QuantumEvent.Subscribe<EventMarioPlayerJumped>(this, OnMarioPlayerJumped, FilterOutReplayFastForward);
            QuantumEvent.Subscribe<EventMarioPlayerGroundpounded>(this, OnMarioPlayerGroundpounded, FilterOutReplayFastForward);
            QuantumEvent.Subscribe<EventMarioPlayerGroundpoundStarted>(this, OnMarioPlayerGroundpoundStarted, FilterOutReplayFastForward);
            QuantumEvent.Subscribe<EventMarioPlayerCrouched>(this, OnMarioPlayerCrouched, FilterOutReplayFastForward);
            QuantumEvent.Subscribe<EventMarioPlayerCollectedPowerup>(this, OnMarioPlayerCollectedPowerup, FilterOutReplayFastForward);
            QuantumEvent.Subscribe<EventMarioPlayerUsedReserveItem>(this, OnMarioPlayerUsedReserveItem, FilterOutReplayFastForward);
            QuantumEvent.Subscribe<EventMarioPlayerCollectedCoin>(this, OnMarioPlayerCollectedCoin, FilterOutReplayFastForward);
            QuantumEvent.Subscribe<EventMarioPlayerCollectedObjectiveCoin>(this, OnMarioPlayerCollectedObjectiveCoin, FilterOutReplayFastForward);
            QuantumEvent.Subscribe<EventMarioPlayerWalljumped>(this, OnMarioPlayerWalljumped, FilterOutReplayFastForward);
            QuantumEvent.Subscribe<EventMarioPlayerShotProjectile>(this, OnMarioPlayerShotProjectile, FilterOutReplayFastForward);
            QuantumEvent.Subscribe<EventMarioPlayerUsedPropeller>(this, OnMarioPlayerUsedPropeller, FilterOutReplayFastForward);
            QuantumEvent.Subscribe<EventMarioPlayerPropellerSpin>(this, OnMarioPlayerPropellerSpin, FilterOutReplayFastForward);
            QuantumEvent.Subscribe<EventMarioPlayerCollectedStar>(this, OnMarioPlayerCollectedStar, FilterOutReplayFastForward);
            QuantumEvent.Subscribe<EventMarioPlayerDied>(this, OnMarioPlayerDied);
            QuantumEvent.Subscribe<EventMarioPlayerPreRespawned>(this, OnMarioPlayerPreRespawned, FilterOutReplayFastForward);
            QuantumEvent.Subscribe<EventMarioPlayerRespawned>(this, OnMarioPlayerRespawned);
            QuantumEvent.Subscribe<EventMarioPlayerPickedUpObject>(this, OnMarioPlayerPickedUpObject, FilterOutReplayFastForward);
            QuantumEvent.Subscribe<EventMarioPlayerThrewObject>(this, OnMarioPlayerThrewObject, FilterOutReplayFastForward);
            QuantumEvent.Subscribe<EventMarioPlayerMegaStart>(this, OnMarioPlayerMegaStart, FilterOutReplayFastForward);
            QuantumEvent.Subscribe<EventMarioPlayerMegaEnd>(this, OnMarioPlayerMegaEnd, FilterOutReplayFastForward);
            QuantumEvent.Subscribe<EventPlayKnockbackEffect>(this, OnPlayKnockbackEffect, FilterOutReplayFastForward);
            QuantumEvent.Subscribe<EventMarioPlayerEnteredPipe>(this, OnMarioPlayerEnteredPipe, FilterOutReplayFastForward);
            QuantumEvent.Subscribe<EventMarioPlayerStoppedSliding>(this, OnMarioPlayerStoppedSliding, FilterOutReplayFastForward);
            QuantumEvent.Subscribe<EventMarioPlayerUsedSpinner>(this, OnMarioPlayerUsedSpinner, FilterOutReplayFastForward);
            QuantumEvent.Subscribe<EventMarioPlayerDeathUp>(this, OnMarioPlayerDeathUp);
            QuantumEvent.Subscribe<EventPlayBumpSound>(this, OnPlayBumpSound, FilterOutReplayFastForward);
            QuantumEvent.Subscribe<EventMarioPlayerStompedByTeammate>(this, OnMarioPlayerStompedByTeammate, FilterOutReplayFastForward);
            QuantumEvent.Subscribe<EventPhysicsObjectLanded>(this, OnPhysicsObjectLanded, FilterOutReplayFastForward);
            QuantumEvent.Subscribe<EventMarioPlayerLandedWithAnimation>(this, OnMarioPlayerLandedWithAnimation, FilterOutReplayFastForward);
            QuantumEvent.Subscribe<EventEnemyKicked>(this, OnEnemyKicked, FilterOutReplayFastForward);
            QuantumEvent.Subscribe<EventMarioPlayerTaunted>(this, OnMarioPlayerTaunted, FilterOutReplayFastForward);
            QuantumEvent.Subscribe<EventMarioPlayerTauntCancelled>(this, OnMarioPlayerTauntCancelled, FilterOutReplayFastForward);
            QuantumEvent.Subscribe<EventMarioPlayerUpdatePowerupQueue>(this, OnMarioPlayerUpdatePowerupQueue, FilterOutReplayFastForward);
        }

        public override void OnActivate(Frame f) {
            var mario = f.Unsafe.GetPointer<MarioPlayer>(EntityRef);

            var playerData = QuantumUtils.GetPlayerData(f, mario->PlayerRef);
            if (playerData != null && f.TryFindAsset(playerData->Palette, out var palette)) {
                skin = palette.GetPaletteForCharacter(character);
            }
            GlowColor = Utils.GetPlayerColor(f, mario->PlayerRef);

            if (Game.PlayerIsLocal(mario->PlayerRef)) {
                MasterCanvas masterCanvas = FindFirstObjectByType<MasterCanvas>();
                PlayerElements elements = Instantiate(masterCanvas.playerElementsPrefab, masterCanvas.transform);
                elements.Initialize(Game, f, EntityRef, mario->PlayerRef);
            }

            AllMarioPlayers.RemoveWhere(ma => ma == null);
            AllMarioPlayers.Add(this);
            MarioPlayerInitialized?.Invoke(Game, f, this);

            forceUpdate = true;
            OnUpdateView();
        }

        public override void OnDeactivate() {
            MarioPlayerDestroyed?.Invoke(Game, VerifiedFrame, this);
        }

        public void OnDestroy() {
            RenderPipelineManager.beginCameraRendering -= URPOnPreRender;

            foreach ((_, var material) in clonedMaterials) {
                Destroy(material);
            }
        }

        public void LateUpdate() {
            largeShellExclude.SetActive(!animator.GetCurrentAnimatorStateInfo(0).IsName("in-shell"));
        }

        public override void OnUpdateView() {
            using var profilerScope = HostProfiler.Start("MarioPlayerAnimator.OnUpdateView");
            Frame f = PredictedFrame;
            if (!f.Exists(EntityRef)) {
                return;
            }

            var mario = f.Unsafe.GetPointer<MarioPlayer>(EntityRef);

            if (VerifiedFrame.Global->GameState >= GameState.Ended && !forceUpdate) {
                animator.speed = 0;
                modelRoot.SetActive(!mario->IsRespawning && !(mario->IsDead && IsBelowDeathplane));
                SetParticleEmission(drillParticle, false);
                SetParticleEmission(sparkles, false);
                SetParticleEmission(iceSkiddingParticle, false);
                SetParticleEmission(waterSkiddingParticle, false);
                SetParticleEmission(dust, false);
                SetParticleEmission(giantParticle, false);
                SetParticleEmission(fireParticle, false);
                SetParticleEmission(bubblesParticle, false);
                return;
            }
            animator.speed = 1;

            var freezable = f.Unsafe.GetPointer<Freezable>(EntityRef);
            var physicsObject = f.Unsafe.GetPointer<PhysicsObject>(EntityRef);

            UpdatePowerupVisuals(f, mario);

            HandleMiscStates(f, mario, physicsObject, freezable);
            HandleAnimations(f, mario, physicsObject, freezable);

            Input inputs = default;
            if (mario->PlayerRef.IsValid) {
                Input* inputPointer = f.GetPlayerInput(mario->PlayerRef);
                if (inputPointer != null) {
                    inputs = *inputPointer;
                }
            }
            
            SetFacingDirection(f, mario, physicsObject);
            InterpolateFacingDirection();
            UpdateAnimatorVariables(f, mario, physicsObject, freezable, ref inputs);
            
            previousPosition = transform.position;
            forceUpdate = false;
        }

        public void HandleAnimations(Frame f, MarioPlayer* mario, PhysicsObject* physicsObject, Freezable* freezable) {
            using var profilerScope = HostProfiler.Start("MarioPlayerAnimator.HandleAnimations");
            // Particles
            bool disableParticles = mario->IsDead || freezable->IsFrozen(f) || f.Global->GameState == GameState.Ended;
            bool walkingOnWater = mario->IsWalkingOnWater(f, EntityRef);
            
            SetParticleEmission(drillParticle, !disableParticles && mario->IsDrilling);
            SetParticleEmission(sparkles, !disableParticles && mario->IsStarmanInvincible);
            SetParticleEmission(iceSkiddingParticle, !disableParticles && physicsObject->IsOnSlipperyGround && ((mario->IsSkidding && physicsObject->Velocity.SqrMagnitude.AsFloat > 0.25f) || mario->FastTurnaroundFrames > 0));
            SetParticleEmission(waterSkiddingParticle, !disableParticles && walkingOnWater && ((mario->IsSkidding && physicsObject->Velocity.SqrMagnitude.AsFloat > 0.25f) || mario->FastTurnaroundFrames > 0));
            SetParticleEmission(waterRunningParticle, !disableParticles && !waterSkiddingParticle.isPlaying && walkingOnWater && FPMath.Abs(physicsObject->Velocity.X) > FP._0_10);
            SetParticleEmission(dust, !disableParticles && !iceSkiddingParticle.isPlaying && !waterSkiddingParticle.isPlaying && (mario->IsWallsliding || (physicsObject->IsTouchingGround && ((mario->IsSkidding || (mario->IsCrouching && !physicsObject->IsOnSlipperyGround)) && Mathf.Abs(physicsObject->Velocity.X.AsFloat) > 0.25f)) || mario->FastTurnaroundFrames > 0 || (((mario->IsSliding && Mathf.Abs(physicsObject->Velocity.X.AsFloat) > 0.25f) || mario->IsInShell) && physicsObject->IsTouchingGround)) && !f.Exists(mario->CurrentPipe));
            SetParticleEmission(giantParticle, !disableParticles && mario->CurrentPowerupState == PowerupState.MegaMushroom && mario->MegaMushroomStartFrames == 0);
            SetParticleEmission(fireParticle, mario->IsDead && !mario->IsRespawning && mario->FireDeath && !physicsObject->IsFrozen);
            SetParticleEmission(bubblesParticle, !disableParticles && physicsObject->IsUnderwater);

            var physicsCollider = f.Unsafe.GetPointer<PhysicsCollider2D>(EntityRef);
            if (mario->IsCrouching || mario->IsSliding || mario->IsSkidding || mario->IsInShell) {
                dust.transform.localPosition = Vector2.zero;
            } else if (mario->IsWallsliding) {
                dust.transform.localPosition = physicsCollider->Shape.Box.Extents.ToUnityVector2() * 1.5f * (mario->WallslideLeft ? new Vector2(-1, 1) : Vector2.one);
            }
            Vector3 flip = mario->FacingRight ? Vector3.one : new Vector3(-1, 1, 1);
            iceSkiddingParticle.transform.localScale = flip;
            waterSkiddingParticle.transform.localScale = flip;
            waterRunningParticle.transform.localScale = flip;

            dustPlayer.SetSoundData((mario->IsInShell || mario->IsSliding || mario->IsCrouchedInShell) ? shellSlideData : wallSlideData);
            drillPlayer.SetSoundData(mario->IsPropellerFlying ? propellerDrillData : spinnerDrillData);
            bubblesParticle.transform.localPosition = new(bubblesParticle.transform.localPosition.x, physicsCollider->Shape.Box.Extents.Y.AsFloat * 2);

            if (!mario->IsDead) {
                var waterColliders = f.ResolveHashSet(physicsObject->LiquidContacts);
                float marioTop = transform.position.y + physicsCollider->Shape.Centroid.Y.AsFloat + physicsCollider->Shape.Box.Extents.Y.AsFloat;
                foreach (EntityRef water in waterColliders) {
                    var liquidCollider = f.Unsafe.GetPointer<PhysicsCollider2D>(water);
                    var liquidTransform = f.Unsafe.GetPointer<Transform2D>(water);
                    var liquid = f.Unsafe.GetPointer<Liquid>(water);

                    liquidCollider->Shape.Compound.GetShapes(f, out Shape2D* shapes, out _);
                    float waterTop = liquidTransform->Position.Y.AsFloat + liquidCollider->Shape.Centroid.Y.AsFloat + shapes[0].Box.Extents.Y.AsFloat;
                    if (marioTop >= waterTop - 0.125f) {
                        FPVector2 current = transform.position.ToFPVector2();
                        current.Y = 0;
                        FPVector2 prev = previousPosition.ToFPVector2();
                        prev.Y = 0;
                        waterSurfaceMovementDistance += QuantumUtils.WrappedDistance(f, prev, current).AsFloat;
                        if (waterSurfaceMovementDistance > 0.3f) {
                            SingleParticleManager.Instance.Play(ParticleEffect.Water_Splash_Surface, new Vector3(transform.position.x, waterTop));
                            waterSurfaceMovementDistance %= 0.3f;
                        }
                    }
                }
            }

            animator.SetLayerWeight(3, physicsObject->IsUnderwater ? 1 : 0);
        }

        private IEnumerator BlinkRoutine() {
            while (true) {
                yield return new WaitForSeconds(3f + (UnityEngine.Random.value * 6f));
                OnStartBlink?.Invoke(EntityRef);
                eyeState = Enums.PlayerEyeState.HalfBlink;
                yield return BlinkDelay;
                eyeState = Enums.PlayerEyeState.FullBlink;
                yield return BlinkDelay;
                eyeState = Enums.PlayerEyeState.HalfBlink;
                yield return BlinkDelay;
                eyeState = Enums.PlayerEyeState.Normal;
            }
        }

        private void SetFacingDirection(Frame f, MarioPlayer* mario, PhysicsObject* physicsObject) {
            using var profilerScope = HostProfiler.Start("MarioPlayerAnimator.SetFacingDirection");
            float delta = Time.deltaTime;

            float angle = mario->CurrentPowerupState switch {
                PowerupState.BlueShell => 90f,
                PowerupState.MegaMushroom => 78.75f,
                _ => 67.5f,
            };
            float angleR = 180 - angle;
            float angleL = 180 + angle;

            modelRotateInstantly = false;
            var freezable = f.Unsafe.GetPointer<Freezable>(EntityRef);

            if (mario->TauntFrames > 0) {
                modelRotationTarget = Quaternion.Euler(0, 180, 0);
            } else if (f.Exists(mario->CurrentPipe)) {
                modelRotationTarget = Quaternion.Euler(0, mario->FacingRight ? angleR : angleL, 0);
                modelRotateInstantly = true;
            } else if (mario->IsInKnockback || freezable->IsFrozen(f)) {
                bool right = mario->FacingRight;
                if (mario->IsInKnockback && (physicsObject->IsUnderwater || mario->IsInWeakKnockback)) {
                    right = mario->KnockbackWasOriginallyFacingRight;
                }
                modelRotationTarget = Quaternion.Euler(0, right ? angleR : angleL, 0);
                modelRotateInstantly = true;

            } else if (mario->IsDead) {
                if (mario->FireDeath && mario->DeathAnimationFrames == 0) {
                    modelRotationTarget = Quaternion.Euler(0, mario->FacingRight ? angleR : angleL, 0);
                } else {
                    modelRotationTarget = Quaternion.Euler(0, 180, 0);
                }
                modelRotateInstantly = true;

            } else if (animator.GetBool(ParamInShell) && (!f.Exists(mario->CurrentSpinner) || Mathf.Abs(physicsObject->Velocity.X.AsFloat) > 0.3f)) {
                var physics = f.FindAsset(mario->PhysicsAsset);
                float percentage = Mathf.Abs(physicsObject->Velocity.X.AsFloat) / physics.WalkMaxVelocity[physics.RunSpeedStage].AsFloat * delta;
                modelRotationTarget *= Quaternion.Euler(0, percentage * 2010.9f * (mario->FacingRight ? -1 : 1), 0);
                modelRotateInstantly = true;

            } else if (wasTurnaround || mario->IsSkidding || mario->IsTurnaround || animator.GetCurrentAnimatorStateInfo(0).IsName("turnaround")) {
                bool flip = mario->FacingRight ^ (animator.GetCurrentAnimatorStateInfo(0).IsName("turnaround") || mario->IsSkidding);
                modelRotationTarget = Quaternion.Euler(0, flip ? angleL : angleR, 0);
                modelRotateInstantly = true;

            } else if (f.Unsafe.TryGetPointer(mario->CurrentSpinner, out Spinner* spinner)
                       && physicsObject->IsTouchingGround && mario->ProjectileDelayFrames == 0
                       && Mathf.Abs(physicsObject->Velocity.X.AsFloat) < 0.3f && !f.Exists(mario->HeldEntity)
                       && !animator.GetCurrentAnimatorStateInfo(0).IsName("fireball")) {

                modelRotationTarget *= Quaternion.Euler(0, spinner->AngularVelocity.AsFloat * delta, 0);
                modelRotateInstantly = true;

            } else if (mario->IsSpinnerFlying || mario->IsPropellerFlying) {
                modelRotationTarget *= Quaternion.Euler(0, (-1200 - ((mario->PropellerLaunchFrames / 60f) * 1400) - (mario->IsDrilling ? 900 : 0) + (mario->IsPropellerFlying && mario->PropellerSpinFrames == 0 && physicsObject->Velocity.Y < 0 ? 700 : 0)) * delta, 0);
                modelRotateInstantly = true;

            } else if (mario->IsWallsliding) {
                modelRotationTarget = Quaternion.Euler(0, mario->WallslideRight ? angleR : angleL, 0);
            } else {
                modelRotationTarget = Quaternion.Euler(0, mario->FacingRight ? angleR : angleL, 0);
            }

            wasTurnaround = mario->IsTurnaround;
        }

        private void InterpolateFacingDirection() {
            using var profilerScope = HostProfiler.Start("MarioPlayerAnimator.InterpolateFacingDirection");
            if (modelRotateInstantly || wasTurnaround) {
                modelRoot.transform.rotation = modelRotationTarget;
            } else /* if (!GameManager.Instance.GameEnded) */ {
                float maxRotation = 2000f * Time.deltaTime;
                modelRoot.transform.rotation = Quaternion.RotateTowards(modelRoot.transform.rotation, modelRotationTarget, maxRotation);
            }
        }

        private void SetParticleEmission(ParticleSystem particle, bool value) {
            if (value) {
                if (particle.isStopped) {
                    particle.Play();
                }
            } else {
                if (particle.isPlaying) {
                    particle.Stop();
                }
            }
        }

        public PowerupState DisplayPowerupState(MarioPlayer* mario, Frame f) {
            // check if Mario is in a powerUP transition
            if (mario->TryGetCurrentPowerTransition(f, out var currAnim)) {
                // now check its timer
                bool displaySecond = currAnim->Timer / Constants.PowerupTransitionOscillation % 2 == 1;
                if (displaySecond) {
                    return currAnim->EndingState;
                } else {
                    return currAnim->StartingState;
                }
            }

            return mario->CurrentPowerupState;
        }

        public void HandleSizeMismatch(ref Vector3 modelScale, PowerupTransitionAnimation* currAnim) {
            var startingVisuals = FindPowerupVisuals(currAnim->StartingState);
            var endingVisuals = FindPowerupVisuals(currAnim->EndingState);

            Vector3 sizeDiff = startingVisuals.ModelScale - endingVisuals.ModelScale;
            sizeDiff.y += startingVisuals.ModelHeightInBlocks - endingVisuals.ModelHeightInBlocks;

            //float transitionTimerNorm = (float) currAnim->Timer / Constants.PowerupAnimLength;

            // for choppyness
            int currStage = currAnim->Timer / Constants.PowerupTransitionOscillation;
            float[] sizes = {0f, 0.25f, 0.15f, 0.5f, 0.4f, 0.85f, 0.75f};

            modelScale = Vector3.Lerp(modelScale, modelScale + sizeDiff, sizes[currStage]);
        }

        public void UpdateAnimatorVariables(Frame f, MarioPlayer* mario, PhysicsObject* physicsObject, Freezable* freezable, ref Input inputs) {
            using var profilerScope = HostProfiler.Start("MarioPlayerAnimator.UpdateAnimatorVariables");

            bool right = inputs.Right.IsDown;
            bool left = inputs.Left.IsDown;

            f.Unsafe.TryGetPointer(mario->HeldEntity, out Holdable* heldObject);

            animator.SetBool(ParamDead, mario->IsDead);
            animator.SetBool(ParamOnLeft, mario->WallslideLeft);
            animator.SetBool(ParamOnRight, mario->WallslideRight);
            animator.SetBool(ParamOnGround, physicsObject->IsTouchingGround || mario->IsStuckInBlock || mario->CoyoteTimeFrames > 0);
            animator.SetBool(ParamInvincible, mario->IsStarmanInvincible);
            animator.SetBool(ParamSkidding, mario->IsSkidding);
            animator.SetBool(ParamPropeller, mario->IsPropellerFlying);
            animator.SetBool(ParamPropellerSpin, mario->IsPropellerFlying && mario->PropellerSpinFrames > 0);
            animator.SetBool(ParamPropellerStart, mario->IsPropellerFlying && mario->PropellerLaunchFrames > 0);
            animator.SetBool(ParamCrouching, mario->IsCrouching);
            animator.SetBool(ParamGroundpound, mario->IsGroundpounding);
            animator.SetBool(ParamSliding, mario->IsSliding);
            animator.SetBool(ParamKnockback, mario->IsInKnockback && mario->KnockbackGetupFrames == 0);
            animator.SetBool(ParamFacingRight, (left ^ right) ? right : mario->FacingRight);
            animator.SetBool(ParamFlying, mario->IsSpinnerFlying);
            animator.SetBool(ParamDrill, mario->IsDrilling);
            animator.SetBool(ParamDoubleJump, mario->JumpState == JumpState.DoubleJump);
            animator.SetBool(ParamTripleJump, mario->JumpState == JumpState.TripleJump);
            animator.SetBool(ParamHolding, f.Exists(mario->HeldEntity));
            animator.SetBool(ParamHeadCarry, heldObject != null && heldObject->HoldAboveHead);
            animator.SetBool(ParamCarryStart, heldObject != null && heldObject->HoldAboveHead && (f.Number - mario->HoldStartFrame) < 27);
            animator.SetBool(ParamPipe, f.Exists(mario->CurrentPipe));
            animator.SetBool(ParamBlueShell, DisplayPowerupState(mario, f) == PowerupState.BlueShell);
            animator.SetBool(ParamMini, mario->CurrentPowerupState == PowerupState.MiniMushroom);
            animator.SetBool(ParamMega, mario->CurrentPowerupState == PowerupState.MegaMushroom);
            animator.SetBool(ParamInShell, mario->IsInShell || (mario->CurrentPowerupState == PowerupState.BlueShell && (mario->IsCrouching || mario->IsGroundpounding || mario->IsSliding) && mario->GroundpoundStartFrames <= 9));
            animator.SetBool(ParamTurnaround, mario->IsTurnaround);
            animator.SetBool(ParamSwimming, physicsObject->IsUnderwater && !mario->IsGroundpounding && !mario->IsDrilling && !freezable->IsFrozen(f));
            animator.SetBool(ParamAHeld, inputs.Jump.IsDown);
            animator.SetBool(ParamFireballKnockback, mario->IsInWeakKnockback);
            animator.SetBool(ParamFireDeath, mario->FireDeath);
            animator.SetBool(ParamPushing, mario->LastPushingFrame + 5 >= f.Number);
            animator.SetBool(ParamFrozen, freezable->IsFrozen(f));
            animator.SetBool(ParamKnockforwards, mario->KnockForwards);
            animator.SetBool(ParamTaunt, mario->TauntFrames > 0);
                
            float animatedVelocity = Mathf.Abs(physicsObject->Velocity.X.AsFloat);
            if (mario->IsStuckInBlock) {
                animatedVelocity = 0;
            } else if (mario->IsPropellerFlying) {
                animatedVelocity = 2f;
            } else if (mario->CurrentPowerupState == PowerupState.MegaMushroom && (left || right)) {
                animatedVelocity = 4.5f;
            } else if (left ^ right && !physicsObject->IsTouchingRightWall && !physicsObject->IsTouchingLeftWall && mario->GroundpoundStandFrames == 0) {
                animatedVelocity = Mathf.Max(physicsObject->IsOnSlipperyGround ? 2.7f : 2f, animatedVelocity);
            } else if (physicsObject->IsOnSlipperyGround) {
                animatedVelocity = 0;
            }
            if (animatedVelocity < 0.03f) {
                animatedVelocity = 0;
            }

            animator.SetFloat(ParamVelocityX, animatedVelocity);
            animator.SetFloat(ParamVelocityY, physicsObject->Velocity.Y.AsFloat);
            animator.SetFloat(ParamVelocityMagnitude, physicsObject->Velocity.Magnitude.AsFloat);
        }

        private void HandleMiscStates(Frame f, MarioPlayer* mario, PhysicsObject* physicsObject, Freezable* freezable) {
            using var profilerScope = HostProfiler.Start("MarioPlayerAnimator.HandleMiscStates");

            // Shader effects
            materialBlock ??= new();
            materialBlock.SetFloat(ParamEyeState, (int) (mario->IsDead || mario->IsInKnockback ? Enums.PlayerEyeState.Death : eyeState));
            materialBlock.SetFloat(ParamModelScale, modelRoot.transform.lossyScale.x * (mario->CurrentPowerupState >= PowerupState.Mushroom ? 1f : 0.5f));
            materialBlock.SetColor(ParamOverallsColor, skin?.OverallsColor.AsColor ?? Color.clear);
            materialBlock.SetColor(ParamShirtColor, skin?.ShirtColor.AsColor ?? Color.clear);
            materialBlock.SetFloat(ParamCapUsesOverallsColor, (skin?.HatUsesOverallsColor ?? false) ? 1 : 0);

            Vector3 giantMultiply = Vector3.one;
            float giantTimeRemaining = mario->MegaMushroomFrames / 60f;
            if (giantTimeRemaining > 0 && giantTimeRemaining < 4) {
                float v = ((Mathf.Sin(giantTimeRemaining * 20f) + 1f) * 0.45f) + 0.1f;
                giantMultiply = new Vector3(v, 1, v);
            }

            materialBlock.SetVector(ParamMultiplyColor, giantMultiply);

            foreach (Renderer r in renderers) {
                r.SetPropertyBlock(materialBlock);
            }

            var newShader = mario->IsStarmanInvincible ? rainbowShader : normalShader;
            foreach ((_, var material) in clonedMaterials) {
                if (material.shader != newShader) {
                    material.shader = newShader;
                }
            }

            // Hit flash
            float remainingDamageInvincibility = mario->DamageInvincibilityFrames / 60f;

            bool modelShouldBeVisible = mario->KnockbackGetupFrames > 0
                || mario->MegaMushroomStartFrames > 0;
            bool modelShouldBeInvisible = f.Global->GameState < GameState.Playing
                || mario->IsRespawning
                || (mario->IsDead && IsBelowDeathplane)
                || (remainingDamageInvincibility > 0 && (f.Number * f.DeltaTime.AsFloat) * (remainingDamageInvincibility <= 0.75f ? 5 : 2) % 0.2f < 0.1f);

            bool modelFinalVisibleState = modelShouldBeVisible || !modelShouldBeInvisible;
            if (modelFinalVisibleState != modelRoot.activeSelf) {
                modelRoot.SetActive(modelFinalVisibleState);
            } 
            
            // Z-positioning
            float newZ = -4;
            if (mario->IsDead) {
                if (physicsObject->IsUnderwater) {
                    newZ = -2;
                } else {
                    newZ = -6;
                }
            } else if (freezable->IsFrozen(f)) {
                newZ = -2;
            } else if (f.Exists(mario->CurrentPipe)) {
                newZ = 1;
            }

            transform.position = new(transform.position.x, transform.position.y, newZ);
        }

        private void UpdatePowerupVisuals(Frame f, MarioPlayer* mario) {
            PowerupVisuals currentPowerupVisuals;
            PowerupVisuals displayPowerupVisuals = FindPowerupVisuals(DisplayPowerupState(mario, f));

            // in transition, apply visuals based on the current transition we're doing!
            bool sizeMismatch = false;
            if (mario->TryGetCurrentPowerTransition(f, out var currAnim)) {
                currentPowerupVisuals = FindPowerupVisuals(currAnim->EndingState);

                var startingVisuals = FindPowerupVisuals(currAnim->StartingState);
                var endingVisuals = FindPowerupVisuals(currAnim->EndingState);

                sizeMismatch = startingVisuals.ModelScale != endingVisuals.ModelScale || startingVisuals.ModelHeightInBlocks != endingVisuals.ModelHeightInBlocks;
            } else {
                currentPowerupVisuals = FindPowerupVisuals(mario->CurrentPowerupState);
            }

            Vector3 modelScale = currentPowerupVisuals.ModelScale;

            // handle a size mismatch
            if (sizeMismatch) {
                HandleSizeMismatch(ref modelScale, currAnim);
            }

            bool starman = mario->IsStarmanInvincible;
            if (previousPowerupVisuals != currentPowerupVisuals || previousPowerupVisuals != displayPowerupVisuals || previousStarmanEnabled != starman) {
                foreach (var powerupVisual in powerupVisuals) {
                    powerupVisual.DisableProps();
                    powerupVisual.DisableModel();
                }

                fallbackPowerupVisuals.ApplyTextureReplacements(starman);

                // swap the model and animations for the next powerUP
                currentPowerupVisuals?.EnableModel();
                currentPowerupVisuals?.SwapAnimations(this);

                // meanwhile enable the props for the displaying powerUP
                displayPowerupVisuals?.EnableProps();
                displayPowerupVisuals?.ApplyTextureReplacements(starman);

                previousPowerupVisuals = displayPowerupVisuals;
                previousStarmanEnabled = starman;
            }

            // Scale
            Vector3 targetScale;
            if (mario->MegaMushroomEndFrames > 0) {
                // Interpoalte from mega scale to normal scale.
                var megaVisuals = FindPowerupVisuals(PowerupState.MegaMushroom);
                float timer = mario->MegaMushroomEndFrames / 90f;
                if (!mario->MegaMushroomStationaryEnd) {
                    timer *= 2;
                }
                targetScale = Vector3.Lerp(megaVisuals.ModelScale, modelScale, 1f - timer);
            } else if (mario->MegaMushroomStartFrames > 0) {
                // Interpolate from normal scale to mega scale.
                var normalVisuals = FindPowerupVisuals(PowerupState.Mushroom);
                float timer = mario->MegaMushroomStartFrames / 90f;
                targetScale = Vector3.Lerp(normalVisuals.ModelScale, modelScale, 1f - timer);
            } else {
                // Just apply the scale
                targetScale = modelScale;
            }
            if (teammateStompTimer > 0) {
                targetScale.y -= Mathf.Sin(teammateStompTimer * Mathf.PI / 0.15f) * 0.2f;
                teammateStompTimer -= Time.deltaTime;
            }
            modelRoot.transform.SetLossyScale(targetScale);
        }

        private PowerupVisuals FindPowerupVisuals(PowerupState state) {
            foreach (var visual in powerupVisuals) {
                if (visual.State == state) {
                    return visual;
                }
            }
            return fallbackPowerupVisuals;
        }

        private unsafe void URPOnPreRender(ScriptableRenderContext context, Camera camera) {
            if (materialBlock == null) {
                return;
            }
            bool teams = PredictedFrame.Global->Rules.TeamsEnabled;
            materialBlock.SetColor(ParamGlowColor, teams || !IsCameraFocus(camera) ? GlowColor : Color.clear);
        }

        private bool IsCameraFocus(Camera camera) {
            foreach (var playerElement in PlayerElements.AllPlayerElements) {
                if (EntityRef == playerElement.Entity && playerElement.IsOurCamera(camera)) {
                    return true;
                }
            }
            return false;
        }

        public void PlaySound(SoundEffect soundEffect, IList<ISoundOverrideProvider> extraProviders = null, int? variant = null, float volume = 1) {
            List<ISoundOverrideProvider> providers = new() { character };
            if (extraProviders != null) {
                providers.AddRange(extraProviders);
            }
            sfx.PlayOneShot(soundEffect, providers, variant, volume);
        }

        public GameObject SpawnParticle(GameObject particle, Vector3 worldPos, Quaternion? rot = null) {
            return Instantiate(particle, worldPos, rot ?? Quaternion.identity);
        }

        /// <summary>
        /// Used by animations as an event
        /// </summary>
        [Preserve]
        public void Footstep() {
            Frame f = PredictedFrame;
            if (IsReplayFastForwarding || !f.Exists(EntityRef)) {
                return;
            }

            var mario = f.Unsafe.GetPointer<MarioPlayer>(EntityRef);
            var marioTransform = f.Unsafe.GetPointer<Transform2D>(EntityRef);
            var physicsObject = f.Unsafe.GetPointer<PhysicsObject>(EntityRef);
            var physics = f.FindAsset(mario->PhysicsAsset);

            Input input;
            try {
                input = *f.GetPlayerInput(mario->PlayerRef);
            } catch {
                input = default;
            }

            if (physicsObject->IsUnderwater || mario->CurrentPowerupState == PowerupState.MegaMushroom) {
                return;
            }

            bool left = input.Left.IsDown;
            bool right = input.Right.IsDown;

            bool reverse = physicsObject->Velocity.X != 0 && ((left ? 1 : -1) == FPMath.Sign(physicsObject->Velocity.X));
            if (physicsObject->IsOnSlipperyGround && (left ^ right) && reverse) {
                PlaySound(SoundEffect.World_Ice_Skidding);
                return;
            }
            if (mario->IsPropellerFlying) {
                PlaySound(SoundEffect.Powerup_PropellerMushroom_Kick);
                return;
            }
            if (FPMath.Abs(physicsObject->Velocity.X) < physics.WalkMaxVelocity[physics.WalkSpeedStage]) {
                return;
            }

            SoundEffect footstepSoundEffect = SoundEffect.Player_Walk_Grass;
            ParticleEffect footstepParticleEffect = ParticleEffect.None;

            if (f.ResolveHashSet(physicsObject->LiquidContacts).Count > 0) {
                footstepSoundEffect = SoundEffect.Player_Walk_Water;
            } else {
                foreach (var contact in f.ResolveList(physicsObject->Contacts)) {
                    if (FPVector2.Dot(contact.Normal, FPVector2.Up) < Constants.PhysicsGroundMaxAngleCos) {
                        continue;
                    }

                    if (f.Exists(contact.Entity)) {
                        if (f.Has<Liquid>(contact.Entity) && mario->CurrentPowerupState == PowerupState.MiniMushroom && physicsObject->IsWaterSolid) {
                            footstepSoundEffect = SoundEffect.Player_Walk_Water;
                        }
                    } else {
                        StageTileInstance tileInstance = ViewContext.Stage.GetTileRelative(f, contact.Tile);
                        if (f.TryFindAsset(tileInstance.Tile, out StageTile tile)) {
                            if (tile.FootstepSound != SoundEffect.Player_Walk_Grass) {
                                footstepSoundEffect = tile.FootstepSound;
                            }
                            if (tile.FootstepParticle != ParticleEffect.None) {
                                footstepParticleEffect = tile.FootstepParticle;
                            }
                        }
                    }
                }
            }

            PlaySound(footstepSoundEffect,
                variant: footstepCounter++,
                volume: (FPMath.Abs(physicsObject->Velocity.X) / (physics.WalkMaxVelocity[physics.RunSpeedStage] + 4)).AsFloat
            );
            SingleParticleManager.Instance.Play(footstepParticleEffect, marioTransform->Position.ToUnityVector3());
        }

        /// <summary>
        /// Used by animations as an event
        /// </summary>
        [Preserve]
        public void PlayMegaFootstep() {
            if (IsReplayFastForwarding) {
                return;
            }

            Frame f = PredictedFrame;
            var physicsObject = f.Unsafe.GetPointer<PhysicsObject>(EntityRef);
            if (physicsObject->IsUnderwater) {
                return;
            }
            var mario = f.Unsafe.GetPointer<MarioPlayer>(EntityRef);
            var marioTransform = f.Unsafe.GetPointer<Transform2D>(EntityRef);

            // CameraController.ScreenShake = 0.15f;
            SpawnParticle(Enums.PrefabParticle.Player_MegaFootstep.GetGameObject(), marioTransform->Position.ToUnityVector2() + new Vector2(mario->FacingRight ? 0.5f : -0.5f, 0));
            PlaySound(SoundEffect.Powerup_MegaMushroom_Walk, variant: footstepCounter++);
            GlobalController.Instance.rumbleManager.RumbleForSeconds(0.5f, 0f, 0.1f, RumbleManager.RumbleSetting.High);
            CameraAnimator.TriggerScreenshake(0.15f);
        }

        private void OnPlayKnockbackEffect(EventPlayKnockbackEffect e) {
            if (e.Entity != EntityRef) {
                return;
            }

            if (e.AttackerPosition.HasValue) {
                SpawnParticle(Enums.PrefabParticle.Player_PlayerBump.GetGameObject(), e.AttackerPosition.Value.ToUnityVector3());
            }

            KnockbackStrength strength = e.Strength;
            if (e.PlayKnockbackSound) {
                PlaySound((strength is KnockbackStrength.FireballBump or KnockbackStrength.CollisionBump) ? SoundEffect.Player_Sound_Collision_Fireball : SoundEffect.Player_Sound_Collision);
            }

            if (IsMarioLocal(e.Entity)) {
                float rumbleStrength = strength switch {
                    KnockbackStrength.Groundpound => 0.9f,
                    KnockbackStrength.Normal => 0.5f,
                    KnockbackStrength.FireballBump => 0.25f,
                    KnockbackStrength.CollisionBump => 0.4f,
                    _ => 0,
                };

                GlobalController.Instance.rumbleManager.RumbleForSeconds(0.3f, 0.6f, rumbleStrength, RumbleManager.RumbleSetting.Low);
            }
        }

        private void OnMarioPlayerMegaEnd(EventMarioPlayerMegaEnd e) {
            if (e.Entity != EntityRef) {
                return;
            }

            if (e.Cancelled) {
                animator.Play(StateMegaCancel, 0, 1f - (e.EndingFrames / 90f));
            } else {
                PlaySound(SoundEffect.Powerup_MegaMushroom_End);
            }
        }

        private void OnMarioPlayerMegaStart(EventMarioPlayerMegaStart e) {
            if (e.Entity != EntityRef) {
                return;
            }

            PlaySound(SoundEffect.Player_Voice_MegaMushroom);
        }

        private void OnMarioPlayerThrewObject(EventMarioPlayerThrewObject e) {
            if (e.Entity != EntityRef) {
                return;
            }

            bool big = false;
            Frame f = PredictedFrame;
            if (f.Unsafe.TryGetPointer(e.OtherEntity, out Holdable* holdable)) {
                big = holdable->HoldAboveHead;
            }
            PlaySound(big ? SoundEffect.Player_Voice_Throw_Large : SoundEffect.Player_Voice_Throw_Small);
            animator.SetTrigger(ParamThrow);
        }

        private void OnMarioPlayerPickedUpObject(EventMarioPlayerPickedUpObject e) {
            if (e.Entity != EntityRef) {
                return;
            }

            animator.ResetTrigger(ParamFireball);
            animator.ResetTrigger(ParamThrow);
            
            if (e.HoldAboveHead) {
                animator.Play(ParamHeadPickup);
            }
            PlaySound(e.HoldAboveHead ? SoundEffect.Player_Voice_Pickup_Large : SoundEffect.Player_Voice_Pickup_Small);
        }

        private void OnMarioPlayerStompedByTeammate(EventMarioPlayerStompedByTeammate e) {
            if (e.Entity != EntityRef) {
                return;
            }

            if (teammateStompTimer == 0) {
                teammateStompTimer = 0.15f;
            }
        }

        private void OnPhysicsObjectLanded(EventPhysicsObjectLanded e) {
            if (e.Entity != EntityRef) {
                return;
            }

            var mario = PredictedFrame.Unsafe.GetPointer<MarioPlayer>(e.Entity);
            if (mario->CurrentPowerupState == PowerupState.MegaMushroom) {
                PlayMegaFootstep();
            }

            var physicsObject = PredictedFrame.Unsafe.GetPointer<PhysicsObject>(e.Entity);
            if (physicsObject->IsUnderwater && physicsObject->PreviousFrameVelocity.Y < -1) {
                SpawnParticle(Enums.PrefabParticle.Player_WaterDust.GetGameObject(), transform.position + Vector3.back * 5);
            }
        }

        private void OnPlayBumpSound(EventPlayBumpSound e) {
            if (e.Entity != EntityRef) {
                return;
            }

            var mario = PredictedFrame.Unsafe.GetPointer<MarioPlayer>(e.Entity);
            if (!mario->IsInShell && (Time.time - lastBumpSound < 0.25f)) {
                return;
            }

            PlaySound(SoundEffect.World_Block_Bump);
            lastBumpSound = Time.time;
        }

        private void OnMarioPlayerRespawned(EventMarioPlayerRespawned e) {
            if (e.Entity != EntityRef) {
                return;
            }

            SpawnParticle(Enums.PrefabParticle.Enemy_Puff.GetGameObject(), transform.position + Vector3.back * 3f);
        }

        private void OnMarioPlayerPreRespawned(EventMarioPlayerPreRespawned e) {
            if (e.Entity != EntityRef) {
                return;
            }

            activeRespawnParticle = SpawnParticle(respawnParticle, e.Spawnpoint.ToUnityVector3() + (Vector3.up * 0.25f));
            foreach (ParticleSystem particle in activeRespawnParticle.GetComponentsInChildren<ParticleSystem>()) {
                var main = particle.main;    
                main.startColor = GlowColor;
            }

            PlaySound(SoundEffect.Player_Sound_Respawn);
        }

        private void OnMarioPlayerDied(EventMarioPlayerDied e) {
            if (e.Entity != EntityRef) {
                return;
            }

            animator.Play("deadstart");
            animator.Play("deadstart", 3);

            if (!IsReplayFastForwarding) {
                if (e.IsLava) {
                    PlaySound(SoundEffect.Player_Sound_LavaHiss);
                }
                PlaySound(IsMarioLocal(e.Entity) ? SoundEffect.Player_Sound_Death : SoundEffect.Player_Sound_DeathOthers);
            }
        }

        private void OnMarioPlayerDeathUp(EventMarioPlayerDeathUp e) {
            if (e.Entity != EntityRef) {
                return;
            }

            animator.SetTrigger("deathup");

            if (e.FireDeath && !IsReplayFastForwarding) {
                PlaySound(SoundEffect.Player_Voice_LavaDeath);
            }
        }

        private void OnMarioPlayerCollectedStar(EventMarioPlayerCollectedStar e) {
            if (e.Entity != EntityRef) {
                return;
            }

            PlaySound(IsMarioLocal(e.Entity) ? SoundEffect.World_Star_Collect : SoundEffect.World_Star_CollectOthers);
            Instantiate(starCollectParticle, e.Position.ToUnityVector3(), Quaternion.identity);
        }

        private void OnMarioPlayerPropellerSpin(EventMarioPlayerPropellerSpin e) {
            if (e.Entity != EntityRef) {
                return;
            }

            PlaySound(SoundEffect.Powerup_PropellerMushroom_Spin);
        }

        private void OnMarioPlayerUsedPropeller(EventMarioPlayerUsedPropeller e) {
            if (e.Entity != EntityRef) {
                return;
            }

            PlaySound(SoundEffect.Powerup_PropellerMushroom_Start);
        }

        private void OnMarioPlayerShotProjectile(EventMarioPlayerShotProjectile e) {
            if (e.Entity != EntityRef) {
                return;
            }

            animator.SetTrigger("fireball");
            var projectile = PredictedFrame.FindAsset(e.Projectile.Asset);
            PlaySound(projectile.ShootSound, new[] { projectile });
        }

        private void OnMarioPlayerWalljumped(EventMarioPlayerWalljumped e) {
            if (e.Entity != EntityRef) {
                return;
            }
            
            if (!IsReplayFastForwarding) {
                Vector3 particleOffset = e.HitboxExtents.ToUnityVector3() + (Vector3.back * 8);
                Quaternion rot = Quaternion.identity;
                if (e.WasOnRightWall) {
                    rot = Quaternion.Euler(0, 0, 180);
                } else {
                    particleOffset.x *= -1;
                }
                SpawnParticle(Enums.PrefabParticle.Player_WallJump.GetGameObject(), e.Position.ToUnityVector3() + particleOffset, rot);
                PlaySound(SoundEffect.Player_Sound_WallJump);
                PlaySound(SoundEffect.Player_Voice_WallJump);
            }
            animator.SetTrigger("walljump");
        }

        private void OnMarioPlayerCollectedCoin(EventMarioPlayerCollectedCoin e) {
            if (e.Entity != EntityRef) {
                return;
            }

            var mario = VerifiedFrame.Unsafe.GetPointer<MarioPlayer>(EntityRef);

            GameObject number = Instantiate(coinNumberParticle, e.CoinLocation.ToUnityVector3(), Quaternion.identity);
            number.GetComponentInChildren<NumberParticle>().Initialize(
                Utils.GetSymbolString(e.Coins.ToString(), Utils.numberSymbols),
                Utils.GetPlayerColor(VerifiedFrame, mario->PlayerRef),
                e.ItemSpawned != EntityRef.None
            );

            PlaySound(SoundEffect.World_Coin_Collect);
            if (e.ItemSpawned != EntityRef.None) {
                PlaySound(SoundEffect.Player_Sound_PowerupReserveUse);
            }

            if (e.CoinFromBlock) {
                GameObject coin = Instantiate(coinFromBlockParticle, e.CoinLocation.ToUnityVector3(), Quaternion.identity);
                coin.GetComponentInChildren<Animator>().SetBool("down", e.Downwards);
                Destroy(coin, 1);
            }
        }

        private void OnMarioPlayerCollectedObjectiveCoin(EventMarioPlayerCollectedObjectiveCoin e) {
            if (e.Entity != EntityRef) {
                return;
            }

            coinSfx.pitch = UnityEngine.Random.Range(1.35f, 1.45f);
            coinSfx.Play();
        }

        private void OnMarioPlayerUsedReserveItem(EventMarioPlayerUsedReserveItem e) {
            if (e.Entity != EntityRef) {
                return;
            }

            if (e.Success) {
                PlaySound(SoundEffect.Player_Sound_PowerupReserveUse);
            } else if (IsMarioLocal(e.Entity)) {
                PlaySound(SoundEffect.UI_Error);
            }
        }

        private void OnMarioPlayerCollectedPowerup(EventMarioPlayerCollectedPowerup e) {
            if (e.Entity != EntityRef) {
                return;
            }

            var powerup = e.Scriptable;

            switch (e.Result) {
            case PowerupReserveResult.CollectNewReserveOld:
            case PowerupReserveResult.CollectNewIgnoreOld: {
                // Just play the collect sound
                /*
                if (powerup.SoundPlaysEverywhere) {
                    PlaySoundEverywhere(powerup.SoundEffect);
                } else {
                    PlaySound(powerup.SoundEffect);
                }
                */

                if (powerup.Instant) {
                    PlaySound(powerup.SoundEffect, new[] { powerup });
                }

                if (powerup.State == PowerupState.MegaMushroom) {
                    // play the sound here
                    PlaySound(powerup.SoundEffect, new[] { powerup });
                    var mario = PredictedFrame.Unsafe.GetPointer<MarioPlayer>(EntityRef);
                    animator.Play(StateMegaScale, 0, 1f - (mario->MegaMushroomStartFrames / 90f));
                    Vector3 spawnPosition = transform.position;
                    spawnPosition.z = -4f;
                    SpawnParticle(Enums.PrefabParticle.Player_MegaMushroom.GetGameObject(), spawnPosition);
                }
                break;
            }
            case PowerupReserveResult.KeepOldReserveNew: {
                // Reserve the new powerup
                PlaySound(SoundEffect.Player_Sound_PowerupReserveStore, new[] { powerup });
                break;
            }
            }
        }

        private void OnMarioPlayerCrouched(EventMarioPlayerCrouched e) {
            if (e.Entity != EntityRef) {
                return;
            }

            PlaySound(e.PowerupState == PowerupState.BlueShell ? SoundEffect.Powerup_BlueShell_Enter : SoundEffect.Player_Sound_Crouch);
        }

        private void OnMarioPlayerGroundpoundStarted(EventMarioPlayerGroundpoundStarted e) {
            if (e.Entity != EntityRef) {
                return;
            }

            PlaySound(SoundEffect.Player_Sound_GroundpoundStart);
        }

        private void OnMarioPlayerGroundpounded(EventMarioPlayerGroundpounded e) {
            if (e.Entity != EntityRef) {
                return;
            }

            // Groundpound
            if (e.PowerupState == PowerupState.MegaMushroom) {
                PlaySound(SoundEffect.Powerup_MegaMushroom_Groundpound);

                SpawnParticle(Enums.PrefabParticle.Player_MegaGroundpoundStars.GetGameObject(), transform.position + (Vector3.back * 5));
                SpawnParticle(Enums.PrefabParticle.Player_MegaGroundpoundDust.GetGameObject(), transform.position + (Vector3.back * 5));
                SpawnParticle(Enums.PrefabParticle.Player_MegaGroundpoundImpact.GetGameObject(), transform.position + (Vector3.back * 5));
                CameraAnimator.TriggerScreenshake(0.35f);

                if (IsMarioLocal(e.Entity)) {
                    GlobalController.Instance.rumbleManager.RumbleForSeconds(0.8f, 0.3f, 0.5f,
                        RumbleManager.RumbleSetting.Low);
                }

            } else {
                SoundEffect soundEffect = e.PowerupState switch {
                    PowerupState.MiniMushroom => SoundEffect.Powerup_MiniMushroom_Groundpound,
                    _ => SoundEffect.Player_Sound_GroundpoundLanding,
                };
                PlaySound(soundEffect);

                SpawnParticle(Enums.PrefabParticle.Player_Groundpound.GetGameObject(), transform.position + (Vector3.back * 5));

                if (IsMarioLocal(e.Entity)) {
                    GlobalController.Instance.rumbleManager.RumbleForSeconds(0.3f, 0.5f, 0.2f,
                        RumbleManager.RumbleSetting.Low);
                }
            }
        }

        private void OnMarioPlayerJumped(EventMarioPlayerJumped e) {
            if (e.Entity != EntityRef) {
                return;
            }

            if (e.IsUnderwater) {
                // Paddle
                if (e.WasBounce) {
                    if (Time.time - lastStompSoundTime > 0.25f) {
                        PlaySound(SoundEffect.Enemy_Generic_Stomp);
                        lastStompSoundTime = Time.time;
                    }
                } else { 
                    PlaySound(SoundEffect.Player_Sound_Swim);
                    animator.SetTrigger(ParamPaddle);
                }
                return;
            }

            // Voice SFX
            switch (e.JumpState) {
            case JumpState.DoubleJump:
                PlaySound(SoundEffect.Player_Voice_DoubleJump);
                break;
            case JumpState.TripleJump:
                PlaySound(SoundEffect.Player_Voice_TripleJump);
                break;
            }

            // Jump SFX
            if (e.WasBounce) {
                if (Time.time - lastStompSoundTime > 0.25f) {
                    PlaySound(SoundEffect.Enemy_Generic_Stomp);
                    lastStompSoundTime = Time.time;
                }

                if (IsMarioLocal(e.Entity)) {
                    GlobalController.Instance.rumbleManager.RumbleForSeconds(0.1f, 0.4f, 0.15f, RumbleManager.RumbleSetting.Low);
                }
            } else {
                SoundEffect soundEffect = e.PowerupState switch {
                    PowerupState.MiniMushroom => SoundEffect.Powerup_MiniMushroom_Jump,
                    PowerupState.MegaMushroom => SoundEffect.Powerup_MegaMushroom_Jump,
                    _ => SoundEffect.Player_Sound_Jump,
                };
                PlaySound(soundEffect);
            }
        }

        private void OnMarioPlayerEnteredPipe(EventMarioPlayerEnteredPipe e) {
            if (e.Entity != EntityRef) {
                return;
            }

            PlaySound(SoundEffect.World_Pipe_Use);

            if (IsMarioLocal(e.Entity) && !e.Exiting && e.HorizontalWarpDirection != 0) {
                GlobalController.Instance.fader.Fade(
                    e.HorizontalWarpDirection == -1 ? AnimatedFader.FadeStyle.LeftSideBumps : AnimatedFader.FadeStyle.RightSideBumps,
                    e.HorizontalWarpDirection == -1 ? AnimatedFader.FadeStyle.RightSideBumps : AnimatedFader.FadeStyle.LeftSideBumps
                );
            }
        }

        private void OnMarioPlayerStoppedSliding(EventMarioPlayerStoppedSliding e) {
            if (e.Entity != EntityRef) {
                return;
            }

            if (e.IsStationary) {
                PlaySound(SoundEffect.Player_Sound_SlideEnd);
            }
        }

        private void OnMarioPlayerUsedSpinner(EventMarioPlayerUsedSpinner e) {
            if (e.Entity != EntityRef) {
                return;
            }

            PlaySound(SoundEffect.Player_Voice_SpinnerLaunch);
        }

        private void OnGameResynced(CallbackGameResynced e) {
            if (activeRespawnParticle) {
                Destroy(activeRespawnParticle);
            }

            Frame f = PredictedFrame;
            if (f.Unsafe.TryGetPointer(EntityRef, out MarioPlayer* mario)) {
                if (mario->MegaMushroomStartFrames > 0) {
                    // Growing animation
                    animator.Play(StateMegaScale, 0, 1f - (mario->MegaMushroomStartFrames / 90f));
                } else if (mario->CurrentPowerupState == PowerupState.MegaMushroom) {
                    // Mega
                    animator.Play(StateMegaIdle);
                } else if (mario->MegaMushroomEndFrames > 0 && mario->MegaMushroomStationaryEnd) {
                    // Shrinking animation
                    animator.Play(StateMegaCancel, 0, 1f - (mario->MegaMushroomEndFrames / 90f));
                }
            }

            previousPowerupVisuals = null;
            GlowColor = Utils.GetPlayerColor(f, mario->PlayerRef);
        }

        private void OnMarioPlayerLandedWithAnimation(EventMarioPlayerLandedWithAnimation e) {
            if (e.Entity != EntityRef) {
                return;
            }

            if (animator.GetCurrentAnimatorStateInfo(0).shortNameHash == StateFalling || animator.GetCurrentAnimatorStateInfo(0).shortNameHash == ParamTripleJump) {
                if (animator.GetCurrentAnimatorStateInfo(0).shortNameHash == StateFalling) {
                    animator.Play(StateJumplanding);
                } else if (animator.GetCurrentAnimatorStateInfo(0).shortNameHash == ParamTripleJump) {
                    animator.Play(StateJumplandingEdge);
                    SpawnParticle(Enums.PrefabParticle.Player_TripleJumpLandingDust.GetGameObject(), transform.position + Vector3.back * 5);
                }
            }
        }

        private void OnEnemyKicked(EventEnemyKicked e) {
            if (e.Entity != EntityRef) {
                return;
            }

            PlaySound(SoundEffect.Powerup_HammerSuit_Bounce);
        }

        private void OnMarioPlayerTaunted(EventMarioPlayerTaunted e) {
            if (e.Entity != EntityRef) {
                return;
            }

            PlaySound(SoundEffect.Player_Voice_Taunt);
        }

        private void OnMarioPlayerTauntCancelled(EventMarioPlayerTauntCancelled e) {
            if (e.Entity != EntityRef) {
                return;
            }

            sfx.Stop();
        }
        private void OnMarioPlayerUpdatePowerupQueue(EventMarioPlayerUpdatePowerupQueue e) {
            if (e.Entity != EntityRef) {
                return;
            }

            var anim = e.Anim;
            if (anim.IsPowerdown) {
                PlaySound(SoundEffect.Player_Sound_Powerdown);
            } else {
                Frame f = PredictedFrame;
                var powerup = f.FindAsset(anim.Scriptable);
                PlaySound(powerup.SoundEffect, new[] { powerup });
            }
        }
    }
}
