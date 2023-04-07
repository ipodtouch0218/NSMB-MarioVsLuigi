using UnityEngine;

using Fusion;
using NSMB.Tiles;

public abstract class Coin : CollectableEntity {

    //---Serialized Variables
    [SerializeField] private SpriteRenderer sRenderer;

    public static void GivePlayerCoin(PlayerController player, Vector3 position) {
        byte newCoins = (byte) (player.Coins + 1);
        bool item = newCoins >= SessionData.Instance.CoinRequirement;

        if (player.Object.HasStateAuthority)
            player.Rpc_SpawnCoinEffects(position, newCoins, item);

        if (item) {
            player.SpawnItem(NetworkPrefabRef.Empty);
            newCoins = 0;
        }

        player.Coins = newCoins;
    }

    //---IPlayerInteractable overrides
    public override void InteractWithPlayer(PlayerController player) {
        if (Collector)
            return;

        Collector = player;
        GivePlayerCoin(player, transform.position);
    }

    //---IBlockBumpable overrides
    public override void BlockBump(BasicEntity bumper, Vector2Int tile, InteractableTile.InteractionDirection direction) {
        if (direction == InteractableTile.InteractionDirection.Down)
            return;

        PlayerController target = null;
        if (bumper is PlayerController player)
            target = player;
        else if (bumper is KoopaWalk koopa)
            target = koopa.PreviousHolder;

        if (!target)
            return;

        InteractWithPlayer(target);
    }

    //CollectableEntity overrides
    public override void OnCollectedChanged() {
        sRenderer.enabled = !Collector;
    }
}
