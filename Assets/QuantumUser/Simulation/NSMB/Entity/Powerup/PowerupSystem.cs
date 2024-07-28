using Photon.Deterministic;

namespace Quantum {

    public unsafe class PowerupSystem : SystemMainThreadFilter<PowerupSystem.Filter>, ISignalOnTrigger2D {
        public struct Filter {
            public EntityRef Entity;
            public Transform2D* Transform;
            public Powerup* Powerup;
            public PhysicsObject* PhysicsObject;
        }

        public override void Update(Frame f, ref Filter filter) {
            var powerup = filter.Powerup;
            var physicsObject = filter.PhysicsObject;
            var transform = filter.Transform;

            if (powerup->ParentMarioPlayer.IsValid) {
                // Attached to a player. Don't interact, and follow the player.
                var marioTransform = f.Get<Transform2D>(powerup->ParentMarioPlayer);
                var marioCamera = f.Get<CameraController>(powerup->ParentMarioPlayer);

                // TODO magic value
                transform->Position = new FPVector2(marioTransform.Position.X, marioCamera.CurrentPosition.Y + FP.FromString("1.68"));

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
            HandleCollision(filter, asset);

            if (powerup->AnimationCurveTimer > 0) {
                transform->Position = powerup->AnimationCurveOrigin + new FPVector2(
                    asset.AnimationCurveX.Evaluate(FPMath.Clamp(powerup->AnimationCurveTimer, 0, asset.AnimationCurveX.EndTime - FP._0_10)),
                    asset.AnimationCurveY.Evaluate(FPMath.Clamp(powerup->AnimationCurveTimer, 0, asset.AnimationCurveY.EndTime - FP._0_10))
                );
                powerup->AnimationCurveTimer += f.DeltaTime;
            }

            if (asset.AvoidPlayers && physicsObject->IsTouchingGround) {
                FPVector2? closestMarioPosition = null;
                var allPlayers = f.Filter<Transform2D, MarioPlayer>();
                while (allPlayers.Next(out _, out Transform2D marioTransform, out _)) {
                    if (closestMarioPosition == null || QuantumUtils.WrappedDistance(f, marioTransform.Position, transform->Position) < QuantumUtils.WrappedDistance(f, closestMarioPosition.Value, transform->Position)) {
                        closestMarioPosition = marioTransform.Position;
                    }
                }

                if (closestMarioPosition.HasValue) {
                    powerup->FacingRight = QuantumUtils.WrappedDirectionSign(f, closestMarioPosition.Value, transform->Position) == -1;
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
                    physicsObject->Velocity.Y = asset.BounceStrength;
                    physicsObject->Velocity.X = asset.Speed * (powerup->FacingRight ? 1 : -1);
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
                || !f.TryGet(info.Entity, out PhysicsObject physicsObject)
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

            var currentScriptable = f.FindAsset(mario->CurrentPowerupScriptable);
            var newScriptable = f.FindAsset(powerup->Scriptable);

            // Change the player's powerup state
            PowerupReserveResult result = PowerupCollect(f, mario, physicsObject, newScriptable);

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

        public PowerupReserveResult PowerupCollect(Frame f, MarioPlayer* mario, PhysicsObject physicsObject, PowerupAsset newPowerup) {
            
            if (newPowerup.Type == PowerupType.Starman) {
                mario->InvincibilityFrames = 600;
                return PowerupReserveResult.NoneButPlaySound;
            }

            PowerupState newState = newPowerup.State;
            var currentPowerup = f.FindAsset(mario->CurrentPowerupScriptable);

            // Reserve if it's the same item
            if (mario->CurrentPowerupState == newState) {
                return PowerupReserveResult.ReserveNewPowerup;
            }

            /* TODO
            // Reserve if we cant fit with our new hitbox
            if (mario->State == Enums.PowerupState.MiniMushroom && physicsObject.IsTouchingGround && runner.GetPhysicsScene2D().Raycast(player.body.Position, Vector2.up, 0.3f, Layers.MaskSolidGround)) {
                return PowerupReserveResult.ReserveNewPowerup;
            }
            */

            sbyte currentPowerupStatePriority = currentPowerup ? currentPowerup.StatePriority : (sbyte) -1;
            sbyte newPowerupItemPriority = newPowerup ? newPowerup.ItemPriority : (sbyte) -1;

            // Reserve if we have a higher priority item
            if (currentPowerupStatePriority > newPowerupItemPriority) {
                return PowerupReserveResult.ReserveNewPowerup;
            }

            mario->PreviousPowerupState = mario->CurrentPowerupState;
            mario->CurrentPowerupState = newState;
            mario->CurrentPowerupScriptable = newPowerup;
            //mario->powerupFlash = 2;
            //mario->IsCrouching |= mario->ForceCrouchCheck();
            mario->IsPropellerFlying = false;
            mario->UsedPropellerThisJump = false;
            mario->IsDrilling &= mario->IsSpinnerFlying;
            mario->PropellerLaunchFrames = 0;
            mario->IsInShell = false;

            // Don't give us an extra mushroom
            if (mario->PreviousPowerupState == PowerupState.NoPowerup || (mario->PreviousPowerupState == PowerupState.Mushroom && newState != PowerupState.Mushroom)) {
                return PowerupReserveResult.NoneButPlaySound;
            }

            return PowerupReserveResult.ReserveOldPowerup;
        }
    }
}