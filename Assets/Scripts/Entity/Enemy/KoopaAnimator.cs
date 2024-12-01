using NSMB.Extensions;
using Quantum;
using Quantum.Profiling;
using UnityEngine;
using static NSMB.Extensions.UnityExtensions;

public class KoopaAnimator : QuantumEntityViewComponent {

    //---Static Variables
    private static readonly int ParamShell = Animator.StringToHash("shell");
    private static readonly int ParamXVel = Animator.StringToHash("xVel");
    private static readonly int ParamDead = Animator.StringToHash("dead");
    private static readonly int ParamTurnaround = Animator.StringToHash("turnaround");

    //---Serialized Variables
    [SerializeField] private Animator animator;
    [SerializeField] private AudioSource sfx;
    [SerializeField] private SpriteRenderer sRenderer;
    [SerializeField] private Transform rotation;
    [SerializeField] private bool mirrorSprite, dontFlip;

    //---Private Variables
    private float dampVelocity;
    private bool facingRight;

    public void OnValidate() {
        this.SetIfNull(ref animator, GetComponentType.Children);
        this.SetIfNull(ref sfx);
        this.SetIfNull(ref sRenderer, GetComponentType.Children);
    }

    public void Start() {
        QuantumEvent.Subscribe<EventPlayComboSound>(this, OnPlayComboSound, NetworkHandler.FilterOutReplayFastForward);
        QuantumEvent.Subscribe<EventPlayBumpSound>(this, OnPlayBumpSound, NetworkHandler.FilterOutReplayFastForward);
        QuantumEvent.Subscribe<EventKoopaKicked>(this, OnKoopaKicked, NetworkHandler.FilterOutReplayFastForward);
    }

    public override unsafe void OnUpdateView() {
        using var profilerScope = HostProfiler.Start("KoopaAnimator.OnUpdateView");
        Frame f = PredictedFrame;
        
        if (!f.Exists(EntityRef)) {
            return;
        }

        if (f.Global->GameState >= GameState.Ended) {
            animator.speed = 0;
            return;
        }

        var enemy = f.Unsafe.GetPointer<Enemy>(EntityRef);
        var koopa = f.Unsafe.GetPointer<Koopa>(EntityRef);
        var holdable = f.Unsafe.GetPointer<Holdable>(EntityRef);
        var physicsObject = f.Unsafe.GetPointer<PhysicsObject>(EntityRef);
        var freezable = f.Unsafe.GetPointer<Freezable>(EntityRef);

        // Animation
        bool isShell = koopa->IsInShell || f.Exists(holdable->Holder);
        animator.speed = freezable->IsFrozen(f) ? 0 : 1;
        animator.SetBool(ParamShell, isShell);
        animator.SetFloat(ParamXVel, (koopa->IsInShell && !koopa->IsKicked) ? 0 : Mathf.Abs(physicsObject->Velocity.X.AsFloat));
        animator.SetBool(ParamDead, enemy->IsDead);

        if (enemy->FacingRight != facingRight && !isShell && koopa->TurnaroundWaitFrames < 12) {
            animator.SetTrigger(ParamTurnaround);
        }
        facingRight = enemy->FacingRight;

        // "Flip" rotation
        float remainingWakeupTimer = koopa->IsKicked ? 0 : (koopa->WakeupFrames / 60f);
        if (enemy->IsDead) {
            sRenderer.transform.rotation *= Quaternion.Euler(0, 0, 400f * (enemy->FacingRight ? -1 : 1) * Time.deltaTime);

        } else if (koopa->IsInShell) {
            if (!freezable->IsFrozen(f)) {
                if (koopa->IsFlipped && !dontFlip) {
                    dampVelocity = Mathf.Min(dampVelocity + Time.deltaTime * 3, 1);
                    sRenderer.transform.eulerAngles = new Vector3(
                        rotation.eulerAngles.x,
                        rotation.eulerAngles.y,
                        Mathf.Lerp(rotation.eulerAngles.z, 180f, dampVelocity) + (remainingWakeupTimer < 3 && remainingWakeupTimer > 0 ? (Mathf.Sin(remainingWakeupTimer * 120f) * 15f) : 0));

                } else {
                    dampVelocity = 0;
                    sRenderer.transform.eulerAngles = new Vector3(
                        rotation.eulerAngles.x,
                        rotation.eulerAngles.y,
                        remainingWakeupTimer < 3 && remainingWakeupTimer > 0 ? (Mathf.Sin(remainingWakeupTimer * 120f) * 15f) : 0);
                }
            }
        } else {
            sRenderer.transform.rotation = Quaternion.identity;
        }

        sRenderer.enabled = enemy->IsActive;
        sRenderer.flipX = enemy->FacingRight ^ mirrorSprite;

        Vector3 modifiedZ = transform.position;
        if (f.Exists(holdable->Holder)) {
            modifiedZ.z = -4.1f;
        }
        transform.position = modifiedZ;
    }

    private void OnPlayBumpSound(EventPlayBumpSound e) {
        if (e.Entity != EntityRef) {
            return;
        }

        sfx.PlayOneShot(SoundEffect.World_Block_Bump);
    }

    private void OnPlayComboSound(EventPlayComboSound e) {
        if (e.Entity != EntityRef) {
            return;
        }

        sfx.PlayOneShot(QuantumUtils.GetComboSoundEffect(e.Combo));
    }

    private unsafe void OnKoopaKicked(EventKoopaKicked e) {
        if (e.Entity != EntityRef) {
            return;
        }

        Instantiate(
            Enums.PrefabParticle.Enemy_HardKick.GetGameObject(),
            transform.position + (Vector3.back * 5) + (Vector3.up * 0.1f),
            Quaternion.identity);
    }

    /*
    private void OnEnemyKilled(EventEnemyKilled e) {
        if (e.Enemy != EntityRef) {
            return;
        }
    }
    */
}