namespace Quantum {
    public class StartDisabledSystemGroup : SystemMainThreadGroup {
        public override bool StartEnabled => false;

        public StartDisabledSystemGroup(string name, params SystemMainThread[] children) : base(name, children) {

        }
    }
}