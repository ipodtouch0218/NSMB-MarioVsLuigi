using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;

public class CustomRigidbodySerializer : MonoBehaviour, ICustomSerializeView {

    public bool Active { get; set; } = true;

    [SerializeField]
    private bool interpolate = true;

    [SerializeField]
    private Rigidbody2D body;

    public void Serialize(List<byte> buffer) {
        //serialize position & velocity
        SerializationUtils.PackToInt(buffer, body.position, GameManager.Instance.GetLevelMinX(), GameManager.Instance.GetLevelMaxX(), -20, 20);
        if (body.bodyType != RigidbodyType2D.Static)
            SerializationUtils.PackToShort(buffer, body.velocity, -10, 10);
    }

    public void Deserialize(List<byte> buffer, ref int index, PhotonMessageInfo info) {
        //position
        SerializationUtils.UnpackFromInt(buffer, ref index, GameManager.Instance.GetLevelMinX(), GameManager.Instance.GetLevelMaxX(), out Vector2 newPosition, -20, 20);
        body.position = newPosition;

        //velocity
        if (body.bodyType != RigidbodyType2D.Static) {
            SerializationUtils.UnpackFromShort(buffer, ref index, -10, 10, out Vector2 newVelocity);

            if (Mathf.Abs(newVelocity.x) < 0.02f)
                newVelocity = new(0, newVelocity.y);
            if (Mathf.Abs(newVelocity.y) < 0.02f)
                newVelocity = new(newVelocity.x, 0);

            body.velocity = newVelocity;

            if (interpolate) {
                float lagInMs = Mathf.Abs((float) (PhotonNetwork.ServerTimestamp - info.SentServerTimestamp));
                float lag = lagInMs / 1000f;
                body.MovePosition(body.position + body.velocity * lag);
            }
        }
    }
}
