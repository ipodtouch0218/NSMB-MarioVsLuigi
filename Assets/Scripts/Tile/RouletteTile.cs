using UnityEngine;
using UnityEngine.Tilemaps;

using Fusion;
using NSMB.Entities;
using NSMB.Entities.Enemies;
using NSMB.Entities.Player;
using NSMB.Game;

namespace NSMB.Tiles {

    [CreateAssetMenu(fileName = "RouletteTile", menuName = "ScriptableObjects/Tiles/RouletteTile")]
    public class RouletteTile : BreakableBrickTile, IHaveTileDependencies {

        //---Serialized Variables
        [SerializeField] private TileBase resultTile;
        [SerializeField] private Vector2 topSpawnOffset;

        public override bool Interact(BasicEntity interacter, InteractionDirection direction, Vector3 worldLocation, out bool bumpSound) {
            if (base.Interact(interacter, direction, worldLocation, out bumpSound))
                return true;

            bumpSound = true;

            Vector2Int tileLocation = Utils.Utils.WorldToTilemapPosition(worldLocation);
            NetworkPrefabRef spawnResult = PrefabList.Instance.Powerup_Mushroom;

            if ((interacter is PlayerController) || (interacter is Koopa koopa && koopa.PreviousHolder != null)) {
                PlayerController player = interacter is PlayerController controller ? controller : ((Koopa) interacter).PreviousHolder;
                spawnResult = Utils.Utils.GetRandomItem(player).prefab;
            }

            Bump(interacter, direction, worldLocation);

            bool downwards = direction == InteractionDirection.Down;
            GameManager.Instance.BumpBlock((short) tileLocation.x, (short) tileLocation.y, this,
                resultTile, downwards, topSpawnOffset, false, spawnResult);

            return false;
        }

        public TileBase[] GetTileDependencies() {
            return new TileBase[] { resultTile };
        }
    }
}