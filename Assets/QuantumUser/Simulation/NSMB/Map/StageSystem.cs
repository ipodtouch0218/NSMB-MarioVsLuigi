using Quantum.Collections;

namespace Quantum {
    public unsafe class StageSystem : SystemMainThread {
        public override void OnInit(Frame f) {
            var stage = f.FindAsset<VersusStageData>(f.Map.UserAsset);
            QList<StageTileInstance> stageData = f.AllocateList<StageTileInstance>(stage.TileData.Length);
            for (int i = 0; i < stage.TileData.Length; i++) {
                stageData.Add(stage.TileData[i]);
            }
            f.Global->Stage = stageData;
        }

        public override void OnDisabled(Frame f) {
            f.FreeList(f.Global->Stage);
        }

        public override void Update(Frame f) {

        }
    }
}