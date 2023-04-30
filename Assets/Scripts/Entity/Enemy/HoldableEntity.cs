using UnityEngine;

using Fusion;
using NSMB.Utils;

[OrderAfter(typeof(PlayerController))]
[OrderBefore(typeof(WrappingObject))]
public abstract class HoldableEntity : KillableEntity {

    //---Networked Variables
    [Networked] public PlayerController Holder { get; set; }
    [Networked] public PlayerController PreviousHolder { get; set; }
    [Networked] protected TickTimer ThrowInvincibility { get; set; }
    [Networked] protected float CurrentKickSpeed { get; set; }
    [Networked(OnChanged = nameof(OnKickedAnimCounterChanged))] protected byte KickedAnimCounter { get; set; }

    //---Serailized Variables
    [SerializeField] protected float throwSpeed = 4.5f;
    [SerializeField] protected NetworkRigidbody2D nrb;

    //--Misc Variables
    public Vector3 holderOffset;
    public bool canPlace = true, canKick = true;

    public override void OnValidate() {
        base.OnValidate();
        if (!nrb) nrb = GetComponentInParent<NetworkRigidbody2D>();
    }

    public override void FixedUpdateNetwork() {
        if (Holder) {
            body.velocity = Holder.body.velocity;
            //body.velocity = Vector2.zero;
            transform.position = new(transform.position.x, transform.position.y, Holder.transform.position.z - 0.1f);

            // Teleport check
            Vector2 newPosition = Holder.body.position + (Vector2) holderOffset;
            Vector2 diff = newPosition - body.position;
            Utils.WrapWorldLocation(ref newPosition);

            if (Mathf.Abs(newPosition.x - body.position.x) > 2) {
                nrb.TeleportToPosition(newPosition, diff);
            } else {
                body.position = newPosition;
            }

            hitbox.enabled = false;
            CheckForEntityCollisions();
        } else {
            hitbox.enabled = true;
            base.FixedUpdateNetwork();
        }
    }

    public virtual void Kick(PlayerController kicker, bool toRight, float kickFactor, bool groundpound) {
        if (Holder)
            return;

        if (kicker) {
            ThrowInvincibility = TickTimer.CreateFromSeconds(Runner, 0.2f);
            PreviousHolder = kicker;
        }
        FacingRight = toRight;
        KickedAnimCounter++;
        CurrentKickSpeed = throwSpeed + 1.5f * kickFactor;
        body.velocity = new(CurrentKickSpeed * (FacingRight ? 1 : -1), groundpound ? 3.5f : 0);
    }

    public virtual void Throw(bool toRight, bool crouching) {
        if (!Holder)
            return;

        if (Utils.IsTileSolidAtWorldLocation(body.position))
            transform.position = body.position = new(Holder.transform.position.x, body.position.y);

        ThrowInvincibility = TickTimer.CreateFromSeconds(Runner, crouching ? 0.6f : 0.35f);
        PreviousHolder = Holder;
        Holder = null;
        FacingRight = toRight;

        body.velocity = new((crouching && canPlace ? 2f : throwSpeed) * (FacingRight ? 1 : -1), body.velocity.y);
    }

    public virtual void Pickup(PlayerController player) {
        if (Holder)
            return;

        player.SetHeldEntity(this);
    }

    //---IPlayerInteractable overrides
    public override void InteractWithPlayer(PlayerController player) {
        //don't interact with our lovely holder
        if (Holder == player)
            return;

        //temporary invincibility
        if (PreviousHolder == player && !ThrowInvincibility.ExpiredOrNotRunning(Runner))
            return;

        base.InteractWithPlayer(player);
    }

    //---KillableEntity overrides
    public override void Kill() {
        if (IsDead)
            return;

        if (Holder)
            Holder.SetHeldEntity(null);

        base.Kill();
    }

    public override void SpecialKill(bool right, bool groundpound, int combo) {
        if (IsDead)
            return;

        if (Holder)
            Holder.SetHeldEntity(null);

        base.SpecialKill(right, groundpound, combo);
    }

    //---OnChanged
    public static void OnKickedAnimCounterChanged(Changed<HoldableEntity> changed) {
        changed.Behaviour.PlaySound(Enums.Sounds.Enemy_Shell_Kick);
    }
}
