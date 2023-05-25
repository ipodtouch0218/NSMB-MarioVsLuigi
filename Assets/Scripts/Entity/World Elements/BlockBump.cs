using UnityEngine;
using UnityEngine.Tilemaps;

using Fusion;
using NSMB.Entities.Collectable.Powerups;
using NSMB.Extensions;
using NSMB.Game;
using NSMB.Tiles;

public class BlockBump : NetworkBehaviour, IPredictedSpawnBehaviour {

    #region //---Combined Variables
    private TickTimer DespawnTimer {
        get => Object.IsPredictedSpawn ? predictiveDespawnTimer : NetworkedDespawnTimer;
        set {
            if (Object.IsPredictedSpawn)
                predictiveDespawnTimer = value;
            else
                NetworkedDespawnTimer = value;
        }
    }
    private ushort ResultTile {
        get => Object.IsPredictedSpawn ? predictiveResultTile : NetworkedResultTile;
        set {
            if (Object.IsPredictedSpawn)
                predictiveResultTile = value;
            else
                NetworkedResultTile = value;
        }
    }
    private ushort BumpTile {
        get => Object.IsPredictedSpawn ? predictiveBumpTile : NetworkedBumpTile;
        set {
            if (Object.IsPredictedSpawn)
                predictiveBumpTile = value;
            else
                NetworkedBumpTile = value;
        }
    }
    private NetworkBool IsDownwards {
        get => Object.IsPredictedSpawn ? predictiveIsDownwards : NetworkedIsDownwards;
        set {
            if (Object.IsPredictedSpawn)
                predictiveIsDownwards = value;
            else
                NetworkedIsDownwards = value;
        }
    }
    private NetworkBool SpawnCoin {
        get => Object.IsPredictedSpawn ? predictiveSpawnCoin : NetworkedSpawnCoin;
        set {
            if (Object.IsPredictedSpawn)
                predictiveSpawnCoin = value;
            else
                NetworkedSpawnCoin = value;
        }
    }
    private NetworkPrefabRef SpawnPrefab {
        get => Object.IsPredictedSpawn ? predictiveSpawnPrefab : NetworkedSpawnPrefab;
        set {
            if (Object.IsPredictedSpawn)
                predictiveSpawnPrefab = value;
            else
                NetworkedSpawnPrefab = value;
        }
    }
    private Vector2 SpawnOffset {
        get => Object.IsPredictedSpawn ? predictiveSpawnOffset : NetworkedSpawnOffset;
        set {
            if (Object.IsPredictedSpawn)
                predictiveSpawnOffset = value;
            else
                NetworkedSpawnOffset = value;
        }
    }
    private Vector2Int TileLocation {
        get => Object.IsPredictedSpawn ? predictiveTileLocation : NetworkedTileLocation;
        set {
            if (Object.IsPredictedSpawn)
                predictiveTileLocation = value;
            else
                NetworkedTileLocation = value;
        }
    }
    #endregion

    //---Networked Variables
    [Networked] private TickTimer NetworkedDespawnTimer { get; set; }
    [Networked] private ushort NetworkedResultTile { get; set; }
    [Networked] private ushort NetworkedBumpTile { get; set; }
    [Networked] private NetworkBool NetworkedIsDownwards { get; set; }
    [Networked] private NetworkBool NetworkedSpawnCoin { get; set; }
    [Networked] private NetworkPrefabRef NetworkedSpawnPrefab { get; set; }
    [Networked] private Vector2 NetworkedSpawnOffset { get; set; }
    [Networked] private Vector2Int NetworkedTileLocation { get; set; }

    #region //---Predictive Variables
    private TickTimer predictiveDespawnTimer;
    private ushort predictiveResultTile;
    private ushort predictiveBumpTile;
    private NetworkBool predictiveIsDownwards;
    private NetworkBool predictiveSpawnCoin;
    private NetworkPrefabRef predictiveSpawnPrefab;
    private Vector2 predictiveSpawnOffset;
    private Vector2Int predictiveTileLocation;
    private Tick spawnTick;
    #endregion

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
        GameManager gm = GameManager.Instance;
        ushort bumpTileId = gm.GetTileIdFromTileInstance(bumpTile);
        ushort resultTileId = gm.GetTileIdFromTileInstance(resultTile);

        OnBeforeSpawned(tileLocation, bumpTileId, resultTileId, spawnPrefab, downwards, spawnCoin, spawnOffset);
    }

    public override void Spawned() {
        //spawn coin effect immediately
        if (!Object.IsPredictedSpawn && SpawnCoin) {
            GameObject coin = Instantiate(PrefabList.Instance.Particle_CoinFromBlock, transform.position + new Vector3(0, IsDownwards ? -0.25f : 0.5f), Quaternion.identity);
            coin.GetComponentInChildren<Animator>().SetBool("down", IsDownwards);
        }

        //graphics bs
        TileBase tile = GameManager.Instance.GetTileInstanceFromTileId(BumpTile);
        Sprite sprite;
        if (tile is TileWithProperties tp) {
            sprite = tp.m_DefaultSprite;
        } else if (tile is AnimatedTile at) {
            sprite = at.m_AnimatedSprites[0];
        } else if (tile is Tile t) {
            sprite = t.sprite;
        } else {
            sprite = null;
        }
        sRenderer.sprite = sprite;

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

    public void PredictedSpawnSpawned() {
        spawnTick = Runner.Tick;
        Spawned();
    }

    public void PredictedSpawnUpdate() {
        FixedUpdateNetwork();
        GameManager.Instance.tileManager.SetTile(TileLocation, null);
    }

    public void PredictedSpawnRender() {
        Render();
    }

    public void PredictedSpawnFailed() {
        Runner.Despawn(Object, true);
    }

    public void PredictedSpawnSuccess() {

    }
}
