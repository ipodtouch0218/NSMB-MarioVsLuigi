using UnityEngine;

using Fusion;

public class GenericMover : NetworkBehaviour {

    //---Serialized Variables
    [SerializeField] private AnimationCurve x, y;
    [SerializeField] private float animationTimeSeconds = 1;

    //---Private Variables
    private Vector3? origin = null;

    public override void Spawned() {
        origin = transform.position;
    }

    public override void FixedUpdateNetwork() {
        int start = GameManager.Instance.GameStartTick;
        int ticksSinceStart = start - Runner.Simulation.Tick.Raw;
        double secondsSinceStart = (double) ticksSinceStart / Runner.Config.Simulation.TickRate;

        float percentage = (float) (secondsSinceStart / animationTimeSeconds) % animationTimeSeconds;

        transform.position = (origin ?? default) + new Vector3(x.Evaluate(percentage), y.Evaluate(percentage), 0);
    }
}