namespace Quantum {
  using System;
  using Photon.Deterministic;
  using UnityEngine;

  public class QuantumStaticPolygonCollider2D : QuantumMonoBehaviour {
#if QUANTUM_ENABLE_PHYSICS2D && !QUANTUM_DISABLE_PHYSICS2D
    public PolygonCollider2D SourceCollider;

    public bool BakeAsStaticEdges2D = false;

    [DrawIf("SourceCollider", 0)]
    public FPVector2[] Vertices = new FPVector2[3] { new FPVector2(0, 2), new FPVector2(-1, 0), new FPVector2(+1, 0) };

    [DrawIf("SourceCollider", 0)]
    [Tooltip("Additional translation applied to transform position when baking")]
    public FPVector2 PositionOffset;

    [Tooltip("Additional rotation (in degrees) applied to transform rotation when baking")]
    public FP RotationOffset;

    public FP                            Height;
    public QuantumStaticColliderSettings Settings = new QuantumStaticColliderSettings();

    protected virtual bool UpdateVerticesFromSourceOnBake => true;

    public void UpdateFromSourceCollider(bool updateVertices = true) {
      if (SourceCollider == null) {
        return;
      }

      Settings.Trigger = SourceCollider.isTrigger;
      PositionOffset   = SourceCollider.offset.ToFPVector2();

      if (updateVertices == false) {
        return;
      }

      Vertices = new FPVector2[SourceCollider.points.Length];

      for (var i = 0; i < SourceCollider.points.Length; i++) {
        Vertices[i] = SourceCollider.points[i].ToFPVector2();
      }
    }

    public virtual void BeforeBake() {
      UpdateFromSourceCollider(UpdateVerticesFromSourceOnBake);
    }
#endif
  }
}