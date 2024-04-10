using UnityEngine;
using UnityEngine.Serialization;

using Fusion;
using NSMB.Tiles;
using NSMB.Utils;

[SimulationBehaviour]
public class EntityMover : NetworkBehaviour, IBeforeTick, IAfterTick, IAfterAllTicks, IRemotePrefabCreated {

    //---Static Variables
    private static readonly RaycastHit2D[] RaycastBuffer = new RaycastHit2D[32];
    private const float Skin = 0.015f, LerpInterpValue = 0.33f;
    private const int MaxIterations = 5;

    //---Networked Variables
    [Networked] private Vector2 InternalPosition { get; set; }
    [Networked] public Vector2 Velocity { get; set; }
    [Networked] public NetworkBool Freeze { get; set; }
    [Networked] public NetworkBool IsKinematic { get; set; }
    [Networked] public NetworkBool LockX { get; set; }
    [Networked] public NetworkBool LockY { get; set; }
    [Networked] public Vector2 Gravity { get; set; }
    [Networked, HideInInspector] public ref PhysicsDataStruct Data => ref MakeRef<PhysicsDataStruct>();
    [Networked] public Vector2 PreviousTickPosition { get; private set; }
    [Networked] public Vector2 PreviousTickVelocity { get; private set; }

    //---Properties
    public Vector2 Position {
        get => InternalPosition;
        set {
            InternalPosition = value;
            transform.position = value;
        }
    }
    public bool ForceSnapshotInterpolation { get; set; } = false;
    private Vector2 ColliderOffset => transform.lossyScale * activeCollider.offset;
    private Vector2 ColliderSize => transform.lossyScale * activeCollider.size;

    //---Serialized Variables
    [Header("Serialized")]
    [SerializeField, FormerlySerializedAs("collider")] private BoxCollider2D activeCollider;
    [SerializeField] private float maxFloorAngle = 70, interpolationTeleportDistance = 1f;
    [SerializeField] private bool bounceOnImpacts;

    //---Private Variables
    private Vector2 previousInternalPosition;
    private Vector2 previousRenderPosition;
    private PropertyReader<Vector2> internalPositionPropertyReader;

    public override void Spawned() {
        if (HasStateAuthority) {
            Position = transform.position;
        }

        internalPositionPropertyReader = GetPropertyReader<Vector2>(nameof(InternalPosition));
    }

    public void BeforeTick() {
        transform.position = Position;
        Physics2D.SyncTransforms();
    }

    public void AfterTick() {
        transform.position = Position;
    }

    public void AfterAllTicks(bool resimulation, int ticks) {
        PreviousTickPosition = Position;
        PreviousTickVelocity = Velocity;
    }

    public void RemotePrefabCreated() {
        transform.position = Position;
    }

    public override void Render() {
        Vector3 newPosition;

        if (Freeze) {
            newPosition = Position;
        } else if (TryGetSnapshotsBuffers(out var from, out var to, out float alpha)) {

            // Snapshot interpolation with no smoothing:
            Vector2 fromVector;
            Vector2 toVector;

            if (ForceSnapshotInterpolation) {
                fromVector = previousInternalPosition;
                toVector = Position;
                previousInternalPosition = Position;
            } else {
                (fromVector, toVector) = internalPositionPropertyReader.Read(from, to);
            }

            if (interpolationTeleportDistance > 0 && Utils.WrappedDistance(fromVector, toVector) > interpolationTeleportDistance) {
                // Teleport over large distances
                newPosition = Utils.WrapWorldLocation(toVector);
            } else {
                // Normal interpolation (over level seams, too)...
                Utils.UnwrapLocations(fromVector, toVector, out Vector2 fromVectorRelative, out Vector2 toVectorRelative);
                newPosition = Vector2.Lerp(fromVectorRelative, toVectorRelative, alpha);
                newPosition = Utils.WrapWorldLocation(newPosition);
            }
        } else {
            // Fallback interpolation with some smoothing:
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
        }

        newPosition.z = transform.position.z;
        previousRenderPosition = transform.position = newPosition;
    }

    public override void FixedUpdateNetwork() {
        bool local = !IsProxy || !ForceSnapshotInterpolation || Data.OnMovingPlatform;
        if (!local) {
            return;
        }

        bool noVelocity = IsProxy && ForceSnapshotInterpolation;

        Data.Reset();

        if (!Freeze) {
            Vector2 movement = Vector2.zero;
            if (!noVelocity) {
                movement = CollideAndSlide(Position + ColliderOffset, Velocity * Runner.DeltaTime, false);
            }
            if (!IsKinematic) {
                movement += CollideAndSlide(Position + ColliderOffset + movement, Gravity * (Runner.DeltaTime * Runner.DeltaTime), true);
            }

            movement *= Runner.TickRate;

            Velocity = movement;
            Position = Utils.WrapWorldLocation(Position + (Velocity * Runner.DeltaTime));
        }

        transform.position = Position;
    }

    private Vector2 CollideAndSlide(Vector2 raycastPos, Vector2 raycastVel, bool gravityPass, int depth = 0) {
        if (depth >= MaxIterations) {
            return Vector2.zero;
        }

        if (IsKinematic) {
            return raycastVel;
        }

        if (LockX) {
            raycastVel.x = 0;
        }

        if (LockY) {
            raycastVel.y = 0;
        }

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
            if (Vector2.Dot(direction, hit.normal) > 0) {
                continue;
            }

            if (gravityPass) {
                Vector2 combinedDirection = raycastVel + (Velocity * Runner.DeltaTime);

                if (Vector2.Dot(combinedDirection, hit.normal) > 0) {
                    continue;
                }
            }
            float angle = Vector2.SignedAngle(hit.normal, Vector2.up);

            // Semisolid check(s)
            if (hit.collider.gameObject.layer == Layers.LayerSemisolid) {

                if (angle > maxFloorAngle) {
                    continue;
                }

                // Semisolid stairs check
                //if (TryGetBehaviour(out PlayerController _)) {
                //    Debug.Log($"{angle} {previousFloorAngle}");
                //}

                //if (Mathf.Abs(angle - previousFloorAngle) > 30) {
                //    continue;
                //}

                if (
                    (raycastPos.y - (ColliderSize.y * 0.5f) < hit.point.y - 0.03f) || // Lower bound of hitbox is BELOW the semisolid hit
                    (Mathf.Abs(angle) > maxFloorAngle) // Didn't hit upwards...
                   ) {

                    continue;
                }
            }

            Vector2 alignedDirection;

            bool isTilemap = hit.collider is CompositeCollider2D;
            if (Mathf.Abs(angle) < maxFloorAngle) {
                // Floor
                Data.OnGround = true;
                Data.CrushableGround |= !hit.collider.CompareTag("platform");
                Data.OnMovingPlatform |= hit.collider.GetComponentInParent<GenericMover>();
                Data.FloorAngle = angle;

                if (hit.collider.GetComponentInParent<NetworkObject>() is NetworkObject no) {
                    AddObjectContacts(no, InteractionDirection.Down);
                } else if (isTilemap) {
                    float lowerBound = raycastPos.x - (ColliderSize.x * 0.5f) - 0.01f;
                    float upperBound = raycastPos.x + (ColliderSize.x * 0.5f) + 0.01f;
                    float y = hit.point.y + hit.normal.y * -0.2f;

                    AddTileContacts(lowerBound, upperBound, y, InteractionDirection.Down);
                }

                alignedDirection = Vector2.up;

            } else if (Mathf.Abs(angle) > 180 - maxFloorAngle) {
                // Roof
                Data.HitRoof = true;

                if (hit.collider.GetComponentInParent<NetworkObject>() is NetworkObject no) {
                    AddObjectContacts(no, InteractionDirection.Up);
                } else if (isTilemap) {
                    float lowerBound = raycastPos.x - (ColliderSize.x * 0.5f) - 0.01f;
                    float upperBound = raycastPos.x + (ColliderSize.x * 0.5f) + 0.01f;
                    float y = hit.point.y + hit.normal.y * -0.2f;

                    AddTileContacts(lowerBound, upperBound, y, InteractionDirection.Up);
                }

                alignedDirection = Vector2.down;

            } else {
                InteractionDirection interactionDirection;
                if (angle > 0) {
                    // Left
                    Data.HitLeft = true;
                    interactionDirection = InteractionDirection.Left;
                    alignedDirection = Vector2.right;
                } else {
                    // Right
                    Data.HitRight = true;
                    interactionDirection = InteractionDirection.Right;
                    alignedDirection = Vector2.left;
                }

                if (hit.collider.GetComponentInParent<NetworkObject>() is NetworkObject no) {
                    AddObjectContacts(no, interactionDirection);
                } else if (isTilemap) {
                    float lowerBound = raycastPos.y - (ColliderSize.y * 0.5f) - 0.01f;
                    float upperBound = raycastPos.y + (ColliderSize.y * 0.5f) + 0.01f;
                    float x = hit.point.x + hit.normal.x * -0.2f;

                    AddTileContacts(lowerBound, upperBound, x, interactionDirection);
                }
            }

            Vector2 positionToSurfacePoint = (direction * hit.distance) + (alignedDirection * Skin);

            // Started inside an object
            if (!gravityPass && hit.distance <= 0) {
                RaycastHit2D stuckHit = Runner.GetPhysicsScene2D().Raycast(raycastPos, (hit.point - raycastPos), Mathf.Max(size.x, size.y), filter);
                if (stuckHit) {
                    Vector2 offset = stuckHit.normal * (Vector2.Distance(hit.point, stuckHit.point) + Skin + Skin + Skin);

                    if (LockX) {
                        offset.x = 0;
                    }

                    if (LockY) {
                        offset.y = 0;
                    }

                    Position += offset;
                    raycastPos += offset;

                    return CollideAndSlide(raycastPos, raycastVel, false, depth + 1);
                }
            }

            // Eject from walls
            if (Vector2.Dot(positionToSurfacePoint, hit.normal) > 0) {
                // Hit normal pushing us away from the wall
                Vector2 offset = (Vector2) Vector3.Project(positionToSurfacePoint, hit.normal);
                // Maybe the offset shouldnt be based on the normal???

                if (LockX) {
                    offset.x = 0;
                }

                if (LockY) {
                    offset.y = 0;
                }

                Position += offset;
                raycastPos += offset;
                direction = ((Vector2) Vector3.ProjectOnPlane(direction, hit.normal)).normalized;
                positionToSurfacePoint = direction * hit.distance;
            }

            if (Data.OnGround && gravityPass) {
                return Vector2.zero;
            }

            Vector2 leftover = raycastVel - positionToSurfacePoint;
            leftover = bounceOnImpacts ? Vector2.Reflect(leftover, hit.normal) : Vector3.ProjectOnPlane(leftover, hit.normal);

            return positionToSurfacePoint + CollideAndSlide(raycastPos + positionToSurfacePoint, leftover, gravityPass, depth + 1);
        }

        return raycastVel;
    }

    public void AddTileContacts(float lowerBound, float upperBound, float otherComponent, InteractionDirection direction) {
        for (float i = Mathf.FloorToInt(lowerBound * 2) * 0.5f; i <= Mathf.FloorToInt(upperBound * 2) * 0.5f; i += 0.5f) {
            Vector2 worldLoc;
            if (direction == InteractionDirection.Up || direction == InteractionDirection.Down) {
                worldLoc = new(i, otherComponent);
            } else {
                worldLoc = new(otherComponent, i);
            }

            PhysicsDataStruct.TileContact tile = new() {
                location = Utils.WorldToTilemapPosition(worldLoc),
                direction = direction,
            };
            if (!Data.TileContacts.Contains(tile)) {
                Data.TileContacts.Add(tile);
            }
        }
    }

    public void AddObjectContacts(NetworkObject obj, InteractionDirection direction) {
        PhysicsDataStruct.ObjectContact contact = new() {
            networkObjectId = obj.Id,
            direction = direction,
        };
        if (!Data.ObjectContacts.Contains(contact)) {
            Data.ObjectContacts.Add(contact);
        }
    }
}