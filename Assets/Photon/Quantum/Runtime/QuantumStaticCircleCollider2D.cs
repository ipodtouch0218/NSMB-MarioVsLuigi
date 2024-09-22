namespace Quantum {
  using Photon.Deterministic;
  using UnityEngine;

  /// <summary>
  /// The script will create a static 2D circle collider during Quantum map baking.
  /// </summary>
  public class QuantumStaticCircleCollider2D : QuantumMonoBehaviour {
#if QUANTUM_ENABLE_PHYSICS2D && !QUANTUM_DISABLE_PHYSICS2D
    /// <summary>
    /// Link a Unity circle collider to copy its size and position of during Quantum map baking.
    /// </summary>
    [InlineHelp, MultiTypeReference(typeof(CircleCollider2D), typeof(SphereCollider))]
    public Component SourceCollider;
    /// <summary>
    /// The radius of the circle.
    /// </summary>
    [InlineHelp, DrawIf("SourceCollider", 0)]
    public FP Radius;
    /// <summary>
    /// The position offset added to the <see cref="Transform.position"/> during baking.
    /// </summary>
    [InlineHelp, DrawIf("SourceCollider", 0)]
    public FPVector2 PositionOffset;
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
        case SphereCollider sphere:
          Radius = sphere.radius.ToFP();
          PositionOffset = sphere.center.ToFPVector2();
          Settings.Trigger = sphere.isTrigger;
          break;
#endif

        case CircleCollider2D circle:
          Radius = circle.radius.ToFP();
          PositionOffset = circle.offset.ToFPVector2();
          Settings.Trigger = circle.isTrigger;
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