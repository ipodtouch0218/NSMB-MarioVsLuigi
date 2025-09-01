namespace Quantum {
  using System;
  using System.Collections.Generic;
  using Editor;
  using JetBrains.Annotations;
  using UnityEditor;
  using UnityEngine;
  using UnityEngine.Serialization;

  /// <summary>
  /// Contains global Quantum editor settings.
  /// </summary>
  [CreateAssetMenu(menuName = "Quantum/Configurations/EditorSettings", fileName = "QuantumEditorSettings", order = EditorDefines.AssetMenuPriorityConfigurations + 30)]
  [QuantumGlobalScriptableObject(DefaultPath)]
  public class QuantumEditorSettings : QuantumGlobalScriptableObject<QuantumEditorSettings> {
    /// <summary>
    /// The default path of the global Quantum editor settings asset.
    /// </summary>
    public const   string DefaultPath                 = "Assets/QuantumUser/Editor/QuantumEditorSettings.asset";
    internal const string AssetGuidOverrideDependency = "QuantumUnityDBUtilitiesAssetGuidOverrideDependency";
    
    /// <summary>
    /// Get the global Quantum editor settings instance and run the provided action and return result of a certain type.
    /// </summary>
    /// <typeparam name="T">Type to return</typeparam>
    /// <param name="check">The func to run on the settings.</param>
    /// <returns>Returns the result of the func or default T</returns>
    public static T? Get<T>(Func<QuantumEditorSettings, T> check) where T : struct {
      return TryGetGlobal(out var instance) ? check(instance) : default;
    }

    /// <summary>
    /// Get the global Quantum editor settings instance and run the provided action and return result of a certain type.
    /// </summary>
    /// <typeparam name="T">Type to return</typeparam>
    /// <param name="check">The func to run on the settings.</param>
    /// <param name="defaultValue">Return this when the settings have not been found.</param>
    /// <returns>Returns the result of the func</returns>
    public static T Get<T>(Func<QuantumEditorSettings, T> check, T defaultValue) {
      return TryGetGlobal(out var instance) ? check(instance) : defaultValue;
    }

    /// <summary>
    /// Test if an asset path is in the asset search paths (<see cref="AssetSearchPaths"/>).
    /// </summary>
    /// <param name="path">Path to check</param>
    /// <returns>True if the path is inside the configured search path</returns>
    public static bool IsInAssetSearchPaths(string path) {
      if (TryGetGlobal(out var quantumEditorSettings)) {
        foreach (var searchPath in quantumEditorSettings.AssetSearchPaths) {
          if (!path.StartsWith(searchPath, StringComparison.Ordinal)) {
            continue;
          }
          return true;
        }
      }
      return false;
    }
    
    /// <summary>
    /// Locations that the QuantumUnityDB disovers Quantum assets.
    /// Changing this requires reimporting all Unity (Quantum) assets manually.
    /// </summary>
    [Header("Editor Features")]
    [InlineHelp]
    public string[] AssetSearchPaths = new string[] { "Assets" };

    /// <summary>
    /// Where to create new Quantum assets by default.
    /// </summary>
    [InlineHelp]
    public string DefaultNewAssetsLocation = "Assets/QuantumUser/Resources";

    /// <summary>
    /// The post processor enables duplicating Quantum assets and prefabs and make sure a new GUID and correct path are set. This can make especially batched processes slow and can be toggled off here.
    /// </summary>
    [FormerlySerializedAs("UseAssetBasePostprocessor")]
    [InlineHelp]
    public bool UseQuantumUnityDBAssetPostprocessor = true;

    /// <summary>
    /// If enabled a scene loading dropdown is displayed next to the play button.
    /// </summary>
    [InlineHelp]
    public bool UseQuantumToolbarUtilities = false;

    /// <summary>
    /// Where to display the toolbar. Requires a domain reload after change.
    /// </summary>
    [InlineHelp]
    public QuantumToolbarZone QuantumToolbarZone = QuantumToolbarZone.ToolbarZoneRightAlign;

    /// <summary>
    /// If enabled a local PhotonPrivateAppVersion scriptable object is created to support the demo menu scene.
    /// </summary>
    [InlineHelp]
    public bool UsePhotonAppVersionsPostprocessor = true;

    /// <summary>
    /// If enabled entity components are displayed inside of EntityPrototype inspector
    /// </summary>
    [FormerlySerializedAs("UseInlineEntityComponents")]
    [InlineHelp]
    public QuantumEntityComponentInspectorMode EntityComponentInspectorMode = QuantumEntityComponentInspectorMode.InlineInEntityPrototypeAndHideMonoBehaviours;
    
    /// <summary>
    /// Obsolete.
    /// </summary>
    [Obsolete("No longer used"), LastSupportedVersion("3.0-alpha")]
    public int FPDisplayPrecision {
      get => default;
      set { }
    }

    /// <summary>
    /// Automatically trigger bake on saving a scene.
    /// </summary>
    [InlineHelp]
    public QuantumMapDataBakeFlags AutoBuildOnSceneSave = QuantumMapDataBakeFlags.BakeMapData;

    /// <summary>
    /// If set MapData will be automatically baked on entering play mode, on saving a scene and on building a player.
    /// </summary>
    [InlineHelp]
    public QuantumMapDataBakeFlags AutoBuildOnPlaymodeChanged = QuantumMapDataBakeFlags.BakeMapData;

    /// <summary>
    /// If set MapData will be automatically baked on building, on saving a scene and on building a player.
    /// </summary>
    [InlineHelp] 
    public QuantumMapDataBakeFlags AutoBuildOnBuild = QuantumMapDataBakeFlags.BakeMapData;

    /// <summary>
    /// A list of Quantum assets that enabled GUID Override. This list is tracked automatically.
    /// </summary>
    [Header("Assets That Have Non-Deterministic GUIDs")]
    [SerializeField]
    [DrawInline]
    [InlineHelp]
    private List<SerializableAssetEntry> AssetGuidOverrides = new();

    [NonSerialized]
    private readonly Dictionary<(GUID, long), SerializableAssetEntry> _assetIdToAssetGuidOverride = new();
    
    [Serializable]
    class SerializableAssetEntry {
      public LazyLoadReference<UnityEngine.Object> Asset;
      public AssetGuid                             Guid;
    }
    
    private void OnValidate() {
      QuantumEditorLog.TraceImport($"OnValidate");
      
      _assetIdToAssetGuidOverride.Clear();

      for (int i = AssetGuidOverrides.Count - 1; i >= 0; i--) {
        var entry = AssetGuidOverrides[i];
        
        if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(entry.Asset, out var unityGuid, out var fileId)) {
          QuantumEditorLog.WarnImport($"Invalid or outdated AssetGuid override for {entry.Asset} ({entry.Guid}), removing");
          AssetGuidOverrides.RemoveAt(i);
          continue;
        } 
        
        if (string.IsNullOrEmpty(AssetDatabase.GUIDToAssetPath(unityGuid))) {
          QuantumEditorLog.TraceImport($"Invalid asset override for {entry.Asset}, removing");
          AssetGuidOverrides.RemoveAt(i);
          continue;
        }
        
        var key = (new GUID(unityGuid), fileId);
        if (_assetIdToAssetGuidOverride.ContainsKey(key)) {
          QuantumEditorLog.TraceImport($"Duplicate asset override for {key}");
          AssetGuidOverrides.RemoveAt(i);
        } else {
          _assetIdToAssetGuidOverride.Add(key, entry);
        }
      }
    }
    
    internal bool TryGetAssetGuidOverride(GUID guid, long fileId, out AssetGuid assetGuid) {
      if (_assetIdToAssetGuidOverride.TryGetValue((guid, fileId), out var entry)) {
        assetGuid = entry.Guid;
        return true;
      } else {
        assetGuid = default;
        return false;
      }
    }
    
    internal bool SetGuidOverride(LazyLoadReference<UnityEngine.Object> asset, AssetGuid assetGuid, out AssetGuid previousGuid) {
      var (unityAssetGuid, unityFileId) = AssetDatabaseUtils.GetGUIDAndLocalFileIdentifierOrThrow(asset);
      
      var key = (new GUID(unityAssetGuid), unityFileId);
      if (_assetIdToAssetGuidOverride.TryGetValue(key, out var entry)) {
        previousGuid = entry.Guid;
        if (assetGuid.IsValid) {
          entry.Guid = assetGuid;
        } else {
          _assetIdToAssetGuidOverride.Remove(key);
          AssetGuidOverrides.Remove(entry);
        }
      } else {
        previousGuid = default;

        if (assetGuid.IsValid) {
          entry = new SerializableAssetEntry {
            Asset = asset,
            Guid  = assetGuid
          };

          _assetIdToAssetGuidOverride.Add(key, entry);
          AssetGuidOverrides.Add(entry);
        }
      }

      if (assetGuid != previousGuid) {
        EditorUtility.SetDirty(this);
        return true;
      } else {
        return false;
      }
    }

    internal AssetGuid RemoveGuidOverride(GUID guid, long fileId) {
      if (_assetIdToAssetGuidOverride.TryGetValue((guid, fileId), out var entry)) {
        var result = entry.Guid;
        _assetIdToAssetGuidOverride.Remove((guid, fileId));
        AssetGuidOverrides.Remove(entry);
        return result;
      } else {
        return default;
      }
    }
    
    #region Legacy
    [Obsolete("Use DefaultNewAssetsLocation")]
    public string DatabasePathInResources => DefaultNewAssetsLocation;

    [Obsolete("Use TryGetGlobal() instead")]
    public QuantumEditorSettings InstanceFailSilently => TryGetGlobal(out var instance) ? instance : null;

    #endregion

    internal void RefreshGuidOverridesHash() {
      Hash128 hash = new Hash128();
      var sw = System.Diagnostics.Stopwatch.StartNew();
      foreach (var entry in AssetGuidOverrides) {
        if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(entry.Asset, out var unityGuid, out var fileId)) {
          hash.Append(unityGuid);
          hash.Append(fileId);
        }
        hash.Append(entry.Guid);
      }
      
      QuantumEditorLog.TraceImport($"Hash for {nameof(AssetGuidOverrides)}: {hash} (took {sw.Elapsed})");
      AssetDatabaseUtils.RegisterCustomDependencyWithMppmWorkaround(AssetGuidOverrideDependency, hash);
    }
    
    [CanBeNull]
    internal string GetAssetLookupRoot() {
      // do packages need to be searched for?
      foreach (var path in AssetSearchPaths) {
        if (!string.IsNullOrEmpty(path) && !path.StartsWith("Assets", StringComparison.Ordinal)) {
          // not rooted in Assets, will perform a project-wide search for assets (which is slower)
          return null;
        }
      }

      return "Assets";
    }
  }

  /// <summary>
  /// The toolbar zone to display the Quantum toolbar.
  /// </summary>
  [Serializable]
  public enum QuantumToolbarZone {
    /// <summary>
    /// Show toolbar on the right side of the play button.
    /// </summary>
    ToolbarZoneRightAlign,
    /// <summary>
    /// Show the toolbar on the left side of the play button.
    /// </summary>
    ToolbarZoneLeftAlign
  }

  /// <summary>
  /// Entity component inspector mode.
  /// </summary>
  public enum QuantumEntityComponentInspectorMode {
    /// <summary>
    /// Show the mono behaviours.
    /// </summary>
    ShowMonoBehaviours,
    /// <summary>
    /// Inline entity prototype and show mono behaviours as stubs.
    /// </summary>
    InlineInEntityPrototypeAndShowMonoBehavioursStubs,
    /// <summary>
    /// Inline entity prototype and hide mono behaviours.
    /// </summary>
    InlineInEntityPrototypeAndHideMonoBehaviours,
  }
}
