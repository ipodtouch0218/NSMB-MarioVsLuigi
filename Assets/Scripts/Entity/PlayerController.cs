using System.Collections;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.InputSystem;
using Photon.Pun;

public class PlayerController : MonoBehaviourPun, IPunObservable {
    
    private static int ANY_GROUND_MASK, ONLY_GROUND_MASK, GROUND_LAYERID, HITS_NOTHING_LAYERID, ENTITY_HITBOX_LAYERID, DEFAULT_LAYERID = 0;
    
    private int playerId = 0;
    [SerializeField] public bool dead = false;
    [SerializeField] public Enums.PowerupState state = Enums.PowerupState.Small, previousState;
    [SerializeField] public float slowriseGravity = 0.85f, normalGravity = 2.5f, flyingGravity = 0.5f, flyingTerminalVelocity = -1f, drillVelocity = 9f, deathUpTime = 0.6f, deathForce = 7f, groundpoundTime = 0.25f, groundpoundVelocity = 12, blinkingSpeed = 0.25f, terminalVelocity = -7f, jumpVelocity = 6.25f, launchVelocity = 20f, walkingAcceleration = 8f, runningAcceleration = 3f, walkingMaxSpeed = 2.7f, runningMaxSpeed = 5, wallslideSpeed = -2f, walljumpVelocity = 6f, pipeDuration = 2f, giantStartTime = 1.5f, icePenalty = 0.4f, blinkDuration = 0.25f;
    [SerializeField] ParticleSystem dust, sparkles, drillParticle;
    private BoxCollider2D hitbox;
    GameObject models;
    private new AudioSource audio;
    private Animator animator;
    public Rigidbody2D body;

    public bool onGround, crushGround, onGroundLastFrame, onRight, onLeft, hitRoof, skidding, turnaround, facingRight = true, singlejump, doublejump, triplejump, bounce, crouching, groundpound, groundpoundSit, knockback, deathUp, hitBlock, running, jumpHeld, ice, flying, drill, inShell, hitLeft, hitRight, iceSliding, snow;
    float walljumping, landing, koyoteTime, deathCounter, groundpoundCounter, holdingDownTimer, hitInvincibilityCounter, powerupFlash, throwInvincibility, jumpBuffer, pipeTimer, giantStartTimer;
    public float invincible = 0, giantTimer = 0, blinkTimer = 0;
    
    private Vector2 pipeDirection;
    public int stars, coins;
    HashSet<Vector3Int> tilesStandingOn = new HashSet<Vector3Int>(), tilesJumpedInto = new HashSet<Vector3Int>(), tilesHitSide = new HashSet<Vector3Int>();
    public string storedPowerup = null;
    HoldableEntity holding, holdingOld;
    public Gradient glowGradient;
    [ColorUsage(true, false)]
    public Color glowColor = Color.clear;


    private float analogDeadzone = 0.35f;
    public Vector2 joystick;

    public GameObject smallModel, largeModel, blueShell;
    public Avatar smallAvatar, largeAvatar;
    public GameObject onSpinner;
    PipeManager pipeEntering;
    private CameraController cameraController;
    private Vector3 cameraOffsetLeft = Vector3.left, cameraOffsetRight = Vector3.right, cameraOffsetZero = Vector3.zero;
    private bool starDirection, step, alreadyGroundpounded;
    private Enums.PlayerEyeState eyeState;
    public PlayerData character;
    public float heightSmallModel = 0.46f, heightLargeModel = 0.82f;

    private long localFrameId = 0;
    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info) {
        if (stream.IsWriting) {

            stream.SendNext(body.position);
            stream.SendNext(body.velocity);
            
            ExitGames.Client.Photon.Hashtable controls = new ExitGames.Client.Photon.Hashtable();
            controls["joystick"] = joystick;
            controls["sprintHeld"] = running;
            controls["jumpHeld"] = jumpHeld;

            stream.SendNext(controls);
            stream.SendNext(localFrameId++);

        } else if (stream.IsReading) {
            Vector3 pos = (Vector3) stream.ReceiveNext();
            Vector2 vel = (Vector2) stream.ReceiveNext();
            ExitGames.Client.Photon.Hashtable controls = (ExitGames.Client.Photon.Hashtable) stream.ReceiveNext();
            long frameId = (long) stream.ReceiveNext();

            if (frameId < localFrameId) {
                //recevied info older than what we have
                return;
            }
            float lag = (float) (PhotonNetwork.Time - info.SentServerTime);

            // if (Vector3.Distance(pos, body.position) > 15 * lag) {
            //     Debug.Log("distance off by " + Vector3.Distance(pos, body.position));
            // }
            body.position = pos;
            body.velocity = vel;
            localFrameId = frameId;

            joystick = (Vector2) controls["joystick"]; 
            running = (bool) controls["sprintHeld"];
            jumpHeld = (bool) controls["jumpHeld"];

            // Debug.Log(lag + " -> " + (int) (lag*1000) + "ms");
            HandleMovement(lag);
            // body.position += (Vector3) (vel * lag);
        }
    }

    void Awake() {
        ANY_GROUND_MASK = LayerMask.GetMask("Ground", "Semisolids");
        ONLY_GROUND_MASK = LayerMask.GetMask("Ground");
        GROUND_LAYERID = LayerMask.NameToLayer("Ground");
        HITS_NOTHING_LAYERID = LayerMask.NameToLayer("HitsNothing");
        ENTITY_HITBOX_LAYERID = LayerMask.NameToLayer("EntityHitbox");
        
        cameraController = Camera.main.GetComponent<CameraController>();
        animator = GetComponentInChildren<Animator>();
        body = GetComponent<Rigidbody2D>();
        audio = GetComponent<AudioSource>();
        hitbox = GetComponent<BoxCollider2D>();
        models = transform.Find("Models").gameObject;
        starDirection = Random.value < 0.5;
        PlayerInput input = GetComponent<PlayerInput>();
        input.enabled = !photonView || photonView.IsMine;

        smallModel.SetActive(false);
        largeModel.SetActive(false);

        if (photonView) {
            playerId = System.Array.IndexOf(PhotonNetwork.PlayerList, photonView.Owner);
            if (!photonView.IsMine) {
                float samplePos = (float) playerId / (float) PhotonNetwork.PlayerList.Length;
                glowColor = glowGradient.Evaluate(samplePos);
            }
        }
    }

    void HandleGroundCollision() {
        tilesJumpedInto.Clear();
        tilesStandingOn.Clear();
        tilesHitSide.Clear();

        bool ignoreRoof = false;
        int down = 0, left = 0, right = 0, up = 0;
        float blockRoofY = 0;

        Tilemap tilemap = GameManager.Instance.tilemap;
        Transform tmtf = tilemap.transform;

        int collisionCount = 0;
        ContactPoint2D[] contacts = new ContactPoint2D[20];
        collisionCount = hitbox.GetContacts(contacts);

        float highestAngleThisFrame = 0;
        crushGround = false;
        for (int i = 0; i < collisionCount; i++) {
            ContactPoint2D contact = contacts[i];
            Vector2 n = contact.normal;
            Vector2 p = contact.point + (contact.normal * -0.15f);
            if (Vector2.Dot(n,Vector2.up) > .5f) {
                Vector2 modifiedVec = p + (new Vector2(0.01f, 0) * (p.x - body.position.x < 0 ? 1 : -1)); 
                Vector3Int vec = Utils.WorldToTilemapPosition(modifiedVec);
                float playerY = Utils.WorldToTilemapPosition(body.position + new Vector2(0, 0.25f)).y;
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
                    Vector2 modifiedVec = p + (new Vector2(0, (p.y - body.position.y > 0.2f ? -0.05f : 0.05f)));
                    Vector3Int vec = Utils.WorldToTilemapPosition(modifiedVec + new Vector2(0.1f, 0));
                    tilesHitSide.Add(vec);
                } else if (Vector2.Dot(n,Vector2.right) > .9f) {
                    left++;
                    Vector2 modifiedVec = p + (new Vector2(0, (p.y - body.position.y > 0.2f ? -0.05f : 0.05f))); 
                    Vector3Int vec = Utils.WorldToTilemapPosition(modifiedVec + new Vector2(-0.1f, 0));
                    tilesHitSide.Add(vec);
                } else if (Vector2.Dot(n,Vector2.down) > .9f && !groundpound) {
                    up++;
                    Vector2 modifiedVec = p + (new Vector2(0.01f, 0) * (p.x - body.position.x < 0 ? 1 : -1)); 
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
                // int xMax = Mathf.Max(tilesStandingOn[0].x, tilesStandingOn[1].x);
                // int xMin = Mathf.Min(tilesStandingOn[0].x, tilesStandingOn[1].x);

                // for (int temp = xMin; temp < xMax; temp++) {
                //     tilesStandingOn.Add(new Vector3Int(temp, tilesStandingOn[0].y, 0));
                // }
            }
        }
        hitLeft = left >= 2;
        onLeft = hitLeft && !inShell && body.velocity.y < -0.1 && !facingRight && !onGround && !holding && state != Enums.PowerupState.Giant && !flying;
        hitRight = right >= 2;
        onRight = hitRight && !inShell && body.velocity.y < -0.1 && facingRight && !onGround && !holding && state != Enums.PowerupState.Giant && !flying;
        hitRoof = !ignoreRoof && !onLeft && !onRight && up >= 2 && body.velocity.y > -0.2f;

        if ((left >= 2 || right >= 2) && tilesHitSide.Count >= 2) {
            // int yMax = Mathf.Max(tilesHitSide[0].y, tilesHitSide[1].y);
            // int yMin = Mathf.Min(tilesHitSide[0].y, tilesHitSide[1].y);

            // for (int temp = yMin; temp < yMax; temp++) {
            //     tilesHitSide.Add(new Vector3Int(tilesHitSide[0].x, temp, 0));
            // }
        }
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
                    if (state == Enums.PowerupState.Mini) {
                        if (!groundpound) {
                            photonView.RPC("PlaySound", RpcTarget.All, "enemy/shell_kick");
                            return;
                        }
                    }
                    otherView.RPC("Knockback", RpcTarget.All, otherObj.transform.position.x < body.position.x, groundpound && state != Enums.PowerupState.Mini ? 2 : 1, photonView.ViewID);
                    return;
                }

                if (state == Enums.PowerupState.Shell && inShell) {
                    if (other.inShell) {
                        otherView.RPC("Knockback", RpcTarget.All, otherObj.transform.position.x < body.position.x, 0, -1);
                        photonView.RPC("Knockback", RpcTarget.All, otherObj.transform.position.x > body.position.x, 0, -1);
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
        if (state == Enums.PowerupState.Giant)
            return;
        if (ice && skidding) {
            PlaySoundFromAnim("player/ice-skid");
            return;
        }
        if (Mathf.Abs(body.velocity.x) < walkingMaxSpeed)
            return;
        
        PlaySoundFromAnim("player/walk" + (snow ? "-snow" : "") + (step ? "-2" : ""), Mathf.Abs(body.velocity.x) / (runningMaxSpeed + 4));
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
        if (state != Enums.PowerupState.FireFlower) return;
        if (dead || triplejump || holding || flying || drill) return;
        if (GameManager.Instance.gameover) return;
        if (pipeEntering) return;

        int count = 0;
        foreach (FireballMover existingFire in GameObject.FindObjectsOfType<FireballMover>()) {
            if (existingFire.photonView.IsMine) {
                if (++count >= 2) 
                    return;
            }
        }

        PhotonNetwork.InstantiateRoomObject("Prefabs/Fireball", body.position + new Vector2(facingRight ? 0.3f : -0.3f, 0.4f), Quaternion.identity, 0, new object[]{!facingRight});
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
        PlaySoundFromAnim("pause");
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
    void Powerup(int actor, string powerup, int powerupViewId) {
        if (PhotonView.Find(actor) == null)
            return;
        bool stateUp = false;
        Enums.PowerupState previous = state;
        string store = null;
        switch (powerup) {
        case "Mushroom": {
            if (state == Enums.PowerupState.Small || state == Enums.PowerupState.Mini) {
                state = Enums.PowerupState.Large;
                stateUp = true;
                transform.localScale = Vector3.one;
            } else if (storedPowerup == null || storedPowerup == "") {
                store = powerup;
            }
            break;
        }
        case "FireFlower": {
            if (state != Enums.PowerupState.Giant && state != Enums.PowerupState.FireFlower) {
                state = Enums.PowerupState.FireFlower;
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
            if (state == Enums.PowerupState.Mini || state == Enums.PowerupState.Giant) {
                store = powerup;
            } else {
                stateUp = true;
                state = Enums.PowerupState.Mini;
                transform.localScale = Vector3.one / 2;
            }
            break;
        }
        case "BlueShell": {
            if (state == Enums.PowerupState.Shell || state == Enums.PowerupState.Giant) {
                store = powerup;
            } else {
                stateUp = true;
                state = Enums.PowerupState.Shell;
                transform.localScale = Vector3.one;
            }
            break;
        }
        case "MegaMushroom": {
            if (state == Enums.PowerupState.Giant) {
                store = powerup;
            } else {
                state = Enums.PowerupState.Giant;
                stateUp = true;
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
            if (powerup == "MiniMushroom") {
                PlaySoundFromAnim("player/powerup-mini");
            } else if (powerup == "MegaMushroom") {
                PlaySoundFromAnim("player/powerup-mega");
            } else {
                PlaySoundFromAnim("player/powerup");
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
        case Enums.PowerupState.Mini:
        case Enums.PowerupState.Small: {
            Death(false);
            break;
        }
        case Enums.PowerupState.Large: {
            state = Enums.PowerupState.Small;
            powerupFlash = 2f;
            SpawnStar();
            break;
        }
        case Enums.PowerupState.FireFlower:
        case Enums.PowerupState.Shell: {
            state = Enums.PowerupState.Large;
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

        Vector2 dir = (body.position - (Vector2) collider.transform.position);
        dir.Normalize();
        bool downwards = Vector2.Dot(dir, Vector2.up) > 0.5f;
        switch (collider.tag) {
            case "goomba": {
                if (!photonView.IsMine) return;
                GoombaWalk goomba = collider.gameObject.GetComponentInParent<GoombaWalk>();
                if (goomba.dead)
                    break;
                if (inShell || invincible > 0 || ((groundpound || drill) && state != Enums.PowerupState.Mini && downwards) || state == Enums.PowerupState.Giant) {
                    collider.gameObject.transform.parent.gameObject.GetPhotonView().RPC("SpecialKill", RpcTarget.All, body.velocity.x > 0, groundpound);
                } else if (downwards) {
                    if (groundpound || state != Enums.PowerupState.Mini) {
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
                if (inShell || invincible > 0 || (groundpound && state != Enums.PowerupState.Mini && downwards) || state == Enums.PowerupState.Giant) {
                    bullet.photonView.RPC("SpecialKill", RpcTarget.All, body.velocity.x > 0, groundpound && state != Enums.PowerupState.Mini);
                } else if (downwards) {
                    if (groundpound || drill || state != Enums.PowerupState.Mini) {
                        bullet.photonView.RPC("SpecialKill", RpcTarget.All, body.velocity.x > 0, groundpound && state != Enums.PowerupState.Mini);
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
                if (inShell || invincible > 0 || state == Enums.PowerupState.Giant) {
                    koopa.photonView.RPC("SpecialKill", RpcTarget.All, !facingRight, false);
                } else if (groundpound && state != Enums.PowerupState.Mini && downwards) {
                    koopa.photonView.RPC("EnterShell", RpcTarget.All);
                    if (!koopa.blue) {
                        koopa.photonView.RPC("Kick", RpcTarget.All, body.position.x < koopa.transform.position.x, groundpound);
                        holdingOld = koopa;
                        throwInvincibility = 0.5f;
                    }
                } else if (downwards && (!koopa.shell || !koopa.IsStationary())) {
                    if (state != Enums.PowerupState.Mini || groundpound) {
                        koopa.photonView.RPC("EnterShell", RpcTarget.All);
                        if (state == Enums.PowerupState.Mini)
                            groundpound = false;
                    }
                    photonView.RPC("PlaySound", RpcTarget.All, "enemy/goomba");
                    bounce = true;
                } else {
                    if (koopa.shell && (koopa.IsStationary())) {
                        if (state != Enums.PowerupState.Mini && !holding && running && !flying && !crouching && !dead && !onLeft && !onRight && !doublejump && !triplejump) {
                            koopa.photonView.RPC("Pickup", RpcTarget.All, photonView.ViewID);
                            holding = koopa;
                        } else {
                            koopa.photonView.RPC("Kick", RpcTarget.All, body.position.x < koopa.transform.position.x, groundpound);
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
                    if (state != Enums.PowerupState.Mini || (groundpound && downwards)) {
                        bomb.photonView.RPC("Light", RpcTarget.All);
                    }
                    photonView.RPC("PlaySound", RpcTarget.All, "enemy/goomba");
                    if (groundpound) {
                        bomb.photonView.RPC("Kick", RpcTarget.All, body.position.x < bomb.transform.position.x, groundpound);
                    } else {
                        bounce = true;
                    }
                } else {
                    if (bomb.lit) {
                        if (state != Enums.PowerupState.Mini && !holding && running && !crouching && !flying && !dead && !onLeft && !onRight && !doublejump && !triplejump && !groundpound) {
                            bomb.photonView.RPC("Pickup", RpcTarget.All, photonView.ViewID);
                            holding = bomb;
                        } else {
                            bomb.photonView.RPC("Kick", RpcTarget.All, body.position.x < bomb.transform.position.x, groundpound);
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
                photonView.RPC("CollectBigStar", RpcTarget.AllViaServer, collider.gameObject.transform.parent.gameObject.GetPhotonView().ViewID);
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
                photonView.RPC("Powerup", RpcTarget.AllViaServer, powerup.photonView.ViewID, collider.gameObject.tag, collider.gameObject.transform.parent.gameObject.GetPhotonView().ViewID);
                break;
            }
            case "Fireball": {
                if (!photonView.IsMine) return;
                FireballMover fireball = collider.gameObject.GetComponentInParent<FireballMover>();
                if (fireball.photonView.IsMine)
                    break;
                fireball.photonView.RPC("Kill", RpcTarget.All);
                if (state == Enums.PowerupState.Shell && (inShell || crouching || groundpound))
                    break;
                if (state == Enums.PowerupState.Mini) {
                    photonView.RPC("Powerdown", RpcTarget.All, false);
                } else {
                    photonView.RPC("Knockback", RpcTarget.All, collider.attachedRigidbody.position.x > body.position.x, 1, fireball.photonView.ViewID);
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

            if (rand < 0.1f) {
                //10% chance for mega mushroom
                item = "MegaMushroom";
            } else if (rand < 0.2f) {
                //10% chance for star
                item = "Star";
            } else if (rand < 0.35f) {
                //15% chance for a mini mushroom
                item = "MiniMushroom";
            } else if (rand < 0.5f) {
                //15% chance for a blue shell
                item = "BlueShell";
            } else if (rand < 0.75f) {
                //25% chance for a fire flower
                item = "FireFlower";
            } else {
                //25% chance for a mushroom
                item = "Mushroom";
            }
        }

        PhotonNetwork.InstantiateRoomObject("Prefabs/Powerup/" + item, body.position + new Vector2(0, 5), Quaternion.identity, 0, new object[]{photonView.ViewID});
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
        knockback = false;
        animator.SetTrigger("dead");
        PlaySoundFromAnim("player/death");
        SpawnStar();
        if (holding) {
            PhotonNetwork.Destroy(holding.photonView);
            holding = null;
        }
        if (deathplane && photonView.IsMine) {
            transform.Translate(0, -20, 0);
        }
    }

    void SpawnStar() {
        if (stars <= 0) return;
        stars--;
        if (!photonView.IsMine) return;
        
        GameObject star = PhotonNetwork.InstantiateRoomObject("Prefabs/BigStar", transform.position, Quaternion.identity, 0, new object[]{starDirection});
        StarBouncer sb = star.GetComponent<StarBouncer>();
        sb.photonView.TransferOwnership(PhotonNetwork.MasterClient);
        photonView.RPC("SetStars", RpcTarget.Others, stars);
        starDirection = !starDirection;
    }

    [PunRPC]
    public void PreRespawn() {
        transform.position = GameManager.Instance.GetSpawnpoint(playerId);
        state = Enums.PowerupState.Small;
        dead = false;
        animator.SetTrigger("respawn");

        GameObject particle = (GameObject) GameObject.Instantiate(Resources.Load("Prefabs/Particle/Respawn"), body.position, Quaternion.identity);
        if (photonView.IsMine) {
            particle.GetComponent<RespawnParticle>().player = this;
        }
        gameObject.SetActive(false);
    }

    [PunRPC]
    public void Respawn() {
        gameObject.SetActive(true);
        dead = false;
        state = Enums.PowerupState.Small;
        previousState = Enums.PowerupState.Small;
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
        ResetKnockback();
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
        Transform tmtf = GameManager.Instance.tilemap.transform;
        float lsX = tmtf.localScale.x;
        float lsY = tmtf.localScale.y;
        float tmX = tmtf.position.x;
        float tmY = tmtf.position.y;

        float posX = body.position.x;
        float posY = body.position.y - 0.4f;
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

                InteractWithTile(new Vector3(relX, relY), false);
            }
        } 
        if (landing) {
            singlejump = false;
        }
    }

    int InteractWithTile(Vector3 tilePos, bool upwards) {
        if (!photonView.IsMine) return 0;
        Tilemap tm = GameManager.Instance.tilemap;
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
        if ((fromRight && Physics2D.Raycast(body.position + new Vector2(0, 0.2f), Vector2.left, 0.3f, ONLY_GROUND_MASK)) ||
            (!fromRight && Physics2D.Raycast(body.position + new Vector2(0, 0.2f), Vector2.right, 0.3f, ONLY_GROUND_MASK))) {
            
            fromRight = !fromRight;
        }
        body.velocity = new Vector2((fromRight ? -1 : 1) * 3 * (starsToDrop + 1), 0);
        inShell = false;
        facingRight = !fromRight;
        groundpound = false;
        flying = false;
        drill = false;
        body.gravityScale = normalGravity;
        if (!photonView.IsMine) return;
        while (starsToDrop-- > 0) {
            SpawnStar();
        }
    }

    [PunRPC]
    void ResetKnockback() {
        hitInvincibilityCounter = 2f;
        bounce = false;
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
            HandleTemporaryInvincibility();
            HandleGroundCollision();
            HandleIce();
            HandleMovement(Time.fixedDeltaTime);
        }
        HandleAnimation(orig);
    }

    bool colliding = true;
    void HandleTemporaryInvincibility() {
        bool shouldntCollide = (hitInvincibilityCounter > 0) || knockback;
        if (shouldntCollide && colliding) {
            colliding = false;
            foreach (var player in GameManager.Instance.allPlayers) {
                Physics2D.IgnoreCollision(hitbox, player.hitbox, true);
            }
        } else if (!shouldntCollide && !colliding) {
            colliding = true;
            foreach (var player in GameManager.Instance.allPlayers) {
                Physics2D.IgnoreCollision(hitbox, player.hitbox, false);
            }
        }
    }

    void HandleIce() {
        //todo: refactor
        ice = false;
        snow = false;
        foreach (Vector3Int pos in tilesStandingOn) {
            TileBase tile = GameManager.Instance.tilemap.GetTile(pos);
            if (!tile) continue;
            if (tile.name == "SnowGrass") {
                snow = true;
            }
            if (tile.name == "Ice") {
                ice = true;
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
            if (!deathUp && body.position.y > GameManager.Instance.GetLevelMinY()) {
                body.velocity = new Vector2(0, deathForce);
                deathUp = true;
            }
            body.gravityScale = 1.2f;
            body.velocity = new Vector2(0, Mathf.Max(-deathForce, body.velocity.y));
            if (body.position.y < GameManager.Instance.GetLevelMinY()) {
                body.position = new Vector2(transform.position.x, GameManager.Instance.GetLevelMinY() - 1);
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
            animator.SetBool("mini", state == Enums.PowerupState.Mini);
            animator.SetBool("mega", state == Enums.PowerupState.Giant);
            animator.SetBool("flying", flying);
            animator.SetBool("drill", drill);
            animator.SetBool("inShell", inShell || (state == Enums.PowerupState.Shell && (groundpound || crouching)));
            animator.SetBool("facingRight", facingRight);

            switch (state) {
            case Enums.PowerupState.Mini:
                transform.localScale = Vector3.one / 2;
                break;
            case Enums.PowerupState.Giant:
                transform.localScale = Vector3.one + (Vector3.one * (Mathf.Min(1, 1 - (giantStartTimer / giantStartTime)) * 3f));
                break;
            default:
                transform.localScale = Vector3.one;
                break;
            }

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
            if (gameObject.layer != HITS_NOTHING_LAYERID) {
                gameObject.layer = HITS_NOTHING_LAYERID;
                body.position = new Vector3(body.position.x, body.position.y, 1);
            }
        } else if (dead) {
            if (gameObject.layer != HITS_NOTHING_LAYERID) {
                gameObject.layer = HITS_NOTHING_LAYERID;
                body.position = new Vector3(body.position.x, body.position.y, -3);
            }
        } else {
            gameObject.layer = DEFAULT_LAYERID;
            body.position = new Vector3(body.position.x, body.position.y, -3);
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

        //Blinking
        if (dead) {
            eyeState = Enums.PlayerEyeState.Death;
        } else {
            if ((blinkTimer -= Time.fixedDeltaTime) < 0) {
                blinkTimer = 3f + (Random.value * 2f);
            }
            if (blinkTimer < blinkDuration) {
                eyeState = Enums.PlayerEyeState.HalfBlink;
            } else if (blinkTimer < blinkDuration*2f) {
                eyeState = Enums.PlayerEyeState.FullBlink;
            } else if (blinkTimer < blinkDuration*3f) {
                eyeState = Enums.PlayerEyeState.HalfBlink;
            } else {
                eyeState = Enums.PlayerEyeState.Normal;
            }
        }
    
        //Enable rainbow effect
        MaterialPropertyBlock block = new MaterialPropertyBlock(); 
        block.SetColor("GlowColor", glowColor);
        block.SetFloat("RainbowEnabled", (animator.GetBool("invincible") ? 1.1f : 0f));
        block.SetFloat("FireEnabled", (state == Enums.PowerupState.FireFlower ? 1.1f : 0f));
        block.SetFloat("EyeState", (int) eyeState);
        block.SetFloat("ModelScale", transform.lossyScale.x);
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
        UpdateHitbox();

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
        bool large = state >= Enums.PowerupState.Large;

        largeModel.SetActive(large);
        smallModel.SetActive(!large);
        blueShell.SetActive(state == Enums.PowerupState.Shell);
        animator.avatar = large ? largeAvatar : smallAvatar;

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

    void UpdateHitbox() {
        float width = hitbox.size.x;
        float height = 0;

        if (state <= Enums.PowerupState.Small || (invincible  > 0 && !onGround && !crouching) || groundpound || inShell) {
            height = heightSmallModel;
        } else {
            height = heightLargeModel;
        }

        if (crouching) {
            height *= 0.7f;
        }

        hitbox.size = new Vector2(width, height);
        hitbox.offset = new Vector2(0, height/2f);
        // Debug.Log(hitbox);
    }

    void FakeOnGroundCheck() {
        if ((onGroundLastFrame || (flying && body.velocity.y < 0)) && pipeEntering == null && !onGround) {
            var hit = Physics2D.Raycast(body.position, Vector2.down, 0.1f, ANY_GROUND_MASK);
            if (hit) {
                Debug.Log("fake on ground hit");
                onGround = true;
                body.position = new Vector2(body.position.x, hit.point.y);
                body.velocity = new Vector2(body.velocity.x, 0);
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
                offset.y -= (state <= Enums.PowerupState.Small ? 0.5f : 0);
            }
            body.position = new Vector3(pipeEntering.otherPipe.transform.position.x, pipeEntering.otherPipe.transform.position.y, transform.position.z) - (Vector3) offset;
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
        if (state == Enums.PowerupState.Giant) return;

        foreach (RaycastHit2D hit in Physics2D.RaycastAll(body.position, Vector2.down, 1f)) {
            GameObject obj = hit.transform.gameObject;
            if (obj.tag != "pipe") continue;
            PipeManager pipe = obj.GetComponent<PipeManager>();
            if (pipe.miniOnly && state != Enums.PowerupState.Mini) continue;
            
            //Enter pipe
            pipeEntering = pipe;
            pipeDirection = Vector2.down;

            body.velocity = Vector2.down;
            body.position = new Vector2(obj.transform.position.x, transform.position.y);

            photonView.RPC("PlaySound", RpcTarget.All, "player/pipe");

            crouching = false;
            break;
        }
    }

    void UpwardsPipeCheck() {
        bool uncrouch = joystick.y > analogDeadzone;
        if (!hitRoof) return;
        if (!uncrouch) return;
        if (state == Enums.PowerupState.Giant) return;

        //todo: change to nonalloc?
        foreach (RaycastHit2D hit in Physics2D.RaycastAll(body.position, Vector2.up, 1f)) {
            GameObject obj = hit.transform.gameObject;
            if (obj.tag != "pipe") continue;
            PipeManager pipe = obj.GetComponent<PipeManager>();
            if (pipe.miniOnly && state != Enums.PowerupState.Mini) continue;

            //pipe found
            pipeEntering = pipe;
            pipeDirection = Vector2.up;

            body.velocity = Vector2.up;
            body.position = new Vector2(obj.transform.position.x, transform.position.y);
                
            photonView.RPC("PlaySound", RpcTarget.All, "player/pipe");
            break;
        }
    }
    
    void HandleCrouching(bool crouchInput) {
        if (state == Enums.PowerupState.Giant) {
            crouching = false;
            return;
        }
        bool prevCrouchState = crouching;
        crouching = ((onGround && crouchInput) || (!onGround && crouchInput && crouching) || (crouching && ForceCrouchCheck()));
        if (crouching && !prevCrouchState) {
            //crouch start sound
            PlaySoundFromAnim("player/crouch");
        }
    }

    bool ForceCrouchCheck() {
        if (state < Enums.PowerupState.Large) return false;
        float width = hitbox.bounds.extents.x;
        float height = hitbox.bounds.size.y*2f - 0.1f;
        Debug.DrawRay(body.position + new Vector2(-width+0.05f,0.05f), Vector2.up * height, Color.magenta);
        Debug.DrawRay(body.position + new Vector2(width-0.05f,0.05f), Vector2.up * height, Color.magenta);

        bool triggerState = Physics2D.queriesHitTriggers;
        Physics2D.queriesHitTriggers = false;
        bool ret = (Physics2D.Raycast(body.position + new Vector2(-width+0.05f,0.05f), Vector2.up, height, ONLY_GROUND_MASK) 
            || Physics2D.Raycast(body.position + new Vector2(width-0.05f,0.05f), Vector2.up, height, ONLY_GROUND_MASK));
        Physics2D.queriesHitTriggers = triggerState;
        return ret;
    }

    void HandleWallslide(bool leftWall, bool jump, bool holdingDirection) {
        triplejump = false;
        doublejump = false;
        singlejump = false;

        if (!holdingDirection) {
            body.position += new Vector2(0.05f * (leftWall ? 1 : -1), 0);
            onLeft = false;
            onRight = false;
        }

        body.velocity = new Vector2(0, Mathf.Max(body.velocity.y, wallslideSpeed));
        dust.transform.localPosition = new Vector3(0.075f * (leftWall ? 1 : -1), 0.075f * (state >= Enums.PowerupState.Large ? 4 : 1), dust.transform.localPosition.z);
            
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
                photonView.RPC("PlaySound", RpcTarget.All, character.soundFolder + "/walljump_1");
            } else {
                photonView.RPC("PlaySound", RpcTarget.All, character.soundFolder + "/walljump_2");
            }
        }
    }
    
    void HandleJumping(bool jump) {
        if (knockback) return;
        if (drill) return;
        if (groundpound) return;
        if (groundpoundCounter > 0) return;

        bool topSpeed = Mathf.Abs(body.velocity.x) + 0.1f > (runningMaxSpeed * (invincible > 0 ? 2 : 1));
        if (bounce || (jump && (onGround || koyoteTime < 0.2f))) {
            koyoteTime = 1;
            jumpBuffer = 0;
            skidding = false;
            turnaround = false;

            if (onSpinner && !inShell && !holding && !(crouching && state == Enums.PowerupState.Shell)) {
                photonView.RPC("PlaySound", RpcTarget.All, character.soundFolder + "/spinner_launch");
                photonView.RPC("PlaySound", RpcTarget.All, "player/spinner_launch");
                body.velocity = new Vector2(body.velocity.x, launchVelocity);
                flying = true;
                onGround = false;
                onGroundLastFrame = false;
                return;
            }

            float vel = Mathf.Max(jumpVelocity + Mathf.Abs(body.velocity.x)/8f);
            if (!flying && topSpeed && landing < 0.1f && !holding && !triplejump && !crouching && !inShell && invincible <= 0) {
                bool canSpecialJump = !Physics2D.Raycast(body.position + new Vector2(0, 0.1f), Vector2.up, 1f, ONLY_GROUND_MASK);
                if (singlejump && canSpecialJump) {
                    //Double jump
                    photonView.RPC("PlaySound", RpcTarget.All, character.soundFolder + "/double_jump_" +  ((int) (Random.value * 2f) + 1));
                    singlejump = false;
                    doublejump = true;
                    triplejump = false;
                    body.velocity = new Vector2(body.velocity.x, vel);
                } else if (doublejump && canSpecialJump) {
                    //Triple jump
                    photonView.RPC("PlaySound", RpcTarget.All, character.soundFolder + "/triple_jump");
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
                photonView.RPC("PlaySound", RpcTarget.All, (state == Enums.PowerupState.Mini ? "player/jump_mini" : "player/jump"));
            bounce = false;
            onGround = false;
            onGroundLastFrame = false;
        }
    }

    void HandleWalkingRunning(bool left, bool right) {
        if (groundpound) return;
        if (pipeEntering) return;
        if (knockback) return;
        if (groundpoundCounter > 0) return;
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

        if ((left && right) || !(left || right)) return;

        float invincibleSpeedBoost = (invincible > 0 ? 2f : 1);
        float airPenalty = (onGround ? 1 : 0.5f);
        float xVel = body.velocity.x;
        float runSpeedTotal = runningMaxSpeed * invincibleSpeedBoost;
        float walkSpeedTotal = walkingMaxSpeed * invincibleSpeedBoost;
        bool reverseBonus = onGround && (((left && body.velocity.x > 0) || (right && body.velocity.x < 0)));
        float reverseFloat = (reverseBonus ? (ice ? icePenalty : 1.2f) : 1);
        float turnaroundSpeedBoost = (turnaround && !reverseBonus ? 2 : 1);
        float stationarySpeedBoost = Mathf.Abs(body.velocity.x) <= 0.005f ? 1f : 1f;

        if ((crouching && !onGround && state != Enums.PowerupState.Shell) || !crouching) {
            
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
                        
                        if (state != Enums.PowerupState.Giant && reverseBonus && xVel > runSpeedTotal - 2) {
                            skidding = true;
                            turnaround = true;
                            facingRight = true;
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

                        if (state != Enums.PowerupState.Giant && reverseBonus && xVel < -runSpeedTotal + 2) {
                            skidding = true;
                            turnaround = true;
                            facingRight = false;
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

        if (state == Enums.PowerupState.Shell && !inShell && onGround && running && !holding && Mathf.Abs(xVel)+0.25f >= runningMaxSpeed && landing > 0.33f) {
            inShell = true;
        }
        if (onGround) {
            body.velocity = new Vector2(body.velocity.x, 0);
        }
    }

    void HandleMovement(float delta) {
        
        if (photonView.IsMine && body.position.y < GameManager.Instance.GetLevelMinY()) {
            //death via pit
            photonView.RPC("Death", RpcTarget.All, true);
            return;
        }

        bool paused = GameManager.Instance.paused;

        if (!pipeEntering) invincible -= delta;
        throwInvincibility -= delta;
        jumpBuffer -= delta;
        walljumping -= delta;
        giantTimer -= delta;
        giantStartTimer -= delta;

        if (giantStartTimer > 0) {
            body.velocity = Vector2.zero;
            body.isKinematic = giantStartTimer-delta > 0;
            if (animator.GetCurrentAnimatorClipInfo(1)[0].clip.name != "mega-scale")
                animator.Play("mega-scale", state >= Enums.PowerupState.Large ? 1 : 0);
            return;
        }

        if (state == Enums.PowerupState.Giant) {
            if (giantTimer <= 0) {
                state = Enums.PowerupState.Large;
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
            body.velocity -= (body.velocity * (delta * 2f));
            if (photonView.IsMine && onGround && Mathf.Abs(body.velocity.x) < 0.05f) {
                photonView.RPC("ResetKnockback", RpcTarget.All);
            }
        }

        //activate blocks jumped into
        if (hitRoof) {
            body.velocity = new Vector2(body.velocity.x, Mathf.Min(body.velocity.y, -0.1f));
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

        if (crouch) {
            holdingDownTimer += delta;
        } else {
            holdingDownTimer = 0;
            alreadyGroundpounded = false;
        }

        if (holding) {
            onLeft = false;
            onRight = false;
            holding.holderOffset = new Vector2((facingRight ? 1 : -1) * 0.25f, (state >= Enums.PowerupState.Large ? 0.5f : 0.25f));
        }
        
        //throwing held item
        if ((!running || state == Enums.PowerupState.Mini || state == Enums.PowerupState.Giant) && holding) {
            bool throwLeft = !facingRight;
            if (left) {
                throwLeft = true;
            }
            if (right) {
                throwLeft = false;
            }
            holding.photonView.RPC("Throw", RpcTarget.All, throwLeft, crouch);
            if (!crouch) {
                photonView.RPC("PlaySound", RpcTarget.All, character.soundFolder + "/walljump_2");
                throwInvincibility = 0.5f;
                animator.SetTrigger("throw");
            }
            holdingOld = holding;
            holding = null;
        }

        //blue shell enter/exit
        if (state != Enums.PowerupState.Shell || !running) {
            inShell = false;
        }
        if (inShell) {
            crouch = true;
            if (photonView.IsMine && (hitLeft || hitRight)) {
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
        
        if (holdingDownTimer > 0.1f && !alreadyGroundpounded) {
            HandleGroundpoundStart(left, right);
        }
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
            if (triplejump && landing == 0 && !(left || right) && !groundpound) {
                body.velocity = new Vector2(0,0);
                animator.Play("jumplanding", state >= Enums.PowerupState.Large ? 1 : 0);
            }
            if ((landing += delta) > 0.2f) {
                singlejump = false;
                doublejump = false;
                triplejump = false;
            }
        
            if (onSpinner && Mathf.Abs(body.velocity.x) < 0.3f && !holding) {
                Transform spnr = onSpinner.transform;
                if (body.position.x > spnr.transform.position.x + 0.02f) {
                    body.position -= (new Vector2(0.01f * 60f, 0) * Time.fixedDeltaTime);
                } else if (body.position.x < spnr.transform.position.x - 0.02f) {
                    body.position += (new Vector2(0.01f * 60f, 0) * Time.fixedDeltaTime);
                }
            }
        } else {
            koyoteTime += delta;
            landing = 0;
            skidding = false;
            turnaround = false;
        }

        //slow-rise check
        if (flying) {
            body.gravityScale = flyingGravity;
        } else {
            float gravityModifier = (state != Enums.PowerupState.Mini ? 1f : 0.4f);
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
        groundpoundCounter -= delta;

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
                body.velocity *= 1-(delta * (ice ? 0.1f : 1f) * (knockback ? 3f : 4f));
                if (Mathf.Abs(body.velocity.x) < 0.05) {
                    body.velocity = new Vector2(0, body.velocity.y);
                }
            }
        }
        //Terminal velocity
        float terminalVelocityModifier = (state != Enums.PowerupState.Mini ? 1f : 0.65f);
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
            if (Physics2D.Raycast(body.position, Vector2.down, 0.5f * (state == Enums.PowerupState.Giant ? 2.5f : 1), ANY_GROUND_MASK)) return;
            
            onLeft = false;
            onRight = false;
            groundpound = true;
            hitBlock = true;
            groundpoundSit = false;
            body.velocity = Vector2.zero;
            groundpoundCounter = groundpoundTime * (state == Enums.PowerupState.Giant ? 1.5f : 1);
            photonView.RPC("PlaySound", RpcTarget.All, "player/groundpound");
            alreadyGroundpounded = true;
        }
    }

    void HandleGroundpound(bool crouch, bool up) {
        if (photonView.IsMine && onGround && (groundpound || drill) && hitBlock) {
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
                if (hitBlock) {
                    koyoteTime = 1;
                } else {
                    photonView.RPC("PlaySound", RpcTarget.All, "player/groundpound-landing" + (state == Enums.PowerupState.Mini ? "-mini" : ""));
                    photonView.RPC("SpawnParticle", RpcTarget.All, "Prefabs/Particle/GroundpoundDust");
                    groundpoundSit = true;
                }
            }
        }

        if (groundpound && ((up && !onGround) || (!crouch && onGround)) && groundpoundCounter <= 0) {
            groundpound = false;
            groundpoundSit = false;
            groundpoundCounter = (state == Enums.PowerupState.Giant ? 0.4f : 0.25f);
        }
    }
}