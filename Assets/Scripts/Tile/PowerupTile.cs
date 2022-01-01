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

            if (player.state == Enums.PowerupState.Small || player.state == Enums.PowerupState.Mini) {
                spawnResult = BlockBump.SpawnResult.Mushroom;
            } else {
                spawnResult = BlockBump.SpawnResult.FireFlower;
            }
        }

        object[] parametersBump = new object[]{tileLocation.x, tileLocation.y, direction == InteractionDirection.Down, resultTile, spawnResult};
        PhotonNetwork.RaiseEvent((byte) Enums.NetEventIds.BumpTile, parametersBump, Utils.EVENT_OTHERS, ExitGames.Client.Photon.SendOptions.SendReliable);
        
        Vector3Int loc = new Vector3Int(tileLocation.x, tileLocation.y,0);
        GameObject bump = (GameObject) GameObject.Instantiate(Resources.Load("Prefabs/Bump/BlockBump"), Utils.TilemapToWorldPosition(loc) + new Vector3(0.25f, 0.25f), Quaternion.identity);
        BlockBump bb = bump.GetComponentInChildren<BlockBump>();

        bb.fromAbove = (bool) parametersBump[2];
        bb.resultTile = (string) parametersBump[3];
        bb.sprite = GameManager.Instance.tilemap.GetSprite(loc);
        bb.spawn = (BlockBump.SpawnResult) parametersBump[4];

        GameManager.Instance.tilemap.SetTile(loc, null);

        if (interacter is MonoBehaviourPun) {
            // ((MonoBehaviourPun) interacter).photonView.RPC("PlaySound", RpcTarget.All, "player/brick_break");
            ((MonoBehaviourPun) interacter).photonView.RPC("PlaySound", RpcTarget.All, "player/item_block");
        }
        return false;
    }
}
