using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

[CreateAssetMenu(fileName = "BreakablePipeTile", menuName = "ScriptableObjects/Tiles/BreakablePipeTile", order = 4)]
public class BreakablePipeTile : InteractableTile {
    public string leftDestroy, rightDestroy;
    public bool upsideDownPipe, leftOfPipe;
    public override bool Interact(MonoBehaviour interacter, InteractionDirection direction, Vector3 worldLocation) {
        if (!(interacter is PlayerController))
            return false;

        PlayerController player = (PlayerController) interacter;
        if (player.state != Enums.PowerupState.MegaMushroom)
            return false;
        if ((leftOfPipe && direction == InteractionDirection.Left) || (!leftOfPipe && direction == InteractionDirection.Right))
            //we've hit the inside of the pipe.
            return false;
        if ((upsideDownPipe && direction == InteractionDirection.Down) || (!upsideDownPipe && direction == InteractionDirection.Up))
            //we've hit the underside of the pipe
            return false;

        Tilemap tilemap = GameManager.Instance.tilemap;
        Vector3Int ourLocation = Utils.WorldToTilemapPosition(worldLocation);
        int height = GetPipeHeight(ourLocation);
        Vector3Int origin = GetPipeOrigin(ourLocation);
        Vector3Int pipeDirection = upsideDownPipe ? Vector3Int.up : Vector3Int.down;
        Vector3Int hat = origin - (pipeDirection * (height - 1));

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
            if (ourLocation == origin)
                addHat = false;
            tileHeight = Mathf.Abs(hat.y - ourLocation.y) + (addHat ? 2 : 1);

            bool alreadyDestroyed = tilemap.GetTile(hat).name.EndsWith("D");
            
            object[] parametersParticle = new object[]{(Vector2) worldLocation + (leftOfPipe ? Vector2.zero : Vector2.left * 0.5f), leftOfPipe, upsideDownPipe, new Vector2(2, tileHeight-(addHat? 1 : 0)), "DestructablePipe" + (alreadyDestroyed ? "-D" : "")};
            GameManager.Instance.SendAndExecuteEvent(Enums.NetEventIds.SpawnResizableParticle, parametersParticle, ExitGames.Client.Photon.SendOptions.SendUnreliable);
        }
        string[] tiles = new string[tileHeight*2];
        
        int start = upsideDownPipe ? (tileHeight*2)-2 : 0;
        if (addHat) {
            if (leftOfPipe) {
                //we're the left side. modify the right side too.
                if (shrink) {
                    tiles[start] = "SpecialPipes/" + tilemap.GetTile(hat).name;
                    tiles[start + 1] = "SpecialPipes/" + tilemap.GetTile(hat + Vector3Int.right).name;
                } else {
                    tiles[start] = "SpecialPipes/" + leftDestroy;
                    tiles[start + 1] = "SpecialPipes/" + rightDestroy;
                }
            } else {
                //we're the right side. modify the left side too.
                if (shrink) {
                    tiles[start] = "SpecialPipes/" + tilemap.GetTile(hat + Vector3Int.left).name;
                    tiles[start + 1] = "SpecialPipes/" + tilemap.GetTile(hat).name;
                } else {
                    tiles[start] = "SpecialPipes/" + leftDestroy;
                    tiles[start + 1] = "SpecialPipes/" + rightDestroy;
                }
            }
        }

        for (int i = 0; i < tiles.Length; i++)
            //photon doesn't like serializing nulls
            if (tiles[i] == null) 
                tiles[i] = "";

        Vector3Int offset = upsideDownPipe ? Vector3Int.zero : pipeDirection * (tileHeight-1);
        BulkModifyTilemap(hat + offset + (leftOfPipe ? Vector3Int.zero : Vector3Int.left), new Vector2Int(2, tileHeight), tiles);
        return true;
    }

    private Vector3Int GetPipeOrigin(Vector3Int ourLocation) {
        Tilemap tilemap = GameManager.Instance.tilemap;
        Vector3Int searchDirection = upsideDownPipe ? Vector3Int.up : Vector3Int.down;
        Vector3Int searchVector = upsideDownPipe ? Vector3Int.up : Vector3Int.down;
        while (tilemap.GetTile<BreakablePipeTile>(ourLocation + searchVector))
            searchVector += searchDirection;
        return ourLocation + searchVector - searchDirection;
    }

    private int GetPipeHeight(Vector3Int ourLocation) {
        int height = 1;
        Tilemap tilemap = GameManager.Instance.tilemap;
        Vector3Int searchVector = Vector3Int.up;
        while (tilemap.GetTile<BreakablePipeTile>(ourLocation + searchVector)) {
            height++;
            searchVector += Vector3Int.up;
        }
        searchVector = Vector3Int.down;
        while (tilemap.GetTile<BreakablePipeTile>(ourLocation + searchVector)) {
            height++;
            searchVector += Vector3Int.down;
        }
        return height;
    }

    private void BulkModifyTilemap(Vector3Int tileOrigin, Vector2Int tileDimensions, string[] tilenames) {
        object[] parametersTile = new object[]{tileOrigin.x, tileOrigin.y, tileDimensions.x, tileDimensions.y, tilenames};
        GameManager.Instance.SendAndExecuteEvent(Enums.NetEventIds.SetTileBatch, parametersTile, ExitGames.Client.Photon.SendOptions.SendReliable);
    }
}
