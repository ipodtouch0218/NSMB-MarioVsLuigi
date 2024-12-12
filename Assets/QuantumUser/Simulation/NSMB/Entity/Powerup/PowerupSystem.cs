using Photon.Deterministic;

namespace Quantum {

    public unsafe class PowerupSystem : SystemMainThreadFilterStage<PowerupSystem.Filter>, ISignalOnEntityBumped {

        public static readonly FP CameraYOffset = FP.FromString("1.68");
        private static readonly FP BumpForce = Constants._5_50;

        public struct Filter {
            public EntityRef Entity;
            public Transform2D* Transform;
            public Powerup* Powerup;
            public PhysicsObject* PhysicsObject;
            public PhysicsCollider2D* Collider;
        }

        public override void OnInit(Frame f) {
            f.Context.RegisterInteraction<Powerup, MarioPlayer>(OnPowerupMarioInteraction);
        }

        public override void Update(Frame f, ref Filter filter, VersusStageData stage) {
            var powerup = filter.Powerup;
            var physicsObject = filter.PhysicsObject;
            var transform = filter.Transform;

            if (f.Exists(powerup->ParentMarioPlayer)) {
                // Attached to a player. Don't interact, and follow the player.
                var marioTransform = f.Unsafe.GetPointer<Transform2D>(powerup->ParentMarioPlayer);
                var marioCamera = f.Unsafe.GetPointer<CameraController>(powerup->ParentMarioPlayer);

                // TODO magic value
                transform->Position = new FPVector2(marioTransform->Position.X, marioCamera->CurrentPosition.Y + CameraYOffset);

                if (QuantumUtils.Decrement(ref powerup->SpawnAnimationFrames)) {
                    powerup->ParentMarioPlayer = EntityRef.None;
                    physicsObject->IsFrozen = false;
                    f.Events.PowerupBecameActive(f, filter.Entity);
                } else {
                    return;
                }
            } else if (powerup->BlockSpawn) {
                // Spawning from a block. Lerp between origin & destination.
                FP t = 1 - ((FP) powerup->SpawnAnimationFrames / (FP) powerup->BlockSpawnAnimationLength);
                transform->Position = FPVector2.Lerp(powerup->BlockSpawnOrigin, powerup->BlockSpawnDestination, t);

                if (QuantumUtils.Decrement(ref powerup->SpawnAnimationFrames)) {
                    if (PhysicsObjectSystem.BoxInGround((FrameThreadSafe) f, transform->Position, filter.Collider->Shape, false, stage, filter.Entity)) {
                        // TODO: poof effect.
                        f.Destroy(filter.Entity);
                        return;
                    }
                    powerup->BlockSpawn = false;
                    physicsObject->IsFrozen = false;
                    f.Events.PowerupBecameActive(f, filter.Entity);
                } else {
                    return;
                }
                return;
            } else if (powerup->LaunchSpawn) {
                // Back to normal layers
                if (QuantumUtils.Decrement(ref powerup->SpawnAnimationFrames)) {
                    powerup->LaunchSpawn = false;
                    physicsObject->DisableCollision = false;
                    f.Events.PowerupBecameActive(f, filter.Entity);
                }
            } else {
                QuantumUtils.Decrement(ref powerup->SpawnAnimationFrames);
            }

            var asset = f.FindAsset(powerup->Scriptable);

            if (asset.AvoidPlayers && physicsObject->IsTouchingGround) {
                FPVector2? closestMarioPosition = null;
                FP? closestDistance = null;
                var allPlayers = f.Filter<MarioPlayer, Transform2D>();
                while (allPlayers.NextUnsafe(out _, out _, out Transform2D* marioTransform)) {
                    FP distance = QuantumUtils.WrappedDistance(stage, marioTransform->Position, transform->Position);
                    if (closestDistance == null || distance < closestDistance) {
                        closestMarioPosition = marioTransform->Position;
                        closestDistance = distance;
                    }
                }

                if (closestMarioPosition.HasValue) {
                    powerup->FacingRight = QuantumUtils.WrappedDirectionSign(stage, closestMarioPosition.Value, transform->Position) == -1;
                }
            }

            HandleCollision(filter, asset);

            if (powerup->AnimationCurveTimer > 0) {
                transform->Position = powerup->AnimationCurveOrigin + new FPVector2(
                    asset.AnimationCurveX.Evaluate(FPMath.Clamp(powerup->AnimationCurveTimer, 0, asset.AnimationCurveX.EndTime - FP._0_10)),
                    asset.AnimationCurveY.Evaluate(FPMath.Clamp(powerup->AnimationCurveTimer, 0, asset.AnimationCurveY.EndTime - FP._0_10))
                );
                powerup->AnimationCurveTimer += f.DeltaTime;
            }

            if (powerup->SpawnAnimationFrames == 0 && physicsObject->DisableCollision) {
                // Test that we're not in a wall anymore
                if (!PhysicsObjectSystem.BoxInGround((FrameThreadSafe) f, transform->Position, filter.Collider->Shape, stage: stage)) {
                    physicsObject->DisableCollision = false;
                }
            }

            if (QuantumUtils.Decrement(ref powerup->Lifetime)) {
                f.Destroy(filter.Entity);
            }
        }

        public void HandleCollision(Filter filter, PowerupAsset asset) {
            var powerup = filter.Powerup;
            var physicsObject = filter.PhysicsObject;

            if (powerup->AnimationCurveTimer > 0) {
                return;
            }

            if (physicsObject->IsTouchingLeftWall || physicsObject->IsTouchingRightWall) {
                powerup->FacingRight = physicsObject->IsTouchingLeftWall;
                physicsObject->Velocity.X = asset.Speed * (powerup->FacingRight ? 1 : -1);
            }

            if (physicsObject->IsTouchingGround) {
                if (asset.FollowAnimationCurve) {
                    physicsObject->IsFrozen = true;
                    powerup->AnimationCurveOrigin = filter.Transform->Position;
                    powerup->AnimationCurveTimer += FP._0_01;
                } else {
                    physicsObject->Velocity.X = asset.Speed * (powerup->FacingRight ? 1 : -1);
                    if (asset.BounceStrength > 0) {
                        physicsObject->Velocity.Y = FPMath.Max(physicsObject->Velocity.Y, asset.BounceStrength);
                        physicsObject->IsTouchingGround = false;
                    }
                }

                /*
                if (data.HitRoof || (data.HitLeft && data.HitRight)) {
                    DespawnEntity();
                    return;
                }
                */
            }
        }

        public void OnPowerupMarioInteraction(Frame f, EntityRef powerupEntity, EntityRef marioEntity) {
            if (f.DestroyPending(powerupEntity)) {
                // Already collected
                return;
            }

            var powerup = f.Unsafe.GetPointer<Powerup>(powerupEntity);

            // Don't be collectable if we're following a player / spawning
            if ((powerup->BlockSpawn && (powerup->SpawnAnimationFrames) > 6) || (!powerup->BlockSpawn && powerup->SpawnAnimationFrames > 0)) {
                return;
            }

            // Don't collect if we're ignoring players (usually, after blue shell spawns from a blue koopa,
            // so we dont collect it instantly)
            if (powerup->IgnorePlayerFrames > 0) {
                return;
            }

            var mario = f.Unsafe.GetPointer<MarioPlayer>(marioEntity);
            var marioPhysicsObject = f.Unsafe.GetPointer<PhysicsObject>(marioEntity);
            var newScriptable = f.FindAsset(powerup->Scriptable);

            // Change the player's powerup state
            PowerupReserveResult result = CollectPowerup(f, marioEntity, mario, marioPhysicsObject, newScriptable);

            f.Destroy(powerupEntity);
            f.Events.MarioPlayerCollectedPowerup(f, marioEntity, result, newScriptable);
        }

        public static PowerupReserveResult CollectPowerup(Frame f, EntityRef marioEntity, MarioPlayer* mario, PhysicsObject* marioPhysicsObject, PowerupAsset newPowerup) {

            if (newPowerup.Type == PowerupType.Starman) {
                mario->InvincibilityFrames = 600;
                return PowerupReserveResult.NoneButPlaySound;
            }

            PowerupState newState = newPowerup.State;
            var currentPowerup = QuantumUtils.FindPowerupAsset(f, mario->CurrentPowerupState);
            var transform = f.Unsafe.GetPointer<Transform2D>(marioEntity);
            var collider = f.Unsafe.GetPointer<PhysicsCollider2D>(marioEntity);

            // Reserve if it's the same item
            if (mario->CurrentPowerupState == newState) {
                mario->SetReserveItem(f, newPowerup);
                return PowerupReserveResult.ReserveNewPowerup;
            }

            /*
            if (mario->CurrentPowerupState == PowerupState.MiniMushroom && marioPhysicsObject->IsTouchingGround) {
                Shape2D shape = collider->Shape;
                shape.Box.Extents *= 2;
                shape.Centroid.Y = shape.Box.Extents.Y / 2 + FP._0_01;

                Draw.Shape(f, ref shape, transform->Position);

                if (PhysicsObjectSystem.BoxInGround(f, transform->Position, shape)) {
                    return PowerupReserveResult.ReserveNewPowerup;
                }
            }
            */

            sbyte currentPowerupStatePriority = currentPowerup ? currentPowerup.StatePriority : (sbyte) -1;
            sbyte newPowerupItemPriority = newPowerup ? newPowerup.ItemPriority : (sbyte) -1;

            // Reserve if we have a higher priority item
            if (currentPowerupStatePriority > newPowerupItemPriority) {
                mario->SetReserveItem(f, newPowerup);
                return PowerupReserveResult.ReserveNewPowerup;
            }

            if (newState == PowerupState.MegaMushroom) {
                mario->MegaMushroomStartFrames = 90;
                mario->IsSliding = false;
                if (marioPhysicsObject->IsTouchingGround) {
                    mario->JumpState = JumpState.None;
                }
                marioPhysicsObject->IsFrozen = true;
                marioPhysicsObject->Velocity = FPVector2.Zero;
            }

            MarioPlayerSystem.Filter fakeFilter = new() {
                Entity = marioEntity,
                MarioPlayer = mario,
                PhysicsObject = marioPhysicsObject,
                Transform = transform,
                PhysicsCollider = collider
            };

            mario->PreviousPowerupState = mario->CurrentPowerupState;
            mario->CurrentPowerupState = newState;
            //mario->powerupFlash = 2;
            mario->IsPropellerFlying = false;
            mario->UsedPropellerThisJump = false;
            mario->IsDrilling &= mario->IsSpinnerFlying;
            mario->PropellerLaunchFrames = 0;
            mario->IsInShell = false;

            // Don't give us an extra mushroom
            if (mario->PreviousPowerupState == PowerupState.NoPowerup
                || (mario->PreviousPowerupState == PowerupState.Mushroom && newState != PowerupState.Mushroom)) {
                return PowerupReserveResult.NoneButPlaySound;
            }

            if (mario->CurrentPowerupState != PowerupState.NoPowerup) {
                mario->SetReserveItem(f, currentPowerup);
            }
            return PowerupReserveResult.ReserveOldPowerup;
        }

        public void OnEntityBumped(Frame f, EntityRef entity, FPVector2 position, EntityRef bumpOwner) {
            if (!f.Unsafe.TryGetPointer(entity, out Transform2D* transform)
                || !f.Unsafe.TryGetPointer(entity, out Powerup* powerup)
                || !f.Unsafe.TryGetPointer(entity, out PhysicsObject* physicsObject)
                || powerup->SpawnAnimationFrames > 0) {

                return;
            }

            QuantumUtils.UnwrapWorldLocations(f, transform->Position, position, out FPVector2 ourPos, out FPVector2 theirPos);
            physicsObject->Velocity = new FPVector2(
                f.FindAsset(powerup->Scriptable).Speed * (ourPos.X > theirPos.X ? 1 : -1),
                BumpForce
            );
            physicsObject->IsTouchingGround = false;
            powerup->FacingRight = ourPos.X > theirPos.X;
        }
    }
}