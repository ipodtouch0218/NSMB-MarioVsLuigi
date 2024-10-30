using Photon.Deterministic;

namespace Quantum {

    public unsafe class PowerupSystem : SystemMainThreadFilterStage<PowerupSystem.Filter>, ISignalOnTrigger2D, ISignalOnEntityBumped {

        private static readonly FP CameraYOffset = FP.FromString("1.68");
        private static readonly FP BumpForce = FP.FromString("5.5");

        public struct Filter {
            public EntityRef Entity;
            public Transform2D* Transform;
            public Powerup* Powerup;
            public PhysicsObject* PhysicsObject;
            public PhysicsCollider2D* Collider;
        }

        public override void Update(Frame f, ref Filter filter, VersusStageData stage) {
            var powerup = filter.Powerup;
            var physicsObject = filter.PhysicsObject;
            var transform = filter.Transform;

            if (powerup->ParentMarioPlayer.IsValid) {
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
                    /* TODO
                    if (Utils.Utils.IsTileSolidAtWorldLocation(body.Position + hitbox.offset)) {
                        DespawnEntity();
                        return;
                    }
                    */
                    powerup->BlockSpawn = false;
                    physicsObject->IsFrozen = false;
                } else {
                    return;
                }

                return;
            } else if (powerup->LaunchSpawn) {
                // Back to normal layers
                if (QuantumUtils.Decrement(ref powerup->SpawnAnimationFrames)) {
                    powerup->LaunchSpawn = false;
                    f.Events.PowerupBecameActive(f, filter.Entity);
                }
            } else {
                QuantumUtils.Decrement(ref powerup->SpawnAnimationFrames);
                //if () {
                //    powerup->LaunchSpawn = false;
                //    f.Events.PowerupBecameActive(f, filter.Entity);
                //}
            }

            var asset = f.FindAsset(powerup->Scriptable);

            if (asset.AvoidPlayers && physicsObject->IsTouchingGround) {
                FPVector2? closestMarioPosition = null;
                FP? closestDistance = null;
                var allPlayers = f.Filter<Transform2D, MarioPlayer>();
                while (allPlayers.Next(out _, out Transform2D marioTransform, out _)) {
                    FP distance = QuantumUtils.WrappedDistance(stage, marioTransform.Position, transform->Position);
                    if (closestDistance == null || distance < closestDistance) {
                        closestMarioPosition = marioTransform.Position;
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
                if (!PhysicsObjectSystem.BoxInGround(f, transform->Position, filter.Collider->Shape, stage: stage)) {
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

        public void OnTrigger2D(Frame f, TriggerInfo2D info) {
            if (!f.Unsafe.TryGetPointer(info.Entity, out MarioPlayer* mario)
                || mario->IsDead
                || !f.Unsafe.TryGetPointer(info.Entity, out PhysicsObject* physicsObject)
                || !f.Unsafe.TryGetPointer(info.Other, out Powerup* powerup)) {
                return;
            }

            if (f.DestroyPending(info.Other)) {
                // Already collected
                return;
            }

            // Don't be collectable if we're following a player / spawning
            if ((powerup->BlockSpawn && (powerup->SpawnAnimationFrames) > 6) || (!powerup->BlockSpawn && powerup->SpawnAnimationFrames > 0)) {
                return;
            }

            // Don't collect if we're ignoring players (usually, after blue shell spawns from a blue koopa,
            // so we dont collect it instantly)
            if (powerup->IgnorePlayerFrames > 0) {
                return;
            }

            var currentScriptable = QuantumUtils.FindPowerupAsset(f, mario->CurrentPowerupState);
            var newScriptable = f.FindAsset(powerup->Scriptable);

            // Change the player's powerup state
            PowerupReserveResult result = CollectPowerup(f, info.Entity, mario, physicsObject, newScriptable);

            switch (result) {
            case PowerupReserveResult.ReserveOldPowerup: {
                if (mario->CurrentPowerupState != PowerupState.NoPowerup) {
                    mario->SetReserveItem(f, currentScriptable);
                }
                break;
            }
            case PowerupReserveResult.ReserveNewPowerup: {
                mario->SetReserveItem(f, newScriptable);
                break;
            }
            }

            f.Destroy(info.Other);
            f.Events.MarioPlayerCollectedPowerup(f, info.Entity, *mario, result, newScriptable);
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
                return PowerupReserveResult.ReserveNewPowerup;
            }

            if (mario->CurrentPowerupState == PowerupState.MiniMushroom && marioPhysicsObject->IsTouchingGround) {
                Shape2D shape = collider->Shape;
                shape.Box.Extents *= 2;
                shape.Centroid.Y = shape.Box.Extents.Y / 2 + FP._0_01;

                Draw.Shape(f, ref shape, transform->Position);

                if (PhysicsObjectSystem.BoxInGround(f, transform->Position, shape)) {
                    return PowerupReserveResult.ReserveNewPowerup;
                }
            }

            sbyte currentPowerupStatePriority = currentPowerup ? currentPowerup.StatePriority : (sbyte) -1;
            sbyte newPowerupItemPriority = newPowerup ? newPowerup.ItemPriority : (sbyte) -1;

            // Reserve if we have a higher priority item
            if (currentPowerupStatePriority > newPowerupItemPriority) {
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
            mario->IsCrouching |= MarioPlayerSystem.ForceCrouchCheck(f, ref fakeFilter, f.FindAsset(mario->PhysicsAsset));
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