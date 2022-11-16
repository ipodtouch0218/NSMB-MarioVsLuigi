using UnityEngine;

using Fusion;

public class GenericMover : NetworkBehaviour {

    //---Serialized Variables
    [SerializeField] private AnimationCurve x, y;
    [SerializeField] private float animationOffset = 0;

    //---Private Variables
    private Vector3? origin = null;

    public override void Spawned() {
        origin = transform.position;
    }

    public override void FixedUpdateNetwork() {
        int start = GameManager.Instance.GameStartTick;
        int ticksSinceStart = Runner.Simulation.Tick.Raw - start;
        double secondsSinceStart = (double) ticksSinceStart * Runner.DeltaTime;

        float xValue = 0, yValue = 0;

        if (x.length > 0) {
            float xEnd = x.keys[^1].time;
            xValue = x.Evaluate((float) ((secondsSinceStart + (animationOffset * xEnd)) % xEnd));
        }
        if (y.length > 0) {
            float yEnd = y.keys[^1].time;
            yValue = y.Evaluate((float) ((secondsSinceStart + (animationOffset * yEnd)) % yEnd));
        }

        transform.position = (origin ?? default) + new Vector3(xValue, yValue, 0);
    }
}
