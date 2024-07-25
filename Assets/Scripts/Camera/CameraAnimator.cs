using Photon.Deterministic;
using Quantum;
using System.Collections.Generic;
using UnityEngine;

public class CameraAnimator : QuantumCallbacks {

    //---Properties
    public EntityRef Target { get; set; }

    //---Serialized Variables
    [SerializeField] private Camera camera;
    [SerializeField] private List<SecondaryCameraPositioner> secondaryPositioners;

    //---Private Variables
    private VersusStageData stage;

    public void OnValidate() {
        GetComponentsInChildren(secondaryPositioners); 
    }

    public void Start() {
        stage = (VersusStageData) QuantumUnityDB.GetGlobalAsset(FindObjectOfType<QuantumMapData>().Asset.UserAsset);
    }

    public override void OnUpdateView(QuantumGame game) {
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
            FP width = stage.TileDimensions.x / FP._2;
            difference.X += (difference.X < 0) ? width : -width;
            target = origin + difference;
        }

        Vector3 newPosition = QuantumUtils.WrapWorld(game.Frames.Predicted, FPVector2.Lerp(origin, target, game.InterpolationFactor.ToFP()), out _).ToUnityVector3();
        newPosition.z = -10;
        camera.transform.position = newPosition;
        if (BackgroundLoop.Instance) {
            BackgroundLoop.Instance.Reposition(camera);
        }

        if (camera.orthographicSize < 14/4f) {
            // Offset to always put the player in the center for extremely long aspect ratios
            var targetTransformPrevious = game.Frames.PredictedPrevious.Get<Transform2D>(Target);
            var targetTransformCurrent = game.Frames.Predicted.Get<Transform2D>(Target);
            var targetCollider = game.Frames.Predicted.Get<PhysicsCollider2D>(Target);

            float cameraFocusY = Mathf.Lerp(targetTransformPrevious.Position.Y.AsFloat, targetTransformCurrent.Position.Y.AsFloat, game.InterpolationFactor) + targetCollider.Shape.Centroid.Y.AsFloat;
            Vector3 offsetPosition = camera.transform.position;
            offsetPosition.y -= offsetPosition.y - cameraFocusY;
            offsetPosition.y = Mathf.Clamp(offsetPosition.y, stage.CameraMinPosition.Y.AsFloat + camera.orthographicSize, Mathf.Max(stage.CameraMinPosition.Y.AsFloat + 7, stage.CameraMaxPosition.Y.AsFloat) - camera.orthographicSize);
            camera.transform.position = offsetPosition;
        }

        secondaryPositioners.RemoveAll(scp => !scp);
        secondaryPositioners.ForEach(scp => scp.UpdatePosition());
    }
}
