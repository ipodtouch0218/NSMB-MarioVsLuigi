namespace Quantum {
  using UnityEngine;

  /// <summary>
  /// A collection of Unity mesh asset to draw gizmos or scene object. 
  /// </summary>
  [QuantumGlobalScriptableObject("Assets/Photon/Quantum/Resources/QuantumMeshCollection.asset")]
  public partial class QuantumMeshCollection : QuantumGlobalScriptableObject<QuantumMeshCollection> {
    /// <summary>
    /// Capsule mesh.
    /// </summary>
    public Mesh Capsule;
    /// <summary>
    /// Circle mesh.
    /// </summary>
    public Mesh Circle;
    /// <summary>
    /// Circle mesh that is aligned to XY plane.
    /// </summary>
    public Mesh CircleXY;
    /// <summary>
    /// Cube mesh.
    /// </summary>
    public Mesh Cube;
    /// <summary>
    /// Cylinder mesh.
    /// </summary>
    public Mesh Cylinder;
    /// <summary>
    /// Cylinder mesh that is aligned to XY plane.
    /// </summary>
    public Mesh CylinderXY;
    /// <summary>
    /// Quad mesh.
    /// </summary>
    public Mesh Quad;
    /// <summary>
    /// Quad mesh that is aligned to XY plane.
    /// </summary>
    public Mesh QuadXY;
    /// <summary>
    /// Sphere mesh.
    /// </summary>
    public Mesh Sphere;
    /// <summary>
    /// Debug material used by <see cref="DebugDraw"/> 
    /// when drawing debug shapes from the Quantum simulation.
    /// </summary>
    public Material DebugMaterial;
  }
}