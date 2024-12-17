using Photon.Deterministic;
using Quantum.Collections;
using Quantum.Profiling;
using System;
using UnityEngine;
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
        }

        public override void OnInit(Frame f) {
            f.Context.RegisterInteraction<MarioPlayer, MarioPlayer>(OnMarioMarioInteraction);
            f.Context.RegisterInteraction<MarioPlayer, Projectile>(OnMarioProjectileInteraction);
            f.Context.RegisterInteraction<MarioPlayer, Coin>(OnMarioCoinInteraction);
            f.Context.RegisterInteraction<MarioPlayer, InvisibleBlock>(OnMarioInvisibleBlockInteraction);
        }

        public override void Update(Frame f, ref Filter filter, VersusStageData stage) {
            var mario = filter.MarioPlayer;
            var player = mario->PlayerRef;

            Input input = default;
            if (player.IsValid) {
                input = *f.GetPlayerInput(player);
            }

            if (f.GetPlayerCommand(player) is CommandSpawnReserveItem) {
                SpawnReserveItem(f, ref filter);
            }

            var physicsObject = filter.PhysicsObject;
            var physics = f.FindAsset(filter.MarioPlayer->PhysicsAsset);
            var freezable = filter.Freezable;
            if (HandleDeathAndRespawning(f, ref filter, stage)) {
                HandleTerminalVelocity(f, ref filter, physics, ref input);
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
            HandleKnockback(f, ref filter);

            if (!QuantumUtils.Decrement(ref mario->KnockbackGetupFrames)) {
                // No inputs allowed in getup frames.
                input = default;
            }

            HandlePowerups(f, ref filter, physics, ref input, stage);
            HandleBreakingBlocks(f, ref filter, physics, ref input, stage);
            HandleCrouching(f, ref filter, physics, ref input);
            HandleGroundpound(f, ref filter, physics, ref input, stage);
            HandleSliding(f, ref filter, physics, ref input);
            HandleWalkingRunning(f, ref filter, physics, ref input);
            HandleSpinners(f, ref filter, stage);
            HandleJumping(f, ref filter, physics, ref input);
            HandleSwimming(f, ref filter, physics, ref input);
            HandleBlueShell(f, ref filter, physics, ref input, stage);
            HandleWallslide(f, ref filter, physics, ref input);
            HandleGravity(f, ref filter, physics, ref input);
            HandleTerminalVelocity(f, ref filter, physics, ref input);
            HandleFacingDirection(f, ref filter, physics, ref input);
            HandlePipes(f, ref filter, physics, stage);
            HandleHitbox(f, ref filter, physics);
        }

        public void HandleWalkingRunning(Frame f, ref Filter filter, MarioPlayerPhysicsInfo physics, ref Input inputs) {
            using var profilerScope = HostProfiler.Start("MarioPlayerSystem.HandleWalkingRunning");
            var mario = filter.MarioPlayer;
            var physicsObject = filter.PhysicsObject;

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

            if (mario->IsGroundpounding || mario->IsInShell || f.Exists(mario->CurrentPipe) || mario->JumpLandingFrames > 0
                || !(mario->WalljumpFrames <= 0 || physicsObject->IsTouchingGround || physicsObject->Velocity.Y < 0)) {
                return;
            }

            bool swimming = physicsObject->IsUnderwater;
            if (!physicsObject->IsTouchingGround || swimming) {
                mario->IsSkidding = false;
            }

            bool run = (inputs.Sprint.IsDown || mario->CurrentPowerupState == PowerupState.MegaMushroom || mario->IsPropellerFlying) & !mario->IsSpinnerFlying;
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
                mario->IsTurnaround = physicsObject->IsTouchingGround && !mario->IsCrouching && xVelAbs < physics.WalkMaxVelocity[1] && !physicsObject->IsTouchingLeftWall && !physicsObject->IsTouchingRightWall;
                mario->IsSkidding = mario->IsTurnaround;

                physicsObject->Velocity.X += (physics.FastTurnaroundAcceleration * (mario->FacingRight ? -1 : 1) * f.DeltaTime);
            } else if ((inputs.Left ^ inputs.Right)
                       && (!mario->IsCrouching || (mario->IsCrouching && !physicsObject->IsTouchingGround && mario->CurrentPowerupState != PowerupState.BlueShell))
                       && !mario->IsInKnockback
                       && !mario->IsSliding) {

                // We can walk here
                int direction = inputs.Left ? -1 : 1;
                if (mario->IsSkidding) {
                    direction = -sign;
                }

                bool reverse = physicsObject->Velocity.X != 0 && (direction != sign);

                // Check that we're not going above our limit
                FP max = maxArray[maxStage] + CalculateSlopeMaxSpeedOffset(FPMath.Abs(physicsObject->FloorAngle) * (uphill ? 1 : -1));
                FP maxAcceleration = FPMath.Abs(max - xVelAbs) * f.UpdateRate;
                acc = FPMath.Clamp(acc, -maxAcceleration, maxAcceleration);
                if (xVelAbs > max) {
                    /*
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
                        acc = physics.WalkAcceleration[0] * Constants._0_85;
                    }
                } else {
                    mario->SlowTurnaroundFrames = 0;
                    mario->IsSkidding &= !mario->IsTurnaround;
                }

                FP newX = xVel + (acc * f.DeltaTime * direction);

                if ((xVel < max && newX > max) || (xVel > -max && newX < -max)) {
                    newX = FPMath.Clamp(newX, -max, max);
                }

                if (mario->IsSkidding && !mario->IsTurnaround && (FPMath.Sign(newX) != sign || xVelAbs < FP._0_05)) {
                    // Turnaround
                    mario->FastTurnaroundFrames = 10;
                    newX = 0;
                }

                physicsObject->Velocity.X = newX;

            } else if (physicsObject->IsTouchingGround || swimming) {
                // Not holding anything, sliding, or holding both directions. decelerate
                mario->IsSkidding = false;
                mario->IsTurnaround = false;

                FP angle = FPMath.Abs(physicsObject->FloorAngle);
                if (mario->IsInKnockback) {
                    acc = -physics.KnockbackDeceleration;
                } else if (swimming) {
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
                                && FPMath.Abs(physicsObject->Velocity.X) >= physics.WalkMaxVelocity[physics.RunSpeedStage] * Constants._0_90
                                && (physicsObject->Velocity.X > 0) == mario->FacingRight;

            mario->IsCrouching &= !mario->IsSliding;

            if (!wasInShell && mario->IsInShell) {
                f.Events.MarioPlayerCrouched(f, filter.Entity);
            }
        }

        private static FP CalculateSlopeMaxSpeedOffset(FP floorAngle) {
            // TODO remove magic constant
            return Constants.WeirdSlopeConstant * floorAngle;
        }

        private void HandleJumping(Frame f, ref Filter filter, MarioPlayerPhysicsInfo physics, ref Input inputs) {
            using var profilerScope = HostProfiler.Start("MarioPlayerSystem.HandleJumping");
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

            bool doJump =
                (mario->JumpBufferFrames > 0 && (physicsObject->IsTouchingGround || mario->CoyoteTimeFrames > 0)) 
                || (!physicsObject->IsUnderwater && mario->SwimForceJumpTimer == 10);

            QuantumUtils.Decrement(ref mario->SwimForceJumpTimer);
            QuantumUtils.Decrement(ref mario->CoyoteTimeFrames);
            QuantumUtils.Decrement(ref mario->JumpBufferFrames);

            if (!mario->DoEntityBounce && (physicsObject->IsUnderwater || !doJump || mario->IsInKnockback || (mario->CurrentPowerupState == PowerupState.MegaMushroom && mario->JumpState == JumpState.SingleJump) || mario->IsWallsliding)) {
                return;
            }

            if (!mario->DoEntityBounce
                && f.Unsafe.TryGetPointer(mario->CurrentSpinner, out Spinner* spinner) && spinner->ArmPosition <= FP._0_75
                && !f.Exists(mario->HeldEntity) && !mario->IsInShell) {
                // Jump of spinner
                physicsObject->Velocity.Y = physics.SpinnerLaunchVelocity;
                spinner->PlatformWaitFrames = 6;

                mario->IsSkidding = false;
                mario->IsTurnaround = false;
                mario->IsSliding = false;
                mario->WallslideEndFrames = 0;
                mario->IsGroundpounding = false;
                mario->GroundpoundStartFrames = 0;
                mario->IsDrilling = false;
                mario->IsSpinnerFlying = true;
                mario->IsPropellerFlying = false;
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
            if (FPMath.Sign(physicsObject->Velocity.X) != 0 && FPMath.Sign(physicsObject->Velocity.X) != FPMath.Sign(physicsObject->FloorAngle)) {
                // TODO: what.
                newY += FPMath.Abs(physicsObject->FloorAngle) * FP._0_01 * alpha;
            }

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

            physicsObject->Velocity.Y = newY;

            f.Events.MarioPlayerJumped(f, filter.Entity, mario->JumpState, mario->DoEntityBounce);
            if (mario->DoEntityBounce) {
                mario->IsCrouching = false;
                mario->PropellerDrillCooldown = 30;
            }
            mario->DoEntityBounce = false;

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
        }

        public void HandleGravity(Frame f, ref Filter filter, MarioPlayerPhysicsInfo physics, ref Input inputs) {
            using var profilerScope = HostProfiler.Start("MarioPlayerSystem.HandleGravity");
            var mario = filter.MarioPlayer;
            var physicsObject = filter.PhysicsObject;

            if (physicsObject->IsTouchingGround && !physicsObject->IsUnderwater) {
                physicsObject->Gravity = FPVector2.Up * physics.GravityAcceleration[0];
                return;
            }

            FP gravity;

            // Slow-rise check
            bool swimming = physicsObject->IsUnderwater;
            if (!swimming && (mario->IsSpinnerFlying || mario->IsPropellerFlying)) {
                gravity = mario->IsDrilling ? physics.GravityAcceleration[^1] : physics.GravityFlyingAcceleration;
            } else if ((mario->IsGroundpounding && !swimming) || physicsObject->IsTouchingGround || mario->CoyoteTimeFrames > 0) {
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

        public void HandleTerminalVelocity(Frame f, ref Filter filter, MarioPlayerPhysicsInfo physics, ref Input inputs) {
            using var profilerScope = HostProfiler.Start("MarioPlayerSystem.HandleTerminalVelocity");

            var mario = filter.MarioPlayer;
            var physicsObject = filter.PhysicsObject;

            FP maxWalkSpeed = physics.WalkMaxVelocity[physics.WalkSpeedStage];
            FP terminalVelocity;

            if (mario->IsDead) {
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
            } else if (physicsObject->IsUnderwater && !(mario->IsGroundpounding || mario->IsDrilling)) {
                terminalVelocity = inputs.Jump.IsDown ? physics.SwimTerminalVelocityButtonHeld : physics.SwimTerminalVelocity;
                physicsObject->Velocity.Y = FPMath.Min(physicsObject->Velocity.Y, physics.SwimMaxVerticalVelocity);
            } else if (mario->IsSpinnerFlying) {
                terminalVelocity = mario->IsDrilling ? physics.TerminalVelocityDrilling : physics.TerminalVelocityFlying;
            } else if (mario->IsPropellerFlying) {
                if (mario->IsDrilling) {
                    terminalVelocity = physics.TerminalVelocityDrilling;
                    physicsObject->Velocity.X = FPMath.Clamp(physicsObject->Velocity.X, -maxWalkSpeed * FP._0_25, maxWalkSpeed * FP._0_25);
                } else {
                    FP remainingTime = mario->PropellerLaunchFrames * f.DeltaTime;
                    // TODO remove magic number
                    FP htv = maxWalkSpeed + (Constants._1_18 * (remainingTime * 2));
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

        public void HandleWallslide(Frame f, ref Filter filter, MarioPlayerPhysicsInfo physics, ref Input inputs) {
            using var profilerScope = HostProfiler.Start("MarioPlayerSystem.HandleWallslide");

            var mario = filter.MarioPlayer;
            var physicsObject = filter.PhysicsObject;

            if (mario->IsInShell || mario->IsGroundpounding || mario->IsCrouching || mario->IsDrilling 
                || mario->IsSpinnerFlying || mario->IsInKnockback || physicsObject->IsUnderwater) {
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

            if (mario->IsWallsliding) {
                HandleWallslideStopChecks(ref filter, ref inputs, currentWallDirection);
            }

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

                    f.Events.MarioPlayerWalljumped(f, filter.Entity, filter.Transform->Position, mario->WallslideRight);
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
        private void HandleWallslideStopChecks(ref Filter filter, ref Input inputs, FPVector2 wallDirection) {
            using var profilerScope = HostProfiler.Start("MarioPlayerSystem.HandleWallslideStopChecks");
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
                    filter.Transform->Position -= wallDirection * FP._0_01;
                }
            } else {
                mario->WallslideEndFrames = 0;
            }
        }


        public void HandleFacingDirection(Frame f, ref Filter filter, MarioPlayerPhysicsInfo physics, ref Input inputs) {
            using var profilerScope = HostProfiler.Start("MarioPlayerSystem.HandleFacingDirection");
            var mario = filter.MarioPlayer;
            var physicsObject = filter.PhysicsObject;

            if (f.Exists(mario->CurrentPipe) || mario->IsInShell || mario->IsCrouchedInShell
                || (mario->IsGroundpounding && !physicsObject->IsTouchingGround) 
                || (mario->IsCrouching && physicsObject->IsTouchingGround && FPMath.Abs(physicsObject->Velocity.X) > FP._0_05)) {
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
                if (!mario->IsInShell && ((FPMath.Abs(physicsObject->Velocity.X) < FP._0_50 && mario->IsCrouching) || physicsObject->IsOnSlipperyGround) && (rightOrLeft)) {
                    mario->FacingRight = inputs.Right.IsDown;
                } else if (mario->IsInKnockback || (physicsObject->IsTouchingGround && mario->CurrentPowerupState != PowerupState.MegaMushroom && FPMath.Abs(physicsObject->Velocity.X) > FP._0_05 && !mario->IsCrouching)) {
                    mario->FacingRight = physicsObject->Velocity.X > 0;
                } else if ((!mario->IsInShell || mario->MegaMushroomStartFrames > 0) && (rightOrLeft)) {
                    mario->FacingRight = inputs.Right.IsDown;
                }
            }
        }

        public void HandleCrouching(Frame f, ref Filter filter, MarioPlayerPhysicsInfo physics, ref Input inputs) {
            using var profilerScope = HostProfiler.Start("MarioPlayerSystem.HandleCrouching");
            var mario = filter.MarioPlayer;
            var physicsObject = filter.PhysicsObject;

            if (mario->WasTouchingGroundLastFrame && !physicsObject->IsTouchingGround) {
                if (physicsObject->Velocity.Y < FP._0_10) {
                     physicsObject->Velocity.Y = mario->IsCrouching ? physics.CrouchOffEdgeVelocity : 0;
                }
            }

            // Can't crouch while sliding, flying, or mega.
            if (mario->IsSliding || mario->IsPropellerFlying || mario->IsSpinnerFlying || mario->IsInKnockback || mario->CurrentPowerupState == PowerupState.MegaMushroom
                || mario->IsWallsliding) {
                mario->IsCrouching = false;
                return;
            }

            /*
            // TODO: magic number
            if (!mario->IsCrouching && physicsObject->IsUnderwater && FPMath.Abs(physicsObject->Velocity.X) > FP._0_03) {
                return;
            }
            */

            bool wasCrouching = mario->IsCrouching;
            mario->IsCrouching = 
                (
                    (physicsObject->IsTouchingGround && inputs.Down.IsDown && !mario->IsGroundpounding && !mario->IsSliding) 
                    || (!physicsObject->IsTouchingGround && (inputs.Down.IsDown || (physicsObject->Velocity.Y > 0 && mario->CurrentPowerupState != PowerupState.BlueShell)) && mario->IsCrouching && !physicsObject->IsUnderwater)
                    /* || (mario->IsCrouching && ForceCrouchCheck(f, ref filter, physics)) */
                ) 
                && !mario->HeldEntity.IsValid 
                && !mario->IsInShell;

            if (mario->IsCrouching && mario->CurrentPowerupState == PowerupState.MiniMushroom) {
                var contacts = f.ResolveList(physicsObject->Contacts);
                foreach (var contact in contacts) {
                    if (f.Has<Liquid>(contact.Entity)) {
                        mario->IsCrouching = false;
                        break;
                    }
                }
            }

            if (!wasCrouching && mario->IsCrouching) {
                f.Events.MarioPlayerCrouched(f, filter.Entity);
            }
        }

        public void HandleGroundpound(Frame f, ref Filter filter, MarioPlayerPhysicsInfo physics, ref Input inputs, VersusStageData stage) {
            using var profilerScope = HostProfiler.Start("MarioPlayerSystem.HandleGroundpound");
            var mario = filter.MarioPlayer;
            var physicsObject = filter.PhysicsObject;

            QuantumUtils.Decrement(ref mario->GroundpoundCooldownFrames);
            QuantumUtils.Decrement(ref mario->PropellerDrillCooldown);

            if (inputs.Down.WasPressed || (mario->IsPropellerFlying && inputs.Down.IsDown)) {
                TryStartGroundpound(f, ref filter, physics, ref inputs, stage);
            }

            if (mario->IsDrilling && mario->IsPropellerFlying && inputs.Down.IsDown) {
                mario->PropellerDrillHoldFrames = 15;
            }

            if (QuantumUtils.Decrement(ref mario->PropellerDrillHoldFrames) && mario->IsPropellerFlying && mario->IsDrilling) {
                mario->IsDrilling = false;
                mario->PropellerDrillCooldown = 20;
            }

            HandleGroundpoundStartAnimation(ref filter, physics);
            HandleGroundpoundBlockCollision(f, ref filter, physics, stage);

            if (physicsObject->IsUnderwater && (mario->IsGroundpounding || mario->IsDrilling)) {
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

        private void TryStartGroundpound(Frame f, ref Filter filter, MarioPlayerPhysicsInfo physics, ref Input inputs, VersusStageData stage) {
            using var profilerScope = HostProfiler.Start("MarioPlayerSystem.TryStartGroundpound");
            var mario = filter.MarioPlayer;
            var physicsObject = filter.PhysicsObject;

            if (physicsObject->IsTouchingGround || mario->IsInKnockback || mario->IsGroundpounding || mario->IsDrilling
                || mario->HeldEntity.IsValid || mario->IsCrouching || mario->IsSliding || mario->IsInShell
                || mario->IsWallsliding || mario->GroundpoundCooldownFrames > 0 || physicsObject->IsUnderwater
                || f.Exists(mario->CurrentPipe)) {
                return;
            }

            /// * intentional: remove left/right requirement when groundpounding
            if (!mario->IsPropellerFlying && !mario->IsSpinnerFlying && (inputs.Left.IsDown || inputs.Right.IsDown)) {
                return;
            }
            // */

            if (mario->IsSpinnerFlying) {
                // Start drill
                if (physicsObject->Velocity.Y < 0) {
                    mario->IsDrilling = true;
                    physicsObject->Velocity.X = 0;
                    mario->IsGroundpoundActive = true;
                }
            } else if (mario->IsPropellerFlying) {
                // Start propeller drill
                if (mario->PropellerDrillCooldown == 0) {
                    mario->IsDrilling = true;
                    mario->PropellerLaunchFrames = 0;
                    mario->IsGroundpoundActive = true;
                }
            } else {
                // Start groundpound
                // Check if high enough above ground
                var transform = filter.Transform;
                if (PhysicsObjectSystem.Raycast((FrameThreadSafe) f, stage, transform->Position, FPVector2.Down, FP._0_50, out _)) {
                    return;
                }

                mario->WallslideLeft = false;
                mario->WallslideRight = false;
                mario->IsGroundpounding = true;
                mario->JumpState = JumpState.None;
                mario->IsSliding = false;
                physicsObject->Velocity = physics.GroundpoundStartVelocity;
                mario->GroundpoundStartFrames = mario->CurrentPowerupState == PowerupState.MegaMushroom ? physics.GroundpoundStartMegaFrames : physics.GroundpoundStartFrames;

                f.Events.MarioPlayerGroundpoundStarted(f, filter.Entity);
            }
        }

        private void HandleGroundpoundStartAnimation(ref Filter filter, MarioPlayerPhysicsInfo physics) {
            using var profilerScope = HostProfiler.Start("MarioPlayerSystem.HandleGroundpoundStartAnimation");
            var mario = filter.MarioPlayer;
            var physicsObject = filter.PhysicsObject;

            if (!mario->IsGroundpounding || mario->GroundpoundStartFrames == 0) {
                return;
            }

            if (QuantumUtils.Decrement(ref mario->GroundpoundStartFrames)) {
                mario->IsGroundpoundActive = true;
            }

            physicsObject->Velocity = mario->GroundpoundStartFrames switch {
                   0 => FPVector2.Up * physics.TerminalVelocityGroundpound,
                >= 4 => physics.GroundpoundStartVelocity,
                   _ => FPVector2.Zero
            };
        }

        private void HandleGroundpoundBlockCollision(Frame f, ref Filter filter, MarioPlayerPhysicsInfo physics, VersusStageData stage) {
            using var profilerScope = HostProfiler.Start("MarioPlayerSystem.HandleGroundpoundBlockCollision");
            var mario = filter.MarioPlayer;
            var physicsObject = filter.PhysicsObject;

            if (!(physicsObject->IsTouchingGround && ((mario->IsGroundpounding && mario->IsGroundpoundActive) || mario->IsDrilling))) {
                return;
            }

            if (!mario->IsDrilling) {
                f.Events.MarioPlayerGroundpounded(f, filter.Entity);
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
                    continueGroundpound &= f.Has<IceBlock>(contact.Entity);
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
            mario->IsGroundpoundActive &= continueGroundpound;

            if (!mario->IsGroundpoundActive && physicsObject->IsOnSlideableGround && !mario->IsInShell && FPMath.Abs(physicsObject->FloorAngle) >= physics.SlideMinimumAngle) {
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
            using var profilerScope = HostProfiler.Start("MarioPlayerSystem.HandleKnockback");
            var entity = filter.Entity;
            var mario = filter.MarioPlayer;
            var physicsObject = filter.PhysicsObject;

            if (mario->IsInKnockback) {
                bool swimming = physicsObject->IsUnderwater;
                int framesInKnockback = f.Number - mario->KnockbackTick;
                if (mario->DoEntityBounce
                    || (swimming && framesInKnockback > 90)
                    || (!swimming && physicsObject->IsTouchingGround && FPMath.Abs(physicsObject->Velocity.X) < FP._0_33 && framesInKnockback > 30)
                    || (!swimming && physicsObject->IsTouchingGround && framesInKnockback > 120)
                    || (!swimming && mario->IsInWeakKnockback && framesInKnockback > 30)) {

                    mario->ResetKnockback(f, entity);
                    return;
                }

                mario->WallslideLeft = false;
                mario->WallslideRight = false;
                mario->IsCrouching = false;
                mario->IsInShell = false;
            }
        }

        public void HandleBlueShell(Frame f, ref Filter filter, MarioPlayerPhysicsInfo physics, ref Input inputs, VersusStageData stage) {
            using var profilerScope = HostProfiler.Start("MarioPlayerSystem.HandleBlueShell");
            var mario = filter.MarioPlayer;
            var physicsObject = filter.PhysicsObject;
            var transform = filter.Transform;

            mario->IsInShell &= inputs.Sprint.IsDown || (inputs.Down.IsDown && !physicsObject->IsTouchingGround);
            if (!mario->IsInShell) {
                return;
            }
            
            if (mario->IsInShell && (physicsObject->IsTouchingLeftWall || physicsObject->IsTouchingRightWall)) {
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

        private bool HandleMegaMushroom(Frame f, ref Filter filter, MarioPlayerPhysicsInfo physics, VersusStageData stage) {
            using var profilerScope = HostProfiler.Start("MarioPlayerSystem.HandleMegaMushroom");
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
                    if (f.Number % 4 == 0 && PhysicsObjectSystem.BoxInGround((FrameThreadSafe) f, transform->Position, collider->Shape, false, stage)) {
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

        private void HandlePowerups(Frame f, ref Filter filter, MarioPlayerPhysicsInfo physics, ref Input inputs, VersusStageData stage) {
            using var profilerScope = HostProfiler.Start("MarioPlayerSystem.HandlePowerups");
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

            physicsObject->IsWaterSolid = mario->CurrentPowerupState == PowerupState.MiniMushroom && !mario->IsGroundpounding && mario->StationaryFrames < 15 && (!mario->IsInKnockback || mario->IsInWeakKnockback);
            if (physicsObject->IsWaterSolid && !physicsObject->WasTouchingGround && physicsObject->IsTouchingGround) {
                // Check if we landed on water
                var contacts = f.ResolveList(physicsObject->Contacts);
                foreach (var contact in contacts) {
                    if (f.Has<Liquid>(contact.Entity)) {
                        f.Events.LiquidSplashed(f, contact.Entity, filter.Entity, 2, filter.Transform->Position, false);
                        break;
                    }
                }
            }

            mario->UsedPropellerThisJump &= !physicsObject->IsTouchingGround;
            mario->IsPropellerFlying &= !physicsObject->IsUnderwater;
            if (mario->IsPropellerFlying) {
                if (!QuantumUtils.Decrement(ref mario->PropellerLaunchFrames)) {
                    FP remainingTime = (FP) mario->PropellerLaunchFrames / 60;
                    if (mario->PropellerLaunchFrames > 52) {
                        physicsObject->Velocity.Y = physics.PropellerLaunchVelocity;
                    } else {
                        FP targetVelocity = physics.PropellerLaunchVelocity - (remainingTime < Constants._0_40 ? (1 - (remainingTime * Constants._2_50)) * physics.PropellerLaunchVelocity : 0);
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
                        f.Events.MarioPlayerPropellerSpin(f, filter.Entity);
                    }
                }
            }

            PowerupState state = mario->CurrentPowerupState;

            if (state == PowerupState.MegaMushroom) {
                if (mario->MegaMushroomStartFrames > 0) {
                    return;
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
                        if (!mario->IsGroundpoundActive) {
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
                                PhysicsObjectSystem.MoveHorizontally((FrameThreadSafe) f, leftoverVelocity, filter.Entity, f.FindAsset<VersusStageData>(f.Map.UserAsset));
                            } else if (direction == InteractionDirection.Up) {
                                physicsObject->Velocity.Y = physicsObject->PreviousFrameVelocity.Y;
                                FP leftoverVelocity = (FPMath.Abs(physicsObject->Velocity.Y) - (contact.Distance * f.UpdateRate)) * (physicsObject->Velocity.Y > 0 ? 1 : -1);
                                PhysicsObjectSystem.MoveVertically((FrameThreadSafe) f, leftoverVelocity, filter.Entity, f.FindAsset<VersusStageData>(f.Map.UserAsset));
                            }
                        }
                    }
                }
            }

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

                FPVector2 spawnPos = filter.Transform->Position + new FPVector2(mario->FacingRight ? Constants._0_40 : -Constants._0_40, Constants._0_35);

                EntityRef newEntity = f.Create(mario->CurrentPowerupState == PowerupState.IceFlower
                    ? f.SimulationConfig.IceballPrototype
                    : f.SimulationConfig.FireballPrototype);

                if (f.Unsafe.TryGetPointer(newEntity, out Projectile* projectile)) {
                    projectile->Initialize(f, newEntity, filter.Entity, spawnPos, mario->FacingRight);
                }
                f.Events.MarioPlayerShotProjectile(f, filter.Entity, *projectile);

                // Weird interaction in the main game...
                mario->WalljumpFrames = 0;
                break;
            }
            case PowerupState.PropellerMushroom: {
                if (mario->UsedPropellerThisJump || physicsObject->IsUnderwater || (mario->IsSpinnerFlying && mario->IsDrilling) || mario->IsPropellerFlying || mario->WalljumpFrames > 0) {
                    return;
                }

                mario->PropellerLaunchFrames = physics.PropellerLaunchFrames;
                mario->UsedPropellerThisJump = true;
                mario->PropellerDrillCooldown = 30;

                mario->IsPropellerFlying = true;
                mario->IsSpinnerFlying = false;
                mario->IsCrouching = false;
                mario->JumpState = JumpState.None;
                mario->WallslideLeft = false;
                mario->WallslideRight = false;

                mario->WasTouchingGroundLastFrame = false;
                filter.PhysicsObject->IsTouchingGround = false;
                f.Events.MarioPlayerUsedPropeller(f, filter.Entity);
                break;
            }
            }
        }

        private void HandleSwimming(Frame f, ref Filter filter, MarioPlayerPhysicsInfo physics, ref Input inputs) {
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

            if (f.Unsafe.TryGetPointer(mario->HeldEntity, out Holdable* holdable)) {
                if (holdable->HoldAboveHead) {
                    mario->HeldEntity = EntityRef.None;
                    holdable->Holder = EntityRef.None;
                }
            }

            mario->WallslideLeft = false;
            mario->WallslideRight = false;
            mario->IsSpinnerFlying = false;
            mario->IsSliding = false;
            mario->IsSkidding = false;
            mario->IsTurnaround = false;
            mario->UsedPropellerThisJump = false;
            mario->IsInShell = false;
            mario->JumpState = JumpState.None;

            if (!mario->IsInKnockback && mario->JumpBufferFrames > 0) {
                if (physicsObject->IsTouchingGround) {
                    // 1.75x off the ground because reasons
                    physicsObject->Velocity.Y = physics.SwimJumpVelocity * FP._0_75;
                }
                physicsObject->Velocity.Y += physics.SwimJumpVelocity;
                physicsObject->IsTouchingGround = false;
                mario->JumpBufferFrames = 0;
                mario->IsCrouching = false;

                f.Events.MarioPlayerJumped(f, filter.Entity, JumpState.None, false);
            }
        }

        private void HandleSliding(Frame f, ref Filter filter, MarioPlayerPhysicsInfo physics, ref Input inputs) {
            using var profilerScope = HostProfiler.Start("MarioPlayerSystem.HandleSliding");
            var mario = filter.MarioPlayer;
            var physicsObject = filter.PhysicsObject;
            bool validFloorAngle = FPMath.Abs(physicsObject->FloorAngle) >= physics.SlideMinimumAngle;

            mario->IsCrouching &= !mario->IsSliding;

            if (physicsObject->IsOnSlideableGround 
                && validFloorAngle
                && !f.Exists(mario->HeldEntity)
                && !((mario->FacingRight && physicsObject->IsTouchingRightWall) || (!mario->FacingRight && physicsObject->IsTouchingLeftWall))
                && (mario->IsCrouching || inputs.Down.IsDown)
                && !mario->IsInShell /* && mario->CurrentPowerupState != PowerupState.MegaMushroom*/
                && !physicsObject->IsUnderwater) {

                mario->IsSliding = true;
                mario->IsCrouching = false;
            }

            if (!mario->IsSliding) {
                return;
            }

            if (mario->IsSliding && mario->CurrentPowerupState == PowerupState.MiniMushroom && physicsObject->IsTouchingGround) {
                var contacts = f.ResolveList(physicsObject->Contacts);
                foreach (var contact in contacts) {
                    if (f.Has<Liquid>(contact.Entity)) {
                        mario->IsSliding = false;
                        return;
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
                mario->IsSliding = false;
                f.Events.MarioPlayerStoppedSliding(f, filter.Entity, stationary);
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
            using var profilerScope = HostProfiler.Start("MarioPlayerSystem.HandleHitbox");
            var mario = filter.MarioPlayer;
            var physicsObject = filter.PhysicsObject;
            var collider = filter.PhysicsCollider;

            QuantumUtils.Decrement(ref mario->DamageInvincibilityFrames);

            FPVector2 iceBlockSize = collider->Shape.Box.Extents;
            FP newHeight;
            bool crouchHitbox = mario->CurrentPowerupState != PowerupState.MiniMushroom && !f.Exists(mario->CurrentPipe) && ((mario->IsCrouching && !mario->IsGroundpounding) || mario->IsInShell || mario->IsSliding);
            bool smallHitbox = (mario->IsStarmanInvincible && !physicsObject->IsTouchingGround && !crouchHitbox && !mario->IsSliding && !mario->IsSpinnerFlying && !mario->IsPropellerFlying) || mario->IsGroundpounding;
            if (mario->CurrentPowerupState <= PowerupState.MiniMushroom || smallHitbox) {
                newHeight = physics.SmallHitboxHeight;
                if (smallHitbox) {
                    iceBlockSize.Y = physics.LargeHitboxHeight;
                } else {
                    iceBlockSize.Y = physics.SmallHitboxHeight;
                }
            } else {
                newHeight = physics.LargeHitboxHeight;
                iceBlockSize.Y = physics.LargeHitboxHeight;
            }

            if (crouchHitbox) {
                newHeight *= mario->CurrentPowerupState <= PowerupState.MiniMushroom ? FP._0_75 : FP._0_50;
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

            if (freezable->IsFrozen(f) || f.Exists(mario->CurrentPipe) || mario->MegaMushroomStartFrames > 0 || (mario->MegaMushroomEndFrames > 0 && mario->MegaMushroomStationaryEnd)) {
                return false;
            }

            var physicsObject = filter.PhysicsObject;
            var transform = filter.Transform;
            Shape2D shape = filter.PhysicsCollider->Shape;

            if (!PhysicsObjectSystem.BoxInGround((FrameThreadSafe) f, transform->Position, shape, stage: stage, entity: filter.Entity)) {
                if (mario->IsStuckInBlock) {
                    physicsObject->DisableCollision = false;
                    physicsObject->Velocity = FPVector2.Zero;
                } 
                mario->IsStuckInBlock = false;
                return false;
            }

            bool wasStuckLastTick = mario->IsStuckInBlock;

            mario->IsStuckInBlock = true;
            mario->IsInKnockback = false;
            mario->IsGroundpounding = false;
            mario->IsPropellerFlying = false;
            mario->IsDrilling = false;
            mario->IsSpinnerFlying = false;
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

        private void HandleBreakingBlocks(Frame f, ref Filter filter, MarioPlayerPhysicsInfo physics, ref Input inputs, VersusStageData stage) {
            using var profilerScope = HostProfiler.Start("MarioPlayerSystem.HandleBreakingBlocks");
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
                    if (!tile) {
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
        }

        private void HandleSpinners(Frame f, ref Filter filter, VersusStageData stage) {
            using var profilerScope = HostProfiler.Start("MarioPlayerSystem.HandleSpinners");
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
                PhysicsObjectSystem.MoveHorizontally((FrameThreadSafe) f, moveVelocity, filter.Entity, stage, contacts);
            }
        }

        private bool HandleDeathAndRespawning(Frame f, ref Filter filter, VersusStageData stage) {
            using var profilerScope = HostProfiler.Start("MarioPlayerSystem.HandleDeathAndRespawning");

            var mario = filter.MarioPlayer;
            var transform = filter.Transform;
            var physicsObject = filter.PhysicsObject;
            var collider = filter.PhysicsCollider;
            var entity = filter.Entity;

            if (!mario->IsDead) {
                if (transform->Position.Y + (collider->Shape.Box.Extents.Y * 2) < stage.StageWorldMin.Y) {
                    // Death via pit
                    mario->Death(f, entity, false);
                } else {
                    return false;
                }
            }

            // Respawn timers
            // Actually respawning
            if (mario->IsRespawning) {
                if (QuantumUtils.Decrement(ref mario->RespawnFrames)) {
                    mario->Respawn(f, entity);
                    return false;
                }
                return true;
            }

            // Waiting to prerespawn
            if (QuantumUtils.Decrement(ref mario->PreRespawnFrames)) {
                mario->PreRespawn(f, entity, stage);
                f.Events.StartCameraFadeIn(f, entity);
                return true;

            } else if (mario->PreRespawnFrames == 20) {
                f.Events.StartCameraFadeOut(f, entity);
                return true;
            }
            
            // Death up
            if (mario->DeathAnimationFrames > 0 && QuantumUtils.Decrement(ref mario->DeathAnimationFrames)) {
                bool doRespawn = !mario->Disconnected && (!f.Global->Rules.IsLivesEnabled || mario->Lives > 0);
                if (!doRespawn && mario->Stars > 0) {
                    // Try to drop more stars
                    mario->SpawnStars(f, entity, 1);
                    mario->DeathAnimationFrames = 30;
                } else {
                    // Play the animation as normal
                    if (transform->Position.Y > stage.StageWorldMin.Y) {
                        physicsObject->Gravity = DeathUpGravity;
                        physicsObject->Velocity = DeathUpForce;
                        physicsObject->IsFrozen = false;
                        physicsObject->DisableCollision = true;
                        f.Events.MarioPlayerDeathUp(f, filter.Entity);
                    }
                    if (!doRespawn) {
                        mario->PreRespawnFrames = 144;
                    }
                }
            }

            return true;
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

            if (!reserveItem || mario->IsDead || mario->MegaMushroomStartFrames > 0 || (mario->MegaMushroomStationaryEnd && mario->MegaMushroomEndFrames > 0)) {
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
            var projectile = f.Unsafe.GetPointer<Projectile>(projectileEntity);

            if (projectile->Owner == marioEntity) {
                return;
            }

            var mario = f.Unsafe.GetPointer<MarioPlayer>(marioEntity);
            var projectileAsset = f.FindAsset(projectile->Asset);
            bool dropStars = true;

            if (f.Unsafe.TryGetPointer(projectile->Owner, out MarioPlayer* ownerMario)) {
                dropStars = ownerMario->Team != mario->Team;
            }

            if (!mario->IsInKnockback
                && mario->CurrentPowerupState != PowerupState.MegaMushroom
                && mario->IsDamageable
                && !mario->IsCrouchedInShell && !mario->IsInShell) {

                switch (projectileAsset.Effect) {
                case ProjectileEffectType.Knockback:
                    if (dropStars && mario->CurrentPowerupState == PowerupState.MiniMushroom) {
                        mario->Death(f, marioEntity, false);
                    } else {
                        mario->DoKnockback(f, marioEntity, !projectile->FacingRight, dropStars ? 1 : 0, true, projectileEntity);
                    }
                    break;
                case ProjectileEffectType.Freeze:
                    if (dropStars && mario->CurrentPowerupState == PowerupState.MiniMushroom) {
                        mario->Death(f, marioEntity, false);
                    } else if (dropStars) {
                        IceBlockSystem.Freeze(f, marioEntity);
                    } else {
                        mario->DoKnockback(f, marioEntity, !projectile->FacingRight, dropStars ? 1 : 0, true, projectileEntity);
                    }
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

            // Don't damage players in the Mega Mushroom grow animation
            if (marioA->MegaMushroomStartFrames > 0 || marioB->MegaMushroomFrames > 0) {
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

            var marioATransform = f.Unsafe.GetPointer<Transform2D>(marioAEntity);
            var marioBTransform = f.Unsafe.GetPointer<Transform2D>(marioBEntity);
            var marioAPhysics = f.Unsafe.GetPointer<PhysicsObject>(marioAEntity);
            var marioBPhysics = f.Unsafe.GetPointer<PhysicsObject>(marioBEntity);

            // Hit players
            bool dropStars = marioA->Team != marioB->Team;

            QuantumUtils.UnwrapWorldLocations(f, marioATransform->Position, marioBTransform->Position, out FPVector2 marioAPosition, out FPVector2 marioBPosition);
            bool fromRight = marioAPosition.X < marioBPosition.X;

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

            FP dot = FPVector2.Dot((marioAPosition - marioBPosition).Normalized, FPVector2.Up);
            bool marioAAbove = dot > Constants._0_66;
            bool marioBAbove = dot < -Constants._0_66;

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
                        marioB->DoKnockback(f, marioBEntity, !fromRight, 0, false, marioAEntity);
                    } else {
                        marioB->DoKnockback(f, marioBEntity, !fromRight, 0, true, marioAEntity);
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
                        marioA->DoKnockback(f, marioAEntity, !fromRight, 0, false, marioBEntity);
                    } else {
                        marioA->DoKnockback(f, marioAEntity, !fromRight, 0, true, marioBEntity);
                    }
                    marioB->FacingRight = !marioB->FacingRight;
                    f.Events.PlayBumpSound(f, marioBEntity);
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
                        marioAPhysics->Velocity.X = marioAPhysicsInfo.WalkMaxVelocity[marioAPhysicsInfo.RunSpeedStage] * (fromRight ? -1 : 1);
                    }

                    if (marioBPhysics->IsTouchingGround) {
                        marioB->DoKnockback(f, marioBEntity, !fromRight, dropStars ? 1 : 0, true, marioAEntity);
                    } else {
                        marioBPhysics->Velocity.X = marioBPhysicsInfo.WalkMaxVelocity[marioBPhysicsInfo.RunSpeedStage] * (fromRight ? 1 : -1);
                    }
                } else {
                    // Collide
                    int directionToOtherPlayer = fromRight ? -1 : 1;
                    var marioACollider = f.Unsafe.GetPointer<PhysicsCollider2D>(marioAEntity);
                    var marioBCollider = f.Unsafe.GetPointer<PhysicsCollider2D>(marioBEntity);
                    FP overlap = (marioACollider->Shape.Box.Extents.X + marioBCollider->Shape.Box.Extents.X - FPMath.Abs(marioAPosition.X - marioBPosition.X)) / 2;

                    if (overlap > 0) {
                        // Move 
                        PhysicsObjectSystem.MoveHorizontally((FrameThreadSafe) f, overlap * directionToOtherPlayer * f.UpdateRate, marioAEntity, stage);
                        PhysicsObjectSystem.MoveHorizontally((FrameThreadSafe) f, overlap * -directionToOtherPlayer * f.UpdateRate, marioBEntity, stage);

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
            if (goLeft && PhysicsObjectSystem.Raycast((FrameThreadSafe) f, stage, raycastPosition, FPVector2.Left, FP._0_25, out _)) {
                // Tile to the right. Force go left.
                goLeft = false;
            } else if (!goLeft && PhysicsObjectSystem.Raycast((FrameThreadSafe) f, stage, raycastPosition, FPVector2.Right, FP._0_25, out _)) {
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
            bool groundpounded = attackerMario->IsGroundpoundActive || attackerMario->IsDrilling;

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
                // Normal knockbacks
                if (defenderMario->CurrentPowerupState == PowerupState.MiniMushroom && groundpounded) {
                    defenderMario->Powerdown(f, defender, false);
                } else {
                    if (!groundpounded && !dropStars) {
                        // Bounce
                        f.Events.MarioPlayerStompedByTeammate(f, defender);
                    } else {
                        if (attackerMario->IsPropellerFlying && attackerMario->IsDrilling) {
                            attackerMario->IsDrilling = false;
                            attackerMario->DoEntityBounce = true;
                        }
                        defenderMario->DoKnockback(f, defender, !fromRight, dropStars ? (groundpounded ? 3 : 1) : 0, false, attacker);
                    }
                }
            }
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
                mario->Lives = f.Global->Rules.Lives;
                mario->PreRespawn(f, entity, stage);
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

            if (!exit && mario->CurrentPowerupState == PowerupState.MiniMushroom && !mario->IsGroundpounding) {
                *doSplash = false;
            }

            if (!exit) {
                switch (liquid->LiquidType) {
                case LiquidType.Water:
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
                mario->IsDead = false;
                mario->PlayerRef = PlayerRef.None;
                mario->Death(f, entity, false);
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

            switch (breakReason) {
            case IceBlockBreakReason.BlockBump:
            case IceBlockBreakReason.HitWall:
            case IceBlockBreakReason.Fireball:
            case IceBlockBreakReason.Other:
                // Soft knockback, 1 star
                mario->DoKnockback(f, entity, mario->FacingRight, 1, true, brokenIceBlock);
                break;

            case IceBlockBreakReason.Groundpounded:
                // Hard knockback, 2 stars
                mario->DoKnockback(f, entity, mario->FacingRight, 2, false, brokenIceBlock);
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

                if (!stage) {
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

            if (underwater && mario->IsInKnockback) {
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