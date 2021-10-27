using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using Photon.Pun;

[CreateAssetMenu]
public class CoinTile : InteractableTile {
    public string resultTile;
    public override bool Interact(MonoBehaviour interacter, InteractionDirection direction, Vector3 worldLocation) {
        Vector3Int tileLocation = Utils.WorldToTilemapPosition(worldLocation);
        
        Bump(interacter, direction, worldLocation);

        if (interacter is PlayerController) {
            PlayerController player = (PlayerController) interacter;

            //Give coin to player
            player.photonView.RPC("CollectCoin", RpcTarget.All, -1, worldLocation.x+0.25f, worldLocation.y+0.25f);
        }

        GameManager.Instance.photonView.RPC("BumpBlock", RpcTarget.All, tileLocation.x, tileLocation.y, resultTile, (int) BlockBump.SpawnResult.Coin, direction == InteractionDirection.Down);
        return false;
    }
}
