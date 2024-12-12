namespace Quantum {
    public class StartDisabledSystemGroup : SystemGroup {
        public override bool StartEnabled => false;

        public StartDisabledSystemGroup(string name, params SystemBase[] children) : base(name, children) {

        }
    }
}