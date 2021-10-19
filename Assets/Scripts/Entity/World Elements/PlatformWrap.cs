using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlatformWrap : MonoBehaviour {
    public float maxY, minY, ySpeed;
    void Start() {
        GetComponent<Rigidbody2D>().velocity = new Vector2(0, ySpeed);
    }
    void Update() {
        float y = transform.position.y;
        if (y > maxY) {
            transform.position = new Vector2(transform.position.x, y - (maxY - minY));
        } else if (y < minY) {
            transform.position = new Vector2(transform.position.x, y + (maxY - minY));
        }
    }
}
