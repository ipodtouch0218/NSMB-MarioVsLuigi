using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;

public class CustomRigidbodySerializer : MonoBehaviourPun, ICustomSerializeView {

    public bool Active { get; set; } = true;

    [SerializeField]
    private bool interpolate = true;

    [SerializeField]
    private Rigidbody2D body;

    private Vector2 interpPosition;
    private float interpDistance;

    public void Start() {
        interpPosition = body.position;
    }

    public void FixedUpdate() {
        if (!photonView.IsMine && interpolate) {
            interpPosition += body.velocity * Time.fixedDeltaTime;
            body.position = Vector2.MoveTowards(body.position, interpPosition, 1f / PhotonNetwork.SerializationRate);
        }
    }

    public void OnDrawGizmos() {
        if (!interpolate)
            return;

        Gizmos.color = Color.cyan;
        Vector2 size = body.GetComponent<BoxCollider2D>().size;
        Gizmos.DrawCube(interpPosition + 0.5f * size * Vector2.up, size);
    }

    public void Serialize(List<byte> buffer) {
        //serialize position & velocity
        SerializationUtils.PackToInt(buffer, body.position, GameManager.Instance.GetLevelMinX() - 0.5f, GameManager.Instance.GetLevelMaxX() + 0.5f, -20, 20);
        if (body.bodyType != RigidbodyType2D.Static)
            SerializationUtils.PackToInt(buffer, body.velocity, -20, 20);
    }

    public void Deserialize(List<byte> buffer, ref int index, PhotonMessageInfo info) {
        //position
        SerializationUtils.UnpackFromInt(buffer, ref index, GameManager.Instance.GetLevelMinX() - 0.5f, GameManager.Instance.GetLevelMaxX() + 0.5f, out Vector2 newPosition, -20, 20);

        //velocity
        if (body.bodyType != RigidbodyType2D.Static) {
            SerializationUtils.UnpackFromInt(buffer, ref index, -20, 20, out Vector2 newVelocity);

            if (Mathf.Abs(newVelocity.x) < 0.02f)
                newVelocity = new(0, newVelocity.y);
            if (Mathf.Abs(newVelocity.y) < 0.02f)
                newVelocity = new(newVelocity.x, 0);

            body.velocity = newVelocity;
        }

        if (interpolate) {
            interpPosition = newPosition;
            if ((interpDistance = Vector2.Distance(interpPosition, body.position)) > 0.5f || interpDistance < 0.02f)
                body.position = newPosition;
        } else {
            body.position = newPosition;
        }

    }
}
