using UnityEngine;

using Fusion;
using NSMB.Entities.Player;
using NSMB.Entities.Enemies;
using NSMB.Game;

namespace NSMB.Entities {
    [OrderAfter(typeof(PlayerController))]
    public abstract class HoldableEntity : KillableEntity, IBeforeTick {

        //---Networked Variables
        [Networked] public PlayerController Holder { get; set; }
        [Networked] public PlayerController PreviousHolder { get; set; }
        [Networked] protected TickTimer ThrowInvincibility { get; set; }
        [Networked] protected float CurrentKickSpeed { get; set; }
        [Networked(OnChanged = nameof(OnKickedAnimCounterChanged))] protected byte KickedAnimCounter { get; set; }

        //---Serailized Variables
        [SerializeField] protected float throwSpeed = 4.5f;

        //--Misc Variables
        public Vector3 holderOffset;
        public bool canPlace = true, canKick = true;
        private bool wasHeldLastTick;

        public void BeforeTick() {
            wasHeldLastTick = Holder;
        }

        public override void Render() {
            if (Holder && nrb.InterpolationTarget) {
                Transform target = nrb.InterpolationTarget.transform;
                Vector3 newPos = Holder.networkRigidbody.InterpolationTarget.position + holderOffset;
                Utils.Utils.WrapWorldLocation(ref newPos);
                target.position = newPos;
            }
        }

        public override void FixedUpdateNetwork() {
            if (Holder) {
                body.velocity = Holder.body.velocity;

                // Teleport check
                Vector2 newPosition = Holder.body.position + (Vector2) holderOffset;
                Vector2 diff = newPosition - body.position;
                bool wrapped = Utils.Utils.WrapWorldLocation(ref newPosition);

                if (!wasHeldLastTick || wrapped) {
                    nrb.TeleportToPosition(newPosition, diff);
                } else {
                    body.position = newPosition;
                }

                hitbox.enabled = false;
                CheckForEntityCollisions();
            } else {
                hitbox.enabled = true;
                base.FixedUpdateNetwork();
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
            KickedAnimCounter++;
            CurrentKickSpeed = throwSpeed + 1.5f * kickFactor;
            body.velocity = new(CurrentKickSpeed * (FacingRight ? 1 : -1), groundpound ? 3.5f : 0);
        }

        public virtual void Throw(bool toRight, bool crouching) {
            if (!Holder)
                return;

            if (Utils.Utils.IsTileSolidAtWorldLocation(body.position))
                transform.position = body.position = new(Holder.transform.position.x, body.position.y);

            ThrowInvincibility = TickTimer.CreateFromSeconds(Runner, crouching ? 0.6f : 0.35f);
            PreviousHolder = Holder;
            Holder = null;
            FacingRight = toRight;

            body.velocity = new((crouching && canPlace ? 2f : throwSpeed) * (FacingRight ? 1 : -1), 0);
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

        protected override void CheckForEntityCollisions() {

            int count = Runner.GetPhysicsScene2D().OverlapBox(body.position + hitbox.offset, hitbox.size, 0, EntityFilter, CollisionBuffer);

            for (int i = 0; i < count; i++) {
                GameObject obj = CollisionBuffer[i].gameObject;

                if (obj.transform.IsChildOf(transform))
                    continue;

                if (Holder && obj.transform.IsChildOf(Holder.transform))
                    continue;

                if (obj.GetComponent<KillableEntity>() is KillableEntity killable) {
                    if (killable.IsDead || !killable.collideWithOtherEnemies || killable is PiranhaPlant)
                        continue;

                    Utils.Utils.UnwrapLocations(body.position, killable.body.position, out Vector2 ourPos, out Vector2 theirPos);
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
        }

        //---OnChanged
        public static void OnKickedAnimCounterChanged(Changed<HoldableEntity> changed) {
            if (!GameData.Instance.PlaySounds)
                return;

            if (!changed.Behaviour.IsActive) return;

            changed.Behaviour.PlaySound(Enums.Sounds.Enemy_Shell_Kick);
        }
    }
}
