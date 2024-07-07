namespace Quantum {
  using System;
  using Photon.Deterministic;
  using UnityEngine;

  public class QuantumStaticSphereCollider3D : QuantumMonoBehaviour {
#if QUANTUM_ENABLE_PHYSICS3D && !QUANTUM_DISABLE_PHYSICS3D
    public SphereCollider SourceCollider;

    [DrawIf("SourceCollider", 0)]
    public FP Radius;

    [DrawIf("SourceCollider", 0)]
    public FPVector3 PositionOffset;
    
    [DrawInline, Space]
    public QuantumStaticColliderSettings Settings = new QuantumStaticColliderSettings();

    private void OnValidate() {
      UpdateFromSourceCollider();
    }

    public void UpdateFromSourceCollider() {
      if (SourceCollider == null) {
        return;
      }

      Radius           = SourceCollider.radius.ToFP();
      PositionOffset   = SourceCollider.center.ToFPVector3();
      Settings.Trigger = SourceCollider.isTrigger;
    }

    public virtual void BeforeBake() {
      UpdateFromSourceCollider();
    }
#endif
  }
}