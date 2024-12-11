using Quantum;
using System;
using UnityEngine;

public class StagePreviewManager : MonoBehaviour {

    //---Serialized Variables
    [SerializeField] private Camera targetCamera;
    [SerializeField] private StagePreviewData[] stages;

    public void Start() {
        PreviewRandomStage();
    }

    public void PreviewRandomStage() {
        UnityEngine.Random.InitState((int) DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        PreviewStage(stages[UnityEngine.Random.Range(0, stages.Length)]);
    }

    public void PreviewStage(StagePreviewData stage) {
        targetCamera.transform.position = stage.CameraPosition.position;
    }

    public void PreviewStage(AssetRef<Map> map) {
        PreviewStage(GetPreviewDataFromMap(map));
    }

    private StagePreviewData GetPreviewDataFromMap(AssetRef<Map> map) {
        foreach (var stage in stages) {
            if (stage.Map == map) {
                return stage;
            }
        }
        return stages[0];
    }


    [Serializable]
    public class StagePreviewData {
        public AssetRef<Map> Map;
        public Transform CameraPosition;
    }
}