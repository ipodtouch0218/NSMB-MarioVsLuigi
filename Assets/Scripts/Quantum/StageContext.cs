using Quantum;
using System;

namespace NSMB.Quantum {
    public class StageContext : QuantumMonoBehaviour, IQuantumViewContext {

        public QuantumMapData MapData;
        [NonSerialized] public VersusStageData Stage;

        public void Awake() {
            Stage = (VersusStageData) QuantumUnityDB.GetGlobalAsset(MapData.Asset.UserAsset);
        }
    }
}
