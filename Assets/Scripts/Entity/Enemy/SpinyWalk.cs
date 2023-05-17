using UnityEngine;

using NSMB.Utils;

//This is pretty much just the koopawalk script but it causes damage when you stand on it.
public class SpinyWalk : KoopaWalk {

    //---IPlayerInteractable overrides
    public override void InteractWithPlayer(PlayerController player) {

        if (Holder)
            return;

        //temporary invincibility, we dont want to spam the kick sound
        if (PreviousHolder == player && !ThrowInvincibility.ExpiredOrNotRunning(Runner))
            return;

        Utils.UnwrapLocations(body.position, player.body.position, out Vector2 ourPos, out Vector2 theirPos);
        bool fromRight = ourPos.x < theirPos.x;
        Vector2 damageDirection = (theirPos - ourPos).normalized;
        bool attackedFromAbove = Vector2.Dot(damageDirection, Vector2.up) > 0f;

        // Special kill
        if (player.InstakillsEnemies) {
            SpecialKill(!player.FacingRight, false, player.StarCombo++);

            if (player.IsInShell && IsInShell && !IsStationary && Mathf.Sign(body.velocity.x) != Mathf.Sign(player.body.velocity.x))
                // Do knockback to player, colliding with us in shell going opposite ways
                player.DoKnockback(!fromRight, 0, false, Object);

            return;
        }

        // Don't interact with players if we're being held.
        if (Holder)
            return;

        // Don't interact with crouched blue shell players
        if (!attackedFromAbove && player.IsCrouchedInShell) {
            FacingRight = damageDirection.x < 0;
            return;
        }

        if (IsInShell) {
            // In shell.
            if (IsActuallyStationary) {
                // We aren't moving. Check for kicks & pickups
                if (player.CanPickupItem) {
                    // Pickup
                    Pickup(player);
                } else {
                    // Kick
                    Kick(player, !fromRight, Mathf.Abs(player.body.velocity.x) / player.RunningMaxSpeed, player.IsGroundpounding);
                }
                return;
            }

            // Moving, in shell. Check for stomps & damage.

            if (attackedFromAbove) {
                // Stomped.
                if (player.State == Enums.PowerupState.MiniMushroom) {
                    //mini mario interactions
                    if (player.IsGroundpounding) {
                        //mini mario is groundpounding, cancel their groundpound & stop moving
                        EnterShell(true, player);
                        player.IsGroundpounding = false;
                    }
                    player.DoEntityBounce = true;
                } else {
                    //normal mario interactions
                    if (player.IsGroundpounding) {
                        //normal mario is groundpounding, we get kick'd
                        Kick(player, !fromRight, Mathf.Abs(player.body.velocity.x) / player.RunningMaxSpeed, player.IsGroundpounding);
                    } else {
                        //normal mario isnt groundpounding, we get stopped
                        EnterShell(true, player);
                        player.DoEntityBounce = true;
                        FacingRight = damageDirection.x > 0;
                    }
                }
                return;
            }

            // Not being stomped on. just do damage.
            if (player.IsDamageable) {
                player.Powerdown(false);
                FacingRight = damageDirection.x > 0;
            }
        } else {
            // Not in shell, we can't be stomped on. Always damage.
            if (player.IsDamageable) {
                player.Powerdown(false);
                FacingRight = damageDirection.x > 0;
                return;
            }
        }
    }

    public override void OnIsActiveChanged() {
        base.OnIsActiveChanged();

        if (IsActive)
            animator.Play("walk");
    }
}
