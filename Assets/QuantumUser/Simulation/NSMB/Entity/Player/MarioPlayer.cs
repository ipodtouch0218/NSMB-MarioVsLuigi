using Photon.Deterministic;

namespace Quantum {
    public unsafe partial struct MarioPlayer {

        public bool IsStarmanInvincible => InvincibilityFrames > 0;
        public bool IsWallsliding => WallslideLeft || WallslideRight;
        public bool IsCrouchedInShell => CurrentPowerupState == PowerupState.BlueShell && IsCrouching && !IsInShell;
        public bool IsInWater => WaterColliderCount > 0;
        public bool IsDamageable => !IsStarmanInvincible && DamageInvincibilityFrames == 0;
       
        public FPVector2 GetHeldItemOffset(Frame f) {
            if (!f.Exists(HeldEntity)) {
                return default;
            }

            /*
             * if (f.TryGet(HeldEntity, out IceBlock ice)) {
                float time = Mathf.Clamp01(((renderTime ? Runner.LocalRenderTime : Runner.SimulationTime) - HoldStartTime) / pickupTime);
                HeldEntity.holderOffset = new(0, MainHitbox.size.y * (1f - Utils.Utils.QuadraticEaseOut(1f - time)), -2);
            } else */{
                var shape = f.Get<PhysicsCollider2D>(HeldEntity).Shape;
                return new FPVector2(
                    (FacingRight ? 1 : -1) * FP._0_25,
                    (CurrentPowerupState >= PowerupState.Mushroom ? FP._0_10 * 4 : FP.FromString("0.09")) + (shape.Box.Extents.Y - shape.Centroid.Y)
                );
            }
        }

        public bool CanHoldItem(Frame f, EntityRef mario) {
            return f.GetPlayerInput(PlayerRef)->Sprint.IsDown && /*!IsFrozen &&*/ CurrentPowerupState != PowerupState.MiniMushroom && !IsSkidding && !IsTurnaround && !IsPropellerFlying && !IsSpinnerFlying && !IsCrouching && !IsDead && !WallslideLeft && !WallslideRight && (f.Get<PhysicsObject>(mario).IsTouchingGround || JumpState < JumpState.DoubleJump) && !IsGroundpounding && !(!f.Exists(HeldEntity) && IsInWater && f.GetPlayerInput(PlayerRef)->Jump.IsDown);
        }

        public bool CanPickupItem(Frame f, EntityRef mario) {
            return !f.Exists(HeldEntity) && CanHoldItem(f, mario);
        }

        public bool InstakillsEnemies(PhysicsObject physicsObject) {
            return CurrentPowerupState == PowerupState.MegaMushroom || IsStarmanInvincible || IsInShell || (IsSliding && FPMath.Abs(physicsObject.Velocity.X) > FP._0_10);
        }

        public int GetSpeedStage(PhysicsObject physicsObject, MarioPlayerPhysicsInfo physicsInfo) {
            FP xVel = FPMath.Abs(physicsObject.Velocity.X) - FP._0_01;
            FP[] arr;
            if (IsInWater) {
                if (physicsObject.IsTouchingGround) {
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

        public int GetGravityStage(PhysicsObject physicsObject, MarioPlayerPhysicsInfo physicsInfo) {
            FP yVel = physicsObject.Velocity.Y;
            FP[] maxArray = IsInWater ? physicsInfo.GravitySwimmingVelocity : (CurrentPowerupState == PowerupState.MegaMushroom ? physicsInfo.GravityMegaVelocity : (CurrentPowerupState == PowerupState.MiniMushroom ? physicsInfo.GravityMiniVelocity : physicsInfo.GravityVelocity));
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

            if ((f.RuntimeConfig.LivesEnabled && QuantumUtils.Decrement(ref Lives)) || Disconnected) {
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
            
            /*
            IsWaterWalking = false;
            IsFrozen = false;
           
            if (FrozenCube) {
                Runner.Despawn(FrozenCube.Object);
            }
            */

            if (f.Exists(HeldEntity) && f.Unsafe.TryGetPointer(HeldEntity, out Holdable* holdable)) {
                holdable->Drop(f, HeldEntity);
            }

            f.Unsafe.GetPointer<PhysicsObject>(entity)->IsFrozen = true;
            f.Events.MarioPlayerDied(f, entity, this);
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
                f.Events.MarioPlayerTookDamage(f, entity, this);
            }
            return true;
        }

        public void SpawnStars(Frame f, EntityRef entity, int amount) {
            var stage = f.FindAsset<VersusStageData>(f.Map.UserAsset);
            var transform = f.Get<Transform2D>(entity);
            bool fastStars = amount > 2 && Stars > 2;
            int starDirection = FacingRight ? 1 : 2;

            // If the level doesn't loop, don't have stars go towards the edges of the map
            if (!stage.IsWrappingLevel) {
                if (transform.Position.X > stage.StageWorldMin.X - 3) {
                    starDirection = 1;
                } else if (transform.Position.X < stage.StageWorldMax.X + 3) {
                    starDirection = 2;
                }
            }

            if (f.RuntimeConfig.LivesEnabled && Lives == 0) {
                fastStars = true;
                NoLivesStarDirection = (byte) ((NoLivesStarDirection + 1) % 4);
                starDirection = NoLivesStarDirection;

                starDirection = starDirection switch {
                    2 => 1,
                    1 => 2,
                    _ => starDirection
                };
            }

            while (amount > 0) {
                if (Stars <= 0) {
                    break;
                }

                if (!fastStars) {
                    starDirection = starDirection switch {
                        0 => 2,
                        3 => 1,
                        _ => starDirection
                    };
                }

                EntityRef newStarEntity = f.Create(f.SimulationConfig.BigStarPrototype);
                var newStar = f.Unsafe.GetPointer<BigStar>(newStarEntity);
                var newStarTransform = f.Unsafe.GetPointer<Transform2D>(newStarEntity);
                newStarTransform->Position = transform.Position;

                newStar->InitializeMovingStar(f, newStarEntity, starDirection);

                Stars--;
                amount--;
            }
            // TODO GameManager.Instance.CheckForWinner();
        }

        public void PreRespawn(Frame f, EntityRef entity, VersusStageData stage) {
            var physicsObject = f.Unsafe.GetPointer<PhysicsObject>(entity);
            var transform = f.Unsafe.GetPointer<Transform2D>(entity);

            RespawnFrames = 78;

            if (f.RuntimeConfig.LivesEnabled && Lives == 0) {
                // TODO GameManager.Instance.CheckForWinner();
                f.Destroy(entity);
                return;
            }

            FPVector2 spawnpoint = stage.GetWorldSpawnpointForPlayer(SpawnpointIndex, f.RuntimeConfig.ExpectedPlayers);
            transform->Position = spawnpoint;
            f.Unsafe.GetPointer<CameraController>(entity)->Recenter(spawnpoint);
            
            IsDead = true;
            // IsFrozen = false;
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
            //MegaTimer = TickTimer.None;
            //MegaEndTimer = TickTimer.None;
            //MegaStartTimer = TickTimer.None;
            IsCrouching = false;
            IsSliding = false;
            IsTurnaround = false;
            IsInKnockback = false;
            IsGroundpounding = false;
            IsSkidding = false;
            IsInShell = false;
            IsTurnaround = false;

            physicsObject->IsFrozen = true;
            physicsObject->Velocity = FPVector2.Zero;

            f.Events.MarioPlayerPreRespawned(f, entity, this);
        }

        public void Respawn(Frame f, EntityRef entity) {
            var physicsObject = f.Unsafe.GetPointer<PhysicsObject>(entity);

            IsDead = false;
            IsRespawning = false;
            DamageInvincibilityFrames = 120;

            physicsObject->IsFrozen = false;
            physicsObject->DisableCollision = false;

            f.Events.MarioPlayerRespawned(f, entity, this);
        }
    }
}