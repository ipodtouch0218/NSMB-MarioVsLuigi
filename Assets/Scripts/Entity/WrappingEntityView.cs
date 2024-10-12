using Photon.Deterministic;
using Quantum;
using UnityEngine;

public unsafe class WrappingEntityView : QuantumEntityView {

    //---Private Variables
    private VersusStageData stage;

    protected override void ApplyTransform(ref UpdatePositionParameter param) {
        Frame f = Game.Frames.Predicted;
        if (!stage) {
            stage = (VersusStageData) QuantumUnityDB.GetGlobalAsset(f.Map.UserAsset);
        }

        Frame fp = Game.Frames.PredictedPrevious;
        if (!fp.Has<Transform2D>(EntityRef)
            || !fp.Has<Transform2D>(EntityRef)) {
            return;
        }

        // TODO: FIX ME DADDY!!
        FPVector2 previousPosition = fp.Unsafe.GetPointer<Transform2D>(EntityRef)->Position;
        FPVector2 currentPosition = f.Unsafe.GetPointer<Transform2D>(EntityRef)->Position + param.ErrorVisualVector.ToFPVector2();

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