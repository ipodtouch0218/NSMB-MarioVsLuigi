namespace Quantum {
  using System;
  using System.Collections.Generic;
  using System.Linq;
  using Photon.Deterministic;
  using UnityEngine;

  /// <summary>
  /// The script will create a static 3D mesh collider during Quantum map baking.
  /// </summary>
  public class QuantumStaticMeshCollider3D : QuantumMonoBehaviour {
#if QUANTUM_ENABLE_PHYSICS3D && !QUANTUM_DISABLE_PHYSICS3D
    /// <summary>
    /// The Unity mesh to convert into a Quantum static mesh colliders.
    /// </summary>
    [InlineHelp]
    public Mesh Mesh;
    /// <summary>
    /// Additional static collider settings.
    /// </summary>
    [InlineHelp, DrawInline, Space]
    public QuantumStaticColliderSettings Settings = new QuantumStaticColliderSettings();

    /// <summary>
    /// The physics solver will resolve sphere and capsule shapes against mesh collisions as if the mesh was a regular flat and smooth plane.
    /// </summary>
    [InlineHelp, Header("Experimental")]
    public Boolean SmoothSphereMeshCollisions = false;

    [NonSerialized]
    public MeshTriangleVerticesCcw MeshTriangles = new MeshTriangleVerticesCcw();

    void Reset() {
      // default to mesh collider
      var meshCollider = GetComponent<MeshCollider>();
      if (meshCollider) {
        Mesh = meshCollider.sharedMesh;
      }

      // try mesh filter
      else {
        var meshFilter = GetComponent<MeshFilter>();
        if (meshFilter) {
          Mesh = meshFilter.sharedMesh;
        }
      }
    }
    
    public bool Bake(Int32 index) {
      MeshTriangles = CreateMeshTriangles();
      MeshTriangles.MeshColliderIndex = index;

#if UNITY_EDITOR
      UnityEditor.EditorUtility.SetDirty(this);
#endif

      return MeshTriangles.Triangles.Length > 0;
    }

    /// <summary>
    /// Create mesh triangles from the Unity mesh.
    /// </summary>
    /// <returns>The resulting <see cref="MeshTriangleVerticesCcw"/>.</returns>
    public MeshTriangleVerticesCcw CreateMeshTriangles() {
      FPMathUtils.LoadLookupTables(false);

      if (!Mesh) {
        Reset();

        if (!Mesh) {
          // log warning
          Debug.LogWarning($"No mesh for static mesh collider selected on {gameObject.name}");

          // don't do anything else
          return null;
        }
      }

      var fpVertices = Mesh.vertices.Select(x => x.ToFPVector3()).ToArray();
     
      var matrix = transform.localToWorldMatrix.ToFPMatrix4X4();
      
      return MeshTriangleVerticesCcw.Create(fpVertices, Mesh.triangles, matrix);
    }
#endif
  }
}