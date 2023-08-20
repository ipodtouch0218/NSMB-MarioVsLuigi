using UnityEngine;

using Fusion;
using NSMB.Utils;

public class EntityMover : NetworkBehaviour {

    //---Static Variables
    private static readonly RaycastHit2D[] RaycastBuffer = new RaycastHit2D[32];
    private static readonly float skin = 0.03f;

    //---Networked Variables
    [Networked] public Vector2 position { get; set; }
    [Networked] public Vector2 velocity { get; set; }
    [Networked] public NetworkBool freeze { get; set; }
    [Networked] public Vector2 gravity { get; set; }

    [Networked] public ref PhysicsDataStruct data => ref MakeRef<PhysicsDataStruct>();

    //---Properties
    private Vector2 ColliderOffset => transform.lossyScale * collider.offset;
    private Vector2 ColliderSize => transform.lossyScale * collider.size;

    //---Serialized Variables
    [Header("Serialized")]
    [SerializeField] public Transform interpolationTarget;
    [SerializeField] private BoxCollider2D collider;
    [SerializeField] private int maxIterations = 3;
    [SerializeField] private float maxFloorAngle = 70, interpolationTeleportDistance = 1f;
    [SerializeField] private bool bounceOnImpacts;

    //---Private Variables
    private RawInterpolator positionInterpolator;

    public override void Spawned() {
        position = transform.position;
        positionInterpolator = GetInterpolator(nameof(position));
    }

    public override void Render() {
        if (!interpolationTarget)
            interpolationTarget = transform;

        unsafe {
            Vector3 newPosition;
            if (freeze || !positionInterpolator.TryGetValues(out void* from, out void* to, out float alpha)) {
                newPosition = position;

            } else {
                Vector2Int fromInt = *(Vector2Int*) from;
                Vector2 fromFloat = (Vector2) fromInt * 0.001f;

                Vector2Int toInt = *(Vector2Int*) to;
                Vector2 toFloat = (Vector2) toInt * 0.001f;

                if (interpolationTeleportDistance > 0 && Utils.WrappedDistance(fromFloat, toFloat) > interpolationTeleportDistance) {
                    // Teleport over large distances
                    newPosition = Utils.WrapWorldLocation(toFloat);
                } else {
                    // Normal interpolation (over level seams, too)...
                    Utils.UnwrapLocations(fromFloat, toFloat, out Vector2 fromFloatRelative, out Vector2 toFloatRelative);
                    Vector2 difference = toFloatRelative - fromFloatRelative;
                    newPosition = Utils.WrapWorldLocation(Vector2.Lerp(fromFloat, fromFloat + difference, alpha));
                }
            }

            newPosition.z = interpolationTarget.position.z;
            interpolationTarget.position = newPosition;
        }
    }

    public override void FixedUpdateNetwork() {
        ResetPhysicsData();

        if (!freeze) {
            bool original = Physics2D.queriesStartInColliders;
            Physics2D.queriesStartInColliders = false;

            Vector2 movement;
            movement = CollideAndSlide(position + ColliderOffset, velocity * Runner.DeltaTime, false);
            movement += CollideAndSlide(position + ColliderOffset + movement, gravity * (Runner.DeltaTime * Runner.DeltaTime), true);
            movement *= Runner.Config.Simulation.TickRate;

            velocity = movement;
            position = Utils.WrapWorldLocation(position + (velocity * Runner.DeltaTime));

            Physics2D.queriesStartInColliders = original;
        }

        transform.position = position;
    }

    private void ResetPhysicsData() {
        data.FloorAngle = 0;
        data.OnGround = false;
        data.CrushableGround = false;
        data.HitRoof = false;
        data.HitRight = false;
        data.HitLeft = false;
    }

    private Vector2 CollideAndSlide(Vector2 position, Vector2 velocity, bool gravityPass, int depth = 0) {
        if (depth >= maxIterations)
            return Vector2.zero;

        float distance = velocity.magnitude + skin;
        Vector2 direction = velocity.normalized;

        int hits = Physics2D.BoxCastNonAlloc(position, ColliderSize, 0, direction, RaycastBuffer, distance, Layers.GetCollisionMask(collider.gameObject.layer));
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

            if (data.OnGround && gravityPass)
                return surfaceDistance;

            float leftoverDistance = leftover.magnitude;
            leftover = bounceOnImpacts ? Vector2.Reflect(leftover, hit.normal) : Vector3.ProjectOnPlane(leftover, hit.normal);
            //leftover = leftover.normalized * leftoverDistance;

            return surfaceDistance + CollideAndSlide(position + surfaceDistance, leftover, gravityPass, depth + 1);
        }

        return velocity;
    }
}
