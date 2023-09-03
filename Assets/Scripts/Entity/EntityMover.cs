using UnityEngine;
using UnityEngine.Serialization;

using Fusion;
using NSMB.Tiles;
using NSMB.Utils;

public class EntityMover : NetworkBehaviour, IBeforeTick, IAfterTick {

    //---Static Variables
    private static readonly RaycastHit2D[] RaycastBuffer = new RaycastHit2D[32];
    private static readonly float Skin = 0.01f, LerpInterpValue = 0.33f;
    private static readonly int MaxIterations = 5;

    //---Networked Variables
    [Networked] public Vector2 Position { get; set; }
    [Networked] public Vector2 Velocity { get; set; }
    [Networked] public NetworkBool Freeze { get; set; }
    [Networked] public Vector2 Gravity { get; set; }
    [Networked] public ref PhysicsDataStruct Data => ref MakeRef<PhysicsDataStruct>();

    //---Properties
    private Vector2 ColliderOffset => transform.lossyScale * activeCollider.offset;
    private Vector2 ColliderSize => transform.lossyScale * activeCollider.size;

    //---Serialized Variables
    [Header("Serialized")]
    [SerializeField] public Transform interpolationTarget;
    [SerializeField, FormerlySerializedAs("collider")] private BoxCollider2D activeCollider;
    [SerializeField] private float maxFloorAngle = 70, interpolationTeleportDistance = 1f;
    [SerializeField] private bool bounceOnImpacts;

    //---Private Variables
    private RawInterpolator positionInterpolator;
    private Vector2 previousRenderPosition;

    public override void Spawned() {
        Position = transform.position;
        positionInterpolator = GetInterpolator(nameof(Position));

        InterpolationDataSource = InterpolationDataSources.Predicted;
    }

    public void BeforeTick() {
        transform.position = Position;
    }

    public void AfterTick() {
        transform.position = Position;
    }

    public unsafe override void Render() {
        if (!interpolationTarget)
            interpolationTarget = transform;

        Vector3 newPosition;

        if (Freeze) {
            newPosition = Position;
        } else if (IsProxy || !positionInterpolator.TryGetValues(out void* from, out void* to, out float alpha)) {

            // Proxy interpolation with some smoothing:

            if (interpolationTeleportDistance > 0 && Utils.WrappedDistance(previousRenderPosition, Position) > interpolationTeleportDistance) {
                // Teleport over large distances
                newPosition = Utils.WrapWorldLocation(Position);
            } else {
                // Interpolate from where we are to the next point.
                Utils.UnwrapLocations(previousRenderPosition, Position, out Vector2 fromRelative, out Vector2 toRelative);
                Vector2 difference = toRelative - fromRelative;
                newPosition = Vector2.Lerp(previousRenderPosition, previousRenderPosition + difference, Mathf.Clamp01(LerpInterpValue - Time.deltaTime));
                newPosition = Utils.WrapWorldLocation(newPosition);
            }
        } else {

            // State/Input Authority interpolation with no smoothing:

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

    public override void FixedUpdateNetwork() {
        ResetPhysicsData();

        if (!Freeze) {
            Vector2 movement;
            movement = CollideAndSlide(Position + ColliderOffset, Velocity * Runner.DeltaTime, false);
            movement += CollideAndSlide(Position + ColliderOffset + movement, Gravity * (Runner.DeltaTime * Runner.DeltaTime), true);
            movement *= Runner.Config.Simulation.TickRate;

            Velocity = movement;
            Position = Utils.WrapWorldLocation(Position + (Velocity * Runner.DeltaTime));
        }

        transform.position = Position;
    }

    private void ResetPhysicsData() {
        Data.FloorAngle = 0;
        Data.OnGround = false;
        Data.CrushableGround = false;
        Data.HitRoof = false;
        Data.HitRight = false;
        Data.HitLeft = false;

        Data.TileContacts.Clear();
        Data.ObjectContacts.Clear();
    }

    private Vector2 CollideAndSlide(Vector2 raycastPos, Vector2 raycastVel, bool gravityPass, int depth = 0) {
        if (depth >= MaxIterations)
            return Vector2.zero;

        float distance = raycastVel.magnitude + Skin;
        Vector2 direction = raycastVel.normalized;
        Vector2 size = ColliderSize;
        int filter = Layers.GetCollisionMask(activeCollider.gameObject.layer);

        int hits = Runner.GetPhysicsScene2D().BoxCast(raycastPos, size, 0, direction, distance, RaycastBuffer, filter);
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
                    (direction.y > 0 || Velocity.y > 0) || // We are moving upwards, impossible to collide.
                    (raycastPos.y - (ColliderSize.y * 0.5f) < hit.point.y - 0.03f) || // Lower bound of hitbox is BELOW the semisolid hit
                    (Mathf.Abs(angle) > maxFloorAngle) // Didn't hit upwards...
                   ) {
                    continue;
                }
            }

            bool isTilemap = hit.collider is CompositeCollider2D;
            if (Mathf.Abs(angle) < maxFloorAngle) {
                // Floor
                Data.OnGround = true;
                Data.CrushableGround |= !hit.collider.CompareTag("platform");
                Data.FloorAngle = angle;

                if (isTilemap) {
                    float lowerBound = raycastPos.x - (ColliderSize.x * 0.5f) - 0.01f;
                    float upperBound = raycastPos.x + (ColliderSize.x * 0.5f) + 0.01f;
                    float y = hit.point.y + hit.normal.y * -0.2f;

                    for (float x = Mathf.FloorToInt(lowerBound * 2) * 0.5f; x <= Mathf.FloorToInt(upperBound * 2) * 0.5f; x += 0.5f) {
                        Vector2Int loc = Utils.WorldToTilemapPosition(new(x, y));
                        PhysicsDataStruct.TileContact tile = new() {
                            location = loc,
                            direction = TileInteractionDirection.Down,
                        };
                        if (!Data.TileContacts.Contains(tile)) {
                            Data.TileContacts.Add(tile);
                        }
                    }
                } else if (hit.collider.GetComponentInParent<NetworkObject>() is NetworkObject no) {
                    PhysicsDataStruct.ObjectContact obj = new() {
                        networkObjectId = no.Id,
                        direction = TileInteractionDirection.Down,
                    };
                    if (!Data.ObjectContacts.Contains(obj)) {
                        Data.ObjectContacts.Add(obj);
                    }
                }

            } else if (Mathf.Abs(angle) > 180 - maxFloorAngle) {
                // Roof
                Data.HitRoof = true;

                if (isTilemap) {
                    float lowerBound = raycastPos.x - (ColliderSize.x * 0.5f) - 0.01f;
                    float upperBound = raycastPos.x + (ColliderSize.x * 0.5f) + 0.01f;
                    float y = hit.point.y + hit.normal.y * -0.2f;

                    for (float x = Mathf.FloorToInt(lowerBound * 2) * 0.5f; x <= Mathf.FloorToInt(upperBound * 2) * 0.5f; x += 0.5f) {
                        Vector2Int loc = Utils.WorldToTilemapPosition(new(x, y));
                        PhysicsDataStruct.TileContact tile = new() {
                            location = loc,
                            direction = TileInteractionDirection.Up,
                        };
                        if (!Data.TileContacts.Contains(tile)) {
                            Data.TileContacts.Add(tile);
                        }
                    }
                } else if (hit.collider.GetComponentInParent<NetworkObject>() is NetworkObject no) {
                    PhysicsDataStruct.ObjectContact obj = new() {
                        networkObjectId = no.Id,
                        direction = TileInteractionDirection.Up,
                    };
                    if (!Data.ObjectContacts.Contains(obj)) {
                        Data.ObjectContacts.Add(obj);
                    }
                }

            } else {
                TileInteractionDirection interactionDirection;
                if (angle > 0) {
                    // Left
                    Data.HitLeft = true;
                    interactionDirection = TileInteractionDirection.Left;
                } else {
                    // Right
                    Data.HitRight = true;
                    interactionDirection = TileInteractionDirection.Right;
                }

                if (isTilemap) {
                    float lowerBound = raycastPos.y - (ColliderSize.y * 0.5f) - 0.01f;
                    float upperBound = raycastPos.y + (ColliderSize.y * 0.5f) + 0.01f;
                    float x = hit.point.x + hit.normal.x * -0.2f;

                    for (float y = Mathf.FloorToInt(lowerBound * 2) * 0.5f; y <= Mathf.FloorToInt(upperBound * 2) * 0.5f; y += 0.5f) {
                        Vector2Int loc = Utils.WorldToTilemapPosition(new(x, y));
                        PhysicsDataStruct.TileContact tile = new() {
                            location = loc,
                            direction = interactionDirection,
                        };
                        if (!Data.TileContacts.Contains(tile)) {
                            Data.TileContacts.Add(tile);
                        }
                    }
                } else if (hit.collider.GetComponentInParent<NetworkObject>() is NetworkObject no) {
                    PhysicsDataStruct.ObjectContact obj = new() {
                        networkObjectId = no.Id,
                        direction = interactionDirection,
                    };
                    if (!Data.ObjectContacts.Contains(obj)) {
                        Data.ObjectContacts.Add(obj);
                    }
                }
            }

            Vector2 positionToSurfacePoint = (direction * hit.distance) + (hit.normal * Skin);

            // Started inside an object
            if (hit.distance <= 0) {
                RaycastHit2D stuckHit = Runner.GetPhysicsScene2D().Raycast(raycastPos, (hit.point - raycastPos), Mathf.Max(size.x, size.y), filter);
                if (stuckHit) {

                    Vector2 offset = stuckHit.normal * (Vector2.Distance(hit.point, stuckHit.point) + Skin + Skin + Skin);
                    Position += offset;
                    raycastPos += offset;

                    return CollideAndSlide(raycastPos, raycastVel, gravityPass, depth + 1);
                }
            }

            // Eject from walls
            if (Vector2.Dot(positionToSurfacePoint, hit.normal) > 0) {
                // Hit normal pushing us away from the wall
                Vector2 offset = (Vector2) Vector3.Project(positionToSurfacePoint, hit.normal);
                Position += offset;
                raycastPos += offset;
                direction = ((Vector2) Vector3.ProjectOnPlane(direction, hit.normal)).normalized;
                positionToSurfacePoint = direction * hit.distance;
            }

            if (Data.OnGround && gravityPass)
                return Vector2.zero;

            Vector2 leftover = raycastVel - positionToSurfacePoint;
            leftover = bounceOnImpacts ? Vector2.Reflect(leftover, hit.normal) : Vector3.ProjectOnPlane(leftover, hit.normal);

            return positionToSurfacePoint + CollideAndSlide(raycastPos + positionToSurfacePoint, leftover, gravityPass, depth + 1);
        }

        return raycastVel;
    }
}
