using UnityEngine;
using UnityEngine.Tilemaps;

using Fusion;
using NSMB.Game;

namespace NSMB.Tiles {

    [OrderAfter(typeof(TileManager))]
    public class TilemapChunk : NetworkBehaviour, IBeforeTick, IAfterTick {

        //---Static Variables
        private static readonly TileBase[] TileBuffer = new TileBase[256];

        //---Networked Variables
        [Networked] public byte DirtyCounter { get; set; }
        [Networked] public ushort ChunkX { get; set; }
        [Networked] public ushort ChunkY { get; set; }
        [Networked, Capacity(256)] public NetworkArray<ushort> Tiles => default;

        //---Private Variables
        private readonly ushort[] originalTiles = new ushort[256];
        private BoundsInt bounds;
        private TilemapCollider2D tilemapCollider;
        private byte latestDirtyCounter;
        private bool updatedDirtyCounterThisTick;
        private bool initialized;

        public void BeforeTick() {
            updatedDirtyCounterThisTick = false;

            if (!Runner.IsResimulation)
                return;

            if (latestDirtyCounter == DirtyCounter)
                return;

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
            if (initialized)
                return;

            Debug.Log($"TilemapChunk Spawned on tick {Runner.Tick}: {GameManager.Instance.tileManager}");
            GameManager.Instance.tileManager.chunks.Add(this);
            transform.SetParent(GameManager.Instance.tileManager.transform, true);
            LoadState();

            if (Runner.IsServer)
                Tiles.CopyFrom(originalTiles, 0, originalTiles.Length);

            tilemapCollider = GameManager.Instance.tilemap.GetComponent<TilemapCollider2D>();
            UpdateTilemapState();

            initialized = true;
        }

        public override void FixedUpdateNetwork() {
            if (initialized)
                return;

            if (!GameManager.Instance)
                return;

            initialized = true;
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
            name = $"TilemapChunk ({ChunkX},{ChunkY})";
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
            if (Tiles[index] == value)
                return;

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
            Gizmos.DrawCube(new(gm.LevelMinX + 4 + (ChunkX * 8), gm.LevelMinY + 4 + (ChunkY * 8)), ChunkSize);
        }
#endif
    }
}
