using UnityEngine;
using UnityEngine.Tilemaps;

using Fusion;

public class TilemapChunk : NetworkBehaviour, IBeforeTick, IAfterTick {

    //---Static Variables
    private static readonly TileBase[] TileBuffer = new TileBase[256];

    //---Public Variables
    public ushort chunkX, chunkY;
    public ushort[] originalTiles = new ushort[256];
    public BoundsInt ourBounds;

    //---Networked Variables
    [Networked] public byte DirtyCounter { get; set; }
    [Networked, Capacity(256)] public NetworkArray<ushort> Tiles => default;

    //---Private Variables
    private byte latestDirtyCounter;
    private bool updatedDirtyCounterThisTick;

    public void BeforeTick() {
        if (!Runner.IsResimulation)
            return;

        if (latestDirtyCounter == DirtyCounter)
            return;

        // the the tilemap is different from it's current state.
        UpdateTilemapState();
        Debug.Log($"{latestDirtyCounter} -> {DirtyCounter}");
        latestDirtyCounter = DirtyCounter;
    }

    public void AfterTick() {
        latestDirtyCounter = DirtyCounter;

        // the tilemap was updated via the dirty counter
        if (updatedDirtyCounterThisTick) {
            UpdateTilemapState();
            updatedDirtyCounterThisTick = false;
        }
    }

    public override void Spawned() {
        Tiles.CopyFrom(originalTiles, 0, originalTiles.Length);
    }

    public void LoadState() {
        GameManager gm = GameManager.Instance;
        Tilemap tilemap = gm.tilemap;
        ourBounds = new(gm.levelMinTileX + (chunkX * 16), gm.levelMinTileY + (chunkY * 16), 0, 16, 16, 1);
        tilemap.GetTilesBlockNonAlloc(ourBounds, TileBuffer);

        TileManager tm = gm.tileManager;
        for (int i = 0; i < TileBuffer.Length; i++) {
            originalTiles[i] = tm.GetTileIdFromTileInstance(TileBuffer[i]);
        }
    }

    public void ResetMap() {
        // Check for changes
        if (!updatedDirtyCounterThisTick) {
            for (int i = 0; i < Tiles.Length; i++) {
                ushort resetTo = originalTiles[i];
                if (Tiles[i] != resetTo) {
                    updatedDirtyCounterThisTick = true;
                    DirtyCounter++;
                }
            }
        }

        Tiles.CopyFrom(originalTiles, 0, originalTiles.Length);
    }

    public void UpdateTilemapState() {
        Tilemap tilemap = GameManager.Instance.tilemap;
        LoadTileBuffer();
        tilemap.SetTilesBlock(ourBounds, TileBuffer);
    }

    private void LoadTileBuffer(ushort[] src = null) {
        GameManager gm = GameManager.Instance;
        TileManager tm = gm.tileManager;

        if (src == null) {
            for (int i = 0; i < TileBuffer.Length; i++) {
                TileBuffer[i] = tm.sceneTiles[Tiles[i]];
            }
        } else {
            for (int i = 0; i < TileBuffer.Length; i++) {
                TileBuffer[i] = tm.sceneTiles[src[i]];
            }
        }
        Debug.Log("tilebuffer loaded ");
    }

    public void SetTile(int index, ushort value) {
        Tiles.Set(index, value);
        if (!updatedDirtyCounterThisTick) {
            updatedDirtyCounterThisTick = true;
            DirtyCounter++;
        }
    }

    public void SetTile(int x, int y, ushort value) {
        SetTile(x + (y * 16), value);
    }
}
