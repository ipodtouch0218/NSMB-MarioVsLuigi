using UnityEngine;

using Fusion;
using NSMB.Entities.Enemies;
using NSMB.Utils;

namespace NSMB.Entities {

    [OrderAfter(typeof(Koopa))]
    public class EntityMover : NetworkBehaviour {

        //---Static Variables
        private static readonly RaycastHit2D[] RaycastBuffer = new RaycastHit2D[32];
        private static readonly float skin = 0.03f;

        //---Networked Variables
        [Networked] public Vector2 velocity { get; set; }
        [Networked] public Vector2 position { get; set; }
        [Networked] public NetworkBool freeze { get; set; }
        [Networked] public Vector2 gravity { get; set; }

        //---Properties
        private Vector2 ColliderOffset => transform.lossyScale * collider.offset;
        private Vector2 ColliderSize => transform.lossyScale * collider.size;

        //---Serialized Variables
        [SerializeField] private Transform interpolationTarget;
        [SerializeField] private BoxCollider2D collider;
        [SerializeField] private LayerMask layerMask;
        [SerializeField] private int maxIterations = 3;

        public override void Spawned() {
            position = transform.position;
        }

        public void LateUpdate() {

            if (!interpolationTarget)
                interpolationTarget = transform;

            interpolationTarget.position = position;

        }

        public override void FixedUpdateNetwork() {
            if (freeze)
                return;

            Vector2 movement;
            movement = CollideAndSlide(position + ColliderOffset, velocity * Runner.DeltaTime) / Runner.DeltaTime;
            movement += CollideAndSlide(position + ColliderOffset + (movement * Runner.DeltaTime), gravity * Runner.DeltaTime);

            velocity = movement;
            position += velocity * Runner.DeltaTime;
        }

        private Vector2 CollideAndSlide(Vector2 position, Vector2 velocity, int depth = 0) {
            if (depth >= maxIterations)
                return Vector2.zero;

            float distance = velocity.magnitude + skin;
            Vector2 direction = velocity.normalized;

            int hits = Runner.GetPhysicsScene2D().BoxCast(position, ColliderSize, 0, direction, distance, RaycastBuffer, layerMask);
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

                Vector2 surfaceDistance = direction * (hit.distance - skin);
                Vector2 leftover = velocity - surfaceDistance;

                if (surfaceDistance.sqrMagnitude <= skin * skin)
                    surfaceDistance = Vector2.zero;

                float leftoverDistance = leftover.magnitude;
                leftover = Vector3.ProjectOnPlane(leftover, hit.normal).normalized;
                leftover *= leftoverDistance;

                return surfaceDistance + CollideAndSlide(position + surfaceDistance, leftover, depth + 1);
            }


            return velocity;
        }

    }
}
