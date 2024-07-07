namespace Quantum {
  using System;
  using Photon.Deterministic;
  using UnityEngine;

  public class QuantumStaticCapsuleCollider2D : QuantumMonoBehaviour {
#if QUANTUM_ENABLE_PHYSICS2D && !QUANTUM_DISABLE_PHYSICS2D
    
    [MultiTypeReference(typeof(CapsuleCollider2D), typeof(CapsuleCollider))]
    public Component SourceCollider;

    [DrawIf("SourceCollider", 0)]
    public FPVector2 Size;

    [DrawIf("SourceCollider", 0)]
    public FPVector2 PositionOffset;
    public FP RotationOffset;
    
    [DrawInline, Space]
    public QuantumStaticColliderSettings Settings = new QuantumStaticColliderSettings();

    private void OnValidate() {
      UpdateFromSourceCollider();
    }

    public void UpdateFromSourceCollider() {
      if (SourceCollider == null) {
        return;
      }

      switch (SourceCollider) {
#if QUANTUM_ENABLE_PHYSICS2D && !QUANTUM_DISABLE_PHYSICS2D
        case CapsuleCollider capsule:
          Size             = new FPVector2(capsule.radius.ToFP(), capsule.height.ToFP());
          PositionOffset   = capsule.center.ToFPVector2();
          Settings.Trigger = capsule.isTrigger;
          break;
#endif

        case CapsuleCollider2D capsule:
          Size             = capsule.size.ToFPVector2();
          PositionOffset   = capsule.offset.ToFPVector2();
          Settings.Trigger = capsule.isTrigger;
          break;

        default:
          SourceCollider = null;
          break;
      }
    }

    public virtual void BeforeBake() {
      UpdateFromSourceCollider();
    }

#endif
    }
}