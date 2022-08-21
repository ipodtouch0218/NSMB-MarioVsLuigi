using System.Collections.Generic;
using UnityEngine;

using Photon.Pun;
using NSMB.Utils;

public class CustomRigidbodySerializer : MonoBehaviourPun, ICustomSerializeView {

    private static readonly float EPSILON = 0.02f, TELEPORT_DISTANCE = 0.4f, RESEND_RATE = 0.5f;

    public bool Active { get; set; } = true;

    [SerializeField]
    private bool interpolate = true;

    [SerializeField]
    private Rigidbody2D body;

    private Vector2 previousPosition, previousVelocity;
    private float lastSendTimestamp;

    private Vector2 interpPosition;
    private float interpDistance;

    public void Start() {
        previousPosition = interpPosition = body.position;
        previousVelocity = body.velocity;
    }

    public void FixedUpdate() {
        if (!photonView.IsMineOrLocal() && interpolate && body) {
            //interpPosition += Time.fixedDeltaTime * body.velocity; // makes it worse?
            body.position = Vector2.MoveTowards(body.position, interpPosition, 1f / PhotonNetwork.SerializationRate);
        }
    }

    public void OnDrawGizmos() {
        if (Application.IsPlaying(this) && !photonView.IsMineOrLocal() && interpolate) {
            Gizmos.color = Color.cyan;
            Gizmos.DrawCube(interpPosition + 0.25f * Vector2.up, Vector2.one * 0.5f);
        }
    }

    public void Serialize(List<byte> buffer) {
        //serialize position & velocity
        bool sendVelocity = body.bodyType != RigidbodyType2D.Static && Vector2.Distance(previousVelocity, body.velocity) > EPSILON;
        bool sendPosition = Vector2.Distance(previousPosition, body.position) > EPSILON;
        bool forceResend = PhotonNetwork.Time - lastSendTimestamp > RESEND_RATE;

        //we can't just send one, we dont know the read length.
        if (forceResend || sendVelocity || sendPosition) {
            SerializationUtils.PackToInt(buffer, body.position, GameManager.Instance.GetLevelMinX() - 0.5f, GameManager.Instance.GetLevelMaxX() + 0.5f, -20, 20);
            SerializationUtils.PackToInt(buffer, body.velocity, -20, 20);

            previousPosition = body.position;
            previousVelocity = body.velocity;
            lastSendTimestamp = (float) PhotonNetwork.Time;
        }
    }

    public void Deserialize(List<byte> buffer, ref int index, PhotonMessageInfo info) {
        //position
        SerializationUtils.UnpackFromInt(buffer, ref index, GameManager.Instance.GetLevelMinX() - 0.5f, GameManager.Instance.GetLevelMaxX() + 0.5f, out Vector2 newPosition, -20, 20);

        //velocity
        bool syncVelocity = body.bodyType != RigidbodyType2D.Static;
        if (syncVelocity) {
            SerializationUtils.UnpackFromInt(buffer, ref index, -20, 20, out Vector2 newVelocity);

            if (Mathf.Abs(newVelocity.x) < EPSILON)
                newVelocity = new(0, newVelocity.y);
            if (Mathf.Abs(newVelocity.y) < EPSILON)
                newVelocity = new(newVelocity.x, 0);

            body.velocity = newVelocity;
        }

        if (syncVelocity && interpolate) {
            interpPosition = newPosition;
            if ((interpDistance = Vector2.Distance(interpPosition, body.position)) > TELEPORT_DISTANCE || interpDistance < EPSILON)
                body.position = newPosition;
        } else {
            body.position = newPosition;
        }
    }
}
