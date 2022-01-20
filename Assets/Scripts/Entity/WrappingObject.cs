using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WrappingObject : MonoBehaviour {
    [SerializeField] float offset;
    private Rigidbody2D body;
    void Start() {
        body = GetComponent<Rigidbody2D>();
    }
    void Update() {
        float leftX = GameManager.Instance.GetLevelMinX();
        float rightX = GameManager.Instance.GetLevelMaxX();
        float width = (rightX - leftX);

        leftX = leftX + (width * offset);

        if (body.position.x < leftX) {
            body.position += new Vector2(width, 0);
        }
        if (body.position.x > rightX) {
            body.position += new Vector2(-width, 0);
        }
    }
}
