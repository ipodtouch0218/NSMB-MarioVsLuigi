namespace Quantum {
    public unsafe partial struct ComboKeeper {
        public static byte IncrementOrDefault(Frame f, EntityRef entity) {
            if (f.Unsafe.TryGetPointer(entity, out ComboKeeper* comboKeeper)) {
                return comboKeeper->Combo++;
            } else {
                return 0;
            }
        }
    }
}