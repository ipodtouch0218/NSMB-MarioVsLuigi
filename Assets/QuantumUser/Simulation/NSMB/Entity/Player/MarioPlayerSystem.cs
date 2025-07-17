using Photon.Deterministic;
using Quantum.Collections;
using Quantum.Profiling;
using System;
using static IInteractableTile;

namespace Quantum {
    [UnityEngine.Scripting.Preserve]
    public unsafe class MarioPlayerSystem : SystemMainThreadEntityFilter<MarioPlayer, MarioPlayerSystem.Filter>, ISignalOnComponentRemoved<Projectile>,
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

        private ComponentGetter<PhysicsObjectSystem.Filter> PhysicsObjectSystemFilterGetter;

        public override void OnInit(Frame f) {
            f.Context.Interactions.Register<MarioPlayer, MarioPlayer>(f, OnMarioMarioInteraction);
            f.Context.Interactions.Register<MarioPlayer, Projectile>(f, OnMarioProjectileInteraction);
            f.Context.Interactions.Register<MarioPlayer, Coin>(f, OnMarioCoinInteraction);
            f.Context.Interactions.Register<MarioPlayer, InvisibleBlock>(f, OnMarioInvisibleBlockInteraction);
            PhysicsObjectSystemFilterGetter = f.Unsafe.ComponentGetter<PhysicsObjectSystem.Filter>();
        }

        public override void Update(Frame f, ref Filter filter, VersusStageData stage) {
            var mario = filter.MarioPlayer;
            var player = mario->PlayerRef;

            // Shuffle RNG.
            _ = mario->RNG.Next();

            Input* inputPtr;
            if (player.IsValid && (inputPtr = f.GetPlayerInput(player)) != null) {
                filter.Inputs = *inputPtr;
            } else {
                filter.Inputs = default;
            }

            var physics = f.FindAsset(filter.MarioPlayer->PhysicsAsset);
            if (HandleDeathAndRespawning(f, ref filter, stage)) {
                HandleTerminalVelocity(f, ref filter, physics);
                return;
            }

            if (f.GetPlayerCommand(player) is CommandSpawnReserveItem) {
                SpawnReserveItem(f, ref filter);
            }

            if (HandleMegaMushroom(f, ref filter, physics, stage)) {
                HandleHitbox(f, ref filter, physics);
                return;
            }
            if (filter.Freezable->IsFrozen(f)) {
                return;
            }

            if (HandleStuckInBlock(f, ref filter, stage)) {
                HandleCrouching(f, ref filter, physics);
                HandleFacingDirection(f, ref filter, physics);
                HandleHitbox(f, ref filter, physics);
                return;
            }
            HandleKnockback(f, ref filter);

            if (mario->IsInKnockback) {
                // No inputs allowed in knockback.
                filter.Inputs = default;
            }

            bool wasGroundpoundActive = mario->IsGroundpounding;
            HandlePowerups(f, ref filter, physics, stage);
            HandleBreakingBlocks(f, ref filter, physics, stage);
            HandleCrouching(f, ref filter, physics);
            HandleGroundpound(f, ref filter, physics, stage);
            HandleSliding(f, ref filter, physics);
            HandleWalkingRunning(f, ref filter, physics);
            HandleSpinners(f, ref filter, stage);
            HandleJumping(f, ref filter, physics, wasGroundpoundActive);
            HandleSwimming(f, ref filter, physics);
            HandleBlueShell(f, ref filter, physics, stage);
            HandleWallslide(f, ref filter, physics);
            HandleGravity(f, ref filter, physics);
            HandleTerminalVelocity(f, ref filter, physics);
            HandleFacingDirection(f, ref filter, physics);
            HandlePipes(f, ref filter, physics, stage);

            if (HandleHitbox(f, ref filter, physics)) {
                // Attempt to eject if our hitbox changes
                HandleStuckInBlock(f, ref filter, stage);
            }
        }

        public void HandleWalkingRunning(Frame f, ref Filter filter, MarioPlayerPhysicsInfo physics) {
            using var profilerScope = HostProfiler.Start("MarioPlayerSystem.HandleWalkingRunning");
            var mario = filter.MarioPlayer;

            if (!QuantumUtils.Decrement(ref mario->WalljumpFrames)) {
                return;
            }

            var physicsObject = filter.PhysicsObject;

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

            ref var inputs = ref filter.Inputs;
            bool mega = mario->CurrentPowerupState == PowerupState.MegaMushroom;
            bool run = (inputs.Sprint.IsDown || mega || mario->IsPropellerFlying) && (mega || !mario->IsSpinnerFlying);
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
                    if (physicsObject->IsOnSlipperyGround) {
                        acc = mario->KnockForwards ? -physics.StomachKnockbackIceDeceleration : -physics.SittingKnockbackIceDeceleration;
                    } else {
                        acc = mario->KnockForwards ? -physics.StomachKnockbackDeceleration : -physics.SittingKnockbackDeceleration;
                    }
                } else if (swimming) {
                    if (mario->IsCrouching) {
                        acc = -physics.WalkAcceleration[0];
                    } else {
                        acc = -physics.SwimDeceleration;
                    }
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
            /*
            if (!wasInShell && mario->IsInShell) {
                f.Events.MarioPlayerCrouched(filter.Entity, mario->CurrentPowerupState);
            }*/
        }

        private static FP CalculateSlopeMaxSpeedOffset(FP floorAngle) {
            // TODO remove magic constant
            return Constants.WeirdSlopeConstant * floorAngle;
        }

        private void HandleJumping(Frame f, ref Filter filter, MarioPlayerPhysicsInfo physics, bool wasGroundpoundActive) {
            using var profilerScope = HostProfiler.Start("MarioPlayerSystem.HandleJumping");
            ref var inputs = ref filter.Inputs;
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

            if (!physicsObject->WasTouchingGround && physicsObject->IsTouchingGround) {
                // Landed Frame
                mario->LandedFrame = f.Number;
                if (mario->JumpState == JumpState.TripleJump && (!inputs.Left.IsDown && !inputs.Right.IsDown)) {
                    physicsObject->Velocity.X = 0;
                }
                if (mario->PreviousJumpState != JumpState.None && mario->PreviousJumpState == mario->JumpState) {
                    mario->JumpState = JumpState.None;
                }
                if (FPMath.Abs(physicsObject->Velocity.X) < FP._0_05 && !wasGroundpoundActive) {
                    f.Events.MarioPlayerLandedWithAnimation(filter.Entity);
                }
                if (mario->CurrentPowerupState == PowerupState.MegaMushroom) {
                    mario->MegaMushroomFootstepFrames = physics.MegaMushroomStepInterval;
                    f.Signals.OnMarioPlayerMegaMushroomFootstep();
                }
                mario->PreviousJumpState = mario->JumpState;
            }

            bool doJump =
                ((mario->JumpBufferFrames > 0 && (physicsObject->IsTouchingGround || mario->CoyoteTimeFrames > 0))
                || (!physicsObject->IsUnderwater && mario->ForceJumpTimer == 10))
                && mario->CantJumpTimer == 0;

            QuantumUtils.Decrement(ref mario->ForceJumpTimer);
            QuantumUtils.Decrement(ref mario->CoyoteTimeFrames);
            QuantumUtils.Decrement(ref mario->JumpBufferFrames);
            QuantumUtils.Decrement(ref mario->CantJumpTimer);

            if (!mario->DoEntityBounce && (physicsObject->IsBeingCrushed || physicsObject->IsUnderwater || !doJump || mario->IsInKnockback || (mario->CurrentPowerupState == PowerupState.MegaMushroom && mario->JumpState == JumpState.SingleJump) || mario->IsWallsliding)) {
                return;
            }

            if (!mario->DoEntityBounce
                && f.Unsafe.TryGetPointer(mario->CurrentSpinner, out Spinner* Spinner) && mario->ProjectileDelayFrames == 0
                && !f.Exists(mario->HeldEntity) && !mario->IsInShell) {
                // Jump of spinner
                physicsObject->Velocity.Y = physics.SpinnerLaunchVelocity;
                Spinner->PlatformWaitFrames = 6;

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
                physicsObject->WasTouchingGround = false;
                physicsObject->IsTouchingGround = false;

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

                f.Events.MarioPlayerUsedSpinner(filter.Entity, mario->CurrentSpinner);

                mario->CurrentSpinner = EntityRef.None;
                return;
            }

            bool topSpeed = FPMath.Abs(physicsObject->Velocity.X) >= (physics.WalkMaxVelocity[physics.RunSpeedStage] - FP._0_10);
            bool canSpecialJump =
                topSpeed
                && !inputs.Down.IsDown
                && mario->CurrentPowerupState != PowerupState.MegaMushroom
                && (doJump || (mario->DoEntityBounce && inputs.Jump.IsDown))
                && mario->JumpState != JumpState.None
                && !mario->IsSpinnerFlying
                && !mario->IsPropellerFlying
                && ((f.Number - mario->LandedFrame < 12) || mario->DoEntityBounce)
                && !f.Exists(mario->HeldEntity)
                && mario->JumpState != JumpState.TripleJump
                && !mario->IsCrouching
                && !mario->IsInShell
                && (physicsObject->Velocity.X < 0 != mario->FacingRight);

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
            physicsObject->WasTouchingGround = false;
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

            f.Events.MarioPlayerJumped(filter.Entity, mario->CurrentPowerupState, mario->JumpState, mario->DoEntityBounce, false);
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
                        f.Events.LiquidSplashed(contact.Entity, filter.Entity, -1, filter.Transform->Position, true);
                        break;
                    }
                }
            }
        }

        public void HandleGravity(Frame f, ref Filter filter, MarioPlayerPhysicsInfo physics) {
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
            if (swimming && f.Exists(mario->HeldEntity)) {
                gravity = 0;
            } else if (!swimming && (mario->IsSpinnerFlying || mario->IsPropellerFlying)) {
                gravity = mario->IsDrilling ? physics.GravityAcceleration[^1] : physics.GravityFlyingAcceleration;
            } else if ((mario->IsGroundpounding && !swimming) || physicsObject->IsTouchingGround || mario->CoyoteTimeFrames > 0) {
                gravity = mario->GroundpoundStartFrames > 0 ? physics.GravityGroundpoundStart : physics.GravityAcceleration[^1];
            } else {
                int stage = mario->GetGravityStage(physicsObject, physics);
                bool mega = mario->CurrentPowerupState == PowerupState.MegaMushroom;
                bool mini = mario->CurrentPowerupState == PowerupState.MiniMushroom;


                FP[] accArr = swimming ? physics.GravitySwimmingAcceleration : (mega ? physics.GravityMegaAcceleration : (mini ? physics.GravityMiniAcceleration : physics.GravityAcceleration));
                FP acc = accArr[stage];

                ref var inputs = ref filter.Inputs;
                if (stage == 0 && !(inputs.Jump.IsDown || swimming || (!swimming && mario->ForceJumpTimer > 0))) {
                    acc = accArr[^1];
                }

                gravity = acc;
            }

            physicsObject->Gravity = FPVector2.Up * gravity;
        }

        public void HandleTerminalVelocity(Frame f, ref Filter filter, MarioPlayerPhysicsInfo physics) {
            using var profilerScope = HostProfiler.Start("MarioPlayerSystem.HandleTerminalVelocity");
            ref var inputs = ref filter.Inputs;

            var mario = filter.MarioPlayer;
            var physicsObject = filter.PhysicsObject;

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

                FP maxWalkSpeed = physics.WalkMaxVelocity[physics.WalkSpeedStage];

                if (mario->IsDrilling) {
                    terminalVelocity = physics.TerminalVelocityDrilling;
                    physicsObject->Velocity.X = FPMath.Clamp(physicsObject->Velocity.X, -maxWalkSpeed * FP._0_25, maxWalkSpeed * FP._0_25);
                } else {
                    FP remainingTime = mario->PropellerLaunchFrames * f.DeltaTime;
                    // TODO remove magic number
                    FP htv = maxWalkSpeed + (Constants._1_18 * (remainingTime * 2));
                    terminalVelocity = mario->PropellerSpinFrames > 0 ? physics.TerminalVelocityPropellerSpin : physics.TerminalVelocityPropeller;
                    physicsObject->Velocity.X = FPMath.Clamp(physicsObject->Velocity.X, -htv, htv);
                    if (remainingTime > 0) {
                        physicsObject->IsTouchingGround = false;
                        physicsObject->WasTouchingGround = false;
                    }
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

        public void HandleWallslide(Frame f, ref Filter filter, MarioPlayerPhysicsInfo physics) {
            using var profilerScope = HostProfiler.Start("MarioPlayerSystem.HandleWallslide");
            var mario = filter.MarioPlayer;
            var physicsObject = filter.PhysicsObject;

            if (mario->IsInShell || mario->IsGroundpounding || mario->IsCrouching || mario->IsDrilling
                || mario->IsSpinnerFlying || mario->IsInKnockback || physicsObject->IsUnderwater) {
                return;
            }

            ref var inputs = ref filter.Inputs;

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
                HandleWallslideStopChecks(ref filter, currentWallDirection);
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

                    f.Events.MarioPlayerWalljumped(filter.Entity, filter.Transform->Position, mario->WallslideRight, filter.PhysicsCollider->Shape.Box.Extents);
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
        private void HandleWallslideStopChecks(ref Filter filter, FPVector2 wallDirection) {
            using var profilerScope = HostProfiler.Start("MarioPlayerSystem.HandleWallslideStopChecks");
            var mario = filter.MarioPlayer;
            var physicsObject = filter.PhysicsObject;

            // bool floorCheck = !Runner.GetPhysicsScene2D().Raycast(body.Position, Vector2.down, 0.1f, Layers.MaskAnyGround);
            bool moveDownCheck = physicsObject->Velocity.Y < 0;
            // bool heightLowerCheck = Runner.GetPhysicsScene2D().Raycast(body.Position + WallSlideLowerHeightOffset, wallDirection, MainHitbox.size.x * 2, Layers.MaskSolidGround);
            if (physicsObject->IsTouchingGround || !moveDownCheck /* || !heightLowerCheck */) {
                mario->WallslideRight = false;
                mario->WallslideLeft = false;
                mario->WallslideEndFrames = 0;
                return;
            }

            ref var inputs = ref filter.Inputs;

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

            if (mario->IsInKnockback) {
                mario->FacingRight = mario->KnockbackWasOriginallyFacingRight;
                return;
            }

            if (f.Exists(mario->CurrentPipe) || mario->IsInShell || mario->IsCrouchedInShell
                || (mario->IsGroundpounding && !physicsObject->IsTouchingGround)
                || (mario->IsCrouching && physicsObject->IsTouchingGround && FPMath.Abs(physicsObject->Velocity.X) > FP._0_05)) {
                return;
            }

            ref var inputs = ref filter.Inputs;
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

        public void HandleCrouching(Frame f, ref Filter filter, MarioPlayerPhysicsInfo physics) {
            using var profilerScope = HostProfiler.Start("MarioPlayerSystem.HandleCrouching");
            ref var inputs = ref filter.Inputs;
            var mario = filter.MarioPlayer;
            var physicsObject = filter.PhysicsObject;

            if (mario->IsCrouching && physicsObject->WasTouchingGround && !physicsObject->IsTouchingGround && physicsObject->Velocity.Y < FP._0_10) {
                physicsObject->Velocity.Y = mario->IsCrouching ? physics.CrouchOffEdgeVelocity : 0;
                physicsObject->HoverFrames = 0;
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
                    (inputs.Down.IsDown && mario->IsStuckInBlock)
                    || (physicsObject->IsTouchingGround && inputs.Down.IsDown && !mario->IsGroundpounding && !mario->IsSliding)
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

            if (!wasCrouching && mario->IsCrouching && !mario->IsInShell) {
                f.Events.MarioPlayerCrouched(filter.Entity, mario->CurrentPowerupState);
            }
        }

        public void HandleGroundpound(Frame f, ref Filter filter, MarioPlayerPhysicsInfo physics, VersusStageData stage) {
            using var profilerScope = HostProfiler.Start("MarioPlayerSystem.HandleGroundpound");
            ref var inputs = ref filter.Inputs;
            var mario = filter.MarioPlayer;
            var physicsObject = filter.PhysicsObject;

            if (inputs.Down.WasPressed && mario->GroundpoundCooldownFrames == 0) {
                // 4 frame delay
                mario->GroundpoundCooldownFrames = 5;
            }

            bool allowGroundpoundStart = mario->GroundpoundCooldownFrames == 1 || mario->IsPropellerFlying || mario->IsSpinnerFlying;
            QuantumUtils.Decrement(ref mario->GroundpoundCooldownFrames);
            QuantumUtils.Decrement(ref mario->PropellerDrillCooldown);

            if (inputs.Down.IsDown && allowGroundpoundStart) {
                TryStartGroundpound(f, ref filter, physics, stage);
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
                if (physicsObject->IsTouchingGround && physicsObject->WasTouchingGround && (!inputs.Down.IsDown || mario->CurrentPowerupState == PowerupState.MegaMushroom)) {
                    // Cancel from being grounded
                    mario->GroundpoundStandFrames = 15;
                    mario->IsGroundpounding = false;
                } else if (inputs.Up.IsDown && mario->GroundpoundStartFrames == 0) {
                    // Cancel from hitting "up"
                    mario->GroundpoundCooldownFrames = 12;
                    mario->IsGroundpounding = false;
                    mario->IsGroundpoundActive = false;
                }
            }

            // Bodge: i can't find the desync...
            mario->IsGroundpoundActive &= mario->IsGroundpounding;
        }

        private void TryStartGroundpound(Frame f, ref Filter filter, MarioPlayerPhysicsInfo physics, VersusStageData stage) {
            using var profilerScope = HostProfiler.Start("MarioPlayerSystem.TryStartGroundpound");
            ref var inputs = ref filter.Inputs;
            var mario = filter.MarioPlayer;
            var physicsObject = filter.PhysicsObject;

            if (physicsObject->IsTouchingGround || mario->IsInKnockback || mario->IsGroundpounding || mario->IsDrilling
                || mario->HeldEntity.IsValid || mario->IsCrouching || mario->IsSliding || mario->IsInShell
                || mario->IsWallsliding || mario->GroundpoundCooldownFrames > 0 || physicsObject->IsUnderwater
                || f.Exists(mario->CurrentPipe)) {
                return;
            }

            var liquidContacts = f.ResolveHashSet(physicsObject->LiquidContacts);
            foreach (var contact in liquidContacts) {
                if (f.Unsafe.TryGetPointer(contact, out Liquid* liquid) && liquid->LiquidType == LiquidType.Water) {
                    return;
                }
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
                if (PhysicsObjectSystem.Raycast(f, stage, transform->Position, FPVector2.Down, FP._0_50, out _)) {
                    return;
                }

                mario->WallslideLeft = false;
                mario->WallslideRight = false;
                mario->IsGroundpounding = true;
                mario->JumpState = JumpState.None;
                mario->IsSliding = false;
                physicsObject->Velocity = physics.GroundpoundStartVelocity;
                mario->GroundpoundStartFrames = mario->CurrentPowerupState == PowerupState.MegaMushroom ? physics.GroundpoundStartMegaFrames : physics.GroundpoundStartFrames;

                f.Events.MarioPlayerGroundpoundStarted(filter.Entity);
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
            var entity = filter.Entity;
            var mario = filter.MarioPlayer;
            var physicsObject = filter.PhysicsObject;

            if (!(physicsObject->IsTouchingGround && ((mario->IsGroundpounding && mario->IsGroundpoundActive) || mario->IsDrilling))) {
                return;
            }

            if (!mario->IsDrilling) {
                f.Events.MarioPlayerGroundpounded(filter.Entity, mario->CurrentPowerupState);
            }

            bool interactedAny = false;
            bool continueGroundpound = true;
            bool? playBumpSound = null;
            QList<PhysicsContact> contacts = f.ResolveList(physicsObject->Contacts);
            foreach (var contact in contacts) {
                if (FPVector2.Dot(contact.Normal, FPVector2.Up) < Constants.PhysicsGroundMaxAngleCos) {
                    continue;
                }

                if (f.Unsafe.TryGetPointer(contact.Entity, out Interactable* interactable)) {
                    // Entity
                    QBoolean continueTemp = true;
                    f.Signals.OnMarioPlayerGroundpoundedSolid(entity, contact, ref continueTemp);
                    continueGroundpound &= continueTemp;
                    interactedAny = true;
                } else {
                    // Floor tiles.
                    var tileInstance = stage.GetTileRelative(f, contact.Tile);
                    StageTile tile = f.FindAsset(tileInstance.Tile);
                    if (tile is IInteractableTile it) {
                        continueGroundpound &= it.Interact(f, entity, InteractionDirection.Down,
                            contact.Tile, tileInstance, out bool tempPlayBumpSound);
                        interactedAny = true;

                        playBumpSound &= (playBumpSound ?? true) & tempPlayBumpSound;
                    }
                }
            }

            if (playBumpSound ?? false) {
                f.Events.PlayBumpSound(entity);
            }

            continueGroundpound &= interactedAny;

            if (!mario->IsDrilling && !filter.Inputs.Down.IsDown) {
                mario->IsGroundpounding = false;
                continueGroundpound = false;
            }
            if (continueGroundpound && !mario->IsDrilling) {
                f.Signals.OnMarioPlayerGroundpoundEnded(entity);
            }
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
                if (mario->KnockbackGetupFrames == 0) {
                    // Normal knockback
                    bool swimming = physicsObject->IsUnderwater;
                    int framesInKnockback = f.Number - mario->KnockbackTick;
                    if (mario->DoEntityBounce
                        || (swimming && framesInKnockback > 60)
                        || (!swimming && !mario->IsInWeakKnockback && physicsObject->IsTouchingGround && FPMath.Abs(physicsObject->Velocity.X) < FP._0_33 && framesInKnockback > 25)
                        || (!swimming && physicsObject->IsTouchingGround && framesInKnockback > 120)
                        || (!swimming && mario->IsInWeakKnockback && framesInKnockback > 45)) {

                        mario->GetupKnockback(f, entity);
                    }
                } else {
                    // In getup frames
                    if (QuantumUtils.Decrement(ref mario->KnockbackGetupFrames)) {
                        mario->ResetKnockback();
                    }
                }

                mario->WallslideLeft = false;
                mario->WallslideRight = false;
                mario->IsCrouching = false;
                mario->IsInShell = false;
            } else {
                QuantumUtils.Decrement(ref mario->DamageInvincibilityFrames);
            }
        }

        public void HandleBlueShell(Frame f, ref Filter filter, MarioPlayerPhysicsInfo physics, VersusStageData stage) {
            using var profilerScope = HostProfiler.Start("MarioPlayerSystem.HandleBlueShell");
            var mario = filter.MarioPlayer;

            if (mario->CurrentPowerupState != PowerupState.BlueShell) {
                mario->IsInShell = false;
                return;
            }

            ref var inputs = ref filter.Inputs;
            var physicsObject = filter.PhysicsObject;
            var transform = filter.Transform;

            if (f.IsPlayerVerifiedOrLocal(mario->PlayerRef)) {
                mario->IsInShell &= inputs.Sprint.IsDown || (inputs.Down.IsDown && !physicsObject->IsTouchingGround);
            }

            if (!mario->IsInShell) {
                return;
            }

            if (mario->IsInShell && (physicsObject->IsTouchingLeftWall || physicsObject->IsTouchingRightWall)) {
                QList<PhysicsContact> contacts = f.ResolveList(physicsObject->Contacts);
                FPVector2? maxVector = null;
                foreach (var contact in contacts) {
                    if (f.Exists(contact.Entity)) {
                        continue;
                    }

                    FP dot = FPVector2.Dot(contact.Normal, FPVector2.Right);
                    if (FPMath.Abs(dot) < FP._0_75) {
                        continue;
                    }

                    // Wall tiles.
                    IntVector2 tileCoords = contact.Tile;
                    var tileInstance = stage.GetTileRelative(f, tileCoords);
                    StageTile tile = f.FindAsset(tileInstance.Tile);
                    if (tile is IInteractableTile it) {
                        it.Interact(f, filter.Entity, dot > 0 ? InteractionDirection.Right : InteractionDirection.Left,
                            tileCoords, tileInstance, out bool tempPlayBumpSound);
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
                f.Events.PlayBumpSound(filter.Entity);
            }

            physicsObject->Velocity.X = physics.WalkMaxVelocity[physics.RunSpeedStage] * physics.WalkBlueShellMultiplier * (mario->FacingRight ? 1 : -1) * (1 - (((FP) mario->ShellSlowdownFrames) / 60));
        }

        private bool HandleMegaMushroom(Frame f, ref Filter filter, MarioPlayerPhysicsInfo physics, VersusStageData stage) {
            using var profilerScope = HostProfiler.Start("MarioPlayerSystem.HandleMegaMushroom");
            var mario = filter.MarioPlayer;
            var physicsObject = filter.PhysicsObject;
            var transform = filter.Transform;
            var collider = filter.PhysicsCollider;
            var entity = filter.Entity;

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
                mario->CurrentKnockback = KnockbackStrength.None;
                mario->DamageInvincibilityFrames = 0;
                mario->InvincibilityFrames = 0;

                if (QuantumUtils.Decrement(ref mario->MegaMushroomStartFrames)) {
                    // Started
                    mario->MegaMushroomFrames = 15 * 60;
                    physicsObject->IsFrozen = false;

                    Span<PhysicsObjectSystem.LocationTilePair> tiles = stackalloc PhysicsObjectSystem.LocationTilePair[64];
                    int overlappingTiles = PhysicsObjectSystem.GetTilesOverlappingHitbox(f, transform->Position, collider->Shape, tiles, stage);

                    for (int i = 0; i < overlappingTiles; i++) {
                        StageTile stageTile = f.FindAsset(tiles[i].Tile.Tile);
                        if (stageTile is IInteractableTile it) {
                            it.Interact(f, filter.Entity, InteractionDirection.Up, tiles[i].Position, tiles[i].Tile, out _);
                        }
                    }

                    f.Events.MarioPlayerMegaStart(filter.Entity);
                } else {
                    // Still growing...
                    if ((f.Number + filter.Entity.Index) % 4 == 0 && PhysicsObjectSystem.BoxInGround(f, transform->Position, collider->Shape, false, stage)) {
                        // Cancel growing
                        mario->CurrentPowerupState = PowerupState.Mushroom;
                        mario->MegaMushroomEndFrames = (byte) (90 - mario->MegaMushroomStartFrames);
                        mario->MegaMushroomStartFrames = 0;

                        physicsObject->IsFrozen = true;
                        mario->MegaMushroomStationaryEnd = true;
                        mario->SetReserveItem(f, QuantumUtils.FindPowerupAsset(f, PowerupState.MegaMushroom));

                        f.Events.MarioPlayerMegaEnd(filter.Entity, true, mario->MegaMushroomEndFrames);
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
                            if (f.Exists(contact.Entity) || FPVector2.Dot(contact.Normal, FPVector2.Up) < FP._0_33 * 2) {
                                continue;
                            }

                            StageTileInstance tileInstance = stage.GetTileRelative(f, contact.Tile);
                            StageTile tile = f.FindAsset(tileInstance.Tile);

                            if (tile is IInteractableTile it) {
                                it.Interact(f, filter.Entity, InteractionDirection.Down, contact.Tile, tileInstance, out _);
                            }
                        }
                    }

                    mario->JumpState = JumpState.None;
                }

                var contacts = f.ResolveList(physicsObject->Contacts);
                foreach (var contact in contacts) {
                    // Try to break this tile as mega mario...
                    if (f.Exists(contact.Entity)) {
                        continue;
                    }

                    InteractionDirection direction;
                    FP upDot = FPVector2.Dot(contact.Normal, FPVector2.Up);
                    if (upDot > Constants.PhysicsGroundMaxAngleCos) {
                        // Ground contact... only allow if groundpounding
                        if (!mario->IsGroundpoundActive) {
                            continue;
                        }
                        direction = InteractionDirection.Down;
                    } else if (upDot < -Constants.PhysicsGroundMaxAngleCos) {
                        direction = InteractionDirection.Up;
                    } else if (contact.Normal.X < 0) {
                        direction = InteractionDirection.Right;
                    } else {
                        direction = InteractionDirection.Left;
                    }

                    StageTileInstance tileInstance = stage.GetTileRelative(f, contact.Tile);
                    StageTile tile = f.FindAsset(tileInstance.Tile);
                    if (tile is not IInteractableTile it) {
                        continue;
                    }

                    if (!it.Interact(f, entity, direction, contact.Tile, tileInstance, out bool _)) {
                        continue;
                    }

                    PhysicsObjectSystem.Filter physicsSystemFilter = new PhysicsObjectSystem.Filter {
                        Entity = entity,
                        Transform = transform,
                        PhysicsObject = physicsObject,
                        Collider = collider,
                    };

                    // Block broke, preserve velocity.
                    if (direction == InteractionDirection.Left || direction == InteractionDirection.Right) {
                        physicsObject->Velocity.X = physicsObject->PreviousFrameVelocity.X;
                        FP leftoverVelocity = (FPMath.Abs(physicsObject->Velocity.X) - (contact.Distance * f.UpdateRate)) * (physicsObject->Velocity.X > 0 ? 1 : -1);
                        PhysicsObjectSystem.MoveHorizontally(f, new FPVector2(leftoverVelocity, 0), ref physicsSystemFilter, stage, contacts, out _);

                    } else if (direction == InteractionDirection.Up || (direction == InteractionDirection.Down && mario->IsGroundpoundActive)) {
                        physicsObject->Velocity.Y = physicsObject->PreviousFrameVelocity.Y;
                        physicsObject->HoverFrames = 0;
                        physicsObject->IsTouchingGround = false;
                        physicsObject->WasTouchingGround = false;
                        FP leftoverVelocity = (FPMath.Abs(physicsObject->Velocity.Y) - (contact.Distance * f.UpdateRate)) * (physicsObject->Velocity.Y > 0 ? 1 : -1);
                        PhysicsObjectSystem.MoveVertically(f, new FPVector2(0, leftoverVelocity), ref physicsSystemFilter, stage, contacts, out _);
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

                    f.Events.MarioPlayerMegaEnd(filter.Entity, false, mario->MegaMushroomEndFrames);
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

        private void HandlePowerups(Frame f, ref Filter filter, MarioPlayerPhysicsInfo physics, VersusStageData stage) {
            using var profilerScope = HostProfiler.Start("MarioPlayerSystem.HandlePowerups");
            ref var inputs = ref filter.Inputs;
            var mario = filter.MarioPlayer;
            var physicsObject = filter.PhysicsObject;

            if (QuantumUtils.Decrement(ref mario->InvincibilityFrames)) {
                f.Unsafe.GetPointer<ComboKeeper>(filter.Entity)->Combo = 0;
            }
            QuantumUtils.Decrement(ref mario->PropellerSpinFrames);
            QuantumUtils.Decrement(ref mario->ProjectileDelayFrames);
            if (QuantumUtils.Decrement(ref mario->ProjectileVolleyFrames)) {
                mario->CurrentVolley = 0;
            }
            if (mario->CurrentPowerupState == PowerupState.MegaMushroom && (filter.Inputs.Left || filter.Inputs.Right) && !mario->IsInKnockback && physicsObject->IsTouchingGround) {
                if (QuantumUtils.Decrement(ref mario->MegaMushroomFootstepFrames)) {
                    mario->MegaMushroomFootstepFrames = physics.MegaMushroomStepInterval;
                    f.Signals.OnMarioPlayerMegaMushroomFootstep();
                }
            } else {
                mario->MegaMushroomFootstepFrames = (byte) (physics.MegaMushroomStepInterval / 2);
            }

            physicsObject->IsWaterSolid = mario->CurrentPowerupState == PowerupState.MiniMushroom && !mario->IsGroundpounding && mario->StationaryFrames < 15 && (!mario->IsInKnockback || mario->IsInWeakKnockback);
            if (physicsObject->IsWaterSolid && !physicsObject->WasTouchingGround && physicsObject->IsTouchingGround) {
                // Check if we landed on water
                var contacts = f.ResolveList(physicsObject->Contacts);
                foreach (var contact in contacts) {
                    if (f.Has<Liquid>(contact.Entity)) {
                        f.Events.LiquidSplashed(contact.Entity, filter.Entity, 2, filter.Transform->Position, false);
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
                        f.Events.MarioPlayerPropellerSpin(filter.Entity);
                    }
                }
            }

            PowerupState state = mario->CurrentPowerupState;
            if (mario->MegaMushroomStartFrames > 0) {
                return;
            }

            if (!(inputs.PowerupAction.WasPressed
                || (state == PowerupState.PropellerMushroom && inputs.PropellerPowerupAction.WasPressed && !physicsObject->IsTouchingGround && !mario->IsWallsliding)
                || ((state == PowerupState.FireFlower || state == PowerupState.IceFlower || state == PowerupState.HammerSuit) && inputs.FireballPowerupAction.WasPressed))) {
                return;
            }

            if (mario->IsDead || filter.Freezable->IsFrozen(f) || mario->IsGroundpounding || mario->IsInKnockback || f.Exists(mario->CurrentPipe)
                || f.Exists(mario->HeldEntity) || mario->IsCrouching || mario->IsSliding) {
                return;
            }

            switch (mario->CurrentPowerupState) {
            case PowerupState.IceFlower:
            case PowerupState.FireFlower:
            case PowerupState.HammerSuit: {

                if (mario->ProjectileDelayFrames > 0 || mario->IsWallsliding || (mario->JumpState == JumpState.TripleJump && !physicsObject->IsTouchingGround)
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

                Projectile* projectile;
                if (mario->CurrentPowerupState == PowerupState.HammerSuit) {
                    projectile = ShootHammerProjectile(f, ref filter, physics);
                } else {
                    projectile = ShootNormalProjectile(f, ref filter, physics);
                }
                f.Events.MarioPlayerShotProjectile(filter.Entity, *projectile);

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
                mario->CoyoteTimeFrames = 0;

                // Fix sticky ground
                physicsObject->WasTouchingGround = false;
                physicsObject->IsTouchingGround = false;
                physicsObject->HoverFrames = 0;

                PhysicsObjectSystem.Filter physicsSystemFilter = new PhysicsObjectSystem.Filter {
                    Entity = filter.Entity,
                    Transform = filter.Transform,
                    PhysicsObject = physicsObject,
                    Collider = filter.PhysicsCollider,
                };
                PhysicsObjectSystem.MoveVertically(f, FPVector2.Up * FP._0_05 * f.UpdateRate, ref physicsSystemFilter, stage, default, out _);

                f.Events.MarioPlayerUsedPropeller(filter.Entity);
                break;
            }
            }
        }

        private Projectile* ShootHammerProjectile(Frame f, ref Filter filter, MarioPlayerPhysicsInfo physics) {
            var mario = filter.MarioPlayer;
            var physicsObject = filter.PhysicsObject;

            FPVector2 spawnPos = filter.Transform->Position + new FPVector2(mario->FacingRight ? FP._0_25 : -FP._0_25, Constants._0_40);
            EntityRef newEntity = f.Create(f.SimulationConfig.HammerPrototype);

            var projectile = f.Unsafe.GetPointer<Projectile>(newEntity);
            projectile->InitializeHammer(f, newEntity, filter.Entity, spawnPos, mario->FacingRight, false /* filter.Inputs.Up.IsDown */);
            return projectile;
        }


        private Projectile* ShootNormalProjectile(Frame f, ref Filter filter, MarioPlayerPhysicsInfo physics) {
            var mario = filter.MarioPlayer;
            var physicsObject = filter.PhysicsObject;

            FPVector2 spawnPos = filter.Transform->Position + new FPVector2(mario->FacingRight ? FP._0_25 : -FP._0_25, Constants._0_35);

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

            if (FPMath.Abs(physicsObject->Velocity.X) > Constants._0_1875 || physicsObject->Velocity.Y > 0 || mario->IsSliding) {
                mario->StationaryFrames = 0;
            } else if (physicsObject->IsTouchingGround && mario->StationaryFrames < byte.MaxValue) {
                mario->StationaryFrames++;
            }

            if (!physicsObject->IsUnderwater || f.Exists(mario->CurrentPipe)) {
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

                FP desiredVelocity = FPMath.Min(distanceToSurface, physics.SwimJumpVelocity);
                if (desiredVelocity > 0) {
                    physicsObject->Velocity.Y = QuantumUtils.MoveTowards(physicsObject->Velocity.Y, desiredVelocity, physics.SwimAcceleration[^1] * f.DeltaTime * 10);
                }

                physicsObject->IsTouchingGround = false;
                physicsObject->WasTouchingGround = false;
            }

            mario->WallslideLeft = false;
            mario->WallslideRight = false;
            mario->IsSpinnerFlying = false;
            mario->IsCrouching |= physicsObject->IsTouchingGround && mario->IsSliding;
            mario->IsSliding = false;
            mario->IsSkidding = false;
            mario->IsTurnaround = false;
            mario->UsedPropellerThisJump = false;
            mario->IsInShell = false;
            mario->JumpState = JumpState.None;

            if (!mario->IsInKnockback && mario->JumpBufferFrames > 0 && mario->CantJumpTimer == 0) {
                if (physicsObject->IsTouchingGround) {
                    // 1.75x off the ground because reasons
                    physicsObject->Velocity.Y = physics.SwimJumpVelocity * FP._0_75;
                }
                physicsObject->Velocity.Y += physics.SwimJumpVelocity;
                physicsObject->IsTouchingGround = false;
                mario->JumpBufferFrames = 0;
                mario->IsCrouching = false;

                f.Events.MarioPlayerJumped(filter.Entity, mario->CurrentPowerupState, JumpState.None, mario->DoEntityBounce, true);
            }
        }

        private void HandleSliding(Frame f, ref Filter filter, MarioPlayerPhysicsInfo physics) {
            using var profilerScope = HostProfiler.Start("MarioPlayerSystem.HandleSliding");
            ref var inputs = ref filter.Inputs;
            var mario = filter.MarioPlayer;
            var physicsObject = filter.PhysicsObject;
            bool validFloorAngle = FPMath.Abs(physicsObject->FloorAngle) >= physics.SlideMinimumAngle;

            mario->IsCrouching &= !mario->IsSliding;

            if (physicsObject->IsOnSlideableGround
                && validFloorAngle
                && !mario->IsInKnockback
                && !f.Exists(mario->HeldEntity)
                && !((mario->FacingRight && physicsObject->IsTouchingRightWall) || (!mario->FacingRight && physicsObject->IsTouchingLeftWall))
                && (mario->IsCrouching || inputs.Down.IsDown)
                && !mario->IsInShell /* && mario->CurrentPowerupState != PowerupState.MegaMushroom*/
                && !physicsObject->IsUnderwater
                && mario->CurrentPowerupState != PowerupState.HammerSuit) { //Hammer Can't Slide, But Can gp To Slide (Weird Interaction But Works)

                mario->IsSliding = true;
                mario->IsCrouching = false;
            }

            if (!mario->IsSliding) {
                return;
            }

            /*
            if (mario->IsSliding && mario->CurrentPowerupState == PowerupState.MiniMushroom && physicsObject->IsTouchingGround) {
                var contacts = f.ResolveList(physicsObject->Contacts);
                foreach (var contact in contacts) {
                    if (f.Has<Liquid>(contact.Entity)) {
                        mario->IsSliding = false;
                        return;
                    }
                }
            }
            */

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
                f.Events.MarioPlayerStoppedSliding(filter.Entity, stationary);
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

                    f.Events.MarioPlayerEnteredPipe(filter.Entity, mario->CurrentPipe);
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

        private bool HandleHitbox(Frame f, ref Filter filter, MarioPlayerPhysicsInfo physics) {
            using var profilerScope = HostProfiler.Start("MarioPlayerSystem.HandleHitbox");
            var mario = filter.MarioPlayer;
            var physicsObject = filter.PhysicsObject;
            var collider = filter.PhysicsCollider;

            FP newHeight;
            bool crouchHitbox = mario->CurrentPowerupState >= PowerupState.Mushroom && mario->CurrentPowerupState != PowerupState.MegaMushroom && !f.Exists(mario->CurrentPipe) && ((mario->IsCrouching && !mario->IsGroundpounding) || mario->IsInShell || mario->IsSliding);
            bool smallHitbox = mario->CurrentPowerupState != PowerupState.MegaMushroom && ((mario->IsStarmanInvincible && !physicsObject->IsTouchingGround && !crouchHitbox && !mario->IsSliding && !mario->IsSpinnerFlying && !mario->IsPropellerFlying) || mario->IsGroundpounding);
            if (mario->CurrentPowerupState <= PowerupState.MiniMushroom || smallHitbox) {
                newHeight = physics.SmallHitboxHeight;
            } else {
                newHeight = physics.LargeHitboxHeight;
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
            newExtents *= FPMath.Lerp(1, Constants._3_50 + FP._0_25, megaPercentage);
            newExtents.X *= FPMath.Lerp(1, 1 - FP._0_20, megaPercentage);

            bool sameHitbox = collider->Shape.Box.Extents == newExtents
                && collider->Shape.Centroid == FPVector2.Up * newExtents.Y;

            if (sameHitbox) {
                return false;
            }

            collider->Shape.Box.Extents = newExtents;
            collider->Shape.Centroid = FPVector2.Up * newExtents.Y;
            // collider->IsTrigger = mario->IsDead;

            filter.Freezable->IceBlockSize = mario->CurrentPowerupState >= PowerupState.Mushroom ? physics.IceBlockBigSize : physics.IceBlockSmallSize;
            filter.Freezable->Offset = mario->CurrentPowerupState >= PowerupState.Mushroom ? physics.IceBlockBigOffset : physics.IceBlockSmallOffset;
            return true;
        }

        private bool HandleStuckInBlock(Frame f, ref Filter filter, VersusStageData stage) {
            using var profilerScope = HostProfiler.Start("MarioPlayerSystem.HandleStuckInBlock");
            var mario = filter.MarioPlayer;
            var freezable = filter.Freezable;

            QuantumUtils.Decrement(ref mario->CrushDamageInvincibilityFrames);

            if (freezable->IsFrozen(f) || f.Exists(mario->CurrentPipe) || mario->MegaMushroomStartFrames > 0 || (mario->MegaMushroomEndFrames > 0 && mario->MegaMushroomStationaryEnd)) {
                return false;
            }

            var physicsObject = filter.PhysicsObject;
            var transform = filter.Transform;
            Shape2D shape = filter.PhysicsCollider->Shape;

            if (physicsObject->IsBeingCrushed) {
                // In a ceiling crusher
                if (mario->CrushDamageInvincibilityFrames == 0) {
                    mario->CrushDamageInvincibilityFrames = 30;
                    mario->Powerdown(f, filter.Entity, true, filter.Entity);
                }
                return false;
            }

            if (!PhysicsObjectSystem.BoxInGround(f, transform->Position, shape, stage: stage, entity: filter.Entity, includeCeilingCrushers: !physicsObject->IsTouchingGround && (!physicsObject->WasTouchingGround || physicsObject->IsTouchingGround))) {
                if (mario->IsStuckInBlock) {
                    physicsObject->DisableCollision = false;
                    physicsObject->Velocity = FPVector2.Zero;
                    mario->IsStuckInBlock = false;
                }

                return false;
            }

            bool wasStuckLastTick = mario->IsStuckInBlock;

            if (!wasStuckLastTick || (f.Number + filter.Entity.Index) % 4 == 0) {
                // Code for mario to instantly teleport to the closest free position when he gets stuck
                if (PhysicsObjectSystem.TryEject(f, filter.Entity, stage)) {
                    physicsObject->DisableCollision = false;
                    if (wasStuckLastTick) {
                        physicsObject->Velocity = FPVector2.Zero;
                    }
                    mario->IsStuckInBlock = false;
                    return false;
                }
            }

            mario->IsStuckInBlock = true;
            mario->CurrentKnockback = KnockbackStrength.None;
            mario->IsGroundpounding = false;
            mario->IsPropellerFlying = false;
            mario->IsDrilling = false;
            mario->IsSpinnerFlying = false;
            physicsObject->IsTouchingGround = false;
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
                    || FPVector2.Dot(contact.Normal, FPVector2.Down) < Constants.PhysicsGroundMaxAngleCos) {
                    continue;
                }

                // Ceiling tiles.
                var tileInstance = stage.GetTileRelative(f, contact.Tile);
                StageTile tile = f.FindAsset(tileInstance.Tile);
                if (tile == null) {
                    playBumpSound = false;
                } else if (tile is IInteractableTile it) {
                    it.Interact(f, filter.Entity, InteractionDirection.Up,
                        contact.Tile, tileInstance, out bool tempPlayBumpSound);

                    playBumpSound = (playBumpSound ?? true) & tempPlayBumpSound;
                }
            }

            if (physicsObject->IsUnderwater) {
                // TODO: magic value
                physicsObject->Velocity.Y = -2;
            }
            if (playBumpSound ?? true) {
                f.Events.PlayBumpSound(filter.Entity);
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
                    && FPVector2.Dot(contact.Normal, FPVector2.Up) > Constants.PhysicsGroundMaxAngleCos) {
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
            var spinBlockTransform = f.Unsafe.GetPointer<Transform2D>(currentSpinner);

            FP moveVelocity = QuantumUtils.MoveTowards(transform->Position.X, spinBlockTransform->Position.X, 4) - transform->Position.X;

            if (FPMath.Abs(moveVelocity) > 0) {
                PhysicsObjectSystem.Filter physicsSystemFilter = new PhysicsObjectSystem.Filter {
                    Entity = filter.Entity,
                    Transform = transform,
                    PhysicsObject = physicsObject,
                    Collider = filter.PhysicsCollider,
                };
                PhysicsObjectSystem.MoveHorizontally(f, new FPVector2(moveVelocity, 0), ref physicsSystemFilter, stage, contacts, out _);
            }
        }

        private bool HandleDeathAndRespawning(Frame f, ref Filter filter, VersusStageData stage) {
            using var profilerScope = HostProfiler.Start("MarioPlayerSystem.HandleDeathAndRespawning");

            var mario = filter.MarioPlayer;
            var transform = filter.Transform;
            var collider = filter.PhysicsCollider;
            var entity = filter.Entity;

            if (!mario->IsDead) {
                if (transform->Position.Y + (collider->Shape.Box.Extents.Y * 2) < stage.StageWorldMin.Y) {
                    // Death via pit
                    mario->Death(f, entity, false, true, filter.Entity);
                    return true;
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
                if (!doRespawn && mario->GamemodeData.StarChasers->Stars > 0) {
                    // Try to drop more stars
                    f.Signals.OnMarioPlayerDropObjective(entity, 1, filter.Entity);
                    mario->DeathAnimationFrames = 30;
                    mario->PreRespawnFrames = 180;
                } else {
                    // Play the animation as normal
                    if (transform->Position.Y > stage.StageWorldMin.Y) {
                        var physicsObject = filter.PhysicsObject;
                        physicsObject->Gravity = DeathUpGravity;
                        physicsObject->Velocity = DeathUpForce;
                        physicsObject->IsFrozen = false;
                        physicsObject->DisableCollision = true;
                        f.Events.MarioPlayerDeathUp(filter.Entity, mario->FireDeath);
                    }
                    if (!doRespawn) {
                        mario->PreRespawnFrames = 144;
                    }
                }
            }

            return true;
        }

        public static void SpawnItem(Frame f, EntityRef marioEntity, MarioPlayer* mario, AssetRef<EntityPrototype> prefab, bool fromBlock) {
            var gamemode = f.FindAsset(f.Global->Rules.Gamemode);
            if (!prefab.IsValid) {
                prefab = gamemode.GetRandomItem(f, mario, fromBlock).Prefab;
            }

            EntityRef newEntity = f.Create(prefab);
            if (f.Unsafe.TryGetPointer(newEntity, out CoinItem* coinItem)) {
                coinItem->ParentToPlayer(f, newEntity, marioEntity);
            }
        }

        public void SpawnReserveItem(Frame f, ref Filter filter) {
            var mario = filter.MarioPlayer;
            var reserveItem = f.FindAsset(mario->ReserveItem);

            if (reserveItem == null || mario->IsDead || mario->MegaMushroomStartFrames > 0 || (mario->MegaMushroomStationaryEnd && mario->MegaMushroomEndFrames > 0)) {
                f.Events.MarioPlayerUsedReserveItem(filter.Entity, false);
                return;
            }

            SpawnItem(f, filter.Entity, mario, reserveItem.Prefab, false);
            mario->ReserveItem = default;
            f.Events.MarioPlayerUsedReserveItem(filter.Entity, true);
        }

        #region Interactions
        public static bool OnMarioInvisibleBlockInteraction(Frame f, EntityRef marioEntity, EntityRef invisibleBlockEntity, PhysicsContact contact) {
            var mario = f.Unsafe.GetPointer<MarioPlayer>(marioEntity);
            var invisibleBlock = f.Unsafe.GetPointer<InvisibleBlock>(invisibleBlockEntity);
            var transform = f.Unsafe.GetPointer<Transform2D>(invisibleBlockEntity);

            VersusStageData stage = f.FindAsset<VersusStageData>(f.Map.UserAsset);
            if (stage.GetTileWorld(f, transform->Position).Tile != default) {
                return false;
            }

            StageTileInstance result = new StageTileInstance {
                Tile = invisibleBlock->Tile,
            };
            f.Signals.OnMarioPlayerCollectedCoin(marioEntity, EntityRef.None, transform->Position, true, false);
            BreakableBrickTile.Bump(f, stage, QuantumUtils.WorldToRelativeTile(stage, transform->Position), invisibleBlock->BumpTile, result, false, marioEntity, false);
            return false;
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

            bool damageable = !mario->IsInKnockback
                && mario->CurrentPowerupState != PowerupState.MegaMushroom
                && mario->IsDamageable
                && !((mario->IsCrouchedInShell || mario->IsInShell) && projectileAsset.DoesntEffectBlueShell);

            if (damageable) {
                bool didKnockback = false;
                bool damaged = false;
                switch (projectileAsset.Effect) {
                case ProjectileEffectType.KillEnemiesAndSoftKnockbackPlayers:
                case ProjectileEffectType.Fire:
                    if (dropStars && mario->CurrentPowerupState == PowerupState.MiniMushroom) {
                        damaged = mario->Powerdown(f, marioEntity, false, projectileEntity);
                    }
                    if (!damaged) {
                        didKnockback = mario->DoKnockback(f, marioEntity, !projectile->FacingRight, dropStars ? 1 : 0, KnockbackStrength.FireballBump, projectileEntity);
                        damaged = true;
                    }
                    break;
                case ProjectileEffectType.Freeze:
                    if (dropStars && mario->CurrentPowerupState == PowerupState.MiniMushroom) {
                        damaged = mario->Powerdown(f, marioEntity, false, projectileEntity);
                    } else if (dropStars) {
                        IceBlockSystem.Freeze(f, marioEntity);
                        damaged = true;
                    }
                    
                    if (!damaged) {
                        didKnockback = mario->DoKnockback(f, marioEntity, !projectile->FacingRight, dropStars ? 1 : 0, KnockbackStrength.FireballBump, projectileEntity);
                        damaged = true;
                    }
                    break;
                }

                if (didKnockback) {
                    FPVector2 particlePos = (f.Unsafe.GetPointer<Transform2D>(marioEntity)->Position + f.Unsafe.GetPointer<Transform2D>(projectileEntity)->Position) / 2;
                    f.Events.PlayKnockbackEffect(marioEntity, projectileEntity, KnockbackStrength.FireballBump, particlePos);
                }
            }

            if (damageable || projectileAsset.DestroyOnHit || ((mario->IsCrouchedInShell || mario->IsInShell) && projectileAsset.DoesntEffectBlueShell)) {
                f.Signals.OnProjectileHitEntity(f, projectileEntity, marioEntity);
            }
        }

        public void OnMarioMarioInteraction(Frame f, EntityRef marioAEntity, EntityRef marioBEntity) {
            var marioA = f.Unsafe.GetPointer<MarioPlayer>(marioAEntity);
            var marioB = f.Unsafe.GetPointer<MarioPlayer>(marioBEntity);

            // Don't damage players in the Mega Mushroom grow animation
            if (marioA->MegaMushroomStartFrames > 0 || marioB->MegaMushroomStartFrames > 0) {
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
            FPVector2 avgPosition = (marioATransform->Position + marioBTransform->Position) / 2;
            bool dropStars = marioA->GetTeam(f) != marioB->GetTeam(f);

            QuantumUtils.UnwrapWorldLocations(f, marioATransform->Position, marioBTransform->Position, out FPVector2 marioAPosition, out FPVector2 marioBPosition);
            bool fromRight = marioAPosition.X < marioBPosition.X;
            var stage = f.FindAsset<VersusStageData>(f.Map.UserAsset);

            bool eitherDamageInvincible = marioA->DamageInvincibilityFrames > 0 || marioB->DamageInvincibilityFrames > 0;

            FPVector2 previousMarioAPosition = marioAPosition - (marioAPhysics->Velocity * f.DeltaTime);
            FPVector2 previousMarioBPosition = marioBPosition - (marioBPhysics->Velocity * f.DeltaTime);
            FP dot = FPVector2.Dot((previousMarioAPosition - previousMarioBPosition).Normalized, FPVector2.Up);
            bool marioAAbove = dot > Constants._0_66;
            bool marioBAbove = dot < -Constants._0_66;

            // Mega mushroom cases
            bool marioAMega = marioA->CurrentPowerupState == PowerupState.MegaMushroom;
            bool marioBMega = marioB->CurrentPowerupState == PowerupState.MegaMushroom;
            if (!eitherDamageInvincible) {
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
                        bool damaged = false;
                        damaged |= marioA->DoKnockback(f, marioAEntity, fromRight, 0, KnockbackStrength.CollisionBump, marioBEntity, true);
                        damaged |= marioB->DoKnockback(f, marioBEntity, !fromRight, 0, KnockbackStrength.CollisionBump, marioAEntity, true);

                        if (damaged) {
                            f.Events.PlayKnockbackEffect(marioAEntity, marioBEntity, KnockbackStrength.CollisionBump, avgPosition);
                        }
                    }
                } else if (marioAMega) {
                    if (dropStars) {
                        marioB->Powerdown(f, marioBEntity, false, marioAEntity);
                    } else {
                        bool damaged = marioB->DoKnockback(f, marioBEntity, !fromRight, 0, KnockbackStrength.CollisionBump, marioAEntity, true);
                        if (damaged) {
                            f.Events.PlayKnockbackEffect(marioBEntity, marioAEntity, KnockbackStrength.CollisionBump, avgPosition);
                        }
                    }
                    return;
                } else if (marioBMega) {
                    if (dropStars) {
                        marioA->Powerdown(f, marioAEntity, false, marioBEntity);
                    } else {
                        bool damaged = marioA->DoKnockback(f, marioAEntity, fromRight, 0, KnockbackStrength.CollisionBump, marioBEntity, true);
                        if (damaged) {
                            f.Events.PlayKnockbackEffect(marioAEntity, marioBEntity, KnockbackStrength.CollisionBump, avgPosition);
                        }
                    }
                }
            }

            if (marioAMega || marioBMega) {
                // Already handled mega cases.
                return;
            }

            if (!eitherDamageInvincible) {
                // Starman cases
                bool marioAStarman = marioA->IsStarmanInvincible;
                bool marioBStarman = marioB->IsStarmanInvincible;
                if (marioAStarman && marioBStarman) {
                    bool damaged = false;
                    damaged |= marioA->DoKnockback(f, marioAEntity, fromRight, dropStars ? 1 : 0, KnockbackStrength.CollisionBump, marioBEntity);
                    damaged |= marioB->DoKnockback(f, marioBEntity, !fromRight, dropStars ? 1 : 0, KnockbackStrength.CollisionBump, marioAEntity);

                    if (damaged) {
                        f.Events.PlayKnockbackEffect(marioAEntity, marioBEntity, KnockbackStrength.CollisionBump, avgPosition);
                    }
                    return;
                } else if (marioAStarman) {
                    MarioMarioAttackStarman(f, marioAEntity, marioBEntity, fromRight, dropStars);
                    return;
                } else if (marioBStarman) {
                    MarioMarioAttackStarman(f, marioBEntity, marioAEntity, !fromRight, dropStars);
                    return;
                }

                // Blue shell cases
                bool marioAShell = marioA->IsInShell;
                bool marioBShell = marioB->IsInShell;
                if (marioAShell && marioBShell) {
                    bool damaged = false;
                    damaged |= marioA->DoKnockback(f, marioAEntity, fromRight, dropStars ? 1 : 0, KnockbackStrength.CollisionBump, marioBEntity);
                    damaged |= marioB->DoKnockback(f, marioBEntity, !fromRight, dropStars ? 1 : 0, KnockbackStrength.CollisionBump, marioAEntity);
                    if (damaged) {
                        f.Events.PlayKnockbackEffect(marioAEntity, marioBEntity, KnockbackStrength.CollisionBump, avgPosition);
                    }
                    return;
                } else if (marioAShell) {
                    if (!marioBAbove) {
                        // Hit them, powerdown them
                        marioB->FacingRight = !fromRight;
                        marioB->DoKnockback(f, marioBEntity, !fromRight, 0, KnockbackStrength.Normal, marioAEntity);
                        if (dropStars) {
                            marioB->Powerdown(f, marioBEntity, false, marioAEntity);
                        }
                        marioA->FacingRight = !marioA->FacingRight;
                        f.Events.PlayBumpSound(marioAEntity);
                        return;
                    }
                } else if (marioBShell) {
                    if (!marioAAbove) {
                        // Hit them, powerdown them
                        marioA->FacingRight = fromRight;
                        marioA->DoKnockback(f, marioAEntity, fromRight, 0, KnockbackStrength.Normal, marioBEntity);
                        if (dropStars) {
                            marioA->Powerdown(f, marioAEntity, false, marioBEntity);
                        }
                        marioB->FacingRight = !marioB->FacingRight;
                        f.Events.PlayBumpSound(marioBEntity);
                        return;
                    }
                }

                // Crouched in shell stomps
                if (marioA->IsCrouchedInShell && !marioA->IsInShell && marioBAbove && !marioB->IsGroundpoundActive && !marioB->IsDrilling) {
                    MarioMarioBlueShellStomp(f, stage, marioBEntity, marioAEntity, fromRight);
                    return;
                } else if (marioB->IsCrouchedInShell && !marioB->IsInShell && marioAAbove && !marioA->IsGroundpoundActive && !marioA->IsDrilling) {
                    MarioMarioBlueShellStomp(f, stage, marioAEntity, marioBEntity, fromRight);
                    return;
                }

                // Normal stomps
                if (marioAAbove && (marioAPhysics->Velocity.Y <= 0 || marioBPhysics->Velocity.Y > 0)) {
                    MarioMarioStomp(f, marioAEntity, marioBEntity, fromRight, dropStars);
                    return;
                } else if (marioBAbove && (marioBPhysics->Velocity.Y <= 0 || marioAPhysics->Velocity.Y > 0)) {
                    MarioMarioStomp(f, marioBEntity, marioAEntity, !fromRight, dropStars);
                    return;
                }

                // Collided with them
                bool marioAMini = marioA->CurrentPowerupState == PowerupState.MiniMushroom;
                bool marioBMini = marioB->CurrentPowerupState == PowerupState.MiniMushroom;
                if (!marioA->IsInKnockback && !marioB->IsInKnockback && marioAMini ^ marioBMini) {
                    // Minis
                    bool damaged = false;
                    if (marioAMini) {
                        damaged = marioA->DoKnockback(f, marioAEntity, fromRight, dropStars ? 1 : 0, KnockbackStrength.Normal, marioBEntity);
                    }
                    if (marioBMini) {
                        damaged = marioB->DoKnockback(f, marioBEntity, !fromRight, dropStars ? 1 : 0, KnockbackStrength.Normal, marioAEntity);
                    }
                    if (damaged) {
                        f.Events.PlayKnockbackEffect(marioAEntity, marioBEntity, KnockbackStrength.Normal, avgPosition);
                    }
                    return;
                }
            }

            if ((marioA->DamageInvincibilityFrames <= 0 || marioA->CurrentKnockback != KnockbackStrength.None || marioA->KnockbackGetupFrames > 0) && (!marioA->IsInKnockback || marioAPhysics->IsTouchingGround)
                && (marioB->DamageInvincibilityFrames <= 0 || marioB->CurrentKnockback != KnockbackStrength.None || marioB->KnockbackGetupFrames > 0) && (!marioB->IsInKnockback || marioBPhysics->IsTouchingGround)) {

                if (!marioA->IsInShell && !marioB->IsInShell) {
                    var marioAPhysicsInfo = f.FindAsset(marioA->PhysicsAsset);
                    var marioBPhysicsInfo = f.FindAsset(marioB->PhysicsAsset);
                    if (FPMath.Abs(marioAPhysics->Velocity.X) > marioAPhysicsInfo.WalkMaxVelocity[marioAPhysicsInfo.WalkSpeedStage]
                        || FPMath.Abs(marioBPhysics->Velocity.X) > marioBPhysicsInfo.WalkMaxVelocity[marioBPhysicsInfo.WalkSpeedStage]) {
                        // Bump
                        bool damaged = false;
                        if (marioAPhysics->IsTouchingGround && !marioAPhysics->IsUnderwater) {
                            damaged = marioA->DoKnockback(f, marioAEntity, fromRight, dropStars ? 1 : 0, KnockbackStrength.CollisionBump, marioBEntity, bypassDamageInvincibility: true);
                        } else {
                            marioAPhysics->Velocity.X = marioAPhysicsInfo.WalkMaxVelocity[marioAPhysicsInfo.RunSpeedStage] * (fromRight ? -1 : 1);
                        }

                        if (marioBPhysics->IsTouchingGround && !marioAPhysics->IsUnderwater) {
                            damaged = marioB->DoKnockback(f, marioBEntity, !fromRight, dropStars ? 1 : 0, KnockbackStrength.CollisionBump, marioAEntity, bypassDamageInvincibility: true);
                        } else {
                            marioBPhysics->Velocity.X = marioBPhysicsInfo.WalkMaxVelocity[marioBPhysicsInfo.RunSpeedStage] * (fromRight ? 1 : -1);
                        }

                        if (damaged) {
                            f.Events.PlayKnockbackEffect(marioAEntity, marioBEntity, KnockbackStrength.CollisionBump, avgPosition);
                        }
                        return;
                    }
                }
            }

            if (!eitherDamageInvincible && !marioA->IsInKnockback && !marioB->IsInKnockback) {
                // Collide
                int directionToOtherPlayer = fromRight ? -1 : 1;
                var marioACollider = f.Unsafe.GetPointer<PhysicsCollider2D>(marioAEntity);
                var marioBCollider = f.Unsafe.GetPointer<PhysicsCollider2D>(marioBEntity);
                FP overlap = (marioACollider->Shape.Box.Extents.X + marioBCollider->Shape.Box.Extents.X - FPMath.Abs(marioAPosition.X - marioBPosition.X)) / 2;

                if (overlap > 0) {
                    // Move 
                    PhysicsObjectSystemFilterGetter.TryGet(f, marioAEntity, out var marioAFilter);
                    PhysicsObjectSystemFilterGetter.TryGet(f, marioBEntity, out var marioBFilter);

                    PhysicsObjectSystem.MoveHorizontally(f, new FPVector2(overlap * directionToOtherPlayer * f.UpdateRate, 0), ref marioAFilter, stage, default, out _);
                    PhysicsObjectSystem.MoveHorizontally(f, new FPVector2(overlap * -directionToOtherPlayer * f.UpdateRate, 0), ref marioBFilter, stage, default, out _);

                    // Transfer velocity
                    FP avgVelocityX = (marioAPhysics->Velocity.X + marioBPhysics->Velocity.X) / 2 * FP._0_75;

                    if (FPMath.Abs(marioAPhysics->Velocity.X) > 1) {
                        marioA->LastPushingFrame = f.Number;
                        marioAPhysics->Velocity.X = avgVelocityX;
                    }
                    if (FPMath.Abs(marioBPhysics->Velocity.X) > 1) {
                        marioB->LastPushingFrame = f.Number;
                        marioBPhysics->Velocity.X = avgVelocityX;
                    }
                }
                return;
            }
        }

        private static void MarioMarioAttackStarman(Frame f, EntityRef attacker, EntityRef defender, bool fromRight, bool dropStars) {
            var attackerMario = f.Unsafe.GetPointer<MarioPlayer>(attacker);
            var defenderMario = f.Unsafe.GetPointer<MarioPlayer>(defender);

            bool damaged = false;
            if (defenderMario->CurrentPowerupState == PowerupState.MegaMushroom) {
                // Wait fuck-
                (attacker, defender) = (defender, attacker);
                damaged = attackerMario->DoKnockback(f, defender, fromRight, dropStars ? 1 : 0, KnockbackStrength.CollisionBump, attacker);
            } else {
                if (dropStars) {
                    defenderMario->Powerdown(f, defender, false, attacker);
                } else {
                    damaged = defenderMario->DoKnockback(f, defender, !fromRight, 0, KnockbackStrength.CollisionBump, attacker);
                }
            }

            if (damaged) {
                FPVector2 avgPosition = (f.Unsafe.GetPointer<Transform2D>(attacker)->Position + f.Unsafe.GetPointer<Transform2D>(defender)->Position) / 2;
                f.Events.PlayKnockbackEffect(attacker, defender, KnockbackStrength.CollisionBump, avgPosition);
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
            var defenderPhysicsObject = f.Unsafe.GetPointer<PhysicsObject>(defender);

            // Hit them from above
            attackerMario->DoEntityBounce = defenderMario->CurrentPowerupState != PowerupState.MiniMushroom && !attackerMario->IsGroundpounding && !attackerMario->IsDrilling;
            bool groundpounded = attackerMario->IsGroundpoundActive || attackerMario->IsDrilling;

            if (attackerMario->CurrentPowerupState == PowerupState.MiniMushroom && defenderMario->CurrentPowerupState != PowerupState.MiniMushroom) {
                // Attacker is mini, they arent. special rules.
                if (groundpounded) {
                    bool damaged = defenderMario->DoKnockback(f, defender, !fromRight, dropStars ? 3 : 0, KnockbackStrength.Groundpound, attacker);
                    if (damaged) {
                        FPVector2 avgPosition = (f.Unsafe.GetPointer<Transform2D>(attacker)->Position + f.Unsafe.GetPointer<Transform2D>(defender)->Position) / 2;
                        f.Events.PlayKnockbackEffect(defender, attacker, KnockbackStrength.Groundpound, avgPosition);
                    }
                    attackerMario->IsGroundpounding = false;
                    attackerMario->DoEntityBounce = true;
                    if (!attackerMario->IsSpinnerFlying && !attackerMario->IsPropellerFlying) {
                        attackerMario->ForceJumpTimer = 8;
                    }
                }
            } else if (defenderMario->CurrentPowerupState == PowerupState.MiniMushroom && groundpounded) {
                // We are big, groundpounding a mini opponent. squish.
                bool damaged = false;
                if (dropStars) {
                    damaged = defenderMario->Powerdown(f, defender, false, attacker);
                }
                if (!damaged) {
                    defenderMario->DoKnockback(f, defender, !fromRight, 0, KnockbackStrength.Groundpound, attacker);
                }
                attackerMario->DoEntityBounce = false;
            } else if (defenderMario->CurrentPowerupState == PowerupState.HammerSuit && defenderPhysicsObject->IsTouchingGround && defenderMario->IsCrouching && !groundpounded) {
                // Bounce
                var attackerPhysicsObject = f.Unsafe.GetPointer<PhysicsObject>(attacker);
                if (FPMath.Abs(attackerPhysicsObject->Velocity.X) < 2) {
                    attackerPhysicsObject->Velocity.X = fromRight ? -2 : 2;
                }
                attackerPhysicsObject->Velocity.Y = 4;
                attackerMario->DoEntityBounce = false;
                f.Events.EnemyKicked(defender, false);
            } else {
                // Normal knockbacks
                if (defenderMario->CurrentPowerupState == PowerupState.MiniMushroom && groundpounded) {
                    defenderMario->Powerdown(f, defender, false, attacker);
                } else {
                    if (!groundpounded && !dropStars) {
                        // Bounce
                        f.Events.MarioPlayerStompedByTeammate(defender);
                    } else {
                        if (attackerMario->IsPropellerFlying && attackerMario->IsDrilling) {
                            attackerMario->IsDrilling = false;
                            attackerMario->DoEntityBounce = true;
                            if (!attackerMario->IsSpinnerFlying && !attackerMario->IsPropellerFlying) {
                                attackerMario->ForceJumpTimer = 8;
                            }
                        }
                        KnockbackStrength strength = groundpounded ? KnockbackStrength.Groundpound : KnockbackStrength.Normal;
                        bool damaged = defenderMario->DoKnockback(f, defender, !fromRight, dropStars ? (groundpounded ? 3 : 1) : 0, strength, attacker);
                        if (damaged) {
                            FPVector2 avgPosition = (f.Unsafe.GetPointer<Transform2D>(attacker)->Position + f.Unsafe.GetPointer<Transform2D>(defender)->Position) / 2;
                            f.Events.PlayKnockbackEffect(defender, attacker, strength, avgPosition);
                        }
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
                mario->PreRespawn(f, entity, stage);
            }
        }

        public void OnBobombExplodeEntity(Frame f, EntityRef bobomb, EntityRef entity) {
            if (f.Unsafe.TryGetPointer(entity, out MarioPlayer* mario)) {
                mario->Powerdown(f, entity, false, bobomb);
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
                    mario->Death(f, entity, true, true, entity);
                    break;
                case LiquidType.Poison:
                    // Kill, normal death
                    mario->Death(f, entity, false, true, entity);
                    break;
                }
            }
        }

        public void OnEntityBumped(Frame f, EntityRef entity, FPVector2 tileWorldPosition, EntityRef bumper, QBoolean fromBelow) {
            if (!f.Unsafe.TryGetPointer(entity, out MarioPlayer* mario)) {
                return;
            }

            if (!fromBelow || mario->IsInKnockback || mario->IsStuckInBlock) {
                return;
            }

            FPVector2 bumperPosition;
            if (f.Unsafe.TryGetPointer(bumper, out Transform2D* bumperTransform)) {
                bumperPosition = bumperTransform->Position;
            } else {
                bumperPosition = tileWorldPosition;
            }
            var marioTransform = f.Unsafe.GetPointer<Transform2D>(entity);
            QuantumUtils.UnwrapWorldLocations(f, marioTransform->Position, bumperPosition, out FPVector2 ourPos, out FPVector2 theirPos);
            bool onRight = ourPos.X > theirPos.X;

            bool damaged = mario->DoKnockback(f, entity, !onRight, 1, KnockbackStrength.Normal, bumper, bypassDamageInvincibility: true);
            if (damaged) {
                f.Events.PlayKnockbackEffect(entity, bumper, KnockbackStrength.Normal, tileWorldPosition);
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
                mario->Death(f, entity, false, true, entity);
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

            var liquidContacts = f.ResolveHashSet(physicsObject->LiquidContacts);
            foreach (var contact in liquidContacts) {
                var liquid = f.Unsafe.GetPointer<Liquid>(contact);
                if (liquid->LiquidType == LiquidType.Poison) {
                    mario->Death(f, entity, false, true, brokenIceBlock);
                    return;
                } else if (liquid->LiquidType == LiquidType.Lava) {
                    mario->Death(f, entity, true, true, brokenIceBlock);
                    return;
                }
            }

            if (breakReason == IceBlockBreakReason.HitWall) {
                // Set facing right to be the wall we hit
                var iceBlockPhysicsObject = f.Unsafe.GetPointer<PhysicsObject>(brokenIceBlock);
                if (iceBlockPhysicsObject->IsTouchingLeftWall) {
                    mario->FacingRight = true;
                } else if (iceBlockPhysicsObject->IsTouchingRightWall) {
                    mario->FacingRight = false;
                }
            }

            bool damaged = false;
            KnockbackStrength strength = KnockbackStrength.Normal;
            switch (breakReason) {
            case IceBlockBreakReason.HitWall:
            case IceBlockBreakReason.BlockBump:
            case IceBlockBreakReason.Fireball:
            case IceBlockBreakReason.Other:
                // Soft knockback, 1 star
                damaged = mario->DoKnockback(f, entity, mario->FacingRight, 1, (strength = KnockbackStrength.FireballBump), brokenIceBlock);
                break;

            case IceBlockBreakReason.Groundpounded:
                // Hard knockback, 2 stars
                damaged = mario->DoKnockback(f, entity, mario->FacingRight, 2, (strength = KnockbackStrength.Normal), brokenIceBlock);
                break;

            case IceBlockBreakReason.Timer:
                // Damage holder, if we can.
                var iceBlockHoldable = f.Unsafe.GetPointer<Holdable>(brokenIceBlock);
                if (f.Unsafe.TryGetPointer(iceBlockHoldable->Holder, out MarioPlayer* holderMario)) {
                    OnMarioMarioInteraction(f, entity, iceBlockHoldable->Holder);
                }
                break;
            default:
                // Fall through.
                break;
            }

            mario->DamageInvincibilityFrames = 120;
            if (damaged) {
                FPVector2 particlePos = f.Unsafe.GetPointer<Transform2D>(brokenIceBlock)->Position;
                particlePos.Y += iceBlock->Size.Y / 2;
                f.Events.PlayKnockbackEffect(entity, brokenIceBlock, strength, particlePos);
            }
        }

        public void OnStageReset(Frame f, QBoolean full) {
            VersusStageData stage = f.FindAsset<VersusStageData>(f.Map.UserAsset);
            var marios = f.Filter<MarioPlayer, Transform2D, PhysicsCollider2D>();
            while (marios.NextUnsafe(out EntityRef entity, out MarioPlayer* mario, out Transform2D* transform, out PhysicsCollider2D* physicsCollider)) {
                Span<PhysicsObjectSystem.LocationTilePair> tiles = stackalloc PhysicsObjectSystem.LocationTilePair[64];
                int overlappingTiles = PhysicsObjectSystem.GetTilesOverlappingHitbox(f, transform->Position, physicsCollider->Shape, tiles, stage);

                if (mario->CurrentPowerupState == PowerupState.MegaMushroom) {
                    for (int i = 0; i < overlappingTiles; i++) {
                        StageTile stageTile = f.FindAsset(tiles[i].Tile.Tile);
                        if (stageTile is IInteractableTile it) {
                            it.Interact(f, entity, InteractionDirection.Up, tiles[i].Position, tiles[i].Tile, out _);
                        }
                    }
                } else if (mario->CurrentPowerupState >= PowerupState.Mushroom) {
                    for (int i = 0; i < overlappingTiles; i++) {
                        StageTile stageTile = f.FindAsset(tiles[i].Tile.Tile);
                        if (stageTile is BreakableBrickTile bbt && bbt.BreakingRules.HasFlag(BreakableBrickTile.BreakableBy.LargeMario)) {
                            f.Events.TileBroken(entity, tiles[i].Position, tiles[i].Tile, false);
                            stage.SetTileRelative(f, tiles[i].Position, default);
                        }
                    }
                }
            }
        }

        public void OnEntityChangeUnderwaterState(Frame f, EntityRef entity, EntityRef liquid, QBoolean underwater) {
            if (!f.Unsafe.TryGetPointer(entity, out MarioPlayer* mario)
                || f.Exists(mario->CurrentPipe)
                || !f.Unsafe.TryGetPointer(entity, out PhysicsObject* physicsObject)) {
                return;
            }

            if (underwater) {
                if (mario->IsInKnockback) {
                    mario->KnockbackTick = f.Number;
                }
                physicsObject->Velocity.Y = mario->IsGroundpounding ? -5 : 0;
                mario->CantJumpTimer = 10;
            } else {
                if (physicsObject->Velocity.Y > 0 && !physicsObject->IsTouchingGround) {
                    mario->ForceJumpTimer = 10;
                }
                mario->CantJumpTimer = 0;
            }
        }

        public void OnEntityFreeze(Frame f, EntityRef entity, EntityRef iceBlock) {
            if (!f.Unsafe.TryGetPointer(entity, out MarioPlayer* mario)) {
                return;
            }

            mario->IsPropellerFlying = false;
            mario->IsSpinnerFlying = false;
            mario->IsDrilling = false;
            mario->IsCrouching = false;
            mario->IsGroundpounding = false;
            mario->IsSliding = false;

            if (f.Unsafe.TryGetPointer(mario->HeldEntity, out Holdable* holdable)) {
                mario->HeldEntity = EntityRef.None;
                holdable->Holder = EntityRef.None;
            }
        }
        #endregion
    }
}