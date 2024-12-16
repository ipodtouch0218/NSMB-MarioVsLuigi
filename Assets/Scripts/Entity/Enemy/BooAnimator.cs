using NSMB.Extensions;
using Quantum;
using UnityEngine;

public unsafe class BooAnimator : QuantumEntityViewComponent {

    //---Static Variables
    private static readonly int ParamFacingRight = Animator.StringToHash("FacingRight");
    private static readonly int ParamScared = Animator.StringToHash("Scared");

    //---Serialized Variables
    [SerializeField] private Transform bobber;
    [SerializeField] private SpriteRenderer sRenderer;
    [SerializeField] private Animator animator;
    [SerializeField] private AudioSource sfx;
    [SerializeField] private float sinSpeed = 1f, sinAmplitude = 0.0875f;

    public void OnValidate() {
        this.SetIfNull(ref sRenderer, UnityExtensions.GetComponentType.Children);
        this.SetIfNull(ref animator, UnityExtensions.GetComponentType.Children);
        this.SetIfNull(ref sfx, UnityExtensions.GetComponentType.Children);
        if (!bobber) {
            bobber = sRenderer.transform;
        }
    }

    public void Start() {
        QuantumEvent.Subscribe<EventBooBecomeActive>(this, OnBooBecameActive, NetworkHandler.FilterOutReplayFastForward);
        QuantumEvent.Subscribe<EventPlayComboSound>(this, OnPlayComboSound, NetworkHandler.FilterOutReplayFastForward);
    }

    public override void OnUpdateView() {
        Frame f = PredictedFrame;
        float time = (f.Number + Game.InterpolationFactor) * f.DeltaTime.AsFloat;

        bobber.localPosition = new(0, Mathf.Sin(2 * Mathf.PI * time * sinSpeed) * sinAmplitude);

        var boo = f.Unsafe.GetPointer<Boo>(EntityRef);
        var enemy = f.Unsafe.GetPointer<Enemy>(EntityRef);

        animator.SetBool(ParamFacingRight, enemy->FacingRight);
        animator.SetBool(ParamScared, boo->UnscaredFrames > 0);
        sRenderer.enabled = enemy->IsActive;

        if (enemy->IsDead) {
            transform.rotation *= Quaternion.Euler(0, 0, 400f * (enemy->FacingRight ? -1 : 1) * Time.deltaTime);
        } else {
            transform.rotation = Quaternion.identity;
        }
    }

    private void OnBooBecameActive(EventBooBecomeActive e) {
        if (e.Entity != EntityRef) {
            return;
        }

        sfx.PlayOneShot(SoundEffect.Enemy_Boo_Laugh);
    }

    private void OnPlayComboSound(EventPlayComboSound e) {
        if (e.Entity != EntityRef) {
            return;
        }

        sfx.PlayOneShot(QuantumUtils.GetComboSoundEffect(e.Combo));
    }
}
