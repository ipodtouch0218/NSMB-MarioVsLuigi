namespace Quantum {
    public class GameplaySystemGroup : SystemMainThreadGroup {
        public override bool StartEnabled => false;

        public GameplaySystemGroup(params SystemMainThread[] children) : base("gameplay", children) {
        }
    }
}