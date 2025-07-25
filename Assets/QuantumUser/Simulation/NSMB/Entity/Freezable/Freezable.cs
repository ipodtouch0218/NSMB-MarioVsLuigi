namespace Quantum {
    public unsafe partial struct Freezable {
        public readonly bool IsFrozen(Frame f) {
            return f.Exists(FrozenCubeEntity);
        }
    }
}