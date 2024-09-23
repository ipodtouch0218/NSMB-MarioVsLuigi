using Photon.Deterministic;
using Quantum.Collections;
using UnityEngine;
using static IInteractableTile;

namespace Quantum {

    public unsafe class MarioPlayerSystem : SystemMainThreadFilterStage<MarioPlayerSystem.Filter>, ISignalOnComponentRemoved<Projectile>, 
        ISignalOnGameStarting, ISignalOnBeforePhysicsCollision, ISignalOnBobombExplodeEntity, ISignalOnTryLiquidSplash, ISignalOnEntityBumped,
        ISignalOnBeforeInteraction {

        public struct Filter {
            public EntityRef Entity;
            public Transform2D* Transform;
            public MarioPlayer* MarioPlayer;
            public PhysicsObject* PhysicsObject;
            public PhysicsCollider2D* PhysicsCollider;
            public Freezable* Freezable;
        }

        public override void OnInit(Frame f) {
            InteractionSystem.RegisterInteraction<MarioPlayer, MarioPlayer>(OnMarioMarioInteraction);
            InteractionSystem.RegisterInteraction<MarioPlayer, Projectile>(OnMarioProjectileInteraction);
            InteractionSystem.RegisterInteraction<MarioPlayer, Coin>(OnMarioCoinInteraction);
        }

        public override void Update(Frame f, ref Filter filter, VersusStageData stage) {
            var player = filter.MarioPlayer->PlayerRef;
            Input input = default;
            if (player.IsValid) {
                input = *f.GetPlayerInput(player);
            }

            if (f.GetPlayerCommand(player) is CommandSpawnReserveItem) {
                SpawnReserveItem(f, ref filter);
            }

            var mario = filter.MarioPlayer;
            var physicsObject = filter.PhysicsObject;
            var physics = f.FindAsset(filter.MarioPlayer->PhysicsAsset);

            if (HandleDeathAndRespawning(f, ref filter, stage)) {
                return;
            }
            if (HandleMegaMushroom(f, ref filter, physics, stage)) {
                HandleHitbox(f, ref filter, physics);
                return;
            }
            HandlePowerups(f, ref filter, physics, input, stage);
            HandleBreakingBlocks(f, ref filter, physics, input, stage);
            HandleKnockback(f, ref filter);
            HandleCrouching(f, ref filter, physics, input);
            HandleGroundpound(f, ref filter, physics, input, stage);
            HandleSliding(f, ref filter, physics, input);
            HandleWalkingRunning(f, ref filter, physics, input);
            HandleSpinners(f, ref filter, stage);
            HandleJumping(f, ref filter, physics, input);
            HandleSwimming(f, ref filter, physics, input);
            HandleBlueShell(f, ref filter, physics, input, stage);
            HandleWallslide(f, ref filter, physics, input);
            HandleGravity(f, ref filter, physics, input);
            HandleTerminalVelocity(f, ref filter, physics, input);
            HandleFacingDirection(f, ref filter, physics, input);
            HandlePipes(f, ref filter, physics, stage);
            HandleHitbox(f, ref filter, physics);
            mario->WasTouchingGroundLastFrame = physicsObject->IsTouchingGround;
        }

        public void HandleWalkingRunning(Frame f, ref Filter filter, MarioPlayerPhysicsInfo physics, Input inputs) {

            var mario = filter.MarioPlayer;
            var physicsObject = filter.PhysicsObject;

            if (mario->WalljumpFrames > 0) {
                mario->WalljumpFrames--;
                if (mario->WalljumpFrames < 12 && (physicsObject->IsTouchingLeftWall || physicsObject->IsTouchingRightWall)) {
                    mario->WalljumpFrames = 0;
                } else {
                    physicsObject->Velocity.X = physics.WalljumpHorizontalVelocity * (mario->FacingRight ? 1 : -1);
                    return;
                }
            }

            if (mario->GroundpoundStandFrames > 0) {
                if (!physicsObject->IsTouchingGround) {
                    mario->GroundpoundStandFrames = 0;
                } else {
                    mario->GroundpoundStandFrames--;
                    return;
                }
            }

            if (mario->IsGroundpounding || mario->IsInShell || mario->CurrentPipe.IsValid || mario->JumpLandingFrames > 0 || !(mario->WalljumpFrames <= 0 || physicsObject->IsTouchingGround || physicsObject->Velocity.Y < 0)) {
                return;
            }

            if (!physicsObject->IsTouchingGround) {
                mario->IsSkidding = false;
            }

            bool run = (inputs.Sprint.IsDown || mario->CurrentPowerupState == PowerupState.MegaMushroom || mario->IsPropellerFlying) & !mario->IsSpinnerFlying;
            int maxStage;
            if (mario->IsInWater) {
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
            if (mario->IsInWater) {
                if (physicsObject->IsTouchingGround) {
                    maxArray = mario->CurrentPowerupState == PowerupState.BlueShell ? physics.SwimWalkShellMaxVelocity : physics.SwimWalkMaxVelocity;
                } else {
                    maxArray = mario->CurrentPowerupState == PowerupState.BlueShell ? physics.SwimShellMaxVelocity : physics.SwimMaxVelocity;
                }
            }
            int stage = mario->GetSpeedStage(physicsObject, physics);

            FP acc;
            if (mario->IsInWater) {
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
            FP sign = FPMath.Sign(xVel);
            bool uphill = FPMath.Abs(physicsObject->FloorAngle) > physics.SlideMinimumAngle && FPMath.Sign(physicsObject->FloorAngle) != sign;

            if (!physicsObject->IsTouchingGround) {
                mario->FastTurnaroundFrames = 0;
            }

            if (mario->FastTurnaroundFrames > 0) {
                physicsObject->Velocity.X = 0;
                if (QuantumUtils.Decrement(ref mario->FastTurnaroundFrames)) {
                    mario->IsTurnaround = true;
                }
            } else if (mario->IsTurnaround) {
                mario->IsTurnaround = physicsObject->IsTouchingGround && !mario->IsCrouching && xVelAbs < physics.WalkMaxVelocity[1] && !physicsObject->IsTouchingLeftWall && !physicsObject->IsTouchingRightWall;
                mario->IsSkidding = mario->IsTurnaround;

                physicsObject->Velocity.X += (physics.FastTurnaroundAcceleration * (mario->FacingRight ? -1 : 1) * f.DeltaTime);

            } else if ((inputs.Left ^ inputs.Right)
                       && (!mario->IsCrouching || (mario->IsCrouching && !physicsObject->IsTouchingGround && mario->CurrentPowerupState != PowerupState.BlueShell))
                       && !mario->IsInKnockback
                       && !mario->IsSliding) {

                // We can walk here
                bool reverse = physicsObject->Velocity.X != 0 && ((inputs.Left ? 1 : -1) == sign);

                // Check that we're not going above our limit
                FP max = maxArray[maxStage] + CalculateSlopeMaxSpeedOffset(FPMath.Abs(physicsObject->FloorAngle) * (uphill ? 1 : -1));
                FP maxAcceleration = FPMath.Abs(max - xVelAbs) * f.UpdateRate;
                acc = FPMath.Clamp(acc, -maxAcceleration, maxAcceleration);
                if (xVelAbs > max) {
                    acc = -acc;
                }

                if (reverse) {
                    mario->IsTurnaround = false;
                    if (physicsObject->IsTouchingGround) {
                        if (!mario->IsInWater && xVelAbs >= physics.SkiddingMinimumVelocity && !mario->HeldEntity.IsValid && mario->CurrentPowerupState != PowerupState.MegaMushroom) {
                            mario->IsSkidding = true;
                            mario->FacingRight = sign == 1;
                        }

                        if (mario->IsSkidding) {
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
                                mario->SlowTurnaroundFrames = (byte) Mathf.Clamp(mario->SlowTurnaroundFrames + 1, 0,
                                    physics.SlowTurnaroundAcceleration.Length - 1);
                                acc = mario->CurrentPowerupState == PowerupState.MegaMushroom
                                    ? physics.SlowTurnaroundMegaAcceleration[mario->SlowTurnaroundFrames]
                                    : physics.SlowTurnaroundAcceleration[mario->SlowTurnaroundFrames];
                            }
                        }
                    } else {
                        // TODO: change 0.85 to a constant?
                        acc = physics.WalkAcceleration[0] * FP.FromString("0.85");
                    }
                } else {
                    mario->SlowTurnaroundFrames = 0;
                    mario->IsSkidding &= !mario->IsTurnaround;
                }

                int direction = inputs.Left ? -1 : 1;
                FP newX = xVel + acc * f.DeltaTime * direction;

                if ((xVel < max && newX > max) || (xVel > -max && newX < -max)) {
                    newX = FPMath.Clamp(newX, -max, max);
                }

                if (mario->IsSkidding && !mario->IsTurnaround && (FPMath.Sign(newX) != sign || xVelAbs < FP._0_05)) {
                    // Turnaround
                    mario->FastTurnaroundFrames = 10;
                    newX = 0;
                }

                physicsObject->Velocity.X = newX;

            } else if (physicsObject->IsTouchingGround || mario->IsInWater) {
                // Not holding anything, sliding, or holding both directions. decelerate
                mario->IsSkidding = false;
                mario->IsTurnaround = false;

                FP angle = FPMath.Abs(physicsObject->FloorAngle);
                if (mario->IsInWater) {
                    acc = -physics.SwimDeceleration;
                } else if (mario->IsSliding) {
                    if (angle > physics.SlideMinimumAngle) {
                        // Uphill / downhill
                        acc = (angle > 30 ? physics.SlideFastAcceleration : physics.SlideSlowAcceleration) * (uphill ? -1 : 1);
                    } else {
                        // Flat ground
                        acc = -physics.WalkAcceleration[0];
                    }
                } else if (physicsObject->IsOnSlipperyGround) {
                    acc = -physics.WalkButtonReleaseIceDeceleration[stage];
                } else if (mario->IsInKnockback) {
                    acc = -physics.KnockbackDeceleration;
                } else {
                    acc = -physics.WalkButtonReleaseDeceleration;
                }

                FP newX = xVel + acc * f.DeltaTime * sign;
                FP target = (angle > 30 && physicsObject->IsOnSlideableGround) ? FPMath.Sign(physicsObject->FloorAngle) * physics.WalkMaxVelocity[0] : 0;
                if ((sign == -1) ^ (newX <= target)) {
                    newX = target;
                }

                if (mario->IsSliding) {
                    newX = FPMath.Clamp(newX, -physics.SlideMaxVelocity, physics.SlideMaxVelocity);
                }

                physicsObject->Velocity.X = newX;

                if (newX != 0) {
                    mario->FacingRight = newX > 0;
                }
            }

            bool wasInShell = mario->IsInShell;
            mario->IsInShell |= mario->CurrentPowerupState == PowerupState.BlueShell && !mario->IsSliding && physicsObject->IsTouchingGround
                                && run && !mario->HeldEntity.IsValid
                                && FPMath.Abs(physicsObject->Velocity.X) >= physics.WalkMaxVelocity[physics.RunSpeedStage] * FP.FromString("0.9")
                                && (physicsObject->Velocity.X > 0) == mario->FacingRight;

            mario->IsCrouching &= !mario->IsSliding;

            if (!wasInShell && mario->IsInShell) {
                f.Events.MarioPlayerCrouched(f, filter.Entity, *mario);
            }
        }

        private static FP CalculateSlopeMaxSpeedOffset(FP floorAngle) {
            // TODO remove magic constant
            return FP.FromString("-0.0304687") * floorAngle;
        }

        private void HandleJumping(Frame f, ref Filter filter, MarioPlayerPhysicsInfo physics, Input inputs) {

            var mario = filter.MarioPlayer;
            var physicsObject = filter.PhysicsObject;

            if (inputs.Jump.WasPressed) {
                // Jump buffer
                mario->JumpBufferFrames = physics.JumpBufferFrames;
            }

            if (physicsObject->IsTouchingGround) {
                // Coyote Time
                mario->CoyoteTimeFrames = physics.CoyoteTimeFrames;
            }

            if (!mario->WasTouchingGroundLastFrame && physicsObject->IsTouchingGround) {
                // Landed Frame
                mario->LandedFrame = f.Number;
                if (mario->PreviousJumpState != JumpState.None && mario->PreviousJumpState == mario->JumpState) {
                    mario->JumpState = JumpState.None;
                }
                mario->PreviousJumpState = mario->JumpState;
            }

            bool doJump = (mario->JumpBufferFrames > 0 && (physicsObject->IsTouchingGround || mario->CoyoteTimeFrames > 0)) || (!mario->IsInWater && mario->SwimExitForceJump);

            QuantumUtils.Decrement(ref mario->CoyoteTimeFrames);
            QuantumUtils.Decrement(ref mario->JumpBufferFrames);

            if (!mario->DoEntityBounce && (mario->IsInWater || !doJump || mario->IsInKnockback || (mario->CurrentPowerupState == PowerupState.MegaMushroom && mario->JumpState == JumpState.SingleJump) || mario->IsWallsliding)) {
                return;
            }

            if (f.Unsafe.TryGetPointer(mario->CurrentSpinner, out Spinner* spinner) && spinner->ArmPosition <= FP._0_75 && !f.Exists(mario->HeldEntity)) {
                // Jump of spinner
                physicsObject->Velocity.Y = physics.SpinnerLaunchVelocity;
                
                mario->IsSkidding = false;
                mario->IsTurnaround = false;
                mario->IsSliding = false;
                mario->WallslideEndFrames = 0;
                mario->IsGroundpounding = false;
                mario->GroundpoundStartFrames = 0;
                mario->IsDrilling = false;
                mario->IsSpinnerFlying = true;
                mario->IsPropellerFlying = false;
                mario->SwimExitForceJump = false;
                mario->JumpBufferFrames = 0;
                mario->WasTouchingGroundLastFrame = false;
                physicsObject->IsTouchingGround = false;

                // Disable koyote time
                mario->CoyoteTimeFrames = 0;

                f.Events.MarioPlayerUsedSpinner(f, filter.Entity, mario->CurrentSpinner);
                mario->CurrentSpinner = EntityRef.None;
                return;
            }

            bool topSpeed = FPMath.Abs(physicsObject->Velocity.X) >= (physics.WalkMaxVelocity[physics.RunSpeedStage] - FP._0_10);
            bool canSpecialJump = topSpeed && !inputs.Down.IsDown && (doJump || (mario->DoEntityBounce && inputs.Jump.IsDown)) && mario->JumpState != JumpState.None && !mario->IsSpinnerFlying && !mario->IsPropellerFlying && ((f.Number - mario->LandedFrame < 12) || mario->DoEntityBounce) && !mario->HeldEntity.IsValid && mario->JumpState != JumpState.TripleJump && !mario->IsCrouching && !mario->IsInShell && (physicsObject->Velocity.X < 0 != mario->FacingRight) /* && !Runner.GetPhysicsScene2D().Raycast(body.Position + new Vector2(0, 0.1f), Vector2.up, 1f, Layers.MaskSolidGround) */;

            mario->IsSkidding = false;
            mario->IsTurnaround = false;
            mario->IsSliding = false;
            mario->WallslideEndFrames = 0;
            mario->IsGroundpounding = false;
            mario->GroundpoundStartFrames = 0;
            mario->IsDrilling = false;
            mario->IsSpinnerFlying &= mario->DoEntityBounce;
            mario->IsPropellerFlying &= mario->DoEntityBounce;
            mario->SwimExitForceJump = false;
            mario->JumpBufferFrames = 0;
            mario->WasTouchingGroundLastFrame = false;
            physicsObject->IsTouchingGround = false;

            // Disable koyote time
            mario->CoyoteTimeFrames = 0;

            PowerupState effectiveState = mario->CurrentPowerupState;
            if (effectiveState == PowerupState.MegaMushroom && mario->DoEntityBounce) {
                effectiveState = PowerupState.NoPowerup;
            }

            // TODO: fix magic
            FP alpha = FPMath.Clamp01(FPMath.Abs(physicsObject->Velocity.X) - physics.WalkMaxVelocity[1] + (physics.WalkMaxVelocity[1] * FP._0_50));
            FP newY = effectiveState switch {
                PowerupState.MegaMushroom => physics.JumpMegaVelocity + FPMath.Lerp(0, physics.JumpMegaSpeedBonusVelocity, alpha),
                PowerupState.MiniMushroom => physics.JumpMiniVelocity + FPMath.Lerp(0, physics.JumpMiniSpeedBonusVelocity, alpha),
                _ => physics.JumpVelocity + FPMath.Lerp(0, physics.JumpSpeedBonusVelocity, alpha),
            };
            if (FPMath.Sign(physicsObject->Velocity.X) == FPMath.Sign(physicsObject->FloorAngle)) {
                // TODO: what.
                newY += FPMath.Abs(physicsObject->FloorAngle) * FP._0_01 * alpha;
            }

            if (canSpecialJump && mario->JumpState == JumpState.SingleJump) {
                //Double jump
                mario->JumpState = JumpState.DoubleJump;
            } else if (canSpecialJump && mario->JumpState == JumpState.DoubleJump) {
                //Triple Jump
                mario->JumpState = JumpState.TripleJump;
                newY += physics.JumpTripleBonusVelocity;
            } else {
                //Normal jump
                mario->JumpState = JumpState.SingleJump;
            }

            if (mario->IsInWater) {
                newY *= FP._0_33;
            }
            physicsObject->Velocity.Y = newY;

            f.Events.MarioPlayerJumped(f, filter.Entity, *filter.MarioPlayer, mario->JumpState, mario->DoEntityBounce);
            mario->DoEntityBounce = false;
        }

        public void HandleGravity(Frame f, ref Filter filter, MarioPlayerPhysicsInfo physics, Input inputs) {

            var mario = filter.MarioPlayer;
            var physicsObject = filter.PhysicsObject;

            if (physicsObject->IsTouchingGround) {
                physicsObject->Gravity = FPVector2.Up * physics.GravityAcceleration[0];
                return;
            }

            FP gravity;

            // Slow-rise check
            if (!mario->IsInWater && (mario->IsSpinnerFlying || mario->IsPropellerFlying)) {
                gravity = mario->IsDrilling ? physics.GravityAcceleration[^1] : physics.GravityFlyingAcceleration;
            } else if ((mario->IsGroundpounding && !mario->IsInWater) || physicsObject->IsTouchingGround || mario->CoyoteTimeFrames > 0) {
                gravity = mario->GroundpoundStartFrames > 0 ? physics.GravityGroundpoundStart : physics.GravityAcceleration[^1];
            } else {
                int stage = mario->GetGravityStage(*physicsObject, physics);
                bool mega = mario->CurrentPowerupState == PowerupState.MegaMushroom;
                bool mini = mario->CurrentPowerupState == PowerupState.MiniMushroom;

                FP[] accArr = mario->IsInWater ? physics.GravitySwimmingAcceleration : (mega ? physics.GravityMegaAcceleration : (mini ? physics.GravityMiniAcceleration : physics.GravityAcceleration));
                FP acc = accArr[stage];
                if (stage == 0) {
                    acc = (inputs.Jump.IsDown || mario->IsInWater) ? accArr[0] : accArr[^1];
                }

                gravity = acc;
            }

            physicsObject->Gravity = FPVector2.Up * gravity;
        }

        public void HandleTerminalVelocity(Frame f, ref Filter filter, MarioPlayerPhysicsInfo physics, Input inputs) {

            var mario = filter.MarioPlayer;
            var physicsObject = filter.PhysicsObject;

            FP maxWalkSpeed = physics.WalkMaxVelocity[physics.WalkSpeedStage];
            FP terminalVelocity;

            if (mario->IsInWater && !(mario->IsGroundpounding || mario->IsDrilling)) {
                terminalVelocity = inputs.Jump.IsDown ? physics.SwimTerminalVelocityButtonHeld : physics.SwimTerminalVelocity;
                physicsObject->Velocity.Y = FPMath.Min(physicsObject->Velocity.Y, physics.SwimMaxVerticalVelocity);
            } else if (mario->IsSpinnerFlying) {
                terminalVelocity = mario->IsDrilling ? physics.TerminalVelocityDrilling : physics.TerminalVelocityFlying;
            } else if (mario->IsPropellerFlying) {
                if (mario->IsDrilling) {
                    terminalVelocity = physics.TerminalVelocityDrilling;
                    physicsObject->Velocity.X = FPMath.Clamp(physicsObject->Velocity.X, -maxWalkSpeed * FP._0_25, maxWalkSpeed * FP._0_25);
                } else {
                    FP remainingTime = mario->PropellerLaunchFrames / 60;
                    // TODO remove magic number
                    FP htv = maxWalkSpeed + (FP.FromString("1.18") * (remainingTime * 2));
                    terminalVelocity = mario->PropellerSpinFrames > 0 ? physics.TerminalVelocityPropellerSpin : physics.TerminalVelocityPropeller;
                    physicsObject->Velocity.X = FPMath.Clamp(physicsObject->Velocity.X, -htv, htv);
                }
            } else if (mario->IsWallsliding) {
                terminalVelocity = physics.TerminalVelocityWallslide;
            } else if (mario->IsGroundpounding) {
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

        public void HandleWallslide(Frame f, ref Filter filter, MarioPlayerPhysicsInfo physics, Input inputs) {

            var mario = filter.MarioPlayer;
            var physicsObject = filter.PhysicsObject;

            if (mario->IsInShell || mario->IsGroundpounding || mario->IsCrouching || mario->IsDrilling || mario->IsSpinnerFlying || mario->IsInWater || mario->IsInKnockback) {
                return;
            }

            FPVector2 currentWallDirection;
            if (mario->WallslideLeft) {
                currentWallDirection = FPVector2.Left;
            } else if (mario->WallslideRight) {
                currentWallDirection = FPVector2.Right;
            } else if (inputs.Left.IsDown ^ inputs.Right.IsDown) {
                if (inputs.Left.IsDown) {
                    currentWallDirection = FPVector2.Left;
                } else if (inputs.Right.IsDown) {
                    currentWallDirection = FPVector2.Right;
                } else {
                    return;
                }
            } else {
                return;
            }

            HandleWallslideStopChecks(filter, inputs, currentWallDirection);

            if (mario->WallslideEndFrames > 0 && QuantumUtils.Decrement(ref mario->WallslideEndFrames)) {
                mario->WallslideRight = false;
                mario->WallslideLeft = false;
                return;
            }

            if (mario->IsWallsliding) {
                // Walljump check
                physicsObject->Velocity.X = FPMath.Clamp(physicsObject->Velocity.X, -FP._0_25, FP._0_25);
                mario->FacingRight = mario->WallslideLeft;
                if (mario->JumpBufferFrames > 0 && mario->WalljumpFrames == 0 /* && !BounceJump */) {
                    // Perform walljump
                    physicsObject->Velocity = new(physics.WalljumpHorizontalVelocity * (mario->WallslideLeft ? 1 : -1), mario->CurrentPowerupState == PowerupState.MiniMushroom ? physics.WalljumpMiniVerticalVelocity : physics.WalljumpVerticalVelocity);
                    mario->JumpState = JumpState.SingleJump;
                    physicsObject->IsTouchingGround = false;
                    mario->DoEntityBounce = false;
                    // timeSinceLastBumpSound = 0;

                    f.Events.MarioPlayerWalljumped(f, filter.Entity, *filter.MarioPlayer, filter.Transform->Position, mario->WallslideRight);
                    mario->WalljumpFrames = 16;
                    mario->WallslideRight = false;
                    mario->WallslideLeft = false;
                    mario->WallslideEndFrames = 0;
                    mario->JumpBufferFrames = 0;
                }
            } else if (physicsObject->IsTouchingLeftWall || physicsObject->IsTouchingRightWall) {
                // Walljump starting check
                bool canWallslide = !mario->IsInShell && physicsObject->Velocity.Y < -FP._0_10 && !mario->IsGroundpounding && !physicsObject->IsTouchingGround && !mario->HeldEntity.IsValid && mario->CurrentPowerupState != PowerupState.MegaMushroom && !mario->IsSpinnerFlying && !mario->IsDrilling && !mario->IsCrouching && !mario->IsSliding && !mario->IsInKnockback && mario->PropellerLaunchFrames == 0;
                if (!canWallslide) {
                    return;
                }

                // Check 1
                if (mario->WalljumpFrames > 0) {
                    return;
                }

                // Check 2
                if (mario->WallslideEndFrames > 0) {
                    return;
                }

                // Check 4: already handled
                // Check 5.2: already handled

                //Check 6
                if (mario->IsCrouching) {
                    return;
                }

                // Check 8
                if (!((currentWallDirection == FPVector2.Right && mario->FacingRight) || (currentWallDirection == FPVector2.Left && !mario->FacingRight))) {
                    return;
                }

                // Start wallslide
                mario->WallslideRight = currentWallDirection == FPVector2.Right && physicsObject->IsTouchingRightWall;
                mario->WallslideLeft = currentWallDirection == FPVector2.Left && physicsObject->IsTouchingLeftWall;
                mario->WallslideEndFrames = 0;

                if (mario->IsWallsliding) {
                    mario->IsPropellerFlying = false;
                }
            }
        }

        private static readonly FPVector2 WallslideLowerHeightOffset = new(0, FP._0_20);
        private void HandleWallslideStopChecks(Filter filter, Input inputs, FPVector2 wallDirection) {
            var mario = filter.MarioPlayer;
            var physicsObject = filter.PhysicsObject;

            // TODO bool floorCheck = !Runner.GetPhysicsScene2D().Raycast(body.Position, Vector2.down, 0.1f, Layers.MaskAnyGround);
            bool moveDownCheck = physicsObject->Velocity.Y < 0;
            // TODO bool heightLowerCheck = Runner.GetPhysicsScene2D().Raycast(body.Position + WallSlideLowerHeightOffset, wallDirection, MainHitbox.size.x * 2, Layers.MaskSolidGround);
            if (physicsObject->IsTouchingGround || !moveDownCheck /* || !heightLowerCheck */) {
                mario->WallslideRight = false;
                mario->WallslideLeft = false;
                mario->WallslideEndFrames = 0;
                return;
            }

            if ((wallDirection == FPVector2.Left && (!inputs.Left.IsDown || !physicsObject->IsTouchingLeftWall)) || (wallDirection == FPVector2.Right && (!inputs.Right.IsDown || !physicsObject->IsTouchingRightWall))) {
                if (mario->WallslideEndFrames == 0) {
                    mario->WallslideEndFrames = 16;
                }
            } else {
                mario->WallslideEndFrames = 0;
            }
        }


        public void HandleFacingDirection(Frame f, ref Filter filter, MarioPlayerPhysicsInfo physics, Input inputs) {
            var mario = filter.MarioPlayer;
            var physicsObject = filter.PhysicsObject;

            if (f.Exists(mario->CurrentPipe) || mario->IsInShell || mario->IsInKnockback || (mario->IsGroundpounding && !physicsObject->IsTouchingGround)) {
                return;
            }

            bool rightOrLeft = (inputs.Right.IsDown ^ inputs.Left.IsDown);

            if (mario->WalljumpFrames > 0) {
                mario->FacingRight = physicsObject->Velocity.X > 0;
            } else if (!mario->IsInShell && !mario->IsSliding && !mario->IsSkidding && !mario->IsInKnockback && !mario->IsTurnaround) {
                if (rightOrLeft) {
                    mario->FacingRight = inputs.Right.IsDown;
                }
            } else if (mario->MegaMushroomStartFrames == 0 && mario->MegaMushroomEndFrames == 0 && !mario->IsSkidding && !mario->IsTurnaround) {
                if (mario->IsInKnockback || (physicsObject->IsTouchingGround && mario->CurrentPowerupState != PowerupState.MegaMushroom && FPMath.Abs(physicsObject->Velocity.X) > FP._0_05 && !mario->IsCrouching)) {
                    mario->FacingRight = physicsObject->Velocity.X > 0;
                } else if ((!mario->IsInShell || mario->MegaMushroomStartFrames > 0) && (rightOrLeft)) {
                    mario->FacingRight = inputs.Right.IsDown;
                }
                if (!mario->IsInShell && ((FPMath.Abs(physicsObject->Velocity.X) < FP._0_50 && mario->IsCrouching) || physicsObject->IsOnSlipperyGround) && (rightOrLeft)) {
                    mario->FacingRight = inputs.Right.IsDown;
                }
            }
        }

        public void HandleCrouching(Frame f, ref Filter filter, MarioPlayerPhysicsInfo physics, Input inputs) {
            var mario = filter.MarioPlayer;
            var physicsObject = filter.PhysicsObject;

            if (mario->WasTouchingGroundLastFrame && !physicsObject->IsTouchingGround) {
                if (physicsObject->Velocity.Y < FP._0_10) {
                     physicsObject->Velocity.Y = mario->IsCrouching ? physics.CrouchOffEdgeVelocity : 0;
                }
            }

            // Can't crouch while sliding, flying, or mega.
            if (mario->IsSliding || mario->IsPropellerFlying || mario->IsSpinnerFlying || mario->IsInKnockback || mario->CurrentPowerupState == PowerupState.MegaMushroom) {
                mario->IsCrouching = false;
                return;
            }

            // TODO: magic number
            if (!mario->IsCrouching && mario->IsInWater && FPMath.Abs(physicsObject->Velocity.X) > FP._0_03) {
                return;
            }

            bool wasCrouching = mario->IsCrouching;
            mario->IsCrouching = 
                (
                    (physicsObject->IsTouchingGround && inputs.Down.IsDown && !mario->IsGroundpounding && !mario->IsSliding) 
                    || (!physicsObject->IsTouchingGround && (inputs.Down.IsDown || (physicsObject->Velocity.Y > 0 && mario->CurrentPowerupState != PowerupState.BlueShell)) && mario->IsCrouching && !mario->IsInWater)
                    || (mario->IsCrouching && ForceCrouchCheck(f, ref filter, physics))
                ) 
                && !mario->HeldEntity.IsValid 
                && !mario->IsInShell;

            if (!wasCrouching && mario->IsCrouching) {
                f.Events.MarioPlayerCrouched(f, filter.Entity, *mario);
            }
        }

        public static bool ForceCrouchCheck(Frame f, ref Filter filter, MarioPlayerPhysicsInfo physics) {
            /* TODO
            // Janky fortress ceiling check, mate
            if (mario->CurrentPowerupState == PowerupState.BlueShell && mario->IsOnGround && SceneManager.GetActiveScene().buildIndex != 4) {
                return false;
            }
            */

            var mario = filter.MarioPlayer;
            var collider = filter.PhysicsCollider;
            var transform = filter.Transform;

            if (mario->CurrentPowerupState <= PowerupState.MiniMushroom) {
                return false;
            }

            Shape2D shape = collider->Shape;
            shape.Box.Extents = new(FP.FromString("0.175"), physics.LargeHitboxHeight / 2);
            shape.Centroid.Y = shape.Box.Extents.Y;

            return PhysicsObjectSystem.BoxInsideTile(f, transform->Position, shape);
        }

        public void HandleGroundpound(Frame f, ref Filter filter, MarioPlayerPhysicsInfo physics, Input inputs, VersusStageData stage) {
            var mario = filter.MarioPlayer;
            var physicsObject = filter.PhysicsObject;

            QuantumUtils.Decrement(ref mario->GroundpoundCooldownFrames);
            QuantumUtils.Decrement(ref mario->PropellerDrillCooldown);

            if (inputs.Down.WasPressed || (mario->IsPropellerFlying && inputs.Down.IsDown)) {
                TryStartGroundpound(f, ref filter, physics, inputs);
            }

            HandleGroundpoundStartAnimation(ref filter, physics);
            HandleGroundpoundBlockCollision(f, ref filter, physics, stage);

            if (mario->IsInWater && (mario->IsGroundpounding || mario->IsDrilling)) {
                physicsObject->Velocity.Y += physics.SwimGroundpoundDeceleration * f.DeltaTime;
                if (physicsObject->Velocity.Y >= physics.SwimTerminalVelocityButtonHeld) {
                    mario->IsGroundpounding = false;
                    mario->IsSpinnerFlying = false;
                    mario->IsPropellerFlying = false;
                    mario->IsDrilling = false;
                }
            } else if (mario->IsGroundpounding) {
                if (physicsObject->IsTouchingGround && !inputs.Down.IsDown) {
                    // Cancel from being grounded
                    mario->GroundpoundStandFrames = 15;
                    mario->IsGroundpounding = false;
                } else if (inputs.Up.WasPressed && mario->GroundpoundStartFrames == 0) {
                    // Cancel from hitting "up"
                    mario->GroundpoundCooldownFrames = 12;
                    mario->IsGroundpounding = false;
                    mario->IsGroundpoundActive = false;
                }
            }
            
            // Bodge: i can't find the desync...
            mario->IsGroundpoundActive &= mario->IsGroundpounding;
        }

        private void TryStartGroundpound(Frame f, ref Filter filter, MarioPlayerPhysicsInfo physics, Input inputs) {

            var mario = filter.MarioPlayer;
            var physicsObject = filter.PhysicsObject;

            if (physicsObject->IsTouchingGround || mario->IsInKnockback || mario->IsGroundpounding || mario->IsDrilling
                || mario->HeldEntity.IsValid || mario->IsCrouching || mario->IsSliding || mario->IsInShell
                || mario->IsWallsliding || mario->GroundpoundCooldownFrames > 0 || mario->IsInWater
                || f.Exists(mario->CurrentPipe)) {
                return;
            }

            if (!mario->IsPropellerFlying && !mario->IsSpinnerFlying && (inputs.Left.IsDown || inputs.Right.IsDown)) {
                return;
            }

            if (mario->IsSpinnerFlying) {
                // Start drill
                if (physicsObject->Velocity.Y < 0) {
                    mario->IsDrilling = true;
                    mario->IsGroundpoundActive = true;
                    physicsObject->Velocity.X = 0;
                }
            } else if (mario->IsPropellerFlying) {
                // Start propeller drill
                if (mario->PropellerLaunchFrames < 12 && physicsObject->Velocity.Y < 0 && mario->PropellerDrillCooldown == 0) {
                    mario->IsDrilling = true;
                    mario->PropellerLaunchFrames = 0;
                    mario->IsGroundpoundActive = true;
                    mario->PropellerDrillCooldown = 12;
                }
            } else {
                // Start groundpound
                // Check if high enough above ground
                /* TODO
                if (Runner.GetPhysicsScene().BoxCast(body.Position, WorldHitboxSize * Vector2.right * 0.5f, Vector3.down, out _, Quaternion.identity, 0.15f * (State == Enums.PowerupState.MegaMushroom ? 2.5f : 1), Layers.MaskAnyGround)) {
                    return;
                }
                */

                mario->WallslideLeft = false;
                mario->WallslideRight = false;
                mario->IsGroundpounding = true;
                mario->JumpState = JumpState.None;
                mario->IsGroundpoundActive = true;
                mario->IsSliding = false;
                physicsObject->Velocity = physics.GroundpoundStartVelocity;
                mario->GroundpoundStartFrames = mario->CurrentPowerupState == PowerupState.MegaMushroom ? physics.GroundpoundStartMegaFrames : physics.GroundpoundStartFrames;

                f.Events.MarioPlayerGroundpoundStarted(f, filter.Entity, *mario);
            }
        }

        private void HandleGroundpoundStartAnimation(ref Filter filter, MarioPlayerPhysicsInfo physics) {
            var mario = filter.MarioPlayer;
            var physicsObject = filter.PhysicsObject;

            if (!mario->IsGroundpounding || mario->GroundpoundStartFrames == 0) {
                return;
            }

            physicsObject->Velocity = --mario->GroundpoundStartFrames switch {
                   0 => FPVector2.Up * physics.TerminalVelocityGroundpound,
                >= 4 => physics.GroundpoundStartVelocity,
                   _ => FPVector2.Zero
            };
        }

        private void HandleGroundpoundBlockCollision(Frame f, ref Filter filter, MarioPlayerPhysicsInfo physics, VersusStageData stage) {
            var mario = filter.MarioPlayer;
            var physicsObject = filter.PhysicsObject;

            if (!(physicsObject->IsTouchingGround && ((mario->IsGroundpounding && mario->IsGroundpoundActive) || mario->IsDrilling))) {
                return;
            }

            if (!mario->IsDrilling) {
                f.Events.MarioPlayerGroundpounded(f, filter.Entity, *mario);
            }

            bool interactedAny = false;
            bool continueGroundpound = true;
            bool? playBumpSound = null;
            QList<PhysicsContact> contacts = f.ResolveList(physicsObject->Contacts);
            foreach (var contact in contacts) {
                if (FPVector2.Dot(contact.Normal, FPVector2.Up) < FP._0_33 * 2) {
                    continue;
                }

                // Floor tiles.
                var tileInstance = stage.GetTileRelative(f, contact.TileX, contact.TileY);
                StageTile tile = f.FindAsset(tileInstance.Tile);
                if (tile is IInteractableTile it) {
                    continueGroundpound &= it.Interact(f, filter.Entity, InteractionDirection.Down,
                        new Vector2Int(contact.TileX, contact.TileY), tileInstance, out bool tempPlayBumpSound);
                    interactedAny = true;

                    playBumpSound &= (playBumpSound ?? true) & tempPlayBumpSound;
                }
            }

            if (playBumpSound ?? false) {
                f.Events.PlayBumpSound(f, filter.Entity);
            }

            continueGroundpound &= interactedAny;
            mario->IsGroundpoundActive &= continueGroundpound;

            if (!mario->IsGroundpoundActive && physicsObject->IsOnSlideableGround && !mario->IsInShell && FPMath.Abs(physicsObject->FloorAngle) >= physics.SlideMinimumAngle && physicsObject->IsOnSlideableGround) {
                mario->IsGroundpounding = false;
                mario->IsSliding = true;
                physicsObject->Velocity.X = FPMath.Sign(physicsObject->FloorAngle) * physics.SlideMaxVelocity;
            }

            if (mario->IsDrilling) {
                mario->IsSpinnerFlying &= continueGroundpound;
                mario->IsPropellerFlying &= continueGroundpound;
                mario->IsDrilling = continueGroundpound;
                if (continueGroundpound) {
                    physicsObject->IsTouchingGround = false;
                }
            }
        }

        public void HandleKnockback(Frame f, ref Filter filter) {
            var mario = filter.MarioPlayer;
            var physicsObject = filter.PhysicsObject;

            if (mario->IsInKnockback) {
                if (mario->DoEntityBounce) {
                    mario->ResetKnockback(f, filter.Entity);
                    return;
                }

                mario->WallslideLeft = false;
                mario->WallslideRight = false;
                mario->IsCrouching = false;
                mario->IsInShell = false;
                // physicsObject->Velocity -= physicsObject->Velocity * (f.DeltaTime * 2);

                int framesInKnockback = f.Number - mario->KnockbackTick;

                if ((mario->IsInWater && framesInKnockback > 90) 
                    || (!mario->IsInWater && physicsObject->IsTouchingGround && FPMath.Abs(physicsObject->Velocity.X) < FP._0_33 && framesInKnockback > 30)
                    || (!mario->IsInWater && mario->IsInWeakKnockback && framesInKnockback > 30)) {
                    
                    mario->ResetKnockback(f, filter.Entity);
                }
            }
        }

        public void HandleBlueShell(Frame f, ref Filter filter, MarioPlayerPhysicsInfo physics, Input inputs, VersusStageData stage) {
            var mario = filter.MarioPlayer;
            var physicsObject = filter.PhysicsObject;

            mario->IsInShell &= inputs.Sprint.IsDown;
            if (!mario->IsInShell) {
                return;
            }
            
            if (mario->IsInShell && (physicsObject->IsTouchingLeftWall || physicsObject->IsTouchingRightWall)) {
                QList<PhysicsContact> contacts = f.ResolveList(physicsObject->Contacts);
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
                }
                
                mario->FacingRight = physicsObject->IsTouchingLeftWall;
                f.Events.PlayBumpSound(f, filter.Entity);
            }

            physicsObject->Velocity.X = physics.WalkMaxVelocity[physics.RunSpeedStage] * physics.WalkBlueShellMultiplier * (mario->FacingRight ? 1 : -1) * (1 - (((FP) mario->ShellSlowdownFrames) / 60));
        }

        private bool HandleMegaMushroom(Frame f, ref Filter filter, MarioPlayerPhysicsInfo physics, VersusStageData stage) {
            var mario = filter.MarioPlayer;
            var physicsObject = filter.PhysicsObject;
            var transform = filter.Transform;
            var collider = filter.PhysicsCollider;

            if (mario->MegaMushroomStartFrames > 0) {
                mario->WallslideLeft = false;
                mario->WallslideRight = false;
                mario->IsTurnaround = false;
                mario->IsGroundpoundActive = false;
                mario->IsGroundpounding = false;
                mario->IsDrilling = false;
                mario->IsSpinnerFlying = false;
                mario->IsPropellerFlying = false;
                mario->IsCrouching = false;
                mario->IsSkidding = false;
                mario->IsInShell = false;
                mario->IsInKnockback = false;
                mario->IsInWeakKnockback = false;
                mario->DamageInvincibilityFrames = 0;

                if (QuantumUtils.Decrement(ref mario->MegaMushroomStartFrames)) {
                    // Started
                    mario->MegaMushroomFrames = 15 * 60;
                    //mario->CurrentPowerupState = PowerupState.Mushroom;
                    
                    physicsObject->IsFrozen = false;
                    f.Events.MarioPlayerMegaStart(f, filter.Entity);

                } else {
                    // Still growing...
                    if (f.Number % 4 == 0 && PhysicsObjectSystem.BoxInsideTile(f, transform->Position, collider->Shape)) {
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

                //animator.enabled = true;
            }
            
            return false;
        }

        private void HandlePowerups(Frame f, ref Filter filter, MarioPlayerPhysicsInfo physics, Input inputs, VersusStageData stage) {
            var mario = filter.MarioPlayer;
            var physicsObject = filter.PhysicsObject;

            if (QuantumUtils.Decrement(ref mario->InvincibilityFrames)) {
                mario->Combo = 0;
            }
            QuantumUtils.Decrement(ref mario->PropellerSpinFrames);
            bool fireballReady = QuantumUtils.Decrement(ref mario->ProjectileDelayFrames);
            if (QuantumUtils.Decrement(ref mario->ProjectileVolleyFrames)) {
                mario->CurrentVolley = 0;
            }

            mario->UsedPropellerThisJump &= physicsObject->IsTouchingGround;
            mario->IsPropellerFlying &= !mario->IsInWater;
            if (mario->IsPropellerFlying) {
                if (!QuantumUtils.Decrement(ref mario->PropellerLaunchFrames)) {
                    FP remainingTime = (FP) mario->PropellerLaunchFrames / 60;
                    if (mario->PropellerLaunchFrames > 52) {
                        physicsObject->Velocity.Y = physics.PropellerLaunchVelocity;
                    } else {
                        FP targetVelocity = physics.PropellerLaunchVelocity - (remainingTime < FP.FromString("0.4") ? (1 - (remainingTime * FP.FromString("2.5"))) * physics.PropellerLaunchVelocity : 0);
                        physicsObject->Velocity.Y = FPMath.Min(physicsObject->Velocity.Y + (24 * f.DeltaTime), targetVelocity);
                    }
                } else {
                    if (physicsObject->IsTouchingGround) {
                        mario->PropellerSpinFrames = 0;
                        mario->UsedPropellerThisJump = false;
                        if (!mario->IsDrilling) {
                            mario->IsPropellerFlying = false;
                        }
                    } else if (inputs.PowerupAction.IsDown && !mario->IsDrilling && physicsObject->Velocity.Y < -FP._0_10 && mario->PropellerSpinFrames < physics.PropellerSpinFrames / 4) {
                        mario->PropellerSpinFrames = physics.PropellerSpinFrames;
                        f.Events.MarioPlayerPropellerSpin(f, filter.Entity, *mario);
                    }
                }
            }

            PowerupState state = mario->CurrentPowerupState;
            if (!(inputs.PowerupAction.WasPressed 
                || (state == PowerupState.PropellerMushroom && inputs.PropellerPowerupAction.WasPressed) 
                || ((state == PowerupState.FireFlower || state == PowerupState.IceFlower) && inputs.FireballPowerupAction.WasPressed))) {
                return;
            }

            if (mario->IsDead || filter.Freezable->IsFrozen(f) || mario->IsGroundpounding || mario->IsInKnockback || f.Exists(mario->CurrentPipe)
                || mario->HeldEntity.IsValid || mario->IsCrouching || mario->IsSliding) {
                return;
            }

            switch (mario->CurrentPowerupState) {
            case PowerupState.IceFlower:
            case PowerupState.FireFlower: {
                if (!fireballReady || mario->IsWallsliding || (mario->JumpState == JumpState.TripleJump && !physicsObject->IsTouchingGround)
                    || mario->IsSpinnerFlying || mario->IsDrilling || mario->IsSkidding || mario->IsTurnaround) {
                    return;
                }

                byte activeProjectiles = mario->CurrentProjectiles;
                if (activeProjectiles >= physics.MaxProjecitles) {
                    return;
                }

                if (activeProjectiles < 2) {
                    // Always allow if < 2
                    mario->CurrentVolley = (byte) (activeProjectiles + 1);
                } else if (mario->CurrentVolley < physics.ProjectileVolleySize) {
                    // Allow in this volley
                    mario->CurrentVolley++;
                } else {
                    // No more left in volley
                    return;
                }

                mario->CurrentProjectiles++;
                mario->ProjectileDelayFrames = physics.ProjectileDelayFrames;
                mario->ProjectileVolleyFrames = physics.ProjectileVolleyFrames;

                FPVector2 spawnPos = filter.Transform->Position + new FPVector2(mario->FacingRight ? FP.FromString("0.4") : FP.FromString("-0.4"), FP.FromString("0.35"));

                EntityRef newEntity = f.Create(mario->CurrentPowerupState == PowerupState.IceFlower
                    ? f.SimulationConfig.IceballPrototype
                    : f.SimulationConfig.FireballPrototype);

                if (f.Unsafe.TryGetPointer(newEntity, out Projectile* projectile)) {
                    projectile->Initialize(f, newEntity, filter.Entity, spawnPos, mario->FacingRight);
                }
                f.Events.MarioPlayerShotProjectile(f, filter.Entity, *mario, *projectile);

                // Weird interaction in the main game...
                mario->WalljumpFrames = 0;
                break;
            }
            case PowerupState.PropellerMushroom: {
                if (mario->UsedPropellerThisJump || mario->IsInWater || (mario->IsSpinnerFlying && mario->IsDrilling) || mario->IsPropellerFlying || mario->WalljumpFrames > 0) {
                    return;
                }

                mario->PropellerLaunchFrames = physics.PropellerLaunchFrames;
                mario->UsedPropellerThisJump = true;

                mario->IsPropellerFlying = true;
                mario->IsSpinnerFlying = false;
                mario->IsCrouching = false;
                mario->JumpState = JumpState.None;
                mario->WallslideLeft = false;
                mario->WallslideRight = false;

                mario->WasTouchingGroundLastFrame = false;
                filter.PhysicsObject->IsTouchingGround = false;
                f.Events.MarioPlayerUsedPropeller(f, filter.Entity, *mario);
                break;
            }
            }
        }

        private void HandleSwimming(Frame f, ref Filter filter, MarioPlayerPhysicsInfo physics, Input inputs) {
            var mario = filter.MarioPlayer;
            var physicsObject = filter.PhysicsObject;

            if (!mario->IsInWater) {
                return;
            }

            /*
            if (HeldEntity is FrozenCube fc) {
                fc.AutoBreakTimer = TickTimer.CreateFromSeconds(Runner, fc.autoBreak);
                fc.Holder = null;
                HeldEntity = null;
            }
            */

            mario->WallslideLeft = false;
            mario->WallslideRight = false;
            mario->IsSliding = false;
            mario->IsSkidding = false;
            mario->IsTurnaround = false;
            mario->UsedPropellerThisJump = false;
            mario->IsInShell = false;
            mario->JumpState = JumpState.None;

            if (physicsObject->IsTouchingGround) {
                physicsObject->Velocity.Y = 0;
            }

            if (!mario->IsInKnockback && mario->JumpBufferFrames > 0) {
                physicsObject->Velocity.Y += physics.SwimJumpVelocity;
                physicsObject->IsTouchingGround = false;
                mario->JumpBufferFrames = 0;
                mario->IsCrouching = false;

                f.Events.MarioPlayerJumped(f, filter.Entity, *mario, JumpState.None, false);
            }
        }

        private void HandleSliding(Frame f, ref Filter filter, MarioPlayerPhysicsInfo physics, Input inputs) {
            var mario = filter.MarioPlayer;
            var physicsObject = filter.PhysicsObject;

            bool validFloorAngle = FPMath.Abs(physicsObject->FloorAngle) >= physics.SlideMinimumAngle;

            mario->IsCrouching &= !mario->IsSliding;

            if (physicsObject->IsOnSlideableGround 
                && validFloorAngle
                && !f.Exists(mario->HeldEntity)
                && (!((mario->FacingRight && physicsObject->IsTouchingRightWall) || (!mario->FacingRight && physicsObject->IsTouchingLeftWall))
                && (mario->IsCrouching || inputs.Down.IsDown)
                && !mario->IsInShell /* && mario->CurrentPowerupState != PowerupState.MegaMushroom*/)) {

                mario->IsSliding = true;
                mario->IsCrouching = false;
            }

            if (mario->IsSliding && physicsObject->IsTouchingGround && validFloorAngle) {
                FP runningMaxSpeed = physics.WalkMaxVelocity[physics.RunSpeedStage];
                FP angleDeg = physicsObject->FloorAngle * FP.Deg2Rad;

                bool uphill = FPMath.Sign(physicsObject->FloorAngle) != FPMath.Sign(physicsObject->Velocity.X);
                FP speed = f.DeltaTime * 5 * (uphill ? FPMath.Clamp01(1 - (FPMath.Abs(physicsObject->Velocity.X) / runningMaxSpeed)) : 4);

                physicsObject->Velocity.X = FPMath.Clamp(physicsObject->Velocity.X + (FPMath.Sin(angleDeg) * speed), -(runningMaxSpeed * FP._1_25), runningMaxSpeed * FP._1_25);
                //FP newY = (uphill ? 0 : -FP._1_50) * FPMath.Abs(newX);
                //= new FPVector2(newX, newY);
            }

            bool stationary = FPMath.Abs(physicsObject->Velocity.X) < FP._0_01 && physicsObject->IsTouchingGround;
            if (mario->IsSliding) {
                if (inputs.Up.IsDown
                    || ((inputs.Left.IsDown ^ inputs.Right.IsDown) && !inputs.Down.IsDown)
                    || (/*physicsObject->IsOnSlideableGround && FPMath.Abs(physicsObject->FloorAngle) < physics.SlideMinimumAngle && */physicsObject->IsTouchingGround && stationary && !inputs.Down.IsDown)
                    || (mario->FacingRight && physicsObject->IsTouchingRightWall)
                    || (!mario->FacingRight && physicsObject->IsTouchingLeftWall)) {

                    mario->IsSliding = false;
                    f.Events.MarioPlayerStoppedSliding(f, filter.Entity, stationary);
                }
            }
        }

        private void HandlePipes(Frame f, ref Filter filter, MarioPlayerPhysicsInfo physics, VersusStageData stage) {
            var mario = filter.MarioPlayer;

            QuantumUtils.Decrement(ref mario->PipeCooldownFrames);

            if (!f.Exists(mario->CurrentPipe)) {
                return;
            }

            var physicsObject = filter.PhysicsObject;
            var interactable = f.Unsafe.GetPointer<Interactable>(filter.Entity);
            var currentPipe = f.Unsafe.GetPointer<EnterablePipe>(mario->CurrentPipe);

            mario->IsGroundpounding = false;
            mario->IsGroundpoundActive = false;
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
                    mario->IsCrouching = false;
                    mario->PipeCooldownFrames = 30;
                    physicsObject->Velocity = FPVector2.Zero;
                    interactable->ColliderDisabled = false;
                }
            }
        }

        private void HandleHitbox(Frame f, ref Filter filter, MarioPlayerPhysicsInfo physics) {
            var mario = filter.MarioPlayer;
            var physicsObject = filter.PhysicsObject;
            var collider = filter.PhysicsCollider;

            QuantumUtils.Decrement(ref mario->DamageInvincibilityFrames);

            FP newHeight;
            bool crouchHitbox = mario->CurrentPowerupState != PowerupState.MiniMushroom && !mario->CurrentPipe.IsValid && ((mario->IsCrouching && !mario->IsGroundpounding) || mario->IsInShell || mario->IsSliding);
            if (mario->CurrentPowerupState <= PowerupState.MiniMushroom || (mario->IsStarmanInvincible && !physicsObject->IsTouchingGround && !crouchHitbox && !mario->IsSliding && !mario->IsSpinnerFlying && !mario->IsPropellerFlying) || mario->IsGroundpounding) {
                newHeight = physics.SmallHitboxHeight;
            } else {
                newHeight = physics.LargeHitboxHeight;
            }

            if (crouchHitbox) {
                newHeight *= mario->CurrentPowerupState <= PowerupState.MiniMushroom ? FP._0_75 : FP._0_50;
            }

            FPVector2 newExtents = new(FP.FromString("0.175"), newHeight / 2);
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
            newExtents *= FPMath.Lerp(1, FP.FromString("3.5"), megaPercentage);

            collider->Shape.Box.Extents = newExtents;
            collider->Shape.Centroid = FPVector2.Up * newExtents.Y;
            collider->IsTrigger = mario->IsDead;

            filter.Freezable->IceBlockSize = collider->Shape.Box.Extents * (2 + FP._0_50);
            filter.Freezable->IceBlockSize.Y += FP._0_10;
            filter.Freezable->IceBlockSize.X += FP._0_25;
        }

        private void HandleBreakingBlocks(Frame f, ref Filter filter, MarioPlayerPhysicsInfo physics, Input inputs, VersusStageData stage) {
            var mario = filter.MarioPlayer;
            var physicsObject = filter.PhysicsObject;

            if (physicsObject->IsTouchingCeiling) {
                bool? playBumpSound = null;
                QList<PhysicsContact> contacts = f.ResolveList(physicsObject->Contacts);
                foreach (var contact in contacts) {
                    if (FPVector2.Dot(contact.Normal, FPVector2.Down) < FP._0_33 * 2) {
                        continue;
                    }

                    // Ceiling tiles.
                    var tileInstance = stage.GetTileRelative(f, contact.TileX, contact.TileY);
                    StageTile tile = f.FindAsset(tileInstance.Tile);
                    if (tile is IInteractableTile it) {
                        it.Interact(f, filter.Entity, InteractionDirection.Up,
                            new Vector2Int(contact.TileX, contact.TileY), tileInstance, out bool tempPlayBumpSound);

                        playBumpSound = (playBumpSound ?? true) & tempPlayBumpSound;
                    }
                }

                if (mario->IsInWater) {
                    // TODO: magic value
                    physicsObject->Velocity.Y = -2;
                }
                if (playBumpSound ?? true) {
                    f.Events.PlayBumpSound(f, filter.Entity);
                }
            }
        }

        private void HandleSpinners(Frame f, ref Filter filter, VersusStageData stage) {
            var mario = filter.MarioPlayer;
            var physicsObject = filter.PhysicsObject;
            
            if (!f.TryResolveList(physicsObject->Contacts, out QList<PhysicsContact> contacts)) {
                return;
            }

            mario->IsSpinnerFlying &= !physicsObject->IsTouchingGround;

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
                PhysicsObjectSystem.MoveHorizontally(f, moveVelocity, filter.Entity, stage, contacts);
            }
        }

        private bool HandleDeathAndRespawning(Frame f, ref Filter filter, VersusStageData stage) {
            var mario = filter.MarioPlayer;
            var transform = filter.Transform;
            var physicsObject = filter.PhysicsObject;
            var collider = filter.PhysicsCollider;

            if (!mario->IsDead) {
                if (transform->Position.Y + (collider->Shape.Box.Extents.Y * 2) < stage.StageWorldMin.Y) {
                    // Death via pit
                    mario->Death(f, filter.Entity, false);
                } else {
                    return false;
                }
            }

            // Respawn timers
            if (mario->IsRespawning) {
                if (QuantumUtils.Decrement(ref mario->RespawnFrames)) {
                    mario->Respawn(f, filter.Entity);
                    return false;
                }
            } else {
                if (QuantumUtils.Decrement(ref mario->PreRespawnFrames)) {
                    mario->PreRespawn(f, filter.Entity, stage);
                } else if (mario->DeathAnimationFrames > 0 && QuantumUtils.Decrement(ref mario->DeathAnimationFrames)) {
                    bool spawnAgain = !((f.Global->Rules.IsLivesEnabled && mario->Lives == 0) || mario->Disconnected);
                    if (!spawnAgain && mario->Stars > 0) {
                        // Try to drop more stars
                        mario->SpawnStars(f, filter.Entity, 1);
                        mario->DeathAnimationFrames = 30;
                    } else {
                        // Play the animation as normal
                        if (transform->Position.Y > stage.StageWorldMin.Y) {
                            physicsObject->Gravity = FPVector2.Down * FP.FromString("11.75");
                            physicsObject->Velocity = FPVector2.Up * 7;
                            physicsObject->IsFrozen = false;
                            physicsObject->DisableCollision = true;
                            f.Events.MarioPlayerDeathUp(f, filter.Entity);
                        }
                        if (!spawnAgain) {
                            mario->PreRespawnFrames = 144;
                        }
                    }
                }
            }

            return true;
        }

        public static void SpawnItem(Frame f, EntityRef marioEntity, MarioPlayer* mario, AssetRef<EntityPrototype> prefab) {
            if (!prefab.IsValid) {
                prefab = QuantumUtils.GetRandomItem(f, *mario).Prefab;
            }

            EntityRef newEntity = f.Create(prefab);
            if (f.Unsafe.TryGetPointer(newEntity, out Powerup* powerup)) {
                powerup->ParentToPlayer(f, newEntity, marioEntity);
            }
        }

        public void SpawnReserveItem(Frame f, ref Filter filter) {
            var mario = filter.MarioPlayer;
            var reserveItem = f.FindAsset(mario->ReserveItem);

            if (!reserveItem || mario->IsDead || mario->MegaMushroomStartFrames > 0 || (mario->MegaMushroomStationaryEnd && mario->MegaMushroomEndFrames > 0)) {
                f.Events.MarioPlayerUsedReserveItem(f, filter.Entity, *mario, false);
                return;
            }

            SpawnItem(f, filter.Entity, mario, reserveItem.Prefab);
            mario->ReserveItem = default;
            f.Events.MarioPlayerUsedReserveItem(f, filter.Entity, *mario, true);
        }

        public void OnRemoved(Frame f, EntityRef entity, Projectile* component) {
            if (f.Unsafe.TryGetPointer(component->Owner, out MarioPlayer* mario)) {
                mario->CurrentProjectiles--;
            }
        }

        public void OnGameStarting(Frame f) {
            // Spawn players
            var config = f.SimulationConfig;
            var playerDatas = f.Filter<PlayerData>();
            while (playerDatas.NextUnsafe(out _, out PlayerData* data)) {
                if (data->IsSpectator) {
                    continue;
                }

                int characterIndex = Mathf.Clamp(data->Character, 0, config.CharacterDatas.Length - 1);
                CharacterAsset character = config.CharacterDatas[characterIndex];

                EntityRef newPlayer = f.Create(character.Prototype);
                var mario = f.Unsafe.GetPointer<MarioPlayer>(newPlayer);
                mario->PlayerRef = data->PlayerRef;
                var newTransform = f.Unsafe.GetPointer<Transform2D>(newPlayer);
                newTransform->Position = f.FindAsset<VersusStageData>(f.Map.UserAsset).Spawnpoint;
            }

            // And respawn them
            var stage = f.FindAsset<VersusStageData>(f.Map.UserAsset);
            var filter = f.Filter<MarioPlayer>();
            while (filter.NextUnsafe(out EntityRef entity, out MarioPlayer* mario)) {
                mario->Lives = f.Global->Rules.Lives;
                mario->PreRespawn(f, entity, stage);
            }
        }

        public void OnBeforePhysicsCollision(Frame f, VersusStageData stage, EntityRef entity, PhysicsContact* contact, bool* allowCollision) {
            if (!f.Unsafe.TryGetPointer(entity, out MarioPlayer* mario)) {
                return;
            }

            if (f.Unsafe.TryGetPointer(contact->Entity, out InvisibleBlock* block)
                && f.Unsafe.TryGetPointer(contact->Entity, out Transform2D* transform)) {

                if (stage.GetTileWorld(f, transform->Position).Tile != default) {
                    return;
                }

                StageTileInstance result = new StageTileInstance {
                    Rotation = 0,
                    Scale = FPVector2.One,
                    Tile = block->Tile,
                };
                f.Signals.OnMarioPlayerCollectedCoin(entity, mario, transform->Position, true, false);
                BreakableBrickTile.Bump(f, stage, QuantumUtils.WorldToRelativeTile(stage, transform->Position), block->BumpTile, result, false, entity);
                return;
            }

            if (mario->CurrentPowerupState != PowerupState.MegaMushroom) {
                return;
            }

            InteractionDirection direction;
            FP upDot = FPVector2.Dot(contact->Normal, FPVector2.Up);
            if (upDot > PhysicsObjectSystem.GroundMaxAngle) {
                // Ground contact... only allow if groundpounding
                if (!mario->IsGroundpoundActive) {
                    return;
                }
                direction = InteractionDirection.Down;

            } else if (upDot < -PhysicsObjectSystem.GroundMaxAngle) {
                direction = InteractionDirection.Up;

            } else if (contact->Normal.X < 0) {
                direction = InteractionDirection.Right;

            } else {
                direction = InteractionDirection.Left;
            }

            // Try to break this tile as mega mario...
            StageTileInstance tileInstance = stage.GetTileRelative(f, contact->TileX, contact->TileY);
            StageTile tile = f.FindAsset(tileInstance.Tile);

            if (tile is IInteractableTile it) {
                *allowCollision = !it.Interact(f, entity, direction, new Vector2Int(contact->TileX, contact->TileY), tileInstance, out bool _);
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

            *doSplash = !mario->IsDead && !f.Exists(mario->CurrentPipe);

            if (!*doSplash) {
                return;
            }

            var liquid = f.Unsafe.GetPointer<Liquid>(liquidEntity);

            if (exit) {
                if (liquid->LiquidType == LiquidType.Water) {
                    mario->WaterColliderCount--;
                    if (QuantumUtils.Decrement(ref mario->WaterColliderCount)) {
                        // Jump
                        mario->SwimExitForceJump = true;
                    }
                }
            } else {
                switch (liquid->LiquidType) {
                case LiquidType.Water:
                    mario->WaterColliderCount++;
                    break;
                case LiquidType.Lava:
                    // Kill, fire death
                    mario->Death(f, entity, true);
                    break;
                case LiquidType.Poison:
                    // Kill, normal death
                    mario->Death(f, entity, false);
                    break;
                }
            }
        }

        public void OnEntityBumped(Frame f, EntityRef entity, FPVector2 tileWorldPosition, EntityRef bumper) {
            if (f.Unsafe.TryGetPointer(entity, out MarioPlayer* mario)) {
                if (mario->IsInKnockback) {
                    return;
                }

                FPVector2 bumperPosition = f.Unsafe.GetPointer<Transform2D>(bumper)->Position;
                var marioTransform = f.Unsafe.GetPointer<Transform2D>(entity);

                QuantumUtils.UnwrapWorldLocations(f, marioTransform->Position, bumperPosition, out FPVector2 ourPos, out FPVector2 theirPos);
                bool onRight = ourPos.X > theirPos.X;

                mario->DoKnockback(f, entity, !onRight, 1, false, bumper);
            }
        }

        public void OnMarioCoinInteraction(Frame f, EntityRef marioEntity, EntityRef coinEntity) {
            CoinSystem.TryCollectCoin(f, coinEntity, marioEntity);
        }

        public static void OnMarioProjectileInteraction(Frame f, EntityRef marioEntity, EntityRef projectileEntity) {
            var projectile = f.Unsafe.GetPointer<Projectile>(projectileEntity);

            if (projectile->Owner == marioEntity) {
                return;
            }

            var mario = f.Unsafe.GetPointer<MarioPlayer>(marioEntity);
            var projectileAsset = f.FindAsset(projectile->Asset);
            bool dropStars = true;

            if (f.Unsafe.TryGetPointer(projectile->Owner, out MarioPlayer* ownerMario)) {
                dropStars = ownerMario->Team == mario->Team;
            }

            if (!mario->IsInKnockback
                && mario->CurrentPowerupState != PowerupState.MegaMushroom) {

                switch (projectileAsset.Effect) {
                case ProjectileEffectType.Knockback:
                    bool dropStar = true;
                    if (f.Unsafe.TryGetPointer(projectile->Owner, out MarioPlayer* marioOwner)) {
                        dropStar = mario->Team != marioOwner->Team;
                    }
                    mario->DoKnockback(f, marioEntity, !projectile->FacingRight, 1, true, projectileEntity);
                    break;
                case ProjectileEffectType.Freeze:
                    IceBlockSystem.Freeze(f, marioEntity);
                    break;
                }
            }

            if (projectileAsset.DestroyOnHit) {
                ProjectileSystem.Destroy(f, projectileEntity, projectileAsset.DestroyParticleEffect);
            }
        }

        public static void OnMarioMarioInteraction(Frame f, EntityRef marioAEntity, EntityRef marioBEntity) {
            var marioA = f.Unsafe.GetPointer<MarioPlayer>(marioAEntity);
            var marioB = f.Unsafe.GetPointer<MarioPlayer>(marioBEntity);

            // Don't damage players with I-Frames
            if (marioA->DamageInvincibilityFrames > 0 || marioB->DamageInvincibilityFrames > 0) {
                return;
            }

            // Or players in the Mega Mushroom grow animation
            if (marioA->MegaMushroomStartFrames > 0 || marioB->MegaMushroomFrames > 0) {
                return;
            }

            // Or if a player just got damaged
            if ((f.Number - marioA->KnockbackTick) < 12 || (f.Number - marioB->KnockbackTick) < 12) {
                return;
            }

            var marioATransform = f.Unsafe.GetPointer<Transform2D>(marioAEntity);
            var marioBTransform = f.Unsafe.GetPointer<Transform2D>(marioBEntity);
            var marioAPhysics = f.Unsafe.GetPointer<PhysicsObject>(marioAEntity);
            var marioBPhysics = f.Unsafe.GetPointer<PhysicsObject>(marioBEntity);

            // Hit players
            bool dropStars = marioA->Team != marioB->Team;

            QuantumUtils.UnwrapWorldLocations(f, marioATransform->Position, marioBTransform->Position, out FPVector2 marioAPosition, out FPVector2 marioBPosition);
            bool fromRight = marioAPosition.X < marioBPosition.X;

            FP dot = FPVector2.Dot((marioAPosition - marioBPosition).Normalized, FPVector2.Up);
            bool marioAAbove = dot > FP._0_33 * 2;
            bool marioBAbove = dot < -FP._0_33 * 2;

            // Starman cases
            bool marioAStarman = marioA->IsStarmanInvincible;
            bool marioBStarman = marioB->IsStarmanInvincible;
            if (marioAStarman && marioBStarman) {
                marioA->DoKnockback(f, marioAEntity, fromRight, dropStars ? 1 : 0, true, marioBEntity);
                marioB->DoKnockback(f, marioBEntity, !fromRight, dropStars ? 1 : 0, true, marioAEntity);
                return;
            } else if (marioAStarman) {
                MarioMarioAttackStarman(f, marioAEntity, marioBEntity, fromRight, dropStars);
                return;
            } else if (marioBStarman) {
                MarioMarioAttackStarman(f, marioBEntity, marioAEntity, !fromRight, dropStars);
                return;
            }

            // Mega mushroom cases
            bool marioAMega = marioA->CurrentPowerupState == PowerupState.MegaMushroom;
            bool marioBMega = marioB->CurrentPowerupState == PowerupState.MegaMushroom;
            if (marioAMega && marioBMega) {
                // Both mega
                if (marioAAbove) {
                    marioA->DoEntityBounce = true;
                    marioA->IsGroundpounding = false;
                    marioA->IsDrilling = false;
                } else if (marioBAbove) {
                    marioB->DoEntityBounce = true;
                    marioB->IsGroundpounding = false;
                    marioB->IsDrilling = false;
                } else {
                    marioA->DoKnockback(f, marioAEntity, fromRight, 0, true, marioBEntity);
                    marioB->DoKnockback(f, marioBEntity, !fromRight, 0, true, marioAEntity);
                }
                return;
            } else if (marioAMega) {
                if (dropStars) {
                    marioB->Powerdown(f, marioBEntity, false);
                } else {
                    marioB->DoKnockback(f, marioBEntity, !fromRight, 0, true, marioAEntity);
                }
                return;
            } else if (marioBMega) {
                if (dropStars) {
                    marioA->Powerdown(f, marioAEntity, false);
                } else {
                    marioA->DoKnockback(f, marioAEntity, fromRight, 0, true, marioBEntity);
                }
                return;
            }

            // Blue shell cases
            bool marioAShell = marioA->IsInShell;
            bool marioBShell = marioB->IsInShell;
            if (marioAShell && marioBShell) {
                marioA->DoKnockback(f, marioAEntity, fromRight, dropStars ? 1 : 0, true, marioBEntity);
                marioB->DoKnockback(f, marioBEntity, !fromRight, dropStars ? 1 : 0, true, marioAEntity);
                return;
            } else if (marioAShell) {
                if (!marioBAbove) {
                    // Hit them, powerdown them
                    if (dropStars) {
                        marioB->Powerdown(f, marioBEntity, false);
                    } else {
                        marioB->DoKnockback(f, marioBEntity, !fromRight, 0, true, marioAEntity);
                    }
                    marioA->FacingRight = fromRight;
                    return;
                }
            } else if (marioBShell) {
                if (!marioAAbove) {
                    // Hit them, powerdown them
                    if (dropStars) {
                        marioA->Powerdown(f, marioAEntity, false);
                    } else {
                        marioA->DoKnockback(f, marioAEntity, !fromRight, 0, true, marioBEntity);
                    }
                    marioB->FacingRight = !fromRight;
                    return;
                }
            }

            var stage = f.FindAsset<VersusStageData>(f.Map.UserAsset);
            // Blue shell stomps
            if (marioA->IsCrouchedInShell && marioAPhysics->IsTouchingGround && marioBAbove && !marioB->IsGroundpoundActive && !marioB->IsDrilling) {
                MarioMarioBlueShellStomp(f, stage, marioBEntity, marioAEntity, fromRight);
                return;
            } else if (marioB->IsCrouchedInShell && marioBPhysics->IsTouchingGround && marioAAbove && !marioA->IsGroundpoundActive && !marioA->IsDrilling) {
                MarioMarioBlueShellStomp(f, stage, marioAEntity, marioBEntity, fromRight);
                return;
            }

            // Normal stomps
            if (marioAAbove) {
                MarioMarioStomp(f, marioAEntity, marioBEntity, fromRight, dropStars);
                return;
            } else if (marioBAbove) {
                MarioMarioStomp(f, marioBEntity, marioAEntity, !fromRight, dropStars);
                return;
            }


            // Pushing
            bool marioAMini = marioA->CurrentPowerupState == PowerupState.MiniMushroom;
            bool marioBMini = marioB->CurrentPowerupState == PowerupState.MiniMushroom;
            if (!marioA->IsInKnockback && !marioB->IsInKnockback) {
                // Collided with them
                var marioAPhysicsInfo = f.FindAsset(marioA->PhysicsAsset);
                var marioBPhysicsInfo = f.FindAsset(marioB->PhysicsAsset);

                if (marioAMini ^ marioBMini) {
                    // Minis
                    if (marioAMini) {
                        marioA->DoKnockback(f, marioAEntity, fromRight, dropStars ? 1 : 0, false, marioBEntity);
                    }
                    if (marioBMini) {
                        marioB->DoKnockback(f, marioBEntity, !fromRight, dropStars ? 1 : 0, false, marioAEntity);
                    }
                } else if (FPMath.Abs(marioAPhysics->Velocity.X) > marioAPhysicsInfo.WalkMaxVelocity[marioAPhysicsInfo.WalkSpeedStage]
                           || FPMath.Abs(marioBPhysics->Velocity.X) > marioBPhysicsInfo.WalkMaxVelocity[marioBPhysicsInfo.WalkSpeedStage]) {
                    
                    // Bump
                    if (marioAPhysics->IsTouchingGround) {
                        marioA->DoKnockback(f, marioAEntity, fromRight, dropStars ? 1 : 0, true, marioBEntity);
                    } else {
                        marioAPhysics->Velocity.X = marioAPhysicsInfo.WalkMaxVelocity[marioAPhysicsInfo.RunSpeedStage] * (fromRight ? 1 : -1);
                    }

                    if (marioBPhysics->IsTouchingGround) {
                        marioB->DoKnockback(f, marioBEntity, !fromRight, dropStars ? 1 : 0, true, marioAEntity);
                    } else {
                        marioBPhysics->Velocity.X = marioBPhysicsInfo.WalkMaxVelocity[marioBPhysicsInfo.RunSpeedStage] * (!fromRight ? 1 : -1);
                    }
                } else {
                    /*
                    // Collide
                    int directionToOtherPlayer = fromRight ? -1 : 1;
                    float overlap = ((WorldHitboxSize.x * 0.5f) + (other.WorldHitboxSize.x * 0.5f) - Mathf.Abs(ours.x - theirs.x)) * 0.5f;

                    if (overlap > 0.02f) {
                        Vector2 ourNewPosition = new(body.Position.x + (overlap * directionToOtherPlayer), body.Position.y);
                        Vector2 theirNewPosition = new(other.body.Position.x + (overlap * -directionToOtherPlayer), other.body.Position.y);

                        int hits = 0;
                        RaycastHit2D hit;
                        if (hit = Runner.GetPhysicsScene2D().BoxCast(ourNewPosition + (WorldHitboxSize * Vector2.up * 0.55f), WorldHitboxSize, 0, Vector2.zero, Physics2D.defaultContactOffset, Layers.MaskSolidGround)) {
                            ourNewPosition.x = hit.point.x + hit.normal.x * (WorldHitboxSize.x * 0.5f + Physics2D.defaultContactOffset);
                            theirNewPosition.x = ourNewPosition.x + hit.normal.x * ((WorldHitboxSize.x + other.WorldHitboxSize.x) * 0.5f + Physics2D.defaultContactOffset);
                            hits++;
                        }
                        if (hit = Runner.GetPhysicsScene2D().BoxCast(theirNewPosition + (other.WorldHitboxSize * Vector2.up * 0.55f), other.WorldHitboxSize, 0, Vector2.zero, Physics2D.defaultContactOffset, Layers.MaskSolidGround)) {
                            theirNewPosition.x = hit.point.x + hit.normal.x * (other.WorldHitboxSize.x * 0.5f + Physics2D.defaultContactOffset);
                            ourNewPosition.x = theirNewPosition.x + hit.normal.x * ((WorldHitboxSize.x + other.WorldHitboxSize.x) * 0.5f + Physics2D.defaultContactOffset);
                            hits++;
                        }

                        if (hits < 2) {
                            body.Position = ourNewPosition;
                            other.body.Position = theirNewPosition;

                            float avgVel = (body.Velocity.x + other.body.Velocity.x) * 0.5f;
                            body.Velocity = new(avgVel, body.Velocity.y);
                            other.body.Velocity = new(avgVel, other.body.Velocity.y);
                        }
                    }
                    */
                }
            }
        }

        private static void MarioMarioAttackStarman(Frame f, EntityRef attacker, EntityRef defender, bool fromRight, bool dropStars) {
            var attackerMario = f.Unsafe.GetPointer<MarioPlayer>(attacker);
            var defenderMario = f.Unsafe.GetPointer<MarioPlayer>(defender);

            if (defenderMario->CurrentPowerupState == PowerupState.MegaMushroom) {
                // Wait fuck-
                attackerMario->DoKnockback(f, attacker, fromRight, dropStars ? 1 : 0, true, defender);
            } else {
                if (dropStars) {
                    defenderMario->Powerdown(f, defender, false);
                } else {
                    defenderMario->DoKnockback(f, defender, !fromRight, 0, true, attacker);
                }
            }
        }

        private static void MarioMarioBlueShellStomp(Frame f, VersusStageData stage, EntityRef attacker, EntityRef defender, bool fromRight) {
            var defenderMario = f.Unsafe.GetPointer<MarioPlayer>(defender);
            var defenderPhysicsObject = f.Unsafe.GetPointer<PhysicsObject>(defender);
            var defenderPhysics = f.FindAsset(defenderMario->PhysicsAsset);

            FPVector2 raycastPosition = f.Unsafe.GetPointer<Transform2D>(defender)->Position + f.Unsafe.GetPointer<PhysicsCollider2D>(defender)->Shape.Centroid;

            bool goLeft = fromRight;
            if (goLeft && PhysicsObjectSystem.Raycast(f, stage, raycastPosition, FPVector2.Left, FP._0_25, out _)) {
                // Tile to the right. Force go left.
                goLeft = false;
            } else if (!goLeft && PhysicsObjectSystem.Raycast(f, stage, raycastPosition, FPVector2.Right, FP._0_25, out _)) {
                // Tile to the left. Force go right.
                goLeft = true;
            }

            defenderMario->IsGroundpounding = false;
            defenderPhysicsObject->Velocity.X = defenderPhysics.WalkMaxVelocity[defenderPhysics.RunSpeedStage] * defenderPhysics.WalkBlueShellMultiplier * (goLeft ? -1 : 1);

            var attackerMario = f.Unsafe.GetPointer<MarioPlayer>(attacker);
            attackerMario->DoEntityBounce = true;
        }

        private static void MarioMarioStomp(Frame f, EntityRef attacker, EntityRef defender, bool fromRight, bool dropStars) {
            var attackerMario = f.Unsafe.GetPointer<MarioPlayer>(attacker);
            var defenderMario = f.Unsafe.GetPointer<MarioPlayer>(defender);

            // Hit them from above
            attackerMario->DoEntityBounce = !attackerMario->IsGroundpounding && !attackerMario->IsDrilling;
            bool groundpounded = attackerMario->IsGroundpoundActive;

            if (attackerMario->CurrentPowerupState == PowerupState.MiniMushroom && defenderMario->CurrentPowerupState != PowerupState.MiniMushroom) {
                // We are mini, they arent. special rules.
                if (groundpounded) {
                    defenderMario->DoKnockback(f, defender, !fromRight, dropStars ? 3 : 0, false, attacker);
                    attackerMario->IsGroundpounding = false;
                    attackerMario->DoEntityBounce = true;
                }
            } else if (defenderMario->CurrentPowerupState == PowerupState.MiniMushroom && groundpounded) {
                // We are big, groundpounding a mini opponent. squish.
                defenderMario->DoKnockback(f, defender, fromRight, dropStars ? 3 : 0, false, attacker);
                attackerMario->DoEntityBounce = false;
            } else {
                if (defenderMario->CurrentPowerupState == PowerupState.MiniMushroom && groundpounded) {
                    defenderMario->Powerdown(f, defender, false);
                } else {
                    defenderMario->DoKnockback(f, defender, !fromRight, dropStars ? (groundpounded ? 3 : 1) : 0, false, attacker);
                }
            }
        }

        public void OnBeforeInteraction(Frame f, EntityRef entity, bool* allowInteraction) {
            *allowInteraction &= !f.Unsafe.TryGetPointer(entity, out MarioPlayer* mario) || !(mario->IsDead || f.Exists(mario->CurrentPipe));
        }
    }
}