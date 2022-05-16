using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using UnityEngine.Tilemaps;

// maybe a better name for the script
public class FrozenCube : HoldableEntity {

    private static int GROUND_LAYER_ID = -1;

    public float throwSpeed = 10f;
    bool deathCheck;
    public BoxCollider2D frozenCubeCollider;
    public SpriteRenderer spriteRenderer;

    public KillableEntity frozenEntity;
    public PlayerController frozenPlayer;

    public float fallTimer, killTimer = .5f;
    float fallTimerCount;

    public bool fastSlide, fallen, crashed, deathFlag;
    public bool kinematicEntity, flyingEntity, plantEntity;

    public float offset;
    // TODO: when ice collides with something while after being thrown it breaks

    new void Start() {
        base.Start();
        dead = false;
        holderOffset = Vector2.one;
        hitbox = GetComponentInChildren<BoxCollider2D>();
        body.velocity = Vector2.zero;

        if (GROUND_LAYER_ID == -1)
            GROUND_LAYER_ID = LayerMask.NameToLayer("Ground");

    }

    private new void LateUpdate() {
        base.LateUpdate();
        if (frozenEntity && !plantEntity && !frozenPlayer) {
            frozenEntity.transform.position = new Vector3(transform.position.x, transform.position.y - (transform.localScale.y / 4) + offset, frozenEntity.transform.position.z);
        } else if (plantEntity) {
            transform.position = new Vector3(frozenEntity.transform.position.x, frozenEntity.transform.position.y + (transform.localScale.y / 1), transform.position.z);
        }
        if (frozenEntity && (frozenEntity.dead && !dead || dead) && !deathCheck && plantEntity) {
            PhotonNetwork.Destroy(photonView);
            deathCheck = true;
        }
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

        body.mass = holder != null ? 0 : 1;

        if (frozenEntity) {
            if (plantEntity) {
                body.constraints = RigidbodyConstraints2D.FreezeRotation | RigidbodyConstraints2D.FreezePositionX | RigidbodyConstraints2D.FreezePositionY;
            } else if (!flyingEntity || fallen) {
                body.constraints = RigidbodyConstraints2D.FreezeRotation;
            }

            if (flyingEntity && !frozenEntity.dead && !dead) {
                if ((fallTimerCount -= Time.fixedDeltaTime) < 0 || holder) {
                    body.isKinematic = false;
                    fallen = true;
                } else {
                    // Do shake thing
                }
            } else if (frozenEntity && flyingEntity && (!frozenEntity.dead || !dead) ) {
                fallen = true;
			}

        } else if (!frozenPlayer) {
            PhotonNetwork.Destroy(photonView);
        }

        if (photonView && !photonView.IsMine)
            return;
        if (!dead && !plantEntity)
            HandleTile();
    }

	// Start is called before the first frame update
	public override void InteractWithPlayer(PlayerController player) {
        Vector2 damageDirection = (player.body.position - body.position).normalized;
        bool attackedFromAbove = Vector2.Dot(damageDirection, Vector2.up) > 0f;
        if (!holder && player.invincible > 0) {
            photonView.RPC("SpecialKill", RpcTarget.All, false, false);
        }
        if (holder || player.frozen)
            return;
        else if (player.groundpound && player.state != Enums.PowerupState.MiniMushroom && attackedFromAbove) {
            if (!plantEntity)
                photonView.RPC("SpecialKill", RpcTarget.All, player.body.velocity.x > 0, player.groundpound);
            else
                photonView.RPC("Kill", RpcTarget.All);
        } else if (Mathf.Abs(body.velocity.x) >= (throwSpeed/2) && !physics.hitRoof) {
            player.photonView.RPC("Knockback", RpcTarget.All, body.position.x > player.body.position.x, 1, false, photonView.ViewID);
        }
        if (!holder && !dead && !plantEntity) {
            if (player.CanPickup() && player.state != Enums.PowerupState.MiniMushroom && !player.holding && player.running && !player.propeller && !player.flying && !player.crouching && !player.dead && !player.wallSlideLeft && !player.wallSlideRight && !player.doublejump && !player.triplejump) {
                fallen = true;
                photonView.RPC("Pickup", RpcTarget.All, player.photonView.ViewID);
                player.photonView.RPC("SetHolding", RpcTarget.All, photonView.ViewID);
            } else {
                player.photonView.RPC("SetHoldingOld", RpcTarget.All, photonView.ViewID);
                previousHolder = player;
            }
        }
    }

    [PunRPC]
    public void Crashed() {
        crashed = true;
    }
    [PunRPC]
    public void setFrozenEntity(string entity, int enitiyID) {

        if (entity != "Player") {
            frozenEntity = PhotonView.Find(enitiyID).GetComponent<KillableEntity>();
            frozenEntity.photonView.RPC("Freeze", RpcTarget.All);
        }

        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        Renderer renderer = frozenEntity.GetComponent<Renderer>();
        frozenCubeCollider.size = spriteRenderer.size = GetComponent<BoxCollider2D>().size = renderer.bounds.size + (Vector3.one * 0.2f);

        switch (entity) {
        case "koopa": {
            if (frozenEntity.GetComponent<KoopaWalk>().shell || frozenEntity.GetComponent<SpinyWalk>()) {
                offset = 0.25f;
                ((KoopaWalk) frozenEntity).stationary = true;
            }
            break;
        }
        case "Player": {
            frozenPlayer = PhotonView.Find(enitiyID).GetComponent<PlayerController>();
            frozenPlayer.frozenObject = this;
            frozenPlayer.photonView.RPC("Freeze", RpcTarget.All);
            break;
        }
        case "bulletbill": {
            kinematicEntity = true;
            flyingEntity = true;
            body.isKinematic = true;
            fallTimerCount = fallTimer;
            break;
        }
        case "piranhaplant": {
            spriteRenderer.sortingOrder = -1;
            plantEntity = true;
            break;
        }
        }
    }

    [PunRPC]
    public override void Kick(bool fromLeft, float kickFactor, bool groundpound) { }

    [PunRPC]
    public override void Throw(bool facingLeft, bool crouch) {
        if (holder == null)
            return;

        fastSlide = !crouch;
        transform.position = new Vector2((holder.facingRight ? holder.transform.position.x + 0.1f : holder.transform.position.x - 0.1f), transform.position.y);

        previousHolder = holder;
        holder = null;

        photonView.TransferOwnership(PhotonNetwork.MasterClient);

        if (frozenEntity && flyingEntity) {
            fallTimer = -1;
            fallen = true;
            frozenEntity.body.isKinematic = false;
            body.isKinematic = false;
        }

        if (crouch) {
            body.velocity = new Vector2(2f * (facingLeft ? -1 : 1), body.velocity.y);
        } else {
            body.velocity = new Vector2(throwSpeed * (facingLeft ? -1 : 1), body.velocity.y);
        }
    }

    new void OnTriggerEnter2D(Collider2D collider) {
        if ((photonView && !photonView.IsMine) || dead)
            return;

        GameObject obj = collider.gameObject;
        KillableEntity killa = obj.GetComponentInParent<KillableEntity>();
        switch (obj.tag) {
        case "koopa":
        case "bobomb":
        case "bulletbill":
        case "goomba":
        case "frozencube":
            if (dead || killa.dead)
                break;
            if (Mathf.Abs(body.velocity.x) >= 1.5f)
                killa.photonView.RPC("SpecialKill", RpcTarget.All, killa.body.position.x > body.position.x, false);
            break;
        case "piranhaplant":
            if (killa.dead)
                break;
            if (Mathf.Abs(body.velocity.x) >= 2f * 1 && (physics.hitLeft || physics.hitRight))
                killa.photonView.RPC("Kill", RpcTarget.All);
            if (holder && (Mathf.Abs(body.velocity.x) >= 2f * 1 && (physics.hitLeft || physics.hitRight)))
                photonView.RPC("Kill", RpcTarget.All);

            break;
        case "coin":
            (holder != null ? holder : previousHolder).photonView.RPC("CollectCoin", RpcTarget.AllViaServer, obj.GetPhotonView().ViewID, new Vector3(obj.transform.position.x, collider.transform.position.y, 0));
            break;
        case "loosecoin":
            if (!holder && previousHolder) {
                Transform parent = obj.transform.parent;
                previousHolder.photonView.RPC("CollectCoin", RpcTarget.All, parent.gameObject.GetPhotonView().ViewID, parent.position);
            }
            break;
        }
    }

    void HandleTile() {
        physics.UpdateCollisions();

        if (((physics.hitLeft || physics.hitRight) && fastSlide) || holder && ((Mathf.Abs(holder.body.velocity.x) > 4 && (physics.hitLeft || physics.hitRight) || physics.hitRoof))) {
            photonView.RPC("Crashed", RpcTarget.All);
            photonView.RPC("SpecialKill", RpcTarget.All, false, false);
        }


    }

    [PunRPC]
    public override void Freeze() {
        Debug.Log("You can't freeze a FrozenCube.");
    }

    [PunRPC]
    public override void Unfreeze() {
        Debug.Log("You can't unfreeze a frozen cube, unfreeze the entity.");
    }

    [PunRPC]
    public override void Kill() {
        if (frozenEntity) {
            frozenEntity.photonView.RPC("Unfreeze", RpcTarget.All);
            if (kinematicEntity)
                frozenEntity.body.isKinematic = true;
        } else if (frozenPlayer) {
            frozenPlayer.photonView.RPC("Unfreeze", RpcTarget.All);
            frozenPlayer.body.isKinematic = false;
        }

        if (holder)
            holder.holding = null;
        holder = null;
        if (!plantEntity)
            frozenEntity = null;
        frozenPlayer = null;
        dead = true;
        photonView.RPC("SpecialKill", RpcTarget.All, false, false);
    }

	[PunRPC]
    public override void SpecialKill(bool right = true, bool groundpound = false) {
        base.SpecialKill(right, groundpound);
        body.isKinematic = false;
        hitbox.enabled = false;
        spriteRenderer.enabled = false;
        if (frozenEntity) {
            if (!plantEntity) {
                frozenEntity.photonView.RPC("SpecialKill", RpcTarget.All, right, false);
            } else {
                frozenEntity.photonView.RPC("Unfreeze", RpcTarget.All);
                frozenEntity.photonView.RPC("Kill", RpcTarget.All);
            }
            if (frozenEntity.tag.Contains("koopa"))
                ((KoopaWalk)frozenEntity).shell = true;

            if (flyingEntity)
                frozenEntity.body.isKinematic = false;
        }
        if (frozenPlayer) {
            // if frozenPlayer audioSource is disabled as the audio is played by the player instead
            audioSource.enabled = false;
            frozenPlayer.photonView.RPC("Unfreeze", RpcTarget.All);
        }

        if (holder)
            holder.holding = null;
        holder = null;

        Instantiate(Resources.Load("Prefabs/Particle/IceBreak"), transform.position, Quaternion.identity);
        dead = true;
        if (frozenPlayer)
            PhotonNetwork.Destroy(photonView);
    }
}
