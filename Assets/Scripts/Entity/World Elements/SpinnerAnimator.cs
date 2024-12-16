using NSMB.Extensions;
using Quantum;
using UnityEngine;

public unsafe class SpinnerAnimator : MonoBehaviour {

    //---Serialized Variables
    [SerializeField] private QuantumEntityView entity;
    [SerializeField] private Transform rotator;
    [SerializeField] private AudioSource sfx;
    [SerializeField] private GameObject launchParticlePrefab;

    public void OnValidate() {
        this.SetIfNull(ref sfx);
    }

    public void Start() {
        QuantumEvent.Subscribe<EventMarioPlayerUsedSpinner>(this, OnMarioPlayerUsedSpinner, NetworkHandler.FilterOutReplayFastForward);
        QuantumCallback.Subscribe<CallbackUpdateView>(this, OnUpdateView);
    }

    public void OnUpdateView(CallbackUpdateView e) {
        QuantumGame game = e.Game;
        Frame f = game.Frames.Predicted;
        Frame fp = game.Frames.PredictedPrevious;

        if (!f.Unsafe.TryGetPointer(entity.EntityRef, out Spinner* spinner)
            || !fp.Unsafe.TryGetPointer(entity.EntityRef, out Spinner* spinnerPrev)) {
            return;
        }

        float rotation = Mathf.LerpAngle(spinnerPrev->Rotation.AsFloat, spinner->Rotation.AsFloat, game.InterpolationFactor); 
        rotator.localRotation = Quaternion.Euler(0, rotation, 0);
    }

    public void OnMarioPlayerUsedSpinner(EventMarioPlayerUsedSpinner e) {
        if (e.Spinner != entity.EntityRef) {
            return;
        }

        sfx.PlayOneShot(SoundEffect.World_Spinner_Launch);
        Instantiate(launchParticlePrefab, transform.position, Quaternion.identity);
    }
}