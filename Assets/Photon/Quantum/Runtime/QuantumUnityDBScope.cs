namespace Quantum {
  using System;
  using System.Collections.Generic;
  using UnityEngine;
  using UnityEngine.Serialization;

  /// <summary>
  /// A collection of <see cref="IQuantumAssetObjectSource"/>. At edit time, when placed in a folder, will break the link between assets and the DB. The DB does not reference scopes;
  /// instead, it assumes a scope is going to be loaded with <see cref="QuantumUnityDB.AddScope"/> at runtime.
  /// One of the possible use cases is compartmentalizing assets that are statically references by Addressable/Asset Bundle scenes. Without scopes, such assets would be
  /// referenced statically by the DB. If a scope is used, it can be marked to be included in the same bundle as the scene and no static references should leak out.
  ///
  /// Scopes can't be nested.
  /// </summary>
  public class QuantumUnityDBScope : QuantumScriptableObject {
    /// <summary>
    /// The unique id of this scope.
    /// </summary>
    [InlineHelp]
    public string Id;

    /// <summary>
    /// All nested asset sources.
    /// </summary>
    [SerializeField]
    [InlineHelp]
    internal List<QuantumUnityDB.Entry> Entries = new();
    
    /// <summary>
    /// AssetGuid to index in <see cref="Entries"/> mapping.
    /// </summary>
    private readonly Dictionary<AssetGuid, int> _guidToIndex = new();

    /// <summary>
    /// Path to index in <see cref="Entries"/> mapping.
    /// </summary>
    private readonly Dictionary<string, int> _pathToIndex = new();

    void OnEnable() {
      _guidToIndex.Clear();
      _pathToIndex.Clear();
      for (var i = 0; i < Entries.Count; ++i) {
        if (Entries[i] == null) {
          // removed, slot not used
          continue;
        } 
        AddSourceMapping(i, Entries[i].Guid, Entries[i].Path);
      }
    }

    /// <summary>
    /// Adds a new scoped asset source.
    /// </summary>
    public void AddSource(IQuantumAssetObjectSource source, AssetGuid guid, string path) {
      // nope
      AddSourceMapping(Entries.Count, guid, path);  
      Entries.Add(new () {
        Source = source,
        Guid = guid,
        Path =  path
      });
    }

    /// <summary>
    /// Adds the current scope to <see cref="QuantumUnityDB.Global"/>
    /// </summary>
    [ContextMenu("Add To Global DB")]
    public void AddToGlobalDB() {
      QuantumUnityDB.Global.AddScope(this);
    }
    
    /// <summary>
    /// Removes the current scope from <see cref="QuantumUnityDB.Global"/>
    /// </summary>
    [ContextMenu("Remove From Global DB")]
    public void RemoveFromGlobalDB() {
      QuantumUnityDB.Global.RemoveScope(this, true);
    }
    
    /// <summary>
    /// Returns the asset source with the given <paramref name="assetGuid"/>. Asset does not get loaded in the process.
    /// </summary>
    /// <param name="assetGuid"></param>
    /// <returns>Asset source or <c>null</c> if the asset is not found</returns>
    public IQuantumAssetObjectSource GetAssetSource(AssetGuid assetGuid) {
      if (_guidToIndex.TryGetValue(assetGuid, out var index)) {
        return Entries[index].Source;
      }
      return default;
    }
    
    private void AddSourceMapping(int index, AssetGuid guid, string path) {
      if (_guidToIndex.TryGetValue(guid, out var existingIndex)) {
        throw new ArgumentException($"Entry with {guid} already exists: {Entries[existingIndex]}", nameof(guid));
      }

      if (!string.IsNullOrEmpty(path)) {
        if (_pathToIndex.TryGetValue(path, out existingIndex)) {
          throw new ArgumentException($"Entry with {path} already exists: {Entries[existingIndex]}", nameof(path));
        }
      }

      _guidToIndex.Add(guid, index);

      if (!string.IsNullOrEmpty(path)) {
        _pathToIndex.Add(path, index);
      }
    }
  }
}