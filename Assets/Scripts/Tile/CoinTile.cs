using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using Photon.Pun;

[CreateAssetMenu(fileName = "CoinTile", menuName = "ScriptableObjects/Tiles/CoinTile", order = 1)]
public class CoinTile : BreakableBrickTile {
    public string resultTile;
    public override bool Interact(MonoBehaviour interacter, InteractionDirection direction, Vector3 worldLocation) {
        if (base.Interact(interacter, direction, worldLocation))
            return true;

        Vector3Int tileLocation = Utils.WorldToTilemapPosition(worldLocation);

        if (interacter is PlayerController) {
            PlayerController player = (PlayerController) interacter;
            if (player.state == Enums.PowerupState.Giant) {
                //Break

                //Tilemap
                object[] parametersTile = new object[]{tileLocation.x, tileLocation.y, null};
                PhotonNetwork.RaiseEvent((byte) Enums.NetEventIds.SetTile, parametersTile, Utils.EVENT_OTHERS, ExitGames.Client.Photon.SendOptions.SendReliable);
                GameManager.Instance.tilemap.SetTile(new Vector3Int(tileLocation.x, tileLocation.y, 0), null);

                //Particle
                object[] parametersParticle = new object[]{tileLocation.x, tileLocation.y, "BrickBreak", new Vector3(particleColor.r, particleColor.g, particleColor.b)};
                PhotonNetwork.RaiseEvent((byte) Enums.NetEventIds.SpawnParticle, parametersParticle, Utils.EVENT_ALL, ExitGames.Client.Photon.SendOptions.SendUnreliable);
                
                if (interacter is MonoBehaviourPun) {
                    ((MonoBehaviourPun) interacter).photonView.RPC("PlaySound", RpcTarget.All, "player/brick_break");
                }
                return true;
            }

            //Give coin to player
            player.photonView.RPC("CollectCoin", RpcTarget.All, -1, worldLocation.x+0.25f, worldLocation.y+0.25f);
        } else {
            interacter.gameObject.GetPhotonView().RPC("PlaySound", RpcTarget.All, "player/coin");
        }

        Bump(interacter, direction, worldLocation);

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
