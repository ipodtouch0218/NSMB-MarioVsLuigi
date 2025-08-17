using Quantum;
using System;
using UnityEngine;

namespace NSMB.UI.MainMenu {
    public class StagePreviewManager : MonoBehaviour {

        //---Serialized Variables
        [SerializeField] private Camera targetCamera;
        [SerializeField] private StagePreviewData[] stages;

        public void Start() {
            PreviewRandomStage();
            QuantumEvent.Subscribe<EventPlayerAdded>(this, OnPlayerAdded);
            QuantumEvent.Subscribe<EventRulesChanged>(this, OnRulesChanged);
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

        private unsafe void OnPlayerAdded(EventPlayerAdded e) {
            if (e.Game.PlayerIsLocal(e.Player)) {
                PreviewStage(e.Game.Frames.Predicted.Global->Rules.Stage);
            }
        }

        private unsafe void OnRulesChanged(EventRulesChanged e) {
            PreviewStage(e.Game.Frames.Predicted.Global->Rules.Stage);
        }


        public void OnDrawGizmos() {
            Gizmos.color = Color.gray;
            foreach (var preview in stages) {
                Gizmos.DrawWireCube(preview.CameraPosition.position, new Vector3(7f * (16f/9f), 7f));
            }
        }

        [Serializable]
        public class StagePreviewData {
            public AssetRef<Map> Map;
            public Transform CameraPosition;
        }
    }
}
