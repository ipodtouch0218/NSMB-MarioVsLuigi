using Photon.Deterministic;
using UnityEngine;

namespace Quantum {
    public unsafe partial struct MarioPlayer {

        public bool IsStarmanInvincible => InvincibilityFrames > 0;
        public bool IsWallsliding => WallslideLeft || WallslideRight;
        public bool IsCrouchedInShell => CurrentPowerupState == PowerupState.BlueShell && IsCrouching && !IsInShell;
        public bool IsDamageable => !IsStarmanInvincible && DamageInvincibilityFrames == 0;

        public FPVector2 GetHeldItemOffset(Frame f, EntityRef mario) {
            if (!f.Exists(HeldEntity)) {
                return default;
            }

            var holdable = f.Unsafe.GetPointer<Holdable>(HeldEntity);
            var holdableShape = f.Unsafe.GetPointer<PhysicsCollider2D>(HeldEntity)->Shape;

            FP holdableYOffset = (holdableShape.Box.Extents.Y - holdableShape.Centroid.Y);

            if (holdable->HoldAboveHead) {
                var marioShape = f.Unsafe.GetPointer<PhysicsCollider2D>(mario)->Shape;
                FP pickupFrames = 27;
                FP time = FPMath.Clamp01((f.Number - HoldStartFrame) / pickupFrames);
                FP alpha = 1 - QuantumUtils.EaseOut(1 - time);
                return new FPVector2(
                    0,
                    (marioShape.Box.Extents.Y * 2 * alpha) + holdableYOffset
                );
            } else {
                return new FPVector2(
                    (FacingRight ? 1 : -1) * FP._0_25,
                    (CurrentPowerupState >= PowerupState.Mushroom ? FP._0_10 * 4 : Constants._0_09) + holdableYOffset
                );
            }
        }

        public bool CanHoldItem(Frame f, EntityRef entity) {
            Input input = default;
            if (PlayerRef.IsValid) {
                input = *f.GetPlayerInput(PlayerRef);
            }
            var physicsObject = f.Unsafe.GetPointer<PhysicsObject>(entity);
            var freezable = f.Unsafe.GetPointer<Freezable>(entity);
            bool forceHold = false;
            bool aboveHead = false;
            if (f.Unsafe.TryGetPointer(HeldEntity, out Holdable* holdable)) {
                aboveHead = holdable->HoldAboveHead;
                if (aboveHead) {
                    forceHold = (f.Number - HoldStartFrame) < 25;
                }
            }

            return (input.Sprint.IsDown || forceHold)
                && !freezable->IsFrozen(f) && CurrentPowerupState != PowerupState.MiniMushroom && !IsSkidding 
                && !IsInKnockback && KnockbackGetupFrames == 0 && !IsTurnaround && !IsPropellerFlying && !IsSpinnerFlying && !IsCrouching && !IsDead
                && !IsInShell && !WallslideLeft && !WallslideRight && (f.Exists(HeldEntity) || physicsObject->IsTouchingGround || JumpState < JumpState.DoubleJump)
                && !IsGroundpounding && !(!f.Exists(HeldEntity) && physicsObject->IsUnderwater && input.Jump.IsDown)
                && !(aboveHead && physicsObject->IsUnderwater);
        }

        public bool CanPickupItem(Frame f, EntityRef mario) {
            return !f.Exists(HeldEntity) && CanHoldItem(f, mario);
        }

        public bool InstakillsEnemies(PhysicsObject* physicsObject, bool includeSliding) {
            return CurrentPowerupState == PowerupState.MegaMushroom
                || IsStarmanInvincible
                || IsInShell
                || (includeSliding && ((IsSliding || IsCrouchedInShell) && FPMath.Abs(physicsObject->Velocity.X) > FP._0_10));
        }

        public int GetSpeedStage(PhysicsObject* physicsObject, MarioPlayerPhysicsInfo physicsInfo) {
            FP xVel = FPMath.Abs(physicsObject->Velocity.X) - FP._0_01;
            FP[] arr;
            if (physicsObject->IsUnderwater) {
                if (physicsObject->IsTouchingGround) {
                    arr = CurrentPowerupState == PowerupState.BlueShell ? physicsInfo.SwimWalkShellMaxVelocity : physicsInfo.SwimWalkMaxVelocity;
                } else {
                    arr = physicsInfo.SwimMaxVelocity;
                }
            } else if ((IsSpinnerFlying || IsPropellerFlying) && CurrentPowerupState != PowerupState.MegaMushroom) {
                arr = physicsInfo.FlyingMaxVelocity;
            } else {
                arr = physicsInfo.WalkMaxVelocity;
            }

            for (int i = 0; i < arr.Length; i++) {
                if (xVel <= arr[i]) {
                    return i;
                }
            }
            return arr.Length - 1;
        }

        public int GetGravityStage(PhysicsObject* physicsObject, MarioPlayerPhysicsInfo physicsInfo) {
            FP yVel = physicsObject->Velocity.Y;
            FP[] maxArray = physicsObject->IsUnderwater ? physicsInfo.GravitySwimmingVelocity : (CurrentPowerupState == PowerupState.MegaMushroom ? physicsInfo.GravityMegaVelocity : (CurrentPowerupState == PowerupState.MiniMushroom ? physicsInfo.GravityMiniVelocity : physicsInfo.GravityVelocity));
            for (int i = 0; i < maxArray.Length; i++) {
                if (yVel >= maxArray[i]) {
                    return i;
                }
            }
            return maxArray.Length;
        }

        public void SetReserveItem(Frame f, PowerupAsset newItem) {
            var currentItem = f.FindAsset(ReserveItem);

            if (!currentItem) {
                // We don't have a reserve item, so we can just set it
                ReserveItem = newItem;
                return;
            }

            if (!newItem) {
                // Not a valid powerup, so just clear our reserve item instead
                ReserveItem = null;
                return;
            }

            sbyte newItemPriority = newItem ? newItem.ItemPriority : (sbyte) -1;
            sbyte currentItemPriority = currentItem ? currentItem.ItemPriority : (sbyte) -1;

            if (newItemPriority < currentItemPriority) {
                // New item is less important than our current reserve item, so we don't want to replace it
                return;
            }

            // Replace our current reserve item with the new one
            ReserveItem = newItem;
        }

        public void Death(Frame f, EntityRef entity, bool fire) {
            if (IsDead) {
                return;
            }

            IsDead = true;
            // DeathplaneDeath = deathplane;
            FireDeath = fire;

            PreRespawnFrames = 180;
            RespawnFrames = 78;

            if ((f.Global->Rules.IsLivesEnabled && QuantumUtils.Decrement(ref Lives)) || Disconnected) {
                // Last death - drop all stars at 0.5s each
                // TODO if (!GameManager.Instance.CheckForWinner()) {
                    SpawnStars(f, entity, 1);
                // }

                DeathAnimationFrames = (Stars > 0) ? (byte) 30 : (byte) 36;
            } else {
                SpawnStars(f, entity, 1);
                DeathAnimationFrames = 36;
            }

            // OnSpinner = null;
            CurrentPipe = default;
            IsInShell = false;
            IsPropellerFlying = false;
            PropellerLaunchFrames = 0;
            PropellerSpinFrames = 0;
            IsSpinnerFlying = false;
            IsDrilling = false;
            IsSliding = false;
            IsCrouching = false;
            IsSkidding = false;
            IsTurnaround = false;
            IsGroundpounding = false;
            IsInKnockback = false;
            WallslideRight = false;
            WallslideLeft = false;
            SwimForceJumpTimer = 0;
            
            /*
            IsWaterWalking = false;
            IsFrozen = false;
           
            if (FrozenCube) {
                Runner.Despawn(FrozenCube.Object);
            }
            */

            if (f.Exists(HeldEntity) && f.Unsafe.TryGetPointer(HeldEntity, out Holdable* holdable)) {
                holdable->DropWithoutThrowing(f, HeldEntity);
            }

            var physicsObject = f.Unsafe.GetPointer<PhysicsObject>(entity);
            physicsObject->IsFrozen = true;
            physicsObject->DisableCollision = true;

            f.Signals.OnMarioPlayerDied(entity);
            f.Events.MarioPlayerDied(f, entity, fire);
        }

        public bool Powerdown(Frame f, EntityRef entity, bool ignoreInvincible) {
            if (!ignoreInvincible && !IsDamageable) {
                return false;
            }

            PreviousPowerupState = CurrentPowerupState;

            switch (CurrentPowerupState) {
            case PowerupState.MiniMushroom:
            case PowerupState.NoPowerup: {
                Death(f,entity, false);
                break;
            }
            case PowerupState.Mushroom: {
                CurrentPowerupState = PowerupState.NoPowerup;
                SpawnStars(f, entity, 1);
                break;
            }
            case PowerupState.FireFlower:
            case PowerupState.IceFlower:
            case PowerupState.PropellerMushroom:
            case PowerupState.BlueShell: {
                CurrentPowerupState = PowerupState.Mushroom;
                SpawnStars(f, entity, 1);
                break;
            }
            }

            IsDrilling &= !IsPropellerFlying;
            IsPropellerFlying = false;
            IsInShell = false;
            PropellerLaunchFrames = 0;
            PropellerSpinFrames = 0;
            UsedPropellerThisJump = false;

            if (!IsDead) {
                DamageInvincibilityFrames = 2 * 60;
                f.Events.MarioPlayerTookDamage(f, entity);
            }
            return true;
        }

        public void SpawnStars(Frame f, EntityRef entity, int amount) {
            var stage = f.FindAsset<VersusStageData>(f.Map.UserAsset);
            var transform = f.Unsafe.GetPointer<Transform2D>(entity);
            bool fastStars = amount > 2 && Stars > 2;
            int starDirection = FacingRight ? 1 : 2;

            // If the level doesn't loop, don't have stars go towards the edges of the map
            if (!stage.IsWrappingLevel) {
                if (transform->Position.X > stage.StageWorldMin.X - 3) {
                    starDirection = 2;
                } else if (transform->Position.X < stage.StageWorldMax.X + 3) {
                    starDirection = 1;
                }
            }

            if (f.Global->Rules.IsLivesEnabled && Lives == 0) {
                fastStars = true;
                NoLivesStarDirection = (byte) ((NoLivesStarDirection + 1) % 4);
                starDirection = NoLivesStarDirection;

                starDirection = starDirection switch {
                    2 => 1,
                    1 => 2,
                    _ => starDirection
                };
            }

            int droppedStars = 0;
            while (amount > 0) {
                if (Stars <= 0) {
                    break;
                }

                int actualStarDirection = starDirection % 4;
                if (!fastStars) {
                    actualStarDirection = starDirection switch {
                        0 => 2,
                        3 => 1,
                        _ => starDirection
                    };
                }

                EntityRef newStarEntity = f.Create(f.SimulationConfig.BigStarPrototype);
                var newStar = f.Unsafe.GetPointer<BigStar>(newStarEntity);
                var newStarTransform = f.Unsafe.GetPointer<Transform2D>(newStarEntity);
                newStarTransform->Position = transform->Position;
                newStar->InitializeMovingStar(f, stage, newStarEntity, actualStarDirection);

                Stars--;
                amount--;
                droppedStars++;
                starDirection++;
            }

            if (droppedStars > 0) {
                f.Events.MarioPlayerDroppedStar(f, entity);
                GameLogicSystem.CheckForGameEnd(f);
            }
        }

        public void PreRespawn(Frame f, EntityRef entity, VersusStageData stage) {
            var physicsObject = f.Unsafe.GetPointer<PhysicsObject>(entity);
            var transform = f.Unsafe.GetPointer<Transform2D>(entity);

            RespawnFrames = 78;

            if ((f.Global->Rules.IsLivesEnabled && Lives == 0) || Disconnected) {
                f.Destroy(entity);
                return;
            }

            FPVector2 spawnpoint = stage.GetWorldSpawnpointForPlayer(SpawnpointIndex, f.Global->TotalMarios);
            transform->Position = spawnpoint;
            f.Unsafe.GetPointer<CameraController>(entity)->Recenter(stage, spawnpoint);
            
            IsDead = true;
            f.Unsafe.GetPointer<Freezable>(entity)->FrozenCubeEntity = EntityRef.None;
            IsRespawning = true;
            FacingRight = true;
            WallslideLeft = false;
            WallslideRight = false;
            WallslideEndFrames = 0;
            WalljumpFrames = 0;
            IsPropellerFlying = false;
            UsedPropellerThisJump = false;
            IsSpinnerFlying = false;
            PropellerLaunchFrames = 0;
            PropellerSpinFrames = 0;
            JumpState = JumpState.None;
            PreviousPowerupState = CurrentPowerupState = PowerupState.NoPowerup;
            //animationController.DisableAllModels();
            DamageInvincibilityFrames = 0;
            InvincibilityFrames = 0;
            MegaMushroomFrames = 0;
            MegaMushroomStartFrames = 0;
            MegaMushroomEndFrames = 0;
            // f.ResolveHashSet(WaterColliders).Clear();
            IsCrouching = false;
            IsSliding = false;
            IsTurnaround = false;
            IsInKnockback = false;
            IsGroundpounding = false;
            IsSkidding = false;
            IsInShell = false;
            IsTurnaround = false;
            SwimForceJumpTimer = 0;

            physicsObject->IsFrozen = true;
            physicsObject->Velocity = FPVector2.Zero;

            f.Events.MarioPlayerPreRespawned(f, entity);
        }

        public void Respawn(Frame f, EntityRef entity) {
            var physicsObject = f.Unsafe.GetPointer<PhysicsObject>(entity);

            IsDead = false;
            IsRespawning = false;
            DamageInvincibilityFrames = 120;
            CoyoteTimeFrames = 0;
            SwimForceJumpTimer = 0;

            physicsObject->IsFrozen = false;
            physicsObject->DisableCollision = false;

            f.Events.MarioPlayerRespawned(f, entity);
        }

        public void DoKnockback(Frame f, EntityRef entity, bool fromRight, int starsToDrop, bool weak, EntityRef attacker) {
            var physicsObject = f.Unsafe.GetPointer<PhysicsObject>(entity);
            if (physicsObject->IsUnderwater) {
                weak = false;
            }

            if (IsInKnockback && ((IsInWeakKnockback && weak) || !IsInWeakKnockback)) {
                return;
            }

            var freezable = f.Unsafe.GetPointer<Freezable>(entity);
            if (DamageInvincibilityFrames > 0 || f.Exists(CurrentPipe) || (freezable->IsFrozen(f) && freezable->FrozenCubeEntity != attacker) || IsDead || MegaMushroomStartFrames > 0 || MegaMushroomEndFrames > 0) {
                return;
            }

            if (CurrentPowerupState == PowerupState.MiniMushroom && starsToDrop > 1) {
                SpawnStars(f, entity, starsToDrop - 1);
                Powerdown(f, entity, false);
                return;
            }

            if (IsInKnockback || IsInWeakKnockback) {
                starsToDrop = Mathf.Min(1, starsToDrop);
            }

            IsInKnockback = true;
            IsInWeakKnockback = weak;
            KnockbackWasOriginallyFacingRight = FacingRight;
            KnockbackTick = f.Number;

            //IsInForwardsKnockback = FacingRight != fromRight;
            //KnockbackAttacker = attacker;

            // Don't go into walls
            var transform = f.Unsafe.GetPointer<Transform2D>(entity);
            var collider = f.Unsafe.GetPointer<PhysicsCollider2D>(entity);

            if (!weak && PhysicsObjectSystem.Raycast((FrameThreadSafe) f, null, transform->Position + collider->Shape.Centroid, fromRight ? FPVector2.Left : FPVector2.Right, FP._0_33, out _)) {
                fromRight = !fromRight;
            }

            physicsObject->Velocity = new FPVector2(
                (fromRight ? -1 : 1) *
                    (starsToDrop + 1) *
                    FP._1_50 *
                    (CurrentPowerupState == PowerupState.MegaMushroom ? 3 : 1) *
                    (CurrentPowerupState == PowerupState.MiniMushroom ? Constants._2_50 : 1) *
                    (weak ? FP._0_50 : 1),

                // Don't go upwards if we got hit by a fireball
                f.Has<Projectile>(attacker) ? 0 : Constants._4_50
            );

            //IsOnGround = false;
            //PreviousTickIsOnGround = false;
            IsInShell = false;
            IsGroundpounding = false;
            IsSpinnerFlying = false;
            IsPropellerFlying = false;
            PropellerLaunchFrames = 0;
            PropellerSpinFrames = 0;
            IsSliding = false;
            IsDrilling = false;
            WallslideLeft = WallslideRight = false;

            SpawnStars(f, entity, starsToDrop);
            //HandleLayerState();
            f.Events.MarioPlayerReceivedKnockback(f, entity, attacker, weak);
        }

        public void ResetKnockback(Frame f, EntityRef entity) {
            var physicsObject = f.Unsafe.GetPointer<PhysicsObject>(entity);
            KnockbackGetupFrames = (byte) (IsInWeakKnockback || physicsObject->IsUnderwater ? 0 : 25);
            DamageInvincibilityFrames = (byte) (60 + KnockbackGetupFrames);
            ////DoEntityBounce = false;
            IsInKnockback = false;
            IsInWeakKnockback = false;
            //IsForwardsKnockback = false;
            FacingRight = KnockbackWasOriginallyFacingRight;
            
            physicsObject->Velocity.X = 0;
        }

        public void EnterPipe(Frame f, EntityRef mario, EntityRef pipe) {
            if (f.Exists(CurrentPipe)
                || PipeCooldownFrames > 0) {
                return;
            }

            var physics = f.FindAsset(f.Unsafe.GetPointer<MarioPlayer>(mario)->PhysicsAsset);
            PipeFrames = physics.PipeEnterDuration;

            CurrentPipe = pipe;

            var pipeComponent = f.Unsafe.GetPointer<EnterablePipe>(pipe);
            PipeDirection = pipeComponent->IsCeilingPipe ? FPVector2.Up : FPVector2.Down;

            var pipeTransform = f.Unsafe.GetPointer<Transform2D>(pipe);
            var marioTransform = f.Unsafe.GetPointer<Transform2D>(mario);
            marioTransform->Position.X = pipeTransform->Position.X;

            IsCrouching = false;
            IsSliding = false;
            IsPropellerFlying = false;
            UsedPropellerThisJump = false;
            PropellerLaunchFrames = 0;
            PropellerSpinFrames = 0;
            IsSpinnerFlying = false;
            IsInShell = false;
            PipeEntering = true;

            if (InvincibilityFrames > 0) {
                InvincibilityFrames += (ushort) (PipeFrames * 2);
            }

            f.Events.MarioPlayerEnteredPipe(f, mario, CurrentPipe);
        }
    }
}