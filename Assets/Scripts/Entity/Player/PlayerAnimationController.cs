using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;

public class PlayerAnimationController : MonoBehaviourPun {

    readonly Vector3 cameraOffsetLeft = Vector3.left, cameraOffsetRight = Vector3.right, cameraOffsetZero = Vector3.zero;

    PlayerController controller;
    Animator animator;
    Rigidbody2D body;
    BoxCollider2D mainHitbox;
    CameraController cameraController;

    [SerializeField] GameObject models, smallModel, largeModel, blueShell, propellerHelmet, propeller;
    [SerializeField] ParticleSystem dust, sparkles, drillParticle, giantParticle;
    [SerializeField] float blinkDuration = 0.1f, pipeDuration = 2f, heightSmallModel = 0.46f, heightLargeModel = 0.82f, deathUpTime = 0.6f, deathForce = 7f;
    [SerializeField] Avatar smallAvatar, largeAvatar;
    [SerializeField] [ColorUsage(true, false)] Color glowColor = Color.clear;
    [SerializeField] Animator propellerAnimator;

    AudioSource drillParticleAudio;
    [SerializeField] AudioClip normalDrill, propellerDrill;

    Enums.PlayerEyeState eyeState;
    float blinkTimer, pipeTimer, deathTimer, propellerVelocity;
    bool wasTurnaround, deathUp;

    public void Start() {
        controller = GetComponent<PlayerController>();
        animator = GetComponent<Animator>();
        body = GetComponent<Rigidbody2D>();
        mainHitbox = GetComponent<BoxCollider2D>();
        cameraController = GetComponent<CameraController>();
        drillParticleAudio = drillParticle.GetComponent<AudioSource>();

        smallModel.SetActive(false);
        largeModel.SetActive(false);
        blueShell.SetActive(false);
        propellerHelmet.SetActive(false);

        if (photonView && !photonView.IsMine)
            glowColor = Color.HSVToRGB(controller.playerId / ((float) PhotonNetwork.PlayerList.Length + 1), 1, 1);
    }

    public void Update() {
        HandleAnimations();
    }

    void HandleAnimations() {
        Vector3 targetEuler = models.transform.eulerAngles;
        bool instant = false;
        if (controller.dead || animator.GetBool("pipe")) {
            targetEuler = new Vector3(0, 180, 0);
            instant = true;
        } else if (animator.GetBool("inShell") && !controller.onSpinner) {
            targetEuler += Mathf.Abs(body.velocity.x) / controller.runningMaxSpeed * Time.deltaTime * new Vector3(0, 1800 * (controller.facingRight ? -1 : 1));
            instant = true;
        } else if (controller.skidding || controller.turnaround) {
            if (controller.facingRight ^ (animator.GetCurrentAnimatorStateInfo(0).IsName("turnaround") || controller.turnaround)) {
                targetEuler = new Vector3(0, 260, 0);
            } else {
                targetEuler = new Vector3(0, 100, 0);
            }
        } else {
            if (controller.onSpinner && controller.onGround && Mathf.Abs(body.velocity.x) < 0.3f && !controller.holding && controller.state != Enums.PowerupState.Giant) {
                targetEuler += new Vector3(0, -1800, 0) * Time.deltaTime;
                instant = true;
            } else if (controller.flying || controller.propeller) {
                targetEuler += new Vector3(0, -1200 - (controller.propellerTimer * 2000) - (controller.drill ? 800 : 0) + (controller.propeller && controller.propellerSpinTimer <= 0 && body.velocity.y < 0 ? 800 : 0), 0) * Time.deltaTime;
                instant = true;
                
            } else {
                targetEuler = new Vector3(0, controller.facingRight ? 100 : 260, 0);
            }
        }
        propellerVelocity = Mathf.Clamp(propellerVelocity + (1800 * ((controller.flying || controller.propeller) ? -1 : 1) * Time.deltaTime), -2500, -300);
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
        wasTurnaround = animator.GetCurrentAnimatorStateInfo(0).IsName("turnaround") || controller.turnaround;

        //Particles
        SetParticleEmission(dust, (controller.onLeft || controller.onRight || (controller.onGround && ((controller.skidding && !controller.doIceSkidding) || (controller.crouching && Mathf.Abs(body.velocity.x) > 1))) || (controller.sliding && Mathf.Abs(body.velocity.x) > 0.2 && controller.onGround)) && !controller.pipeEntering);
        SetParticleEmission(drillParticle, controller.drill);
        if (controller.drill)
            drillParticleAudio.clip = (controller.state == Enums.PowerupState.PropellerMushroom ? propellerDrill : normalDrill);
        SetParticleEmission(sparkles, controller.invincible > 0);
        SetParticleEmission(giantParticle, controller.state == Enums.PowerupState.Giant && controller.giantStartTimer <= 0);

        //Blinking
        if (controller.dead) {
            eyeState = Enums.PlayerEyeState.Death;
        } else {
            if ((blinkTimer -= Time.fixedDeltaTime) < 0)
                blinkTimer = 3f + (Random.value * 2f);
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

        if (photonView.IsMine)
            HorizontalCamera.OFFSET_TARGET = controller.flying ? 0.75f : 0f;
        if (controller.flying) {
            float percentage = Mathf.Abs(body.velocity.x) / controller.walkingMaxSpeed;
            cameraController.offset = 2f * percentage * (body.velocity.x > 0 ? cameraOffsetRight : cameraOffsetLeft);
            cameraController.exactCentering = true;
            cameraController.dampingTime = 0.5f;
        } else {
            cameraController.offset = cameraOffsetZero;
            cameraController.exactCentering = false;
            cameraController.dampingTime = 0;
        }

        if (controller.crouching || controller.sliding || controller.skidding) {
            dust.transform.localPosition = Vector2.zero;
        } else {
            dust.transform.localPosition = new Vector2(mainHitbox.size.x * (3f / 4f) * (controller.onLeft ? -1 : 1), mainHitbox.size.y * (3f / 4f));
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

        if (photonView.IsMine) {
            //Animation
            animator.SetBool("skidding", !controller.doIceSkidding && controller.skidding);
            animator.SetBool("turnaround", controller.turnaround);
            animator.SetBool("onLeft", controller.onLeft);
            animator.SetBool("onRight", controller.onRight);
            animator.SetBool("onGround", controller.onGround);
            animator.SetBool("invincible", controller.invincible > 0);
            float animatedVelocity = Mathf.Abs(body.velocity.x) + Mathf.Abs(body.velocity.y * Mathf.Sin(controller.floorAngle * Mathf.Deg2Rad));
            if (controller.stuckInBlock) {
                animatedVelocity = 0;
            } else if (controller.propeller) {
                animatedVelocity = 2.5f;
            } else if (controller.doIceSkidding) {
                if (controller.skidding)
                    animatedVelocity = 3.5f;
                if (controller.iceSliding)
                    animatedVelocity = 0f;
            }
            animator.SetFloat("velocityX", animatedVelocity);
            animator.SetFloat("velocityY", body.velocity.y);
            animator.SetBool("doublejump", controller.doublejump);
            animator.SetBool("triplejump", controller.triplejump);
            animator.SetBool("crouching", controller.crouching);
            animator.SetBool("groundpound", controller.groundpound);
            animator.SetBool("sliding", controller.sliding);
            animator.SetBool("holding", controller.holding != null);
            animator.SetBool("knockback", controller.knockback);
            animator.SetBool("pipe", controller.pipeEntering != null);
            animator.SetBool("mini", controller.state == Enums.PowerupState.Mini);
            animator.SetBool("mega", controller.state == Enums.PowerupState.Giant);
            animator.SetBool("flying", controller.flying);
            animator.SetBool("drill", controller.drill);
            animator.SetBool("inShell", controller.inShell || (controller.state == Enums.PowerupState.Shell && (controller.crouching || controller.groundpound)));
            animator.SetBool("facingRight", controller.facingRight);
            animator.SetBool("propeller", controller.propeller);
            animator.SetBool("propellerSpin", controller.propellerSpinTimer > 0);
        } else {
            controller.onLeft = animator.GetBool("onLeft");
            controller.onRight = animator.GetBool("onRight");
            controller.onGround = animator.GetBool("onGround");
            controller.skidding = animator.GetBool("skidding");
            controller.turnaround = animator.GetBool("turnaround");
            controller.crouching = animator.GetBool("crouching");
            controller.invincible = animator.GetBool("invincible") ? 1f : 0f;
            controller.flying = animator.GetBool("flying");
            controller.drill = animator.GetBool("drill");
            controller.sliding = animator.GetBool("sliding");
            controller.facingRight = animator.GetBool("facingRight");
            controller.propellerSpinTimer = animator.GetBool("propellerSpin") ? 1f : 0f;
        }

        if (controller.giantEndTimer > 0) {
            transform.localScale = Vector3.one + (Vector3.one * (Mathf.Min(1, controller.giantEndTimer / (controller.giantStartTime / 2f)) * 2.6f));
        } else {
            transform.localScale = controller.state switch {
                Enums.PowerupState.Mini => Vector3.one / 2,
                Enums.PowerupState.Giant => Vector3.one + (Vector3.one * (Mathf.Min(1, 1 - (controller.giantStartTimer / controller.giantStartTime)) * 2.6f)),
                _ => Vector3.one,
            };
        }

        //Enable rainbow effect
        MaterialPropertyBlock block = new();
        block.SetFloat("RainbowEnabled", animator.GetBool("invincible") ? 1.1f : 0f);
        int ps = controller.state switch {
            Enums.PowerupState.FireFlower => 1,
            Enums.PowerupState.PropellerMushroom => 2,
            Enums.PowerupState.IceFlower => 3,
            _ => 0
        };
        block.SetFloat("PowerupState", ps);
        block.SetFloat("EyeState", (int) eyeState);
        block.SetFloat("ModelScale", transform.lossyScale.x);
        block.SetColor("GlowColor", glowColor);
        Vector3 giantMultiply = Vector3.one;
        if (controller.giantTimer > 0 && controller.giantTimer < 4) {
            float v = ((Mathf.Sin(controller.giantTimer * 20f) + 1f) / 2f * 0.9f) + 0.1f;
            giantMultiply = new Vector3(v, 1, v);
        }
        block.SetVector("MultiplyColor", giantMultiply);
        foreach (MeshRenderer renderer in GetComponentsInChildren<MeshRenderer>())
            renderer.SetPropertyBlock(block);
        foreach (SkinnedMeshRenderer renderer in GetComponentsInChildren<SkinnedMeshRenderer>())
            renderer.SetPropertyBlock(block);

        //hit flash
        models.SetActive(!(controller.hitInvincibilityCounter > 0 && controller.hitInvincibilityCounter * (controller.hitInvincibilityCounter <= 0.75f ? 5 : 2) % (blinkDuration * 2f) < blinkDuration));

        //Hitbox changing
        UpdateHitbox();

        //Model changing
        bool large = controller.state >= Enums.PowerupState.Large;

        largeModel.SetActive(large);
        smallModel.SetActive(!large);
        blueShell.SetActive(controller.state == Enums.PowerupState.Shell);
        propellerHelmet.SetActive(controller.state == Enums.PowerupState.PropellerMushroom);
        animator.avatar = large ? largeAvatar : smallAvatar;

        HandleDeathAnimation();
        HandlePipeAnimation();

        if (animator.GetBool("pipe")) {
            gameObject.layer = PlayerController.HITS_NOTHING_LAYERID;
            transform.position = new Vector3(body.position.x, body.position.y, 1);
        } else if (controller.dead || controller.stuckInBlock || controller.giantStartTimer > 0 || controller.giantEndTimer > 0) {
            gameObject.layer = PlayerController.HITS_NOTHING_LAYERID;
            transform.position = new Vector3(body.position.x, body.position.y, -4);
        } else {
            gameObject.layer = PlayerController.DEFAULT_LAYERID;
            transform.position = new Vector3(body.position.x, body.position.y, -4);
        }
    }
    void HandleDeathAnimation() {
        if (!controller.dead) {
            deathTimer = 0;
            return;
        }
        if (body.position.y < GameManager.Instance.GetLevelMinY() - transform.lossyScale.y)
            transform.position = body.position = new Vector2(body.position.x, GameManager.Instance.GetLevelMinY() - 20);

        deathTimer += Time.fixedDeltaTime;
        if (deathTimer < deathUpTime) {
            deathUp = false;
            body.gravityScale = 0;
            body.velocity = Vector2.zero;
        } else {
            if (!deathUp && body.position.y > GameManager.Instance.GetLevelMinY()) {
                body.velocity = new Vector2(0, deathForce);
                deathUp = true;
            }
            body.gravityScale = 1.2f;
            body.velocity = new Vector2(0, Mathf.Max(-deathForce, body.velocity.y));
        }
        if (controller.photonView.IsMine && deathTimer + Time.fixedDeltaTime > (3-0.43f) && deathTimer < (3 - 0.43f))
            controller.fadeOut.FadeOutAndIn(0.33f, .1f);

        if (photonView.IsMine && deathTimer >= 3f)
            photonView.RPC("PreRespawn", RpcTarget.All);
    }

    void UpdateHitbox() {
        float width = mainHitbox.size.x;
        float height;

        if (controller.state <= Enums.PowerupState.Small || (controller.invincible > 0 && !controller.onGround && !controller.crouching && !controller.sliding) || controller.groundpound) {
            height = heightSmallModel;
        } else {
            height = heightLargeModel;
        }

        if (controller.crouching || controller.inShell || controller.sliding)
            height *= controller.state <= Enums.PowerupState.Small ? 0.7f : 0.5f;

        mainHitbox.size = new Vector2(width, height);
        mainHitbox.offset = new Vector2(0, height / 2f);
    }
    void HandlePipeAnimation() {
        if (!photonView.IsMine)
            return;
        if (!controller.pipeEntering) {
            pipeTimer = 0;
            return;
        }

        PipeManager pe = controller.pipeEntering;

        body.isKinematic = true;
        body.velocity = controller.pipeDirection;

        if (pipeTimer < pipeDuration / 2f && pipeTimer + Time.fixedDeltaTime >= pipeDuration / 2f) {
            //tp to other pipe
            if (pe.otherPipe.bottom == pe.bottom)
                controller.pipeDirection *= -1;

            Vector2 offset = controller.pipeDirection * (pipeDuration / 2f);
            if (pe.otherPipe.bottom) {
                offset -= controller.pipeDirection;
                offset.y -= heightLargeModel - (mainHitbox.size.y * transform.localScale.y);
            }
            transform.position = body.position = new Vector3(pe.otherPipe.transform.position.x, pe.otherPipe.transform.position.y, 1) - (Vector3) offset;
            photonView.RPC("PlaySound", RpcTarget.All, "player/pipe");
        }
        if (pipeTimer >= pipeDuration) {
            controller.pipeEntering = null;
            body.isKinematic = false;
        }
        pipeTimer += Time.fixedDeltaTime;
    }
}