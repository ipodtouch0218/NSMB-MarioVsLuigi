namespace Quantum {
  using Photon.Deterministic;
  using UnityEngine;

  public class QuantumStaticCapsuleCollider3D : QuantumMonoBehaviour {
#if QUANTUM_ENABLE_PHYSICS3D && !QUANTUM_DISABLE_PHYSICS3D
    public CapsuleCollider SourceCollider;

    [DrawIf("SourceCollider", 0)] public FP Radius;

    [DrawIf("SourceCollider", 0)] public FP Height;

    [DrawIf("SourceCollider", 0)] public FPVector3 PositionOffset;

    public FPVector3 RotationOffset;

    [DrawInline, Space] public QuantumStaticColliderSettings Settings = new QuantumStaticColliderSettings();

    private void OnValidate() {
      UpdateFromSourceCollider();
    }

    public void UpdateFromSourceCollider() {
      if (SourceCollider == null) {
        return;
      }

      Radius = SourceCollider.radius.ToFP();
      Height = SourceCollider.height.ToFP();
      PositionOffset = SourceCollider.center.ToFPVector3();
      Settings.Trigger = SourceCollider.isTrigger;
    }

    public virtual void BeforeBake() {
      UpdateFromSourceCollider();
    }
#endif
  }
}