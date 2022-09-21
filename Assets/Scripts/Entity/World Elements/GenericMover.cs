using UnityEngine;

using Fusion;

public class GenericMover : NetworkBehaviour {

    [SerializeField] private AnimationCurve x, y;
    [SerializeField] private float animationTimeSeconds = 1;

    private Vector3? origin = null;
    private double timestamp = 0;

    public void Awake() {
        if (origin == null)
            origin = transform.position;
    }

    public void Update() {
        int start = GameManager.Instance.startServerTime;

        if (PhotonNetwork.Time <= timestamp) {
            timestamp += Time.deltaTime;
        } else {
            timestamp = (float) PhotonNetwork.Time;
        }

        double time = timestamp - (start / (double) 1000);
        time /= animationTimeSeconds;
        time %= animationTimeSeconds;

        transform.position = (origin ?? default) + new Vector3(x.Evaluate((float) time), y.Evaluate((float) time), 0);
    }
}