using UnityEngine;

namespace NSMB.Tiles {

    [CreateAssetMenu(fileName = "BreakableBulletBillLauncher", menuName = "ScriptableObjects/Tiles/BreakableBulletBillLauncher")]
    public class BreakableBulletBillLauncher : InteractableTile {

        //---Serialized Variables
        [SerializeField] private GameObject breakParticle;

        public override bool Interact(BasicEntity interacter, InteractionDirection direction, Vector3 worldLocation, out bool bumpSound) {
            bumpSound = true;
            if (interacter is not PlayerController)
                return false;

            PlayerController player = (PlayerController) interacter;
            if (player.State != Enums.PowerupState.MegaMushroom)
                return false;
            if (direction == InteractionDirection.Down || direction == InteractionDirection.Up)
                return false;

            Vector2Int ourLocation = Utils.Utils.WorldToTilemapPosition(worldLocation);
            int height = GetLauncherHeight(ourLocation);
            Vector2Int origin = GetLauncherOrigin(ourLocation);

            ushort[] emptyTiles = new ushort[height];

            GameManager.Instance.SpawnResizableParticle((Vector2) worldLocation, direction == InteractionDirection.Right, false, new Vector2(1, height), breakParticle);
            GameManager.Instance.tileManager.SetTilesBlock(origin.x, origin.y, 1, height, emptyTiles);
            bumpSound = false;
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
