using Photon.Deterministic;
using UnityEngine;

namespace Quantum {

    public unsafe class IceBlockSystem : SystemMainThreadFilterStage<IceBlockSystem.Filter>, ISignalOnThrowHoldable, ISignalOnEntityBumped, ISignalOnBeforeInteraction,
        ISignalOnBobombExplodeEntity, ISignalOnTryLiquidSplash {

        public struct Filter {
            public EntityRef Entity;
            public Transform2D* Transform;
            public IceBlock* IceBlock;
            public PhysicsObject* PhysicsObject;
            public PhysicsCollider2D* PhysicsCollider;
        }

        public override void OnInit(Frame f) {
            InteractionSystem.RegisterInteraction<IceBlock, Projectile>(OnIceBlockProjectileInteraction);
            InteractionSystem.RegisterInteraction<IceBlock, MarioPlayer>(OnIceBlockMarioInteraction);
            InteractionSystem.RegisterInteraction<IceBlock, Coin>(OnIceBlockCoinInteraction);
        }

        public override void Update(Frame f, ref Filter filter, VersusStageData stage) {
            var entity = filter.Entity;
            var iceBlock = filter.IceBlock;
            if (!f.Exists(iceBlock->Entity)) {
                // Child despawned.
                Destroy(f, entity, IceBlockBreakReason.None);
            }

            var transform = filter.Transform;
            var childFreezable = f.Unsafe.GetPointer<Freezable>(iceBlock->Entity);
            var physicsObject = filter.PhysicsObject;

            if (f.Number % 2 == 0
                && PhysicsObjectSystem.BoxInGround(f, transform->Position, filter.PhysicsCollider->Shape, stage)) {

                Destroy(f, entity, IceBlockBreakReason.HitWall);
                return;
            }

            if (childFreezable->IsCarryable) {
                var childTransform = f.Unsafe.GetPointer<Transform2D>(iceBlock->Entity);
                childTransform->Position = transform->Position - iceBlock->ChildOffset;
            }

            if (iceBlock->IsSliding) {
                physicsObject->IsFrozen = false;
                physicsObject->Velocity.X = iceBlock->SlidingSpeed * (iceBlock->FacingRight ? 1 : -1);

                if (physicsObject->IsTouchingLeftWall || physicsObject->IsTouchingRightWall) {
                    Destroy(f, entity, IceBlockBreakReason.HitWall);
                    return;
                }
            } else if (iceBlock->IsFlying) {
                physicsObject->IsFrozen = true;
            }

            if (iceBlock->WaterColliderCount > 0) {
                iceBlock->IsSliding = false;
                if (iceBlock->InLiquidType != LiquidType.Water) {
                    physicsObject->Velocity.X *= FP.FromString(".85");
                    physicsObject->Velocity.Y = FPMath.Max(-FP._0_50, physicsObject->Velocity.Y);

                } else {
                    FP newVelocity = physicsObject->Velocity.Y;
                    if (newVelocity < 0) {
                        newVelocity *= FP._0_99;
                    }
                    newVelocity += (25 * f.DeltaTime);
                    newVelocity = FPMath.Min(1, newVelocity);

                    physicsObject->Velocity.X *= FP.FromString(".85");
                    physicsObject->Velocity.Y = newVelocity;
                }
            }

            if (iceBlock->AutoBreakFrames > 0 && iceBlock->TimerEnabled(f, entity)) {
                if (QuantumUtils.Decrement(ref iceBlock->AutoBreakFrames)) {
                    if (iceBlock->IsFlying && !physicsObject->IsTouchingGround) {
                        physicsObject->IsFrozen = false;
                        iceBlock->AutoBreakFrames = 1;

                    } else {
                        Destroy(f, entity, IceBlockBreakReason.Timer);
                        return;
                    }
                }
            }
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

            if (!iceBlock->IsSliding) {
                // Attempt pickup (assuming it isn't already picked up)
                var holdable2 = f.Unsafe.GetPointer<Holdable>(iceBlockEntity);
                var child = f.Unsafe.GetPointer<Freezable>(iceBlock->Entity);

                if (!f.Exists(holdable2->Holder)
                    && child->IsCarryable
                    && mario->CanPickupItem(f, marioEntity)) {

                    // Pickup successful
                    holdable2->Pickup(f, iceBlockEntity, marioEntity);

                    // Don't allow overflow
                    iceBlock->AutoBreakFrames = (byte) FPMath.Clamp(iceBlock->AutoBreakFrames + child->AutoBreakGrabAdditionalFrames, 0, byte.MaxValue);
                }
            }
        }

        public void OnIceBlockCoinInteraction(Frame f, EntityRef iceBlockEntity, EntityRef coinEntity) {
            var iceBlock = f.Unsafe.GetPointer<IceBlock>(iceBlockEntity);
            var holdable = f.Unsafe.GetPointer<Holdable>(iceBlockEntity);

            if (!iceBlock->IsSliding
                || !f.Exists(holdable->PreviousHolder)) {
                return;
            }

            CoinSystem.TryCollectCoin(f, coinEntity, holdable->PreviousHolder);
        }

        public void OnIceBlockProjectileInteraction(Frame f, EntityRef frozenCubeEntity, EntityRef projectileEntity, PhysicsContact contact) {
            var projectileAsset = f.FindAsset(f.Unsafe.GetPointer<Projectile>(projectileEntity)->Asset);

            if (projectileAsset.Effect == ProjectileEffectType.Knockback) {
                // Fireball: destroy
                Destroy(f, frozenCubeEntity, IceBlockBreakReason.Fireball);
            }

            if (projectileAsset.DestroyOnHit) {
                ProjectileSystem.Destroy(f, projectileEntity, projectileAsset.DestroyParticleEffect);
            }
        }

        public static EntityRef Freeze(Frame f, EntityRef entityToFreeze, bool flying = false) {
            if (!f.Has<Freezable>(entityToFreeze)) {
                return default;
            }

            EntityRef frozenCubeEntity = f.Create(f.SimulationConfig.IceBlockPrototype);
            var frozenCube = f.Unsafe.GetPointer<IceBlock>(frozenCubeEntity);
            frozenCube->Initialize(f, frozenCubeEntity, entityToFreeze);
            frozenCube->IsFlying = flying;
            return frozenCubeEntity;
        }

        public static void Destroy(Frame f, EntityRef frozenCube, IceBlockBreakReason breakReason) {
            f.Signals.OnIceBlockBroken(frozenCube, breakReason);
            f.Destroy(frozenCube);
        }

        public void OnThrowHoldable(Frame f, EntityRef entity, EntityRef marioEntity, QBoolean crouching, QBoolean dropped) {
            if (!f.Unsafe.TryGetPointer(entity, out IceBlock* ice)
                || !f.Unsafe.TryGetPointer(entity, out Holdable* holdable)
                || !f.Unsafe.TryGetPointer(entity, out PhysicsObject* physicsObject)
                || !f.Unsafe.TryGetPointer(marioEntity, out MarioPlayer* mario)
                || !f.Unsafe.TryGetPointer(marioEntity, out PhysicsObject* marioPhysicsObject)) {
                return;
            }

            ice->IsSliding = !dropped;
            ice->IsFlying = false;
            ice->FacingRight = mario->FacingRight;
            FP bonusSpeed = FPMath.Abs(marioPhysicsObject->Velocity.X / 3);
            if (FPMath.Sign(marioPhysicsObject->Velocity.X) != (mario->FacingRight ? 1 : -1)) {
                bonusSpeed *= -1;
            }
            ice->SlidingSpeed += bonusSpeed;
            physicsObject->Velocity.Y = 0;
            holdable->IgnoreOwnerFrames = 15;

            if (!dropped) {
                f.Events.MarioPlayerThrewObject(f, marioEntity, mario, entity);
            }
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

        public void OnTryLiquidSplash(Frame f, EntityRef entity, EntityRef liquidEntity, QBoolean exit, bool* doSplash) {
            if (f.Unsafe.TryGetPointer(entity, out IceBlock* iceBlock)) {
                *doSplash = true;

                if (exit) {
                    iceBlock->WaterColliderCount--;
                } else {
                    iceBlock->WaterColliderCount++;
                }

                var liquid = f.Unsafe.GetPointer<Liquid>(liquidEntity);
                iceBlock->InLiquidType = liquid->LiquidType;

                if (iceBlock->InLiquidType != LiquidType.Water) {
                    f.Events.IceBlockSinking(entity, iceBlock->InLiquidType);
                }
            } else if (f.Unsafe.TryGetPointer(entity, out Freezable* freezable)) {
                *doSplash &= !freezable->IsFrozen(f);
            }
        }
    }
}
