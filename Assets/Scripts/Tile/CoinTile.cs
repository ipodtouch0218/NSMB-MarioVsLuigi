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
        } else {
            interacter.gameObject.GetPhotonView().RPC("PlaySound", RpcTarget.All, "player/coin");
        }

        object[] parametersBump = new object[]{tileLocation.x, tileLocation.y, direction == InteractionDirection.Down, resultTile, BlockBump.SpawnResult.Coin};
        PhotonNetwork.RaiseEvent((byte) Enums.NetEventIds.BumpTile, parametersBump, Utils.EVENT_OTHERS, ExitGames.Client.Photon.SendOptions.SendReliable);
                
        //Bump for ourself
        Vector3Int loc = new Vector3Int(tileLocation.x, tileLocation.y,0);
        GameObject bump = (GameObject) GameObject.Instantiate(Resources.Load("Prefabs/Bump/BlockBump"), Utils.TilemapToWorldPosition(loc) + new Vector3(0.25f, 0.25f), Quaternion.identity);
        BlockBump bb = bump.GetComponentInChildren<BlockBump>();

        bb.fromAbove = (bool) parametersBump[2];
        bb.resultTile = (string) parametersBump[3];
        bb.sprite = GameManager.Instance.tilemap.GetSprite(loc);
        bb.spawn = (BlockBump.SpawnResult) parametersBump[4];

        GameManager.Instance.tilemap.SetTile(loc, null);
        return false;
    }
}
