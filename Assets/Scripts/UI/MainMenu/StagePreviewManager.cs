using Quantum;
using System;
using UnityEngine;

namespace NSMB.UI.MainMenu {
    public class StagePreviewManager : MonoBehaviour {

        //---Serialized Variables
        [SerializeField] private Camera targetCamera;
        [SerializeField] private StagePreviewData[] stages;
        [SerializeField] private Transform randomizedStageCameraPosition;

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

        //Not the same as PreviewRandomStage. PreviewRandomStage choose a random stage to preview from the regular stages.
        //This one is here to indicate the player that the stage is set to "randomized"
        public void PreviewStageAsRandomized() {
            targetCamera.transform.position = randomizedStageCameraPosition.position;
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
                if (e.Game.Frames.Predicted.Global->Rules.RandomizeStage) {
                    PreviewStageAsRandomized();
                    return;
                }
                PreviewStage(e.Game.Frames.Predicted.Global->Rules.Stage);
            }
        }

        private unsafe void OnRulesChanged(EventRulesChanged e) {
            if (e.Game.Frames.Predicted.Global->Rules.RandomizeStage) 
            {
                PreviewStageAsRandomized();
                return;
            }
            PreviewStage(e.Game.Frames.Predicted.Global->Rules.Stage);
        }


        [Serializable]
        public class StagePreviewData {
            public AssetRef<Map> Map;
            public Transform CameraPosition;
        }
    }
}
