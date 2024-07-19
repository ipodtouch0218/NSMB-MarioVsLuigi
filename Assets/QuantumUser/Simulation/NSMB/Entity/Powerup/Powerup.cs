using Photon.Deterministic;

namespace Quantum {
    public unsafe partial struct Powerup {

        public void Initialize(byte spawnAnimationLength) {
            SpawnAnimationFrames = spawnAnimationLength;
            Lifetime += spawnAnimationLength;
        }

        public void Initialize(Frame f, EntityRef thisEntity, byte spawnAnimationLength, FPVector2 spawnOrigin, FPVector2 spawnDestination, bool launch = false) {
            Initialize(spawnAnimationLength);

            LaunchSpawn = launch;
            BlockSpawn = !launch;
            BlockSpawnOrigin = spawnOrigin;
            BlockSpawnDestination = spawnDestination;
            BlockSpawnAnimationLength = spawnAnimationLength;
            f.Unsafe.GetPointer<Transform2D>(thisEntity)->Position = spawnOrigin;
            f.Unsafe.GetPointer<PhysicsObject>(thisEntity)->IsFrozen = true;

            if (BlockSpawnDestination.Y < BlockSpawnOrigin.Y) {
                // Downwards powerup, adjust based on the powerup's height.
                BlockSpawnDestination.Y -= f.Get<PhysicsCollider2D>(thisEntity).Shape.Box.Extents.Y * 2;
            }

            var physicsObject = f.Unsafe.GetPointer<PhysicsObject>(thisEntity);
            if (launch) {
                // TODO magic number
                physicsObject->Velocity = new FPVector2(2, 9);
            } else {
                physicsObject->IsFrozen = true;
            }
        }

        public void ParentToPlayer(Frame f, EntityRef thisEntity, EntityRef playerToFollow) {
            Initialize(60);
            ParentMarioPlayer = playerToFollow;

            var marioTransform = f.Get<Transform2D>(playerToFollow);
            var marioCamera = f.Get<CameraController>(playerToFollow);

            // TODO magic value
            f.Unsafe.GetPointer<Transform2D>(thisEntity)->Position = new FPVector2(marioTransform.Position.X, marioCamera.CurrentPosition.Y + FP.FromString("1.68"));
            f.Unsafe.GetPointer<PhysicsObject>(thisEntity)->IsFrozen = true;
        }
    }
}