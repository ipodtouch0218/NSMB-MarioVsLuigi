namespace Quantum {
  using System;
  using Photon.Deterministic;
  using UnityEngine;

  public class QuantumStaticMeshCollider3D : QuantumMonoBehaviour {
#if QUANTUM_ENABLE_PHYSICS3D && !QUANTUM_DISABLE_PHYSICS3D
    public Mesh Mesh;
    public QuantumStaticColliderSettings Settings = new QuantumStaticColliderSettings();

    [Header("Experimental")] public Boolean SmoothSphereMeshCollisions = false;

    [NonSerialized] public MeshTriangleVerticesCcw MeshTriangles = new MeshTriangleVerticesCcw();

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
      
      var result = new MeshTriangleVerticesCcw();

      var localToWorld = transform.localToWorldMatrix;

      // Normally, Unity Mesh triangles are defined in CW order. However, if the local-to-world
      // transformation scales the mesh with negative values in an even number of axes,
      // this will result in vertices that now define a CCW triangle, which needs to be taken
      // into consideration when baking the transformed vertices in the static mesh collider.
      var scale = localToWorld.lossyScale;
      var isCcw = scale.x * scale.y * scale.z < 0;

      var degenerateCount = 0;

      result.Vertices = new FPVector3[Mesh.vertices.Length];
      result.Triangles = new TriangleVerticesCcw[Mesh.triangles.Length / 3];

      // Save the arrays to reduce overhead of the property calls during the loop.
      var cachedUnityTriangles = Mesh.triangles;
      var cachedUnityVertices = Mesh.vertices;

      for (int vertexId = 0; vertexId < cachedUnityVertices.Length; vertexId++) {
        result.Vertices[vertexId] = localToWorld.MultiplyPoint(cachedUnityVertices[vertexId]).ToFPVector3();
      }

      for (int i = 0; i < cachedUnityTriangles.Length; i += 3) {
        var vertexA = cachedUnityTriangles[i];
        var vertexB = cachedUnityTriangles[i + 1];
        var vertexC = cachedUnityTriangles[i + 2];

        TriangleVerticesCcw triVertices;
        if (isCcw) {
          triVertices = new TriangleVerticesCcw(vertexA, vertexB, vertexC);
        } else {
          triVertices = new TriangleVerticesCcw(vertexC, vertexB, vertexA);
        }

        result.Triangles[i / 3] = triVertices;

        var vA = result.Vertices[triVertices.VertexA];
        var vB = result.Vertices[triVertices.VertexB];
        var vC = result.Vertices[triVertices.VertexC];
        var edgeAB = vB - vA;
        var edgeBC = vC - vB;
        var edgeCA = vA - vC;
        var normal = FPVector3.Cross(edgeAB, edgeCA).Normalized;

        if (normal == default || edgeAB.SqrMagnitude == default || edgeBC.SqrMagnitude == default ||
            edgeCA.SqrMagnitude == default) {
          degenerateCount++;
          Debug.LogWarning($"Degenerate triangle on game object {gameObject.name} using mesh {Mesh.name}. " +
                           $"Triangle vertices in world space: \n" +
                           $"Vertex A: index {vertexA}, value {localToWorld.MultiplyPoint(cachedUnityVertices[vertexA])} \n" +
                           $"Vertex B: index {vertexB}, value {localToWorld.MultiplyPoint(cachedUnityVertices[vertexB])} \n" +
                           $"Vertex C: index {vertexC}, value {localToWorld.MultiplyPoint(cachedUnityVertices[vertexC])}.");
        }
      }

      if (degenerateCount > 0) {
        Array.Resize(ref result.Triangles, result.Triangles.Length - degenerateCount);
      }
      
      return result;
    }
#endif
  }
}