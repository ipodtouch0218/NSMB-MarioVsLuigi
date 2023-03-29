using UnityEngine;
using UnityEngine.Tilemaps;

using Fusion;

public class TilemapChunk : NetworkBehaviour {

    //---Static Variables
    private static readonly TileBase[] TileBuffer = new TileBase[128];

    //---Public Variables
    public ushort defaultChunkX, defaultChunkY;

    //---Networked Variables
    [Networked(Default = nameof(defaultChunkX))] public ushort ChunkX { get; set; }
    [Networked(Default = nameof(defaultChunkY))] public ushort ChunkY { get; set; }
    [Networked, Capacity(128)] public NetworkArray<ushort> Tiles => default;

    //---Private Variables
    private readonly ushort[] originalTiles = new ushort[128];

    public ushort this[int x, int y] {
        get => Tiles[x + (y * 16)];
        set => Tiles.Set(x + (y * 16), value);
    }

    public void LoadState() {
        GameManager gm = GameManager.Instance;
        Tilemap tilemap = gm.tilemap;
        BoundsInt bounds = new(gm.levelMinTileX + (ChunkX * 16), gm.levelMinTileY + (ChunkX * 16), 0, 16, 16, 1);
        tilemap.GetTilesBlockNonAlloc(bounds, TileBuffer);

        TileManager tm = gm.tileManager;
        for (int i = 0; i < TileBuffer.Length; i++) {
            originalTiles[i] = tm.GetTileIdFromTileInstance(TileBuffer[i]);
        }

        ResetState();
    }

    public void ResetState() {
        for (int i = 0; i < Tiles.Length; i++) {
            Tiles.Set(i, originalTiles[i]);
        }
    }
}
