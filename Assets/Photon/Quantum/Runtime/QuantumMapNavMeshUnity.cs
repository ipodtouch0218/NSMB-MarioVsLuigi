namespace Quantum {
  using UnityEngine;

  public class QuantumMapNavMeshUnity : QuantumMonoBehaviour {
    public GameObject[] NavMeshSurfaces;
    [DrawInline]
    public QuantumNavMesh.ImportSettings Settings;
  }
}