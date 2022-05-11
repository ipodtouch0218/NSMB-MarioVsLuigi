using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlatformWrap : MonoBehaviour {
    public float maxY, minY, ySpeed;
    private Rigidbody2D body;
    void Start() {
        body = GetComponent<Rigidbody2D>();
        body.velocity = new Vector2(0, ySpeed);
        body.isKinematic = true;
    }
    void Update() {
        float y = transform.position.y;
        if (y > maxY) {
            body.position = new Vector2(body.position.x, y - (maxY - minY));
        } else if (y < minY) {
            body.position = new Vector2(body.position.x, y + (maxY - minY));
        }
    }
}
