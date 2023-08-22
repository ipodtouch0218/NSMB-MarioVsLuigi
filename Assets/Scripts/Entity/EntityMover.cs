using UnityEngine;

using Fusion;
using NSMB.Utils;

public class EntityMover : NetworkBehaviour {

    //---Static Variables
    private static readonly RaycastHit2D[] RaycastBuffer = new RaycastHit2D[32];
    private static readonly float skin = 0.005f;

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
        //if (Runner.IsForward)
        //    position = transform.position;

        if (!freeze) {
            //bool original = Physics2D.queriesStartInColliders;
            //Physics2D.queriesStartInColliders = false;

            Vector2 movement;
            movement = CollideAndSlide(position + ColliderOffset, velocity * Runner.DeltaTime, false);
            movement += CollideAndSlide(position + ColliderOffset + movement, gravity * (Runner.DeltaTime * Runner.DeltaTime), true);
            movement *= Runner.Config.Simulation.TickRate;

            velocity = movement;
            position = Utils.WrapWorldLocation(position + (velocity * Runner.DeltaTime));

            //Physics2D.queriesStartInColliders = original;
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

        data.TilesStandingOn.Clear();
        data.TilesHitSide.Clear();
        data.TilesHitRoof.Clear();
    }

    private Vector2 CollideAndSlide(Vector2 position, Vector2 velocity, bool gravityPass, int depth = 0) {
        if (depth >= maxIterations)
            return Vector2.zero;

        float distance = velocity.magnitude + skin;
        Vector2 direction = velocity.normalized;

        int hits = Physics2D.BoxCastNonAlloc(position, ColliderSize, 0, direction, RaycastBuffer, distance, Layers.GetCollisionMask(collider.gameObject.layer));
        for (int i = 0; i < hits; i++) {
            RaycastHit2D hit = RaycastBuffer[i];

            // Exception
            if (Vector2.Dot(direction, hit.normal) > 0.1f)
                continue;

            // Semisolid check.
            if (hit.collider.gameObject.layer == Layers.LayerSemisolid) {
                if (
                    (direction.y > 0) || // We are moving upwards, impossible to collide.
                    (position.y - (ColliderSize.y * 0.5f) < hit.point.y) // Lower bound of hitbox is BELOW the semisolid hit
                   ) {
                    continue;
                }
            }

            bool isTilemap = hit.collider is CompositeCollider2D;
            float angle = Vector2.SignedAngle(hit.normal, Vector2.up);
            if (Mathf.Abs(angle) < maxFloorAngle) {
                // Up
                data.OnGround = true;
                data.CrushableGround |= !hit.collider.CompareTag("platform");
                data.FloorAngle = angle;

                if (isTilemap) {
                    float lowerBound = position.x - (ColliderSize.x * 0.5f);
                    float upperBound = position.x + (ColliderSize.x * 0.5f);
                    float y = hit.point.y + hit.normal.y * -0.1f;

                    for (float x = Mathf.FloorToInt(lowerBound * 2) * 0.5f; x <= Mathf.FloorToInt(upperBound * 2) * 0.5f; x += 0.5f) {
                        Vector2Int loc = Utils.WorldToTilemapPosition(new(x, y));
                        if (!data.TilesStandingOn.Contains(loc)) {
                            data.TilesStandingOn.Add(loc);
                        }
                    }
                }

            } else if (Mathf.Abs(angle) > (90 - maxFloorAngle) + maxFloorAngle) {
                // Down
                data.HitRoof = true;

                if (isTilemap) {
                    float lowerBound = position.x - ColliderSize.x * 0.5f - 0.005f;
                    float upperBound = position.x + ColliderSize.x * 0.5f + 0.005f;
                    float y = hit.point.y + hit.normal.y * -0.1f;

                    for (float x = Mathf.FloorToInt(lowerBound * 2) * 0.5f; x <= Mathf.FloorToInt(upperBound * 2) * 0.5f; x += 0.5f) {
                        Vector2Int loc = Utils.WorldToTilemapPosition(new(x, y));
                        if (!data.TilesHitRoof.Contains(loc)) {
                            data.TilesHitRoof.Add(loc);
                        }
                    }
                }

            } else {
                if (angle > 0) {
                    // Left
                    data.HitLeft = true;
                } else {
                    // Right
                    data.HitRight = true;
                }

                if (isTilemap) {
                    float lowerBound = position.y - ColliderSize.y * 0.5f - 0.005f;
                    float upperBound = position.y + ColliderSize.y * 0.5f + 0.005f;
                    float x = hit.point.x + hit.normal.x * -0.1f;

                    for (float y = Mathf.FloorToInt(lowerBound * 2) * 0.5f; y <= Mathf.FloorToInt(upperBound * 2) * 0.5f; y += 0.5f) {
                        Vector2Int loc = Utils.WorldToTilemapPosition(new(x, y));
                        if (!data.TilesHitSide.Contains(loc)) {
                            data.TilesHitSide.Add(loc);
                        }
                    }
                }
            }

            Vector2 positionToSurfacePoint = (direction * hit.distance) + (hit.normal * skin);

            if (Vector2.Dot(positionToSurfacePoint, hit.normal) > 0) {
                // Hit normal pushing us away from the wall
                this.position += (Vector2) Vector3.Project(positionToSurfacePoint, hit.normal);
                direction = ((Vector2) Vector3.ProjectOnPlane(direction, hit.normal)).normalized;
                positionToSurfacePoint = direction * hit.distance;
            }

            if (data.OnGround && gravityPass)
                return positionToSurfacePoint;

            Vector2 leftover = velocity - positionToSurfacePoint;
            leftover = bounceOnImpacts ? Vector2.Reflect(leftover, hit.normal) : Vector3.ProjectOnPlane(leftover, hit.normal);

            return positionToSurfacePoint + CollideAndSlide(position + positionToSurfacePoint, leftover, gravityPass, depth + 1);
        }

        return velocity;
    }
}
