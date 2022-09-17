using UnityEngine;

using NSMB.Utils;
using Fusion;

// maybe a better name for the script
public class FrozenCube : HoldableEntity {

    //---Networked Variables
    [Networked] public TickTimer AutoBreakTimer { get; set; }
    [Networked] private FreezableEntity FrozenEntity { get; set; }

    //---Serialized Variables
    [SerializeField] private float shakeSpeed = 1f, shakeAmount = 0.1f;

    public FreezableEntity.UnfreezeReason unfreezeReason = FreezableEntity.UnfreezeReason.Other;

    //---Components

    private Vector2 entityPositionOffset;
    private bool fastSlide, fallen;
    private int combo;
    private float throwTimer;

    #region Unity Methods
    public void OnBeforeSpawned(FreezableEntity entityToFreeze) {
        FrozenEntity = entityToFreeze;
    }

    public override void Spawned() {
        holderOffset = Vector2.one;

        if (FrozenEntity == null)
            Kill();

        FrozenEntity.Freeze(this);


        Bounds bounds = default;
        GameObject rendererObject = FrozenEntity.gameObject;
        Renderer[] renderers = FrozenEntity.GetComponentsInChildren<Renderer>();
        foreach (Renderer renderer in renderers) {
            if (!renderer.enabled || renderer is ParticleSystemRenderer)
                continue;

            renderer.ResetBounds();

            if (bounds == default)
                bounds = new(renderer.bounds.center, renderer.bounds.size);
            else
                bounds.Encapsulate(renderer.bounds);
        }

        hitbox.size = sRenderer.size = GetComponent<BoxCollider2D>().size = bounds.size;
        hitbox.offset = Vector2.up * hitbox.size / 2;

        entityPositionOffset = -(bounds.center - Vector3.up.Multiply(bounds.size / 2) - rendererObject.transform.position);

        transform.position -= (Vector3) entityPositionOffset - Vector3.down * 0.1f;

        flying = FrozenEntity.IsFlying;
        ApplyConstraints();
    }

    public override void FixedUpdateNetwork() {
        base.FixedUpdateNetwork();

        if (Dead)
            return;

        if (GameManager.Instance && GameManager.Instance.gameover) {
            body.velocity = Vector2.zero;
            body.angularVelocity = 0;
            animator.enabled = false;
            body.isKinematic = true;
            return;
        }
        if (body.position.y + hitbox.size.y < GameManager.Instance.GetLevelMinY()) {
            Kill();
            return;
        }
        if (Holder && Utils.IsAnyTileSolidBetweenWorldBox(body.position + hitbox.offset, hitbox.size * transform.lossyScale * 0.75f)) {
            KillWithReason((byte) FreezableEntity.UnfreezeReason.HitWall);
            return;
        }

        if (!fastSlide) {
            float remainingTime = AutoBreakTimer.RemainingTime(Runner) ?? 0f;
            if (remainingTime < 1f) {
                body.position = new(body.position.x + Mathf.Sin(remainingTime * shakeSpeed) * shakeAmount * Runner.DeltaTime, transform.position.y);
            }
        }

        //our entity despawned. remove.
        if (FrozenEntity == null) {
            Runner.Despawn(Object);
            return;
        }

        //handle interactions with tiles
        if (FrozenEntity.IsCarryable) {
            HandleTile();

            //be inside us, entity. cmaaahn~
            FrozenEntity.transform.position = (Vector2) transform.position + entityPositionOffset;
        }

        if (fastSlide && physics.onGround && physics.floorAngle != 0) {
            RaycastHit2D ray = Physics2D.BoxCast(body.position + Vector2.up * hitbox.size / 2f, hitbox.size, 0, Vector2.down, 0.2f, Layers.MaskOnlyGround);
            if (ray) {
                body.position = new Vector2(body.position.x, ray.point.y + Physics2D.defaultContactOffset);
                if (ray.distance < 0.1f)
                    body.velocity = new Vector2(body.velocity.x, Mathf.Min(0, body.velocity.y));
            }
        }

        body.velocity = new(throwSpeed * (FacingRight ? 1 : -1), body.velocity.y);

        if (FrozenEntity is PlayerController || (!Holder && !fastSlide)) {

            if (AutoBreakTimer.Expired(Runner)) {
                if (!fastSlide)
                    unfreezeReason = FreezableEntity.UnfreezeReason.Timer;

                if (flying)
                    fallen = true;
                else {
                    KillWithReason((byte) FreezableEntity.UnfreezeReason.Timer);
                }
            }
        }

        if (throwTimer > 0 && throwTimer - Time.fixedDeltaTime <= 0) {
            Physics2D.IgnoreCollision(hitbox, PreviousHolder.MainHitbox, false);
        }
        Utils.TickTimer(ref throwTimer, 0, Time.fixedDeltaTime);

        ApplyConstraints();
    }
    #endregion

    private Collider2D[] collisions = new Collider2D[32];
    private void CheckForEntityCollisions() {

        if (!fastSlide)
            return;

        int count = Runner.GetPhysicsScene2D().OverlapBox(body.position + hitbox.offset, hitbox.size, 0, default, collisions);

        for (int i = 0; i < count; i++) {
            GameObject obj = collisions[i].gameObject;

            //killable entities
            if (obj.TryGetComponent(out KillableEntity killable)) {
                if (killable.Dead || killable == FrozenEntity)
                    continue;

                //kill entity we ran into
                killable.SpecialKill(killable.body.position.x > body.position.x, false, combo++);

                //kill ourselves if we're being held too
                if (Holder)
                    SpecialKill(killable.body.position.x < body.position.x, false, 0);

                continue;
            }

            //coins
            if (PreviousHolder && obj.TryGetComponent(out Coin coin)) {
                coin.InteractWithPlayer(PreviousHolder);
                continue;
            }
        }
    }

    #region Helper Methods
    private void HandleTile() {
        physics.UpdateCollisions();

        if ((fastSlide && (physics.hitLeft || physics.hitRight))
            || (flying && fallen && physics.onGround && !Holder)
            || ((Holder || physics.onGround) && physics.hitRoof)) {

            Kill();
        }
    }

    public override void InteractWithPlayer(PlayerController player) {

        //don't interact with our lovely holder
        if (Holder == player)
            return;

        //temporary invincibility
        if (PreviousHolder == player && !ThrowInvincibility.ExpiredOrNotRunning(Runner))
            return;

        Vector2 damageDirection = (player.body.position - body.position).normalized;
        bool attackedFromAbove = damageDirection.y > -0.4f;
        if (PreviousHolder == player && throwTimer > 0)
            return;

        if (!Holder && (player.IsStarmanInvincible || player.State == Enums.PowerupState.MegaMushroom || player.inShell)) {
            Kill();
            return;
        }
        if (fallen || player.IsFrozen)
            return;

        if ((player.groundpound || player.groundpoundLastFrame) && attackedFromAbove && player.State != Enums.PowerupState.MiniMushroom) {
            KillWithReason((byte) FreezableEntity.UnfreezeReason.Groundpounded);

        } else if (!attackedFromAbove && player.State != Enums.PowerupState.MiniMushroom) {
            KillWithReason((byte) FreezableEntity.UnfreezeReason.BlockBump);

        } else if (fastSlide) {
            player.Knockback(body.position.x > player.body.position.x, 1, false, 0);
            Kill();
        }
        if (FrozenEntity.IsCarryable && !Holder && !Dead) {
            if (player.CanPickup() && player.onGround) {
                fallen = true;
                Pickup(player);
            }
        }
    }

    private void ApplyConstraints() {
        body.constraints = RigidbodyConstraints2D.FreezeRotation;
        body.mass = Holder ? 0 : 1;
        body.isKinematic = !FrozenEntity.IsCarryable;

        if (!Holder) {
            if (!fastSlide)
                body.constraints |= RigidbodyConstraints2D.FreezePositionX;

            if (flying && !fallen)
                body.constraints |= RigidbodyConstraints2D.FreezePositionY;
        }
    }

    public void KillWithReason(byte reasonByte) {
        unfreezeReason = (FreezableEntity.UnfreezeReason) reasonByte;
        Kill();
    }


    #endregion

    #region Interface Methods
    public override void Pickup(PlayerController player) {
        base.Pickup(player);
        Physics2D.IgnoreCollision(hitbox, player.MainHitbox);
        AutoBreakTimer = TickTimer.CreateFromSeconds(Runner, AutoBreakTimer.RemainingTime(Runner) ?? 0f + 1f);
    }

    public override void Throw(bool toRight, bool crouch) {
        if (Holder == null)
            return;

        fallen = false;
        flying = false;
        fastSlide = true;

        throwTimer = 1f;

        if (FrozenEntity.IsFlying) {
            fallen = true;
            body.isKinematic = false;
        }
        ApplyConstraints();
    }

    public override void Kick(bool fromLeft, float kickFactor, bool groundpound) {
        //kicking does nothing.
    }

    public override void Kill() {
        FrozenEntity?.Unfreeze((byte) unfreezeReason);

        if (Holder)
            Holder.SetHolding(null);

        Instantiate(Resources.Load("Prefabs/Particle/IceBreak"), transform.position, Quaternion.identity);
        Runner.Despawn(Object);
    }

    public override void SpecialKill(bool right, bool groundpound, int combo) {
        Kill();
    }
    #endregion
}
