using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

using Fusion;

[OrderBefore(typeof(PlayerController))]
public class TileManager : NetworkBehaviour {

    //---Properties
    public int ChunksX => Mathf.CeilToInt(gm.levelWidthTile / 16f);
    public int ChunksY => Mathf.CeilToInt(gm.levelHeightTile / 16f);
    public int WorldOriginX => gm.levelMinTileX;
    public int WorldOriginY => gm.levelMinTileY;

    //---Public Variables
    public TileBase[] sceneTiles;
    public TilemapChunk[] chunks;

    //---Serialized Variables
    [SerializeField, HideInInspector] private GameManager gm;

    public void OnValidate() {
        if (!gm) gm = GetComponentInParent<GameManager>();
    }

    public override void FixedUpdateNetwork() {
        base.FixedUpdateNetwork();
    }

    public void SetTile(TileBase tile, int x, int y) {
        SetTile(GetTileIdFromTileInstance(tile), x, y);
    }

    public void SetTile(ushort tileId, int x, int y) {
        if (tileId > sceneTiles.Length)
            return;

        TilemapChunk chunk = GetChunkAtTileLocation(x, y);
        if (!chunk)
            return;

        int chunkX = (x - WorldOriginX) % 16;
        int chunkY = (y - WorldOriginY) % 16;

        chunk[chunkX, chunkY] = tileId;
    }

    public ushort GetTileIdFromTileInstance(TileBase tile) {
        if (!tile)
            return 0;

        for (ushort i = 0; i < sceneTiles.Length; i++) {
            if (sceneTiles[i] == tile) {
                return i;
            }
        }

        return 0;
    }

    private TilemapChunk GetChunkAtTileLocation(int x, int y) {

        int chunkX = (x - WorldOriginX) / 16;
        int chunkY = (y - WorldOriginY) / 16;

        if (chunkX < 0 || chunkX >= ChunksX || chunkY < 0 || chunkY >= ChunksY)
            return null;

        return chunks[chunkX + (chunkY * ChunksX)];
    }

#if UNITY_EDITOR
    private static Vector3 ChunkSize = new(8, 8, 0);
    public void OnDrawGizmos() {
        Gizmos.color = Color.black;
        for (int x = 0; x < ChunksX; x++) {
            for (int y = 0; y < ChunksY; y++) {
                Gizmos.DrawWireCube(new(gm.LevelMinX + 4 + (x * 8), gm.LevelMinY + 4 + (y * 8)), ChunkSize);
            }
        }
    }
#endif
}
