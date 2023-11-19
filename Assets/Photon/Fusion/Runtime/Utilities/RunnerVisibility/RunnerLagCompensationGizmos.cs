namespace Fusion
{
  using LagCompensation;
  using UnityEngine;
  
  [ScriptHelp(BackColor = ScriptHeaderBackColor.Sand)]
  [DisallowMultipleComponent]
  public class RunnerLagCompensationGizmos : Behaviour
  {
    public bool DrawSnapshotHistory;
    public bool DrawBroadphaseNodes;

    public Color StateAuthHitboxCollor = Color.green;
    public Color NonStateAuthHitboxCollor = Color.cyan;

    private NetworkRunner _runner;

    private void Awake()
    {
      _runner = GetComponentInParent<NetworkRunner>();
      
      if (_runner == null) {
        Debug.LogWarning($"{this} was not able to find the NetworkRunner reference. Destroying the component.");
        Destroy(this);
      }
    }

    private void OnDrawGizmos()
    {
      if (_runner == null || _runner.IsRunning == false || _runner.GetVisible() == false || _runner.LagCompensation.DrawInfo == default) return;

      if (DrawBroadphaseNodes)
      {
        RenderBHVBroadphase();
      }

      if (DrawSnapshotHistory)
      {
        RenderHitboxHistory();
      }
    }

    private void RenderHitboxHistory()
    {
      Gizmos.color = _runner.IsServer ? StateAuthHitboxCollor : NonStateAuthHitboxCollor;

      foreach (var snapshotDrawInfo in _runner.LagCompensation.DrawInfo.SnapshotHistoryDraw) {
        foreach (var colliderDrawInfo in snapshotDrawInfo) {
          Gizmos.matrix = colliderDrawInfo.LocalToWorldMatrix;
          switch (colliderDrawInfo.Type) {
            case HitboxTypes.Box:
              Gizmos.DrawWireCube(colliderDrawInfo.Offset, colliderDrawInfo.BoxExtents * 2);
              break;
            case HitboxTypes.Sphere:
              Gizmos.DrawWireSphere(colliderDrawInfo.Offset, colliderDrawInfo.Radius);
              break;
            case HitboxTypes.Capsule:
              LagCompensationDraw.GizmosDrawWireCapsule(Vector3.up * colliderDrawInfo.CapsuleHeight, Vector3.down * colliderDrawInfo.CapsuleHeight, colliderDrawInfo.Radius);
              break;
            default:
              Debug.LogWarning($"HitboxType {colliderDrawInfo.Type} not supported to draw.");
              break;
          }
        }
      }
      
      Gizmos.matrix = Matrix4x4.identity;
    }

    private void RenderBHVBroadphase()
    {
      var initialColor = Color.green;

      foreach (var nodeDrawInfo in _runner.LagCompensation.DrawInfo.BVHDraw) {
        Gizmos.color = initialColor + Color.red * nodeDrawInfo.Depth / nodeDrawInfo.MaxDepth;
        Gizmos.DrawWireCube(nodeDrawInfo.Bounds.center,  nodeDrawInfo.Bounds.size);
      }
    }
  }
}