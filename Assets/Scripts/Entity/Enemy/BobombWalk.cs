using UnityEngine;
using UnityEngine.Tilemaps;

using Photon.Pun;
using NSMB.Utils;

public class BobombWalk : HoldableEntity {

    [SerializeField] private GameObject explosionPrefab;
    [SerializeField] private float walkSpeed = 0.6f, kickSpeed = 4.5f, detonationTime = 4f;
    [SerializeField] private int explosionTileSize = 2;

    public bool lit, detonated;

    private Vector3 previousFrameVelocity;
    private float detonateCount;

    #region Unity Methods
    public override void Start() {
        base.Start();

        body.velocity = new(walkSpeed * (left ? -1 : 1), body.velocity.y);
    }

    public override void FixedUpdate() {
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

        previousFrameVelocity = body.velocity;
    }
    #endregion

    #region Helper Methods
    public override void InteractWithPlayer(PlayerController player) {
        Vector2 damageDirection = (player.body.position - body.position).normalized;
        bool attackedFromAbove = Vector2.Dot(damageDirection, Vector2.up) > 0.5f;

        if (!attackedFromAbove && player.state == Enums.PowerupState.BlueShell && player.crouching && !player.inShell) {
            photonView.RPC("SetLeft", RpcTarget.All, damageDirection.x > 0);
        } else if (player.sliding || player.inShell || player.invincible > 0) {
            photonView.RPC("SpecialKill", RpcTarget.All, player.body.velocity.x > 0, false, player.StarCombo++);
            return;
        } else if (attackedFromAbove && !lit) {
            if (player.state != Enums.PowerupState.MiniMushroom || (player.groundpound && attackedFromAbove))
                photonView.RPC("Light", RpcTarget.All);
            photonView.RPC("PlaySound", RpcTarget.All, Enums.Sounds.Enemy_Generic_Stomp);
            if (player.groundpound && player.state != Enums.PowerupState.MiniMushroom) {
                photonView.RPC("Kick", RpcTarget.All, player.body.position.x < body.position.x, Mathf.Abs(player.body.velocity.x) / player.RunningMaxSpeed, player.groundpound);
            } else {
                player.bounce = true;
                player.groundpound = false;
            }
            player.drill = false;
        } else {
            if (lit) {
                if (!holder) {
                    if (player.CanPickup()) {
                        photonView.RPC("Pickup", RpcTarget.All, player.photonView.ViewID);
                        player.photonView.RPC("SetHolding", RpcTarget.All, photonView.ViewID);
                    } else {
                        photonView.RPC("Kick", RpcTarget.All, player.body.position.x < body.position.x, Mathf.Abs(player.body.velocity.x) / player.RunningMaxSpeed, player.groundpound);
                    }
                }
            } else if (player.hitInvincibilityCounter <= 0) {
                player.photonView.RPC("Powerdown", RpcTarget.All, false);
                photonView.RPC("SetLeft", RpcTarget.All, damageDirection.x < 0);
            }
        }
    }

    private void HandleCollision() {
        if (holder)
            return;

        physics.UpdateCollisions();
        if (lit && physics.onGround) {
            body.velocity -= body.velocity * (Time.fixedDeltaTime * 3f);
            if (Mathf.Abs(body.velocity.x) < 0.05) {
                body.velocity = new Vector2(0, body.velocity.y);
            }
        }

        if (!photonView.IsMineOrLocal())
            return;

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
            photonView.RPC("SpecialKill", RpcTarget.All);
    }
    #endregion

    #region PunRPCs
    [PunRPC]
    public void Detonate() {

        sRenderer.enabled = false;
        hitbox.enabled = false;
        detonated = true;

        Instantiate(explosionPrefab, transform.position, Quaternion.identity);

        if (!photonView.IsMine)
            return;

        RaycastHit2D[] hits = Physics2D.CircleCastAll(transform.position + new Vector3(0,0.5f), 1f, Vector2.zero);
        foreach (RaycastHit2D hit in hits) {
            GameObject obj = hit.collider.gameObject;

            if (obj == gameObject)
                continue;

            if (obj.GetComponent<KillableEntity>() is KillableEntity en) {
                en.photonView.RPC("SpecialKill", RpcTarget.All, transform.position.x < obj.transform.position.x, false, 0);
                continue;
            }

            switch (hit.collider.tag) {
            case "Player": {
                obj.GetPhotonView().RPC("Powerdown", RpcTarget.All, false);
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
        detonateCount = detonationTime;
        body.velocity = Vector2.zero;
        lit = true;
        PlaySound(Enums.Sounds.Enemy_Bobomb_Fuse);
    }

    [PunRPC]
    public override void Throw(bool facingLeft, bool crouch, Vector2 pos) {
        if (!holder)
            return;

        body.position = pos;
        if (Utils.IsTileSolidAtWorldLocation(body.position))
            transform.position = body.position = new(holder.transform.position.x, transform.position.y);

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
        body.velocity = new(kickSpeed * (left ? -1 : 1), 3f);
        PlaySound(Enums.Sounds.Enemy_Shell_Kick);
    }

    [PunRPC]
    public void Turnaround(bool hitWallOnLeft) {
        left = !hitWallOnLeft;
        sRenderer.flipX = left;
        body.velocity = new((lit ? -previousFrameVelocity.x : walkSpeed) * (left ? -1 : 1), body.velocity.y);
        animator.SetTrigger("turnaround");
    }
    #endregion
}
