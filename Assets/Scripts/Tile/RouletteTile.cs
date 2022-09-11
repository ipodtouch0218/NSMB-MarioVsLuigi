using UnityEngine;
using Photon.Pun;
using NSMB.Utils;

[CreateAssetMenu(fileName = "RouletteTile", menuName = "ScriptableObjects/Tiles/RouletteTile", order = 5)]
public class RouletteTile : BreakableBrickTile {

    public string resultTile;
    public Vector2 topSpawnOffset, bottomSpawnOffset;

    public override bool Interact(MonoBehaviour interacter, InteractionDirection direction, Vector3 worldLocation) {
        if (base.Interact(interacter, direction, worldLocation))
            return true;

        Vector3Int tileLocation = Utils.WorldToTilemapPosition(worldLocation);

        string spawnResult = "Mushroom";

        if ((interacter is PlayerController) || (interacter is KoopaWalk koopa && koopa.previousHolder != null)) {
            PlayerController player = interacter is PlayerController controller ? controller : ((KoopaWalk) interacter).previousHolder;
            if (player.state == Enums.PowerupState.MegaMushroom) {
                //Break

                //Tilemap
                object[] parametersTile = new object[] { tileLocation.x, tileLocation.y, null };
                GameManager.Instance.SendAndExecuteEvent(Enums.NetEventIds.SetTile, parametersTile, ExitGames.Client.Photon.SendOptions.SendReliable);

                //Particles
                for (int x = 0; x < 2; x++) {
                    for (int y = 0; y < 2; y++) {

                        object[] parametersParticle = new object[] { tileLocation.x + x, tileLocation.y - y, "BrickBreak", new Vector3(particleColor.r, particleColor.g, particleColor.b) };
                        GameManager.Instance.SendAndExecuteEvent(Enums.NetEventIds.SpawnParticle, parametersParticle, ExitGames.Client.Photon.SendOptions.SendUnreliable);

                    }
                }

                if (interacter is MonoBehaviourPun pun)
                    pun.photonView.RPC("PlaySound", RpcTarget.All, Enums.Sounds.World_Block_Break);
                return true;
            }

            spawnResult = Utils.GetRandomItem(player).prefab;
        }

        Bump(interacter, direction, worldLocation);

        Vector2 offset = direction == InteractionDirection.Down ? bottomSpawnOffset + ( spawnResult == "MegaMushroom" ? Vector2.down * 0.5f : Vector2.zero) : topSpawnOffset;
        object[] parametersBump = new object[] { tileLocation.x, tileLocation.y, direction == InteractionDirection.Down, resultTile, spawnResult, offset };
        GameManager.Instance.SendAndExecuteEvent(Enums.NetEventIds.BumpTile, parametersBump, ExitGames.Client.Photon.SendOptions.SendReliable);

        if (interacter is MonoBehaviourPun pun2)
            pun2.photonView.RPC("PlaySound", RpcTarget.All, Enums.Sounds.World_Block_Powerup);
        return false;
    }
}