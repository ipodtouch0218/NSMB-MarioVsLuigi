namespace Quantum {
    public class StartDisabledSystemGroup : SystemGroup {
        public override bool StartEnabled => false;

        public StartDisabledSystemGroup(params SystemBase[] children) : base(children) {

        }
    }
}