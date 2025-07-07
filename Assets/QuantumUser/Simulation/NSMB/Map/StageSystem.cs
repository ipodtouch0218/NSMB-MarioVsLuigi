using System;

namespace Quantum {
    public unsafe class StageSystem : SystemSignalsOnly, ISignalOnGameStarting, ISignalOnMapChanged {
        public override void OnInit(Frame f) {
            OnMapChanged(f, default);
        }

        public void OnGameStarting(Frame f) {
            var stage = f.FindAsset<VersusStageData>(f.Map.UserAsset);
            stage.ResetStage(f, true);
        }

        public void OnMapChanged(Frame f, AssetRef<Map> previousMap) {
            if (f.Map != null && f.TryFindAsset(f.Map.UserAsset, out VersusStageData stage)) {
                f.StageTiles = new StageTileInstance[stage.TileDimensions.X * stage.TileDimensions.Y];
                Array.Copy(stage.TileData, f.StageTiles, stage.TileDimensions.X * stage.TileDimensions.Y);
            } else {
                // Not a valid VersusStage
                f.StageTiles = Array.Empty<StageTileInstance>();
            }
        }
    }
}