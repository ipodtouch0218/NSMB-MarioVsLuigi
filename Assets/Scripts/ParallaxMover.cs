using UnityEngine;

public class ParallaxMover : MonoBehaviour {
    [SerializeField] float speed;
    private Vector3 moveBy;

    void Start() {
        moveBy = new(speed, 0, 0);
        foreach (var mover in GetComponentsInParent<ParallaxMover>()) {
            if (mover.transform != transform) {
                enabled = false;
                break;
            }
        }
    }

    void Update() {
        transform.position += moveBy * Time.deltaTime;
    }
}
