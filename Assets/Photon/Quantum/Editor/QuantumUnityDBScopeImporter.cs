namespace Quantum.Editor {
  using System;
  using System.Collections.Generic;
  using System.Diagnostics;
  using System.IO;
  using UnityEditor;
  using UnityEditor.AssetImporters;
  using UnityEngine;
  using UnityEngine.Profiling;
  
#if UNITY_6000_3_OR_NEWER
  using ObjectIdType = UnityEngine.EntityId;
  using HierarchyIteratorType = UnityEditor.HierarchyIterator;
#else 
  using ObjectIdType = System.Int32;
  using HierarchyIteratorType = UnityEditor.HierarchyProperty;
#endif

  [ScriptedImporter(6, Extension, importQueueOffset: QuantumUnityDBImporter.ImportQueueOffset - 1)]
  internal unsafe partial class QuantumUnityDBScopeImporter : ScriptedImporter {
    public const  string Extension              = "qunitydbscope";
    public const  string ExtensionWithDot       = ".qunitydbscope";

    const string LogPrefix              = "[QuantumUnityDBScopeImporter] ";

    /// <summary>
    /// If enabled, performs an additional step during import to verify the imported assets have correct GUIDs and paths.
    /// </summary>
    [InlineHelp]
    public bool Verify = false;
    
    /// <summary>
    /// If enabled, logs the time it took to import assets. 
    /// </summary>
    [InlineHelp] 
    public bool LogImportTimes = false;

    /// <summary>
    /// If true, <see cref="QuantumUnityDB"/> will emit an edit-time warning if there are assets that also exists in other scopes.
    /// </summary>
    [InlineHelp]
    public bool IsUnique = true;
    
    /// <summary>
    /// If true, include all <see cref="AssetObject"/> in the current folder and all the subfolders.
    /// </summary>
    [InlineHelp]
    public bool IncludeSubfolders = true;

    /// <summary>
    /// Include <see cref="AssetObject"/> being a part of specific Asset Bundles.
    /// </summary>
    [InlineHelp]
    public string[] AssetBundles;

    /// <summary>
    /// Assets included explicitly.
    /// </summary>
    [InlineHelp]
    public LazyLoadReference<AssetObject>[] ExplicitAssets;
    
    public override void OnImportAsset(AssetImportContext ctx) {

      if (!QuantumEditorSettings.TryGetGlobal(out _)) {
        ctx.LogImportWarning($"{nameof(QuantumEditorSettings)} hasn't been created yet");
        return;
      }
      
      var db = ScriptableObject.CreateInstance<QuantumUnityDBScope>();
      db.Id = AssetDatabase.AssetPathToGUID(ctx.assetPath);

      var folder = Path.GetDirectoryName(ctx.assetPath);
      
      var sources = new List<(IQuantumAssetObjectSource, AssetGuid, string)>();

      var logTimingStopwatch = Stopwatch.StartNew();

      var factory = new QuantumAssetSourceFactory();
      var assets = new HashSet<int>();

      Profiler.BeginSample("QuantumAssetDB"); 

      if (IncludeSubfolders) {
        Profiler.BeginSample("Iterating Assets");
        foreach (var it in QuantumUnityDBUtilities.IterateAssets(folder)) {
          if (!assets.Add(it.GetObjectId())) {
            continue;
          }
          try {
            var source = QuantumUnityDBImporter.CreateAssetSource(factory, it.GetObjectId(), it.name, it.isMainRepresentation);
            if (source != default) {
              sources.Add(source);
            }
          } catch (Exception ex) {
            ctx.LogImportError($"Failed to create asset source for {it.name} ({it.guid}): {ex.Message}");
          }
        }
        Profiler.EndSample();
      }
      
      if (AssetBundles?.Length > 0) {
        Profiler.BeginSample("Asset Bundles");
        foreach (var assetBundle in AssetBundles) {
          foreach (var path in AssetDatabase.GetAssetPathsFromAssetBundle(assetBundle)) {
            foreach (var it in QuantumUnityDBUtilities.IterateAssets(path)) {
              if (!assets.Add(it.GetObjectId())) {
                continue;
              }
              try {
                var source = QuantumUnityDBImporter.CreateAssetSource(factory, it.GetObjectId(), it.name, it.isMainRepresentation);
                if (source != default) {
                  sources.Add(source);
                }
              } catch (Exception ex) {
                ctx.LogImportError($"Failed to create asset source for {it.name} ({it.guid}): {ex.Message}");
              }
            }
          }
        }
        Profiler.EndSample();
      }

      if (ExplicitAssets?.Length > 0) {
        Profiler.BeginSample("Explicit Assets");
        int index = 0;
        foreach (var asset in ExplicitAssets) {
          if (!asset.isSet || asset.isBroken) {
            ctx.LogImportWarning($"Invalid asset reference at {index}: null or broken ({asset})");
            continue;
          }
          
          var path = AssetDatabaseUtils.GetAssetPathOrThrow(asset.GetObjectId());
          if (!AssetDatabaseUtils.HasLabel(path, QuantumUnityDBUtilities.AssetLabel)) {
            ctx.LogImportWarning($"Invalid asset reference at {index}: is not marked with {QuantumUnityDBUtilities.AssetLabel} label ({asset})");
            continue;
          }

          var isMainAsset = AssetDatabase.IsMainAsset(asset.GetObjectId());
          
          try {
            // TODO: getting an asset name here is suboptimal, but not sure how to get it otherwise
            var source = QuantumUnityDBImporter.CreateAssetSource(factory, asset.GetObjectId(), asset.asset.name, isMainAsset);
            if (source != default) {
              sources.Add(source);
            }
          } catch (Exception ex) {
            ctx.LogImportError($"Failed to create asset source for {asset}: {ex.Message}");
          }
        }
        Profiler.EndSample();
      }
      
      {
        Profiler.BeginSample("Sorting Assets");
        sources.Sort((x, y) => string.CompareOrdinal(x.Item3, y.Item3));
        Profiler.EndSample();
      }

      {
        Profiler.BeginSample("Adding Assets");
        foreach (var (source, guid, path) in sources) {
          if ((guid.Value & AssetGuid.ReservedBits) != 0) {
            ctx.LogImportError($"{LogPrefix}Failed to import asset {guid} ({path}): GUID uses reserved bits");
            continue;
          }
          db.AddSource(source, guid, path);
        }
        Profiler.EndSample();
      }

      Profiler.EndSample();

      if (LogImportTimes) {
        QuantumEditorLog.Log($"{LogPrefix}Imported {sources.Count} assets in {logTimingStopwatch.Elapsed}");
      }

      ctx.AddObjectToAsset("root", db);
      ctx.DependsOnCustomDependency(QuantumUnityDBImporter.AssetObjectHashDependency.Name);
      ctx.DependsOnCustomDependency(QuantumUnityDBImporter.AddressablesDependency.Name);
      QuantumUnityDBUtilities.AddAssetGuidOverridesDependency(ctx);
      
      ctx.SetMainObject(db);
    }
  }
}

