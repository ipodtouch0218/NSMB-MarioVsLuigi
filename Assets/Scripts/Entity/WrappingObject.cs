using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WrappingObject : MonoBehaviour {
    private Rigidbody2D body;
    void Awake() {
        body = GetComponent<Rigidbody2D>();
        if (!body) body = GetComponentInParent<Rigidbody2D>();
    }
    void Update() {
        if (!GameManager.Instance) return;
        if (!GameManager.Instance.loopingLevel) {
            this.enabled = false;
            return;
        }

        WrapMainObject();
    }
    void WrapMainObject() {
        float leftX = GameManager.Instance.GetLevelMinX();
        float rightX = GameManager.Instance.GetLevelMaxX();
        float width = (rightX - leftX);

        if (body.position.x < leftX) {
            body.position += new Vector2(width, 0);
        }
        if (body.position.x > rightX) {
            body.position += new Vector2(-width, 0);
        }
        body.centerOfMass = Vector2.zero;
    }
}
