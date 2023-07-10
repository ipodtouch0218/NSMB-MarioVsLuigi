using UnityEngine;

using Fusion;
using NSMB.Game;
using UnityEngine.Rendering.UI;

public class GenericMover : NetworkBehaviour {

    //---Networked Variables
    [Networked] private Vector3 Origin { get; set; }

    //---Serialized Variables
    [SerializeField] private AnimationCurve x, y;
    [SerializeField] private float animationOffset = 0;
    [SerializeField] private Transform interpolationTarget;
    [SerializeField] private bool notNetworked;

    //---Private Variables
    private Vector3 origin;
    private float? xCurveLength, yCurveLength;

    public void Start() {
        if (notNetworked)
            origin = transform.position;
    }

    public override void Spawned() {
        Origin = transform.position;
    }

    public void Update() {
        if (notNetworked)
            SetPosition(interpolationTarget, origin, Time.time);
    }

    public override void Render() {
        SetPosition(interpolationTarget, Origin, Runner.SimulationRenderTime - GameData.Instance.GameStartTime);
    }

    public override void FixedUpdateNetwork() {
        SetPosition(transform, Origin, Runner.SimulationTime - GameData.Instance.GameStartTime);
    }

    private void SetPosition(Transform target, Vector3 origin, float secondsElapsed) {
        if (!target)
            target = transform;

        float xOffset = EvaluateCurve(x, ref xCurveLength, animationOffset, secondsElapsed);
        float yOffset = EvaluateCurve(y, ref yCurveLength, animationOffset, secondsElapsed);

        target.position = origin + new Vector3(xOffset, yOffset, 0);
    }

    private static float EvaluateCurve(AnimationCurve curve, ref float? length, double offset, double time) {
        if (curve.length <= 0)
            return 0;

        length ??= curve.keys[^1].time;

        return curve.Evaluate((float) ((time + (offset * length)) % length));
    }
}
