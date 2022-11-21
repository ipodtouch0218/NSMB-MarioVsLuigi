using UnityEngine;

using Fusion;

public class GenericMover : NetworkBehaviour {

    //---Networked Variables
    [Networked] private Vector3 Origin { get; set; }

    //---Serialized Variables
    [SerializeField] private AnimationCurve x, y;
    [SerializeField] private float animationOffset = 0;

    public override void Spawned() {
        Origin = transform.position;
    }

    public override void FixedUpdateNetwork() {
        float secondsElapsed = GameManager.Instance.GameStartTime - Runner.SimulationTime;
        float xOffset = EvaluateCurve(x, animationOffset, secondsElapsed);
        float yOffset = EvaluateCurve(y, animationOffset, secondsElapsed);

        transform.position = Origin + new Vector3(xOffset, yOffset,0);
    }

    private static float EvaluateCurve(AnimationCurve curve, double offset, double time) {
        if (curve.length <= 0)
            return 0;

        float end = curve.keys[^1].time;
        return curve.Evaluate((float) ((time + (offset * end)) % end));
    }
}
