using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

using Fusion;

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

            GameManager gm = Object.FindObjectOfType<GameManager>();

            // No GameManager = Not in a level scene.
            if (!gm || !gm.tilemap)
                return;

            Debug.Log("Handling saving of a gameplay scene at " + path);
            Tilemap tilemap = gm.tilemap;

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
            foreach (IHaveTileDependencies td in Object.FindObjectsOfType<MonoBehaviour>().OfType<IHaveTileDependencies>()) {
                GetDependenciesRecursively(td, uniqueTilesList, checkedForDependencies);
            }
            uniqueTilesList = uniqueTilesList.Distinct().ToList();
            uniqueTilesList.Sort(new TileBaseSorter());

            int chunkmapWidth = Mathf.CeilToInt(gm.levelWidthTile / 16f);
            int chunkmapHeight = Mathf.CeilToInt(gm.levelHeightTile / 16f);
            int requiredChunks = chunkmapWidth * chunkmapHeight;

            Transform parent = gm.transform.Find("Chunks");
            List<TilemapChunk> chunks = new(gm.GetComponentsInChildren<TilemapChunk>());
            while (chunks.Count < requiredChunks) {
                GameObject newObject = new();
                newObject.transform.SetParent(parent);
                TilemapChunk newChunk = newObject.AddComponent<TilemapChunk>();
                newObject.AddComponent<NetworkObject>();
                chunks.Add(newChunk);
            }

            for (int i = 0; i < requiredChunks; i++) {
                TilemapChunk chunk = chunks[i];
                ushort x = (ushort) (i % chunkmapWidth);
                ushort y = (ushort) (i / chunkmapWidth);

                chunk.name = $"TilemapChunk ({x},{y})";
                chunk.chunkX = x;
                chunk.chunkY = y;
                chunk.LoadState();
            }


            gm.tileManager.sceneTiles = uniqueTilesList.ToArray();
            gm.tileManager.chunks = chunks.ToArray();

            Debug.Log($"Successfully saved level data. {uniqueTilesList.Count} unique tiles, {requiredChunks} chunks ({gm.levelWidthTile} x {gm.levelHeightTile}).");
            EditorUtility.SetDirty(gm.tileManager);
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
                if (!x) return 1;
                if (!y) return -1;
                if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(x, out string xGuid, out long _) &&
                    AssetDatabase.TryGetGUIDAndLocalFileIdentifier(y, out string yGuid, out long _)) {

                    return xGuid.CompareTo(yGuid);
                }
                return x.name.CompareTo(y.name);
            }
        }
    }
}
