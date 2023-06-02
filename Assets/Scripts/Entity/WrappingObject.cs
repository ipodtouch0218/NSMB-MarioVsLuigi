using UnityEngine;

using Fusion;
using NSMB.Game;
using NSMB.Utils;

[RequireComponent(typeof(NetworkRigidbody2D))]
[OrderBefore(typeof(NetworkPhysicsSimulation2D))]
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

    public void LateUpdate() {
        if (nrb.InterpolationTarget) {
            Vector3 pos = nrb.InterpolationTarget.position;
            if (Utils.WrapWorldLocation(ref pos)) {
                nrb.InterpolationTarget.position = pos;
            }
        }
    }

    public override void FixedUpdateNetwork() {
        if (nrb.Rigidbody.position.x < GameManager.Instance.LevelMinX) {
            nrb.TeleportToPosition(nrb.Rigidbody.position + width, nrb.Rigidbody.velocity, false);
        } else if (nrb.Rigidbody.position.x > GameManager.Instance.LevelMaxX) {
            nrb.TeleportToPosition(nrb.Rigidbody.position - width, nrb.Rigidbody.velocity, false);
        }
    }
}
