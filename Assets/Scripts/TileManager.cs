using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

using Fusion;
using NSMB.Entities;
using NSMB.Entities.Collectable;
using NSMB.Entities.Player;
using NSMB.Game;
using NSMB.Utils;

namespace NSMB.Tiles {

    [OrderBefore(typeof(PlayerController))]
    public class TileManager : NetworkBehaviour {

        //---Properties
        public int ChunksX { get; private set; }
        public int ChunksY { get; private set; }
        public int WorldOriginX { get; private set; }
        public int WorldOriginY { get; private set; }

        //---Public Variables
        public List<TilemapChunk> chunks;

        //---Private Variables
        private GameManager gm;

        public override void Spawned() {
            if (!gm)
                Initialize(GameManager.Instance);
        }

        public void Initialize(GameManager gm) {
            this.gm = gm;
            gm.tileManager = this;

            ChunksX = Mathf.CeilToInt(gm.levelWidthTile / 16f);
            ChunksY = Mathf.CeilToInt(gm.levelHeightTile / 16f);
            WorldOriginX = gm.levelMinTileX;
            WorldOriginY = gm.levelMinTileY;

            int requiredChunks = ChunksX * ChunksY;

            for (int i = 0; i < requiredChunks; i++) {
                Runner.Spawn(PrefabList.Instance.TilemapChunk, onBeforeSpawned: (runner, obj) => {
                    obj.GetComponent<TilemapChunk>().OnBeforeSpawned(
                        (ushort) (i % ChunksX),
                        (ushort) (i / ChunksX)
                    );
                });
            }
        }

        public void AddChunk(TilemapChunk chunk) {
            if (chunks.Contains(chunk))
                return;

            chunks.Add(chunk);
            chunks.Sort((chunkA, chunkB) => {
                if (chunkA.ChunkY == chunkB.ChunkY)
                    return chunkA.ChunkX - chunkB.ChunkX;

                return chunkA.ChunkY - chunkB.ChunkY;
            });
        }

        public void ResetMap() {
            foreach (TilemapChunk chunk in chunks)
                chunk.ResetMap();

            foreach (FloatingCoin coin in gm.coins)
                coin.ResetCoin();

            foreach (KillableEntity enemy in gm.enemies) {
                if (enemy.checkForNearbyPlayersWhenRespawning) {
                    if (Runner.GetPhysicsScene2D().OverlapCircle(enemy.body.position, 2.5f, Layers.MaskOnlyPlayers)) {
                        continue;
                    }
                }

                enemy.RespawnEntity();
            }

            GameData.Instance.BigStarRespawnTimer = TickTimer.CreateFromSeconds(Runner, 10.4f - GameData.Instance.RealPlayerCount * 0.2f);
        }

        public TileBase GetTile(int x, int y) {

            TilemapChunk chunk = GetChunkAtTileLocation(x, y);
            if (!chunk)
                return null;

            ushort tileId = chunk.GetTile(TileLocationToChunkIndex(x, y));
            return gm.GetTileInstanceFromTileId(tileId);
        }

        public TileBase GetTile(Vector2Int loc) {
            return GetTile(loc.x, loc.y);
        }

        public bool GetTile<T>(int x, int y, out T outTile) where T : TileBase {
            TileBase tile = GetTile(x, y);
            if (tile is T tTile) {
                outTile = tTile;
                return true;
            }

            outTile = null;
            return false;
        }

        public bool GetTile<T>(Vector2Int loc, out T outTile) where T : TileBase {
            return GetTile(loc.x, loc.y, out outTile);
        }

        public void SetTilesBlock(Vector2Int loc, Vector2Int dimensions, TileBase[] tiles) {
            SetTilesBlock(loc.x, loc.y, dimensions.x, dimensions.y, tiles);
        }

        public void SetTilesBlock(Vector2Int loc, Vector2Int dimensions, ushort[] tileIds) {
            SetTilesBlock(loc.x, loc.y, dimensions.x, dimensions.y, tileIds);
        }

        public void SetTilesBlock(int x, int y, int width, int height, TileBase[] tiles) {
            ushort[] tileIds = new ushort[tiles.Length];
            for (int i = 0; i < tiles.Length; i++)
                tileIds[i] = gm.GetTileIdFromTileInstance(tiles[i]);

            SetTilesBlock(x, y, width, height, tileIds);
        }

        public void SetTilesBlock(int x, int y, int width, int height, ushort[] tileIds) {
            for (int i = 0; i < width; i++) {
                for (int j = 0; j < height; j++) {
                    SetTile(x + i, y + j, tileIds[i + j * width]);
                }
            }
        }

        public void SetTile(Vector2Int loc, TileBase tile) {
            SetTile(loc.x, loc.y, tile);
        }

        public void SetTile(Vector2Int loc, ushort tileId) {
            SetTile(loc.x, loc.y, tileId);
        }

        public void SetTile(int x, int y, TileBase tile) {
            SetTile(x, y, gm.GetTileIdFromTileInstance(tile));
        }

        public void SetTile(int x, int y, ushort tileId) {
            if (tileId > gm.sceneTiles.Length)
                return;

            TilemapChunk chunk = GetChunkAtTileLocation(x, y);
            if (!chunk)
                return;

            chunk.SetTile(TileLocationToChunkIndex(x, y), tileId);
        }

        private int TileLocationToChunkIndex(int x, int y) {
            x = (x - WorldOriginX) % 16;
            y = (y - WorldOriginY) % 16;
            return x + (y * 16);
        }

        private TilemapChunk GetChunkAtTileLocation(int x, int y) {

            int chunkX = (x - WorldOriginX) >> 4;
            int chunkY = (y - WorldOriginY) >> 4;

            if (chunkX < 0 || chunkX >= ChunksX || chunkY < 0 || chunkY >= ChunksY)
                return null;

            return chunks[chunkX + (chunkY * ChunksX)];
        }

#if UNITY_EDITOR
        private static Vector3 ChunkSize = new(8, 8, 0);
        public void OnDrawGizmos() {
            if (!gm.tilemap)
                return;

            Gizmos.color = Color.black;
            for (int x = 0; x < ChunksX; x++) {
                for (int y = 0; y < ChunksY; y++) {
                    Gizmos.DrawWireCube(new(gm.LevelMinX + 4 + (x * 8), gm.LevelMinY + 4 + (y * 8)), ChunkSize);
                }
            }
        }
#endif
    }
}
