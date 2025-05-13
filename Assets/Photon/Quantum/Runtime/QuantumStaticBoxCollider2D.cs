namespace Quantum {
  using Photon.Deterministic;
  using UnityEngine;

  /// <summary>
  /// The script will create a static 2D Quantum box collider during map baking.
  /// </summary>
  public class QuantumStaticBoxCollider2D : QuantumMonoBehaviour {
#if QUANTUM_ENABLE_PHYSICS2D && !QUANTUM_DISABLE_PHYSICS2D
    /// <summary>
    /// Link a Unity box collider to copy its size and position of during Quantum map baking.
    /// </summary>
#if QUANTUM_ENABLE_PHYSICS3D && !QUANTUM_DISABLE_PHYSICS3D
    [InlineHelp, MultiTypeReference(typeof(BoxCollider2D), typeof(BoxCollider))]
#else
    [InlineHelp, SerializeReferenceTypePicker(typeof(BoxCollider2D))]
#endif
    public Component SourceCollider;
    /// <summary>
    /// Set the size of the box collider.
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
    /// Set an optional pseudo height of the collider.
    /// </summary>
    [InlineHelp]
    public FP Height;
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
      Size.X = FPMath.Clamp(Size.X, 0, Size.X);
      Size.Y = FPMath.Clamp(Size.Y, 0, Size.Y);
      Height = FPMath.Clamp(Height, 0, Height);
      if (SourceCollider == null) {
        return;
      }

      switch (SourceCollider) {
#if QUANTUM_ENABLE_PHYSICS3D && !QUANTUM_DISABLE_PHYSICS3D
        case BoxCollider box:
          Size = box.size.ToFPVector2();
          PositionOffset = box.center.ToFPVector2();
          Settings.Trigger = box.isTrigger;
          break;
#endif

        case BoxCollider2D box:
          Size = box.size.ToFPVector2();
          PositionOffset = box.offset.ToFPVector2();
          Settings.Trigger = box.isTrigger;
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