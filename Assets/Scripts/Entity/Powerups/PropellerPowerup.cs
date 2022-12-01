using UnityEngine;

using Fusion;
using NSMB.Utils;

public class PropellerPowerup : MovingPowerup {

    //---Default Variables
    private float defaultTimeFlyingStarted = -1;

    //---Networked Variables
    [Networked(Default = nameof(defaultTimeFlyingStarted))] private float TimeFlyingStarted { get; set; }
    [Networked] private Vector2 FlightOrigin { get; set; }

    //---Serialized Variables
    [SerializeField] private AnimationCurve flyingPathX, flyingPathY;

    public override void FixedUpdateNetwork() {
        if (GameManager.Instance && GameManager.Instance.gameover) {
            body.velocity = Vector2.zero;
            body.isKinematic = true;
            return;
        }

        if (FollowPlayer) {
            base.FixedUpdateNetwork();
            return;
        }

        if (TimeFlyingStarted < 0) {
            gameObject.layer = Layers.LayerEntity;
            base.FixedUpdateNetwork();

            if (physics.OnGround) {
                //start flying
                TimeFlyingStarted = Runner.SimulationTime;
                FlightOrigin = body.position;
            }
        } else {
            //we are flying. follow the curve.
            float elapsedTime = Runner.SimulationTime - TimeFlyingStarted;
            float x = flyingPathX.Evaluate(elapsedTime);
            float y = flyingPathY.Evaluate(elapsedTime);

            body.isKinematic = true;
            gameObject.layer = Layers.LayerHitsNothing;
            body.position = FlightOrigin + new Vector2(x, y);
        }
    }

    //---IBlockBumpable overrides
    public override void BlockBump(BasicEntity bumper, Vector3Int tile, InteractableTile.InteractionDirection direction) {
        //do nothing when bumped. We're flying, remember?
    }
}
