using UnityEngine;
using UnityEngine.Tilemaps;

using Fusion;
using NSMB.Utils;

[RequireComponent(typeof(GameManager))]
public class GameEventRpcs : NetworkBehaviour {

    //---Static Variables
    private static readonly Vector3 OneFourth = new(0.25f, 0.25f, 0f);

    //---Private Variables
    private GameManager gm;
    private Tilemap tilemap;
    private FloatingCoin[] coins;

    public void Awake() {
        gm = GetComponent<GameManager>();
        tilemap = gm.tilemap;

        coins = FindObjectsOfType<FloatingCoin>();
    }

    //---TILES
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void Rpc_ResetTilemap() {
        tilemap.SetTilesBlock(gm.originalTilesOrigin, gm.originalTiles);

        foreach (FloatingCoin coin in coins)
            coin.ResetCoin();

        foreach (EnemySpawnpoint point in gm.enemySpawns)
            point.AttemptSpawning();

        gm.BigStarRespawnTimer = TickTimer.CreateFromSeconds(Runner, 10.4f - gm.RealPlayerCount * 0.2f);
    }

    public void BumpBlock(short x, short y, string oldTile, string newTile, bool downwards, Vector2 offset, bool spawnCoin, NetworkPrefabRef spawnPrefab) {
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

#pragma warning disable IDE0060
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void Rpc_UpdateSpectatorTilemap([RpcTarget] PlayerRef targetPlayer, TileChangeInfo[] changes, string[] tileNames) {
        foreach (TileChangeInfo change in changes) {
            TileBase tile = Utils.GetCacheTile(tileNames[change.tileIndex]);
            tilemap.SetTile(new(change.x, change.y, 0), tile);
        }
    }
#pragma warning restore IDE0060

    //---GAME STATE
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void Rpc_EndGame(int team) {
        if (gm.GameEnded)
            return;

        // TODO: don't use a coroutine?
        // eh, it should be alrite, since it's an RPC and isn't predictive.
        StartCoroutine(gm.EndGame(team));
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void Rpc_LoadingComplete() {
        // Populate scoreboard
        ScoreboardUpdater.Instance.CreateEntries(gm.AlivePlayers);
        if (Settings.Instance.scoreboardAlways)
            ScoreboardUpdater.Instance.SetEnabled();

        GlobalController.Instance.loadingCanvas.EndLoading();
    }
}
