namespace Quantum.Editor {
  using System.IO;
  using UnityEditor;
  using UnityEditor.AssetImporters;
  using UnityEngine;


  [ScriptedImporter(16, Extension, 100000)]
  public partial class QuantumEntityPrototypeAssetObjectImporter : ScriptedImporter {
    public const string Extension = "qprototype";
    public const string ExtensionWithDot = ".qprototype";
    public const string Suffix = "EntityPrototype";

    const long AssetFileId = 3097001405596171208;

    public static string GetPathForPrefab(string prefabPath) {
      var directory = Path.GetDirectoryName(prefabPath) ?? string.Empty;
      var prefabName = Path.GetFileNameWithoutExtension(prefabPath);
      return PathUtils.Normalize(Path.Combine(directory, prefabName + "EntityPrototype" + QuantumEntityPrototypeAssetObjectImporter.ExtensionWithDot));
    }
    
    public override void OnImportAsset(AssetImportContext ctx) {
      
      QuantumEditorLog.TraceImport($"Importing {ctx.assetPath}");
      
      var path = ctx.assetPath;

      var prefabGuid = File.ReadAllText(path);
      var prefabPath = AssetDatabase.GUIDToAssetPath(prefabGuid);
      
      // following are the checks to make sure the asset moves/destroys itself; this could be done perhaps
      // more correctly by using AssetPostprocessor, but that would require a lot of code
      
      var prefab = string.IsNullOrEmpty(prefabPath) ? null : AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
      
      if (!prefab) {
        QuantumEditorLog.TraceImport(ctx.assetPath, $"Not importing, prefab {prefabGuid} not found or failed to load");
        return;
      }
      
      if (PrefabUtility.GetPrefabAssetType(prefab) == PrefabAssetType.Variant) {
        // ok variant needs to trace back to the original prefab
        Object source = prefab;
        while ((source = PrefabUtility.GetCorrespondingObjectFromSource(source)) != null) {
          var sourcePath = AssetDatabase.GetAssetPath(source);
          QuantumEditorLog.Assert(!string.IsNullOrEmpty(sourcePath));
          QuantumEditorLog.TraceImport(ctx.assetPath, $"Prefab {prefabGuid} is a variant, tracing back to source {sourcePath}");
          ctx.DependsOnSourceAsset(sourcePath);
        }
      }
      
      ctx.DependsOnSourceAsset(new GUID(prefabGuid));
      ctx.DependsOnSourceAsset(prefabPath);
      ctx.DependsOnArtifact(new GUID(prefabGuid));
      
      QuantumUnityDBUtilities.AddAssetGuidOverridesDependency(ctx);
      
      if (!prefab.TryGetComponent(out QuantumEntityPrototype component)) {
        QuantumEditorLog.TraceImport(ctx.assetPath, $"Not importing, prefab {prefabGuid} does not have a {nameof(QuantumEntityPrototype)} component");
        return;
      }

      // create root object
      var selfViewAsset = AssetDatabase.LoadAssetAtPath<Quantum.EntityView>(prefabPath);
      
      var guid = AssetDatabaseUtils.GetAssetGuidOrThrow(ctx.assetPath);
      var asset = ScriptableObject.CreateInstance<Quantum.EntityPrototype>();
      asset.name = prefab.name + Suffix;

      bool isOverride = false;
      var assetGuid = QuantumUnityDBUtilities.RemoveAssetGuidOverride(new GUID(prefabGuid), -325511733217504505);
      if (assetGuid.IsValid) {
        QuantumEditorLog.LogImport(path, $"Using 3.0 early guid override: {assetGuid}");
        isOverride = true;
        EditorApplication.delayCall += () => {
          // can only update overrides once the asset is imported
          var asset = AssetDatabase.LoadAssetAtPath<Quantum.EntityPrototype>(path);
          if (asset) {
            QuantumUnityDBUtilities.SetAssetGuidOverride(asset, assetGuid);
          }
        };
      } else {
        assetGuid = QuantumUnityDBUtilities.GetExpectedAssetGuid(new GUID(guid), AssetFileId, out isOverride);
      }

      asset.Guid = assetGuid;
      asset.Path = QuantumUnityDBUtilities.GetExpectedAssetPath(path, asset.name, true);
      
      component.InitializeAssetObject(asset, selfViewAsset);
      ctx.AddObjectToAsset(Suffix, asset);
    }
  }
}