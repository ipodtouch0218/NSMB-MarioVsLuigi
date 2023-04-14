using UnityEngine;
using UnityEngine.Tilemaps;

using Fusion;
using NSMB.Tiles;

public class BlockBump : NetworkBehaviour {

    //---Networked Variables
    [Networked] private TickTimer        DespawnTimer { get; set; }
    [Networked] private ushort           ResultTile { get; set; }
    [Networked] private ushort           BumpTile { get; set; }
    [Networked] private NetworkBool      IsDownwards { get; set; }
    [Networked] private NetworkBool      SpawnCoin { get; set; }
    [Networked] private NetworkPrefabRef SpawnPrefab { get; set; }
    [Networked] private Vector2          SpawnOffset { get; set; }
    [Networked] private Vector2Int       TileLocation { get; set; }

    //---Components
    private SpriteRenderer spriteRenderer;
    private Animator animator;

    public void Awake() {
        animator = GetComponent<Animator>();
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
    }

    public void OnBeforeSpawned(Vector2Int tileLocation, ushort bumpTile, ushort resultTile, NetworkPrefabRef? spawnPrefab, bool downwards, bool spawnCoin, Vector2 spawnOffset = default) {
        TileLocation = tileLocation;
        BumpTile = bumpTile;
        ResultTile = resultTile;
        SpawnPrefab = spawnPrefab ?? NetworkPrefabRef.Empty;
        IsDownwards = downwards;
        SpawnOffset = spawnOffset;
        SpawnCoin = spawnCoin;

        DespawnTimer = TickTimer.CreateFromSeconds(Runner, 0.25f);
    }

    public void OnBeforeSpawned(Vector2Int tileLocation, TileBase bumpTile, TileBase resultTile, NetworkPrefabRef? spawnPrefab, bool downwards, bool spawnCoin, Vector2 spawnOffset = default) {
        TileManager tm = GameManager.Instance.tileManager;
        ushort bumpTileId = tm.GetTileIdFromTileInstance(bumpTile);
        ushort resultTileId = tm.GetTileIdFromTileInstance(resultTile);

        OnBeforeSpawned(tileLocation, bumpTileId, resultTileId, spawnPrefab, downwards, spawnCoin, spawnOffset);
    }


    public override void Spawned() {
        animator.SetBool("down", IsDownwards);
        animator.SetTrigger("start");

        //spawn coin effect immediately
        if (SpawnCoin) {
            GameObject coin = Instantiate(PrefabList.Instance.Particle_CoinFromBlock, transform.position + new Vector3(0, IsDownwards ? -0.25f : 0.5f), Quaternion.identity);
            coin.GetComponentInChildren<Animator>().SetBool("down", IsDownwards);
        }

        //graphics bs
        Tilemap tilemap = GameManager.Instance.tilemap;
        TileManager tm = GameManager.Instance.tileManager;

        tilemap.SetTile((Vector3Int) TileLocation, tm.GetTileInstanceFromTileId(BumpTile));
        spriteRenderer.sprite = GameManager.Instance.tilemap.GetSprite((Vector3Int) TileLocation);
        tm.SetTile(TileLocation, null);

        BoxCollider2D hitbox = GetComponentInChildren<BoxCollider2D>();
        hitbox.size = spriteRenderer.sprite.bounds.size;
        hitbox.offset = (hitbox.size - Vector2.one) * new Vector2(0.5f, -0.5f);

    }

    public override void FixedUpdateNetwork() {
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
            float blockSize = spriteRenderer.sprite.bounds.size.y * 0.5f;
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
}
