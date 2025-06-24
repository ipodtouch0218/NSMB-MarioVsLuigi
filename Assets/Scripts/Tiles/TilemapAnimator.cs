using NSMB.Quantum;
using NSMB.Utilities.Extensions;
using Quantum;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using static NSMB.Utilities.QuantumViewUtils;

namespace NSMB.Tiles {
    public class TilemapAnimator : QuantumSceneViewComponent<StageContext> {

        //---Serialized Variables
        [SerializeField] private Tilemap tilemap;
        [SerializeField] private ParticleSystem tileBreakParticleSystem;

        //---Private Variables
        private readonly Dictionary<EntityRef, AudioSource> entityBreakBlockSounds = new();
        private readonly Dictionary<EventKey, Vector3Int> tilePositionData = new();
        private readonly Dictionary<Vector3Int, List<TileChangeData>> tileUndoData = new();
        private double startTime;

        public void OnValidate() {
            this.SetIfNull(ref tilemap);
        }

        public override void OnEnable() {
            UseFindUpdater = true;
            base.OnEnable();
        }

        public void Start() {
            QuantumCallback.Subscribe<CallbackGameResynced>(this, OnGameResynced);
            QuantumCallback.Subscribe<CallbackEventCanceled>(this, OnEventCanceled);
            QuantumCallback.Subscribe<CallbackEventConfirmed>(this, OnEventConfirmed);
            QuantumEvent.Subscribe<EventTileChanged>(this, OnTileChanged);
            QuantumEvent.Subscribe<EventTileBroken>(this, OnTileBroken, FilterOutReplayFastForward);
            QuantumEvent.Subscribe<EventGameStateChanged>(this, OnGameStateChanged);

            if (QuantumRunner.DefaultGame != null) {
                RefreshMap(QuantumRunner.DefaultGame.Frames.Verified);
            } else {
                tilemap.RefreshAllTiles();
            }
            startTime = Time.timeAsDouble;
        }

        public void RefreshMap(Frame f) {
            VersusStageData stage = ViewContext.Stage;
            int width = stage.TileDimensions.X;
            if (f == null
                || f.StageTiles == null
                || f.StageTiles.Length != width * stage.TileDimensions.Y) {
                return;
            }

            for (int x = 0; x < width; x++) {
                for (int y = 0; y < stage.TileDimensions.Y; y++) {
                    Vector3Int coords = new(stage.TileOrigin.X + x, stage.TileOrigin.Y + y, 0);

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
                    tilemap.RefreshTile(coords);
                    if (unityTile is AnimatedTile at) {
                        tilemap.SetAnimationTime(coords, (float) (Time.timeAsDouble - startTime) * at.m_MaxSpeed % at.m_AnimatedSprites.Length);
                    }
                }
            }
        }

        private void OnGameStateChanged(EventGameStateChanged e) {
            if (e.NewState == GameState.Playing) {
                tilemap.RefreshAllTiles();
                startTime = Time.timeAsDouble;
            }
        }

        private void OnEventCanceled(CallbackEventCanceled e) {
            EventKey key = e.EventKey;

            if (!tilePositionData.TryGetValue(key, out Vector3Int coords)) {
                return;
            }

            var undoData = tileUndoData[coords];
            if (undoData.Count == 1) {
                // This is a cancelled tile change event.
                tilemap.SetTile(coords, undoData[0].tile);
                tilemap.SetTransformMatrix(coords, undoData[0].transform);
                tilemap.RefreshTile(coords);
                if (undoData[0].tile is AnimatedTile at) {
                    tilemap.SetAnimationTime(coords, (float) (Time.timeAsDouble - startTime) * at.m_MaxSpeed % at.m_AnimatedSprites.Length);
                }
            }

            if (undoData.Count > 0) {
                undoData.RemoveAt(0);
            }

            tilePositionData.Remove(key);
        }

        private void OnEventConfirmed(CallbackEventConfirmed e) {
            if (tilePositionData.TryGetValue(e.EventKey, out Vector3Int coords)) {
                if (tileUndoData.TryGetValue(coords, out List<TileChangeData> undoData)) {
                    if (undoData.Count > 0) {
                        undoData.RemoveAt(0);
                    }
                }
            }
            tilePositionData.Remove(e.EventKey);
        }

        private void OnTileChanged(EventTileChanged e) {
            Vector3Int coords = (Vector3Int) e.Position.ToVector2Int();
            Vector2 scale = new Vector2 {
                x = e.NewTile.Flags.HasFlag(StageTileFlags.MirrorX) ? -1 : 1,
                y = e.NewTile.Flags.HasFlag(StageTileFlags.MirrorY) ? -1 : 1,
            };

            var tile = QuantumUnityDB.GetGlobalAsset(e.NewTile.Tile);
            TileBase unityTile = tile ? tile.Tile : null;
            Matrix4x4 mat = Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(0, 0, e.NewTile.Rotation / (float) (ushort.MaxValue / 360f)), scale);

            tilePositionData[e] = coords;

            if (!tileUndoData.TryGetValue(coords, out var list)) {
                tileUndoData[coords] = list = new();
            }
            list.Add(new TileChangeData {
                position = coords,
                tile = tilemap.GetTile(coords),
                transform = tilemap.GetTransformMatrix(coords),
            });

            tilemap.SetTile(coords, unityTile);
            tilemap.SetTransformMatrix(coords, mat);
            tilemap.RefreshTile(coords);
            if (unityTile is AnimatedTile at) {
                tilemap.SetAnimationTime(coords, (float) (Time.timeAsDouble - startTime) * at.m_MaxSpeed % at.m_AnimatedSprites.Length);
            }
        }

        private unsafe void OnTileBroken(EventTileBroken e) {
            ParticleSystem particle = Instantiate(tileBreakParticleSystem,
                QuantumUtils.RelativeTileToWorld(ViewContext.Stage, e.Position).ToUnityVector2() + (Vector2.one * 0.25f), Quaternion.identity);

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

        private void OnGameResynced(CallbackGameResynced e) {
            RefreshMap(PredictedFrame);
            tileUndoData.Clear();
            tilePositionData.Clear();
        }
    }
}
