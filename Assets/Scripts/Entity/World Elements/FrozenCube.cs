using UnityEngine;

using Fusion;
using NSMB.Extensions;
using NSMB.Utils;

public class FrozenCube : HoldableEntity {

    //---Networked Variables
    [Networked] public TickTimer AutoBreakTimer { get; set; }
    [Networked] private TickTimer ThrowTimer { get; set; }
    [Networked] private FreezableEntity FrozenEntity { get; set; }
    [Networked] private NetworkBool FastSlide { get; set; }

    //---Serialized Variables
    [SerializeField] private float shakeSpeed = 1f, shakeAmount = 0.1f, autoBreak = 3f;

    //---Private Variables
    public UnfreezeReason unfreezeReason = UnfreezeReason.Other;
    private Vector2 entityPositionOffset;
    private int combo;
    private bool fallen;

    public void OnBeforeSpawned(FreezableEntity entityToFreeze) {
        FrozenEntity = entityToFreeze;
    }

    public override void Spawned() {
        base.Spawned();
        holderOffset = Vector2.one;

        if (!FrozenEntity) {
            Kill();
            return;
        }

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

        entityPositionOffset = -(bounds.center - Vector3.up.Multiply(bounds.size * 0.5f) - rendererObject.transform.position);

        transform.position -= (Vector3) entityPositionOffset - Vector3.down * 0.1f;

        AutoBreakTimer = TickTimer.CreateFromSeconds(Runner, autoBreak);
        flying = FrozenEntity.IsFlying;
        ApplyConstraints();

        FrozenEntity.Freeze(this);

        //move entity inside us
        if (FrozenEntity.IsCarryable) {
            FrozenEntity.transform.SetParent(transform);
            FrozenEntity.transform.position = (Vector2) transform.position + entityPositionOffset;
        }
    }

    public override void Despawned(NetworkRunner runner, bool hasState) {
        Instantiate(PrefabList.Instance.Particle_IceBreak, transform.position, Quaternion.identity);

        if (FrozenEntity && FrozenEntity.IsCarryable)
            FrozenEntity.transform.SetParent(null);
    }

    public override void FixedUpdateNetwork() {
        base.FixedUpdateNetwork();

        if (IsDead)
            return;

        if (GameManager.Instance && GameManager.Instance.gameover) {
            body.velocity = Vector2.zero;
            body.angularVelocity = 0;
            animator.enabled = false;
            body.isKinematic = true;
            return;
        }

        if (ThrowTimer.Expired(Runner)) {
            ThrowTimer = TickTimer.None;

            if (PreviousHolder)
                Physics2D.IgnoreCollision(hitbox, PreviousHolder.MainHitbox, false);
        }

        if (body.position.y + hitbox.size.y < GameManager.Instance.GetLevelMinY()) {
            Kill();
            return;
        }

        if (Holder && Utils.IsAnyTileSolidBetweenWorldBox(body.position + hitbox.offset, hitbox.size * transform.lossyScale * 0.75f)) {
            KillWithReason(UnfreezeReason.HitWall);
            return;
        }

        if (!FastSlide && !Holder) {
            float remainingTime = AutoBreakTimer.RemainingTime(Runner) ?? 0f;
            if (remainingTime < 1f)
                body.position = new(body.position.x + Mathf.Sin(remainingTime * shakeSpeed) * shakeAmount * Runner.DeltaTime, transform.position.y);
        }

        //our entity despawned. remove.
        if (FrozenEntity == null) {
            Runner.Despawn(Object);
            return;
        }

        //handle interactions with tiles
        if (FrozenEntity.IsCarryable) {
            if (!HandleTile())
                return;

            //be inside us, entity. cmaaahn~
            FrozenEntity.transform.position = (Vector2) transform.position + entityPositionOffset;
        }

        if (FastSlide && physics.OnGround && physics.FloorAngle != 0) {
            RaycastHit2D ray = Runner.GetPhysicsScene2D().BoxCast(body.position + Vector2.up * hitbox.size / 2f, hitbox.size, 0, Vector2.down, 0.2f, Layers.MaskOnlyGround);
            if (ray) {
                body.position = new(body.position.x, ray.point.y + Physics2D.defaultContactOffset);
                if (ray.distance < 0.1f)
                    body.velocity = new(body.velocity.x, Mathf.Min(0, body.velocity.y));
            }
        }

        body.velocity = new(throwSpeed * (FacingRight ? 1 : -1), body.velocity.y);

        if (FrozenEntity is PlayerController || (!Holder && !FastSlide)) {

            if (AutoBreakTimer.Expired(Runner)) {
                if (!FastSlide)
                    unfreezeReason = UnfreezeReason.Timer;

                if (flying)
                    fallen = true;
                else {
                    KillWithReason(UnfreezeReason.Timer);
                    return;
                }
            }
        }

        ApplyConstraints();
    }

    private bool HandleTile() {
        physics.UpdateCollisions();

        if ((FastSlide && (physics.HitLeft || physics.HitRight))
            || (flying && fallen && physics.OnGround && !Holder)
            || ((Holder || physics.OnGround) && physics.HitRoof)) {

            Kill();
            return false;
        }

        return true;
    }

    private void ApplyConstraints() {
        body.constraints = RigidbodyConstraints2D.FreezeRotation;
        body.mass = Holder ? 0 : 1;
        body.isKinematic = !FrozenEntity.IsCarryable;

        if (!Holder) {
            if (!FastSlide)
                body.constraints |= RigidbodyConstraints2D.FreezePositionX;

            if (flying && !fallen)
                body.constraints |= RigidbodyConstraints2D.FreezePositionY;
        }
    }

    public void KillWithReason(UnfreezeReason reason) {
        unfreezeReason = reason;
        Kill();
    }

    //---IPlayerInteractable overrides
    public override void InteractWithPlayer(PlayerController player) {

        //don't interact with our lovely holder
        if (Holder == player)
            return;

        //temporary invincibility
        if (PreviousHolder == player && !ThrowInvincibility.ExpiredOrNotRunning(Runner))
            return;

        Vector2 damageDirection = (player.body.position - body.position).normalized;
        bool attackedFromAbove = damageDirection.y > -0.4f;

        if (PreviousHolder == player)
            return;

        if (!Holder && (player.IsStarmanInvincible || player.State == Enums.PowerupState.MegaMushroom || player.IsInShell)) {
            Kill();
            return;
        }
        if (fallen || player.IsFrozen)
            return;

        if ((player.IsGroundpounding || player.groundpoundLastFrame) && attackedFromAbove && player.State != Enums.PowerupState.MiniMushroom) {
            KillWithReason(UnfreezeReason.Groundpounded);

        } else if (!attackedFromAbove && player.State != Enums.PowerupState.MiniMushroom) {
            KillWithReason(UnfreezeReason.BlockBump);

        } else if (FastSlide) {
            player.DoKnockback(body.position.x > player.body.position.x, 1, false, 0);
            Kill();
        }
        if (FrozenEntity.IsCarryable && !Holder && !IsDead && player.CanPickupItem && player.IsOnGround) {
            fallen = true;
            Pickup(player);
        }
    }

    //---IFireballInteractable overrides
    public override bool InteractWithFireball(FireballMover fireball) {
        if (!fireball.IsIceball)
            Kill();

        return true;
    }

    public override bool InteractWithIceball(FireballMover iceball) {
        return true;
    }

    //---IThrowableEntity overrides
    public override void Pickup(PlayerController player) {
        base.Pickup(player);
        Physics2D.IgnoreCollision(hitbox, player.MainHitbox);
        AutoBreakTimer = TickTimer.CreateFromSeconds(Runner, (AutoBreakTimer.RemainingTime(Runner) ?? 0f) + 1f);
    }

    public override void Throw(bool toRight, bool crouch) {
        base.Throw(toRight, false);

        fallen = false;
        flying = false;
        FastSlide = true;
        ThrowTimer = TickTimer.CreateFromSeconds(Runner, 1f);

        if (FrozenEntity.IsFlying) {
            fallen = true;
            body.isKinematic = false;
        }
        ApplyConstraints();
    }

    public override void Kick(PlayerController kicker, bool fromLeft, float kickFactor, bool groundpound) {
        //kicking does nothing.
    }

    //---IKillableEntity overrides
    protected override void CheckForEntityCollisions() {
        //don't call base, we dont wanna turn around.

        if (!FastSlide)
            return;

        //only run when fastsliding...

        int count = Runner.GetPhysicsScene2D().OverlapBox(body.position + hitbox.offset, hitbox.size, 0, default, CollisionBuffer);

        for (int i = 0; i < count; i++) {
            GameObject obj = CollisionBuffer[i].gameObject;

            if (obj == gameObject || Holder?.gameObject == obj || PreviousHolder?.gameObject == obj || FrozenEntity?.gameObject == obj)
                continue;

            if (PreviousHolder && obj.TryGetComponent(out Coin coin)) {
                coin.InteractWithPlayer(PreviousHolder);
                continue;
            }

            if (obj.TryGetComponent(out KillableEntity killable)) {
                if (killable.IsDead || killable == FrozenEntity)
                    continue;

                //kill entity we ran into
                killable.SpecialKill(killable.body.position.x > body.position.x, false, combo++);

                //kill ourselves if we're being held too
                if (Holder)
                    SpecialKill(killable.body.position.x < body.position.x, false, 0);

                continue;
            }
        }
    }

    public override void Kill() {
        FrozenEntity?.Unfreeze(unfreezeReason);

        if (Holder)
            Holder.SetHeldEntity(null);

        Runner.Despawn(Object);
    }

    public override void SpecialKill(bool right, bool groundpound, int combo) {
        Kill();
    }
}
