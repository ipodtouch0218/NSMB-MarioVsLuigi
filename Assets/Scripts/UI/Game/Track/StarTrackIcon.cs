using NSMB.Extensions;
using Quantum;
using UnityEngine;

namespace NSMB.UI.Game.Track {
    public unsafe class StarTrackIcon : TrackIcon {

        //---Static Variables
        private static readonly Vector3 ThreeFourths = new(0.75f, 0.75f, 1f);

        //---Serialized Variables
        [SerializeField] private Animator animator;

        public override void OnValidate() {
            base.OnValidate();
            this.SetIfNull(ref animator);
        }

        public void Start() {
            Frame f = QuantumRunner.DefaultGame.Frames.Predicted;
            var star = f.Unsafe.GetPointer<BigStar>(targetEntity);

            if (star->IsStationary) {
                animator.enabled = true;
                transform.localScale = Vector3.zero;
            } else {
                transform.localScale = ThreeFourths;
            }
        }
    }
}
