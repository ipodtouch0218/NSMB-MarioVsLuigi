using Photon.Deterministic;
using Quantum.Collections;
using UnityEngine;
using static IInteractableTile;

namespace Quantum {

    public unsafe class MarioPlayerSystem : SystemMainThreadFilterStage<MarioPlayerSystem.Filter>, ISignalOnComponentRemoved<Projectile>, ISignalOnGameStarting, ISignalOnPreTileCollide, ISignalOnBobombExplodeEntity {
        public struct Filter {
            public EntityRef Entity;
            public Transform2D* Transform;
            public MarioPlayer* MarioPlayer;
            public PhysicsObject* PhysicsObject;
            public PhysicsCollider2D* PhysicsCollider;
        }

        public override void Update(Frame f, ref Filter filter, VersusStageData stage) {
            var player = filter.MarioPlayer->PlayerRef;
            Input input = *f.GetPlayerInput(player);

            if (f.GetPlayerCommand(player) is CommandSpawnReserveItem) {
                SpawnReserveItem(f, filter);
            }

            var mario = filter.MarioPlayer;
            var physicsObject = filter.PhysicsObject;
            var physics = f.FindAsset(filter.MarioPlayer->PhysicsAsset);

            if (HandleDeathAndRespawning(f, filter, stage)) {
                return;
            }
            if (HandleMegaMushroom(f, filter, physics, stage)) {
                return;
            }
            HandlePowerups(f, filter, physics, input, stage);
            HandleBreakingBlocks(f, filter, physics, input, stage);
            HandleCrouching(f, filter, physics, input);
            HandleGroundpound(f, filter, physics, input, stage);
            HandleSliding(f, filter, physics, input);
            HandleWalkingRunning(f, filter, physics, input);
            HandleJumping(f, filter, physics, input);
            HandleSwimming(f, filter, physics, input);
            HandleBlueShell(f, filter, physics, input, stage);
            HandleWallslide(f, filter, physics, input);
            HandleGravity(f, filter, physics, input);
            HandleTerminalVelocity(f, filter, physics, input);
            HandleFacingDirection(f, filter, physics, input);
            HandleHitbox(f, filter, physics);
            mario->WasTouchingGroundLastFrame = physicsObject->IsTouchingGround;
        }

        public void HandleWalkingRunning(Frame f, Filter filter, MarioPlayerPhysicsInfo physics, Input inputs) {

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

            if (mario->IsGroundpounding || mario->IsInShell || mario->IsInKnockback || mario->CurrentPipe.IsValid || mario->JumpLandingFrames > 0 || !(mario->WalljumpFrames <= 0 || physicsObject->IsTouchingGround || physicsObject->Velocity.Y < 0)) {
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
            int stage = mario->GetSpeedStage(*physicsObject, physics);

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
            bool uphill = FPMath.Sign(physicsObject->FloorAngle) == sign;

            if (!physicsObject->IsTouchingGround) {
                mario->FastTurnaroundFrames = 0;
            }

            if (mario->FastTurnaroundFrames > 0) {
                physicsObject->Velocity.X = 0;
                if (--mario->FastTurnaroundFrames == 0) {
                    mario->IsTurnaround = true;
                }

            } else if (mario->IsTurnaround) {
                mario->IsTurnaround &= physicsObject->IsTouchingGround && !mario->IsCrouching && xVelAbs < physics.WalkMaxVelocity[1] && !physicsObject->IsTouchingLeftWall && !physicsObject->IsTouchingRightWall;
                mario->IsSkidding &= mario->IsTurnaround;

                physicsObject->Velocity.X += (physics.FastTurnaroundAcceleration * (mario->FacingRight ? -1 : 1) * f.DeltaTime);

            } else if ((inputs.Left ^ inputs.Right) &&
                       (!mario->IsCrouching || (mario->IsCrouching && !physicsObject->IsTouchingGround && mario->CurrentPowerupState != PowerupState.BlueShell)) &&
                       !mario->IsInKnockback && !mario->IsSliding) {
                // We can walk here
                bool reverse = physicsObject->Velocity.X != 0 && ((inputs.Left ? 1 : -1) == sign);

                // Check that we're not going above our limit
                FP max = maxArray[maxStage] + CalculateSlopeMaxSpeedOffset(FPMath.Abs(physicsObject->FloorAngle) * (uphill ? 1 : -1));
                FP maxAcceleration = FPMath.Abs(max - xVelAbs) * (1 / f.DeltaTime);
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
                        acc = (angle > 30 ? physics.SlideFastAcceleration : physics.SlideSlowAcceleration) * (uphill ? 1 : -1);
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
                FP target = (angle > 30 && physicsObject->IsOnSlideableGround) ? FPMath.Sign(physicsObject->FloorAngle) * -physics.WalkMaxVelocity[0] : 0;
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

            if (!wasInShell && mario->IsInShell) {
                f.Events.MarioPlayerCrouched(f, filter.Entity, *mario);
            }
        }

        private static FP CalculateSlopeMaxSpeedOffset(FP floorAngle) {
            // TODO remove magic constant
            return FP.FromString("-0.0304687") * floorAngle;
        }

        private void HandleJumping(Frame f, Filter filter, MarioPlayerPhysicsInfo physics, Input inputs) {

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

            bool tryJump = mario->JumpBufferFrames > 0 && (physicsObject->IsTouchingGround || mario->IsWallsliding);
            bool doJump = (tryJump && (physicsObject->IsTouchingGround || mario->CoyoteTimeFrames > 0)) || (!mario->IsInWater && mario->SwimExitForceJump);

            QuantumUtils.Decrement(ref mario->CoyoteTimeFrames);
            QuantumUtils.Decrement(ref mario->JumpBufferFrames);

            if (!mario->DoEntityBounce && (mario->IsInWater || !doJump || mario->IsInKnockback || (mario->CurrentPowerupState == PowerupState.MegaMushroom && mario->JumpState == JumpState.SingleJump) || mario->IsWallsliding)) {
                return;
            }

            /*
            if (!mario->DoEntityBounce && OnSpinner && !HeldEntity) {
                // Jump of spinner
                body.Velocity = new(body.Velocity.x, launchVelocity);
                IsSpinnerFlying = true;
                SpinnerLaunchAnimCounter++;
                IsOnGround = false;
                PreviousTickIsOnGround = false;
                IsCrouching = false;
                IsInShell = false;
                IsSkidding = false;
                IsTurnaround = false;
                IsSliding = false;
                WallSlideEndTimer = TickTimer.None;
                IsGroundpounding = false;
                GroundpoundStartTimer = TickTimer.None;
                IsDrilling = false;
                IsPropellerFlying = false;
                OnSpinner.ArmPosition = 0;
                OnSpinner = null;
                return;
            }
            */

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

        public void HandleGravity(Frame f, Filter filter, MarioPlayerPhysicsInfo physics, Input inputs) {

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

        public void HandleTerminalVelocity(Frame f, Filter filter, MarioPlayerPhysicsInfo physics, Input inputs) {

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

        public void HandleWallslide(Frame f, Filter filter, MarioPlayerPhysicsInfo physics, Input inputs) {

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

            if (mario->WallslideEndFrames > 0) {
                if (--mario->WallslideEndFrames == 0) {
                    mario->WallslideRight = false;
                    mario->WallslideLeft = false;
                    return;
                }
            }

            if (mario->IsWallsliding) {
                // Walljump check
                mario->FacingRight = mario->WallslideLeft;
                if (mario->JumpBufferFrames > 0 && mario->WalljumpFrames == 0 /* && !BounceJump */) {
                    // Perform walljump
                    physicsObject->Velocity = new(physics.WalljumpHorizontalVelocity * (mario->WallslideLeft ? 1 : -1), mario->CurrentPowerupState == PowerupState.MiniMushroom ? physics.WalljumpMiniVerticalVelocity : physics.WalljumpVerticalVelocity);
                    mario->JumpState = JumpState.SingleJump;
                    physicsObject->IsTouchingGround = false;
                    // DoEntityBounce = false;
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
            if (/* !floorCheck || */ !moveDownCheck /* || !heightLowerCheck */) {
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


        public void HandleFacingDirection(Frame f, Filter filter, MarioPlayerPhysicsInfo physics, Input inputs) {
            var mario = filter.MarioPlayer;
            var physicsObject = filter.PhysicsObject;

            if (mario->IsGroundpounding && !physicsObject->IsTouchingGround) {
                return;
            }

            if (mario->IsInShell) {
                return;
            }

            bool rightOrLeft = (inputs.Right.IsDown ^ inputs.Left.IsDown);

            if (mario->WalljumpFrames > 0) {
                mario->FacingRight = physicsObject->Velocity.X > 0;
            } else if (!mario->IsInShell && !mario->IsSliding && !mario->IsSkidding && !mario->IsInKnockback && !(/*animator.GetCurrentAnimatorStateInfo(0).IsName("turnaround") || */ mario->IsTurnaround)) {
                if (rightOrLeft) {
                    mario->FacingRight = inputs.Right.IsDown;
                }
            } else if (/*MegaStartTimer.ExpiredOrNotRunning(Runner) && MegaEndTimer.ExpiredOrNotRunning(Runner) && */ !mario->IsSkidding && !(/*animator.GetCurrentAnimatorStateInfo(0).IsName("turnaround") ||*/ mario->IsTurnaround)) {
                if (mario->IsInKnockback || (physicsObject->IsTouchingGround && mario->CurrentPowerupState != PowerupState.MegaMushroom && FPMath.Abs(physicsObject->Velocity.X) > FP._0_05 && !mario->IsCrouching)) {
                    mario->FacingRight = physicsObject->Velocity.X > 0;
                } else if ((!mario->IsInShell /*|| MegaStartTimer.IsActive(Runner)*/) && (rightOrLeft)) {
                    mario->FacingRight = inputs.Right.IsDown;
                }
                if (!mario->IsInShell && ((FPMath.Abs(physicsObject->Velocity.X) < FP._0_50 && mario->IsCrouching) || physicsObject->IsOnSlipperyGround) && (rightOrLeft)) {
                    mario->FacingRight = inputs.Right.IsDown;
                }
            }
        }

        public void HandleCrouching(Frame f, Filter filter, MarioPlayerPhysicsInfo physics, Input inputs) {
            var mario = filter.MarioPlayer;
            var physicsObject = filter.PhysicsObject;

            if (mario->WasTouchingGroundLastFrame && !physicsObject->IsTouchingGround) {
                physicsObject->Velocity.Y = mario->IsCrouching ? physics.CrouchOffEdgeVelocity : 0;
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
            mario->IsCrouching = ((physicsObject->IsTouchingGround && inputs.Down.IsDown && !mario->IsGroundpounding) || (!physicsObject->IsTouchingGround && (inputs.Down.IsDown || (physicsObject->Velocity.Y > 0 && mario->CurrentPowerupState != PowerupState.BlueShell)) && mario->IsCrouching && !mario->IsInWater) || (mario->IsCrouching && ForceCrouchCheck(f, filter, physics, inputs))) && !mario->HeldEntity.IsValid;

            if (!wasCrouching && mario->IsCrouching) {
                f.Events.MarioPlayerCrouched(f, filter.Entity, *mario);
            }
        }

        public bool ForceCrouchCheck(Frame f, Filter filter, MarioPlayerPhysicsInfo physics, Input inputs) {
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

        public void HandleGroundpound(Frame f, Filter filter, MarioPlayerPhysicsInfo physics, Input inputs, VersusStageData stage) {
            var mario = filter.MarioPlayer;
            var physicsObject = filter.PhysicsObject;

            QuantumUtils.Decrement(ref mario->GroundpoundCooldownFrames);
            QuantumUtils.Decrement(ref mario->PropellerDrillCooldown);

            if (inputs.Down.WasPressed || (mario->IsPropellerFlying && inputs.Down.IsDown)) {
                TryStartGroundpound(f, filter, physics, inputs);
            }

            HandleGroundpoundStartAnimation(filter, physics);
            HandleGroundpoundBlockCollision(f, filter, physics, stage);

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

        private void TryStartGroundpound(Frame f, Filter filter, MarioPlayerPhysicsInfo physics, Input inputs) {

            var mario = filter.MarioPlayer;
            var physicsObject = filter.PhysicsObject;

            if (physicsObject->IsTouchingGround || mario->IsInKnockback || mario->IsGroundpounding || mario->IsDrilling
                || mario->HeldEntity.IsValid || mario->IsCrouching || mario->IsSliding || mario->IsInShell
                || mario->IsWallsliding || mario->GroundpoundCooldownFrames > 0 || mario->IsInWater) {
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

        private void HandleGroundpoundStartAnimation(Filter filter, MarioPlayerPhysicsInfo physics) {
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

        private void HandleGroundpoundBlockCollision(Frame f, Filter filter, MarioPlayerPhysicsInfo physics, VersusStageData stage) {
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

        public void HandleBlueShell(Frame f, Filter filter, MarioPlayerPhysicsInfo physics, Input inputs, VersusStageData stage) {
            var mario = filter.MarioPlayer;
            var physicsObject = filter.PhysicsObject;

            mario->IsInShell &= inputs.Sprint.IsDown;
            if (!mario->IsInShell) {
                return;
            }
            
            if (mario->IsInShell && (physicsObject->IsTouchingLeftWall || physicsObject->IsTouchingRightWall)) {
                bool? playBumpSound = null;
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

                        playBumpSound &= (playBumpSound ?? true) & tempPlayBumpSound;
                    }
                }
                
                mario->FacingRight = physicsObject->IsTouchingLeftWall;
                if (playBumpSound ?? true) {
                    f.Events.PlayBumpSound(f, filter.Entity);
                }
            }

            physicsObject->Velocity.X = physics.WalkMaxVelocity[physics.RunSpeedStage] * physics.WalkBlueShellMultiplier * (mario->FacingRight ? 1 : -1) * (1 - (((FP) mario->ShellSlowdownFrames) / 60));
        }

        private bool HandleMegaMushroom(Frame f, Filter filter, MarioPlayerPhysicsInfo physics, VersusStageData stage) {
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

        private void HandlePowerups(Frame f, Filter filter, MarioPlayerPhysicsInfo physics, Input inputs, VersusStageData stage) {
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

            if (mario->IsDead || /*mario->IsFrozen || */ mario->IsGroundpounding || mario->IsInKnockback || mario->CurrentPipe.IsValid || mario->HeldEntity.IsValid || mario->IsCrouching || mario->IsSliding) {
                return;
            }

            switch (mario->CurrentPowerupState) {
            case PowerupState.IceFlower:
            case PowerupState.FireFlower: {
                if (!fireballReady || mario->IsWallsliding || mario->JumpState == JumpState.TripleJump || mario->IsSpinnerFlying || mario->IsDrilling || mario->IsSkidding || mario->IsTurnaround) {
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

        private void HandleSwimming(Frame f, Filter filter, MarioPlayerPhysicsInfo physics, Input inputs) {
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

        private void HandleSliding(Frame f, Filter filter, MarioPlayerPhysicsInfo physics, Input inputs) {
            var mario = filter.MarioPlayer;
            var physicsObject = filter.PhysicsObject;

            if (physicsObject->IsOnSlideableGround 
                && (!((mario->FacingRight && physicsObject->IsTouchingRightWall) || (!mario->FacingRight && physicsObject->IsTouchingLeftWall))
                && mario->IsCrouching && FPMath.Abs(physicsObject->FloorAngle) >= physics.SlideMinimumAngle
                && !mario->IsInShell && mario->CurrentPowerupState != PowerupState.MegaMushroom)) {

                mario->IsSliding = true;
                mario->IsCrouching = false;
            }
            if (mario->IsSliding && physicsObject->IsTouchingGround && FPMath.Abs(physicsObject->FloorAngle) > physics.SlideMinimumAngle) {
                FP runningMaxSpeed = physics.WalkMaxVelocity[physics.RunSpeedStage];
                FP angleDeg = physicsObject->FloorAngle * FP.Deg2Rad;

                bool uphill = FPMath.Sign(physicsObject->FloorAngle) != FPMath.Sign(physicsObject->Velocity.X);
                FP speed = f.DeltaTime * 5 * (uphill ? FPMath.Clamp01(1 - (FPMath.Abs(physicsObject->Velocity.X) / runningMaxSpeed)) : 4);

                physicsObject->Velocity.X = FPMath.Clamp(physicsObject->Velocity.X + (FPMath.Sin(angleDeg) * speed), -(runningMaxSpeed * FP._1_25), runningMaxSpeed * FP._1_25);
                //FP newY = (uphill ? 0 : -FP._1_50) * FPMath.Abs(newX);
                //= new FPVector2(newX, newY);
            }

            if (inputs.Up.IsDown || ((inputs.Left.IsDown ^ inputs.Right.IsDown) && !inputs.Down.IsDown) || (physicsObject->IsOnSlideableGround && FPMath.Abs(physicsObject->FloorAngle) < physics.SlideMinimumAngle && physicsObject->IsTouchingGround && physicsObject->Velocity.X == 0 && !inputs.Down.IsDown) || (mario->FacingRight && physicsObject->IsTouchingRightWall) || (!mario->FacingRight && physicsObject->IsTouchingLeftWall)) {
                mario->IsSliding = false;
            }
        }

        private void HandleHitbox(Frame f, Filter filter, MarioPlayerPhysicsInfo physics) {
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
        }

        private void HandleBreakingBlocks(Frame f, Filter filter, MarioPlayerPhysicsInfo physics, Input inputs, VersusStageData stage) {
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

        private bool HandleDeathAndRespawning(Frame f, Filter filter, VersusStageData stage) {
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
                    bool spawnAgain = !((f.RuntimeConfig.LivesEnabled && mario->Lives == 0) || mario->Disconnected);
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

        public void SpawnReserveItem(Frame f, Filter filter) {
            var mario = filter.MarioPlayer;
            var reserveItem = f.FindAsset(mario->ReserveItem);

            if (!reserveItem || mario->IsDead /*|| MegaStartTimer.IsActive(Runner) || (IsStationaryMegaShrink && MegaEndTimer.IsActive(Runner))*/) {
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
            var stage = f.FindAsset<VersusStageData>(f.Map.UserAsset);
            var filter = f.Filter<MarioPlayer>();
            while (filter.NextUnsafe(out EntityRef entity, out MarioPlayer* mario)) {
                mario->Lives = f.RuntimeConfig.Lives;
                mario->PreRespawn(f, entity, stage);
            }
        }

        public void OnPreTileCollide(Frame f, VersusStageData stage, EntityRef entity, PhysicsContact* contact, bool* allowCollision) {
            if (!f.Unsafe.TryGetPointer(entity, out MarioPlayer* mario)
                || mario->CurrentPowerupState != PowerupState.MegaMushroom) {
                return;
            }

            InteractionDirection direction;
            FP upDot = FPVector2.Dot(contact->Normal, FPVector2.Up);
            if (upDot > FP._0_75) {
                // Ground contact... only allow if groundpounding
                if (!mario->IsGroundpoundActive) {
                    return;
                }
                direction = InteractionDirection.Down;

            } else if (upDot < -FP._0_75) {
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
    }
}