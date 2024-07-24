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
        if (!Target.IsValid || !game.Frames.Predicted.Exists(Target)) {
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

        secondaryPositioners.RemoveAll(scp => !scp);
        secondaryPositioners.ForEach(scp => scp.UpdatePosition());
    }
}
