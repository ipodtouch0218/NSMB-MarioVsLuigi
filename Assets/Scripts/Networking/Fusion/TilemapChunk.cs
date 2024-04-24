using UnityEngine;
using UnityEngine.Tilemaps;

using Fusion;
using NSMB.Game;

namespace NSMB.Tiles {
    public class TilemapChunk : NetworkBehaviour, IBeforeTick, IAfterTick {

        //---Static Variables
        private static readonly TileBase[] TileBuffer = new TileBase[256];

        //---Networked Variables
        [Networked] public ushort ChunkX { get; set; }
        [Networked] public ushort ChunkY { get; set; }
        [Networked] public byte DirtyCounter { get; set; }
        [Networked, Capacity(256)] public NetworkArray<ushort> Tiles => default;

        //---Private Variables
        private readonly ushort[] originalTiles = new ushort[256];
        private BoundsInt bounds;
        private TilemapCollider2D tilemapCollider;
        private byte latestDirtyCounter;
        private bool updatedDirtyCounterThisTick;
        private bool initialized;
        private ChangeDetector changeDetector;

        public void BeforeTick() {
            updatedDirtyCounterThisTick = false;

            if (!Runner.IsResimulation || latestDirtyCounter == DirtyCounter) {
                return;
            }

            // The the tilemap is different from it's current state.
            UpdateTilemapState();
            latestDirtyCounter = DirtyCounter;
        }

        public void AfterTick() {
            // The tilemap was updated via the dirty counter
            if (updatedDirtyCounterThisTick || (latestDirtyCounter != DirtyCounter)) {
                UpdateTilemapState();
                updatedDirtyCounterThisTick = false;
            }

            latestDirtyCounter = DirtyCounter;
        }

        public void OnBeforeSpawned(ushort x, ushort y) {
            ChunkX = x;
            ChunkY = y;
        }

        public override void Spawned() {
            if (Runner.Topology == Topologies.ClientServer) {
                Runner.SetIsSimulated(Object, true);
            }

            if (initialized) {
                return;
            }

            transform.SetParent(GameManager.Instance.tileManager.transform, true);
            tilemapCollider = GameManager.Instance.tilemap.GetComponent<TilemapCollider2D>();
            LoadState();
            if (HasStateAuthority) {
                Tiles.CopyFrom(originalTiles, 0, originalTiles.Length);
            }

            UpdateTilemapState();
            GameManager.Instance.tileManager.AddChunk(this);
            changeDetector = GetChangeDetector(ChangeDetector.Source.SnapshotTo);
            initialized = true;
        }

        public override void FixedUpdateNetwork() {
            if (Runner.Topology == Topologies.Shared) {
                foreach (var x in changeDetector.DetectChanges(this)) {
                    switch (x) {
                    case nameof(DirtyCounter):
                        UpdateTilemapState();
                        break;
                    }
                }
            }
        }

        public void LoadState() {
            GameManager gm = GameManager.Instance;

            int chunkOriginIndex = (ChunkX * 16) + (ChunkY * GameManager.Instance.tileManager.ChunksX * 256);

            for (int i = 0; i < 256; i++) {
                int x = i % 16;
                int y = i / 16;

                int index = chunkOriginIndex + x + (y * 16 * GameManager.Instance.tileManager.ChunksX);
                if (index < gm.originalTiles.Length) {
                    originalTiles[i] = gm.originalTiles[index];
                }
            }

            bounds = new(gm.levelMinTileX + (ChunkX * 16), gm.levelMinTileY + (ChunkY * 16), 0, 16, 16, 1);
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
            tilemap.SetTilesBlock(bounds, TileBuffer);
            tilemapCollider.ProcessTilemapChanges();
        }

        private void LoadTileBuffer(ushort[] src = null) {
            GameManager gm = GameManager.Instance;

            if (src == null) {
                for (int i = 0; i < TileBuffer.Length; i++) {
                    TileBuffer[i] = gm.GetTileInstanceFromTileId(Tiles[i]);
                }
            } else {
                for (int i = 0; i < TileBuffer.Length; i++) {
                    TileBuffer[i] = gm.GetTileInstanceFromTileId(src[i]);
                }
            }
        }

        public void SetTile(int index, ushort value) {
            if (Tiles[index] == value) {
                return;
            }

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
        // --- Debug
        private static readonly Color SelectedColor = new(0.5f, 0.5f, 0.5f, 0.2f);
        private static readonly Vector3 ChunkSize = new(8, 8, 0);
        public void OnDrawGizmosSelected() {
            Gizmos.color = SelectedColor;
            GameManager gm = GameManager.Instance;
            Gizmos.DrawCube(new(gm.LevelMinX + 4 + (ChunkX * 8), gm.LevelMinY + 4 + (ChunkY * 8)), ChunkSize);
        }
#endif
    }
}
