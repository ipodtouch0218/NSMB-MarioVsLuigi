using UnityEngine;

using NSMB.Utils;

[CreateAssetMenu(fileName = "CoinTile", menuName = "ScriptableObjects/Tiles/CoinTile", order = 1)]
public class CoinTile : BreakableBrickTile {
    public string resultTile;
    public override bool Interact(BasicEntity interacter, InteractionDirection direction, Vector3 worldLocation) {
        if (base.Interact(interacter, direction, worldLocation))
            return true;

        Vector3Int tileLocation = Utils.WorldToTilemapPosition(worldLocation);

        PlayerController player = null;
        if (interacter is PlayerController controller)
            player = controller;
        if (interacter is KoopaWalk koopa)
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

        GameManager.Instance.CreateBlockBump(tileLocation.x, tileLocation.y, direction == InteractionDirection.Down, resultTile, null, true);
        return false;
    }
}
