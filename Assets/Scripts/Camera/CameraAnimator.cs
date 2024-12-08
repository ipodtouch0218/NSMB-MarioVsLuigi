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

    public override void OnDestroy() {
        base.OnDestroy();
        OnScreenshake -= OnScreenshakeCallback;
    }

    public void OnUpdateView(CallbackUpdateView e) {
        QuantumGame game = e.Game;
        Frame f = game.Frames.Predicted;
        Frame fp = game.Frames.PredictedPrevious;

        if (!Target.IsValid || !f.Exists(Target) || !fp.Exists(Target)) {
            return;
        }


        var cameraControllerCurrent = f.Unsafe.GetPointer<CameraController>(Target);
        var cameraControllerPrevious = fp.Unsafe.GetPointer<CameraController>(Target);

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
        Vector2 cameraFocus = Vector2.Lerp(targetTransformPrevious->Position.ToUnityVector2(), targetTransformCurrent->Position.ToUnityVector2(), game.InterpolationFactor);
        cameraFocus.y += playerHeight * 0.5f;

        if (!targetMario->IsDead || targetMario->IsRespawning) {
            float cameraHalfHeight = ourCamera.orthographicSize - (playerHeight * 0.5f) - 0.25f;
            newPosition.y = Mathf.Clamp(newPosition.y, cameraFocus.y - cameraHalfHeight, cameraFocus.y + cameraHalfHeight);
        }

        // Clamp
        float cameraMinX = stage.CameraMinPosition.X.AsFloat - (ourCamera.orthographicSize * ourCamera.aspect);
        float cameraMaxX = stage.CameraMaxPosition.X.AsFloat + (ourCamera.orthographicSize * ourCamera.aspect);
        newPosition.x = Mathf.Clamp(newPosition.x, cameraMinX, cameraMaxX);

        float cameraMinY = stage.CameraMinPosition.Y.AsFloat + ourCamera.orthographicSize;
        float cameraMaxY = Mathf.Max(stage.CameraMinPosition.Y.AsFloat + 7, stage.CameraMaxPosition.Y.AsFloat) - ourCamera.orthographicSize;
        newPosition.y = Mathf.Clamp(newPosition.y, cameraMinY, cameraMaxY);

        // Screenshake (ignores clamping)
        if ((screenshakeTimer -= Time.deltaTime) > 0) {
            newPosition += new Vector3((UnityEngine.Random.value - 0.5f) * screenshakeTimer, (UnityEngine.Random.value - 0.5f) * screenshakeTimer);
        }

        ourCamera.transform.position = newPosition;
        secondaryPositioners.RemoveAll(scp => !scp);
        secondaryPositioners.ForEach(scp => scp.UpdatePosition());

        if (BackgroundLoop.Instance) {
            BackgroundLoop.Instance.Reposition(ourCamera);
        }

    }

    private void OnScreenshakeCallback(float screenshake) {
        Frame f = QuantumRunner.DefaultGame.Frames.Predicted;

        if (f.Unsafe.TryGetPointer(Target, out PhysicsObject* physicsObject)
            && physicsObject->IsTouchingGround) {

            screenshakeTimer += screenshake;
        }
    }
}
