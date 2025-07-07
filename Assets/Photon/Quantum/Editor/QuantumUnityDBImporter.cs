namespace Quantum.Editor {
  using System;
  using System.Collections.Generic;
  using System.Diagnostics;
  using UnityEditor;
  using UnityEditor.AssetImporters;
  using UnityEngine;
  using UnityEngine.Profiling;
  using UnityEngine.Serialization;
  using Debug = UnityEngine.Debug;

  [ScriptedImporter(6, Extension, importQueueOffset: 200000)]
  internal unsafe partial class QuantumUnityDBImporter : ScriptedImporter {
    public const  string Extension              = "qunitydb";
    public const  string ExtensionWithDot       = ".qunitydb";

    private const string LogPrefix              = "[QuantumUnityDBImporter] ";
    private const string AddressablesDependency = "QuantumUnityDBImporterAddressablesDependency";
    private const string AssetObjectsDependency = "QuantumUnityDBImporterAssetObjectsDependency";

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
    
#if (QUANTUM_ADDRESSABLES || QUANTUM_ENABLE_ADDRESSABLES) && !QUANTUM_DISABLE_ADDRESSABLES
    [InitializeOnLoadMethod]
    static void RegisterAddressableEventListeners() {
      AssetDatabaseUtils.AddAddressableAssetsWithLabelMonitor(QuantumUnityDBUtilities.AssetLabel, (hash) => {
        AssetDatabaseUtils.RegisterCustomDependencyWithMppmWorkaround(AddressablesDependency, hash);
      });
    }
#endif


    public override void OnImportAsset(AssetImportContext ctx) {

      if (!QuantumEditorSettings.TryGetGlobal(out var editorSettings)) {
        ctx.LogImportWarning($"{nameof(QuantumEditorSettings)} hasn't been created yet");
        return;
      }

      var db = ScriptableObject.CreateInstance<QuantumUnityDB>();
      
      var sources = new List<(IQuantumAssetObjectSource, AssetGuid, string)>();

      var logTimingStopwatch = Stopwatch.StartNew();

      var factory = new QuantumAssetSourceFactory();

      Profiler.BeginSample("QuantumAssetDB"); 

      {
        Profiler.BeginSample("Iterating Assets");
        foreach (HierarchyProperty it in QuantumUnityDBUtilities.IterateAssets()) {
          try {
            var source = CreateAssetSource(factory, it.instanceID, it.name, it.isMainRepresentation);
            if (source != default) {
              sources.Add(source);
            }
          } catch (Exception ex) {
            ctx.LogImportError($"Failed to create asset source for {it.name} ({it.guid}): {ex.Message}");
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
          
          var existingSource = db.GetAssetSource(guid);
          if (existingSource != null) {
            var sourceInstance = source.EditorInstance;
            var otherInstance  = db.GetAssetSource(guid)?.EditorInstance;
            Debug.Assert(sourceInstance != null, $"{nameof(sourceInstance)} != null for {guid} {path}");
            Debug.Assert(otherInstance != null, $"{nameof(otherInstance)} != null for {guid} {path}");
            ctx.LogImportWarning($"{LogPrefix}Duplicate asset GUID {guid} found in {source.EditorInstance.name} ({AssetDatabase.GetAssetPath(sourceInstance)}) and {otherInstance.name} ({AssetDatabase.GetAssetPath(otherInstance)}). " +
              $"If GUID override is used, consider disabling it for one of the assets or assign a new, unique value.", sourceInstance);
          } else {
            try {
              db.AddSource(source, guid, path);  
            } catch (Exception ex) {
              ctx.LogImportError($"{LogPrefix}Failed to add asset {guid} ({path}) to Quantum DB: {ex.Message}", source.EditorInstance);
            }
            
          }
        }
        Profiler.EndSample();
      }

      Profiler.EndSample();

      if (LogImportTimes) {
        QuantumEditorLog.Log($"{LogPrefix}Imported {sources.Count} assets in {logTimingStopwatch.Elapsed}");
      }

      ctx.AddObjectToAsset("root", db);
      ctx.DependsOnCustomDependency(AssetObjectsDependency);
      ctx.DependsOnCustomDependency(AddressablesDependency);
      QuantumUnityDBUtilities.AddAssetGuidOverridesDependency(ctx);
      
      ctx.SetMainObject(db);
    }

    private (IQuantumAssetObjectSource, AssetGuid, string) CreateAssetSource(QuantumAssetSourceFactory factory, int instanceID, string unityAssetName, bool isMain) {
      
      var (unityAssetGuid, fileId) = AssetDatabaseUtils.GetGUIDAndLocalFileIdentifierOrThrow(instanceID);
      
      var quantumAssetGuid = QuantumUnityDBUtilities.GetExpectedAssetGuid(new GUID(unityAssetGuid), fileId, out _);
      Debug.Assert(quantumAssetGuid.IsValid);
      
      var quantumAssetPath = QuantumUnityDBUtilities.GetExpectedAssetPath(instanceID, unityAssetName, isMain);
      Debug.Assert(!string.IsNullOrEmpty(quantumAssetPath));

      IQuantumAssetObjectSource source = null;

      var context = new QuantumAssetSourceFactoryContext(unityAssetGuid, instanceID, unityAssetName, isMain);
      source = factory.TryCreateAssetObjectSource(context);

      if (source == null) {
        QuantumEditorLog.ErrorImport($"No source found for asset {unityAssetName} ({unityAssetGuid})", EditorUtility.InstanceIDToObject(instanceID));
        return default;
      }

      if (Verify) {
        var asset = EditorUtility.InstanceIDToObject(instanceID);
        if (asset == null) {
          QuantumEditorLog.WarnImport($"Asset {unityAssetName} ({unityAssetGuid}) is null");
        } else {

          if (asset.name != unityAssetName) {
            QuantumEditorLog.WarnImport($"Asset name mismatch for {AssetDatabase.GetAssetPath(asset)}. Expected {unityAssetName}, got {asset.name}", asset);
          }

          var assetObject = asset as Quantum.AssetObject;
          if (!assetObject) {
            QuantumEditorLog.WarnImport($"Asset {AssetDatabase.GetAssetPath(asset)} is not an instance of {nameof(Quantum.AssetObject)}");
          } else {
            if (assetObject.Guid != quantumAssetGuid) {
              //QuantumEditorLog.WarnImport($"Asset GUID mismatch for {AssetDatabase.GetAssetPath(asset)}. Expected {quantumAssetGuid}, got {assetObject.Guid}", asset);
            }

            if (assetObject.Path != quantumAssetPath) {
              QuantumEditorLog.WarnImport($"Asset path mismatch for {AssetDatabase.GetAssetPath(asset)}. Expected {quantumAssetPath}, got {assetObject.Path}", asset);
            }

            if (source.AssetType != (Type)null && source.AssetType != asset.GetType()) {
              QuantumEditorLog.WarnImport($"Asset type mismatch for {AssetDatabase.GetAssetPath(asset)}. Expected {source.AssetType}, got {asset.GetType()}", asset);
            }
          }
        }
      }

      return (source, quantumAssetGuid, quantumAssetPath);
    }
    
    public static void RefreshAssetObjectHash() {
      var sw = Stopwatch.StartNew();
      
      var hash = new Hash128();
      foreach (var it in QuantumUnityDBUtilities.IterateAssets()) {
        // any new/deleted asset should alter the hash right here
        hash.Append(it.guid);
        // so does moving...
        hash.Append(AssetDatabase.GUIDToAssetPath(it.guid));
        // ... and renaming, if this is a nested asset
        if (!it.isMainRepresentation) {
          hash.Append(it.name);
        }
        // any changes to asset's guid affects the hash
        var assetGuid = QuantumUnityDBUtilities.GetExpectedAssetGuid(it.instanceID, out _);
        hash.Append(assetGuid);
      }
      
      QuantumEditorLog.TraceImport($"Refreshing {AssetObjectsDependency} dependency hash: {hash} (took: {sw.Elapsed}");
      AssetDatabaseUtils.RegisterCustomDependencyWithMppmWorkaround(AssetObjectsDependency, hash);
    }

  }
}
