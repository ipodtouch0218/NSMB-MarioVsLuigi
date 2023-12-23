namespace Fusion.Editor {
  using System;
  using System.Collections;
  using System.Collections.Generic;
  using System.IO;
  using System.Linq;
  using UnityEditor;
  using UnityEditor.AssetImporters;
  using UnityEditor.PackageManager;
  using UnityEngine;

  [ScriptedImporter(3, ExtensionWithoutDot, ImportQueueOffset)]
  public class NetworkProjectConfigImporter : ScriptedImporter {
    public const string ExtensionWithoutDot = "fusion";
    public const string Extension = "." + ExtensionWithoutDot;
    public const int ImportQueueOffset = 1000;
    
    public const string FusionPrefabTag            = "FusionPrefab";
    public const string FusionPrefabTagSearchTerm  = "l:FusionPrefab";
    public const string ScriptOrderDependencyName  = "Fusion.ScriptOrderDependency";
    public const string AddressablesDependencyName = "Fusion.AddressablesDependency";
    public const string PrefabsDependencyName      = "Fusion.PrefabsDependency";

    [Header("Prefabs")]
    [DrawInline]
    public NetworkPrefabTableOptions PrefabOptions;
    
#if FUSION_ENABLE_ADDRESSABLES && !FUSION_DISABLE_ADDRESSABLES
    [InitializeOnLoadMethod]
    static void RegisterAddressableEventListeners() {
      AssetDatabaseUtils.AddAddressableAssetsWithLabelMonitor(FusionPrefabTag, (hash) => {
        AssetDatabase.RegisterCustomDependency(AddressablesDependencyName, hash);
      });
    }
#endif

    public override void OnImportAsset(AssetImportContext ctx) {
      FusionEditorLog.TraceImport(ctx.assetPath, "Staring scripted import");

      NetworkProjectConfig.UnloadGlobal();
      NetworkProjectConfig config = LoadConfigFromFile(ctx.assetPath);

      var root = ScriptableObject.CreateInstance<NetworkProjectConfigAsset>();
      root.Config = config;
      ctx.AddObjectToAsset("root", root);
      
      root.Prefabs = DiscoverPrefabs(ctx);
      root.BehaviourMeta = CreateBehaviourMeta(ctx);
      root.PrefabOptions = PrefabOptions;
      
      ctx.DependsOnCustomDependency(AddressablesDependencyName);
      ctx.DependsOnCustomDependency(ScriptOrderDependencyName);
      ctx.DependsOnCustomDependency(PrefabsDependencyName);
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

    private static List<INetworkPrefabSource> DiscoverPrefabs(AssetImportContext ctx) {
      var result = new List<INetworkPrefabSource>();

      var factory = new NetworkAssetSourceFactory();
      var detailsLog = new System.Text.StringBuilder();
      var paths = new List<string>();

      foreach (var it in AssetDatabaseUtils.IterateAssets<GameObject>(label: FusionPrefabTag)) {
        var prefabPath = AssetDatabase.GetAssetPath(it.instanceID);
        var context    = new NetworkAssetSourceFactoryContext(it);

        INetworkPrefabSource source = factory.TryCreatePrefabSource(context);

        if (source == null) {
          ctx.LogImportError($"Unable to create prefab asset for {AssetDatabase.GetAssetPath(it.instanceID)} ({it.guid})");
          continue;
        }

#if FUSION_EDITOR_TRACE
        detailsLog.AppendLine($"{assetPath} -> {((INetworkPrefabSource)prefabSource).EditorSummary}");
#endif
        
        var index = paths.BinarySearch(prefabPath, StringComparer.Ordinal);
        if (index < 0) {
          index = ~index;
        } else {
          ctx.LogImportWarning($"Prefab with path {prefabPath} already added");
        }

        paths.Insert(index, prefabPath);
        result.Insert(index, source);
      }

      FusionEditorLog.TraceImport($"Discover prefabs details [{result.Count}] :\n{detailsLog}");
      return result;
    }

    private NetworkProjectConfigAsset.SerializableSimulationBehaviourMeta[] CreateBehaviourMeta(AssetImportContext ctx) {
      var result = new List<NetworkProjectConfigAsset.SerializableSimulationBehaviourMeta>();

      foreach (var monoScript in MonoImporter.GetAllRuntimeMonoScripts()) {
        var scriptType = monoScript.GetClass();
        if (scriptType?.IsSubclassOf(typeof(SimulationBehaviour)) != true) {
          continue;
        }

        var executionOrder = MonoImporter.GetExecutionOrder(monoScript);
        if (executionOrder == 0) {
          // no need to add it to the list
          continue;
        }

        result.Add(new() {
          Type = scriptType,
          ExecutionOrder = executionOrder
        });
      }

      return result.OrderBy(x => x.ExecutionOrder).ToArray();
    }

    class Postprocessor : AssetPostprocessor {
      static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths) {
        foreach (var path in deletedAssets) {
          if (path.EndsWith(Extension, StringComparison.OrdinalIgnoreCase)) {
            NetworkProjectConfig.UnloadGlobal();
            break;
          }
        }

        foreach (var path in movedAssets) {
          if (path.EndsWith(Extension, StringComparison.OrdinalIgnoreCase)) {
            NetworkProjectConfig.UnloadGlobal();
            break;
          }
        }

        foreach (var path in importedAssets) {
          if (HasSimulationBehaviours(path)) {
            EditorApplication.delayCall -= RefreshScriptOrderDependencyHash;
            EditorApplication.delayCall += RefreshScriptOrderDependencyHash;
            break;
          }
        }
      }

      private static bool HasSimulationBehaviours(string path) {
        if (path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)) {
          // check if there is MB in there (with MonoImporter) and if it is a simulation behaviour
          var importer = AssetImporter.GetAtPath(path) as MonoImporter;
          if (importer == null) {
            return false;
          }

          var scriptType = importer.GetScript()?.GetClass();
          if (scriptType?.IsSubclassOf(typeof(SimulationBehaviour)) != true) {
            return false;
          }

          return true;
        }

        if (path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)) {
          // check if there is MB in there (with MonoImporter) and if it is a simulation behaviour
          foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path)) {
            if (asset is MonoScript monoScript) {
              var scriptType = monoScript.GetClass();
              if (scriptType?.IsSubclassOf(typeof(SimulationBehaviour)) == true) {
                return true;
              }
            }
          }

          return false;
        }

        return false;
      }

      private static void RefreshScriptOrderDependencyHash() {
        var hash = CalculateScriptOrderDependencyHash();
        FusionEditorLog.TraceImport($"Refreshing {ScriptOrderDependencyName} dependency hash: {hash}");
        AssetDatabase.RegisterCustomDependency(ScriptOrderDependencyName, hash);
        AssetDatabase.Refresh();
      }

      private static Hash128 CalculateScriptOrderDependencyHash() {
        var hash = new Hash128();

        var scripts = MonoImporter.GetAllRuntimeMonoScripts();

        foreach (var monoScript in scripts) {
          var scriptType = monoScript.GetClass();
          if (scriptType?.IsSubclassOf(typeof(SimulationBehaviour)) != true) {
            continue;
          }

          var executionOrder = MonoImporter.GetExecutionOrder(monoScript);
          hash.Append(executionOrder);
        }

        return hash;
      }
    }

    public static void RefreshNetworkObjectPrefabHash() {
      var hash = new Hash128();
      
      foreach (var it in AssetDatabaseUtils.IterateAssets<GameObject>(label: FusionPrefabTag)) {
        hash.Append(it.guid);
      }
      
      FusionEditorLog.TraceImport($"Refreshing {PrefabsDependencyName} dependency hash: {hash}");
      AssetDatabase.RegisterCustomDependency(PrefabsDependencyName, hash);
      AssetDatabase.Refresh();
    }
  }
}
