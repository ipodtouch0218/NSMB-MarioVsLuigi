using UnityEngine;
using UnityEngine.Tilemaps;

using Fusion;

namespace NSMB.Tiles {
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
            updatedDirtyCounterThisTick = false;

            if (!Runner.IsResimulation)
                return;

            if (latestDirtyCounter == DirtyCounter)
                return;

            // the the tilemap is different from it's current state.
            UpdateTilemapState();
            latestDirtyCounter = DirtyCounter;
        }

        public void AfterTick() {

            // the tilemap was updated via the dirty counter
            if (updatedDirtyCounterThisTick) {
                UpdateTilemapState();
                updatedDirtyCounterThisTick = false;
            }

            latestDirtyCounter = DirtyCounter;
        }

        public override void Spawned() {
            if (Runner.IsServer)
                Tiles.CopyFrom(originalTiles, 0, originalTiles.Length);

            UpdateTilemapState();
        }

        public override void Render() {
            if (latestDirtyCounter != DirtyCounter) {
                UpdateTilemapState();
                latestDirtyCounter = DirtyCounter;
            }
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

        public ushort GetTile(int index) {
            return Tiles[index];
        }

        public ushort GetTile(int x, int y) {
            return GetTile(x + (y * 16));
        }

#if UNITY_EDITOR
        private static readonly Color SelectedColor = new(0.5f, 0.5f, 0.5f, 0.2f);
        private static readonly Vector3 ChunkSize = new(8, 8, 0);
        public void OnDrawGizmosSelected() {
            Gizmos.color = SelectedColor;
            GameManager gm = GameManager.Instance;
            Gizmos.DrawCube(new(gm.LevelMinX + 4 + (chunkX * 8), gm.LevelMinY + 4 + (chunkY * 8)), ChunkSize);
        }
#endif
    }
}
