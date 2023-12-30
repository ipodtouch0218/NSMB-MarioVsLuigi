using UnityEngine;

using Fusion;
using NSMB.Game;

//[OrderBefore(typeof(EntityMover))]
public class GenericMover : NetworkBehaviour, IBeforeTick, IWaitForGameStart {

    //---Networked Variables
    [Networked] private Vector3 Origin { get; set; }
    [Networked] private NetworkBool Enabled { get; set; }

    //---Serialized Variables
    [SerializeField] private AnimationCurve x, y;
    [SerializeField] private float animationOffset = 0;
    [SerializeField] private Transform interpolationTarget;
    [SerializeField] private bool notNetworked;

    //---Private Variables
    private Vector3 origin;
    private float? xCurveLength, yCurveLength;

    public void Start() {
        if (notNetworked) {
            origin = transform.position;
        }
    }

    public override void Spawned() {
        Origin = transform.position;
    }

    public void BeforeTick() {
        if (!Enabled) {
            return;
        }

        SetPosition(transform, Origin, Runner.SimulationTime - Runner.DeltaTime - GameManager.Instance.GameStartTime);
    }

    public void Update() {
        if (notNetworked) {
            SetPosition(interpolationTarget, origin, Time.time);
        }
    }

    public override void Render() {
        if (!Enabled) {
            return;
        }

        SetPosition(interpolationTarget, Origin, Runner.LocalRenderTime - GameManager.Instance.GameStartTime);
    }

    public override void FixedUpdateNetwork() {
        if (!Enabled) {
            return;
        }

        SetPosition(transform, Origin, Runner.SimulationTime - GameManager.Instance.GameStartTime);
    }

    public void Execute() {
        Enabled = true;
    }

    private void SetPosition(Transform target, Vector3 origin, float secondsElapsed) {
        if (!target) {
            target = transform;
        }

        float xOffset = EvaluateCurve(x, ref xCurveLength, animationOffset, secondsElapsed);
        float yOffset = EvaluateCurve(y, ref yCurveLength, animationOffset, secondsElapsed);

        target.position = origin + new Vector3(xOffset, yOffset, 0);
    }

    private static float EvaluateCurve(AnimationCurve curve, ref float? length, double offset, double time) {
        if (curve.length <= 0) {
            return 0;
        }

        length ??= curve.keys[^1].time;

        return curve.Evaluate((float) ((time + (offset * length)) % length));
    }
}
