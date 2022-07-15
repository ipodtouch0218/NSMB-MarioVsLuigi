using UnityEngine;
using UnityEngine.Tilemaps;
using Photon.Pun;
using NSMB.Utils;

public class BobombWalk : HoldableEntity {

    private readonly int explosionTileSize = 2;
    public float walkSpeed, kickSpeed, detonateTimer;
    public bool lit, detonated;
    float detonateCount;
    public GameObject explosion;

    new void Start() {
        base.Start();
        body.velocity = new Vector2(walkSpeed * (left ? -1 : 1), body.velocity.y);
        physics = GetComponent<PhysicsEntity>();
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


        if (lit)
            animator.SetTrigger("lit");


        if (!photonView || photonView.IsMine)
            HandleCollision();

        sRenderer.flipX = left;

        if (lit && !detonated) {
            if ((detonateCount -= Time.fixedDeltaTime) < 0) {
                if (photonView.IsMine)
                    photonView.RPC("Detonate", RpcTarget.All);
                return;
            }
            float redOverlayPercent = 5.39f/(detonateCount+2.695f)*10f % 1f;
            MaterialPropertyBlock block = new();
            block.SetFloat("FlashAmount", redOverlayPercent);
            sRenderer.SetPropertyBlock(block);
        }
    }
    [PunRPC]
    public void Detonate() {

        sRenderer.enabled = false;
        hitbox.enabled = false;
        detonated = true;

        Instantiate(explosion, transform.position, Quaternion.identity);

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
                obj.GetComponentInParent<PhotonView>().RPC("SpecialKill", RpcTarget.All, transform.position.x < obj.transform.position.x, false, 0);
                break;
            }
            }
        }

        Vector3Int tileLocation = Utils.WorldToTilemapPosition(body.position);
        Tilemap tm = GameManager.Instance.tilemap;
        for (int x = -explosionTileSize; x <= explosionTileSize; x++) {
            for (int y = -explosionTileSize; y <= explosionTileSize; y++) {
                if (Mathf.Abs(x) + Mathf.Abs(y) > explosionTileSize) continue;
                Vector3Int ourLocation = tileLocation + new Vector3Int(x, y, 0);
                Utils.WrapTileLocation(ref ourLocation);

                TileBase tile = tm.GetTile(ourLocation);
                if (tile is InteractableTile iTile) {
                    iTile.Interact(this, InteractableTile.InteractionDirection.Up, Utils.TilemapToWorldPosition(ourLocation));
                }
            }
        }
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
        PlaySound(Enums.Sounds.Enemy_Bobomb_Fuse);
    }
    [PunRPC]
    public override void Throw(bool facingLeft, bool crouch) {
        if (!holder)
            return;
        if (Utils.IsTileSolidAtWorldLocation(body.position)) {
            transform.position = body.position = new Vector2(holder.transform.position.x, transform.position.y);
        }
        holder = null;
        photonView.TransferOwnership(PhotonNetwork.MasterClient);
        left = facingLeft;
        sRenderer.flipX = left;
        if (crouch) {
            body.velocity = new Vector2(2f * (facingLeft ? -1 : 1), body.velocity.y);
        } else {
            body.velocity = new Vector2(kickSpeed * (facingLeft ? -1 : 1), body.velocity.y);
        }
    }

    [PunRPC]
    public override void Kick(bool fromLeft, float speed, bool groundpound) {
        left = !fromLeft;
        sRenderer.flipX = left;
        body.velocity = new Vector2(kickSpeed * (left ? -1 : 1), 2f);
        photonView.RPC("PlaySound", RpcTarget.All, Enums.Sounds.Enemy_Shell_Kick);
    }

    public override void InteractWithPlayer(PlayerController player) {
        Vector2 damageDirection = (player.body.position - body.position).normalized;
        bool attackedFromAbove = Vector2.Dot(damageDirection, Vector2.up) > 0.5f;

        if (!attackedFromAbove && player.state == Enums.PowerupState.BlueShell && player.crouching && !player.inShell) {
            photonView.RPC("SetLeft", RpcTarget.All, damageDirection.x > 0);
        } else if(player.sliding || player.inShell || player.invincible > 0) {
            photonView.RPC("SpecialKill", RpcTarget.All, player.body.velocity.x > 0, false, 0);
            return;
        } else if (attackedFromAbove && !lit) {
            if (player.state != Enums.PowerupState.MiniMushroom || (player.groundpound && attackedFromAbove))
                photonView.RPC("Light", RpcTarget.All);
            photonView.RPC("PlaySound", RpcTarget.All, Enums.Sounds.Enemy_Generic_Stomp);
            if (player.groundpound && player.state != Enums.PowerupState.MiniMushroom) {
                photonView.RPC("Kick", RpcTarget.All, player.body.position.x < body.position.x, Mathf.Abs(player.body.velocity.x) / player.runningMaxSpeed, player.groundpound);
            } else {
                player.bounce = true;
                player.groundpound = false;
            }
        } else {
            if (lit) {
                if (!holder) {
                    if (player.CanPickup()) {
                        photonView.RPC("Pickup", RpcTarget.All, player.photonView.ViewID);
                        player.photonView.RPC("SetHolding", RpcTarget.All, photonView.ViewID);
                    } else {
                        photonView.RPC("Kick", RpcTarget.All, player.body.position.x < body.position.x, Mathf.Abs(player.body.velocity.x) / player.runningMaxSpeed, player.groundpound);
                    }
                }
            } else if (player.hitInvincibilityCounter <= 0) {
                player.photonView.RPC("Powerdown", RpcTarget.All, false);
                photonView.RPC("SetLeft", RpcTarget.All, damageDirection.x < 0);
            }
        }
    }

    void HandleCollision() {
        if (holder)
            return;

        physics.UpdateCollisions();
        if (lit && physics.onGround) {
            body.velocity -= body.velocity * (Time.fixedDeltaTime * 3f);
            if (Mathf.Abs(body.velocity.x) < 0.05) {
                body.velocity = new Vector2(0, body.velocity.y);
            }
        }

        if (photonView && !photonView.IsMine) {
            return;
        }
        if (physics.hitRight && !left) {
            if (photonView) {
                photonView.RPC("Turnaround", RpcTarget.All, false);
            } else {
                Turnaround(false);
            }
        } else if (physics.hitLeft && left) {
            if (photonView) {
                photonView.RPC("Turnaround", RpcTarget.All, true);
            } else {
                Turnaround(true);
            }
        }

        if (physics.onGround && physics.hitRoof)
            photonView.RPC("Detonate", RpcTarget.All);
    }
    [PunRPC]
    void Turnaround(bool hitWallOnLeft) {
        left = !hitWallOnLeft;
        sRenderer.flipX = left;
        body.velocity = new Vector2(walkSpeed * (left ? -1 : 1), body.velocity.y);
        animator.SetTrigger("turnaround");
    }
}
