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
    [Networked] private byte PredictionCounter { get; set; }

    public void Awake() {
        gm = GetComponent<GameManager>();
    }

    //---TILES
    public void BumpBlock(short x, short y, TileBase oldTile, TileBase newTile, bool downwards, Vector2 offset, bool spawnCoin, NetworkPrefabRef spawnPrefab) {
        Vector2Int loc = new(x, y);

        Vector3 spawnLocation = Utils.TilemapToWorldPosition(loc) + OneFourth;

        NetworkObject bumper = Runner.Spawn(PrefabList.Instance.Obj_BlockBump, spawnLocation, onBeforeSpawned: (runner, obj) => {
            obj.GetComponentInChildren<BlockBump>().OnBeforeSpawned(loc, oldTile, newTile, spawnPrefab, downwards, spawnCoin, offset);
        }, predictionKey: new() { Byte1 = (byte) Runner.Tick, Byte0 = PredictionCounter++ });

        gm.tileManager.SetTile(loc, null);
    }

    //---GAME STATE
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void Rpc_EndGame(int team) {
        //if (gm.GameEnded)
        //    return;

        // TODO: don't use a coroutine?
        // eh, it should be alrite, since it's an RPC and isn't predictive.
        StartCoroutine(gm.EndGame(team));
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void Rpc_LoadingComplete() {
        // Populate scoreboard
        ScoreboardUpdater.Instance.CreateEntries(gm.AlivePlayers);
        if (Settings.Instance.genericScoreboardAlways)
            ScoreboardUpdater.Instance.SetEnabled();

        GlobalController.Instance.loadingCanvas.EndLoading();
    }
}
