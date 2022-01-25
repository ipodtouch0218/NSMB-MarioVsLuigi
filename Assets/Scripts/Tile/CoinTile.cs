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
                GameManager.Instance.SendAndExecuteEvent(Enums.NetEventIds.SetTile, parametersTile, ExitGames.Client.Photon.SendOptions.SendReliable);

                //Particle
                object[] parametersParticle = new object[]{tileLocation.x, tileLocation.y, "BrickBreak", new Vector3(particleColor.r, particleColor.g, particleColor.b)};
                GameManager.Instance.SendAndExecuteEvent(Enums.NetEventIds.SpawnParticle, parametersParticle, ExitGames.Client.Photon.SendOptions.SendUnreliable);
                
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
        GameManager.Instance.SendAndExecuteEvent(Enums.NetEventIds.BumpTile, parametersBump, ExitGames.Client.Photon.SendOptions.SendReliable);
        return false;
    }
}
