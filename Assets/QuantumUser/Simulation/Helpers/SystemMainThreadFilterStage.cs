namespace Quantum {

    public unsafe abstract class SystemMainThreadFilterStage<T> : SystemMainThreadFilter<T> where T : unmanaged {
        public override void Update(Frame f, ref T filter) {

            // Same as SystemMainThreadFilter, but with the stage only being accessed once.
            var stage = f.FindAsset<VersusStageData>(f.Map.UserAsset);

            var filtered = f.Unsafe.FilterStruct<T>();
            T filterStruct = default;
            while (filtered.Next(&filterStruct)) {
                Update(f, ref filterStruct, stage);
            }
        }

        public abstract void Update(Frame f, ref T filter, VersusStageData stage);
    }

}