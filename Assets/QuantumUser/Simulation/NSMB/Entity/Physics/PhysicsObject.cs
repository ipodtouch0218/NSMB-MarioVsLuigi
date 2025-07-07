using Photon.Deterministic;

namespace Quantum {
    public unsafe partial struct PhysicsObject {
        public bool IsUnderwater => UnderwaterCounter > 0;
        public bool IsTouchingGround {
            get => HasFlag(CurrentData, PhysicsFlags.IsTouchingGround);
            set => SetFlag(ref CurrentData, PhysicsFlags.IsTouchingGround, value);
        }
        public bool WasTouchingGround {
            get => HasFlag(PreviousData, PhysicsFlags.IsTouchingGround);
            set => SetFlag(ref PreviousData, PhysicsFlags.IsTouchingGround, value);
        }
        public bool IsTouchingLeftWall {
            get => HasFlag(CurrentData, PhysicsFlags.IsTouchingLeftWall);
            set => SetFlag(ref CurrentData, PhysicsFlags.IsTouchingLeftWall, value);
        }
        public bool IsTouchingRightWall {
            get => HasFlag(CurrentData, PhysicsFlags.IsTouchingRightWall);
            set => SetFlag(ref CurrentData, PhysicsFlags.IsTouchingRightWall, value);
        }
        public bool IsTouchingCeiling {
            get => HasFlag(CurrentData, PhysicsFlags.IsTouchingCeiling);
            set => SetFlag(ref CurrentData, PhysicsFlags.IsTouchingCeiling, value);
        }
        public bool IsOnSlideableGround {
            get => HasFlag(CurrentData, PhysicsFlags.IsOnSlideableGround);
            set => SetFlag(ref CurrentData, PhysicsFlags.IsOnSlideableGround, value);
        }
        public bool IsOnSlipperyGround {
            get => HasFlag(CurrentData, PhysicsFlags.IsOnSlipperyGround);
            set => SetFlag(ref CurrentData, PhysicsFlags.IsOnSlipperyGround, value);
        }
        public bool IsBeingCrushed {
            get => HasFlag(CurrentData, PhysicsFlags.IsBeingCrushed);
            set => SetFlag(ref CurrentData, PhysicsFlags.IsBeingCrushed, value);
        }
        public bool WasBeingCrushed {
            get => HasFlag(PreviousData, PhysicsFlags.IsBeingCrushed);
            set => SetFlag(ref PreviousData, PhysicsFlags.IsBeingCrushed, value);
        }
        public FP FloorAngle {
            get => CurrentData.FloorAngle;
            set => CurrentData.FloorAngle = value;
        }

        private bool HasFlag(in PhysicsObjectData data, PhysicsFlags flag) {
            return ((int) data.Flags & (int) flag) != 0;
        }

        private void SetFlag(ref PhysicsObjectData data, PhysicsFlags flag, bool value) {
            if (value) {
                data.Flags = (PhysicsFlags) (((int) data.Flags) | ((int) flag));
            } else {
                data.Flags = (PhysicsFlags) (((int) data.Flags) & ~((int) flag));
            }
        }
    }
}