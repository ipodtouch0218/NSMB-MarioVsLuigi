using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraController : MonoBehaviour {

    public float screenShakeTimer = 0;
    public bool controlCamera = false, onGround = false;
    public Vector3 currentPosition;
    
    private Vector2 airThreshold = new(0.5f, 1.5f), groundedThreshold = new(0.5f, .25f);
    private Vector2 airOffset = new(0, 1f), groundedOffset = new(0, 1.25f);

    private Vector3 smoothDampVel;
    private Camera targetCamera;
    private float startingZ;

    private Rigidbody2D body;

    void Start() {
        //only control the camera if we're the local player.
        targetCamera = Camera.main;
        startingZ = targetCamera.transform.position.z;
        body = GetComponent<Rigidbody2D>();
    }

    public void LateUpdate() {
        currentPosition = CalculateNewPosition();
        if (controlCamera) {
            targetCamera.transform.position = currentPosition;
            if (BackgroundLoop.Instance)
                BackgroundLoop.Instance.LateUpdate();
        }
    }

    public void Recenter() {
        currentPosition = (Vector2) transform.position + airOffset;
        smoothDampVel = Vector3.zero;
        LateUpdate();
    }

    private Vector3 CalculateNewPosition() {
        float minY = GameManager.Instance.cameraMinY, heightY = GameManager.Instance.cameraHeightY;
        float minX = GameManager.Instance.cameraMinX, maxX = GameManager.Instance.cameraMaxX;

        Vector2 threshold = onGround ? groundedThreshold : airThreshold;
        Vector2 offset = onGround ? groundedOffset : airOffset;

        Vector2 playerPos = (Vector2) transform.position + offset;
        playerPos.y += 16 * Time.fixedDeltaTime * body.velocity.y;
        float xDifference = Vector2.Distance(Vector2.right * currentPosition.x, Vector2.right * playerPos.x);
        float yDifference = Vector2.Distance(Vector2.up * currentPosition.y, Vector2.up * playerPos.y);

        Vector3 newPosition = currentPosition;
        bool right = currentPosition.x > playerPos.x;
        if (xDifference >= 8) {
            newPosition.x += (right ? -1 : 1) * GameManager.Instance.levelWidthTile / 2f;
            xDifference = Vector2.Distance(Vector2.right * newPosition.x, Vector2.right * playerPos.x);
        }
        if (xDifference >= threshold.x) {
            newPosition.x += (threshold.x - xDifference) * (right ? 1 : -1);
        }
        if (yDifference >= threshold.y) {
            newPosition.y += (threshold.y - yDifference) * (currentPosition.y > playerPos.y ? 1 : -1);
        }

        currentPosition.x = newPosition.x;
        Vector3 targetPos = Vector3.SmoothDamp(currentPosition, newPosition, ref smoothDampVel, 0.25f);

        if ((screenShakeTimer -= Time.deltaTime) > 0) 
            targetPos += new Vector3((Random.value - 0.5f) * (screenShakeTimer / 2), (Random.value - 0.5f) * (screenShakeTimer / 2));

        float vOrtho = targetCamera.orthographicSize;
        float hOrtho = vOrtho * targetCamera.aspect;
        targetPos.x = Mathf.Clamp(targetPos.x, minX + hOrtho, maxX - hOrtho);
        targetPos.y = Mathf.Clamp(targetPos.y, minY + vOrtho, heightY == 0 ? (minY + vOrtho) : (minY + heightY - vOrtho));
        targetPos.z = startingZ;

        return targetPos;
    }
    private void OnDrawGizmos() {
        Gizmos.color = Color.blue;
        Vector2 threshold = onGround ? groundedThreshold : airThreshold;
        Vector2 offset = onGround ? groundedOffset : airOffset;
        Gizmos.DrawWireCube((Vector2) transform.position + offset, new Vector3(threshold.x * 2, threshold.y * 2, 1));
    }
}
