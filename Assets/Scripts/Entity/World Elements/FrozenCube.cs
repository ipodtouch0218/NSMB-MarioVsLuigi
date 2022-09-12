using UnityEngine;

using Photon.Pun;
using NSMB.Utils;

// maybe a better name for the script
public class FrozenCube : HoldableEntity {

    [SerializeField] private float throwSpeed = 10f, shakeSpeed = 1f, shakeAmount = 0.1f;

    public IFreezableEntity.UnfreezeReason unfreezeReason = IFreezableEntity.UnfreezeReason.Other;
    public float autoBreakTimer = 10;

    private SpriteRenderer spriteRenderer;
    private IFreezableEntity entity;
    private PhotonView entityView;
    private Rigidbody2D entityBody;

    private Vector2 entityPositionOffset;
    private bool fastSlide, fallen;
    private int combo;
    private float throwTimer;

    #region Unity Methods
    public new void Start() {
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

            entityPositionOffset = -(bounds.center - Vector3.up.Multiply(bounds.size / 2) - rendererObject.transform.position);

            transform.position -= (Vector3) entityPositionOffset - Vector3.down * 0.1f;

            flying = entity.IsFlying;
            ApplyConstraints();
        }
    }

    public new void LateUpdate() {
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
            entityBody.transform.position = entityBody.position = (Vector2) transform.position + entityPositionOffset;
    }

    public override void FixedUpdate() {
        if (GameManager.Instance && GameManager.Instance.gameover) {
            body.velocity = Vector2.zero;
            body.angularVelocity = 0;
            animator.enabled = false;
            body.isKinematic = true;
            return;
        }
        if (photonView.IsMine && (body.position.y + hitbox.size.y < GameManager.Instance.GetLevelMinY() || Utils.IsTileSolidAtWorldLocation(body.position + (hitbox.size.y / 2f) * Vector2.up))) {
            entityView.RPC(nameof(IFreezableEntity.Unfreeze), RpcTarget.All, (byte) IFreezableEntity.UnfreezeReason.Other);
            PhotonNetwork.Destroy(photonView);
            return;
        }
        if (photonView.IsMine && holder && Utils.IsAnyTileSolidBetweenWorldBox(body.position + hitbox.offset, hitbox.size * transform.lossyScale * 0.75f)) {
            photonView.RPC(nameof(KillWithReason), RpcTarget.All, (byte) IFreezableEntity.UnfreezeReason.HitWall);
            return;
        }

        if (!fastSlide && autoBreakTimer < 1f)
            body.position = new(body.position.x + Mathf.Sin(autoBreakTimer * shakeSpeed) * shakeAmount * Time.fixedDeltaTime, transform.position.y);

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
                if (!fastSlide)
                    unfreezeReason = IFreezableEntity.UnfreezeReason.Timer;

                if (flying)
                    fallen = true;
                else if (photonView.IsMine) {
                    photonView.RPC(nameof(KillWithReason), RpcTarget.All, (byte) IFreezableEntity.UnfreezeReason.Timer);
                }
            }
        }

        if (throwTimer > 0 && throwTimer - Time.fixedDeltaTime <= 0) {
            Physics2D.IgnoreCollision(hitbox, previousHolder.MainHitbox, false);
        }
        Utils.TickTimer(ref throwTimer, 0, Time.fixedDeltaTime);

        ApplyConstraints();
    }
    #endregion

    #region Unity Callbacks
    public new void OnTriggerEnter2D(Collider2D collider) {
        if (!photonView.IsMineOrLocal() || dead || !fastSlide)
            return;

        GameObject obj = collider.gameObject;
        KillableEntity killa = obj.GetComponentInParent<KillableEntity>();

        if (killa) {
            if (killa.dead || killa.photonView.ViewID == entityView.ViewID)
                return;

            killa.photonView.RPC(nameof(KillableEntity.SpecialKill), RpcTarget.All, killa.transform.position.x > transform.position.x, false, combo++);
        }

        switch (obj.tag) {
        case "coin": {
            (holder != null ? holder : previousHolder).photonView.RPC(nameof(PlayerController.AttemptCollectCoin), RpcTarget.All, obj.GetPhotonView().ViewID, (Vector2) obj.transform.position);
            break;
        }
        case "loosecoin": {
            Transform parent = obj.transform.parent;
            (holder != null ? holder : previousHolder).photonView.RPC(nameof(PlayerController.AttemptCollectCoin), RpcTarget.All, parent.gameObject.GetPhotonView().ViewID, (Vector2) parent.position);
            break;
        }
        }
    }
    #endregion

    #region Helper Methods
    private void HandleTile() {
        if (!photonView.IsMineOrLocal())
            return;

        physics.UpdateCollisions();

        if ((fastSlide && (physics.hitLeft || physics.hitRight))
            || (flying && fallen && physics.onGround && !holder)
            || ((holder || physics.onGround) && physics.hitRoof)) {

            photonView.RPC("Kill", RpcTarget.All);
        }
    }

    public override void InteractWithPlayer(PlayerController player) {
        Vector2 damageDirection = (player.body.position - body.position).normalized;
        bool attackedFromAbove = damageDirection.y > -0.4f;
        if (previousHolder == player && throwTimer > 0)
            return;

        if (!holder && (player.invincible > 0 || player.state == Enums.PowerupState.MegaMushroom || player.inShell)) {
            photonView.RPC(nameof(Kill), RpcTarget.All);
            return;
        }
        if (holder || fallen || player.Frozen || (player.throwInvincibility > 0 && player.holdingOld == gameObject))
            return;

        if ((player.groundpound || player.groundpoundLastFrame) && attackedFromAbove && player.state != Enums.PowerupState.MiniMushroom) {
            photonView.RPC(nameof(KillWithReason), RpcTarget.All, (byte) IFreezableEntity.UnfreezeReason.Groundpounded);

        } else if (!attackedFromAbove && player.state != Enums.PowerupState.MiniMushroom) {

            photonView.RPC(nameof(KillWithReason), RpcTarget.All, (byte) IFreezableEntity.UnfreezeReason.BlockBump);

        } else if (fastSlide) {
            player.photonView.RPC(nameof(PlayerController.Knockback), RpcTarget.All, body.position.x > player.body.position.x, 1, false, photonView.ViewID);
            photonView.RPC(nameof(Kill), RpcTarget.All);
        }
        if (entity.IsCarryable && !holder && !dead) {
            if (player.CanPickup() && player.onGround) {
                fallen = true;
                photonView.RPC(nameof(Pickup), RpcTarget.All, player.photonView.ViewID);
                player.photonView.RPC(nameof(PlayerController.SetHolding), RpcTarget.All, photonView.ViewID);
            } else {
                player.photonView.RPC(nameof(PlayerController.SetHoldingOld), RpcTarget.All, photonView.ViewID);
                previousHolder = player;
            }
        }
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

    #endregion

    #region PunRPCs
    [PunRPC]
    public override void Pickup(int view) {
        base.Pickup(view);
        Physics2D.IgnoreCollision(hitbox, holder.MainHitbox);
        autoBreakTimer += 1f;
    }

    [PunRPC]
    public override void Throw(bool facingLeft, bool crouch, Vector2 pos) {
        if (holder == null)
            return;

        fallen = false;
        flying = false;
        left = facingLeft;
        fastSlide = true;
        body.position = new(pos.x + (holder.facingRight ? 0.1f : -0.1f), pos.y);

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

        body.velocity = new(throwSpeed * (left ? -1 : 1), Mathf.Min(0, body.velocity.y));
    }

    [PunRPC]
    public override void Kick(bool fromLeft, float kickFactor, bool groundpound) {
        //kicking does nothing.
    }

    [PunRPC]
    public void KillWithReason(byte reasonByte) {
        unfreezeReason = (IFreezableEntity.UnfreezeReason) reasonByte;
        Kill();
    }

    [PunRPC]
    public override void Kill() {
        entity?.Unfreeze((byte) unfreezeReason);

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
    #endregion
}
