using UnityEngine;

using Fusion;
using NSMB.Utils;

public abstract class Coin : CollectableEntity {

    public override void InteractWithPlayer(PlayerController player) {
        if (IsCollected)
            return;

        IsCollected = true;
        GivePlayerCoin(player, transform.position);
    }

    public static void GivePlayerCoin(PlayerController player, Vector3 position) {
        byte newCoins = (byte) (player.Coins + 1);

        player.Rpc_SpawnCoinEffects(position, newCoins);

        if (newCoins >= GameManager.Instance.coinRequirement) {
            player.SpawnItem(NetworkPrefabRef.Empty);
            newCoins = 0;
        }
        player.Coins = newCoins;
    }

    public override void Bump(BasicEntity bumper, Vector3Int tile, InteractableTile.InteractionDirection direction) {
        if (direction == InteractableTile.InteractionDirection.Down)
            return;

        PlayerController target = null;
        if (bumper is PlayerController player)
            target = player;
        else if (bumper is KoopaWalk koopa)
            target = koopa.PreviousHolder;

        if (!target)
            return;

        GivePlayerCoin(null, Vector3.zero);
    }
}
