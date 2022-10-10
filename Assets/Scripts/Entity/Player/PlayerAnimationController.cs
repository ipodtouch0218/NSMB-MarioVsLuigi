using System.Collections.Generic;
using UnityEngine;

using Fusion;
using NSMB.Extensions;
using NSMB.Utils;

public class PlayerAnimationController : NetworkBehaviour {

    //---Networked Variables

    //---Serialized Variables
    [SerializeField] private Avatar smallAvatar, largeAvatar;
    [SerializeField] private ParticleSystem dust, sparkles, drillParticle, giantParticle, fireParticle;
    [SerializeField] private GameObject models, smallModel, largeModel, largeShellExclude, blueShell, propellerHelmet, propeller;
    [SerializeField] private Material glowMaterial;
    [SerializeField] private Color primaryColor = Color.clear, secondaryColor = Color.clear;
    [SerializeField] [ColorUsage(true, false)] private Color? _glowColor = null;
    [SerializeField] private float blinkDuration = 0.1f, pipeDuration = 2f, deathUpTime = 0.6f, deathForce = 7f;

    private PlayerController controller;
    private Animator animator;
    private Rigidbody2D body;
    private readonly List<Renderer> renderers = new();
    private MaterialPropertyBlock materialBlock;

    public Color GlowColor {
        get {
            if (_glowColor == null)
                _glowColor = Utils.GetPlayerColor(Runner, controller.Object.InputAuthority);

            return _glowColor.Value;
        }
        set => _glowColor = value;
    }

    AudioSource drillParticleAudio;
    [SerializeField] private AudioClip normalDrill, propellerDrill;

    Enums.PlayerEyeState eyeState;
    float blinkTimer, pipeTimer, propellerVelocity;
    public bool deathUp, wasTurnaround, enableGlow;

    public void Awake() {
        controller = GetComponent<PlayerController>();
        animator = GetComponent<Animator>();
        body = GetComponent<Rigidbody2D>();
        drillParticleAudio = drillParticle.GetComponent<AudioSource>();
    }

    public override void Spawned() {

        DisableAllModels();

        if (!controller.Object.HasInputAuthority)
            GameManager.Instance.CreateNametag(controller);

        PlayerData data = Object.InputAuthority.GetPlayerData(Runner);

        if (GlobalController.Instance.skins[data?.SkinIndex ?? 0] is PlayerColorSet colorSet) {
            PlayerColors colors = colorSet.GetPlayerColors(controller.character);
            primaryColor = colors.overallsColor.linear;
            secondaryColor = colors.hatColor.linear;
        }
    }

    public override void FixedUpdateNetwork() {
        HandleAnimations();

        if (renderers.Count == 0) {
            renderers.AddRange(GetComponentsInChildren<MeshRenderer>(true));
            renderers.AddRange(GetComponentsInChildren<SkinnedMeshRenderer>(true));
        }
    }

    public void HandleAnimations() {
        bool gameover = GameManager.Instance.gameover;

        if (gameover)
            models.SetActive(true);

        float deathTimer = 3f - (controller.DeathTimer.RemainingTime(Runner) ?? 0f);

        if (Runner.IsForward) {
            float delta = Runner.DeltaTime;

            Vector3 targetEuler = models.transform.eulerAngles;
            bool instant = false, changeFacing = false;
            if (!gameover && !controller.IsFrozen) {
                if (controller.IsInKnockback) {
                    targetEuler = new Vector3(0, controller.FacingRight ? 110 : 250, 0);
                    instant = true;
                } else if (controller.IsDead) {
                    if (animator.GetBool("firedeath") && deathTimer > deathUpTime) {
                        targetEuler = new Vector3(-15, controller.FacingRight ? 110 : 250, 0);
                    } else {
                        targetEuler = new Vector3(0, 180, 0);
                    }
                    instant = true;
                } else if (animator.GetBool("pipe")) {
                    targetEuler = new Vector3(0, 180, 0);
                    instant = true;
                } else if (animator.GetBool("inShell") && (!controller.onSpinner || Mathf.Abs(body.velocity.x) > 0.3f)) {
                    targetEuler += Mathf.Abs(body.velocity.x) / controller.RunningMaxSpeed * delta * new Vector3(0, 1800 * (controller.FacingRight ? -1 : 1));
                    instant = true;
                } else if (wasTurnaround || controller.skidding || controller.turnaround || animator.GetCurrentAnimatorStateInfo(0).IsName("turnaround")) {
                    if (controller.FacingRight ^ (animator.GetCurrentAnimatorStateInfo(0).IsName("turnaround") || controller.skidding)) {
                        targetEuler = new Vector3(0, 250, 0);
                    } else {
                        targetEuler = new Vector3(0, 110, 0);
                    }
                    instant = true;
                } else {
                    if (controller.onSpinner && controller.IsOnGround && Mathf.Abs(body.velocity.x) < 0.3f && !controller.HeldEntity) {
                        targetEuler += new Vector3(0, -1800, 0) * delta;
                        instant = true;
                        changeFacing = true;
                    } else if (controller.IsSpinnerFlying || controller.IsPropellerFlying) {
                        targetEuler += new Vector3(0, -1200 - ((controller.PropellerLaunchTimer.RemainingTime(Runner) ?? 0f) * 2000) - (controller.IsDrilling ? 800 : 0) + (controller.IsPropellerFlying && controller.PropellerSpinTimer.Expired(Runner) && body.velocity.y < 0 ? 800 : 0), 0) * delta;
                        instant = true;
                    } else if (controller.WallSlideLeft || controller.WallSlideRight) {
                        targetEuler = new Vector3(0, controller.WallSlideRight ? 110 : 250, 0);
                    } else {
                        targetEuler = new Vector3(0, controller.FacingRight ? 110 : 250, 0);
                    }
                }
                propellerVelocity = Mathf.Clamp(propellerVelocity + (1800 * ((controller.IsSpinnerFlying || controller.IsPropellerFlying || controller.usedPropellerThisJump) ? -1 : 1) * delta), -2500, -300);
                propeller.transform.Rotate(Vector3.forward, propellerVelocity * delta);

                if (instant || wasTurnaround) {
                    models.transform.rotation = Quaternion.Euler(targetEuler);
                } else {
                    float maxRotation = 2000f * delta;
                    float x = models.transform.eulerAngles.x, y = models.transform.eulerAngles.y, z = models.transform.eulerAngles.z;
                    x += Mathf.Clamp(targetEuler.x - x, -maxRotation, maxRotation);
                    y += Mathf.Clamp(targetEuler.y - y, -maxRotation, maxRotation);
                    z += Mathf.Clamp(targetEuler.z - z, -maxRotation, maxRotation);
                    models.transform.rotation = Quaternion.Euler(x, y, z);
                }

                if (changeFacing)
                    controller.FacingRight = models.transform.eulerAngles.y < 180;

                wasTurnaround = animator.GetCurrentAnimatorStateInfo(0).IsName("turnaround");
            }

            //Particles
            SetParticleEmission(dust, !gameover && (controller.WallSlideLeft || controller.WallSlideRight || (controller.IsOnGround && (controller.skidding || (controller.IsCrouching && Mathf.Abs(body.velocity.x) > 1))) || (controller.IsSliding && Mathf.Abs(body.velocity.x) > 0.2 && controller.IsOnGround)) && !controller.pipeEntering);
            SetParticleEmission(drillParticle, !gameover && controller.IsDrilling);
            if (controller.IsDrilling)
                drillParticleAudio.clip = (controller.State == Enums.PowerupState.PropellerMushroom ? propellerDrill : normalDrill);
            SetParticleEmission(sparkles, !gameover && controller.IsStarmanInvincible);
            SetParticleEmission(giantParticle, !gameover && controller.State == Enums.PowerupState.MegaMushroom && controller.GiantStartTimer.ExpiredOrNotRunning(Runner));
            SetParticleEmission(fireParticle, !gameover && animator.GetBool("firedeath") && controller.IsDead && deathTimer > deathUpTime);

            //Blinking
            if (controller.IsDead) {
                eyeState = Enums.PlayerEyeState.Death;
            } else {
                if ((blinkTimer -= Time.fixedDeltaTime) < 0)
                    blinkTimer = 3f + (Random.value * 6f);
                if (blinkTimer < blinkDuration) {
                    eyeState = Enums.PlayerEyeState.HalfBlink;
                } else if (blinkTimer < blinkDuration * 2f) {
                    eyeState = Enums.PlayerEyeState.FullBlink;
                } else if (blinkTimer < blinkDuration * 3f) {
                    eyeState = Enums.PlayerEyeState.HalfBlink;
                } else {
                    eyeState = Enums.PlayerEyeState.Normal;
                }
            }
        }

        if (controller.cameraController.IsControllingCamera)
            HorizontalCamera.OFFSET_TARGET = (controller.IsSpinnerFlying || controller.IsPropellerFlying) ? 0.5f : 0f;

        if (controller.IsCrouching || controller.IsSliding || controller.skidding) {
            dust.transform.localPosition = Vector2.zero;
        } else if (controller.WallSlideLeft || controller.WallSlideRight) {
            dust.transform.localPosition = new Vector2(controller.MainHitbox.size.x * (3f / 4f) * (controller.WallSlideLeft ? -1 : 1), controller.MainHitbox.size.y * (3f / 4f));
        }
    }
    private void SetParticleEmission(ParticleSystem particle, bool value) {
        if (value) {
            if (particle.isStopped)
                particle.Play();
        } else {
            if (particle.isPlaying)
                particle.Stop();
        }
    }

    public void UpdateAnimatorStates() {

        bool right = controller.currentInputs.buttons.IsSet(PlayerControls.Right);
        bool left = controller.currentInputs.buttons.IsSet(PlayerControls.Left);

        animator.SetBool("onLeft", controller.WallSlideLeft);
        animator.SetBool("onRight", controller.WallSlideRight);
        animator.SetBool("onGround", controller.IsOnGround);
        animator.SetBool("invincible", controller.IsStarmanInvincible);
        animator.SetBool("skidding", controller.skidding);
        animator.SetBool("propeller", controller.IsPropellerFlying);
        animator.SetBool("propellerSpin", !controller.PropellerSpinTimer.ExpiredOrNotRunning(Runner));
        animator.SetBool("crouching", controller.IsCrouching);
        animator.SetBool("groundpound", controller.IsGroundpounding);
        animator.SetBool("sliding", controller.IsSliding);
        animator.SetBool("knockback", controller.IsInKnockback);
        animator.SetBool("facingRight", (left ^ right) ? right : controller.FacingRight);
        animator.SetBool("flying", controller.IsSpinnerFlying);
        animator.SetBool("drill", controller.IsDrilling);

        //Animation
        animator.SetBool("turnaround", controller.turnaround);
        float animatedVelocity = Mathf.Abs(body.velocity.x) + Mathf.Abs(body.velocity.y * Mathf.Sin(controller.floorAngle * Mathf.Deg2Rad)) * (Mathf.Sign(controller.floorAngle) == Mathf.Sign(body.velocity.x) ? 0 : 1);
        if (controller.stuckInBlock) {
            animatedVelocity = 0;
        } else if (controller.IsPropellerFlying) {
            animatedVelocity = 2.5f;
        } else if (controller.State == Enums.PowerupState.MegaMushroom && (controller.currentInputs.buttons.IsSet(PlayerControls.Left) || controller.currentInputs.buttons.IsSet(PlayerControls.Right))) {
            animatedVelocity = 4.5f;
        } else if (left ^ right && !controller.hitRight && !controller.hitLeft) {
            animatedVelocity = Mathf.Max(2f, animatedVelocity);
        } else if (controller.onIce) {
            animatedVelocity = 0;
        }
        animator.SetFloat("velocityX", animatedVelocity);
        animator.SetFloat("velocityY", body.velocity.y);
        animator.SetBool("doublejump", controller.IsDoubleJump);
        animator.SetBool("triplejump", controller.IsTripleJump);
        animator.SetBool("holding", controller.HeldEntity != null);
        animator.SetBool("head carry", controller.HeldEntity != null && controller.HeldEntity is FrozenCube);
        animator.SetBool("pipe", controller.pipeEntering != null);
        animator.SetBool("blueshell", controller.State == Enums.PowerupState.BlueShell);
        animator.SetBool("mini", controller.State == Enums.PowerupState.MiniMushroom);
        animator.SetBool("mega", controller.State == Enums.PowerupState.MegaMushroom);
        animator.SetBool("inShell", controller.IsInShell || (controller.State == Enums.PowerupState.BlueShell && (controller.IsCrouching || controller.IsGroundpounding) && (controller.GroundpoundStartTimer.RemainingTime(Runner) ?? 0f) <= 0.15f));
    }
    public void HandleMiscStates() {
        if (!controller.GiantEndTimer.ExpiredOrNotRunning(Runner)) {
            transform.localScale = Vector3.one + (Vector3.one * (Mathf.Min(1, (controller.GiantEndTimer.RemainingTime(Runner) ?? 0f) / (controller.giantStartTime / 2f)) * 2.6f));
        } else {
            transform.localScale = controller.State switch {
                Enums.PowerupState.MiniMushroom => Vector3.one / 2,
                Enums.PowerupState.MegaMushroom => Vector3.one + (Vector3.one * (Mathf.Min(1, 1 - ((controller.GiantStartTimer.RemainingTime(Runner) ?? 0f) / controller.giantStartTime)) * 2.6f)),
                _ => Vector3.one,
            };
        }

        //Shader effects
        if (materialBlock == null)
            materialBlock = new();

        materialBlock.SetFloat("RainbowEnabled", controller.IsStarmanInvincible ? 1.1f : 0f);
        int ps = controller.State switch {
            Enums.PowerupState.FireFlower => 1,
            Enums.PowerupState.PropellerMushroom => 2,
            Enums.PowerupState.IceFlower => 3,
            _ => 0
        };
        materialBlock.SetFloat("PowerupState", ps);
        materialBlock.SetFloat("EyeState", (int) eyeState);
        materialBlock.SetFloat("ModelScale", transform.lossyScale.x);
        if (enableGlow)
            materialBlock.SetColor("GlowColor", GlowColor);

        //Customizeable player color
        materialBlock.SetVector("OverallsColor", primaryColor);
        materialBlock.SetVector("ShirtColor", secondaryColor);

        Vector3 giantMultiply = Vector3.one;
        float giantTimeRemaining = controller.GiantTimer.RemainingTime(Runner) ?? 0f;
        if (controller.State == Enums.PowerupState.MegaMushroom && controller.GiantTimer.IsRunning && giantTimeRemaining < 4) {
            float v = ((Mathf.Sin(giantTimeRemaining * 20f) + 1f) / 2f * 0.9f) + 0.1f;
            giantMultiply = new Vector3(v, 1, v);
        }
        materialBlock.SetVector("MultiplyColor", giantMultiply);

        foreach (Renderer r in renderers)
            r.SetPropertyBlock(materialBlock);

        //hit flash
        float remainingDamageInvincibility = controller.DamageInvincibilityTimer.RemainingTime(Runner) ?? 0f;
        models.SetActive(!controller.IsRespawning && (GameManager.Instance.gameover || controller.IsDead || !(remainingDamageInvincibility > 0 && remainingDamageInvincibility * (remainingDamageInvincibility <= 0.75f ? 5 : 2) % (blinkDuration * 2f) < blinkDuration)));

        //Model changing
        bool large = controller.State >= Enums.PowerupState.Mushroom;

        largeModel.SetActive(large);
        smallModel.SetActive(!large);
        blueShell.SetActive(controller.State == Enums.PowerupState.BlueShell);

        largeShellExclude.SetActive(!animator.GetCurrentAnimatorStateInfo(0).IsName("in-shell"));
        propellerHelmet.SetActive(controller.State == Enums.PowerupState.PropellerMushroom);
        animator.avatar = large ? largeAvatar : smallAvatar;
        animator.runtimeAnimatorController = large ? controller.character.largeOverrides : controller.character.smallOverrides;

        HandleDeathAnimation();
        HandlePipeAnimation();

        transform.position = new(transform.position.x, transform.position.y, animator.GetBool("pipe") ? 1 : -4);
    }
    private void HandleDeathAnimation() {
        if (!controller.IsDead || controller.IsRespawning)
            return;

        float deathTimer = 3f - (controller.DeathTimer.RemainingTime(Runner) ?? 0f);

        if (deathTimer < deathUpTime) {
            deathUp = false;
            body.gravityScale = 0;
            body.velocity = Vector2.zero;
            if (deathTimer < (deathUpTime * 0.5f)) {
                animator.Play("deadstart");
                animator.ResetTrigger("respawn");
            }
        } else {
            if (!deathUp && body.position.y > GameManager.Instance.GetLevelMinY()) {
                body.velocity = new Vector2(0, deathForce);
                deathUp = true;
                if (animator.GetBool("firedeath") && Runner.IsForward) {
                    controller.PlaySound(Enums.Sounds.Player_Voice_LavaDeath);
                    controller.PlaySound(Enums.Sounds.Player_Sound_LavaHiss);
                }
                animator.SetTrigger("deathup");
            }
            body.gravityScale = 1.2f;
            body.velocity = new Vector2(0, Mathf.Max(-deathForce, body.velocity.y));
        }
        if (controller.Object.HasInputAuthority && deathTimer + Runner.DeltaTime > (3 - 0.43f) && deathTimer < (3 - 0.43f))
            controller.fadeOut.FadeOutAndIn(0.33f, .1f);

        if (body.position.y < GameManager.Instance.GetLevelMinY() - transform.lossyScale.y) {
            //models.SetActive(false);
            body.velocity = Vector2.zero;
            body.gravityScale = 0;
        }
    }

    void HandlePipeAnimation() {
        if (!controller.pipeEntering) {
            pipeTimer = 0;
            return;
        }

        controller.UpdateHitbox();

        PipeManager pe = controller.pipeEntering;

        body.isKinematic = true;
        body.velocity = controller.pipeDirection;

        if (pipeTimer < pipeDuration / 2f && pipeTimer + Time.fixedDeltaTime >= pipeDuration / 2f) {
            //tp to other pipe
            if (pe.otherPipe.bottom == pe.bottom)
                controller.pipeDirection *= -1;

            Vector2 offset = controller.pipeDirection * (pipeDuration / 2f);
            if (pe.otherPipe.bottom) {
                float size = controller.MainHitbox.size.y * transform.localScale.y;
                offset.y += size;
            }
            Vector3 tpPos = new Vector3(pe.otherPipe.transform.position.x, pe.otherPipe.transform.position.y, 1) - (Vector3) offset;
            transform.position = body.position = tpPos;
            controller.PlaySound(Enums.Sounds.Player_Sound_Powerdown);
            controller.cameraController.Recenter(tpPos);
        }
        if (pipeTimer >= pipeDuration) {
            controller.pipeEntering = null;
            body.isKinematic = false;
            controller.IsOnGround = false;
            controller.properJump = false;
            controller.koyoteTime = 1;
            controller.IsCrouching = false;
            controller.alreadyGroundpounded = true;
            controller.pipeTimer = 0.25f;
            body.velocity = Vector2.zero;
        }
        pipeTimer += Time.fixedDeltaTime;
    }

    public void DisableAllModels() {
        smallModel.SetActive(false);
        largeModel.SetActive(false);
        blueShell.SetActive(false);
        propellerHelmet.SetActive(false);
        animator.avatar = smallAvatar;
    }
}