using UnityEngine;
using UnityEngine.Tilemaps;

using Fusion;
using NSMB.Entities;
using NSMB.Entities.Enemies;
using NSMB.Entities.Player;
using NSMB.Game;

namespace NSMB.Tiles {

    [CreateAssetMenu(fileName = "PowerupTile", menuName = "ScriptableObjects/Tiles/PowerupTile")]
    public class PowerupTile : BreakableBrickTile, IHaveTileDependencies {

        //---Static Variables
        private static readonly Vector2 SpawnOffset = new(0, -0.25f);

        //---Serialized Variables
        [SerializeField] private TileBase resultTile;

        public override bool Interact(BasicEntity interacter, InteractionDirection direction, Vector3 worldLocation, out bool bumpSound) {
            if (base.Interact(interacter, direction, worldLocation, out bumpSound)) {
                return true;
            }

            bumpSound = true;

            Vector2Int tileLocation = Utils.Utils.WorldToTilemapPosition(worldLocation);

            NetworkPrefabRef spawnResult = PrefabList.Instance.Powerup_Mushroom;

            if ((interacter is PlayerController) || (interacter is Koopa koopa && koopa.PreviousHolder != null)) {
                PlayerController player = interacter is PlayerController controller ? controller : ((Koopa) interacter).PreviousHolder;

                if (player.State >= Enums.PowerupState.Mushroom) {
                    spawnResult = PrefabList.Instance.Powerup_FireFlower;
                }
            }

            Bump(interacter, direction, worldLocation);
            bool downwards = direction == InteractionDirection.Down;

            GameManager.Instance.BumpBlock((short) tileLocation.x, (short) tileLocation.y, this,
                resultTile, downwards, SpawnOffset, false, spawnResult);
            return false;
        }

        public TileBase[] GetTileDependencies() {
            return new TileBase[] { resultTile };
        }
    }
}
