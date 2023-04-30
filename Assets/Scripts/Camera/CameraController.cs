using System.Collections.Generic;
using UnityEngine;

using NSMB.Utils;

public class CameraController : MonoBehaviour {

    //---Static Variables
    private static readonly Vector2 AirOffset = new(0, .65f);
    private static readonly Vector2 AirThreshold = new(0.55f, 1.3f), GroundedThreshold = new(0.55f, 0f);
    public static float ScreenShake = 0;

    //---Public Variables
    public Vector3 currentPosition;

    private bool _isControllingCamera;
    public bool IsControllingCamera {
        get => _isControllingCamera;
        set {
            _isControllingCamera = value;
            if (value)
                UIUpdater.Instance.player = controller;
        }
    }

    //---Private Variables
    private readonly List<SecondaryCameraPositioner> secondaryPositioners = new();
    private PlayerController controller;
    private Vector3 smoothDampVel, playerPos;
    private Camera targetCamera;
    private float startingZ, lastFloor;

    public void Awake() {
        //only control the camera if we're the local player.
        targetCamera = Camera.main;
        startingZ = targetCamera.transform.position.z;
        controller = GetComponentInParent<PlayerController>();
        targetCamera.GetComponentsInChildren(secondaryPositioners);
    }

    public void LateUpdate() {
        currentPosition = CalculateNewPosition();
        if (!IsControllingCamera)
            return;

        Vector3 shakeOffset = Vector3.zero;
        if ((ScreenShake -= Time.deltaTime) > 0 && controller.IsOnGround)
            shakeOffset = new Vector3((Random.value - 0.5f) * ScreenShake, (Random.value - 0.5f) * ScreenShake);

        SetPosition(currentPosition + shakeOffset);
    }

    public void Recenter(Vector2 pos) {
        playerPos = currentPosition = pos + AirOffset;
        smoothDampVel = Vector3.zero;
        SetPosition(playerPos);
    }

    private void SetPosition(Vector3 position) {
        if (!IsControllingCamera)
            return;

        targetCamera.transform.position = position;
        if (BackgroundLoop.Instance)
            BackgroundLoop.Instance.Reposition();

        secondaryPositioners.RemoveAll(scp => !scp);
        secondaryPositioners.ForEach(scp => scp.UpdatePosition());
    }

    private Vector3 CalculateNewPosition() {
        float minY = GameManager.Instance.cameraMinY, heightY = GameManager.Instance.cameraHeightY;
        float minX = GameManager.Instance.cameraMinX, maxX = GameManager.Instance.cameraMaxX;

        if (!controller.IsDead && !controller.IsRespawning)
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
            currentPosition.x += (right ? -1 : 1) * GameManager.Instance.LevelWidth;
            xDifference = Vector2.Distance(Vector2.right * currentPosition.x, Vector2.right * playerPos.x);
            right = currentPosition.x > playerPos.x;
            if (IsControllingCamera)
                BackgroundLoop.Instance.teleportedThisFrame = true;
        }

        if (xDifference > 0.25f)
            currentPosition.x += (0.25f - xDifference - 0.01f) * (right ? 1 : -1);

        // lagging camera movements
        Vector3 targetPosition = currentPosition;
        if (controller.IsOnGround)
            lastFloor = playerPos.y;
        bool validFloor = controller.IsOnGround || lastFloor < playerPos.y;

        //top camera clip ON GROUND. slowly pan up, dont do it instantly.
        if (validFloor && lastFloor - (currentPosition.y + vOrtho) + cameraTopMax + 2f > 0)
            targetPosition.y = playerPos.y - vOrtho + cameraTopMax + 2f;

        // Smoothing
        targetPosition = Vector3.SmoothDamp(currentPosition, targetPosition, ref smoothDampVel, 0.5f);

        // Clamping to within level bounds

        targetPosition.x = Mathf.Clamp(targetPosition.x, minX + xOrtho, maxX - xOrtho);
        targetPosition.y = Mathf.Clamp(targetPosition.y, minY + vOrtho, heightY == 0 ? (minY + vOrtho) : (minY + heightY - vOrtho));

        // Z preservation

        //targetPosition = AntiJitter(targetPosition);
        targetPosition.z = startingZ;

        return targetPosition;
    }

    //---DEBUG
#if UNITY_EDITOR
    private static Vector3 HalfRight = Vector3.right * 0.5f;
    public void OnDrawGizmos() {
        if (!controller)
            return;

        Gizmos.color = Color.blue;
        Vector2 threshold = controller.IsOnGround ? GroundedThreshold : AirThreshold;
        Gizmos.DrawWireCube(playerPos, threshold * 2);
        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(new(playerPos.x, lastFloor), HalfRight);
    }
#endif

    private static Vector2 AntiJitter(Vector3 vec) {
        vec.y = ((int) (vec.y * 100)) * 0.01f;
        return vec;
    }
}
