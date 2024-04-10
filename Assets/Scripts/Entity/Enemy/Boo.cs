using UnityEngine;

using Fusion;
using NSMB.Entities;
using NSMB.Entities.Player;
using NSMB.Extensions;
using NSMB.Game;
using NSMB.Tiles;
using NSMB.Utils;

public class Boo : KillableEntity {

    //---Static Variables
    private static readonly int ParamFacingRight = Animator.StringToHash("FacingRight");
    private static readonly int ParamScared = Animator.StringToHash("Scared");

    //---Networked Variables
    [Networked] private PlayerController CurrentTarget { get; set; }
    [Networked] private NetworkBool Scared { get; set; }
    [Networked] private TickTimer UnscaredTimer { get; set; }

    //---Serailzied Variables
    [SerializeField] private GameObject bounceOffsetObject;

    [SerializeField] private float maxSpeed = 1;
    [SerializeField] private float maxSpeedChangeRatePerSecond = 1;
    [SerializeField] private float maxMoveAngle = 45;
    [SerializeField] private float maxMoveAngleChangeRatePerSecond = 90;
    [SerializeField] private float unscaredTime = 0.2f;
    [SerializeField] private float maxTargetRange = 15f;
    [SerializeField] private Vector2 targetingOffset = new(0, 0.5f);

    [SerializeField] private float sinSpeed = 1;
    [SerializeField] private float sinAmplitude = 0.5f;


    public override void Render() {
        base.Render();

        if (!IsDead) {
            bounceOffsetObject.transform.localPosition = new(0, Mathf.Sin(2 * Mathf.PI * Runner.LocalRenderTime * sinSpeed) * sinAmplitude);
        }
    }

    public override void FixedUpdateNetwork() {
        base.FixedUpdateNetwork();

        if (!Object || !IsActive) {
            return;
        }

        if (IsDead) {
            return;
        } else {
            AngularVelocity = 0;
            body.IsKinematic = true;
            body.Gravity = Vector2.zero;
        }

        // Change targets infrequently to avoid jittery movement from changing targets too fast
        if ((CurrentTarget && CurrentTarget.IsDead) || (Runner.Tick % (Runner.TickRate / 2) == 0)) {
            CurrentTarget = GetNearestPlayer(maxTargetRange);
        }

        // If we don't have a target, don't move. Ever.
        if (!CurrentTarget) {
            Scared = true;
            body.Velocity = Vector2.zero;
            return;
        }

        // Check for becoming scared
        Utils.UnwrapLocations(body.Position, CurrentTarget.body.Position + targetingOffset, out Vector2 ourPosition, out Vector2 targetPosition);
        bool targetOnRight = Utils.WrappedDirectionSign(body.Position, CurrentTarget.body.Position + targetingOffset) == -1;

        bool beingLookedAt = CurrentTarget.FacingRight == !targetOnRight;
        if (beingLookedAt) {
            // Become scared
            Scared = true;
            UnscaredTimer = TickTimer.None;

        } else if (Scared) {
            if (!UnscaredTimer.IsRunning) {
                // Start the unscared timer
                UnscaredTimer = TickTimer.CreateFromSeconds(Runner, unscaredTime);

            } else if (UnscaredTimer.Expired(Runner)) {
                // Become unscared
                Scared = false;
                UnscaredTimer = TickTimer.None;
            }
        }

        // Freeze if we're being scared, move if not.
        if (Scared) {
            body.Velocity = Vector2.zero;

        } else {
            FacingRight = targetOnRight;

            Vector2 forward = Vector2.right * (FacingRight ? 1 : -1);
            Vector2 directionToTarget = (targetPosition - ourPosition).normalized;

            float targetAngle = Mathf.Clamp(Vector2.SignedAngle(forward, directionToTarget), -maxMoveAngle, maxMoveAngle);

            if (body.Velocity == Vector2.zero) {
                // Initial movement
                body.Velocity = maxSpeedChangeRatePerSecond * Runner.DeltaTime * directionToTarget;

            } else {
                float currentAngle = Vector2.SignedAngle(forward, body.Velocity);
                float newAngle = Mathf.MoveTowardsAngle(currentAngle, targetAngle, maxMoveAngleChangeRatePerSecond * Runner.DeltaTime);

                Vector2 newMovementDirection = Quaternion.Euler(0, 0, newAngle) * forward;
                float newSpeed = Mathf.MoveTowards(body.Velocity.magnitude, maxSpeed, maxSpeedChangeRatePerSecond * Runner.DeltaTime);

                body.Velocity = newMovementDirection * newSpeed;
            }
        }
    }

    private PlayerController GetNearestPlayer(float maxDistance) {
        PlayerController closestPlayer = null;
        float closestDistance = maxDistance;

        foreach (PlayerController player in GameManager.Instance.AlivePlayers) {
            if (player.IsDead) {
                continue;
            }

            float dist = Utils.WrappedDistance(body.Position, player.body.Position);
            if (dist < closestDistance) {
                closestPlayer = player;
                closestDistance = dist;
            }
        }

        return closestPlayer;
    }

    public override void InteractWithPlayer(PlayerController player, PhysicsDataStruct.IContactStruct contact = null) {
        if (player.State == Enums.PowerupState.MegaMushroom || player.IsStarmanInvincible || player.IsInShell) {
            bool fromRight = Utils.WrappedDirectionSign(body.Position, CurrentTarget.body.Position + targetingOffset) == -1;
            SpecialKill(!fromRight, false, !player.IsStarmanInvincible, player.StarCombo++);
            return;
        }

        player.Powerdown(false);
    }

    public override bool InteractWithFireball(Fireball fireball) {
        // Do nothing, but destroy the fireball
        return true;
    }

    public override bool InteractWithIceball(Fireball iceball) {
        // Do nothing, but destroy the iceball
        return true;
    }

    public override void BlockBump(BasicEntity bumper, Vector2Int tile, InteractionDirection direction) {
        // Do nothing
    }

    public override void SpecialKill(bool right, bool groundpound, bool mega, int combo) {
        if (IsDead) {
            return;
        }

        IsDead = true;
        WasSpecialKilled = true;
        WasKilledByMega = mega;
        ComboCounter = (byte) combo;
        Scared = true;

        FacingRight = right;
        body.IsKinematic = false;
        body.Velocity = new(2f * (FacingRight ? 1 : -1), 2.5f);
        AngularVelocity = 400f * (FacingRight ? 1 : -1);
        body.Gravity = Vector2.down * 14.75f;

        if (HasStateAuthority) {
            Runner.Spawn(PrefabList.Instance.Obj_LooseCoin, body.Position + hitbox.offset);
        }
    }

    public override void OnIsDeadChanged() {
        base.OnIsDeadChanged();

        if (IsDead) {
            if (WasKilledByMega) {
                sfx.PlayOneShot(Enums.Sounds.Powerup_MegaMushroom_Break_Block);
            } else {
                Instantiate(Enums.PrefabParticle.Enemy_Puff.GetGameObject(), body.Position, Quaternion.identity);
            }
        } else {
            sRenderer.enabled = true;
        }
    }

    public override void OnFacingRightChanged() {
        animator.SetBool(ParamFacingRight, FacingRight);
    }

    protected override void HandleRenderChanges(bool fillBuffer, ref NetworkBehaviourBuffer oldBuffer, ref NetworkBehaviourBuffer newBuffer) {
        base.HandleRenderChanges(fillBuffer, ref oldBuffer, ref newBuffer);

        foreach (var change in ChangesBuffer) {
            switch (change) {
            case nameof(Scared):
                OnScaredChanged();
                break;
            }
        }
    }

    private void OnScaredChanged() {
        animator.SetBool(ParamScared, Scared);

        if (!Scared) {
            sfx.PlayOneShot(Enums.Sounds.Enemy_Boo_Laugh);
        }
    }
}
