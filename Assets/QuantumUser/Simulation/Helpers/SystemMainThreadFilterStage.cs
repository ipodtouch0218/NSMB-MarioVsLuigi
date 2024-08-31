namespace Quantum {
    public unsafe abstract class SystemMainThreadFilterStage<T> : SystemMainThread where T : unmanaged {
        public override void Update(Frame f) {
            var stage = f.FindAsset<VersusStageData>(f.Map.UserAsset);
            BeforeUpdate(f, stage);

            var filtered = f.Unsafe.FilterStruct<T>();
            T filterStruct = default;
            while (filtered.Next(&filterStruct)) {
                Update(f, ref filterStruct, stage);
            }
        }

        public abstract void Update(Frame f, ref T filter, VersusStageData stage);
        public virtual void BeforeUpdate(Frame f, VersusStageData stage) { }
    }
}