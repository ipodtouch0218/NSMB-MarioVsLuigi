using UnityEngine;

using Fusion;
using NSMB.Utils;

public class EntityMover : NetworkBehaviour, IBeforeTick, IAfterTick {

    //---Static Variables
    private static readonly RaycastHit2D[] RaycastBuffer = new RaycastHit2D[32];
    private static readonly float Skin = 0.005f, LerpInterpValue = 0.33f;

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
    private Vector2 previousRenderPosition;

    public override void Spawned() {
        position = transform.position;
        positionInterpolator = GetInterpolator(nameof(position));

        InterpolationDataSource = InterpolationDataSources.Predicted;
    }

    public void BeforeTick() {
        transform.position = position;
    }

    public void AfterTick() {
        transform.position = position;
    }

    public override void Render() {
        if (!interpolationTarget)
            interpolationTarget = transform;

        unsafe {
            Vector3 newPosition;

            if (freeze) {
                newPosition = position;
            } else if (IsProxy || !positionInterpolator.TryGetValues(out void* from, out void* to, out float alpha)) {

                // Proxy interpolation with some smoothing:

                if (interpolationTeleportDistance > 0 && Utils.WrappedDistance(previousRenderPosition, position) > interpolationTeleportDistance) {
                    // Teleport over large distances
                    newPosition = Utils.WrapWorldLocation(position);
                } else {
                    // Interpolate from where we are to the next point.
                    Utils.UnwrapLocations(previousRenderPosition, position, out Vector2 fromRelative, out Vector2 toRelative);
                    Vector2 difference = toRelative - fromRelative;
                    newPosition = Vector2.Lerp(previousRenderPosition, previousRenderPosition + difference, Mathf.Clamp01(LerpInterpValue - Time.deltaTime));
                    newPosition = Utils.WrapWorldLocation(newPosition);
                }
            } else {

                // Non-Proxy interpolation with no smoothing:

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
                    newPosition = Vector2.Lerp(fromFloat, fromFloat + difference, alpha);
                    newPosition = Utils.WrapWorldLocation(newPosition);
                }
            }

            newPosition.z = interpolationTarget.position.z;
            previousRenderPosition = interpolationTarget.position = newPosition;
        }
    }

    public override void FixedUpdateNetwork() {
        ResetPhysicsData();

        if (!freeze) {
            Vector2 movement;
            movement = CollideAndSlide(position + ColliderOffset, velocity * Runner.DeltaTime, false);
            movement += CollideAndSlide(position + ColliderOffset + movement, gravity * (Runner.DeltaTime * Runner.DeltaTime), true);
            movement *= Runner.Config.Simulation.TickRate;

            velocity = movement;
            position = Utils.WrapWorldLocation(position + (velocity * Runner.DeltaTime));
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

        float distance = velocity.magnitude + Skin;
        Vector2 direction = velocity.normalized;

        int hits = Runner.GetPhysicsScene2D().BoxCast(position, ColliderSize, 0, direction, distance, RaycastBuffer, Layers.GetCollisionMask(collider.gameObject.layer));
        for (int i = 0; i < hits; i++) {
            RaycastHit2D hit = RaycastBuffer[i];

            // Exception: dont hit our own hitboxes
            if (hit.collider.transform == transform || hit.collider.transform.IsChildOf(transform)) {
                continue;
            }

            // Exception: dont hit objects if we're moving away from them
            if (Vector2.Dot(direction, hit.normal) > 0.1f) {
                continue;
            }

            float angle = Vector2.SignedAngle(hit.normal, Vector2.up);

            // Semisolid check.
            if (hit.collider.gameObject.layer == Layers.LayerSemisolid) {
                if (
                    (direction.y > 0) || // We are moving upwards, impossible to collide.
                    (position.y - (ColliderSize.y * 0.5f) < hit.point.y - 0.03f) || // Lower bound of hitbox is BELOW the semisolid hit
                    (Mathf.Abs(angle) > maxFloorAngle) // Didn't hit upwards...
                   ) {
                    continue;
                }
            }

            bool isTilemap = hit.collider is CompositeCollider2D;
            if (Mathf.Abs(angle) < maxFloorAngle) {
                // Floor
                data.OnGround = true;
                data.CrushableGround |= !hit.collider.CompareTag("platform");
                data.FloorAngle = angle;

                if (isTilemap) {
                    float lowerBound = position.x - (ColliderSize.x * 0.5f) - 0.015f;
                    float upperBound = position.x + (ColliderSize.x * 0.5f) + 0.015f;
                    float y = hit.point.y + hit.normal.y * -0.2f;

                    for (float x = Mathf.FloorToInt(lowerBound * 2) * 0.5f; x <= Mathf.FloorToInt(upperBound * 2) * 0.5f; x += 0.5f) {
                        Vector2Int loc = Utils.WorldToTilemapPosition(new(x, y));
                        if (!data.TilesStandingOn.Contains(loc)) {
                            data.TilesStandingOn.Add(loc);
                        }
                    }
                }

            } else if (Mathf.Abs(angle) > 180 - maxFloorAngle) {
                // Roof
                data.HitRoof = true;

                if (isTilemap) {
                    float lowerBound = position.x - (ColliderSize.x * 0.5f) - 0.015f;
                    float upperBound = position.x + (ColliderSize.x * 0.5f) + 0.015f;
                    float y = hit.point.y + hit.normal.y * -0.2f;

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
                    float lowerBound = position.y - (ColliderSize.y * 0.5f) - 0.015f;
                    float upperBound = position.y + (ColliderSize.y * 0.5f) + 0.015f;
                    float x = hit.point.x + hit.normal.x * -0.2f;

                    for (float y = Mathf.FloorToInt(lowerBound * 2) * 0.5f; y <= Mathf.FloorToInt(upperBound * 2) * 0.5f; y += 0.5f) {
                        Vector2Int loc = Utils.WorldToTilemapPosition(new(x, y));
                        if (!data.TilesHitSide.Contains(loc)) {
                            data.TilesHitSide.Add(loc);
                        }
                    }
                }
            }

            Vector2 positionToSurfacePoint = (direction * hit.distance) + (hit.normal * Skin);

            if (Vector2.Dot(positionToSurfacePoint, hit.normal) > 0) {
                // Hit normal pushing us away from the wall
                this.position += (Vector2) Vector3.Project(positionToSurfacePoint, hit.normal);
                direction = ((Vector2) Vector3.ProjectOnPlane(direction, hit.normal)).normalized;
                positionToSurfacePoint = direction * hit.distance;
            }

            if (data.OnGround && gravityPass)
                return Vector2.zero;

            Vector2 leftover = velocity - positionToSurfacePoint;
            leftover = bounceOnImpacts ? Vector2.Reflect(leftover, hit.normal) : Vector3.ProjectOnPlane(leftover, hit.normal);

            return positionToSurfacePoint + CollideAndSlide(position + positionToSurfacePoint, leftover, gravityPass, depth + 1);
        }

        return velocity;
    }
}
