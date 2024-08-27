using Photon.Deterministic;
using Quantum;
using System.Collections.Generic;
using UnityEngine;

public class CameraAnimator : ResizingCamera {

    //---Properties
    public EntityRef Target { get; set; }

    //---Serialized Variables
    [SerializeField] private List<SecondaryCameraPositioner> secondaryPositioners;

    //---Private Variables
    private VersusStageData stage;

    public override void OnValidate() {
        base.OnValidate();
        GetComponentsInChildren(secondaryPositioners); 
    }

    public override void Start() {
        QuantumCallback.Subscribe<CallbackUpdateView>(this, OnUpdateView);
        stage = (VersusStageData) QuantumUnityDB.GetGlobalAsset(FindObjectOfType<QuantumMapData>().Asset.UserAsset);
    }

    public void OnUpdateView(CallbackUpdateView e) {
        QuantumGame game = e.Game;
        if (!Target.IsValid || !game.Frames.Predicted.Exists(Target) || !game.Frames.PredictedPrevious.Exists(Target)) {
            return;
        }

        var cameraControllerCurrent = game.Frames.Predicted.Get<CameraController>(Target);
        var cameraControllerPrevious = game.Frames.PredictedPrevious.Get<CameraController>(Target);

        FPVector2 origin = cameraControllerPrevious.CurrentPosition;
        FPVector2 target = cameraControllerCurrent.CurrentPosition;
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
        camera.transform.position = newPosition;
        if (BackgroundLoop.Instance) {
            BackgroundLoop.Instance.Reposition(camera);
        }

        var targetTransformPrevious = game.Frames.PredictedPrevious.Get<Transform2D>(Target);
        var targetTransformCurrent = game.Frames.Predicted.Get<Transform2D>(Target);
        var targetMario = game.Frames.Predicted.Get<MarioPlayer>(Target);

        float playerHeight = targetMario.CurrentPowerupState switch {
            PowerupState.MegaMushroom => 3.5f,
            > PowerupState.Mushroom => 1f,
            _ => 0.5f,
        };

        // Offset to always put the player in the center for extremely long aspect ratios
        if (!targetMario.IsDead || targetMario.IsRespawning) {
            float cameraFocusY = Mathf.Lerp(targetTransformPrevious.Position.Y.AsFloat, targetTransformCurrent.Position.Y.AsFloat, game.InterpolationFactor) + (playerHeight * 0.5f);
            float cameraHalfHeight = camera.orthographicSize - (playerHeight * 0.5f) - 0.25f;
            newPosition.y = Mathf.Clamp(newPosition.y, cameraFocusY - cameraHalfHeight, cameraFocusY + cameraHalfHeight);
        }

        // Clamp
        float cameraMinY = stage.CameraMinPosition.Y.AsFloat + camera.orthographicSize;
        float cameraMaxY = Mathf.Max(stage.CameraMinPosition.Y.AsFloat + 7, stage.CameraMaxPosition.Y.AsFloat) - camera.orthographicSize;
        newPosition.y = Mathf.Clamp(newPosition.y, cameraMinY, cameraMaxY);

        camera.transform.position = newPosition;
        secondaryPositioners.RemoveAll(scp => !scp);
        secondaryPositioners.ForEach(scp => scp.UpdatePosition());
    }
}
