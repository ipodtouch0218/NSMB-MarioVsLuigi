using Photon.Deterministic;
using Quantum;
using UnityEngine;

public class WrappingEntityView : QuantumEntityView {

    //---Private Variables
    private VersusStageData stage;

    protected override void ApplyTransform(ref UpdatePositionParameter param) {
        Frame current = Game.Frames.Predicted;
        if (!stage) {
            stage = (VersusStageData) QuantumUnityDB.GetGlobalAsset(current.Map.UserAsset);
        }

        Frame previous = Game.Frames.PredictedPrevious;
        if (!previous.Has<Transform2D>(EntityRef)
            || !previous.Has<Transform2D>(EntityRef)) {
            return;
        }

        FPVector2 previousPosition = previous.Get<Transform2D>(EntityRef).Position;
        FPVector2 currentPosition = current.Get<Transform2D>(EntityRef).Position + param.ErrorVisualVector.ToFPVector2();

        QuantumUtils.UnwrapWorldLocations(stage, previousPosition, currentPosition, out FPVector2 oldUnwrapped, out FPVector2 newUnwrapped);
        FPVector2 change = newUnwrapped - oldUnwrapped;

        FPVector2 result;
        if (change.SqrMagnitude > 2 * 2) {
            result = oldUnwrapped + change;
        } else {
            result = FPVector2.Lerp(oldUnwrapped, oldUnwrapped + change, FP.FromFloat_UNSAFE(Game.InterpolationFactor));
        }

        result = QuantumUtils.WrapWorld(stage, result, out _);

        Vector3 unityResult = result.ToUnityVector3();
        unityResult.z = transform.position.z;
        transform.position = unityResult;
    }

}