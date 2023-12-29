using UnityEngine;

public class ParallaxMover : MonoBehaviour {

    //---Serialized Variables
    [SerializeField] protected float speed;

    //---Private Variables
    protected Vector3 moveBy;

    public void OnValidate() {
        moveBy = Vector3.right * speed;
    }

    public void Start() {
        moveBy = Vector3.right * speed;
        foreach (var mover in GetComponentsInParent<ParallaxMover>()) {
            if (mover.transform != transform) {
                enabled = false;
                break;
            }
        }
    }

    public virtual void Update() {
        transform.position += Time.deltaTime * moveBy;
    }
}
