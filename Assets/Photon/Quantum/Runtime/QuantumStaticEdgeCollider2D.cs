namespace Quantum {
  using System;
  using System.Diagnostics;
  using Photon.Deterministic;
  using UnityEngine;

  public class QuantumStaticEdgeCollider2D : QuantumMonoBehaviour {
#if QUANTUM_ENABLE_PHYSICS2D && !QUANTUM_DISABLE_PHYSICS2D
    public EdgeCollider2D SourceCollider;

    [DrawIf("SourceCollider", 0)]
    public FPVector2 VertexA = new FPVector2(2, 2);

    [DrawIf("SourceCollider", 0)]
    public FPVector2 VertexB = new FPVector2(-2, -2);

    [DrawIf("SourceCollider", 0)]
    public FPVector2 PositionOffset;

    public FP                            RotationOffset;
    public FP                            Height;
    public QuantumStaticColliderSettings Settings = new QuantumStaticColliderSettings();

    public void UpdateFromSourceCollider() {
      if (SourceCollider == null) {
        return;
      }

      Settings.Trigger = SourceCollider.isTrigger;
      PositionOffset   = SourceCollider.offset.ToFPVector2();

      VertexA = SourceCollider.points[0].ToFPVector2();
      VertexB = SourceCollider.points[1].ToFPVector2();
    }

    public virtual void BeforeBake() {
      UpdateFromSourceCollider();
    }
    
    public static void GetEdgeGizmosSettings(Transform t, FPVector2 posOffset, FP rotOffset, FPVector2 localStart, FPVector2 localEnd, FP localHeight, out Vector3 start, out Vector3 end, out float height) {
      var scale = t.lossyScale;
      var trs = Matrix4x4.TRS(t.TransformPoint(posOffset.ToUnityVector3()), t.rotation * rotOffset.FlipRotation().ToUnityQuaternionDegrees(), scale);

      start = trs.MultiplyPoint(localStart.ToUnityVector3());
      end   = trs.MultiplyPoint(localEnd.ToUnityVector3());

#if QUANTUM_XY
      height = localHeight.AsFloat * -scale.z;
#else
      height = localHeight.AsFloat * scale.y;
#endif
    }
    
#endif
  }
}