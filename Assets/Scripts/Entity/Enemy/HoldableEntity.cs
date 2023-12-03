using UnityEngine;

using Fusion;
using NSMB.Entities.Player;
using NSMB.Entities.Enemies;
using NSMB.Game;

namespace NSMB.Entities {
    //[OrderAfter(typeof(PlayerController))]
    public abstract class HoldableEntity : KillableEntity, IBeforeTick {

        //---Networked Variables
        [Networked] public PlayerController Holder { get; set; }
        [Networked] public PlayerController PreviousHolder { get; set; }
        [Networked] protected TickTimer ThrowInvincibility { get; set; }
        [Networked] protected float CurrentKickSpeed { get; set; }
        [Networked] protected byte KickedAnimCounter { get; set; }

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
            base.Render();
            if (IsDead)
                return;

            if (Holder) {
                Vector3 newPos = Holder.transform.position + holderOffset;
                Utils.Utils.WrapWorldLocation(ref newPos);
                transform.position = newPos;
            }
        }

        public override void FixedUpdateNetwork() {
            if (Holder) {
                body.Velocity = Holder.body.Velocity;
                body.Position = Holder.body.Position + (Vector2) holderOffset;
                hitbox.enabled = false;

                body.IsKinematic = true;
                CheckForEntityCollisions();
            } else {
                hitbox.enabled = true;
                body.IsKinematic = false;
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
            body.Velocity = new(CurrentKickSpeed * (FacingRight ? 1 : -1), groundpound ? 3.5f : 0);
        }

        public virtual void Throw(bool toRight, bool crouching) {
            if (!Holder)
                return;

            if (Utils.Utils.IsTileSolidAtWorldLocation(body.Position))
                transform.position = body.Position = new(Holder.transform.position.x, body.Position.y);

            ThrowInvincibility = TickTimer.CreateFromSeconds(Runner, crouching ? 0.6f : 0.35f);
            PreviousHolder = Holder;
            Holder = null;
            FacingRight = toRight;

            body.Velocity = new((crouching && canPlace ? 2f : throwSpeed) * (FacingRight ? 1 : -1), 0);
        }

        public virtual void Pickup(PlayerController player) {
            if (Holder)
                return;

            player.SetHeldEntity(this);
        }

        //---IPlayerInteractable overrides
        public override void InteractWithPlayer(PlayerController player, PhysicsDataStruct.IContactStruct contact = null) {
            // Don't interact with our lovely holder
            if (Holder == player)
                return;

            // Temporary invincibility
            if (PreviousHolder == player && !ThrowInvincibility.ExpiredOrNotRunning(Runner))
                return;

            base.InteractWithPlayer(player, contact);
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

            int count = Runner.GetPhysicsScene2D().OverlapBox(body.Position + hitbox.offset, hitbox.size, 0, EntityFilter, CollisionBuffer);

            for (int i = 0; i < count; i++) {
                GameObject obj = CollisionBuffer[i].gameObject;

                if (obj.transform.IsChildOf(transform))
                    continue;

                if (Holder && obj.transform.IsChildOf(Holder.transform))
                    continue;

                if (obj.GetComponent<KillableEntity>() is KillableEntity killable) {
                    if (killable.IsDead || !killable.collideWithOtherEnemies || killable is PiranhaPlant)
                        continue;

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
        }

        //---OnChanged
        protected override void HandleRenderChanges(bool fillBuffer, ref NetworkBehaviourBuffer oldBuffer, ref NetworkBehaviourBuffer newBuffer) {
            base.HandleRenderChanges(fillBuffer, ref oldBuffer, ref newBuffer);

            foreach (var change in ChangesBuffer) {
                switch (change) {
                case nameof(KickedAnimCounter): OnKickedAnimCounterChanged(); break;
                }
            }
        }

        public void OnKickedAnimCounterChanged() {
            if (GameManager.Instance.GameState != Enums.GameState.Playing)
                return;

            if (!IsActive) return;

            PlaySound(Enums.Sounds.Enemy_Shell_Kick);
        }
    }
}
