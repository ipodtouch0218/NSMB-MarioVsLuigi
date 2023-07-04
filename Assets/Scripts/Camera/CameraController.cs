using System.Collections.Generic;
using UnityEngine;

using Fusion;
using NSMB.Entities.Player;
using NSMB.Game;
using NSMB.Utils;

//[SimulationBehaviour(Stages = SimulationStages.Forward)]
public class CameraController : NetworkBehaviour {

    //---Static Variables
    private static readonly Vector2 AirOffset = new(0, .65f);
    private static readonly Vector2 AirThreshold = new(0.6f, 1.3f), GroundedThreshold = new(0.6f, 0f);
    public static float ScreenShake = 0;

    //---Networked Variables
    [Networked] public Vector3 CurrentPosition { get; set; }
    [Networked] private Vector3 SmoothDampVel { get; set; }
    [Networked] private Vector3 PlayerPos { get; set; }
    [Networked] private float LastFloorHeight { get; set; }

    //---Public Variables
    //public Vector3 currentPosition;

    private bool _isControllingCamera;
    public bool IsControllingCamera {
        get => _isControllingCamera;
        set {
            _isControllingCamera = value;
            if (value)
                UIUpdater.Instance.player = controller;
        }
    }

    //---Serialized Variables
    [SerializeField] private PlayerController controller;

    //---Private Variables
    private readonly List<SecondaryCameraPositioner> secondaryPositioners = new();
    private Camera targetCamera;

    public void OnValidate() {
        if (!controller) controller = GetComponentInParent<PlayerController>();
    }

    public void Awake() {
        targetCamera = Camera.main;
        targetCamera.GetComponentsInChildren(secondaryPositioners);
    }

    public override void FixedUpdateNetwork() {
        CurrentPosition = CalculateNewPosition(false);
    }

    public void LateUpdate() {
        if (!IsControllingCamera)
            return;

        Vector3 position = CalculateNewPosition(true);

        Vector3 shakeOffset = Vector3.zero;
        if ((ScreenShake -= Time.deltaTime) > 0 && controller.IsOnGround)
            shakeOffset = new Vector3((Random.value - 0.5f) * ScreenShake, (Random.value - 0.5f) * ScreenShake);

        SetPosition(position + shakeOffset);
    }

    public void Recenter(Vector2 pos) {
        PlayerPos = CurrentPosition = pos + AirOffset;
        SmoothDampVel = Vector3.zero;
        SetPosition(PlayerPos);
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

    private Vector3 CalculateNewPosition(bool render) {
        float minY = GameManager.Instance.cameraMinY, heightY = GameManager.Instance.cameraHeightY;
        float minX = GameManager.Instance.cameraMinX, maxX = GameManager.Instance.cameraMaxX;

        if (!controller.IsDead && !controller.IsRespawning)
            PlayerPos = AntiJitter(transform.position);

        float vOrtho = targetCamera.orthographicSize;
        float xOrtho = vOrtho * targetCamera.aspect;

        // Instant camera movements. we dont want to lag behind in these cases
        Vector3 newCameraPosition = CurrentPosition;

        // Bottom camera clip
        float cameraBottomMax = Mathf.Max(3.5f - transform.lossyScale.y, 1.5f);
        if (PlayerPos.y - (newCameraPosition.y - vOrtho) < cameraBottomMax)
            newCameraPosition.y = PlayerPos.y + vOrtho - cameraBottomMax;

        // Top camera clip
        float playerHeight = controller.WorldHitboxSize.y;
        float cameraTopMax = Mathf.Min(1.5f + playerHeight, 4f);
        if (PlayerPos.y - (newCameraPosition.y + vOrtho) + cameraTopMax > 0)
            newCameraPosition.y = PlayerPos.y - vOrtho + cameraTopMax;

        Vector3 wrappedPos = PlayerPos;
        Utils.WrapWorldLocation(ref wrappedPos);
        PlayerPos = wrappedPos;

        float xDifference = Vector2.Distance(Vector2.right * newCameraPosition.x, Vector2.right * PlayerPos.x);
        bool right = newCameraPosition.x > PlayerPos.x;

        if (xDifference >= 8) {
            newCameraPosition.x += (right ? -1 : 1) * GameManager.Instance.LevelWidth;
            xDifference = Vector2.Distance(Vector2.right * newCameraPosition.x, Vector2.right * PlayerPos.x);
            right = newCameraPosition.x > PlayerPos.x;
            if (IsControllingCamera)
                BackgroundLoop.Instance.teleportedThisFrame = true;
        }

        if (xDifference > 0.25f)
            newCameraPosition.x += (0.25f - xDifference - 0.01f) * (right ? 1 : -1);

        // Lagging camera movements
        Vector3 targetPosition = newCameraPosition;
        if (controller.IsOnGround)
            LastFloorHeight = PlayerPos.y;
        bool validFloor = controller.IsOnGround || LastFloorHeight < PlayerPos.y;

        // Top camera clip ON GROUND. slowly pan up, dont do it instantly.
        if (validFloor && LastFloorHeight - (newCameraPosition.y + vOrtho) + cameraTopMax + 2f > 0)
            targetPosition.y = PlayerPos.y - vOrtho + cameraTopMax + 2f;

        // Smoothing
        Vector3 smoothDamp = SmoothDampVel;
        targetPosition = Vector3.SmoothDamp(newCameraPosition, targetPosition, ref smoothDamp, 0.5f, float.MaxValue, render ? Time.deltaTime : Runner.DeltaTime);
        SmoothDampVel = smoothDamp;

        // Clamping to within level bounds
        targetPosition.x = Mathf.Clamp(targetPosition.x, minX + xOrtho, maxX - xOrtho);
        targetPosition.y = Mathf.Clamp(targetPosition.y, minY + vOrtho, heightY == 0 ? (minY + vOrtho) : (minY + heightY - vOrtho));

        // Z preservation
        targetPosition.z = -10;

        return targetPosition;
    }

    //---Helpers
    private static Vector2 AntiJitter(Vector3 vec) {
        vec.y = ((int) (vec.y * 100)) * 0.01f;
        return vec;
    }

    //---Debug
#if UNITY_EDITOR
    private static Vector3 HalfRight = Vector3.right * 0.5f;
    public void OnDrawGizmos() {
        if (!controller || !controller.Object)
            return;

        Gizmos.color = Color.blue;
        Vector2 threshold = controller.IsOnGround ? GroundedThreshold : AirThreshold;
        Gizmos.DrawWireCube(PlayerPos, threshold * 2);
        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(new(PlayerPos.x, LastFloorHeight), HalfRight);
    }
#endif
}
