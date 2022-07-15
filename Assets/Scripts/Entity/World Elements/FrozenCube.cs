using UnityEngine;
using Photon.Pun;
using NSMB.Utils;

// maybe a better name for the script
public class FrozenCube : HoldableEntity {

    public float throwSpeed = 10f, shakeSpeed = 1f, shakeAmount = 0.1f;
    public SpriteRenderer spriteRenderer;

    IFreezableEntity entity;
    PhotonView entityView;
    Rigidbody2D entityBody;

    public float autoBreakTimer = 10, throwTimer;

    public bool fastSlide, fallen;
    private int combo;
    public Vector2 offset;

    new void Start() {
        base.Start();
        dead = false;
        holderOffset = Vector2.one;
        body.velocity = Vector2.zero;

        if (photonView && photonView.InstantiationData != null) {
            int id = (int) photonView.InstantiationData[0];
            entityView = PhotonView.Find(id);

            entity = entityView.GetComponent<IFreezableEntity>();
            if (entity == null || (photonView.IsMine && entity.Frozen)) {
                Destroy(gameObject);
                return;
            }

            entityBody = entityView.GetComponentInParent<Rigidbody2D>();

            if (photonView.IsMine)
                entityView.RPC("Freeze", RpcTarget.All, photonView.ViewID);

            spriteRenderer = GetComponent<SpriteRenderer>();

            Bounds bounds = default;
            GameObject rendererObject = entityView.gameObject;
            Renderer[] renderers = entityView.GetComponentsInChildren<Renderer>();
            foreach (Renderer renderer in renderers) {
                if (!renderer.enabled || renderer is ParticleSystemRenderer)
                    continue;

                renderer.ResetBounds();

                if (bounds == default)
                    bounds = new(renderer.bounds.center, renderer.bounds.size);
                else
                    bounds.Encapsulate(renderer.bounds);
            }

            hitbox.size = spriteRenderer.size = GetComponent<BoxCollider2D>().size = bounds.size;
            hitbox.offset = Vector2.up * hitbox.size / 2;

            offset = -(bounds.center - Vector3.up.Multiply(bounds.size / 2) - rendererObject.transform.position);

            transform.position -= (Vector3) offset - Vector3.down * 0.1f;

            flying = entity.IsFlying;
            ApplyConstraints();
        }
    }

    private new void LateUpdate() {
        base.LateUpdate();

        if (entity == null || !entityView) {
            if (photonView.IsMine) {
                PhotonNetwork.Destroy(gameObject);
            } else {
                Destroy(gameObject);
            }
            return;
        }

        //move the entity to be inside of us
        if (entity.IsCarryable)
            entityBody.transform.position = entityBody.position = (Vector2) transform.position + offset;
    }

    public override void FixedUpdate() {
        if (GameManager.Instance && GameManager.Instance.gameover) {
            body.velocity = Vector2.zero;
            body.angularVelocity = 0;
            animator.enabled = false;
            body.isKinematic = true;
            return;
        }
        if (photonView.IsMine && body.position.y + hitbox.size.y < GameManager.Instance.GetLevelMinY()) {
            entityView.RPC("Unfreeze", RpcTarget.All);
            PhotonNetwork.Destroy(photonView);
            return;
        }
        if (photonView.IsMine && holder && Utils.IsTileSolidAtWorldLocation(body.position + hitbox.offset * transform.lossyScale)) {
            photonView.RPC("Kill", RpcTarget.All);
            return;
        }

        if (!fastSlide && autoBreakTimer < 1f)
            transform.position = new(body.position.x + Mathf.Sin(autoBreakTimer * shakeSpeed) * shakeAmount * Time.fixedDeltaTime, transform.position.y, transform.position.z);

        if (dead)
            return;

        //our entity despawned. remove.
        if (entity == null) {
            if (photonView.IsMine) {
                PhotonNetwork.Destroy(photonView);
            }
            Destroy(gameObject);
            return;
        }

        //handle interactions with tiles
        if (entity.IsCarryable && photonView.IsMineOrLocal())
            HandleTile();

        if (fastSlide && physics.onGround && physics.floorAngle != 0) {
            RaycastHit2D ray = Physics2D.BoxCast(body.position + Vector2.up * hitbox.size / 2f, hitbox.size, 0, Vector2.down, 0.2f, Layers.MaskOnlyGround);
            if (ray) {
                body.position = new Vector2(body.position.x, ray.point.y + Physics2D.defaultContactOffset);
                if (ray.distance < 0.1f)
                    body.velocity = new Vector2(body.velocity.x, Mathf.Min(0, body.velocity.y));
            }
        }

        body.velocity = new Vector2(throwSpeed * (left ? -1 : 1), body.velocity.y);

        if (autoBreakTimer > 0 && (entity is PlayerController || (!holder && !fastSlide))) {
            Utils.TickTimer(ref autoBreakTimer, 0, Time.fixedDeltaTime);
            if (autoBreakTimer <= 0) {

                if (flying)
                    fallen = true;
                else if (photonView.IsMine)
                    photonView.RPC("Kill", RpcTarget.All);
            }
        }

        if (throwTimer > 0 && throwTimer - Time.fixedDeltaTime <= 0) {
            Physics2D.IgnoreCollision(hitbox, previousHolder.MainHitbox, false);
        }
        Utils.TickTimer(ref throwTimer, 0, Time.fixedDeltaTime);

        ApplyConstraints();
    }

    private void ApplyConstraints() {
        body.constraints = RigidbodyConstraints2D.FreezeRotation;
        body.mass = holder ? 0 : 1;
        body.isKinematic = !entity.IsCarryable;

        if (!holder) {
            if (!fastSlide)
                body.constraints |= RigidbodyConstraints2D.FreezePositionX;

            if (flying && !fallen)
                body.constraints |= RigidbodyConstraints2D.FreezePositionY;
        }
    }

	public override void InteractWithPlayer(PlayerController player) {
        Vector2 damageDirection = (player.body.position - body.position).normalized;
        bool attackedFromAbove = Vector2.Dot(damageDirection, Vector2.up) > 0f;
        if (previousHolder == player && throwTimer > 0)
            return;

        if (!holder && (player.invincible > 0 || player.state == Enums.PowerupState.MegaMushroom || player.inShell)) {
            photonView.RPC("Kill", RpcTarget.All);
            return;
        }
        if (holder || fallen || player.Frozen || (player.throwInvincibility > 0 && player.holdingOld == gameObject))
            return;

        if (player.groundpound && player.state != Enums.PowerupState.MiniMushroom && attackedFromAbove) {
            photonView.RPC("Kill", RpcTarget.All);
            if (entity is PlayerController pc)
                pc.photonView.RPC("Knockback", RpcTarget.All, pc.facingRight, 1, false, player.photonView.ViewID);

        } else if (Mathf.Abs(body.velocity.x) >= (throwSpeed/2) && !physics.hitRoof) {
            player.photonView.RPC("Knockback", RpcTarget.All, body.position.x > player.body.position.x, 1, false, photonView.ViewID);
            photonView.RPC("Kill", RpcTarget.All);
        }
        if (entity.IsCarryable && !holder && !dead) {
            if (player.CanPickup() && player.onGround) {
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

        fallen = false;
        flying = false;
        left = facingLeft;
        fastSlide = true;
        transform.position = new(holder.facingRight ? holder.transform.position.x + 0.1f : holder.transform.position.x - 0.1f, transform.position.y, transform.position.z);

        previousHolder = holder;
        holder.SetHoldingOld(photonView.ViewID);
        holder = null;
        throwTimer = 1f;

        photonView.TransferOwnership(PhotonNetwork.MasterClient);

        if (entity.IsFlying) {
            fallen = true;
            body.isKinematic = false;
        }
        ApplyConstraints();

        body.velocity = new Vector2(throwSpeed * (left ? -1 : 1), Mathf.Min(0, body.velocity.y));
    }

    new void OnTriggerEnter2D(Collider2D collider) {
        if (!photonView.IsMineOrLocal() || dead || !fastSlide)
            return;

        GameObject obj = collider.gameObject;
        KillableEntity killa = obj.GetComponentInParent<KillableEntity>();

        if (killa && (killa.dead ||killa.photonView.ViewID == entityView.ViewID))
            return;

        switch (obj.tag) {
        case "koopa":
        case "bobomb":
        case "bulletbill":
        case "goomba":
        case "piranhaplant":
        case "frozencube": {
            killa.photonView.RPC("SpecialKill", RpcTarget.All, killa.transform.position.x > transform.position.x, false, combo++);
            break;
        }
        case "coin": {
            (holder != null ? holder : previousHolder).photonView.RPC("CollectCoin", RpcTarget.AllViaServer, obj.GetPhotonView().ViewID, new Vector3(obj.transform.position.x, collider.transform.position.y, 0));
            break;
        }
        case "loosecoin": {
            Transform parent = obj.transform.parent;
            (holder != null ? holder : previousHolder).photonView.RPC("CollectCoin", RpcTarget.AllViaServer, parent.gameObject.GetPhotonView().ViewID, parent.position);
            break;
        }
        }
    }

    void HandleTile() {
        if (!photonView.IsMineOrLocal())
            return;

        physics.UpdateCollisions();

        if ((fastSlide && (physics.hitLeft || physics.hitRight))
            || (flying && fallen && physics.onGround && !holder)
            || ((holder || physics.onGround) && physics.hitRoof)) {

            photonView.RPC("Kill", RpcTarget.All);
        }
    }

    [PunRPC]
    public override void Pickup(int view) {
        base.Pickup(view);
        Physics2D.IgnoreCollision(hitbox, holder.MainHitbox);
    }

    [PunRPC]
    public override void Kill() {
        entity?.Unfreeze();

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
    public override void SpecialKill(bool right, bool groundpound, int combo) {
        Kill();
    }
}
