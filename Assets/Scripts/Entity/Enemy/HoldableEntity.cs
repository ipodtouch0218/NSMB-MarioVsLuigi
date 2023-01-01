using UnityEngine;

using Fusion;
using NSMB.Utils;

[OrderAfter(typeof(PlayerController))]
public abstract class HoldableEntity : KillableEntity {

    //---Networked Variables
    [Networked] public PlayerController Holder { get; set; }
    [Networked] public PlayerController PreviousHolder { get; set; }
    [Networked] protected TickTimer ThrowInvincibility { get; set; }
    [Networked] protected float CurrentKickSpeed { get; set; }

    //---Serailized Variables
    [SerializeField] protected float throwSpeed = 4.5f;

    //--Misc Variables
    public Vector3 holderOffset;
    public bool canPlace = true, canKick = true;

    public override void FixedUpdateNetwork() {
        if (Holder) {
            body.velocity = Vector2.zero;
            transform.position = new(transform.position.x, transform.position.y, Holder.transform.position.z - 0.1f);
            body.position = Holder.body.position + (Vector2) holderOffset;
            hitbox.enabled = false;
            sRenderer.flipX = !FacingRight;
            CheckForEntityCollisions();
        } else {
            base.FixedUpdateNetwork();
            hitbox.enabled = true;
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

        CurrentKickSpeed = throwSpeed + 1.5f * kickFactor;
        body.velocity = new(CurrentKickSpeed * (FacingRight ? 1 : -1), groundpound ? 3.5f : 0);

        if (Runner.IsForward)
            PlaySound(Enums.Sounds.Enemy_Shell_Kick);
    }

    public virtual void Throw(bool toRight, bool crouching) {
        if (!Holder)
            return;

        if (Utils.IsTileSolidAtWorldLocation(body.position))
            transform.position = body.position = new(Holder.transform.position.x, body.position.y);

        ThrowInvincibility = TickTimer.CreateFromSeconds(Runner, crouching ? 0.5f : 0.2f);
        PreviousHolder = Holder;
        Holder = null;
        FacingRight = toRight;

        body.velocity = new Vector2((crouching && canPlace ? 2f : throwSpeed) * (FacingRight ? 1 : -1), body.velocity.y);
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
}
