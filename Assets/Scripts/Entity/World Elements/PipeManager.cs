using UnityEngine;

using Fusion;

public class PipeManager : NetworkBehaviour {

    public bool entryAllowed = true, bottom = false, miniOnly = false;
    public PipeManager otherPipe;

    //---Debug
#if UNITY_EDITOR
    private static readonly Color LineColor = new(1f, 1f, 0f, 0.25f);
    private static readonly float LineWidth = 0.125f;
    public void OnDrawGizmos() {
        if (!otherPipe)
            return;

        Gizmos.color = LineColor;
        Vector3 source = transform.position;
        Vector3 destination = otherPipe.transform.position;
        Vector3 direction = (destination - source).normalized;
        while (Vector3.Distance(source, destination) > LineWidth * 4f) {
            Gizmos.DrawLine(source, source + direction * LineWidth);
            source += LineWidth * 4f * direction;
        }
        Gizmos.DrawLine(source, destination);
    }
#endif
}
