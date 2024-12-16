using NSMB.Extensions;
using Quantum;
using UnityEngine;
using UnityEngine.UI;

namespace NSMB.UI.Game.Track {
    public abstract class TrackIcon : MonoBehaviour {

        //---Serialized Variables
        [SerializeField] private float trackMinX, trackMaxX;
        [SerializeField] protected Image image;

        //---Private Variables
        protected EntityRef targetEntity;
        protected Transform targetTransform;
        protected VersusStageData stage;
        protected PlayerElements playerElements;

        private float levelWidthReciprocal;
        private float levelMinX;
        private float trackWidth;

        public virtual void OnValidate() {
            this.SetIfNull(ref image);
        }

        public void Initialize(PlayerElements playerElements, EntityRef targetEntity, Transform targetTransform, VersusStageData stage) {
            this.playerElements = playerElements;
            this.targetEntity = targetEntity;
            this.targetTransform = targetTransform;
            this.stage = stage;
            levelMinX = stage.StageWorldMin.X.AsFloat;
            trackWidth = trackMaxX - trackMinX;
            levelWidthReciprocal = 2f / stage.TileDimensions.x;
        }

        public virtual void LateUpdate() {
            float percentage = (targetTransform.position.x - levelMinX) * levelWidthReciprocal;
            transform.localPosition = new(percentage * trackWidth - trackMaxX, transform.localPosition.y, transform.localPosition.z);
        }
    }
}
