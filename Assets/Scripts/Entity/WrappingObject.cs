using UnityEngine;

public class WrappingObject : MonoBehaviour {

    private static GameManager gm;
    private Rigidbody2D body;

    public void Start() {
        body = GetComponent<Rigidbody2D>();
        if (!body)
            body = GetComponentInParent<Rigidbody2D>();
    }
    public void FixedUpdate() {
        if (!gm)
            gm = GameManager.Instance;
        if (!gm)
            return;

        if (!gm.loopingLevel) {
            enabled = false;
            return;
        }

        WrapMainObject();
    }
    public void WrapMainObject() {
        float width = gm.levelWidthTile / 2;
        if (body.position.x < gm.GetLevelMinX()) {
            transform.position = body.position += new Vector2(width, 0);
        } else if (body.position.x > gm.GetLevelMaxX()) {
            transform.position = body.position += new Vector2(-width, 0);
        }
        body.centerOfMass = Vector2.zero;
    }
}
