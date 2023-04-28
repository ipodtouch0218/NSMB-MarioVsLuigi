using UnityEngine;

//This is pretty much just the koopawalk script but it causes damage when you stand on it.
public class SpinyWalk : KoopaWalk {

    //---IPlayerInteractable overrides
    public override void InteractWithPlayer(PlayerController player) {

        //TODO: refactor. heavily.

        if (Holder)
            return;

        //temporary invincibility, we dont want to spam the kick sound
        if (PreviousHolder == player && !ThrowInvincibility.ExpiredOrNotRunning(Runner))
            return;

        Vector2 damageDirection = (player.body.position - body.position).normalized;
        bool attackedFromAbove = Vector2.Dot(damageDirection, Vector2.up) > 0f;

        if (!attackedFromAbove && player.State == Enums.PowerupState.BlueShell && player.IsCrouching && !player.IsInShell) {
            FacingRight = damageDirection.x < 0;
        } else if (player.InstakillsEnemies) {
            //Special kill
            bool originalFacing = player.FacingRight;
            if (player.IsInShell && IsInShell && !IsStationary && Mathf.Sign(body.velocity.x) != Mathf.Sign(player.body.velocity.x))
                //Do knockback to player, colliding with us in shell going opposite ways
                player.DoKnockback(player.body.position.x < body.position.x, 0, false, Object);

            SpecialKill(!originalFacing, false, player.StarCombo++);
        } else if (!Holder) {
            if (IsInShell) {
                if (IsActuallyStationary) {
                    //we aren't moving. check for kicks & pickups
                    if (player.CanPickupItem) {
                        //pickup-able
                        Pickup(player);
                    } else {
                        //non-pickup able, kick.
                        Kick(player, player.body.position.x < body.position.x, Mathf.Abs(player.body.velocity.x) / player.RunningMaxSpeed, player.IsGroundpounding);
                    }
                } else {
                    //in shell, moving.
                    if (attackedFromAbove) {
                        //being stomped on
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
                                Kick(player, player.body.position.x < body.position.x, Mathf.Abs(player.body.velocity.x) / player.RunningMaxSpeed, player.IsGroundpounding);
                            } else {
                                //normal mario isnt groundpounding, we get stopped
                                EnterShell(true, player);
                                player.DoEntityBounce = true;
                                FacingRight = damageDirection.x > 0;
                            }
                        }
                    } else if (player.IsDamageable) {
                        //not being stomped on. just do damage.
                        player.Powerdown(false);
                    }
                }
            } else if (player.IsDamageable) {
                //Not in shell, we can't be stomped on.
                player.Powerdown(false);
                FacingRight = damageDirection.x > 0;
            }
        }
    }

    public override void OnIsActiveChanged() {
        base.OnIsActiveChanged();

        if (IsActive)
            animator.Play("walk");
    }
}
