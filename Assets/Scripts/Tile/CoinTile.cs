using UnityEngine;

using Fusion;
using NSMB.Utils;

[CreateAssetMenu(fileName = "CoinTile", menuName = "ScriptableObjects/Tiles/CoinTile", order = 1)]
public class CoinTile : BreakableBrickTile {

    [SerializeField] private string resultTile;

    public override bool Interact(BasicEntity interacter, InteractionDirection direction, Vector3 worldLocation) {
        if (base.Interact(interacter, direction, worldLocation))
            return true;

        Vector3Int tileLocation = Utils.WorldToTilemapPosition(worldLocation);

        PlayerController player = null;
        if (interacter is PlayerController controller)
            player = controller;
        else if (interacter is KoopaWalk koopa)
            player = koopa.PreviousHolder;

        if (player) {
            if (player.State == Enums.PowerupState.MegaMushroom) {
                //Break

                //Tilemap
                GameManager.Instance.tilemap.SetTile(tileLocation, null);

                //Particle
                //TODO:
                //object[] parametersParticle = new object[]{tileLocation.x, tileLocation.y, "BrickBreak", new Vector3(particleColor.r, particleColor.g, particleColor.b)};
                //GameManager.Instance.SendAndExecuteEvent(Enums.NetEventIds.SpawnParticle, parametersParticle, ExitGames.Client.Photon.SendOptions.SendUnreliable);

                player.PlaySound(Enums.Sounds.World_Block_Break);
                return true;
            }

            //Give coin to player
            Coin.GivePlayerCoin(player, worldLocation + (Vector3) (Vector2.one / 4f));
        } else {
            interacter.PlaySound(Enums.Sounds.World_Coin_Collect);
        }

        Bump(interacter, direction, worldLocation);

        if (GameManager.Instance.Object.HasStateAuthority) {
            GameManager.Instance.rpcs.Rpc_BumpBlock((short) tileLocation.x, (short) tileLocation.y, "",
                resultTile, direction == InteractionDirection.Down, Vector2.zero, true, NetworkPrefabRef.Empty);
        }

        return false;
    }
}
