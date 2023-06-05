using UnityEngine;

using Fusion;
using NSMB.Entities.Collectable;
using NSMB.Entities.Player;
using NSMB.Extensions;
using NSMB.Game;
using NSMB.Utils;

namespace NSMB.Entities {

    [OrderAfter(typeof(NetworkPhysicsSimulation2D), typeof(BasicEntity), typeof(FreezableEntity))]
    public class FrozenCube : HoldableEntity {

        //---Networked Variables
        [Networked] private FreezableEntity FrozenEntity { get; set; }
        [Networked] private Vector2 EntityPositionOffset { get; set; }
        [Networked] private NetworkBool FastSlide { get; set; }
        [Networked] public TickTimer AutoBreakTimer { get; set; }
        [Networked] private byte Combo { get; set; }
        [Networked] private NetworkBool Fallen { get; set; }
        [Networked] public UnfreezeReason KillReason { get; set; }

        //---Serialized Variables
        [SerializeField] private float shakeSpeed = 1f, shakeAmount = 0.1f, autoBreak = 3f;

        //---Private Variables

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
            hitbox.offset = Vector2.up * hitbox.size * 0.5f;

            EntityPositionOffset = -(bounds.center - Vector3.up.Multiply(bounds.size * 0.5f) - rendererObject.transform.position);

            body.position -= EntityPositionOffset - Vector2.down * 0.1f;

            AutoBreakTimer = TickTimer.CreateFromSeconds(Runner, autoBreak);
            flying = FrozenEntity.IsFlying;
            ApplyConstraints();

            FrozenEntity.Freeze(this);

            // Move entity inside us
            if (!FrozenEntity.IsCarryable) {
                dieWhenInsideBlock = false;
            }
        }

        public override void Despawned(NetworkRunner runner, bool hasState) {
            Instantiate(PrefabList.Instance.Particle_IceBreak, transform.position, Quaternion.identity);
        }

        public override void Render() {
            base.Render();

            if (FrozenEntity && FrozenEntity.IsCarryable && FrozenEntity.nrb.InterpolationTarget && nrb.InterpolationTarget) {
                Transform target = FrozenEntity.nrb.InterpolationTarget.transform;
                Vector3 newPos = nrb.InterpolationTarget.position + (Vector3) EntityPositionOffset + (Vector3.forward * -0.1f);
                Utils.Utils.WrapWorldLocation(ref newPos);
                target.position = newPos;
            }
        }

        public override void FixedUpdateNetwork() {
            base.FixedUpdateNetwork();

            if (!Object)
                return;

            gameObject.layer = (Holder || FastSlide) ? Layers.LayerEntity : Layers.LayerGroundEntity;

            if (body.position.y + hitbox.size.y < GameManager.Instance.LevelMinY) {
                Kill();
                return;
            }

            if (Holder && Utils.Utils.IsAnyTileSolidBetweenWorldBox(body.position + hitbox.offset, hitbox.size * transform.lossyScale * 0.75f)) {
                KillWithReason(UnfreezeReason.HitWall);
                return;
            }

            // Handle interactions with tiles
            if (FrozenEntity.IsCarryable) {
                if (!HandleTile())
                    return;

                FrozenEntity.body.position = body.position + EntityPositionOffset;
            }

            if (FrozenEntity is PlayerController || (!Holder && !FastSlide)) {

                if (AutoBreakTimer.Expired(Runner)) {
                    if (!FastSlide)
                        KillReason = UnfreezeReason.Timer;

                    if (flying)
                        Fallen = true;
                    else {
                        KillWithReason(UnfreezeReason.Timer);
                        return;
                    }
                }
            }

            if (Holder)
                return;

            if (!FastSlide) {
                float remainingTime = AutoBreakTimer.RemainingTime(Runner) ?? 0f;
                if (remainingTime < 1f)
                    body.position = new(body.position.x + Mathf.Sin(remainingTime * shakeSpeed) * shakeAmount * Runner.DeltaTime, transform.position.y);
            }


            // Our entity despawned. remove.
            if (!FrozenEntity) {
                Runner.Despawn(Object);
                return;
            }

            // Handle interactions with tiles
            if (FrozenEntity.IsCarryable) {
                if (!HandleTile())
                    return;
            }

            if (FastSlide && physics.Data.OnGround && physics.Data.FloorAngle != 0) {
                RaycastHit2D ray = Runner.GetPhysicsScene2D().BoxCast(body.position + Vector2.up * hitbox.size * 0.5f, hitbox.size, 0, Vector2.down, 0.2f, Layers.MaskSolidGround);
                if (ray) {
                    body.position = new(body.position.x, ray.point.y + Physics2D.defaultContactOffset);
                    if (ray.distance < 0.1f)
                        body.velocity = new(body.velocity.x, Mathf.Min(0, body.velocity.y));
                }
            }

            body.velocity = new(throwSpeed * (FacingRight ? 1 : -1), body.velocity.y);


            ApplyConstraints();
        }

        private bool HandleTile() {
            physics.UpdateCollisions();

            if ((FastSlide && (physics.Data.HitLeft || physics.Data.HitRight))
                || (flying && Fallen && physics.Data.OnGround && !Holder)
                || ((Holder || physics.Data.OnGround) && physics.Data.HitRoof)) {

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

                if (flying && !Fallen)
                    body.constraints |= RigidbodyConstraints2D.FreezePositionY;
            }
        }

        public void KillWithReason(UnfreezeReason reason) {
            KillReason = reason;
            Kill();
        }

        //---IPlayerInteractable overrides
        public override void InteractWithPlayer(PlayerController player) {

            // Don't interact with our lovely holder
            if (Holder == player)
                return;

            // Temporary invincibility
            if (PreviousHolder == player && ThrowInvincibility.IsActive(Runner))
                return;

            Vector2 damageDirection = (player.body.position - body.position).normalized;
            bool attackedFromAbove = damageDirection.y > -0.4f;

            //if (PreviousHolder == player)
            //    return;

            if (!Holder && (player.IsStarmanInvincible || player.State == Enums.PowerupState.MegaMushroom || player.IsInShell)) {
                Kill();
                return;
            }
            if (Fallen || player.IsFrozen)
                return;

            if ((player.IsGroundpounding || player.groundpoundLastFrame) && attackedFromAbove && player.State != Enums.PowerupState.MiniMushroom) {
                KillWithReason(UnfreezeReason.Groundpounded);
                return;

            } else if (!attackedFromAbove && player.State != Enums.PowerupState.MiniMushroom) {
                KillWithReason(UnfreezeReason.BlockBump);
                return;

            } else if (FastSlide) {
                player.DoKnockback(body.position.x > player.body.position.x, 1, false, Object);
                Kill();
                return;
            }
            if (FrozenEntity.IsCarryable && !Holder && !IsDead && player.CanPickupItem && player.IsOnGround && !player.IsSwimming) {
                Fallen = true;
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
            body.velocity = Vector2.zero;
        }

        public override void Throw(bool toRight, bool crouch) {
            base.Throw(toRight, false);

            Fallen = false;
            flying = false;
            FastSlide = true;

            if (FrozenEntity.IsFlying) {
                Fallen = true;
                body.isKinematic = false;
            }
            ApplyConstraints();
        }

        public override void Kick(PlayerController kicker, bool fromLeft, float kickFactor, bool groundpound) {
            // Kicking does nothing.
        }

        //---IKillableEntity overrides
        protected override void CheckForEntityCollisions() {
            if (Holder || !FastSlide)
                return;

            // Only run when fastsliding...
            int count = Runner.GetPhysicsScene2D().OverlapBox(body.position + hitbox.offset, hitbox.size, 0, default, CollisionBuffer);

            for (int i = 0; i < count; i++) {
                GameObject obj = CollisionBuffer[i].gameObject;

                if (obj == gameObject)
                    continue;

                if (PreviousHolder && obj.TryGetComponent(out Coin coin)) {
                    coin.InteractWithPlayer(PreviousHolder);
                    continue;
                }

                if (obj.TryGetComponent(out KillableEntity killable)) {
                    if (killable.IsDead || killable == FrozenEntity)
                        continue;

                    if (Holder == killable || PreviousHolder == killable || FrozenEntity == killable)
                        continue;

                    // Kill entity we ran into
                    killable.SpecialKill(killable.body.position.x > body.position.x, false, Combo++);

                    // Kill ourselves if we're being held too
                    if (Holder)
                        SpecialKill(killable.body.position.x < body.position.x, false, 0);

                    continue;
                }
            }
        }

        public override void Kill() {
            if (Holder) {
                bool dropStars = !(FrozenEntity is PlayerController pc && pc.data.Team == Holder.data.Team);
                Holder.DoKnockback(Holder.FacingRight, dropStars ? 1 : 0, false, FrozenEntity.Object);
                Holder.SetHeldEntity(null);
            }

            if (FrozenEntity) {
                FrozenEntity.Unfreeze(KillReason);
            }

            IsDead = true;
            Runner.Despawn(Object);
        }

        public override void SpecialKill(bool right, bool groundpound, int combo) {
            Kill();
        }

        public override void OnIsDeadChanged() {
            base.OnIsDeadChanged();

            sRenderer.enabled = !IsDead;
        }
    }
}
