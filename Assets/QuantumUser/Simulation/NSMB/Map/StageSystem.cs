namespace Quantum {
    public unsafe class StageSystem : SystemSignalsOnly, ISignalOnGameStarting {
        public override void OnDisabled(Frame f) {
            f.FreeList(f.Global->Stage);
        }

        public void OnGameStarting(Frame f) {
            var stage = f.FindAsset<VersusStageData>(f.Map.UserAsset);
            stage.ResetStage(f, true);
        }
    }
}