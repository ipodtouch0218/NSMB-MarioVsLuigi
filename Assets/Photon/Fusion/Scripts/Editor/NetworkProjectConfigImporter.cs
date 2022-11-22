namespace Fusion.Editor {
  using System;
  using System.Collections.Generic;
  using System.IO;
  using System.Linq;
  using UnityEditor;
  using UnityEditor.AssetImporters;
  using UnityEngine;

  [ScriptedImporter(1, ExtensionWithoutDot, ImportQueueOffset)]
  public class NetworkProjectConfigImporter : ScriptedImporter {

    public const string ExtensionWithoutDot = "fusion";
    public const string Extension = "." + ExtensionWithoutDot;
    public const int ImportQueueOffset = 1000;

    public string PrefabAssetsContainerPath = string.Empty;
    public const string FusionPrefabTag = "FusionPrefab";
    public const string FusionPrefabTagSearchTerm = "l:FusionPrefab";


    const string MissingPrefabPrefix = "~MISSING~";

    public override void OnImportAsset(AssetImportContext ctx) {

      FusionEditorLog.TraceImport(assetPath, "Staring scripted import");

      NetworkProjectConfig.UnloadGlobal();
      NetworkProjectConfig config = LoadConfigFromFile(ctx.assetPath);

      var root = ScriptableObject.CreateInstance<NetworkProjectConfigAsset>();
      root.Config = config;
      ctx.AddObjectToAsset("root", root);

      root.Prefabs = DiscoverPrefabs(ctx);
      RefreshPrefabAssets(root);
    }

    public static NetworkProjectConfig LoadConfigFromFile(string path) {
      var config = new NetworkProjectConfig();
      try {
        var text = File.ReadAllText(path);
        if (string.IsNullOrWhiteSpace(text)) {
          throw new System.ArgumentException("Empty string");
        }
        EditorJsonUtility.FromJsonOverwrite(text, config);
      } catch (System.ArgumentException ex) {
        throw new System.ArgumentException($"Failed to parse {path}: {ex.Message}");
      }
      return config;
    }

    private void RefreshPrefabAssets(NetworkProjectConfigAsset config) {
#if !FUSION_UNITY_DISABLE_PREFAB_ASSETS
      if (string.IsNullOrEmpty(PrefabAssetsContainerPath)) {
        return;
      }

      bool hadChanges = false;

      var mainAsset = AssetDatabase.LoadMainAssetAtPath(PrefabAssetsContainerPath);
      var root = mainAsset as NetworkPrefabAssetCollection;

      var existingPrefabs = AssetDatabase.LoadAllAssetsAtPath(PrefabAssetsContainerPath)
        .OfType<NetworkPrefabAsset>()
        .ToList();

      var remainingPrefabs = config.Prefabs
        .GroupBy(x => x.AssetGuid)
        .ToDictionary(x => x.Key, x => x.First());

      if (mainAsset == null) {
        root = ScriptableObject.CreateInstance<NetworkPrefabAssetCollection>();
        root.name = Path.GetFileNameWithoutExtension(PrefabAssetsContainerPath);
        AssetDatabase.CreateAsset(root, PrefabAssetsContainerPath);
        hadChanges = true;
      } else if (root == null) {
        FusionEditorLog.WarnImport($"{nameof(PrefabAssetsContainerPath)} needs to point to an asset that is not {typeof(NetworkPrefabAssetCollection)} type: {mainAsset.GetType()}");
        return;
      }

      foreach (var existingPrefab in existingPrefabs) {
        if (remainingPrefabs.TryGetValue(existingPrefab.AssetGuid, out var source)) {
          // keep
          remainingPrefabs.Remove(existingPrefab.AssetGuid);
          var asset = AssetDatabaseUtils.SetScriptableObjectType<NetworkPrefabAsset>(existingPrefab);
          if (asset.name != source.name) {
            FusionEditorLog.TraceImport(PrefabAssetsContainerPath, $"Noticed renamed or restored: {asset.name}");
            hadChanges = true;
            asset.name = source.name;
          }
        } else {
          // remove
          var asset = AssetDatabaseUtils.SetScriptableObjectType<NetworkPrefabAssetMissing>(existingPrefab);
          if (!asset.name.StartsWith(MissingPrefabPrefix)) {
            FusionEditorLog.TraceImport(PrefabAssetsContainerPath, $"Noticed missing: {asset.name}");
            asset.name = MissingPrefabPrefix + asset.name;
            hadChanges = true;
          }
        }
      }

      foreach (var source in remainingPrefabs) {
        // new
        var asset = ScriptableObject.CreateInstance<NetworkPrefabAsset>();
        asset.name = source.Value.name;
        asset.AssetGuid = source.Key;
        AssetDatabase.AddObjectToAsset(asset, PrefabAssetsContainerPath);

        FusionEditorLog.TraceImport(PrefabAssetsContainerPath, $"Noticed new {asset.name}");
        hadChanges = true;
      }

      if (hadChanges) {
        AssetDatabase.SaveAssets();
      }
#endif
    }

    private NetworkPrefabSourceUnityBase[] DiscoverPrefabs(AssetImportContext ctx) {

      var result = new List<NetworkPrefabSourceUnityBase>();

      var guids = AssetDatabase.FindAssets($"l:{FusionPrefabTag} t:prefab");
      FusionEditorLog.TraceImport(assetPath, $"Found {guids.Length} assets marked with {FusionPrefabTag} label");

      var detailsLog = new System.Text.StringBuilder();
      var paths = new List<string>();

      foreach (var guid in guids) {
        var assetPath = AssetDatabase.GUIDToAssetPath(guid);

        NetworkPrefabSourceUnityBase prefabSource;
        try {
          prefabSource = NetworkPrefabSourceFactory.Create(assetPath);
#if FUSION_EDITOR_TRACE
          detailsLog.AppendLine($"{assetPath} -> {((INetworkPrefabSource)prefabSource).EditorSummary}");
#endif
        } catch (Exception ex) {
          ctx.LogImportWarning($"Unable to create prefab asset for {assetPath}: {ex}");
          continue;
        }

        prefabSource.name = Path.GetFileNameWithoutExtension(assetPath);
        prefabSource.hideFlags = HideFlags.HideInHierarchy;
        prefabSource.AssetGuid = NetworkObjectGuid.Parse(guid);

        ctx.DependsOnSourceAsset(assetPath);
        ctx.AddObjectToAsset(guid, prefabSource);

        var index = paths.BinarySearch(assetPath);
        if (index < 0) {
          index = ~index;
        } else {
          ctx.LogImportWarning($"Prefab with path {assetPath} already added");
        }
        paths.Insert(index, assetPath);
        result.Insert(index, prefabSource);
      }

      FusionEditorLog.TraceImport($"Discover prefabs details [{result.Count}] :\n{detailsLog}");
      return result.ToArray();
    }

    class Postprocessor : AssetPostprocessor {
      static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths) {

        foreach (var path in deletedAssets) {
          if (path.EndsWith(Extension)) {
            NetworkProjectConfig.UnloadGlobal();
            break;
          }
        }
        foreach (var path in movedAssets) {
          if (path.EndsWith(Extension)) {
            NetworkProjectConfig.UnloadGlobal();
            break;
          }
        }
      }
    }
  }
}