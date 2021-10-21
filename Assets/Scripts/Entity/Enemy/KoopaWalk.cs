using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using UnityEngine.Tilemaps;

public class KoopaWalk : HoldableEntity {
    [SerializeField] float walkSpeed, kickSpeed, wakeup = 15;
    [SerializeField] public bool red, blue, shell;
    public bool left = true, putdown = false;
    float wakeupTimer;
    new private BoxCollider2D collider;
    Vector2 blockOffset = new Vector3(0, 0.05f);
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
            if (body.velocity.x == 0) {
                if ((wakeupTimer -= Time.fixedDeltaTime) < 0) {
                    photonView.RPC("WakeUp", RpcTarget.All);
                }
            } else {
                wakeupTimer = wakeup;
            }
            return;
        }

        if ((red || blue) && !shell && !Physics2D.Raycast(transform.position + new Vector3(0.1f * (left ? -1 : 1), 0, 0), Vector2.down, 0.5f, LayerMask.GetMask("Ground", "Semisolids"))) {
            if (photonView)
                photonView.RPC("Turnaround", RpcTarget.All, left);
            else
                Turnaround(left);
        }
    }
    [PunRPC]
    public override void Kick(bool fromLeft) {
        left = !fromLeft;
        body.velocity = new Vector2(kickSpeed * (left ? -1 : 1), 0);
        photonView.RPC("PlaySound", RpcTarget.All, "enemy/shell_kick");
    }
    [PunRPC]
    public override void Throw(bool facingLeft, bool crouch) {
        if (holder == null) {
            return;
        }
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
            if (koopa.shell && Mathf.Abs(koopa.body.velocity.x) > 0.5) {
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
        case "pirahnaplant": {
            KillableEntity killa = obj.GetComponentInParent<KillableEntity>();
            killa.photonView.RPC("Kill", RpcTarget.All);
            break;
        }
        }
    }

    void HandleTile() {
        if (holder)
            return;
        
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

                if (!putdown && shell) {
                    if (!sound) {
                        photonView.RPC("PlaySound", RpcTarget.All, "player/block_bump");
                        sound = true;
                    }
                    Vector3Int tileLoc = Utils.WorldToTilemapPosition(point.point + blockOffset);
                    TileBase tile = GameManager.Instance.tilemap.GetTile(tileLoc);
                    if (tile == null) continue;
                    if (!shell) continue;
                    
                    if (tile is InteractableTile) {
                        ((InteractableTile) tile).Interact(this, InteractableTile.InteractionDirection.Up, point.point);
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
        left = !hitWallOnLeft;
        if (shell) {
            body.velocity = new Vector2(kickSpeed * (left ? -1 : 1), body.velocity.y);
        } else {
            body.velocity = new Vector2(walkSpeed * (left ? -1 : 1), body.velocity.y);
        }
    }

    public bool IsStationary() {
        return !holder && Mathf.Abs(body.velocity.x) < 0.05;
    }

    [PunRPC]
    public override void SpecialKill(bool right = true, bool groundpound = false) {
        base.SpecialKill(right, groundpound);
        shell = true;
        holder = null;
    } 
}