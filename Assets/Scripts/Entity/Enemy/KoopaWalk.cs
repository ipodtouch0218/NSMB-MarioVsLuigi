using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using UnityEngine.Tilemaps;

public class KoopaWalk : HoldableEntity {
    [SerializeField] float walkSpeed, kickSpeed, wakeup = 15;
    [SerializeField] public bool red, blue, shell, stationary, hardkick, upsideDown;
    public bool left = true, putdown = false;
    public float wakeupTimer;
    new private BoxCollider2D collider;
    Vector2 blockOffset = new Vector3(0, 0.05f);
    private float dampVelocity;
    new void Start() {
        base.Start();
        hitbox = GetComponentInChildren<BoxCollider2D>();
        collider = GetComponent<BoxCollider2D>();

        body.velocity = new Vector2(-walkSpeed, 0);
    }

    void FixedUpdate() {
        if (GameManager.Instance && GameManager.Instance.gameover) {
            body.velocity = Vector2.zero;
            animator.enabled = false;
            body.isKinematic = true;
            return;
        }

        renderer.flipX = !left;

        if (photonView && !photonView.IsMine)
            return;

        HandleTile();
        if (!blue) {
            animator.SetBool("shell", shell || holder != null);
            animator.SetFloat("xVel", Mathf.Abs(body.velocity.x));
        }

        if (shell) {
            if (stationary) {
                if ((wakeupTimer -= Time.fixedDeltaTime) < 0) {
                    photonView.RPC("WakeUp", RpcTarget.All);
                }

            } else {
                wakeupTimer = wakeup;
            }
        }

        if ((red || blue) && !shell && !Physics2D.Raycast(transform.position + new Vector3(0.1f * (left ? -1 : 1), 0, 0), Vector2.down, 0.5f, LayerMask.GetMask("Ground", "Semisolids"))) {
            if (photonView)
                photonView.RPC("Turnaround", RpcTarget.All, left);
            else
                Turnaround(left);
        }

        if (!dead) {
            if (upsideDown) {
                dampVelocity = Mathf.Min(dampVelocity + Time.deltaTime * 3, 1);
                transform.eulerAngles = new Vector3(
                    transform.eulerAngles.x, 
                    transform.eulerAngles.y, 
                    Mathf.Lerp(transform.eulerAngles.z, 180f, dampVelocity) + (wakeupTimer < 3 && wakeupTimer > 0 ? (Mathf.Sin(wakeupTimer * 120f) * 15f) : 0));
            } else {
                dampVelocity = 0;
                transform.eulerAngles = new Vector3(
                    transform.eulerAngles.x, 
                    transform.eulerAngles.y, 
                    (wakeupTimer < 3 && wakeupTimer > 0 ? (Mathf.Sin(wakeupTimer * 120f) * 15f) : 0));
            }
        }
        if (!stationary) {
            if (shell) {
                body.velocity = new Vector2(kickSpeed * (left ? -1 : 1) * (hardkick ? 1.2f : 1f), body.velocity.y);
            } else {
                body.velocity = new Vector2(walkSpeed * (left ? -1 : 1), body.velocity.y);
            }
        }
    }
    [PunRPC]
    public override void Kick(bool fromLeft, bool groundpound) {
        left = !fromLeft;
        stationary = false;
        hardkick = groundpound;
        body.velocity = new Vector2(kickSpeed * (left ? -1 : 1) * (hardkick ? 1.2f : 1f), (hardkick ? 3.5f : 0));
        photonView.RPC("PlaySound", RpcTarget.All, "enemy/shell_kick");
    }
    [PunRPC]
    public override void Throw(bool facingLeft, bool crouch) {
        if (holder == null) {
            return;
        }
        stationary = crouch;
        hardkick = false;
        transform.position = new Vector2(holder.transform.position.x, transform.position.y);
        this.holder = null;
        shell = true;
        photonView.TransferOwnership(PhotonNetwork.MasterClient);
        this.left = facingLeft;
        if (crouch) {
            body.velocity = new Vector2(2f * (facingLeft ? -1 : 1), body.velocity.y);
            putdown = true;
        } else {
            body.velocity = new Vector2(kickSpeed * (facingLeft ? -1 : 1), body.velocity.y);
        }
    }
    [PunRPC]
    public void WakeUp() {
        shell = false;
        body.velocity = new Vector2(-walkSpeed, 0);
        left = true;
        upsideDown = false;
        stationary = false;
        if (holder)
            holder.GetPhotonView().RPC("HoldingWakeup", RpcTarget.All);
        holder = null;
    }
    [PunRPC]
    public void EnterShell() {
        if (blue) {
            if (photonView.IsMine) {
                PhotonNetwork.Destroy(photonView);
            }
            if (PhotonNetwork.IsMasterClient) {
                PhotonNetwork.Instantiate("Prefabs/Powerup/BlueShell", transform.position, Quaternion.identity);
            }
        }
        body.velocity = Vector2.zero;
        wakeupTimer = wakeup;
        shell = true;
        stationary = true;
    }

    void OnTriggerEnter2D(Collider2D collider) {
        if ((photonView && !photonView.IsMine) || !shell || IsStationary() || putdown || holder) {
            return;
        }
        GameObject obj = collider.gameObject;
        switch (obj.tag) {
        case "koopa": {
            KoopaWalk koopa = obj.GetComponentInParent<KoopaWalk>();
            if (koopa.dead) break;
            if (koopa.shell && !koopa.IsStationary()) {
                photonView.RPC("SpecialKill", RpcTarget.All, obj.transform.position.x > transform.position.x, false);
            }
            koopa.photonView.RPC("SpecialKill", RpcTarget.All, obj.transform.position.x < transform.position.x, false);
            break;
        }
        case "bobomb":
        case "bulletbill":
        case "goomba": {
            KillableEntity killa = obj.GetComponentInParent<KillableEntity>();
            if (killa.dead) break;
            killa.photonView.RPC("SpecialKill", RpcTarget.All, obj.transform.position.x > transform.position.x, false);
            break;
        }
        case "piranhaplant": {
            KillableEntity killa = obj.GetComponentInParent<KillableEntity>();
            killa.photonView.RPC("Kill", RpcTarget.All);
            break;
        }
        }
    }

    void HandleTile() {
        if (holder)
            return;
        physics.Update();
        
        bool sound = false;
        ContactPoint2D[] collisions = new ContactPoint2D[20];
        int collisionAmount = collider.GetContacts(collisions);
        for (int i = 0; i < collisionAmount; i++) {
            var point = collisions[i];
            if (Mathf.Abs(point.normal.x) > 0.2f) {
                
                if (photonView)
                    photonView.RPC("Turnaround", RpcTarget.All, point.normal.x > 0);
                else
                    Turnaround(point.normal.x > 0);

                if (!putdown && shell && !stationary) {
                    if (!sound) {
                        photonView.RPC("PlaySound", RpcTarget.All, "player/block_bump");
                        sound = true;
                    }
                    Vector3Int tileLoc = Utils.WorldToTilemapPosition(point.point + blockOffset);
                    TileBase tile = GameManager.Instance.tilemap.GetTile(tileLoc);
                    if (tile == null) continue;
                    if (!shell) continue;
                    
                    if (tile is InteractableTile) {
                        ((InteractableTile) tile).Interact(this, InteractableTile.InteractionDirection.Up, Utils.TilemapToWorldPosition(tileLoc));
                    }
                }
            } else if (point.normal.y > 0 && putdown) {
                body.velocity = new Vector2(0, body.velocity.y);
                putdown = false;
            }
        }
    }
    
    [PunRPC]
    void Turnaround(bool hitWallOnLeft) {
        if (stationary)
            return;
        left = !hitWallOnLeft;
        if (shell) {
            body.velocity = new Vector2(kickSpeed * (left ? -1 : 1) * (hardkick ? 1.2f : 1f), body.velocity.y);
        } else {
            body.velocity = new Vector2(walkSpeed * (left ? -1 : 1), body.velocity.y);
        }
    }

    [PunRPC]
    void Bump() {
        if (blue) {
            if (photonView.IsMine) {
                PhotonNetwork.Destroy(photonView);
            }
            if (PhotonNetwork.IsMasterClient) {
                PhotonNetwork.Instantiate("Prefabs/Powerup/BlueShell", transform.position, Quaternion.identity);
            }
        }
        if (!shell) {
            stationary = true;
            putdown = true;
        }
        wakeupTimer = wakeup;
        shell = true;
        upsideDown = true;
        body.velocity = new Vector2(body.velocity.x, 5.5f);
    }

    public bool IsStationary() {
        return !holder && stationary;
    }

    [PunRPC]
    public override void SpecialKill(bool right = true, bool groundpound = false) {
        base.SpecialKill(right, groundpound);
        shell = true;
        holder = null;
    } 
}