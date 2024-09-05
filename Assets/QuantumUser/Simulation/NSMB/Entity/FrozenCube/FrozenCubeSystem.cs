using Photon.Deterministic;

namespace Quantum {

    public unsafe class FrozenCubeSystem : SystemMainThreadFilter<FrozenCubeSystem.Filter>, ISignalOnThrowHoldable, ISignalOnEntityBumped {
        public struct Filter {
            public EntityRef Entity;
            public Transform2D* Transform;
            public FrozenCube* FrozenCube;
        }

        public override void OnInit(Frame f) {
            // TODO:
            // this can't use the interactable system
            // since it's a solid entity....
            InteractionSystem.RegisterInteraction<FrozenCube, Projectile>(OnFrozenCubeProjectileInteraction);
        }

        public override void Update(Frame f, ref Filter filter) {
            var childTransform = f.Unsafe.GetPointer<Transform2D>(filter.FrozenCube->Entity);

            childTransform->Position = filter.Transform->Position; // + offset
        }

        public void OnFrozenCubeProjectileInteraction(Frame f, EntityRef frozenCubeEntity, EntityRef projectileEntity) {
            // TODO:
            // this can't use the interactable system
            // since it's a solid entity....

            var projectileAsset = f.FindAsset(f.Get<Projectile>(projectileEntity).Asset);

            if (projectileAsset.Effect == ProjectileEffectType.Knockback) {
                // Fireball- destroy
                Destroy(f, frozenCubeEntity, FrozenCubeBreakReason.Fireball);
            }

            if (projectileAsset.DestroyOnHit) {
                ProjectileSystem.Destroy(f, projectileEntity, projectileAsset.DestroyParticleEffect);
            }
        }

        public static void Freeze(Frame f, EntityRef entityToFreeze) {
            if (!f.Has<Freezable>(entityToFreeze)) {
                return;
            }

            EntityRef frozenCubeEntity = f.Create(f.SimulationConfig.FrozenCubePrototype);
            var frozenCube = f.Unsafe.GetPointer<FrozenCube>(frozenCubeEntity);
            frozenCube->Initialize(f, frozenCubeEntity, entityToFreeze);
        }

        public void Destroy(Frame f, EntityRef frozenCube, FrozenCubeBreakReason breakReason) {
            f.Signals.OnFrozenCubeBroken(frozenCube, breakReason);
            f.Destroy(frozenCube);
        }

        public void OnThrowHoldable(Frame f, EntityRef entity, EntityRef marioEntity, QBoolean crouching) {
            if (!f.Unsafe.TryGetPointer(entity, out FrozenCube* cube)
                || !f.Unsafe.TryGetPointer(entity, out Holdable* holdable)
                || !f.Unsafe.TryGetPointer(entity, out PhysicsObject* physicsObject)
                || !f.Unsafe.TryGetPointer(marioEntity, out MarioPlayer* mario)) {
                return;
            }

            cube->IsSliding = true;
            cube->IsFlying = false;
            cube->FacingRight = mario->FacingRight;
            physicsObject->Velocity.Y = 0;
            holdable->IgnoreOwnerFrames = 15;

            f.Events.MarioPlayerThrewObject(f, marioEntity, mario, entity);
        }

        public void OnEntityBumped(Frame f, EntityRef entity, FPVector2 tileWorldPosition, EntityRef blockBump) {
            if (f.Has<FrozenCube>(entity)) {
                Destroy(f, entity, FrozenCubeBreakReason.BlockBump);
            }
        }
    }
}
