using UnityEngine;
using Photon.Pun;
using UnityEngine.Tilemaps;
using NSMB.Utils;

public class KoopaWalk : HoldableEntity {

    protected int combo;
    public float walkSpeed, kickSpeed, wakeup = 15;
    public bool red, blue, shell, stationary, upsideDown, canBeFlipped = true, flipXFlip = false;
    public bool putdown = false;
    public float wakeupTimer;
    private BoxCollider2D worldHitbox;
    Vector2 blockOffset = new Vector3(0, 0.05f), velocityLastFrame;
    private float dampVelocity, speed;

    [SerializeField] Vector2 outShellHitboxSize, inShellHitboxSize;
    [SerializeField] Vector2 outShellHitboxOffset, inShellHitboxOffset;

    new void Start() {
        base.Start();
        hitbox = transform.GetChild(0).GetComponent<BoxCollider2D>();
        worldHitbox = GetComponent<BoxCollider2D>();

        body.velocity = new Vector2(-walkSpeed, 0);
    }

    new void FixedUpdate() {
        if (GameManager.Instance && GameManager.Instance.gameover) {
            body.velocity = Vector2.zero;
            body.angularVelocity = 0;
            animator.enabled = false;
            body.isKinematic = true;
            return;
        }
        base.FixedUpdate();

        if (Frozen || dead)
            return;

        sRenderer.flipX = !left ^ flipXFlip;

        if (upsideDown) {
            dampVelocity = Mathf.Min(dampVelocity + Time.fixedDeltaTime * 3, 1);
            transform.eulerAngles = new Vector3(
                transform.eulerAngles.x,
                transform.eulerAngles.y,
                Mathf.Lerp(transform.eulerAngles.z, 180f, dampVelocity) + (wakeupTimer < 3 && wakeupTimer > 0 ? (Mathf.Sin(wakeupTimer * 120f) * 15f) : 0));
        } else {
            dampVelocity = 0;
            transform.eulerAngles = new Vector3(
                transform.eulerAngles.x,
                transform.eulerAngles.y,
                wakeupTimer < 3 && wakeupTimer > 0 ? (Mathf.Sin(wakeupTimer * 120f) * 15f) : 0);
        }

        if (shell) {
            worldHitbox.size = hitbox.size = inShellHitboxSize;
            worldHitbox.offset = hitbox.offset = inShellHitboxOffset;

            if (stationary) {
                if (physics.onGround)
                    body.velocity = new Vector2(0, body.velocity.y);
                if ((wakeupTimer -= Time.fixedDeltaTime) < 0) {
                    if (photonView.IsMine)
                        photonView.RPC("WakeUp", RpcTarget.All);
                }
            } else {
                wakeupTimer = wakeup;
            }
        } else {
            worldHitbox.size = hitbox.size = outShellHitboxSize;
            worldHitbox.offset = hitbox.offset = outShellHitboxOffset;
        }

        if (physics.hitRight && !left) {
            if (photonView && photonView.IsMine) {
                photonView.RPC("Turnaround", RpcTarget.All, false, velocityLastFrame.x);
            } else {
                Turnaround(false, velocityLastFrame.x);
            }
        } else if (physics.hitLeft && left) {
            if (photonView && photonView.IsMine) {
                photonView.RPC("Turnaround", RpcTarget.All, true, velocityLastFrame.x);
            } else {
                Turnaround(true, velocityLastFrame.x);
            }
        }

        if (physics.onGround && Physics2D.Raycast(body.position, Vector2.down, 0.5f, Layers.MaskAnyGround) && red && !shell) {
            Vector3 redCheckPos = body.position + new Vector2(0.1f * (left ? -1 : 1), 0);
            if (GameManager.Instance)
                Utils.WrapWorldLocation(ref redCheckPos);

            if (!Physics2D.Raycast(redCheckPos, Vector2.down, 0.5f, Layers.MaskAnyGround)) {
                if (photonView && photonView.IsMine) {
                    photonView.RPC("Turnaround", RpcTarget.All, left, velocityLastFrame.x);
                } else {
                    Turnaround(left, velocityLastFrame.x);
                }
            }
        }

        if (physics.onGround) {
            if (stationary) {
                body.velocity = new(body.velocity.x, 0);
            } else {
                body.velocity = new Vector2((shell ? speed : walkSpeed) * (left ? -1 : 1), 0);
            }
        }

        velocityLastFrame = body.velocity;

        if (!photonView.IsMineOrLocal())
            return;

        HandleTile();
        animator.SetBool("shell", shell || holder != null);
        if (!blue)
            animator.SetFloat("xVel", Mathf.Abs(body.velocity.x));
    }
    public override void InteractWithPlayer(PlayerController player) {
        Vector2 damageDirection = (player.body.position - body.position).normalized;
        bool attackedFromAbove = Vector2.Dot(damageDirection, Vector2.up) > 0f;
        if (holder)
            return;

        if (!attackedFromAbove && player.state == Enums.PowerupState.BlueShell && player.crouching && !player.inShell) {
            player.body.velocity = new(0, player.body.velocity.y);
            photonView.RPC("SetLeft", RpcTarget.All, damageDirection.x > 0);
        } else if (player.sliding || player.inShell || player.invincible > 0 || player.state == Enums.PowerupState.MegaMushroom || player.drill) {
            bool originalFacing = player.facingRight;
            if (shell && !stationary && player.inShell && Mathf.Sign(body.velocity.x) != Mathf.Sign(player.body.velocity.x))
                player.photonView.RPC("Knockback", RpcTarget.All, player.body.position.x < body.position.x, 0, true, photonView.ViewID);
            photonView.RPC("SpecialKill", RpcTarget.All, !originalFacing, false, 0);
        } else if (player.groundpound && player.state != Enums.PowerupState.MiniMushroom && attackedFromAbove) {
            photonView.RPC("EnterShell", RpcTarget.All);
            if (!blue) {
                photonView.RPC("Kick", RpcTarget.All, player.body.position.x < body.position.x, 1f, player.groundpound);
                player.photonView.RPC("SetHoldingOld", RpcTarget.All, photonView.ViewID);
                previousHolder = player;
            }
        } else if (attackedFromAbove && (!shell || !IsStationary())) {
            if (player.state != Enums.PowerupState.MiniMushroom || player.groundpound) {
                photonView.RPC("EnterShell", RpcTarget.All);
                if (player.state == Enums.PowerupState.MiniMushroom)
                    player.groundpound = false;
            }
            player.photonView.RPC("PlaySound", RpcTarget.All, Enums.Sounds.Enemy_Generic_Stomp);
            player.bounce = true;
        } else {
            if (shell && IsStationary()) {
                if (!holder) {
                    if (player.CanPickup()) {
                        photonView.RPC("Pickup", RpcTarget.All, player.photonView.ViewID);
                        player.photonView.RPC("SetHolding", RpcTarget.All, photonView.ViewID);
                    } else {
                        photonView.RPC("Kick", RpcTarget.All, player.body.position.x < body.position.x, Mathf.Abs(player.body.velocity.x) / player.runningMaxSpeed, player.groundpound);
                        player.photonView.RPC("SetHoldingOld", RpcTarget.All, photonView.ViewID);
                        previousHolder = player;
                    }
                }
            } else if (player.hitInvincibilityCounter <= 0) {
                player.photonView.RPC("Powerdown", RpcTarget.All, false);
                if (!shell)
                    photonView.RPC("SetLeft", RpcTarget.All, damageDirection.x < 0);
            }
        }
    }

    [PunRPC]
    public override void Freeze(int cube) {
        base.Freeze(cube);
        stationary = true;
    }

    [PunRPC]
    public override void Kick(bool fromLeft, float kickFactor, bool groundpound) {
        left = !fromLeft;
        stationary = false;
        speed = kickSpeed + 1.5f * kickFactor;
        body.velocity = new Vector2(speed * (left ? -1 : 1), groundpound ? 3.5f : 0);
        photonView.RPC("PlaySound", RpcTarget.All, Enums.Sounds.Enemy_Shell_Kick);
    }

    [PunRPC]
    public override void Throw(bool facingLeft, bool crouch) {
        if (holder == null)
            return;

        stationary = crouch;
        speed = kickSpeed + 1.5f * (Mathf.Abs(holder.body.velocity.x) / holder.runningMaxSpeed);
        if (Utils.IsTileSolidAtWorldLocation(body.position))
            transform.position = body.position = new Vector2(holder.transform.position.x, transform.position.y);

        previousHolder = holder;
        holder = null;
        shell = true;
        photonView.TransferOwnership(PhotonNetwork.MasterClient);
        left = facingLeft;
        if (crouch) {
            body.velocity = new Vector2(2f * (facingLeft ? -1 : 1), body.velocity.y);
            putdown = true;
        } else {
            body.velocity = new Vector2(speed * (facingLeft ? -1 : 1), body.velocity.y);
        }
    }
    [PunRPC]
    public void WakeUp() {
        shell = false;
        body.velocity = new Vector2(-walkSpeed, 0);
        left = true;
        upsideDown = false;
        stationary = false;
        if (holder && photonView.IsMine)
            holder.photonView.RPC("HoldingWakeup", RpcTarget.All);
        holder = null;
        previousHolder = null;
    }
    [PunRPC]
    public void EnterShell() {
        if (blue) {
            if (photonView.IsMine)
                PhotonNetwork.Destroy(photonView);

            if (PhotonNetwork.IsMasterClient)
                PhotonNetwork.Instantiate("Prefabs/Powerup/BlueShell", transform.position, Quaternion.identity, 0, new object[]{0.1f});
        }
        body.velocity = Vector2.zero;
        wakeupTimer = wakeup;
        combo = 0;
        shell = true;
        stationary = true;
    }

    public new void OnTriggerEnter2D(Collider2D collider) {
        if (!shell)
            base.OnTriggerEnter2D(collider);

        if (!photonView.IsMineOrLocal() || !shell || IsStationary() || putdown || dead)
            return;

        GameObject obj = collider.gameObject;
        KillableEntity killa = obj.GetComponentInParent<KillableEntity>();
        switch (obj.tag) {
        case "koopa":
        case "bobomb":
        case "bulletbill":
        case "frozencube":
        case "goomba":
            if (killa.dead)
                break;
            killa.photonView.RPC("SpecialKill", RpcTarget.All, killa.body.position.x > body.position.x, false, combo++);
            if (holder)
                photonView.RPC("SpecialKill", RpcTarget.All, killa.body.position.x < body.position.x, false, combo++);
            break;
        case "piranhaplant":
            if (killa.dead)
                break;
            killa.photonView.RPC("Kill", RpcTarget.All);
            if (holder)
                photonView.RPC("Kill", RpcTarget.All);

            break;
        case "coin":
            if (!holder && !stationary && previousHolder)
                previousHolder.photonView.RPC("CollectCoin", RpcTarget.AllViaServer, obj.GetPhotonView().ViewID, new Vector3(obj.transform.position.x, collider.transform.position.y, 0));
            break;
        case "loosecoin":
            if (!holder && !stationary && previousHolder) {
                Transform parent = obj.transform.parent;
                previousHolder.photonView.RPC("CollectCoin", RpcTarget.All, parent.gameObject.GetPhotonView().ViewID, parent.position);
            }
            break;
        }
    }

    void HandleTile() {
        if (holder)
            return;
        physics.UpdateCollisions();

        ContactPoint2D[] collisions = new ContactPoint2D[20];
        int collisionAmount = worldHitbox.GetContacts(collisions);
        for (int i = 0; i < collisionAmount; i++) {
            var point = collisions[i];
            Vector2 p = point.point + (point.normal * -0.15f);
            if (Mathf.Abs(point.normal.x) == 1 && point.collider.gameObject.layer == Layers.LayerGround) {
                if (!putdown && shell && !stationary) {
                    Vector3Int tileLoc = Utils.WorldToTilemapPosition(p + blockOffset);
                    TileBase tile = GameManager.Instance.tilemap.GetTile(tileLoc);
                    if (tile == null)
                        continue;
                    if (!shell)
                        continue;

                    if (tile is InteractableTile it)
                        it.Interact(this, InteractableTile.InteractionDirection.Up, Utils.TilemapToWorldPosition(tileLoc));
                }
            } else if (point.normal.y > 0 && putdown) {
                body.velocity = new Vector2(0, body.velocity.y);
                putdown = false;
            }
        }
    }

    [PunRPC]
    protected void Turnaround(bool hitWallOnLeft, float x) {
        if (IsStationary())
            return;

        if (shell && hitWallOnLeft != left)
            PlaySound(Enums.Sounds.World_Block_Bump);

        left = !hitWallOnLeft;
        body.velocity = new Vector2((x > 0 ? Mathf.Abs(x) : speed) * (left ? -1 : 1), body.velocity.y);
        if (shell && !IsStationary())
            PlaySound(Enums.Sounds.World_Block_Bump);
    }

    [PunRPC]
    protected void Bump() {
        if (dead)
            return;

        if (blue) {
            if (photonView.IsMine)
                PhotonNetwork.Destroy(photonView);

            if (PhotonNetwork.IsMasterClient)
                PhotonNetwork.InstantiateRoomObject("Prefabs/Powerup/BlueShell", transform.position, Quaternion.identity);

            return;
        }
        if (!shell) {
            stationary = true;
            putdown = true;
        }
        wakeupTimer = wakeup;
        shell = true;
        upsideDown = canBeFlipped;
        photonView.RPC("PlaySound", RpcTarget.All, Enums.Sounds.Enemy_Shell_Kick);
        body.velocity = new Vector2(body.velocity.x, 5.5f);
    }

    public bool IsStationary() {
        return !holder && stationary;
    }

    [PunRPC]
    public override void Kill() {
        EnterShell();
    }

    [PunRPC]
    public override void SpecialKill(bool right, bool groundpound, int combo) {
        base.SpecialKill(right, groundpound, combo);
        shell = true;
        if (holder)
            holder.photonView.RPC("SetHolding", RpcTarget.All, -1);

        holder = null;
    }
}