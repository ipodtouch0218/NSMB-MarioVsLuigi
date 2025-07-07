namespace Quantum {
    public unsafe partial struct Freezable {
        public bool IsFrozen(Frame f) {
            return f.Exists(FrozenCubeEntity);
        }
    }
}