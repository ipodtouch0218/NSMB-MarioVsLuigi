using UnityEngine;

public class ParallaxMover : MonoBehaviour {
    [SerializeField] float speed;
    
    void Update() {
        transform.position += new Vector3(speed * Time.deltaTime, 0, 0);
    }
}
