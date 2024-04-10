using UnityEngine;

using Fusion;
using NSMB.Entities.Collectable;
using NSMB.Entities.Collectable.Powerups;
using NSMB.Entities.Player;
using NSMB.Extensions;
using NSMB.Game;
using NSMB.Tiles;
using NSMB.Utils;

namespace NSMB.Entities.Enemies {

    public class Koopa : HoldableEntity {

        //---Static Variables
        private static readonly int ParamShell = Animator.StringToHash("shell");
        private static readonly int ParamXVel = Animator.StringToHash("xVel");
        private static readonly int ParamDead = Animator.StringToHash("dead");

        //---Networked Variables
        [Networked] public TickTimer WakeupTimer { get; set; }
        [Networked] public NetworkBool IsInShell { get; set; }
        [Networked] public NetworkBool IsStationary { get; set; }
        [Networked] public NetworkBool IsUpsideDown { get; set; }
        [Networked] private NetworkBool Putdown { get; set; }
        [Networked] private PlayerController BlueShellCollector { get; set; }

        //---Serialized Variables
        [SerializeField] private Sprite deadSprite;
        [SerializeField] private Transform graphicsTransform;
        [SerializeField] private Vector2 outShellHitboxSize, inShellHitboxSize;
        [SerializeField] private Vector2 outShellHitboxOffset, inShellHitboxOffset;
        [SerializeField] protected float walkSpeed, wakeup = 15;
        [SerializeField] public bool dontFallOffEdges, blue, canBeFlipped = true, flipXFlip;

        //---Properties
        public bool IsActuallyStationary => !Holder && IsStationary;

        //---Private Variables
        private IPowerupCollect powerupCollect;
        private float dampVelocity;

        public override void Start() {
            powerupCollect = GetComponent<IPowerupCollect>();
        }

        public override void Render() {
            base.Render();
            if (IsFrozen || IsDead) {
                return;
            }

            // Animation
            animator.SetBool(ParamShell, IsInShell || Holder != null);
            animator.SetFloat(ParamXVel, IsStationary ? 0 : Mathf.Abs(body.Velocity.x));
            animator.SetBool(ParamDead, !IsActive);

            // "Flip" rotation
            float remainingWakeupTimer = WakeupTimer.RemainingRenderTime(Runner) ?? 0f;
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
            if (!Object) {
                return;
            }

            if (GameManager.Instance && GameManager.Instance.GameEnded) {
                body.Velocity = Vector2.zero;
                animator.enabled = false;
                body.Freeze = true;
                return;
            }

            if (IsFrozen || IsDead) {
                hitbox.enabled = false;
                return;
            } else {
                hitbox.enabled = true;
            }

            if (Holder) {
                FacingRight = Holder.FacingRight;
            }

            PhysicsDataStruct data = body.Data;
            if (IsInShell) {
                hitbox.size = inShellHitboxSize;
                hitbox.offset = inShellHitboxOffset;

                if (IsStationary) {
                    if (data.OnGround && body.Velocity.y < 1) {
                        body.Velocity = Vector2.zero;
                    }

                    if (WakeupTimer.Expired(Runner)) {
                        WakeUp();
                        WakeupTimer = TickTimer.None;
                    }
                }
            } else {
                hitbox.size = outShellHitboxSize;
                hitbox.offset = outShellHitboxOffset;
            }

            if (!Holder && !IsStationary) {
                if (data.HitRight && FacingRight) {
                    Turnaround(false, body.Velocity.x);
                } else if (data.HitLeft && !FacingRight) {
                    Turnaround(true, body.Velocity.x);
                }
            }

            if (dontFallOffEdges && !IsInShell && data.OnGround && Runner.GetPhysicsScene2D().Raycast(body.Position, Vector2.down, 0.5f, Layers.MaskAnyGround)) {
                Vector3 redCheckPos = body.Position + new Vector2(0.1f * (FacingRight ? 1 : -1), 0);
                if (GameManager.Instance) {
                    Utils.Utils.WrapWorldLocation(ref redCheckPos);
                }

                // Turn around if no ground
                if (!Runner.GetPhysicsScene2D().Raycast(redCheckPos, Vector2.down, 0.5f, Layers.MaskAnyGround)) {
                    Turnaround(!FacingRight, body.Velocity.x);
                }
            }

            if (!IsStationary) {
                float x = (IsInShell ? CurrentKickSpeed : walkSpeed) * (FacingRight ? 1 : -1);
                body.Velocity = new(x, body.Velocity.y);
            }

            HandleTile();
        }

        private void HandleTile() {
            if (Holder) {
                return;
            }

            PhysicsDataStruct data = body.Data;

            if (Putdown) {
                if (data.OnGround) {
                    body.Velocity = Vector2.zero;
                    Putdown = false;
                }

            } else if (IsInShell && !IsStationary && (data.HitLeft || data.HitRight)) {
                foreach (PhysicsDataStruct.TileContact tile in data.TilesHitSide) {
                    if (GameManager.Instance.tileManager.GetTile(tile.location, out InteractableTile it)) {
                        it.Interact(this, InteractionDirection.Up, Utils.Utils.TilemapToWorldPosition(tile.location), out bool _);
                    }
                }
            }
        }

        public void WakeUp() {
            IsInShell = false;
            body.Velocity = new(-walkSpeed, 0);
            FacingRight = false;
            IsUpsideDown = false;
            IsStationary = false;

            if (Holder) {
                Holder.SetHeldEntity(null);
            }

            Holder = null;
            PreviousHolder = null;
        }

        public void EnterShell(bool becomeItem, PlayerController player) {
            if (IsDead) {
                return;
            }

            if (blue && !IsInShell && becomeItem) {
                BlueBecomeItem(player);
                return;
            }
            body.Velocity = Vector2.zero;
            WakeupTimer = TickTimer.CreateFromSeconds(Runner, wakeup);
            ComboCounter = 0;
            IsInShell = true;
            IsStationary = true;

            if (player) {
                Holder = null;
                PreviousHolder = player;
                ThrowInvincibility = TickTimer.CreateFromSeconds(Runner, 0.2f);
            }
        }

        public void BlueBecomeItem(PlayerController player) {
            if (player.HasGroundpoundHitbox && !player.IsDrilling) {
                BlueShellCollector = player;
                powerupCollect.OnPowerupCollect(player, Enums.GetPowerupScriptable(Enums.PowerupState.BlueShell));
            } else {
                if (Runner.IsServer) {
                    Runner.Spawn(PrefabList.Instance.Powerup_BlueShell, transform.position + Vector3.down * 0.15f, onBeforeSpawned: (runner, obj) => {
                        obj.GetComponent<Powerup>().OnBeforeSpawned(0.1f);
                    });
                }
            }
            DespawnEntity();
        }

        protected void Turnaround(bool hitWallOnLeft, float x) {
            if (IsActuallyStationary) {
                return;
            }

            if (Runner.IsForward && IsInShell && hitWallOnLeft != FacingRight) {
                PlaySound(Enums.Sounds.World_Block_Bump);
            }

            FacingRight = hitWallOnLeft;
            body.Velocity = new((x > 0.5f ? Mathf.Abs(x) : CurrentKickSpeed) * (FacingRight ? 1 : -1), body.Velocity.y);
        }

        //---BasicEntity overrides
        public override void RespawnEntity() {
            if (IsActive) {
                return;
            }

            base.RespawnEntity();
            IsInShell = false;
            IsStationary = false;
            IsUpsideDown = false;
            WakeupTimer = TickTimer.None;
            Putdown = false;
            BlueShellCollector = null;
        }

        //---IPlayerInteractable overrides
        public override void InteractWithPlayer(PlayerController player, PhysicsDataStruct.IContactStruct contact = null) {

            // Don't interact with anyone if we're being held.
            if (Holder) {
                return;
            }

            // Temporary invincibility
            if (PreviousHolder == player && ThrowInvincibility.IsActive(Runner)) {
                return;
            }

            Utils.Utils.UnwrapLocations(body.Position, player.body.Position, out Vector2 ourPos, out Vector2 theirPos);
            bool fromRight = ourPos.x < theirPos.x;
            Vector2 damageDirection = (theirPos - ourPos).normalized;
            bool attackedFromAbove = damageDirection.y > 0;

            // Do knockback to players in shells
            if (player.IsInShell && !player.IsStarmanInvincible && IsInShell && !IsStationary) {
                player.DoKnockback(!fromRight, 0, true, Object);
                SpecialKill(!fromRight, false, false, player.StarCombo++);
                return;
            }

            // Always damage exceptions
            if (player.InstakillsEnemies) {
                SpecialKill(!fromRight, false, player.State == Enums.PowerupState.MegaMushroom, player.StarCombo++);
                return;
            }

            // Attempt to be picked up (or kicked)
            if (IsInShell && IsActuallyStationary) {
                if (!Holder) {
                    if (player.CanPickupItem) {
                        Pickup(player);
                    } else {
                        Kick(player, !fromRight, Mathf.Abs(player.body.Velocity.x) / player.RunningMaxSpeed, player.IsGroundpounding);
                        PreviousHolder = player;
                    }
                }
                return;
            }

            if (attackedFromAbove) {
                // Get hit by player

                // Groundpound by big mario: shell & kick
                if (player.HasGroundpoundHitbox && player.State != Enums.PowerupState.MiniMushroom) {
                    EnterShell(true, player);
                    if (!blue) {
                        Kick(player, !fromRight, 1f, true);
                        PreviousHolder = player;
                    }
                    if (player.IsDrilling) {
                        player.IsDrilling = false;
                        player.DoEntityBounce = true;
                    }
                    return;
                }

                // Bounced on
                if (player.State == Enums.PowerupState.MiniMushroom) {
                    if (player.HasGroundpoundHitbox) {
                        player.IsGroundpounding = false;
                        EnterShell(true, player);
                    }
                    player.DoEntityBounce = true;
                } else {
                    // Blue Koopa: check to become a blue shell item
                    if (blue && IsInShell && player.HasGroundpoundHitbox) {
                        BlueBecomeItem(player);
                        player.DoEntityBounce = !player.IsGroundpounding;
                        return;
                    }

                    EnterShell(true, player);
                    player.DoEntityBounce = !player.IsGroundpounding;
                }

                player.IsDrilling = false;

            } else {
                // Damage player

                // Turn around when hitting a crouching blue shell player
                if (!IsInShell && player.IsCrouchedInShell) {
                    player.body.Velocity = new(0, player.body.Velocity.y);
                    FacingRight = !fromRight;
                    return;
                }

                // Finally attempt to damage player
                bool damageable = player.IsDamageable;
                player.Powerdown(false);
                if (damageable && !IsInShell) {
                    FacingRight = fromRight;
                }
            }
        }

        //---IBlockBumpable overrides
        public override void BlockBump(BasicEntity bumper, Vector2Int tile, InteractionDirection direction) {
            if (IsDead) {
                return;
            }

            if (!IsInShell || IsStationary) {
                EnterShell(false, bumper as PlayerController);
                Putdown = true;
            }
            IsUpsideDown = canBeFlipped;

            body.Velocity = new(body.Velocity.x, 5.5f);

            if (Holder) {
                Holder.SetHeldEntity(null);
            }

            if (IsStationary) {
                body.Velocity = new(bumper.body.Position.x < body.Position.x ? 1f : -1f, body.Velocity.y);
                body.Data.OnGround = false;
            }

            KickedAnimCounter++;
        }

        //---FreezableEntity overrides
        public override void Freeze(FrozenCube cube) {
            base.Freeze(cube);
            IsStationary = true;
        }

        //---KillableEntity overrides
        protected override void CheckForEntityCollisions() {

            if (!((!IsInShell && !Holder) || IsActuallyStationary || Putdown || IsDead)) {

                int count = Runner.GetPhysicsScene2D().OverlapBox(body.Position + hitbox.offset, hitbox.size, 0, EntityFilter, CollisionBuffer);

                for (int i = 0; i < count; i++) {
                    GameObject obj = CollisionBuffer[i].gameObject;

                    if (obj.transform.IsChildOf(transform)) {
                        continue;
                    }

                    if (Holder && obj.transform.IsChildOf(Holder.transform)) {
                        continue;
                    }

                    // Killable entities
                    if (obj.GetComponentInParent<KillableEntity>() is KillableEntity killable) {
                        if (!killable.collideWithOtherEnemies || killable.IsDead) {
                            continue;
                        }

                        Utils.Utils.UnwrapLocations(body.Position, killable.body ? killable.body.Position : killable.transform.position, out Vector2 ourPos, out Vector2 theirPos);
                        bool fromRight = ourPos.x < theirPos.x;

                        // Kill entity we ran into
                        killable.SpecialKill(fromRight, false, false, ComboCounter++);

                        // If we hit another moving shell (or we're being held), we both die.
                        if (Holder || (killable is Koopa kw && kw.IsInShell && !kw.IsActuallyStationary)) {
                            SpecialKill(!fromRight, false, false, 0);
                            return;
                        }

                        continue;
                    }

                    // Coins
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

            if (IsDead) {
                sRenderer.sprite = deadSprite;
            }
        }

        //---ThrowableEntity overrides
        public override void Kick(PlayerController thrower, bool toRight, float kickFactor, bool groundpound) {
            base.Kick(thrower, toRight, kickFactor, groundpound);
            IsStationary = false;
            WakeupTimer = TickTimer.None;
            ComboCounter = 1;
        }

        public override void Throw(bool toRight, bool crouch) {
            CurrentKickSpeed = throwSpeed + 1.5f * (Mathf.Abs(Holder.body.Velocity.x) / Holder.RunningMaxSpeed);
            base.Throw(toRight, crouch);

            IsStationary = crouch;
            IsInShell = true;
            if (!crouch) {
                WakeupTimer = TickTimer.None;
            }

            Putdown = crouch;
        }

        //---OnChangeds
        protected override void HandleRenderChanges(bool fillBuffer, ref NetworkBehaviourBuffer oldBuffer, ref NetworkBehaviourBuffer newBuffer) {
            base.HandleRenderChanges(fillBuffer, ref oldBuffer, ref newBuffer);

            foreach (var change in ChangesBuffer) {
                switch (change) {
                case nameof(BlueShellCollector):
                    OnBlueShellCollectorChanged();
                    break;
                }
            }
        }

        private void OnBlueShellCollectorChanged() {
            if (BlueShellCollector) {
                BlueShellCollector.PlaySound(Enums.Sounds.Player_Sound_PowerupCollect);
            }
        }
    }
}
