using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

using NSMB.Game;

namespace NSMB.Tiles {
    public class TilemapChunkPopulator : AssetModificationProcessor {

        public static string[] OnWillSaveAssets(string[] paths) {

            // Only process scene objects.
            foreach (string path in paths) {
                if (path.EndsWith(".unity"))
                    ProcessScene(path);
            }

            return paths;
        }

        private static void ProcessScene(string path) {

            GameManager gm = Object.FindFirstObjectByType<GameManager>();

            // No GameManager = Not in a level scene.
            if (!gm || !gm.tilemap) {
                Debug.Log($"Saving non-MvL Game scene at {path} (no GameManager!)");
                return;
            }

            Debug.Log("Handling saving of a gameplay scene at " + path);
            Tilemap tilemap = gm.tilemap;
            TileManager tileManager = gm.tileManager;

            // Get unique tiles
            TileBase[] uniqueTiles = new TileBase[ushort.MaxValue];
            int tiles = tilemap.GetUsedTilesNonAlloc(uniqueTiles);
            List<TileBase> uniqueTilesList = new(uniqueTiles.Take(tiles));
            uniqueTilesList.Insert(0, null);

            List<IHaveTileDependencies> checkedForDependencies = new();

            for (int i = 1; i <= tiles; i++) {
                TileBase tb = uniqueTilesList[i];
                GetDependenciesRecursively(tb as IHaveTileDependencies, uniqueTilesList, checkedForDependencies);
            }
            foreach (IHaveTileDependencies td in Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).OfType<IHaveTileDependencies>()) {
                GetDependenciesRecursively(td, uniqueTilesList, checkedForDependencies);
            }
            uniqueTilesList = uniqueTilesList.Distinct().ToList();
            uniqueTilesList.Sort(new TileBaseSorter());
            gm.sceneTiles = uniqueTilesList.ToArray();

            int requiredChunksX = (int) Mathf.Ceil(gm.levelWidthTile / 16f);
            int requiredChunksY = (int) Mathf.Ceil(gm.levelHeightTile / 16f);

            BoundsInt bounds = new(gm.levelMinTileX, gm.levelMinTileY, 0, requiredChunksX * 16, requiredChunksY * 16, 1);
            TileBase[] tileBases = tilemap.GetTilesBlock(bounds);
            ushort[] tileIds = new ushort[tileBases.Length];

            for (int i = 0; i < tileBases.Length; i++) {
                tileIds[i] = gm.GetTileIdFromTileInstance(tileBases[i]);
            }

            gm.originalTiles = tileIds;

            Debug.Log($"Successfully saved level data. {uniqueTilesList.Count} unique tiles ({gm.levelWidthTile} x {gm.levelHeightTile} tiles, {requiredChunksX} x {requiredChunksY} chunks).");
            EditorUtility.SetDirty(gm);
            EditorUtility.SetDirty(tileManager);
        }

        private static void GetDependenciesRecursively(IHaveTileDependencies td, List<TileBase> results, List<IHaveTileDependencies> alreadyChecked) {
            if (td == null)
                return;

            if (alreadyChecked.Contains(td))
                return;

            alreadyChecked.Add(td);
            foreach (TileBase childTile in td.GetTileDependencies()) {
                results.Add(childTile);
                GetDependenciesRecursively(childTile as IHaveTileDependencies, results, alreadyChecked);
            }
        }

        private class TileBaseSorter : IComparer<TileBase> {
            public int Compare(TileBase x, TileBase y) {
                if (!x) return -1;
                if (!y) return 1;

                return x.name.CompareTo(y.name);
            }
        }
    }
}
