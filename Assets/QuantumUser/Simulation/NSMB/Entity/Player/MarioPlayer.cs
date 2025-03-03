using Photon.Deterministic;
using System;
using System.Drawing.Drawing2D;
using static Quantum.Core.FrameBase;

namespace Quantum {
    public unsafe partial struct MarioPlayer {

        public bool IsStarmanInvincible => InvincibilityFrames > 0;
        public bool IsCrouchedInShell => Action == PlayerAction.BlueShellCrouch;
        public bool IsDamageable => !IsStarmanInvincible && DamageInvincibilityFrames == 0;
        public bool IsDead => Action is PlayerAction.Death or PlayerAction.LavaDeath;
        public const int DropStarRight = 1 << 8;
        public const int NoStarLoss = -1;

        public byte GetTeam(Frame f) {
            var data = QuantumUtils.GetPlayerData(f, PlayerRef);
            if (data == null) {
                return 0;
            } else {
                return data->RealTeam;
            }
        }

        public int GetDeathArgs(Frame f) {
            if (!f.Global->Rules.IsLivesEnabled || Lives > 0) {
                return 0;
            } else {
                return 2;
            }
        }

        public ActionFlags GetActionFlags(PlayerAction action) {
            return action switch {
                PlayerAction.Idle                   => ActionFlags.AllowBump,
                PlayerAction.Walk                   => ActionFlags.AllowBump | ActionFlags.AllowHold,
                PlayerAction.Skidding               => ActionFlags.AllowBump,
                PlayerAction.Crouch                 => ActionFlags.AllowBump | ActionFlags.UsesCrouchHitbox | ActionFlags.IrregularVelocity,
                PlayerAction.CrouchAir              => ActionFlags.AllowBump | ActionFlags.UsesCrouchHitbox | ActionFlags.AirAction,
                PlayerAction.Sliding                => ActionFlags.AllowBump | ActionFlags.Attacking | ActionFlags.IrregularVelocity,
                // PlayerAction.Bounce                 => 0 all this action does is set to another action
                PlayerAction.SingleJump             => ActionFlags.AllowBump | ActionFlags.AllowHold | ActionFlags.AirAction,
                PlayerAction.DoubleJump             => ActionFlags.AllowBump | ActionFlags.AllowHold | ActionFlags.AirAction,
                PlayerAction.TripleJump             => ActionFlags.AllowBump | ActionFlags.AllowHold |ActionFlags.AirAction,
                PlayerAction.Freefall               => ActionFlags.AllowBump | ActionFlags.AllowHold | ActionFlags.AirAction,
                PlayerAction.HoldIdle               => ActionFlags.AllowBump | ActionFlags.Holding,
                PlayerAction.HoldWalk               => ActionFlags.AllowBump | ActionFlags.Holding,
                PlayerAction.HoldJump               => ActionFlags.AllowBump | ActionFlags.Holding | ActionFlags.AirAction,
                PlayerAction.HoldFall               => ActionFlags.AllowBump | ActionFlags.Holding | ActionFlags.AirAction,
                PlayerAction.WallSlide              => ActionFlags.AirAction,
                PlayerAction.Wallkick               => ActionFlags.AirAction,
                PlayerAction.GroundPound            => ActionFlags.AirAction | ActionFlags.DisableTurnaround | ActionFlags.NoPlayerBounce | ActionFlags.NoEnemyBounce | ActionFlags.StrongAction | ActionFlags.IrregularVelocity, // the 3 stars flag gets applied later
                // PlayerAction.MiniGroundPound        => (int) (ActionFlags.AirAction), // has player bounce
                PlayerAction.SoftKnockback          => ActionFlags.Intangible | ActionFlags.DisableTurnaround | ActionFlags.IrregularVelocity,
                PlayerAction.NormalKnockback        => ActionFlags.Intangible | ActionFlags.DisableTurnaround | ActionFlags.IrregularVelocity,
                PlayerAction.HardKnockback          => ActionFlags.Intangible | ActionFlags.DisableTurnaround | ActionFlags.IrregularVelocity,
                PlayerAction.SpinBlockSpin          => ActionFlags.AirAction | ActionFlags.CameraChange,
                PlayerAction.SpinBlockDrill         => ActionFlags.AirAction | ActionFlags.NoPlayerBounce,
                PlayerAction.BlueShellCrouch        => ActionFlags.IsShelled | ActionFlags.DisableTurnaround | ActionFlags.UsesCrouchHitbox | ActionFlags.IrregularVelocity,
                PlayerAction.BlueShellCrouchAir     => ActionFlags.IsShelled | ActionFlags.DisableTurnaround | ActionFlags.UsesCrouchHitbox | ActionFlags.IrregularVelocity,
                PlayerAction.BlueShellSliding       => ActionFlags.IsShelled | ActionFlags.DisableTurnaround | ActionFlags.UsesCrouchHitbox | ActionFlags.BreaksBlocks | ActionFlags.Attacking | ActionFlags.AirAction | ActionFlags.NoPlayerBounce | ActionFlags.IrregularVelocity,
                PlayerAction.BlueShellJump          => ActionFlags.IsShelled | ActionFlags.DisableTurnaround | ActionFlags.UsesCrouchHitbox | ActionFlags.AirAction | ActionFlags.IrregularVelocity, // the no player bounce based off ActionArg
                // PlayerAction.BlueShellGroundPound   => (int) (ActionFlags.IsShelled | ActionFlags.AirAction | ActionFlags.NoPlayerBounce),
                PlayerAction.PropellerSpin          => ActionFlags.AirAction | ActionFlags.CameraChange,
                // PlayerAction.PropellerFall          => (int) (ActionFlags.AirAction | ActionFlags.Takes1Star | ActionFlags.GivesNormalKnockback),
                PlayerAction.PropellerDrill         => ActionFlags.AirAction,
                PlayerAction.MegaMushroom           => ActionFlags.Cutscene | ActionFlags.OverrideAll,
                PlayerAction.PowerupShoot           => ActionFlags.AllowBump,
                PlayerAction.Pushing                => ActionFlags.AllowBump,
                PlayerAction.Death                  => ActionFlags.Cutscene | ActionFlags.Intangible | ActionFlags.OverrideAll,
                PlayerAction.LavaDeath              => ActionFlags.Cutscene | ActionFlags.Intangible | ActionFlags.OverrideAll,
                PlayerAction.Respawning             => ActionFlags.Cutscene | ActionFlags.Intangible,
                PlayerAction.EnteringPipe           => ActionFlags.Cutscene | ActionFlags.Intangible,
                _                                   => 0 // null
            };
        }

        public void DropItem(Frame f, EntityRef entity) {
            if (f.Unsafe.TryGetPointer(HeldEntity, out Holdable* heldItem)) {
                heldItem->Throw(f, entity, true);
            }
        }

        public void ThrowItem(Frame f, EntityRef entity) {
            if (f.Unsafe.TryGetPointer(HeldEntity, out Holdable* heldItem)) {
                heldItem->Throw(f, entity, false);
            }
        }

        public void DiscardItem(Frame f, EntityRef entity) {
            if (f.Unsafe.TryGetPointer(HeldEntity, out Holdable* heldItem)) {
                heldItem->DropWithoutThrowing(f, entity);
            }
        }

        public PlayerAction SetPlayerAction(PlayerAction playerAction, Frame f, int arg = 0, EntityRef actionObject = default, bool throwItem = false, bool dropItem = false, bool discardItem = false, EntityRef actionObjectB = default) {
            PrevAction = Action;
            PreActionInput = default;
            if (PlayerRef.IsValid && f.GetPlayerInput(PlayerRef) != null) {
                PreActionInput = *f.GetPlayerInput(PlayerRef);
            }

            if (throwItem) {
                ThrowItem(f, actionObjectB);
            } else if (dropItem) {
                DropItem(f, actionObjectB);
            } else if (discardItem) {
                DiscardItem(f, actionObjectB);
            }

            Action = playerAction;

            ActionTimer = 0;
            ActionState = 0;
            ActionArg = arg;
            ActionObject = actionObject;

            StarStealCount = NoStarLoss;
            StompAction = default;
            SetActionFlags(GetActionFlags(Action));

            UnityEngine.Debug.Log($"[Player] Set action to [{Enum.GetName(typeof(PlayerAction), playerAction)}] with Arg [{arg}]");
            return Action;
        }

        public bool SetPlayerActionOnce(PlayerAction playerAction, Frame f, int arg = 0, EntityRef actionObject = default) {
            if (Action == playerAction) {
                return false;
            }
            SetPlayerAction(playerAction, f, arg, actionObject);
            return true;
        }

        public PlayerAction? SetGroundAction(PhysicsObject* physicsObject, Frame f, PlayerAction? groundAction = null, int actionArg = 0) {
            if (physicsObject->IsTouchingGround) {
                PlayerAction targetAction;
                if (groundAction == null) {
                    if (HasActionFlags(ActionFlags.Holding)) {
                        targetAction = physicsObject->Velocity.X != 0 ? PlayerAction.HoldWalk : PlayerAction.HoldIdle;
                    } else {
                        targetAction = physicsObject->Velocity.X != 0 ? PlayerAction.Walk : PlayerAction.Idle;
                    }
                } else {
                    targetAction = groundAction.GetValueOrDefault();
                }
                return SetPlayerAction(targetAction, f, actionArg);
            }
            return null;
        }

        public PlayerAction? SetAirAction(PhysicsObject* physicsObject, Frame f, PlayerAction? airAction = null, int actionArg = 0, bool ignCoyote = false) {
            if (ignCoyote) {
                CoyoteTimeFrames = 0;
            }

            if (!physicsObject->IsTouchingGround && (CoyoteTimeFrames <= 0)) {
                PlayerAction targetAction;
                if (airAction == null) {
                    if (HasActionFlags(ActionFlags.Holding)) {
                        targetAction = PlayerAction.HoldFall;
                    } else {
                        targetAction = PlayerAction.Freefall;
                    }
                } else {
                    targetAction = airAction.GetValueOrDefault();
                }
                return SetPlayerAction(targetAction, f, actionArg);
            }
            return null;
        }

        public bool HasActionFlags(ActionFlags actionFlags) {
            return (this.CurrActionFlags & (int)actionFlags) != 0;
        }

        public void AddActionFlags(ActionFlags actionFlags) {
            this.CurrActionFlags |= (int) actionFlags;
        }

        public void ClearActionFlags(ActionFlags actionFlags) {
            this.CurrActionFlags &= ~(int) actionFlags;
        }

        public void ToggleActionFlags(ActionFlags actionFlags, bool add) {
            if (!add) {
                ClearActionFlags(actionFlags);
            } else {
                AddActionFlags(actionFlags);
            }
        }

        public void SetActionFlags(ActionFlags actionFlags) {
            this.CurrActionFlags = (int) actionFlags;
        }

        public void SetStompEvents(PlayerAction victimAction = PlayerAction.NormalKnockback, int starsToDrop = 1) {
            this.StarStealCount = starsToDrop;
            this.StompAction = victimAction;
        }

        public void ClearStompEvents() {
            this.StarStealCount = NoStarLoss;
            this.StompAction = default;
        }

        public bool CheckEntityBounce(Frame f, bool checkPlayer = false) {
            // invincible players should never bounce
            if (IsStarmanInvincible) {
                return false;
            }

            if (!checkPlayer) {
                if (HasActionFlags(ActionFlags.NoEnemyBounce)) {
                    return false;
                }
            } else {
                if (HasActionFlags(ActionFlags.NoPlayerBounce)) {
                    return false;
                }
            }
            SetPlayerAction(PlayerAction.Bounce, f);
            return true;
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
                && (HasActionFlags(ActionFlags.AllowHold) || HasActionFlags(ActionFlags.Holding)) && (f.Exists(item) || physicsObject->IsTouchingGround)
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

        public bool Powerdown(Frame f, EntityRef entity, bool ignoreInvincible) {
            if (!ignoreInvincible && !IsDamageable) {
                return false;
            }

            PreviousPowerupState = CurrentPowerupState;

            switch (CurrentPowerupState) {
            case PowerupState.MiniMushroom:
            case PowerupState.NoPowerup: {
                SetPlayerAction(PlayerAction.Death, f, GetDeathArgs(f), entity, discardItem: true);
                return true;
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

            DamageInvincibilityFrames = 2 * 60;
            f.Events.MarioPlayerTookDamage(f, entity);
            return true;
        }

        public void SpawnStars(Frame f, EntityRef entity, int amount) {
            var stage = f.FindAsset<VersusStageData>(f.Map.UserAsset);
            var transform = f.Unsafe.GetPointer<Transform2D>(entity);
            bool fastStars = amount > 2 && Stars > 2;
            int starDirection = FacingRight ? 1 : 2;

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