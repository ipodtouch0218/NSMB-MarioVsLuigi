using UnityEngine;

using Fusion;
using NSMB.Entities.Collectable;
using NSMB.Entities.Player;
using NSMB.Extensions;
using NSMB.Game;
using NSMB.Utils;

namespace NSMB.Entities {

    public class FrozenCube : HoldableEntity {

        //---Networked Variables
        [Networked] private FreezableEntity FrozenEntity { get; set; }
        [Networked] private Vector2 EntityPositionOffset { get; set; }
        [Networked] private Vector2 CubeSize { get; set; }
        [Networked] private NetworkBool FastSlide { get; set; }
        [Networked] public TickTimer AutoBreakTimer { get; set; }
        [Networked] private byte Combo { get; set; }
        [Networked] private NetworkBool Fallen { get; set; }
        [Networked] public UnfreezeReason KillReason { get; set; }

        //---Serialized Variables
        public float autoBreak = 3f;
        [SerializeField] private float shakeSpeed = 1f, shakeAmount = 0.1f, floatSpeed = 0.25f, floatAcceleration = 2f, floatDamping = 0.995f;

        public void OnBeforeSpawned(FreezableEntity entityToFreeze, Vector2 size, Vector2 offset) {
            FrozenEntity = entityToFreeze;
            CubeSize = size;
            EntityPositionOffset = offset;

            if (!FrozenEntity.IsCarryable) {
                body.IsKinematic = true;
            }
        }

        public override void Spawned() {
            base.Spawned();
            holderOffset = Vector2.one;

            if (!FrozenEntity) {
                Kill();
                return;
            }

            sRenderer.size = CubeSize;
            hitbox.size = CubeSize - (Vector2.one * 0.05f);
            hitbox.offset = CubeSize * Vector2.up * 0.5f;

            AutoBreakTimer = TickTimer.CreateFromSeconds(Runner, autoBreak);
            flying = FrozenEntity.IsFlying;
            ApplyConstraints();

            FrozenEntity.Freeze(this);
            sfx.PlayOneShot(Enums.Sounds.Enemy_Generic_Freeze);

            // Move entity inside us
            if (!FrozenEntity.IsCarryable) {
                dieWhenInsideBlock = false;
                body.LockY = true;
            }

            body.LockX = true;
            transform.position = new(transform.position.x, transform.position.y, -4.5f);
            if (Runner.Topology == Topologies.ClientServer) {
                Runner.SetIsSimulated(Object, true);
            }
        }

        public override void Despawned(NetworkRunner runner, bool hasState) {
            Instantiate(PrefabList.Instance.Particle_IceBreak, transform.position, Quaternion.identity);
        }

        public override void Render() {
            base.Render();

            if (!Object || IsDead || !FrozenEntity) {
                return;
            }

            if (!Holder) {
                Vector3 newPosition = transform.position;
                float remainingTime = AutoBreakTimer.RemainingRenderTime(Runner) ?? 0f;

                if (remainingTime < 1 && ((!Holder && !FastSlide) || FrozenEntity is PlayerController)) {
                    // Shaking animation. Don't play if we're being held or moving fast, unless we're a player
                    newPosition = body.Position + Vector2.right * (Mathf.Sin(remainingTime * shakeSpeed) * shakeAmount);
                }
                newPosition.z = FrozenEntity.transform.position.z - 1;
                transform.position = newPosition;
            }

            if (FrozenEntity && FrozenEntity.IsCarryable) {
                Transform target = FrozenEntity.transform;
                Vector2 newPos = (Vector2) transform.position + EntityPositionOffset;
                Utils.Utils.WrapWorldLocation(ref newPos);
                target.position = new(newPos.x, newPos.y, target.position.z);
            }
        }

        public override void FixedUpdateNetwork() {

            // Our entity despawned. remove.
            if (!FrozenEntity) {
                Runner.Despawn(Object);
                return;
            }

            if (!Object) {
                return;
            }

            if (GameManager.Instance.GameEnded) {
                body.Velocity = Vector2.zero;
                return;
            }

            base.FixedUpdateNetwork();

            if (!Object || IsDead) {
                gameObject.layer = Layers.LayerHitsNothing;
                return;
            }

            gameObject.layer = (Holder || FastSlide) ? Layers.LayerEntity : Layers.LayerGroundEntity;

            if (body.Position.y + hitbox.size.y < GameManager.Instance.LevelMinY) {
                KillWithReason(UnfreezeReason.Other);
                return;
            }

            if (Holder && Utils.Utils.IsAnyTileSolidBetweenWorldBox(body.Position + hitbox.offset, hitbox.size * transform.lossyScale * 0.75f)) {
                KillWithReason(UnfreezeReason.HitWall);
                return;
            }

            // Handle interactions with tiles
            if (FrozenEntity.IsCarryable) {
                if (!HandleTile()) {
                    return;
                }

                FrozenEntity.body.Position = body.Position + EntityPositionOffset;
                FrozenEntity.body.Velocity = Vector2.zero;
                FrozenEntity.body.Freeze = true;
            }

            if (FrozenEntity is PlayerController || (!Holder && !FastSlide)) {
                if (AutoBreakTimer.Expired(Runner)) {
                    if (!FastSlide) {
                        KillReason = UnfreezeReason.Timer;
                    }

                    if (flying) {
                        Fallen = true;
                        ApplyConstraints();
                    } else {
                        KillWithReason(UnfreezeReason.Timer);
                        return;
                    }
                }
            }

            if (Holder) {
                return;
            }

            // Handle interactions with tiles
            if (FrozenEntity.IsCarryable) {
                if (!HandleTile()) {
                    return;
                }
            }

            if (FastSlide) {
                body.Velocity = new(CurrentKickSpeed * (FacingRight ? 1 : -1), body.Velocity.y);
            }

            if (InWater) {
                if (FastSlide && FrozenEntity is not PlayerController) {
                    AutoBreakTimer = TickTimer.CreateFromSeconds(Runner, autoBreak);
                }
                FastSlide = false;

                float newVelocity = body.Velocity.y;
                if (newVelocity < 0) {
                    newVelocity *= floatDamping;
                }
                newVelocity += (floatAcceleration * Runner.DeltaTime);
                newVelocity = Mathf.Min(floatSpeed, newVelocity);

                body.Velocity = new Vector2(body.Velocity.x * 0.95f, newVelocity);
            }

            ApplyConstraints();
        }

        private bool HandleTile() {
            if ((FastSlide && (body.Data.HitLeft || body.Data.HitRight))
                || (flying && Fallen && body.Data.OnGround && !Holder)
                || ((Holder || body.Data.OnGround) && body.Data.HitRoof)) {

                Kill();
                return false;
            }

            return true;
        }

        private void ApplyConstraints() {
            if (!FrozenEntity.IsCarryable) {
                body.Freeze = true;
                return;
            }

            body.Freeze = false;

            if (!Holder) {
                body.LockX = !FastSlide && !InWater;
                body.LockY = flying && !Fallen;
            } else {
                body.LockX = false;
                body.LockY = false;
            }
        }

        public void KillWithReason(UnfreezeReason reason) {
            KillReason = reason;
            Kill();
        }

        //---IPlayerInteractable overrides
        public override void InteractWithPlayer(PlayerController player, PhysicsDataStruct.IContactStruct contact = null) {

            // Don't interact with our lovely holder
            if (Holder == player) {
                return;
            }

            // Don't interact with other frozen players
            if (player.IsFrozen) {
                return;
            }

            // Temporary invincibility
            if (PreviousHolder == player && ThrowInvincibility.IsActive(Runner)) {
                return;
            }

            Utils.Utils.UnwrapLocations(body.Position, player.body.Position, out Vector2 ourPos, out Vector2 playerPos);

            bool attackedFromAbove = Vector2.Dot((playerPos - ourPos).normalized, Vector2.up) > 0.4f;
            bool attackedFromBelow = playerPos.y < ourPos.y
                && playerPos.x - (player.MainHitbox.size.x * 0.5f) < (ourPos.x + hitbox.size.x * 0.5f)
                && playerPos.x + (player.MainHitbox.size.x * 0.5f) > (ourPos.x - hitbox.size.x * 0.5f);

            // Player should instakill
            if (!Holder && player.InstakillsEnemies) {
                Kill();
                return;
            }

            if (player.HasGroundpoundHitbox && attackedFromAbove && player.State != Enums.PowerupState.MiniMushroom) {
                // Groundpounded by player
                player.body.Velocity = new(0, 38.671875f * Runner.DeltaTime);
                player.GroundpoundAnimCounter++;
                FrozenEntity.FacingRight = ourPos.x < playerPos.x;
                KillWithReason(UnfreezeReason.Groundpounded);
                return;

            } else if (attackedFromBelow && player.State != Enums.PowerupState.MiniMushroom) {
                // Bumped from below
                KillWithReason(UnfreezeReason.BlockBump);
                return;

            } else if (FastSlide || Holder) {
                // Do damage
                player.DoKnockback(ourPos.x > playerPos.x, 1, false, Object);
                Kill();
                return;

            } else if (!Fallen) {
                if (FrozenEntity.IsCarryable && !Holder && !IsDead && player.CanPickupItem && player.IsOnGround && !player.InWater) {
                    // Pickup
                    Fallen = true;
                    Pickup(player);
                }
            }
        }

        //---IFireballInteractable overrides
        public override bool InteractWithFireball(Fireball fireball) {
            if (!fireball.IsIceball) {
                Kill();
            }

            return true;
        }

        public override bool InteractWithIceball(Fireball iceball) {
            return true;
        }

        //---IThrowableEntity overrides
        public override void Pickup(PlayerController player) {
            base.Pickup(player);
            Physics2D.IgnoreCollision(hitbox, player.MainHitbox);
            AutoBreakTimer = TickTimer.CreateFromSeconds(Runner, (AutoBreakTimer.RemainingTime(Runner) ?? 0f) + 1f);
            body.Velocity = Vector2.zero;
        }

        public override void Throw(bool toRight, bool crouch) {
            base.Throw(toRight, false);

            Fallen = false;
            flying = false;
            FastSlide = true;
            ThrowInvincibility = TickTimer.CreateFromSeconds(Runner, 3f);

            if (FrozenEntity.IsFlying) {
                Fallen = true;
                body.Freeze = false;
            }
            ApplyConstraints();
        }

        public override void Kick(PlayerController kicker, bool fromLeft, float kickFactor, bool groundpound) {
            // Kicking does nothing.
        }

        //---IKillableEntity overrides
        public override void Crushed() {
            KillWithReason(UnfreezeReason.Other);
        }

        protected override void CheckForEntityCollisions() {
            if (Holder || !FastSlide) {
                return;
            }

            // Only run when fastsliding...
            int count = Runner.GetPhysicsScene2D().OverlapBox(body.Position + hitbox.offset, hitbox.size, 0, CollisionBuffer, Layers.MaskEntities);

            for (int i = 0; i < count; i++) {
                GameObject obj = CollisionBuffer[i].gameObject;

                if (obj.transform.IsChildOf(transform)) {
                    continue;
                }

                if (PreviousHolder && obj.TryGetComponent(out Coin coin)) {
                    coin.InteractWithPlayer(PreviousHolder);
                    continue;
                }

                if (obj.TryGetComponent(out KillableEntity killable)) {
                    if (killable.IsDead || killable == FrozenEntity) {
                        continue;
                    }

                    if (Holder == killable || PreviousHolder == killable || FrozenEntity == killable) {
                        continue;
                    }

                    // Kill entity we ran into
                    killable.SpecialKill(killable.body.Position.x > body.Position.x, false, false, Combo++);

                    // Kill ourselves if we're being held too
                    if (Holder) {
                        SpecialKill(killable.body.Position.x < body.Position.x, false, false, 0);
                    }

                    continue;
                }
            }
        }

        public override void Kill() {
            if (Holder) {
                if (FrozenEntity is PlayerController pc) {
                    bool dropStars = pc.Data.Team != Holder.Data.Team;
                    Holder.DoKnockback(Holder.FacingRight, dropStars ? 1 : 0, false, FrozenEntity.Object);
                }
                Holder.SetHeldEntity(null);
            }

            if (FrozenEntity) {
                FrozenEntity.Unfreeze(KillReason);
            }

            IsDead = true;
            IsActive = false;
            body.Freeze = true;
            Runner.Despawn(Object);
        }

        public override void SpecialKill(bool right, bool groundpound, bool mega, int combo) {
            Kill();
        }

        public override void OnFacingRightChanged() {
            // Never rotate our sprite
        }

        //---OnChangeds
        public override void OnIsDeadChanged() {
            base.OnIsDeadChanged();

            sRenderer.enabled = !IsDead;
        }

        //---Static
        public static void FreezeEntity(NetworkRunner runner, FreezableEntity entity) {
            if (!runner.IsServer) {
                return;
            }

            Vector2 entityPosition = entity.body ? entity.body.Position : entity.transform.position;
            Vector2 spawnPosition = entityPosition - entity.FrozenOffset + (Vector2.up * 0.05f);

            runner.Spawn(PrefabList.Instance.Obj_FrozenCube, spawnPosition, onBeforeSpawned: (runner, obj) => {
                FrozenCube cube = obj.GetComponent<FrozenCube>();
                cube.OnBeforeSpawned(entity, entity.FrozenSize, entity.FrozenOffset);
            });
        }
    }
}
