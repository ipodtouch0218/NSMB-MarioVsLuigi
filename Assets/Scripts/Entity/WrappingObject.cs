using UnityEngine;

using Fusion;
using NSMB.Utils;

[RequireComponent(typeof(NetworkRigidbody2D))]
[OrderAfter(typeof(PlayerController), typeof(HoldableEntity), typeof(NetworkRigidbody2D))]
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

    public override void Render() {
        Vector3 pos = transform.position;
        Utils.WrapWorldLocation(ref pos);
        transform.position = pos;
    }

    public override void FixedUpdateNetwork() {
        if (nrb.Rigidbody.position.x < GameManager.Instance.LevelMinX) {
            nrb.Rigidbody.position = nrb.Rigidbody.position + width;
        } else if (nrb.Rigidbody.position.x > GameManager.Instance.LevelMaxX) {
            nrb.Rigidbody.position = nrb.Rigidbody.position - width;
        }
    }
}
