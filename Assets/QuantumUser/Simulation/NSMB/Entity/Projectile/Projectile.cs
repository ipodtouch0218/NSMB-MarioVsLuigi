using Photon.Deterministic;

namespace Quantum {
    public unsafe partial struct Projectile {

        public void Initialize(Frame f, EntityRef thisEntity, EntityRef owner, FPVector2 spawnpoint, bool right) {
            var asset = f.FindAsset(Asset);
            var transform = f.Unsafe.GetPointer<Transform2D>(thisEntity);
            var physicsObject = f.Unsafe.GetPointer<PhysicsObject>(thisEntity);

            // Vars
            Owner = owner;
            FacingRight = right;

            // Speed
            Speed = asset.Speed;
            physicsObject->Gravity = asset.Gravity;
            if (asset.InheritShooterVelocity
                && f.Unsafe.TryGetPointer(owner, out PhysicsObject* ownerPhysicsObject)
                // Moving in same direction
                && FPMath.Sign(ownerPhysicsObject->Velocity.X) == 1 == FacingRight) { 

                Speed += FPMath.Abs(ownerPhysicsObject->Velocity.X / 3);
            }

            if (asset.LockTo45Degrees) {
                physicsObject->TerminalVelocity = -Speed;
            }

            // Physics
            transform->Position = spawnpoint;
            physicsObject->Velocity = new(Speed * (FacingRight ? 1 : -1), -Speed);
        }

        public void InitializeHammer(Frame f, EntityRef thisEntity, EntityRef owner, FPVector2 spawnpoint, bool right, bool playerHoldingUp) {
            var asset = f.FindAsset(Asset);
            var transform = f.Unsafe.GetPointer<Transform2D>(thisEntity);
            var physicsObject = f.Unsafe.GetPointer<PhysicsObject>(thisEntity);

            // Vars
            Owner = owner;
            FacingRight = right;

            // Initial Velocity
            FPVector2 velocity = playerHoldingUp ? new FPVector2(FP.FromString("3.8822"), FP.FromString("14.4888")) : new FPVector2(FP.FromString("6.25"), FP.FromString("7.5"));
            Speed = velocity.X;
            
            // Apply
            transform->Position = spawnpoint;
            physicsObject->Velocity = velocity;
            physicsObject->Gravity = FPVector2.Up * (playerHoldingUp ? FP.FromString("-37.512") : FP.FromString("-28.125"));
        }

        public void InitializeBoomerang(Frame f, EntityRef thisEntity, EntityRef owner, FPVector2 spawnpoint, bool right) {
            var asset = f.FindAsset(Asset);
            var transform = f.Unsafe.GetPointer<Transform2D>(thisEntity);
            var physicsObject = f.Unsafe.GetPointer<PhysicsObject>(thisEntity);

            // Vars
            Owner = owner;
            FacingRight = right;

            // Speed
            Speed = asset.Speed;
            if (asset.InheritShooterVelocity
                && f.Unsafe.TryGetPointer(owner, out PhysicsObject* ownerPhysicsObject)
                && FPMath.Sign(ownerPhysicsObject->Velocity.X) == 1 == FacingRight) {
                Speed += FPMath.Abs(ownerPhysicsObject->Velocity.X / 3);
            }

            // Physics
            Combo = 0; // There are 3 phases for Boomerang: 0 = going, 1 = pausing, 2 = returning
            Frame = 0; // Lifetime counter
            transform->Position = spawnpoint;
            physicsObject->Velocity = new(Speed * (FacingRight ? 1 : -1), 0);
        }

        public void UpdateBoomerang(Frame f, EntityRef thisEntity, PhysicsObject* physicsObject, VersusStageData stage) {
            if (!f.Exists(thisEntity) || f.DestroyPending(thisEntity)) {
                return;
            }

            Frame++;
            var asset = f.FindAsset(Asset);

            // Going phase
            if (Combo == 0) {
                if (Frame >= 15) {
                    // Proceed to the next phase upon 15 frames of lifetime
                    Combo = 1;
                    // Reset frame counter
                    Frame = 0;
                }
            // Pausing phase
            } else if (Combo == 1) {
                // Slowdown
                Speed = asset.Speed * (15 - Frame) / 15;
                if (Frame >= 15) {
                    // Next phase
                    Combo = 2;
                    Frame = 0;
                    Speed = 0;
                }
            // Returning phase
            } else if (Combo == 2) {
                // Speed up
                Speed = Frame >= 15 ? asset.Speed : asset.Speed * Frame / 15;

                if (!f.Unsafe.TryGetPointer(thisEntity, out Transform2D* transform)
                    || !f.Unsafe.TryGetPointer(thisEntity, out PhysicsCollider2D* collider)) {
                    return;
                }

                // Touching the owner despawns it
                var hits = f.Physics2D.OverlapShape(transform->Position, 0, collider->Shape, f.Context.PlayerOnlyMask);
                for (int i = 0; i < hits.Count; i++) {
                    if (hits[i].Entity == Owner) {
                        ProjectileSystem.Destroy(f, thisEntity, asset.DestroyParticleEffect);
                        return;
                    }
                }

                if (f.Unsafe.TryGetPointer(Owner, out Transform2D* ownerTransform) && f.Unsafe.TryGetPointer(Owner, out PhysicsCollider2D* ownerCollider)) {
                    // Fix Mario's toes and fix loop points.
                    FPVector2 ownerCenter = ownerTransform->Position + ownerCollider->Shape.Centroid + new FPVector2(0, ownerCollider->Shape.Box.Extents.Y / 2);
                    QuantumUtils.UnwrapWorldLocations(stage, transform->Position, ownerCenter, out _, out FPVector2 closestOwner);
                    FPVector2 direction = (closestOwner - transform->Position).Normalized;
                    physicsObject->Velocity = direction * Speed;
                }

                physicsObject->Gravity = FPVector2.Zero;
            }
        }
    }
}