using UnityEngine;
using Photon.Pun;

public class GenericMover : MonoBehaviour {

    public AnimationCurve x;
    public AnimationCurve y;

    public float animationTimeSeconds = 1;

    private Vector3 origin;

    public void OnEnable() {
        origin = transform.position;
    }

    public void Update() {
        float start = GameManager.Instance.startServerTime;
        float time = (float) PhotonNetwork.Time - (start / 1000f);
        time /= animationTimeSeconds;
        time %= animationTimeSeconds;

        transform.position = origin + new Vector3(x.Evaluate(time), y.Evaluate(time), 0);
    }
}