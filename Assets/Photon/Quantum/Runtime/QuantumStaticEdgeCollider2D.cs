namespace Quantum {
  using Photon.Deterministic;
  using UnityEngine;

  /// <summary>
  /// The script will create a static 2D edge collider during Quantum map baking.
  /// </summary>
  public class QuantumStaticEdgeCollider2D : QuantumMonoBehaviour {
#if QUANTUM_ENABLE_PHYSICS2D && !QUANTUM_DISABLE_PHYSICS2D
    /// <summary>
    /// Link a Unity edge collider to copy its size and position of during Quantum map baking.
    /// </summary>
    public EdgeCollider2D SourceCollider;
    /// <summary>
    /// Vertex A of the edge.
    /// </summary>
    [InlineHelp, DrawIf("SourceCollider", 0)]
    public FPVector2 VertexA = new FPVector2(2, 2);
    /// <summary>
    /// Vertex B of the edge.
    /// </summary>
    [InlineHelp, DrawIf("SourceCollider", 0)]
    public FPVector2 VertexB = new FPVector2(-2, -2);
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
    /// Copy collider configuration from source collider if exist. 
    /// </summary>
    public void UpdateFromSourceCollider() {
      if (SourceCollider == null) {
        return;
      }

      Settings.Trigger = SourceCollider.isTrigger;
      PositionOffset = SourceCollider.offset.ToFPVector2();

      VertexA = SourceCollider.points[0].ToFPVector2();
      VertexB = SourceCollider.points[1].ToFPVector2();
    }

    /// <summary>
    /// Callback before baking the collider.
    /// </summary>
    public virtual void BeforeBake() {
      UpdateFromSourceCollider();
    }

    /// <summary>
    /// Edge transformation to bake.
    /// </summary>
    public static void GetEdgeGizmosSettings(Transform t, FPVector2 posOffset, FP rotOffset, FPVector2 localStart, FPVector2 localEnd, FP localHeight, out Vector3 start, out Vector3 end, out float height) {
      var scale = t.lossyScale;
      var trs = Matrix4x4.TRS(t.TransformPoint(posOffset.ToUnityVector3()), t.rotation * rotOffset.FlipRotation().ToUnityQuaternionDegrees(), scale);

      start = trs.MultiplyPoint(localStart.ToUnityVector3());
      end = trs.MultiplyPoint(localEnd.ToUnityVector3());

#if QUANTUM_XY
      height = localHeight.AsFloat * -scale.z;
#else
      height = localHeight.AsFloat * scale.y;
#endif
    }

#endif
  }
}