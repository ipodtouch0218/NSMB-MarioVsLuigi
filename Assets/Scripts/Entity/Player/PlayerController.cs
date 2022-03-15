using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.InputSystem;
using Photon.Pun;
using Photon.Realtime;
using ExitGames.Client.Photon;

public class PlayerController : MonoBehaviourPun, IPunObservable {
    
    public static int ANY_GROUND_MASK = -1, ONLY_GROUND_MASK, GROUND_LAYERID, HITS_NOTHING_LAYERID, DEFAULT_LAYERID;
    
    public int playerId = -1;
    public bool dead = false;
    public Enums.PowerupState state = Enums.PowerupState.Small, previousState;
    public float slowriseGravity = 0.85f, normalGravity = 2.5f, flyingGravity = 0.8f, flyingTerminalVelocity = 1.25f, drillVelocity = 7f, groundpoundTime = 0.25f, groundpoundVelocity = 10, blinkingSpeed = 0.25f, terminalVelocity = -7f, jumpVelocity = 6.25f, launchVelocity = 12f, walkingAcceleration = 8f, runningAcceleration = 3f, walkingMaxSpeed = 2.7f, runningMaxSpeed = 5, wallslideSpeed = -4.25f, walljumpVelocity = 5.6f, giantStartTime = 1.5f, soundRange = 10f;
    public float propellerLaunchVelocity = 6, propellerFallSpeed = 2, propellerSpinFallSpeed = 1.5f, propellerSpinTime = 0.75f;

    private BoxCollider2D[] hitboxes;
    GameObject models;

    public CameraController cameraController;
    public FadeOutManager fadeOut;
    
    private AudioSource sfx;
    private Animator animator;
    public Rigidbody2D body;
    private PlayerAnimationController animationController;

    public bool onGround, crushGround, doGroundSnap, onRight, onLeft, hitRoof, skidding, turnaround, facingRight = true, singlejump, doublejump, triplejump, bounce, crouching, groundpound, sliding, knockback, hitBlock, running, functionallyRunning, jumpHeld, flying, drill, inShell, hitLeft, hitRight, iceSliding, stuckInBlock, propeller;
    public float walljumping, landing, koyoteTime, groundpoundCounter, groundpoundDelay, hitInvincibilityCounter, powerupFlash, throwInvincibility, jumpBuffer, giantStartTimer, giantEndTimer, propellerTimer, propellerSpinTimer;
    public float invincible, giantTimer, floorAngle;
    
    public Vector2 pipeDirection;
    public int stars, coins, lives = -1;
    public string storedPowerup = null;
    public HoldableEntity holding, holdingOld;


    private bool powerupButtonHeld;
    private readonly float analogDeadzone = 0.35f;
    public Vector2 joystick, giantSavedVelocity, previousFrameVelocity;

    public GameObject onSpinner;
    public PipeManager pipeEntering;
    private bool starDirection, step, alreadyGroundpounded;
    public PlayerData character;

    //Tile data
    private string footstepMaterial = "";
    public bool doIceSkidding;
    private float tileFriction = 1;
    private readonly HashSet<Vector3Int> tilesStandingOn = new(), 
        tilesJumpedInto = new(), 
        tilesHitSide = new();


    #region -- SERIALIZATION / EVENTS --

    private long localFrameId = 0;
    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info) {
        if (stream.IsWriting) {
            stream.SendNext(body.position);
            stream.SendNext(body.velocity);

            ExitGames.Client.Photon.Hashtable controls = new()
            {
                ["joystick"] = joystick,
                ["sprintHeld"] = running,
                ["jumpHeld"] = jumpHeld
            };

            stream.SendNext(controls);
            stream.SendNext(localFrameId++);

        } else if (stream.IsReading) {
            Vector2 pos = (Vector2) stream.ReceiveNext();
            Vector2 vel = (Vector2) stream.ReceiveNext();
            ExitGames.Client.Photon.Hashtable controls = (ExitGames.Client.Photon.Hashtable) stream.ReceiveNext();
            long frameId = (long) stream.ReceiveNext();

            if (frameId < localFrameId)
                //recevied info older than what we have
                return;

            float lag = (float) (PhotonNetwork.Time - info.SentServerTime);

            body.position = pos;
            body.velocity = vel;
            localFrameId = frameId;

            joystick = (Vector2) controls["joystick"]; 
            running = (bool) controls["sprintHeld"];
            jumpHeld = (bool) controls["jumpHeld"];

            HandleMovement(lag);
        }
    }
    #endregion

    #region -- START / UPDATE --
    public void Awake() {
        if (ANY_GROUND_MASK == -1) {
            ANY_GROUND_MASK = LayerMask.GetMask("Ground", "Semisolids");
            ONLY_GROUND_MASK = LayerMask.GetMask("Ground");
            GROUND_LAYERID = LayerMask.NameToLayer("Ground");
            HITS_NOTHING_LAYERID = LayerMask.NameToLayer("HitsNothing");
            DEFAULT_LAYERID = LayerMask.NameToLayer("Default");
        }
        
        cameraController = GetComponent<CameraController>();
        animator = GetComponentInChildren<Animator>();
        body = GetComponent<Rigidbody2D>();
        sfx = GetComponent<AudioSource>();
        animationController = GetComponent<PlayerAnimationController>();
        fadeOut = GameObject.FindGameObjectWithTag("FadeUI").GetComponent<FadeOutManager>();
        
        models = transform.Find("Models").gameObject;
        starDirection = Random.value < 0.5;
        PlayerInput input = GetComponent<PlayerInput>();
        input.enabled = !photonView || photonView.IsMine;
        input.camera = Camera.main;

        playerId = PhotonNetwork.CurrentRoom != null ? System.Array.IndexOf(PhotonNetwork.PlayerList, photonView.Owner) : -1;
        lives = (int) PhotonNetwork.CurrentRoom.CustomProperties[Enums.NetRoomProperties.Lives];
        if (lives != -1)
            lives++;
    }

    public void Start() {
        hitboxes = GetComponents<BoxCollider2D>();
    }
    public void OnGameStart() {
        photonView.RPC("PreRespawn", RpcTarget.All);
    }


    public void FixedUpdate() {
        //game ended, freeze.

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

        if (!dead) {
            HandleTemporaryInvincibility();
            bool snapped = GroundSnapCheck();
            HandleGroundCollision();
            onGround |= snapped;
            doGroundSnap = onGround;
            HandleTileProperties();
            TickCounters();
            HandleMovement(Time.fixedDeltaTime);
        }
        animationController.UpdateAnimatorStates();
        previousFrameVelocity = body.velocity;
    }
    #endregion

    #region -- COLLISIONS --
    void HandleGroundCollision() {
        tilesJumpedInto.Clear();
        tilesStandingOn.Clear();
        tilesHitSide.Clear();

        bool ignoreRoof = false;
        int down = 0, left = 0, right = 0, up = 0;

        crushGround = false;
        foreach (BoxCollider2D hitbox in hitboxes) {
            ContactPoint2D[] contacts = new ContactPoint2D[20];
            int collisionCount = hitbox.GetContacts(contacts);

            for (int i = 0; i < collisionCount; i++) {
                ContactPoint2D contact = contacts[i];
                Vector2 n = contact.normal;
                Vector2 p = contact.point + (contact.normal * -0.15f);
                if (n == Vector2.up && contact.point.y > body.position.y) 
                    continue;
                Vector3Int vec = Utils.WorldToTilemapPosition(p);
                if (!contact.collider || contact.collider.CompareTag("Player")) 
                    continue;

                if (Vector2.Dot(n,Vector2.up) > .05f) {
                    if (Vector2.Dot(body.velocity.normalized, n) > 0.1f && !onGround) {
                        if (!contact.rigidbody || contact.rigidbody.velocity.y < body.velocity.y)
                            //invalid flooring
                            continue;
                    }
                    crushGround |= !contact.collider.gameObject.CompareTag("platform");
                    down++;
                    tilesStandingOn.Add(vec );
                } else if (contact.collider.gameObject.layer == GROUND_LAYERID) {
                    if (Vector2.Dot(n,Vector2.left) > .9f) {
                        right++;
                        tilesHitSide.Add(vec);
                    } else if (Vector2.Dot(n,Vector2.right) > .9f) {
                        left++;
                        tilesHitSide.Add(vec);
                    } else if (Vector2.Dot(n,Vector2.down) > .9f && !groundpound) {
                        up++;
                        tilesJumpedInto.Add(vec);
                    }
                } else {
                    ignoreRoof = true;
                }
            }
        }

        onGround = down >= 1;
        hitLeft = left >= 2;
        onLeft = hitLeft && !inShell && body.velocity.y < -0.1 && !facingRight && !onGround && !holding && state != Enums.PowerupState.Giant && !flying && !propeller && !crouching && !sliding;
        hitRight = right >= 2;
        onRight = hitRight && !inShell && body.velocity.y < -0.1 && facingRight && !onGround && !holding && state != Enums.PowerupState.Giant && !flying && !propeller && !crouching && !sliding;
        hitRoof = !ignoreRoof && !onLeft && !onRight && up >= 2 && body.velocity.y > -0.2f;
    }
    void HandleTileProperties() {
        doIceSkidding = false;
        tileFriction = -1;
        footstepMaterial = "";
        foreach (Vector3Int pos in tilesStandingOn) {
            TileBase tile = Utils.GetTileAtTileLocation(pos);
            if (tile == null)
                continue;
            if (tile is TileWithProperties propTile) {
                footstepMaterial = propTile.footstepMaterial;
                doIceSkidding = propTile.iceSkidding;
                tileFriction = Mathf.Max(tileFriction, propTile.frictionFactor);
            } else {
                tileFriction = 1;
            }
        }
        if (tileFriction == -1)
            tileFriction = 1;
    }

    public void OnCollisionEnter2D(Collision2D collision) {
        if (!photonView.IsMine || !collision.gameObject.CompareTag("Player"))
            return;

        //hit antoher player
        foreach (ContactPoint2D contact in collision.contacts) {
            GameObject otherObj = collision.gameObject;
            PlayerController other = otherObj.GetComponent<PlayerController>();
            PhotonView otherView = other.photonView;

            if (other.animator.GetBool("invincible"))
                return;

            if (invincible > 0) {
                otherView.RPC("Powerdown", RpcTarget.All, false);
                return;
            }

            if (state == Enums.PowerupState.Giant || other.state == Enums.PowerupState.Giant) {
                if (state == Enums.PowerupState.Giant && other.state == Enums.PowerupState.Giant) {
                    otherView.RPC("Knockback", RpcTarget.All, otherObj.transform.position.x < body.position.x, 0, -1);
                    photonView.RPC("Knockback", RpcTarget.All, otherObj.transform.position.x > body.position.x, 0, -1);
                } else if (state == Enums.PowerupState.Giant) {
                    otherView.RPC("Powerdown", RpcTarget.All, false);
                }
                return;
            }

            if (contact.normal.y > 0 || (other.inShell && body.position.y > other.body.position.y)) {
                //hit them from above
                bounce = !groundpound;
                drill = false;
                if (state == Enums.PowerupState.Mini && !groundpound) {
                    photonView.RPC("PlaySound", RpcTarget.All, "enemy/shell_kick");
                } else if (other.state == Enums.PowerupState.Mini && (groundpound || drill)) {
                    otherView.RPC("Powerdown", RpcTarget.All, false);
                    bounce = false;
                } else {
                    otherView.RPC("Knockback", RpcTarget.All, otherObj.transform.position.x < body.position.x, (groundpound || drill) && state != Enums.PowerupState.Mini ? 2 : 1, photonView.ViewID);
                }
                body.velocity = new Vector2(previousFrameVelocity.x, body.velocity.y);
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
    }
    protected void OnTriggerEnter2D(Collider2D collider) {
        if (!photonView.IsMine || dead)
            return;

        HoldableEntity holdable = collider.gameObject.GetComponentInParent<HoldableEntity>();
        if (holdable && (holding == holdable || (holdingOld == holdable && throwInvincibility > 0)))
            return;
        KillableEntity killable = collider.gameObject.GetComponentInParent<KillableEntity>();
        if (killable && !killable.dead) {
            killable.InteractWithPlayer(this);
            return;
        }

        GameObject obj = collider.gameObject;
        switch (obj.tag) {
            case "bigstar":
                if (!knockback)
                    photonView.RPC("CollectBigStar", RpcTarget.AllViaServer, obj.transform.parent.gameObject.GetPhotonView().ViewID);
                break;
            case "loosecoin":
                Transform parent = obj.transform.parent;
                photonView.RPC("CollectCoin", RpcTarget.AllViaServer, parent.gameObject.GetPhotonView().ViewID, parent.position);
                break;
            case "coin":
                photonView.RPC("CollectCoin", RpcTarget.AllViaServer, obj.GetPhotonView().ViewID, new Vector3(obj.transform.position.x, collider.transform.position.y, 0));
                break;
            case "Fireball":
                FireballMover fireball = obj.GetComponentInParent<FireballMover>();
                if (fireball.photonView.IsMine)
                    break;
                fireball.photonView.RPC("Kill", RpcTarget.All);
                if (state == Enums.PowerupState.Giant && state == Enums.PowerupState.Shell && (inShell || crouching || groundpound))
                    break;
                if (state == Enums.PowerupState.Mini) {
                    photonView.RPC("Powerdown", RpcTarget.All, false);
                } else if (state != Enums.PowerupState.Giant) {
                    photonView.RPC("Knockback", RpcTarget.All, collider.attachedRigidbody.position.x > body.position.x, 1, fireball.photonView.ViewID);
                }
                break;

        }
    }
    protected void OnTriggerStay2D(Collider2D collider) {
        GameObject obj = collider.gameObject;
        switch (obj.tag) {
            case "spinner":
                onSpinner = obj;
                break;
            case "Mushroom":
            case "BlueShell":
            case "Star":
            case "MiniMushroom":
            case "FireFlower":
            case "PropellerMushroom":
            case "MegaMushroom":
                if (!photonView.IsMine)
                    return;
                MovingPowerup powerup = obj.GetComponentInParent<MovingPowerup>();
                if (powerup.followMeCounter > 0 || powerup.ignoreCounter > 0)
                    break;
                photonView.RPC("Powerup", RpcTarget.AllViaServer, powerup.photonView.ViewID, obj.tag, obj.transform.parent.gameObject.GetPhotonView().ViewID);
                Destroy(collider);
                break;
            case "poison":
                if (!photonView.IsMine)
                    return;
                photonView.RPC("Death", RpcTarget.All, false);
                break;
        }
    }
    protected void OnTriggerExit2D(Collider2D collider) {
        if (collider.CompareTag("spinner"))
            onSpinner = null;
    }
    #endregion

    #region -- CONTROLLER FUNCTIONS --
    protected void OnMovement(InputValue value) {
        if (!photonView.IsMine || GameManager.Instance.paused) 
            return;
        joystick = value.Get<Vector2>();
    }

    protected void OnJump(InputValue value) {
        if (!photonView.IsMine || GameManager.Instance.paused) 
            return;
        jumpHeld = value.Get<float>() >= 0.5f;
        if (jumpHeld)
            jumpBuffer = 0.15f;
    }

    protected void OnSprint(InputValue value) {
        if (!photonView.IsMine || GameManager.Instance.paused) 
            return;
        running = value.Get<float>() >= 0.5f;
    }

    protected void OnPowerupAction(InputValue value) {
        if (!photonView.IsMine || dead || GameManager.Instance.paused)
            return;
        powerupButtonHeld = value.Get<float>() >= 0.5f;
        if (!powerupButtonHeld || knockback || pipeEntering || GameManager.Instance.gameover) 
            return;

        switch (state) {
            case Enums.PowerupState.FireFlower:
                if (onLeft || onRight || groundpound || triplejump || holding || flying || drill || crouching || sliding)
                    return;

                int count = 0;
                foreach (FireballMover existingFire in FindObjectsOfType<FireballMover>()) {
                    if (existingFire.photonView.IsMine && ++count >= 2)
                        return;
                }

                PhotonNetwork.Instantiate("Prefabs/Fireball", body.position + new Vector2(facingRight ? 0.3f : -0.3f, 0.4f), Quaternion.identity, 0, new object[] { !facingRight });
                photonView.RPC("PlaySound", RpcTarget.All, "player/fireball");
                animator.SetTrigger("fireball");
                break;

            case Enums.PowerupState.PropellerMushroom:
                if (groundpound || knockback || holding || (flying && drill) || propeller || crouching || sliding)
                    return;

                photonView.RPC("StartPropeller", RpcTarget.All);
                break;
        }
    }
    [PunRPC]
    protected void StartPropeller() {
        PlaySound("player/propeller_start");
        body.velocity = new Vector2(body.velocity.x, propellerLaunchVelocity);
        propeller = true;
        flying = false;
        propellerTimer = 1f;
        crouching = false;
        onGround = false;
        doGroundSnap = false;
        body.position += Vector2.up * 0.15f;
    }

    protected void OnItem() {
        if (!photonView.IsMine || storedPowerup == null || storedPowerup.Length <= 0 || GameManager.Instance.paused || dead) 
            return;

        SpawnItem(storedPowerup);
        storedPowerup = null;
    }

    protected void OnPause() {
        if (!photonView.IsMine)
            return;
        PlaySound("pause");
        GameManager.Instance.Pause();
    }
    #endregion

    #region -- POWERUP / POWERDOWN --
    [PunRPC]
    protected void Powerup(int actor, string powerup, int powerupViewId) {
        if (!PhotonView.Find(actor))
            return;
        bool stateUp = false;
        Enums.PowerupState previous = state;
        string powerupSfx = "powerup";
        string store = null;
        switch (powerup) {
        case "Mushroom": 
            if (state <= Enums.PowerupState.Small) {
                state = Enums.PowerupState.Large;
                stateUp = true;
                transform.localScale = Vector3.one;
            } else if (storedPowerup == null || storedPowerup == "") {
                store = powerup;
            }
            break;
        
        case "FireFlower": 
            if (state != Enums.PowerupState.Giant && state != Enums.PowerupState.FireFlower) {
                state = Enums.PowerupState.FireFlower;
                stateUp = true;
                transform.localScale = Vector3.one;
            } else {
                store = powerup;
            }
            break;
        
        case "PropellerMushroom": 
            if (state != Enums.PowerupState.Giant && state != Enums.PowerupState.PropellerMushroom) {
                state = Enums.PowerupState.PropellerMushroom;
                stateUp = true;
                transform.localScale = Vector3.one;
            } else {
                store = powerup;
            }
            break;

        case "Star": 
            invincible = 10f;
            stateUp = true;
            break;
        
        case "MiniMushroom": 
            if (state == Enums.PowerupState.Mini || state == Enums.PowerupState.Giant) {
                store = powerup;
            } else {
                stateUp = true;
                state = Enums.PowerupState.Mini;
                transform.localScale = Vector3.one / 2;
                powerupSfx = "powerup-mini";
            }
            break;
        
        case "BlueShell": 
            if (state == Enums.PowerupState.Shell || state == Enums.PowerupState.Giant) {
                store = powerup;
            } else {
                stateUp = true;
                state = Enums.PowerupState.Shell;
                transform.localScale = Vector3.one;
            }
            break;
        
        case "MegaMushroom":
            if (state == Enums.PowerupState.Giant) {
                store = powerup;
                break;
            }
            state = Enums.PowerupState.Giant;
            stateUp = true;
            powerupSfx = "powerup-mega";
            giantStartTimer = giantStartTime;
            groundpound = false;
            crouching = false;
            propeller = false;
            flying = false;
            drill = false;
            giantTimer = 15f;
            transform.localScale = Vector3.one;
            Instantiate(Resources.Load("Prefabs/Particle/GiantPowerup"), transform.position, Quaternion.identity);

            break;
        }
        if (store != null)
            storedPowerup = store;

        if (stateUp) {
            previousState = previous;
            PlaySound("player/" + powerupSfx);
            powerupFlash = 2;
            crouching |= ForceCrouchCheck();
            propeller = false;
            drill &= flying;
            propellerTimer = 0;
        } else {
            PlaySound("player/reserve_item_store");
        }

        PhotonView view = PhotonView.Find(powerupViewId);
        if (view.IsMine)
            PhotonNetwork.Destroy(view);
        DestroyImmediate(view.gameObject);
    }

    [PunRPC]
    protected void Powerdown(bool ignoreInvincible = false) {
        if (!ignoreInvincible && hitInvincibilityCounter > 0)
            return;

        previousState = state;
        bool nowDead = false;

        switch (state) {
            case Enums.PowerupState.Mini:
            case Enums.PowerupState.Small:
                if (photonView.IsMine)
                    photonView.RPC("Death", RpcTarget.All, false);
                nowDead = true;
                break;
            case Enums.PowerupState.Large:
                state = Enums.PowerupState.Small;
                powerupFlash = 2f;
                SpawnStar();
                break;
            case Enums.PowerupState.FireFlower:
            case Enums.PowerupState.PropellerMushroom:
            case Enums.PowerupState.Shell:
                state = Enums.PowerupState.Large;
                propeller = false;
                powerupFlash = 2f;
                propellerTimer = 0;
                SpawnStar();
                break;
        }

        if (!nowDead) {
            hitInvincibilityCounter = 3f;
            PlaySound("player/powerdown");
        }
    }
    #endregion

    #region -- PHOTON SETTERS --
    [PunRPC]
    protected void SetCoins(int coins) {
        this.coins = coins;
    }
    [PunRPC]
    protected void SetStars(int stars) {
        this.stars = stars;
    }
    #endregion

    #region -- COIN / STAR COLLECTION --
    [PunRPC]
    protected void CollectBigStar(int starID) {
        PhotonView view = PhotonView.Find(starID);
        if (view == null)
            return;
        GameObject star = view.gameObject;
        StarBouncer starScript = star.GetComponent<StarBouncer>();
        if (starScript.readyForUnPassthrough > 0 && starScript.creator == photonView.ViewID)
            return;

        if (photonView.IsMine) {
            photonView.RPC("SetStars", RpcTarget.Others, ++stars);
            if (starScript.stationary)
                GameManager.Instance.SendAndExecuteEvent(Enums.NetEventIds.ResetTiles, null, SendOptions.SendReliable);
        }

        GameManager.Instance.CheckForWinner();
        Instantiate(Resources.Load("Prefabs/Particle/StarCollect"), star.transform.position, Quaternion.identity);
        PlaySound("player/star_collect", 999);
        if (view.IsMine)
            PhotonNetwork.Destroy(view);
        DestroyImmediate(star);
    }

    [PunRPC]
    protected void CollectCoin(int coinID, Vector3 position) {
        if (coinID != -1) {
            PhotonView coinView = PhotonView.Find(coinID);
            if (!coinView)
                return;
            GameObject coin = coinView.gameObject;
            if (coin.CompareTag("loosecoin")) {
                if (coin.GetPhotonView().IsMine)
                    PhotonNetwork.Destroy(coin);
                DestroyImmediate(coin);
            } else {
                SpriteRenderer renderer = coin.GetComponent<SpriteRenderer>();
                if (!renderer.enabled)
                    return;
                renderer.enabled = false;
                coin.GetComponent<BoxCollider2D>().enabled = false;
            }
        }
        Instantiate(Resources.Load("Prefabs/Particle/CoinCollect"), position, Quaternion.identity);

        coins++;

        PlaySound("player/coin");
        GameObject num = (GameObject) Instantiate(Resources.Load("Prefabs/Particle/Number"), position, Quaternion.identity);
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

    public void SpawnItem(string item = null) {
        if (item == null)
            item = Utils.GetRandomItem(stars).prefab;

        PhotonNetwork.Instantiate("Prefabs/Powerup/" + item, body.position + new Vector2(0, 5), Quaternion.identity, 0, new object[]{photonView.ViewID});
        photonView.RPC("PlaySound", RpcTarget.All, "player/reserve_item_use");
    }

    void SpawnStar() {
        if (stars <= 0) 
            return;
        stars--;
        if (!PhotonNetwork.IsMasterClient) 
            return;

        GameObject star = PhotonNetwork.InstantiateRoomObject("Prefabs/BigStar", transform.position, Quaternion.identity, 0, new object[]{starDirection, photonView.ViewID});
        StarBouncer sb = star.GetComponent<StarBouncer>();
        sb.photonView.TransferOwnership(PhotonNetwork.MasterClient);
        photonView.RPC("SetStars", RpcTarget.Others, stars);
        starDirection = !starDirection;
    }
    #endregion

    #region -- DEATH / RESPAWNING --
    [PunRPC]
    protected void Death(bool deathplane) {
        dead = true;
        onSpinner = null;
        pipeEntering = null;
        propeller = false;
        propellerSpinTimer = 0;
        flying = false;
        drill = false;
        animator.SetBool("flying", false);
        onLeft = false;
        onRight = false;
        skidding = false;
        turnaround = false;
        inShell = false;
        knockback = false;
        animator.SetBool("knockback", false);
        animator.Play("deadstart", state >= Enums.PowerupState.Large ? 1 : 0);
        PlaySound("player/death");
        SpawnStar();
        if (holding) {
            holding.photonView.RPC("Throw", RpcTarget.All, !facingRight, true);
            holding = null;
        }
        if (deathplane)
            transform.position = body.position += Vector2.down * 20;
    }

    [PunRPC]
    public void PreRespawn() {
        if (--lives == 0) {
            GameManager.Instance.CheckForWinner();
            if (photonView.IsMine)
                PhotonNetwork.Destroy(photonView);
            return;
        }
        cameraController.currentPosition = transform.position = body.position = GameManager.Instance.GetSpawnpoint(playerId);
        cameraController.scrollAmount = 0;
        cameraController.Update();
        gameObject.layer = DEFAULT_LAYERID;
        state = Enums.PowerupState.Small;
        dead = false;
        animator.SetTrigger("respawn");
        invincible = 0;
        giantTimer = 0;
        giantEndTimer = 0;
        giantStartTimer = 0;
        groundpound = false;
        body.isKinematic = false;

        GameObject particle = (GameObject) Instantiate(Resources.Load("Prefabs/Particle/Respawn"), body.position, Quaternion.identity);
        if (photonView.IsMine)
            particle.GetComponent<RespawnParticle>().player = this;
        gameObject.SetActive(false);
    }

    [PunRPC]
    public void Respawn() {
        gameObject.SetActive(true);
        dead = false;
        state = Enums.PowerupState.Small;
        previousState = Enums.PowerupState.Small;
        body.velocity = Vector2.zero;
        onLeft = false;
        onRight = false;
        flying = false;
        propeller = false;
        crouching = false;
        onGround = false;
        jumpBuffer = 0;
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
        Instantiate(Resources.Load("Prefabs/Particle/Puff"), transform.position, Quaternion.identity);
    }
    #endregion

    #region -- SOUNDS / PARTICLES --
    [PunRPC]
    protected void PlaySoundEverywhere(string sound) {
        GameManager.Instance.sfx.PlayOneShot((AudioClip)Resources.Load("Sound/" + sound));
    }
    [PunRPC]
    protected void PlaySound(string sound, float volume) {
        float volumeByRange = 1;

        sfx.PlayOneShot((AudioClip) Resources.Load("Sound/" + sound), Mathf.Clamp01(volumeByRange * volume));
    }
    [PunRPC]
    protected void PlaySound(string sound) {
        PlaySound(sound, 1);
    }

    [PunRPC]
    protected void SpawnParticle(string particle, Vector2 worldPos) {
        Instantiate(Resources.Load(particle), worldPos, Quaternion.identity);
    }
    
    [PunRPC]
    protected void SpawnParticle(string particle, Vector2 worldPos, Vector3 rot) {
        Instantiate(Resources.Load(particle), worldPos, Quaternion.Euler(rot));
    }
    protected void GiantFootstep() {
        cameraController.screenShakeTimer = 0.15f;
        SpawnParticle("Prefabs/Particle/GroundpoundDust", body.position + new Vector2(facingRight ? 0.5f : -0.5f, 0));
        //TODO: find stomp sound
    }
    protected void Footstep(int layer) {
        if ((state <= Enums.PowerupState.Small ? 0 : 1) != layer)
            return;
        if (state == Enums.PowerupState.Giant)
            return;
        if (doIceSkidding && skidding) {
            PlaySound("player/ice-skid");
            return;
        }
        if (propeller) {
            PlaySound("player/propeller_kick");
            return;
        }
        if (Mathf.Abs(body.velocity.x) < walkingMaxSpeed)
            return;

        PlaySound("player/walk" + (footstepMaterial != "" ? "-" + footstepMaterial : "") + (step ? "-2" : ""), Mathf.Abs(body.velocity.x) / (runningMaxSpeed + 4));
        step = !step;
    }
    #endregion

    #region -- TILE COLLISIONS --
    void HandleGiantTiles(bool pipes) {
        if (!photonView.IsMine)
            return;
        Vector2 checkSize = hitboxes[0].size * transform.lossyScale * 1.1f;
        Vector2 normalizedVelocity = body.velocity;
        if (!groundpound)
            normalizedVelocity.y = Mathf.Max(0, body.velocity.y);

        Vector2 offset = Vector2.zero;
        if (singlejump && onGround)
            offset = Vector2.down / 2f;

        Vector2 checkPosition = body.position + (2 * Time.fixedDeltaTime * normalizedVelocity) + new Vector2(0, checkSize.y / 2) + offset;

        Vector3Int minPos = Utils.WorldToTilemapPosition(checkPosition - (checkSize / 2), wrap: false);
        Vector3Int size = Utils.WorldToTilemapPosition(checkPosition + (checkSize / 2), wrap: false) - minPos;


        for (int x = 0; x <= size.x; x++) {
            for (int y = 0; y <= size.y; y++) {
                Vector3Int tileLocation = new(minPos.x + x, minPos.y + y, 0);
                Utils.WrapTileLocation(ref tileLocation);

                InteractableTile.InteractionDirection dir = InteractableTile.InteractionDirection.Up;
                if (y == 0 && singlejump && onGround) {
                    dir = InteractableTile.InteractionDirection.Down;
                } else if (tileLocation.x / 2f < checkPosition.x && y != size.y) {
                    dir = InteractableTile.InteractionDirection.Left;
                } else if (tileLocation.x / 2f > checkPosition.x && y != size.y) {
                    dir = InteractableTile.InteractionDirection.Right;
                }

                BreakablePipeTile pipe = GameManager.Instance.tilemap.GetTile<BreakablePipeTile>(tileLocation);
                if (pipe && (pipe.upsideDownPipe || !pipes || groundpound))
                    continue;

                InteractWithTile(tileLocation, dir);
            }
        }
        if (pipes)
            for (int x = 0; x <= size.x; x++) {
                for (int y = size.y; y >= 0; y--) {
                    Vector3Int tileLocation = new(minPos.x + x, minPos.y + y, 0);
                    Utils.WrapTileLocation(ref tileLocation);

                    InteractableTile.InteractionDirection dir = InteractableTile.InteractionDirection.Up;
                    if (y == 0 && singlejump && onGround) {
                        dir = InteractableTile.InteractionDirection.Down;
                    } else if (tileLocation.x / 2f < checkPosition.x && y != size.y) {
                        dir = InteractableTile.InteractionDirection.Left;
                    } else if (tileLocation.x / 2f > checkPosition.x && y != size.y) {
                        dir = InteractableTile.InteractionDirection.Right;
                    }

                    BreakablePipeTile pipe = GameManager.Instance.tilemap.GetTile<BreakablePipeTile>(tileLocation);
                    if (!pipe || !pipe.upsideDownPipe || dir == InteractableTile.InteractionDirection.Up) 
                        continue;

                    InteractWithTile(tileLocation, dir);
                }
            }
    }

    int InteractWithTile(Vector3Int tilePos, InteractableTile.InteractionDirection direction) {
        if (!photonView.IsMine) 
            return 0;

        TileBase tile = GameManager.Instance.tilemap.GetTile(tilePos);
        if (!tile) 
            return -1;
        if (tile is InteractableTile it)
            return it.Interact(this, direction, Utils.TilemapToWorldPosition(tilePos)) ? 1 : 0;

        return 0;
    }
    #endregion

    #region -- KNOCKBACK --

    [PunRPC]
    protected void Knockback(bool fromRight, int starsToDrop, int attackerView) {
        if (invincible > 0 || knockback || hitInvincibilityCounter > 0) 
            return;
        knockback = true;
        PhotonView attacker = PhotonNetwork.GetPhotonView(attackerView);
        if (attacker)
            SpawnParticle("Prefabs/Particle/PlayerBounce", attacker.transform.position);
        
        if ((fromRight && Physics2D.Raycast(body.position + new Vector2(0, 0.2f), Vector2.left, 0.3f, ONLY_GROUND_MASK)) ||
            (!fromRight && Physics2D.Raycast(body.position + new Vector2(0, 0.2f), Vector2.right, 0.3f, ONLY_GROUND_MASK))) {
            
            fromRight = !fromRight;
        }
        body.velocity = new Vector2((fromRight ? -1 : 1) * 3 * (starsToDrop + 1) * (state == Enums.PowerupState.Giant ? 3 : 1), 0);
        inShell = false;
        facingRight = !fromRight;
        groundpound = false;
        flying = false;
        propeller = false;
        drill = false;
        body.gravityScale = normalGravity;
        while (starsToDrop-- > 0)
            SpawnStar();
    }

    [PunRPC]
    protected void ResetKnockback() {
        hitInvincibilityCounter = 2f;
        bounce = false;
        knockback = false;
    }
    #endregion

    #region -- ENTITY HOLDING --
    [PunRPC]
    protected void HoldingWakeup() {
        holding = null;
        holdingOld = null;
        throwInvincibility = 0;
        Powerdown(false);
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
    #endregion

    void HandleSliding(bool up) {
        if (groundpound) {
            if (onGround) {
                if (state == Enums.PowerupState.Giant) {
                    groundpound = false;
                    groundpoundCounter = 0.5f;
                    return;
                } if (!inShell && Mathf.Abs(floorAngle) >= 20) {
                    groundpound = false;
                    sliding = true;
                    alreadyGroundpounded = true;
                    body.velocity = new Vector2(-Mathf.Sign(floorAngle) * groundpoundVelocity, 0);
                } else if (up || !crouching) {
                    groundpound = false;
                    groundpoundCounter = state == Enums.PowerupState.Giant ? 0.4f : 0.25f;
                }
            }
            if (up && state != Enums.PowerupState.Giant)
                groundpound = false;
        }
        if (crouching && Mathf.Abs(floorAngle) >= 20 && !inShell && state != Enums.PowerupState.Giant) {
            sliding = true;
            crouching = false;
            alreadyGroundpounded = true;
        }
        if (sliding && onGround && Mathf.Abs(floorAngle) > 20) {
            float angleDeg = floorAngle * Mathf.Deg2Rad;

            bool uphill = Mathf.Sign(floorAngle) == Mathf.Sign(body.velocity.x);
            float speed = runningMaxSpeed * Time.fixedDeltaTime * 4f * (uphill && Mathf.Abs(body.velocity.x) > 1  ? 0.7f : 1f);

            float newX = Mathf.Clamp(body.velocity.x - (Mathf.Sin(angleDeg) * speed), -(runningMaxSpeed * 1.3f), runningMaxSpeed * 1.3f);
            float newY = Mathf.Sin(angleDeg) * newX;
            body.velocity = new Vector2(newX, newY);
        }
            
        if (up || (Mathf.Abs(floorAngle) < 20 && onGround && Mathf.Abs(body.velocity.x) < 0.1)) {
            sliding = false;
            alreadyGroundpounded = false;
        }
    }

    void HandleSlopes() {
        if (!onGround) {
            floorAngle = 0;
            return;
        }
        BoxCollider2D mainCollider = hitboxes[0];
        RaycastHit2D hit = Physics2D.BoxCast(body.position + (Vector2.up * 0.05f), new Vector2(mainCollider.size.x * transform.lossyScale.x, 0.1f), 0, body.velocity.normalized, (body.velocity * Time.fixedDeltaTime).magnitude, ANY_GROUND_MASK);
        if (hit) {
            //hit ground
            float angle = Vector2.SignedAngle(Vector2.up, hit.normal); 
            if (angle < -89 || angle > 89) 
                return;
            floorAngle = angle;

            float change = Mathf.Sin(angle * Mathf.Deg2Rad) * body.velocity.x * 1.25f;
            body.velocity = new Vector2(body.velocity.x, change);
            onGround = true;
            doGroundSnap = true;
        } else {
            hit = Physics2D.BoxCast(body.position + (Vector2.up * 0.05f), new Vector2(mainCollider.size.x - 0.1f, 0.1f) * transform.lossyScale, 0, Vector2.down, 0.3f, ANY_GROUND_MASK);
            if (hit) {
                float angle = Vector2.SignedAngle(Vector2.up, hit.normal); 
                if (angle < -89 || angle > 89) 
                    return;
                floorAngle = angle;

                float change = Mathf.Sin(angle * Mathf.Deg2Rad) * body.velocity.x * 1.25f;
                body.velocity = new Vector2(body.velocity.x, change);
                onGround = true;
                doGroundSnap = true;
            } else {
                floorAngle = 0;
            }
        }
        if (joystick.x == 0 && !inShell && !sliding && Mathf.Abs(floorAngle) > 40 && state != Enums.PowerupState.Giant) {
            //steep slope, continously walk downwards
            float autowalkMaxSpeed = floorAngle / 30;
            if (Mathf.Abs(body.velocity.x) > autowalkMaxSpeed)
                return;
            float newX = Mathf.Clamp(body.velocity.x - (autowalkMaxSpeed * Time.fixedDeltaTime), -Mathf.Abs(autowalkMaxSpeed), Mathf.Abs(autowalkMaxSpeed));
            body.velocity = new Vector2(newX, Mathf.Sin(floorAngle * Mathf.Deg2Rad) * newX);
        }
    }

    bool colliding = true;
    void HandleTemporaryInvincibility() {
        bool shouldntCollide = (hitInvincibilityCounter > 0) || knockback;
        if (shouldntCollide && colliding) {
            colliding = false;
            foreach (var player in GameManager.Instance.allPlayers) {
                foreach (BoxCollider2D hitbox in hitboxes) {
                    foreach (BoxCollider2D otherHitbox in player.hitboxes)
                        Physics2D.IgnoreCollision(hitbox, otherHitbox, true);
                }
            }
        } else if (!shouldntCollide && !colliding) {
            colliding = true;
            foreach (var player in GameManager.Instance.allPlayers) {
                foreach (BoxCollider2D hitbox in hitboxes) {
                    foreach (BoxCollider2D otherHitbox in player.hitboxes)
                        Physics2D.IgnoreCollision(hitbox, otherHitbox, false);
                }
            }
        }
    }

    bool GroundSnapCheck() {
        if ((body.velocity.y > 0 && !onGround)|| !doGroundSnap || pipeEntering) 
            return false;
       
        BoxCollider2D hitbox = hitboxes[0];
        RaycastHit2D hit = Physics2D.BoxCast(body.position + new Vector2(0, 0.1f), new Vector2(hitbox.size.x * transform.lossyScale.x, 0.05f), 0, Vector2.down, 0.4f, ANY_GROUND_MASK);
        if (hit) {
            body.position = new Vector2(body.position.x, hit.point.y + Physics2D.defaultContactOffset);
            return true;
        }
        return false;
    }

    #region -- PIPES --

    void DownwardsPipeCheck() {
        if (!photonView.IsMine || !(crouching || sliding) || state == Enums.PowerupState.Giant || !onGround || knockback || inShell) 
            return;

        foreach (RaycastHit2D hit in Physics2D.RaycastAll(body.position, Vector2.down, 0.5f)) {
            GameObject obj = hit.transform.gameObject;
            if (!obj.CompareTag("pipe"))
                continue;
            PipeManager pipe = obj.GetComponent<PipeManager>();
            if (pipe.miniOnly && state != Enums.PowerupState.Mini) 
                continue;
            
            //Enter pipe
            pipeEntering = pipe;
            pipeDirection = Vector2.down;

            body.velocity = Vector2.down;
            transform.position = body.position = new Vector2(obj.transform.position.x, transform.position.y);

            photonView.RPC("PlaySound", RpcTarget.All, "player/pipe");
            crouching = false;
            sliding = false;
            drill = false;
            break;
        }
    }

    void UpwardsPipeCheck() {
        if (!photonView.IsMine || !hitRoof || joystick.y < analogDeadzone || state == Enums.PowerupState.Giant) 
            return;

        //todo: change to nonalloc?
        foreach (RaycastHit2D hit in Physics2D.RaycastAll(body.position, Vector2.up, 1f)) {
            GameObject obj = hit.transform.gameObject;
            if (!obj.CompareTag("pipe"))
                continue;
            PipeManager pipe = obj.GetComponent<PipeManager>();
            if (pipe.miniOnly && state != Enums.PowerupState.Mini) 
                continue;

            //pipe found
            pipeEntering = pipe;
            pipeDirection = Vector2.up;

            body.velocity = Vector2.up;
            transform.position = body.position = new Vector2(obj.transform.position.x, transform.position.y);
                
            photonView.RPC("PlaySound", RpcTarget.All, "player/pipe");
            crouching = false;
            sliding = false;
            propeller = false;
            flying = false;
            break;
        }
    }
    #endregion

    void HandleCrouching(bool crouchInput) {
        if (sliding || propeller)  
            return;
        if (state == Enums.PowerupState.Giant) {
            crouching = false;
            return;
        }
        bool prevCrouchState = crouching;
        crouching = ((onGround && crouchInput) || (!onGround && crouchInput && crouching) || (crouching && ForceCrouchCheck())) && !holding;
        if (crouching && !prevCrouchState)
            //crouch start sound
            PlaySound("player/crouch");
    }

    bool ForceCrouchCheck() {
        if (state < Enums.PowerupState.Large) 
            return false;
        float width = hitboxes[0].bounds.extents.x;

        bool triggerState = Physics2D.queriesHitTriggers;
        Physics2D.queriesHitTriggers = false;
        
        bool ret = Physics2D.BoxCast(body.position + new Vector2(0, 0.86f / 2f + 0.1f), new Vector2(width, 0.76f), 0, Vector2.zero, 0, ONLY_GROUND_MASK);
        
        Physics2D.queriesHitTriggers = triggerState;
        return ret;
    }

    void HandleWallslide(bool leftWall, bool jump, bool holdingDirection) {
        triplejump = false;
        doublejump = false;
        singlejump = false;

        body.velocity = new Vector2(0, Mathf.Max(body.velocity.y, wallslideSpeed));
            
        if (jump && body.velocity.y <= wallslideSpeed/4f) {
            Vector2 offset = new(hitboxes[0].size.x / 2f * (leftWall ? -1 : 1), hitboxes[0].size.y / 2f);
            photonView.RPC("SpawnParticle", RpcTarget.All, "Prefabs/Particle/WalljumpParticle", body.position + offset, leftWall ? Vector3.zero : Vector3.up * 180);
        
            onLeft = false;
            onRight = false;
            body.velocity = new Vector2(runningMaxSpeed * (3/4f) * (leftWall ? 1 : -1), walljumpVelocity);
            walljumping = 0.5f;
            facingRight = leftWall;
            singlejump = true;
            doublejump = false;
            triplejump = false;
            onGround = false;
            photonView.RPC("PlaySound", RpcTarget.All, "player/walljump");
            photonView.RPC("PlaySound", RpcTarget.All, character.soundFolder + "/walljump_" + Random.Range(1, 3));
        }

        if (!holdingDirection) {
            body.position += new Vector2(0.05f * (leftWall ? 1 : -1), 0);
            onLeft = false;
            onRight = false;
        }
    }
    
    void HandleJumping(bool jump) {
        if (knockback || drill || groundpound || groundpoundCounter > 0 || (state == Enums.PowerupState.Giant && singlejump)) 
            return;

        bool topSpeed = Mathf.Abs(body.velocity.x) + 0.1f > (runningMaxSpeed * (invincible > 0 ? 2 : 1));
        if (bounce || (jump && (onGround || (koyoteTime < 0.2f && !propeller)))) {
            koyoteTime = 1;
            jumpBuffer = 0;
            skidding = false;
            turnaround = false;
            sliding = false;
            alreadyGroundpounded = false;
            drill = false;
            flying &= bounce;
            propeller &= bounce;

            if (onSpinner && state != Enums.PowerupState.Giant && !inShell && !holding && !(crouching && state == Enums.PowerupState.Shell)) {
                photonView.RPC("PlaySound", RpcTarget.All, character.soundFolder + "/spinner_launch");
                photonView.RPC("PlaySound", RpcTarget.All, "player/spinner_launch");
                body.velocity = new Vector2(body.velocity.x, launchVelocity);
                flying = true;
                onGround = false;
                return;
            }

            float vel = jumpVelocity + Mathf.Abs(body.velocity.x)/8f * (state == Enums.PowerupState.Giant ? 1.5f : 1f);
            bool canSpecialJump = !flying && !propeller && topSpeed && landing < 0.1f && !holding && !triplejump && !crouching && !inShell && invincible <= 0 && ((body.velocity.x < 0 && !facingRight) || (body.velocity.x > 0 && facingRight)) && !Physics2D.Raycast(body.position + new Vector2(0, 0.1f), Vector2.up, 1f, ONLY_GROUND_MASK);
            float jumpBoost = 0;

            if (canSpecialJump && singlejump) {
                //Double jump
                singlejump = false;
                doublejump = true;
                triplejump = false;
                photonView.RPC("PlaySound", RpcTarget.All, character.soundFolder + "/double_jump_" + Random.Range(1, 3));
            } else if (canSpecialJump && doublejump) {
                //Triple Jump
                singlejump = false;
                doublejump = false;
                triplejump = true;
                jumpBoost = 0.5f;
                photonView.RPC("PlaySound", RpcTarget.All, character.soundFolder + "/triple_jump");
            } else {
                //Normal jump
                singlejump = true;
                doublejump = false;
                triplejump = false;
            }
            body.velocity = new Vector2(body.velocity.x, vel + jumpBoost);
            onGround = false;
            doGroundSnap = false;
            body.position += Vector2.up * 0.075f;

            if (!bounce) {
                //play jump sound
                string sound = state switch { 
                    Enums.PowerupState.Mini => "jump_mini", 
                    Enums.PowerupState.Giant => "jump_mega", 
                    _ => "jump"
                };
                photonView.RPC("PlaySound", RpcTarget.All, "player/" + sound);
            }
            bounce = false;
        }
    }

    void HandleWalkingRunning(bool left, bool right) {
        if (groundpound || groundpoundCounter > 0 || sliding || knockback || pipeEntering || !(walljumping <= 0 || onGround)) 
            return;

        iceSliding = false;
        if (!left && !right) {
            skidding = false;
            turnaround = false;
            if (doIceSkidding)
                iceSliding = true;
        }

        if (Mathf.Abs(body.velocity.x) < 0.5f || !onGround)
            skidding = false;

        if (inShell) {
            body.velocity = new Vector2(runningMaxSpeed * (facingRight ? 1 : -1), body.velocity.y);
            return;
        }

        if ((left && right) || !(left || right)) 
            return;

        float invincibleSpeedBoost = invincible > 0 ? 2f : 1;
        float airPenalty = onGround ? 1 : 0.5f;
        float xVel = body.velocity.x;
        float runSpeedTotal = runningMaxSpeed * invincibleSpeedBoost;
        float walkSpeedTotal = walkingMaxSpeed * invincibleSpeedBoost;
        bool reverseSlowing = onGround && ((left && body.velocity.x > 0.02) || (right && body.velocity.x < -0.02));
        float reverseFloat = reverseSlowing && doIceSkidding ? 0.4f : 1;
        float turnaroundSpeedBoost = turnaround && !reverseSlowing ? 5 : 1;
        float stationarySpeedBoost = Mathf.Abs(body.velocity.x) <= 0.005f ? 1f : 1f;
        float propellerBoost = propellerTimer > 0 ? 2.5f : 1;

        if ((crouching && !onGround && state != Enums.PowerupState.Shell) || !crouching) {
            
            if (left) {
                if (functionallyRunning && !flying && xVel <= -(walkingMaxSpeed - 0.3f)) {
                    skidding = false;
                    turnaround = false;
                    if (xVel > -runSpeedTotal) {
                        float change = propellerBoost * invincibleSpeedBoost * invincibleSpeedBoost * invincibleSpeedBoost * turnaroundSpeedBoost * runningAcceleration * airPenalty * stationarySpeedBoost * Time.fixedDeltaTime;    
                        body.velocity += new Vector2(change * -1, 0);
                    }
                } else {
                    if (xVel > -walkSpeedTotal) {
                        float change = propellerBoost * invincibleSpeedBoost * reverseFloat * turnaroundSpeedBoost * walkingAcceleration * stationarySpeedBoost * Time.fixedDeltaTime;
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
                        float change = propellerBoost * invincibleSpeedBoost * invincibleSpeedBoost * invincibleSpeedBoost * turnaroundSpeedBoost * runningAcceleration * airPenalty * stationarySpeedBoost * Time.fixedDeltaTime;
                        body.velocity += new Vector2(change * 1, 0);
                    }
                } else {
                    if (xVel < walkSpeedTotal) {
                        float change = propellerBoost * invincibleSpeedBoost * reverseFloat * turnaroundSpeedBoost * walkingAcceleration * stationarySpeedBoost * Time.fixedDeltaTime;
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

        inShell |= state == Enums.PowerupState.Shell && onGround && !inShell && functionallyRunning && !holding && Mathf.Abs(xVel) + 0.25f >= runningMaxSpeed && landing > 0.15f;
        if (onGround)
            body.velocity = new Vector2(body.velocity.x, 0);
    }

    bool HandleStuckInBlock() {
        if (!body || hitboxes == null) 
            return false;
        Vector2 checkPos = body.position + new Vector2(0, hitboxes[0].size.y/4f);
        if (!Utils.IsTileSolidAtWorldLocation(checkPos)) {
            stuckInBlock = false;
            return false;
        }
        stuckInBlock = true;
        body.gravityScale = 0;
        onGround = true;
        if (!Utils.IsTileSolidAtWorldLocation(checkPos + new Vector2(0, 0.3f))) {
            transform.position = body.position = new Vector2(body.position.x, Mathf.Floor((checkPos.y + 0.3f) * 2) / 2);
            return true;
        } else if (!Utils.IsTileSolidAtWorldLocation(checkPos - new Vector2(0, 0.3f))) {
            transform.position = body.position = new Vector2(body.position.x, Mathf.Floor((checkPos.y - 0.3f) * 2) / 2);
            return true;
        } else if (!Utils.IsTileSolidAtWorldLocation(checkPos + new Vector2(0.25f, 0))) {
            body.velocity = Vector2.right * 2f;
            return true;
        } else if (!Utils.IsTileSolidAtWorldLocation(checkPos + new Vector2(-0.25f, 0))) {
            body.velocity = Vector2.left * 2f;
            return true;
        }
        RaycastHit2D rightRaycast = Physics2D.Raycast(checkPos, Vector2.right, 15, ONLY_GROUND_MASK);
        RaycastHit2D leftRaycast = Physics2D.Raycast(checkPos, Vector2.left, 15, ONLY_GROUND_MASK);
        float rightDistance = 0, leftDistance = 0;
        if (rightRaycast) 
            rightDistance = rightRaycast.distance;
        if (leftRaycast) 
            leftDistance = leftRaycast.distance;
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
        if (!pipeEntering) 
            TickCounter(ref invincible, 0, delta);

        TickCounter(ref throwInvincibility, 0, delta);
        TickCounter(ref jumpBuffer, 0, delta);
        TickCounter(ref walljumping, 0, delta);
        if (giantStartTimer <= 0) 
            TickCounter(ref giantTimer, 0, delta);
        TickCounter(ref giantStartTimer, 0, delta);
        TickCounter(ref groundpoundCounter, 0, delta);
        TickCounter(ref giantEndTimer, 0, delta);
        TickCounter(ref groundpoundDelay, 0, delta);
        TickCounter(ref hitInvincibilityCounter, 0, delta);
        TickCounter(ref propellerSpinTimer, 0, delta);
        TickCounter(ref propellerTimer, 0, delta);
    }

    [PunRPC]
    public void FinishMegaMario(bool success) {
        if (success) {
            PlaySoundEverywhere(character.soundFolder + "/mega_start");
        } else {
            //hit a wall, cancel
            giantSavedVelocity = Vector2.zero;
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
        RaycastHit2D hit = Physics2D.BoxCast(body.position + new Vector2(0, 1.6f), new Vector2(0.45f, 2.6f), 0, Vector2.zero, 0, ONLY_GROUND_MASK);
        photonView.RPC("FinishMegaMario", RpcTarget.All, !(bool) hit);
    }
    void HandleFacingDirection() {
        //Facing direction
        bool right = joystick.x > analogDeadzone;
        bool left = joystick.x < -analogDeadzone;
        if (!sliding && !groundpound) {
            if (doIceSkidding && !inShell && !groundpound) {
                if (right || left)
                    facingRight = right;
            } else if (giantStartTimer <= 0 && giantEndTimer <= 0 && !skidding && !(animator.GetCurrentAnimatorStateInfo(0).IsName("turnaround") || turnaround)) {
                if (knockback || (onGround && state != Enums.PowerupState.Giant && Mathf.Abs(body.velocity.x) > 0.05f)) {
                    facingRight = body.velocity.x > 0;
                } else if (((walljumping <= 0 && !inShell) || giantStartTimer > 0) && (right || left)) {
                    facingRight = right;
                }
                if (!inShell && ((Mathf.Abs(body.velocity.x) < 0.5f && crouching) || doIceSkidding) && (right || left))
                    facingRight = right;
            }
        }
    }
    void HandleMovement(float delta) {
        functionallyRunning = running || state == Enums.PowerupState.Giant || propeller;

        if (photonView.IsMine && body.position.y + transform.lossyScale.y < GameManager.Instance.GetLevelMinY()) {
            //death via pit
            photonView.RPC("Death", RpcTarget.All, true);
            return;
        }

        bool paused = GameManager.Instance.paused;

        if (giantStartTimer > 0) {
            body.velocity = Vector2.zero;
            if (giantStartTimer - delta <= 0) {
                //start by checking bounding
                giantStartTimer = 0;
                if (photonView.IsMine)
                    StartCoroutine(CheckForGiantStartupTiles());
            } else {
                body.isKinematic = true;
                if (animator.GetCurrentAnimatorClipInfo(1).Length <= 0 || animator.GetCurrentAnimatorClipInfo(1)[0].clip.name != "mega-scale")
                    animator.Play("mega-scale", 1);
            }
            return;
        }
        if (giantEndTimer > 0) {
            body.velocity = Vector2.zero;
            body.isKinematic = true;

            if (giantEndTimer - delta <= 0) {
                hitInvincibilityCounter = 3f;
                body.velocity = giantSavedVelocity;
                animator.enabled = true;
                body.isKinematic = false;
            }
            return;
        }

        if (state == Enums.PowerupState.Giant) {
            HandleGiantTiles(true);
            if (onGround && singlejump) {
                photonView.RPC("SpawnParticle", RpcTarget.All, "Prefabs/Particle/GroundpoundDust", body.position);
                singlejump = false;
            }
            invincible = 0;
        }

        //pipes > stuck in block, else the animation gets janked.
        if (pipeEntering || giantStartTimer > 0 || giantEndTimer > 0)
            return;
        if (HandleStuckInBlock())
            return;

        //Pipes
        if (!paused) {
            DownwardsPipeCheck();
            UpwardsPipeCheck();
        }

        if (knockback) {
            onLeft = false;
            onRight = false;
            crouching = false;
            inShell = false;
            body.velocity -= body.velocity * (delta * 2f);
            if (photonView.IsMine && onGround && Mathf.Abs(body.velocity.x) < 0.2f)
                photonView.RPC("ResetKnockback", RpcTarget.All);
            if (holding) {
                holding.photonView.RPC("Throw", RpcTarget.All, !facingRight, true);
                holding = null;
            }
        }

        //activate blocks jumped into
        if (hitRoof) {
            body.velocity = new Vector2(body.velocity.x, Mathf.Min(body.velocity.y, -0.1f));
            bool tempHitBlock = false;
            foreach (Vector3Int tile in tilesJumpedInto) {
                int temp = InteractWithTile(tile, InteractableTile.InteractionDirection.Up);
                if (temp != -1)
                    tempHitBlock |= temp == 1;
            }
            if (tempHitBlock && state == Enums.PowerupState.Giant) {
                cameraController.screenShakeTimer = 0.15f;
                photonView.RPC("PlaySound", RpcTarget.All, "player/block_bump");
            }
        }

        bool right = joystick.x > analogDeadzone && !paused;
        bool left = joystick.x < -analogDeadzone && !paused;
        bool crouch = joystick.y < -analogDeadzone && !paused;
        bool up = joystick.y > analogDeadzone && !paused;
        bool jump = jumpBuffer > 0 && (onGround || koyoteTime < 0.1f || onLeft || onRight) && !paused;
        
        if (drill) {
            propellerSpinTimer = 0;
            if (propeller && up)
                drill = false;
        }

        if (propellerTimer > 0)
            body.velocity = new Vector2(body.velocity.x, propellerLaunchVelocity - (propellerTimer < .4f ? (1 - (propellerTimer / .4f)) * propellerLaunchVelocity : 0));

        if (!crouch)
            alreadyGroundpounded = false;

        if (powerupButtonHeld) {
            if (body.velocity.y < -0.1f && propeller && !drill && propellerSpinTimer < propellerSpinTime / 4f) {
                propellerSpinTimer = propellerSpinTime;
                photonView.RPC("PlaySound", RpcTarget.All, "player/propeller_spin");
            }
        }

        if (holding) {
            onLeft = false;
            onRight = false;
            holding.holderOffset = new Vector2((facingRight ? 1 : -1) * 0.25f, state >= Enums.PowerupState.Large ? 0.5f : 0.25f);
        }

        //throwing held item
        if ((!functionallyRunning || state == Enums.PowerupState.Mini || state == Enums.PowerupState.Giant || invincible > 0 || flying || propeller) && holding) {
            bool throwLeft = !facingRight;
            if (left)
                throwLeft = true;
            if (right)
                throwLeft = false;

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
        if (state != Enums.PowerupState.Shell || !functionallyRunning)
            inShell = false;

        if (inShell) {
            crouch = true;
            if (photonView.IsMine && (hitLeft || hitRight)) {
                foreach (var tile in tilesHitSide)
                    InteractWithTile(tile, InteractableTile.InteractionDirection.Up);
                facingRight = hitLeft;
                photonView.RPC("PlaySound", RpcTarget.All, "player/block_bump");
            }
        }

        //Ground
        if (onGround) {
            if (photonView.IsMine && hitRoof && crushGround && body.velocity.y <= 0.1 && state != Enums.PowerupState.Giant)
                //Crushed.
                photonView.RPC("Powerdown", RpcTarget.All, true);

            koyoteTime = 0;
            onLeft = false;
            onRight = false;
            if (drill)
                photonView.RPC("SpawnParticle", RpcTarget.All, "Prefabs/Particle/GroundpoundDust", body.position);

            if (onSpinner && Mathf.Abs(body.velocity.x) < 0.3f && !holding && state != Enums.PowerupState.Giant) {
                Transform spnr = onSpinner.transform;
                if (body.position.x > spnr.transform.position.x + 0.02f) {
                    body.position -= new Vector2(0.01f * 60f, 0) * Time.fixedDeltaTime;
                } else if (body.position.x < spnr.transform.position.x - 0.02f) {
                    body.position += new Vector2(0.01f * 60f, 0) * Time.fixedDeltaTime;
                }
            }
        } else {
            koyoteTime += delta;
            landing = 0;
            skidding = false;
            turnaround = false;
        }

        //Crouching
        HandleCrouching(crouch);

        if (onLeft)
            HandleWallslide(true, jump, left);
        if (onRight)
            HandleWallslide(false, jump, right);

        if ((walljumping <= 0 || onGround) && !groundpound) {
            //Normal walking/running
            HandleWalkingRunning(left, right);

            //Jumping
            HandleJumping(jump);
        }

        if (crouch && !alreadyGroundpounded)
            HandleGroundpoundStart(left, right);
        HandleGroundpound();

        if (state == Enums.PowerupState.Giant && giantTimer <= 0) {
            giantSavedVelocity = state != Enums.PowerupState.Large ? body.velocity : Vector2.zero;
            giantEndTimer = giantStartTime / 2f;
            state = Enums.PowerupState.Large;
            body.isKinematic = true;
            animator.enabled = false;
            photonView.RPC("PlaySoundEverywhere", RpcTarget.All, "player/mega-end");
        }

        //slow-rise check
        if (flying || propeller) {
            body.gravityScale = flyingGravity;
        } else {
            float gravityModifier = state != Enums.PowerupState.Mini ? 1f : 0.4f;
            if (body.velocity.y > 2.5) {
                if (jump || jumpHeld) {
                    body.gravityScale = slowriseGravity;
                } else {
                    body.gravityScale = normalGravity * 1.5f * gravityModifier;
                }
            } else if (onGround || groundpound) {
                body.gravityScale = 0f;
            } else {
                body.gravityScale = normalGravity * (gravityModifier / 1.2f);
            }
        }

        if (groundpound && groundpoundCounter <= 0)
            body.velocity = new Vector2(0f, -groundpoundVelocity);

        if (!inShell && onGround && !(sliding && Mathf.Abs(floorAngle) > 20)) {
            bool abovemax;
            float invincibleSpeedBoost = invincible > 0 ? 2f : 1;
            bool uphill = Mathf.Sign(floorAngle) == Mathf.Sign(body.velocity.x);
            float max = (functionallyRunning ? runningMaxSpeed : walkingMaxSpeed) * invincibleSpeedBoost * (uphill ? (1 - (Mathf.Abs(floorAngle) / 270f)) : 1);
            if (!sliding && left && !crouching) {
                abovemax = body.velocity.x < -max;
            } else if (!sliding && right && !crouching) {
                abovemax = body.velocity.x > max;
            } else if (Mathf.Abs(floorAngle) > 40) {
                abovemax = Mathf.Abs(body.velocity.x) > (Mathf.Abs(floorAngle) / 30f);
            } else {
                abovemax = true;
            }
            //Friction...
            if (abovemax) {
                body.velocity *= 1 - (delta * tileFriction * (knockback ? 3f : 4f) * (sliding ? 0.7f : 1f));
                if (Mathf.Abs(body.velocity.x) < 0.15f)
                    body.velocity = new Vector2(0, body.velocity.y);
            }
        }
        //Terminal velocity
        float terminalVelocityModifier = state == Enums.PowerupState.Mini ? 0.65f : 1f;
        if (flying) {
            if (drill) {
                body.velocity = new Vector2(Mathf.Max(-1.5f, Mathf.Min(1.5f, body.velocity.x)), -drillVelocity);
            } else {
                body.velocity = new Vector2(Mathf.Clamp(body.velocity.x, -walkingMaxSpeed, walkingMaxSpeed), Mathf.Max(body.velocity.y, -flyingTerminalVelocity));
            }
        } else if (propeller) {
            if (drill) {
                body.velocity = new Vector2(Mathf.Max(-1.5f, Mathf.Min(1.5f, body.velocity.x)), -drillVelocity);
            } else {
                float htv = walkingMaxSpeed * 1.18f + (propellerTimer * 2f);
                body.velocity = new Vector2(Mathf.Clamp(body.velocity.x, -htv, htv), Mathf.Max(body.velocity.y, propellerSpinTimer > 0 ? -propellerSpinFallSpeed : -propellerFallSpeed));
            }
        } else if (!groundpound) {
            body.velocity = new Vector2(body.velocity.x, Mathf.Max(body.velocity.y, terminalVelocity * terminalVelocityModifier));
        }
        if (!onGround)
            body.velocity = new Vector2(Mathf.Max(-runningMaxSpeed * 1.2f, Mathf.Min(runningMaxSpeed * 1.2f, body.velocity.x)), body.velocity.y);

        HandleSlopes();
        HandleSliding(up);
        HandleFacingDirection();

        if (crouching || sliding || skidding) {
            onLeft = false;
            onRight = false;
        }
        if (onGround) {
            if (propellerTimer < 0.5f) {
                propeller = false;
                propellerTimer = 0;
            }
            flying = false;
            drill = false;
            if (triplejump && landing == 0 && !(left || right) && !groundpound) {
                if (!doIceSkidding)
                    body.velocity = Vector2.zero;
                animator.Play("jumplanding", state >= Enums.PowerupState.Large ? 1 : 0);
                photonView.RPC("SpawnParticle", RpcTarget.All, "Prefabs/Particle/GroundpoundDust", body.position);
            }
            if ((landing += delta) > 0.1f) {
                singlejump = false;
                doublejump = false;
                triplejump = false;
            }
        }
    }

    void HandleGroundpoundStart(bool left, bool right) {
        if (!photonView.IsMine)
            return;
        if (onGround || knockback || groundpound || drill 
            || holding || crouching || sliding 
            || onLeft || onRight || groundpoundDelay > 0) 
            
            return;
        if (!propeller && !flying && (left || right)) 
            return;

        if (flying || propeller) {
            //start drill
            if (body.velocity.y < 0 && propellerTimer <= 0) {
                drill = true;
                hitBlock = true;
            }
        } else {
            //start groundpound
            //check if high enough above ground
            if (Physics2D.BoxCast(body.position, hitboxes[0].size, 0, Vector2.down, 0.25f * (state == Enums.PowerupState.Giant ? 2.5f : 1), ANY_GROUND_MASK)) 
                return;
            
            onLeft = false;
            onRight = false;
            groundpound = true;
            singlejump = false;
            doublejump = false;
            triplejump = false;
            hitBlock = true;
            sliding = false;
            body.velocity = Vector2.zero;
            groundpoundCounter = groundpoundTime * (state == Enums.PowerupState.Giant ? 1.5f : 1);
            photonView.RPC("PlaySound", RpcTarget.All, "player/groundpound");
            alreadyGroundpounded = true;
            groundpoundDelay = 0.7f;
        }
    }

    void HandleGroundpound() {
        if (!(photonView.IsMine && onGround && (groundpound || drill) && hitBlock)) 
            return;
        bool tempHitBlock = false, hitAnyBlock = false;
        foreach (Vector3Int tile in tilesStandingOn) {
            int temp = InteractWithTile(tile, InteractableTile.InteractionDirection.Down);
            if (temp != -1) {
                hitAnyBlock = true;
                tempHitBlock |= temp == 1;
            }
        }
        hitBlock = tempHitBlock;
        if (drill) {
            flying &= hitBlock;
            propeller &= hitBlock;
            drill = hitBlock;
            if (hitBlock)
                onGround = false;
        } else {
            //groundpound
            if (hitAnyBlock) {
                if (state != Enums.PowerupState.Giant) {
                    photonView.RPC("PlaySound", RpcTarget.All, "player/groundpound-landing" + (state == Enums.PowerupState.Mini ? "-mini" : ""));
                    photonView.RPC("SpawnParticle", RpcTarget.All, "Prefabs/Particle/GroundpoundDust", body.position);
                } else {
                    cameraController.screenShakeTimer = 0.15f;
                }
            }
            if (hitBlock) {
                koyoteTime = 1.5f;
            } else if (state == Enums.PowerupState.Giant) {
                photonView.RPC("PlaySound", RpcTarget.All, "player/groundpound_mega");
                photonView.RPC("SpawnParticle", RpcTarget.All, "Prefabs/Particle/GroundpoundDust", body.position);
                cameraController.screenShakeTimer = 0.35f;
            }
        }
    }
    void OnDrawGizmos() {
        Gizmos.DrawRay(body.position, body.velocity);
        Gizmos.DrawCube(body.position + new Vector2(0, hitboxes[0].size.y/2f * transform.lossyScale.y) + (body.velocity * Time.fixedDeltaTime), hitboxes[0].size * transform.lossyScale);
    }
}