using Photon.Deterministic;

namespace Quantum {

    public unsafe class IceBlockSystem : SystemMainThreadEntityFilter<IceBlock, IceBlockSystem.Filter>, ISignalOnThrowHoldable, ISignalOnEntityBumped,
        ISignalOnBeforeInteraction, ISignalOnBobombExplodeEntity, ISignalOnTryLiquidSplash, ISignalOnEntityChangeUnderwaterState {

        public struct Filter {
            public EntityRef Entity;
            public Transform2D* Transform;
            public IceBlock* IceBlock;
            public PhysicsObject* PhysicsObject;
            public PhysicsCollider2D* PhysicsCollider;
        }

        public override void OnInit(Frame f) {
            f.Context.Interactions.Register<Projectile, IceBlock>(f, OnIceBlockProjectileInteraction);
            f.Context.Interactions.Register<MarioPlayer, IceBlock>(f, OnIceBlockMarioInteraction);
            f.Context.Interactions.Register<Coin, IceBlock>(f, OnIceBlockCoinInteraction);
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

            if ((f.Number + entity.Index) % 2 == 0 && PhysicsObjectSystem.BoxInGround((FrameThreadSafe) f, transform->Position, filter.PhysicsCollider->Shape, true, stage, entity)) {
                Destroy(f, entity, IceBlockBreakReason.HitWall);
                return;
            }

            if (childFreezable->IsCarryable) {
                var childTransform = f.Unsafe.GetPointer<Transform2D>(iceBlock->Entity);
                FPVector2 newPosition = transform->Position - iceBlock->ChildOffset;
                if (stage.IsWrappingLevel && FPVector2.Distance(childTransform->Position, newPosition) > stage.TileDimensions.X / (FP) 4) {
                    childTransform->Teleport(f, newPosition);
                } else {
                    childTransform->Position = newPosition;
                }
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

            if (physicsObject->IsUnderwater) {
                iceBlock->IsSliding = false;
                if (iceBlock->InLiquidType != LiquidType.Water) {
                    physicsObject->Velocity.X *= Constants._0_85;
                    physicsObject->Velocity.Y = FPMath.Max(-FP._0_50, physicsObject->Velocity.Y);
                } else {
                    FP newVelocity = physicsObject->Velocity.Y;
                    if (newVelocity < 0) {
                        newVelocity *= Constants._0_90;
                    }
                    newVelocity += (35 * f.DeltaTime);
                    newVelocity = FPMath.Min(Constants._0_90, newVelocity);

                    physicsObject->Velocity.X *= Constants._0_85;
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

        public static EntityRef Freeze(Frame f, EntityRef entityToFreeze, bool flying = false) {
            if (!f.Has<Freezable>(entityToFreeze)) {
                return default;
            }

            EntityRef iceBlockEntity = f.Create(f.SimulationConfig.IceBlockPrototype);
            var iceBlock = f.Unsafe.GetPointer<IceBlock>(iceBlockEntity);
            iceBlock->Initialize(f, iceBlockEntity, entityToFreeze);
            iceBlock->IsFlying = flying;
            return iceBlockEntity;
        }

        public static void Destroy(Frame f, EntityRef iceBlockEntity, IceBlockBreakReason breakReason) {
            f.Signals.OnIceBlockBroken(iceBlockEntity, breakReason);
            f.Destroy(iceBlockEntity);
        }

        #region Interactions
        public static bool OnIceBlockMarioInteraction(Frame f, EntityRef marioEntity, EntityRef iceBlockEntity, PhysicsContact contact) {
            var mario = f.Unsafe.GetPointer<MarioPlayer>(marioEntity);
            var iceBlock = f.Unsafe.GetPointer<IceBlock>(iceBlockEntity);

            if (mario->IsStarmanInvincible || mario->CurrentPowerupState == PowerupState.MegaMushroom) {
                Destroy(f, iceBlockEntity, IceBlockBreakReason.Other);
                return true;
            }

            FP upDot = FPVector2.Dot(contact.Normal, FPVector2.Up);
            if (upDot >= PhysicsObjectSystem.GroundMaxAngle) {
                // Top
                if (mario->IsGroundpoundActive) {
                    Destroy(f, iceBlockEntity, IceBlockBreakReason.Groundpounded);
                    return true;
                }
            } else if (upDot <= -PhysicsObjectSystem.GroundMaxAngle) {
                // Bottom
                Destroy(f, iceBlockEntity, IceBlockBreakReason.BlockBump);
                return false;
            } else {
                // Side
                bool rightContact = contact.Normal.X > 0;
                if ((iceBlock->IsSliding && iceBlock->FacingRight == rightContact) || mario->IsInShell) {
                    var holdable = f.Unsafe.GetPointer<Holdable>(iceBlockEntity);
                    bool dropStars = !f.Unsafe.TryGetPointer(holdable->PreviousHolder, out MarioPlayer* holderMario) || mario->GetTeam(f) != holderMario->GetTeam(f);
                    bool damaged = mario->DoKnockback(f, marioEntity, contact.Normal.X < 0, dropStars ? 1 : 0, KnockbackStrength.Normal, iceBlockEntity);
                    if (damaged) {
                        FPVector2 particlePos = (f.Unsafe.GetPointer<Transform2D>(marioEntity)->Position + f.Unsafe.GetPointer<Transform2D>(iceBlockEntity)->Position) / 2;
                        f.Events.PlayKnockbackEffect(marioEntity, iceBlockEntity, KnockbackStrength.Normal, particlePos);
                    }

                    Destroy(f, iceBlockEntity, IceBlockBreakReason.HitWall);
                    return false;
                }
            }

            if (!iceBlock->IsSliding) {
                // Attempt pickup (assuming it isn't already picked up)
                var holdable2 = f.Unsafe.GetPointer<Holdable>(iceBlockEntity);
                var child = f.Unsafe.GetPointer<Freezable>(iceBlock->Entity);

                if (!f.Exists(holdable2->Holder)
                    && child->IsCarryable
                    && mario->CanPickupItem(f, marioEntity, iceBlockEntity)) {

                    // Pickup successful
                    holdable2->Pickup(f, iceBlockEntity, marioEntity);

                    // Don't allow overflow
                    iceBlock->AutoBreakFrames = (byte) FPMath.Clamp(iceBlock->AutoBreakFrames + child->AutoBreakGrabAdditionalFrames, 0, byte.MaxValue);
                }
            }
            return false;
        }

        public static void OnIceBlockCoinInteraction(Frame f, EntityRef coinEntity, EntityRef iceBlockEntity) {
            var iceBlock = f.Unsafe.GetPointer<IceBlock>(iceBlockEntity);
            var holdable = f.Unsafe.GetPointer<Holdable>(iceBlockEntity);

            if (!iceBlock->IsSliding
                || !f.Exists(holdable->PreviousHolder)) {
                return;
            }

            CoinSystem.TryCollectCoin(f, coinEntity, holdable->PreviousHolder);
        }

        public static bool OnIceBlockProjectileInteraction(Frame f, EntityRef projectileEntity, EntityRef iceBlockEntity, PhysicsContact contact) {
            var projectileAsset = f.FindAsset(f.Unsafe.GetPointer<Projectile>(projectileEntity)->Asset);

            if (projectileAsset.Effect == ProjectileEffectType.Fire) {
                // Fireball: destroy
                Destroy(f, iceBlockEntity, IceBlockBreakReason.Fireball);
            }

            if (projectileAsset.DestroyOnHit) {
                ProjectileSystem.Destroy(f, projectileEntity, projectileAsset.DestroyParticleEffect);
            }
            return false;
        }
        #endregion

        #region Signals
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
                f.Events.MarioPlayerThrewObject(marioEntity, entity);
            }
        }

        public void OnEntityBumped(Frame f, EntityRef entity, FPVector2 tileWorldPosition, EntityRef blockBump, QBoolean fromBelow) {
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

                var liquid = f.Unsafe.GetPointer<Liquid>(liquidEntity);
                iceBlock->InLiquidType = liquid->LiquidType;

                if (iceBlock->InLiquidType != LiquidType.Water) {
                    f.Events.IceBlockSinking(entity, iceBlock->InLiquidType);
                }
            } else if (f.Unsafe.TryGetPointer(entity, out Freezable* freezable)) {
                *doSplash &= !freezable->IsFrozen(f);
            }
        }

        public void OnEntityChangeUnderwaterState(Frame f, EntityRef entity, EntityRef liquidEntity, QBoolean underwater) {
            if (!underwater
                || !f.Unsafe.TryGetPointer(entity, out IceBlock* iceBlock)
                || !f.Unsafe.TryGetPointer(liquidEntity, out Liquid* liquid)) {
                return;
            }

            iceBlock->InLiquidType = liquid->LiquidType;
        }

        public void OnBeforePhysicsCollision(Frame f, VersusStageData stage, EntityRef entity, PhysicsContact* contact, bool* allowCollision) {
            if (!f.Unsafe.TryGetPointer(entity, out IceBlock* iceBlockA)
                || !f.Unsafe.TryGetPointer(contact->Entity, out IceBlock* iceBlockB)
                || FPMath.Abs(FPVector2.Dot(contact->Normal, FPVector2.Up)) > FP._0_33) {
                return;
            }

            if (iceBlockA->IsSliding) {
                if (iceBlockB->IsSliding) {
                    Destroy(f, entity, IceBlockBreakReason.Other);
                }
                Destroy(f, contact->Entity, IceBlockBreakReason.Other);
                *allowCollision = false;
            }
        }
        #endregion
    }
}
