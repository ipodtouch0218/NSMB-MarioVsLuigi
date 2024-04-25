using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using Fusion;
using NSMB.Extensions;
using NSMB.Game;

namespace NSMB.Entities.Player {

    //[OrderAfter(typeof(PlayerController))]
    public class PlayerAnimationController : NetworkBehaviour {

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
        public bool deathUp, wasTurnaround, enableGlow;
        public GameObject models;

        //---Serialized Variables
        [SerializeField] private Avatar smallAvatar, largeAvatar;
        [SerializeField] private ParticleSystem dust, sparkles, drillParticle, giantParticle, fireParticle, bubblesParticle;
        [SerializeField] private GameObject smallModel, largeModel, largeShellExclude, blueShell, propellerHelmet, propeller;
        [SerializeField] public float deathUpTime = 0.6f, deathForce = 7f;
        [SerializeField] private AudioClip normalDrill, propellerDrill;
        [SerializeField] private LoopingSoundPlayer dustPlayer, drillPlayer;
        [SerializeField] private LoopingSoundData wallSlideData, shellSlideData, spinnerDrillData, propellerDrillData;

        //---Components
        private readonly List<Renderer> renderers = new();
        private PlayerController controller;
        private Animator animator;
        private MaterialPropertyBlock materialBlock;

        //---Properties
        public Color GlowColor { get; private set; }
        public EntityMover body => controller.body;
        private bool IsSpinningOnSpinner => controller.OnSpinner && controller.IsOnGround && controller.FireballDelayTimer.ExpiredOrNotRunning(Runner) && Mathf.Abs(body.Velocity.x) < 0.3f && !controller.HeldEntity;

        //---Private Variables
        private Enums.PlayerEyeState eyeState;
        private float propellerVelocity;
        private Vector3 modelRotationTarget;
        private bool modelRotateInstantly;
        private Coroutine blinkRoutine;
        private PlayerColors skin;
        private TrackIcon icon;

        public void OnEnable() {
            GameManager.OnAllPlayersLoaded += OnAllPlayersLoaded;
        }

        public void OnDisable() {
            GameManager.OnAllPlayersLoaded -= OnAllPlayersLoaded;
        }

        public void Awake() {
            controller = GetComponent<PlayerController>();
            animator = GetComponent<Animator>();
        }

        public override void Spawned() {

            DisableAllModels();

            PlayerData data = controller.Data;
            if (ScriptableManager.Instance.skins[data ? data.SkinIndex : 0] is PlayerColorSet colorSet) {
                skin = colorSet.GetPlayerColors(controller.character);
            }

            renderers.AddRange(GetComponentsInChildren<MeshRenderer>(true));
            renderers.AddRange(GetComponentsInChildren<SkinnedMeshRenderer>(true));

            modelRotationTarget = models.transform.rotation.eulerAngles;

            if (blinkRoutine == null) {
                blinkRoutine = StartCoroutine(BlinkRoutine());
            }
        }

        public override void Despawned(NetworkRunner runner, bool hasState) {
            if (icon) {
                Destroy(icon.gameObject);
            }
        }

        public override void Render() {
            if (GameManager.Instance.GameStartTimer.IsRunning) {
                DisableAllModels();
                return;
            }

            UpdateAnimatorVariables();
            HandleAnimations();
            SetFacingDirection();
            InterpolateFacingDirection();
            HandleMiscStates();
        }

        public override void FixedUpdateNetwork() {
            if (IsSpinningOnSpinner) {
                controller.FacingRight = models.transform.eulerAngles.y < 180;
            }
        }

        public void HandleAnimations() {
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

            float deathTimer = 3f - (controller.PreRespawnTimer.RemainingTime(Runner) ?? 0f);

            // Particles
            SetParticleEmission(drillParticle, !controller.IsDead && controller.IsDrilling);
            SetParticleEmission(sparkles, !controller.IsDead && controller.IsStarmanInvincible);
            SetParticleEmission(dust, !controller.IsDead && (controller.WallSlideLeft || controller.WallSlideRight || (controller.IsOnGround && (controller.IsSkidding || (controller.IsCrouching && body.Velocity.sqrMagnitude > 0.25f))) || (((controller.IsSliding && body.Velocity.sqrMagnitude > 0.25f) || controller.IsInShell) && controller.IsOnGround)) && !controller.CurrentPipe);
            SetParticleEmission(giantParticle, !controller.IsDead && controller.State == Enums.PowerupState.MegaMushroom && controller.MegaStartTimer.ExpiredOrNotRunning(Runner));
            SetParticleEmission(fireParticle, !controller.IsRespawning && controller.FireDeath && controller.IsDead && deathTimer > deathUpTime);
            SetParticleEmission(bubblesParticle, controller.InWater);

            if (controller.IsCrouching || controller.IsSliding || controller.IsSkidding) {
                dust.transform.localPosition = Vector2.zero;
            } else if (controller.WallSlideLeft || controller.WallSlideRight) {
                dust.transform.localPosition = new Vector2(controller.MainHitbox.size.x * 0.75f * (controller.WallSlideLeft ? -1 : 1), controller.MainHitbox.size.y * 0.75f);
            }

            dustPlayer.SetSoundData((controller.IsInShell || controller.IsSliding || controller.IsCrouchedInShell) ? shellSlideData : wallSlideData);
            drillPlayer.SetSoundData(controller.IsPropellerFlying ? propellerDrillData : spinnerDrillData);

            bubblesParticle.transform.localPosition = new(bubblesParticle.transform.localPosition.x, controller.WorldHitboxSize.y);

            if (controller.cameraController.IsControllingCamera) {
                HorizontalCamera.SizeIncreaseTarget = (controller.IsSpinnerFlying || controller.IsPropellerFlying) ? 0.5f : 0f;
            }
        }

        private IEnumerator BlinkRoutine() {
            while (true) {
                yield return new WaitForSeconds(3f + (Random.value * 6f));
                eyeState = Enums.PlayerEyeState.HalfBlink;
                yield return BlinkDelay;
                eyeState = Enums.PlayerEyeState.FullBlink;
                yield return BlinkDelay;
                eyeState = Enums.PlayerEyeState.HalfBlink;
                yield return BlinkDelay;
                eyeState = Enums.PlayerEyeState.Normal;
            }
        }

        private void SetFacingDirection() {
            //TODO: refactor
            if (GameManager.Instance.GameEnded) {
                if (controller.IsDead) {
                    modelRotationTarget.Set(0, 180, 0);
                    modelRotateInstantly = true;
                }
                return;
            }

            //rotChangeTarget = models.transform.rotation.eulerAngles;
            float delta = Time.deltaTime;

            modelRotateInstantly = false;

            if (controller.IsInKnockback || controller.IsFrozen) {
                bool right = controller.FacingRight;
                if (controller.IsInKnockback && (controller.InWater || controller.IsWeakKnockback)) {
                    right = controller.KnockbackWasOriginallyFacingRight;
                }
                modelRotationTarget.Set(0, right ? 110 : 250, 0);
                modelRotateInstantly = true;

            } else if (controller.IsDead) {
                if (controller.FireDeath && !controller.DeathAnimationTimer.IsRunning) {
                    modelRotationTarget.Set(-15, controller.FacingRight ? 110 : 250, 0);
                } else {
                    modelRotationTarget.Set(0, 180, 0);
                }
                modelRotateInstantly = true;

            } else if (animator.GetBool(ParamInShell) && (!controller.OnSpinner || Mathf.Abs(body.Velocity.x) > 0.3f)) {
                modelRotationTarget += Mathf.Abs(body.Velocity.x) / controller.RunningMaxSpeed * delta * new Vector3(0, 1400 * (controller.FacingRight ? -1 : 1));
                modelRotateInstantly = true;

            } else if (wasTurnaround || controller.IsSkidding || controller.IsTurnaround || animator.GetCurrentAnimatorStateInfo(0).IsName("turnaround")) {
                bool flip = controller.FacingRight ^ (animator.GetCurrentAnimatorStateInfo(0).IsName("turnaround") || controller.IsSkidding);
                modelRotationTarget.Set(0, flip ? 250 : 110, 0);
                modelRotateInstantly = true;

            } else {
                if (IsSpinningOnSpinner && !animator.GetCurrentAnimatorStateInfo(0).IsName("fireball")) {
                    modelRotationTarget += controller.OnSpinner.spinSpeed * delta * Vector3.up;
                    modelRotateInstantly = true;
                } else if (controller.IsSpinnerFlying || controller.IsPropellerFlying) {
                    modelRotationTarget += new Vector3(0, -1200 - ((controller.PropellerLaunchTimer.RemainingTime(Runner) ?? 0f) * 1400) - (controller.IsDrilling ? 900 : 0) + (controller.IsPropellerFlying && controller.PropellerSpinTimer.ExpiredOrNotRunning(Runner) && body.Velocity.y < 0 ? 700 : 0), 0) * delta;
                    modelRotateInstantly = true;
                } else if (controller.WallSlideLeft || controller.WallSlideRight) {
                    modelRotationTarget.Set(0, controller.WallSlideRight ? 110 : 250, 0);
                } else {
                    modelRotationTarget.Set(0, controller.FacingRight ? 110 : 250, 0);
                }
            }

            propellerVelocity = Mathf.Clamp(propellerVelocity + (1200 * ((controller.IsSpinnerFlying || controller.IsPropellerFlying || controller.UsedPropellerThisJump) ? -1 : 1) * delta), -2500, -300);

            wasTurnaround = animator.GetCurrentAnimatorStateInfo(0).IsName("turnaround");
        }

        private void InterpolateFacingDirection() {

            if (modelRotateInstantly || wasTurnaround) {
                models.transform.rotation = Quaternion.Euler(modelRotationTarget);
            } else if (!GameManager.Instance.GameEnded) {
                float maxRotation = 2000f * Time.deltaTime;
                float x = models.transform.eulerAngles.x, y = models.transform.eulerAngles.y, z = models.transform.eulerAngles.z;
                x += Mathf.Clamp(modelRotationTarget.x - x, -maxRotation, maxRotation);
                y += Mathf.Clamp(modelRotationTarget.y - y, -maxRotation, maxRotation);
                z += Mathf.Clamp(modelRotationTarget.z - z, -maxRotation, maxRotation);
                models.transform.rotation = Quaternion.Euler(x, y, z);
            }

            if (GameManager.Instance.GameEnded) {
                return;
            }

            if (controller.State == Enums.PowerupState.PropellerMushroom && !controller.IsFrozen) {
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

        public void UpdateAnimatorVariables() {

            bool right = controller.PreviousInputs.Buttons.IsSet(PlayerControls.Right);
            bool left = controller.PreviousInputs.Buttons.IsSet(PlayerControls.Left);

            animator.SetBool(ParamOnLeft, controller.WallSlideLeft);
            animator.SetBool(ParamOnRight, controller.WallSlideRight);
            animator.SetBool(ParamOnGround, controller.IsOnGround || controller.IsStuckInBlock || (Runner.SimulationTime <= controller.CoyoteTime - 0.05f));
            animator.SetBool(ParamInvincible, controller.IsStarmanInvincible);
            animator.SetBool(ParamSkidding, controller.IsSkidding);
            animator.SetBool(ParamPropeller, controller.IsPropellerFlying);
            animator.SetBool(ParamPropellerSpin, controller.PropellerSpinTimer.IsActive(Runner));
            animator.SetBool(ParamPropellerStart, controller.PropellerLaunchTimer.IsActive(Runner));
            animator.SetBool(ParamCrouching, controller.IsCrouching);
            animator.SetBool(ParamGroundpound, controller.IsGroundpounding);
            animator.SetBool(ParamSliding, controller.IsSliding);
            animator.SetBool(ParamKnockback, controller.IsInKnockback);
            animator.SetBool(ParamFacingRight, (left ^ right) ? right : controller.FacingRight);
            animator.SetBool(ParamFlying, controller.IsSpinnerFlying);
            animator.SetBool(ParamDrill, controller.IsDrilling);
            animator.SetBool(ParamDoubleJump, controller.ProperJump && controller.JumpState == PlayerController.PlayerJumpState.DoubleJump);
            animator.SetBool(ParamTripleJump, controller.ProperJump && controller.JumpState == PlayerController.PlayerJumpState.TripleJump);
            animator.SetBool(ParamHolding, controller.HeldEntity);
            animator.SetBool(ParamHeadCarry, controller.HeldEntity && controller.HeldEntity is FrozenCube);
            animator.SetBool(ParamCarryStart, controller.HeldEntity && controller.HeldEntity is FrozenCube && (Runner.SimulationTime - controller.HoldStartTime) < controller.pickupTime);
            animator.SetBool(ParamPipe, controller.CurrentPipe);
            animator.SetBool(ParamBlueShell, controller.State == Enums.PowerupState.BlueShell);
            animator.SetBool(ParamMini, controller.State == Enums.PowerupState.MiniMushroom);
            animator.SetBool(ParamMega, controller.State == Enums.PowerupState.MegaMushroom);
            animator.SetBool(ParamInShell, controller.IsInShell || (controller.State == Enums.PowerupState.BlueShell && (controller.IsCrouching || controller.IsGroundpounding || controller.IsSliding) && (controller.GroundpoundStartTimer.RemainingTime(Runner) ?? 0f) <= 0.15f));
            animator.SetBool(ParamTurnaround, controller.IsTurnaround);
            animator.SetBool(ParamSwimming, controller.InWater && !controller.IsGroundpounding && !controller.IsDrilling && !controller.IsFrozen);
            animator.SetBool(ParamAHeld, controller.PreviousInputs.Buttons.IsSet(PlayerControls.Jump));
            animator.SetBool(ParamFireballKnockback, controller.IsWeakKnockback);
            animator.SetBool(ParamKnockforwards, controller.IsForwardsKnockback);

            float animatedVelocity = controller.IsOnGround ? Mathf.Abs(body.Velocity.x) : body.Velocity.magnitude;
            if (controller.IsStuckInBlock) {
                animatedVelocity = 0;
            } else if (controller.IsPropellerFlying) {
                animatedVelocity = 2f;
            } else if (controller.State == Enums.PowerupState.MegaMushroom && (left || right)) {
                animatedVelocity = 4.5f;
            } else if (left ^ right && !controller.HitRight && !controller.HitLeft) {
                animatedVelocity = Mathf.Max(controller.OnIce ? 2.7f : 2f, animatedVelocity);
            } else if (controller.OnIce) {
                animatedVelocity = 0;
            }
            if (animatedVelocity < 0.01f) {
                animatedVelocity = 0;
            }

            animator.SetFloat(ParamVelocityX, animatedVelocity);
            animator.SetFloat(ParamVelocityY, body.Velocity.y);
        }

        private void HandleMiscStates() {
            if (controller.MegaStartTimer.IsActive(Runner)) {
                if (animator.GetCurrentAnimatorClipInfo(0).Length <= 0 ||
                    animator.GetCurrentAnimatorClipInfo(0)[0].clip.name != "mega-scale") {
                    animator.Play("mega-scale");
                }
            }

            // Scale
            models.transform.SetLossyScale(controller.CalculateScale(true));

            // Shader effects
            TryCreateMaterialBlock();
            materialBlock.SetFloat(ParamRainbowEnabled, controller.IsStarmanInvincible ? 1f : 0f);
            int ps = controller.State switch {
                Enums.PowerupState.FireFlower => 1,
                Enums.PowerupState.PropellerMushroom => 2,
                Enums.PowerupState.IceFlower => 3,
                _ => 0
            };
            materialBlock.SetFloat(ParamPowerupState, ps);
            materialBlock.SetFloat(ParamEyeState, (int) (controller.IsDead ? Enums.PlayerEyeState.Death : eyeState));
            materialBlock.SetFloat(ParamModelScale, transform.lossyScale.x);

            Vector3 giantMultiply = Vector3.one;
            float giantTimeRemaining = controller.MegaTimer.RemainingTime(Runner) ?? 0f;
            if (controller.State == Enums.PowerupState.MegaMushroom && controller.MegaTimer.IsRunning &&
                giantTimeRemaining < 4) {
                float v = ((Mathf.Sin(giantTimeRemaining * 20f) + 1f) * 0.45f) + 0.1f;
                giantMultiply = new Vector3(v, 1, v);
            }

            materialBlock.SetVector(ParamMultiplyColor, giantMultiply);

            foreach (Renderer r in renderers) {
                r.SetPropertyBlock(materialBlock);
            }

            // Hit flash
            float remainingDamageInvincibility = controller.DamageInvincibilityTimer.RemainingRenderTime(Runner) ?? 0f;
            models.SetActive(!controller.IsRespawning && (GameManager.Instance.GameEnded || controller.IsDead ||
                                                          !(remainingDamageInvincibility > 0 &&
                                                            remainingDamageInvincibility *
                                                            (remainingDamageInvincibility <= 0.75f ? 5 : 2) % 0.2f <
                                                            0.1f)));

            // Model changing
            bool large = controller.State >= Enums.PowerupState.Mushroom;

            largeModel.SetActive(large);
            smallModel.SetActive(!large);
            blueShell.SetActive(controller.State == Enums.PowerupState.BlueShell);

            largeShellExclude.SetActive(!animator.GetCurrentAnimatorStateInfo(0).IsName("in-shell"));
            propellerHelmet.SetActive(controller.State == Enums.PowerupState.PropellerMushroom);
            animator.avatar = large ? largeAvatar : smallAvatar;
            animator.runtimeAnimatorController =
                large ? controller.character.largeOverrides : controller.character.smallOverrides;


            float newZ = -4;
            if (controller.IsDead) {
                newZ = -6;
            } else if (controller.IsFrozen) {
                newZ = -2;
            } else if (controller.CurrentPipe) {
                newZ = 1;
            }

            transform.position = new(transform.position.x, transform.position.y, newZ);
        }

        public void DisableAllModels() {
            smallModel.SetActive(false);
            largeModel.SetActive(false);
            blueShell.SetActive(false);
            propellerHelmet.SetActive(false);
            animator.avatar = smallAvatar;
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

        private void OnAllPlayersLoaded() {
            enableGlow = SessionData.Instance.Teams || !Object.HasControlAuthority();
            GlowColor = Utils.Utils.GetPlayerColor(controller.Data.Owner);

            if (!Object.HasControlAuthority()) {
                GameManager.Instance.CreateNametag(controller);
            }

            icon = UIUpdater.Instance.CreateTrackIcon(controller);
            TryCreateMaterialBlock();
        }
    }
}
