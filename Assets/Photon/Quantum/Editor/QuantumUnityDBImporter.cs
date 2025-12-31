namespace Quantum.Editor {
  using System;
  using System.Collections.Generic;
  using System.Diagnostics;
  using System.Linq;
  using UnityEditor;
  using UnityEditor.AssetImporters;
  using UnityEngine;
  using UnityEngine.Profiling;
  using UnityEngine.Serialization;
  using Debug = UnityEngine.Debug;
  using Object = UnityEngine.Object;

#if UNITY_6000_3_OR_NEWER
  using ObjectIdType = UnityEngine.EntityId;
  using HierarchyIteratorType = UnityEditor.HierarchyIterator;
#else 
  using ObjectIdType = System.Int32;
  using HierarchyIteratorType = UnityEditor.HierarchyProperty;
#endif
  
  [ScriptedImporter(6, Extension, importQueueOffset: ImportQueueOffset)]
  internal unsafe partial class QuantumUnityDBImporter : ScriptedImporter {
    public const int ImportQueueOffset = 200000;
    
    public const  string Extension              = "qunitydb";
    public const  string ExtensionWithDot       = ".qunitydb";

    private const string LogPrefix              = "[QuantumUnityDBImporter] ";
    private const string AssetObjectsDependency = "QuantumUnityDBImporterAssetObjectsDependency";
    
    /// <summary>
    /// If enabled, logs the time it took to import assets. 
    /// </summary>
    [InlineHelp] 
    public bool LogImportTimes = false;
    
#if (QUANTUM_ADDRESSABLES || QUANTUM_ENABLE_ADDRESSABLES) && !QUANTUM_DISABLE_ADDRESSABLES
    [InitializeOnLoadMethod]
    static void RegisterAddressableEventListeners() {
      AssetDatabaseUtils.AddAddressableAssetsWithLabelMonitor(QuantumUnityDBUtilities.AssetLabel, (hash) => {
        AddressablesDependency.Refresh();
      });
    }
#endif


    public override void OnImportAsset(AssetImportContext ctx) {

      if (!QuantumEditorSettings.TryGetGlobal(out var editorSettings)) {
        ctx.LogImportWarning($"{nameof(QuantumEditorSettings)} hasn't been created yet");
        return;
      }

      var rootFolder = editorSettings.GetAssetLookupRoot();
      
      var db = ScriptableObject.CreateInstance<QuantumUnityDB>();
      
      var sources = new List<(IQuantumAssetObjectSource, AssetGuid, string)>();

      var logTimingStopwatch = Stopwatch.StartNew();

      var factory = new QuantumAssetSourceFactory();

      Profiler.BeginSample("QuantumAssetDB");

      var scopeInfo = new ScopeContext(ctx);

      {
        Profiler.BeginSample("Discovering Scopes");

        List<string> scopeGuids = new();

        foreach (var it in AssetDatabaseUtils.IterateAssets(rootFolder, type: typeof(QuantumUnityDBScope))) {
          var scopePath = AssetDatabaseUtils.GetAssetPathOrThrow(it.GetObjectId());
          var importer = (QuantumUnityDBScopeImporter)AssetImporter.GetAtPath(scopePath);
          scopeInfo.AddScope(importer);
          scopeGuids.Add(it.guid);
          ctx.DependsOnArtifact(scopePath);
        }
        
        db.Scopes = scopeGuids.ToArray();
        Profiler.EndSample();
      }
      
      {
        Profiler.BeginSample("Iterating Assets");
        foreach (var it in QuantumUnityDBUtilities.IterateAssets(rootFolder)) {
          try {
            if (scopeInfo.Contains(it)) {
              QuantumEditorLog.TraceImport(AssetDatabaseUtils.GetAssetGuidOrThrow(it.GetObjectId()), $"Asset not added to the DB, already a part of a scope");
              continue;
            }
            
            var source = CreateAssetSource(factory, it.GetObjectId(), it.name, it.isMainRepresentation);
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
              ctx.LogImportError($"{LogPrefix}Failed to add asset {guid} ({path}) to Quantum DB: {ex}", source.EditorInstance);
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
      ctx.DependsOnCustomDependency(AssetObjectHashDependency.Name);
      ctx.DependsOnCustomDependency(AddressablesDependency.Name);
      QuantumUnityDBUtilities.AddAssetGuidOverridesDependency(ctx);
      
      ctx.SetMainObject(db);
    }

    internal static (IQuantumAssetObjectSource, AssetGuid, string) CreateAssetSource(QuantumAssetSourceFactory factory, ObjectIdType instanceID, string unityAssetName, bool isMain) {
      
      var (unityAssetGuid, fileId) = AssetDatabaseUtils.GetGUIDAndLocalFileIdentifierOrThrow(instanceID);
      
      var quantumAssetGuid = QuantumUnityDBUtilities.GetExpectedAssetGuid(new GUID(unityAssetGuid), fileId, out _);
      Debug.Assert(quantumAssetGuid.IsValid);
      
      var quantumAssetPath = QuantumUnityDBUtilities.GetExpectedAssetPath(instanceID, unityAssetName, isMain);
      Debug.Assert(!string.IsNullOrEmpty(quantumAssetPath));

      var context = new QuantumAssetSourceFactoryContext(unityAssetGuid, instanceID, unityAssetName, isMain);
      IQuantumAssetObjectSource source = factory.TryCreateAssetObjectSource(context);

      if (source == null) {
        QuantumEditorLog.ErrorImport($"No source found for asset {unityAssetName} ({unityAssetGuid})", new LazyLoadReference<Object>(instanceID).asset);
        return default;
      }

      return (source, quantumAssetGuid, quantumAssetPath);
    }

    internal static readonly QuantumCustomDependency AssetObjectHashDependency = new QuantumCustomDependency("QuantumUnityDBImporterAssetObjectsDependency", () => {
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
        var assetGuid = QuantumUnityDBUtilities.GetExpectedAssetGuid(it.GetObjectId(), out _);
        hash.Append(assetGuid);
      }

      // adding/removing/moving the scope should update the DB as well
      foreach (var it in AssetDatabaseUtils.IterateAssets(type: typeof(QuantumUnityDBScope))) {
        hash.Append(it.guid);
        hash.Append(AssetDatabase.GUIDToAssetPath(it.guid));
      }

      return hash;
    });

    internal static readonly QuantumCustomDependency AddressablesDependency = new QuantumCustomDependency("QuantumUnityDBImporterAddressablesDependency", () => {
#if QUANTUM_ENABLE_ADDRESSABLES && !QUANTUM_DISABLE_ADDRESSABLES
      var assetsSettings = UnityEditor.AddressableAssets.AddressableAssetSettingsDefaultObject.Settings;
      if (assetsSettings) {
        return assetsSettings.currentHash;
      }
#endif
      return default;
    });
    
    public static void RefreshAssetObjectHash(bool immediate) => AssetObjectHashDependency.Refresh(immediate);

    class ScopeContext {
      public ScopeContext(AssetImportContext context) {
        _ctx = context;
      }
      
      readonly AssetImportContext                _ctx;
      readonly List<ScopeEntry>                  _importers         = new();
      readonly List<QuantumUnityDBScopeImporter> _matchingImporters = new();
      
      public void AddScope(QuantumUnityDBScopeImporter importer) {
        HashSet<ObjectIdType> instances = null;
        HashSet<string> bundles = null;
        
        if (importer.ExplicitAssets?.Length > 0) {
          instances = new HashSet<ObjectIdType>();
          foreach (var asset in importer.ExplicitAssets) {
            instances.Add(asset.GetObjectId());
          }
        }

        if (importer.AssetBundles?.Length > 0) {
          foreach (var bundle in importer.AssetBundles) {
            if (string.IsNullOrEmpty(bundle)) {
              continue;
            }

            bundles ??= new HashSet<string>(StringComparer.Ordinal);
            bundles.Add(bundle);
          }
        }
        
        _importers.Add(new ScopeEntry() {
          Importer = importer,
          Bundles = bundles,
          Instances = instances,
          FolderWithTrailingSlash = importer.IncludeSubfolders ? PathUtils.Normalize(System.IO.Path.GetDirectoryName(importer.assetPath)) + '/' : null,
        });
      }
      
      public bool Contains(HierarchyIteratorType it) {
        string assetObjectPath = null;
        string bundleName = null;
        
        _matchingImporters.Clear();
        
        foreach (var entry in _importers) {
          if (entry.Instances?.Contains(it.GetObjectId()) == true) {
            _matchingImporters.Add(entry.Importer);
            continue;
          }

          if (entry.Bundles?.Contains(GetBundleName()) == true) {
            _matchingImporters.Add(entry.Importer);
            continue;
          }

          if (entry.FolderWithTrailingSlash != null && GetAssetPath().StartsWith(entry.FolderWithTrailingSlash, StringComparison.Ordinal)) {
            _matchingImporters.Add(entry.Importer);
            continue;
          }
        }
        
        if (_matchingImporters.Count == 0) {
          return false;
        }

        if (_matchingImporters.Count > 1) {
          foreach (var importer in _matchingImporters) {
            if (importer.IsUnique) {
              _ctx.LogImportWarning($"AssetObject {GetAssetPath()}{(it.isMainRepresentation ? "" : $" ({it.name})")} belongs to multiple scopes and at least one of them is marked as unique: {string.Join(", ", _matchingImporters.Select(x => x.assetPath))}");
              break;
            }
          }
        }

        return true;
        
        string GetAssetPath() {
          return assetObjectPath ??= AssetDatabaseUtils.GetAssetPathOrThrow(it.GetObjectId());
        }

        string GetBundleName() {
          return bundleName ??= AssetDatabase.GetImplicitAssetBundleName(GetAssetPath());
        }
      }
      
      struct ScopeEntry {
        public QuantumUnityDBScopeImporter Importer;
        public HashSet<ObjectIdType>       Instances;
        public HashSet<string>             Bundles;
        public string                      FolderWithTrailingSlash;
      }
    }
  }
}
