using UnityEngine;

public class ParallaxMover : MonoBehaviour {
    [SerializeField] protected float speed;
    [SerializeField] protected bool bypassLimit = false;
    protected Vector3 moveBy;


    public void Start() {
        moveBy = new(speed, 0, 0);
        foreach (var mover in GetComponentsInParent<ParallaxMover>()) {
            if (mover.transform != transform && !bypassLimit) {
                enabled = false;
                break;
            }
        }
    }

    public virtual void Update() {
        transform.position += Time.deltaTime * moveBy;
    }
}
