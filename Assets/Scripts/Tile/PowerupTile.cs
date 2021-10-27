using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using Photon.Pun;

[CreateAssetMenu]
public class PowerupTile : InteractableTile {
    public string resultTile;
    public override bool Interact(MonoBehaviour interacter, InteractionDirection direction, Vector3 worldLocation) {
        Vector3Int tileLocation = Utils.WorldToTilemapPosition(worldLocation);
        
        Bump(interacter, direction, worldLocation);

        BlockBump.SpawnResult spawnResult = BlockBump.SpawnResult.Mushroom;

        if (interacter is PlayerController) {
            PlayerController player = (PlayerController) interacter;

            if (player.state == PlayerController.PlayerState.Small || player.state == PlayerController.PlayerState.Mini) {
                spawnResult = BlockBump.SpawnResult.Mushroom;
            } else {
                spawnResult = BlockBump.SpawnResult.FireFlower;
            }
        }

        GameManager.Instance.photonView.RPC("BumpBlock", RpcTarget.All, tileLocation.x, tileLocation.y, resultTile, (int) spawnResult, direction == InteractionDirection.Down);
        if (interacter is MonoBehaviourPun) {
            ((MonoBehaviourPun) interacter).photonView.RPC("PlaySound", RpcTarget.All, "player/brick_break");
            ((MonoBehaviourPun) interacter).photonView.RPC("PlaySound", RpcTarget.All, "player/item_block");
        }
        return false;
    }
}
