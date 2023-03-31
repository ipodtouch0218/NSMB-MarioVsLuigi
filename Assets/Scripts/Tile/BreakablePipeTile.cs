using UnityEngine;
using UnityEngine.Tilemaps;

using NSMB.Utils;

[CreateAssetMenu(fileName = "BreakablePipeTile", menuName = "ScriptableObjects/Tiles/BreakablePipeTile", order = 4)]
public class BreakablePipeTile : InteractableTile {

    [SerializeField] private string leftDestroy, rightDestroy;
    [SerializeField] public bool upsideDownPipe, leftOfPipe;

    [SerializeField] GameObject pipeParticle, destroyedPipeParticle;

    public override bool Interact(BasicEntity interacter, InteractionDirection direction, Vector3 worldLocation) {
        if (!(interacter is PlayerController))
            return false;

        PlayerController player = (PlayerController) interacter;
        if (player.State != Enums.PowerupState.MegaMushroom)
            return false;

        if ((upsideDownPipe && direction == InteractionDirection.Down) || (!upsideDownPipe && direction == InteractionDirection.Up))
            //we've hit the underside of the pipe
            return false;


        TileManager tilemap = GameManager.Instance.tileManager;
        Vector2Int ourLocation = Utils.WorldToTilemapPosition(worldLocation);

        if (leftOfPipe && direction == InteractionDirection.Left) {
            if (Utils.GetTileAtTileLocation(ourLocation + Vector2Int.right) is InteractableTile otherPipe) {
                return otherPipe.Interact(interacter, direction, worldLocation + (Vector3.right * 0.5f));
            }
        }
        if (!leftOfPipe && direction == InteractionDirection.Right) {
            if (Utils.GetTileAtTileLocation(ourLocation + Vector2Int.left) is InteractableTile otherPipe) {
                return otherPipe.Interact(interacter, direction, worldLocation + (Vector3.left* 0.5f));
            }
        }

        int height = GetPipeHeight(ourLocation);
        Vector2Int origin = GetPipeOrigin(ourLocation);
        Vector2Int pipeDirection = upsideDownPipe ? Vector2Int.up : Vector2Int.down;
        Vector2Int hat = origin - (pipeDirection * (height - 1));

        if (ourLocation.y == GameManager.Instance.levelMinTileY + 1)
            //exception: dont break out of bounds.
            return false;

        bool bottom = false;

        if (origin.y < (GameManager.Instance.cameraMinY - 9f) || (origin.y + height) >= GameManager.Instance.levelMinTileY + GameManager.Instance.levelHeightTile)
            bottom = true;

        int tileHeight;
        bool shrink = false;
        bool addHat = true;

        if (direction == InteractionDirection.Down || direction == InteractionDirection.Up) {
            //hit top/bottom of pipe.
            if (hat == origin || height <= 1)
                return false;

            //shrink the pipe by 1. simple as moving the hat tiles up/down one
            tileHeight = 2;
            shrink = true;
        } else {
            //hit left/right side of pipe

            Vector2 world = worldLocation;
            bool alreadyDestroyed = tilemap.GetTile(hat).name.EndsWith("D");

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


            GameManager.Instance.SpawnResizableParticle(world + (leftOfPipe ? Vector2.zero : Vector2.left * 0.5f), leftOfPipe, upsideDownPipe, new Vector2(2, tileHeight - (addHat ? 1 : 0)), alreadyDestroyed ? destroyedPipeParticle : pipeParticle);
        }
        string[] tiles = new string[tileHeight*2];

        int start = upsideDownPipe ? (tileHeight*2)-2 : 0;
        if (addHat) {
            if (leftOfPipe) {
                //we're the left side. modify the right side too.
                //if (shrink) {
                //    tiles[start] = "SpecialPipes/" + tilemap.GetTile(hat).name;
                //    tiles[start + 1] = "SpecialPipes/" + tilemap.GetTile(hat + Vector3Int.right).name;
                //} else {
                //    tiles[start] = "SpecialPipes/" + leftDestroy;
                //    tiles[start + 1] = "SpecialPipes/" + rightDestroy;
                //}
            } else {
                //we're the right side. modify the left side too.
                //if (shrink) {
                //    tiles[start] = "SpecialPipes/" + tilemap.GetTile(hat + Vector3Int.left).name;
                //    tiles[start + 1] = "SpecialPipes/" + tilemap.GetTile(hat).name;
                //} else {
                //    tiles[start] = "SpecialPipes/" + leftDestroy;
                //    tiles[start + 1] = "SpecialPipes/" + rightDestroy;
                //}
            }
        }

        for (int i = 0; i < tiles.Length; i++)
            //photon doesn't like serializing nulls
            if (tiles[i] == null)
                tiles[i] = "";

        Vector2Int offset = upsideDownPipe ? Vector2Int.zero : pipeDirection * (tileHeight-1);
        GameManager.Instance.BulkModifyTilemap(hat + offset + (leftOfPipe ? Vector2Int.zero : Vector2Int.left), new Vector2Int(2, tileHeight), tiles);

        player.PlaySound(Enums.Sounds.Powerup_MegaMushroom_Break_Pipe);
        return true;
    }

    private Vector2Int GetPipeOrigin(Vector2Int ourLocation) {
        TileManager tm = GameManager.Instance.tileManager;
        Vector2Int searchDirection = upsideDownPipe ? Vector2Int.up : Vector2Int.down;
        Vector2Int searchVector = upsideDownPipe ? Vector2Int.up : Vector2Int.down;

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
}
