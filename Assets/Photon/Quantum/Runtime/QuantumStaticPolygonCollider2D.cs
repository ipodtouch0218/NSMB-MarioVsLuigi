namespace Quantum {
  using Photon.Deterministic;
  using UnityEngine;

  /// <summary>
  /// The script will create a static 2D polygon collider during Quantum map baking.
  /// </summary>
  public class QuantumStaticPolygonCollider2D : QuantumMonoBehaviour {
#if QUANTUM_ENABLE_PHYSICS2D && !QUANTUM_DISABLE_PHYSICS2D
    /// <summary>
    /// Link a Unity polygon collider to copy its size and position of during Quantum map baking.
    /// </summary>
    [InlineHelp] 
    public PolygonCollider2D SourceCollider;
    /// <summary>
    /// Bake the static collider as 2D edges instead.
    /// </summary>
    [InlineHelp] 
    public bool BakeAsStaticEdges2D = false;
    /// <summary>
    /// The individual vertices of the polygon.
    /// </summary>
    [InlineHelp, DrawIf("SourceCollider", 0)]
    public FPVector2[] Vertices = new FPVector2[3] { new FPVector2(0, 2), new FPVector2(-1, 0), new FPVector2(+1, 0) };
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
    /// The optional 2D pseudo height of the collider.
    /// </summary>
    [InlineHelp] 
    public FP Height;
    /// <summary>
    /// Additional static collider settings.
    /// </summary>
    [InlineHelp, DrawInline, Space]
    public QuantumStaticColliderSettings Settings = new QuantumStaticColliderSettings();
    /// <summary>
    /// Should the <see cref="Vertices"/> be set from the source collider during baking.
    /// </summary>
    protected virtual bool UpdateVerticesFromSourceOnBake => true;

    private void OnValidate() {
      Height = FPMath.Clamp(Height, 0, Height);
      UpdateFromSourceCollider();
    }

    /// <summary>
    /// Copy collider configuration from source collider if exist. 
    /// </summary>
    public void UpdateFromSourceCollider(bool updateVertices = true) {
      if (SourceCollider == null) {
        return;
      }

      Settings.Trigger = SourceCollider.isTrigger;
      PositionOffset = SourceCollider.offset.ToFPVector2();

      if (updateVertices == false) {
        return;
      }

      Vertices = new FPVector2[SourceCollider.points.Length];

      for (var i = 0; i < SourceCollider.points.Length; i++) {
        Vertices[i] = SourceCollider.points[i].ToFPVector2();
      }
    }

    /// <summary>
    /// Callback before baking the collider.
    /// </summary>
    public virtual void BeforeBake() {
      UpdateFromSourceCollider(UpdateVerticesFromSourceOnBake);
    }
#endif
  }
}