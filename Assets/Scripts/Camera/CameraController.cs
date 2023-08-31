using System.Collections.Generic;
using UnityEngine;

using Fusion;
using NSMB.Entities.Collectable.Powerups;
using NSMB.Entities.Player;
using NSMB.Game;
using NSMB.Utils;

[OrderBefore(typeof(Powerup))]
//[OrderAfter(typeof(NetworkRigidbody2D), typeof(PlayerController))]
public class CameraController : NetworkBehaviour {

    //---Static Variables
    private static readonly Vector2 AirOffset = new(0, 0.65f);
    private static readonly Vector2 AirThreshold = new(0.6f, 1.3f), GroundedThreshold = new(0.6f, 0f);
    private static CameraController CurrentController;

    private static float _screenShake;
    public static float ScreenShake {
        get => _screenShake;
        set {
            if (CurrentController && !CurrentController.controller.IsOnGround)
                return;

            _screenShake = value;
        }
    }

    //---Networked Variables
    [Networked, HideInInspector] public Vector3 CurrentPosition { get; set; }
    [Networked] private Vector3 SmoothDampVel { get; set; }
    [Networked] private Vector3 PlayerPos { get; set; }
    [Networked] private float LastFloorHeight { get; set; }

    //---Properties
    private bool _isControllingCamera;
    public bool IsControllingCamera {
        get => _isControllingCamera;
        set {
            _isControllingCamera = value;
            if (value) {
                UIUpdater.Instance.player = controller;
                CurrentController = this;
            }
        }
    }

    //---Serialized Variables
    [SerializeField] private float floorOffset = 1f;
    [SerializeField] private PlayerController controller;

    //---Private Variables
    private readonly List<SecondaryCameraPositioner> secondaryPositioners = new();
    private Camera targetCamera;
    private Interpolator<Vector3> positionInterpolator;
    private float currentExtrapolationValue;

    public void OnValidate() {
        if (!controller) controller = GetComponentInParent<PlayerController>();
    }

    public void Awake() {
        targetCamera = Camera.main;
        targetCamera.GetComponentsInChildren(secondaryPositioners);
    }

    public override void Spawned() {
        positionInterpolator = GetInterpolator<Vector3>(nameof(CurrentPosition));
    }

    public void LateUpdate() {
        if (!IsControllingCamera)
            return;

        float delta = (Runner.SimulationRenderTime - Runner.SimulationTime) * Runner.Simulation.Config.TickRate;
        float difference = delta - currentExtrapolationValue;
        CurrentPosition = CalculateNewPosition(Runner.DeltaTime * difference);
        currentExtrapolationValue = delta;

        Vector3 shakeOffset = Vector3.zero;
        if ((_screenShake -= Time.deltaTime) > 0)
            shakeOffset = new Vector3((Random.value - 0.5f) * _screenShake, (Random.value - 0.5f) * _screenShake);

        SetPosition(CurrentPosition + shakeOffset);
    }

    public override void FixedUpdateNetwork() {
        CurrentPosition = CalculateNewPosition(Runner.DeltaTime);
        currentExtrapolationValue = 0;
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

    private Vector3 CalculateNewPosition(float delta) {
        float minY = GameManager.Instance.cameraMinY, heightY = GameManager.Instance.cameraHeightY;
        float minX = GameManager.Instance.cameraMinX, maxX = GameManager.Instance.cameraMaxX;

        if (!controller.IsDead && !controller.IsRespawning)
            PlayerPos = AntiJitter(controller.body.interpolationTarget.position);

        float vOrtho = targetCamera.orthographicSize;
        float xOrtho = vOrtho * targetCamera.aspect;

        // Instant camera movements. we dont want to lag behind in these cases
        Vector3 newCameraPosition = CurrentPosition;

        // Bottom camera clip
        float cameraBottom = newCameraPosition.y - vOrtho;
        float cameraBottomDistanceToPlayer = PlayerPos.y - cameraBottom;
        float cameraBottomMinDistance = (2.5f/3.5f) * vOrtho;

        if (cameraBottomDistanceToPlayer < cameraBottomMinDistance)
            newCameraPosition.y -= (cameraBottomMinDistance - cameraBottomDistanceToPlayer);

        // Top camera clip
        float playerHeight = controller.transform.localScale.y;
        float cameraTop = newCameraPosition.y + vOrtho;
        float cameraTopDistanceToPlayer = cameraTop - (PlayerPos.y + playerHeight);
        float cameraTopMinDistance = (1.25f/3.5f) * vOrtho;

        if (cameraTopDistanceToPlayer < cameraTopMinDistance)
            newCameraPosition.y += (cameraTopMinDistance - cameraTopDistanceToPlayer);

        Vector3 wrappedPos = PlayerPos;
        Utils.WrapWorldLocation(ref wrappedPos);
        PlayerPos = wrappedPos;

        float xDifference = Vector2.Distance(Vector2.right * newCameraPosition.x, Vector2.right * PlayerPos.x);
        bool right = newCameraPosition.x > PlayerPos.x;

        if (xDifference >= 2) {
            newCameraPosition.x += (right ? -1 : 1) * GameManager.Instance.LevelWidth;
            xDifference = Vector2.Distance(Vector2.right * newCameraPosition.x, Vector2.right * PlayerPos.x);
            right = newCameraPosition.x > PlayerPos.x;
        }

        if (xDifference > 0.25f)
            newCameraPosition.x += (0.25f - xDifference - 0.01f) * (right ? 1 : -1);

        // Lagging camera movements
        Vector3 targetPosition = newCameraPosition;
        if (controller.IsOnGround)
            LastFloorHeight = PlayerPos.y;
        bool validFloor = controller.IsOnGround || LastFloorHeight < PlayerPos.y;

        // Floor height
        if (validFloor)
            targetPosition.y = Mathf.Max(targetPosition.y, LastFloorHeight + floorOffset);

        // Smoothing
        Vector3 smoothDamp = SmoothDampVel;
        targetPosition = Vector3.SmoothDamp(newCameraPosition, targetPosition, ref smoothDamp, 0.5f, float.MaxValue, delta);
        SmoothDampVel = smoothDamp;

        // Clamping to within level bounds
        float maxY = heightY == 0 ? (minY + vOrtho) : (minY + heightY - vOrtho);
        if (targetPosition.y > maxY)
            SmoothDampVel = Vector3.zero;

        targetPosition.x = Mathf.Clamp(targetPosition.x, minX + xOrtho, maxX - xOrtho);
        targetPosition.y = Mathf.Clamp(targetPosition.y, minY + vOrtho, maxY);

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
