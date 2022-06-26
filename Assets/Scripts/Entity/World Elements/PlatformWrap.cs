using UnityEngine;

public class PlatformWrap : MonoBehaviour {

    public float maxY, minY, ySpeed;
    private Rigidbody2D body;

    public void Start() {
        body = GetComponent<Rigidbody2D>();
        body.velocity = ySpeed * Vector2.up;
        body.isKinematic = true;
    }

    public void FixedUpdate() {
        float y = body.position.y;
        if (y > maxY) {
            body.position = new Vector2(body.position.x, y - (maxY - minY));
        } else if (y < minY) {
            body.position = new Vector2(body.position.x, y + (maxY - minY));
        }
    }
}
