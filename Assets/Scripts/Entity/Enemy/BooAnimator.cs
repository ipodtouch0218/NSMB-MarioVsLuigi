using NSMB.Extensions;
using Quantum;
using UnityEngine;

public class BooAnimator : MonoBehaviour {

    //---Static Variables
    private static readonly int ParamFacingRight = Animator.StringToHash("FacingRight");
    private static readonly int ParamScared = Animator.StringToHash("Scared");

    //---Serialized Variables
    [SerializeField] private Transform bobber;
    [SerializeField] private SpriteRenderer sRenderer;
    [SerializeField] private QuantumEntityView entity;
    [SerializeField] private Animator animator;
    [SerializeField] private AudioSource sfx;
    [SerializeField] private float sinSpeed = 1f, sinAmplitude = 0.0875f;

    public void OnValidate() {
        this.SetIfNull(ref entity);
        this.SetIfNull(ref sRenderer, UnityExtensions.GetComponentType.Children);
        this.SetIfNull(ref animator, UnityExtensions.GetComponentType.Children);
        this.SetIfNull(ref sfx, UnityExtensions.GetComponentType.Children);
        if (!bobber) {
            bobber = sRenderer.transform;
        }
    }

    public void Start() {
        QuantumCallback.Subscribe<CallbackUpdateView>(this, OnUpdateView);
        QuantumEvent.Subscribe<EventBooBecomeActive>(this, OnBooBecameActive);
        QuantumEvent.Subscribe<EventPlayComboSound>(this, OnPlayComboSound);
    }

    public void OnUpdateView(CallbackUpdateView e) {
        QuantumGame game = e.Game;
        Frame f = game.Frames.Predicted;
        float time = (f.Number + game.InterpolationFactor) * f.DeltaTime.AsFloat;

        bobber.localPosition = new(0, Mathf.Sin(2 * Mathf.PI * time * sinSpeed) * sinAmplitude);

        var boo = f.Get<Boo>(entity.EntityRef);
        var enemy = f.Get<Enemy>(entity.EntityRef);

        animator.SetBool(ParamFacingRight, enemy.FacingRight);
        animator.SetBool(ParamScared, boo.UnscaredFrames > 0);
        sRenderer.enabled = enemy.IsActive;

        if (enemy.IsDead) {
            transform.rotation *= Quaternion.Euler(0, 0, 400f * (enemy.FacingRight ? -1 : 1) * Time.deltaTime);
        } else {
            transform.rotation = Quaternion.identity;
        }
    }

    private void OnBooBecameActive(EventBooBecomeActive e) {
        if (e.Entity != entity.EntityRef) {
            return;
        }

        sfx.PlayOneShot(SoundEffect.Enemy_Boo_Laugh);
    }

    private void OnPlayComboSound(EventPlayComboSound e) {
        if (e.Entity != entity.EntityRef) {
            return;
        }

        sfx.PlayOneShot(QuantumUtils.GetComboSoundEffect(e.Combo));
    }
}
