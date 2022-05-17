using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using UnityEngine.Tilemaps;

// maybe a better name for the script
public class FrozenCube : HoldableEntity {

    private static int GROUND_LAYER_ID = -1;

    public float throwSpeed = 10f;
    public BoxCollider2D frozenCubeCollider;
    public SpriteRenderer spriteRenderer;

    public IFreezableEntity entity;
    public PhotonView entityView;

    public float fallTime = 5, killTimer = .5f;
    float fallTimer;

    public bool fastSlide, fallen, deathFlag;
    public bool kinematicEntity, flyingEntity, plantEntity;

    public Vector2 offset;

    new void Start() {
        base.Start();
        dead = false;
        holderOffset = Vector2.one;
        hitbox = GetComponentInChildren<BoxCollider2D>();
        body.velocity = Vector2.zero;

        if (GROUND_LAYER_ID == -1)
            GROUND_LAYER_ID = LayerMask.NameToLayer("Ground");

        if (photonView.InstantiationData != null) {
            int id = (int) photonView.InstantiationData[0];
            entityView = PhotonView.Find(id);

            entity = entityView.GetComponent<IFreezableEntity>();
            if (entity == null) {
                Destroy(gameObject);
                return;
            }

            if (photonView.IsMine)
                entityView.RPC("Freeze", RpcTarget.All, photonView.ViewID);

            spriteRenderer = GetComponentInChildren<SpriteRenderer>();

            Bounds bounds = new(entityView.transform.position, Vector3.zero);
            Renderer[] renderers = entityView.GetComponentsInChildren<Renderer>();
            foreach (Renderer renderer in renderers) {
                renderer.ResetBounds();
                Debug.Log(renderer.GetType().Name);
                if (renderer is ParticleSystemRenderer)
                    continue;
                bounds.Encapsulate(renderer.bounds);
            }

            hitbox.size = frozenCubeCollider.size = spriteRenderer.size = GetComponent<BoxCollider2D>().size = bounds.size + (Vector3.one * 0.1f);
            hitbox.offset = frozenCubeCollider.offset = Vector2.up * frozenCubeCollider.size / 2;
            
            offset = -(bounds.center - Vector3.up.Multiply(bounds.size/2) - entityView.transform.position);
            Debug.Log(offset);
        }
    }

    private new void LateUpdate() {
        base.LateUpdate();

        if (entity == null) {
            if (photonView.IsMine) {
                PhotonNetwork.Destroy(gameObject);
            } else {
                Destroy(gameObject);
            }
            return;
        }

        if (entity.IsCarryable) {
            //move the entity to be inside of us
            entityView.transform.position = (Vector2) transform.position + offset;
        } else {
            //move ourselves to be inside the entity
            transform.position = new Vector2(entityView.transform.position.x, entityView.transform.position.y + (transform.localScale.y / 1));
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
        if (photonView.IsMine && body.position.y + hitbox.size.y < GameManager.Instance.GetLevelMinY()) {
            entityView.RPC("Unfreeze", RpcTarget.All, photonView.ViewID);
            PhotonNetwork.Destroy(photonView);
            return;
        }
        base.FixedUpdate();

        body.mass = holder != null ? 0 : 1;

        if (dead)
            return;

        //our entity despawned. remove.
        if (entity == null) {
            if (photonView.IsMine) {
                PhotonNetwork.Destroy(photonView);
            } else {
                Destroy(gameObject);
            }
            return;
        }
            

        //handle flying timer
        if (!fallen) {
            if (entity.IsFlying) {
                //count down flying timer

                Utils.TickTimer(ref fallTimer, Time.fixedDeltaTime, 0);
                if (fallTimer <= 0) {
                    fallen = true; 
                    ApplyConstraints();
                } else {
                    //do shaking animation
                }
            } else {
                fallen = true;
                ApplyConstraints();
            }
        }


        //handle interactions with tiles
        if (entity.IsCarryable && (photonView?.IsMine ?? false)) {
            HandleTile();
        }
    }

    private void ApplyConstraints() {
        if (entity.IsCarryable && fallen) {
            body.constraints = RigidbodyConstraints2D.FreezeRotation;
        } else {
            body.constraints = RigidbodyConstraints2D.FreezeAll;
        }
    }

	public override void InteractWithPlayer(PlayerController player) {
        Vector2 damageDirection = (player.body.position - body.position).normalized;
        bool attackedFromAbove = Vector2.Dot(damageDirection, Vector2.up) > 0f;
        if (!holder && player.invincible > 0) {
            photonView.RPC("Kill", RpcTarget.All);

        }
        if (holder || player.frozen)
            return;
        else if (player.groundpound && player.state != Enums.PowerupState.MiniMushroom && attackedFromAbove) {
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
    public override void Kick(bool fromLeft, float kickFactor, bool groundpound) { }

    [PunRPC]
    public override void Throw(bool facingLeft, bool crouch) {
        if (holder == null)
            return;

        fastSlide = !crouch;
        transform.position = new Vector2(holder.facingRight ? holder.transform.position.x + 0.1f : holder.transform.position.x - 0.1f, transform.position.y);

        previousHolder = holder;
        holder = null;

        photonView.TransferOwnership(PhotonNetwork.MasterClient);

        if (entity.IsFlying) {
            fallen = true;
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

        if (killa?.dead ?? false) {
            return;
        }

        switch (obj.tag) {
        case "koopa":
        case "bobomb":
        case "bulletbill":
        case "goomba":
        case "frozencube": {
            if (!killa)
                return;

            if (Mathf.Abs(body.velocity.x) >= 1.5f)
                killa.photonView.RPC("SpecialKill", RpcTarget.All, killa.body.position.x > body.position.x, false);
            break;
        }
        case "piranhaplant": {
            if (!killa)
                return;

            if (Mathf.Abs(body.velocity.x) >= 2f * 1 && (physics.hitLeft || physics.hitRight))
                killa.photonView.RPC("Kill", RpcTarget.All);
            if (holder && (Mathf.Abs(body.velocity.x) >= 2f * 1 && (physics.hitLeft || physics.hitRight)))
                photonView.RPC("Kill", RpcTarget.All);
            break;
        }
        case "coin": {
            (holder != null ? holder : previousHolder).photonView.RPC("CollectCoin", RpcTarget.AllViaServer, obj.GetPhotonView().ViewID, new Vector3(obj.transform.position.x, collider.transform.position.y, 0));
            break;
        }
        case "loosecoin": {
            if (!holder && previousHolder) {
                Transform parent = obj.transform.parent;
                previousHolder.photonView.RPC("CollectCoin", RpcTarget.All, parent.gameObject.GetPhotonView().ViewID, parent.position);
            }
            break;
        }
        }
    }

    void HandleTile() {
        if (!photonView.IsMine)
            return;

        physics.UpdateCollisions();

        if (((physics.hitLeft || physics.hitRight) && fastSlide) || holder && (Mathf.Abs(holder.body.velocity.x) > 4 && (physics.hitLeft || physics.hitRight) || physics.hitRoof)) {
            photonView.RPC("Kill", RpcTarget.All);
        }
    }

    [PunRPC]
    public override void Kill() {
        if (entity != null)
            entity.Unfreeze();

        if (holder)
            holder.holding = null;
        holder = null;

        Instantiate(Resources.Load("Prefabs/Particle/IceBreak"), transform.position, Quaternion.identity);
        if (photonView.IsMine) {
            PhotonNetwork.Destroy(photonView);
        } else {
            Destroy(gameObject);
        }
    }

	[PunRPC]
    public override void SpecialKill(bool right = true, bool groundpound = false) {
        Kill();
    }
}
