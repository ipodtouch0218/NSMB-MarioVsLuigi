using NSMB.Utilities.Extensions;
using Quantum;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace NSMB.UI.Game.Track {
    public class TrackIcon : QuantumSceneViewComponent {

        //---Serialized Variables
        [SerializeField] private float trackMinX, trackMaxX;
        [SerializeField] protected Image image;

        //---Private Variables
        protected EntityRef targetEntity;
        protected Transform targetTransform;
        protected PlayerElements playerElements;
        protected VersusStageData stage;

        private float levelWidthReciprocal;
        private float levelMinX;
        private float trackWidth;

        public virtual void OnValidate() {
            this.SetIfNull(ref image);
        }

        public void Initialize(PlayerElements playerElements, EntityRef targetEntity, Transform targetTransform) {
            this.playerElements = playerElements;
            this.targetEntity = targetEntity;
            this.targetTransform = targetTransform;

            Frame f = Updater.ObservedGame.Frames.Predicted;
            stage = f.FindAsset<VersusStageData>(f.Map.UserAsset);
            
            levelMinX = stage.StageWorldMin.X.AsFloat;
            trackWidth = trackMaxX - trackMinX;
            levelWidthReciprocal = 2f / stage.TileDimensions.X;

            name = $"TrackIcon ({targetEntity})";
            OnLateUpdateView();
        }

        public override void OnLateUpdateView() {
            if (!targetTransform) {
                return;
            }

            float percentage = (targetTransform.position.x - levelMinX) * levelWidthReciprocal;
            transform.localPosition = new(percentage * trackWidth - trackMaxX, transform.localPosition.y, transform.localPosition.z);
        }
    }
}
