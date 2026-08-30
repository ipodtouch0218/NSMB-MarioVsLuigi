using Photon.Deterministic;

namespace Quantum {
    public unsafe partial struct Enemy {
        public readonly bool IsAlive => !IsDead && IsActive;

        /**
         * <summary>
         * Sets the respawn data for the enemy
         * </summary>
         * <param name="waitTime">How long to wait until the enemy respawns in frames.</param>
         * <param name="sparklesTime">When the sparkles will spawn (based off time remaining) also in frames.</param>
         */
        public void SetDelayedRespawn(int waitTime = 420, int sparklesTime = 80) {
            RespawnTimer = waitTime;
            RespawnSparklesTimer = sparklesTime;
        }

        public void Respawn(Frame f, EntityRef entity) {
            var transform = f.Unsafe.GetPointer<Transform2D>(entity);

            IsActive = true;
            IsDead = false;
            IgnoreOffscreen = false;
            SetDelayedRespawn(0, 0);
            transform->Teleport(f, Spawnpoint);

            if (f.Unsafe.TryGetPointer(entity, out PhysicsObject* physicsObject)) {
                physicsObject->IsFrozen = false;
                physicsObject->Velocity = FPVector2.Zero;
                physicsObject->DisableCollision = false;
            }

            // face left by default
            var shouldFaceRight = false;

            // use closest player and face them
            if (QuantumUtils.FindClosestAliveMario(f, Spawnpoint, out FPVector2 closestMarioPosition) != EntityRef.None) {
                QuantumUtils.WrappedDistance(f, Spawnpoint, closestMarioPosition, out FP xDiff);
                shouldFaceRight = xDiff < 0;
            }

            FacingRight = shouldFaceRight;
            f.Signals.OnEnemyRespawned(entity);
        }

        public void ChangeFacingRight(Frame f, EntityRef entity, bool newFacingRight) {
            if (FacingRight != newFacingRight) {
                FacingRight = newFacingRight;
                f.Signals.OnEnemyTurnaround(entity);
            }
        }
    }
}