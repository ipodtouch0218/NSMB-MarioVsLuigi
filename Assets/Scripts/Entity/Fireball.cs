using UnityEngine;

using Fusion;
using NSMB.Entities.Player;
using NSMB.Extensions;
using NSMB.Game;
using NSMB.Tiles;

namespace NSMB.Entities {

    [RequireComponent(typeof(PhysicsEntity))]
    [OrderAfter(typeof(PlayerController), typeof(NetworkPhysicsSimulation2D))]
    public class Fireball : BasicEntity, IPlayerInteractable, IFireballInteractable {

        //---Static Variables
        private static readonly Collider2D[] CollisionBuffer = new Collider2D[16];

        //---Networked Variables
        [Networked] public PlayerController Owner { get; set; }
        [Networked] private float CurrentSpeed { get; set; }
        [Networked] public NetworkBool AlreadyBounced { get; set; }
        [Networked] public NetworkBool IsIceball { get; set; }
        [Networked(OnChanged = nameof(OnBreakEffectAnimCounterChanged))] public byte BreakEffectAnimCounter { get; set; }

        //---Serialized Variables
        [SerializeField] private ParticleSystem iceBreak, fireBreak, iceTrail, fireTrail;
        [SerializeField] private GameObject iceGraphics, fireGraphics;
        [SerializeField] private Color ourTeamColor = new(0.6f, 0.6f, 0.6f), enemyTeamColor = Color.white;
        [SerializeField] private float fireSpeed = 6.25f, iceSpeed = 4.25f;
        [SerializeField] private float bounceHeight = 6.75f, terminalVelocity = 6.25f;

        //---Components
        [SerializeField] private PhysicsEntity physics;
        [SerializeField] private SpriteRenderer[] renderers;
        [SerializeField] private BoxCollider2D hitbox;

        [SerializeField] private EntityMover bodyy;

        public override void OnValidate() {
            base.OnValidate();
            if (!physics) physics = GetComponent<PhysicsEntity>();
            if (!nrb) nrb = GetComponent<NetworkRigidbody2D>();
            if (!hitbox) hitbox = GetComponent<BoxCollider2D>();
            if ((renderers?.Length ?? 0) == 0) renderers = GetComponentsInChildren<SpriteRenderer>();
        }

        public void Initialize(PlayerController owner, Vector2 spawnpoint, bool ice, bool right) {
            // Vars
            IsActive = true;
            IsIceball = ice;
            FacingRight = right;
            AlreadyBounced = false;
            Owner = owner;

            // Speed
            bodyy.gravity = 9.81f * (IsIceball ? 2.2f : 4.4f) * Vector2.down;
            if (IsIceball) {
                CurrentSpeed = iceSpeed + Mathf.Abs(owner.body.velocity.x / 3f);
            } else {
                CurrentSpeed = fireSpeed;
            }

            // Physics
            bodyy.position = spawnpoint;
            //nrb.TeleportToPosition(spawnpoint, Vector3.zero);
            //nrb.Rigidbody.position = spawnpoint;
            bodyy.freeze = false;
            bodyy.velocity = new(CurrentSpeed * (FacingRight ? 1 : -1), -CurrentSpeed);
        }

        public override void Spawned() {
            base.Spawned();

            bodyy.freeze = true;
            iceGraphics.SetActive(false);
            fireGraphics.SetActive(false);

            transform.SetParent(GameManager.Instance.objectPoolParent.transform);

            if (!GameData.Instance.PooledFireballs.Contains(this))
                GameData.Instance.PooledFireballs.Add(this);
        }

        public override void Render() {
            if (GameData.Instance.GameEnded) {
                foreach (var anim in GetComponentsInChildren<LegacyAnimateSpriteRenderer>())
                    anim.enabled = false;
            }
        }

        public override void FixedUpdateNetwork() {
            bodyy.freeze = !IsActive;
            hitbox.enabled = IsActive;

            if (!IsActive) {
                bodyy.velocity = Vector2.zero;
                return;
            }

            if (GameData.Instance && GameData.Instance.GameEnded) {
                bodyy.freeze = true;
                return;
            }

            if (bodyy.position.y < GameManager.Instance.LevelMinY) {
                DespawnEntity();
                return;
            }

            //if (!HandleCollision())
            //    return;

            if (!CheckForEntityCollision())
                return;

            bodyy.velocity = new(CurrentSpeed * (FacingRight ? 1 : -1), Mathf.Max(-terminalVelocity, bodyy.velocity.y));
        }

        //---Helper Methods
        private bool HandleCollision() {
            if (!IsActive)
                return false;

            PhysicsEntity.PhysicsDataStruct data = physics.UpdateCollisions();

            if (data.OnGround && !AlreadyBounced) {
                float boost = bounceHeight * Mathf.Abs(Mathf.Sin(physics.Data.FloorAngle * Mathf.Deg2Rad)) * 1.25f;
                if (Mathf.Sign(data.FloorAngle) != Mathf.Sign(bodyy.velocity.x))
                    boost = 0;

                bodyy.velocity = new(bodyy.velocity.x, bounceHeight + boost);
            } else if (IsIceball && bodyy.velocity.y > 0.1f) {
                AlreadyBounced = true;
            }
            bool breaking = data.HitLeft || data.HitRight || data.HitRoof || (data.OnGround && AlreadyBounced && bodyy.velocity.y < 1f);
            if (breaking) {
                DespawnEntity();
                return false;
            }

            if (Utils.Utils.IsTileSolidAtWorldLocation(bodyy.position)) {
                DespawnEntity();
                return false;
            }

            return true;
        }

        private bool CheckForEntityCollision() {
            if (!IsActive)
                return false;

            int count = Runner.GetPhysicsScene2D().OverlapBox(bodyy.position + physics.currentCollider.offset, ((BoxCollider2D) physics.currentCollider).size, 0, default, CollisionBuffer);

            for (int i = 0; i < count; i++) {
                GameObject collidedObject = CollisionBuffer[i].gameObject;

                // Don't interact with ourselves.
                if (CollisionBuffer[i].attachedRigidbody == bodyy)
                    continue;

                if (collidedObject.GetComponentInParent<IFireballInteractable>() is IFireballInteractable interactable) {
                    // Don't interact with our owner
                    if (interactable is PlayerController player && player == Owner)
                        continue;

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
            if (data is not bool playEffect || playEffect)
                BreakEffectAnimCounter++;

            IsActive = false;
            bodyy.freeze = true;
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

                bool sameTeam = Owner.data.Team == Runner.GetLocalPlayerData().Team || Owner.cameraController.IsControllingCamera;
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
        public void InteractWithPlayer(PlayerController player) {
            // If we're not active, don't collide.
            if (!IsActive)
                return;

            if (!Runner.IsServer && !player.HasInputAuthority)
                return;

            // Check if they own us. If so, don't collide.
            if (Owner == player)
                return;

            // If they have knockback invincibility, don't collide.
            if (player.DamageInvincibilityTimer.IsActive(Runner))
                return;

            // Iceball combo exception
            if (IsIceball && player.IsInKnockback) {
                DespawnEntity();
                return;
            }

            // Should do damage checks
            if (!player.IsStarmanInvincible) {

                bool dropStars = player.data.Team != Owner.data.Team;

                // Player state checks
                switch (player.State) {
                case Enums.PowerupState.MegaMushroom: {
                    return;
                }
                case Enums.PowerupState.MiniMushroom: {
                    if (dropStars)
                        player.Death(false, false);
                    else
                        player.DoKnockback(!FacingRight, 0, true, Object);

                    DespawnEntity();
                    return;
                }
                case Enums.PowerupState.BlueShell: {
                    if (player.IsInShell || player.IsCrouching || player.IsGroundpounding) {
                        if (IsIceball)
                            player.ShellSlowdownTimer = TickTimer.CreateFromSeconds(Runner, 0.65f);
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
            if (!IsActive || !fireball.IsActive)
                return false;

            // Fire + ice = both destroy
            if (IsIceball) {
                DespawnEntity();
                fireball.DespawnEntity();
                return true;
            }
            return false;
        }

        public bool InteractWithIceball(Fireball iceball) {
            if (!IsActive || !iceball.IsActive)
                return false;

            // Fire + ice = both destroy
            if (!IsIceball) {
                DespawnEntity();
                iceball.DespawnEntity();
                return true;
            }
            return false;
        }

        //---IBlockBumpable overrides
        public override void BlockBump(BasicEntity bumper, Vector2Int tile, InteractableTile.InteractionDirection direction) {
            // Do nothing when bumped
        }

        //---OnChangeds
        public static void OnBreakEffectAnimCounterChanged(Changed<Fireball> changed) {
            if (!GameData.Instance.PlaySounds)
                return;

            Fireball fireball = changed.Behaviour;

            // Don't play particles below the killplane
            if (fireball.bodyy.position.y < GameManager.Instance.LevelMinY)
                return;

            // Or if the game is over
            if (GameData.Instance.GameState == Enums.GameState.Ended)
                return;

            if (fireball.IsIceball) {
                fireball.iceBreak.Play();
                fireball.sfx.PlayOneShot(Enums.Sounds.Powerup_Iceball_Break);
            } else {
                fireball.fireBreak.Play();
                fireball.sfx.PlayOneShot(Enums.Sounds.Powerup_Fireball_Break);
            }
        }
    }
}
