namespace Quantum {
    public unsafe abstract class SystemMainThreadFilterStage<T> : SystemMainThread where T : unmanaged {
        public virtual bool UseCulling => true;

        public override void Update(Frame f) {
            var filtered = f.Unsafe.FilterStruct<T>();
            filtered.UseCulling = UseCulling;

            VersusStageData stage = null;
            T filterStruct = default;
            while (filtered.Next(&filterStruct)) {
                if (!stage) {
                    stage = f.FindAsset<VersusStageData>(f.Map.UserAsset);
                    BeforeUpdate(f, stage);
                }
                Update(f, ref filterStruct, stage);
            }
        }

        public abstract void Update(Frame f, ref T filter, VersusStageData stage);
        public virtual void BeforeUpdate(Frame f, VersusStageData stage) { }
    }
}