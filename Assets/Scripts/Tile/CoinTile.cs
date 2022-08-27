using UnityEngine;

using Photon.Pun;
using NSMB.Utils;

[CreateAssetMenu(fileName = "CoinTile", menuName = "ScriptableObjects/Tiles/CoinTile", order = 1)]
public class CoinTile : BreakableBrickTile {
    public string resultTile;
    public override bool Interact(MonoBehaviour interacter, InteractionDirection direction, Vector3 worldLocation) {
        if (base.Interact(interacter, direction, worldLocation))
            return true;

        Vector3Int tileLocation = Utils.WorldToTilemapPosition(worldLocation);

        PlayerController player = null;
        if (interacter is PlayerController controller)
            player = controller;
        if (interacter is KoopaWalk koopa)
            player = koopa.previousHolder;

        if (player) {
            if (player.state == Enums.PowerupState.MegaMushroom) {
                //Break

                //Tilemap
                object[] parametersTile = new object[]{tileLocation.x, tileLocation.y, null};
                GameManager.Instance.SendAndExecuteEvent(Enums.NetEventIds.SetTile, parametersTile, ExitGames.Client.Photon.SendOptions.SendReliable);

                //Particle
                object[] parametersParticle = new object[]{tileLocation.x, tileLocation.y, "BrickBreak", new Vector3(particleColor.r, particleColor.g, particleColor.b)};
                GameManager.Instance.SendAndExecuteEvent(Enums.NetEventIds.SpawnParticle, parametersParticle, ExitGames.Client.Photon.SendOptions.SendUnreliable);

                player.photonView.RPC(nameof(PlayerController.PlaySound), RpcTarget.All, Enums.Sounds.World_Block_Break);
                return true;
            }

            //Give coin to player
            player.photonView.RPC(nameof(PlayerController.AttemptCollectCoin), RpcTarget.All, -1, (Vector2) worldLocation + Vector2.one/4f);
        } else {
            interacter.gameObject.GetPhotonView().RPC(nameof(HoldableEntity.PlaySound), RpcTarget.All, Enums.Sounds.World_Coin_Collect);
        }

        Bump(interacter, direction, worldLocation);

        object[] parametersBump = new object[]{tileLocation.x, tileLocation.y, direction == InteractionDirection.Down, resultTile, "Coin"};
        GameManager.Instance.SendAndExecuteEvent(Enums.NetEventIds.BumpTile, parametersBump, ExitGames.Client.Photon.SendOptions.SendReliable);
        return false;
    }
}
