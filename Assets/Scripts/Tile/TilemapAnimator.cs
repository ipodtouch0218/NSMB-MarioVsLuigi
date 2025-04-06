using NSMB.Extensions;
using Quantum;
using System.Collections.Generic;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.Tilemaps;

public class TilemapAnimator : MonoBehaviour {

    //---Serialized Variables
    [SerializeField] private Tilemap tilemap;
    [SerializeField] private ParticleSystem tileBreakParticleSystem;

    //---Private Variables
    private readonly Dictionary<EntityRef, AudioSource> entityBreakBlockSounds = new();
    private readonly Dictionary<EventKey, Vector3Int> tileEventPositions = new();
    private VersusStageData stage;


    public void OnValidate() {
        this.SetIfNull(ref tilemap);
    }

    public void Start() {
        QuantumEvent.Subscribe<EventTileChanged>(this, OnTileChanged);
        QuantumEvent.Subscribe<EventTileBroken>(this, OnTileBroken, NetworkHandler.FilterOutReplayFastForward);
        QuantumEvent.Subscribe<EventGameStateChanged>(this, OnGameStateChanged);
        QuantumCallback.Subscribe<CallbackGameResynced>(this, e => RefreshMap(e.Game.Frames.Predicted));
        QuantumCallback.Subscribe<CallbackEventCanceled>(this, OnEventCanceled);
        QuantumCallback.Subscribe<CallbackEventConfirmed>(this, OnEventConfirmed);

        stage = (VersusStageData) QuantumUnityDB.GetGlobalAsset(FindObjectOfType<QuantumMapData>().Asset.UserAsset);
        if (QuantumRunner.DefaultGame != null) {
            RefreshMap(QuantumRunner.DefaultGame.Frames.Verified);
        }
        tilemap.RefreshAllTiles();
    }

    private void OnGameStateChanged(EventGameStateChanged e) {
        if (e.NewState == GameState.Playing) {
            tilemap.RefreshAllTiles();
        }
    }

    private void OnEventCanceled(CallbackEventCanceled e) {
        EventKey id = e.EventKey;

        if (!tileEventPositions.TryGetValue(id, out Vector3Int coords)) {
            return;
        }


        // This is a tile change event.
        // Refer back to the simulation
        Frame f = e.Game.Frames.Predicted;
        StageTileInstance tileInstance = stage.GetTileRelative(f, coords.x, coords.y);

        var tile = QuantumUnityDB.GetGlobalAsset(tileInstance.Tile);
        TileBase unityTile = tile ? tile.Tile : null;
        Vector2 scale = new Vector2 {
            x = tileInstance.Flags.HasFlag(StageTileFlags.MirrorX) ? -1 : 1,
            y = tileInstance.Flags.HasFlag(StageTileFlags.MirrorY) ? -1 : 1,
        };
        Matrix4x4 mat = Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(0, 0, tileInstance.Rotation / (float) (ushort.MaxValue / 360f)), scale);

        // Debug.Log($"tile event cancelled at {coords}. Was {tilemap.GetTile(coords)?.name}, changing back to {unityTile}");

        tilemap.SetTile(coords, unityTile);
        tilemap.SetTransformMatrix(coords, mat);
        tilemap.RefreshTile(coords);

        tileEventPositions.Remove(id);
    }

    private void OnEventConfirmed(CallbackEventConfirmed e) {
        if (tileEventPositions.TryGetValue(e.EventKey, out Vector3Int coords)) {
            // Debug.Log($"tile event CONFIRMED at {coords}.");
        }
        tileEventPositions.Remove(e.EventKey);
    }

    private void OnTileChanged(EventTileChanged e) {
        Vector3Int coords = new(e.TileX, e.TileY, 0);
        Vector2 scale = new Vector2 {
            x = e.NewTile.Flags.HasFlag(StageTileFlags.MirrorX) ? -1 : 1,
            y = e.NewTile.Flags.HasFlag(StageTileFlags.MirrorY) ? -1 : 1,
        };

        var tile = QuantumUnityDB.GetGlobalAsset(e.NewTile.Tile);
        TileBase unityTile = tile ? tile.Tile : null;
        Matrix4x4 mat = Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(0, 0, e.NewTile.Rotation / (float) (ushort.MaxValue / 360f)), scale);

        tilemap.SetTile(coords, unityTile);
        tilemap.SetTransformMatrix(coords, mat);
        tilemap.RefreshTile(coords);
        
        tileEventPositions[e] = coords;
    }

    private unsafe void OnTileBroken(EventTileBroken e) {
        ParticleSystem particle = Instantiate(tileBreakParticleSystem,
            QuantumUtils.RelativeTileToWorld(stage, new Quantum.Vector2Int(e.TileX, e.TileY)).ToUnityVector2() + (Vector2.one * 0.25f), Quaternion.identity);

        if (QuantumUnityDB.GetGlobalAsset(e.Tile.Tile) is BreakableBrickTile bbt) {
            var main = particle.main;
            main.startColor = bbt.ParticleColor;
        }

        if (particle.TryGetComponent(out AudioSource sfx)) {
            if (entityBreakBlockSounds.TryGetValue(e.Entity, out AudioSource audio) && audio) {
                audio.Stop();
            }

            sfx.PlayOneShot(e.BrokenByMega ? SoundEffect.Powerup_MegaMushroom_Break_Block : SoundEffect.World_Block_Break);
            entityBreakBlockSounds[e.Entity] = sfx;
        }

        particle.Play();
    }
    
    private void RefreshMap(Frame f) {
        if (f == null
            || f.StageTiles == null 
            || f.StageTiles.Length != stage.TileDimensions.x * stage.TileDimensions.y) {
            return;
        }

        int width = stage.TileDimensions.x;
        for (int x = 0; x < width; x++) {
            for (int y = 0; y < stage.TileDimensions.y; y++) {
                Vector3Int coords = new(stage.TileOrigin.x + x, stage.TileOrigin.y + y, 0);

                StageTileInstance tileInstance = f.StageTiles[x + y * width];
                StageTile stageTile = QuantumUnityDB.GetGlobalAsset(tileInstance.Tile);
                TileBase unityTile = stageTile ? stageTile.Tile : null;

                Vector2 scale = new Vector2 {
                    x = tileInstance.Flags.HasFlag(StageTileFlags.MirrorX) ? -1 : 1,
                    y = tileInstance.Flags.HasFlag(StageTileFlags.MirrorY) ? -1 : 1,
                };

                tilemap.SetTile(coords, unityTile);
                Matrix4x4 mat = Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(0, 0, tileInstance.Rotation / (float) (ushort.MaxValue / 360f)), scale);
                tilemap.SetTransformMatrix(coords, mat);
            }
        }

        tilemap.RefreshAllTiles();
    }

    public struct TileEventData {
        public EventKey Id;
        public TileChangeData ChangeData;
    }
}
