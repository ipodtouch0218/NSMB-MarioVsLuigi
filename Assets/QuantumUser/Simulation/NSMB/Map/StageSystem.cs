namespace Quantum {
    public unsafe class StageSystem : SystemMainThread {
        public override void OnInit(Frame f) {
            var stage = f.FindAsset<VersusStageData>(f.Map.UserAsset);
            stage.ResetStage(f);
        }

        public override void OnDisabled(Frame f) {
            f.FreeList(f.Global->Stage);
        }

        public override void Update(Frame f) {

        }
    }
}