using NSMB.Extensions;
using Quantum;
using UnityEngine;

public class SpinnerAnimator : MonoBehaviour {

    //---Serialized Variables
    [SerializeField] private QuantumEntityView entity;
    [SerializeField] private Transform rotator;
    [SerializeField] private AudioSource sfx;

    public void OnValidate() {
        this.SetIfNull(ref sfx);
    }

    public void Start() {
        QuantumEvent.Subscribe<EventMarioPlayerUsedSpinner>(this, OnMarioPlayerUsedSpinner);
        QuantumCallback.Subscribe<CallbackUpdateView>(this, OnUpdateView);
    }

    public void OnUpdateView(CallbackUpdateView e) {
        QuantumGame game = e.Game;
        Frame f = game.Frames.Predicted;

        if (!f.Exists(entity.EntityRef)) {
            return;
        }

        var spinner = f.Get<Spinner>(entity.EntityRef);

        Frame fp = game.Frames.PredictedPrevious;
        var spinnerPrev = fp.Get<Spinner>(entity.EntityRef);

        float rotation = Mathf.LerpAngle(spinnerPrev.Rotation.AsFloat, spinner.Rotation.AsFloat, game.InterpolationFactor); 
        rotator.localRotation = Quaternion.Euler(0, rotation, 0);
    }

    public void OnMarioPlayerUsedSpinner(EventMarioPlayerUsedSpinner e) {
        if (e.Spinner != entity.EntityRef) {
            return;
        }

        // TODO: the particle effect that comes from a spinner launch

        sfx.PlayOneShot(SoundEffect.World_Spinner_Launch);
    }
}