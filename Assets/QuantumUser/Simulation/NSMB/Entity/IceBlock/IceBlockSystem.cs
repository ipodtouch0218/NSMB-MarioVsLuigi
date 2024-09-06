using Photon.Deterministic;

namespace Quantum {

    public unsafe class IceBlockSystem : SystemMainThreadFilter<IceBlockSystem.Filter>, ISignalOnThrowHoldable, ISignalOnEntityBumped, ISignalOnBeforeInteraction,
        ISignalOnBobombExplodeEntity {

        public struct Filter {
            public EntityRef Entity;
            public Transform2D* Transform;
            public IceBlock* IceBlock;
        }

        public override void OnInit(Frame f) {
            InteractionSystem.RegisterInteraction<IceBlock, Projectile>(OnIceBlockProjectileInteraction);
            InteractionSystem.RegisterInteraction<IceBlock, MarioPlayer>(OnIceBlockMarioInteraction);
        }

        public override void Update(Frame f, ref Filter filter) {
            var childTransform = f.Unsafe.GetPointer<Transform2D>(filter.IceBlock->Entity);

            childTransform->Position = filter.Transform->Position; // + offset
        }

        public void OnIceBlockMarioInteraction(Frame f, EntityRef iceBlockEntity, EntityRef marioEntity, PhysicsContact contact) {
            var mario = f.Unsafe.GetPointer<MarioPlayer>(marioEntity);
            var iceBlock = f.Unsafe.GetPointer<IceBlock>(iceBlockEntity);

            FP upDot = FPVector2.Dot(contact.Normal, FPVector2.Up);
            if (upDot >= PhysicsObjectSystem.GroundMaxAngle) {
                // Top
                if (mario->IsGroundpoundActive) {
                    Destroy(f, iceBlockEntity, IceBlockBreakReason.Groundpounded);
                    return;
                }
            } else if (upDot <= -PhysicsObjectSystem.GroundMaxAngle) {
                // Bottom
                Destroy(f, iceBlockEntity, IceBlockBreakReason.BlockBump);
                return;
            } else {
                // Side
                if (iceBlock->IsSliding) {
                    var holdable = f.Unsafe.GetPointer<Holdable>(iceBlockEntity);
                    bool dropStars = !f.Unsafe.TryGetPointer(holdable->PreviousHolder, out MarioPlayer* holderMario) || mario->Team != holderMario->Team;
                    mario->DoKnockback(f, marioEntity, contact.Normal.X > 0, dropStars ? 1 : 0, !dropStars, iceBlockEntity);

                    Destroy(f, iceBlockEntity, IceBlockBreakReason.HitWall);
                    return;
                }
            }

            // Attempt pickup (assuming it isn't already picked up)
            if (!iceBlock->IsSliding) {
                var holdable2 = f.Unsafe.GetPointer<Holdable>(iceBlockEntity);
                if (!f.Exists(holdable2->Holder)
                    && mario->CanPickupItem(f, marioEntity)) {
                    // Pickup successful
                    holdable2->Pickup(f, iceBlockEntity, marioEntity);
                }
            }
        }

        public void OnIceBlockProjectileInteraction(Frame f, EntityRef frozenCubeEntity, EntityRef projectileEntity, PhysicsContact contact) {
            var projectileAsset = f.FindAsset(f.Get<Projectile>(projectileEntity).Asset);

            if (projectileAsset.Effect == ProjectileEffectType.Knockback) {
                // Fireball: destroy
                Destroy(f, frozenCubeEntity, IceBlockBreakReason.Fireball);
            }

            if (projectileAsset.DestroyOnHit) {
                ProjectileSystem.Destroy(f, projectileEntity, projectileAsset.DestroyParticleEffect);
            }
        }

        public static void Freeze(Frame f, EntityRef entityToFreeze) {
            if (!f.Has<Freezable>(entityToFreeze)) {
                return;
            }

            EntityRef frozenCubeEntity = f.Create(f.SimulationConfig.IceBlockPrototype);
            var frozenCube = f.Unsafe.GetPointer<IceBlock>(frozenCubeEntity);
            frozenCube->Initialize(f, frozenCubeEntity, entityToFreeze);
        }

        public void Destroy(Frame f, EntityRef frozenCube, IceBlockBreakReason breakReason) {
            f.Signals.OnIceBlockBroken(frozenCube, breakReason);
            f.Destroy(frozenCube);
        }

        public void OnThrowHoldable(Frame f, EntityRef entity, EntityRef marioEntity, QBoolean crouching) {
            if (!f.Unsafe.TryGetPointer(entity, out IceBlock* ice)
                || !f.Unsafe.TryGetPointer(entity, out Holdable* holdable)
                || !f.Unsafe.TryGetPointer(entity, out PhysicsObject* physicsObject)
                || !f.Unsafe.TryGetPointer(marioEntity, out MarioPlayer* mario)) {
                return;
            }

            ice->IsSliding = true;
            ice->IsFlying = false;
            ice->FacingRight = mario->FacingRight;
            physicsObject->Velocity.Y = 0;
            holdable->IgnoreOwnerFrames = 15;

            f.Events.MarioPlayerThrewObject(f, marioEntity, mario, entity);
        }

        public void OnEntityBumped(Frame f, EntityRef entity, FPVector2 tileWorldPosition, EntityRef blockBump) {
            if (f.Has<IceBlock>(entity)) {
                Destroy(f, entity, IceBlockBreakReason.BlockBump);
            }
        }

        public void OnBeforeInteraction(Frame f, EntityRef entity, bool* allowInteraction) {
            *allowInteraction &= !f.Unsafe.TryGetPointer(entity, out Freezable* freezable) || !freezable->IsFrozen(f);
        }

        public void OnBobombExplodeEntity(Frame f, EntityRef bobomb, EntityRef entity) {
            if (f.Has<IceBlock>(entity)) {
                Destroy(f, entity, IceBlockBreakReason.None);
            }
        }
    }
}
