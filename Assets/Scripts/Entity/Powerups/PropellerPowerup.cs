using UnityEngine;

using Fusion;
using NSMB.Game;
using NSMB.Tiles;
using NSMB.Utils;

namespace NSMB.Entities.Collectable.Powerups {
    public class PropellerPowerup : MovingPowerup {

        //---Networked Variables
        [Networked] private float TimeFlyingStarted { get; set; }
        [Networked] private Vector2 FlightOrigin { get; set; }

        //---Serialized Variables
        [SerializeField] private AnimationCurve flyingPathX, flyingPathY;

        public override void FixedUpdateNetwork() {
            if (GameData.Instance && GameData.Instance.GameEnded) {
                body.velocity = Vector2.zero;
                body.isKinematic = true;
                return;
            }

            if (FollowPlayer) {
                base.FixedUpdateNetwork();
                return;
            }


            if (TimeFlyingStarted <= 0) {
                gameObject.layer = Layers.LayerEntity;
                base.FixedUpdateNetwork();

                if (!Object)
                    return;

                if (physics.Data.OnGround) {
                    // Start flying
                    TimeFlyingStarted = Runner.SimulationTime;
                    FlightOrigin = body.position;
                }
            } else {
                // We are flying. follow the curve.
                float elapsedTime = Runner.SimulationTime - TimeFlyingStarted;
                float x = flyingPathX.Evaluate(elapsedTime);
                float y = flyingPathY.Evaluate(elapsedTime);

                body.isKinematic = true;
                gameObject.layer = Layers.LayerHitsNothing;
                body.position = FlightOrigin + new Vector2(x, y);

            }

            if (DespawnTimer.Expired(Runner)) {
                Runner.Despawn(Object);
                return;
            }
        }

        //---IBlockBumpable overrides
        public override void BlockBump(BasicEntity bumper, Vector2Int tile, InteractableTile.InteractionDirection direction) {
            // Do nothing when bumped. We're flying, remember?
        }
    }
}
