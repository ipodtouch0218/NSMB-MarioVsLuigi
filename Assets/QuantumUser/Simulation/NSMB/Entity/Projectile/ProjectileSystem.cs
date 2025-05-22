using Photon.Deterministic;

namespace Quantum {
    public unsafe class ProjectileSystem : SystemMainThreadEntityFilter<Projectile, ProjectileSystem.Filter>, ISignalOnProjectileHitEntity {
        public struct Filter {
            public EntityRef Entity;
            public Transform2D* Transform;
            public Projectile* Projectile;
            public PhysicsObject* PhysicsObject;
            public PhysicsCollider2D* PhysicsCollider;
        }

        public override void Update(Frame f, ref Filter filter, VersusStageData stage) {
            var collider = filter.PhysicsCollider;
            var transform = filter.Transform;

            if (filter.Transform->Position.Y + collider->Shape.Centroid.Y + collider->Shape.Box.Extents.Y < stage.StageWorldMin.Y) {
                Destroy(f, filter.Entity, ParticleEffect.None);
                return;
            }

            var projectile = filter.Projectile;
            var physicsObject = filter.PhysicsObject;
            var asset = f.FindAsset(projectile->Asset);

            // Check to instant-despawn if spawned inside a wall
            if (!physicsObject->DisableCollision && !projectile->CheckedCollision) {
                if (PhysicsObjectSystem.BoxInGround((FrameThreadSafe) f, transform->Position, collider->Shape)) {
                    Destroy(f, filter.Entity, asset.DestroyParticleEffect);
                    return;
                }
                projectile->CheckedCollision = true;
            }

            HandleTileCollision(f, ref filter, asset);

            physicsObject->Velocity.X = projectile->Speed * (projectile->FacingRight ? 1 : -1);

            if (asset.LockTo45Degrees) {
                physicsObject->TerminalVelocity = -projectile->Speed;
            }
        }

        public void HandleTileCollision(Frame f, ref Filter filter, ProjectileAsset asset) {
            var projectile = filter.Projectile;
            var physicsObject = filter.PhysicsObject;

            // Despawn
            if ((physicsObject->IsTouchingLeftWall
                || physicsObject->IsTouchingRightWall
                || physicsObject->IsTouchingCeiling
                || (physicsObject->IsTouchingGround && (!asset.Bounce || (projectile->HasBounced && asset.DestroyOnSecondBounce)))
                || PhysicsObjectSystem.BoxInGround((FrameThreadSafe) f, filter.Transform->Position, filter.PhysicsCollider->Shape)) && !physicsObject->DisableCollision) {

                Destroy(f, filter.Entity, asset.DestroyParticleEffect);
                return;
            }

            // Bounce
            if (physicsObject->IsTouchingGround && asset.Bounce) {
                FP boost = asset.BounceStrength * FPMath.Abs(FPMath.Sin(physicsObject->FloorAngle * FP.Deg2Rad)) * FP._1_25;
                if ((physicsObject->FloorAngle > 0) == projectile->FacingRight) {
                    boost = 0;
                }

                physicsObject->Velocity.Y = asset.BounceStrength + boost;
                physicsObject->IsTouchingGround = false;
                projectile->HasBounced = true;
            }
        }

        public static void Destroy(Frame f, EntityRef entity, ParticleEffect particle) {
            var transform = f.Unsafe.GetPointer<Transform2D>(entity);
            f.Events.ProjectileDestroyed(entity, particle, transform->Position);
            f.Destroy(entity);
        }

        public void OnProjectileHitEntity(Frame f, Frame frame, EntityRef projectileEntity, EntityRef hitEntity) {
            var projectile = f.Unsafe.GetPointer<Projectile>(projectileEntity);
            var projectileAsset = f.FindAsset(projectile->Asset);

            if (projectileAsset.DestroyOnHit) {
                Destroy(f, projectileEntity, projectileAsset.DestroyParticleEffect);
            } else if (projectileAsset.Bounce) {
                var physicsObject = f.Unsafe.GetPointer<PhysicsObject>(projectileEntity);
                projectile->Speed *= Constants._0_85;
                physicsObject->Gravity *= Constants._0_85;
                physicsObject->Velocity.Y = projectile->Speed;

                f.Events.EnemyKicked(hitEntity, false);
                if (projectile->Speed < 1) {
                    Destroy(f, projectileEntity, projectileAsset.DestroyParticleEffect);
                }
            }
        }
    }
}