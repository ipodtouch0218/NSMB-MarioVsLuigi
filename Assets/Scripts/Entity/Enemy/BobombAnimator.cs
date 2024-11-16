using NSMB.Extensions;
using Quantum;
using UnityEngine;

public unsafe class BobombAnimator : QuantumEntityViewComponent {

    //---Static Variables
    private static readonly int ParamLit = Animator.StringToHash("lit");
    private static readonly int ParamTurnaround = Animator.StringToHash("turnaround");
    private static readonly int ParamFlashAmount = Shader.PropertyToID("FlashAmount");

    //---Serialized Variables
    [SerializeField] private AudioSource sfx;
    [SerializeField] private SpriteRenderer sRenderer;
    [SerializeField] private Animator animator;
    [SerializeField] private GameObject explosionPrefab;

    //---Private Variables
    private MaterialPropertyBlock mpb;

    public void OnValidate() {
        this.SetIfNull(ref sfx);
        this.SetIfNull(ref sRenderer, UnityExtensions.GetComponentType.Children);
        this.SetIfNull(ref animator, UnityExtensions.GetComponentType.Children);
    }

    public void Start() {
        QuantumEvent.Subscribe<EventBobombExploded>(this, OnBobombExploded, NetworkHandler.FilterOutReplayFastForward);
        QuantumEvent.Subscribe<EventBobombLit>(this, OnBobombLit, NetworkHandler.FilterOutReplayFastForward);
        QuantumEvent.Subscribe<EventEntityBlockBumped>(this, OnEntityBlockBumped, NetworkHandler.FilterOutReplayFastForward);
        QuantumEvent.Subscribe<EventPlayComboSound>(this, OnPlayComboSound, NetworkHandler.FilterOutReplayFastForward);

        sRenderer.GetPropertyBlock(mpb = new());
    }

    public override void OnUpdateView() {
        Frame f = PredictedFrame;
        if (!f.Exists(EntityRef)) {
            return;
        }

        var freezable = f.Unsafe.GetPointer<Freezable>(EntityRef);
        if (freezable->IsFrozen(f)) {
            animator.speed = 0;
            sfx.Stop();
            return;
        } else {
            animator.speed = 1;
        }

        var bobomb = f.Unsafe.GetPointer<Bobomb>(EntityRef);
        var enemy = f.Unsafe.GetPointer<Enemy>(EntityRef);
        var holdable = f.Unsafe.GetPointer<Holdable>(EntityRef);

        bool lit = bobomb->CurrentDetonationFrames > 0;
        animator.SetBool(ParamLit, lit);

        if (!lit) {
            mpb.SetFloat(ParamFlashAmount, 0);
        } else {
            float detonationTimer = bobomb->CurrentDetonationFrames / 60f;
            float redOverlayPercent = 5.39f / (detonationTimer + 2.695f) * 10f % 1f;
            mpb.SetFloat(ParamFlashAmount, redOverlayPercent);
        }

        // Bodge...
        if (!enemy->IsAlive) {
            sfx.Stop();
        }

        sRenderer.SetPropertyBlock(mpb);
        sRenderer.enabled = enemy->IsActive;
        sRenderer.flipX = !enemy->FacingRight;

        Vector3 modifiedZ = transform.position;
        if (f.Exists(holdable->Holder)) {
            modifiedZ.z = -4.1f;
        }
        transform.position = modifiedZ;
        
        if (enemy->IsDead) {
            transform.rotation *= Quaternion.Euler(0, 0, 400f * (enemy->FacingRight ? -1 : 1) * Time.deltaTime);
        } else {
            transform.rotation = Quaternion.identity;
        }
    }

    private void OnPlayComboSound(EventPlayComboSound e) {
        if (e.Entity != EntityRef) {
            return;
        }

        sfx.PlayOneShot(QuantumUtils.GetComboSoundEffect(e.Combo));
    }

    private void OnEntityBlockBumped(EventEntityBlockBumped e) {
        if (e.Entity != EntityRef) {
            return;
        }

        sfx.PlayOneShot(SoundEffect.Enemy_Shell_Kick);
    }

    private void OnBobombExploded(EventBobombExploded e) {
        if (e.Entity != EntityRef) {
            return;
        }

        Instantiate(explosionPrefab, sRenderer.bounds.center, Quaternion.identity);
        sfx.Stop();
    }

    private void OnBobombLit(EventBobombLit e) {
        if (e.Entity != EntityRef) {
            return;
        }

        sfx.clip = SoundEffect.Enemy_Bobomb_Fuse.GetClip();
        sfx.Play();

        /*
        if (e.Stomped) {
            sfx.PlayOneShot(SoundEffect.Enemy_Generic_Stomp);
        }
        */
    }
}