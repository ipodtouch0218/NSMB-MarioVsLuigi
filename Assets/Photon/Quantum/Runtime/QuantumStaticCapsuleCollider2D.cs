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
#if QUANTUM_ENABLE_PHYSICS3D && !QUANTUM_DISABLE_PHYSICS3D
    [InlineHelp, MultiTypeReference(typeof(CapsuleCollider2D), typeof(CapsuleCollider))]
#else
    [InlineHelp, SerializeReferenceTypePicker(typeof(CapsuleCollider2D))]
#endif
    public Component SourceCollider;
    /// <summary>
    /// Define the capsule size if not source collider exists. The x-axis is the diameter and the y-axis is the height.
    /// </summary>
    [InlineHelp, DrawIf("SourceCollider", 0)]
    public FPVector2 Size;
    /// <summary>
    /// The world axis that the capsule will be aligned.
    /// </summary>
    [InlineHelp, DrawIf("SourceCollider", 0)]
    public CapsuleDirection2D Direction = CapsuleDirection2D.Vertical;
    /// <summary>
    /// Additional static collider settings.
    /// </summary>
    [InlineHelp, DrawIf("SourceCollider", 0)]
    public FPVector2 PositionOffset;
    /// <summary>
    /// The rotation offset added to the <see cref="Transform.rotation"/> during baking.
    /// </summary>
    [InlineHelp]
    public FP RotationOffset;
    /// <summary>
    /// The optional 2D pseudo height of the collider.
    /// </summary>
    [InlineHelp]
    public FP Height;
    /// <summary>
    /// Additional static collider settings.
    /// </summary>
    [InlineHelp, DrawInline, Space]
    public QuantumStaticColliderSettings Settings = new QuantumStaticColliderSettings();

    private void OnValidate() {
      Size.X = FPMath.Clamp(Size.X, 0, Size.X);
      Size.Y = FPMath.Clamp(Size.Y, 0, Size.Y);
      Height = FPMath.Clamp(Height, 0, Height);

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
#if QUANTUM_ENABLE_PHYSICS3D && !QUANTUM_DISABLE_PHYSICS3D
        case CapsuleCollider capsule:
          switch (capsule.direction) {
            case 0: // X-Axs
              Direction = CapsuleDirection2D.Horizontal;
              break;
            case 1: // Y-Axs
              Direction = CapsuleDirection2D.Vertical;
              break;
            case 2: // Z-Axs
#if QUANTUM_XY
              Direction = CapsuleDirection2D.Horizontal;
#else
              Direction = CapsuleDirection2D.Vertical;
#endif
              break;
          }

          var capsuleRadius = capsule.radius.ToFP();
          var capsuleHeight = capsule.height.ToFP();

          Size = Direction == CapsuleDirection2D.Horizontal
            ? new FPVector2(capsuleHeight, capsuleRadius * 2)
            : new FPVector2(capsuleRadius * 2, capsuleHeight);

          PositionOffset = capsule.center.ToFPVector2();
          Settings.Trigger = capsule.isTrigger;
          
          break;
#endif
            case CapsuleCollider2D capsule: {
            Size = capsule.size.ToFPVector2();
            PositionOffset = capsule.offset.ToFPVector2();
            Settings.Trigger = capsule.isTrigger;
            Direction = capsule.direction;
            break;
          }

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