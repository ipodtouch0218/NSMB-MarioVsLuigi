using UnityEngine;
using UnityEngine.Tilemaps;

using NSMB.Utils;
using Fusion;

public class KoopaWalk : HoldableEntity {

    //---Networked Variables
    [Networked] public TickTimer WakeupTimer { get; set; }
    [Networked] public NetworkBool IsInShell { get; set; }
    [Networked] public NetworkBool IsStationary { get; set; }
    [Networked] public NetworkBool IsUpsideDown { get; set; }

    //---Serialized Variables
    [SerializeField] private Vector2 outShellHitboxSize, inShellHitboxSize;
    [SerializeField] private Vector2 outShellHitboxOffset, inShellHitboxOffset;
    [SerializeField] protected float walkSpeed, kickSpeed, wakeup = 15;

    //---Properties
    public bool IsActuallyStationary => !Holder && IsStationary;

    public bool dontFallOffEdges, blue, canBeFlipped = true, flipXFlip, putdown;

    private BoxCollider2D worldHitbox;
    private Vector2 blockOffset = new(0, 0.05f), velocityLastFrame;
    private float dampVelocity, currentSpeed;
    protected int combo;

    #region Unity Methods
    public override void Awake() {
        base.Awake();
        hitbox = transform.GetChild(0).GetComponent<BoxCollider2D>();
        worldHitbox = GetComponent<BoxCollider2D>();
    }

    public override void FixedUpdateNetwork() {
        base.FixedUpdateNetwork();
        if (GameManager.Instance && GameManager.Instance.gameover) {
            body.velocity = Vector2.zero;
            body.angularVelocity = 0;
            animator.enabled = false;
            body.isKinematic = true;
            return;
        }

        if (IsFrozen || Dead)
            return;

        sRenderer.flipX = FacingRight ^ flipXFlip;

        float remainingWakeupTimer = WakeupTimer.RemainingTime(Runner) ?? 0f;
        if (IsUpsideDown) {
            dampVelocity = Mathf.Min(dampVelocity + Time.fixedDeltaTime * 3, 1);
            transform.eulerAngles = new Vector3(
                transform.eulerAngles.x,
                transform.eulerAngles.y,
                Mathf.Lerp(transform.eulerAngles.z, 180f, dampVelocity) + (remainingWakeupTimer < 3 && remainingWakeupTimer > 0 ? (Mathf.Sin(remainingWakeupTimer * 120f) * 15f) : 0));
        } else {
            dampVelocity = 0;
            transform.eulerAngles = new Vector3(
                transform.eulerAngles.x,
                transform.eulerAngles.y,
                remainingWakeupTimer < 3 && remainingWakeupTimer > 0 ? (Mathf.Sin(remainingWakeupTimer * 120f) * 15f) : 0);
        }

        if (IsInShell) {
            worldHitbox.size = hitbox.size = inShellHitboxSize;
            worldHitbox.offset = hitbox.offset = inShellHitboxOffset;

            if (IsStationary) {
                if (physics.onGround)
                    body.velocity = new(0, body.velocity.y);

                if (WakeupTimer.Expired(Runner)) {
                    WakeUp();
                    WakeupTimer = TickTimer.None;
                }
            }
        } else {
            worldHitbox.size = hitbox.size = outShellHitboxSize;
            worldHitbox.offset = hitbox.offset = outShellHitboxOffset;
        }

        if (physics.hitRight && FacingRight) {
            Turnaround(false, velocityLastFrame.x);
        } else if (physics.hitLeft && !FacingRight) {
            Turnaround(true, velocityLastFrame.x);
        }

        if (physics.onGround && Physics2D.Raycast(body.position, Vector2.down, 0.5f, Layers.MaskAnyGround) && dontFallOffEdges && !IsInShell) {
            Vector3 redCheckPos = body.position + new Vector2(0.1f * (FacingRight ? 1 : -1), 0);
            if (GameManager.Instance)
                Utils.WrapWorldLocation(ref redCheckPos);

            //turn around if no ground
            if (!Runner.GetPhysicsScene2D().Raycast(redCheckPos, Vector2.down, 0.5f, Layers.MaskAnyGround))
                Turnaround(!FacingRight, velocityLastFrame.x);
        }

        if (physics.onGround && !IsStationary)
            body.velocity = new((IsInShell ? currentSpeed : walkSpeed) * (FacingRight ? 1 : -1), body.velocity.y);

        CheckForEntityCollisions();
        HandleTile();
        animator.SetBool("shell", IsInShell || Holder != null);
        animator.SetFloat("xVel", Mathf.Abs(body.velocity.x));
        velocityLastFrame = body.velocity;
    }

    private Collider2D[] collisions = new Collider2D[32];
    private void CheckForEntityCollisions() {

        if (!IsInShell || IsActuallyStationary || putdown || Dead)
            return;

        int count = Runner.GetPhysicsScene2D().OverlapBox(body.position + hitbox.offset, hitbox.size, 0, default, collisions);

        for (int i = 0; i < count; i++) {
            GameObject obj = collisions[i].gameObject;

            //killable entities
            if (obj.TryGetComponent(out KillableEntity killable)) {
                if (killable.Dead)
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

    #endregion

    #region Public Methods
    public override void InteractWithPlayer(PlayerController player) {

        //don't interact with our lovely holder
        if (Holder == player)
            return;

        //temporary invincibility
        if (PreviousHolder == player && !ThrowInvincibility.ExpiredOrNotRunning(Runner))
            return;

        Vector2 damageDirection = (player.body.position - body.position).normalized;
        bool attackedFromAbove = damageDirection.y > 0;

        if (IsInShell && blue && player.groundpound && !player.onGround) {
            BlueBecomeItem();
            return;
        }
        if (!attackedFromAbove && player.State == Enums.PowerupState.BlueShell && player.crouching && !player.inShell) {
            player.body.velocity = new(0, player.body.velocity.y);
            FacingRight = damageDirection.x < 0;

        } else if (player.sliding || player.inShell || player.IsStarmanInvincible || player.State == Enums.PowerupState.MegaMushroom) {
            bool originalFacing = player.FacingRight;
            if (IsInShell && !IsStationary && player.inShell && Mathf.Sign(body.velocity.x) != Mathf.Sign(player.body.velocity.x))
                player.DoKnockback(player.body.position.x < body.position.x, 0, true, 0);

            SpecialKill(!originalFacing, false, player.StarCombo++);

        } else if (player.groundpound && player.State != Enums.PowerupState.MiniMushroom && attackedFromAbove) {
            EnterShell(true);
            if (!blue) {
                Kick(player.body.position.x < body.position.x, 1f, player.groundpound);
                PreviousHolder = player;
            }

        } else if (attackedFromAbove && (!IsInShell || !IsActuallyStationary)) {
            if (player.State == Enums.PowerupState.MiniMushroom) {
                if (player.groundpound) {
                    player.groundpound = false;
                    EnterShell(true);
                }
                player.bounce = true;
            } else {
                EnterShell(true);
                player.bounce = !player.groundpound;
            }
            PlaySound(Enums.Sounds.Enemy_Generic_Stomp);
            player.drill = false;
        } else {
            if (IsInShell && IsActuallyStationary) {
                if (!Holder) {
                    if (player.CanPickup()) {
                        Pickup(player);
                    } else {
                        Kick(player.body.position.x < body.position.x, Mathf.Abs(player.body.velocity.x) / player.RunningMaxSpeed, player.groundpound);
                        PreviousHolder = player;
                    }
                }
            } else if (player.IsDamageable) {
                player.Powerdown(false);
                if (!IsInShell)
                    FacingRight = damageDirection.x > 0;
            }
        }
    }
    #endregion

    #region Helper Methods
    private void HandleTile() {
        if (Holder)
            return;
        physics.UpdateCollisions();

        ContactPoint2D[] collisions = new ContactPoint2D[20];
        int collisionAmount = worldHitbox.GetContacts(collisions);
        for (int i = 0; i < collisionAmount; i++) {
            var point = collisions[i];
            Vector2 p = point.point + (point.normal * -0.15f);
            if (Mathf.Abs(point.normal.x) == 1 && point.collider.gameObject.layer == Layers.LayerGround) {
                if (!putdown && IsInShell && !IsStationary) {
                    Vector3Int tileLoc = Utils.WorldToTilemapPosition(p + blockOffset);
                    TileBase tile = GameManager.Instance.tilemap.GetTile(tileLoc);
                    if (tile == null)
                        continue;
                    if (!IsInShell)
                        continue;

                    if (tile is InteractableTile it)
                        it.Interact(this, InteractableTile.InteractionDirection.Up, Utils.TilemapToWorldPosition(tileLoc));
                }
            } else if (point.normal.y > 0 && putdown) {
                body.velocity = new Vector2(0, body.velocity.y);
                putdown = false;
            }
        }
    }
    #endregion

    #region PunRPCs
    public override void Freeze(FrozenCube cube) {
        base.Freeze(cube);
        IsStationary = true;
    }

    public override void Kick(bool fromLeft, float kickFactor, bool groundpound) {
        FacingRight = !fromLeft;
        IsStationary = false;
        currentSpeed = kickSpeed + 1.5f * kickFactor;
        body.velocity = new(currentSpeed * (FacingRight ? 1 : -1), groundpound ? 3.5f : 0);
        PlaySound(Enums.Sounds.Enemy_Shell_Kick);
    }

    public override void Throw(bool toRight, bool crouch) {
        base.Throw(toRight, crouch);
        if (Holder == null)
            return;

        IsStationary = crouch;
        IsInShell = true;
        throwSpeed = kickSpeed + 1.5f * (Mathf.Abs(Holder.body.velocity.x) / Holder.RunningMaxSpeed);
        putdown = crouch;
    }

    public void WakeUp() {
        IsInShell = false;
        body.velocity = new(-walkSpeed, 0);
        FacingRight = false;
        IsUpsideDown = false;
        IsStationary = false;

        if (Holder)
            Holder.HoldingWakeup();

        Holder = null;
        PreviousHolder = null;
    }

    public void EnterShell(bool becomeItem) {
        if (blue && !IsInShell && becomeItem) {
            BlueBecomeItem();
            return;
        }
        body.velocity = Vector2.zero;
        WakeupTimer = TickTimer.CreateFromSeconds(Runner, wakeup);
        combo = 0;
        IsInShell = true;
        IsStationary = true;
    }

    public void BlueBecomeItem() {
        Runner.Despawn(Object);
        Runner.Spawn(PrefabList.Instance.Powerup_BlueShell, transform.position, onBeforeSpawned: (runner, obj) => {
            obj.GetComponent<MovingPowerup>().OnBeforeSpawned(null, 0.1f);
        });
    }


    protected void Turnaround(bool hitWallOnLeft, float x) {
        if (IsActuallyStationary)
            return;

        if (IsInShell && hitWallOnLeft == FacingRight)
            PlaySound(Enums.Sounds.World_Block_Bump);

        FacingRight = hitWallOnLeft;
        body.velocity = new((x > 0.5f ? Mathf.Abs(x) : currentSpeed) * (FacingRight ? 1 : -1), body.velocity.y);
        if (IsInShell)
            PlaySound(Enums.Sounds.World_Block_Bump);
    }

    public override void Bump(BasicEntity bumper, Vector3Int tile, InteractableTile.InteractionDirection direction) {
        if (Dead || direction == InteractableTile.InteractionDirection.Down)
            return;

        if (!IsInShell) {
            IsStationary = true;
            putdown = true;
        }
        EnterShell(false);
        IsUpsideDown = canBeFlipped;
        PlaySound(Enums.Sounds.Enemy_Shell_Kick);
        body.velocity = new(body.velocity.x, 5.5f);

        if (IsStationary)
            body.velocity = new(bumper.body.position.x < body.position.x ? 3 : -3, body.velocity.y);
    }

    public override void Kill() {
        EnterShell(false);
    }

    public override void SpecialKill(bool right, bool groundpound, int combo) {
        base.SpecialKill(right, groundpound, combo);
        IsInShell = true;
    }

    #endregion
}