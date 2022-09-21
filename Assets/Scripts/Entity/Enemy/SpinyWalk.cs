using UnityEngine;

//This is pretty much just the koopawalk script but it causes damage when you stand on it.
public class SpinyWalk : KoopaWalk {

    public override void InteractWithPlayer(PlayerController player) {
        Vector2 damageDirection = (player.body.position - body.position).normalized;
        bool attackedFromAbove = Vector2.Dot(damageDirection, Vector2.up) > 0f;
        if (Holder)
            return;

        if (!attackedFromAbove && player.State == Enums.PowerupState.BlueShell && player.crouching && !player.inShell) {
            FacingRight = damageDirection.x < 0;
        } else if (player.sliding || player.inShell || player.IsStarmanInvincible || player.State == Enums.PowerupState.MegaMushroom) {
            //Special kill
            bool originalFacing = player.FacingRight;
            if (player.inShell && IsInShell && !IsStationary && Mathf.Sign(body.velocity.x) != Mathf.Sign(player.body.velocity.x))
                //Do knockback to player, colliding with us in shell going opposite ways
                player.DoKnockback(player.body.position.x < body.position.x, 0, false, 0);

            SpecialKill(!originalFacing, false, player.StarCombo++);
        } else if (!Holder) {
            if (IsInShell) {
                if (IsActuallyStationary) {
                    //we aren't moving. check for kicks & pickups
                    if (player.CanPickup()) {
                        //pickup-able
                        Pickup(player);
                    } else {
                        //non-pickup able, kick.
                        Kick(player.body.position.x < body.position.x, Mathf.Abs(player.body.velocity.x) / player.RunningMaxSpeed, player.groundpound);
                        PreviousHolder = player;
                    }
                } else {
                    //in shell, moving.
                    if (attackedFromAbove) {
                        //being stomped on
                        if (player.State == Enums.PowerupState.MiniMushroom) {
                            //mini mario interactions
                            if (player.groundpound) {
                                //mini mario is groundpounding, cancel their groundpound & stop moving
                                EnterShell(true);
                                player.groundpound = false;
                            } else {
                                //mini mario not groundpounding, just bounce.
                                PlaySound(Enums.Sounds.Enemy_Generic_Stomp);
                            }
                            player.bounce = true;
                        } else {
                            //normal mario interactions
                            if (player.groundpound) {
                                //normal mario is groundpounding, we get kick'd
                                Kick(player.body.position.x < body.position.x, Mathf.Abs(player.body.velocity.x) / player.RunningMaxSpeed, player.groundpound);
                                PreviousHolder = player;
                            } else {
                                //normal mario isnt groundpounding, we get stopped
                                EnterShell(true);
                                PlaySound(Enums.Sounds.Enemy_Generic_Stomp);
                                player.bounce = true;
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
}