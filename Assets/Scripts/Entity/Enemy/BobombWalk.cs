using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using Photon.Pun;

public class BobombWalk : HoldableEntity {

    public float walkSpeed, kickSpeed, detonateTimer;
    bool left;
    public bool lit, detonated;
    float detonateCount;
    Vector3 startingScale;
    public GameObject explosion;

    new void Start() {
        base.Start();
        body.velocity = new Vector2(walkSpeed * (left ? -1 : 1), body.velocity.y);
        startingScale = transform.localScale;
        physics = GetComponent<PhysicsEntity>();
    }

    void FixedUpdate() {
        if (GameManager.Instance && GameManager.Instance.gameover) {
            body.velocity = Vector2.zero;
            animator.enabled = false;
            body.isKinematic = true;
            return;
        }

        if (!photonView || photonView.IsMine)
            HandleCollision();
        
        if (lit && !detonated && !dead) {
                
            if ((detonateCount -= Time.fixedDeltaTime) < 0) {
                if (photonView.IsMine)
                    photonView.RPC("Detonate", RpcTarget.All);
                return;
            }
            float redOverlayPercent = (5.39f/(detonateCount+2.695f))*10f % 1f;
            MaterialPropertyBlock block = new MaterialPropertyBlock(); 
            block.SetFloat("FlashAmount", redOverlayPercent);
            base.sRenderer.SetPropertyBlock(block);
        }
    }
    [PunRPC]
    public void Detonate() {
        
        base.sRenderer.enabled = false;
        hitbox.enabled = false;
        detonated = true;

        GameObject.Instantiate(explosion, transform.position, Quaternion.identity);

        if (!photonView.IsMine)
            return;

        RaycastHit2D[] hits = Physics2D.CircleCastAll(transform.position + new Vector3(0,0.5f), 1.2f, Vector2.zero);
        foreach (RaycastHit2D hit in hits) {
            GameObject obj = hit.collider.gameObject;
            switch (hit.collider.tag) {
            case "Player": {
                obj.GetPhotonView().RPC("Powerdown", RpcTarget.All, false);
                break;
            }
            case "goomba":
            case "koopa":
            case "bulletbill":
            case "bobomb": {
                if (obj == gameObject || obj == gameObject.transform.Find("Hitbox").gameObject)
                    continue;
                obj.GetComponentInParent<PhotonView>().RPC("SpecialKill", RpcTarget.All, transform.position.x < obj.transform.position.x, false);
                break;
            }
            }
        }

        Tilemap tm = GameManager.Instance.tilemap;
        for (int x = -1; x <= 1; x++) {
            for (int y = -1; y <= 1; y++) {
                
                Vector3Int loc = Utils.WorldToTilemapPosition(body.position) + new Vector3Int(x, y, 0);

                TileBase tile = tm.GetTile(loc);
                if (tile == null) continue;

                if (tile is InteractableTile) {
                    ((InteractableTile) tile).Interact(this, (InteractableTile.InteractionDirection.Up), Utils.TilemapToWorldPosition(loc));
                }
            }
        }
        //TODO tile breaking effects
        PhotonNetwork.Destroy(gameObject);
    }

    [PunRPC]
    public override void Kill() {
        Light();
    }
    [PunRPC]
    public void Light() {
        animator.SetTrigger("lit");
        detonateCount = detonateTimer;
        body.velocity = Vector2.zero;
        lit = true;
        PlaySound("enemy/bobomb_light");
        PlaySound("enemy/bobomb_beep");
    }
    [PunRPC]
    public override void Throw(bool facingLeft, bool crouch) {
        if (holder == null) {
            return;
        }
        this.holder = null;
        photonView.TransferOwnership(PhotonNetwork.MasterClient);
        this.left = facingLeft;
        base.sRenderer.flipX = left;
        if (crouch) {
            body.velocity = new Vector2(2f * (facingLeft ? -1 : 1), body.velocity.y);
        } else {
            body.velocity = new Vector2(kickSpeed * (facingLeft ? -1 : 1), body.velocity.y);
        }
    }

    [PunRPC]
    public override void Kick(bool fromLeft, bool groundpound) {
        left = !fromLeft;
        base.sRenderer.flipX = left;
        body.velocity = new Vector2(kickSpeed * (left ? -1 : 1), 2f);
        photonView.RPC("PlaySound", RpcTarget.All, "enemy/shell_kick");
    }

    public override void InteractWithPlayer(PlayerController player) {
        Vector2 damageDirection = (player.body.position - body.position).normalized;
        bool attackedFromAbove = Vector2.Dot(damageDirection, Vector2.up) > 0.5f;

        if (player.inShell || player.invincible > 0) {
            photonView.RPC("SpecialKill", RpcTarget.All, player.body.velocity.x > 0, false);
            return;
        }
        if (attackedFromAbove && !lit) {
            if (player.state != Enums.PowerupState.Mini || (player.groundpound && attackedFromAbove)) {
                photonView.RPC("Light", RpcTarget.All);
            }
            photonView.RPC("PlaySound", RpcTarget.All, "enemy/goomba");
            if (player.groundpound) {
                photonView.RPC("Kick", RpcTarget.All, player.body.position.x < body.position.x, player.groundpound);
            } else {
                player.bounce = true;
            }
        } else {
            if (lit) {
                if (player.state != Enums.PowerupState.Mini && !player.holding && player.running && !player.crouching && !player.flying && !player.dead && !player.onLeft && !player.onRight && !player.doublejump && !player.triplejump && !player.groundpound) {
                    photonView.RPC("Pickup", RpcTarget.All, player.photonView.ViewID);
                    player.holding = this;
                } else {
                    photonView.RPC("Kick", RpcTarget.All, player.body.position.x < body.position.x, player.groundpound);
                }
            } else {
                player.photonView.RPC("Powerdown", RpcTarget.All, false);
            }
        }
    }

    void HandleCollision() {
        if (holder)
            return;

        physics.Update();
        if (lit && physics.onGround) {
            body.velocity -= (body.velocity * (Time.fixedDeltaTime * 3f));
            if (Mathf.Abs(body.velocity.x) < 0.05) {
                body.velocity = new Vector2(0, body.velocity.y);
            }
        }
        
        if (photonView && !photonView.IsMine) {
            return;
        }
        if (physics.hitRight && !left) {
            if (photonView)
                photonView.RPC("Turnaround", RpcTarget.All, false);
            else
                Turnaround(false);
        } else if (physics.hitLeft && left) {
            if (photonView)
                photonView.RPC("Turnaround", RpcTarget.All, true);
            else
                Turnaround(true);
        }

        if (physics.onGround && physics.hitRoof) {
            photonView.RPC("Detonate", RpcTarget.All);
        }
    }
    [PunRPC]
    void Turnaround(bool hitWallOnLeft) {
        left = !hitWallOnLeft;
        base.sRenderer.flipX = left;
        body.velocity = new Vector2(walkSpeed * (left ? -1 : 1), body.velocity.y);
        animator.SetTrigger("turnaround");
    }
}
