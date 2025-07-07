namespace Quantum {
    public enum PhysicsFlags : byte {
        IsTouchingLeftWall = 1 << 0,
        IsTouchingRightWall = 1 << 1,
        IsTouchingCeiling = 1 << 2,
        IsTouchingGround = 1 << 3,
        IsOnSlipperyGround = 1 << 4,
        IsOnSlideableGround = 1 << 5,
        IsBeingCrushed = 1 << 6,
    }
}
