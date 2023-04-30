using UnityEngine;
using UnityEngine.Tilemaps;

using Fusion;
using NSMB.Tiles;
using NSMB.Utils;

public class KoopaWalk : HoldableEntity {

    //---Static Variables
    private static readonly Vector2 BlockOffset = new(0, 0.05f);

    //---Networked Variables
    [Networked] public TickTimer WakeupTimer { get; set; }
    [Networked] public NetworkBool IsInShell { get; set; }
    [Networked] public NetworkBool IsStationary { get; set; }
    [Networked] public NetworkBool IsUpsideDown { get; set; }
    [Networked] private NetworkBool Putdown { get; set; }

    //---Serialized Variables
    [SerializeField] private Sprite deadSprite;
    [SerializeField] private Transform graphicsTransform;
    [SerializeField] private Vector2 outShellHitboxSize, inShellHitboxSize;
    [SerializeField] private Vector2 outShellHitboxOffset, inShellHitboxOffset;
    [SerializeField] protected float walkSpeed, kickSpeed, wakeup = 15;
    [SerializeField] public bool dontFallOffEdges, blue, canBeFlipped = true, flipXFlip;

    //---Properties
    public bool IsActuallyStationary => !Holder && IsStationary;

    //---Private Variables
    private float dampVelocity;

    public override void Render() {
        if (IsFrozen || IsDead)
            return;

        // Animation
        animator.SetBool("shell", IsInShell || Holder != null);
        animator.SetFloat("xVel", IsStationary ? 0 : Mathf.Abs(body.velocity.x));
        animator.SetBool("dead",  !IsActive);

        // "Flip" rotation
        float remainingWakeupTimer = WakeupTimer.RemainingTime(Runner) ?? 0f;
        if (IsUpsideDown) {
            dampVelocity = Mathf.Min(dampVelocity + Time.deltaTime * 3, 1);
            graphicsTransform.eulerAngles = new Vector3(
                graphicsTransform.eulerAngles.x,
                graphicsTransform.eulerAngles.y,
                Mathf.Lerp(graphicsTransform.eulerAngles.z, 180f, dampVelocity) + (remainingWakeupTimer < 3 && remainingWakeupTimer > 0 ? (Mathf.Sin(remainingWakeupTimer * 120f) * 15f) : 0));
        } else {
            dampVelocity = 0;
            graphicsTransform.eulerAngles = new Vector3(
                graphicsTransform.eulerAngles.x,
                graphicsTransform.eulerAngles.y,
                remainingWakeupTimer < 3 && remainingWakeupTimer > 0 ? (Mathf.Sin(remainingWakeupTimer * 120f) * 15f) : 0);
        }
    }

    public override void FixedUpdateNetwork() {
        base.FixedUpdateNetwork();
        if (!Object)
            return;

        if (GameManager.Instance && GameManager.Instance.GameEnded) {
            body.velocity = Vector2.zero;
            body.angularVelocity = 0;
            animator.enabled = false;
            body.isKinematic = true;
            return;
        }

        if (IsFrozen || IsDead)
            return;

        if (Holder)
            FacingRight = Holder.FacingRight;

        PhysicsEntity.PhysicsDataStruct data = physics.UpdateCollisions();
        if (IsInShell) {
            hitbox.size = inShellHitboxSize;
            hitbox.offset = inShellHitboxOffset;

            if (IsStationary) {
                if (data.OnGround)
                    body.velocity = new(0, body.velocity.y);

                if (WakeupTimer.Expired(Runner)) {
                    WakeUp();
                    WakeupTimer = TickTimer.None;
                }
            }
        } else {
            hitbox.size = outShellHitboxSize;
            hitbox.offset = outShellHitboxOffset;
        }


        if (data.HitRight && FacingRight) {
            Turnaround(false, physics.previousTickVelocity.x);
        } else if (data.HitLeft && !FacingRight) {
            Turnaround(true, physics.previousTickVelocity.x);
        }

        if (data.OnGround && Runner.GetPhysicsScene2D().Raycast(body.position, Vector2.down, 0.5f, Layers.MaskAnyGround) && dontFallOffEdges && !IsInShell) {
            Vector3 redCheckPos = body.position + new Vector2(0.1f * (FacingRight ? 1 : -1), 0);
            if (GameManager.Instance)
                Utils.WrapWorldLocation(ref redCheckPos);

            //turn around if no ground
            if (!Runner.GetPhysicsScene2D().Raycast(redCheckPos, Vector2.down, 0.5f, Layers.MaskAnyGround))
                Turnaround(!FacingRight, physics.previousTickVelocity.x);
        }

        if (!IsStationary)
            body.velocity = new((IsInShell ? CurrentKickSpeed : walkSpeed) * (FacingRight ? 1 : -1), body.velocity.y);

        HandleTile();
    }

    private void HandleTile() {
        if (Holder)
            return;

        int collisionAmount = hitbox.GetContacts(ContactBuffer);
        for (int i = 0; i < collisionAmount; i++) {
            ContactPoint2D point = ContactBuffer[i];
            Vector2 p = point.point + (point.normal * -0.15f);
            if (Mathf.Abs(point.normal.x) == 1 && point.collider.gameObject.layer == Layers.LayerGround) {
                if (!Putdown && IsInShell && !IsStationary) {
                    Vector2Int tileLoc = Utils.WorldToTilemapPosition(p + BlockOffset);
                    TileBase tile = GameManager.Instance.tileManager.GetTile(tileLoc);
                    if (!tile || !IsInShell)
                        continue;

                    if (tile is InteractableTile it)
                        it.Interact(this, InteractableTile.InteractionDirection.Up, Utils.TilemapToWorldPosition(tileLoc));
                }
            } else if (point.normal.y > 0 && Putdown) {
                body.velocity = new Vector2(0, body.velocity.y);
                Putdown = false;
            }
        }
    }

    public void WakeUp() {
        IsInShell = false;
        body.velocity = new(-walkSpeed, 0);
        FacingRight = false;
        IsUpsideDown = false;
        IsStationary = false;

        if (Holder)
            Holder.SetHeldEntity(null);

        Holder = null;
        PreviousHolder = null;
    }

    public void EnterShell(bool becomeItem, PlayerController player) {
        if (blue && !IsInShell && becomeItem) {
            BlueBecomeItem();
            return;
        }
        body.velocity = Vector2.zero;
        WakeupTimer = TickTimer.CreateFromSeconds(Runner, wakeup);
        ComboCounter = 1;
        IsInShell = true;
        IsStationary = true;

        if (player) {
            Holder = null;
            PreviousHolder = player;
            ThrowInvincibility = TickTimer.CreateFromSeconds(Runner, 0.2f);
        }
    }

    public void BlueBecomeItem() {
        Runner.Spawn(PrefabList.Instance.Powerup_BlueShell, transform.position, onBeforeSpawned: (runner, obj) => {
            obj.GetComponent<MovingPowerup>().OnBeforeSpawned(0.1f);
        });
        DespawnEntity();
    }

    protected void Turnaround(bool hitWallOnLeft, float x) {
        if (IsActuallyStationary)
            return;

        if (Runner.IsForward && IsInShell && hitWallOnLeft != FacingRight)
            PlaySound(Enums.Sounds.World_Block_Bump);

        FacingRight = hitWallOnLeft;
        body.velocity = new((x > 0.5f ? Mathf.Abs(x) : CurrentKickSpeed) * (FacingRight ? 1 : -1), body.velocity.y);
    }

    //---BasicEntity overrides
    public override void RespawnEntity() {
        if (IsActive)
            return;

        base.RespawnEntity();
        IsInShell = false;
        IsStationary = false;
        IsUpsideDown = false;
        WakeupTimer = TickTimer.None;
        Putdown = false;
    }

    //---IPlayerInteractable overrides
    public override void InteractWithPlayer(PlayerController player) {

        // don't interact with our lovely holder
        if (Holder == player)
            return;

        // temporary invincibility
        if (PreviousHolder == player && !ThrowInvincibility.ExpiredOrNotRunning(Runner))
            return;

        Vector2 damageDirection = (player.body.position - body.position).normalized;
        bool attackedFromAbove = damageDirection.y > 0;

        // always damage exceptions
        if (player.InstakillsEnemies) {
            bool originalFacing = player.FacingRight;
            if (IsInShell && !IsStationary && player.IsInShell && Mathf.Sign(body.velocity.x) != Mathf.Sign(player.body.velocity.x))
                player.DoKnockback(player.body.position.x < body.position.x, 0, true, Object);

            SpecialKill(!originalFacing, false, player.StarCombo++);
            return;
        }

        // attempt to be picked up (or kick)
        if (IsInShell && IsActuallyStationary) {
            if (!Holder) {
                if (player.CanPickupItem) {
                    Pickup(player);
                } else {
                    Kick(player, player.body.position.x < body.position.x, Mathf.Abs(player.body.velocity.x) / player.RunningMaxSpeed, player.IsGroundpounding);
                    PreviousHolder = player;
                }
            }
            return;
        }

        if (attackedFromAbove) {
            // get hit by player

            // blue koopa: check to become a blue shell item
            if (blue && (!IsInShell || (IsInShell && player.HasGroundpoundHitbox))) {
                BlueBecomeItem();
                player.DoEntityBounce = !player.IsGroundpounding;
                return;
            }

            // groundpound by big mario: shell & kick
            if (player.HasGroundpoundHitbox && player.State != Enums.PowerupState.MiniMushroom) {
                EnterShell(true, player);
                if (!blue) {
                    Kick(player, player.body.position.x < body.position.x, 1f, true);
                    PreviousHolder = player;
                }
                return;
            }

            // bounced on
            if (player.State == Enums.PowerupState.MiniMushroom) {
                if (player.HasGroundpoundHitbox) {
                    player.IsGroundpounding = false;
                    EnterShell(true, player);
                }
                player.DoEntityBounce = true;
            } else {
                EnterShell(true, player);
                player.DoEntityBounce = !player.IsGroundpounding;
            }

            player.IsDrilling = false;

        } else {
            // damage player

            // turn around when hitting a crouching blue shell player
            if (player.State == Enums.PowerupState.BlueShell && player.IsCrouching && !player.IsInShell) {
                player.body.velocity = new(0, player.body.velocity.y);
                FacingRight = damageDirection.x < 0;
                return;
            }

            // finally attempt to damage player
            if (player.Powerdown(false) && !IsInShell)
                FacingRight = damageDirection.x > 0;
        }
    }

    //---IBlockBumpable overrides
    public override void BlockBump(BasicEntity bumper, Vector2Int tile, InteractableTile.InteractionDirection direction) {
        if (IsDead)
            return;

        if (!IsInShell) {
            IsStationary = true;
            Putdown = true;
        }

        EnterShell(false, bumper as PlayerController);
        IsUpsideDown = canBeFlipped;
        body.velocity = new(body.velocity.x, 5.5f);

        if (Holder)
            Holder.SetHeldEntity(null);

        if (IsStationary) {
            body.velocity = new(bumper.body.position.x < body.position.x ? 1f : -1f, body.velocity.y);
            physics.Data.OnGround = false;
        }

        if (Runner.IsForward)
            PlaySound(Enums.Sounds.Enemy_Shell_Kick);
    }

    //---FreezableEntity overrides
    public override void Freeze(FrozenCube cube) {
        base.Freeze(cube);
        IsStationary = true;
    }

    //---KillableEntity overrides
    protected override void CheckForEntityCollisions() {

        if (!((!IsInShell && !Holder) || IsActuallyStationary || Putdown || IsDead)) {

            int count = Runner.GetPhysicsScene2D().OverlapBox(body.position + hitbox.offset, hitbox.size, 0, default, CollisionBuffer);

            for (int i = 0; i < count; i++) {
                GameObject obj = CollisionBuffer[i].gameObject;

                if (obj == gameObject)
                    continue;

                //killable entities
                if (obj.TryGetComponent(out KillableEntity killable)) {
                    if (killable.IsDead)
                        continue;

                    //kill entity we ran into
                    killable.SpecialKill((killable.body ? killable.body.position.x : killable.transform.position.x) > body.position.x, false, ComboCounter++);

                    //if we hit another moving shell (or we're being held), we both die.
                    if (Holder || (killable is KoopaWalk kw && kw.IsInShell && !kw.IsActuallyStationary)) {
                        SpecialKill((killable.body ? killable.body.position.x : killable.transform.position.x) < body.position.x, false, 0);
                        return;
                    }

                    continue;
                }

                //coins
                if (PreviousHolder && obj.TryGetComponent(out Coin coin)) {
                    coin.InteractWithPlayer(PreviousHolder);
                    continue;
                }
            }
        }

        base.CheckForEntityCollisions();
    }

    public override void Kill() {
        EnterShell(false, null);
    }

    public override void OnIsDeadChanged() {
        base.OnIsDeadChanged();

        if (IsDead)
            sRenderer.sprite = deadSprite;
    }

    //---ThrowableEntity overrides
    public override void Kick(PlayerController thrower, bool toRight, float kickFactor, bool groundpound) {
        base.Kick(thrower, toRight, kickFactor, groundpound);
        IsStationary = false;
        WakeupTimer = TickTimer.None;
    }

    public override void Throw(bool toRight, bool crouch) {
        throwSpeed = CurrentKickSpeed = kickSpeed + 1.5f * (Mathf.Abs(Holder.body.velocity.x) / Holder.RunningMaxSpeed);
        base.Throw(toRight, crouch);

        IsStationary = crouch;
        IsInShell = true;
        if (!crouch)
            WakeupTimer = TickTimer.None;
        Putdown = crouch;
    }
}
