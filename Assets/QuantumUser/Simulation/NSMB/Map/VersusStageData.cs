using Photon.Deterministic;
using Quantum;
using Quantum.Profiling;
using UnityEngine;

public unsafe class VersusStageData : AssetObject {

    //---Properties
    public FPVector2 StageWorldMin => new FPVector2(TileOrigin.x, TileOrigin.y) / 2 + TilemapWorldPosition;
    public FPVector2 StageWorldMax => new FPVector2(TileOrigin.x + TileDimensions.x, TileOrigin.y + TileDimensions.y) / 2 + TilemapWorldPosition;

    //---Serialized
    [Header("-- Information")]
    public string StageAuthor;
    public string MusicComposer;
    public string TranslationKey;
    public string DiscordStageImage;
    public Sprite Icon;

    [Header("-- Tilemap")]
    public bool OverrideAutomaticTilemapSettings;
    public Vector2Int TileDimensions;
    public Vector2Int TileOrigin;
    public FPVector2 TilemapWorldPosition;
    public bool IsWrappingLevel = true;
    public bool ExtendCeilingHitboxes = false;

    [Header("-- Spawnpoint")]
    public FPVector2 Spawnpoint;
    public FPVector2 SpawnpointArea;

    [Header("-- Camera")]
    public bool OverrideAutomaticCameraSettings;
    public FPVector2 CameraMinPosition;
    public FPVector2 CameraMaxPosition;

    [Header("-- UI")]
    public Color UIColor = new(24, 178, 170);
    public bool HidePlayersOnMinimap;

    [Header("-- Powerups")]
    public bool SpawnBigPowerups = true;
    public bool SpawnVerticalPowerups = true;

    [Header("-- Music")]
    public LoopingMusicData[] MainMusic;
    public LoopingMusicData InvincibleMusic;
    public LoopingMusicData MegaMushroomMusic;


    [HideInInspector] public StageTileInstance[] TileData;
    [HideInInspector] public FPVector2[] BigStarSpawnpoints;

    public FPVector2 GetWorldSpawnpointForPlayer(int playerIndex, int totalPlayers) {
        FP comp = ((FP) playerIndex / totalPlayers) * 2 * FP.Pi + FP.PiOver2 + (FP.Pi / (2 * totalPlayers));
        FP scale = (FP._2 - ((FP) totalPlayers + 1) / totalPlayers) * SpawnpointArea.X;

        FPVector2 offset = new(
            FPMath.Sin(comp) * scale,
            FPMath.Cos(comp) * (totalPlayers > 2 ? scale * SpawnpointArea.Y: 0)
        );

        return Spawnpoint + offset;
    }

    public StageTileInstance GetTileRelative(Frame f, int x, int y) {
        int index = x + y * TileDimensions.x;
        StageTileInstance[] stageLayout = f.StageTiles;
        if (index < 0 || index >= stageLayout.Length) {
            return default;
        }

        return stageLayout[index];
    }

    public StageTileInstance GetTileRelative(Frame f, Vector2Int tile) {
        return GetTileRelative(f, tile.x, tile.y);
    }

    public StageTileInstance GetTileWorld(Frame f, FPVector2 worldPosition) {
        return GetTileRelative(f, QuantumUtils.WorldToRelativeTile(f, worldPosition));
    }

    public void SetTileRelative(Frame f, int x, int y, StageTileInstance tile) {
        int index = x + y * TileDimensions.x;
        StageTileInstance[] stageLayout = f.StageTiles;
        if (index < 0 || index >= stageLayout.Length) {
            return;
        }

        stageLayout[index] = tile;
        f.Signals.OnTileChanged(x, y, tile);
        f.Events.TileChanged(f, x + TileOrigin.x, y + TileOrigin.y, tile);
    }

    public void ResetStage(Frame f, bool full) {
        using var scope = HostProfiler.Start("VersusStageData.ResetStage");
        StageTileInstance[] stageData = f.StageTiles;

        for (int i = 0; i < TileData.Length; i++) {
            ref StageTileInstance newTile = ref TileData[i];
            if (!stageData[i].Equals(newTile)) {
                using var callbackScope = HostProfiler.Start("VersusStageData.ExecuteCallbacks");
                int x = i % TileDimensions.x + TileOrigin.x;
                int y = i / TileDimensions.x + TileOrigin.y;
                f.Signals.OnTileChanged(x, y, newTile);
                f.Events.TileChanged(f, x, y, newTile);
            }
            stageData[i] = newTile;
        }
        f.Signals.OnStageReset(full);
    }
}