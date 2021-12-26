using System.Collections;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.InputSystem;

using Photon.Pun;

public class PlayerController : MonoBehaviourPun {
    
    private static int ANY_GROUND_MASK, ONLY_GROUND_MASK, GROUND_LAYERID;
    
    private int playerId = 0;
    [SerializeField] public bool dead = false;
    [SerializeField] public PlayerState state = PlayerState.Small, previousState;
    [SerializeField] public float slowriseGravity = 0.85f, normalGravity = 2.5f, flyingGravity = 0.5f, flyingTerminalVelocity = -1f, drillVelocity = 9f, deathUpTime = 0.6f, deathForce = 7f, groundpoundTime = 0.25f, groundpoundVelocity = 12, blinkingSpeed = 0.25f, terminalVelocity = -7f, jumpVelocity = 6.25f, launchVelocity = 20f, walkingAcceleration = 8f, runningAcceleration = 3f, walkingMaxSpeed = 2.7f, runningMaxSpeed = 5, wallslideSpeed = -2f, walljumpVelocity = 6f, pipeDuration = 2f, giantStartTime = 1.5f, icePenalty = 0.4f;
    [SerializeField] ParticleSystem dust, sparkles, drillParticle;
    [SerializeField] BoxCollider2D smolHitbox, bigHitbox;
    GameObject models;
    private new AudioSource audio;
    private Animator animator;
    public Rigidbody2D body;

    public bool onGround, crushGround, onGroundLastFrame, onRight, onLeft, hitRoof, skidding, turnaround, facingRight = true, singlejump, doublejump, triplejump, bounce, crouching, groundpound, groundpoundSit, knockback, deathUp, hitBlock, running, jumpHeld, ice, flying, drill, inShell, hitLeft, hitRight, iceSliding;
    float walljumping, landing, koyoteTime, deathCounter, groundpoundCounter, hitInvincibilityCounter, powerupFlash, throwInvincibility, jumpBuffer, pipeTimer, giantStartTimer;
    public float invincible = 0f, giantTimer = 0f, floorAngle;
    
    private Vector2 pipeDirection;
    public int stars, coins;
    List<Vector3Int> tilesStandingOn = new List<Vector3Int>(), tilesJumpedInto = new List<Vector3Int>(), tilesHitSide = new List<Vector3Int>();
    public string storedPowerup = null;
    HoldableEntity holding, holdingOld;
    public Gradient glowGradient;
    [ColorUsage(true, false)]
    public Color glowColor = Color.clear;

    public List<string> giantTiles = new List<string>();

    private float analogDeadzone = 0.35f;
    private Vector2 joystick;

    public GameObject smallMarioModel, largeMarioModel, marioBlueShell;
    public Avatar smallMarioAvatar, largeMarioAvatar;
    public GameObject onSpinner;
    PipeManager pipeEntering;
    private CameraController cameraController;
    private Vector3 cameraOffsetLeft = Vector3.left, cameraOffsetRight = Vector3.right, cameraOffsetZero = Vector3.zero;

    private bool starDirection, step;

    void Awake() {
        ANY_GROUND_MASK = LayerMask.GetMask("Ground", "Semisolids");
        ONLY_GROUND_MASK = LayerMask.GetMask("Ground");
        GROUND_LAYERID = LayerMask.NameToLayer("Ground");
        
        cameraController = Camera.main.GetComponent<CameraController>();
        animator = GetComponentInChildren<Animator>();
        body = GetComponent<Rigidbody2D>();
        audio = GetComponent<AudioSource>();
        models = transform.Find("Models").gameObject;
        starDirection = Random.value < 0.5;
        PlayerInput input = GetComponent<PlayerInput>();
        input.enabled = !photonView || photonView.IsMine;

        if (photonView) {
            playerId = System.Array.IndexOf(PhotonNetwork.PlayerList, photonView.Owner);
            if (!photonView.IsMine) {
                float samplePos = (float) playerId / (float) PhotonNetwork.PlayerList.Length;
                glowColor = glowGradient.Evaluate(samplePos);
            }
        }
    }

    void HandleGroundCollision() {
        bool ignoreRoof = false;
        int down = 0, left = 0, right = 0, up = 0;
        float blockRoofY = 0;

        Tilemap tilemap = GameManager.Instance.tilemap;
        Transform tmtf = tilemap.transform;

        int collisionCount = 0;
        ContactPoint2D[] contacts = new ContactPoint2D[20];
        if (smolHitbox.enabled) {
            collisionCount = smolHitbox.GetContacts(contacts);
        } else {
            collisionCount = bigHitbox.GetContacts(contacts);
        }

        float highestAngleThisFrame = 0;
        crushGround = false;
        for (int i = 0; i < collisionCount; i++) {
            ContactPoint2D contact = contacts[i];
            Vector2 n = contact.normal;
            if (Vector2.Dot(n,Vector2.up) > .5f) {
                Vector2 modifiedVec = contact.point + (new Vector2(0.01f, 0) * (contact.point.x - transform.position.x < 0 ? 1 : -1)); 
                Vector3Int vec = Utils.WorldToTilemapPosition(modifiedVec);
                float playerY = Utils.WorldToTilemapPosition(transform.position + new Vector3(0, 0.25f)).y;
                if (((contact.collider.gameObject.layer != GROUND_LAYERID && playerY <= vec.y) || body.velocity.y > 0.2f) && (contact.collider.gameObject.tag != "platform")) {
                    //invalid flooring
                    continue;
                }
                crushGround |= (contact.collider.gameObject.tag != "platform");
                down++;
                highestAngleThisFrame = Mathf.Max(highestAngleThisFrame, Vector2.Angle(n, Vector2.up));
                tilesStandingOn.Add(vec);
            } else if (contact.collider.gameObject.layer == GROUND_LAYERID) {
                if (Vector2.Dot(n,Vector2.left) > .9f) {
                    right++;
                    Vector2 modifiedVec = contact.point + (new Vector2(0, (contact.point.y - transform.position.y > 0.2f ? -0.05f : 0.05f)));
                    Vector3Int vec = Utils.WorldToTilemapPosition(modifiedVec + new Vector2(0.1f, 0));
                    tilesHitSide.Add(vec);
                } else if (Vector2.Dot(n,Vector2.right) > .9f) {
                    left++;
                    Vector2 modifiedVec = contact.point + (new Vector2(0, (contact.point.y - transform.position.y > 0.2f ? -0.05f : 0.05f))); 
                    Vector3Int vec = Utils.WorldToTilemapPosition(modifiedVec + new Vector2(-0.1f, 0));
                    tilesHitSide.Add(vec);
                } else if (Vector2.Dot(n,Vector2.down) > .9f && !groundpound) {
                    up++;
                    Vector2 modifiedVec = contact.point + (new Vector2(0.01f, 0) * (contact.point.x - transform.position.x < 0 ? 1 : -1)); 
                    Vector3Int vec = Utils.WorldToTilemapPosition(modifiedVec);
                    blockRoofY = vec.y;
                    tilesJumpedInto.Add(vec);
                }
            } else {
                ignoreRoof = true;
            }
        }

        onGround = down >= 2 && body.velocity.y < 3;
        if (onGround) {
            onGroundLastFrame = true;

            if (tilesStandingOn.Count >= 2) {
                int xMax = Mathf.Max(tilesStandingOn[0].x, tilesStandingOn[1].x);
                int xMin = Mathf.Min(tilesStandingOn[0].x, tilesStandingOn[1].x);

                for (int temp = xMin; temp < xMax; temp++) {
                    tilesStandingOn.Add(new Vector3Int(temp, tilesStandingOn[0].y, 0));
                }
            }
        }
        hitLeft = left >= 2;
        onLeft = hitLeft && !inShell && body.velocity.y < -0.1 && !facingRight && !onGround && !holding && state != PlayerState.Giant && !flying;
        hitRight = right >= 2;
        onRight = hitRight && !inShell && body.velocity.y < -0.1 && facingRight && !onGround && !holding && state != PlayerState.Giant && !flying;
        hitRoof = !ignoreRoof && !onLeft && !onRight && up >= 2 && body.velocity.y > -0.2f;

        if ((left >= 2 || right >= 2) && tilesHitSide.Count >= 2) {
            int yMax = Mathf.Max(tilesHitSide[0].y, tilesHitSide[1].y);
            int yMin = Mathf.Min(tilesHitSide[0].y, tilesHitSide[1].y);

            for (int temp = yMin; temp < yMax; temp++) {
                tilesHitSide.Add(new Vector3Int(tilesHitSide[0].x, temp, 0));
            }
        }
        floorAngle = highestAngleThisFrame;
    }

    void OnCollisionEnter2D(Collision2D collision) {
        if (!photonView.IsMine)
            return;
        if (collision.gameObject.tag == "Player") {
            //hit antoher player
            foreach (ContactPoint2D contact in collision.contacts) {
                GameObject otherObj = collision.gameObject;
                PlayerController other = otherObj.GetComponent<PlayerController>();
                PhotonView otherView = other.photonView;

                if (other.animator.GetBool("invincible")) {
                    return;
                }
                if (invincible > 0) {
                    otherView.RPC("Powerdown", RpcTarget.All, false);
                    return;
                }

                if (contact.normal.y > 0) {
                    //hit them from above
                    bounce = !groundpound;
                    drill = false;
                    otherView.RPC("Knockback", RpcTarget.All, otherObj.transform.position.x < transform.position.x, groundpound ? 2 : 1, photonView.ViewID);
                    return;
                }

                if (state == PlayerState.Shell && inShell) {
                    if (other.inShell) {
                        otherView.RPC("Knockback", RpcTarget.All, otherObj.transform.position.x < transform.position.x, 0, -1);
                        photonView.RPC("Knockback", RpcTarget.All, otherObj.transform.position.x > transform.position.x, 0, -1);
                    } else {
                        otherView.RPC("Powerdown", RpcTarget.All, false);
                    }
                    return;
                }

            }
            return;
        }
    }

    void Footstep() {
        if (ice && skidding) {
            PlaySoundFromAnim("player/ice-skid");
            return;
        }
        if (Mathf.Abs(body.velocity.x) < walkingMaxSpeed)
            return;
        
        PlaySoundFromAnim("player/walk" + (step ? "-2" : ""), Mathf.Abs(body.velocity.x) / (runningMaxSpeed + 4));
        step = !step;
    }

    void OnMovement(InputValue value) {
        if (!photonView.IsMine) return;
        joystick = value.Get<Vector2>();
    }

    void OnJump(InputValue value) {
        if (!photonView.IsMine) return;
        jumpHeld = value.Get<float>() >= 0.5f;
        if (jumpHeld) {
            jumpBuffer = 0.15f;
        }
    }

    void OnSprint(InputValue value) {
        if (!photonView.IsMine) return;
        running = value.Get<float>() >= 0.5f;
    }

    void OnFireball() {
        if (!photonView.IsMine) return;
        if (GameManager.Instance.paused) return;
        if (crouching) return;
        if (onLeft || onRight) return;
        if (groundpound || knockback) return;
        if (state != PlayerState.FireFlower) return;
        if (dead || holding || flying || drill) return;
        if (GameManager.Instance.gameover) return;
        if (pipeEntering) return;

        int count = 0;
        foreach (FireballMover existingFire in GameObject.FindObjectsOfType<FireballMover>()) {
            if (existingFire.photonView.IsMine) {
                if (++count >= 2) 
                    return;
            }
        }

        PhotonNetwork.Instantiate("Prefabs/Fireball", transform.position + new Vector3(facingRight ? 0.3f : -0.3f ,0.4f), Quaternion.identity, 0, new object[]{!facingRight});
        photonView.RPC("PlaySound", RpcTarget.All, "player/fireball");
        animator.SetTrigger("fireball");
    }

    void OnItem() {
        if (!photonView.IsMine) return;
        if (GameManager.Instance.paused) return;
        if (dead) return;
        if (storedPowerup == null || storedPowerup.Length <= 0) return; 

        SpawnItem(storedPowerup);
        storedPowerup = null;
    }

    void OnPause() {
        if (!photonView.IsMine) return;
        PlaySound("pause");
        GameManager.Instance.Pause();
    }

    [PunRPC]
    void HoldingWakeup() {
        holding = null;
        holdingOld = null;
        throwInvincibility = 0;
        Powerdown(false);
    }

    [PunRPC]
    void Powerup(string powerup, int powerupViewId) {
        bool stateUp = false;
        PlayerState previous = state;
        string store = null;
        switch (powerup) {
        case "Mushroom": {
            if (state == PlayerState.Small || state == PlayerState.Mini) {
                state = PlayerState.Large;
                stateUp = true;
                transform.localScale = Vector3.one;
            } else if (storedPowerup == null || storedPowerup == "") {
                store = powerup;
            }
            break;
        }
        case "FireFlower": {
            if (state != PlayerState.Giant && state != PlayerState.FireFlower) {
                state = PlayerState.FireFlower;
                stateUp = true;
                transform.localScale = Vector3.one;
            } else {
                store = powerup;
            }
            break;
        }
        case "Star": {
            invincible = 10f;
            stateUp = true;
            break;
        }
        case "MiniMushroom": {
            if (state == PlayerState.Mini || state == PlayerState.Giant) {
                store = powerup;
            } else {
                stateUp = true;
                state = PlayerState.Mini;
                transform.localScale = Vector3.one / 2;
            }
            break;
        }
        case "BlueShell": {
            if (state == PlayerState.Shell || state == PlayerState.Giant) {
                store = powerup;
            } else {
                stateUp = true;
                state = PlayerState.Shell;
                transform.localScale = Vector3.one;
            }
            break;
        }
        case "MegaMushroom": {
            if (state == PlayerState.Giant) {
                store = powerup;
            } else {
                state = PlayerState.Giant;
                stateUp = true;
                animator.SetTrigger("megapowerup");
                giantStartTimer = giantStartTime;
                giantTimer = 15f;
                transform.localScale = Vector3.one;
            }
            break;
        }
        }
        if (store != null) {
            storedPowerup = store;
        }
        if (stateUp) {
            previousState = previous;
            switch (state) {
            case PlayerState.Mini: {
                PlaySoundFromAnim("player/powerup-mini");
                break;
            }
            case PlayerState.Giant: {
                PlaySoundFromAnim("player/powerup-mega");
                break;
            }
            default: {
                PlaySoundFromAnim("player/powerup");
                break;
            }
            }
            powerupFlash = 2f;
            if (ForceCrouchCheck()) {
                crouching = true;
            }
        } else {
            PlaySoundFromAnim("player/reserve_item_store");
        }

        PhotonView view = PhotonView.Find(powerupViewId);
        if (view.IsMine) {
            PhotonNetwork.Destroy(view);
        } else {
            GameObject.Destroy(view.gameObject);
        }
    }

    [PunRPC]
    void Powerdown(bool ignoreInvincible = false) {
        if (!ignoreInvincible && hitInvincibilityCounter > 0)
            return;

        previousState = state;

        switch (state) {
        case PlayerState.Mini:
        case PlayerState.Small: {
            Death(false);
            break;
        }
        case PlayerState.Large: {
            state = PlayerState.Small;
            powerupFlash = 2f;
            SpawnStar();
            break;
        }
        case PlayerState.FireFlower:
        case PlayerState.Shell: {
            state = PlayerState.Large;
            powerupFlash = 2f;
            SpawnStar();
            break;
        }
        }

        if (!dead) {
            hitInvincibilityCounter = 3f;
            PlaySoundFromAnim("player/powerdown");
        }
    }

    [PunRPC]
    void SetCoins(int coins) {
        this.coins = coins;
    }
    [PunRPC]
    void SetStars(int stars) {
        this.stars = stars;
    }
    void OnTriggerEnter2D(Collider2D collider) {
        if (dead) return;
        
        Vector2 dir = (transform.position - collider.transform.position);
        dir.Normalize();
        bool downwards = Vector2.Dot(dir, Vector2.up) > 0.5f;
        switch (collider.tag) {
            case "goomba": {
                if (!photonView.IsMine) return;
                GoombaWalk goomba = collider.gameObject.GetComponentInParent<GoombaWalk>();
                if (goomba.dead)
                    break;
                if (inShell || invincible > 0 || ((groundpound || drill) && state != PlayerState.Mini && downwards) || state == PlayerState.Giant) {
                    collider.gameObject.transform.parent.gameObject.GetPhotonView().RPC("SpecialKill", RpcTarget.All, body.velocity.x > 0, groundpound);
                } else if (downwards) {
                    if (groundpound || state != PlayerState.Mini) {
                        collider.gameObject.transform.parent.gameObject.GetPhotonView().RPC("Kill", RpcTarget.All);
                        groundpound = false;
                    }
                    bounce = !drill;
                    photonView.RPC("PlaySound", RpcTarget.All, "enemy/goomba");
                } else {
                    if (holding) {
                        goomba.photonView.RPC("SpecialKill", RpcTarget.All, !facingRight, false);
                        holding.photonView.RPC("SpecialKill", RpcTarget.All, facingRight, false);
                        holding = null;
                    } else {
                        photonView.RPC("Powerdown", RpcTarget.All, false);
                    }
                }
                break;
            }
            case "bulletbill": {
                if (!photonView.IsMine) return;
                BulletBillMover bullet = collider.gameObject.GetComponentInParent<BulletBillMover>();
                if (bullet.dead)
                    break;
                if (inShell || invincible > 0 || (groundpound && state != PlayerState.Mini && downwards) || state == PlayerState.Giant) {
                    bullet.photonView.RPC("SpecialKill", RpcTarget.All, body.velocity.x > 0, groundpound && state != PlayerState.Mini);
                } else if (downwards) {
                    if (groundpound || drill || state != PlayerState.Mini) {
                        bullet.photonView.RPC("SpecialKill", RpcTarget.All, body.velocity.x > 0, groundpound && state != PlayerState.Mini);
                        groundpound = false;
                    }
                    bounce = true;
                    drill = false;
                    photonView.RPC("PlaySound", RpcTarget.All, "enemy/goomba");
                } else {
                    if (holding) {
                        bullet.photonView.RPC("SpecialKill", RpcTarget.All, !facingRight, false);
                        holding.photonView.RPC("SpecialKill", RpcTarget.All, facingRight, false);
                        holding = null;
                    } else {
                        photonView.RPC("Powerdown", RpcTarget.All, false);
                    }
                }
                break;
            }
            case "koopa": {
                if (!photonView.IsMine) return;
                downwards = Vector2.Dot(dir, Vector2.up) > 0;
                KoopaWalk koopa = collider.gameObject.GetComponentInParent<KoopaWalk>();
                if (holding == koopa)
                    break;
                if (koopa.dead)
                    break;
                if (throwInvincibility > 0 && holdingOld == koopa)
                    break;
                if (inShell || invincible > 0 || state == PlayerState.Giant) {
                    koopa.photonView.RPC("SpecialKill", RpcTarget.All, !facingRight, false);
                } else if (groundpound && state != PlayerState.Mini && downwards) {
                    koopa.photonView.RPC("EnterShell", RpcTarget.All);
                    if (!koopa.blue) {
                        koopa.photonView.RPC("Kick", RpcTarget.All, transform.position.x < koopa.transform.position.x, groundpound);
                        holdingOld = koopa;
                        throwInvincibility = 0.5f;
                    }
                } else if (downwards && (!koopa.shell || !koopa.IsStationary())) {
                    if (state != PlayerState.Mini || groundpound) {
                        koopa.photonView.RPC("EnterShell", RpcTarget.All);
                        if (state == PlayerState.Mini)
                            groundpound = false;
                    }
                    photonView.RPC("PlaySound", RpcTarget.All, "enemy/goomba");
                    bounce = true;
                } else {
                    if (koopa.shell && (koopa.IsStationary())) {
                        if (state != PlayerState.Mini && !holding && running && !flying && !crouching && !dead && !onLeft && !onRight && !doublejump && !triplejump) {
                            koopa.photonView.RPC("Pickup", RpcTarget.All, photonView.ViewID);
                            holding = koopa;
                        } else {
                            koopa.photonView.RPC("Kick", RpcTarget.All, transform.position.x < koopa.transform.position.x, groundpound);
                            holdingOld = koopa;
                            throwInvincibility = 0.5f;
                        }
                    } else {
                        if (holding) {
                            koopa.photonView.RPC("SpecialKill", RpcTarget.All, !facingRight, false);
                            holding.photonView.RPC("SpecialKill", RpcTarget.All, facingRight, false);
                            holding = null;
                        } else if (!koopa.putdown) {
                            photonView.RPC("Powerdown", RpcTarget.All, false);
                        }
                    }
                }
                break;
            }
            case "bobomb": {
                if (!photonView.IsMine) return;
                BobombWalk bomb = collider.gameObject.GetComponentInParent<BobombWalk>();
                if (holding == bomb || bomb.dead || (throwInvincibility > 0 && holdingOld == bomb))
                    break;
                if (inShell || invincible > 0) {
                    bomb.photonView.RPC("SpecialKill", RpcTarget.All, body.velocity.x > 0, false);
                    break;
                }
                if (downwards && !bomb.lit) {
                    if (state != PlayerState.Mini || (groundpound && downwards)) {
                        bomb.photonView.RPC("Light", RpcTarget.All);
                    }
                    photonView.RPC("PlaySound", RpcTarget.All, "enemy/goomba");
                    if (groundpound) {
                        bomb.photonView.RPC("Kick", RpcTarget.All, transform.position.x < bomb.transform.position.x, groundpound);
                    } else {
                        bounce = true;
                    }
                } else {
                    if (bomb.lit) {
                        if (state != PlayerState.Mini && !holding && running && !crouching && !flying && !dead && !onLeft && !onRight && !doublejump && !triplejump && !groundpound) {
                            bomb.photonView.RPC("Pickup", RpcTarget.All, photonView.ViewID);
                            holding = bomb;
                        } else {
                            bomb.photonView.RPC("Kick", RpcTarget.All, transform.position.x < bomb.transform.position.x, groundpound);
                        }
                    } else {
                        if (holding) {
                            bomb.photonView.RPC("SpecialKill", RpcTarget.All, !facingRight, false);
                            holding.photonView.RPC("SpecialKill", RpcTarget.All, facingRight, false);
                            holding = null;
                        } else {
                            photonView.RPC("Powerdown", RpcTarget.All, false);
                        }
                    }
                }
                break;
            }
            case "piranhaplant": {
                if (!photonView.IsMine) return;
                PiranhaPlantController piranha = collider.gameObject.GetComponentInParent<PiranhaPlantController>();
                if (inShell || invincible > 0) {
                    collider.gameObject.GetPhotonView().RPC("Kill", RpcTarget.All);
                } else {
                    if (holding) {
                        piranha.photonView.RPC("Kill", RpcTarget.All);
                        holding.photonView.RPC("Kill", RpcTarget.All);
                        holding = null;
                    } else {
                        photonView.RPC("Powerdown", RpcTarget.All, false);
                    }
                }
                break;
            }
            case "bigstar": {
                if (!photonView.IsMine) return;
                photonView.RPC("CollectBigStar", RpcTarget.All, collider.gameObject.transform.parent.gameObject.GetPhotonView().ViewID);
                break;
            }
            case "loosecoin": {
                if (!photonView.IsMine) return;
                Transform parent = collider.gameObject.transform.parent;
                photonView.RPC("CollectCoin", RpcTarget.All, parent.gameObject.GetPhotonView().ViewID, parent.position.x, parent.position.y);
                break;
            }
            case "coin": {
                if (!photonView.IsMine) return;
                photonView.RPC("CollectCoin", RpcTarget.All, collider.gameObject.GetPhotonView().ViewID, collider.transform.position.x, collider.transform.position.y);
                break;
            }
            case "BlueShell":
            case "Star":
            case "MiniMushroom":
            case "FireFlower":
            case "MegaMushroom":
            case "Mushroom": {
                if (!photonView.IsMine) return;
                MovingPowerup powerup = collider.gameObject.GetComponentInParent<MovingPowerup>();
                if (powerup.followMeCounter > 0)
                    break;
                photonView.RPC("Powerup", RpcTarget.All, collider.gameObject.tag, collider.gameObject.transform.parent.gameObject.GetPhotonView().ViewID);
                break;
            }
            case "Fireball": {
                if (!photonView.IsMine) return;
                FireballMover fireball = collider.gameObject.GetComponentInParent<FireballMover>();
                if (fireball.photonView.IsMine)
                    break;
                fireball.photonView.RPC("Kill", RpcTarget.All);
                if (state == PlayerState.Shell && (inShell || crouching || groundpound))
                    break;
                if (state == PlayerState.Mini) {
                    photonView.RPC("Powerdown", RpcTarget.All, false);
                } else {
                    photonView.RPC("Knockback", RpcTarget.All, collider.transform.position.x > transform.position.x, 1, fireball.photonView.ViewID);
                }
                break;
            }
            case "spinner": {
                onSpinner = collider.gameObject;
                break;
            }
            case "poison": {
                if (!photonView.IsMine) return;
                photonView.RPC("Death", RpcTarget.All, false);
                break;
            }
        }
    }
    void OnTriggerExit2D(Collider2D collider) {
        switch (collider.tag) {
            case "spinner": {
                onSpinner = null;
                break;
            }
        }
    }

    [PunRPC]
    void CollectBigStar(int starID) {
        PhotonView view = PhotonView.Find(starID);
        if (view == null)
            return;
        GameObject star = view.gameObject;
        if (star == null) 
            return;
        StarBouncer starScript = star.GetComponent<StarBouncer>();
        if (starScript.passthrough)
            return;
        
        if (photonView.IsMine) {
            photonView.RPC("SetStars", RpcTarget.Others, ++stars);
        }
        if (starScript.stationary) {
            //Main star, reset the tiles.
            GameManager.Instance.ResetTiles();
        }
        GameObject.Instantiate(Resources.Load("Prefabs/Particle/StarCollect"), star.transform.position, Quaternion.identity);
        PlaySoundFromAnim("player/star_collect");
        if (view.IsMine) {
            PhotonNetwork.Destroy(view);
        }
    }

    [PunRPC]
    void CollectCoin(int coinID, float x, float y) {
        if (PhotonView.Find(coinID)) {
            GameObject coin = PhotonView.Find(coinID).gameObject;
            if (coin.tag == "loosecoin") {
                if (coin.GetPhotonView().IsMine) {
                    PhotonNetwork.Destroy(coin);
                }
            } else {
                SpriteRenderer renderer = coin.GetComponent<SpriteRenderer>();
                if (!renderer.enabled)
                    return;
                renderer.enabled = false;
                coin.GetComponent<BoxCollider2D>().enabled = false;
            }
            GameObject.Instantiate(Resources.Load("Prefabs/Particle/CoinCollect"), new Vector3(x, y, 0), Quaternion.identity);
        }

        coins++;

        PlaySoundFromAnim("player/coin");
        GameObject num = (GameObject) GameObject.Instantiate(Resources.Load("Prefabs/Particle/Number"), new Vector3(x, y, 0), Quaternion.identity);
        Animator anim = num.GetComponentInChildren<Animator>();
        anim.SetInteger("number", coins);
        anim.SetTrigger("ready");

        if (photonView.IsMine && coins >= 8) {
            if (coins >= 8) {
                SpawnItem();
                coins = 0;
            }
            photonView.RPC("SetCoins", RpcTarget.Others, coins);
        }
    }

    void SpawnItem(string item = null) {
        if (item == null) {
            float rand = UnityEngine.Random.value;

            //megamushroom is still broken to hell, not supporting it yet.
            if (rand < 0.1) {
                item = "Star";
            } else if (rand < 0.4) {
                item = "FireFlower";
            } else if (rand < 0.55) {
                item = "MiniMushroom";
            } else if (rand < 0.7) {
                item = "BlueShell";
            } else {
                item = "Mushroom";
            }
        }

        GameObject obj = PhotonNetwork.Instantiate("Prefabs/Powerup/" + item, transform.position + new Vector3(0,5f), Quaternion.identity);
        MovingPowerup pow = obj.GetComponent<MovingPowerup>();
        pow.photonView.RPC("SetFollowMe", RpcTarget.All, photonView.ViewID);
        photonView.RPC("PlaySound", RpcTarget.All, "player/reserve_item_use");
    }

    [PunRPC]
    void Death(bool deathplane) {
        dead = true;

        onSpinner = null;
        pipeEntering = null;
        flying = false;
        drill = false;
        animator.SetBool("flying", false);
        deathCounter = 0;
        dust.Stop();
        onLeft = false;
        onRight = false;
        skidding = false;
        turnaround = false;
        inShell = false;
        animator.SetTrigger("dead");
        PlaySoundFromAnim("player/death");
        SpawnStar();
        if (holding) {
            PhotonNetwork.Destroy(holding.photonView);
            holding = null;
        }
        if (deathplane && photonView.IsMine) {
            transform.Translate(0, -4, 0);
        }
    }

    void SpawnStar() {
        if (stars > 0) {
            stars--;
            if (photonView.IsMine) {
                GameObject star = PhotonNetwork.Instantiate("Prefabs/BigStar", transform.position, Quaternion.identity, 0, new object[]{starDirection});
                StarBouncer sb = star.GetComponent<StarBouncer>();
                sb.photonView.TransferOwnership(PhotonNetwork.MasterClient);
                photonView.RPC("SetStars", RpcTarget.Others, stars);
                starDirection = !starDirection;
            }
        }
    }

    [PunRPC]
    public void PreRespawn() {
        transform.position = GameManager.Instance.GetSpawnpoint(playerId);
        dead = false;
        body.position = transform.position;
        animator.SetTrigger("respawn");
        state = PlayerState.Small;

        GameObject particle = (GameObject) GameObject.Instantiate(Resources.Load("Prefabs/Particle/Respawn"), transform.position, Quaternion.identity);
        if (photonView.IsMine) {
            particle.GetComponent<RespawnParticle>().player = this;
        }
        gameObject.SetActive(false);
    }

    [PunRPC]
    public void Respawn() {
        gameObject.SetActive(true);
        dead = false;
        state = PlayerState.Small;
        if (body)
            body.velocity = Vector2.zero;
        onLeft = false;
        onRight = false;
        flying = false;
        crouching = false;
        onGround = false;
        jumpBuffer = 0;
        dust.Stop();
        sparkles.Stop();
        drillParticle.Stop();
        invincible = 0;
        giantStartTimer = 0;
        giantTimer = 0;
        smolHitbox.enabled = true;
        bigHitbox.enabled = false;
        singlejump = false;
        doublejump = false;
        turnaround = false;
        triplejump = false;
        knockback = false;
        bounce = false;
        skidding = false;
        walljumping = 0f;
        groundpound = false;
        inShell = false;
        landing = 0f;
        hitInvincibilityCounter = 3f;
        GameObject.Instantiate(Resources.Load("Prefabs/Particle/Puff"), transform.position, Quaternion.identity);
    }

    [PunRPC]
    void PlaySound(string sound) {
        audio.PlayOneShot((AudioClip) Resources.Load("Sound/" + sound));
    }

    [PunRPC]
    void SpawnParticle(string particle) {
        GameObject.Instantiate(Resources.Load(particle), transform.position, Quaternion.identity);
    }

    [PunRPC]
    void SpawnParticle(string particle, float x, float y) {
        GameObject.Instantiate(Resources.Load(particle), new Vector2(x, y), Quaternion.identity);
    }

    void HandleGiantTiles() {
        //todo: refactor to use ne wtile system
        Transform tmtf = GameManager.Instance.tilemap.transform;
        float lsX = tmtf.localScale.x;
        float lsY = tmtf.localScale.y;
        float tmX = tmtf.position.x;
        float tmY = tmtf.position.y;

        float posX = transform.position.x;
        float posY = transform.position.y - 0.4f;
        bool landing = singlejump && onGround;

        for (int x = -2; x < 2; x++) {
            for (int y = 0; y <= 9; y++) {
                
                if (y == 0 && !(landing || groundpound))
                    continue;

                if (y < 8 && y > 0 && (Mathf.Abs(body.velocity.x) < 0.3f))
                    continue;

                if (y >= 8 && (body.velocity.y < 0 || onGround))
                    continue;

                int relX = Mathf.FloorToInt(((posX - tmX) / lsX) + x + 0.4f);
                int relY = Mathf.FloorToInt((posY - tmY) / lsY) + y;
                GiantDestroyTile(new Vector3Int(relX, relY, 0)); 
            }
        } 
        if (landing) {
            singlejump = false;
        }
    }

    bool GiantDestroyTile(Vector3Int vec) {
        //todo: refactor to use new tile system
        Tilemap tm = GameManager.Instance.tilemap;

        TileBase tile = tm.GetTile(vec);
        if (tile == null) {
            tm = GameManager.Instance.semiSolidTilemap;
            tile = tm.GetTile(vec);
            if (tile == null) {
                return false;
            }
        }

        if (giantTiles.Contains(tile.name)) {
            photonView.RPC("ModifyTilemap", RpcTarget.All, vec.x, vec.y, null);
            photonView.RPC("SpawnParticle", RpcTarget.All, vec.x, vec.y, (tile.name.Contains("Blue")));
            photonView.RPC("PlaySound", RpcTarget.All, "player/brick_break");

            return true;
        }

        return false;
    }

    int InteractWithTile(Vector3 tilePos, bool upwards) {
        Tilemap tm = GameManager.Instance.tilemap;
        Transform tmtf = tm.transform;
        int x = Mathf.FloorToInt(tilePos.x);
        int y = Mathf.FloorToInt(tilePos.y);
        Vector3Int loc = new Vector3Int(x, y, 0);

        TileBase tile = tm.GetTile(loc);
        if (tile == null) {
            tm = GameManager.Instance.semiSolidTilemap;
            tile = tm.GetTile(loc);
            if (tile == null) {
                return -1;
            }
        }

        if (tile is InteractableTile) {
            return ((InteractableTile) tile).Interact(this, (upwards ? InteractableTile.InteractionDirection.Down : InteractableTile.InteractionDirection.Up), Utils.TilemapToWorldPosition(loc)) ? 1 : 0;
        }
        return 0;
    }
    
    [PunRPC]
    void ModifyTilemap(int x, int y, string newtile) {
        Tilemap tm = GameManager.Instance.tilemap;
        Vector3Int loc = new Vector3Int(x,y,0);
        tm.SetTile(loc, (Tile) Resources.Load("Tilemaps/Tiles" + newtile));
    }

    public void PlaySoundFromAnim(string sound, float volume = 1) {
        audio.PlayOneShot((AudioClip) Resources.Load("Sound/" + sound), volume);
    }

    [PunRPC]
    void Knockback(bool fromRight, int starsToDrop, int attackerView) {
        if (hitInvincibilityCounter > 0) return;
        if (knockback) return;
        if (invincible > 0) return;
        knockback = true;
        PhotonView attacker = PhotonNetwork.GetPhotonView(attackerView);
        if (attacker) {
            if (attacker.gameObject.GetComponent<PlayerController>()) {
                //attacker is a player
                SpawnParticle("Prefabs/Particle/PlayerBounce", attacker.transform.position.x, attacker.transform.position.y);
            }
        }
        if (!photonView.IsMine) return;
        inShell = false;
        while (starsToDrop-- > 0) {
            SpawnStar();
        }
        body.velocity = new Vector2((fromRight ? -1 : 1) * 2 * (starsToDrop + 1), 0);
        facingRight = !fromRight;
        groundpound = false;
        flying = false;
        drill = false;
        body.gravityScale = normalGravity;
    }

    [PunRPC]
    void ResetKnockback() {
        hitInvincibilityCounter = 2f;
        knockback = false;
    }

    void FixedUpdate() {
        //game ended, freeze.
        
        if (GameManager.Instance) {
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
        }

        bool orig = facingRight;

        if (!dead) {
            HandleGroundCollision();
            HandleIce();
            if (photonView.IsMine)
                HandleMovement();

            tilesJumpedInto.Clear();
            tilesStandingOn.Clear();
            tilesHitSide.Clear();
        }
        HandleAnimation(orig);
    }

    void HandleIce() {
        //todo: refactor
        ice = false;
        foreach (Vector3Int pos in tilesStandingOn) {
            TileBase tile = GameManager.Instance.tilemap.GetTile(pos);
            if (!tile) continue;
            if (tile.name == "Ice") {
                ice = true;
                break;
            }
        }
    }

    void HandleDeathAnimation() {
        if (!dead) return;

        deathCounter += Time.fixedDeltaTime;
        if (deathCounter < deathUpTime) {
            deathUp = false;
            body.gravityScale = 0;
            body.velocity = Vector2.zero;
        } else {
            if (!deathUp && transform.position.y > GameManager.Instance.GetLevelMinY()) {
                body.velocity = new Vector2(0, deathForce);
                deathUp = true;
            }
            body.gravityScale = 1.2f;
            body.velocity = new Vector2(0, Mathf.Max(-deathForce, body.velocity.y));
            if (transform.position.y < GameManager.Instance.GetLevelMinY()) {
                transform.position = new Vector2(transform.position.x, GameManager.Instance.GetLevelMinY() - 1);
            }
        }

        if (photonView.IsMine && deathCounter >= 3f) {
            photonView.RPC("PreRespawn", RpcTarget.All);
        }
    }

    void HandleAnimation(bool orig) {
        //Dust particles
        if (photonView.IsMine) {

            //Facing direction
            bool right = joystick.x > analogDeadzone;
            bool left = joystick.x < -analogDeadzone;
            if (onGround) {
                if (body.velocity.x > 0.1) {
                    facingRight = true;
                } else if (body.velocity.x < -0.1) {
                    facingRight = false;
                }
            } else if (walljumping < 0 && !inShell) {
                if (right) {
                    facingRight = true;
                } else if (left) {
                    facingRight = false;
                }
            }

            if (!inShell && ((Mathf.Abs(body.velocity.x) < 0.5 && crouching) || ice)) {
                if (right) {
                    facingRight = true;
                }
                if (left) {
                    facingRight = false;
                }
            }

            //Animation
            animator.SetBool("skidding", !ice && skidding);
            animator.SetBool("turnaround", turnaround);
            animator.SetBool("onLeft", onLeft);
            animator.SetBool("onRight", onRight);
            animator.SetBool("onGround", onGround);
            animator.SetBool("invincible", invincible > 0);
            float animatedVelocity = Mathf.Abs(body.velocity.x);
            if (ice) {
                if (skidding) {
                    animatedVelocity = 3.5f;
                }
                if (iceSliding) {
                    animatedVelocity = 0f;
                }
            }
            animator.SetFloat("velocityX", animatedVelocity);
            animator.SetFloat("velocityY", body.velocity.y);
            animator.SetBool("doublejump", doublejump);
            animator.SetBool("triplejump", triplejump);
            animator.SetBool("crouching", crouching);
            animator.SetBool("groundpound", groundpound);
            animator.SetBool("holding", holding != null);
            animator.SetBool("knockback", knockback);
            animator.SetBool("pipe", pipeEntering != null);
            animator.SetBool("mini", state == PlayerState.Mini);
            animator.SetBool("mega", state == PlayerState.Giant);
            animator.SetBool("flying", flying);
            animator.SetBool("drill", drill);
            animator.SetBool("inShell", inShell || (state == PlayerState.Shell && (groundpound || crouching)));
            animator.SetBool("facingRight", facingRight);

            switch (state) {
            case PlayerState.Mini:
                transform.localScale = Vector3.one / 2;
                break;
            case PlayerState.Giant:
                transform.localScale = Vector3.one + (Vector3.one * (Mathf.Min(1, 1 - (giantStartTimer / giantStartTime)) * 3f));
                break;
            default:
                transform.localScale = Vector3.one;
                break;
            }

            transform.position = new Vector3(transform.position.x, transform.position.y, (pipeEntering == null ? -1 : 1));

        } else {
            onLeft = animator.GetBool("onLeft");
            onRight = animator.GetBool("onRight");
            onGround = animator.GetBool("onGround");
            skidding = animator.GetBool("skidding");
            turnaround = animator.GetBool("turnaround");
            crouching = animator.GetBool("crouching");
            invincible = animator.GetBool("invincible") ? 1f : 0f;
            flying = animator.GetBool("flying");
            drill = animator.GetBool("drill");
            inShell = animator.GetBool("inShell");
            // knockback = animator.GetBool("knockback");
            facingRight = animator.GetBool("facingRight");
        }

        if (animator.GetBool("pipe")) {
            gameObject.layer = LayerMask.NameToLayer("HitsNothing");
            transform.position = new Vector3(transform.position.x, transform.position.y, 1);
        } else if (dead) {
            gameObject.layer = LayerMask.NameToLayer("HitsNothing");
            transform.position = new Vector3(transform.position.x, transform.position.y, -1);
        } else if (knockback) {
            gameObject.layer = LayerMask.NameToLayer("Entity");
            transform.position = new Vector3(transform.position.x, transform.position.y, -1);
        } else {
            gameObject.layer = LayerMask.NameToLayer("Default");
            transform.position = new Vector3(transform.position.x, transform.position.y, -1);
        }

        if (dead || animator.GetBool("pipe")) {
            models.transform.eulerAngles = new Vector3(0,180,0);
        } else if (animator.GetBool("inShell") && !onSpinner) {
            models.transform.eulerAngles += (new Vector3(0, 1800 * (facingRight ? -1 : 1)) * Time.deltaTime) * (Mathf.Abs(body.velocity.x) / runningMaxSpeed);
        } else if (skidding && !turnaround) {
            if (facingRight) {
                models.transform.eulerAngles = new Vector3(0,-100,0);
            } else {
                models.transform.eulerAngles = new Vector3(0,100,0);
            }
        } else if ((holding != null || ice || !turnaround)) {
            if (onSpinner && onGround && Mathf.Abs(body.velocity.x) < 0.3f && !holding) {
                models.transform.eulerAngles += (new Vector3(0, -1800, 0) * Time.deltaTime);
            } else if (flying) {
                if (drill) {
                    models.transform.eulerAngles += (new Vector3(0, -2000, 0) * Time.deltaTime);
                } else {
                    models.transform.eulerAngles += (new Vector3(0, -1200, 0) * Time.deltaTime);
                }
            } else {
                if (facingRight) {
                    models.transform.eulerAngles = new Vector3(0,100,0);
                } else {
                    models.transform.eulerAngles = new Vector3(0,-100,0);
                }
            }
        }

        if (onLeft || onRight || (onGround && ((skidding && !ice) || (crouching && Mathf.Abs(body.velocity.x) > 1))) ) {
            if (!dust.isPlaying)
                dust.Play();
        } else {
            dust.Stop();
        }

        if (drill) {
            if (!drillParticle.isPlaying)
                drillParticle.Play();
        } else {
            if (drillParticle.isPlaying)
                drillParticle.Stop();
        }
    
        //Enable rainbow effect
        MaterialPropertyBlock block = new MaterialPropertyBlock(); 
        block.SetColor("GlowColor", glowColor);
        block.SetFloat("RainbowEnabled", (animator.GetBool("invincible") ? 1.1f : 0f));
        block.SetFloat("FireEnabled", (state == PlayerState.FireFlower ? 1.1f : 0f));
        foreach (MeshRenderer renderer in GetComponentsInChildren<MeshRenderer>()) {
            renderer.SetPropertyBlock(block);
        }
        foreach (SkinnedMeshRenderer renderer in GetComponentsInChildren<SkinnedMeshRenderer>()) {
            renderer.SetPropertyBlock(block);
        }
        if (invincible > 0) {
            if (!sparkles.isPlaying)
                sparkles.Play();
        } else {
            sparkles.Stop();
        }
        
        //Hitbox changing
        if (state == PlayerState.Shell || state == PlayerState.Large || state == PlayerState.FireFlower || state == PlayerState.Giant) {
            if (crouching || (invincible > 0 && !onGround) || inShell || groundpound) {
                bigHitbox.enabled = false;
                smolHitbox.enabled = true;
            } else {
                bigHitbox.enabled = true;
                smolHitbox.enabled = false;
            }
        } else {
            bigHitbox.enabled = false;
            smolHitbox.enabled = true;
        }

        //hit flash
        if (hitInvincibilityCounter >= 0) {
            hitInvincibilityCounter -= Time.fixedDeltaTime;
            
            bool invisible;
            if (hitInvincibilityCounter <= 0.75f) {
                invisible = ((hitInvincibilityCounter * 5f) % (blinkingSpeed*2f) < blinkingSpeed);
            } else {
                invisible = (hitInvincibilityCounter * 2f) % (blinkingSpeed*2) < blinkingSpeed;
            }
            models.SetActive(!invisible);
        } else {
            models.SetActive(true);
        }

        //Model changing
        bool large = state >= PlayerState.Large;

        largeMarioModel.SetActive(large);
        smallMarioModel.SetActive(!large);
        marioBlueShell.SetActive(state == PlayerState.Shell);
        animator.avatar = large ? largeMarioAvatar : smallMarioAvatar;

        HandleDeathAnimation();
        HandlePipeAnimation();

        if (photonView.IsMine) {
            HorizontalCamera.OFFSET_TARGET = (flying ? 0.75f : 0f);
            if (flying) {
                float percentage = Mathf.Abs(body.velocity.x) / walkingMaxSpeed;
                cameraController.targetOffset = (body.velocity.x > 0 ? cameraOffsetRight : cameraOffsetLeft) * percentage * 2f;
                cameraController.exactCentering = true;
            } else {
                cameraController.targetOffset = cameraOffsetZero;
                cameraController.exactCentering = false;
            }
        }
    }

    void FakeOnGroundCheck() {
        if ((onGroundLastFrame || (flying && body.velocity.y < 0)) && pipeEntering == null) {
            var hit = Physics2D.Raycast(transform.position, Vector2.down, 0.1f, ANY_GROUND_MASK);
            if (hit) {
                onGround = true;
                transform.position = new Vector2(body.position.x, hit.point.y);
                body.velocity = new Vector2(body.velocity.x, -1);
            }
        }
    }
    
    void HandlePipeAnimation() {
        if (!photonView.IsMine) return;
        if (!pipeEntering) {
            pipeTimer = 0;
            return;
        }

        body.isKinematic = true;
        body.velocity = pipeDirection;
            
        if (pipeTimer < pipeDuration / 2f && pipeTimer+Time.fixedDeltaTime >= pipeDuration / 2f) {
            //tp to other pipe
            if (pipeEntering.otherPipe.bottom == pipeEntering.bottom) {
                pipeDirection *= -1;
            }
            Vector2 offset = (pipeDirection * (pipeDuration / 2f));
            if (pipeEntering.otherPipe.bottom) {
                offset -= pipeDirection;
                offset.y -= (state == PlayerState.Small ? 0.5f : 0);
            }
            transform.position = new Vector2(pipeEntering.otherPipe.transform.position.x, pipeEntering.otherPipe.transform.position.y) - offset;
            photonView.RPC("PlaySound", RpcTarget.All, "player/pipe");
        }
        if (pipeTimer >= pipeDuration) {
            pipeEntering = null;
            body.isKinematic = false;
        }
        pipeTimer += Time.fixedDeltaTime;
    }

    void DownwardsPipeCheck() {
        if (!onGround) return;
        if (!crouching) return;
        if (inShell) return;
        if (knockback) return;
        if (state == PlayerState.Giant) return;

        foreach (RaycastHit2D hit in Physics2D.RaycastAll(transform.position, Vector2.down, 1f)) {
            GameObject obj = hit.transform.gameObject;
            if (obj.tag == "pipe") {
                //Enter pipe
                pipeEntering = obj.GetComponent<PipeManager>();
                pipeDirection = Vector2.down;

                body.velocity = Vector2.down;
                transform.position.Set(obj.transform.position.x, transform.position.y, 1);

                photonView.RPC("PlaySound", RpcTarget.All, "player/pipe");

                crouching = false;
                break;
            }
        }
    }

    void UpwardsPipeCheck() {
        bool uncrouch = joystick.y > analogDeadzone;
        if (!hitRoof) return;
        if (!uncrouch) return;
        if (state == PlayerState.Giant) return;

        //todo: change to nonalloc?
        foreach (RaycastHit2D hit in Physics2D.RaycastAll(transform.position, Vector2.up, 1f)) {
            GameObject obj = hit.transform.gameObject;
            if (obj.tag == "pipe") {
                //pipe found
                pipeEntering = obj.GetComponent<PipeManager>();
                pipeDirection = Vector2.up;

                body.velocity = Vector2.up;
                transform.position.Set(obj.transform.position.x, transform.position.y, 1);
                
                photonView.RPC("PlaySound", RpcTarget.All, "player/pipe");
                break;
            }
        }
    }
    
    void HandleCrouching(bool crouchInput) {
        if (state == PlayerState.Giant) {
            crouching = false;
            return;
        }
        bool prevCrouchState = crouching;
        crouching = ((onGround && crouchInput) || (!onGround && crouchInput && crouching) || (crouching && ForceCrouchCheck()));
        if (crouching && !prevCrouchState) {
            //crouch start sound
            photonView.RPC("PlaySound", RpcTarget.All, "player/crouch");
        }
    }

    bool ForceCrouchCheck() {
        if (state < PlayerState.Large) return false;
        float width = smolHitbox.bounds.extents.x;
        float height = smolHitbox.bounds.size.y*2f - 0.1f;
        Debug.DrawRay(transform.position + new Vector3(-width+0.05f,0.05f,0), Vector2.up * height, Color.magenta);
        Debug.DrawRay(transform.position + new Vector3(width-0.05f,0.05f,0), Vector2.up * height, Color.magenta);

        bool triggerState = Physics2D.queriesHitTriggers;
        Physics2D.queriesHitTriggers = false;
        bool ret = (Physics2D.Raycast(transform.position + new Vector3(-width+0.05f,0.05f,0), Vector2.up, height, ONLY_GROUND_MASK) 
            || Physics2D.Raycast(transform.position + new Vector3(width-0.05f,0.05f,0), Vector2.up, height, ONLY_GROUND_MASK));
        Physics2D.queriesHitTriggers = triggerState;
        return ret;
    }

    void HandleWallslide(bool leftWall, bool jump, bool holdingDirection) {
        triplejump = false;
        doublejump = false;
        singlejump = false;

        if (!holdingDirection) {
            transform.position += new Vector3(0.05f * (leftWall ? 1 : -1), 0, 0);
            onLeft = false;
            onRight = false;
        }

        body.velocity = new Vector2(0, Mathf.Max(body.velocity.y, wallslideSpeed));
        dust.transform.localPosition = new Vector3(0.075f * (leftWall ? 1 : -1), 0.075f * (state >= PlayerState.Large ? 4 : 1), dust.transform.localPosition.z);
            
        if (jump) {
            onLeft = false;
            onRight = false;
            body.velocity = new Vector2((runningMaxSpeed + walkingMaxSpeed)/2f * (leftWall ? 1 : -1), walljumpVelocity);
            walljumping = 0.5f;
            facingRight = leftWall;
            singlejump = true;
            doublejump = false;
            triplejump = false;
            onGround = false;
            onGroundLastFrame = false;
            photonView.RPC("PlaySound", RpcTarget.All, "player/walljump");
            if (Random.value < 0.5) {
                photonView.RPC("PlaySound", RpcTarget.All, "mario/walljump_1");
            } else {
                photonView.RPC("PlaySound", RpcTarget.All, "mario/walljump_2");
            }
        }
    }
    
    void HandleJumping(bool jump) {
        if (knockback) return;
        if (drill) return;
        if (groundpound) return;

        bool topSpeed = Mathf.Abs(body.velocity.x) + 0.1f > (runningMaxSpeed * (invincible > 0 ? 2 : 1));
        if (bounce || (jump && (onGround || koyoteTime < 0.2f))) {
            koyoteTime = 1;
            jumpBuffer = 0;
            skidding = false;
            turnaround = false;

            if (onSpinner && !inShell && !holding && !(crouching && state == PlayerState.Shell)) {
                photonView.RPC("PlaySound", RpcTarget.All, "mario/spinner_launch");
                photonView.RPC("PlaySound", RpcTarget.All, "player/spinner_launch");
                body.velocity = new Vector2(body.velocity.x, launchVelocity);
                flying = true;
                onGround = false;
                onGroundLastFrame = false;
                return;
            }

            float vel = Mathf.Max(jumpVelocity + Mathf.Abs(body.velocity.x)/8f);
            if (!flying && topSpeed && landing < 0.1f && !holding && !triplejump && !crouching && !inShell && invincible <= 0) {
                if (singlejump) {
                    //Double jump
                    photonView.RPC("PlaySound", RpcTarget.All, "mario/double_jump_" +  ((int) (Random.value * 2f) + 1));
                    singlejump = false;
                    doublejump = true;
                    triplejump = false;
                    body.velocity = new Vector2(body.velocity.x, vel);
                } else if (doublejump) {
                    //Triple jump
                    photonView.RPC("PlaySound", RpcTarget.All, "mario/triple_jump");
                    singlejump = false;
                    doublejump = false;
                    triplejump = true;
                    body.velocity = new Vector2(body.velocity.x, vel + 0.5f);
                } else {
                    //Normal jump
                    singlejump = true;
                    doublejump = false;
                    triplejump = false;
                    body.velocity = new Vector2(body.velocity.x, vel);
                }
            } else {
                //Normal jump
                singlejump = true;
                doublejump = false;
                triplejump = false;
                body.velocity = new Vector2(body.velocity.x, vel);
                if (!bounce) {
                    drill = false;
                    flying = false;
                }
            }
            if (!bounce) 
                photonView.RPC("PlaySound", RpcTarget.All, (state == PlayerState.Mini ? "player/jump_mini" : "player/jump"));
            bounce = false;
            onGround = false;
            onGroundLastFrame = false;
        }
    }

    void HandleWalkingRunning(bool left, bool right) {
        if (groundpound) return;
        if (pipeEntering) return;
        if (knockback) return;
        if (!(walljumping < 0 || onGround)) return;

        iceSliding = false;
        if (!left && !right) {
            skidding = false;
            turnaround = false;
            if (ice) {
                iceSliding = true;
            }
        }

        if (Mathf.Abs(body.velocity.x) < 0.5f || !onGround) {
            skidding = false;
            if (!left && !right) {
                turnaround = false;
            }
        }

        if (inShell) {
            body.velocity = new Vector2(runningMaxSpeed * (facingRight ? 1 : -1), body.velocity.y);
            return;
        }

        if (left && right) return;
        if (!(left || right)) return;

        float invincibleSpeedBoost = (invincible > 0 ? 2f : 1);
        float airPenalty = (onGround ? 1 : 0.5f);
        float xVel = body.velocity.x;
        float runSpeedTotal = runningMaxSpeed * invincibleSpeedBoost;
        float walkSpeedTotal = walkingMaxSpeed * invincibleSpeedBoost;
        bool reverseBonus = onGround && (((left && body.velocity.x > 0) || (right && body.velocity.x < 0)));
        float reverseFloat = (reverseBonus ? (ice ? icePenalty : 1.2f) : 1);
        float turnaroundSpeedBoost = (turnaround && !reverseBonus ? 2 : 1);
        float stationarySpeedBoost = Mathf.Abs(body.velocity.x) <= 0.005f ? 1f : 1f;

        if ((crouching && !onGround && state != PlayerState.Shell) || !crouching) {
            
            if (left) {
                if (running && !flying && xVel <= -(walkingMaxSpeed - 0.3f)) {
                    skidding = false;
                    turnaround = false;
                    if (xVel > -runSpeedTotal) {
                        float change = invincibleSpeedBoost * invincibleSpeedBoost * invincibleSpeedBoost * turnaroundSpeedBoost * runningAcceleration * airPenalty * stationarySpeedBoost * Time.fixedDeltaTime;    
                        body.velocity += new Vector2(change * -1, 0);
                    }
                } else {
                    if (xVel > -walkSpeedTotal) {
                        float change = invincibleSpeedBoost * reverseFloat * turnaroundSpeedBoost * walkingAcceleration * stationarySpeedBoost * Time.fixedDeltaTime;
                        body.velocity += new Vector2(change * -1, 0);
                        
                        if (state != PlayerState.Giant && reverseBonus && xVel > runSpeedTotal - 2) {
                            skidding = true;
                            turnaround = true;
                        }
                    }
                }
            }
            if (right) {
                if (running && !flying && xVel >= (walkingMaxSpeed - 0.3f)) {
                    skidding = false;
                    turnaround = false;
                    if (xVel < runSpeedTotal) {
                        float change = invincibleSpeedBoost * invincibleSpeedBoost * invincibleSpeedBoost * turnaroundSpeedBoost * runningAcceleration * airPenalty * stationarySpeedBoost * Time.fixedDeltaTime;
                        body.velocity += new Vector2(change * 1, 0);
                    }
                } else {
                    if (xVel < walkSpeedTotal) {
                        float change = invincibleSpeedBoost * reverseFloat * turnaroundSpeedBoost * walkingAcceleration * stationarySpeedBoost * Time.fixedDeltaTime;
                        body.velocity += new Vector2(change * 1, 0);

                        if (state != PlayerState.Giant && reverseBonus && xVel < -runSpeedTotal + 2) {
                            skidding = true;
                            turnaround = true;
                        }
                    }
                }
            }
        } else {
            turnaround = false;
            skidding = false;
        }
        if (skidding) {
            dust.transform.localPosition = Vector3.zero;
        }

        if (state == PlayerState.Shell && !inShell && onGround && running && !holding && Mathf.Abs(xVel)+0.25f >= runningMaxSpeed && landing > 0.33f) {
            inShell = true;
        }
        if (onGround) {
            body.velocity = new Vector2(body.velocity.x, Mathf.Sin(Mathf.Deg2Rad * floorAngle));
        }
    }

    void HandleMovement() {
        
        if (transform.position.y < GameManager.Instance.GetLevelMinY()) {
            //death via pit
            photonView.RPC("Death", RpcTarget.All, true);
            return;
        }

        bool paused = GameManager.Instance.paused;
        float delta = Time.fixedDeltaTime;

        if (!pipeEntering) invincible -= delta;
        throwInvincibility -= delta;
        jumpBuffer -= delta;
        walljumping -= delta;
        giantTimer -= delta;
        giantStartTimer -= delta;

        if (giantStartTimer > 0) {
            body.velocity = Vector2.zero;
            body.isKinematic = giantStartTimer-delta > 0;
            return;
        }

        if (state == PlayerState.Giant) {
            if (giantTimer <= 0) {
                state = PlayerState.Large;
                hitInvincibilityCounter = 3f;
                body.isKinematic = false;
                //todo: play shrink sfx
            } else {
                //destroy tiles
                HandleGiantTiles();
            }
        }

        //Pipes
        if (!paused) {
            DownwardsPipeCheck();
            UpwardsPipeCheck();
        }
        
        if (knockback) {
            body.velocity -= (body.velocity * (Time.fixedDeltaTime * 2f));
            if (photonView.IsMine && onGround && Mathf.Abs(body.velocity.x) < 0.05f) {
                photonView.RPC("ResetKnockback", RpcTarget.All);
            }
        }

        //activate blocks jumped into
        if (hitRoof) {
            foreach (Vector3 tile in tilesJumpedInto) {
                InteractWithTile(tile, false);
            }
        }

        bool origRunning = running;
        bool orig = facingRight;

        bool right = joystick.x > analogDeadzone && !paused;
        bool left = joystick.x < -analogDeadzone && !paused;
        bool crouch = joystick.y < -analogDeadzone && !paused;
        bool up = joystick.y > analogDeadzone && !paused;
        bool jump = (jumpBuffer > 0 && (onGround || koyoteTime < 0.1f || onLeft || onRight)) && !paused; 

        if (holding) {
            onLeft = false;
            onRight = false;
            holding.holderOffset = new Vector2((facingRight ? 1 : -1) * 0.25f, (state >= PlayerState.Large ? 0.5f : 0.25f));
        }
        
        //throwing held item
        if ((!running || state == PlayerState.Mini || state == PlayerState.Giant) && holding) {
            bool throwLeft = !facingRight;
            if (left) {
                throwLeft = true;
            }
            if (right) {
                throwLeft = false;
            }
            holding.photonView.RPC("Throw", RpcTarget.All, throwLeft, crouch);
            if (!crouch) {
                photonView.RPC("PlaySound", RpcTarget.All, "mario/walljump_2");
                throwInvincibility = 0.5f;
                animator.SetTrigger("throw");
            }
            holdingOld = holding;
            holding = null;
        }

        //blue shell enter/exit
        if (state != PlayerState.Shell || !running) {
            inShell = false;
        }
        if (inShell) {
            crouch = true;
            if (hitLeft || hitRight) {
                foreach (var tile in tilesHitSide) {
                    InteractWithTile(tile, false);
                }
                facingRight = hitLeft;
                photonView.RPC("PlaySound", RpcTarget.All, "player/block_bump");
            }
        }

        //Crouching
        HandleCrouching(crouch);
        if (crouching) {
            onLeft = false;
            onRight = false;
            dust.transform.localPosition = Vector3.zero;
        }

        if (onLeft) {
            HandleWallslide(true, jump, left);
        }
        if (onRight) {
            HandleWallslide(false, jump, right);
        }

        if ((walljumping < 0 || onGround) && !groundpound) {
            //Normal walking/running
            HandleWalkingRunning(left, right);

            //Jumping
            HandleJumping(jump);
        }
        
        if (crouch)
            HandleGroundpoundStart(left, right);
        HandleGroundpound(crouch, up);

        //Ground
        FakeOnGroundCheck();
        if (onGround) {
            if (hitRoof && crushGround && body.velocity.y <= 0.1) {
                //Crushed.
                photonView.RPC("Powerdown", RpcTarget.All, true);
            }
            koyoteTime = 0;
            onLeft = false;
            onRight = false;
            flying = false;
            if ((landing += delta) > 0.2f) {
                singlejump = false;
                doublejump = false;
                triplejump = false;
            }
        
            if (onSpinner && Mathf.Abs(body.velocity.x) < 0.3f && !holding) {
                Transform spnr = onSpinner.transform;
                if (transform.position.x > spnr.transform.position.x + 0.02f) {
                    transform.position -= (new Vector3(0.01f * 60f, 0, 0) * Time.fixedDeltaTime);
                } else if (transform.position.x < spnr.transform.position.x - 0.02f) {
                    transform.position += (new Vector3(0.01f * 60f, 0, 0) * Time.fixedDeltaTime);
                }
            }
        } else {
            koyoteTime += delta;
            landing = 0;
            skidding = false;
            turnaround = false;
            floorAngle = 0;
        }

        //slow-rise check
        if (flying) {
            body.gravityScale = flyingGravity;
        } else {
        float gravityModifier = (state != PlayerState.Mini ? 1f : 0.4f);
        if (body.velocity.y > 2.5) {
            if (jump || jumpHeld) {
                body.gravityScale = slowriseGravity;
            } else {
                body.gravityScale = normalGravity * 1.5f * gravityModifier;
            }
        } else if (!groundpoundSit && groundpound && !onGround) {
            body.gravityScale = 0f;
        } else {
            body.gravityScale = normalGravity * (gravityModifier / 1.2f);
        }
        }
        groundpoundCounter -= Time.fixedDeltaTime;

        if (!groundpoundSit && groundpound && (groundpoundCounter) <= 0) {
            body.velocity = new Vector2(0f, -groundpoundVelocity);
        }

        if (!inShell) {
            bool abovemax;
            float invincibleSpeedBoost = (invincible > 0 ? 2f : 1);
            float max = (running ? runningMaxSpeed : walkingMaxSpeed) * invincibleSpeedBoost;
            if (left && !crouching) {
                abovemax = body.velocity.x < -max; 
            } else if (right && !crouching) {
                abovemax = body.velocity.x > max;
            } else {
                abovemax = true;
            }
            //Friction...
            if (onGround && abovemax) {
                body.velocity *= 1-(Time.fixedDeltaTime * (ice ? 0.1f : 1f) * (knockback ? 1f : 4f));
                if (Mathf.Abs(body.velocity.x) < 0.05) {
                    body.velocity = new Vector2(0, body.velocity.y);
                }
            }
        }
        //Terminal velocity
        float terminalVelocityModifier = (state != PlayerState.Mini ? 1f : 0.65f);
        if (flying) {
            if (drill) {
                body.velocity = new Vector2(Mathf.Max(-1.5f, Mathf.Min(1.5f, body.velocity.x)), -drillVelocity);
            } else {
                body.velocity = new Vector2(Mathf.Max(-walkingMaxSpeed, Mathf.Min(walkingMaxSpeed, body.velocity.x)), Mathf.Max(body.velocity.y, -flyingTerminalVelocity));
            }
        } else if (!groundpound) { 
            body.velocity = new Vector2(body.velocity.x, Mathf.Max(body.velocity.y, terminalVelocity * terminalVelocityModifier));
        }
        if (!onGround) {
            body.velocity = new Vector2(Mathf.Max(-runningMaxSpeed, Mathf.Min(runningMaxSpeed, body.velocity.x)), body.velocity.y);
        }

        onGroundLastFrame = onGround;
    }

    void HandleGroundpoundStart(bool left, bool right) {
        if (onGround) return;
        if (!flying && (left || right)) return;
        if (groundpound || drill) return;
        if (holding) return;
        if (crouching) return;
        if (onLeft || onRight) return;

        if (flying) {
            //start drill
            if (body.velocity.y < 0) {
                drill = true;
                hitBlock = true;
            }
        } else {
            //start groundpound
            //check if high enough above ground
            if (Physics2D.Raycast(transform.position, Vector2.down, 0.5f, ANY_GROUND_MASK)) return;
            
            onLeft = false;
            onRight = false;
            groundpound = true;
            hitBlock = true;
            groundpoundSit = false;
            body.velocity = Vector2.zero;
            groundpoundCounter = groundpoundTime;
            photonView.RPC("PlaySound", RpcTarget.All, "player/groundpound");
        }
    }

    void HandleGroundpound(bool crouch, bool up) {
        if (onGround && (groundpound || drill) && hitBlock) {
            bool tempHitBlock = false;
            foreach (Vector3 tile in tilesStandingOn) {
                int temp = InteractWithTile(tile, true);
                if (temp != -1) {
                    tempHitBlock |= (temp == 1);
                }
            }
            hitBlock = tempHitBlock;
            if (drill) {
                flying = hitBlock;
                drill = hitBlock;
                if (hitBlock) {
                    onGround = false;
                    onGroundLastFrame = false;
                }
            } else {
                //groundpound
                groundpound = crouch;
                if (hitBlock) {
                    koyoteTime = 1;
                } else {
                    photonView.RPC("PlaySound", RpcTarget.All, "player/groundpound-landing" + (state == PlayerState.Mini ? "-mini" : ""));
                    photonView.RPC("SpawnParticle", RpcTarget.All, "Prefabs/Particle/GroundpoundDust");
                    groundpoundSit = true;
                }
            }
        }

        if (groundpound && ((up && !onGround) || (!crouch && onGround)) && groundpoundCounter <= 0) {
            groundpound = false;
            groundpoundSit = false;
            groundpoundCounter = 0.25f;
        }
    }

    public enum PlayerState {
        Small, Mini, Large, FireFlower, Shell, Giant
    }
}