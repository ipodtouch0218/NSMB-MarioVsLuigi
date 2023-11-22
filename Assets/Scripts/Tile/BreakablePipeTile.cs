using UnityEngine;
using UnityEngine.Tilemaps;

using NSMB.Entities;
using NSMB.Entities.Player;
using NSMB.Game;

namespace NSMB.Tiles {

    [CreateAssetMenu(fileName = "BreakablePipeTile", menuName = "ScriptableObjects/Tiles/BreakablePipeTile", order = 4)]
    public class BreakablePipeTile : InteractableTile, IHaveTileDependencies {

        //---Public Variables
        public bool upsideDownPipe, leftOfPipe;

        //---Serialized Variables
        [SerializeField] private TileBase leftBrokenHatTile, rightBrokenHatTile;
        [SerializeField] private Enums.PrefabParticle pipeParticle, destroyedPipeParticle;

        public override bool Interact(BasicEntity interacter, InteractionDirection direction, Vector3 worldLocation, out bool bumpSound) {
            bumpSound = true;
            if (interacter is not PlayerController player)
                return false;

            bumpSound = !player.IsGroundpounding && !player.IsDrilling;

            if (player.State != Enums.PowerupState.MegaMushroom)
                return false;

            // we've hit the underside of the pipe
            if ((upsideDownPipe && direction == InteractionDirection.Down) || (!upsideDownPipe && direction == InteractionDirection.Up))
                return false;

            TileManager tilemap = GameManager.Instance.tileManager;
            Vector2Int ourLocation = Utils.Utils.WorldToTilemapPosition(worldLocation);

            if (leftOfPipe && direction == InteractionDirection.Left) {
                if (Utils.Utils.GetTileAtTileLocation(ourLocation + Vector2Int.right) is InteractableTile otherPipe) {
                    return otherPipe.Interact(interacter, direction, worldLocation + (Vector3.right * 0.5f), out bumpSound);
                }
            }
            if (!leftOfPipe && direction == InteractionDirection.Right) {
                if (Utils.Utils.GetTileAtTileLocation(ourLocation + Vector2Int.left) is InteractableTile otherPipe) {
                    return otherPipe.Interact(interacter, direction, worldLocation + (Vector3.left * 0.5f), out bumpSound);
                }
            }

            int height = GetPipeHeight(ourLocation);
            Vector2Int origin = GetPipeOrigin(ourLocation);
            Vector2Int pipeDirection = upsideDownPipe ? Vector2Int.up : Vector2Int.down;
            Vector2Int hat = origin - (pipeDirection * (height - 1));

            if (ourLocation.y == GameManager.Instance.levelMinTileY + 1)
                // Exception: dont break out of bounds.
                return false;

            bool bottom = false;

            if (origin.y < (GameManager.Instance.cameraMinY - 9f) || (origin.y + height) >= GameManager.Instance.levelMinTileY + GameManager.Instance.levelHeightTile)
                bottom = true;

            int tileHeight;
            bool shrink = false;
            bool addHat = true;

            if (direction == InteractionDirection.Down || direction == InteractionDirection.Up) {
                // Hit top/bottom of pipe.
                if (hat == origin || height <= 1)
                    return false;

                // Shrink the pipe by 1. simple as moving the hat tiles up/down one
                tileHeight = 2;
                shrink = true;
            } else {
                // Hit left/right side of pipe

                Vector2 world = worldLocation;
                tilemap.GetTile(hat, out BreakablePipeTile hatTile);
                bool alreadyDestroyed = (hatTile == hatTile.leftBrokenHatTile || hatTile == hatTile.rightBrokenHatTile);

                if (upsideDownPipe) {
                    if (ourLocation == origin)
                        addHat = false;

                    tileHeight = Mathf.Abs(hat.y - ourLocation.y) + (addHat ? 2 : 1);

                    if (bottom && ourLocation == origin && (tileHeight != 1 || alreadyDestroyed))
                        return false;

                } else {
                    addHat = bottom;
                    tileHeight = GetPipeHeight(ourLocation);

                    world -= (Vector2) (ourLocation - origin) * 0.5f;

                    if (bottom)
                        world += Vector2.up * 0.5f;
                }

                if (tileHeight - (addHat ? 1 : 0) < 0)
                    return false;

                player.SpawnResizableParticle(world + (leftOfPipe ? Vector2.zero : Vector2.left * 0.5f), leftOfPipe, upsideDownPipe, new Vector2(2, tileHeight - (addHat ? 1 : 0)), alreadyDestroyed ? destroyedPipeParticle : pipeParticle);
            }
            TileBase[] tiles = new TileBase[tileHeight*2];

            int start = upsideDownPipe ? (tileHeight*2)-2 : 0;
            if (addHat) {
                if (shrink) {
                    if (leftOfPipe) {
                        // We're the left side. modify the right side too.
                        tiles[start] = tilemap.GetTile(hat);
                        tiles[start + 1] = tilemap.GetTile(hat + Vector2Int.right);
                    } else {
                        // We're the right side. modify the left side too.
                        tiles[start] = tilemap.GetTile(hat + Vector2Int.left);
                        tiles[start + 1] = tilemap.GetTile(hat);
                    }
                } else {
                    tiles[start] = leftBrokenHatTile;
                    tiles[start + 1] = rightBrokenHatTile;
                }
            }

            Vector2Int offset = upsideDownPipe ? Vector2Int.zero : pipeDirection * (tileHeight-1);
            Vector2Int finalPosition = hat + offset + (leftOfPipe ? Vector2Int.zero : Vector2Int.left);

            GameManager.Instance.tileManager.SetTilesBlock(finalPosition.x, finalPosition.y, 2, tileHeight, tiles);

            bumpSound = false;
            return true;
        }

        private Vector2Int GetPipeOrigin(Vector2Int ourLocation) {
            TileManager tm = GameManager.Instance.tileManager;
            Vector2Int searchDirection = upsideDownPipe ? Vector2Int.up : Vector2Int.down;
            Vector2Int searchVector = searchDirection;

            while (tm.GetTile(ourLocation + searchVector) is BreakablePipeTile)
                searchVector += searchDirection;

            return ourLocation + searchVector - searchDirection;
        }

        private int GetPipeHeight(Vector2Int ourLocation) {
            int height = 1;
            TileManager tm = GameManager.Instance.tileManager;
            Vector2Int searchVector = Vector2Int.up;
            while (tm.GetTile(ourLocation + searchVector) is BreakablePipeTile) {
                height++;
                searchVector += Vector2Int.up;
            }
            searchVector = Vector2Int.down;
            while (tm.GetTile(ourLocation + searchVector) is BreakablePipeTile) {
                height++;
                searchVector += Vector2Int.down;
            }
            return height;
        }

        public TileBase[] GetTileDependencies() {
            return new TileBase[] { leftBrokenHatTile, rightBrokenHatTile };
        }
    }
}
