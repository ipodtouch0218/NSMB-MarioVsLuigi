using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WrappingObject : MonoBehaviour {
    private Rigidbody2D body;
    void Start() {
        body = GetComponent<Rigidbody2D>();
        if (!body) 
            body = GetComponentInParent<Rigidbody2D>();
    }
    void Update() {
        if (!GameManager.Instance) 
            return;
        if (!GameManager.Instance.loopingLevel) {
            this.enabled = false;
            return;
        }

        WrapMainObject();
    }
    void WrapMainObject() {
        float width = GameManager.Instance.levelWidthTile / 2;
        if (body.position.x < GameManager.Instance.GetLevelMinX()) {
            body.position += new Vector2(width, 0);
        } else if (body.position.x > GameManager.Instance.GetLevelMaxX()) {
            body.position += new Vector2(-width, 0);
        }
        body.centerOfMass = Vector2.zero;
    }
}
