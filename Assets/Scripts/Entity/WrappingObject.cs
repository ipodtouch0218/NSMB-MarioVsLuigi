using UnityEngine;

using Fusion;

[RequireComponent(typeof(NetworkRigidbody2D))]
public class WrappingObject : NetworkBehaviour {

    private NetworkRigidbody2D nrb;
    private Vector2 width;
    private float min, max;

    public void Awake() {
        nrb = GetComponent<NetworkRigidbody2D>();
        if (!nrb)
            nrb = GetComponentInParent<NetworkRigidbody2D>();

        if (!nrb || !GameManager.Instance || !GameManager.Instance.loopingLevel) {
            enabled = false;
            return;
        }

        min = GameManager.Instance.GetLevelMinX();
        max = GameManager.Instance.GetLevelMaxX();
        width = new(GameManager.Instance.levelWidthTile * 0.5f, 0);
    }

    public override void FixedUpdateNetwork() {
        if (nrb.Rigidbody.position.x < min) {
            Vector3 newPos = nrb.Rigidbody.position + width;
            nrb.TeleportToPosition(newPos);
        } else if (nrb.Rigidbody.position.x > max) {
            Vector3 newPos = nrb.Rigidbody.position - width;
            nrb.TeleportToPosition(newPos);
        }
        //nrb.centerOfMass = Vector2.zero;
    }
}
