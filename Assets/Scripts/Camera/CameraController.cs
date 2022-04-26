using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraController : MonoBehaviour {

    public float screenShakeTimer = 0;
    public bool controlCamera = false;
    public Vector3 currentPosition;
    
    private Vector2 airThreshold = new(0.5f, 1.3f), groundedThreshold = new(0.5f, 0f);
    private Vector2 airOffset = new(0, .65f), groundedOffset = new(0, 1.015f);

    private Vector3 smoothDampVel;
    private Camera targetCamera;
    private float startingZ, floorHeight;
    private bool validGround;

    private Rigidbody2D body;
    private PlayerController controller;
    private Vector3 playerPos;

    void Start() {
        //only control the camera if we're the local player.
        targetCamera = Camera.main;
        startingZ = targetCamera.transform.position.z;
        body = GetComponent<Rigidbody2D>();
        controller = GetComponent<PlayerController>();
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
        floorHeight = transform.position.y;
        smoothDampVel = Vector3.zero;
        LateUpdate();
    }

    private Vector3 CalculateNewPosition() {
        float minY = GameManager.Instance.cameraMinY, heightY = GameManager.Instance.cameraHeightY;
        float minX = GameManager.Instance.cameraMinX, maxX = GameManager.Instance.cameraMaxX;


        float time = (Time.time - Time.fixedTime) / Time.fixedDeltaTime;
        playerPos = Vector2.Lerp(body.position, body.position + (body.velocity * Time.fixedDeltaTime), time);
        Utils.WrapWorldLocation(ref playerPos);

        if (controller.onGround)
            floorHeight = body.position.y;

        //float floorRange = (controller.singlejump || controller.doublejump || controller.triplejump) ? 3.75f : 1.75f;
        float floorRange = 3.5f;
        validGround = body.position.y + (Time.fixedDeltaTime * body.velocity.y) - floorHeight < floorRange && body.position.y - floorHeight > -.5f;

        RaycastHit2D hit;
        if (!controller.dead) {
            if (validGround) {
                if (!controller.onGround && (hit = Physics2D.BoxCast(transform.position, controller.hitboxes[0].size * 0.95f, 0, Vector2.down, floorRange, PlayerController.ANY_GROUND_MASK))) {
                    floorHeight = hit.point.y;
                }
                playerPos.y = floorHeight;
            } else if (hit = Physics2D.BoxCast(transform.position, controller.hitboxes[0].size * 0.95f, 0, Vector2.down, 1f, PlayerController.ANY_GROUND_MASK)) {
                floorHeight = hit.point.y;
                playerPos.y = floorHeight;
                validGround = true;
            }
        }
        if (!validGround || controller.dead || controller.flying) {
            playerPos.y += 24 * Time.fixedDeltaTime * body.velocity.y * (controller.dead ? 0.5f : 1f);
        }

        Vector2 threshold = (controller.onGround || validGround) ? groundedThreshold : airThreshold;
        Vector3 offset = (controller.onGround || validGround) ? groundedOffset : airOffset;

        playerPos += offset;

        float xDifference = Vector2.Distance(Vector2.right * currentPosition.x, Vector2.right * playerPos.x);
        float yDifference = Vector2.Distance(Vector2.up * currentPosition.y, Vector2.up * playerPos.y);

        Vector3 newPosition = currentPosition;
        bool right = currentPosition.x > playerPos.x;
        bool up = currentPosition.y > playerPos.y;
        if (xDifference >= 8) {
            newPosition.x += (right ? -1 : 1) * GameManager.Instance.levelWidthTile / 2f;
            xDifference = Vector2.Distance(Vector2.right * newPosition.x, Vector2.right * playerPos.x);
        }
        if (xDifference > threshold.x) {
            newPosition.x += (threshold.x - xDifference + 0.01f) * (right ? 1 : -1);
        }
        if (yDifference > threshold.y) {
            newPosition.y += (threshold.y - yDifference + 0.01f) * (up ? 1 : -1);
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
        Vector2 threshold = controller.onGround ? groundedThreshold : airThreshold;
        Gizmos.DrawWireCube(playerPos, threshold * 2);
    }
}
