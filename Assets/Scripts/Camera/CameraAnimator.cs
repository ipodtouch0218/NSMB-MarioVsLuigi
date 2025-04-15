using NSMB.Background;
using NSMB.Extensions;
using NSMB.UI.Game;
using Photon.Deterministic;
using Quantum;
using System;
using System.Collections.Generic;
using UnityEngine;

public unsafe class CameraAnimator : ResizingCamera {

    //---Static Variables
    private static event Action<float> OnScreenshake;
    public static void TriggerScreenshake(float duration) => OnScreenshake?.Invoke(duration);

    //---Properties
    public EntityRef Target => playerElements.Entity;
    public CameraMode Mode { get; set; } = CameraMode.FollowPlayer;

    //---Serialized Variables
    [SerializeField] private PlayerElements playerElements;
    [SerializeField] private List<SecondaryCameraPositioner> secondaryPositioners;

    //---Private Variables
    private VersusStageData stage;
    private float screenshakeTimer;

    public override void OnValidate() {
        base.OnValidate();
        GetComponentsInChildren(secondaryPositioners);
        this.SetIfNull(ref playerElements, UnityExtensions.GetComponentType.Parent);
    }

    public override void Start() {
        base.Start();
        QuantumCallback.Subscribe<CallbackUpdateView>(this, OnUpdateView);
        stage = (VersusStageData) QuantumUnityDB.GetGlobalAsset(FindObjectOfType<QuantumMapData>().Asset.UserAsset);

        OnScreenshake += OnScreenshakeCallback;
    }

    public void OnDestroy() {
        OnScreenshake -= OnScreenshakeCallback;
    }

    public override void Update() {
        // Do nothing, let OnUpdateView handle it.
    }

    public void OnUpdateView(CallbackUpdateView e) {

        switch (Mode) {
        case CameraMode.FollowPlayer:
            UpdateCameraFollowPlayerMode(e);
            break;
        case CameraMode.Freecam:
            UpdateCameraFreecamMode(e);
            break;
        }

        secondaryPositioners.RemoveAll(scp => !scp);
        secondaryPositioners.ForEach(scp => scp.UpdatePosition());

        if (BackgroundLoop.Instance) {
            BackgroundLoop.Instance.Reposition(ourCamera);
        }
    }

    private void UpdateCameraFollowPlayerMode(CallbackUpdateView e) {
        QuantumGame game = e.Game;
        Frame f = game.Frames.Predicted;
        Frame fp = game.Frames.PredictedPrevious;

        if (!Target.IsValid || !f.Exists(Target) || !fp.Exists(Target)) {
            return;
        }

        var cameraControllerCurrent = f.Unsafe.GetPointer<CameraController>(Target);
        var cameraControllerPrevious = fp.Unsafe.GetPointer<CameraController>(Target);

        // Resize from game
        float targetOrthoSize = Mathf.Lerp(cameraControllerPrevious->OrthographicSize.AsFloat, cameraControllerCurrent->OrthographicSize.AsFloat, game.InterpolationFactor);
        ClampCameraAspectRatio(targetOrthoSize * 0.5f);

        FPVector2 origin = cameraControllerPrevious->CurrentPosition;
        FPVector2 target = cameraControllerCurrent->CurrentPosition;
        FPVector2 difference = target - origin;
        FP distance = difference.Magnitude;
        if (distance > 10) {
            // Wrapped the level
            //difference.X *= -1;
            FP width = stage.TileDimensions.x / (FP) 2;
            difference.X += (difference.X < 0) ? width : -width;
            target = origin + difference;
        }

        Vector3 newPosition = QuantumUtils.WrapWorld(stage, FPVector2.Lerp(origin, target, game.InterpolationFactor.ToFP()), out _).ToUnityVector3();
        newPosition.z = -10;

        var targetTransformPrevious = fp.Unsafe.GetPointer<Transform2D>(Target);
        var targetTransformCurrent = f.Unsafe.GetPointer<Transform2D>(Target);
        var targetMario = f.Unsafe.GetPointer<MarioPlayer>(Target);

        float playerHeight = targetMario->CurrentPowerupState switch {
            PowerupState.MegaMushroom => 3.5f,
            > PowerupState.Mushroom => 1f,
            _ => 0.5f,
        };

        // Offset to always put the player in the center for extremely long aspect ratios
        float screenAspect = ourCamera.aspect;
        float orthoSize = ourCamera.orthographicSize;
        if (Mathf.Abs((16f / 9f) - screenAspect) < 0.05f) {
            screenAspect = 16f / 9f;
        }

        Vector2 cameraFocus = Vector2.Lerp(targetTransformPrevious->Position.ToUnityVector2(), targetTransformCurrent->Position.ToUnityVector2(), game.InterpolationFactor);
        cameraFocus.y += playerHeight * 0.5f;

        if (!targetMario->IsDead || targetMario->IsRespawning) {
            float cameraHalfHeight = orthoSize - (playerHeight * 0.5f) - 0.25f;
            newPosition.y = Mathf.Clamp(newPosition.y, cameraFocus.y - cameraHalfHeight, cameraFocus.y + cameraHalfHeight);
        }

        // Clamp
        float cameraMinX = stage.CameraMinPosition.X.AsFloat + (orthoSize * screenAspect);
        float cameraMaxX = stage.CameraMaxPosition.X.AsFloat - (orthoSize * screenAspect);
        newPosition.x = Mathf.Clamp(newPosition.x, cameraMinX, cameraMaxX);

        float cameraMinY = stage.CameraMinPosition.Y.AsFloat + orthoSize;
        float cameraMaxY = Mathf.Max(stage.CameraMinPosition.Y.AsFloat + Mathf.Max(7, orthoSize * 2), stage.CameraMaxPosition.Y.AsFloat) - orthoSize;
        newPosition.y = Mathf.Clamp(newPosition.y, cameraMinY, cameraMaxY);

        // Screenshake (ignores clamping)
        if ((screenshakeTimer -= Time.deltaTime) > 0) {
            newPosition += new Vector3((UnityEngine.Random.value - 0.5f) * screenshakeTimer, (UnityEngine.Random.value - 0.5f) * screenshakeTimer);
        }

        ourCamera.transform.position = newPosition;
    }

    private void UpdateCameraFreecamMode(CallbackUpdateView e) {

    }

    private void OnScreenshakeCallback(float screenshake) {
        Frame f = QuantumRunner.DefaultGame.Frames.Predicted;

        if (!f.Unsafe.TryGetPointer(Target, out PhysicsObject* physicsObject)
            || physicsObject->IsTouchingGround) {

            screenshakeTimer += screenshake;
        }
    }

    public enum CameraMode {
        FollowPlayer, Freecam
    }
}
