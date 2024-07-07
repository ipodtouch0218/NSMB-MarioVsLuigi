namespace Quantum {
  using Photon.Deterministic;
  using UnityEngine;

  public class QuantumNavMeshRegion : QuantumMonoBehaviour {
    public enum RegionCastType {
      CastRegion,
      NoRegion
    }

    [Tooltip("All regions with the same id are toggle-able as one region. Check Map.Regions to see the results.")]
    public string Id;

    [Tooltip("Set to CastRegion when the region should be casted onto the navmesh. For Links for example chose NoRegion.")]
    public RegionCastType CastRegion;

    [Tooltip("Cost modifier that is applied to the heuristics of the path finding. Automatically gets the Unity area cost when adding the scripts. Toggle Overwrite to set to a custom value.")]
    public FP Cost = FP._1;

    public bool OverwriteCost = false;
  }
}