namespace Quantum {
    public unsafe abstract class SystemMainThreadEntityFilter<E,F> : SystemMainThreadEntity<E> where E : unmanaged, IComponent where F : unmanaged {
        public virtual bool UseCulling => true;

        public override void Update(Frame f) {
            var filtered = f.Unsafe.FilterStruct<F>();
            filtered.UseCulling = UseCulling;

            VersusStageData stage = null;
            F filterStruct = default;
            while (filtered.Next(&filterStruct)) {
                if (stage == null) {
                    stage = f.FindAsset<VersusStageData>(f.Map.UserAsset);
                }
                Update(f, ref filterStruct, stage);
            }
        }

        public abstract void Update(Frame f, ref F filter, VersusStageData stage);
    }
}