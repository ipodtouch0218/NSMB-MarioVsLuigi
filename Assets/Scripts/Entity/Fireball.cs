using UnityEngine;

using Fusion;
using NSMB.Entities.Player;
using NSMB.Extensions;
using NSMB.Game;
using NSMB.Tiles;
using NSMB.Utils;

namespace NSMB.Entities {
    public class Fireball : BasicEntity, IPlayerInteractable, IFireballInteractable {

        //---Static Variables
        private static readonly Collider2D[] CollisionBuffer = new Collider2D[16];

        //---Networked Variables
        [Networked] public PlayerController Owner { get; set; }
        [Networked] private float CurrentSpeed { get; set; }
        [Networked] public NetworkBool AlreadyBounced { get; set; }
        [Networked] public NetworkBool IsIceball { get; set; }
        [Networked] public int InactiveTick { get; set; }
        [Networked] private Vector2 BreakEffectLocation { get; set; }
        [Networked] private byte BreakEffectAnimCounter { get; set; }

        //---Properties
        public bool IsHitboxActive => IsActive || ((Runner.Tick - InactiveTick) <= Runner.TickRate * 0.5f);

        //---Serialized Variables
        [SerializeField] private ParticleSystem iceBreak, fireBreak, iceTrail, fireTrail;
        [SerializeField] private GameObject iceGraphics, fireGraphics;
        [SerializeField] private Color ourTeamColor = new(0.6f, 0.6f, 0.6f), enemyTeamColor = Color.white;
        [SerializeField] private float fireSpeed = 6.25f, iceSpeed = 4.25f;
        [SerializeField] private float bounceHeight = 6.75f, terminalVelocity = 6.25f;

        //---Components
        [SerializeField] private SpriteRenderer[] renderers;
        [SerializeField] private BoxCollider2D hitbox;

        public override void OnValidate() {
            base.OnValidate();
            if (!hitbox) {
                hitbox = GetComponent<BoxCollider2D>();
            }

            if ((renderers?.Length ?? 0) == 0) {
                renderers = GetComponentsInChildren<SpriteRenderer>();
            }
        }

        public void Initialize(PlayerController owner, Vector2 spawnpoint, bool ice, bool right) {
            // Vars
            IsActive = true;
            IsIceball = ice;
            FacingRight = right;
            AlreadyBounced = false;
            Owner = owner;

            // Speed
            body.Gravity = 9.81f * (IsIceball ? 2.2f : 4.4f) * Vector2.down;
            if (IsIceball) {
                CurrentSpeed = iceSpeed + Mathf.Abs(owner.body.Velocity.x / 3f);
            } else {
                CurrentSpeed = fireSpeed;
            }

            // Physics
            body.Position = spawnpoint;
            body.Freeze = false;
            body.Velocity = new(CurrentSpeed * (FacingRight ? 1 : -1), -CurrentSpeed);
            Runner.SetIsSimulated(Object, !owner.IsProxy);
        }

        public override void Spawned() {
            base.Spawned();

            body.Freeze = true;
            iceGraphics.SetActive(false);
            fireGraphics.SetActive(false);

            transform.SetParent(GameManager.Instance.objectPoolParent.transform);

            if (!GameManager.Instance.PooledFireballs.Contains(this)) {
                GameManager.Instance.PooledFireballs.Add(this);
            }
        }

        public override void Render() {
            base.Render();

            if (GameManager.Instance.GameEnded) {
                foreach (var anim in GetComponentsInChildren<LegacyAnimateSpriteRenderer>()) {
                    anim.enabled = false;
                }
            }
        }

        public override void FixedUpdateNetwork() {
            body.Freeze = !IsActive;
            gameObject.layer = IsActive ? Layers.LayerEntity : Layers.LayerEntityNoGroundEntity;

            if (!IsActive) {
                body.Velocity = Vector2.zero;
                if (!IsHitboxActive) {
                    return;
                }
            }

            if (GameManager.Instance && GameManager.Instance.GameEnded) {
                body.Freeze = true;
                return;
            }

            if (body.Position.y < GameManager.Instance.LevelMinY) {
                DespawnEntity();
                return;
            }

            if (!HandleCollision()) {
                return;
            }

            if (!CheckForEntityCollision()) {
                return;
            }

            body.Velocity = new(CurrentSpeed * (FacingRight ? 1 : -1), Mathf.Max(-terminalVelocity, body.Velocity.y));
        }

        //---Helper Methods
        private bool HandleCollision() {
            if (!IsActive) {
                return IsHitboxActive;
            }

            PhysicsDataStruct data = body.Data;

            if (data.OnGround && !AlreadyBounced) {
                float boost = bounceHeight * Mathf.Abs(Mathf.Sin(data.FloorAngle * Mathf.Deg2Rad)) * 1.25f;
                if ((data.FloorAngle > 0) == FacingRight) {
                    boost = 0;
                }

                body.Velocity = new(body.Velocity.x, bounceHeight + boost);
            } else if (IsIceball && body.Velocity.y > 0.1f) {
                AlreadyBounced = true;
            }
            bool breaking = data.HitLeft || data.HitRight || data.HitRoof || (data.OnGround && AlreadyBounced);
            if (breaking) {
                DespawnEntity();
                return false;
            }

            if (Utils.Utils.IsTileSolidAtWorldLocation(body.Position)) {
                DespawnEntity();
                return false;
            }

            return true;
        }

        private bool CheckForEntityCollision() {
            if (!IsHitboxActive) {
                return false;
            }

            int count = Runner.GetPhysicsScene2D().OverlapBox(body.Position + hitbox.offset, hitbox.size, 0, default, CollisionBuffer);

            for (int i = 0; i < count; i++) {
                GameObject collidedObject = CollisionBuffer[i].gameObject;

                // Don't interact with ourselves.
                if (CollisionBuffer[i].attachedRigidbody == body) {
                    continue;
                }

                if (collidedObject.GetComponentInParent<IFireballInteractable>() is IFireballInteractable interactable) {
                    // Don't interact with players in this way
                    if (interactable is PlayerController) {
                        continue;
                    }

                    bool result = IsIceball ? interactable.InteractWithIceball(this) : interactable.InteractWithFireball(this);
                    if (result) {
                        // True = interacted & destroy.
                        DespawnEntity();
                        return false;
                    }
                }
            }

            return true;
        }

        //---BasicEntity overrides
        public override void DespawnEntity(object data = null) {
            if (!IsActive) {
                return;
            }

            if (data is not bool playEffect || playEffect) {
                if (body.Position.y > GameManager.Instance.LevelMinY && GameManager.Instance.GameState != Enums.GameState.Ended) {
                    if (body.Position == BreakEffectLocation) {
                        BreakEffectLocation += Vector2.up * 0.01f;
                    } else {
                        BreakEffectLocation = body.Position;
                    }
                    BreakEffectAnimCounter++;
                }
            }

            IsActive = false;
            InactiveTick = Runner.Tick;
            body.Freeze = true;
            body.Position = new(0, -1000); // Bodge...
        }

        public override void OnIsActiveChanged() {
            if (IsActive) {
                // Activate graphics and particles
                bool ice = IsIceball;

                if (ice) {
                    iceTrail.Play();
                    fireTrail.Stop();
                } else {
                    fireTrail.Play();
                    iceTrail.Stop();
                }
                iceGraphics.SetActive(ice);
                fireGraphics.SetActive(!ice);

                bool sameTeam = Owner.Data.Team == Runner.GetLocalPlayerData().Team || Owner.cameraController.IsControllingCamera;
                foreach (SpriteRenderer r in renderers) {
                    r.flipX = FacingRight;
                    r.color = sameTeam ? ourTeamColor : enemyTeamColor;
                }

            } else {
                // Disable graphics & trail, but play poof fx
                iceGraphics.SetActive(false);
                fireGraphics.SetActive(false);
                iceTrail.Stop();
                fireTrail.Stop();
            }
        }

        //---IPlayerInteractable overrides
        public void InteractWithPlayer(PlayerController player, PhysicsDataStruct.IContactStruct contact = null) {
            // If we're not active, don't collide.
            if (!IsHitboxActive) {
                return;
            }

            // Check if they own us. If so, don't collide.
            if (Owner == player) {
                return;
            }

            // If they have knockback invincibility, don't collide.
            if (player.DamageInvincibilityTimer.IsActive(Runner)) {
                return;
            }

            // Iceball combo exception
            if (IsIceball && player.IsInKnockback) {
                DespawnEntity();
                return;
            }

            // Should do damage checks
            if (!player.IsStarmanInvincible) {

                bool dropStars = player.Data.Team != Owner.Data.Team;

                // Player state checks
                switch (player.State) {
                case Enums.PowerupState.MegaMushroom: {
                    return;
                }
                case Enums.PowerupState.MiniMushroom: {
                    if (dropStars) {
                        player.Death(false, false);
                    } else {
                        player.DoKnockback(!FacingRight, 0, true, Object);
                    }

                    DespawnEntity();
                    return;
                }
                case Enums.PowerupState.BlueShell: {
                    if (player.IsInShell || player.IsCrouching || player.IsGroundpounding) {
                        if (IsIceball) {
                            player.ShellSlowdownTimer = TickTimer.CreateFromSeconds(Runner, 0.65f);
                        }

                        DespawnEntity();
                        return;
                    }

                    break;
                }
                }

                // Collision is a GO
                if (IsIceball && dropStars) {
                    // Iceball
                    if (!player.IsFrozen) {
                        FrozenCube.FreezeEntity(Runner, player);
                    }
                } else {
                    // Fireball
                    player.DoKnockback(!FacingRight, dropStars ? 1 : 0, true, Object);
                }
            }

            // Destroy ourselves.
            DespawnEntity();
        }

        //---IFireballInteractable overrides
        public bool InteractWithFireball(Fireball fireball) {
            if (!IsActive || !fireball.IsActive) {
                return false;
            }

            // Fire + ice = both destroy
            if (IsIceball) {
                DespawnEntity();
                fireball.DespawnEntity();
                return true;
            }
            return false;
        }

        public bool InteractWithIceball(Fireball iceball) {
            if (!IsActive || !iceball.IsActive) {
                return false;
            }

            // Fire + ice = both destroy
            if (!IsIceball) {
                DespawnEntity();
                iceball.DespawnEntity();
                return true;
            }
            return false;
        }

        //---IBlockBumpable overrides
        public override void BlockBump(BasicEntity bumper, Vector2Int tile, InteractionDirection direction) {
            // Do nothing when bumped
        }

        //---OnChangeds
        protected override void HandleRenderChanges(bool fillBuffer, ref NetworkBehaviourBuffer oldBuffer, ref NetworkBehaviourBuffer newBuffer) {
            base.HandleRenderChanges(fillBuffer, ref oldBuffer, ref newBuffer);

            foreach (var change in ChangesBuffer) {
                switch (change) {
                case nameof(BreakEffectAnimCounter):
                    OnBreakEffectAnimCounterChanged();
                    break;
                }
            }
        }

        public void OnBreakEffectAnimCounterChanged() {
            if (GameManager.Instance.GameState != Enums.GameState.Playing) {
                return;
            }

            sfx.transform.position = BreakEffectLocation;
            if (IsIceball) {
                iceBreak.Play();
                sfx.PlayOneShot(Enums.Sounds.Powerup_Iceball_Break);
            } else {
                fireBreak.Play();
                sfx.PlayOneShot(Enums.Sounds.Powerup_Fireball_Break);
            }
        }
    }
}
