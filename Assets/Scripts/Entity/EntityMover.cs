using UnityEngine;

using Fusion;
using NSMB.Utils;

public class EntityMover : NetworkBehaviour {

    //---Static Variables
    private static readonly RaycastHit2D[] RaycastBuffer = new RaycastHit2D[32];
    private static readonly float skin = 0.03f;

    //---Networked Variables
    [Networked] public Vector2 velocity { get; set; }
    [Networked] public Vector2 positionnn { get; set; }
    [Networked] public NetworkBool freeze { get; set; }
    [Networked] public Vector2 gravity { get; set; }

    [Networked] public ref PhysicsDataStruct data => ref MakeRef<PhysicsDataStruct>();

    //---Properties
    private Vector2 ColliderOffset => transform.lossyScale * collider.offset;
    private Vector2 ColliderSize => transform.lossyScale * collider.size;

    //---Serialized Variables
    [SerializeField] private Transform interpolationTarget;
    [SerializeField] private BoxCollider2D collider;
    [SerializeField] private LayerMask layerMask;
    [SerializeField] private int maxIterations = 3;
    [SerializeField] private float maxFloorAngle = 70, interpolationTeleportDistance = 1f;
    [SerializeField] private bool bounceOnImpacts;

    //---Private Variables
    private RawInterpolator positionInterpolator;

    public override void Spawned() {
        positionnn = transform.position;
        positionInterpolator = GetInterpolator(nameof(positionnn));
    }

    public void LateUpdate() {
        if (!interpolationTarget)
            interpolationTarget = transform;

        unsafe {
            if (freeze || !positionInterpolator.TryGetValues(out void* from, out void* to, out float alpha)) {
                interpolationTarget.position = positionnn;

            } else {
                Vector2 fromValue = *(Vector2*) from;
                Vector2 toValue = *(Vector2*) to;

                if (interpolationTeleportDistance > 0 && Utils.WrappedDistance(fromValue, toValue) > interpolationTeleportDistance) {
                    // Teleport over large distances
                    interpolationTarget.position = Utils.WrapWorldLocation(toValue);
                } else {
                    // Normal interpolation (over level seams, too)...
                    interpolationTarget.position = Utils.WrapWorldLocation(Vector2.Lerp(fromValue, toValue, alpha));
                }
            }
        }

        //interpolationTarget.position = position;
    }

    public override void FixedUpdateNetwork() {
        ResetPhysicsData();

        if (freeze)
            return;

        Vector2 movement = CollideAndSlide(positionnn + ColliderOffset, ((gravity * Runner.DeltaTime) + velocity) * Runner.DeltaTime) * Runner.Config.Simulation.TickRate;
        velocity = movement;
        positionnn = Utils.WrapWorldLocation(positionnn + (velocity * Runner.DeltaTime));

        transform.position = positionnn;
    }

    private void ResetPhysicsData() {
        data.FloorAngle = 0;
        data.OnGround = false;
        data.CrushableGround = false;
        data.HitRoof = false;
        data.HitRight = false;
        data.HitLeft = false;
    }

    private Vector2 CollideAndSlide(Vector2 position, Vector2 velocity, int depth = 0) {
        if (depth >= maxIterations)
            return Vector2.zero;

        float distance = velocity.magnitude + skin;
        Vector2 direction = velocity.normalized;

        int hits = Physics2D.BoxCastNonAlloc(position, ColliderSize, 0, direction, RaycastBuffer, distance, layerMask);
        for (int i = 0; i < hits; i++) {
            RaycastHit2D hit = RaycastBuffer[i];

            // Semisolid check.
            if (hit.collider.gameObject.layer == Layers.LayerSemisolid) {
                if (
                    (direction.y > 0) || // We are moving upwards, impossible to collide.
                    (position.y - (ColliderSize.y * 0.5f) < hit.point.y) // Lower bound of hitbox is BELOW the semisolid hit
                   ) {
                    continue;
                }
            }

            float angle = Vector2.SignedAngle(hit.normal, Vector2.up);
            if (Mathf.Abs(angle) < maxFloorAngle) {
                // Up
                data.OnGround = true;
                data.CrushableGround |= !hit.collider.CompareTag("platform");
                data.FloorAngle = angle;

            } else if (Mathf.Abs(angle) > (90 - maxFloorAngle) + maxFloorAngle) {
                // Down
                data.HitRoof = true;
            } else if (angle > 0) {
                // Left
                data.HitLeft = true;
            } else {
                // Right
                data.HitRight = true;
            }

            Vector2 surfaceDistance = direction * (hit.distance - skin);
            Vector2 leftover = velocity - surfaceDistance;

            if (surfaceDistance.sqrMagnitude <= skin * skin)
                surfaceDistance = Vector2.zero;

            float leftoverDistance = leftover.magnitude;
            leftover = bounceOnImpacts ? Vector2.Reflect(leftover, hit.normal).normalized : Vector3.ProjectOnPlane(leftover, hit.normal).normalized;
            leftover *= leftoverDistance;

            return surfaceDistance + CollideAndSlide(position + surfaceDistance, leftover, depth + 1);
        }

        return velocity;
    }
}
