using Photon.Deterministic;

namespace Quantum {
    public unsafe class IceBlockSystem : SystemMainThreadEntityFilter<IceBlock, IceBlockSystem.Filter>, ISignalOnThrowHoldable, ISignalOnEntityBumped,
        ISignalOnBeforeInteraction, ISignalOnBobombExplodeEntity, ISignalOnTryLiquidSplash, ISignalOnEntityChangeUnderwaterState, ISignalOnMarioPlayerDied {

        public struct Filter {
            public EntityRef Entity;
            public Transform2D* Transform;
            public IceBlock* IceBlock;
            public PhysicsObject* PhysicsObject;
            public PhysicsCollider2D* PhysicsCollider;
            public Holdable* Holdable;
        }

        public override void OnInit(Frame f) {
            f.Context.Interactions.Register<MarioPlayer, IceBlock>(f, OnIceBlockMarioInteraction);
            f.Context.Interactions.Register<Coin, IceBlock>(f, OnIceBlockCoinInteraction);
        }

        public override void Update(Frame f, ref Filter filter, VersusStageData stage) {
            var entity = filter.Entity;
            var iceBlock = filter.IceBlock;

            if (!f.Unsafe.TryGetPointer(iceBlock->Entity, out Freezable* childFreezable)) {
                // Child despawned.
                Destroy(f, entity, IceBlockBreakReason.None, EntityRef.None);
                return;
            }

            var transform = filter.Transform;
            var physicsCollider = filter.PhysicsCollider;
            if (transform->Position.Y + (physicsCollider->Shape.Box.Extents.Y * 2) < stage.StageWorldMin.Y) {
                // Below the world
                Destroy(f, entity, IceBlockBreakReason.Other, EntityRef.None);
                return;
            }

            if (childFreezable->IsCarryable && (f.Number + entity.Index) % 2 == 0 
                && PhysicsObjectSystem.BoxInGround(f, transform->Position, physicsCollider->Shape, true, stage, entity)) {
                Destroy(f, entity, IceBlockBreakReason.HitWall, EntityRef.None);
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

            var physicsObject = filter.PhysicsObject;
            if (iceBlock->IsSliding) {
                physicsObject->IsFrozen = false;
                physicsObject->Velocity.X = (iceBlock->SlidingSpeed + iceBlock->BonusSpeed) * (iceBlock->FacingRight ? 1 : -1);

                if (physicsObject->IsTouchingLeftWall || physicsObject->IsTouchingRightWall) {
                    Destroy(f, entity, IceBlockBreakReason.HitWall, filter.Holdable->PreviousHolder);
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
                        Destroy(f, entity, IceBlockBreakReason.Timer, EntityRef.None);
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

        public static void Destroy(Frame f, EntityRef iceBlockEntity, IceBlockBreakReason breakReason, EntityRef attacker) {
            var iceBlock = f.Unsafe.GetPointer<IceBlock>(iceBlockEntity);
            if (f.Unsafe.TryGetPointer(iceBlock->Entity, out PhysicsObject* childPhysicsObject)) {
                childPhysicsObject->IsFrozen = false;
            }
            f.Signals.OnIceBlockBroken(iceBlockEntity, breakReason, attacker);
            f.Destroy(iceBlockEntity);
        }

        #region Interactions
        public static bool OnIceBlockMarioInteraction(Frame f, EntityRef marioEntity, EntityRef iceBlockEntity, PhysicsContact contact) {
            var mario = f.Unsafe.GetPointer<MarioPlayer>(marioEntity);
            var iceBlock = f.Unsafe.GetPointer<IceBlock>(iceBlockEntity);
            var holdable = f.Unsafe.GetPointer<Holdable>(iceBlockEntity);

            if (mario->IsStarmanOrMega) {
                Destroy(f, iceBlockEntity, IceBlockBreakReason.InvincibleMario, marioEntity);
                return true;
            }

            FP upDot = FPVector2.Dot(contact.Normal, FPVector2.Up);
            if (upDot >= Constants.PhysicsGroundMaxAngleCos) {
                // Top
                if (mario->IsGroundpoundActive) {
                    Destroy(f, iceBlockEntity, IceBlockBreakReason.Groundpounded, marioEntity);
                    return true;
                }
            } else if (upDot <= -Constants.PhysicsGroundMaxAngleCos) {
                // Bottom
                if (iceBlock->IsSliding) {
                    TryDamageMario();
                    Destroy(f, iceBlockEntity, IceBlockBreakReason.HitWall, marioEntity);
                    return false;
                } else if (f.Exists(holdable->Holder)) {
                    return false;
                }

                Destroy(f, iceBlockEntity, IceBlockBreakReason.BlockBump, marioEntity);
                return false;
            } else {
                // Side
                bool rightContact = contact.Normal.X > 0;
                if (mario->IsInShell) {
                    Destroy(f, iceBlockEntity, IceBlockBreakReason.Shell, marioEntity);
                    return false;
                } else if (iceBlock->IsSliding && iceBlock->FacingRight == rightContact) {
                    TryDamageMario();
                    Destroy(f, iceBlockEntity, IceBlockBreakReason.HitWall, marioEntity);
                    return false;
                }
            }

            if (!iceBlock->IsSliding) {
                // Attempt pickup (assuming it isn't already picked up)
                var child = f.Unsafe.GetPointer<Freezable>(iceBlock->Entity);

                if (!f.Exists(holdable->Holder)
                    && child->IsCarryable
                    && mario->CanPickupItem(f, marioEntity, iceBlockEntity)) {

                    // Pickup successful
                    holdable->Pickup(f, iceBlockEntity, marioEntity);

                    // Don't allow overflow
                    iceBlock->AutoBreakFrames = (byte) FPMath.Clamp(iceBlock->AutoBreakFrames + child->AutoBreakGrabAdditionalFrames, 0, byte.MaxValue);
                }
            }

            return false;

            void TryDamageMario() {
                bool dropStars = false;
                bool allowHit = holdable->PreviousHolder != marioEntity && mario->CheckTeamAttack(f, holdable->PreviousHolder, out dropStars);

                if (allowHit) {
                    bool damaged = mario->DoKnockback(f, marioEntity, contact.Normal.X < 0, dropStars ? 1 : 0, KnockbackStrength.FireballBump, iceBlockEntity);
                    if (damaged) {
                        FPVector2 particlePos = (f.Unsafe.GetPointer<Transform2D>(marioEntity)->Position + f.Unsafe.GetPointer<Transform2D>(iceBlockEntity)->Position) / 2;
                        f.Events.PlayKnockbackEffect(marioEntity, iceBlockEntity, KnockbackStrength.FireballBump, particlePos, true);
                    }
                }
            }
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
            if (dropped) {
                physicsObject->Velocity.X = 0;
            }
            ice->IsFlying = false;
            ice->FacingRight = mario->FacingRight;
            FP bonusSpeed = FPMath.Abs(marioPhysicsObject->Velocity.X / 3);
            if (FPMath.Sign(marioPhysicsObject->Velocity.X) != (mario->FacingRight ? 1 : -1)) {
                bonusSpeed *= -1;
            }
            ice->BonusSpeed = bonusSpeed;
            physicsObject->Velocity.Y = 0;
            holdable->IgnoreOwnerFrames = 15;

            if (!dropped) {
                f.Events.MarioPlayerThrewObject(marioEntity, entity);
            }
        }

        public void OnEntityBumped(Frame f, EntityRef entity, FPVector2 tileWorldPosition, EntityRef blockBump, QBoolean fromBelow) {
            if (f.Has<IceBlock>(entity)) {
                Destroy(f, entity, IceBlockBreakReason.BlockBump, blockBump);
            }
        }

        public void OnBeforeInteraction(Frame f, EntityRef entity, bool* allowInteraction) {
            *allowInteraction &= !f.Unsafe.TryGetPointer(entity, out Freezable* freezable) || !freezable->IsFrozen(f);
        }

        public void OnBobombExplodeEntity(Frame f, EntityRef bobomb, EntityRef entity) {
            if (f.Has<IceBlock>(entity)) {
                Destroy(f, entity, IceBlockBreakReason.Explosion, bobomb);
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
                    Destroy(f, entity, IceBlockBreakReason.Other, contact->Entity);
                }
                Destroy(f, contact->Entity, IceBlockBreakReason.Other, entity);
                *allowCollision = false;
            }
        }

        public void OnMarioPlayerDied(Frame f, EntityRef entity) {
            var freezable = f.Unsafe.GetPointer<Freezable>(entity);

            if (f.Exists(freezable->FrozenCubeEntity)) {
                Destroy(f, freezable->FrozenCubeEntity, IceBlockBreakReason.Other, EntityRef.None);
            }
        }
        #endregion
    }
}
