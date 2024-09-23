using System;

namespace Quantum {
    public unsafe class StageSystem : SystemSignalsOnly, ISignalOnGameStarting, ISignalOnMapChanged {
        public void OnGameStarting(Frame f) {
            var stage = f.FindAsset<VersusStageData>(f.Map.UserAsset);
            f.StageTiles = new StageTileInstance[stage.TileDimensions.x * stage.TileDimensions.y];
            stage.ResetStage(f, true);
        }

        public void OnMapChanged(Frame f, AssetRef<Map> previousMap) {
            var stage = f.FindAsset<VersusStageData>(f.Map.UserAsset);
            if (!stage) {
                f.StageTiles = Array.Empty<StageTileInstance>();
            }
        }
    }
}