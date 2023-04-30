using UnityEngine;

using Fusion;

[RequireComponent(typeof(NetworkRigidbody2D))]
[OrderAfter(typeof(PlayerController), typeof(HoldableEntity))]
public class WrappingObject : SimulationBehaviour {

    //---Serialized Variables
    [SerializeField] private NetworkRigidbody2D nrb;

    //---Private Variables
    private Vector2 width;

    public void OnValidate() {
        if (!nrb) nrb = GetComponentInParent<NetworkRigidbody2D>();
    }

    public void Awake() {
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
