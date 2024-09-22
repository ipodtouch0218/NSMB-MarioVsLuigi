namespace Quantum {
  using Photon.Deterministic;
  using UnityEngine;

  /// <summary>
  /// This script can be placed on a game object that has a mesh renderer and is used to cast Quantum navmesh toggle-able regions onto the Unity generated navmesh.
  /// </summary>
  public class QuantumNavMeshRegion : QuantumMonoBehaviour {
    /// <summary>
    /// The Quantum region cast type.
    /// </summary>
    public enum RegionCastType {
      /// <summary>
      /// Create a region on the navmesh.
      /// </summary>
      CastRegion,
      /// <summary>
      /// Do not create a region on the navmesh (Quantum navmesh links for example).
      /// </summary>
      NoRegion
    }

    /// <summary>
    /// All regions with the same id are toggle-able as one region. Check Map.Regions to see the results.
    /// </summary>
    [Tooltip("All regions with the same id are toggle-able as one region. Check Map.Regions to see the results.")]
    public string Id;

    /// <summary>
    /// Set to CastRegion when the region should be casted onto the navmesh. For Links for example chose NoRegion.
    /// </summary>
    [Tooltip("Set to CastRegion when the region should be casted onto the navmesh. For Links for example chose NoRegion.")]
    public RegionCastType CastRegion;

    /// <summary>
    /// Cost modifier that is applied to the heuristics of the path finding. Automatically gets the Unity area cost when adding the scripts. Toggle <see cref="OverwriteCost"/> to set to a custom value.
    /// </summary>
    [Tooltip("Cost modifier that is applied to the heuristics of the path finding. Automatically gets the Unity area cost when adding the scripts. Toggle Overwrite to set to a custom value.")]
    public FP Cost = FP._1;

    /// <summary>
    /// Enabled to set a different <see cref="Cost"/>.
    /// </summary>
    public bool OverwriteCost = false;
  }
}