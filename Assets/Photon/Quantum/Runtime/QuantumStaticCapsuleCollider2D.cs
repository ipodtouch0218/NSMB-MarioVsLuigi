namespace Quantum {
  using Photon.Deterministic;
  using UnityEngine;

  /// <summary>
  /// The script will create a static 2D capsule collider during Quantum map baking.
  /// </summary>
  public class QuantumStaticCapsuleCollider2D : QuantumMonoBehaviour {
#if QUANTUM_ENABLE_PHYSICS2D && !QUANTUM_DISABLE_PHYSICS2D
    /// <summary>
    /// Link a Unity capsule collider to copy its size and position of during Quantum map baking.
    /// </summary>
    [InlineHelp, MultiTypeReference(typeof(CapsuleCollider2D), typeof(CapsuleCollider))]
    public Component SourceCollider;
    /// <summary>
    /// Define the capsule size if not source collider exists. The x-axis is the diameter and the y-axis is the height.
    /// </summary>
    [InlineHelp, DrawIf("SourceCollider", 0)]
    public FPVector2 Size;
    /// <summary>
    /// The position offset added to the <see cref="Transform.position"/> during baking.
    /// </summary>
    [InlineHelp, DrawIf("SourceCollider", 0)]
    public FPVector2 PositionOffset;
    /// <summary>
    /// The rotation offset added to the <see cref="Transform.rotation"/> during baking.
    /// </summary>
    [InlineHelp]
    public FP RotationOffset;
    /// <summary>
    /// Additional static collider settings.
    /// </summary>
    [InlineHelp, DrawInline, Space]
    public QuantumStaticColliderSettings Settings = new QuantumStaticColliderSettings();

    private void OnValidate() {
      UpdateFromSourceCollider();
    }

    /// <summary>
    /// Copy collider configuration from source collider if exist. 
    /// </summary>
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

    /// <summary>
    /// Callback before baking the collider.
    /// </summary>
    public virtual void BeforeBake() {
      UpdateFromSourceCollider();
    }

#endif
    }
}