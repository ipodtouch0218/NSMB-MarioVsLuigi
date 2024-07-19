using Photon.Deterministic;
using Quantum;
using Quantum.Collections;
using UnityEngine;

public unsafe class VersusStageData : AssetObject {

    //---Properties
    public FPVector2 StageWorldMin => new FPVector2(TileOrigin.x, TileOrigin.y) / 2 + TilemapWorldPosition;
    public FPVector2 StageWorldMax => new FPVector2(TileOrigin.x + TileDimensions.x, TileOrigin.y + TileDimensions.y) / 2 + TilemapWorldPosition;

    //---Serialized
    [Header("-- Tilemap")]
    public bool OverrideAutomaticTilemapSettings;
    public Vector2Int TileDimensions;
    public Vector2Int TileOrigin;
    public FPVector2 TilemapWorldPosition;
    public bool IsWrappingLevel = true;

    [Header("-- Spawnpoint")]
    public FPVector2 Spawnpoint;
    public FPVector2 SpawnpointArea;

    [Header("-- Camera")]
    public bool OverrideAutomaticCameraSettings;
    public FPVector2 CameraMinPosition;
    public FPVector2 CameraMaxPosition;

    [Header("-- Powerups")]
    public bool SpawnBigPowerups = true;
    public bool SpawnVerticalPowerups = true;

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

    public StageTileInstance GetTileWorld(Frame f, FPVector2 worldPosition) {
        worldPosition -= TilemapWorldPosition;
        worldPosition *= 2;
        int x = FPMath.FloorToInt(worldPosition.X) - TileOrigin.x;
        x = (x % TileDimensions.x + TileDimensions.x) % TileDimensions.x; // Wrapping
        int y = FPMath.FloorToInt(worldPosition.Y) - TileOrigin.y;
        int index = x + y * TileDimensions.x;

        QList<StageTileInstance> stageLayout = f.ResolveList(f.Global->Stage);
        if (index < 0 || index >= stageLayout.Count) {
            return default;
        }
        return stageLayout[index];
    }

}