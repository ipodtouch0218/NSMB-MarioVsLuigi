namespace Quantum {
  using System;
  using Photon.Deterministic;
  using UnityEngine;

  public class QuantumStaticBoxCollider2D : QuantumMonoBehaviour {
#if QUANTUM_ENABLE_PHYSICS2D && !QUANTUM_DISABLE_PHYSICS2D
    
    [MultiTypeReference(typeof(BoxCollider2D), typeof(BoxCollider))]
    public Component SourceCollider;

    [DrawIf("SourceCollider", 0)]
    public FPVector2 Size;

    [DrawIf("SourceCollider", 0)]
    public FPVector2 PositionOffset;

    public FP                            RotationOffset;
    public FP                            Height;
    
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
#if QUANTUM_ENABLE_PHYSICS3D && !QUANTUM_DISABLE_PHYSICS3D
        case BoxCollider box:
          Size             = box.size.ToFPVector2();
          PositionOffset   = box.center.ToFPVector2();
          Settings.Trigger = box.isTrigger;
          break;
#endif

        case BoxCollider2D box:
          Size             = box.size.ToFPVector2();
          PositionOffset   = box.offset.ToFPVector2();
          Settings.Trigger = box.isTrigger;
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