using UnityEngine;
using UnityEngine.Tilemaps;

using Fusion;
using NSMB.Utils;

namespace NSMB.Tiles {
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

        public void ResetMap() {
            foreach (TilemapChunk chunk in chunks)
                chunk.ResetMap();

            foreach (FloatingCoin coin in gm.coins)
                coin.ResetCoin();

            foreach (KillableEntity enemy in gm.enemies) {
                if (enemy.checkForNearbyPlayersWhenRespawning) {
                    if (Runner.GetPhysicsScene2D().OverlapCircle(enemy.body.position, 1.5f, Layers.MaskOnlyPlayers)) {
                        continue;
                    }
                }

                enemy.RespawnEntity();
            }

            gm.BigStarRespawnTimer = TickTimer.CreateFromSeconds(Runner, 10.4f - gm.RealPlayerCount * 0.2f);
        }

        public TileBase GetTile(int x, int y) {

            TilemapChunk chunk = GetChunkAtTileLocation(x, y);
            if (!chunk)
                return null;

            ushort tileId = chunk.GetTile(TileLocationToChunkIndex(x, y));
            return GetTileInstanceFromTileId(tileId);
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
                tileIds[i] = GetTileIdFromTileInstance(tiles[i]);

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
            SetTile(x, y, GetTileIdFromTileInstance(tile));
        }

        public void SetTile(int x, int y, ushort tileId) {
            if (tileId > sceneTiles.Length)
                return;

            TilemapChunk chunk = GetChunkAtTileLocation(x, y);
            if (!chunk)
                return;

            chunk.SetTile(TileLocationToChunkIndex(x, y), tileId);
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

        public TileBase GetTileInstanceFromTileId(ushort id) {
            return sceneTiles[id];
        }

        private int TileLocationToChunkIndex(int x, int y) {
            x = (x - WorldOriginX) % 16;
            y = (y - WorldOriginY) % 16;
            return x + (y * 16);
        }

        private TilemapChunk GetChunkAtTileLocation(int x, int y) {

            int chunkX = Mathf.FloorToInt((x - WorldOriginX) / 16f);
            int chunkY = Mathf.FloorToInt((y - WorldOriginY) / 16f);

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
