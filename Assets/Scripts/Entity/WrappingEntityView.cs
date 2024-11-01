using Quantum;
using UnityEngine;

public unsafe class WrappingEntityView : QuantumEntityView {

    /*
    //---Private Variables
    private VersusStageData stage;
    */

    protected override void ApplyTransform(ref UpdatePositionParameter param) {
        float previousZ = transform.position.z;
        Quaternion previousRotation = transform.rotation;

        base.ApplyTransform(ref param);

        Vector3 newPosition = transform.position;
        newPosition.z = previousZ;
        transform.SetPositionAndRotation(newPosition, previousRotation);

        /*
        Frame f = Game.Frames.Predicted;
        if (!stage) {
            stage = (VersusStageData) QuantumUnityDB.GetGlobalAsset(f.Map.UserAsset);
        }
        Frame fpup = Game.Frames.PreviousUpdatePredicted;
        Frame fp = Game.Frames.PredictedPrevious;
        if (!fp.Has<Transform2D>(EntityRef)
            || !fp.Has<Transform2D>(EntityRef)) {
            return;
        }

        // TODO: FIX ME DADDY!!
        FPVector2 previousPosition = fp.Unsafe.GetPointer<Transform2D>(EntityRef)->Position;
        FPVector2 targetPosition = f.Unsafe.GetPointer<Transform2D>(EntityRef)->Position;

        QuantumUtils.UnwrapWorldLocations(stage, previousPosition, targetPosition, out FPVector2 oldUnwrapped, out FPVector2 newUnwrapped);
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
        */
    }

    public void OnDrawGizmosSelected() {
        if (TryGetComponent(out QPrototypeCullable cullable)) {
            Gizmos.color = new Color(1, 0, 0, 0.5f);
            Gizmos.DrawSphere(transform.position + cullable.Prototype.Offset.ToUnityVector3(), cullable.Prototype.BroadRadius.AsFloat);
        }
    }
}