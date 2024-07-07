namespace Quantum {
  using System;
  using Photon.Deterministic;
  using UnityEngine;

  public class QuantumStaticBoxCollider3D : QuantumMonoBehaviour {
#if QUANTUM_ENABLE_PHYSICS3D && !QUANTUM_DISABLE_PHYSICS3D
    public BoxCollider SourceCollider;

    [DrawIf("SourceCollider", 0)]
    public FPVector3 Size;

    [DrawIf("SourceCollider", 0)]
    public FPVector3 PositionOffset;

    public FPVector3                     RotationOffset;
    
    [DrawInline, Space]
    public QuantumStaticColliderSettings Settings = new QuantumStaticColliderSettings();

    private void OnValidate() {
      UpdateFromSourceCollider();
    }

    public void UpdateFromSourceCollider() {
      if (SourceCollider == null) {
        return;
      }

      Size             = SourceCollider.size.ToFPVector3();
      PositionOffset   = SourceCollider.center.ToFPVector3();
      Settings.Trigger = SourceCollider.isTrigger;
    }

    public virtual void BeforeBake() {
      UpdateFromSourceCollider();
    }
#endif
  }
}