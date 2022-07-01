using UnityEngine;
using Photon.Pun;

public class KillEntity : KillableEntity {
    [PunRPC]
    public override void InteractWithPlayer(PlayerController player) {
        if (player.state == Enums.PowerupState.MegaMushroom || player.invincible > 0 || player.inShell) {
            return;
        } else {
            player.photonView.RPC("Powerdown", RpcTarget.All, false);
        }
    }
    [PunRPC]
    public override void Kill() {
        
    }
}