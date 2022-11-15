using UnityEngine;
using UnityEngine.Tilemaps;

using Fusion;
using NSMB.Utils;

[RequireComponent(typeof(GameManager))]
public class GameEventRpcs : NetworkBehaviour {

    private static readonly Vector3 OneFourth = new(0.25f, 0.25f);

    //---Private Variables
    private GameManager gm;
    private Tilemap tilemap;
    private BoundsInt originalTilesOrigin;
    private TileBase[] originalTiles;
    private FloatingCoin[] coins;

    public void Awake() {
        gm = GetComponent<GameManager>();
        tilemap = gm.tilemap;

        coins = FindObjectsOfType<FloatingCoin>();

        originalTilesOrigin = new(gm.levelMinTileX, gm.levelMinTileY, 0, gm.levelWidthTile, gm.levelHeightTile, 1);
        originalTiles = tilemap.GetTilesBlock(originalTilesOrigin);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void Rpc_ResetTilemap() {
        tilemap.SetTilesBlock(originalTilesOrigin, originalTiles);

        foreach (FloatingCoin coin in coins)
            coin.ResetCoin();

        foreach (EnemySpawnpoint point in gm.enemySpawns)
            point.AttemptSpawning();

        gm.BigStarRespawnTimer = TickTimer.CreateFromSeconds(Runner, 10.4f - gm.RealPlayerCount * 0.2f);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void Rpc_BumpBlock(short x, short y, string oldTile, string newTile, bool downwards, Vector2 offset, bool spawnCoin, NetworkPrefabRef spawnPrefab) {
        Vector3Int loc = new(x, y, 0);

        Vector3 spawnLocation = Utils.TilemapToWorldPosition(loc) + OneFourth;

        Runner.Spawn(PrefabList.Instance.Obj_BlockBump, spawnLocation, onBeforeSpawned: (runner, obj) => {
            obj.GetComponentInChildren<BlockBump>().OnBeforeSpawned(loc, oldTile, newTile, spawnPrefab, downwards, spawnCoin, offset);
        });
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void Rpc_SetTile(short x, short y, string tilename) {
        TileBase tile = Utils.GetCacheTile(tilename);
        tilemap.SetTile(new(x, y, 0), tile);
    }
}
