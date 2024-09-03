using NSMB.Extensions;
using Photon.Deterministic;
using Quantum;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Input = Quantum.Input;

namespace NSMB.Entities.Player {

    public unsafe class MarioAnimator : QuantumCallbacks {

        //---Static
        public static event Action<Frame, MarioAnimator> MarioPlayerInitialized;
        public static event Action<Frame, MarioAnimator> MarioPlayerDestroyed;

        //---Static Variables
        private static readonly WaitForSeconds BlinkDelay = new(0.1f);
        #region Animator Hashes
        private static readonly int ParamRainbowEnabled = Shader.PropertyToID("RainbowEnabled");
        private static readonly int ParamPowerupState = Shader.PropertyToID("PowerupState");
        private static readonly int ParamEyeState = Shader.PropertyToID("EyeState");
        private static readonly int ParamModelScale = Shader.PropertyToID("ModelScale");
        private static readonly int ParamMultiplyColor = Shader.PropertyToID("MultiplyColor");
        private static readonly int ParamOverallsColor = Shader.PropertyToID("OverallsColor");
        private static readonly int ParamShirtColor = Shader.PropertyToID("ShirtColor");
        private static readonly int ParamHatUsesOverallsColor = Shader.PropertyToID("HatUsesOverallsColor");
        private static readonly int ParamGlowColor = Shader.PropertyToID("GlowColor");
        private static readonly int ParamVelocityX = Animator.StringToHash("velocityX");
        private static readonly int ParamDead = Animator.StringToHash("dead");
        private static readonly int ParamVelocityY = Animator.StringToHash("velocityY");
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
        private static readonly int ParamFireballKnockback = Animator.StringToHash("fireballKnockback");
        private static readonly int ParamKnockforwards = Animator.StringToHash("knockforwards");
        #endregion

        //---Public Variables
        public bool wasTurnaround, enableGlow;
        public GameObject models;

        //---Serialized Variables
        [SerializeField] public QuantumEntityView entity;
        [SerializeField] private CharacterAsset character;
        [SerializeField] private GameObject coinNumberParticle, coinFromBlockParticle, respawnParticle, starCollectParticle;
        [SerializeField] private Animator animator;
        [SerializeField] private Avatar smallAvatar, largeAvatar;
        [SerializeField] private ParticleSystem dust, sparkles, drillParticle, giantParticle, fireParticle, bubblesParticle;
        [SerializeField] private GameObject smallModel, largeModel, largeShellExclude, blueShell, propellerHelmet, propeller;
        [SerializeField] private AudioClip normalDrill, propellerDrill;
        [SerializeField] private LoopingSoundPlayer dustPlayer, drillPlayer;
        [SerializeField] private LoopingSoundData wallSlideData, shellSlideData, spinnerDrillData, propellerDrillData;

        [SerializeField] private AudioSource sfx;

        //---Components
        private readonly List<Renderer> renderers = new();

        //---Properties
        public Color GlowColor { get; private set; }

        //---Private Variables
        private Enums.PlayerEyeState eyeState;
        private float propellerVelocity;
        private Vector3 modelRotationTarget;
        private bool modelRotateInstantly, footstepVariant;
        private PlayerColors skin;
        private bool doDeathUp;
        private float lastBumpSound;
        private MaterialPropertyBlock materialBlock;

        private VersusStageData stage;

        public void OnValidate() {
            this.SetIfNull(ref animator);
            this.SetIfNull(ref entity);
        }

        protected override void OnEnable() {
            base.OnEnable();
            // GameManager.OnAllPlayersLoaded += OnAllPlayersLoaded;
        }

        protected override void OnDisable() {
            base.OnDisable();
            // GameManager.OnAllPlayersLoaded -= OnAllPlayersLoaded;
        }

        public void Start() {
            // DisableAllModels();

            renderers.AddRange(GetComponentsInChildren<MeshRenderer>(true));
            renderers.AddRange(GetComponentsInChildren<SkinnedMeshRenderer>(true));

            modelRotationTarget = models.transform.rotation.eulerAngles;

            StartCoroutine(BlinkRoutine());

            QuantumEvent.Subscribe<EventMarioPlayerJumped>(this, OnMarioPlayerJumped);
            QuantumEvent.Subscribe<EventMarioPlayerGroundpounded>(this, OnMarioPlayerGroundpounded);
            QuantumEvent.Subscribe<EventMarioPlayerGroundpoundStarted>(this, OnMarioPlayerGroundpoundStarted);
            QuantumEvent.Subscribe<EventMarioPlayerCrouched>(this, OnMarioPlayerCrouched);
            QuantumEvent.Subscribe<EventMarioPlayerCollectedPowerup>(this, OnMarioPlayerCollectedPowerup);
            QuantumEvent.Subscribe<EventMarioPlayerUsedReserveItem>(this, OnMarioPlayerUsedReserveItem);
            QuantumEvent.Subscribe<EventMarioPlayerCollectedCoin>(this, OnMarioPlayerCollectedCoin);
            QuantumEvent.Subscribe<EventMarioPlayerWalljumped>(this, OnMarioPlayerWalljumped);
            QuantumEvent.Subscribe<EventMarioPlayerShotProjectile>(this, OnMarioPlayerShotProjectile);
            QuantumEvent.Subscribe<EventMarioPlayerUsedPropeller>(this, OnMarioPlayerUsedPropeller);
            QuantumEvent.Subscribe<EventMarioPlayerPropellerSpin>(this, OnMarioPlayerPropellerSpin);
            QuantumEvent.Subscribe<EventMarioPlayerCollectedStar>(this, OnMarioPlayerCollectedStar);
            QuantumEvent.Subscribe<EventMarioPlayerDied>(this, OnMarioPlayerDied);
            QuantumEvent.Subscribe<EventMarioPlayerPreRespawned>(this, OnMarioPlayerPreRespawned);
            QuantumEvent.Subscribe<EventMarioPlayerRespawned>(this, OnMarioPlayerRespawned);
            QuantumEvent.Subscribe<EventMarioPlayerTookDamage>(this, OnMarioPlayerTookDamage);
            QuantumEvent.Subscribe<EventPlayBumpSound>(this, OnPlayBumpSound);
            QuantumEvent.Subscribe<EventMarioPlayerPickedUpObject>(this, OnMarioPlayerPickedUpObject);
            QuantumEvent.Subscribe<EventMarioPlayerThrewObject>(this, OnMarioPlayerThrewObject);
            QuantumEvent.Subscribe<EventMarioPlayerMegaStart>(this, OnMarioPlayerMegaStart);
            QuantumEvent.Subscribe<EventMarioPlayerMegaEnd>(this, OnMarioPlayerMegaEnd);
            QuantumEvent.Subscribe<EventMarioPlayerReceivedKnockback>(this, OnMarioPlayerReceivedKnockback);
            QuantumEvent.Subscribe<EventMarioPlayerEnteredPipe>(this, OnMarioPlayerEnteredPipe);

            stage = (VersusStageData) QuantumUnityDB.GetGlobalAsset(FindObjectOfType<QuantumMapData>().Asset.UserAsset);
        }

        public void Initialize(QuantumGame game) {
            var mario = game.Frames.Predicted.Get<MarioPlayer>(entity.EntityRef);
            var playerData = game.Frames.Predicted.GetPlayerData(mario.PlayerRef);

            if (ScriptableManager.Instance.skins[playerData?.SkinIndex ?? 0] is PlayerColorSet colorSet) {
                skin = colorSet.GetPlayerColors(character);
            }

            GlowColor = Utils.Utils.GetPlayerColor(game, mario.PlayerRef);

            foreach (PlayerElements pe in PlayerElements.AllPlayerElements) {
                if (pe.Player == mario.PlayerRef) {
                    pe.SetEntity(entity.EntityRef);
                    break;
                }
            }

            MarioPlayerInitialized?.Invoke(game.Frames.Verified, this);
        }

        public void Destroy(QuantumGame game) {
            MarioPlayerDestroyed?.Invoke(game.Frames.Verified, this);
        }

        public void Update() {
            largeShellExclude.SetActive(!animator.GetCurrentAnimatorStateInfo(0).IsName("in-shell"));
        }

        public override void OnUpdateView(QuantumGame game) {
            /*
            if (GameManager.Instance.GameStartTimer.IsRunning) {
                DisableAllModels();
                return;
            }
            */

            Frame f = game.Frames.Predicted;
            MarioPlayer mario = f.Get<MarioPlayer>(entity.EntityRef);
            PhysicsObject physicsObject = f.Get<PhysicsObject>(entity.EntityRef);
            Input inputs = default;
            if (mario.PlayerRef.IsValid) {
                inputs = *f.GetPlayerInput(mario.PlayerRef);
            }

            HandleAnimations(f, mario, physicsObject);
            SetFacingDirection(f, mario, physicsObject);
            InterpolateFacingDirection(mario);
            HandleMiscStates(mario);
            UpdateAnimatorVariables(f, mario, physicsObject, inputs);
        }

        public void HandleAnimations(Frame f, MarioPlayer mario, PhysicsObject physicsObject) {
            /*
            if (GameManager.Instance.GameEnded) {
                models.SetActive(true);

                // Disable Particles
                SetParticleEmission(drillParticle, false);
                SetParticleEmission(sparkles, false);
                SetParticleEmission(dust, false);
                SetParticleEmission(giantParticle, false);
                SetParticleEmission(fireParticle, false);
                SetParticleEmission(bubblesParticle, false);
                return;
            }
            */

            // Particles
            SetParticleEmission(drillParticle, !mario.IsDead && mario.IsDrilling);
            SetParticleEmission(sparkles, !mario.IsDead && mario.IsStarmanInvincible);
            SetParticleEmission(dust, !mario.IsDead && (mario.IsWallsliding || (physicsObject.IsTouchingGround && (mario.IsSkidding || (mario.IsCrouching && physicsObject.Velocity.SqrMagnitude.AsFloat > 0.25f))) || (((mario.IsSliding && Mathf.Abs(physicsObject.Velocity.X.AsFloat) > 0.25f) || mario.IsInShell) && physicsObject.IsTouchingGround)) && !mario.CurrentPipe.IsValid);
            SetParticleEmission(giantParticle, !mario.IsDead && mario.CurrentPowerupState == PowerupState.MegaMushroom && mario.MegaMushroomStartFrames == 0);
            SetParticleEmission(fireParticle, mario.IsDead && !mario.IsRespawning && mario.FireDeath && !physicsObject.IsFrozen);
            SetParticleEmission(bubblesParticle, mario.IsInWater);

            if (mario.IsDead && !physicsObject.IsFrozen && doDeathUp) {
                animator.SetTrigger("deathup");
                doDeathUp = false;
            }

            var hitbox = f.Get<PhysicsCollider2D>(entity.EntityRef);
            if (mario.IsCrouching || mario.IsSliding || mario.IsSkidding || mario.IsInShell) {
                dust.transform.localPosition = Vector2.zero;
            } else if (mario.IsWallsliding) {
                dust.transform.localPosition = hitbox.Shape.Box.Extents.ToUnityVector2() * 1.5f * (mario.WallslideLeft ? new Vector2(-1, 1) : Vector2.one);
            }

            dustPlayer.SetSoundData((mario.IsInShell || mario.IsSliding || mario.IsCrouchedInShell) ? shellSlideData : wallSlideData);
            drillPlayer.SetSoundData(mario.IsPropellerFlying ? propellerDrillData : spinnerDrillData);
            bubblesParticle.transform.localPosition = new(bubblesParticle.transform.localPosition.x, hitbox.Shape.Box.Extents.Y.AsFloat * 2);

            /*
            if (cameraController.IsControllingCamera) {
                HorizontalCamera.SizeIncreaseTarget = (mario.IsSpinnerFlying || mario.IsPropellerFlying) ? 0.5f : 0f;
            }
            */
        }

        private IEnumerator BlinkRoutine() {
            while (true) {
                yield return new WaitForSeconds(3f + (UnityEngine.Random.value * 6f));
                eyeState = Enums.PlayerEyeState.HalfBlink;
                yield return BlinkDelay;
                eyeState = Enums.PlayerEyeState.FullBlink;
                yield return BlinkDelay;
                eyeState = Enums.PlayerEyeState.HalfBlink;
                yield return BlinkDelay;
                eyeState = Enums.PlayerEyeState.Normal;
            }
        }

        private void SetFacingDirection(Frame f, MarioPlayer mario, PhysicsObject physicsObject) {
            //TODO: refactor
            /*
            if (GameManager.Instance.GameEnded) {
                if (mario.IsDead) {
                    modelRotationTarget.Set(0, 180, 0);
                    modelRotateInstantly = true;
                }
                return;
            }
            */

            //rotChangeTarget = models.transform.rotation.eulerAngles;
            float delta = Time.deltaTime;

            modelRotateInstantly = false;

            if (mario.IsInKnockback /* || controller.IsFrozen*/) {
                bool right = mario.FacingRight;
                if (mario.IsInKnockback && (mario.IsInWater || mario.IsInWeakKnockback)) {
                    right = mario.KnockbackWasOriginallyFacingRight;
                }
                modelRotationTarget.Set(0, right ? 110 : 250, 0);
                modelRotateInstantly = true;

            } else if (mario.IsDead) {
                if (mario.FireDeath /*&& !controller.DeathAnimationTimer.IsRunning*/) {
                    modelRotationTarget.Set(-15, mario.FacingRight ? 110 : 250, 0);
                } else {
                    modelRotationTarget.Set(0, 180, 0);
                }
                modelRotateInstantly = true;

            } else if (animator.GetBool(ParamInShell) && (/*!controller.OnSpinner ||*/ Mathf.Abs(physicsObject.Velocity.X.AsFloat) > 0.3f)) {
                var physics = f.FindAsset(mario.PhysicsAsset);
                modelRotationTarget += Mathf.Abs(physicsObject.Velocity.X.AsFloat) / physics.WalkMaxVelocity[physics.RunSpeedStage].AsFloat * delta * new Vector3(0, 1400 * (mario.FacingRight ? -1 : 1));
                modelRotateInstantly = true;

            } else if (wasTurnaround || mario.IsSkidding || mario.IsTurnaround || animator.GetCurrentAnimatorStateInfo(0).IsName("turnaround")) {
                bool flip = mario.FacingRight ^ (animator.GetCurrentAnimatorStateInfo(0).IsName("turnaround") || mario.IsSkidding);
                modelRotationTarget.Set(0, flip ? 250 : 110, 0);
                modelRotateInstantly = true;
            } else {
                /*if ((controller.OnSpinner && physicsObject.IsTouchingGround && controller.FireballDelayTimer.ExpiredOrNotRunning(Runner) && Mathf.Abs(physicsObject.Velocity.X.AsFloat) < 0.3f && !mario.HeldEntity.IsValid) && !animator.GetCurrentAnimatorStateInfo(0).IsName("fireball")) {
                    modelRotationTarget += controller.OnSpinner.spinSpeed * delta * Vector3.up;
                    modelRotateInstantly = true;
                } else*/ if (mario.IsSpinnerFlying || mario.IsPropellerFlying) {
                    modelRotationTarget += new Vector3(0, -1200 - ((mario.PropellerLaunchFrames / 60f) * 1400) - (mario.IsDrilling ? 900 : 0) + (mario.IsPropellerFlying && mario.PropellerSpinFrames == 0 && physicsObject.Velocity.Y < 0 ? 700 : 0), 0) * delta;
                    modelRotateInstantly = true;
                } else if (mario.IsWallsliding) {
                    modelRotationTarget.Set(0, mario.WallslideRight ? 110 : 250, 0);
                } else {
                    modelRotationTarget.Set(0, mario.FacingRight ? 110 : 250, 0);
                }
            }

            propellerVelocity = Mathf.Clamp(propellerVelocity + (1200 * ((mario.IsSpinnerFlying || mario.IsPropellerFlying || mario.UsedPropellerThisJump) ? -1 : 1) * delta), -2500, -300);

            wasTurnaround = animator.GetCurrentAnimatorStateInfo(0).IsName("turnaround");
        }

        private void InterpolateFacingDirection(MarioPlayer mario) {

            if (modelRotateInstantly || wasTurnaround) {
                models.transform.rotation = Quaternion.Euler(modelRotationTarget);
            } else /* if (!GameManager.Instance.GameEnded) */ {
                float maxRotation = 2000f * Time.deltaTime;
                float x = models.transform.eulerAngles.x, y = models.transform.eulerAngles.y, z = models.transform.eulerAngles.z;
                x += Mathf.Clamp(modelRotationTarget.x - x, -maxRotation, maxRotation);
                y += Mathf.Clamp(modelRotationTarget.y - y, -maxRotation, maxRotation);
                z += Mathf.Clamp(modelRotationTarget.z - z, -maxRotation, maxRotation);
                models.transform.rotation = Quaternion.Euler(x, y, z);
            }

            /*
            if (GameManager.Instance.GameEnded) {
                return;
            }
            */

            if (mario.CurrentPowerupState == PowerupState.PropellerMushroom /* && !controller.IsFrozen */) {
                propeller.transform.Rotate(Vector3.forward, propellerVelocity * Time.deltaTime);
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

        public void UpdateAnimatorVariables(Frame f, MarioPlayer mario, PhysicsObject physicsObject, Input inputs) {

            bool right = inputs.Right.IsDown;
            bool left = inputs.Left.IsDown;

            animator.SetBool(ParamDead, mario.IsDead);
            animator.SetBool(ParamOnLeft, mario.WallslideLeft);
            animator.SetBool(ParamOnRight, mario.WallslideRight);
            animator.SetBool(ParamOnGround, physicsObject.IsTouchingGround /* || controller.IsStuckInBlock */ || mario.CoyoteTimeFrames > 0);
            animator.SetBool(ParamInvincible, mario.IsStarmanInvincible);
            animator.SetBool(ParamSkidding, mario.IsSkidding);
            animator.SetBool(ParamPropeller, mario.IsPropellerFlying);
            animator.SetBool(ParamPropellerSpin, mario.PropellerSpinFrames > 0);
            animator.SetBool(ParamPropellerStart, mario.PropellerLaunchFrames > 0);
            animator.SetBool(ParamCrouching, mario.IsCrouching);
            animator.SetBool(ParamGroundpound, mario.IsGroundpounding);
            animator.SetBool(ParamSliding, mario.IsSliding);
            animator.SetBool(ParamKnockback, mario.IsInKnockback);
            animator.SetBool(ParamFacingRight, (left ^ right) ? right : mario.FacingRight);
            animator.SetBool(ParamFlying, mario.IsSpinnerFlying);
            animator.SetBool(ParamDrill, mario.IsDrilling);
            animator.SetBool(ParamDoubleJump, mario.JumpState == JumpState.DoubleJump);
            animator.SetBool(ParamTripleJump, mario.JumpState == JumpState.TripleJump);
            animator.SetBool(ParamHolding, mario.HeldEntity.IsValid);
            //animator.SetBool(ParamHeadCarry, mario.HeldEntity.IsValid && controller.HeldEntity is FrozenCube);
            //animator.SetBool(ParamCarryStart, mario.HeldEntity.IsValid && controller.HeldEntity is FrozenCube && (Runner.SimulationTime - controller.HoldStartTime) < controller.pickupTime);
            animator.SetBool(ParamPipe, mario.CurrentPipe.IsValid);
            animator.SetBool(ParamBlueShell, mario.CurrentPowerupState == PowerupState.BlueShell);
            animator.SetBool(ParamMini, mario.CurrentPowerupState == PowerupState.MiniMushroom);
            animator.SetBool(ParamMega, mario.CurrentPowerupState == PowerupState.MegaMushroom);
            animator.SetBool(ParamInShell, mario.IsInShell || (mario.CurrentPowerupState == PowerupState.BlueShell && (mario.IsCrouching || mario.IsGroundpounding || mario.IsSliding) && mario.GroundpoundStartFrames <= 9));
            animator.SetBool(ParamTurnaround, mario.IsTurnaround);
            animator.SetBool(ParamSwimming, mario.IsInWater && !mario.IsGroundpounding && !mario.IsDrilling /*&& !mario.IsFrozen*/);
            animator.SetBool(ParamAHeld, inputs.Jump.IsDown);
            animator.SetBool(ParamFireballKnockback, mario.IsInWeakKnockback);
            //animator.SetBool(ParamKnockforwards, mario.IsInForwardsKnockback);

            float animatedVelocity = physicsObject.IsTouchingGround ? physicsObject.Velocity.Magnitude.AsFloat : Mathf.Abs(physicsObject.Velocity.X.AsFloat);
            /*
            if (controller.IsStuckInBlock) {
                animatedVelocity = 0;
            } else */if (mario.IsPropellerFlying) {
                animatedVelocity = 2f;
            } else if (mario.CurrentPowerupState == PowerupState.MegaMushroom && (left || right)) {
                animatedVelocity = 4.5f;
            } else if (left ^ right && !physicsObject.IsTouchingRightWall && !physicsObject.IsTouchingLeftWall && mario.GroundpoundStandFrames == 0) {
                animatedVelocity = Mathf.Max(physicsObject.IsOnSlipperyGround ? 2.7f : 2f, animatedVelocity);
            } else if (physicsObject.IsOnSlipperyGround) {
                animatedVelocity = 0;
            }
            if (animatedVelocity < 0.01f) {
                animatedVelocity = 0;
            }

            animator.SetFloat(ParamVelocityX, animatedVelocity);
            animator.SetFloat(ParamVelocityY, physicsObject.Velocity.Y.AsFloat);
        }

        private void HandleMiscStates(MarioPlayer mario) {
            // Scale
            Vector3 scale;
            if (mario.MegaMushroomEndFrames > 0) {
                float endTimer = mario.MegaMushroomEndFrames / 60f;
                if (!mario.MegaMushroomStationaryEnd) {
                    endTimer *= 2;
                }

                scale = Vector3.one + (Vector3.one * (Mathf.Min(1, endTimer / 1.5f) * 2.6f));
            } else {
                float startTimer = mario.MegaMushroomStartFrames / 60f;

                scale = mario.CurrentPowerupState switch {
                    PowerupState.MiniMushroom => Vector3.one * 0.5f,
                    PowerupState.MegaMushroom => Vector3.one + (Vector3.one * (Mathf.Min(1, 1 - (startTimer / 1.5f)) * 2.6f)),
                    _ => Vector3.one,
                };
            }
            models.transform.SetLossyScale(scale);

            // Shader effects
            TryCreateMaterialBlock();
            materialBlock.SetFloat(ParamRainbowEnabled, mario.IsStarmanInvincible ? 1f : 0f);
            int ps = mario.CurrentPowerupState switch {
                PowerupState.FireFlower => 1,
                PowerupState.PropellerMushroom => 2,
                PowerupState.IceFlower => 3,
                _ => 0
            };
            materialBlock.SetFloat(ParamPowerupState, ps);
            materialBlock.SetFloat(ParamEyeState, (int) (mario.IsDead ? Enums.PlayerEyeState.Death : eyeState));
            materialBlock.SetFloat(ParamModelScale, transform.lossyScale.x);

            Vector3 giantMultiply = Vector3.one;
            float giantTimeRemaining = mario.MegaMushroomFrames / 60f;
            if (giantTimeRemaining > 0 && giantTimeRemaining < 4) {
                float v = ((Mathf.Sin(giantTimeRemaining * 20f) + 1f) * 0.45f) + 0.1f;
                giantMultiply = new Vector3(v, 1, v);
            }

            materialBlock.SetVector(ParamMultiplyColor, giantMultiply);

            foreach (Renderer r in renderers) {
                r.SetPropertyBlock(materialBlock);
            }

            // Hit flash
            float remainingDamageInvincibility = mario.DamageInvincibilityFrames / 60f;
            models.SetActive(mario.MegaMushroomStartFrames > 0 || (!mario.IsRespawning && (mario.IsDead || !(remainingDamageInvincibility > 0 && remainingDamageInvincibility * (remainingDamageInvincibility <= 0.75f ? 5 : 2) % 0.2f < 0.1f))));

            // Model changing
            bool large = mario.CurrentPowerupState >= PowerupState.Mushroom;

            largeModel.SetActive(large);
            smallModel.SetActive(!large);
            blueShell.SetActive(mario.CurrentPowerupState == PowerupState.BlueShell);
            propellerHelmet.SetActive(mario.CurrentPowerupState == PowerupState.PropellerMushroom);

            Avatar targetAvatar = large ? largeAvatar : smallAvatar;
            bool changedAvatar = animator.avatar != targetAvatar;

            if (changedAvatar) {
                // Preserve Animations
                AnimatorStateInfo[] layerInfo = new AnimatorStateInfo[animator.layerCount];
                for (int i = 0; i < animator.layerCount; i++) {
                    layerInfo[i] = animator.GetCurrentAnimatorStateInfo(i);
                    Debug.Log(i + " - " + layerInfo[i].fullPathHash);
                }

                animator.avatar = targetAvatar;
                animator.runtimeAnimatorController = large ? character.LargeOverrides : character.SmallOverrides;

                // Push back state 
                animator.Update(0);

                for (int i = 0; i < animator.layerCount; i++) {
                    animator.Play(layerInfo[i].fullPathHash, i, layerInfo[i].normalizedTime);
                    animator.Update(0);
                    Debug.Log(i + " -" + animator.GetCurrentAnimatorStateInfo(i).fullPathHash);
                }

                animator.Update(0);
            }


            float newZ = -4;
            if (mario.IsDead) {
                newZ = -6;
            } /*else if (controller.IsFrozen) {
                newZ = -2;
            }*/ else if (mario.CurrentPipe.IsValid) {
                newZ = 1;
            }

            transform.position = new(transform.position.x, transform.position.y, newZ);
        }

        private void TryCreateMaterialBlock() {
            if (materialBlock != null) {
                return;
            }

            materialBlock = new();

            // Customizable player color
            materialBlock.SetVector(ParamOverallsColor, skin?.overallsColor.linear ?? Color.clear);
            materialBlock.SetVector(ParamShirtColor, skin?.shirtColor != null ? skin.shirtColor.linear : Color.clear);
            materialBlock.SetFloat(ParamHatUsesOverallsColor, (skin?.hatUsesOverallsColor ?? false) ? 1 : 0);

            // Glow Color
            if (enableGlow) {
                materialBlock.SetColor(ParamGlowColor, GlowColor);
            }
        }

        public void PlaySoundEverywhere(SoundEffect soundEffect) {
            // GameManager.Instance.sfx.PlayOneShot(sound, character);
        }
        public void PlaySound(SoundEffect soundEffect, CharacterAsset characterData = null, byte variant = 0, float volume = 1) {
            characterData ??= character;
            sfx.PlayOneShot(soundEffect, characterData, variant, volume);
        }
        public GameObject SpawnParticle(string particle, Vector2 worldPos, Quaternion? rot = null) {
            return Instantiate(Resources.Load(particle), worldPos, rot ?? Quaternion.identity) as GameObject;
        }
        public GameObject SpawnParticle(GameObject particle, Vector2 worldPos, Quaternion? rot = null) {
            return Instantiate(particle, worldPos, rot ?? Quaternion.identity);
        }

        public void Footstep() {
            Frame f = entity.Game.Frames.Predicted;
            var mario = f.Get<MarioPlayer>(entity.EntityRef);
            var marioTransform = f.Get<Transform2D>(entity.EntityRef);
            var physicsObject = f.Get<PhysicsObject>(entity.EntityRef);
            var physics = f.FindAsset(mario.PhysicsAsset);
            Input input = *f.GetPlayerInput(mario.PlayerRef);

            if (mario.IsInWater || mario.CurrentPowerupState == PowerupState.MegaMushroom) {
                return;
            }

            bool left = input.Left.IsDown;
            bool right = input.Right.IsDown;

            bool reverse = physicsObject.Velocity.X != 0 && ((left ? 1 : -1) == FPMath.Sign(physicsObject.Velocity.X));
            if (physicsObject.IsOnSlipperyGround && (left ^ right) && reverse) {
                PlaySound(SoundEffect.World_Ice_Skidding);
                return;
            }
            if (mario.IsPropellerFlying) {
                PlaySound(SoundEffect.Powerup_PropellerMushroom_Kick);
                return;
            }
            /*
            if (IsWaterWalking) {
                footstepSound = Enums.Sounds.Player_Walk_Water;
            }
            if (footstepParticle != Enums.Particle.None) {
                GameManager.Instance.particleManager.Play((Enums.Particle) ((int) footstepParticle + (FacingRight ? 1 : 0)), body.Position);
            }
            */
            SoundEffect footstepSoundEffect = SoundEffect.Player_Walk_Grass;
            ParticleEffect footstepParticleEffect = ParticleEffect.None;

            foreach (var contact in f.ResolveList(physicsObject.Contacts)) {
                if (FPVector2.Dot(contact.Normal, FPVector2.Up) > FP._0_33) {
                    StageTileInstance tileInstance = stage.GetTileRelative(f, contact.TileX, contact.TileY);
                    if (QuantumUnityDB.TryGetGlobalAsset(tileInstance.Tile, out StageTile tile)) {
                        if (tile.FootstepSound != SoundEffect.Player_Walk_Grass) {
                            footstepSoundEffect = tile.FootstepSound;
                        }
                        if (tile.FootstepParticle != ParticleEffect.None) {
                            footstepParticleEffect = tile.FootstepParticle;
                        }
                    }
                }
            } 

            if (/*!IsWaterWalking && */ FPMath.Abs(physicsObject.Velocity.X) < physics.WalkMaxVelocity[physics.WalkSpeedStage]) {
                return;
            }

            PlaySound(footstepSoundEffect,
                variant: (byte) (footstepVariant ? 1 : 2),
                volume: (FPMath.Abs(physicsObject.Velocity.X) / (physics.WalkMaxVelocity[physics.RunSpeedStage] + 4)).AsFloat
            );
            SingleParticleManager.Instance.Play(footstepParticleEffect, marioTransform.Position.ToUnityVector3());
            footstepVariant = !footstepVariant;
        }


        public void PlayMegaFootstep() {
            Frame f = entity.Game.Frames.Predicted;
            var mario = f.Get<MarioPlayer>(entity.EntityRef);
            if (mario.IsInWater) {
                return;

            }
            var marioTransform = f.Get<Transform2D>(entity.EntityRef);

            // CameraController.ScreenShake = 0.15f;
            SpawnParticle(Enums.PrefabParticle.Player_Groundpound.GetGameObject(), marioTransform.Position.ToUnityVector2() + new Vector2(mario.FacingRight ? 0.5f : -0.5f, 0));
            PlaySound(SoundEffect.Powerup_MegaMushroom_Walk, variant: (byte) (footstepVariant ? 1 : 2));
            GlobalController.Instance.rumbleManager.RumbleForSeconds(0.5f, 0f, 0.1f, RumbleManager.RumbleSetting.High);
            footstepVariant = !footstepVariant;
        }

        /*
        private void OnAllPlayersLoaded() {
            enableGlow = SessionData.Instance.Teams || !Object.HasControlAuthority();
            GlowColor = Utils.Utils.GetPlayerColor(controller.Data.Owner);

            if (!Object.HasControlAuthority()) {
                GameManager.Instance.CreateNametag(controller);
            }

            icon = UIUpdater.Instance.CreateTrackIcon(controller);
            TryCreateMaterialBlock();
        }
        */

        private void OnMarioPlayerReceivedKnockback(EventMarioPlayerReceivedKnockback e) {
            if (e.Entity != entity.EntityRef) {
                return;
            }

            if (e.Frame.TryGet(e.Attacker, out Transform2D attackerTransform)) {
                SpawnParticle("Prefabs/Particle/PlayerBounce", attackerTransform.Position.ToUnityVector3());
            }

            PlaySound(e.Weak ? SoundEffect.Player_Sound_Collision_Fireball : SoundEffect.Player_Sound_Collision);

            /*
            if (cameraController.IsControllingCamera) {
                GlobalController.Instance.rumbleManager.RumbleForSeconds(0.3f, 0.6f, e.Weak ? 0.3f : 0.5f, RumbleManager.RumbleSetting.Low);
            }
            */
        }

        private void OnMarioPlayerMegaEnd(EventMarioPlayerMegaEnd e) {
            if (e.Entity != entity.EntityRef) {
                return;
            }

            if (!e.Cancelled) {
                PlaySound(SoundEffect.Powerup_MegaMushroom_End);
            }
        }

        private void OnMarioPlayerMegaStart(EventMarioPlayerMegaStart e) {
            if (e.Entity != entity.EntityRef) {
                return;
            }

            PlaySound(SoundEffect.Player_Voice_MegaMushroom);
        }

        private void OnMarioPlayerThrewObject(EventMarioPlayerThrewObject e) {
            if (e.Entity != entity.EntityRef) {
                return;
            }

            PlaySound(SoundEffect.Player_Voice_WallJump, variant: 2);
            animator.SetTrigger("throw");
        }

        private void OnMarioPlayerPickedUpObject(EventMarioPlayerPickedUpObject e) {
            if (e.Entity != entity.EntityRef) {
                return;
            }

            /*
            if (HeldEntity is FrozenCube) {
                animator.Play("head-pickup");
                animator.ResetTrigger("fireball");
                PlaySound(SoundEffect.Player_Voice_DoubleJump, variant: 2);
            }
            */

            animator.ResetTrigger("throw");
        }

        private void OnPlayBumpSound(EventPlayBumpSound e) {
            if (e.Entity != entity.EntityRef) {
                return;
            }

            if (Time.time - lastBumpSound < 0.25f) {
                return;
            }

            PlaySound(SoundEffect.World_Block_Bump);
            lastBumpSound = Time.time;
        }

        private void OnMarioPlayerTookDamage(EventMarioPlayerTookDamage e) {
            if (e.Entity != entity.EntityRef) {
                return;
            }

            PlaySound(SoundEffect.Player_Sound_Powerdown);
        }

        private void OnMarioPlayerRespawned(EventMarioPlayerRespawned e) {
            if (e.Entity != entity.EntityRef) {
                return;
            }

            doDeathUp = false;
        }

        private void OnMarioPlayerPreRespawned(EventMarioPlayerPreRespawned e) {
            if (e.Entity != entity.EntityRef) {
                return;
            }

            var marioTransform = e.Frame.Get<Transform2D>(e.Entity);
            GameObject respawn = SpawnParticle(respawnParticle, marioTransform.Position.ToUnityVector3());
            foreach (ParticleSystem particle in respawn.GetComponentsInChildren<ParticleSystem>()) {
                var main = particle.main;    
                main.startColor = GlowColor;
            }
            PlaySound(SoundEffect.Player_Sound_Respawn);
        }

        private void OnMarioPlayerDied(EventMarioPlayerDied e) {
            if (e.Entity != entity.EntityRef) {
                return;
            }

            var mario = e.Frame.Get<MarioPlayer>(e.Entity);
            PlaySound(e.Game.PlayerIsLocal(mario.PlayerRef) ? SoundEffect.Player_Sound_Death : SoundEffect.Player_Sound_DeathOthers);
            animator.Play("deadstart");
            doDeathUp = true;
        }

        private void OnMarioPlayerCollectedStar(EventMarioPlayerCollectedStar e) {
            if (e.Entity != entity.EntityRef) {
                return;
            }

            var mario = e.Frame.Get<MarioPlayer>(e.Entity);
            PlaySound(e.Game.PlayerIsLocal(mario.PlayerRef) ? SoundEffect.World_Star_Collect : SoundEffect.World_Star_CollectOthers);
            Instantiate(starCollectParticle, e.Position.ToUnityVector3(), Quaternion.identity);
        }

        private void OnMarioPlayerPropellerSpin(EventMarioPlayerPropellerSpin e) {
            if (e.Entity != entity.EntityRef) {
                return;
            }

            PlaySound(SoundEffect.Powerup_PropellerMushroom_Spin);
        }

        private void OnMarioPlayerUsedPropeller(EventMarioPlayerUsedPropeller e) {
            if (e.Entity != entity.EntityRef) {
                return;
            }

            PlaySound(SoundEffect.Powerup_PropellerMushroom_Start);
        }

        private void OnMarioPlayerShotProjectile(EventMarioPlayerShotProjectile e) {
            if (e.Entity != entity.EntityRef) {
                return;
            }

            animator.SetTrigger("fireball");
            sfx.PlayOneShot(e.Mario.CurrentPowerupState == PowerupState.IceFlower ? SoundEffect.Powerup_Iceball_Shoot : SoundEffect.Powerup_Fireball_Shoot);
        }

        private void OnMarioPlayerWalljumped(EventMarioPlayerWalljumped e) {
            if (e.Entity != entity.EntityRef) {
                return;
            }

            var hitbox = e.Frame.Get<PhysicsCollider2D>(e.Entity);

            Vector2 particleOffset = hitbox.Shape.Box.Extents.ToUnityVector2();
            Quaternion rot = Quaternion.identity;
            if (e.WasOnRightWall) {
                rot = Quaternion.Euler(0, 180, 0);
            } else {
                particleOffset.x *= -1;
            }
            SpawnParticle(Enums.PrefabParticle.Player_WallJump.GetGameObject(), e.Position.ToUnityVector2() + particleOffset, rot);


            PlaySound(SoundEffect.Player_Sound_WallJump);
            PlaySound(SoundEffect.Player_Voice_WallJump, variant: (byte) UnityEngine.Random.Range(1, 3));
            animator.SetTrigger("walljump");
        }

        private void OnMarioPlayerCollectedCoin(EventMarioPlayerCollectedCoin e) {
            if (e.Entity != entity.EntityRef) {
                return;
            }

            GameObject number = Instantiate(coinNumberParticle, e.CoinLocation.ToUnityVector3(), Quaternion.identity);
            number.GetComponentInChildren<NumberParticle>().Initialize(
                Utils.Utils.GetSymbolString(e.Coins.ToString(), Utils.Utils.numberSymbols),
                Utils.Utils.GetPlayerColor(e.Game, e.Mario.PlayerRef),
                e.ItemSpawned
            );

            PlaySound(SoundEffect.World_Coin_Collect);
            if (e.ItemSpawned) {
                PlaySound(SoundEffect.Player_Sound_PowerupReserveUse);
            }

            if (e.CoinFromBlock) {
                GameObject coin = Instantiate(coinFromBlockParticle, e.CoinLocation.ToUnityVector3(), Quaternion.identity);
                coin.GetComponentInChildren<Animator>().SetBool("down", e.Downwards);
                Destroy(coin, 1);
            }
        }

        private void OnMarioPlayerUsedReserveItem(EventMarioPlayerUsedReserveItem e) {
            if (e.Entity != entity.EntityRef) {
                return;
            }

            if (e.Success) {
                PlaySound(SoundEffect.Player_Sound_PowerupReserveUse);
            } else if (e.Game.PlayerIsLocal(e.Mario.PlayerRef)) {
                PlaySound(SoundEffect.UI_Error);
            }
        }

        private void OnMarioPlayerCollectedPowerup(EventMarioPlayerCollectedPowerup e) {
            if (e.Entity != entity.EntityRef) {
                return;
            }

            var powerup = e.Scriptable;
            var marioTransform = e.Frame.Get<Transform2D>(e.Entity);

            switch (e.Result) {
            case PowerupReserveResult.ReserveOldPowerup:
            case PowerupReserveResult.NoneButPlaySound: {
                // Just play the collect sound
                if (powerup.SoundPlaysEverywhere) {
                    PlaySoundEverywhere(powerup.SoundEffect);
                } else {
                    PlaySound(powerup.SoundEffect);
                }

                if (powerup.State == PowerupState.MegaMushroom) {
                    animator.Play("mega-scale");
                    SpawnParticle(Enums.PrefabParticle.Player_MegaMushroom.GetGameObject(), marioTransform.Position.ToUnityVector2());
                }
                break;
            }
            case PowerupReserveResult.ReserveNewPowerup: {
                // Reserve the new powerup
                PlaySound(SoundEffect.Player_Sound_PowerupReserveStore);
                break;
            }
            }
        }

        private void OnMarioPlayerCrouched(EventMarioPlayerCrouched e) {
            if (e.Entity != entity.EntityRef) {
                return;
            }

            PlaySound(e.Mario.CurrentPowerupState == PowerupState.BlueShell ? SoundEffect.Powerup_BlueShell_Enter : SoundEffect.Player_Sound_Crouch);
        }

        private void OnMarioPlayerGroundpoundStarted(EventMarioPlayerGroundpoundStarted e) {
            if (e.Entity != entity.EntityRef) {
                return;
            }

            PlaySound(SoundEffect.Player_Sound_GroundpoundStart);
        }

        private void OnMarioPlayerGroundpounded(EventMarioPlayerGroundpounded e) {
            if (e.Entity != entity.EntityRef) {
                return;
            }

            var mario = e.Mario;
            var marioTransform = e.Frame.Get<Transform2D>(e.Entity);

            // Groundpound
            if (mario.CurrentPowerupState == PowerupState.MegaMushroom) {
                PlaySound(SoundEffect.Powerup_MegaMushroom_Groundpound);

                SpawnParticle(Enums.PrefabParticle.Player_Groundpound.GetGameObject(), marioTransform.Position.ToUnityVector2());
                CameraAnimator.TriggerScreenshake(0.35f);

                /* TODO
                if (cameraController.IsControllingCamera) {
                    GlobalController.Instance.rumbleManager.RumbleForSeconds(0.8f, 0.3f, 0.5f,
                        RumbleManager.RumbleSetting.Low);
                }
                */

            } else {
                SoundEffect soundEffect = mario.CurrentPowerupState switch {
                    PowerupState.MiniMushroom => SoundEffect.Powerup_MiniMushroom_Groundpound,
                    _ => SoundEffect.Player_Sound_GroundpoundLanding,
                };
                PlaySound(soundEffect);

                SpawnParticle(Enums.PrefabParticle.Player_Groundpound.GetGameObject(), marioTransform.Position.ToUnityVector2());
                /* TODO

                if (cameraController.IsControllingCamera) {
                    GlobalController.Instance.rumbleManager.RumbleForSeconds(0.3f, 0.5f, 0.2f,
                        RumbleManager.RumbleSetting.Low);
                }
                */
            }
        }

        private void OnMarioPlayerJumped(EventMarioPlayerJumped e) {
            if (e.Entity != entity.EntityRef) {
                return;
            }

            var mario = e.Mario;

            if (mario.IsInWater) {
                // Paddle
                if (!e.WasBounce) {
                    PlaySound(SoundEffect.Player_Sound_Swim);
                    animator.SetTrigger("paddle");
                }
                return;
            }

            // Voice SFX
            switch (e.JumpState) {
            case JumpState.DoubleJump:
                PlaySound(SoundEffect.Player_Voice_DoubleJump, variant: (byte) UnityEngine.Random.Range(1, 3));
                break;
            case JumpState.TripleJump:
                PlaySound(SoundEffect.Player_Voice_TripleJump);
                break;
            }

            // Jump SFX
            if (e.WasBounce) {
                PlaySound(SoundEffect.Enemy_Generic_Stomp);

                /*
                if (cameraController.IsControllingCamera) {
                    GlobalController.Instance.rumbleManager.RumbleForSeconds(0.1f, 0.4f, 0.15f, RumbleManager.RumbleSetting.Low);
                }
                */
            } else {
                SoundEffect soundEffect = mario.CurrentPowerupState switch {
                    PowerupState.MiniMushroom => SoundEffect.Powerup_MiniMushroom_Jump,
                    PowerupState.MegaMushroom => SoundEffect.Powerup_MegaMushroom_Jump,
                    _ => SoundEffect.Player_Sound_Jump,
                };
                PlaySound(soundEffect);
            }
        }

        private void OnMarioPlayerEnteredPipe(EventMarioPlayerEnteredPipe e) {
            if (e.Entity != entity.EntityRef) {
                return;
            }

            sfx.PlayOneShot(SoundEffect.Player_Sound_Powerdown);
        }
    }
}
