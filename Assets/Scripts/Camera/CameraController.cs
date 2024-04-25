using System.Collections.Generic;
using UnityEngine;

using Fusion;
using NSMB.Entities.Player;
using NSMB.Extensions;
using NSMB.Game;
using NSMB.Utils;

//[OrderBefore(typeof(Powerup))]
public class CameraController : NetworkBehaviour {

    //---Static Variables
    private static readonly Vector2 AirOffset = new(0, 0.65f);
    private static CameraController CurrentController;

    private static float _screenShake;
    public static float ScreenShake {
        get => _screenShake;
        set {
            if (CurrentController && !CurrentController.controller.IsOnGround) {
                return;
            }

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
    public Camera TargetCamera { get; private set; }

    //---Serialized Variables
    [SerializeField] private float floorOffset = 1f;
    [SerializeField] private PlayerController controller;

    //---Private Variables
    private readonly List<SecondaryCameraPositioner> secondaryPositioners = new();
    private PropertyReader<Vector3> currentPositionReader;
    private Vector2 previousCurrentPosition;

    public void OnValidate() {
        this.SetIfNull(ref controller, UnityExtensions.GetComponentType.Parent);
    }

    public void Awake() {
        TargetCamera = Camera.main;
        TargetCamera.GetComponentsInChildren(secondaryPositioners);
    }

    public override void Spawned() {
        currentPositionReader = GetPropertyReader<Vector3>(nameof(CurrentPosition));
        Object.RenderTimeframe = !controller.IsProxy ? RenderTimeframe.Auto : RenderTimeframe.Remote;
    }

    public override void Render() {
        if (!IsControllingCamera) {
            return;
        }

        Vector3 newPosition;

        if (TryGetSnapshotsBuffers(out var from, out var to, out float alpha)) {
            Vector2 fromVector, toVector;

            if (Object.RenderTimeframe == RenderTimeframe.Remote) {
                // Weird interpolation stuff taken from EntityMover
                fromVector = previousCurrentPosition;
                toVector = CurrentPosition;
                previousCurrentPosition = CurrentPosition;
            } else {
                (fromVector, toVector) = currentPositionReader.Read(from, to);
            }

            Vector2 dest;
            Utils.UnwrapLocations(fromVector, toVector, out var fromVectorRelative, out var toVectorRelative);
            if (Vector2.Distance(fromVectorRelative, toVectorRelative) > 1f) {
                dest = toVectorRelative;
            } else {
                dest = Vector2.Lerp(fromVectorRelative, toVectorRelative, alpha);
            }
            //Utils.WrapWorldLocation(ref dest);

            newPosition = dest;
            newPosition.z = CurrentPosition.z;
        } else {
            newPosition = CurrentPosition;
        }

        Vector3 shakeOffset = Vector3.zero;
        if ((_screenShake -= Time.deltaTime) > 0) {
            shakeOffset = new Vector3((Random.value - 0.5f) * _screenShake, (Random.value - 0.5f) * _screenShake);
        }

        SetPosition(newPosition + shakeOffset);
    }

    public override void FixedUpdateNetwork() {
        CurrentPosition = CalculateNewPosition(Runner.DeltaTime);
    }

    public void Recenter(Vector2 pos) {
        PlayerPos = CurrentPosition = pos + AirOffset;
        LastFloorHeight = CurrentPosition.y;
        SmoothDampVel = Vector3.zero;
        SetPosition(PlayerPos);
    }

    private void SetPosition(Vector3 position) {
        if (!IsControllingCamera) {
            return;
        }

        TargetCamera.transform.position = position;
        if (BackgroundLoop.Instance) {
            BackgroundLoop.Instance.Reposition();
        }

        secondaryPositioners.RemoveAll(scp => !scp);
        secondaryPositioners.ForEach(scp => scp.UpdatePosition());
    }

    private Vector3 CalculateNewPosition(float delta) {
        float minY = GameManager.Instance.cameraMinY, heightY = GameManager.Instance.cameraHeightY;
        float minX = GameManager.Instance.cameraMinX, maxX = GameManager.Instance.cameraMaxX;

        if (!controller.IsDead && !controller.IsRespawning) {
            PlayerPos = AntiJitter(controller.transform.position);
        }

        float vOrtho = TargetCamera.orthographicSize;
        float xOrtho = vOrtho * TargetCamera.aspect;
        Vector3 newCameraPosition = CurrentPosition;

        // Lagging camera movements
        if (controller.IsOnGround) {
            LastFloorHeight = PlayerPos.y;
        }

        bool validFloor = controller.IsOnGround || LastFloorHeight < PlayerPos.y;

        // Floor height
        if (validFloor) {
            newCameraPosition.y = Mathf.Max(newCameraPosition.y, LastFloorHeight + floorOffset);
        }

        // Smoothing
        Vector3 smoothDamp = SmoothDampVel;
        newCameraPosition = Vector3.SmoothDamp(CurrentPosition, newCameraPosition, ref smoothDamp, 0.5f, float.MaxValue, delta);
        SmoothDampVel = smoothDamp;

        // Bottom camera clip
        float cameraBottom = newCameraPosition.y - vOrtho;
        float cameraBottomDistanceToPlayer = PlayerPos.y - cameraBottom;
        float cameraBottomMinDistance = (2.5f/3.5f) * vOrtho;

        if (cameraBottomDistanceToPlayer < cameraBottomMinDistance) {
            newCameraPosition.y -= (cameraBottomMinDistance - cameraBottomDistanceToPlayer);
            SmoothDampVel = new(SmoothDampVel.x, 0);
        }

        // Top camera clip
        float playerHeight = controller.models.transform.lossyScale.y;
        float cameraTop = newCameraPosition.y + vOrtho;
        float cameraTopDistanceToPlayer = cameraTop - (PlayerPos.y + playerHeight);
        float cameraTopMinDistance = (1.25f/3.5f) * vOrtho;

        if (cameraTopDistanceToPlayer < cameraTopMinDistance) {
            newCameraPosition.y += (cameraTopMinDistance - cameraTopDistanceToPlayer);
            SmoothDampVel = new(SmoothDampVel.x, 0);
        }

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

        if (xDifference > 0.25f) {
            newCameraPosition.x += (0.25f - xDifference - 0.01f) * (right ? 1 : -1);
        }

        // Clamping to within level bounds
        float maxY = heightY == 0 ? (minY + vOrtho) : (minY + heightY - vOrtho);
        if (newCameraPosition.y > maxY) {
            SmoothDampVel = Vector3.zero;
        }

        newCameraPosition.x = Mathf.Clamp(newCameraPosition.x, minX + xOrtho, maxX - xOrtho);
        newCameraPosition.y = Mathf.Clamp(newCameraPosition.y, minY + vOrtho, maxY);

        // Z preservation
        newCameraPosition.z = -10;

        return newCameraPosition;
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
        if (!controller || !controller.Object) {
            return;
        }

        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(new(PlayerPos.x, LastFloorHeight), HalfRight);
    }
#endif
}
