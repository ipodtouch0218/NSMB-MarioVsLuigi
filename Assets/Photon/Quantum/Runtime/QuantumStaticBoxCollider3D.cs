namespace Quantum {
  using Photon.Deterministic;
  using UnityEngine;

  /// <summary>
  /// The script will create a static 3D Quantum box collider during map baking.
  /// </summary>
  public class QuantumStaticBoxCollider3D : QuantumMonoBehaviour {
#if QUANTUM_ENABLE_PHYSICS3D && !QUANTUM_DISABLE_PHYSICS3D
    /// <summary>
    /// The Unity box collider to copy the size and position of during Quantum map baking.
    /// </summary>
    [InlineHelp]
    public BoxCollider SourceCollider;
    /// <summary>
    /// The size of the collider.
    /// </summary>
    [InlineHelp, DrawIf("SourceCollider", 0)]
    public FPVector3 Size;
    /// <summary>
    /// The position offset added to the <see cref="Transform.position"/> during baking.
    /// </summary>
    [InlineHelp, DrawIf("SourceCollider", 0)]
    public FPVector3 PositionOffset;
    /// <summary>
    /// The rotation offset added to the <see cref="Transform.rotation"/> during baking.
    /// </summary>
    [InlineHelp]
    public FPVector3 RotationOffset;
    /// <summary>
    /// Additional static collider settings.
    /// </summary>
    [InlineHelp, DrawInline, Space]
    public QuantumStaticColliderSettings Settings = new QuantumStaticColliderSettings();

    private void ClampSize() {
      Size.X = FPMath.Clamp(Size.X, 0, Size.X);
      Size.Y = FPMath.Clamp(Size.Y, 0, Size.Y);
      Size.Z = FPMath.Clamp(Size.Z, 0, Size.Z);
    }

    private void OnValidate() {
      UpdateFromSourceCollider();
    }

    /// <summary>
    /// Copy collider configuration from source collider if exist. 
    /// </summary>
    public void UpdateFromSourceCollider() {
      if (SourceCollider == null) {
        ClampSize();
        return;
      }

      Size = SourceCollider.size.ToFPVector3();
      Size.X = FPMath.Abs(Size.X);
      Size.Y = FPMath.Abs(Size.Y);
      Size.Z = FPMath.Abs(Size.Z);
      PositionOffset = SourceCollider.center.ToFPVector3();
      Settings.Trigger = SourceCollider.isTrigger;
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