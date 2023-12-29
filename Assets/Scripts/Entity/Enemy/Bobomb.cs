using System.Collections.Generic;
using System.Linq;
using UnityEngine;

using Fusion;
using NSMB.Entities.Player;
using NSMB.Extensions;
using NSMB.Game;
using NSMB.Tiles;
using NSMB.Utils;

namespace NSMB.Entities.Enemies {
    public class Bobomb : HoldableEntity {

        //---Static Variables
        private static readonly List<Collider2D> DetonationHits = new();

        //---Networked Variables
        [Networked] public TickTimer DetonationTimer { get; set; }
        [Networked] private NetworkBool IsDetonated { get; set; }

        //---Serialized Variables
        [SerializeField] private GameObject explosionPrefab;
#pragma warning disable CS0414
        [SerializeField] private float walkSpeed = 0.6f, kickSpeed = 4.5f, detonationTime = 4f;
#pragma warning restore CS0414
        [SerializeField] private int explosionTileSize = 1;

        //---Misc Variables
        private MaterialPropertyBlock mpb;

        //---Properties
        public bool Lit => DetonationTimer.IsActive(Runner);

        public override void Spawned() {
            base.Spawned();
            body.Velocity = new(walkSpeed * (FacingRight ? 1 : -1), body.Velocity.y);
            mpb ??= new();
        }

        public override void Render() {
            base.Render();

            if (!Lit) {
                mpb.SetFloat("FlashAmount", 0);
                sRenderer.SetPropertyBlock(mpb);
                return;
            }

            float timeUntilDetonation = DetonationTimer.RemainingTime(Runner) ?? 0f;
            float redOverlayPercent = 5.39f / (timeUntilDetonation + 2.695f) * 10f % 1f;

            mpb.SetFloat("FlashAmount", redOverlayPercent);
            sRenderer.SetPropertyBlock(mpb);
        }

        public override void FixedUpdateNetwork() {
            base.FixedUpdateNetwork();
            if (GameManager.Instance.GameEnded) {
                body.Velocity = Vector2.zero;
                body.Freeze = true;
                AngularVelocity = 0;
                animator.enabled = false;
                return;
            }

            if (!Object || IsFrozen || IsDead) {
                return;
            }

            if (HandleCollision()) {
                return;
            }

            if (DetonationTimer.Expired(Runner)) {
                Detonate();
                return;
            }

            if (!Lit) {
                body.Velocity = new(walkSpeed * (FacingRight ? 1 : -1), body.Velocity.y);
            }
        }

        private bool HandleCollision() {
            if (Holder) {
                return false;
            }

            PhysicsDataStruct data = body.Data;

            if (Lit && data.OnGround) {
                //apply friction
                body.Velocity -= body.Velocity * (Runner.DeltaTime * 3.5f);
                if (Mathf.Abs(body.Velocity.x) < 0.05) {
                    body.Velocity = new(0, body.Velocity.y);
                }
            }

            if (data.HitRight && FacingRight) {
                Turnaround(false);
            } else if (data.HitLeft && !FacingRight) {
                Turnaround(true);
            }

            if (data.OnGround && data.HitRoof) {
                Detonate();
                return true;
            }

            return false;
        }

        public void Light() {
            if (Lit) {
                return;
            }

            DetonationTimer = TickTimer.CreateFromSeconds(Runner, detonationTime);
            body.Velocity = Vector2.zero;
        }

        public void Detonate() {
            IsDead = true;
            IsDetonated = true;

            // Damage entities in range. TODO: change to nonalloc?
            DetonationHits.Clear();
            Runner.GetPhysicsScene2D().OverlapCircle(body.Position + hitbox.offset, 1f, default, DetonationHits);

            // Use distinct to only damage enemies once
            foreach (GameObject hitObj in DetonationHits.Select(c => c.gameObject).Distinct()) {
                // Don't interact with ourselves
                if (hitObj == gameObject) {
                    continue;
                }

                // Interact with players by powerdown-ing them
                if (hitObj.GetComponentInParent<PlayerController>() is PlayerController player) {
                    player.Powerdown(false);
                    continue;
                }

                // Interact with other entities by special killing htem
                if (hitObj.GetComponentInParent<KillableEntity>() is KillableEntity en) {
                    en.SpecialKill(transform.position.x < hitObj.transform.position.x, false, 0);
                    continue;
                }
            }

            // (sort or) 'splode tiles in range.
            Vector2Int tileLocation = Utils.Utils.WorldToTilemapPosition(body.Position);
            TileManager tm = GameManager.Instance.tileManager;
            for (int x = -explosionTileSize; x <= explosionTileSize; x++) {
                for (int y = -explosionTileSize; y <= explosionTileSize; y++) {
                    // Use taxi-cab distance to make a somewhat circular explosion
                    if (Mathf.Abs(x) + Mathf.Abs(y) > explosionTileSize) {
                        continue;
                    }

                    Vector2Int ourLocation = tileLocation + new Vector2Int(x, y);
                    Utils.Utils.WrapTileLocation(ref ourLocation);

                    if (tm.GetTile(ourLocation, out InteractableTile tile)) {
                        tile.Interact(this, InteractionDirection.Up, Utils.Utils.TilemapToWorldPosition(ourLocation), out bool _);
                    }
                }
            }

            // Suicide is badass
            DespawnTimer = TickTimer.CreateFromSeconds(Runner, 0.5f);
        }

        public void Turnaround(bool hitWallOnLeft) {
            FacingRight = hitWallOnLeft;
            body.Velocity = new((Lit ? Mathf.Abs(body.Velocity.x) : walkSpeed) * (FacingRight ? 1 : -1), body.Velocity.y);

            if (Runner.IsForward) {
                animator.SetTrigger("turnaround");
            }
        }

        //---IPlayerInteractable overrides
        public override void InteractWithPlayer(PlayerController player, PhysicsDataStruct.IContactStruct contact = null) {

            // Temporary invincibility, we dont want to spam the kick sound
            if (PreviousHolder == player && ThrowInvincibility.IsActive(Runner)) {
                return;
            }

            // Special insta-kill cases
            if (player.InstakillsEnemies) {
                SpecialKill(player.body.Velocity.x > 0, false, player.StarCombo++);
                return;
            }

            Utils.Utils.UnwrapLocations(body.Position + Vector2.up * 0.1f, player.body.Position, out Vector2 ourPos, out Vector2 theirPos);
            bool fromRight = ourPos.x < theirPos.x;

            Vector2 damageDirection = (theirPos - ourPos).normalized;
            bool attackedFromAbove = Vector2.Dot(damageDirection, Vector2.up) > 0.3f;

            // Normal interactions
            if (Lit) {
                if (!Holder && player.CanPickupItem) {
                    // Pickup by player
                    Pickup(player);
                } else {
                    // Kicked by player
                    Kick(player, !fromRight, Mathf.Abs(player.body.Velocity.x) / player.RunningMaxSpeed, player.IsGroundpounding);
                }
            } else {
                if (attackedFromAbove) {
                    // Light
                    bool mini = player.State == Enums.PowerupState.MiniMushroom;
                    if (!mini || player.IsGroundpounding) {
                        Light();
                    }

                    if (!mini && player.IsGroundpounding) {
                        Kick(player, !fromRight, Mathf.Abs(player.body.Velocity.x) / player.RunningMaxSpeed, player.IsGroundpounding);

                    } else {
                        player.DoEntityBounce = true;
                        player.IsGroundpounding = false;
                    }

                    player.IsDrilling = false;
                } else if (player.IsCrouchedInShell) {
                    // Bounce off blue shell crouched player
                    FacingRight = damageDirection.x < 0;
                    player.body.Velocity = new(0, player.body.Velocity.y);
                    return;

                } else if (player.IsDamageable) {
                    // Damage
                    player.Powerdown(false);
                    FacingRight = damageDirection.x > 0;
                }
            }
        }

        //---IBlockBumpable overrides
        public override void BlockBump(BasicEntity bumper, Vector2Int tile, InteractionDirection direction) {
            //Light if we get bumped
            Light();
        }

        //---IFireballInteractable overrides
        public override bool InteractWithFireball(Fireball fireball) {
            if (!Lit) {
                Light();
            } else {
                Kick(null, fireball.FacingRight, 0f, false);
            }
            return true;
        }

        //---KillableEntity overrides
        public override void Kill() {
            Light();
        }

        public override void RespawnEntity() {
            if (IsActive) {
                return;
            }

            base.RespawnEntity();
            IsDetonated = false;
            DetonationTimer = TickTimer.None;
        }

        public override void OnIsDeadChanged() {
            base.OnIsDeadChanged();

            if (IsDead) {
                sfx.Stop();
            }
        }

        protected override void CheckForEntityCollisions() {
            base.CheckForEntityCollisions();
            if (IsDead || !Lit || Mathf.Abs(body.Velocity.x) < 1f) {
                return;
            }

            int count = Runner.GetPhysicsScene2D().OverlapBox(body.Position + hitbox.offset, hitbox.size, 0, CollisionBuffer, Layers.MaskEntities);

            for (int i = 0; i < count; i++) {
                GameObject obj = CollisionBuffer[i].gameObject;

                if (obj.transform.IsChildOf(transform)) {
                    continue;
                }

                // Killable entities
                if (obj.TryGetComponent(out KillableEntity killable)) {
                    if (killable.IsDead) {
                        continue;
                    }

                    // Kill entity we ran into
                    killable.SpecialKill(killable.body.Position.x > body.Position.x, false, ComboCounter++);

                    // Kill ourselves if we're being held too
                    if (Holder) {
                        SpecialKill(killable.body.Position.x < body.Position.x, false, 0);
                    }

                    continue;
                }
            }
        }

        //---ThrowableEntity overrides
        public override void Kick(PlayerController kicker, bool toRight, float speed, bool groundpound) {
            // Always do a groundpound variant kick
            base.Kick(kicker, toRight, speed, true);
        }

        //---OnChangeds
        protected override void HandleRenderChanges(bool fillBuffer, ref NetworkBehaviourBuffer oldBuffer, ref NetworkBehaviourBuffer newBuffer) {
            base.HandleRenderChanges(fillBuffer, ref oldBuffer, ref newBuffer);

            foreach (var change in ChangesBuffer) {
                switch (change) {
                case nameof(IsDetonated): OnIsDetonatedChanged(); break;
                case nameof(DetonationTimer): OnDetonationTimerChanged(); break;
                }
            }
        }

        private GameObject explosion;
        public void OnIsDetonatedChanged() {

            if (IsDetonated) {
                // Spawn explosion
                if (!explosion) {
                    explosion = Instantiate(explosionPrefab, sRenderer.bounds.center, Quaternion.identity);
                }

                sRenderer.enabled = false;
                sfx.Pause();
            } else {
                sRenderer.enabled = true;
                sfx.UnPause();
            }
        }

        public void OnDetonationTimerChanged() {
            animator.SetBool("lit", Lit);

            if (!Lit) {
                return;
            }

            PlaySound(Enums.Sounds.Enemy_Bobomb_Fuse);
        }
    }
}
