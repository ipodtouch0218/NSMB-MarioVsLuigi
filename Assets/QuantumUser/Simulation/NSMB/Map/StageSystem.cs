using System;

namespace Quantum {
    public unsafe class StageSystem : SystemSignalsOnly, ISignalOnGameStarting, ISignalOnMapChanged {
        public void OnGameStarting(Frame f) {
            var stage = f.FindAsset<VersusStageData>(f.Map.UserAsset);
            f.StageTiles = new StageTileInstance[stage.TileDimensions.x * stage.TileDimensions.y];
            stage.ResetStage(f, true);
        }

        public void OnMapChanged(Frame f, AssetRef<Map> previousMap) {
            if (!f.Map || !f.TryFindAsset<VersusStageData>(f.Map.UserAsset, out _)) {
                // Not a valid VersusStage
                f.StageTiles = Array.Empty<StageTileInstance>();
            }
        }
    }
}