using Unity.Collections.LowLevel.Unsafe;

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
                int count = stage.TileDimensions.X * stage.TileDimensions.Y;
                f.ReallocStageTiles(count);

                fixed (StageTileInstance* originalData = &stage.TileData[0]) {
                    UnsafeUtility.MemCpy(f.StageTiles, originalData, StageTileInstance.SIZE * count);
                }
            } else {
                // Not a valid VersusStage
                f.ReallocStageTiles(0);
            }
        }
    }
}