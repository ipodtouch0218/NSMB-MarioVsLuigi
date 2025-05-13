using Quantum;
using UnityEngine;

public class StageContext : QuantumMonoBehaviour, IQuantumViewContext {

    public QuantumMapData MapData;
    [HideInInspector] public VersusStageData Stage;

    public void Awake() {
        Stage = (VersusStageData) QuantumUnityDB.GetGlobalAsset(MapData.GetAsset(false).UserAsset);
    }
}