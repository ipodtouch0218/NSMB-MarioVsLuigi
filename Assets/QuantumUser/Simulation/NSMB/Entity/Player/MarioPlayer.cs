using Photon.Deterministic;

namespace Quantum {
    public unsafe partial struct MarioPlayer {

        public bool FacingRight {
            readonly get => Flags.IsSet(0); 
            set => SetValue(ref Flags, 0, value); 
        }
        public bool IsSkidding {
            readonly get => Flags.IsSet(1);
            set => SetValue(ref Flags, 1, value);
        }
        public bool IsTurnaround {
            readonly get => Flags.IsSet(2);
            set => SetValue(ref Flags, 2, value);
        }
        public bool DoEntityBounce {
            readonly get => Flags.IsSet(3);
            set => SetValue(ref Flags, 3, value);
        }
        public bool WallslideLeft {
            readonly get => Flags.IsSet(4);
            set => SetValue(ref Flags, 4, value);
        }
        public bool WallslideRight {
            readonly get => Flags.IsSet(5);
            set => SetValue(ref Flags, 5, value);
        }
        public bool IsGroundpounding {
            readonly get => Flags.IsSet(6);
            set => SetValue(ref Flags, 6, value);
        }
        public bool IsGroundpoundActive {
            readonly get => Flags.IsSet(7);
            set => SetValue(ref Flags, 7, value);
        }
        public bool IsInWeakKnockback {
            readonly get => Flags.IsSet(8);
            set => SetValue(ref Flags, 8, value);
        }
        public bool KnockForwards {
            readonly get => Flags.IsSet(9);
            set => SetValue(ref Flags, 9, value);
        }
        public bool KnockbackWasOriginallyFacingRight {
            readonly get => Flags.IsSet(10);
            set => SetValue(ref Flags, 10, value);
        }
        public bool IsCrouching {
            readonly get => Flags.IsSet(11);
            set => SetValue(ref Flags, 11, value);
        }
        public bool IsSliding {
            readonly get => Flags.IsSet(12);
            set => SetValue(ref Flags, 12, value);
        }
        public bool IsSpinnerFlying {
            readonly get => Flags.IsSet(13);
            set => SetValue(ref Flags, 13, value);
        }
        public bool IsDrilling {
            readonly get => Flags.IsSet(14);
            set => SetValue(ref Flags, 14, value);
        }
        public bool IsStuckInBlock {
            readonly get => Flags.IsSet(15);
            set => SetValue(ref Flags, 15, value);
        }
        public bool MegaMushroomStationaryEnd {
            readonly get => Flags.IsSet(16);
            set => SetValue(ref Flags, 16, value);
        }
        public bool IsInShell {
            readonly get => Flags.IsSet(17);
            set => SetValue(ref Flags, 17, value);
        }
        public bool IsPropellerFlying {
            readonly get => Flags.IsSet(18);
            set => SetValue(ref Flags, 18, value);
        }
        public bool UsedPropellerThisJump {
            readonly get => Flags.IsSet(19);
            set => SetValue(ref Flags, 19, value);
        }
        public bool PipeEntering {
            readonly get => Flags.IsSet(20);
            set => SetValue(ref Flags, 20, value);
        }

        public readonly bool IsStarmanInvincible => InvincibilityFrames > 0;
        public readonly bool IsWallsliding => WallslideLeft || WallslideRight;
        public readonly bool IsCrouchedInShell => CurrentPowerupState == PowerupState.BlueShell && (IsCrouching || IsGroundpounding && GroundpoundStartFrames <= 11) && !IsInShell;
        public readonly bool IsInKnockback => CurrentKnockback != KnockbackStrength.None;
        public readonly bool CanCollectOwnTeamsObjectiveCoins => !IsInKnockback && DamageInvincibilityFrames == 0;
        public readonly bool IsStarmanOrMega => IsStarmanInvincible || CurrentPowerupState == PowerupState.MegaMushroom;
        public readonly bool IsValid(Frame f) => !Disconnected && !(f.Global->Rules.IsLivesEnabled && Lives == 0);
        public readonly bool IsDamageable(Frame f) => !IsStarmanInvincible && DamageInvincibilityFrames == 0 && !TryGetCurrentPowerTransition(f, out _);

        /**
         * <summary>Outputs a pointer to the current transition animation Mario is in, if he is in one.</summary>
         * <returns><strong>true</strong> if in a transition otherwise <strong>false</strong>.</returns>
         */
        public readonly bool TryGetCurrentPowerTransition(Frame f, out PowerupTransitionAnimation* transition) {
            transition = null;
            var queue = f.ResolveList(PowerupTransitionQueue);

            if (queue.Count == 0) {
                return false;
            }

            transition = queue.GetPointer(0);
            return true;
        }

        public readonly byte? GetTeam(Frame f) {
            var data = QuantumUtils.GetPlayerData(f, PlayerRef);
            if (data == null) {
                return null;
            } else {
                return (byte) (data->RealTeam % Constants.MaxPlayers);
            }
        }

        public readonly FPVector2 GetHeldItemOffset(Frame f, EntityRef marioEntity) {
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
                    (marioShape.Box.Extents.Y * (2 - FP._0_05) * alpha) + holdableYOffset
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

        public readonly bool CanHoldItem(Frame f, EntityRef entity, EntityRef item) {
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

            return (input.Sprint.IsDown || forceHold || (f.Exists(HeldEntity) && !f.IsPlayerVerifiedOrLocal(PlayerRef)))
                && !freezable->IsFrozen(f) && CurrentPowerupState is not PowerupState.MiniMushroom or PowerupState.MegaMushroom && !IsSkidding 
                && !IsInKnockback && KnockbackGetupFrames == 0 && !IsTurnaround && !IsPropellerFlying && !IsSpinnerFlying && !IsCrouching && !IsDead
                && !IsInShell && !WallslideLeft && !WallslideRight && (f.Exists(item) || physicsObject->IsTouchingGround || JumpState < JumpState.DoubleJump)
                && !IsGroundpounding && !(!f.Exists(item) && physicsObject->IsUnderwater && input.Jump.IsDown)
                && !(aboveHead && physicsObject->IsUnderwater);
        }

        public readonly bool CanPickupItem(Frame f, EntityRef mario, EntityRef item) {
            return !f.Exists(HeldEntity) && CanHoldItem(f, mario, item) && ForceJumpTimer <= 5;
        }

        public readonly bool InstakillsEnemies(PhysicsObject* physicsObject, bool includeSliding) {
            return CurrentPowerupState == PowerupState.MegaMushroom
                || IsStarmanInvincible
                || IsInShell
                || includeSliding && IsSliding && FPMath.Abs(physicsObject->Velocity.X) > FP._0_33;
        }

        public readonly int GetSpeedStage(PhysicsObject* physicsObject, MarioPlayerPhysicsInfo physicsInfo) {
            FP xVel = FPMath.Abs(physicsObject->Velocity.X) - FP._0_01;
            FP[] arr;
            if (physicsObject->IsUnderwater) {
                if (physicsObject->IsTouchingGround) {
                    arr = CurrentPowerupState == PowerupState.BlueShell ? physicsInfo.SwimWalkShellMaxVelocity : physicsInfo.SwimWalkMaxVelocity;
                } else {
                    arr = physicsInfo.SwimMaxVelocity;
                }
            } else if ((IsSpinnerFlying || IsPropellerFlying) && CurrentPowerupState != PowerupState.MegaMushroom) {
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

        public readonly int GetGravityStage(PhysicsObject* physicsObject, MarioPlayerPhysicsInfo physicsInfo) {
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

        public void QueuePowerupAnim(Frame f, EntityRef marioEntity, PowerupState startingState, PowerupState endingState, bool isPowerdown, PowerupAsset powerupAsset = null) {
            var list = f.ResolveList(PowerupTransitionQueue);
            list.Add(new() {
                StartingState = startingState,
                EndingState = endingState,

                Scriptable = powerupAsset,
                IsPowerdown = isPowerdown,
                Timer = Constants.PowerupTransitionLength
            });

            // count the number of things in the list, check if 3
            if (list.Count > Constants.PowerupTransitionMax) {
                // set the second powerUP transition's timer
                var firstAnim = list.GetPointer(0);
                var secondAnim = list.GetPointer(1);
                secondAnim->Timer = firstAnim->Timer;

                f.Events.MarioPlayerUpdatePowerupQueue(marioEntity, secondAnim);

                // delete the current powerUP transition
                list.RemoveAt(0);
            }
        }

        public void Death(Frame f, EntityRef entity, bool fire, bool dropObjectives, EntityRef attacker) {
            if (IsDead) {
                return;
            }

            var gamemode = f.FindAsset(f.Global->Rules.Gamemode);
            int oldObjectiveCount = gamemode.GetObjectiveCount(f, f.Unsafe.GetPointer<MarioPlayer>(entity));

            f.ResolveList(PowerupTransitionQueue).Clear();

            IsDead = true;
            FireDeath = fire;
            QuantumUtils.Decrement(ref Lives);
            f.Unsafe.GetPointer<Interactable>(entity)->ColliderDisabled = true;
            PreRespawnFrames = 180;
            RespawnFrames = 78;
            DeathAnimationFrames = 36;

            if (dropObjectives) {
                f.Signals.OnMarioPlayerDropObjective(entity, 1, attacker);
            }

            // OnSpinner = null;
            DoEntityBounce = false;
            CurrentPipe = EntityRef.None;
            IsInShell = false;
            IsPropellerFlying = false;
            PropellerLaunchFrames = 0;
            PropellerSpinFrames = 0;
            IsSpinnerFlying = false;
            IsDrilling = false;
            IsSliding = false;
            IsCrouching = false;
            IsSkidding = false;
            IsTurnaround = false;
            IsGroundpounding = false;
            CurrentKnockback = KnockbackStrength.None;
            KnockbackGetupFrames = 0;
            WallslideRight = false;
            WallslideLeft = false;
            ForceJumpTimer = 0;
            LastAttacker = EntityRef.None;
            TauntFrames = 0;
            
            if (f.Unsafe.TryGetPointer(HeldEntity, out Holdable* holdable)) {
                holdable->DropWithoutThrowing(f, HeldEntity);
            }

            var physicsObject = f.Unsafe.GetPointer<PhysicsObject>(entity);
            physicsObject->IsFrozen = true;
            physicsObject->DisableCollision = true;
            physicsObject->CurrentData = default;

            // set the amount of stars to drop if no longer valid
            if (!IsValid(f) && gamemode is StarChasersGamemode) {
                var starChasers = GamemodeData.StarChasers;

                // this wacky formula is how we figure out how many stars to drop before dying
                // to set the "DeathStarThreshold" we get the amount of stars we currently have
                // then we multiply it by the StarFountain, rounding UP. StarFountain is value between 0 and 1
                // round down since a star WILL be dropped on the initial death
                FP starPercentage = (FP)f.Global->Rules.StarFountain / 100;
                starChasers->DeathStarThreshold = (byte) (starChasers->Stars -  FPMath.FloorToInt(starChasers->Stars * starPercentage));
            }

            f.Signals.OnMarioPlayerDied(entity);
            f.Events.MarioPlayerDied(entity, fire, oldObjectiveCount, attacker);
        }

        public bool Powerdown(Frame f, EntityRef entity, bool ignoreInvincible, EntityRef attacker) {
            if (!ignoreInvincible && (!IsDamageable(f) || CurrentPowerupState == PowerupState.MegaMushroom)) {
                return false;
            }

            QBoolean doDamage = true;
            f.Signals.OnMarioPlayerTakeDamage(entity, ref doDamage);
            if (!doDamage) {
                return false;
            }

            var gamemode = f.FindAsset(f.Global->Rules.Gamemode);
            int oldObjectiveCount = gamemode.GetObjectiveCount(f, f.Unsafe.GetPointer<MarioPlayer>(entity));

            PreviousPowerupState = CurrentPowerupState;

            switch (CurrentPowerupState) {
            case PowerupState.MiniMushroom:
            case PowerupState.NoPowerup: {
                Death(f, entity, false, true, attacker);
                break;
            }
            case PowerupState.Mushroom: {
                CurrentPowerupState = PowerupState.NoPowerup;
                f.Signals.OnMarioPlayerDropObjective(entity, 1, attacker);
                break;
            }
            case PowerupState.HammerSuit:
            case PowerupState.FireFlower:
            case PowerupState.IceFlower:
            case PowerupState.PropellerMushroom:
            case PowerupState.BlueShell: {
                CurrentPowerupState = PowerupState.Mushroom;
                f.Signals.OnMarioPlayerDropObjective(entity, 1, attacker);
                break;
            }
            }

            IsDrilling &= !IsPropellerFlying;
            IsPropellerFlying = false;
            IsInShell = false;
            PropellerLaunchFrames = 0;
            PropellerSpinFrames = 0;
            UsedPropellerThisJump = false;

            if (ignoreInvincible) {
                f.ResolveList(PowerupTransitionQueue).Clear();
            }
            // queue a powerUP animation here too...
            QueuePowerupAnim(f, entity, PreviousPowerupState, CurrentPowerupState, true);

            if (!IsDead) {
                DamageInvincibilityFrames = Constants.DamageInvincibilityFrames;
                f.Events.MarioPlayerTookDamage(entity, oldObjectiveCount, attacker);
            }
            return true;
        }

        public void PreRespawn(Frame f, EntityRef entity, VersusStageData stage) {
            var physicsObject = f.Unsafe.GetPointer<PhysicsObject>(entity);
            var transform = f.Unsafe.GetPointer<Transform2D>(entity);

            RespawnFrames = 78;

            if (!IsValid(f)) {
                f.Destroy(entity);
                return;
            }

            FPVector2 spawnpoint = stage.GetWorldSpawnpointForPlayer(SpawnpointIndex, f.Global->TotalMarios);
            transform->Position = spawnpoint;
            f.Unsafe.GetPointer<CameraController>(entity)->Recenter(stage, spawnpoint);
            
            IsDead = true;
            f.Unsafe.GetPointer<Freezable>(entity)->FrozenCubeEntity = EntityRef.None;
            IsRespawning = true;
            DoEntityBounce = false;
            FacingRight = true;
            WallslideLeft = false;
            WallslideRight = false;
            WallslideEndFrames = 0;
            WalljumpFrames = 0;
            IsPropellerFlying = false;
            UsedPropellerThisJump = false;
            IsSpinnerFlying = false;
            PropellerLaunchFrames = 0;
            PropellerSpinFrames = 0;
            JumpState = JumpState.None;
            PreviousPowerupState = CurrentPowerupState = PowerupState.NoPowerup;
            DamageInvincibilityFrames = 0;
            InvincibilityFrames = 0;
            MegaMushroomFrames = 0;
            MegaMushroomStartFrames = 0;
            MegaMushroomEndFrames = 0;
            IsCrouching = false;
            IsSliding = false;
            IsTurnaround = false;
            CurrentKnockback = KnockbackStrength.None;
            IsGroundpounding = false;
            IsSkidding = false;
            IsInShell = false;
            IsTurnaround = false;
            ForceJumpTimer = 0;

            f.ResolveList(PowerupTransitionQueue).Clear();

            physicsObject->IsFrozen = true;
            physicsObject->Velocity = FPVector2.Zero;
            f.Unsafe.GetPointer<Interactable>(entity)->ColliderDisabled = false;

            f.Events.MarioPlayerPreRespawned(entity, spawnpoint);
        }

        public void Respawn(Frame f, EntityRef entity) {
            var physicsObject = f.Unsafe.GetPointer<PhysicsObject>(entity);

            IsDead = false;
            IsRespawning = false;
            DamageInvincibilityFrames = Constants.DamageInvincibilityFrames;
            CoyoteTimeFrames = 0;
            ForceJumpTimer = 0;
            LastAttacker = EntityRef.None;

            physicsObject->IsFrozen = false;
            physicsObject->DisableCollision = false;

            f.Events.MarioPlayerRespawned(entity);

            if (!IsValid(f)) {
                // Disconnected while respawning
                Death(f, entity, false, true, EntityRef.None);
            }
        }

        public bool DoKnockback(Frame f, EntityRef entity, bool fromRight, int starsToDrop, KnockbackStrength strength, EntityRef attacker, bool bypassDamageInvincibility = false, ProjectileEffectType projectileEffectType = ProjectileEffectType.None, bool wasBlueShell = false, bool ignoreInvincibleStates = false) {
            var physicsObject = f.Unsafe.GetPointer<PhysicsObject>(entity);
            if (physicsObject->IsUnderwater) {
                strength = KnockbackStrength.Normal;
            }

            if (IsImmuneFromKnockbackStrength(CurrentKnockback, strength)) {
                return false;
            }

            var freezable = f.Unsafe.GetPointer<Freezable>(entity);
            if ((!bypassDamageInvincibility && DamageInvincibilityFrames > 0) || f.Exists(CurrentPipe) || (freezable->IsFrozen(f) && freezable->FrozenCubeEntity != attacker) || IsDead || MegaMushroomStartFrames > 0 || MegaMushroomEndFrames > 0) {
                return false;
            }

            if (!ignoreInvincibleStates && IsStarmanOrMega) {
                return false;
            }

            if (IsInKnockback) {
                ResetKnockback(f, entity);
            }

            var gamemode = f.FindAsset(f.Global->Rules.Gamemode);
            int oldObjectiveCount = gamemode.GetObjectiveCount(f, f.Unsafe.GetPointer<MarioPlayer>(entity));

            /*
            // Don't go into walls
            var transform = f.Unsafe.GetPointer<Transform2D>(entity);
            var collider = f.Unsafe.GetPointer<PhysicsCollider2D>(entity);

            if (!IsInWeakKnockback && PhysicsObjectSystem.Raycast(f, null, transform->Position + collider->Shape.Centroid, fromRight ? FPVector2.Left : FPVector2.Right, FP._0_33, out _)) {
                fromRight = !fromRight;
            }
            */

            FPVector2 knockbackVelocity = strength switch {
                KnockbackStrength.Groundpound => new(Constants._8_25 / 2, Constants._3_50),
                KnockbackStrength.FireballBump => new(Constants._3_75 / 2, 0),
                KnockbackStrength.CollisionBump => new(Constants._2_50, Constants._3_50),
                KnockbackStrength.Normal or _ => new(Constants._3_75 / 2, Constants._3_50),
            };
            if (CurrentKnockback == KnockbackStrength.CollisionBump) {
                knockbackVelocity = FPVector2.Zero;
            }
            knockbackVelocity.X *= fromRight ? -1 : 1;
            if (CurrentPowerupState == PowerupState.MiniMushroom) {
                var physics = f.FindAsset(PhysicsAsset);
                knockbackVelocity.X *= physics.KnockbackMiniMultiplier.X;
                knockbackVelocity.Y *= physics.KnockbackMiniMultiplier.Y;
            }

            KnockbackTick = f.Number;

            bool forceWeak = false;
            if (freezable->IsFrozen(f) && strength != KnockbackStrength.Normal && strength != KnockbackStrength.Groundpound) {
                forceWeak = true;
                KnockbackTick -= 25;
            }
            if (strength == KnockbackStrength.FireballBump && !physicsObject->IsTouchingGround) {
                // FacingRight = fromRight;
                knockbackVelocity.X *= FP._0_75;
            }

            CurrentKnockback = strength;
            IsInWeakKnockback = forceWeak || (CurrentPowerupState != PowerupState.MegaMushroom && (strength == KnockbackStrength.CollisionBump || (strength == KnockbackStrength.FireballBump && physicsObject->IsTouchingGround)));

            physicsObject->Velocity = knockbackVelocity;
            physicsObject->IsTouchingGround = false;
            physicsObject->WasTouchingGround = false;
            physicsObject->HoverFrames = 0;

            KnockbackWasOriginallyFacingRight = FacingRight;
            KnockForwards = FacingRight != fromRight;
            IsInShell = false;
            IsGroundpounding = false;
            IsSpinnerFlying = false;
            IsPropellerFlying = false;
            PropellerLaunchFrames = 0;
            PropellerSpinFrames = 0;
            IsSliding = false;
            IsDrilling = false;
            WallslideLeft = WallslideRight = false;

            if (f.Unsafe.TryGetPointer(attacker, out Projectile* projectile)) {
                attacker = projectile->Owner;
            }
            LastAttacker = attacker;

            f.Signals.OnMarioPlayerDropObjective(entity, starsToDrop, attacker);
            f.Events.MarioPlayerTookKnockback(entity, attacker, starsToDrop, oldObjectiveCount, strength, projectileEffectType, wasBlueShell);
            return true;
        }

        private static bool IsImmuneFromKnockbackStrength(KnockbackStrength currentStrength, KnockbackStrength newStrength) {
            return currentStrength == newStrength
                || (currentStrength == KnockbackStrength.Groundpound && newStrength == KnockbackStrength.Normal)
                || (currentStrength == KnockbackStrength.Normal && newStrength == KnockbackStrength.Groundpound)
                || (currentStrength == KnockbackStrength.FireballBump && newStrength == KnockbackStrength.CollisionBump);
        }

        public void GetupKnockback(Frame f, EntityRef entity) {
            var physicsObject = f.Unsafe.GetPointer<PhysicsObject>(entity);
            if (IsInWeakKnockback) {
                physicsObject->Velocity.X = 0;
            }
            if (IsInWeakKnockback || DoEntityBounce || physicsObject->IsUnderwater) {
                // No getup frames
                ResetKnockback(f, entity);
            } else {
                KnockbackGetupFrames = 25;
            }
        }

        public void ResetKnockback(Frame f, EntityRef mario) {
            KnockbackGetupFrames = 0;
            DamageInvincibilityFrames = 90; // Exception: knockback does 90f instead of the usual 120f
            CurrentKnockback = KnockbackStrength.None;
            IsInWeakKnockback = false;
            FacingRight = KnockbackWasOriginallyFacingRight;
            LastAttacker = EntityRef.None;
            f.Events.MarioPlayerKnockbackOver(mario);
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
            var otherPipeTransform = f.Unsafe.GetPointer<Transform2D>(pipeComponent->OtherPipe);
            var marioTransform = f.Unsafe.GetPointer<Transform2D>(mario);
            marioTransform->Position.X = pipeTransform->Position.X;

            IsCrouching = false;
            IsSliding = false;
            IsPropellerFlying = false;
            UsedPropellerThisJump = false;
            PropellerLaunchFrames = 0;
            PropellerSpinFrames = 0;
            IsSpinnerFlying = false;
            IsInShell = false;
            PipeEntering = true;

            if (InvincibilityFrames > 0) {
                InvincibilityFrames += (ushort) (PipeFrames * 2);
            }

            sbyte horizontalDirection;
            if (pipeComponent->TransitionOnlyPanning) {
                horizontalDirection = 0;
            } else {
                horizontalDirection = (sbyte) (otherPipeTransform->Position.X < pipeTransform->Position.X ? -1 : 1);
            }

            f.Events.MarioPlayerEnteredPipe(mario, CurrentPipe, false, horizontalDirection, FPVector2.Zero);
        }

        private static void SetValue(ref BitSet21 bitset, int index, bool value) {
            if (value) {
                bitset.Set(index);
            } else {
                bitset.Clear(index);
            }
        }
    }
}