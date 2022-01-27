using System.Collections;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.InputSystem;
using Photon.Pun;

public class PlayerController : MonoBehaviourPun, IPunObservable {
    
    private static int ANY_GROUND_MASK, ONLY_GROUND_MASK, GROUND_LAYERID, HITS_NOTHING_LAYERID, ENTITY_HITBOX_LAYERID, DEFAULT_LAYERID;
    
    private int playerId = 0;
    [SerializeField] public bool dead = false;
    [SerializeField] public Enums.PowerupState state = Enums.PowerupState.Small, previousState;
    [SerializeField] public float slowriseGravity = 0.85f, normalGravity = 2.5f, flyingGravity = 0.5f, flyingTerminalVelocity = -1f, drillVelocity = 9f, deathUpTime = 0.6f, deathForce = 7f, groundpoundTime = 0.25f, groundpoundVelocity = 12, blinkingSpeed = 0.25f, terminalVelocity = -7f, jumpVelocity = 6.25f, launchVelocity = 20f, walkingAcceleration = 8f, runningAcceleration = 3f, walkingMaxSpeed = 2.7f, runningMaxSpeed = 5, wallslideSpeed = -2f, walljumpVelocity = 6f, pipeDuration = 2f, giantStartTime = 1.5f, blinkDuration = 0.25f;
    [SerializeField] ParticleSystem dust, sparkles, drillParticle, giantParticle;
    private BoxCollider2D[] hitboxes;
    GameObject models;
    public CameraController cameraController;
    private AudioSource sfx;
    private Animator animator;
    public Rigidbody2D body;

    public bool onGround, crushGround, onGroundLastFrame, onRight, onLeft, hitRoof, skidding, turnaround, facingRight = true, singlejump, doublejump, triplejump, bounce, crouching, groundpound, groundpoundSit, knockback, deathUp, hitBlock, running, functionallyRunning, jumpHeld, flying, drill, inShell, hitLeft, hitRight, iceSliding, stuckInBlock;
    public float walljumping, landing, koyoteTime, deathCounter, groundpoundCounter, groundpoundDelay, hitInvincibilityCounter, powerupFlash, throwInvincibility, jumpBuffer, pipeTimer, giantStartTimer, giantEndTimer;
    public float invincible = 0, giantTimer = 0, blinkTimer = 0;
    
    private Vector2 pipeDirection;
    public int stars, coins;
    public string storedPowerup = null;
    public HoldableEntity holding, holdingOld;
    [ColorUsage(true, false)]
    public Color glowColor = Color.clear;


    private float analogDeadzone = 0.35f;
    public Vector2 joystick, savedVelocity;

    public GameObject smallModel, largeModel, blueShell;
    public Avatar smallAvatar, largeAvatar;
    public GameObject onSpinner;
    PipeManager pipeEntering;
    private Vector3 cameraOffsetLeft = Vector3.left, cameraOffsetRight = Vector3.right, cameraOffsetZero = Vector3.zero;
    private bool starDirection, step, alreadyGroundpounded, cancelledMega, wasTurnaround;
    private Enums.PlayerEyeState eyeState;
    public PlayerData character;
    public float heightSmallModel = 0.46f, heightLargeModel = 0.82f;

    //Tile data
    private string footstepMaterial = "";
    private bool doIceSkidding;
    private float tileFriction = 1;
    private HashSet<Vector3Int> tilesStandingOn = new HashSet<Vector3Int>(), tilesJumpedInto = new HashSet<Vector3Int>(), tilesHitSide = new HashSet<Vector3Int>();
    

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
            Vector2 pos = (Vector2) stream.ReceiveNext();
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
        DEFAULT_LAYERID = LayerMask.NameToLayer("Default");
        
        cameraController = GetComponent<CameraController>();
        animator = GetComponentInChildren<Animator>();
        body = GetComponent<Rigidbody2D>();
        sfx = GetComponent<AudioSource>();
        models = transform.Find("Models").gameObject;
        starDirection = Random.value < 0.5;
        PlayerInput input = GetComponent<PlayerInput>();
        input.enabled = !photonView || photonView.IsMine;

        smallModel.SetActive(false);
        largeModel.SetActive(false);

        if (photonView) {
            playerId = System.Array.IndexOf(PhotonNetwork.PlayerList, photonView.Owner);
            if (!photonView.IsMine) {
                glowColor = Color.HSVToRGB((float) playerId / ((float) PhotonNetwork.PlayerList.Length + 1), 1, 1);
            }
        }
    }
    void Start() {
        hitboxes = GetComponents<BoxCollider2D>();
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
        float highestAngleThisFrame = 0;
        crushGround = false;
        foreach (BoxCollider2D hitbox in hitboxes) {
            ContactPoint2D[] contacts = new ContactPoint2D[20];
            collisionCount = hitbox.GetContacts(contacts);

            for (int i = 0; i < collisionCount; i++) {
                ContactPoint2D contact = contacts[i];
                Vector2 n = contact.normal;
                Vector2 p = contact.point + (contact.normal * -0.15f);
                Vector3Int vec = Utils.WorldToTilemapPosition(p);
                if (contact.collider.tag == "Player") continue;

                if (Vector2.Dot(n,Vector2.up) > .5f) {
                    // Vector2 modifiedVec = p + (new Vector2(0.01f, 0) * (p.x - body.position.x < 0 ? 1 : -1)); 
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
                        tilesHitSide.Add(vec);
                    } else if (Vector2.Dot(n,Vector2.right) > .9f) {
                        left++;
                        tilesHitSide.Add(vec);
                    } else if (Vector2.Dot(n,Vector2.down) > .9f && !groundpound) {
                        up++;
                        blockRoofY = vec.y;
                        tilesJumpedInto.Add(vec);
                    }
                } else {
                    ignoreRoof = true;
                }
            }
        }

        onGround = down >= 2 && body.velocity.y < 3;
        if (onGround) {
            onGroundLastFrame = true;
        }
        hitLeft = left >= 2;
        onLeft = hitLeft && !inShell && body.velocity.y < -0.1 && !facingRight && !onGround && !holding && state != Enums.PowerupState.Giant && !flying;
        hitRight = right >= 2;
        onRight = hitRight && !inShell && body.velocity.y < -0.1 && facingRight && !onGround && !holding && state != Enums.PowerupState.Giant && !flying;
        hitRoof = !ignoreRoof && !onLeft && !onRight && up >= 2 && body.velocity.y > -0.2f;
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

                if (state == Enums.PowerupState.Giant) {
                    if (other.state == Enums.PowerupState.Giant) {
                        otherView.RPC("Knockback", RpcTarget.All, otherObj.transform.position.x < body.position.x, 0, -1);
                        photonView.RPC("Knockback", RpcTarget.All, otherObj.transform.position.x > body.position.x, 0, -1);
                    } else {
                        otherView.RPC("Powerdown", RpcTarget.All, false);
                    }
                    return;
                }

                if (contact.normal.y > 0) {
                    //hit them from above
                    bounce = !groundpound;
                    drill = false;
                    if (state == Enums.PowerupState.Mini && !groundpound) {
                        photonView.RPC("PlaySound", RpcTarget.All, "enemy/shell_kick");
                    } else if (other.state == Enums.PowerupState.Mini && groundpound) {
                        otherView.RPC("Powerdown", RpcTarget.All, false);
                        bounce = false;
                    } else {
                        otherView.RPC("Knockback", RpcTarget.All, otherObj.transform.position.x < body.position.x, groundpound && state != Enums.PowerupState.Mini ? 2 : 1, photonView.ViewID);
                    }
                    return;
                }

                if (inShell) {
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
    void GiantFootstep() {
        cameraController.screenShakeTimer = 0.15f;
        SpawnParticle("Prefabs/Particle/GroundpoundDust", body.position.x + (facingRight ? 0.5f : -0.5f), body.position.y);
        //TODO: find stomp sound
    }
    void Footstep() {
        if (state == Enums.PowerupState.Giant) {
            return;
        }
        if (doIceSkidding && skidding) {
            PlaySoundFromAnim("player/ice-skid");
            return;
        }
        if (Mathf.Abs(body.velocity.x) < walkingMaxSpeed)
            return;
        
        PlaySoundFromAnim("player/walk" + (footstepMaterial != "" ? "-" + footstepMaterial : "") + (step ? "-2" : ""), Mathf.Abs(body.velocity.x) / (runningMaxSpeed + 4));
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

        PhotonNetwork.Instantiate("Prefabs/Fireball", body.position + new Vector2(facingRight ? 0.3f : -0.3f, 0.4f), Quaternion.identity, 0, new object[]{!facingRight});
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
                groundpound = false;
                giantTimer = 15f;
                transform.localScale = Vector3.one;
                GameObject.Instantiate(Resources.Load("Prefabs/Particle/GiantPowerup"), (Vector3) body.position + (Vector3.forward * transform.position.z), Quaternion.identity);
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
        if (!photonView.IsMine) return;

        HoldableEntity holdable = collider.gameObject.GetComponentInParent<HoldableEntity>();
        if (holdable && (holding == holdable || (holdingOld == holdable && throwInvincibility > 0))) return;
        KillableEntity killable = collider.gameObject.GetComponentInParent<KillableEntity>();
        if (killable && !killable.dead) {
            killable.InteractWithPlayer(this);
            return;
        }

        GameObject obj = collider.gameObject;
        switch (obj.tag) {
            case "bigstar": {
                photonView.RPC("CollectBigStar", RpcTarget.AllViaServer, obj.transform.parent.gameObject.GetPhotonView().ViewID);
                break;
            }
            case "loosecoin": {
                Transform parent = obj.transform.parent;
                photonView.RPC("CollectCoin", RpcTarget.AllViaServer, parent.gameObject.GetPhotonView().ViewID, parent.position.x, parent.position.y);
                break;
            }
            case "coin": {
                photonView.RPC("CollectCoin", RpcTarget.All, obj.GetPhotonView().ViewID, obj.transform.position.x, collider.transform.position.y);
                break;
            }
            case "Fireball": {
                FireballMover fireball = obj.GetComponentInParent<FireballMover>();
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
        }
    }
    void OnTriggerStay2D(Collider2D collider) {
        GameObject obj = collider.gameObject;
        switch (obj.tag) {
            case "spinner":
                onSpinner = obj;
                break;
            case "BlueShell":
            case "Star":
            case "MiniMushroom":
            case "FireFlower":
            case "MegaMushroom":
            case "Mushroom": {
                if (!photonView.IsMine) return;
                MovingPowerup powerup = obj.GetComponentInParent<MovingPowerup>();
                if (powerup.followMeCounter > 0 || powerup.ignoreCounter > 0)
                    break;
                photonView.RPC("Powerup", RpcTarget.AllViaServer, powerup.photonView.ViewID, obj.tag, obj.transform.parent.gameObject.GetPhotonView().ViewID);
                Destroy(collider);
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
        if (view == null) return;
        GameObject star = view.gameObject;
        StarBouncer starScript = star.GetComponent<StarBouncer>();
        if (starScript.readyForUnPassthrough > 0) return;
        if (starScript.passthrough) return;
        
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

        if (photonView.IsMine) {
            if (coins >= 8) {
                SpawnItem();
                coins = 0;
            }
            photonView.RPC("SetCoins", RpcTarget.Others, coins);
        }
    }

    void SpawnItem(string item = null) {
        if (item == null) {
            item = Utils.GetRandomItem(stars).prefab;
        }

        PhotonNetwork.Instantiate("Prefabs/Powerup/" + item, body.position + new Vector2(0, 5), Quaternion.identity, 0, new object[]{photonView.ViewID});
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
        animator.Play("deadstart", state >= Enums.PowerupState.Large ? 1 : 0);
        PlaySoundFromAnim("player/death");
        SpawnStar();
        if (holding) {
            holding.photonView.RPC("Throw", RpcTarget.All, !facingRight, true);
            holding = null;
        }
        if (deathplane && photonView.IsMine) {
            transform.Translate(0, -20, 0);
        }
    }

    void SpawnStar() {
        if (stars <= 0) return;
        stars--;
        if (!PhotonNetwork.IsMasterClient) return;

        GameObject star = PhotonNetwork.InstantiateRoomObject("Prefabs/BigStar", transform.position, Quaternion.identity, 0, new object[]{starDirection});
        StarBouncer sb = star.GetComponent<StarBouncer>();
        sb.photonView.TransferOwnership(PhotonNetwork.MasterClient);
        photonView.RPC("SetStars", RpcTarget.Others, stars);
        starDirection = !starDirection;
        
    }

    [PunRPC]
    public void PreRespawn() {
        transform.position = body.position = GameManager.Instance.GetSpawnpoint(playerId);
        cameraController.scrollAmount = 0;
        cameraController.Update();
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
        sfx.PlayOneShot((AudioClip) Resources.Load("Sound/" + sound));
    }

    [PunRPC]
    void SpawnParticle(string particle) {
        GameObject.Instantiate(Resources.Load(particle), transform.position, Quaternion.identity);
    }

    [PunRPC]
    void SpawnParticle(string particle, float x, float y) {
        GameObject.Instantiate(Resources.Load(particle), new Vector2(x, y), Quaternion.identity);
    }

    void HandleGiantTiles(bool pipes) {
        Vector3Int worldOffset = new Vector3Int(GameManager.Instance.levelWidthTile, 0, 0);
        int minY = (singlejump && onGround) ? 0 : 1, maxY = Mathf.Abs(body.velocity.y) > 0.05f ? 8 : 7;
        Vector2 offset = (Vector2.right * 0.3f) * (facingRight ? 1 : -1);
        int width = 1;
        if (groundpound) {
            offset = new Vector2(0, -0.3f);
        }
        for (int x = -width; x <= width; x++) {
            for (int y = minY; y <= maxY; y++) {
                Vector3Int tileLocation = Utils.WorldToTilemapPosition(body.position + offset + new Vector2(x/2f, y/2f - 0.4f));
                if (tileLocation.x < GameManager.Instance.levelMinTileX) {
                    tileLocation += worldOffset;
                }
                BreakablePipeTile pipe = GameManager.Instance.tilemap.GetTile<BreakablePipeTile>(tileLocation);
                if (pipe && (pipe.upsideDownPipe || !pipes)) continue;

                InteractableTile.InteractionDirection dir = InteractableTile.InteractionDirection.Up;
                if (y == minY && singlejump && onGround) {
                    dir = InteractableTile.InteractionDirection.Down;
                } else if (x == -width) {
                    dir = InteractableTile.InteractionDirection.Left;
                } else if (x == width) {
                    dir = InteractableTile.InteractionDirection.Right;
                }

                InteractWithTile(tileLocation, dir);
            }
        }
        if (pipes) {
            for (int x = -width; x <= width; x++) {
                for (int y = maxY; y >= minY; y--) {
                    Vector3Int tileLocation = Utils.WorldToTilemapPosition(body.position + offset + new Vector2(x/2f, y/2f - 0.45f));
                    if (tileLocation.x < GameManager.Instance.levelMinTileX) {
                        tileLocation += worldOffset;
                    }
                    BreakablePipeTile pipe = GameManager.Instance.tilemap.GetTile<BreakablePipeTile>(tileLocation);
                    if (!pipe || !pipe.upsideDownPipe) continue;

                    InteractableTile.InteractionDirection dir = InteractableTile.InteractionDirection.Up;
                    if (y == minY && singlejump && onGround) {
                        dir = InteractableTile.InteractionDirection.Down;
                    } else if (x == -width) {
                        dir = InteractableTile.InteractionDirection.Left;
                    } else if (x == width) {
                        dir = InteractableTile.InteractionDirection.Right;
                    }

                    InteractWithTile(tileLocation, dir);
                }
            }
        }
    }

    int InteractWithTile(Vector3Int tilePos, InteractableTile.InteractionDirection direction) {
        if (!photonView.IsMine) return 0;

        TileBase tile = GameManager.Instance.tilemap.GetTile(tilePos);
        if (tile == null) return -1;
        if (tile is InteractableTile) {
            return ((InteractableTile) tile).Interact(this, direction, Utils.TilemapToWorldPosition(tilePos)) ? 1 : 0;
        }
        return 0;
    }
    
    public void PlaySoundFromAnim(string sound, float volume = 1) {
        sfx.PlayOneShot((AudioClip) Resources.Load("Sound/" + sound), volume);
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
        body.velocity = new Vector2((fromRight ? -1 : 1) * 3 * (starsToDrop + 1) * (state == Enums.PowerupState.Giant ? 3 : 1), 0);
        inShell = false;
        facingRight = !fromRight;
        groundpound = false;
        flying = false;
        drill = false;
        body.gravityScale = normalGravity;
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
            HandleCustomTiles();
            TickCounters();
            HandleMovement(Time.fixedDeltaTime);
        }
        HandleAnimation();
    }

    bool colliding = true;
    void HandleTemporaryInvincibility() {
        bool shouldntCollide = (hitInvincibilityCounter > 0) || knockback;
        if (shouldntCollide && colliding) {
            colliding = false;
            foreach (var player in GameManager.Instance.allPlayers) {
                foreach (BoxCollider2D hitbox in hitboxes) {
                    foreach (BoxCollider2D otherHitbox in player.hitboxes) {
                        Physics2D.IgnoreCollision(hitbox, otherHitbox, true);
                    }
                }
            }
        } else if (!shouldntCollide && !colliding) {
            colliding = true;
            foreach (var player in GameManager.Instance.allPlayers) {
                foreach (BoxCollider2D hitbox in hitboxes) {
                    foreach (BoxCollider2D otherHitbox in player.hitboxes) {
                        Physics2D.IgnoreCollision(hitbox, otherHitbox, false);
                    }
                }
            }
        }
    }

    void HandleCustomTiles() {
        doIceSkidding = false;
        tileFriction = -1;
        footstepMaterial = "";
        foreach (Vector3Int pos in tilesStandingOn) {
            TileBase tile = Utils.GetTileAtTileLocation(pos);
            if (tile == null) continue;
            if (tile is TileWithProperties) {
                TileWithProperties propTile = (TileWithProperties) tile;
                footstepMaterial = propTile.footstepMaterial;
                doIceSkidding = propTile.iceSkidding;
                tileFriction = Mathf.Max(tileFriction, propTile.frictionFactor);
            } else {
                tileFriction = 1;
            }
        }
        if (tileFriction == -1) {
            tileFriction = 1;
        }
    }

    void HandleDeathAnimation() {
        if (!dead) return;
        if (body.position.y < GameManager.Instance.GetLevelMinY()) {
            body.position = new Vector2(body.position.x, GameManager.Instance.GetLevelMinY() - 8);
        }

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
        }

        if (photonView.IsMine && deathCounter >= 3f) {
            photonView.RPC("PreRespawn", RpcTarget.All);
        }
    }

    void HandleAnimation() {
        //Dust particles
        if (photonView.IsMine) {

            //Facing direction
            bool right = joystick.x > analogDeadzone;
            bool left = joystick.x < -analogDeadzone;
            if (doIceSkidding && !inShell && !groundpound) {
                if (right || left) {
                    facingRight = right;
                }
            } else if (giantStartTimer <= 0 && giantEndTimer <= 0 && !skidding && !(animator.GetCurrentAnimatorStateInfo(0).IsName("turnaround") || turnaround)) {
                if (onGround && state != Enums.PowerupState.Giant && Mathf.Abs(body.velocity.x) > 0.05f) {
                    facingRight = body.velocity.x > 0;
                } else if (((walljumping <= 0 && !inShell) || giantStartTimer > 0) && (right || left)) {
                    facingRight = right;
                }
                if (!inShell && ((Mathf.Abs(body.velocity.x) < 0.5f && crouching) || doIceSkidding) && (right || left)) {
                    facingRight = right;
                }
            }

            //Animation
            animator.SetBool("skidding", !doIceSkidding && skidding);
            animator.SetBool("turnaround", turnaround);
            animator.SetBool("onLeft", onLeft);
            animator.SetBool("onRight", onRight);
            animator.SetBool("onGround", onGround);
            animator.SetBool("invincible", invincible > 0);
            float animatedVelocity = Mathf.Abs(body.velocity.x);
            if (stuckInBlock) {
                animatedVelocity = 0;
            } else if (doIceSkidding) {
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
            animator.SetBool("inShell", inShell || (state == Enums.PowerupState.Shell && (crouching || groundpound)));
            animator.SetBool("facingRight", facingRight);
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
            // inShell = animator.GetBool("inShell");
            // knockback = animator.GetBool("knockback");
            facingRight = animator.GetBool("facingRight");
        }
        
        if (giantEndTimer > 0) {
            transform.localScale = Vector3.one + (Vector3.one * (Mathf.Min(1, giantEndTimer / (giantStartTime / 2f)) * 2.6f));
        } else {
            switch (state) {
            case Enums.PowerupState.Mini:
                transform.localScale = Vector3.one / 2;
                break;
            case Enums.PowerupState.Giant:
                transform.localScale = Vector3.one + (Vector3.one * (Mathf.Min(1, 1 - (giantStartTimer / giantStartTime)) * 2.6f));
                break;
            default:
                transform.localScale = Vector3.one;
                break;
            }
        }

        if (animator.GetBool("pipe")) {
            gameObject.layer = HITS_NOTHING_LAYERID;
            transform.position = new Vector3(body.position.x, body.position.y, 1);
        } else if (dead || stuckInBlock || giantStartTimer > 0 || giantEndTimer > 0) {
            gameObject.layer = HITS_NOTHING_LAYERID;
            transform.position = new Vector3(body.position.x, body.position.y, -4);
        } else {
            gameObject.layer = DEFAULT_LAYERID;
            transform.position = new Vector3(body.position.x, body.position.y, -4);
        }

        Vector3 targetEuler = models.transform.eulerAngles;
        bool instant = false;
        if (dead || animator.GetBool("pipe")) {
            targetEuler = new Vector3(0,180,0);
            instant = true;
        } else if (animator.GetBool("inShell") && !onSpinner) {
            targetEuler += (new Vector3(0, 1800 * (facingRight ? -1 : 1)) * Time.fixedDeltaTime) * (Mathf.Abs(body.velocity.x) / runningMaxSpeed);
            instant = true;
        } else if (skidding || turnaround) {
            if (facingRight ^ (animator.GetCurrentAnimatorStateInfo(0).IsName("turnaround") || turnaround)) {
                targetEuler = new Vector3(0,360-100,0);
            } else {
                targetEuler = new Vector3(0,100,0);
            }
        } else {
            if (onSpinner && onGround && Mathf.Abs(body.velocity.x) < 0.3f && !holding) {
                targetEuler += (new Vector3(0, -1800, 0) * Time.fixedDeltaTime);
                instant = true;
            } else if (flying) {
                if (drill) {
                    targetEuler += (new Vector3(0, -2000, 0) * Time.fixedDeltaTime);
                } else {
                    targetEuler += (new Vector3(0, -1200, 0) * Time.fixedDeltaTime);
                }
                instant = true;
            } else {
                if (facingRight) {
                    targetEuler = new Vector3(0,100,0);
                } else {
                    targetEuler = new Vector3(0,360-100,0);
                }
            }
        }
        if (instant || wasTurnaround) {
            models.transform.rotation = Quaternion.Euler(targetEuler);
        } else {
            float maxRotation = 2000f * Time.fixedDeltaTime;
            float x = models.transform.eulerAngles.x, y = models.transform.eulerAngles.y, z = models.transform.eulerAngles.z;
            x += Mathf.Max(Mathf.Min(maxRotation, targetEuler.x - x), -maxRotation);
            y += Mathf.Max(Mathf.Min(maxRotation, targetEuler.y - y), -maxRotation);
            z += Mathf.Max(Mathf.Min(maxRotation, targetEuler.z - z), -maxRotation);
            models.transform.rotation = Quaternion.Euler(x, y, z);
        }
        wasTurnaround = animator.GetCurrentAnimatorStateInfo(0).IsName("turnaround") || turnaround;

        if ((onLeft || onRight || (onGround && ((skidding && !doIceSkidding) || (crouching && Mathf.Abs(body.velocity.x) > 1)))) && !pipeEntering) {
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
        Vector3 giantMultiply = Vector3.one;
        if (giantTimer > 0 && giantTimer < 4) {
            float v = (((Mathf.Sin(giantTimer * 20f) + 1f) / 2f) * 0.9f) + 0.1f;
            giantMultiply = new Vector3(v, 1, v);
        }
        block.SetVector("MultiplyColor", giantMultiply);
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
        if (state == Enums.PowerupState.Giant && giantStartTimer < 0) {
            if (!giantParticle.isPlaying)
                giantParticle.Play();
        } else {
            giantParticle.Stop();
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
                cameraController.offset = (body.velocity.x > 0 ? cameraOffsetRight : cameraOffsetLeft) * percentage * 2f;
                cameraController.exactCentering = true;
            } else {
                cameraController.offset = cameraOffsetZero;
                cameraController.exactCentering = false;
            }
        }
    }

    [PunRPC]
    public void SetHolding(int view) {
        holding = PhotonView.Find(view).GetComponent<HoldableEntity>();
    }
    [PunRPC]
    public void SetHoldingOld(int view) {
        holdingOld = PhotonView.Find(view).GetComponent<HoldableEntity>();
        throwInvincibility = 0.5f;
    }

    void UpdateHitbox() {
        float width = hitboxes[0].size.x;
        float height = 0;

        if (state <= Enums.PowerupState.Small || (invincible  > 0 && !onGround && !crouching) || groundpound) {
            height = heightSmallModel;
        } else {
            height = heightLargeModel;
        }

        if (crouching || inShell) {
            height *= (state <= Enums.PowerupState.Small ? 0.7f : 0.5f);
        }

        hitboxes[0].size = new Vector2(width, height);
        hitboxes[0].offset = new Vector2(0, height/2f);
    }

    void FakeOnGroundCheck() {
        if ((onGroundLastFrame || (flying && body.velocity.y < 0)) && pipeEntering == null && !onGround) {
            var hit = Physics2D.Raycast(body.position, Vector2.down, 0.1f, ANY_GROUND_MASK);
            if (hit) {
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
            transform.position = body.position = new Vector3(pipeEntering.otherPipe.transform.position.x, pipeEntering.otherPipe.transform.position.y, 1) - (Vector3) offset;
            photonView.RPC("PlaySound", RpcTarget.All, "player/pipe");
        }
        if (pipeTimer >= pipeDuration) {
            pipeEntering = null;
            body.isKinematic = false;
        }
        pipeTimer += Time.fixedDeltaTime;
    }

    void DownwardsPipeCheck() {
        if (!photonView.IsMine) return;
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
            transform.position = body.position = new Vector2(obj.transform.position.x, transform.position.y);

            photonView.RPC("PlaySound", RpcTarget.All, "player/pipe");
            crouching = false;
            break;
        }
    }

    void UpwardsPipeCheck() {
        if (!photonView.IsMine) return;
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
            transform.position = body.position = new Vector2(obj.transform.position.x, transform.position.y);
                
            photonView.RPC("PlaySound", RpcTarget.All, "player/pipe");
            crouching = false;
            break;
        }
    }
    
    void HandleCrouching(bool crouchInput) {
        if (state == Enums.PowerupState.Giant) {
            crouching = false;
            return;
        }
        bool prevCrouchState = crouching;
        crouching = ((onGround && crouchInput) || (!onGround && crouchInput && crouching) || (crouching && ForceCrouchCheck())) && !holding;
        if (crouching && !prevCrouchState) {
            //crouch start sound
            PlaySoundFromAnim("player/crouch");
        }
    }

    bool ForceCrouchCheck() {
        if (state < Enums.PowerupState.Large) return false;
        float width = hitboxes[0].bounds.extents.x;
        Debug.DrawRay(body.position + new Vector2(-width+0.05f,0.05f), Vector2.up * heightLargeModel, Color.magenta);
        Debug.DrawRay(body.position + new Vector2(width-0.05f,0.05f), Vector2.up * heightLargeModel, Color.magenta);

        bool triggerState = Physics2D.queriesHitTriggers;
        Physics2D.queriesHitTriggers = false;
        bool ret = (Physics2D.Raycast(body.position + new Vector2(-width+0.05f,0.05f), Vector2.up, heightLargeModel, ONLY_GROUND_MASK) 
            || Physics2D.Raycast(body.position + new Vector2(width-0.05f,0.05f), Vector2.up, heightLargeModel, ONLY_GROUND_MASK));
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
        if (state == Enums.PowerupState.Giant && singlejump) return;

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

            float vel = jumpVelocity + Mathf.Abs(body.velocity.x)/8f * (state == Enums.PowerupState.Giant ? 1.5f : 1f);
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
            if (!bounce) {
                //play jump
                string sound = "jump";
                switch (state) {
                case Enums.PowerupState.Giant: {
                    sound = "jump_mega";
                    break;
                }
                case Enums.PowerupState.Mini: {
                    sound = "jump_mini";
                    break;
                }
                }
                photonView.RPC("PlaySound", RpcTarget.All, "player/" + sound);
            }
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
        if (!(walljumping <= 0 || onGround)) return;

        iceSliding = false;
        if (!left && !right) {
            skidding = false;
            turnaround = false;
            if (doIceSkidding) {
                iceSliding = true;
            }
        }

        if (Mathf.Abs(body.velocity.x) < 0.5f || !onGround) {
            skidding = false;
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
        bool reverseSlowing = onGround && (((left && body.velocity.x > 0.02) || (right && body.velocity.x < -0.02)));
        float reverseFloat = (reverseSlowing ? (doIceSkidding ? 0.4f : 0.7f) : 1);
        float turnaroundSpeedBoost = (turnaround && !reverseSlowing ? 5 : 1);
        float stationarySpeedBoost = Mathf.Abs(body.velocity.x) <= 0.005f ? 1f : 1f;

        if ((crouching && !onGround && state != Enums.PowerupState.Shell) || !crouching) {
            
            if (left) {
                if (functionallyRunning && !flying && xVel <= -(walkingMaxSpeed - 0.3f)) {
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
                        
                        if (state != Enums.PowerupState.Giant && reverseSlowing && xVel > runSpeedTotal - 2) {
                            skidding = true;
                            turnaround = true;
                            facingRight = true;
                        }
                    }
                }
            }
            if (right) {
                if (functionallyRunning && !flying && xVel >= (walkingMaxSpeed - 0.3f)) {
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

                        if (state != Enums.PowerupState.Giant && reverseSlowing && xVel < -runSpeedTotal + 2) {
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

        if (state == Enums.PowerupState.Shell && !inShell && onGround && functionallyRunning && !holding && Mathf.Abs(xVel)+0.25f >= runningMaxSpeed && landing > 0.33f) {
            inShell = true;
        }
        if (onGround) {
            body.velocity = new Vector2(body.velocity.x, 0);
        }
    }

    bool HandleStuckInBlock(float delta) {
        if (!body || hitboxes == null) return false;
        Vector2 checkPos = body.position + new Vector2(0, hitboxes[0].size.y/4f);
        if (!Utils.IsTileSolidAtWorldLocation(checkPos)) {
            stuckInBlock = false;
            return false;
        }
        TileBase tile = Utils.GetTileAtWorldLocation(checkPos);
        stuckInBlock = true;
        body.gravityScale = 0;
        onGround = true;
        if (!Utils.IsTileSolidAtWorldLocation(checkPos + new Vector2(0, 0.3f))) {
            transform.position = body.position = new Vector2(body.position.x, Mathf.Floor((checkPos.y + 0.3f)*2)/2);
            return true;
        } else if (!Utils.IsTileSolidAtWorldLocation(checkPos + new Vector2(0.25f, 0))) {
            body.velocity = Vector2.right*2f;
            return true;
        } else if (!Utils.IsTileSolidAtWorldLocation(checkPos + new Vector2(-0.25f, 0))) {
            body.velocity = Vector2.left*2f;
            return true;
        }
        RaycastHit2D rightRaycast = Physics2D.Raycast(checkPos, Vector2.right, 15, ONLY_GROUND_MASK);
        RaycastHit2D leftRaycast = Physics2D.Raycast(checkPos, Vector2.left, 15, ONLY_GROUND_MASK);
        float rightDistance = 0, leftDistance = 0;
        if (rightRaycast) rightDistance = rightRaycast.distance;
        if (leftRaycast) leftDistance = leftRaycast.distance;
        if (rightDistance <= leftDistance) {
            body.velocity = Vector2.right*2f;
        } else {
            body.velocity = Vector2.left*2f;
        }
        return true;
    }

    void TickCounter(ref float counter, float min, float delta) {
        counter = Mathf.Max(0, counter - delta); 
    }

    void TickCounters() {
        float delta = Time.fixedDeltaTime;
        if (!pipeEntering) TickCounter(ref invincible, 0, delta);
        if (state == Enums.PowerupState.Giant) invincible = 0;

        TickCounter(ref throwInvincibility, 0, delta);
        TickCounter(ref jumpBuffer, 0, delta);
        TickCounter(ref walljumping, 0, delta);
        if (giantStartTimer <= 0) TickCounter(ref giantTimer, 0, delta);
        TickCounter(ref giantStartTimer, 0, delta);
        TickCounter(ref groundpoundCounter, 0, delta);
        TickCounter(ref giantEndTimer, 0, delta);
        TickCounter(ref groundpoundDelay, 0, delta);
    }

    [PunRPC]
    public void FinishMegaMario(bool success) {
        if (success) {
            PlaySound(character.soundFolder + "/mega_start");
        } else {
            //hit a wall, cancel
            savedVelocity = Vector2.zero;
            state = Enums.PowerupState.Large;
            giantEndTimer = giantStartTime;
            storedPowerup = "MegaMushroom";
            giantTimer = 0;
            animator.enabled = true;
            animator.Play("mega-cancel", 1);
            PlaySound("player/reserve_item_store");
        }
        body.isKinematic = false;
    }

    IEnumerator CheckForGiantStartupTiles() {
        HandleGiantTiles(false);
        yield return null;
        Vector2 o = body.position + new Vector2(0.3f * (facingRight ? 1 : -1), 1.75f);
        RaycastHit2D hit = Physics2D.BoxCast(o, new Vector2(0.6f, 3f), 0, Vector2.zero, 0, ONLY_GROUND_MASK);
        photonView.RPC("FinishMegaMario", RpcTarget.All, !(bool) hit);
    }
    
    void HandleMovement(float delta) {
        
        functionallyRunning = running || state == Enums.PowerupState.Giant;

        if (photonView.IsMine && body.position.y < GameManager.Instance.GetLevelMinY()) {
            //death via pit
            photonView.RPC("Death", RpcTarget.All, true);
            return;
        }

        bool paused = GameManager.Instance.paused;

        if (giantStartTimer > 0) {
            body.velocity = Vector2.zero;
            if (giantStartTimer-delta <= 0) {
                //start by checking bounding
                giantStartTimer = 0;
                if (photonView.IsMine)
                    StartCoroutine(CheckForGiantStartupTiles());
            } else {
                body.isKinematic = true;
                if (animator.GetCurrentAnimatorClipInfo(1)[0].clip.name != "mega-scale") {
                    animator.Play("mega-scale", 1);
                }
            }
            return;
        }
        if (giantEndTimer > 0) {
            body.velocity = Vector2.zero;
            body.isKinematic = true;
            
            if (giantEndTimer - delta <= 0) {
                hitInvincibilityCounter = 3f;
                body.velocity = savedVelocity;
                animator.enabled = true;
                body.isKinematic = false;
            }
            return;
        }

        if (state == Enums.PowerupState.Giant) {
            if (giantTimer <= 0) {
                if (state != Enums.PowerupState.Large) {
                    savedVelocity = body.velocity;
                } else {
                    savedVelocity = Vector2.zero;
                }
                state = Enums.PowerupState.Large;
                hitInvincibilityCounter = 3f;
                body.isKinematic = true;
                animator.enabled = false;
                giantEndTimer = giantStartTime / 2f;
                //todo: play shrink sfx
            } else {
                //destroy tiles
                HandleGiantTiles(true);
            }
        }
        
        if (HandleStuckInBlock(delta))
            return;

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
            foreach (Vector3Int tile in tilesJumpedInto) {
                InteractWithTile(tile, InteractableTile.InteractionDirection.Up);
            }
        }

        bool right = joystick.x > analogDeadzone && !paused;
        bool left = joystick.x < -analogDeadzone && !paused;
        bool crouch = joystick.y < -analogDeadzone && !paused;
        bool up = joystick.y > analogDeadzone && !paused;
        bool jump = (jumpBuffer > 0 && (onGround || koyoteTime < 0.1f || onLeft || onRight)) && !paused; 

        if (!crouch) {
            alreadyGroundpounded = false;
        }

        if (holding) {
            onLeft = false;
            onRight = false;
            holding.holderOffset = new Vector2((facingRight ? 1 : -1) * 0.25f, (state >= Enums.PowerupState.Large ? 0.5f : 0.25f));
        }
        
        //throwing held item
        if ((!functionallyRunning || state == Enums.PowerupState.Mini || state == Enums.PowerupState.Giant || invincible > 0) && holding) {
            bool throwLeft = !facingRight;
            if (left) {
                throwLeft = true;
            }
            if (right) {
                throwLeft = false;
            }
            holding.photonView.RPC("Throw", RpcTarget.All, throwLeft, crouch);
            if (!crouch && !knockback) {
                photonView.RPC("PlaySound", RpcTarget.All, character.soundFolder + "/walljump_2");
                throwInvincibility = 0.5f;
                animator.SetTrigger("throw");
            }
            holdingOld = holding;
            holding = null;
        }

        //blue shell enter/exit
        if (state != Enums.PowerupState.Shell || !functionallyRunning) {
            inShell = false;
        }
        if (inShell) {
            crouch = true;
            if (photonView.IsMine && (hitLeft || hitRight)) {
                foreach (var tile in tilesHitSide) {
                    InteractWithTile(tile, InteractableTile.InteractionDirection.Up);
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

        if ((walljumping <= 0 || onGround) && !groundpound) {
            //Normal walking/running
            HandleWalkingRunning(left, right);

            //Jumping
            HandleJumping(jump);
        }
        
        if (crouch && !alreadyGroundpounded) {
            HandleGroundpoundStart(left, right);
        }
        HandleGroundpound(crouch, up);

        //Ground
        FakeOnGroundCheck();
        if (onGround) {
            if (photonView.IsMine && hitRoof && crushGround && body.velocity.y <= 0.1) {
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
                photonView.RPC("SpawnParticle", RpcTarget.All, "Prefabs/Particle/GroundpoundDust");
            }
            if (singlejump && state == Enums.PowerupState.Giant) {
                photonView.RPC("SpawnParticle", RpcTarget.All, "Prefabs/Particle/GroundpoundDust");
                singlejump = false;
            }
            if ((landing += delta) > 0.1f) {
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

        if (!groundpoundSit && groundpound && groundpoundCounter <= 0) {
            body.velocity = new Vector2(0f, -groundpoundVelocity);
        }

        if (!inShell) {
            bool abovemax;
            float invincibleSpeedBoost = (invincible > 0 ? 2f : 1);
            float max = (functionallyRunning ? runningMaxSpeed : walkingMaxSpeed) * invincibleSpeedBoost;
            if (left && !crouching) {
                abovemax = body.velocity.x < -max; 
            } else if (right && !crouching) {
                abovemax = body.velocity.x > max;
            } else {
                abovemax = true;
            }
            //Friction...
            if (onGround && abovemax) {
                body.velocity *= 1-(delta * tileFriction * (knockback ? 3f : 4f));
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
        if (groundpoundDelay > 0) return;

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
            groundpoundDelay = 0.5f;
        }
    }

    void HandleGroundpound(bool crouch, bool up) {
        if (photonView.IsMine && onGround && (groundpound || drill) && hitBlock) {
            bool tempHitBlock = false;
            foreach (Vector3Int tile in tilesStandingOn) {
                int temp = InteractWithTile(tile, InteractableTile.InteractionDirection.Down);
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
                    if (state == Enums.PowerupState.Giant) {
                        photonView.RPC("PlaySound", RpcTarget.All, "player/groundpound_mega");
                        cameraController.screenShakeTimer = 0.35f;
                    }
                    photonView.RPC("SpawnParticle", RpcTarget.All, "Prefabs/Particle/GroundpoundDust");
                    groundpoundSit = state != Enums.PowerupState.Giant;
                }
            }
        }

        if (groundpound && ((up && !onGround) || ((!crouch || state == Enums.PowerupState.Giant) && onGround)) && groundpoundCounter <= 0) {
            groundpound = false;
            groundpoundSit = false;
            groundpoundCounter = (state == Enums.PowerupState.Giant ? 0.4f : 0.25f);
        }
    }
}