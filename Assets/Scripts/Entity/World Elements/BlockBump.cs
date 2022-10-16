using UnityEngine;
using UnityEngine.Tilemaps;

using NSMB.Utils;
using Fusion;

public class BlockBump : NetworkBehaviour {

    //---Networked Variables
    [Networked] private TickTimer DespawnTimer { get; set; }
    [Networked] private NetworkString<_128> ResultTile { get; set; }
    [Networked] private NetworkBool IsDownwards { get; set; }
    [Networked] private NetworkBool SpawnCoin { get; set; }
    [Networked] private NetworkPrefabRef SpawnPrefab { get; set; }
    [Networked] private Vector2 SpawnOffset { get; set; }

    //---Components
    private SpriteRenderer spriteRenderer;
    private Animator animator;

    public void Awake() {
        animator = GetComponent<Animator>();
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
    }

    public void OnBeforeSpawned(Vector3Int spriteLocation, string resultTile, NetworkPrefabRef? spawnPrefab, bool downwards, bool spawnCoin, Vector2 spawnOffset = default) {
        spriteRenderer.sprite = GameManager.Instance.tilemap.GetSprite(spriteLocation);
        ResultTile = resultTile;
        SpawnPrefab = spawnPrefab ?? NetworkPrefabRef.Empty;
        IsDownwards = downwards;
        SpawnOffset = spawnOffset;
        SpawnCoin = spawnCoin;

        DespawnTimer = TickTimer.CreateFromSeconds(Runner, 0.25f);
    }

    public override void Spawned() {
        animator.SetBool("down", IsDownwards);
        animator.SetTrigger("start");

        //spawn coin effect immediately
        if (SpawnCoin) {
            GameObject coin = Instantiate(PrefabList.Instance.Particle_CoinFromBlock, transform.position + new Vector3(0, IsDownwards ? -0.25f : 0.5f), Quaternion.identity);
            coin.GetComponentInChildren<Animator>().SetBool("down", IsDownwards);
        }

        BoxCollider2D hitbox = GetComponentInChildren<BoxCollider2D>();
        hitbox.size = spriteRenderer.sprite.bounds.size;
        hitbox.offset = (hitbox.size - Vector2.one) * new Vector2(0.5f, -0.5f);
    }

    public override void FixedUpdateNetwork() {
        if (DespawnTimer.Expired(Runner)) {
            Vector3Int loc = Utils.WorldToTilemapPosition(transform.position);
            Kill(loc);
        }
    }

    public void Kill(Vector3Int loc) {
        if (Object.HasStateAuthority)
            GameManager.Instance.Rpc_SetTile(loc.x, loc.y, ResultTile.ToString());

        if (SpawnPrefab == NetworkPrefabRef.Empty) {
            Runner.Despawn(Object);
            return;
        }

        Vector3 pos = transform.position + Vector3.up * (IsDownwards ? -0.7f : 0.25f);
        Runner.Spawn(SpawnPrefab, pos + (Vector3) SpawnOffset);
        Runner.Despawn(Object);
    }

    public override void Despawned(NetworkRunner runner, bool hasState) {
        base.Despawned(runner, hasState);
    }
}
