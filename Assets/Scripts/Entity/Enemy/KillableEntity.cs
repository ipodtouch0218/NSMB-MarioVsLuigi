using UnityEngine;

using Fusion;
using NSMB.Entities.Enemies;
using NSMB.Entities.Player;
using NSMB.Extensions;
using NSMB.Game;
using NSMB.Tiles;
using NSMB.Utils;

namespace NSMB.Entities {
    public abstract class KillableEntity : FreezableEntity, IPlayerInteractable, IFireballInteractable {

        //---Static Variables
        private static readonly Enums.Sounds[] ComboSounds = {
            Enums.Sounds.Enemy_Shell_Kick,
            Enums.Sounds.Enemy_Shell_Combo1,
            Enums.Sounds.Enemy_Shell_Combo2,
            Enums.Sounds.Enemy_Shell_Combo3,
            Enums.Sounds.Enemy_Shell_Combo4,
            Enums.Sounds.Enemy_Shell_Combo5,
            Enums.Sounds.Enemy_Shell_Combo6,
            Enums.Sounds.Enemy_Shell_Combo7,
        };
        protected static readonly Collider2D[] CollisionBuffer = new Collider2D[32];
        protected static readonly ContactPoint2D[] ContactBuffer = new ContactPoint2D[32];
        protected static ContactFilter2D EntityFilter;

        //---Networked Variables
        [Networked] public NetworkBool IsDead { get; set; }
        [Networked] protected NetworkBool WasSpecialKilled { get; set; }
        [Networked] protected NetworkBool WasCrushed { get; set; }
        [Networked] protected NetworkBool WasKilledByMega { get; set; }
        [Networked] protected NetworkBool WasGroundpounded { get; set; }
        [Networked] protected float AngularVelocity { get; set; }
        [Networked] protected byte ComboCounter { get; set; }

        //---Properties
        public override float Height => hitbox.size.y;
        public override bool IsCarryable => iceCarryable;
        public override bool IsFlying => flying;
        public override Vector2 FrozenSize {
            get {
                Bounds bounds = default;
                Renderer[] renderers = GetComponentsInChildren<Renderer>();
                foreach (Renderer renderer in renderers) {
                    if (!renderer.enabled || renderer is ParticleSystemRenderer) {
                        continue;
                    }

                    renderer.ResetBounds();

                    if (bounds == default) {
                        bounds = new(renderer.bounds.center, renderer.bounds.size);
                    } else {
                        bounds.Encapsulate(renderer.bounds);
                    }
                }

                Vector2 size = bounds.size;
                return size;
            }
        }
        public override Vector2 FrozenOffset {
            get {
                Vector2 entityPosition = transform.position;
                Bounds bounds = default;
                Renderer[] renderers = GetComponentsInChildren<Renderer>();
                foreach (Renderer renderer in renderers) {
                    if (!renderer.enabled || renderer is ParticleSystemRenderer) {
                        continue;
                    }

                    renderer.ResetBounds();

                    if (bounds == default) {
                        bounds = new(renderer.bounds.center, renderer.bounds.size);
                    } else {
                        bounds.Encapsulate(renderer.bounds);
                    }
                }

                Vector2 position = new(bounds.center.x, bounds.min.y);
                Vector2 offset = entityPosition - position;

                return offset;
            }
        }

        //---Serialized Variables
        [SerializeField] protected bool iceCarryable = true;
        [SerializeField] protected bool flying = false;
        [SerializeField] public bool collideWithOtherEnemies = true;
        [SerializeField] protected bool dieWhenInsideBlock = true;
        [SerializeField] protected bool flipSpriteRenderer = false;

        //---Components
        [SerializeField] public BoxCollider2D hitbox;
        [SerializeField] protected Animator animator;
        [SerializeField] protected LegacyAnimateSpriteRenderer legacyAnimation;
        [SerializeField] public SpriteRenderer sRenderer;

        public override void OnValidate() {
            base.OnValidate();
            this.SetIfNull(ref hitbox);
            this.SetIfNull(ref animator, UnityExtensions.GetComponentType.Children);
            this.SetIfNull(ref sRenderer, UnityExtensions.GetComponentType.Children);
            this.SetIfNull(ref legacyAnimation, UnityExtensions.GetComponentType.Children);
        }

        public virtual void Start() {
            if (!EntityFilter.useLayerMask) {
                EntityFilter.SetLayerMask(Layers.MaskEntities);
            }
        }

        public override void Spawned() {
            base.Spawned();
            OnFacingRightChanged();

            if (Runner.Topology == Topologies.ClientServer) {
                Runner.SetIsSimulated(Object, true);
            }
        }

        public override void Render() {
            base.Render();
            if (!IsActive) {
                return;
            }

            if (IsDead) {
                transform.rotation *= Quaternion.Euler(0, 0, AngularVelocity * Time.deltaTime);
            } else {
                transform.rotation = Quaternion.identity;
            }
        }

        public override void FixedUpdateNetwork() {
            base.FixedUpdateNetwork();
            if (!GameManager.Instance || !Object || !body) {
                return;
            }

            if (!IsActive) {
                gameObject.layer = Layers.LayerHitsNothing;
                AngularVelocity = 0;
                transform.rotation = Quaternion.identity;
                body.Velocity = Vector2.zero;
                body.Freeze = true;
                return;

            } else if (IsDead || IsFrozen) {
                gameObject.layer = Layers.LayerHitsNothing;
                body.Freeze = false;

                if (WasSpecialKilled) {
                    AngularVelocity = 400f * (FacingRight ? 1 : -1);
                }
                return;
            } else {
                gameObject.layer = Layers.LayerEntity;
                body.Freeze = false;
            }

            if (collideWithOtherEnemies) {
                CheckForEntityCollisions();
            }

            if (dieWhenInsideBlock) {
                Vector2 loc = body.Position + hitbox.offset * transform.lossyScale;
                if (!body.Freeze && Utils.Utils.IsTileSolidAtWorldLocation(loc)) {
                    Crushed();
                    return;
                }
            }
        }

        protected virtual void CheckForEntityCollisions() {

            int count = Runner.GetPhysicsScene2D().OverlapBox(body.Position + hitbox.offset, hitbox.size, 0, CollisionBuffer, Layers.MaskEntities);

            for (int i = 0; i < count; i++) {
                GameObject obj = CollisionBuffer[i].gameObject;

                if (obj.transform.IsChildOf(transform)) {
                    continue;
                }

                if (obj.GetComponent<KillableEntity>() is not { } killable) {
                    continue;
                }

                if (killable.IsDead || !killable.collideWithOtherEnemies || killable is PiranhaPlant) {
                    continue;
                }

                Utils.Utils.UnwrapLocations(body.Position, killable.body.Position, out Vector2 ourPos, out Vector2 theirPos);
                bool goRight = ourPos.x > theirPos.x;

                if (Mathf.Abs(ourPos.x - theirPos.x) < 0.015f) {
                    if (Mathf.Abs(ourPos.y - theirPos.y) < 0.015f) {
                        goRight = Object.Id.Raw < killable.Object.Id.Raw;
                    } else {
                        goRight = ourPos.y < theirPos.y;
                    }
                }

                FacingRight = goRight;
            }
        }

        public virtual void Kill() {
            if (IsDead) {
                return;
            }

            SpecialKill(false, false, false, 0);
        }

        public virtual void SpecialKill(bool right, bool groundpound, bool mega, int combo) {
            if (IsDead) {
                return;
            }

            IsDead = true;
            WasSpecialKilled = true;
            WasGroundpounded = groundpound;
            WasKilledByMega = mega;
            WasCrushed = false;
            ComboCounter = (byte) combo;
            FacingRight = right;

            body.Velocity = new(2f * (FacingRight ? 1 : -1), 2.5f);
            AngularVelocity = 400f * (FacingRight ? 1 : -1);
            body.Gravity = Vector2.down * 14.75f;

            if (HasStateAuthority) {
                Runner.Spawn(PrefabList.Instance.Obj_LooseCoin, body.Position + hitbox.offset);
            }
        }

        public virtual void Crushed() {
            if (WasCrushed) {
                return;
            }

            DespawnEntity();
            IsActive = false;
            WasCrushed = true;
            ComboCounter = 0;
            body.Velocity = new(2f * (FacingRight ? 1 : -1), 2.5f);
        }

        public virtual void OnIsDeadChanged() {
            if (IsDead) {
                // Death effects
                if (animator) {
                    animator.enabled = false;
                }

                sfx.enabled = true;

                if (WasKilledByMega) {
                    PlaySound(Enums.Sounds.Powerup_MegaMushroom_Break_Block);
                } else if (WasSpecialKilled || WasCrushed) {
                    PlaySound(!IsFrozen ? ComboSounds[Mathf.Min(ComboSounds.Length - 1, ComboCounter)] : Enums.Sounds.Enemy_Generic_FreezeShatter);
                }

                if (WasGroundpounded) {
                    Instantiate(PrefabList.Instance.Particle_EnemySpecialKill, body.Position + hitbox.offset, Quaternion.identity);
                }
            } else {
                // Undo death effects
                if (animator) {
                    animator.enabled = true;
                }
            }
        }

        public void PlaySound(Enums.Sounds sound) {
            sfx.PlayOneShot(sound);
        }

        //---BasicEntity overrides
        public override void OnIsActiveChanged() {
            if (IsActive) {
                if (sRenderer) {
                    sRenderer.enabled = true;
                }

                if (body) {
                    transform.rotation = Quaternion.identity;
                }
            } else {
                if (sRenderer) {
                    sRenderer.enabled = false;
                }
            }
        }

        public override void OnFacingRightChanged() {
            sRenderer.flipX = FacingRight ^ flipSpriteRenderer;
        }

        public override void RespawnEntity() {
            if (IsActive) {
                return;
            }

            base.RespawnEntity();
            IsDead = false;
            IsFrozen = false;
            FacingRight = false;
            WasSpecialKilled = false;
            WasGroundpounded = false;
            WasKilledByMega = false;
            WasCrushed = false;
            ComboCounter = 0;

            if (body) {
                body.Gravity = Vector2.down * 21.5f;
            }
        }

        public override void DespawnEntity(object data = null) {
            base.DespawnEntity(data);
            if (!Object) {
                return;
            }

            IsDead = true;
            WasSpecialKilled = false;
            WasGroundpounded = false;
            WasKilledByMega = false;
            WasCrushed = false;
        }

        //---IPlayerInteractable overrides
        public virtual void InteractWithPlayer(PlayerController player, PhysicsDataStruct.IContactStruct contact = null) {

            Utils.Utils.UnwrapLocations(body.Position + Vector2.up * 0.1f, player.body.Position, out Vector2 ourPos, out Vector2 theirPos);
            Vector2 damageDirection = (theirPos - ourPos).normalized;
            bool attackedFromAbove = Vector2.Dot(damageDirection, Vector2.up) > 0.3f;

            bool groundpounded = attackedFromAbove && player.HasGroundpoundHitbox && player.State != Enums.PowerupState.MiniMushroom;
            if (player.InstakillsEnemies || groundpounded) {
                if (player.IsDrilling) {
                    Kill();
                    player.DoEntityBounce = true;
                } else {
                    SpecialKill(player.body.Velocity.x > 0, player.IsGroundpounding, player.State == Enums.PowerupState.MegaMushroom, player.StarCombo++);
                }
                return;
            }

            if (attackedFromAbove) {
                if (player.State == Enums.PowerupState.MiniMushroom) {
                    if (player.IsGroundpounding) {
                        player.IsGroundpounding = false;
                        Kill();
                    }
                    player.DoEntityBounce = true;
                } else {
                    Kill();
                    player.DoEntityBounce = !player.IsGroundpounding;
                }

                player.IsDrilling = false;

            } else if (player.IsCrouchedInShell) {
                FacingRight = damageDirection.x < 0;
                player.body.Velocity = new(0, player.body.Velocity.y);

            } else if (player.IsDamageable) {
                player.Powerdown(false);
                FacingRight = damageDirection.x > 0;
            }
        }

        //---IFireballInteractable overrides
        public virtual bool InteractWithFireball(Fireball fireball) {
            if (IsDead) {
                return false;
            }

            SpecialKill(fireball.FacingRight, false, false, 0);
            return true;
        }

        public virtual bool InteractWithIceball(Fireball iceball) {
            if (IsDead) {
                return false;
            }

            if (!IsFrozen) {
                FrozenCube.FreezeEntity(Runner, this);
            }
            return true;
        }

        //---IBlockBumpable overrides
        public override void BlockBump(BasicEntity bumper, Vector2Int tile, InteractionDirection direction) {
            SpecialKill(false, false, false, 0);
        }

        //---FreezableEntity overrides
        public override void Freeze(FrozenCube cube) {
            IsFrozen = true;

            if (body) {
                body.Velocity = Vector2.zero;
                body.Freeze = true;
            }
        }

        public override void Unfreeze(UnfreezeReason reasonByte) {
            IsFrozen = false;
            hitbox.enabled = true;

            SpecialKill(false, false, false, 0);
        }

        public override void OnIsFrozenChanged() {
            if (IsFrozen) {
                sfx.Stop();
            }

            if (animator) {
                animator.enabled = !IsFrozen;
            }

            if (legacyAnimation) {
                legacyAnimation.enabled = !IsFrozen;
            }
        }

        //---OnChangeds
        protected override void HandleRenderChanges(bool fillBuffer, ref NetworkBehaviourBuffer oldBuffer, ref NetworkBehaviourBuffer newBuffer) {
            base.HandleRenderChanges(fillBuffer, ref oldBuffer, ref newBuffer);

            foreach (var change in ChangesBuffer) {
                switch (change) {
                case nameof(IsDead): OnIsDeadChanged(); break;
                case nameof(WasCrushed): OnWasCrushedChanged(); break;
                }
            }
        }

        public void OnWasCrushedChanged() {
            if (!WasCrushed) {
                return;
            }

            PlaySound(Enums.Sounds.Enemy_Shell_Kick, null, 0, 0.5f);
            SpawnParticle(body.Position, Enums.PrefabParticle.Enemy_Puff);
        }
    }
}
