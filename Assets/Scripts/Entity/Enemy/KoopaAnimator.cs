using NSMB.Extensions;
using Quantum;
using UnityEngine;
using static NSMB.Extensions.UnityExtensions;

public class KoopaAnimator : MonoBehaviour {

    //---Static Variables
    private static readonly int ParamShell = Animator.StringToHash("shell");
    private static readonly int ParamXVel = Animator.StringToHash("xVel");
    private static readonly int ParamDead = Animator.StringToHash("dead");

    //---Serialized Variables
    [SerializeField] private QuantumEntityView entity;
    [SerializeField] private Animator animator;
    [SerializeField] private AudioSource sfx;
    [SerializeField] private SpriteRenderer sRenderer;
    [SerializeField] private Transform rotation;

    [SerializeField] private bool mirrorSprite, dontFlip;

    //---Private Variables
    private float dampVelocity;

    public void OnValidate() {
        this.SetIfNull(ref animator, GetComponentType.Children);
        this.SetIfNull(ref entity);
        this.SetIfNull(ref sfx);
        this.SetIfNull(ref sRenderer, GetComponentType.Children);
    }

    public void Start() {
        QuantumCallback.Subscribe<CallbackUpdateView>(this, OnUpdateView);
        QuantumEvent.Subscribe<EventPlayComboSound>(this, OnPlayComboSound, NetworkHandler.FilterOutReplayFastForward);
        QuantumEvent.Subscribe<EventPlayBumpSound>(this, OnPlayBumpSound, NetworkHandler.FilterOutReplayFastForward);
        QuantumEvent.Subscribe<EventKoopaEnteredShell>(this, OnKoopaEnteredShell, NetworkHandler.FilterOutReplayFastForward);
        //QuantumEvent.Subscribe<EventEnemyKilled>(this, OnEnemyKilled, NetworkHandler.FilterOutReplayFastForward);
    }

    private unsafe void OnUpdateView(CallbackUpdateView e) {
        QuantumGame game = e.Game;
        Frame f = game.Frames.Predicted;

        if (!f.Exists(entity.EntityRef)) {
            return;
        }

        if (f.Global->GameState >= GameState.Ended) {
            animator.speed = 0;
            return;
        }

        var enemy = f.Unsafe.GetPointer<Enemy>(entity.EntityRef);
        var koopa = f.Unsafe.GetPointer<Koopa>(entity.EntityRef);
        var holdable = f.Unsafe.GetPointer<Holdable>(entity.EntityRef);
        var physicsObject = f.Unsafe.GetPointer<PhysicsObject>(entity.EntityRef);
        var freezable = f.Unsafe.GetPointer<Freezable>(entity.EntityRef);

        // Animation
        animator.speed = freezable->IsFrozen(f) ? 0 : 1;
        animator.SetBool(ParamShell, koopa->IsInShell || f.Exists(holdable->Holder));
        animator.SetFloat(ParamXVel, (koopa->IsInShell && !koopa->IsKicked) ? 0 : Mathf.Abs(physicsObject->Velocity.X.AsFloat));
        animator.SetBool(ParamDead, enemy->IsDead);

        // "Flip" rotation
        float remainingWakeupTimer = koopa->WakeupFrames / 60f;
        if (enemy->IsDead) {
            transform.rotation *= Quaternion.Euler(0, 0, 400f * (enemy->FacingRight ? -1 : 1) * Time.deltaTime);

        } else if (koopa->IsInShell && !koopa->IsKicked) {
            if (!freezable->IsFrozen(f)) {
                if (koopa->IsFlipped && !dontFlip) {
                    dampVelocity = Mathf.Min(dampVelocity + Time.deltaTime * 3, 1);
                    transform.eulerAngles = new Vector3(
                        rotation.eulerAngles.x,
                        rotation.eulerAngles.y,
                        Mathf.Lerp(rotation.eulerAngles.z, 180f, dampVelocity) + (remainingWakeupTimer < 3 && remainingWakeupTimer > 0 ? (Mathf.Sin(remainingWakeupTimer * 120f) * 15f) : 0));

                } else {
                    dampVelocity = 0;
                    transform.eulerAngles = new Vector3(
                        rotation.eulerAngles.x,
                        rotation.eulerAngles.y,
                        remainingWakeupTimer < 3 && remainingWakeupTimer > 0 ? (Mathf.Sin(remainingWakeupTimer * 120f) * 15f) : 0);
                }
            }
        } else {
            transform.rotation = Quaternion.identity;
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
        if (e.Entity != entity.EntityRef) {
            return;
        }

        sfx.PlayOneShot(SoundEffect.World_Block_Bump);
    }

    private void OnPlayComboSound(EventPlayComboSound e) {
        if (e.Entity != entity.EntityRef) {
            return;
        }

        sfx.PlayOneShot(QuantumUtils.GetComboSoundEffect(e.Combo));
    }

    private unsafe void OnKoopaEnteredShell(EventKoopaEnteredShell e) {
        if (e.Entity != entity.EntityRef) {
            return;
        }

        if (e.Groundpounded) {
            Instantiate(
                Enums.PrefabParticle.Enemy_HardKick.GetGameObject(),
                transform.position + (Vector3.back * 5) + (Vector3.up * 0.1f),
                Quaternion.identity);
        }
    }

    /*
    private void OnEnemyKilled(EventEnemyKilled e) {
        if (e.Enemy != entity.EntityRef) {
            return;
        }
    }
    */
}