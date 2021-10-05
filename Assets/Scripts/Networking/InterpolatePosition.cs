using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;

public class InterpolatePosition : MonoBehaviour, IPunObservable {
    Queue<Vector3> positions = new Queue<Vector3>();
    Queue<Quaternion> rotations = new Queue<Quaternion>();
    Queue<int> frameNumbers = new Queue<int>();
    static int amountBuffered = 1;
    static float oneTickTime = 1.0f / 20;

    PhotonView photonView;
    int localSentFrameNumber = 0;
    int lastReceivedFrameNumber = -1;
    int lastFrameNumberMovedTowards = 0;

    int expectedExecuteFrameNumber = 0; //An estimation of what frame number should be executing at this time.
    float expectedExecuteFrameNumberLastExecutedTime = 0;

    Vector3 moveTowards;
    float moveTowardsSpeed = 1.0f;
    bool moveInProgress = false;

    void Start() {
        photonView = gameObject.GetComponent<PhotonView>();
        moveTowards = transform.position;
    }

    void Update() {
        if (photonView.IsMine) return;

        transform.position = Vector3.MoveTowards(transform.position, moveTowards, moveTowardsSpeed * Time.deltaTime);
        if (Mathf.Abs(Vector3.Distance(transform.position, moveTowards)) < 0.001f) {
            moveInProgress = false;
        } else {
            moveInProgress = true;
        }

        if (expectedExecuteFrameNumberLastExecutedTime + oneTickTime < Time.realtimeSinceStartup) {
            expectedExecuteFrameNumber++;
            expectedExecuteFrameNumberLastExecutedTime = Time.realtimeSinceStartup;
        }

        if (frameNumbers.Count > 0 && ((frameNumbers.Peek() + amountBuffered <= lastReceivedFrameNumber) || frameNumbers.Peek() <= expectedExecuteFrameNumber - amountBuffered)) {
            if (lastReceivedFrameNumber - frameNumbers.Peek() > amountBuffered) {
                int trimUntil = lastReceivedFrameNumber - amountBuffered;
                while (frameNumbers.Peek() < trimUntil) {
                    positions.Dequeue();
                    rotations.Dequeue();
                    frameNumbers.Dequeue();
                }
            }

            Vector3 position = positions.Dequeue();
            Quaternion rotation = rotations.Dequeue();
            int frameNumber = frameNumbers.Dequeue();

            transform.rotation = rotation;

            if (Vector3.Distance(transform.position, position) > 10) {
                transform.position = position;
                moveInProgress = false;
                moveTowards = position;
                moveTowardsSpeed = 0;

                lastFrameNumberMovedTowards = frameNumber;

                expectedExecuteFrameNumber = frameNumber;
                expectedExecuteFrameNumberLastExecutedTime = Time.realtimeSinceStartup;
                return;
            }

            if (moveInProgress == true) {
                transform.position = moveTowards;
            }

            float distance = Vector3.Distance(transform.position, position);
            moveTowards = position;
            moveTowardsSpeed = distance * (frameNumber - lastFrameNumberMovedTowards) / oneTickTime;

            lastFrameNumberMovedTowards = frameNumber;

            expectedExecuteFrameNumber = frameNumber;
            expectedExecuteFrameNumberLastExecutedTime = Time.realtimeSinceStartup;
        }
    }

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info) {
        if (stream.IsWriting) {
            stream.SendNext(transform.position);
            stream.SendNext(transform.rotation);
            stream.SendNext(transform.localScale);
            stream.SendNext(localSentFrameNumber++);
        } else {
            positions.Enqueue((Vector3)stream.ReceiveNext());
            rotations.Enqueue((Quaternion)stream.ReceiveNext());
            transform.localScale = (Vector3) stream.ReceiveNext();
            int frameNumber = (int)stream.ReceiveNext();
            frameNumbers.Enqueue(frameNumber);
            lastReceivedFrameNumber = frameNumber;
        }
    }
}
