using UnityEngine;

public class ParallaxMover : MonoBehaviour {
    [SerializeField] float speed;
    private Vector3 moveBy;

    void Start() {
        moveBy = new(speed, 0, 0);    
    }

    void Update() {
        transform.position += moveBy * Time.deltaTime;
    }
}
