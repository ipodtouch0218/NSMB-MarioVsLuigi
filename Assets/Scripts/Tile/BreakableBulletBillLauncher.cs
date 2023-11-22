using UnityEngine;

using NSMB.Entities;
using NSMB.Entities.Player;
using NSMB.Game;

namespace NSMB.Tiles {

    [CreateAssetMenu(fileName = "BreakableBulletBillLauncher", menuName = "ScriptableObjects/Tiles/BreakableBulletBillLauncher")]
    public class BreakableBulletBillLauncher : InteractableTile {

        //---Serialized Variables
        [SerializeField] private Enums.PrefabParticle breakParticle;

        public override bool Interact(BasicEntity interacter, InteractionDirection direction, Vector3 worldLocation, out bool bumpSound) {
            if (interacter is not PlayerController) {
                bumpSound = true;
                return false;
            }
            bumpSound = false;

            PlayerController player = (PlayerController) interacter;

            if (player.IsInShell && (direction == InteractionDirection.Left || direction == InteractionDirection.Right))
                bumpSound = true;

            if (player.State != Enums.PowerupState.MegaMushroom || direction == InteractionDirection.Down || direction == InteractionDirection.Up)
                return false;

            Vector2Int ourLocation = Utils.Utils.WorldToTilemapPosition(worldLocation);
            int height = GetLauncherHeight(ourLocation);
            Vector2Int origin = GetLauncherOrigin(ourLocation);

            ushort[] emptyTiles = new ushort[height];

            interacter.SpawnResizableParticle(worldLocation, direction == InteractionDirection.Right, false, new Vector2(1, height), breakParticle);

            GameManager.Instance.tileManager.SetTilesBlock(origin.x, origin.y, 1, height, emptyTiles);
            return true;
        }

        private Vector2Int GetLauncherOrigin(Vector2Int ourLocation) {
            TileManager tm = GameManager.Instance.tileManager;
            Vector2Int searchDirection = Vector2Int.down;
            Vector2Int searchVector = Vector2Int.down;

            while (tm.GetTile(ourLocation + searchVector) is BreakableBulletBillLauncher)
                searchVector += searchDirection;

            return ourLocation + searchVector - searchDirection;
        }

        private int GetLauncherHeight(Vector2Int ourLocation) {
            int height = 1;
            TileManager tm = GameManager.Instance.tileManager;
            Vector2Int searchVector = Vector2Int.up;
            while (tm.GetTile(ourLocation + searchVector) is BreakableBulletBillLauncher) {
                height++;
                searchVector += Vector2Int.up;
            }

            searchVector = Vector2Int.down;
            while (tm.GetTile(ourLocation + searchVector) is BreakableBulletBillLauncher) {
                height++;
                searchVector += Vector2Int.down;
            }
            return height;
        }
    }
}
