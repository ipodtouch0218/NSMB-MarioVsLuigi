using UnityEngine;
using Photon.Pun;

//This is pretty much just the koopawalk script but it causes damage when you stand on it.
public class SpinyWalk : KoopaWalk {

    public override void InteractWithPlayer(PlayerController player) {
        Vector2 damageDirection = (player.body.position - body.position).normalized;
        bool attackedFromAbove = Vector2.Dot(damageDirection, Vector2.up) > 0f;
        if (holder)
            return;

        if (!attackedFromAbove && player.state == Enums.PowerupState.BlueShell && player.crouching && !player.inShell) {
            photonView.RPC("SetLeft", RpcTarget.All, damageDirection.x > 0);
        } else if (player.sliding || player.inShell || player.invincible > 0 || player.state == Enums.PowerupState.MegaMushroom) {
            //Special kill
            bool originalFacing = player.facingRight;
            if (player.inShell && shell && !stationary && Mathf.Sign(body.velocity.x) != Mathf.Sign(player.body.velocity.x))
                //Do knockback to player, colliding with us in shell going opposite ways
                player.photonView.RPC("Knockback", RpcTarget.All, player.body.position.x < body.position.x, 0, photonView.ViewID);

            photonView.RPC("SpecialKill", RpcTarget.All, !originalFacing, false, player.StarCombo++);
        } else if (!holder) {
            if (shell) {
                if (IsStationary) {
                    //we aren't moving. check for kicks & pickups
                    if (player.CanPickup()) {
                        //pickup-able
                        photonView.RPC("Pickup", RpcTarget.All, player.photonView.ViewID);
                        player.photonView.RPC("SetHolding", RpcTarget.All, photonView.ViewID);
                    } else {
                        //non-pickup able, kick.
                        photonView.RPC("Kick", RpcTarget.All, player.body.position.x < body.position.x, Mathf.Abs(player.body.velocity.x) / player.RunningMaxSpeed, player.groundpound);
                        player.photonView.RPC("SetHoldingOld", RpcTarget.All, photonView.ViewID);
                        previousHolder = player;
                    }
                } else {
                    //in shell, moving.
                    if (attackedFromAbove) {
                        //being stomped on
                        if (player.state == Enums.PowerupState.MiniMushroom) {
                            //mini mario interactions
                            if (player.groundpound) {
                                //mini mario is groundpounding, cancel their groundpound & stop moving
                                photonView.RPC("EnterShell", RpcTarget.All);
                                player.groundpound = false;
                            } else {
                                //mini mario not groundpounding, just bounce.
                                photonView.RPC("PlaySound", RpcTarget.All, Enums.Sounds.Enemy_Generic_Stomp);
                            }
                            player.bounce = true;
                        } else {
                            //normal mario interactions
                            if (player.groundpound) {
                                //normal mario is groundpounding, we get kick'd
                                photonView.RPC("Kick", RpcTarget.All, player.body.position.x < body.position.x, Mathf.Abs(player.body.velocity.x) / player.RunningMaxSpeed, true);
                                player.photonView.RPC("SetHoldingOld", RpcTarget.All, photonView.ViewID);
                            } else {
                                //normal mario isnt groundpounding, we get stopped
                                photonView.RPC("EnterShell", RpcTarget.All);
                                photonView.RPC("PlaySound", RpcTarget.All, Enums.Sounds.Enemy_Generic_Stomp);
                                player.bounce = true;
                                photonView.RPC("SetLeft", RpcTarget.All, damageDirection.x < 0);
                            }
                        }
                    } else if (player.hitInvincibilityCounter <= 0) {
                        //not being stomped on. just do damage.
                        player.photonView.RPC("Powerdown", RpcTarget.All, false);
                    }
                }
            } else if (player.hitInvincibilityCounter <= 0) {
                //Not in shell, we can't be stomped on.
                player.photonView.RPC("Powerdown", RpcTarget.All, false);
                photonView.RPC("SetLeft", RpcTarget.All, damageDirection.x < 0);
            }
        }
    }
}