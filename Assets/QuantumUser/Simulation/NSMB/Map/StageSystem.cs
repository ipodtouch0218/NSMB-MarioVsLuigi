namespace Quantum {
    public unsafe class StageSystem : SystemSignalsOnly, ISignalOnGameStarting {
        public void OnGameStarting(Frame f) {
            var stage = f.FindAsset<VersusStageData>(f.Map.UserAsset);
            stage.ResetStage(f, true);
        }
    }
}