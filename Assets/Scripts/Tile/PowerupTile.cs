using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using Photon.Pun;

[CreateAssetMenu(fileName = "PowerupTile", menuName = "ScriptableObjects/Tiles/PowerupTile", order = 2)]
public class PowerupTile : BreakableBrickTile {
    public string resultTile;
    public override bool Interact(MonoBehaviour interacter, InteractionDirection direction, Vector3 worldLocation) {
        if (base.Interact(interacter, direction, worldLocation))
            return true;

        Vector3Int tileLocation = Utils.WorldToTilemapPosition(worldLocation);

        BlockBump.SpawnResult spawnResult = BlockBump.SpawnResult.Mushroom;

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

            if (player.state <= Enums.PowerupState.Small) {
                spawnResult = BlockBump.SpawnResult.Mushroom;
            } else {
                spawnResult = BlockBump.SpawnResult.FireFlower;
            }
        }
        
        Bump(interacter, direction, worldLocation);

        object[] parametersBump = new object[]{tileLocation.x, tileLocation.y, direction == InteractionDirection.Down, resultTile, spawnResult};
        GameManager.Instance.SendAndExecuteEvent(Enums.NetEventIds.BumpTile, parametersBump, ExitGames.Client.Photon.SendOptions.SendReliable);

        if (interacter is MonoBehaviourPun) {
            // ((MonoBehaviourPun) interacter).photonView.RPC("PlaySound", RpcTarget.All, "player/brick_break");
            ((MonoBehaviourPun) interacter).photonView.RPC("PlaySound", RpcTarget.All, "player/item_block");
        }
        return false;
    }
}
