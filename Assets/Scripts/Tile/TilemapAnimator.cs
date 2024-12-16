using NSMB.Extensions;
using Quantum;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class TilemapAnimator : MonoBehaviour {

    //---Serialized Variables
    [SerializeField] private Tilemap tilemap;
    [SerializeField] private ParticleSystem tileBreakParticleSystem;

    //---Private Variables
    private readonly Dictionary<EntityRef, AudioSource> entityBreakBlockSounds = new();
    private readonly Dictionary<int, TileChangeData> eventData = new();
    private readonly Dictionary<Vector3Int, List<TileEventData>> tileStack = new();
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
    }

    private void OnGameStateChanged(EventGameStateChanged e) {
        if (e.NewState == GameState.Playing) {
            tilemap.RefreshAllTiles();
        }
    }

    private void OnEventConfirmed(CallbackEventConfirmed e) {
        int id = e.EventKey.Id;

        if (eventData.TryGetValue(id, out var data)) {
            if (tileStack.TryGetValue(data.position, out var stack)) {
                var root = stack[0];
                root.ChangeData = data;
                stack[0] = root;

                stack.RemoveAll(ted => ted.Id == id);
            }

            eventData.Remove(id);
        }
    }

    private void OnEventCanceled(CallbackEventCanceled e) {
        int id = e.EventKey.Id;

        if (!eventData.TryGetValue(id, out var data)) {
            return;
        }

        if (tileStack.TryGetValue(data.position, out var stack)) {
            if (stack[^1].Id == id) {
                // Revert
                var root = stack[0].ChangeData;

                tilemap.SetTile(root.position, root.tile);
                tilemap.SetTransformMatrix(root.position, root.transform);
                tilemap.RefreshTile(root.position);
            }

            stack.RemoveAll(ted => ted.Id == id);
        }

        eventData.Remove(id);
    }

    private void OnTileChanged(EventTileChanged e) {
        Vector3Int coords = new(e.TileX, e.TileY, 0);

        if (!e.Synced) {
            if (!tileStack.ContainsKey(coords)) {
                tileStack[coords] = new() {
                    new TileEventData {
                        Id = -1,
                        ChangeData = new TileChangeData {
                            position = coords,
                            tile = tilemap.GetTile(coords),
                            transform = tilemap.GetTransformMatrix(coords)
                        }
                    }
                };
            }
        }

        var tile = QuantumUnityDB.GetGlobalAsset(e.NewTile.Tile);
        TileBase unityTile = tile ? tile.Tile : null;

        tilemap.SetTile(coords, unityTile);
        Matrix4x4 mat = Matrix4x4.TRS(default, Quaternion.Euler(0, 0, e.NewTile.Rotation.AsFloat), new Vector3(e.NewTile.Scale.X.AsFloat, e.NewTile.Scale.Y.AsFloat, 1));
        tilemap.SetTransformMatrix(coords, mat);

        if (!e.Synced) {
            eventData[e.Id] = new TileChangeData {
                position = coords,
                tile = unityTile,
                transform = mat
            };
        }

        tilemap.RefreshTile(coords);
    }

    private unsafe void OnTileBroken(EventTileBroken e) {
        ParticleSystem particle = Instantiate(tileBreakParticleSystem,
            QuantumUtils.RelativeTileToWorld(stage, new Vector2Int(e.TileX, e.TileY)).ToUnityVector2() + (Vector2.one * 0.25f), Quaternion.identity);

        if (QuantumUnityDB.GetGlobalAsset(e.Tile.Tile) is BreakableBrickTile bbt) {
            var main = particle.main;
            main.startColor = bbt.ParticleColor;
        }

        if (particle.TryGetComponent(out AudioSource sfx)) {
            if (entityBreakBlockSounds.TryGetValue(e.Entity, out AudioSource audio) && audio) {
                audio.Stop();
            }

            sfx.PlayOneShot(
                e.Frame.Unsafe.TryGetPointer(e.Entity, out MarioPlayer* mario) && mario->CurrentPowerupState == PowerupState.MegaMushroom
                    ? SoundEffect.Powerup_MegaMushroom_Break_Block
                    : SoundEffect.World_Block_Break);

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
                Vector3Int coords = new Vector3Int(x, y, 0) + (Vector3Int) stage.TileOrigin;

                StageTileInstance tileInstance = f.StageTiles[x + y * width];
                StageTile stageTile = QuantumUnityDB.GetGlobalAsset(tileInstance.Tile);
                TileBase unityTile = stageTile ? stageTile.Tile : null;

                tilemap.SetTile(coords, unityTile);
                Matrix4x4 mat = Matrix4x4.TRS(default, Quaternion.Euler(0, 0, tileInstance.Rotation.AsFloat), new Vector3(tileInstance.Scale.X.AsFloat, tileInstance.Scale.Y.AsFloat, 1));
                tilemap.SetTransformMatrix(coords, mat);
            }
        }

        tilemap.RefreshAllTiles();
    }

    public struct TileEventData {
        public int Id;
        public TileChangeData ChangeData;
    }
}
