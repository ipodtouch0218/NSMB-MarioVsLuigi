using System.Collections.Generic;
using UnityEngine;

using NSMB.Utils;

public class CameraController : MonoBehaviour {

    private static readonly Vector2 airOffset = new(0, .65f);

    public static float ScreenShake = 0;
    public Vector3 currentPosition;
    public bool IsControllingCamera { get; set; } = false;

    private Vector2 airThreshold = new(0.5f, 1.3f), groundedThreshold = new(0.5f, 0f);
    private readonly List<SecondaryCameraPositioner> secondaryPositioners = new();
    private PlayerController controller;
    private Vector3 smoothDampVel, playerPos;
    private Camera targetCamera;
    private float startingZ, lastFloor;

    public void Awake() {
        //only control the camera if we're the local player.
        targetCamera = Camera.main;
        startingZ = targetCamera.transform.position.z;
        controller = GetComponent<PlayerController>();
        targetCamera.GetComponentsInChildren(secondaryPositioners);
    }

    public void LateUpdate() {
        currentPosition = CalculateNewPosition();
        if (IsControllingCamera) {

            Vector3 shakeOffset = Vector3.zero;
            if ((ScreenShake -= Time.deltaTime) > 0 && controller.onGround)
                shakeOffset = new Vector3((Random.value - 0.5f) * ScreenShake, (Random.value - 0.5f) * ScreenShake);

            targetCamera.transform.position = currentPosition + shakeOffset;
            if (BackgroundLoop.Instance)
                BackgroundLoop.Instance.Reposition();

            secondaryPositioners.RemoveAll(scp => scp == null);
            secondaryPositioners.ForEach(scp => scp.UpdatePosition());
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

        if (!controller.dead)
            playerPos = AntiJitter(transform.position);

        float vOrtho = targetCamera.orthographicSize;
        float xOrtho = vOrtho * targetCamera.aspect;

        // instant camera movements. we dont want to lag behind in these cases

        float cameraBottomMax = Mathf.Max(3.5f - transform.lossyScale.y, 1.5f);
        //bottom camera clip
        if (playerPos.y - (currentPosition.y - vOrtho) < cameraBottomMax)
            currentPosition.y = playerPos.y + vOrtho - cameraBottomMax;

        float playerHeight = controller.WorldHitboxSize.y;
        float cameraTopMax = Mathf.Min(1.5f + playerHeight, 4f);

        //top camera clip
        if (playerPos.y - (currentPosition.y + vOrtho) + cameraTopMax > 0)
            currentPosition.y = playerPos.y - vOrtho + cameraTopMax;

        Utils.WrapWorldLocation(ref playerPos);
        float xDifference = Vector2.Distance(Vector2.right * currentPosition.x, Vector2.right * playerPos.x);
        bool right = currentPosition.x > playerPos.x;

        if (xDifference >= 8) {
            currentPosition.x += (right ? -1 : 1) * GameManager.Instance.levelWidthTile / 2f;
            xDifference = Vector2.Distance(Vector2.right * currentPosition.x, Vector2.right * playerPos.x);
            right = currentPosition.x > playerPos.x;
            if (IsControllingCamera)
                BackgroundLoop.Instance.wrap = true;
        }

        if (xDifference > 0.25f)
            currentPosition.x += (0.25f - xDifference - 0.01f) * (right ? 1 : -1);

        // lagging camera movements
        Vector3 targetPosition = currentPosition;
        if (controller.onGround)
            lastFloor = playerPos.y;
        bool validFloor = controller.onGround || lastFloor < playerPos.y;

        //top camera clip ON GROUND. slowly pan up, dont do it instantly.
        if (validFloor && lastFloor - (currentPosition.y + vOrtho) + cameraTopMax + 2f > 0)
            targetPosition.y = playerPos.y - vOrtho + cameraTopMax + 2f;


        // Smoothing

        targetPosition = Vector3.SmoothDamp(currentPosition, targetPosition, ref smoothDampVel, .5f);

        // Clamping to within level bounds

        targetPosition.x = Mathf.Clamp(targetPosition.x, minX + xOrtho, maxX - xOrtho);
        targetPosition.y = Mathf.Clamp(targetPosition.y, minY + vOrtho, heightY == 0 ? (minY + vOrtho) : (minY + heightY - vOrtho));

        // Z preservation

        //targetPosition = AntiJitter(targetPosition);
        targetPosition.z = startingZ;

        return targetPosition;
    }
    private void OnDrawGizmos() {
        if (!controller)
            return;

        Gizmos.color = Color.blue;
        Vector2 threshold = controller.onGround ? groundedThreshold : airThreshold;
        Gizmos.DrawWireCube(playerPos, threshold * 2);
        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(new(playerPos.x, lastFloor), Vector3.right / 2);
    }

    private static Vector2 AntiJitter(Vector3 vec) {
        vec.y = ((int) (vec.y * 100)) / 100f;
        return vec;
    }
}
