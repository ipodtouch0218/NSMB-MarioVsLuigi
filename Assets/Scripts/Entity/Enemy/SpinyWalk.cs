using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using UnityEngine.Tilemaps;

//This is pretty much just the koopawalk script but it causes damage when you stand on it.
public class SpinyWalk : KoopaWalk {

    public override void InteractWithPlayer(PlayerController player) {
        Vector2 damageDirection = (player.body.position - body.position).normalized;
        bool attackedFromAbove = Vector2.Dot(damageDirection, Vector2.up) > 0f;
        if (holder) 
            return;

        if (player.sliding || player.inShell || player.invincible > 0 || player.state == Enums.PowerupState.Giant) {
            //Special kill
            bool originalFacing = player.facingRight;
            if (player.inShell && shell && !stationary && Mathf.Sign(body.velocity.x) != Mathf.Sign(player.body.velocity.x))
                //Do knockback to player, colliding with us in shell going opposite ways
                player.photonView.RPC("Knockback", RpcTarget.All, player.body.position.x < body.position.x, 0, photonView.ViewID);

            photonView.RPC("SpecialKill", RpcTarget.All, !originalFacing, false);
        } else if (!holder) {
            if (shell) {
                if (IsStationary()) {
                    //we aren't moving. check for kicks & pickups
                    if (player.CanPickup()) {
                        //pickup-able
                        photonView.RPC("Pickup", RpcTarget.All, player.photonView.ViewID);
                        player.photonView.RPC("SetHolding", RpcTarget.All, photonView.ViewID);
                    } else {
                        //non-pickup able, kick.
                        photonView.RPC("Kick", RpcTarget.All, player.body.position.x < body.position.x, player.groundpound);
                        player.photonView.RPC("SetHoldingOld", RpcTarget.All, photonView.ViewID);
                        previousHolder = player;
                    }
                } else {
                    //in shell, moving.
                    if (attackedFromAbove) {
                        //being stomped on
                        if (player.state == Enums.PowerupState.Mini) {
                            //mini mario interactions
                            if (player.groundpound) {
                                //mini mario is groundpounding, cancel their groundpound & stop moving
                                photonView.RPC("EnterShell", RpcTarget.All);
                                player.groundpound = false;
                            } else {
                                //mini mario not groundpounding, just bounce.
                                photonView.RPC("PlaySound", RpcTarget.All, "enemy/goomba");
                            }
                            player.bounce = true;
                        } else {
                            //normal mario interactions
                            if (player.groundpound) {
                                //normal mario is groundpounding, we get kick'd
                                photonView.RPC("Kick", RpcTarget.All, player.body.position.x < body.position.x, true);
                                player.photonView.RPC("SetHoldingOld", RpcTarget.All, photonView.ViewID);
                            } else {
                                //normal mario isnt groundpounding, we get stopped
                                photonView.RPC("EnterShell", RpcTarget.All);
                                photonView.RPC("PlaySound", RpcTarget.All, "enemy/goomba");
                            }
                        }
                    } else {
                        //not being stomped on. just do damage.
                        player.photonView.RPC("Powerdown", RpcTarget.All, false);
                    }
                }
            } else {
                //Not in shell, we can't be stomped on.
                player.photonView.RPC("Powerdown", RpcTarget.All, false);
            }
        }
    }
}