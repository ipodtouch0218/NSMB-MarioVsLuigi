namespace Quantum {
  using System.Collections.Generic;
  using UnityEngine;

  /// <summary>
  /// Unity component that holds and bakes the map data for a Quantum map from a given scene.
  /// </summary>
  [ExecuteInEditMode]
  public class QuantumMapData : QuantumMonoBehaviour {

    /// <summary>
    /// The source asset to bake the data into.
    /// </summary>
    [InlineHelp]
    public Map Asset;

    /// <summary>
    /// How the map data should be baked.
    /// </summary>
    [HideInInspector] 
    public QuantumMapDataBakeFlags BakeAllMode = QuantumMapDataBakeFlags.BakeMapData | QuantumMapDataBakeFlags.GenerateAssetDB;

    /// <summary>
    /// <see cref="Quantum.NavMeshSerializeType"/>
    /// </summary>
    [InlineHelp]
    public NavMeshSerializeType NavMeshSerializeType;

    /// <summary>
    /// One-to-one mapping of Quantum 2D static collider entries in QAssetMap to their original source scripts. 
    /// Purely for convenience to do post bake mappings and not required by the Quantum simulation.
    /// </summary>
    [InlineHelp]
    public List<MonoBehaviour> StaticCollider2DReferences = new List<MonoBehaviour>();

    /// <summary>
    /// One-to-one mapping of Quantum 3D static collider entries in QAssetMap to their original source scripts. 
    /// Purely for convenience to do post bake mappings and not required by the Quantum simulation.
    /// </summary>
    [InlineHelp]
    public List<MonoBehaviour> StaticCollider3DReferences = new List<MonoBehaviour>();

    /// <summary>
    /// One-to-one mapping of Quantum map entity entries in QAssetMap to their original source scripts.
    /// </summary>
    [InlineHelp]
    public List<QuantumEntityView> MapEntityReferences = new List<QuantumEntityView>();

    void Update() {
      transform.position = Vector3.zero;
      transform.rotation = Quaternion.identity;
    }
  }
}