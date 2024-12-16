namespace Quantum {
    public unsafe partial struct PhysicsObject {
        public bool IsUnderwater => UnderwaterCounter > 0;
    }
}