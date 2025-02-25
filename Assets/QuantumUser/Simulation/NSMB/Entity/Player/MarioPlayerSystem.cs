using Photon.Deterministic;
using Quantum.Collections;
using Quantum.Profiling;
using System;
using System.Drawing.Drawing2D;
using static IInteractableTile;

namespace Quantum {
    public unsafe class MarioPlayerSystem : SystemMainThreadFilterStage<MarioPlayerSystem.Filter>, ISignalOnComponentRemoved<Projectile>,
        ISignalOnGameStarting, ISignalOnBobombExplodeEntity, ISignalOnTryLiquidSplash, ISignalOnEntityBumped, ISignalOnBeforeInteraction,
        ISignalOnPlayerDisconnected, ISignalOnIceBlockBroken, ISignalOnStageReset, ISignalOnEntityChangeUnderwaterState, ISignalOnEntityFreeze {

        private static readonly FPVector2 DeathUpForce = new FPVector2(0, FP.FromString("6.5"));
        private static readonly FPVector2 DeathUpGravity = new FPVector2(0, FP.FromString("-12.75"));

        public struct Filter {
            public EntityRef Entity;
            public Transform2D* Transform;
            public MarioPlayer* MarioPlayer;
            public PhysicsObject* PhysicsObject;
            public PhysicsCollider2D* PhysicsCollider;
            public Freezable* Freezable;

            public Input Inputs;
        }

        public override void OnInit(Frame f) {
            f.Context.Interactions.Register<MarioPlayer, MarioPlayer>(f, OnMarioMarioInteraction);
            f.Context.Interactions.Register<MarioPlayer, Projectile>(f, OnMarioProjectileInteraction);
            f.Context.Interactions.Register<MarioPlayer, Coin>(f, OnMarioCoinInteraction);
            f.Context.Interactions.Register<MarioPlayer, InvisibleBlock>(f, OnMarioInvisibleBlockInteraction);
        }

        public override void Update(Frame f, ref Filter filter, VersusStageData stage) {
            var mario = filter.MarioPlayer;
            var player = mario->PlayerRef;

            Input* inputPtr;
            if (player.IsValid && (inputPtr = f.GetPlayerInput(player)) != null) {
                filter.Inputs = *inputPtr;
            } else {
                filter.Inputs = default;
            }

            if (f.GetPlayerCommand(player) is CommandSpawnReserveItem) {
                SpawnReserveItem(f, ref filter);
            }

            var physicsObject = filter.PhysicsObject;
            var physics = f.FindAsset(filter.MarioPlayer->PhysicsAsset);
            var freezable = filter.Freezable;
            if (HandleDeathAndRespawning(f, ref filter, stage)) {
                return;
            }
            if (HandleMegaMushroom(f, ref filter, physics, stage)) {
                HandleHitbox(f, ref filter, physics);
                return;
            }
            if (freezable->IsFrozen(f)) {
                return;
            }

            if (HandleStuckInBlock(f, ref filter, stage)) {
                return;
            }
            //HandleKnockback(f, ref filter);

            HandleJumping(f, ref filter, physics);
            HandleActions(f, ref filter, physics, stage);
            HandleGlobals(f, ref filter, physics, stage);
            HandlePowerups(f, ref filter, physics, stage);
            HandleBreakingBlocks(f, ref filter, physics, stage);
            //HandleGroundpound(f, ref filter, physics, ref input, stage);
            //HandleSliding(f, ref filter, physics, ref input);
            HandleWalkingRunning(f, ref filter, physics);
            HandleSpinners(f, ref filter, stage);
            HandleSwimming(f, ref filter, physics);
            //HandleBlueShell(f, ref filter, physics, ref input, stage);
            //HandleWallslide(f, ref filter, physics, ref input);
            HandleGravity(f, ref filter, physics);
            HandleTerminalVelocity(f, ref filter, physics);
            HandleFacingDirection(f, ref filter, physics);
            HandlePipes(f, ref filter, physics, stage);
            HandleHitbox(f, ref filter, physics);
        }

        #region Actions
        private void ActionIdleWalking(Frame f, ref Filter filter, MarioPlayerPhysicsInfo physics, VersusStageData stage) {
            var mario = filter.MarioPlayer;
            var physicsObject = filter.PhysicsObject;
            var entity = filter.Entity;
            var inputs = filter.Inputs;

            if (EnableShootingPowerups(f, ref filter, physics, mario->CurrentPowerupState) == HelperState.Success) return;
            if (EnablePropellerPowerup(f, ref filter, physics, mario->CurrentPowerupState, stage) == HelperState.Success) return;

            if (mario->LastPushingFrame + 5 >= f.Number) {
                mario->SetPlayerActionOnce(PlayerAction.Pushing, f);
            } else if (physicsObject->Velocity.X != 0) {
                mario->SetPlayerActionOnce(f.Exists(mario->HeldEntity) ? PlayerAction.HoldWalk : PlayerAction.Walk, f);
            } else {
                mario->SetPlayerActionOnce(f.Exists(mario->HeldEntity) ? PlayerAction.HoldIdle : PlayerAction.Idle, f);
            }

            if (mario->HasActionFlags(ActionFlags.Holding)) {
                if (!inputs.Sprint.IsDown) {
                    mario->ThrowItem(f, entity);
                }
            }

            if (inputs.Down.IsDown) {
                bool validFloorAngle = FPMath.Abs(physicsObject->FloorAngle) >= physics.SlideMinimumAngle;
                if (physicsObject->IsOnSlideableGround
                    && validFloorAngle
                    && !mario->HasActionFlags(ActionFlags.Holding)
                    && !((mario->FacingRight && physicsObject->IsTouchingRightWall) || (!mario->FacingRight && physicsObject->IsTouchingLeftWall))
                    //&& !mario->IsInShell /* && mario->CurrentPowerupState != PowerupState.MegaMushroom*/
                    // && !physicsObject->IsUnderwater
                    && mario->CurrentPowerupState != PowerupState.HammerSuit) { //Hammer Can't Slide, But Can gp To Slide (Weird Interaction But Works)

                    mario->SetPlayerAction(PlayerAction.Sliding, f);
                    return;
                }
                if (mario->CurrentPowerupState != PowerupState.BlueShell) {
                    mario->SetPlayerAction(PlayerAction.Crouch, f, dropItem: mario->HasActionFlags(ActionFlags.Holding), actionObjectB: filter.Entity);
                } else {
                    mario->SetPlayerAction(PlayerAction.BlueShellCrouch, f, dropItem: mario->HasActionFlags(ActionFlags.Holding), actionObjectB: filter.Entity);
                }
                return;
            }

            if (mario->Action == PlayerAction.Walk) {
                if (mario->CurrentPowerupState == PowerupState.BlueShell && !mario->HasActionFlags(ActionFlags.Holding)
                                && FPMath.Abs(physicsObject->Velocity.X) >= physics.WalkMaxVelocity[physics.RunSpeedStage] * Constants._0_90
                                && (physicsObject->Velocity.X > 0) == mario->FacingRight) {
                    mario->SetPlayerAction(PlayerAction.BlueShellSliding, f);
                    return;
                }
            }
            if (EnableSpinner(f, ref filter, physics)) {
                return;
            }
            if (JumpHandler(f, ref filter, physics, mario->HasActionFlags(ActionFlags.Holding) ? PlayerAction.HoldJump : null)) {
                f.Events.MarioPlayerJumped(f, filter.Entity, ConvertJumpState(mario->JumpState), false);
                return;
            }
            mario->SetAirAction(physicsObject, f);
        }

        private void ActionCrouching(Frame f, ref Filter filter, MarioPlayerPhysicsInfo physics, VersusStageData stage) {
            var mario = filter.MarioPlayer;
            var physicsObject = filter.PhysicsObject;
            var entity = filter.Entity;
            var inputs = filter.Inputs;

            mario->Stars += 9;

            if (!inputs.Down.IsDown) {
                mario->SetPlayerAction(physicsObject->Velocity.X == 0 ? PlayerAction.Idle : PlayerAction.Walk, f);
                return;
            }

            if (JumpHandler(f, ref filter, physics, PlayerAction.CrouchAir)) {
                f.Events.MarioPlayerJumped(f, entity, PlayerAction.CrouchAir, false);
                return;
            }

            if (mario->ActionTimer == 0) {
                f.Events.MarioPlayerCrouched(f, filter.Entity);
            }

            if (physicsObject->IsTouchingGround && FPMath.Abs(physicsObject->Velocity.X) > FP._0_05) {
                mario->AddActionFlags(ActionFlags.DisableTurnaround);
            } else if (mario->CurrentPowerupState != PowerupState.BlueShell) {
                mario->ClearActionFlags(ActionFlags.DisableTurnaround);
            }

            if (FPMath.Abs(physicsObject->Velocity.X) < FP._0_50) {
                mario->FacingRight = inputs.Right.IsDown;
            }

            bool validFloorAngle = FPMath.Abs(physicsObject->FloorAngle) >= physics.SlideMinimumAngle;
            if (physicsObject->IsOnSlideableGround
                && validFloorAngle
                && !((mario->FacingRight && physicsObject->IsTouchingRightWall) || (!mario->FacingRight && physicsObject->IsTouchingLeftWall))
                && mario->CurrentPowerupState != PowerupState.HammerSuit) {

                mario->SetPlayerAction(PlayerAction.Sliding, f);
                return;
            }

            QuantumUtils.Increment(ref mario->ActionTimer);
            mario->SetAirAction(physicsObject, f, PlayerAction.CrouchAir, 1, true);
        }

        private void ActionCrouchAir(Frame f, ref Filter filter, MarioPlayerPhysicsInfo physics, VersusStageData stage) {
            var mario = filter.MarioPlayer;
            var physicsObject = filter.PhysicsObject;
            var inputs = filter.Inputs;

            if (!inputs.Down.IsDown && physicsObject->Velocity.Y < 0) {
                // you can double jump if you do this in the original game
                mario->SetPlayerAction(PlayerAction.SingleJump, f);
                return;
            }
            EnablePropellerPowerup(f, ref filter, physics, mario->CurrentPowerupState, stage);

            mario->SetGroundAction(physicsObject, f, PlayerAction.Crouch);
        }

        private void ActionSliding(Frame f, ref Filter filter, MarioPlayerPhysicsInfo physics, VersusStageData stage) {
            var mario = filter.MarioPlayer;
            var physicsObject = filter.PhysicsObject;
            var inputs = filter.Inputs;

            /*if (!inputs.Down.IsDown) {
                mario->SetPlayerAction(physicsObject->Velocity.X == 0 ? PlayerAction.Idle : PlayerAction.Walk);
                return;
            }

            if (mario->ActionState == 0) {
                f.Events.MarioPlayerCrouched(f, filter.Entity);
                mario->ActionState++;
            }

            if (physicsObject->IsTouchingGround && FPMath.Abs(physicsObject->Velocity.X) > FP._0_05) {
                mario->AddActionFlags(ActionFlags.DisableTurnaround);
            } else if (mario->CurrentPowerupState != PowerupState.BlueShell) {
                mario->ClearActionFlags(ActionFlags.DisableTurnaround);
            }*/
            if (!SlidingPhysics(f, ref filter, physics)) {
                mario->SetPlayerAction(PlayerAction.Idle, f);
                return;
            }
            mario->SetAirAction(physicsObject, f);
        }

        private void ActionBounce(Frame f, ref Filter filter, MarioPlayerPhysicsInfo physics, VersusStageData stage) {
            var mario = filter.MarioPlayer;
            var physicsObject = filter.PhysicsObject;
            var inputs = filter.Inputs;
 
            // set the action based off the action right before this
            switch (mario->PrevAction) {
            case PlayerAction.SingleJump: {
                mario->LandedFrame = f.Number;
                mario->JumpState = JumpState.SingleJump;
                JumpHandler(f, ref filter, physics, actionArg: 1, skipJumpCheck: true, checkJumpDown: true);
                f.Events.MarioPlayerJumped(f, filter.Entity, ConvertJumpState(mario->JumpState), true);
                return;
            }
            case PlayerAction.DoubleJump: {
                mario->LandedFrame = f.Number;
                mario->JumpState = JumpState.DoubleJump;
                JumpHandler(f, ref filter, physics, actionArg: 1, skipJumpCheck: true, checkJumpDown: true);
                f.Events.MarioPlayerJumped(f, filter.Entity, ConvertJumpState(mario->JumpState), true);
                return;
            }
            case PlayerAction.TripleJump: {
                mario->LandedFrame = f.Number;
                mario->JumpState = JumpState.TripleJump;
                JumpHandler(f, ref filter, physics, actionArg: 1, skipJumpCheck: true, checkJumpDown: true);
                f.Events.MarioPlayerJumped(f, filter.Entity, ConvertJumpState(mario->JumpState), true);
                return;
            }
            case PlayerAction.PropellerDrill or PlayerAction.PropellerSpin: {
                mario->PropellerDrillCooldown = 30;
                physicsObject->Velocity.Y = physics.PropellerLaunchVelocity;
                mario->SetPlayerAction(PlayerAction.PropellerSpin, f, 1);
                return;
            }
            case PlayerAction.SpinBlockSpin or PlayerAction.SpinBlockDrill: {
                physicsObject->Velocity.Y = physics.JumpVelocity; // ?
                mario->SetPlayerAction(PlayerAction.SpinBlockSpin, f, 1);
                f.Events.MarioPlayerJumped(f, filter.Entity, ConvertJumpState(mario->JumpState), true);
                return;
            }
            default: {
                mario->LandedFrame = f.Number;
                mario->JumpState = JumpState.None;
                JumpHandler(f, ref filter, physics, PlayerAction.SingleJump, 1, true, true);
                f.Events.MarioPlayerJumped(f, filter.Entity, ConvertJumpState(mario->JumpState), true);
                return;
            }
            }
        }

        private void ActionSingleDoubleJump(Frame f, ref Filter filter, MarioPlayerPhysicsInfo physics, VersusStageData stage) {
            var mario = filter.MarioPlayer;
            var physicsObject = filter.PhysicsObject;
            var inputs = filter.Inputs;

            mario->JumpState = (mario->Action == PlayerAction.SingleJump) ? JumpState.SingleJump : JumpState.DoubleJump;
            if (EnableGroundpound(f, ref filter, physics, stage)
                || EnableShootingPowerups(f, ref filter, physics, mario->CurrentPowerupState) == HelperState.Success
                || EnablePropellerPowerup(f, ref filter, physics, mario->CurrentPowerupState, stage) == HelperState.Success) {
                return;
            }

            EnableWallKick(f, ref filter, physics);

            mario->ToggleActionFlags(ActionFlags.UsesSmallHitbox | ActionFlags.StarSpinAction, mario->IsStarmanInvincible && !physicsObject->IsTouchingGround);
            mario->SetStompEvents();
            
            if (JumpHandler(f, ref filter, physics)) {
                f.Events.MarioPlayerJumped(f, filter.Entity, ConvertJumpState(mario->JumpState), false);
                return;
            }
            mario->SetGroundAction(physicsObject, f);
        }

        private void ActionTripleJump(Frame f, ref Filter filter, MarioPlayerPhysicsInfo physics, VersusStageData stage) {
            var mario = filter.MarioPlayer;
            var physicsObject = filter.PhysicsObject;
            var inputs = filter.Inputs;

            EnableGroundpound(f, ref filter, physics, stage);
            if (physicsObject->IsTouchingGround) EnableShootingPowerups(f, ref filter, physics, mario->CurrentPowerupState);
            EnablePropellerPowerup(f, ref filter, physics, mario->CurrentPowerupState, stage);

            EnableWallKick(f, ref filter, physics);

            mario->ToggleActionFlags(ActionFlags.UsesSmallHitbox | ActionFlags.StarSpinAction, mario->IsStarmanInvincible && !physicsObject->IsTouchingGround);
            mario->SetStompEvents();

            if (!physicsObject->WasTouchingGround && physicsObject->IsTouchingGround) {
                // Landed Frame
                mario->LandedFrame = f.Number;
                if (!inputs.Left.IsDown && !inputs.Right.IsDown) {
                    physicsObject->Velocity.X = 0;
                    f.Events.MarioPlayerLandedWithAnimation(f, filter.Entity);
                }
            }

            if (JumpHandler(f, ref filter, physics)) {
                f.Events.MarioPlayerJumped(f, filter.Entity, ConvertJumpState(mario->JumpState), false);
                return;
            }

            if (mario->SetGroundAction(physicsObject, f) != PlayerAction.TripleJump) {
                f.Events.MarioPlayerLandedWithAnimation(f, filter.Entity);
            };
        }

        private void ActionFreefall(Frame f, ref Filter filter, MarioPlayerPhysicsInfo physics, VersusStageData stage) {
            var mario = filter.MarioPlayer;
            var physicsObject = filter.PhysicsObject;
            var inputs = filter.Inputs;

            EnableGroundpound(f, ref filter, physics, stage);
            EnableShootingPowerups(f, ref filter, physics, mario->CurrentPowerupState);
            EnablePropellerPowerup(f, ref filter, physics, mario->CurrentPowerupState, stage);

            EnableWallKick(f, ref filter, physics);

            mario->SetStompEvents();
            mario->SetGroundAction(physicsObject, f);
        }

        private void ActionHoldJump(Frame f, ref Filter filter, MarioPlayerPhysicsInfo physics, VersusStageData stage) {
            var mario = filter.MarioPlayer;
            var physicsObject = filter.PhysicsObject;
            var entity = filter.Entity;
            var inputs = filter.Inputs;

            if (!inputs.Sprint.IsDown) {
                mario->SetPlayerAction(PlayerAction.SingleJump, f, throwItem: true, actionObjectB: filter.Entity);
            }

            mario->SetStompEvents();
            mario->SetGroundAction(physicsObject, f);
        }

        private void ActionHoldFall(Frame f, ref Filter filter, MarioPlayerPhysicsInfo physics, VersusStageData stage) {
            var mario = filter.MarioPlayer;
            var physicsObject = filter.PhysicsObject;
            var entity = filter.Entity;
            var inputs = filter.Inputs;

            if (EnableGroundpound(f, ref filter, physics, stage)) {
                mario->DropItem(f, entity);
                return;
            }
            if (EnablePropellerPowerup(f, ref filter, physics, mario->CurrentPowerupState, stage) == HelperState.Success) {
                mario->DropItem(f, entity);
                return;
            }

            // EnableWallKick(f, ref filter, physics, ref inputs);

            mario->SetStompEvents();
            mario->SetGroundAction(physicsObject, f);
        }

        private void ActionWallSlide(Frame f, ref Filter filter, MarioPlayerPhysicsInfo physics, VersusStageData stage) {
            var mario = filter.MarioPlayer;
            var physicsObject = filter.PhysicsObject;
            var inputs = filter.Inputs;

            bool isLeft = mario->ActionArg == 0;
            FPVector2 currentWallDirection;
            if (isLeft) {
                currentWallDirection = FPVector2.Left;
                if (inputs.Left.IsDown) {
                    mario->ActionTimer = -1;
                }
            } else {
                currentWallDirection = FPVector2.Right;
                if (inputs.Right.IsDown) {
                    mario->ActionTimer = -1;
                }
            }

            HandleWallslideStopChecks(ref filter, currentWallDirection);

            if (mario->ActionTimer > 30) {
                mario->SetPlayerAction(PlayerAction.Freefall, f);
                return;
            }

            // Start wallslide
            mario->WallslideEndFrames = 0;

            // Walljump check
            physicsObject->Velocity.X = FPMath.Clamp(physicsObject->Velocity.X, -FP._0_25, FP._0_25);
            mario->FacingRight = !isLeft;
            if (mario->JumpBufferFrames > 0 && mario->WalljumpFrames == 0) {
                mario->SetPlayerAction(PlayerAction.Wallkick, f, mario->ActionArg);
                return;
            }

            mario->SetGroundAction(physicsObject, f);
            QuantumUtils.Increment(ref mario->ActionTimer);
        }

        private void ActionWallKick(Frame f, ref Filter filter, MarioPlayerPhysicsInfo physics, VersusStageData stage) {
            var mario = filter.MarioPlayer;
            var physicsObject = filter.PhysicsObject;
            var inputs = filter.Inputs;
            if (mario->ActionTimer == 0) {
                // Perform walljump
                physicsObject->Velocity = new(physics.WalljumpHorizontalVelocity * (mario->ActionArg == 0 ? 1 : -1), mario->CurrentPowerupState == PowerupState.MiniMushroom ? physics.WalljumpMiniVerticalVelocity : physics.WalljumpVerticalVelocity);
                mario->JumpState = JumpState.None;
                physicsObject->IsTouchingGround = false;
                f.Events.MarioPlayerWalljumped(f, filter.Entity, filter.Transform->Position, mario->ActionArg != 0);
                mario->WalljumpFrames = 16;
                mario->WallslideEndFrames = 0;
                mario->JumpBufferFrames = 0;
            }
            
            if (EnableShootingPowerups(f, ref filter, physics, mario->CurrentPowerupState) == HelperState.Success) {
                return;
            }

            if (mario->ActionTimer >= 20) {
                if (EnablePropellerPowerup(f, ref filter, physics, mario->CurrentPowerupState, stage) == HelperState.Success
                    || EnableWallKick(f, ref filter, physics)) {
                    return;
                }
            }
            mario->ToggleActionFlags(ActionFlags.UsesSmallHitbox | ActionFlags.StarSpinAction, mario->IsStarmanInvincible && !physicsObject->IsTouchingGround);
            mario->SetStompEvents();
            mario->SetGroundAction(physicsObject, f);
            QuantumUtils.Increment(ref mario->ActionTimer);
        }

        private void ActionGroundPound(Frame f, ref Filter filter, MarioPlayerPhysicsInfo physics, VersusStageData stage) {
            var mario = filter.MarioPlayer;
            var physicsObject = filter.PhysicsObject;
            var inputs = filter.Inputs;

            if (mario->CurrentPowerupState == PowerupState.MiniMushroom) {
                // remove these flags
                mario->ClearActionFlags(ActionFlags.NoPlayerBounce | ActionFlags.NoEnemyBounce | ActionFlags.StrongAction);
            }

            // flags such as ActionFlags.BreaksBlocks are added here
            HandleGroundpoundStartAnimation(ref filter, physics);
            HandleGroundpoundBlockCollision(f, ref filter, physics, stage, true);

            if (physicsObject->IsTouchingGround) {
                // remove these flags
                if (!mario->HasActionFlags(ActionFlags.IsShelled)) {
                    mario->ClearActionFlags(ActionFlags.DisableTurnaround);
                }
                if (!inputs.Down.IsDown) {
                    // Cancel from being grounded
                    mario->GroundpoundStandFrames = 15;
                    mario->SetGroundAction(physicsObject, f);
                    return;
                }
            } else if (inputs.Up.IsDown && mario->GroundpoundStartFrames == 0) {
                // Cancel from hitting "up"
                mario->GroundpoundCooldownFrames = 12;
                mario->SetAirAction(physicsObject, f);
                return;
            }
        }

        private void ActionKnockback(Frame f, ref Filter filter, MarioPlayerPhysicsInfo physics, VersusStageData stage) {
            var mario = filter.MarioPlayer;
            var entity = filter.Entity;
            var physicsObject = f.Unsafe.GetPointer<PhysicsObject>(entity);
            var inputs = filter.Inputs;

            var freezable = f.Unsafe.GetPointer<Freezable>(entity);

            bool weak = mario->Action == PlayerAction.SoftKnockback;
            bool fromRight = (mario->ActionArg & MarioPlayer.DropStarRight) != 0;

            int droppedStars = mario->ActionArg % MarioPlayer.DropStarRight;

            mario->KnockbackWasOriginallyFacingRight = mario->FacingRight;
            mario->KnockbackTick = f.Number;

            //IsInForwardsKnockback = FacingRight != fromRight;
            //KnockbackAttacker = attacker;

            // Don't go into walls
            var transform = f.Unsafe.GetPointer<Transform2D>(entity);
            var collider = f.Unsafe.GetPointer<PhysicsCollider2D>(entity);

            if (!weak && PhysicsObjectSystem.Raycast((FrameThreadSafe) f, null, transform->Position + collider->Shape.Centroid, fromRight ? FPVector2.Left : FPVector2.Right, FP._0_33, out _)) {
                fromRight = !fromRight;
            }

            // disable inputs
            filter.Inputs = default;

            if (mario->ActionState == 0) {
                if (mario->ActionTimer == 0) {
                    physicsObject->Velocity = new FPVector2(
                    (fromRight ? -1 : 1) *
                        (droppedStars + 1) *
                        FP._1_50 *
                        (mario->CurrentPowerupState == PowerupState.MegaMushroom ? 3 : 1) *
                        (mario->CurrentPowerupState == PowerupState.MiniMushroom ? Constants._2_50 : 1) *
                        (weak ? FP._0_50 : 1),

                    // Don't go upwards if we got hit by a fireball
                        f.Has<Projectile>(mario->ActionObject) ? 0 : Constants._4_50
                    );

                    mario->SpawnStars(f, entity, droppedStars);
                    UnityEngine.Debug.Log("Star drop count " + droppedStars);
                    //HandleLayerState();
                    f.Events.MarioPlayerReceivedKnockback(f, entity, mario->ActionObject, mario->Action);
                }

                if (physicsObject->IsTouchingGround || weak) {
                    mario->ActionState++;
                    mario->ActionTimer = 0;
                }
            } else if (mario->ActionState == 1) {
                int getUpTimes = mario->Action switch {
                    PlayerAction.NormalKnockback => 30,
                    PlayerAction.HardKnockback => 30,
                    PlayerAction.SoftKnockback => 60,
                    _ => -1,
                };

                if (!physicsObject->IsTouchingGround && !weak) {
                    mario->ActionTimer = 0;
                }

                if (mario->ActionTimer >= getUpTimes) {
                    mario->ActionState = 2;
                    mario->ActionTimer = 0;
                }
            } else if (mario->ActionState == 2) {
                int getupDelay = weak || physicsObject->IsUnderwater ? 0 : 25;
                if (mario->ActionTimer >= getupDelay) {
                    mario->DamageInvincibilityFrames = 60;
                    mario->FacingRight = mario->KnockbackWasOriginallyFacingRight;
                    physicsObject->Velocity = FPVector2.Zero;
                    mario->SetGroundAction(physicsObject, f);
                    mario->SetAirAction(physicsObject, f);
                    return;
                }
            }

            QuantumUtils.Increment(ref mario->ActionTimer);
        }

        private void ActionSpinBlockSpin(Frame f, ref Filter filter, MarioPlayerPhysicsInfo physics, VersusStageData stage) {
            var mario = filter.MarioPlayer;
            var physicsObject = filter.PhysicsObject;
            var inputs = filter.Inputs;
            mario->SetStompEvents();
            if (inputs.Down.IsDown) {
                // Start drill
                if (physicsObject->Velocity.Y < 0) {
                    physicsObject->Velocity.X = 0;
                    mario->SetPlayerAction(PlayerAction.SpinBlockDrill, f);
                    return;
                }
            }
            mario->SetGroundAction(physicsObject, f);
        }

        private void ActionSpinBlockDrill(Frame f, ref Filter filter, MarioPlayerPhysicsInfo physics, VersusStageData stage) {
            var mario = filter.MarioPlayer;
            var physicsObject = filter.PhysicsObject;
            var inputs = filter.Inputs;
            if (mario->ActionArg == 2) {
                mario->AddActionFlags(ActionFlags.NoEnemyBounce | ActionFlags.NoPlayerBounce);
                mario->SetStompEvents(PlayerAction.SoftKnockback, 1);
            } else {
                mario->SetStompEvents(PlayerAction.HardKnockback, 3);
            }

            if (!HandleGroundpoundBlockCollision(f, ref filter, physics, stage, false)) {
                mario->SetGroundAction(physicsObject, f);
            }
        }

        private void ActionBlueShellSliding(Frame f, ref Filter filter, MarioPlayerPhysicsInfo physics, VersusStageData stage) {
            var mario = filter.MarioPlayer;
            var physicsObject = filter.PhysicsObject;
            var inputs = filter.Inputs;

            BlueShellPhysics(f, ref filter, physics, stage);

            if (!inputs.Sprint.IsDown) {
                mario->SetGroundAction(physicsObject, f, PlayerAction.Walk);
            }
        }

        private void ActionBlueShellJump(Frame f, ref Filter filter, MarioPlayerPhysicsInfo physics, VersusStageData stage) {
            var mario = filter.MarioPlayer;
            var physicsObject = filter.PhysicsObject;
            var inputs = filter.Inputs;

            if (mario->ActionArg != 0) {
                mario->AddActionFlags(ActionFlags.BreaksBlocks);
                BlueShellPhysics(f, ref filter, physics, stage);
            }

            if (!inputs.Sprint.IsDown) {
                mario->SetAirAction(physicsObject, f);
            }
        }

        private void ActionPropellerSpin(Frame f, ref Filter filter, MarioPlayerPhysicsInfo physics, VersusStageData stage) {
            var mario = filter.MarioPlayer;
            var physicsObject = filter.PhysicsObject;
            var inputs = filter.Inputs;

            // got damaged
            if (mario->CurrentPowerupState <= PowerupState.Mushroom) {
                physicsObject->Velocity.Y = 0;
                mario->SetPlayerAction(PlayerAction.SpinBlockDrill, f, 2);
                return;
            } else if (mario->CurrentPowerupState != PowerupState.PropellerMushroom) {
                mario->SetAirAction(physicsObject, f);
                return;
            }

            if (inputs.Down.IsDown && mario->ActionTimer > (mario->ActionArg == 1 ? 20 : 30)) {
                mario->SetPlayerAction(PlayerAction.PropellerDrill, f);
                mario->PropellerLaunchFrames = 0;
                return;
            }
            if (!QuantumUtils.Decrement(ref mario->PropellerLaunchFrames)) {
                FP remainingTime = (FP) mario->PropellerLaunchFrames / 60;
                if (mario->PropellerLaunchFrames > 52) {
                    physicsObject->Velocity.Y = physics.PropellerLaunchVelocity;
                } else {
                    FP targetVelocity = physics.PropellerLaunchVelocity - (remainingTime < Constants._0_40 ? (1 - (remainingTime * Constants._2_50)) * physics.PropellerLaunchVelocity : 0);
                    physicsObject->Velocity.Y = FPMath.Min(physicsObject->Velocity.Y + (24 * f.DeltaTime), targetVelocity);
                }
            } else {
                if (EnableWallKick(f, ref filter, physics)) {
                    return;
                }
                if (physicsObject->IsTouchingGround) {
                    mario->PropellerSpinFrames = 0;
                    mario->UsedPropellerThisJump = false;
                } else if (inputs.PowerupAction.IsDown && physicsObject->Velocity.Y < -FP._0_10 && mario->PropellerSpinFrames < physics.PropellerSpinFrames / 4) {
                    mario->PropellerSpinFrames = physics.PropellerSpinFrames;
                    f.Events.MarioPlayerPropellerSpin(f, filter.Entity);
                }
            }
            mario->SetStompEvents();
            mario->SetGroundAction(physicsObject, f);
            QuantumUtils.Increment(ref mario->ActionTimer);
        }

        private void ActionPropellerDrill(Frame f, ref Filter filter, MarioPlayerPhysicsInfo physics, VersusStageData stage) {
            var mario = filter.MarioPlayer;
            var physicsObject = filter.PhysicsObject;
            var inputs = filter.Inputs;

            // got damaged revert to Spin Block Drill
            if (mario->CurrentPowerupState <= PowerupState.Mushroom) {
                mario->SetPlayerAction(PlayerAction.SpinBlockDrill, f, 1);
                return;
            } else if (mario->CurrentPowerupState != PowerupState.PropellerMushroom) {
                mario->SetAirAction(physicsObject, f);
                return;
            }

            if (inputs.Down.IsDown) {
                mario->PropellerDrillHoldFrames = 15;
            }

            if (QuantumUtils.Decrement(ref mario->PropellerDrillHoldFrames)) {
                mario->SetPlayerAction(PlayerAction.PropellerSpin, f, 1);
                return;
            }

            mario->SetStompEvents(PlayerAction.HardKnockback, 2);
            if (!HandleGroundpoundBlockCollision(f, ref filter, physics, stage, false)) {
                mario->SetGroundAction(physicsObject, f);
            }
        }

        private void ActionMegaMushroom(Frame f, ref Filter filter, MarioPlayerPhysicsInfo physics, VersusStageData stage) {
            var mario = filter.MarioPlayer;
            var physicsObject = filter.PhysicsObject;
            var transform = filter.Transform;
            var collider = filter.PhysicsCollider;
        }

        private void ActionPowerupShoot(Frame f, ref Filter filter, MarioPlayerPhysicsInfo physics, VersusStageData stage) {
            var mario = filter.MarioPlayer;
            var physicsObject = filter.PhysicsObject;
            var inputs = filter.Inputs;

            EntityPrototype tmpProj = null;
            switch (mario->ActionArg) {
            case (int) PowerupState.FireFlower:
                tmpProj = f.SimulationConfig.FireballPrototype;
                break;
            case (int) PowerupState.IceFlower:
                tmpProj = f.SimulationConfig.IceballPrototype;
                break;
            case (int) PowerupState.HammerSuit:
                tmpProj = f.SimulationConfig.HammerPrototype;
                break;
            default:
                break;
            }

            mario->CurrentProjectiles++;
            mario->ProjectileDelayFrames = physics.ProjectileDelayFrames;
            mario->ProjectileVolleyFrames = physics.ProjectileVolleyFrames;

            FPVector2 spawnPos = filter.Transform->Position + new FPVector2(mario->FacingRight ? Constants._0_40 : -Constants._0_40, Constants._0_35);

            if (tmpProj != null) {
                EntityRef newEntity = f.Create(tmpProj);

                if (f.Unsafe.TryGetPointer(newEntity, out Projectile* projectile)) {
                    projectile->Initialize(f, newEntity, filter.Entity, spawnPos, mario->FacingRight);
                }
                f.Events.MarioPlayerShotProjectile(f, filter.Entity, *projectile);
            }

            // Weird interaction in the main game...
            mario->WalljumpFrames = 0;
            mario->SetGroundAction(physicsObject, f); // return Action to idle
            mario->SetAirAction(physicsObject, f); // return Action to freefall if air
        }

        private void ActionDeath(Frame f, ref Filter filter, MarioPlayerPhysicsInfo physics, VersusStageData stage) {
            var mario = filter.MarioPlayer;
            var entity = filter.Entity;
            var transform = filter.Transform;
            var physicsObject = filter.PhysicsObject;
            var inputs = filter.Inputs;
            mario->ActionArg = Math.Max(mario->ActionArg, 2);
            f.Unsafe.GetPointer<Interactable>(entity)->ColliderDisabled = true;

            // disable inputs
            filter.Inputs = default;

            int deathUpFlag = 1 << 8;
            int actionState = mario->ActionState % deathUpFlag;

            bool doRespawn = mario->ActionArg < 2;
            switch (actionState) {
            case 0: {
                int endFrame = 160;
                if (mario->ActionTimer == 0) {
                    if ((f.Global->Rules.IsLivesEnabled && QuantumUtils.Decrement(ref mario->Lives)) || mario->Disconnected) {
                        mario->ActionTimer += 6;
                    }
                    mario->SpawnStars(f, entity, 1);
                    mario->ActionArg++;

                    if (f.Exists(mario->HeldEntity) && f.Unsafe.TryGetPointer(mario->HeldEntity, out Holdable* holdable)) {
                        holdable->DropWithoutThrowing(f, mario->HeldEntity);
                    }

                    physicsObject->IsFrozen = true;
                    physicsObject->DisableCollision = true;
                    physicsObject->CurrentData = default;

                    f.Signals.OnMarioPlayerDied(entity);
                    f.Events.MarioPlayerDied(f, entity, mario->Action == PlayerAction.LavaDeath);
                }
                
                if (!doRespawn && mario->Stars > 0) {
                    var volly = 33;
                    var speed = Math.Floor((decimal)((mario->ActionArg - 2) / 5)) * 3;
                    if (mario->ActionTimer == volly - Math.Min(speed, volly - 4)) {
                        // alternate
                        mario->FacingRight = !mario->FacingRight;

                        // Try to drop more stars
                        mario->SpawnStars(f, entity, 1);
                        mario->ActionTimer = 0;
                        mario->ActionArg++;
                    }
                }
                
                if (mario->ActionTimer >= 34 && (mario->ActionState & deathUpFlag) == 0) {
                    // Play the animation as normal
                    if (transform->Position.Y > stage.StageWorldMin.Y) {
                        mario->ActionState |= deathUpFlag;
                        physicsObject->Gravity = DeathUpGravity;
                        physicsObject->Velocity = DeathUpForce;
                        physicsObject->IsFrozen = false;
                        f.Events.MarioPlayerDeathUp(f, filter.Entity);
                    }
                    if (!doRespawn) {
                        endFrame = 124;
                    }
                }

                if (mario->ActionTimer == endFrame) {
                    mario->ActionState++;
                    mario->ActionTimer = 0;
                    return;
                }
                break;
            }
            case 1: {
                switch (mario->ActionTimer) {
                case 0: {
                    f.Events.StartCameraFadeOut(f, entity);
                    break;
                }
                case 20: {
                    mario->ActionState++;
                    mario->ActionTimer = 0;
                    return;
                }
                }
                break;
            }
            case 2: {
                f.Events.StartCameraFadeIn(f, entity);
                if (!doRespawn) {
                    f.Destroy(entity);
                    return;
                }
                mario->SetPlayerAction(PlayerAction.Respawning, f, mario->ActionArg);
                return;
            }
            }
            QuantumUtils.Increment(ref mario->ActionTimer);
        }

        private void ActionRespawning(Frame f, ref Filter filter, MarioPlayerPhysicsInfo physics, VersusStageData stage) {
            var mario = filter.MarioPlayer;
            var entity = filter.Entity;
            var physicsObject = f.Unsafe.GetPointer<PhysicsObject>(entity);
            var transform = f.Unsafe.GetPointer<Transform2D>(entity);
            var inputs = filter.Inputs;
            switch (mario->ActionState) {
            case 0: {
                if (mario->ActionTimer == 0) {
                    FPVector2 spawnpoint = stage.GetWorldSpawnpointForPlayer(mario->SpawnpointIndex, f.Global->TotalMarios);
                    transform->Position = spawnpoint;
                    f.Unsafe.GetPointer<CameraController>(entity)->Recenter(stage, spawnpoint);

                    f.Unsafe.GetPointer<Freezable>(entity)->FrozenCubeEntity = EntityRef.None;
                    mario->FacingRight = true;
                    mario->WallslideEndFrames = 0;
                    mario->WalljumpFrames = 0;
                    mario->UsedPropellerThisJump = false;
                    mario->PropellerLaunchFrames = 0;
                    mario->PropellerSpinFrames = 0;
                    mario->JumpState = JumpState.None;
                    mario->PreviousPowerupState = mario->CurrentPowerupState = PowerupState.NoPowerup;
                    //animationController.DisableAllModels();
                    mario->DamageInvincibilityFrames = 0;
                    mario->InvincibilityFrames = 0;
                    mario->MegaMushroomFrames = 0;
                    mario->MegaMushroomStartFrames = 0;
                    mario->MegaMushroomEndFrames = 0;
                    // f.ResolveHashSet(WaterColliders).Clear();
                    mario->SwimForceJumpTimer = 0;

                    physicsObject->IsFrozen = true;
                    f.Unsafe.GetPointer<Interactable>(entity)->ColliderDisabled = false;

                    f.Events.MarioPlayerPreRespawned(f, entity);
                } else if (mario->ActionTimer == 78) {
                    mario->ActionState++;
                    mario->ActionTimer = 0;
                    return;
                }
                break;
            }
            case 1: {
                mario->DamageInvincibilityFrames = 120;
                mario->CoyoteTimeFrames = 0;
                mario->SwimForceJumpTimer = 0;
                physicsObject->IsFrozen = false;
                physicsObject->DisableCollision = false;
                physicsObject->Velocity = FPVector2.Zero;
                mario->SetGroundAction(physicsObject, f);
                mario->SetAirAction(physicsObject, f);

                f.Events.MarioPlayerRespawned(f, entity);
                return;
            }
            }
            QuantumUtils.Increment(ref mario->ActionTimer);
        }

        public void HandleActions(Frame f, ref Filter filter, MarioPlayerPhysicsInfo physics, VersusStageData stage) {
            using var profilerScope = HostProfiler.Start("MarioPlayerSystem.HandleActions");
            var mario = filter.MarioPlayer;
            switch (mario->Action) {
            case PlayerAction.Idle:             ActionIdleWalking(f, ref filter, physics, stage); break;
            case PlayerAction.Walk:             ActionIdleWalking(f, ref filter, physics, stage); break;
            case PlayerAction.Skidding:         break; // needs code
            case PlayerAction.Crouch:           ActionCrouching(f, ref filter, physics, stage); break; // velocity is off, needs crouch v-speed
            case PlayerAction.CrouchAir:        ActionCrouchAir(f, ref filter, physics, stage); break;
            case PlayerAction.Sliding:          ActionSliding(f, ref filter, physics, stage); break;
            case PlayerAction.Bounce:           ActionBounce(f, ref filter, physics, stage); break;
            case PlayerAction.SingleJump:       ActionSingleDoubleJump(f, ref filter, physics, stage); break;
            case PlayerAction.DoubleJump:       ActionSingleDoubleJump(f, ref filter, physics, stage); break;
            case PlayerAction.TripleJump:       ActionTripleJump(f, ref filter, physics, stage); break;
            case PlayerAction.Freefall:         ActionFreefall(f, ref filter, physics, stage); break;
            case PlayerAction.HoldIdle:         ActionIdleWalking(f, ref filter, physics, stage); break; // hold action needs fixing
            case PlayerAction.HoldWalk:         ActionIdleWalking(f, ref filter, physics, stage); break; // hold action needs fixing
            case PlayerAction.HoldJump:         ActionHoldJump(f, ref filter, physics, stage); break; // hold action needs fixing
            case PlayerAction.HoldFall:         ActionHoldFall(f, ref filter, physics, stage); break; // hold action needs fixing
            case PlayerAction.WallSlide:        ActionWallSlide(f, ref filter, physics, stage); break;
            case PlayerAction.Wallkick:         ActionWallKick(f, ref filter, physics, stage); break;
            case PlayerAction.GroundPound:      ActionGroundPound(f, ref filter, physics, stage); break;
            case PlayerAction.SoftKnockback:    ActionKnockback(f, ref filter, physics, stage); break;
            case PlayerAction.NormalKnockback:  ActionKnockback(f, ref filter, physics, stage); break;
            case PlayerAction.HardKnockback:    ActionKnockback(f, ref filter, physics, stage); break;
            case PlayerAction.SpinBlockSpin:    ActionSpinBlockSpin(f, ref filter, physics, stage); break;
            case PlayerAction.SpinBlockDrill:   ActionSpinBlockDrill(f, ref filter, physics, stage); break;
            case PlayerAction.BlueShellCrouch:  ActionCrouching(f, ref filter, physics, stage); break;
            case PlayerAction.BlueShellSliding: ActionBlueShellSliding(f, ref filter, physics, stage); break;
            case PlayerAction.BlueShellJump:    ActionBlueShellJump(f, ref filter, physics, stage); break;
            case PlayerAction.PropellerSpin:    ActionPropellerSpin(f, ref filter, physics, stage); break;
            case PlayerAction.PropellerDrill:   ActionPropellerDrill(f, ref filter, physics, stage); break;
            case PlayerAction.MegaMushroom:     ActionMegaMushroom(f, ref filter, physics, stage); break; // no code
            case PlayerAction.PowerupShoot:     ActionPowerupShoot(f, ref filter, physics, stage); break;
            case PlayerAction.Pushing:          ActionIdleWalking(f, ref filter, physics, stage); break;
            case PlayerAction.Death:            ActionDeath(f, ref filter, physics, stage); break;
            case PlayerAction.LavaDeath:        ActionDeath(f, ref filter, physics, stage); break;
            case PlayerAction.Respawning:       ActionRespawning(f, ref filter, physics, stage); break;
            case PlayerAction.EnteringPipe:     break;
            }
        }
        #endregion

        /*
         * These are a set of methods that help actions function.
         */
        #region Action Helpers

        private enum HelperState {
            NotActive,
            Mismatch,
            MatchFail,
            Success
        }

        private bool JumpHandler(Frame f, ref Filter filter, MarioPlayerPhysicsInfo physics, PlayerAction? targetAction = null, int actionArg = 0, bool skipJumpCheck = false, bool checkJumpDown = false) {
            var mario = filter.MarioPlayer;
            var physicsObject = filter.PhysicsObject;
            var inputs = filter.Inputs;

            bool doJump =
                (mario->JumpBufferFrames > 0 && (physicsObject->IsTouchingGround || mario->CoyoteTimeFrames > 0))
                || (!physicsObject->IsUnderwater && mario->SwimForceJumpTimer == 10);

            if (!doJump &! skipJumpCheck) return false;
            bool topSpeed = FPMath.Abs(physicsObject->Velocity.X) >= (physics.WalkMaxVelocity[physics.RunSpeedStage] - FP._0_10);
            bool canSpecialJump = topSpeed && !inputs.Down.IsDown && (doJump || skipJumpCheck) && (!checkJumpDown || inputs.Jump.IsDown) && mario->JumpState is not JumpState.None and not JumpState.TripleJump && (f.Number - mario->LandedFrame < 12) && !mario->HeldEntity.IsValid && (physicsObject->Velocity.X < 0 != mario->FacingRight) /* && !Runner.GetPhysicsScene2D().Raycast(body.Position + new Vector2(0, 0.1f), Vector2.up, 1f, Layers.MaskSolidGround) */;

            mario->WallslideEndFrames = 0;
            mario->GroundpoundStartFrames = 0;
            mario->JumpBufferFrames = 0;
            physicsObject->WasTouchingGround = false;
            physicsObject->IsTouchingGround = false;

            // Disable koyote time
            mario->CoyoteTimeFrames = 0;

            PowerupState effectiveState = mario->CurrentPowerupState;
            if (effectiveState == PowerupState.MegaMushroom && mario->Action == PlayerAction.Bounce) {
                effectiveState = PowerupState.NoPowerup;
            }

            // TODO: fix magic
            FP alpha = FPMath.Clamp01(FPMath.Abs(physicsObject->Velocity.X) - physics.WalkMaxVelocity[1] + (physics.WalkMaxVelocity[1] * FP._0_50));
            FP newY = effectiveState switch {
                PowerupState.MegaMushroom => physics.JumpMegaVelocity + FPMath.Lerp(0, physics.JumpMegaSpeedBonusVelocity, alpha),
                PowerupState.MiniMushroom => physics.JumpMiniVelocity + FPMath.Lerp(0, physics.JumpMiniSpeedBonusVelocity, alpha),
                _ => physics.JumpVelocity + FPMath.Lerp(0, physics.JumpSpeedBonusVelocity, alpha),
            };
            if (FPMath.Sign(physicsObject->Velocity.X) != 0 && FPMath.Sign(physicsObject->Velocity.X) != FPMath.Sign(physicsObject->FloorAngle)) {
                // TODO: what.
                newY += FPMath.Abs(physicsObject->FloorAngle) * FP._0_01 * alpha;
            }

            if (targetAction == null) {
                if (canSpecialJump && mario->JumpState == JumpState.SingleJump) {
                    // Double jump
                    mario->JumpState = JumpState.DoubleJump;
                } else if (canSpecialJump && mario->JumpState == JumpState.DoubleJump) {
                    // Triple Jump
                    mario->JumpState = JumpState.TripleJump;
                    newY += physics.JumpTripleBonusVelocity;
                } else {
                    // Normal jump
                    mario->JumpState = JumpState.SingleJump;
                }
                mario->SetPlayerAction(ConvertJumpState(mario->JumpState), f, actionArg);
            } else {
                mario->SetPlayerAction(targetAction.GetValueOrDefault(), f, actionArg);
            }
            physicsObject->Velocity.Y = newY;

            if (physicsObject->IsWaterSolid) {
                // Check if we jumped off the water
                var contacts = f.ResolveList(physicsObject->Contacts);
                foreach (var contact in contacts) {
                    if (f.Has<Liquid>(contact.Entity)) {
                        f.Events.LiquidSplashed(f, contact.Entity, filter.Entity, -1, filter.Transform->Position, true);
                        break;
                    }
                }
            }
            return true;
        }

        private bool EnableSpinner(Frame f, ref Filter filter, MarioPlayerPhysicsInfo physics) {
            var mario = filter.MarioPlayer;
            var physicsObject = filter.PhysicsObject;
            var inputs = filter.Inputs;

            // I don't think this uses the jump buffer
            if (f.Unsafe.TryGetPointer(mario->CurrentSpinner, out Spinner* spinner) && spinner->ArmPosition <= FP._0_75
                && inputs.Jump.WasPressed) {
                // Jump of spinner
                physicsObject->IsTouchingGround = false;
                physicsObject->Velocity.Y = physics.SpinnerLaunchVelocity;
                spinner->PlatformWaitFrames = 6;

                mario->SetPlayerAction(PlayerAction.SpinBlockSpin, f);

                /*
                var contacts = f.ResolveList(physicsObject->Contacts);
                for (int i = contacts.Count - 1; i >= 0; i--) {
                    if (contacts[i].Entity == mario->CurrentSpinner) {
                        contacts.RemoveAtUnordered(i);
                    }
                }
                */

                // Disable koyote time
                mario->CoyoteTimeFrames = 0;

                f.Events.MarioPlayerUsedSpinner(f, filter.Entity, mario->CurrentSpinner);

                mario->CurrentSpinner = EntityRef.None;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Add this to an Action to allow the player to shoot something while in that Action.
        /// </summary>
        /// <returns><b>HelperState.NotActive</b> if the button is not pressed<br></br>
        /// <b>HelperState.Mismatch</b> if the powerup state does not match any of the shooting powerups<br></br>
        /// <b>HelperState.MatchFail</b> if the powerup state does match a shooting powerup, but cannot shoot a projectile<br></br>
        /// <b>HelperState.Success</b> if we could shoot a projectile.<br></br>
        /// </returns>
        private HelperState EnableShootingPowerups(Frame f, ref Filter filter, MarioPlayerPhysicsInfo physics, PowerupState state) {
            var inputs = filter.Inputs;
            if (!inputs.PowerupAction.WasPressed) return HelperState.NotActive;

            var mario = filter.MarioPlayer;
            switch (state) {
            case PowerupState.IceFlower:
            case PowerupState.FireFlower:
            case PowerupState.HammerSuit: {
                if (mario->ProjectileDelayFrames != 0) return HelperState.MatchFail;
                byte activeProjectiles = mario->CurrentProjectiles;
                if (activeProjectiles >= physics.MaxProjecitles) {
                    return HelperState.MatchFail;
                }

                if (activeProjectiles < 2) {
                    // Always allow if < 2
                    mario->CurrentVolley = (byte) (activeProjectiles + 1);
                } else if (mario->CurrentVolley < physics.ProjectileVolleySize) {
                    // Allow in this volley
                    mario->CurrentVolley++;
                } else {
                    // No more left in volley
                    return HelperState.MatchFail;
                }

                mario->SetPlayerAction(PlayerAction.PowerupShoot, f, (int) state);
                return HelperState.Success;
            }
            }
            return HelperState.Mismatch;
        }

        /// <summary>
        /// Add this to an Action to allow the player to use the propeller
        /// </summary>
        /// <returns><b>HelperState.NotActive</b> if the button is not pressed<br></br>
        /// <b>HelperState.Mismatch</b> if the powerup state does not match any of the shooting powerups<br></br>
        /// <b>HelperState.MatchFail</b> if the powerup state does match a shooting powerup, but cannot shoot a projectile<br></br>
        /// <b>HelperState.Success</b> if we could shoot a projectile.<br></br>
        /// </returns>
        private HelperState EnablePropellerPowerup(Frame f, ref Filter filter, MarioPlayerPhysicsInfo physics, PowerupState state, VersusStageData stage) {
            var inputs = filter.Inputs;
            if (!inputs.PowerupAction.WasPressed) return HelperState.NotActive;
            if (state != PowerupState.PropellerMushroom) return HelperState.Mismatch;
            var mario = filter.MarioPlayer;
            var physicsObject = filter.PhysicsObject;
            if (mario->UsedPropellerThisJump || physicsObject->IsUnderwater || mario->WalljumpFrames > 0) {
                return HelperState.MatchFail;
            }

            mario->PropellerLaunchFrames = physics.PropellerLaunchFrames;
            mario->UsedPropellerThisJump = true;
            mario->SetPlayerAction(PlayerAction.PropellerSpin, f);

            mario->JumpState = JumpState.None;
            mario->CoyoteTimeFrames = 0;

            // Fix sticky ground
            physicsObject->WasTouchingGround = false;
            physicsObject->IsTouchingGround = false;
            physicsObject->HoverFrames = 0;
            PhysicsObjectSystem.MoveVertically((FrameThreadSafe) f, FPVector2.Up * FP._0_05 * f.UpdateRate, filter.Entity, stage);

            f.Events.MarioPlayerUsedPropeller(f, filter.Entity);
            return HelperState.Success;
        }

        /// <summary>
        /// Attach this to an Action to allow the player to slide on walls
        /// </summary>
        /// <param name="f"></param>
        /// <param name="filter"></param>
        /// <param name="physics"></param>
        /// <param name="inputs"></param>
        /// <param name="wallslide">if true then allow the player to wallslide.</param>
        /// <returns>if the wallslide was possible or not.</returns>
        private bool EnableWallKick(Frame f, ref Filter filter, MarioPlayerPhysicsInfo physics, bool wallslide = true) {
            var mario = filter.MarioPlayer;
            var physicsObject = filter.PhysicsObject;
            var inputs = filter.Inputs;

            FPVector2 currentWallDirection;
            if (physicsObject->IsTouchingLeftWall && inputs.Left.IsDown) {
                currentWallDirection = FPVector2.Left;
            } else if (physicsObject->IsTouchingRightWall && inputs.Right.IsDown) {
                currentWallDirection = FPVector2.Right;
            } else {
                return false;
            }

            // Walljump starting check
            bool canWallslide = physicsObject->Velocity.Y < -FP._0_10 && !physicsObject->IsTouchingGround && mario->CurrentPowerupState != PowerupState.MegaMushroom;
            if (!canWallslide) {
                return false;
            }

            // Check 4: already handled
            // Check 5.2: already handled

            // Check 8
            if (!((currentWallDirection == FPVector2.Right && mario->FacingRight) || (currentWallDirection == FPVector2.Left && !mario->FacingRight))) {
                return false;
            }

            // Start wallslide
            bool isLeft = currentWallDirection == FPVector2.Left;

            if (wallslide) {
                mario->SetPlayerAction(PlayerAction.WallSlide, f, isLeft ? 0 : 1);
                return true;
            } else if (mario->JumpBufferFrames > 0 && mario->WalljumpFrames == 0) {
                mario->SetPlayerAction(PlayerAction.Wallkick, f);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Attach this to an Action to allow the player to ground pound.
        /// </summary>
        /// <returns>if the ground pound was successful or not.</returns>
        private bool EnableGroundpound(Frame f, ref Filter filter, MarioPlayerPhysicsInfo physics, VersusStageData stage, bool ignoreLeftRight = false, bool ignorePrevDown = false) {
            using var profilerScope = HostProfiler.Start("MarioPlayerSystem.TryStartGroundpound");
            var mario = filter.MarioPlayer;
            var physicsObject = filter.PhysicsObject;
            var inputs = filter.Inputs;

            if (!ignorePrevDown) {
                mario->PreActionInput.Down = inputs.Down;
                if (mario->PreActionInput.Down.IsDown) {
                    return false;
                }
            }

            if (inputs.Down.WasPressed && mario->GroundpoundCooldownFrames == 0) {
                // 4 frame delay
                mario->GroundpoundCooldownFrames = 5;
            }

            if (physicsObject->IsTouchingGround || mario->GroundpoundCooldownFrames > 0 || physicsObject->IsUnderwater
                || f.Exists(mario->CurrentPipe)) {
                return false;
            }

            if (!(inputs.Down.IsDown)) {
                return false;
            }

            /// * intentional: remove left/right requirement when groundpounding
            if (ignoreLeftRight && (inputs.Left.IsDown || inputs.Right.IsDown)) {
                return false;
            }
            // */
            // Start groundpound
            // Check if high enough above ground
            var transform = filter.Transform;
            if (PhysicsObjectSystem.Raycast((FrameThreadSafe) f, stage, transform->Position, FPVector2.Down, FP._0_50, out _)) {
                return false;
            }

            mario->SetPlayerAction(PlayerAction.GroundPound, f);
            mario->JumpState = JumpState.None;
            physicsObject->Velocity = physics.GroundpoundStartVelocity;
            mario->GroundpoundStartFrames = mario->CurrentPowerupState == PowerupState.MegaMushroom ? physics.GroundpoundStartMegaFrames : physics.GroundpoundStartFrames;

            f.Events.MarioPlayerGroundpoundStarted(f, filter.Entity);
            return true;
        }

        private void HandleGroundpoundStartAnimation(ref Filter filter, MarioPlayerPhysicsInfo physics) {
            using var profilerScope = HostProfiler.Start("MarioPlayerSystem.HandleGroundpoundStartAnimation");
            var mario = filter.MarioPlayer;
            var physicsObject = filter.PhysicsObject;

            if (mario->GroundpoundStartFrames == 0) {
                return;
            }

            if (QuantumUtils.Decrement(ref mario->GroundpoundStartFrames)) {
                mario->AddActionFlags(ActionFlags.StrongAction | ActionFlags.BreaksBlocks);
                mario->SetStompEvents(PlayerAction.HardKnockback, 3);
                if (mario->CurrentPowerupState == PowerupState.BlueShell) {
                    mario->AddActionFlags(ActionFlags.IsShelled);
                }
            }

            physicsObject->Velocity = mario->GroundpoundStartFrames switch {
                0 => FPVector2.Up * physics.TerminalVelocityGroundpound,
                >= 4 => physics.GroundpoundStartVelocity,
                _ => FPVector2.Zero
            };
        }

        private bool HandleGroundpoundBlockCollision(Frame f, ref Filter filter, MarioPlayerPhysicsInfo physics, VersusStageData stage, bool isGroundpound) {
            using var profilerScope = HostProfiler.Start("MarioPlayerSystem.HandleGroundpoundBlockCollision");
            var mario = filter.MarioPlayer;
            var physicsObject = filter.PhysicsObject;

            if (!(physicsObject->IsTouchingGround)) {
                mario->ActionState = 0;
                return false;
            }

            // drilling also uses this
            if (isGroundpound && mario->ActionState == 0) {
                f.Events.MarioPlayerGroundpounded(f, filter.Entity);
                mario->ActionState++;
            }

            bool interactedAny = false;
            bool continueGroundpound = true;
            bool? playBumpSound = null;
            QList<PhysicsContact> contacts = f.ResolveList(physicsObject->Contacts);
            foreach (var contact in contacts) {
                if (FPVector2.Dot(contact.Normal, FPVector2.Up) < PhysicsObjectSystem.GroundMaxAngle) {
                    continue;
                }

                // Floor tiles.
                if (f.Exists(contact.Entity)) {
                    // Manual Fix: allow ice block groundpound continues
                    bool ice = f.Has<IceBlock>(contact.Entity);
                    continueGroundpound &= ice;
                    interactedAny |= ice;
                } else {
                    var tileInstance = stage.GetTileRelative(f, contact.TileX, contact.TileY);
                    StageTile tile = f.FindAsset(tileInstance.Tile);
                    if (tile is IInteractableTile it) {
                        continueGroundpound &= it.Interact(f, filter.Entity, InteractionDirection.Down,
                            new Vector2Int(contact.TileX, contact.TileY), tileInstance, out bool tempPlayBumpSound);
                        interactedAny = true;

                        playBumpSound &= (playBumpSound ?? true) & tempPlayBumpSound;
                    }
                }
            }

            if (playBumpSound ?? false) {
                f.Events.PlayBumpSound(f, filter.Entity);
            }

            continueGroundpound &= interactedAny;
            if (!continueGroundpound && isGroundpound) {
                // remove these flags
                mario->ClearActionFlags(ActionFlags.BreaksBlocks | ActionFlags.StrongAction);
                mario->ClearStompEvents();
                return false;
            }

            if (physicsObject->IsOnSlideableGround && !mario->HasActionFlags(ActionFlags.IsShelled) && FPMath.Abs(physicsObject->FloorAngle) >= physics.SlideMinimumAngle) {
                mario->SetPlayerAction(PlayerAction.Sliding, f);
                physicsObject->Velocity.X = FPMath.Sign(physicsObject->FloorAngle) * physics.SlideMaxVelocity;
                return false;
            }

            // confusing code
            /*if (mario->IsDrilling) {
                mario->IsSpinnerFlying &= continueGroundpound;
                mario->IsPropellerFlying &= continueGroundpound;
                mario->IsDrilling = continueGroundpound;
                if (continueGroundpound) {
                    physicsObject->IsTouchingGround = false;
                }
            }*/
            return continueGroundpound;
        }

        public void BlueShellPhysics(Frame f, ref Filter filter, MarioPlayerPhysicsInfo physics, VersusStageData stage) {
            using var profilerScope = HostProfiler.Start("MarioPlayerSystem.HandleBlueShell");
            var mario = filter.MarioPlayer;
            var physicsObject = filter.PhysicsObject;
            var inputs = filter.Inputs;
            var transform = filter.Transform;

            if (physicsObject->IsTouchingLeftWall || physicsObject->IsTouchingRightWall) {
                QList<PhysicsContact> contacts = f.ResolveList(physicsObject->Contacts);
                FPVector2? maxVector = null;
                foreach (var contact in contacts) {
                    FP dot = FPVector2.Dot(contact.Normal, FPVector2.Right);
                    if (FPMath.Abs(dot) < FP._0_75) {
                        continue;
                    }

                    // Wall tiles.
                    var tileInstance = stage.GetTileRelative(f, contact.TileX, contact.TileY);
                    StageTile tile = f.FindAsset(tileInstance.Tile);
                    if (tile is IInteractableTile it) {
                        it.Interact(f, filter.Entity, dot > 0 ? InteractionDirection.Right : InteractionDirection.Left,
                            new Vector2Int(contact.TileX, contact.TileY), tileInstance, out bool tempPlayBumpSound);
                    }

                    FPVector2 vector = contact.Normal * (contact.Distance + FP._0_05);
                    if (maxVector == null || maxVector.Value.SqrMagnitude < vector.SqrMagnitude) {
                        maxVector = vector;
                    }
                }

                if (maxVector.HasValue) {
                    // Bounce, needed for block skipping.
                    transform->Position += maxVector.Value;
                }

                mario->FacingRight = physicsObject->IsTouchingLeftWall;
                f.Events.PlayBumpSound(f, filter.Entity);
            }

            physicsObject->Velocity.X = physics.WalkMaxVelocity[physics.RunSpeedStage] * physics.WalkBlueShellMultiplier * (mario->FacingRight ? 1 : -1) * (1 - (((FP) mario->ShellSlowdownFrames) / 60));
        }

        private bool SlidingPhysics(Frame f, ref Filter filter, MarioPlayerPhysicsInfo physics) {
            using var profilerScope = HostProfiler.Start("MarioPlayerSystem.HandleSliding");
            var mario = filter.MarioPlayer;
            var physicsObject = filter.PhysicsObject;
            var inputs = filter.Inputs;
            bool validFloorAngle = FPMath.Abs(physicsObject->FloorAngle) >= physics.SlideMinimumAngle;

            if (mario->CurrentPowerupState == PowerupState.MiniMushroom && physicsObject->IsTouchingGround) {
                var contacts = f.ResolveList(physicsObject->Contacts);
                foreach (var contact in contacts) {
                    if (f.Has<Liquid>(contact.Entity)) {
                        return false;
                    }
                }
            }

            if (physicsObject->IsTouchingGround && validFloorAngle) {
                // Slide down slopes
                FP runningMaxSpeed = physics.WalkMaxVelocity[physics.RunSpeedStage];
                FP angleDeg = physicsObject->FloorAngle * FP.Deg2Rad;

                bool uphill = FPMath.Sign(physicsObject->FloorAngle) != FPMath.Sign(physicsObject->Velocity.X);
                FP speed = f.DeltaTime * 5 * (uphill ? FPMath.Clamp01(1 - (FPMath.Abs(physicsObject->Velocity.X) / runningMaxSpeed)) : 4);

                physicsObject->Velocity.X = FPMath.Clamp(physicsObject->Velocity.X + (FPMath.Sin(angleDeg) * speed), -(runningMaxSpeed * FP._1_25), runningMaxSpeed * FP._1_25);
                //FP newY = (uphill ? 0 : -FP._1_50) * FPMath.Abs(newX);
                //= new FPVector2(newX, newY);
            }

            bool stationary = FPMath.Abs(physicsObject->Velocity.X) < FP._0_01 && physicsObject->IsTouchingGround;
            if (inputs.Up.IsDown
                || ((inputs.Left.IsDown ^ inputs.Right.IsDown) && !inputs.Down.IsDown)
                || (/*physicsObject->IsOnSlideableGround && FPMath.Abs(physicsObject->FloorAngle) < physics.SlideMinimumAngle && */physicsObject->IsTouchingGround && stationary && !inputs.Down.IsDown)
                || (mario->FacingRight && physicsObject->IsTouchingRightWall)
                || (!mario->FacingRight && physicsObject->IsTouchingLeftWall)) {

                // End sliding
                f.Events.MarioPlayerStoppedSliding(f, filter.Entity, stationary);
                return false;
            }
            return true;
        }
        #endregion

        public void HandleWalkingRunning(Frame f, ref Filter filter, MarioPlayerPhysicsInfo physics) {
            using var profilerScope = HostProfiler.Start("MarioPlayerSystem.HandleWalkingRunning");
            var mario = filter.MarioPlayer;
            var physicsObject = filter.PhysicsObject;
            var inputs = filter.Inputs;

            if (!QuantumUtils.Decrement(ref mario->WalljumpFrames)) {
                return;
            }

            if (mario->GroundpoundStandFrames > 0) {
                if (!physicsObject->IsTouchingGround) {
                    mario->GroundpoundStandFrames = 0;
                } else {
                    mario->GroundpoundStandFrames--;
                    return;
                }
            }

            if (mario->HasActionFlags(ActionFlags.IrregularVelocity) || f.Exists(mario->CurrentPipe) || mario->JumpLandingFrames > 0
                || !(mario->WalljumpFrames <= 0 || physicsObject->IsTouchingGround || physicsObject->Velocity.Y < 0)) {
                return;
            }

            bool swimming = physicsObject->IsUnderwater;

            bool run = (inputs.Sprint.IsDown || mario->CurrentPowerupState == PowerupState.MegaMushroom || mario->Action == PlayerAction.PropellerSpin) & mario->Action != PlayerAction.SpinBlockSpin;
            int maxStage;
            if (swimming) {
                if (mario->CurrentPowerupState == PowerupState.BlueShell) {
                    maxStage = physics.SwimShellMaxVelocity.Length - 1;
                } else {
                    maxStage = physics.SwimMaxVelocity.Length - 1;
                }
            } else if (mario->IsStarmanInvincible && run && physicsObject->IsTouchingGround) {
                maxStage = physics.StarSpeedStage;
            } else if (run) {
                maxStage = physics.RunSpeedStage;
            } else {
                maxStage = physics.WalkSpeedStage;
            }


            FP[] maxArray = physics.WalkMaxVelocity;
            if (swimming) {
                if (physicsObject->IsTouchingGround) {
                    maxArray = mario->CurrentPowerupState == PowerupState.BlueShell ? physics.SwimWalkShellMaxVelocity : physics.SwimWalkMaxVelocity;
                } else {
                    maxArray = mario->CurrentPowerupState == PowerupState.BlueShell ? physics.SwimShellMaxVelocity : physics.SwimMaxVelocity;
                }
            }
            int stage = mario->GetSpeedStage(physicsObject, physics);
            FP acc;
            if (swimming) {
                if (physicsObject->IsTouchingGround) {
                    acc = mario->CurrentPowerupState == PowerupState.BlueShell ? physics.SwimWalkShellAcceleration[stage] : physics.SwimWalkAcceleration[stage];
                } else {
                    acc = mario->CurrentPowerupState == PowerupState.BlueShell ? physics.SwimShellAcceleration[stage] : physics.SwimAcceleration[stage];
                }
            } else if (physicsObject->IsOnSlipperyGround) {
                acc = physics.WalkIceAcceleration[stage];
            } else if (mario->CurrentPowerupState == PowerupState.MegaMushroom) {
                acc = physics.WalkMegaAcceleration[stage];
            } else {
                acc = physics.WalkAcceleration[stage];
            }

            FP xVel = physicsObject->Velocity.X;
            FP xVelAbs = FPMath.Abs(xVel);
            int sign = FPMath.SignInt(xVel);
            bool uphill = FPMath.Abs(physicsObject->FloorAngle) > physics.SlideMinimumAngle && FPMath.SignInt(physicsObject->FloorAngle) != sign;

            if (!physicsObject->IsTouchingGround) {
                mario->FastTurnaroundFrames = 0;
            }

            if (mario->FastTurnaroundFrames > 0) {
                physicsObject->Velocity.X = 0;
                if (QuantumUtils.Decrement(ref mario->FastTurnaroundFrames)) {
                    mario->IsTurnaround = true;
                }
            } else if (mario->IsTurnaround && !physicsObject->IsOnSlipperyGround) {
                // Can't fast turnaround on ice.
                mario->IsTurnaround = physicsObject->IsTouchingGround && mario->Action != PlayerAction.Crouch && xVelAbs < physics.WalkMaxVelocity[1] && !physicsObject->IsTouchingLeftWall && !physicsObject->IsTouchingRightWall;
                if (mario->IsTurnaround) mario->SetPlayerAction(PlayerAction.Skidding, f);

                physicsObject->Velocity.X += (physics.FastTurnaroundAcceleration * (mario->FacingRight ? -1 : 1) * f.DeltaTime);
            } else if ((inputs.Left ^ inputs.Right)
                       /*&& (mario->Action != PlayerAction.Crouch || (mario->Action == PlayerAction.BlueShellCrouch && !physicsObject->IsTouchingGround))
                       && (mario->Action < PlayerAction.SoftKnockback && mario->Action > PlayerAction.HardKnockback)
                       && mario->Action != PlayerAction.Sliding*/) {

                // We can walk here
                int direction = inputs.Left ? -1 : 1;
                if (mario->Action == PlayerAction.Skidding) {
                    direction = -sign;
                }

                bool reverse = physicsObject->Velocity.X != 0 && (direction != sign);
                // Check that we're not going above our limit
                FP max = maxArray[maxStage];
                if (!swimming) {
                    max += CalculateSlopeMaxSpeedOffset(FPMath.Abs(physicsObject->FloorAngle) * (uphill ? 1 : -1));
                }
                FP maxAcceleration = FPMath.Abs(max - xVelAbs) * f.UpdateRate;
                acc = FPMath.Clamp(acc, -maxAcceleration, maxAcceleration);
                if (xVelAbs > max) {
                    /*
                    // This kills water hyperspeed.
                    // technically, it's accurate. but it's fun... soo...
                    if (swimming) {
                        acc = physics.WalkAcceleration[^1];
                    }
                    */
                    acc = -acc;
                }

                if (reverse) {
                    mario->IsTurnaround = false;
                    if (physicsObject->IsTouchingGround) {
                        if (!swimming && xVelAbs >= physics.SkiddingMinimumVelocity && !mario->HeldEntity.IsValid && mario->CurrentPowerupState != PowerupState.MegaMushroom) {
                            //mario->SetPlayerAction(PlayerAction.Skidding);
                            mario->FacingRight = sign == 1;
                        }

                        if (mario->Action == PlayerAction.Skidding) {
                            if (physicsObject->IsOnSlipperyGround) {
                                acc = physics.SkiddingIceDeceleration;
                            } else if (xVelAbs > maxArray[physics.RunSpeedStage]) {
                                acc = physics.SkiddingStarmanDeceleration;
                            } else {
                                acc = physics.SkiddingDeceleration;
                            }

                            mario->SlowTurnaroundFrames = 0;
                        } else {
                            if (physicsObject->IsOnSlipperyGround) {
                                acc = physics.SlowTurnaroundIceAcceleration;
                            } else {
                                mario->SlowTurnaroundFrames = (byte) FPMath.Clamp(mario->SlowTurnaroundFrames + 1, 0,
                                    physics.SlowTurnaroundAcceleration.Length - 1);
                                acc = mario->CurrentPowerupState == PowerupState.MegaMushroom
                                    ? physics.SlowTurnaroundMegaAcceleration[mario->SlowTurnaroundFrames]
                                    : physics.SlowTurnaroundAcceleration[mario->SlowTurnaroundFrames];
                            }
                        }
                    } else {
                        // TODO: change 0.85 to a constant?
                        acc = physics.WalkAcceleration[0] * Constants._0_85;
                    }
                } else {
                    mario->SlowTurnaroundFrames = 0;
                    //mario->IsSkidding &= !mario->IsTurnaround;
                }

                FP newX = xVel + (acc * f.DeltaTime * direction);

                if ((xVel < max && newX > max) || (xVel > -max && newX < -max)) {
                    newX = FPMath.Clamp(newX, -max, max);
                }

                if (mario->Action == PlayerAction.Skidding && !mario->IsTurnaround && (FPMath.Sign(newX) != sign || xVelAbs < FP._0_05)) {
                    // Turnaround
                    mario->FastTurnaroundFrames = 10;
                    newX = 0;
                }

                physicsObject->Velocity.X = newX;

            } else if (physicsObject->IsTouchingGround || swimming) {
                // Not holding anything, sliding, or holding both directions. decelerate
                mario->IsTurnaround = false;

                FP angle = FPMath.Abs(physicsObject->FloorAngle);
                if (mario->Action is PlayerAction.SoftKnockback or PlayerAction.NormalKnockback or PlayerAction.HardKnockback) {
                    acc = -physics.KnockbackDeceleration;
                } else if (swimming) {
                    if (mario->Action is PlayerAction.Crouch or PlayerAction.BlueShellCrouch) {
                        acc = -physics.WalkAcceleration[0];
                    } else {
                        acc = -physics.SwimDeceleration;
                    }
                } else if (mario->Action == PlayerAction.Sliding) {
                    if (angle > physics.SlideMinimumAngle) {
                        // Uphill / downhill
                        acc = (angle > 30 ? physics.SlideFastAcceleration : physics.SlideSlowAcceleration) * (uphill ? -1 : 1);
                    } else {
                        // Flat ground
                        acc = -physics.WalkAcceleration[0];
                    }
                } else if (physicsObject->IsOnSlipperyGround) {
                    acc = -physics.WalkButtonReleaseIceDeceleration[stage];
                } else {
                    acc = -physics.WalkButtonReleaseDeceleration;
                }

                FP newX = xVel + acc * f.DeltaTime * sign;
                FP target = (angle > 30 && physicsObject->IsOnSlideableGround) ? FPMath.Sign(physicsObject->FloorAngle) * physics.WalkMaxVelocity[0] : 0;
                if ((sign == -1) ^ (newX <= target)) {
                    newX = target;
                }

                if (mario->Action == PlayerAction.Sliding) {
                    newX = FPMath.Clamp(newX, -physics.SlideMaxVelocity, physics.SlideMaxVelocity);
                }

                physicsObject->Velocity.X = newX;

                if (newX != 0) {
                    mario->FacingRight = newX > 0;
                }
            }

            //bool wasInShell = mario->IsInShell;

            /*if (!wasInShell && mario->IsInShell) {
                f.Events.MarioPlayerCrouched(f, filter.Entity);
            }*/
        }

        private static FP CalculateSlopeMaxSpeedOffset(FP floorAngle) {
            // TODO remove magic constant
            return Constants.WeirdSlopeConstant * floorAngle;
        }

        private static PlayerAction ConvertJumpState(JumpState state) {
            return state switch {
                JumpState.SingleJump => PlayerAction.SingleJump,
                JumpState.DoubleJump => PlayerAction.DoubleJump,
                JumpState.TripleJump => PlayerAction.TripleJump,
                _ => default
            };
        }

        private void HandleJumping(Frame f, ref Filter filter, MarioPlayerPhysicsInfo physics) {
            using var profilerScope = HostProfiler.Start("MarioPlayerSystem.HandleJumping");
            var mario = filter.MarioPlayer;
            var physicsObject = filter.PhysicsObject;
            var inputs = filter.Inputs;

            if (inputs.Jump.WasPressed) {
                // Jump buffer
                mario->JumpBufferFrames = physics.JumpBufferFrames;
            }

            if (physicsObject->IsTouchingGround) {
                // Coyote Time
                mario->CoyoteTimeFrames = physics.CoyoteTimeFrames;
            }

            if (!physicsObject->WasTouchingGround && physicsObject->IsTouchingGround) {
                // Landed Frame
                mario->LandedFrame = f.Number;
                if (mario->PreviousJumpState != JumpState.None && mario->PreviousJumpState == mario->JumpState) {
                    mario->JumpState = JumpState.None;
                }
                mario->PreviousJumpState = mario->JumpState;
            }

            bool doJump =
                (mario->JumpBufferFrames > 0 && (physicsObject->IsTouchingGround || mario->CoyoteTimeFrames > 0)) 
                || (!physicsObject->IsUnderwater && mario->SwimForceJumpTimer == 10);

            QuantumUtils.Decrement(ref mario->SwimForceJumpTimer);
            QuantumUtils.Decrement(ref mario->CoyoteTimeFrames);
            QuantumUtils.Decrement(ref mario->JumpBufferFrames);
        }

        public void HandleGravity(Frame f, ref Filter filter, MarioPlayerPhysicsInfo physics) {
            using var profilerScope = HostProfiler.Start("MarioPlayerSystem.HandleGravity");
            var mario = filter.MarioPlayer;
            var physicsObject = filter.PhysicsObject;
            var inputs = filter.Inputs;

            if (physicsObject->IsTouchingGround && !physicsObject->IsUnderwater) {
                physicsObject->Gravity = FPVector2.Up * physics.GravityAcceleration[0];
                return;
            }

            FP gravity;

            // Slow-rise check
            bool swimming = physicsObject->IsUnderwater;
            if (swimming && f.Exists(mario->HeldEntity)) {
                gravity = 0;
            } else if (!swimming && (mario->Action is PlayerAction.SpinBlockSpin or PlayerAction.PropellerSpin)) {
                gravity = physics.GravityFlyingAcceleration;
            } else if (!swimming && (mario->Action is PlayerAction.SpinBlockDrill or PlayerAction.PropellerDrill)) {
                gravity = physics.GravityAcceleration[^1];
            } else if ((mario->Action == PlayerAction.GroundPound && !swimming) || physicsObject->IsTouchingGround || mario->CoyoteTimeFrames > 0) {
                gravity = mario->GroundpoundStartFrames > 0 ? physics.GravityGroundpoundStart : physics.GravityAcceleration[^1];
            } else {
                int stage = mario->GetGravityStage(physicsObject, physics);
                bool mega = mario->CurrentPowerupState == PowerupState.MegaMushroom;
                bool mini = mario->CurrentPowerupState == PowerupState.MiniMushroom;

                FP[] accArr = swimming ? physics.GravitySwimmingAcceleration : (mega ? physics.GravityMegaAcceleration : (mini ? physics.GravityMiniAcceleration : physics.GravityAcceleration));
                FP acc = accArr[stage];
                if (stage == 0 && !(inputs.Jump.IsDown || swimming || (!swimming && mario->SwimForceJumpTimer > 0))) {
                    acc = accArr[^1];
                }

                gravity = acc;
            }

            physicsObject->Gravity = FPVector2.Up * gravity;
        }

        public void HandleTerminalVelocity(Frame f, ref Filter filter, MarioPlayerPhysicsInfo physics) {
            using var profilerScope = HostProfiler.Start("MarioPlayerSystem.HandleTerminalVelocity");

            var mario = filter.MarioPlayer;
            var physicsObject = filter.PhysicsObject;
            var inputs = filter.Inputs;

            FP maxWalkSpeed = physics.WalkMaxVelocity[physics.WalkSpeedStage];
            FP terminalVelocity;

            if (mario->Action is PlayerAction.Death or PlayerAction.LavaDeath) {
                bool isUnderwater = false;
                if (physicsObject->IsUnderwater) {
                    var contacts = f.ResolveHashSet(physicsObject->LiquidContacts);
                    foreach (var contact in contacts) {
                        if (f.Unsafe.GetPointer<Liquid>(contact)->LiquidType == LiquidType.Water) {
                            isUnderwater = true;
                            break;
                        }
                    }
                }
                if (isUnderwater) {
                    terminalVelocity = -Constants.OnePixelPerFrame;
                } else {
                    terminalVelocity = -8;
                }
            } else if (physicsObject->IsUnderwater) {
                terminalVelocity = inputs.Jump.IsDown ? physics.SwimTerminalVelocityButtonHeld : physics.SwimTerminalVelocity;
                physicsObject->Velocity.Y = FPMath.Min(physicsObject->Velocity.Y, physics.SwimMaxVerticalVelocity);
            } else if (mario->Action == PlayerAction.SpinBlockSpin) {
                 terminalVelocity = physics.TerminalVelocityFlying;
            } else if (mario->Action is PlayerAction.SpinBlockDrill or PlayerAction.PropellerDrill) {
                terminalVelocity = physics.TerminalVelocityDrilling;
                if (mario->Action == PlayerAction.PropellerDrill) {
                    physicsObject->Velocity.X = FPMath.Clamp(physicsObject->Velocity.X, -maxWalkSpeed * FP._0_25, maxWalkSpeed * FP._0_25);
                }
            } else if (mario->Action == PlayerAction.PropellerSpin) {
                FP remainingTime = mario->PropellerLaunchFrames * f.DeltaTime;
                // TODO remove magic number
                FP htv = maxWalkSpeed + (Constants._1_18 * (remainingTime * 2));
                terminalVelocity = mario->PropellerSpinFrames > 0 ? physics.TerminalVelocityPropellerSpin : physics.TerminalVelocityPropeller;
                physicsObject->Velocity.X = FPMath.Clamp(physicsObject->Velocity.X, -htv, htv);
            } else if (mario->Action == PlayerAction.WallSlide) {
                terminalVelocity = physics.TerminalVelocityWallslide;
            } else if (mario->Action == PlayerAction.GroundPound) {
                terminalVelocity = physics.TerminalVelocityGroundpound;
                physicsObject->Velocity.X = 0;
            } else {
                FP terminalVelocityModifier = mario->CurrentPowerupState switch {
                    PowerupState.MiniMushroom => physics.TerminalVelocityMiniMultiplier,
                    PowerupState.MegaMushroom => physics.TerminalVelocityMegaMultiplier,
                    _ => 1,
                };
                terminalVelocity = physics.TerminalVelocity * terminalVelocityModifier;
            }

            physicsObject->TerminalVelocity = terminalVelocity;
        }

        private static readonly FPVector2 WallslideLowerHeightOffset = new(0, FP._0_20);
        private void HandleWallslideStopChecks(ref Filter filter, FPVector2 wallDirection) {
            using var profilerScope = HostProfiler.Start("MarioPlayerSystem.HandleWallslideStopChecks");
            var mario = filter.MarioPlayer;
            var physicsObject = filter.PhysicsObject;
            var inputs = filter.Inputs;

            // TODO bool floorCheck = !Runner.GetPhysicsScene2D().Raycast(body.Position, Vector2.down, 0.1f, Layers.MaskAnyGround);
            bool moveDownCheck = physicsObject->Velocity.Y < 0;
            // TODO bool heightLowerCheck = Runner.GetPhysicsScene2D().Raycast(body.Position + WallSlideLowerHeightOffset, wallDirection, MainHitbox.size.x * 2, Layers.MaskSolidGround);
            if ((wallDirection == FPVector2.Left && (!inputs.Left.IsDown || !physicsObject->IsTouchingLeftWall)) || (wallDirection == FPVector2.Right && (!inputs.Right.IsDown || !physicsObject->IsTouchingRightWall))) {
                if (mario->WallslideEndFrames == 0) {
                    mario->WallslideEndFrames = 16;
                    filter.Transform->Position -= wallDirection * FP._0_01;
                }
            } else {
                mario->WallslideEndFrames = 0;
            }
        }


        public void HandleFacingDirection(Frame f, ref Filter filter, MarioPlayerPhysicsInfo physics) {
            using var profilerScope = HostProfiler.Start("MarioPlayerSystem.HandleFacingDirection");
            var mario = filter.MarioPlayer;
            var physicsObject = filter.PhysicsObject;
            var inputs = filter.Inputs;

            if (f.Exists(mario->CurrentPipe) || mario->HasActionFlags(ActionFlags.DisableTurnaround)) {
                return;
            }

            bool rightOrLeft = (inputs.Right.IsDown ^ inputs.Left.IsDown);

            if (mario->WalljumpFrames > 0) {
                mario->FacingRight = physicsObject->Velocity.X > 0;
                // TODO: make this a flag
            } else if (!mario->HasActionFlags(ActionFlags.IsShelled) && mario->Action is not PlayerAction.Sliding and not PlayerAction.Skidding && !mario->IsTurnaround) {
                if (rightOrLeft) {
                    mario->FacingRight = inputs.Right.IsDown;
                }
            } else if (mario->MegaMushroomStartFrames == 0 && mario->MegaMushroomEndFrames == 0 && mario->Action != PlayerAction.Skidding && !mario->IsTurnaround) {
                if (physicsObject->IsOnSlipperyGround && rightOrLeft) {
                    mario->FacingRight = inputs.Right.IsDown;
                } else if ((physicsObject->IsTouchingGround && mario->CurrentPowerupState != PowerupState.MegaMushroom && FPMath.Abs(physicsObject->Velocity.X) > FP._0_05)) {
                    mario->FacingRight = physicsObject->Velocity.X > 0;
                } else if ((mario->MegaMushroomStartFrames > 0) && (rightOrLeft)) {
                    mario->FacingRight = inputs.Right.IsDown;
                }
            }
        }

        /*public void HandleKnockback(Frame f, ref Filter filter) {
            using var profilerScope = HostProfiler.Start("MarioPlayerSystem.HandleKnockback");
            var entity = filter.Entity;
            var mario = filter.MarioPlayer;
            var physicsObject = filter.PhysicsObject;

            if (mario->Action >= PlayerAction.SoftKnockback || mario->Action <= PlayerAction.HardKnockback) {
                bool swimming = physicsObject->IsUnderwater;
                int framesInKnockback = f.Number - mario->KnockbackTick;
                if (mario->DoEntityBounce
                    || (swimming && framesInKnockback > 90)
                    || (!swimming && physicsObject->IsTouchingGround && FPMath.Abs(physicsObject->Velocity.X) < FP._0_33 && framesInKnockback > 30)
                    || (!swimming && physicsObject->IsTouchingGround && framesInKnockback > 120)
                    || (!swimming && mario->Action == PlayerAction.SoftKnockback && framesInKnockback > 30)) {

                    mario->ResetKnockback(f, entity);
                    return;
                }
            }
        }*/

        private bool HandleMegaMushroom(Frame f, ref Filter filter, MarioPlayerPhysicsInfo physics, VersusStageData stage) {
            using var profilerScope = HostProfiler.Start("MarioPlayerSystem.HandleMegaMushroom");
            var mario = filter.MarioPlayer;
            var physicsObject = filter.PhysicsObject;
            var transform = filter.Transform;
            var collider = filter.PhysicsCollider;

            if (mario->MegaMushroomStartFrames > 0) {
                mario->DamageInvincibilityFrames = 0;
                mario->InvincibilityFrames = 0;

                if (QuantumUtils.Decrement(ref mario->MegaMushroomStartFrames)) {
                    // Started
                    mario->MegaMushroomFrames = 15 * 60;
                    physicsObject->IsFrozen = false;

                    Span<PhysicsObjectSystem.LocationTilePair> tiles = stackalloc PhysicsObjectSystem.LocationTilePair[64];
                    int overlappingTiles = PhysicsObjectSystem.GetTilesOverlappingHitbox((FrameThreadSafe) f, transform->Position, collider->Shape, tiles, stage);

                    for (int i = 0; i < overlappingTiles; i++) {
                        StageTile stageTile = f.FindAsset(tiles[i].Tile.Tile);
                        if (stageTile is IInteractableTile it) {
                            it.Interact(f, filter.Entity, InteractionDirection.Up, tiles[i].Position, tiles[i].Tile, out _);
                        }
                    }

                    f.Events.MarioPlayerMegaStart(f, filter.Entity);
                } else {
                    // Still growing...
                    if ((f.Number + filter.Entity.Index) % 4 == 0 && PhysicsObjectSystem.BoxInGround((FrameThreadSafe) f, transform->Position, collider->Shape, false, stage)) {
                        // Cancel growing
                        mario->CurrentPowerupState = PowerupState.Mushroom;
                        mario->MegaMushroomEndFrames = (byte) (90 - mario->MegaMushroomStartFrames);
                        mario->MegaMushroomStartFrames = 0;

                        physicsObject->IsFrozen = true;
                        mario->MegaMushroomStationaryEnd = true;
                        mario->SetReserveItem(f, QuantumUtils.FindPowerupAsset(f, PowerupState.MegaMushroom));

                        f.Events.MarioPlayerMegaEnd(f, filter.Entity, true);
                    }
                    return true;
                }
            }
            if (mario->MegaMushroomFrames > 0) {
                mario->InvincibilityFrames = 0;

                if (physicsObject->IsTouchingGround) {
                    if (mario->JumpState != JumpState.None) {
                        // Break ground
                        foreach (var contact in f.ResolveList(physicsObject->Contacts)) {
                            if (FPVector2.Dot(contact.Normal, FPVector2.Up) < FP._0_33 * 2) {
                                continue;
                            }

                            StageTileInstance tileInstance = stage.GetTileRelative(f, contact.TileX, contact.TileY);
                            StageTile tile = f.FindAsset(tileInstance.Tile);

                            if (tile is IInteractableTile it) {
                                it.Interact(f, filter.Entity, InteractionDirection.Down, new Vector2Int(contact.TileX, contact.TileY), tileInstance, out _);
                            }
                        }
                    }

                    mario->JumpState = JumpState.None;
                }

                var contacts = f.ResolveList(physicsObject->Contacts);
                foreach (var contact in contacts) {
                    // Try to break this tile as mega mario...
                    if (contact.TileX == -1 || contact.TileY == -1) {
                        continue;
                    }

                    InteractionDirection direction;
                    FP upDot = FPVector2.Dot(contact.Normal, FPVector2.Up);
                    if (upDot > PhysicsObjectSystem.GroundMaxAngle) {
                        // Ground contact... only allow if groundpounding
                        if (mario->Action == PlayerAction.GroundPound && mario->HasActionFlags(ActionFlags.BreaksBlocks)) {
                            continue;
                        }
                        direction = InteractionDirection.Down;
                    } else if (upDot < -PhysicsObjectSystem.GroundMaxAngle) {
                        direction = InteractionDirection.Up;
                    } else if (contact.Normal.X < 0) {
                        direction = InteractionDirection.Right;
                    } else {
                        direction = InteractionDirection.Left;
                    }

                    StageTileInstance tileInstance = stage.GetTileRelative(f, contact.TileX, contact.TileY);
                    StageTile tile = f.FindAsset(tileInstance.Tile);
                    if (tile is IInteractableTile it) {
                        if (it.Interact(f, filter.Entity, direction, new Vector2Int(contact.TileX, contact.TileY), tileInstance, out bool _)) {
                            // Block broke, preserve velocity.
                            if (direction == InteractionDirection.Left || direction == InteractionDirection.Right) {
                                physicsObject->Velocity.X = physicsObject->PreviousFrameVelocity.X;
                                FP leftoverVelocity = (FPMath.Abs(physicsObject->Velocity.X) - (contact.Distance * f.UpdateRate)) * (physicsObject->Velocity.X > 0 ? 1 : -1);
                                PhysicsObjectSystem.MoveHorizontally((FrameThreadSafe) f, new FPVector2(leftoverVelocity, 0), filter.Entity, f.FindAsset<VersusStageData>(f.Map.UserAsset));
                            } else if (direction == InteractionDirection.Up) {
                                physicsObject->Velocity.Y = physicsObject->PreviousFrameVelocity.Y;
                                FP leftoverVelocity = (FPMath.Abs(physicsObject->Velocity.Y) - (contact.Distance * f.UpdateRate)) * (physicsObject->Velocity.Y > 0 ? 1 : -1);
                                PhysicsObjectSystem.MoveVertically((FrameThreadSafe) f, new FPVector2(0, leftoverVelocity), filter.Entity, f.FindAsset<VersusStageData>(f.Map.UserAsset));
                            }
                        }
                    }
                }

                if (QuantumUtils.Decrement(ref mario->MegaMushroomFrames)) {
                    // Ended
                    mario->PreviousPowerupState = mario->CurrentPowerupState;
                    mario->CurrentPowerupState = PowerupState.Mushroom;

                    mario->MegaMushroomEndFrames = 45;
                    mario->MegaMushroomStationaryEnd = false;
                    mario->DamageInvincibilityFrames = 2 * 60;

                    if (physicsObject->Velocity.Y > 0) {
                        physicsObject->Velocity.Y *= FP._0_33;
                    }

                    f.Events.MarioPlayerMegaEnd(f, filter.Entity, false);
                }
            }
            if (mario->MegaMushroomEndFrames > 0 && QuantumUtils.Decrement(ref mario->MegaMushroomEndFrames) && mario->MegaMushroomStationaryEnd) {
                // Ended after premature shrink, allow to move.

                mario->DamageInvincibilityFrames = 2 * 60;
                physicsObject->Velocity = FPVector2.Zero;
                physicsObject->IsFrozen = false;
                mario->CurrentPowerupState = mario->PreviousPowerupState;
                mario->MegaMushroomStationaryEnd = false;
            }
            
            return false;
        }

        // this refrs to things that run every frame
        private void HandleGlobals(Frame f, ref Filter filter, MarioPlayerPhysicsInfo physics, VersusStageData stage) {
            var mario = filter.MarioPlayer;
            var physicsObject = filter.PhysicsObject;
            QuantumUtils.Decrement(ref mario->GroundpoundCooldownFrames);
            QuantumUtils.Decrement(ref mario->PropellerDrillCooldown);


            if (QuantumUtils.Decrement(ref mario->InvincibilityFrames)) {
                //f.Unsafe.GetPointer<ComboKeeper>(filter.Entity)->Combo = 0;
            }
            QuantumUtils.Decrement(ref mario->PropellerSpinFrames);
            QuantumUtils.Decrement(ref mario->ProjectileDelayFrames);
            if (QuantumUtils.Decrement(ref mario->ProjectileVolleyFrames)) {
                mario->CurrentVolley = 0;
            }

            mario->UsedPropellerThisJump &= !physicsObject->IsTouchingGround;
        }

        private void HandlePowerups(Frame f, ref Filter filter, MarioPlayerPhysicsInfo physics, VersusStageData stage) {
            using var profilerScope = HostProfiler.Start("MarioPlayerSystem.HandlePowerups");
            var mario = filter.MarioPlayer;
            var physicsObject = filter.PhysicsObject;

            /*physicsObject->IsWaterSolid = mario->CurrentPowerupState == PowerupState.MiniMushroom && !mario->IsGroundpounding && mario->StationaryFrames < 15 && (!mario->IsInKnockback || mario->IsInWeakKnockback);
            if (physicsObject->IsWaterSolid && !physicsObject->WasTouchingGround && physicsObject->IsTouchingGround) {
                // Check if we landed on water
                var contacts = f.ResolveList(physicsObject->Contacts);
                foreach (var contact in contacts) {
                    if (f.Has<Liquid>(contact.Entity)) {
                        f.Events.LiquidSplashed(f, contact.Entity, filter.Entity, 2, filter.Transform->Position, false);
                        break;
                    }
                }
            }*/
        }

        private Projectile* ShootHammerProjectile(Frame f, ref Filter filter, MarioPlayerPhysicsInfo physics) {
            var mario = filter.MarioPlayer;
            var physicsObject = filter.PhysicsObject;

            FPVector2 spawnPos = filter.Transform->Position + new FPVector2(mario->FacingRight ? FP._0_25 : -FP._0_25, Constants._0_40);
            EntityRef newEntity = f.Create(f.SimulationConfig.HammerPrototype);

            var projectile = f.Unsafe.GetPointer<Projectile>(newEntity);
            projectile->InitializeHammer(f, newEntity, filter.Entity, spawnPos, mario->FacingRight, filter.Inputs.Up.IsDown);
            return projectile;
        }


        private Projectile* ShootNormalProjectile(Frame f, ref Filter filter, MarioPlayerPhysicsInfo physics) {
            var mario = filter.MarioPlayer;
            var physicsObject = filter.PhysicsObject;

            FPVector2 spawnPos = filter.Transform->Position + new FPVector2(mario->FacingRight ? Constants._0_40 : -Constants._0_40, Constants._0_35);

            EntityRef newEntity = f.Create(mario->CurrentPowerupState == PowerupState.IceFlower
                ? f.SimulationConfig.IceballPrototype
                : f.SimulationConfig.FireballPrototype);

            var projectile = f.Unsafe.GetPointer<Projectile>(newEntity);
            projectile->Initialize(f, newEntity, filter.Entity, spawnPos, mario->FacingRight);
            return projectile;
        }

        private void HandleSwimming(Frame f, ref Filter filter, MarioPlayerPhysicsInfo physics) {
            using var profilerScope = HostProfiler.Start("MarioPlayerSystem.HandleSwimming");
            var mario = filter.MarioPlayer;
            var physicsObject = filter.PhysicsObject;

            if (FPMath.Abs(physicsObject->Velocity.X) > Constants._0_1875 || physicsObject->Velocity.Y > 0) {
                mario->StationaryFrames = 0;
            } else if (physicsObject->IsTouchingGround && mario->StationaryFrames < byte.MaxValue) {
                mario->StationaryFrames++;
            }

            if (!physicsObject->IsUnderwater) {
                return;
            }

            bool holdingSmallObject = false;
            if (f.Unsafe.TryGetPointer(mario->HeldEntity, out Holdable* holdable)) {
                if (holdable->HoldAboveHead) {
                    // Drop without throw
                    mario->HeldEntity = EntityRef.None;
                    holdable->Holder = EntityRef.None;
                } else {
                    holdingSmallObject = true;
                }
            }

            if (holdingSmallObject) {
                FP maxSurface = FP.MinValue;
                var liquids = f.ResolveHashSet(physicsObject->LiquidContacts);
                foreach (var liquidEntity in liquids) {
                    var liquid = f.Unsafe.GetPointer<Liquid>(liquidEntity);
                    var liquidTransform = f.Unsafe.GetPointer<Transform2D>(liquidEntity);
                    FP surface = liquid->GetSurfaceHeight(liquidTransform);

                    maxSurface = FPMath.Max(surface, maxSurface);
                }

                var hitboxShape = filter.PhysicsCollider->Shape;
                FP hitboxCenter = filter.Transform->Position.Y + hitboxShape.Centroid.Y + FP._0_10;
                FP distanceToSurface = maxSurface - hitboxCenter;

                if (distanceToSurface > 0) {
                    FP original = physicsObject->Velocity.Y;
                    if (physicsObject->IsTouchingGround && physicsObject->Velocity.Y < 0) {
                        physicsObject->Velocity.Y = 1;
                    }
                    physicsObject->Velocity.Y = FPMath.Min(physicsObject->Velocity.Y + (physics.SwimAcceleration[^1] * FPMath.Clamp01(distanceToSurface * (1 - FPMath.Clamp01(physicsObject->Velocity.Y / physics.SwimJumpVelocity)))), physics.SwimJumpVelocity);
                    physicsObject->IsTouchingGround = false;
                    physicsObject->WasTouchingGround = false;
                }
            }

            //mario->IsCrouching |= physicsObject->IsTouchingGround && mario->IsSliding;
            mario->JumpState = JumpState.None;

            if (/*!mario->IsInKnockback &&*/ mario->JumpBufferFrames > 0) {
                if (physicsObject->IsTouchingGround) {
                    // 1.75x off the ground because reasons
                    physicsObject->Velocity.Y = physics.SwimJumpVelocity * FP._0_75;
                }
                physicsObject->Velocity.Y += physics.SwimJumpVelocity;
                physicsObject->IsTouchingGround = false;
                mario->JumpBufferFrames = 0;

                f.Events.MarioPlayerJumped(f, filter.Entity, ConvertJumpState(JumpState.None), false);
            }
        }

        private void HandlePipes(Frame f, ref Filter filter, MarioPlayerPhysicsInfo physics, VersusStageData stage) {
            using var profilerScope = HostProfiler.Start("MarioPlayerSystem.HandlePipes");
            var mario = filter.MarioPlayer;

            QuantumUtils.Decrement(ref mario->PipeCooldownFrames);

            if (!f.Exists(mario->CurrentPipe)) {
                return;
            }

            var physicsObject = filter.PhysicsObject;
            var interactable = f.Unsafe.GetPointer<Interactable>(filter.Entity);
            var currentPipe = f.Unsafe.GetPointer<EnterablePipe>(mario->CurrentPipe);

            interactable->ColliderDisabled = true;
            physicsObject->Velocity = mario->PipeDirection;
            physicsObject->DisableCollision = true;

            if (QuantumUtils.Decrement(ref mario->PipeFrames)) {
                if (mario->PipeEntering) {
                    // Teleport to other pipe
                    var otherPipe = f.Unsafe.GetPointer<EnterablePipe>(currentPipe->OtherPipe);
                    var otherPipeTransform = f.Unsafe.GetPointer<Transform2D>(currentPipe->OtherPipe);

                    if (otherPipe->IsCeilingPipe == currentPipe->IsCeilingPipe) {
                        mario->PipeDirection *= -1;
                    }

                    FPVector2 offset = mario->PipeDirection * ((physics.PipeEnterDuration - 3) / (FP) 60);
                    if (otherPipe->IsCeilingPipe) {
                        offset.Y += filter.PhysicsCollider->Shape.Box.Extents.Y * 2;
                    }
                    FPVector2 tpPos = otherPipeTransform->Position - offset;
                    filter.Transform->Teleport(f, tpPos);

                    var camera = f.Unsafe.GetPointer<CameraController>(filter.Entity);
                    camera->Recenter(stage, tpPos + offset);
                    mario->PipeFrames = physics.PipeEnterDuration;
                    mario->PipeEntering = false;
                    mario->CurrentPipe = currentPipe->OtherPipe;

                    f.Events.MarioPlayerEnteredPipe(f, filter.Entity, mario->CurrentPipe);
                } else {
                    // End pipe animation
                    mario->CurrentPipe = EntityRef.None;
                    physicsObject->DisableCollision = false;
                    //IsOnGround = false;
                    mario->JumpState = JumpState.None;
                    mario->PipeCooldownFrames = 30;
                    physicsObject->Velocity = FPVector2.Zero;
                    interactable->ColliderDisabled = false;
                }
            }
        }

        private void HandleHitbox(Frame f, ref Filter filter, MarioPlayerPhysicsInfo physics) {
            using var profilerScope = HostProfiler.Start("MarioPlayerSystem.HandleHitbox");
            var mario = filter.MarioPlayer;
            var physicsObject = filter.PhysicsObject;
            var collider = filter.PhysicsCollider;

            QuantumUtils.Decrement(ref mario->DamageInvincibilityFrames);

            FPVector2 iceBlockSize = collider->Shape.Box.Extents;
            FP newHeight;
            bool crouchHitbox = mario->HasActionFlags(ActionFlags.UsesCrouchHitbox) && mario->CurrentPowerupState >= PowerupState.Mushroom && !f.Exists(mario->CurrentPipe);
            bool smallHitbox = mario->HasActionFlags(ActionFlags.UsesSmallHitbox) && !crouchHitbox;

            if (mario->CurrentPowerupState <= PowerupState.MiniMushroom || smallHitbox) {
                newHeight = physics.SmallHitboxHeight;
                if (smallHitbox) {
                    iceBlockSize.Y = physics.LargeHitboxHeight / 2;
                } else {
                    iceBlockSize.Y = physics.SmallHitboxHeight / 2;
                }
            } else {
                newHeight = physics.LargeHitboxHeight;
                iceBlockSize.Y = physics.LargeHitboxHeight / 2;
            }

            if (crouchHitbox) {
                newHeight /= 2;
            }

            FPVector2 newExtents = new(Constants._0_1875, newHeight / 2);
            if (mario->CurrentPowerupState == PowerupState.MiniMushroom) {
                newExtents /= 2;
            }

            FP megaPercentage = 0;
            if (mario->MegaMushroomStartFrames > 0) {
                megaPercentage = 1 - (mario->MegaMushroomStartFrames / (FP) 90);
            } else if (mario->MegaMushroomEndFrames > 0) {
                megaPercentage = mario->MegaMushroomEndFrames / (FP) 45;
            } else if (mario->MegaMushroomFrames > 0) {
                megaPercentage = 1;
            }
            newExtents *= FPMath.Lerp(1, Constants._3_50, megaPercentage);

            collider->Shape.Box.Extents = newExtents;
            collider->Shape.Centroid = FPVector2.Up * newExtents.Y;
            collider->IsTrigger = mario->IsDead;

            filter.Freezable->IceBlockSize = iceBlockSize * Constants._2_50;
            filter.Freezable->IceBlockSize.Y += FP._0_10;
            filter.Freezable->IceBlockSize.X += FP._0_10;
        }

        private bool HandleStuckInBlock(Frame f, ref Filter filter, VersusStageData stage) {
            using var profilerScope = HostProfiler.Start("MarioPlayerSystem.HandleStuckInBlock");
            var mario = filter.MarioPlayer;
            var freezable = filter.Freezable;

            QuantumUtils.Decrement(ref mario->CrushDamageInvincibilityFrames);

            if (freezable->IsFrozen(f) || f.Exists(mario->CurrentPipe) || mario->MegaMushroomStartFrames > 0 || (mario->MegaMushroomEndFrames > 0 && mario->MegaMushroomStationaryEnd) || mario->HasActionFlags(ActionFlags.Cutscene)) {
                return false;
            }

            var physicsObject = filter.PhysicsObject;
            var transform = filter.Transform;
            Shape2D shape = filter.PhysicsCollider->Shape;

            if (!PhysicsObjectSystem.BoxInGround((FrameThreadSafe) f, transform->Position, shape, stage: stage, entity: filter.Entity, includeCeilingCrushers: !physicsObject->IsTouchingGround && (!physicsObject->WasTouchingGround || physicsObject->IsTouchingGround))) {
                if (mario->IsStuckInBlock) {
                    physicsObject->DisableCollision = false;
                    physicsObject->Velocity = FPVector2.Zero;
                    mario->IsStuckInBlock = false;
                } 

                if (physicsObject->IsBeingCrushed) {
                    // In a ceiling crusher
                    if (mario->CrushDamageInvincibilityFrames == 0) {
                        mario->CrushDamageInvincibilityFrames = 30;
                        mario->Powerdown(f, filter.Entity, true);
                    }
                }
                return false;
            }

            bool wasStuckLastTick = mario->IsStuckInBlock;

            mario->IsStuckInBlock = true;
            mario->SetGroundAction(physicsObject, f);
            mario->SetAirAction(physicsObject, f);
            physicsObject->IsTouchingGround = false;

            if (!wasStuckLastTick) {
                // Code for mario to instantly teleport to the closest free position when he gets stuck
                if (PhysicsObjectSystem.TryEject((FrameThreadSafe) f, filter.Entity, stage)) {
                    mario->IsStuckInBlock = false;
                    return false;
                }
            }

            physicsObject->Gravity = FPVector2.Zero;
            physicsObject->Velocity = FPVector2.Right * 2;
            physicsObject->DisableCollision = true;
            return true;
        }

        private void HandleBreakingBlocks(Frame f, ref Filter filter, MarioPlayerPhysicsInfo physics, VersusStageData stage) {
            var physicsObject = filter.PhysicsObject;
            if (!physicsObject->IsTouchingCeiling) {
                return;
            }

            using var profilerScope = HostProfiler.Start("MarioPlayerSystem.HandleBreakingBlocks");
            var mario = filter.MarioPlayer;

            bool? playBumpSound = null;
            QList<PhysicsContact> contacts = f.ResolveList(physicsObject->Contacts);
            foreach (var contact in contacts) {
                if (f.Exists(contact.Entity)
                    || FPVector2.Dot(contact.Normal, FPVector2.Down) < PhysicsObjectSystem.GroundMaxAngle) {
                    continue;
                }

                // Ceiling tiles.
                var tileInstance = stage.GetTileRelative(f, contact.TileX, contact.TileY);
                StageTile tile = f.FindAsset(tileInstance.Tile);
                if (tile == null) {
                    playBumpSound = false;
                } else if (tile is IInteractableTile it) {
                    it.Interact(f, filter.Entity, InteractionDirection.Up,
                        new Vector2Int(contact.TileX, contact.TileY), tileInstance, out bool tempPlayBumpSound);

                    playBumpSound = (playBumpSound ?? true) & tempPlayBumpSound;
                }
            }

            if (physicsObject->IsUnderwater) {
                // TODO: magic value
                physicsObject->Velocity.Y = -2;
            }
            if (playBumpSound ?? true) {
                f.Events.PlayBumpSound(f, filter.Entity);
            }
        }

        private void HandleSpinners(Frame f, ref Filter filter, VersusStageData stage) {
            using var profilerScope = HostProfiler.Start("MarioPlayerSystem.HandleSpinners");
            var mario = filter.MarioPlayer;
            var physicsObject = filter.PhysicsObject;
            
            if (!f.TryResolveList(physicsObject->Contacts, out QList<PhysicsContact> contacts)) {
                return;
            }

            if (physicsObject->IsTouchingGround) {
                //mario->SetPlayerAction(PlayerAction.SpinBlockSpin);
            }

            EntityRef currentSpinner = EntityRef.None;
            foreach (var contact in contacts) {
                if (f.Has<Spinner>(contact.Entity)
                    && FPVector2.Dot(contact.Normal, FPVector2.Up) > PhysicsObjectSystem.GroundMaxAngle) {
                    currentSpinner = contact.Entity;
                    break;
                }
            }

            mario->CurrentSpinner = currentSpinner;

            if (currentSpinner == EntityRef.None) {
                return;
            }

            // We have a spinner
            if (FPMath.Abs(physicsObject->Velocity.X) > FP._0_10) {
                // Too fast to be auto-moved
                return;
            }

            var transform = filter.Transform;
            var spinnerTransform = f.Unsafe.GetPointer<Transform2D>(currentSpinner);

            FP moveVelocity = QuantumUtils.MoveTowards(transform->Position.X, spinnerTransform->Position.X, 4) - transform->Position.X;

            if (FPMath.Abs(moveVelocity) > 0) {
                PhysicsObjectSystem.MoveHorizontally((FrameThreadSafe) f, new FPVector2(moveVelocity, 0), filter.Entity, stage, contacts);
            }
        }

        private bool HandleDeathAndRespawning(Frame f, ref Filter filter, VersusStageData stage) {
            using var profilerScope = HostProfiler.Start("MarioPlayerSystem.HandleDeathAndRespawning");

            var mario = filter.MarioPlayer;
            var transform = filter.Transform;
            var physicsObject = filter.PhysicsObject;
            var collider = filter.PhysicsCollider;
            var entity = filter.Entity;

            // ignore cutscene actions
            if (!mario->HasActionFlags(ActionFlags.Cutscene)) {
                if (transform->Position.Y + (collider->Shape.Box.Extents.Y * 2) < stage.StageWorldMin.Y) {
                    // Death via pit
                    mario->SetPlayerAction(PlayerAction.Death, f, mario->GetDeathArgs(f), discardItem: true);
                }
            }
            return false;
        }

        public static void SpawnItem(Frame f, EntityRef marioEntity, MarioPlayer* mario, AssetRef<EntityPrototype> prefab) {
            if (!prefab.IsValid) {
                prefab = QuantumUtils.GetRandomItem(f, mario).Prefab;
            }

            EntityRef newEntity = f.Create(prefab);
            if (f.Unsafe.TryGetPointer(newEntity, out Powerup* powerup)) {
                powerup->ParentToPlayer(f, newEntity, marioEntity);
            }
        }

        public void SpawnReserveItem(Frame f, ref Filter filter) {
            var mario = filter.MarioPlayer;
            var reserveItem = f.FindAsset(mario->ReserveItem);

            if (reserveItem == null || mario->IsDead || mario->MegaMushroomStartFrames > 0 || (mario->MegaMushroomStationaryEnd && mario->MegaMushroomEndFrames > 0)) {
                f.Events.MarioPlayerUsedReserveItem(f, filter.Entity, false);
                return;
            }

            SpawnItem(f, filter.Entity, mario, reserveItem.Prefab);
            mario->ReserveItem = default;
            f.Events.MarioPlayerUsedReserveItem(f, filter.Entity, true);
        }

        #region Interactions
        public static void OnMarioInvisibleBlockInteraction(Frame f, EntityRef marioEntity, EntityRef invisibleBlockEntity, PhysicsContact contact) {
            var mario = f.Unsafe.GetPointer<MarioPlayer>(marioEntity);
            var invisibleBlock = f.Unsafe.GetPointer<InvisibleBlock>(invisibleBlockEntity);
            var transform = f.Unsafe.GetPointer<Transform2D>(invisibleBlockEntity);

            VersusStageData stage = f.FindAsset<VersusStageData>(f.Map.UserAsset);
            if (stage.GetTileWorld(f, transform->Position).Tile != default) {
                return;
            }

            StageTileInstance result = new StageTileInstance {
                Rotation = 0,
                Scale = FPVector2.One,
                Tile = invisibleBlock->Tile,
            };
            f.Signals.OnMarioPlayerCollectedCoin(marioEntity, mario, transform->Position, true, false);
            BreakableBrickTile.Bump(f, stage, QuantumUtils.WorldToRelativeTile(stage, transform->Position), invisibleBlock->BumpTile, result, false, marioEntity, false);
        }

        public static void OnMarioCoinInteraction(Frame f, EntityRef marioEntity, EntityRef coinEntity) {
            CoinSystem.TryCollectCoin(f, coinEntity, marioEntity);
        }

        public static void OnMarioProjectileInteraction(Frame f, EntityRef marioEntity, EntityRef projectileEntity) {
            if (!f.Exists(projectileEntity)) {
                return;
            }

            var projectile = f.Unsafe.GetPointer<Projectile>(projectileEntity);
            if (projectile->Owner == marioEntity) {
                return;
            }

            var mario = f.Unsafe.GetPointer<MarioPlayer>(marioEntity);
            var projectileAsset = f.FindAsset(projectile->Asset);
            bool dropStars = true;

            if (f.Unsafe.TryGetPointer(projectile->Owner, out MarioPlayer* ownerMario)) {
                dropStars = ownerMario->GetTeam(f) != mario->GetTeam(f);
            }

            if (!mario->HasActionFlags(ActionFlags.Intangible)
                && mario->CurrentPowerupState != PowerupState.MegaMushroom
                && mario->IsDamageable
                && !(mario->HasActionFlags(ActionFlags.IsShelled) && projectileAsset.DoesntEffectBlueShell)) { 

                switch (projectileAsset.Effect) {
                case ProjectileEffectType.KillEnemiesAndSoftKnockbackPlayers:
                case ProjectileEffectType.Fire:
                    if (dropStars && mario->CurrentPowerupState == PowerupState.MiniMushroom) {
                        mario->SetPlayerAction(PlayerAction.LavaDeath, f, mario->GetDeathArgs(f), discardItem: true);
                    } else {
                        int args = (dropStars ? 1 : 0) + (!projectile->FacingRight ? MarioPlayer.DropStarRight : 0);
                        mario->SetPlayerAction(PlayerAction.SoftKnockback, f, args, projectileEntity);
                    }
                    break;
                case ProjectileEffectType.Freeze:
                    if (dropStars && mario->CurrentPowerupState == PowerupState.MiniMushroom) {
                        mario->SetPlayerAction(PlayerAction.Death, f, mario->GetDeathArgs(f), discardItem: true);
                    } else if (dropStars) {
                        IceBlockSystem.Freeze(f, marioEntity);
                    } else {
                        int args = (dropStars ? 1 : 0) + (!projectile->FacingRight ? MarioPlayer.DropStarRight : 0);
                        mario->SetPlayerAction(PlayerAction.SoftKnockback, f, args, projectileEntity, dropItem: true);
                    }
                    break;
                }
            }

            f.Signals.OnProjectileHitEntity(f, projectileEntity, marioEntity);
        }

        public static void OnMarioMarioInteraction(Frame f, EntityRef marioAEntity, EntityRef marioBEntity) {
            var marioA = f.Unsafe.GetPointer<MarioPlayer>(marioAEntity);
            var marioB = f.Unsafe.GetPointer<MarioPlayer>(marioBEntity);

            // Don't damage players in the Mega Mushroom grow animation
            if (marioA->MegaMushroomStartFrames > 0 || marioB->MegaMushroomStartFrames > 0) {
                return;
            }

            // Or with invincibility frames
            if (marioA->DamageInvincibilityFrames > 0 || marioB->DamageInvincibilityFrames > 0) {
                return;
            }

            // Or players that are stuck
            if (marioA->IsStuckInBlock || marioB->IsStuckInBlock) {
                return;
            }

            // Or if a player just got damaged
            if ((f.Number - marioA->KnockbackTick) < 12 || (f.Number - marioB->KnockbackTick) < 12) {
                return;
            }

            // Or intangible actions
            if (marioA->HasActionFlags(ActionFlags.Intangible) || marioB->HasActionFlags(ActionFlags.Intangible)) {
                return;
            }

            var marioATransform = f.Unsafe.GetPointer<Transform2D>(marioAEntity);
            var marioBTransform = f.Unsafe.GetPointer<Transform2D>(marioBEntity);
            var marioAPhysics = f.Unsafe.GetPointer<PhysicsObject>(marioAEntity);
            var marioBPhysics = f.Unsafe.GetPointer<PhysicsObject>(marioBEntity);

            // Hit players
            bool dropStars = marioA->GetTeam(f) != marioB->GetTeam(f);

            QuantumUtils.UnwrapWorldLocations(f, marioATransform->Position, marioBTransform->Position, out FPVector2 marioAPosition, out FPVector2 marioBPosition);
            bool fromRight = marioAPosition.X < marioBPosition.X;

            // Starman cases
            bool marioAStarman = marioA->IsStarmanInvincible;
            bool marioBStarman = marioB->IsStarmanInvincible;
            if (marioAStarman && marioBStarman) {
                marioA->SetPlayerAction(PlayerAction.SoftKnockback, f, (dropStars ? 1 : 0) + (fromRight ? MarioPlayer.DropStarRight : 0), marioBEntity, dropItem: true);
                marioB->SetPlayerAction(PlayerAction.SoftKnockback, f, (dropStars ? 1 : 0) + (!fromRight ? MarioPlayer.DropStarRight : 0), marioAEntity, dropItem: true);
                return;
            } else if (marioAStarman) {
                MarioMarioAttackStarman(f, marioAEntity, marioBEntity, fromRight, dropStars);
                return;
            } else if (marioBStarman) {
                MarioMarioAttackStarman(f, marioBEntity, marioAEntity, !fromRight, dropStars);
                return;
            }

            FP dot = FPVector2.Dot((marioAPosition - marioBPosition).Normalized, FPVector2.Up);
            bool marioAAbove = dot > Constants._0_66;
            bool marioBAbove = dot < -Constants._0_66;

            // Mega mushroom cases
            bool marioAMega = marioA->CurrentPowerupState == PowerupState.MegaMushroom;
            bool marioBMega = marioB->CurrentPowerupState == PowerupState.MegaMushroom;
            if (marioAMega && marioBMega) {
                // Both mega
                if (marioAAbove) {
                    marioA->CheckEntityBounce(f, true);
                } else if (marioBAbove) {
                    marioB->CheckEntityBounce(f, true);
                } else {
                    marioA->SetPlayerAction(PlayerAction.SoftKnockback, f, fromRight ? MarioPlayer.DropStarRight : 0, marioBEntity, dropItem: true);
                    marioB->SetPlayerAction(PlayerAction.SoftKnockback, f, !fromRight ? MarioPlayer.DropStarRight : 0, marioAEntity, dropItem: true);
                }
                return;
            } else if (marioAMega) {
                if (dropStars) {
                    marioB->Powerdown(f, marioBEntity, false);
                } else {
                    marioB->SetPlayerAction(PlayerAction.SoftKnockback, f, !fromRight ? MarioPlayer.DropStarRight : 0, marioAEntity, dropItem: true);
                }
                return;
            } else if (marioBMega) {
                if (dropStars) {
                    marioA->Powerdown(f, marioAEntity, false);
                } else {
                    marioA->SetPlayerAction(PlayerAction.SoftKnockback, f, dropStars ? 1 : 0 + (fromRight ? MarioPlayer.DropStarRight : 0), marioBEntity, dropItem: true);
                }
                return;
            }

            // Blue shell cases
            bool marioAShell = marioA->HasActionFlags(ActionFlags.IsShelled | ActionFlags.Attacking);
            bool marioBShell = marioB->HasActionFlags(ActionFlags.IsShelled | ActionFlags.Attacking);
            if (marioAShell && marioBShell) {
                marioA->SetPlayerAction(PlayerAction.SoftKnockback, f, dropStars ? 1 : 0 + (fromRight ? MarioPlayer.DropStarRight : 0), marioBEntity, throwItem: true);
                marioB->SetPlayerAction(PlayerAction.SoftKnockback, f, dropStars ? 1 : 0 + (!fromRight ? MarioPlayer.DropStarRight : 0), marioAEntity, throwItem: true);
                return;
            } else if (marioAShell) {
                if (!marioBAbove) {
                    // Hit them, powerdown them
                    if (dropStars) {
                        marioB->Powerdown(f, marioBEntity, false);
                        marioB->SetPlayerAction(PlayerAction.NormalKnockback, f, !fromRight ? MarioPlayer.DropStarRight : 0, marioAEntity, dropItem: true);
                    } else {
                        marioB->SetPlayerAction(PlayerAction.SoftKnockback, f, !fromRight ? MarioPlayer.DropStarRight : 0, marioAEntity, dropItem: true);
                    }
                    marioA->FacingRight = !marioA->FacingRight;
                    f.Events.PlayBumpSound(f, marioAEntity);
                    return;
                }
            } else if (marioBShell) {
                if (!marioAAbove) {
                    // Hit them, powerdown them
                    if (dropStars) {
                        marioA->Powerdown(f, marioAEntity, false);
                        marioA->SetPlayerAction(PlayerAction.NormalKnockback, f, !fromRight ? MarioPlayer.DropStarRight : 0, marioBEntity, dropItem: true);
                    } else {
                        marioA->SetPlayerAction(PlayerAction.SoftKnockback, f, !fromRight ? MarioPlayer.DropStarRight : 0, marioBEntity, throwItem: true);
                    }
                    marioB->FacingRight = !marioB->FacingRight;
                    f.Events.PlayBumpSound(f, marioBEntity);
                    return;
                }
            }

            var stage = f.FindAsset<VersusStageData>(f.Map.UserAsset);
            // Crouched in shell stomps
            if (marioA->IsCrouchedInShell && marioAPhysics->IsTouchingGround && marioBAbove && marioB->StarStealCount < 3) {
                MarioMarioBlueShellStomp(f, stage, marioBEntity, marioAEntity, fromRight);
                return;
            } else if (marioB->IsCrouchedInShell && marioBPhysics->IsTouchingGround && marioAAbove && marioA->StarStealCount < 3) {
                MarioMarioBlueShellStomp(f, stage, marioAEntity, marioBEntity, fromRight);
                return;
            }

            // Normal stomps
            if (marioAAbove && marioA->StarStealCount != MarioPlayer.NoStarLoss) {
                MarioMarioStomp(f, marioAEntity, marioBEntity, fromRight, dropStars);
                return;
            } else if (marioBAbove && marioB->StarStealCount != MarioPlayer.NoStarLoss) {
                MarioMarioStomp(f, marioBEntity, marioAEntity, !fromRight, dropStars);
                return;
            }

            // Pushing
            bool marioAMini = marioA->CurrentPowerupState == PowerupState.MiniMushroom;
            bool marioBMini = marioB->CurrentPowerupState == PowerupState.MiniMushroom;

            // Collided with them
            var marioAPhysicsInfo = f.FindAsset(marioA->PhysicsAsset);
            var marioBPhysicsInfo = f.FindAsset(marioB->PhysicsAsset);

            if (marioAMini ^ marioBMini) {
                // Minis
                if (marioAMini && marioA->HasActionFlags(ActionFlags.AllowBump)) {
                    marioA->SetPlayerAction(PlayerAction.NormalKnockback, f, (dropStars ? 1 : 0) + (fromRight ? MarioPlayer.DropStarRight : 0), marioBEntity);
                }
                if (marioBMini && marioB->HasActionFlags(ActionFlags.AllowBump)) {
                    marioB->SetPlayerAction(PlayerAction.NormalKnockback, f, (dropStars ? 1 : 0) + (!fromRight ? MarioPlayer.DropStarRight : 0), marioAEntity);
                }
            } else if (FPMath.Abs(marioAPhysics->Velocity.X) > marioAPhysicsInfo.WalkMaxVelocity[marioAPhysicsInfo.WalkSpeedStage]
                        || FPMath.Abs(marioBPhysics->Velocity.X) > marioBPhysicsInfo.WalkMaxVelocity[marioBPhysicsInfo.WalkSpeedStage]) {

                // Bump
                if (marioA->HasActionFlags(ActionFlags.AllowBump)) {
                    if (marioAPhysics->IsTouchingGround) {
                        marioA->SetPlayerAction(PlayerAction.SoftKnockback, f, (dropStars ? 1 : 0) + (fromRight ? MarioPlayer.DropStarRight : 0), marioBEntity);
                    } else {
                        marioAPhysics->Velocity.X = marioAPhysicsInfo.WalkMaxVelocity[marioAPhysicsInfo.RunSpeedStage] * (fromRight ? -1 : 1);
                    }
                }

                if (marioB->HasActionFlags(ActionFlags.AllowBump)) {
                    if (marioBPhysics->IsTouchingGround && marioB->HasActionFlags(ActionFlags.AllowBump)) {
                        marioB->SetPlayerAction(PlayerAction.SoftKnockback, f, (dropStars ? 1 : 0) + (!fromRight ? MarioPlayer.DropStarRight : 0), marioAEntity);
                    } else {
                        marioBPhysics->Velocity.X = marioBPhysicsInfo.WalkMaxVelocity[marioBPhysicsInfo.RunSpeedStage] * (fromRight ? 1 : -1);
                    }
                }
            } else {
                // Collide
                int directionToOtherPlayer = fromRight ? -1 : 1;
                var marioACollider = f.Unsafe.GetPointer<PhysicsCollider2D>(marioAEntity);
                var marioBCollider = f.Unsafe.GetPointer<PhysicsCollider2D>(marioBEntity);
                FP overlap = (marioACollider->Shape.Box.Extents.X + marioBCollider->Shape.Box.Extents.X - FPMath.Abs(marioAPosition.X - marioBPosition.X)) / 2;

                if (overlap > 0) {
                    // Move 
                    PhysicsObjectSystem.MoveHorizontally((FrameThreadSafe) f, new FPVector2(overlap * directionToOtherPlayer * f.UpdateRate, 0), marioAEntity, stage);
                    PhysicsObjectSystem.MoveHorizontally((FrameThreadSafe) f, new FPVector2(overlap * -directionToOtherPlayer * f.UpdateRate, 0), marioBEntity, stage);

                    // Transfer velocity
                    FP avgVelocityX = (marioAPhysics->Velocity.X + marioBPhysics->Velocity.X) * FP._0_75;

                    if (FPMath.Abs(marioAPhysics->Velocity.X) > 1) {
                        marioA->LastPushingFrame = f.Number;
                        marioAPhysics->Velocity.X = avgVelocityX;
                    }
                    if (FPMath.Abs(marioBPhysics->Velocity.X) > 1) {
                        marioB->LastPushingFrame = f.Number;
                        marioBPhysics->Velocity.X = avgVelocityX;
                    }
                }
            }
        }

        private static void MarioMarioAttackStarman(Frame f, EntityRef attacker, EntityRef defender, bool fromRight, bool dropStars) {
            var attackerMario = f.Unsafe.GetPointer<MarioPlayer>(attacker);
            var defenderMario = f.Unsafe.GetPointer<MarioPlayer>(defender);

            if (defenderMario->CurrentPowerupState == PowerupState.MegaMushroom) {
                // Wait fuck-
                attackerMario->SetPlayerAction(PlayerAction.SoftKnockback, f, (dropStars ? 1 : 0) + (fromRight ? MarioPlayer.DropStarRight : 0), defender);
            } else {
                if (dropStars) {
                    defenderMario->Powerdown(f, defender, false);
                } else {
                    defenderMario->SetPlayerAction(PlayerAction.SoftKnockback, f, 0 + (!fromRight ? MarioPlayer.DropStarRight : 0), attacker);
                }
            }
        }

        private static void MarioMarioBlueShellStomp(Frame f, VersusStageData stage, EntityRef attacker, EntityRef defender, bool fromRight) {
            var defenderMario = f.Unsafe.GetPointer<MarioPlayer>(defender);
            var defenderPhysicsObject = f.Unsafe.GetPointer<PhysicsObject>(defender);
            var defenderPhysics = f.FindAsset(defenderMario->PhysicsAsset);

            FPVector2 raycastPosition = f.Unsafe.GetPointer<Transform2D>(defender)->Position + f.Unsafe.GetPointer<PhysicsCollider2D>(defender)->Shape.Centroid;

            bool goLeft = fromRight;
            if (goLeft && PhysicsObjectSystem.Raycast((FrameThreadSafe) f, stage, raycastPosition, FPVector2.Left, FP._0_25, out _)) {
                // Tile to the right. Force go left.
                goLeft = false;
            } else if (!goLeft && PhysicsObjectSystem.Raycast((FrameThreadSafe) f, stage, raycastPosition, FPVector2.Right, FP._0_25, out _)) {
                // Tile to the left. Force go right.
                goLeft = true;
            }

            defenderPhysicsObject->Velocity.X = defenderPhysics.WalkMaxVelocity[defenderPhysics.RunSpeedStage] * defenderPhysics.WalkBlueShellMultiplier * (goLeft ? -1 : 1);

            var attackerMario = f.Unsafe.GetPointer<MarioPlayer>(attacker);
            attackerMario->CheckEntityBounce(f, true);
        }

        private static void MarioMarioStomp(Frame f, EntityRef attacker, EntityRef defender, bool fromRight, bool dropStars) {
            var attackerMario = f.Unsafe.GetPointer<MarioPlayer>(attacker);
            var defenderMario = f.Unsafe.GetPointer<MarioPlayer>(defender);

            // Hit them from above
            bool killMini = attackerMario->HasActionFlags(ActionFlags.KillMiniStomp);

            if (defenderMario->CurrentPowerupState == PowerupState.MiniMushroom && killMini) {
                // We are big and our action is telling us we kill mini players
                defenderMario->SpawnStars(f, defender, attackerMario->StarStealCount);
                //defenderMario->Death(f, defender, false, false);
                if (killMini) {
                    defenderMario->SetPlayerAction(PlayerAction.Death, f, defenderMario->GetDeathArgs(f), defender, discardItem: true);
                }
            } else {
                // Normal knockbacks
                if (!dropStars) {
                    // Bounce
                    f.Events.MarioPlayerStompedByTeammate(f, defender);
                } else {
                    if (attackerMario->Action == PlayerAction.PropellerDrill) {
                        attackerMario->SetPlayerAction(PlayerAction.PropellerSpin, f, 1);
                    }
                    defenderMario->SetPlayerAction(attackerMario->StompAction, f, dropStars ? attackerMario->StarStealCount : 0 + (!fromRight ? MarioPlayer.DropStarRight : 0), attacker);
                }
            }
            attackerMario->CheckEntityBounce(f, true);
        }
        #endregion

        #region Signals
        public void OnRemoved(Frame f, EntityRef entity, Projectile* component) {
            if (f.Unsafe.TryGetPointer(component->Owner, out MarioPlayer* mario)) {
                mario->CurrentProjectiles--;
            }
        }

        public void OnGameStarting(Frame f) {
            // Respawn players
            var stage = f.FindAsset<VersusStageData>(f.Map.UserAsset);
            var filter = f.Filter<MarioPlayer>();
            filter.UseCulling = false;
            while (filter.NextUnsafe(out EntityRef entity, out MarioPlayer* mario)) {
                mario->Lives = (byte) f.Global->Rules.Lives;
                mario->SetPlayerAction(PlayerAction.Respawning, f);
            }
        }

        public void OnBobombExplodeEntity(Frame f, EntityRef bobomb, EntityRef entity) {
            if (f.Unsafe.TryGetPointer(entity, out MarioPlayer* mario)) {
                mario->Powerdown(f, entity, false);
            }
        }

        public void OnTryLiquidSplash(Frame f, EntityRef entity, EntityRef liquidEntity, QBoolean exit, bool* doSplash) {
            if (!f.Unsafe.TryGetPointer(entity, out MarioPlayer* mario)) {
                return;
            }

            var liquid = f.Unsafe.GetPointer<Liquid>(liquidEntity);
            *doSplash &= (!mario->IsDead || liquid->LiquidType == LiquidType.Water) && !f.Exists(mario->CurrentPipe);

            if (!exit && mario->CurrentPowerupState == PowerupState.MiniMushroom && mario->Action == PlayerAction.GroundPound) {
                *doSplash = false;
            }

            if (!exit) {
                switch (liquid->LiquidType) {
                case LiquidType.Water:
                    break;
                case LiquidType.Lava:
                    // Kill, fire death
                    mario->SetPlayerAction(PlayerAction.LavaDeath, f, mario->GetDeathArgs(f), discardItem: true);
                    break;
                case LiquidType.Poison:
                    // Kill, normal death
                    mario->SetPlayerAction(PlayerAction.Death, f, mario->GetDeathArgs(f), discardItem: true);
                    break;
                }
            }
        }

        public void OnEntityBumped(Frame f, EntityRef entity, FPVector2 tileWorldPosition, EntityRef bumper) {
            if (f.Unsafe.TryGetPointer(entity, out MarioPlayer* mario)) {
                if (mario->HasActionFlags(ActionFlags.Intangible)) {
                    return;
                }

                FPVector2 bumperPosition = f.Unsafe.GetPointer<Transform2D>(bumper)->Position;
                var marioTransform = f.Unsafe.GetPointer<Transform2D>(entity);

                QuantumUtils.UnwrapWorldLocations(f, marioTransform->Position, bumperPosition, out FPVector2 ourPos, out FPVector2 theirPos);
                bool onRight = ourPos.X > theirPos.X;

                mario->SetPlayerAction(PlayerAction.NormalKnockback, f, 1 + (!onRight ? MarioPlayer.DropStarRight : 0), bumper);
            }
        }

        public void OnBeforeInteraction(Frame f, EntityRef entity, bool* allowInteraction) {
            if (!f.Unsafe.TryGetPointer(entity, out MarioPlayer* mario)) {
                return;
            }

            *allowInteraction &= !(
                mario->IsDead 
                || f.Exists(mario->CurrentPipe) 
                || mario->MegaMushroomStartFrames > 0 
                || (mario->MegaMushroomEndFrames > 0 && mario->MegaMushroomStationaryEnd));
        }

        public void OnPlayerDisconnected(Frame f, PlayerRef player) {
            var marios = f.Filter<MarioPlayer>();
            while (marios.NextUnsafe(out EntityRef entity, out MarioPlayer* mario)) {
                if (mario->PlayerRef != player) {
                    continue;
                }

                mario->Disconnected = true;
                mario->PlayerRef = PlayerRef.None;
                mario->SetPlayerAction(PlayerAction.Death, f, 2);
            }
        }

        public void OnIceBlockBroken(Frame f, EntityRef brokenIceBlock, IceBlockBreakReason breakReason) {
            var iceBlock = f.Unsafe.GetPointer<IceBlock>(brokenIceBlock);
            EntityRef entity = iceBlock->Entity;
            if (!f.Unsafe.TryGetPointer(entity, out MarioPlayer* mario)
                || !f.Unsafe.TryGetPointer(entity, out PhysicsObject* physicsObject)) {
                return;
            }

            physicsObject->Velocity = FPVector2.Zero;
            f.Unsafe.GetPointer<Interactable>(entity)->ColliderDisabled = false;

            switch (breakReason) {
            case IceBlockBreakReason.BlockBump:
            case IceBlockBreakReason.HitWall:
            case IceBlockBreakReason.Fireball:
            case IceBlockBreakReason.Other:
                // Soft knockback, 1 star
                mario->SetPlayerAction(PlayerAction.SoftKnockback, f, 1 + (mario->FacingRight ? MarioPlayer.DropStarRight : 0), brokenIceBlock);
                break;

            case IceBlockBreakReason.Groundpounded:
                // Hard knockback, 2 stars
                mario->SetPlayerAction(PlayerAction.HardKnockback, f, 2 + (mario->FacingRight ? MarioPlayer.DropStarRight : 0), brokenIceBlock);
                break;

            case IceBlockBreakReason.Timer:
            default:
                // Do nothing
                mario->DamageInvincibilityFrames = 30;
                break;
            }
        }

        public void OnStageReset(Frame f, QBoolean full) {
            VersusStageData stage = null;
            var marios = f.Filter<MarioPlayer, Transform2D, PhysicsCollider2D>();
            while (marios.NextUnsafe(out EntityRef entity, out MarioPlayer* mario, out Transform2D* transform, out PhysicsCollider2D* physicsCollider)) {
                if (mario->CurrentPowerupState != PowerupState.MegaMushroom) {
                    continue;
                }

                if (stage == null) {
                    stage = f.FindAsset<VersusStageData>(f.Map.UserAsset);
                }

                Span<PhysicsObjectSystem.LocationTilePair> tiles = stackalloc PhysicsObjectSystem.LocationTilePair[64];
                int overlappingTiles = PhysicsObjectSystem.GetTilesOverlappingHitbox((FrameThreadSafe) f, transform->Position, physicsCollider->Shape, tiles, stage);

                for (int i = 0; i < overlappingTiles; i++) {
                    StageTile stageTile = f.FindAsset(tiles[i].Tile.Tile);
                    if (stageTile is IInteractableTile it) {
                        it.Interact(f, entity, InteractionDirection.Up, tiles[i].Position, tiles[i].Tile, out _);
                    }
                }
            }
        }

        public void OnEntityChangeUnderwaterState(Frame f, EntityRef entity, EntityRef liquid, QBoolean underwater) {
            if (!f.Unsafe.TryGetPointer(entity, out MarioPlayer* mario)
                || !f.Unsafe.TryGetPointer(entity, out PhysicsObject* physicsObject)) {
                return;
            }

            if (underwater /*&& mario->IsInKnockback*/) {
                mario->KnockbackTick = f.Number;
            }
            if (!underwater && physicsObject->Velocity.Y > 0 && !physicsObject->IsTouchingGround) {
                mario->SwimForceJumpTimer = 10;
            }
        }

        public void OnEntityFreeze(Frame f, EntityRef entity, EntityRef iceBlock) {
            if (!f.Unsafe.TryGetPointer(entity, out MarioPlayer* mario)) {
                return;
            }

            if (f.Unsafe.TryGetPointer(mario->HeldEntity, out Holdable* holdable)) {
                mario->HeldEntity = EntityRef.None;
                holdable->Holder = EntityRef.None;
            }
        }
        #endregion
    }
}