using JimmysUnityUtilities;
using NSMB.Tiles;
using Photon.Deterministic;
using Quantum;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEditor;
using AssetObjectQuery = Quantum.AssetObjectQuery;

[assembly: QuantumMapBakeAssembly]
namespace NSMB.Quantum { 
    public class VersusStageBaker : MapDataBakerCallback {

        private static readonly FPVector2[] GridCoords = new FPVector2[] {
        new(FP._0_50, FP._0_50),
        new(FP._0_50, -FP._0_50),
        new(-FP._0_50, -FP._0_50),
        new(-FP._0_50, FP._0_50),
    };

        private QuantumMapData data;

        public override void OnBeforeBake(QuantumMapData data) {
        }

        public override void OnBake(QuantumMapData data) {
            int changed = 0;
            foreach (var stageTile in FindAssetsByType<StageTile>()) {
                var shapes = stageTile.CollisionData.Shapes;
                if (shapes.Length != 1) {
                    continue;
                }

                if (shapes[0].Vertices.ContainsAll(GridCoords)) {
                    changed++;
                    stageTile.CollisionData = Grid;
                    EditorUtility.SetDirty(stageTile);
                }
            }
            if (changed != 0) {
                LogInfo($"{changed} StageTile(s) had a custom physics shape which was just a full grid. Migrated those custom shapes to 'IsFullTile' automatically. This should only appear once.");
            }

            this.data = data;
            var stage = QuantumUnityDB.GetGlobalAssetEditorInstance<VersusStageData>(data.Asset.UserAsset);
            if (!stage) {
                LogError("Scene does not have a VersusStageData instance as the Map's UserAsset", data);
                return;
            }

            GameObject tilemapGo = GameObject.FindFirstObjectByType<TilemapAnimator>().gameObject;
            if (!tilemapGo || !tilemapGo.TryGetComponent(out Tilemap tilemap)) {
                LogError("Could not find a main tilemap (one that has a TilemapAnimator component!)");
                return;
            }

            // --- Tilemap Settings
            tilemap.CompressBounds();
            stage.TilemapWorldPosition = tilemap.transform.position.ToFPVector2();
            if (!stage.OverrideAutomaticTilemapSettings) {
                stage.TileOrigin = new IntVector2(tilemap.cellBounds.xMin, tilemap.cellBounds.yMin);
                stage.TileDimensions = new IntVector2(tilemap.cellBounds.size.x, tilemap.cellBounds.size.y);
                LogInfo($"Automatically found stage dimensions: origin={stage.TileOrigin} size={stage.TileDimensions}");
            }

            // --- Camera Settings
            if (!stage.OverrideAutomaticCameraSettings) {
                stage.CameraMinPosition = stage.TilemapWorldPosition + ((FPVector2) stage.TileOrigin / 2);
                stage.CameraMaxPosition = stage.CameraMinPosition + ((FPVector2) stage.TileDimensions / 2);

                if (stage.IsWrappingLevel) {
                    stage.CameraMinPosition.X = -1000;
                    stage.CameraMaxPosition.X = 1000;
                }
                // Adjust so we don't see the absolute bottom of the stage.
                stage.CameraMinPosition += FPVector2.Up * FP._1_50;
                LogInfo($"Automatically found camera bounds: min={stage.CameraMinPosition} max={stage.CameraMaxPosition}");
            }

            // --- Bake Tilemap
            HashSet<AssetRef<StageTile>> uniqueTiles = new();
            BoundsInt stageBounds = new(stage.TileOrigin.X, stage.TileOrigin.Y, 0, stage.TileDimensions.X, stage.TileDimensions.Y, 1);
            TileBase[] stageTiles = tilemap.GetTilesBlock(stageBounds);
            stage.TileData = new StageTileInstance[stageTiles.Length];
            for (int i = 0; i < stageTiles.Length; i++) {
                Matrix4x4 mat = tilemap.GetTransformMatrix(
                    new Vector3Int(
                        i % stage.TileDimensions.X + stage.TileOrigin.X,
                        i / stage.TileDimensions.X + stage.TileOrigin.Y
                ));
                stage.TileData[i] = GetStageTileInstance(stageTiles[i], mat);
                uniqueTiles.Add(stage.TileData[i].Tile);
            }
            LogInfo($"Baked level data with {uniqueTiles.Count} unique tiles");

            // --- Bake Star Spawns
            GameObject[] starSpawns = GameObject.FindGameObjectsWithTag("StarSpawn");
            stage.BigStarSpawnpoints = starSpawns.Select(go => go.transform.position.ToFPVector2()).Take(64).ToArray();
            if (starSpawns.Length <= 64) {
                LogInfo($"Automatically found big star spawns: {stage.BigStarSpawnpoints.Length}");
            } else {
                LogError($"The stage has a hard limit of 64 star spawns! (Found {starSpawns.Length})");
            }

            // --- Bake Enemies(' spawnpoints)
            QPrototypeEnemy[] enemies = GameObject.FindObjectsByType<QPrototypeEnemy>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var enemy in enemies) {
                enemy.Prototype.Spawnpoint = enemy.transform.position.ToFPVector2();
                EditorUtility.SetDirty(enemy);
            }
            LogInfo($"Baked {enemies.Length} enemies");

            /*
            // --- Bake Breakable Objects
            QPrototypeBreakableObject[] breakables = GameObject.FindObjectsByType<QPrototypeBreakableObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var breakable in breakables) {
                var prototype = breakable.GetComponent<QuantumEntityPrototype>();
                var shape = prototype.PhysicsCollider.Shape2D;
                shape.PositionOffset = FPVector2.Up * (breakable.Prototype.OriginalHeight / 4);
                shape.BoxExtents.Y = (breakable.Prototype.OriginalHeight / 4);

                SpriteRenderer sRenderer = breakable.GetComponentInChildren<SpriteRenderer>();
                sRenderer.size = new Vector2(sRenderer.size.x, breakable.Prototype.OriginalHeight.AsFloat);
                EditorUtility.SetDirty(breakable);
                EditorUtility.SetDirty(prototype);
            }
            LogInfo($"Baked {breakables.Length} breakable objects");
            */

            EditorUtility.SetDirty(stage);
        }

        private StageTileInstance GetStageTileInstance(TileBase tile, Matrix4x4 mat) {
            StageTileFlags flags = default;
            if (mat.lossyScale.x < 0) {
                flags |= StageTileFlags.MirrorX;
            }
            if (mat.lossyScale.y < 0) {
                flags |= StageTileFlags.MirrorY;
            }
            return new StageTileInstance {
                Tile = GetStageTile(tile),
                Rotation = (ushort) ((Mathf.Repeat(mat.rotation.eulerAngles.z, 360) / 360) * ushort.MaxValue),
                Flags = flags,
            };
        }

        private StageTile GetStageTile(TileBase tile) {
            if (!tile) {
                return default;
            }

            StageTile existingTile = QuantumUnityDB.FindGlobalAssetGuids(new AssetObjectQuery(typeof(StageTile)))
                .Select(guid => new AssetRef<StageTile>(guid))
                .Select(QuantumUnityDB.GetGlobalAssetEditorInstance)
                .FirstOrDefault(st => st.Tile == tile);

            if (existingTile) {
                return existingTile;
            }

            return CreateStageTile(tile);
        }

        public StageTile CreateStageTile(TileBase tile) {
            StageTile newTile = ScriptableObject.CreateInstance<StageTile>();
            newTile.Tile = tile;

            switch (tile) {
            case Tile t: {
                newTile.CollisionData = GetTileCollisionData(t.colliderType, t.sprite);
                break;
            }
            case AnimatedTile at: {
                newTile.CollisionData = GetTileCollisionData(at.m_TileColliderType, at.m_AnimatedSprites[0]);
                break;
            }
            case RuleTile rt: {
                if (rt.m_DefaultColliderType == Tile.ColliderType.Sprite ||
                    rt.m_TilingRules.Any(tr => tr.m_ColliderType == Tile.ColliderType.Sprite)) {
                    // Does not support sprite collision
                    LogError("RuleTile \"Sprite\" collider mode is not supported!", rt);
                    throw new ArgumentException();
                }

                if (rt.m_TilingRules.Any(tr => tr.m_ColliderType != rt.m_DefaultColliderType)) {
                    // All tiles must have same collision type (the default set)
                    LogError("All RuleTile rules must have the same collision type as the default!", rt);
                    throw new ArgumentException();
                }

                newTile.CollisionData = GetTileCollisionData(rt.m_DefaultColliderType, rt.m_DefaultSprite);
                break;
            }
            default:
                LogError($"Tile {tile} is not currently supported!", tile);
                throw new ArgumentException();
            }

            string existingTilePath = AssetDatabase.GetAssetPath(tile);
            string newTilePath = Regex.Replace(existingTilePath, @"\..+", "") + "StageTile.asset";
            AssetDatabase.CreateAsset(newTile, newTilePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            QuantumUnityDB.Global.AddAsset(newTile);

            return newTile;
        }

        private static readonly StageTile.TileCollisionData Grid = new() {
            IsFullTile = true,
        };

        private static StageTile.TileCollisionData GetTileCollisionData(Tile.ColliderType collider, Sprite sprite) {
            switch (collider) {
            case Tile.ColliderType.Grid:
                // Single full-tile square.
                return Grid;
            case Tile.ColliderType.Sprite:
                // Get from sprite
                StageTile.TileCollisionData data = new() {
                    Shapes = new StageTile.TileCollisionData.TileShape[sprite.GetPhysicsShapeCount()]
                };
                for (int i = 0; i < data.Shapes.Length; i++) {
                    List<Vector2> vertices = new();
                    sprite.GetPhysicsShape(i, vertices);
                    data.Shapes[i].Vertices = vertices
                        .Select(v2 => v2.ToFPVector2())
                        .ToArray();
                    FPVector2.MakeCounterClockWise(data.Shapes[i].Vertices);
                }
                return data;
            default:
            case Tile.ColliderType.None:
                // No shapes.
                return default;
            }
        }

        //---Helpers
        private void LogInfo(string message) {
            Debug.Log($"[VersusStageBaker] {message}");
        }

        private void LogError(string message, UnityEngine.Object focus = null) {
            Debug.LogWarning($"[VersusStageBaker] Unable to bake scene \"{data.Asset.ScenePath}\": {message}", focus);
        }

        // https://discussions.unity.com/t/getting-all-assets-of-the-specified-type/75031/5
        private static IEnumerable<T> FindAssetsByType<T>() where T : UnityEngine.Object {
            var guids = AssetDatabase.FindAssets($"t:{typeof(T)}");
            foreach (var t in guids) {
                var assetPath = AssetDatabase.GUIDToAssetPath(t);
                var asset = AssetDatabase.LoadAssetAtPath<T>(assetPath);
                if (asset != null) {
                    yield return asset;
                }
            }
        }

        private class TileBaseSorter : IComparer<TileBase> {
            public int Compare(TileBase x, TileBase y) {
                if (!x) {
                    return -1;
                }

                if (!y) {
                    return 1;
                }

                return x.name.CompareTo(y.name);
            }
        }
    }
}
