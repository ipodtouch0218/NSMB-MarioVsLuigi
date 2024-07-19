using Photon.Deterministic;

namespace Quantum {
    public partial struct MarioPlayer {

        public bool IsStarmanInvincible => InvincibilityFrames > 0;
        public bool IsWallsliding => WallslideLeft || WallslideRight;
        public bool IsCrouchedInShell => CurrentPowerupState == PowerupState.BlueShell && IsCrouching && !IsInShell;
        public bool IsInWater => WaterColliderCount > 0;

        public int GetSpeedStage(PhysicsObject physicsObject, MarioPlayerPhysicsInfo physicsInfo) {
            FP xVel = FPMath.Abs(physicsObject.Velocity.X) - FP._0_01;
            FP[] arr;
            if (IsInWater) {
                if (physicsObject.IsTouchingGround) {
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

        public int GetGravityStage(PhysicsObject physicsObject, MarioPlayerPhysicsInfo physicsInfo) {
            FP yVel = physicsObject.Velocity.Y;
            FP[] maxArray = IsInWater ? physicsInfo.GravitySwimmingVelocity : (CurrentPowerupState == PowerupState.MegaMushroom ? physicsInfo.GravityMegaVelocity : (CurrentPowerupState == PowerupState.MiniMushroom ? physicsInfo.GravityMiniVelocity : physicsInfo.GravityVelocity));
            for (int i = 0; i < maxArray.Length; i++) {
                if (yVel >= maxArray[i]) {
                    return i;
                }
            }
            return maxArray.Length;
        }

        public void SetReserveItem(Frame f, PowerupAsset newItem) {
            var currentItem = f.FindAsset(ReserveItem);

            if (!currentItem) {
                // We don't have a reserve item, so we can just set it
                ReserveItem = newItem;
                return;
            }

            if (!newItem) {
                // Not a valid powerup, so just clear our reserve item instead
                ReserveItem = null;
                return;
            }

            sbyte newItemPriority = newItem ? newItem.ItemPriority : (sbyte) -1;
            sbyte currentItemPriority = currentItem ? currentItem.ItemPriority : (sbyte) -1;

            if (newItemPriority < currentItemPriority) {
                // New item is less important than our current reserve item, so we don't want to replace it
                return;
            }

            // Replace our current reserve item with the new one
            ReserveItem = newItem;
        }
    }
}