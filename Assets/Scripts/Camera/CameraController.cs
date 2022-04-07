using System.Collections;
using System.Collections.Generic;
using UnityEngine;
public class CameraController : MonoBehaviour {

    public float scrollAmount, dampingTime = 0.5f;
    public float screenShakeTimer = 0;
    public bool exactCentering = false, controlCamera = false;
    public Vector3 offset = Vector3.zero, currentPosition;

    private Vector3 dampVelocity;
    private Camera targetCamera;
    private float startingZ;

    void Start() {
        //only control the camera if we're the local player.
        targetCamera = Camera.main;
        startingZ = targetCamera.transform.position.z;
    }

    public void Update() {
        currentPosition = CalculateNewPosition();
        if (controlCamera)
            targetCamera.transform.position = currentPosition;
    }

    private Vector3 CalculateNewPosition() {
        float minY = GameManager.Instance.cameraMinY, heightY = GameManager.Instance.cameraHeightY;
        float minX = GameManager.Instance.cameraMinX, maxX = GameManager.Instance.cameraMaxX; 
        Vector3 ctp = transform.position + offset + new Vector3(0, transform.localScale.y/2f);
        Vector3 targetPos = ctp;

        if (!exactCentering) {
            float currX = currentPosition.x;
            float currY = currentPosition.y;

            float tp = targetPos.x - currX;
            float targetX = currX;

            if (Mathf.Abs(tp) > 5) {
                targetX = targetPos.x + scrollAmount;
            } else if (targetPos.x - currX > 0.5f) {
                targetX = targetPos.x - 0.5f;
            } else if (targetPos.x - currX < -0.5f) {
                targetX = targetPos.x + 0.5f;
            }

            float targetY = currY;
            if (targetPos.y - currY > 0.5f) {
                targetY = targetPos.y - 0.5f;
            } else if (targetPos.y - currY < -0.5f) {
                targetY = targetPos.y + 0.5f;
            }

            targetPos.x = targetX;
            targetPos.y = targetY;
        }

        float vOrtho = targetCamera.orthographicSize;
        float hOrtho = vOrtho * targetCamera.aspect;
        targetPos.x = Mathf.Clamp(targetPos.x, minX + hOrtho, maxX - hOrtho);
        targetPos.y = Mathf.Clamp(targetPos.y, minY + vOrtho, heightY == 0 ? (minY + vOrtho) : (minY + heightY - vOrtho));
        
        if (exactCentering) {
            if (Vector2.Distance(currentPosition, targetPos) > 5) {
                bool right = currentPosition.x < targetPos.x;
                currentPosition += new Vector3((right ? 1 : -1) * GameManager.Instance.levelWidthTile / 2f, 0, 0);
            }
            Vector3 result = Vector3.SmoothDamp(currentPosition, targetPos, ref dampVelocity, dampingTime);
            result.y = targetPos.y;
            targetPos = result;
        }
        scrollAmount = Mathf.Clamp(currentPosition.x - ctp.x, -0.5f, 0.5f);
        if ((screenShakeTimer -= Time.deltaTime) > 0) 
            targetPos += new Vector3((Random.value - 0.5f) * (screenShakeTimer / 2), (Random.value - 0.5f) * (screenShakeTimer / 2));

        //perserve camera z
        targetPos.z = startingZ;
        return targetPos;
    }
}
