using Photon.Deterministic;
using System;
using System.Drawing.Drawing2D;

namespace Quantum {
    public unsafe partial struct MarioPlayer {

        public bool IsStarmanInvincible => InvincibilityFrames > 0;
        public bool IsWallsliding => WallslideLeft || WallslideRight;
        public bool IsCrouchedInShell => Action == PlayerAction.BlueShellCrouch;
        public bool IsDamageable => !IsStarmanInvincible && DamageInvincibilityFrames == 0;
        public const int DropStarRight = 1 << 8;

        public byte GetTeam(Frame f) {
            var data = QuantumUtils.GetPlayerData(f, PlayerRef);
            if (data == null) {
                return 0;
            } else {
                return data->RealTeam;
            }
        }

        public ActionFlags GetActionFlags(PlayerAction action) {
            return action switch {
                PlayerAction.Idle                   => ActionFlags.AllowBump,
                PlayerAction.HoldIdle               => ActionFlags.AllowBump,
                PlayerAction.Walk                   => ActionFlags.AllowBump,
                PlayerAction.HoldWalk               => ActionFlags.AllowBump,
                PlayerAction.Skidding               => ActionFlags.AllowBump,
                PlayerAction.Crouch                 => ActionFlags.AllowBump | ActionFlags.DisableTurnaround | ActionFlags.UsesCrouchHitbox,
                PlayerAction.Sliding                => ActionFlags.AllowBump | ActionFlags.Attacking,
                PlayerAction.SingleJump             => ActionFlags.AllowBump | ActionFlags.AirAction | ActionFlags.GivesNormalKnockback,
                PlayerAction.DoubleJump             => ActionFlags.AllowBump | ActionFlags.AirAction | ActionFlags.GivesNormalKnockback,
                PlayerAction.TripleJump             => ActionFlags.AllowBump | ActionFlags.AirAction | ActionFlags.GivesNormalKnockback,
                PlayerAction.HoldJump               => ActionFlags.AllowBump | ActionFlags.AirAction | ActionFlags.GivesNormalKnockback,
                PlayerAction.Freefall               => ActionFlags.AllowBump | ActionFlags.AirAction | ActionFlags.GivesNormalKnockback,
                PlayerAction.WallSlide              => ActionFlags.AirAction | ActionFlags.GivesNormalKnockback,
                PlayerAction.Wallkick               => ActionFlags.AirAction | ActionFlags.GivesNormalKnockback,
                PlayerAction.GroundPound            => ActionFlags.AirAction | ActionFlags.DisableTurnaround | ActionFlags.NoPlayerBounce | ActionFlags.NoEnemyBounce | ActionFlags.StrongAction, // the 3 stars flag gets applied later
                // PlayerAction.MiniGroundPound        => (int) (ActionFlags.AirAction), // has player bounce
                PlayerAction.SoftKnockback          => ActionFlags.Intangible | ActionFlags.DisableTurnaround,
                PlayerAction.NormalKnockback        => ActionFlags.Intangible | ActionFlags.DisableTurnaround,
                PlayerAction.HardKnockback          => ActionFlags.Intangible | ActionFlags.DisableTurnaround,
                PlayerAction.SpinBlockSpin          => ActionFlags.AirAction | ActionFlags.CameraChange | ActionFlags.GivesNormalKnockback,
                PlayerAction.SpinBlockDrill         => ActionFlags.AirAction | ActionFlags.GivesHardKnockback | ActionFlags.NoPlayerBounce,
                PlayerAction.BlueShellCrouch        => ActionFlags.IsShelled | ActionFlags.DisableTurnaround | ActionFlags.UsesCrouchHitbox,
                PlayerAction.BlueShellSliding       => ActionFlags.IsShelled | ActionFlags.DisableTurnaround | ActionFlags.UsesCrouchHitbox | ActionFlags.BreaksBlocks | ActionFlags.Attacking | ActionFlags.AirAction | ActionFlags.NoPlayerBounce,
                PlayerAction.BlueShellJump          => ActionFlags.IsShelled | ActionFlags.DisableTurnaround | ActionFlags.UsesCrouchHitbox | ActionFlags.AirAction, // the no player bounce based off ActionArg
                // PlayerAction.BlueShellGroundPound   => (int) (ActionFlags.IsShelled | ActionFlags.AirAction | ActionFlags.NoPlayerBounce),
                PlayerAction.PropellerSpin          => ActionFlags.AirAction | ActionFlags.CameraChange | ActionFlags.GivesNormalKnockback,
                // PlayerAction.PropellerFall          => (int) (ActionFlags.AirAction | ActionFlags.Takes1Star | ActionFlags.GivesNormalKnockback),
                PlayerAction.PropellerDrill         => ActionFlags.AirAction | ActionFlags.GivesHardKnockback,
                PlayerAction.MegaMushroom           => ActionFlags.Cutscene,
                PlayerAction.PowerupShoot           => ActionFlags.AllowBump,
                PlayerAction.Pushing                => ActionFlags.AllowBump,
                PlayerAction.Death                  => ActionFlags.Cutscene,
                PlayerAction.LavaDeath              => ActionFlags.Cutscene,
                PlayerAction.Respawning             => ActionFlags.Cutscene,
                PlayerAction.EnteringPipe           => ActionFlags.Cutscene,
                _                                   => 0 // null
            };
        }

        public PlayerAction SetPlayerAction(PlayerAction playerAction, int arg = 0, Frame f = null, EntityRef entityA = default, EntityRef entityB = default) {
            PrevAction = Action;
            Action = playerAction;

            ActionTimer = 0;
            ActionState = 0;
            ActionArg = arg;

            SetActionFlags(GetActionFlags(Action));

            UnityEngine.Debug.Log($"[Player] Set action to [{Enum.GetName(typeof(PlayerAction), playerAction)}]");
            return Action;
        }

        public bool SetPlayerActionOnce(PlayerAction playerAction, int arg = 0, Frame f = null, EntityRef entityA = default, EntityRef entityB = default) {
            if (Action == playerAction) {
                return false;
            }
            SetPlayerAction(playerAction, arg, f, entityA, entityB);
            return true;
        }

        public PlayerAction SetGroundAction(PhysicsObject* physicsObject, PlayerAction groundAction = PlayerAction.Idle, int actionArg = 0) {
            if (physicsObject->IsTouchingGround) {
                return SetPlayerAction(groundAction, actionArg);
            }
            return Action;
        }

        public PlayerAction SetAirAction(PhysicsObject* physicsObject, PlayerAction airAction = PlayerAction.Freefall, int actionArg = 0) {
            if (!physicsObject->IsTouchingGround) {
                return SetPlayerAction(airAction, actionArg);
            }
            return Action;
        }

        public bool HasActionFlags(ActionFlags actionFlags) {
            return (this.ActionFlags & (int)actionFlags) != 0;
        }

        public void AddActionFlags(ActionFlags actionFlags) {
            this.ActionFlags |= (int) actionFlags;
        }

        public void ClearActionFlags(ActionFlags actionFlags) {
            this.ActionFlags &= ~(int) actionFlags;
        }

        public void ToggleActionFlags(ActionFlags actionFlags, bool add) {
            if (!add) {
                ClearActionFlags(actionFlags);
            } else {
                AddActionFlags(actionFlags);
            }
        }

        public void SetActionFlags(ActionFlags actionFlags) {
            this.ActionFlags = (int) actionFlags;
        }

        public void CheckEntityBounce(bool checkPlayer = false) {
            if (!checkPlayer) {
                if (HasActionFlags(ActionFlags.NoEnemyBounce)) {
                    DoEntityBounce = false;
                    return;
                }
            } else {
                if (HasActionFlags(ActionFlags.NoPlayerBounce)) {
                    DoEntityBounce = false;
                    return;
                }
            }
            DoEntityBounce = true;
        }

        public FPVector2 GetHeldItemOffset(Frame f, EntityRef marioEntity) {
            if (!f.Exists(HeldEntity)) {
                return default;
            }

            var holdable = f.Unsafe.GetPointer<Holdable>(HeldEntity);
            var holdableShape = f.Unsafe.GetPointer<PhysicsCollider2D>(HeldEntity)->Shape;

            FP holdableYOffset = (holdableShape.Box.Extents.Y - holdableShape.Centroid.Y);

            if (holdable->HoldAboveHead) {
                var marioShape = f.Unsafe.GetPointer<PhysicsCollider2D>(marioEntity)->Shape;
                FP pickupFrames = 27;
                FP time = FPMath.Clamp01((f.Number - HoldStartFrame) / pickupFrames);
                FP alpha = 1 - QuantumUtils.EaseOut(1 - time);
                return new FPVector2(
                    0,
                    (marioShape.Box.Extents.Y * 2 * alpha) + holdableYOffset
                );
            } else {
                var marioPhysicsObject = f.Unsafe.GetPointer<PhysicsObject>(marioEntity);
                if (marioPhysicsObject->IsUnderwater) {
                    return new FPVector2(
                        (FacingRight ? 1 : -1) * (CurrentPowerupState >= PowerupState.Mushroom ? Constants._0_40 : FP._0_33),
                        (CurrentPowerupState >= PowerupState.Mushroom ? Constants._0_09 : FP._0_04) + holdableYOffset
                    );
                } else {
                    return new FPVector2(
                        (FacingRight ? 1 : -1) * FP._0_25,
                        (CurrentPowerupState >= PowerupState.Mushroom ? Constants._0_40 : Constants._0_09) + holdableYOffset
                    );
                }
            }
        }

        public bool CanHoldItem(Frame f, EntityRef entity, EntityRef item) {
            Input input = default;
            if (PlayerRef.IsValid) {
                input = *f.GetPlayerInput(PlayerRef);
            }
            var physicsObject = f.Unsafe.GetPointer<PhysicsObject>(entity);
            var freezable = f.Unsafe.GetPointer<Freezable>(entity);
            bool forceHold = false;
            bool aboveHead = false;
            if (f.Unsafe.TryGetPointer(item, out Holdable* holdable)) {
                aboveHead = holdable->HoldAboveHead;
                if (aboveHead) {
                    forceHold = (f.Number - HoldStartFrame) < 25;
                }
            }

            return (input.Sprint.IsDown || forceHold)
                && !freezable->IsFrozen(f) && CurrentPowerupState != PowerupState.MiniMushroom
                && HasActionFlags(ActionFlags.AllowHold) && (f.Exists(item) || physicsObject->IsTouchingGround)
                && !(!f.Exists(item) && physicsObject->IsUnderwater && input.Jump.IsDown) && !(aboveHead && physicsObject->IsUnderwater);
        }

        public bool CanPickupItem(Frame f, EntityRef mario, EntityRef item) {
            return !f.Exists(HeldEntity) && CanHoldItem(f, mario, item);
        }

        public bool InstakillsEnemies(PhysicsObject* physicsObject, bool includeSliding) {
            return CurrentPowerupState == PowerupState.MegaMushroom
                || IsStarmanInvincible
                || HasActionFlags(ActionFlags.Attacking)
                || (includeSliding && (Action == PlayerAction.Sliding || Action == PlayerAction.BlueShellSliding) && FPMath.Abs(physicsObject->Velocity.X) > FP._0_33);
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
            } else if ((Action == PlayerAction.SpinBlockSpin || Action == PlayerAction.PropellerSpin) && CurrentPowerupState != PowerupState.MegaMushroom) {
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

            if (currentItem == null) {
                // We don't have a reserve item, so we can just set it
                ReserveItem = newItem;
                return;
            }

            if (newItem == null) {
                // Not a valid powerup, so just clear our reserve item instead
                ReserveItem = null;
                return;
            }

            sbyte newItemPriority = newItem != null ? newItem.ItemPriority : (sbyte) -1;
            sbyte currentItemPriority = currentItem != null ? currentItem.ItemPriority : (sbyte) -1;

            if (newItemPriority < currentItemPriority) {
                // New item is less important than our current reserve item, so we don't want to replace it
                return;
            }

            // Replace our current reserve item with the new one
            ReserveItem = newItem;
        }

        public void Death(Frame f, EntityRef entity, bool fire, bool stars = true) {
            FireDeath = fire;
            f.Unsafe.GetPointer<Interactable>(entity)->ColliderDisabled = true;

            PreRespawnFrames = 180;
            RespawnFrames = 78;

            if ((f.Global->Rules.IsLivesEnabled && QuantumUtils.Decrement(ref Lives)) || Disconnected) {
                SpawnStars(f, entity, 1);
                DeathAnimationFrames = (Stars > 0) ? (byte) 30 : (byte) 36;
            } else {
                if (stars) {
                    SpawnStars(f, entity, 1);
                }
                DeathAnimationFrames = 36;
            }
            
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
            physicsObject->CurrentData = default;

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
                Death(f, entity, false);
                break;
            }
            case PowerupState.Mushroom: {
                CurrentPowerupState = PowerupState.NoPowerup;
                SpawnStars(f, entity, 1);
                break;
            }
            case PowerupState.HammerSuit:
            case PowerupState.FireFlower:
            case PowerupState.IceFlower:
            case PowerupState.PropellerMushroom:
            case PowerupState.BlueShell: {
                CurrentPowerupState = PowerupState.Mushroom;
                SpawnStars(f, entity, 1);
                break;
            }
            }

            if (Action == PlayerAction.PropellerDrill) {
                SetPlayerAction(PlayerAction.SpinBlockDrill, 1);
            } else if (HasActionFlags(ActionFlags.IsShelled)) {
                SetPlayerAction(PlayerAction.Walk);
            }

            if (Action != PlayerAction.Death && Action != PlayerAction.LavaDeath) {
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
            FacingRight = true;
            WallslideEndFrames = 0;
            WalljumpFrames = 0;
            UsedPropellerThisJump = false;
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
            SwimForceJumpTimer = 0;

            physicsObject->IsFrozen = true;
            physicsObject->Velocity = FPVector2.Zero;
            f.Unsafe.GetPointer<Interactable>(entity)->ColliderDisabled = false;

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
            SetPlayerAction(PlayerAction.Freefall);

            f.Events.MarioPlayerRespawned(f, entity);
        }

        public void DoKnockback(Frame f, EntityRef entity, EntityRef attacker) {
            var physicsObject = f.Unsafe.GetPointer<PhysicsObject>(entity);
            if (HasActionFlags(ActionFlags.Intangible)) {
                return;
            }

            var freezable = f.Unsafe.GetPointer<Freezable>(entity);
            if (freezable->IsFrozen(f)) {
                return;
            }

            bool weak = Action == PlayerAction.SoftKnockback;
            bool fromRight = (ActionArg & DropStarRight) != 0;

            int droppedStars = ActionArg % 256;
            /*if (CurrentPowerupState == PowerupState.MiniMushroom && starsToDrop > 1) {
                SpawnStars(f, entity, starsToDrop - 1);
                Powerdown(f, entity, false);
                return;
            }*/

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
                    (droppedStars + 1) *
                    FP._1_50 *
                    (CurrentPowerupState == PowerupState.MegaMushroom ? 3 : 1) *
                    (CurrentPowerupState == PowerupState.MiniMushroom ? Constants._2_50 : 1) *
                    (weak ? FP._0_50 : 1),

                // Don't go upwards if we got hit by a fireball
                f.Has<Projectile>(attacker) ? 0 : Constants._4_50
            );

            SpawnStars(f, entity, droppedStars);
            //HandleLayerState();
            f.Events.MarioPlayerReceivedKnockback(f, entity, attacker, Action);
        }

        public void ResetKnockback(Frame f, EntityRef entity) {
            var physicsObject = f.Unsafe.GetPointer<PhysicsObject>(entity);
            KnockbackGetupFrames = (byte) (Action == PlayerAction.SoftKnockback || physicsObject->IsUnderwater ? 0 : 25);
            DamageInvincibilityFrames = (byte) (60 + KnockbackGetupFrames);
            FacingRight = KnockbackWasOriginallyFacingRight;
            SetPlayerAction(PlayerAction.Idle);
            
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

            UsedPropellerThisJump = false;
            PropellerLaunchFrames = 0;
            PropellerSpinFrames = 0;

            if (InvincibilityFrames > 0) {
                InvincibilityFrames += (ushort) (PipeFrames * 2);
            }

            f.Events.MarioPlayerEnteredPipe(f, mario, CurrentPipe);
        }
    }
}