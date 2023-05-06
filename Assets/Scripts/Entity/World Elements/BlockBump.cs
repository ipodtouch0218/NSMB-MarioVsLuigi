using UnityEngine;
using UnityEngine.Tilemaps;

using Fusion;
using NSMB.Tiles;
using NSMB.Extensions;

public class BlockBump : NetworkBehaviour, IPredictedSpawnBehaviour {

    //---Networked Variables
    [Networked] private TickTimer        DespawnTimer { get; set; }
    [Networked] private ushort           ResultTile { get; set; }
    [Networked] private ushort           BumpTile { get; set; }
    [Networked] private NetworkBool      IsDownwards { get; set; }
    [Networked] private NetworkBool      SpawnCoin { get; set; }
    [Networked] private NetworkPrefabRef SpawnPrefab { get; set; }
    [Networked] private Vector2          SpawnOffset { get; set; }
    [Networked] private Vector2Int       TileLocation { get; set; }

    //---Serialized Variables
    [SerializeField] private Transform hitbox;
    [SerializeField] private Transform graphics;
    [SerializeField] private float bumpSize = 0.35f;
    [SerializeField] private float bumpScale = 0.25f;
    [SerializeField] private float bumpDuration = 0.15f;

    //---Components
    [SerializeField] private SpriteRenderer sRenderer;

    public void OnBeforeSpawned(Vector2Int tileLocation, ushort bumpTile, ushort resultTile, NetworkPrefabRef? spawnPrefab, bool downwards, bool spawnCoin, Vector2 spawnOffset = default) {
        TileLocation = tileLocation;
        BumpTile = bumpTile;
        ResultTile = resultTile;
        SpawnPrefab = spawnPrefab ?? NetworkPrefabRef.Empty;
        IsDownwards = downwards;
        SpawnOffset = spawnOffset;
        SpawnCoin = spawnCoin;

        DespawnTimer = TickTimer.CreateFromSeconds(Runner, bumpDuration);
    }

    public void OnBeforeSpawned(Vector2Int tileLocation, TileBase bumpTile, TileBase resultTile, NetworkPrefabRef? spawnPrefab, bool downwards, bool spawnCoin, Vector2 spawnOffset = default) {
        TileManager tm = GameManager.Instance.tileManager;
        ushort bumpTileId = tm.GetTileIdFromTileInstance(bumpTile);
        ushort resultTileId = tm.GetTileIdFromTileInstance(resultTile);

        OnBeforeSpawned(tileLocation, bumpTileId, resultTileId, spawnPrefab, downwards, spawnCoin, spawnOffset);
    }

    public override void Spawned() {
        //spawn coin effect immediately
        if (SpawnCoin) {
            GameObject coin = Instantiate(PrefabList.Instance.Particle_CoinFromBlock, transform.position + new Vector3(0, IsDownwards ? -0.25f : 0.5f), Quaternion.identity);
            coin.GetComponentInChildren<Animator>().SetBool("down", IsDownwards);
        }

        //graphics bs
        Tilemap tilemap = GameManager.Instance.tilemap;
        TileManager tm = GameManager.Instance.tileManager;

        tilemap.SetTile((Vector3Int) TileLocation, tm.GetTileInstanceFromTileId(BumpTile));
        sRenderer.sprite = GameManager.Instance.tilemap.GetSprite((Vector3Int) TileLocation);
        tm.SetTile(TileLocation, null);

        BoxCollider2D hitbox = GetComponentInChildren<BoxCollider2D>();
        hitbox.size = sRenderer.sprite.bounds.size;
        hitbox.offset = (hitbox.size - Vector2.one) * new Vector2(0.5f, -0.5f);
    }

    public override void Render() {

        float elapsedTime = DespawnTimer.RemainingRenderTime(Runner) ?? 0f;
        float sin = Mathf.Sin((elapsedTime / bumpDuration) * Mathf.PI);

        graphics.localPosition = new(0, (bumpSize * sin) * (IsDownwards ? -1 : 1), 0);
        graphics.localScale = new(1 + (bumpScale * sin), 1 + (bumpScale * sin), 1);
    }

    public override void FixedUpdateNetwork() {

        float elapsedTime = DespawnTimer.RemainingTime(Runner) ?? 0f;
        float sin = Mathf.Sin((elapsedTime / bumpDuration) * Mathf.PI);

        hitbox.localPosition = new(0, (bumpSize * sin) * (IsDownwards ? -1 : 1), 0);
        hitbox.localScale = new(1 + (bumpScale * sin), 1 + (bumpScale * sin), 1);

        if (DespawnTimer.Expired(Runner)) {
            DespawnTimer = TickTimer.None;
            Kill();
        }
    }

    public void Kill() {
        GameManager.Instance.tileManager.SetTile(TileLocation, ResultTile);

        if (SpawnPrefab == NetworkPrefabRef.Empty) {
            Runner.Despawn(Object);
            return;
        }

        bool mega = SpawnPrefab == PrefabList.Instance.Powerup_MegaMushroom;
        Vector2 pos = (Vector2) transform.position + SpawnOffset;
        Vector2 animOrigin = pos;
        Vector2 animDestination;
        float pickupDelay = 0.75f;

        if (mega) {
            animOrigin += (Vector2.up * 0.5f);
            animDestination = animOrigin;
            pickupDelay = 1.5f;

        } else if (IsDownwards) {
            float blockSize = sRenderer.sprite.bounds.size.y * 0.5f;
            animOrigin += (0.5f - blockSize) * Vector2.up;
            animDestination = animOrigin + (Vector2.down * 0.5f);

        } else {
            animDestination = animOrigin + (Vector2.up * 0.5f);
        }

        Runner.Spawn(SpawnPrefab, animOrigin, onBeforeSpawned: (runner, obj) => {
            obj.GetComponent<MovingPowerup>().OnBeforeSpawned(pickupDelay, animOrigin, animDestination);
        });
        Runner.Despawn(Object);
    }

    public void PredictedSpawnSpawned() { }

    public void PredictedSpawnUpdate() { }

    public void PredictedSpawnRender() { }

    public void PredictedSpawnFailed() {
        Runner.Despawn(Object, true);
    }

    public void PredictedSpawnSuccess() { }
}
