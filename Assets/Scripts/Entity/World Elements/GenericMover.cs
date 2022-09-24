using UnityEngine;

using Fusion;

public class GenericMover : NetworkBehaviour {

    //---Serialized Variables
    [SerializeField] private AnimationCurve x, y;
    [SerializeField] private float animationTimeSeconds = 1;

    //---Private Variables
    private Vector3? origin = null;

    public void Awake() {
        if (origin == null)
            origin = transform.position;
    }

    public void FixedNetworkUpdate() {
        int start = GameManager.Instance.GameStartTick;
        int ticksSinceStart = start - Runner.Simulation.Tick.Raw;
        float secondsSinceStart = ticksSinceStart / Runner.Config.Simulation.TickRate;

        float percentage = secondsSinceStart / animationTimeSeconds % animationTimeSeconds;

        transform.position = (origin ?? default) + new Vector3(x.Evaluate(percentage), y.Evaluate(percentage), 0);
    }
}