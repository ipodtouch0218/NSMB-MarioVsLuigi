using System.Collections.Generic;
using UnityEngine;

using Photon.Pun;
using NSMB.Utils;

public class PlayerAnimationController : MonoBehaviourPun {

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
    private BoxCollider2D mainHitbox;
    private List<Renderer> renderers = new();
    private MaterialPropertyBlock materialBlock;

    public Color GlowColor {
        get {
            if (_glowColor == null)
                _glowColor = Utils.GetPlayerColor(photonView.Owner);

            return _glowColor ?? Color.white;
        }
        set => _glowColor = value;
    }

    AudioSource drillParticleAudio;
    [SerializeField] AudioClip normalDrill, propellerDrill;

    Enums.PlayerEyeState eyeState;
    float blinkTimer, pipeTimer, deathTimer, propellerVelocity;
    public bool deathUp, wasTurnaround, enableGlow;

    public void Start() {
        controller = GetComponent<PlayerController>();
        animator = GetComponent<Animator>();
        body = GetComponent<Rigidbody2D>();
        mainHitbox = GetComponent<BoxCollider2D>();
        drillParticleAudio = drillParticle.GetComponent<AudioSource>();

        DisableAllModels();

        if (photonView) {
            enableGlow = !photonView.IsMine;
            if (!photonView.IsMine)
                GameManager.Instance.CreateNametag(controller);

            PlayerColorSet colorSet = GlobalController.Instance.skins[(int) photonView.Owner.CustomProperties[Enums.NetPlayerProperties.PlayerColor]];
            PlayerColors colors = colorSet.GetPlayerColors(controller.character);
            primaryColor = colors.overallsColor.linear;
            secondaryColor = colors.hatColor.linear;
        }
    }

    public void Update() {
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

        Vector3 targetEuler = models.transform.eulerAngles;
        bool instant = false, changeFacing = false;
        if (!gameover && !controller.Frozen) {
            if (controller.knockback) {
                targetEuler = new Vector3(0, controller.facingRight ? 110 : 250, 0);
                instant = true;
            } else if (controller.dead) {
                if (animator.GetBool("firedeath") && deathTimer > deathUpTime) {
                    targetEuler = new Vector3(-15, controller.facingRight ? 110 : 250, 0);
                } else {
                    targetEuler = new Vector3(0, 180, 0);
                }
                instant = true;
            } else if (animator.GetBool("pipe")) {
                targetEuler = new Vector3(0, 180, 0);
                instant = true;
            } else if (animator.GetBool("inShell") && (!controller.onSpinner || Mathf.Abs(body.velocity.x) > 0.3f)) {
                targetEuler += Mathf.Abs(body.velocity.x) / controller.RunningMaxSpeed * Time.deltaTime * new Vector3(0, 1800 * (controller.facingRight ? -1 : 1));
                instant = true;
            } else if (wasTurnaround || controller.skidding || controller.turnaround || animator.GetCurrentAnimatorStateInfo(0).IsName("turnaround")) {
                if (controller.facingRight ^ (animator.GetCurrentAnimatorStateInfo(0).IsName("turnaround") || controller.skidding)) {
                    targetEuler = new Vector3(0, 250, 0);
                } else {
                    targetEuler = new Vector3(0, 110, 0);
                }
                instant = true;
            } else {
                if (controller.onSpinner && controller.onGround && Mathf.Abs(body.velocity.x) < 0.3f && !controller.holding) {
                    targetEuler += new Vector3(0, -1800, 0) * Time.deltaTime;
                    instant = true;
                    changeFacing = true;
                } else if (controller.flying || controller.propeller) {
                    targetEuler += new Vector3(0, -1200 - (controller.propellerTimer * 2000) - (controller.drill ? 800 : 0) + (controller.propeller && controller.propellerSpinTimer <= 0 && body.velocity.y < 0 ? 800 : 0), 0) * Time.deltaTime;
                    instant = true;
                } else {
                    targetEuler = new Vector3(0, controller.facingRight ? 110 : 250, 0);
                }
            }
            propellerVelocity = Mathf.Clamp(propellerVelocity + (1800 * ((controller.flying || controller.propeller || controller.usedPropellerThisJump) ? -1 : 1) * Time.deltaTime), -2500, -300);
            propeller.transform.Rotate(Vector3.forward, propellerVelocity * Time.deltaTime);

            if (instant || wasTurnaround) {
                models.transform.rotation = Quaternion.Euler(targetEuler);
            } else {
                float maxRotation = 2000f * Time.deltaTime;
                float x = models.transform.eulerAngles.x, y = models.transform.eulerAngles.y, z = models.transform.eulerAngles.z;
                x += Mathf.Clamp(targetEuler.x - x, -maxRotation, maxRotation);
                y += Mathf.Clamp(targetEuler.y - y, -maxRotation, maxRotation);
                z += Mathf.Clamp(targetEuler.z - z, -maxRotation, maxRotation);
                models.transform.rotation = Quaternion.Euler(x, y, z);
            }

            if (changeFacing)
                controller.facingRight = models.transform.eulerAngles.y < 180;

            wasTurnaround = animator.GetCurrentAnimatorStateInfo(0).IsName("turnaround");
        }

        //Particles
        SetParticleEmission(dust, !gameover && (controller.wallSlideLeft || controller.wallSlideRight || (controller.onGround && (controller.skidding || (controller.crouching && Mathf.Abs(body.velocity.x) > 1))) || (controller.sliding && Mathf.Abs(body.velocity.x) > 0.2 && controller.onGround)) && !controller.pipeEntering);
        SetParticleEmission(drillParticle, !gameover && controller.drill);
        if (controller.drill)
            drillParticleAudio.clip = (controller.state == Enums.PowerupState.PropellerMushroom ? propellerDrill : normalDrill);
        SetParticleEmission(sparkles, !gameover && controller.invincible > 0);
        SetParticleEmission(giantParticle, !gameover && controller.state == Enums.PowerupState.MegaMushroom && controller.giantStartTimer <= 0);
        SetParticleEmission(fireParticle, !gameover && animator.GetBool("firedeath") && controller.dead && deathTimer > deathUpTime);

        //Blinking
        if (controller.dead) {
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

        if (controller.cameraController.IsControllingCamera)
            HorizontalCamera.OFFSET_TARGET = (controller.flying || controller.propeller) ? 0.5f : 0f;

        if (controller.crouching || controller.sliding || controller.skidding) {
            dust.transform.localPosition = Vector2.zero;
        } else if (controller.wallSlideLeft || controller.wallSlideRight) {
            dust.transform.localPosition = new Vector2(mainHitbox.size.x * (3f / 4f) * (controller.wallSlideLeft ? -1 : 1), mainHitbox.size.y * (3f / 4f));
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

        bool right = controller.joystick.x > 0.35f;
        bool left = controller.joystick.x < -0.35f;

        animator.SetBool("onLeft", controller.wallSlideLeft);
        animator.SetBool("onRight", controller.wallSlideRight);
        animator.SetBool("onGround", controller.onGround);
        animator.SetBool("invincible", controller.invincible > 0);
        animator.SetBool("skidding", controller.skidding);
        animator.SetBool("propeller", controller.propeller);
        animator.SetBool("propellerSpin", controller.propellerSpinTimer > 0);
        animator.SetBool("crouching", controller.crouching);
        animator.SetBool("groundpound", controller.groundpound);
        animator.SetBool("sliding", controller.sliding);
        animator.SetBool("knockback", controller.knockback);
        animator.SetBool("facingRight", (left ^ right) ? right : controller.facingRight);
        animator.SetBool("flying", controller.flying);
        animator.SetBool("drill", controller.drill);

        if (photonView.IsMine) {
            //Animation
            animator.SetBool("turnaround", controller.turnaround);
            float animatedVelocity = Mathf.Abs(body.velocity.x) + Mathf.Abs(body.velocity.y * Mathf.Sin(controller.floorAngle * Mathf.Deg2Rad)) * (Mathf.Sign(controller.floorAngle) == Mathf.Sign(body.velocity.x) ? 0 : 1);
            if (controller.stuckInBlock) {
                animatedVelocity = 0;
            } else if (controller.propeller) {
                animatedVelocity = 2.5f;
            } else if (controller.state == Enums.PowerupState.MegaMushroom && Mathf.Abs(controller.joystick.x) > .2f) {
                animatedVelocity = 4.5f;
            } else if (left ^ right && !controller.hitRight && !controller.hitLeft) {
                animatedVelocity = Mathf.Max(3.5f, animatedVelocity);
            } else if (controller.onIce) {
                animatedVelocity = 0;
            }
            animator.SetFloat("velocityX", animatedVelocity);
            animator.SetFloat("velocityY", body.velocity.y);
            animator.SetBool("doublejump", controller.doublejump);
            animator.SetBool("triplejump", controller.triplejump);
            animator.SetBool("holding", controller.holding != null);
            animator.SetBool("head carry", controller.holding != null && controller.holding is FrozenCube);
            animator.SetBool("pipe", controller.pipeEntering != null);
            animator.SetBool("blueshell", controller.state == Enums.PowerupState.BlueShell);
            animator.SetBool("mini", controller.state == Enums.PowerupState.MiniMushroom);
            animator.SetBool("mega", controller.state == Enums.PowerupState.MegaMushroom);
            animator.SetBool("inShell", controller.inShell || (controller.state == Enums.PowerupState.BlueShell && (controller.crouching || controller.groundpound) && controller.groundpoundCounter <= 0.15f));
        } else {
            //controller.wallSlideLeft = animator.GetBool("onLeft");
            //controller.wallSlideRight = animator.GetBool("onRight");
            //controller.onGround = animator.GetBool("onGround");
            //controller.skidding = animator.GetBool("skidding");
            //controller.groundpound = animator.GetBool("groundpound");
            controller.turnaround = animator.GetBool("turnaround");
            //controller.crouching = animator.GetBool("crouching");
            controller.invincible = animator.GetBool("invincible") ? 1f : 0f;
            //controller.flying = animator.GetBool("flying");
            //controller.drill = animator.GetBool("drill");
            //controller.sliding = animator.GetBool("sliding");
            //controller.facingRight = animator.GetBool("facingRight");
            //controller.propellerSpinTimer = animator.GetBool("propellerSpin") ? 1f : 0f;
        }

        if (controller.giantEndTimer > 0) {
            transform.localScale = Vector3.one + (Vector3.one * (Mathf.Min(1, controller.giantEndTimer / (controller.giantStartTime / 2f)) * 2.6f));
        } else {
            transform.localScale = controller.state switch {
                Enums.PowerupState.MiniMushroom => Vector3.one / 2,
                Enums.PowerupState.MegaMushroom => Vector3.one + (Vector3.one * (Mathf.Min(1, 1 - (controller.giantStartTimer / controller.giantStartTime)) * 2.6f)),
                _ => Vector3.one,
            };
        }

        //Shader effects
        if (materialBlock == null)
            materialBlock = new();

        materialBlock.SetFloat("RainbowEnabled", controller.invincible > 0 ? 1.1f : 0f);
        int ps = controller.state switch {
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
        if (controller.giantTimer > 0 && controller.giantTimer < 4) {
            float v = ((Mathf.Sin(controller.giantTimer * 20f) + 1f) / 2f * 0.9f) + 0.1f;
            giantMultiply = new Vector3(v, 1, v);
        }
        materialBlock.SetVector("MultiplyColor", giantMultiply);

        foreach (Renderer r in renderers)
            r.SetPropertyBlock(materialBlock);

        //hit flash
        models.SetActive(GameManager.Instance.gameover || controller.dead || !(controller.hitInvincibilityCounter > 0 && controller.hitInvincibilityCounter * (controller.hitInvincibilityCounter <= 0.75f ? 5 : 2) % (blinkDuration * 2f) < blinkDuration));

        //Model changing
        bool large = controller.state >= Enums.PowerupState.Mushroom;

        largeModel.SetActive(large);
        smallModel.SetActive(!large);
        blueShell.SetActive(controller.state == Enums.PowerupState.BlueShell);

        largeShellExclude.SetActive(!animator.GetCurrentAnimatorStateInfo(0).IsName("in-shell"));
        propellerHelmet.SetActive(controller.state == Enums.PowerupState.PropellerMushroom);
        animator.avatar = large ? largeAvatar : smallAvatar;
        animator.runtimeAnimatorController = large ? controller.character.largeOverrides : controller.character.smallOverrides;

        HandleDeathAnimation();
        HandlePipeAnimation();

        transform.position = new(transform.position.x, transform.position.y, animator.GetBool("pipe") ? 1 : -4);
    }
    void HandleDeathAnimation() {
        if (!controller.dead) {
            deathTimer = 0;
            return;
        }

        deathTimer += Time.fixedDeltaTime;
        if (deathTimer < deathUpTime) {
            deathUp = false;
            body.gravityScale = 0;
            body.velocity = Vector2.zero;
            animator.Play("deadstart");
        } else {
            if (!deathUp && body.position.y > GameManager.Instance.GetLevelMinY()) {
                body.velocity = new Vector2(0, deathForce);
                deathUp = true;
                if (animator.GetBool("firedeath")) {
                    controller.PlaySound(Enums.Sounds.Player_Voice_LavaDeath);
                    controller.PlaySound(Enums.Sounds.Player_Sound_LavaHiss);
                }
                animator.SetTrigger("deathup");
            }
            body.gravityScale = 1.2f;
            body.velocity = new Vector2(0, Mathf.Max(-deathForce, body.velocity.y));
        }
        if (controller.photonView.IsMine && deathTimer + Time.fixedDeltaTime > (3 - 0.43f) && deathTimer < (3 - 0.43f))
            controller.fadeOut.FadeOutAndIn(0.33f, .1f);

        if (photonView.IsMine && deathTimer >= 3f)
            photonView.RPC("PreRespawn", RpcTarget.All);

        if (body.position.y < GameManager.Instance.GetLevelMinY() - transform.lossyScale.y) {
            models.SetActive(false);
            body.velocity = Vector2.zero;
            body.gravityScale = 0;
        }
    }

    void HandlePipeAnimation() {
        if (!photonView.IsMine)
            return;
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
            transform.position = body.position = new Vector3(pe.otherPipe.transform.position.x, pe.otherPipe.transform.position.y, 1) - (Vector3) offset;
            photonView.RPC("PlaySound", RpcTarget.All, Enums.Sounds.Player_Sound_Powerdown);
            controller.cameraController.Recenter();
        }
        if (pipeTimer >= pipeDuration) {
            controller.pipeEntering = null;
            body.isKinematic = false;
            controller.onGround = false;
            controller.properJump = false;
            controller.koyoteTime = 1;
            controller.crouching = false;
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