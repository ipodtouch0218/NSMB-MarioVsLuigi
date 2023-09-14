using UnityEngine;

using Fusion;
using NSMB.Entities.Enemies;
using NSMB.Entities.Player;
using NSMB.Tiles;

namespace NSMB.Entities.Collectable {
    public abstract class Coin : CollectableEntity {

        //---Components
        [SerializeField] protected SpriteRenderer sRenderer;

        public override void OnValidate() {
            if (!sRenderer) sRenderer = GetComponentInChildren<SpriteRenderer>();
        }

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
        public override void InteractWithPlayer(PlayerController player, PhysicsDataStruct.IContactStruct contact = null) {
            if (Collector)
                return;

            Collector = player;
            GivePlayerCoin(player, transform.position);
        }

        //---IBlockBumpable overrides
        public override void BlockBump(BasicEntity bumper, Vector2Int tile, InteractionDirection direction) {
            if (direction == InteractionDirection.Down)
                return;

            PlayerController target = null;
            if (bumper is PlayerController player)
                target = player;
            else if (bumper is Koopa koopa)
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
}
