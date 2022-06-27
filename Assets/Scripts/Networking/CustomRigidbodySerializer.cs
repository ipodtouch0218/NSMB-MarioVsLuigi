using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;

public class CustomRigidbodySerializer : MonoBehaviour, ICustomSerializeView {

    public bool Active { get; set; } = true;

    [SerializeField]
    private bool extrapolate = false;
    [SerializeField]
    private bool interpolate = true;

    [SerializeField]
    private Rigidbody2D body;

    private Vector2 interpPosition;
    private float interpDistance;

    public void FixedUpdate() {
        if (interpolate)
            body.position = Vector2.MoveTowards(body.position, interpPosition, interpDistance * (1f / PhotonNetwork.SerializationRate));
    }

    public void Serialize(List<byte> buffer) {
        //serialize position & velocity
        SerializationUtils.PackToInt(buffer, body.position, GameManager.Instance.GetLevelMinX() - 0.5f, GameManager.Instance.GetLevelMaxX() + 0.5f, -20, 20);
        if (body.bodyType != RigidbodyType2D.Static)
            SerializationUtils.PackToShort(buffer, body.velocity, -10, 10);
    }

    public void Deserialize(List<byte> buffer, ref int index, PhotonMessageInfo info) {
        //position
        SerializationUtils.UnpackFromInt(buffer, ref index, GameManager.Instance.GetLevelMinX() - 0.5f, GameManager.Instance.GetLevelMaxX() + 0.5f, out Vector2 newPosition, -20, 20);
        if (interpolate) {
            interpPosition = newPosition;
            if ((interpDistance = Vector2.Distance(interpPosition, body.position)) > 0.75f || interpDistance < 0.02)
                body.position = newPosition;
        } else {
            body.position = newPosition;
        }

        //velocity
        if (body.bodyType != RigidbodyType2D.Static) {
            SerializationUtils.UnpackFromShort(buffer, ref index, -10, 10, out Vector2 newVelocity);

            if (Mathf.Abs(newVelocity.x) < 0.02f)
                newVelocity = new(0, newVelocity.y);
            if (Mathf.Abs(newVelocity.y) < 0.02f)
                newVelocity = new(newVelocity.x, 0);

            body.velocity = newVelocity;

            if (extrapolate) {
                float lag = (float) (PhotonNetwork.Time - info.SentServerTime);
                body.MovePosition(body.position + body.velocity * lag);
            }
        }
    }
}
