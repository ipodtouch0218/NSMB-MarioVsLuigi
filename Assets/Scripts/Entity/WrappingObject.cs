using UnityEngine;

using Fusion;

[RequireComponent(typeof(NetworkRigidbody2D))]
public class WrappingObject : NetworkBehaviour {

    private NetworkRigidbody2D nrb;
    private Vector2 width;

    public void Awake() {
        nrb = GetComponent<NetworkRigidbody2D>();
        if (!nrb)
            nrb = GetComponentInParent<NetworkRigidbody2D>();

        if (!nrb || !GameManager.Instance || !GameManager.Instance.loopingLevel) {
            enabled = false;
            return;
        }

        width = new(GameManager.Instance.LevelWidth, 0);
    }

    public override void FixedUpdateNetwork() {
        if (nrb.Rigidbody.position.x < GameManager.Instance.LevelMinX) {
            Vector3 newPos = nrb.Rigidbody.position + width;
            nrb.TeleportToPosition(newPos);
        } else if (nrb.Rigidbody.position.x > GameManager.Instance.LevelMaxX) {
            Vector3 newPos = nrb.Rigidbody.position - width;
            nrb.TeleportToPosition(newPos);
        }
    }
}
