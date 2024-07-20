using NSMB.Extensions;
using Photon.Deterministic;
using Quantum;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class CameraAnimator : QuantumCallbacks {

    //---Properties
    public bool IsActive { get; set; }

    //---Serialized Variables
    [SerializeField] private QuantumEntityView entity;

    //---Private Variables
    private Transform cameraTransform;
    private List<SecondaryCameraPositioner> secondaryPositioners;
    private VersusStageData stage;

    public void OnValidate() {
        this.SetIfNull(ref entity);
    }

    public void Start() {
        cameraTransform = Camera.main.transform;
        secondaryPositioners = FindObjectsOfType<SecondaryCameraPositioner>().ToList();
        stage = (VersusStageData) QuantumUnityDB.GetGlobalAsset(FindObjectOfType<QuantumMapData>().Asset.UserAsset);
    }

    public void Initialize(QuantumGame game) {
        Frame f = game.Frames.Predicted;
        IsActive = game.PlayerIsLocal(f.Get<MarioPlayer>(entity.EntityRef).PlayerRef);
    }

    public override void OnUpdateView(QuantumGame game) {
        if (!IsActive) {
            return;
        }

        var cameraControllerCurrent = game.Frames.Predicted.Get<CameraController>(entity.EntityRef);
        var cameraControllerPrevious = game.Frames.PredictedPrevious.Get<CameraController>(entity.EntityRef);

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
        cameraTransform.position = newPosition;
        if (BackgroundLoop.Instance) {
            BackgroundLoop.Instance.Reposition();
        }

        secondaryPositioners.RemoveAll(scp => !scp);
        secondaryPositioners.ForEach(scp => scp.UpdatePosition());
    }
}
