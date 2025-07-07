using NSMB.Utilities.Extensions;
using Quantum;
using UnityEngine;
using static NSMB.Utilities.QuantumViewUtils;

namespace NSMB.Entities.World {
    public unsafe class SpinnerAnimator : QuantumEntityViewComponent {

        //---Serialized Variables
        [SerializeField] private Transform rotator;
        [SerializeField] private AudioSource sfx;
        [SerializeField] private GameObject launchParticlePrefab;

        public void OnValidate() {
            this.SetIfNull(ref sfx);
        }

        public void Start() {
            QuantumEvent.Subscribe<EventMarioPlayerUsedSpinner>(this, OnMarioPlayerUsedSpinner, FilterOutReplayFastForward);
        }

        public override void OnUpdateView() {
            Frame f = PredictedFrame;
            Frame fp = PredictedPreviousFrame;

            if (!f.Unsafe.TryGetPointer(EntityRef, out Spinner* spinner)
                || !fp.Unsafe.TryGetPointer(EntityRef, out Spinner* spinnerPrev)) {
                return;
            }

            float rotation = Mathf.LerpAngle(spinnerPrev->Rotation.AsFloat, spinner->Rotation.AsFloat, Game.InterpolationFactor);
            rotator.localRotation = Quaternion.Euler(0, rotation, 0);
        }

        public void OnMarioPlayerUsedSpinner(EventMarioPlayerUsedSpinner e) {
            if (e.Spinner != EntityRef) {
                return;
            }

            sfx.PlayOneShot(SoundEffect.World_Spinner_Launch);
            Instantiate(launchParticlePrefab, transform.position, Quaternion.identity);
        }
    }
}
