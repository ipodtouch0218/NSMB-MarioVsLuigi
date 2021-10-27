using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using UnityEngine.Tilemaps;

[CreateAssetMenu]
public class BreakableBrickTile : InteractableTile {
    public Color particleColor;
    public override bool Interact(MonoBehaviour interacter, InteractionDirection direction, Vector3 worldLocation) {
        Vector3Int tileLocation = Utils.WorldToTilemapPosition(worldLocation);

        Bump(interacter, direction, worldLocation);
        
        if (interacter is PlayerController) {
            PlayerController player = (PlayerController) interacter;

            if (player.state == PlayerController.PlayerState.Small 
                || player.state == PlayerController.PlayerState.Mini) {
                //Bump

                GameManager.Instance.photonView.RPC("BumpBlock", RpcTarget.All, tileLocation.x, tileLocation.y, "SpecialTiles/" + this.name, (int) BlockBump.SpawnResult.Nothing, direction == InteractionDirection.Down);
                return false;
            }
        }

        //Break
        GameManager.Instance.photonView.RPC("ModifyTilemap", RpcTarget.All, tileLocation.x, tileLocation.y, null);
        GameManager.Instance.photonView.RPC("SpawnBreakParticle", RpcTarget.All, tileLocation.x, tileLocation.y, particleColor.r, particleColor.g, particleColor.b);
        if (interacter is MonoBehaviourPun) {
            ((MonoBehaviourPun) interacter).photonView.RPC("PlaySound", RpcTarget.All, "player/brick_break");
        }
        return true;
    }
}
