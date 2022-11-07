using UnityEngine;

using Fusion;

public class GenericMover : NetworkBehaviour {

    //---Serialized Variables
    [SerializeField] private AnimationCurve x, y;
    [SerializeField] private float animationTimeSeconds = 1, animationMultiplier = 1, animationOffset = 0;

    //---Private Variables
    private Vector3? origin = null;

    public override void Spawned() {
        origin = transform.position;
    }

    public override void FixedUpdateNetwork() {
        int start = GameManager.Instance.GameStartTick;
        int ticksSinceStart = Runner.Simulation.Tick.Raw - start;
        double secondsSinceStart = (double) ticksSinceStart / Runner.Config.Simulation.TickRate;

        float percentage = (float) (secondsSinceStart / animationTimeSeconds) % animationTimeSeconds;
        percentage += animationOffset;
        percentage %= 1f;

        transform.position = (origin ?? default) + new Vector3(x.Evaluate(percentage) * animationMultiplier, y.Evaluate(percentage) * animationMultiplier, 0);
    }
}
